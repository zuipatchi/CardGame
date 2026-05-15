using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Deck;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Main
{
    public sealed partial class MainPresenter : MonoBehaviour, IStartable
    {
        private const int InitialHandSize = 5;
        private const float DrawStagger = 0.12f;
        private const float CpuThinkSeconds = 0.8f;
        private const float CpuCardFlyDuration = 0.3f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private DeckModel _deckModel;
        private GameModel _gameModel;

        private HandView _handView;
        private HandView _opponentHandView;
        private FieldView _playerFieldView;
        private FieldView _opponentFieldView;
        private CharacterSlotView _playerCharacterSlot;
        private CharacterSlotView _opponentCharacterSlot;
        private DeckView _playerDeckView;
        private DeckView _opponentDeckView;
        private GraveyardView _playerGraveyardView;
        private GraveyardView _opponentGraveyardView;
        private VisualElement _actionButtonsArea;
        private Button _okButton;
        private Button _backButton;
        private Button _passButton;
        private VisualElement _turnOverlay;
        private Label _turnLabel;
        private VisualElement _resolveOverlay;
        private Label _resolveLabel;
        private VisualElement _playerAtkCounterOverlay;
        private Label _playerAtkCounterLabel;
        private Label _playerDamageFormulaLabel;
        private VisualElement _opponentAtkCounterOverlay;
        private Label _opponentAtkCounterLabel;
        private Label _opponentDamageFormulaLabel;
        private VisualElement _dragLayer;

        private readonly HashSet<CardView> _cpuCards = new HashSet<CardView>();
        private bool _isGameOver;

        private int _playerAtkBoost;
        private int _opponentAtkBoost;
        private int _playerDefBoost;
        private int _opponentDefBoost;

        // キャラセットフェーズの入力待ち（null=パス、card=スロットに置くカード）
        private UniTaskCompletionSource<CardView> _charSetInputTcs;
        private CardView _stagedCharSetCard;

        // 準備フェーズの入力待ち（null=パス、card=Ready するカード）
        private UniTaskCompletionSource<CardView> _prepInputTcs;
        private CardView _stagedPrepCard;

        // 戦闘前フェーズの入力待ち（null=パス、card=プレイしたカード）
        private UniTaskCompletionSource<CardView> _preBattleInputTcs;
        private CardView _stagedPreBattleCard;
        private bool _isLocalPreBattleActive;

        [Inject]
        public void Construct(CardStore cardStore, CardDatabase cardDatabase, DeckModel deckModel, GameModel gameModel)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _gameModel = gameModel;
        }

        void IStartable.Start()
        {
            BuildAsync().Forget();
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded;

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                VisualElement mainRoot = root.Q<VisualElement>("MainRoot");
                VisualElement deckArea = root.Q<VisualElement>("DeckArea");
                VisualElement graveyardArea = root.Q<VisualElement>("GraveyardArea");
                VisualElement opponentDeckArea = root.Q<VisualElement>("OpponentDeckArea");
                VisualElement opponentGraveyardArea = root.Q<VisualElement>("OpponentGraveyardArea");
                VisualElement opponentHandArea = root.Q<VisualElement>("OpponentHandArea");
                VisualElement handArea = root.Q<VisualElement>("HandArea");
                VisualElement opponentFieldArea = root.Q<VisualElement>("OpponentFieldArea");
                VisualElement playerFieldArea = root.Q<VisualElement>("PlayerFieldArea");
                VisualElement playerCharacterArea = root.Q<VisualElement>("PlayerCharacterArea");
                VisualElement opponentCharacterArea = root.Q<VisualElement>("OpponentCharacterArea");

                mainRoot.style.backgroundImage = new StyleBackground(_cardStore.BattleField);

                _dragLayer = new VisualElement();
                _dragLayer.AddToClassList("main-drag-layer");
                _dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(_dragLayer);

                CardData[] allCards = _cardDatabase.AllCards.ToArray();
                CardData[] playerDeckFull = _deckModel.Count > 0
                    ? _cardDatabase.BuildDeck(_deckModel.CardIds)
                    : allCards;
                int handSize = Mathf.Min(InitialHandSize, playerDeckFull.Length);

                CardData[] playerShuffled = Shuffle((CardData[])playerDeckFull.Clone());
                CardData[] playerHandCards = playerShuffled.Take(handSize).ToArray();
                CardData[] playerDeckCards = playerShuffled.Skip(handSize).ToArray();

                CardData[] cpuShuffled = Shuffle((CardData[])allCards.Clone());
                CardData[] cpuHandCards = cpuShuffled.Take(handSize).ToArray();
                CardData[] cpuDeckCards = cpuShuffled.Skip(handSize).ToArray();

                _playerCharacterSlot = new CharacterSlotView();
                _playerCharacterSlot.OnCardDisplaced += card => _playerGraveyardView.AddCard(card);
                playerCharacterArea.Add(_playerCharacterSlot);

                _opponentCharacterSlot = new CharacterSlotView();
                _opponentCharacterSlot.OnCardDisplaced += card => _opponentGraveyardView.AddCard(card);
                opponentCharacterArea.Add(_opponentCharacterSlot);

                _opponentFieldView = new FieldView();
                opponentFieldArea.Add(_opponentFieldView);

                _playerFieldView = new FieldView();
                playerFieldArea.Add(_playerFieldView);

                _playerAtkCounterOverlay = new VisualElement();
                _playerAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _playerAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _playerAtkCounterOverlay.style.display = DisplayStyle.None;
                _playerAtkCounterLabel = new Label("0");
                _playerAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _playerAtkCounterLabel.AddToClassList("atk-counter-label");
                _playerAtkCounterOverlay.Add(_playerAtkCounterLabel);
                _playerDamageFormulaLabel = new Label();
                _playerDamageFormulaLabel.pickingMode = PickingMode.Ignore;
                _playerDamageFormulaLabel.AddToClassList("atk-counter-damage-label");
                _playerAtkCounterOverlay.Add(_playerDamageFormulaLabel);
                _playerFieldView.Add(_playerAtkCounterOverlay);

                _opponentAtkCounterOverlay = new VisualElement();
                _opponentAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _opponentAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
                _opponentAtkCounterLabel = new Label("0");
                _opponentAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterLabel.AddToClassList("atk-counter-label");
                _opponentAtkCounterOverlay.Add(_opponentAtkCounterLabel);
                _opponentDamageFormulaLabel = new Label();
                _opponentDamageFormulaLabel.pickingMode = PickingMode.Ignore;
                _opponentDamageFormulaLabel.AddToClassList("atk-counter-damage-label");
                _opponentAtkCounterOverlay.Add(_opponentDamageFormulaLabel);
                _opponentFieldView.Add(_opponentAtkCounterOverlay);

                _opponentHandView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, faceDown: true, interactive: false);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer);
                handArea.Add(_handView);
                _handView.OnCardDropped = HandlePlayerCardDrop;

                _resolveOverlay = new VisualElement();
                _resolveOverlay.AddToClassList("resolve-overlay");
                _resolveOverlay.pickingMode = PickingMode.Ignore;
                _resolveOverlay.style.display = DisplayStyle.None;
                _resolveLabel = new Label("Resolve");
                _resolveLabel.pickingMode = PickingMode.Ignore;
                _resolveLabel.AddToClassList("resolve-label");
                _resolveOverlay.Add(_resolveLabel);
                mainRoot.Add(_resolveOverlay);

                _turnOverlay = new VisualElement();
                _turnOverlay.AddToClassList("turn-announcement-overlay");
                _turnOverlay.pickingMode = PickingMode.Ignore;
                _turnOverlay.style.display = DisplayStyle.None;
                _turnLabel = new Label();
                _turnLabel.pickingMode = PickingMode.Ignore;
                _turnLabel.AddToClassList("turn-announcement-label");
                _turnOverlay.Add(_turnLabel);
                mainRoot.Add(_turnOverlay);

                _actionButtonsArea = root.Q<VisualElement>("ActionButtonsArea");
                _okButton = root.Q<Button>("OkButton");
                _backButton = root.Q<Button>("BackButton");
                _passButton = root.Q<Button>("PassButton");
                _okButton.clicked += OnOkClicked;
                _backButton.clicked += OnBackClicked;
                _passButton.clicked += OnPassClicked;

                _playerDeckView = new DeckView(_cardStore.CardTemplate, playerDeckCards, _cardStore.CardBack);
                deckArea.Add(_playerDeckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, cpuDeckCards, _cardStore.CardBack);
                opponentDeckArea.Add(_opponentDeckView);

                _playerGraveyardView = new GraveyardView();
                graveyardArea.Add(_playerGraveyardView);

                _opponentGraveyardView = new GraveyardView();
                opponentGraveyardArea.Add(_opponentGraveyardView);

                CancellationToken ct = destroyCancellationToken;
                await UniTask.NextFrame(ct);

                Rect deckWorldRect = _playerDeckView.worldBound;
                Rect opponentDeckWorldRect = _opponentDeckView.worldBound;
                UniTask[] drawTasks = new UniTask[handSize * 2];
                for (int i = 0; i < handSize; i++)
                {
                    drawTasks[i] = _handView.AddCardAnimatedAsync(playerHandCards[i], deckWorldRect, i * DrawStagger, ct);
                    drawTasks[handSize + i] = _opponentHandView.AddCardAnimatedAsync(cpuHandCards[i], opponentDeckWorldRect, i * DrawStagger, ct);
                }
                await UniTask.WhenAll(drawTasks);

                foreach (CardView cpuCard in _opponentHandView.Cards)
                {
                    _cpuCards.Add(cpuCard);
                }

                RunGameAsync(ct).Forget();
            }
            catch (System.OperationCanceledException) { }
        }
    }
}

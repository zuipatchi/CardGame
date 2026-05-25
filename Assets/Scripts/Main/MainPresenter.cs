using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Deck;
using Common.GameSession;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using Main.Network;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Main
{
    public sealed partial class MainPresenter : MonoBehaviour, IStartable, ISceneReady
    {
        private const int InitialHandSize = 5;
        private const float DrawStagger = 0.12f;
        private const float CpuThinkSeconds = 0.8f;
        private const float CpuCardFlyDuration = 0.3f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private const float AttackWindupDuration = 0.15f;
        private const float AttackWindupDistance = 50f;
        private const float AttackFlyDuration = 0.65f;
        private const float AttackKnockbackDistance = 35f;
        private const float AttackKnockbackDuration = 0.15f;

        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private DeckModel _deckModel;
        private GameModel _gameModel;
        private SceneTransitioner _sceneTransitioner;
        private GameSessionModel _gameSessionModel;
        private NetworkGameService _networkGameService;

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
        private VisualElement _opponentAtkCounterOverlay;
        private Label _opponentAtkCounterLabel;
        private VisualElement _dragLayer;
        private Label _costWarningLabel;
        private VisualElement _gameEndOverlay;
        private Label _gameEndLabel;
        private Button _gameEndTitleButton;

        private CardDetailModal _cardDetailModal;
        private AttributeCompatibilityModal _attrCompatibilityModal;
        private readonly HashSet<CardView> _cpuCards = new HashSet<CardView>();
        private bool _isGameOver;

        private int _playerAtkBoost;
        private int _opponentAtkBoost;
        private int _playerDefBoost;
        private int _opponentDefBoost;

        private readonly StagedInput _charSetInput = new StagedInput();
        private readonly StagedInput _prepInput = new StagedInput();
        private readonly StagedInput _preBattleInput = new StagedInput();
        private bool _isLocalPreBattleActive;

        private sealed class StagedInput
        {
            internal UniTaskCompletionSource<CardView> Tcs;
            internal CardView Card;
        }

        private readonly UniTaskCompletionSource _readyTcs = new UniTaskCompletionSource();

        public UniTask ReadyAsync(CancellationToken ct) => _readyTcs.Task.AttachExternalCancellation(ct);

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            DeckModel deckModel,
            GameModel gameModel,
            SceneTransitioner sceneTransitioner,
            GameSessionModel gameSessionModel,
            NetworkGameService networkGameService)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _gameModel = gameModel;
            _sceneTransitioner = sceneTransitioner;
            _gameSessionModel = gameSessionModel;
            _networkGameService = networkGameService;
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
                _readyTcs.TrySetResult();

                _dragLayer = new VisualElement();
                _dragLayer.AddToClassList("main-drag-layer");
                _dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(_dragLayer);

                CardData[] allCards = _cardDatabase.AllCards.ToArray();
                bool isOnline = _gameSessionModel.HasSession;

                CardData[] playerDeckFull = null;
                CardData[] playerHandCards;
                CardData[] playerDeckCards;
                CardData[] cpuHandCards;
                CardData[] cpuDeckCards;
                int handSize;

                if (isOnline)
                {
                    IReadOnlyList<string> deckIds = _deckModel.Count > 0
                        ? _deckModel.CardIds
                        : allCards.Select(c => c.Id).ToList();
                    OnlineInitialState state = await _networkGameService.PrepareDecksAsync(deckIds, destroyCancellationToken);
                    handSize = state.LocalHand.Length;
                    playerHandCards = state.LocalHand;
                    playerDeckCards = state.LocalDeck;
                    CardData placeholder = allCards.Length > 0 ? allCards[0] : null;
                    cpuHandCards = new CardData[state.OpponentHandCount];
                    for (int i = 0; i < cpuHandCards.Length; i++) cpuHandCards[i] = placeholder;
                    cpuDeckCards = new CardData[state.OpponentDeckCount];
                    for (int i = 0; i < cpuDeckCards.Length; i++) cpuDeckCards[i] = placeholder;
                }
                else
                {
                    playerDeckFull = _deckModel.Count > 0
                        ? _cardDatabase.BuildDeck(_deckModel.CardIds)
                        : allCards;
                    handSize = Mathf.Min(InitialHandSize, playerDeckFull.Length);
                    CardData[] playerShuffled = Shuffle((CardData[])playerDeckFull.Clone());
                    playerHandCards = playerShuffled.Take(handSize).ToArray();
                    playerDeckCards = playerShuffled.Skip(handSize).ToArray();
                    CpuDeckSO cpuDeckSo = _cardStore.CpuDeck;
                    CardData[] cpuPool = cpuDeckSo != null && cpuDeckSo.CardIds.Count > 0
                        ? _cardDatabase.BuildDeck(cpuDeckSo.CardIds)
                        : allCards;
                    CardData[] cpuShuffled = Shuffle((CardData[])cpuPool.Clone());
                    cpuHandCards = cpuShuffled.Take(handSize).ToArray();
                    cpuDeckCards = cpuShuffled.Skip(handSize).ToArray();
                }

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
                VisualElement playerAtkIconWrapper = new VisualElement();
                playerAtkIconWrapper.AddToClassList("atk-counter-icon-wrapper");
                playerAtkIconWrapper.pickingMode = PickingMode.Ignore;
                VisualElement playerAtkIcon = new VisualElement();
                playerAtkIcon.AddToClassList("atk-counter-icon");
                playerAtkIcon.pickingMode = PickingMode.Ignore;
                playerAtkIconWrapper.Add(playerAtkIcon);
                _playerAtkCounterOverlay.Add(playerAtkIconWrapper);
                _playerAtkCounterLabel = new Label("0");
                _playerAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _playerAtkCounterLabel.AddToClassList("atk-counter-label");
                _playerAtkCounterOverlay.Add(_playerAtkCounterLabel);
                _playerFieldView.Add(_playerAtkCounterOverlay);

                _opponentAtkCounterOverlay = new VisualElement();
                _opponentAtkCounterOverlay.AddToClassList("atk-counter-overlay");
                _opponentAtkCounterOverlay.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterOverlay.style.display = DisplayStyle.None;
                VisualElement opponentAtkIconWrapper = new VisualElement();
                opponentAtkIconWrapper.AddToClassList("atk-counter-icon-wrapper");
                opponentAtkIconWrapper.pickingMode = PickingMode.Ignore;
                VisualElement opponentAtkIcon = new VisualElement();
                opponentAtkIcon.AddToClassList("atk-counter-icon");
                opponentAtkIcon.pickingMode = PickingMode.Ignore;
                opponentAtkIconWrapper.Add(opponentAtkIcon);
                _opponentAtkCounterOverlay.Add(opponentAtkIconWrapper);
                _opponentAtkCounterLabel = new Label("0");
                _opponentAtkCounterLabel.pickingMode = PickingMode.Ignore;
                _opponentAtkCounterLabel.AddToClassList("atk-counter-label");
                _opponentAtkCounterOverlay.Add(_opponentAtkCounterLabel);
                _opponentFieldView.Add(_opponentAtkCounterOverlay);

                _opponentHandView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, faceDown: true, interactive: false, attrIconDb: _cardStore.AttributeDatabase);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, attrIconDb: _cardStore.AttributeDatabase);
                handArea.Add(_handView);
                _handView.OnCardDropped = HandlePlayerCardDrop;

                _attrCompatibilityModal = new AttributeCompatibilityModal(mainRoot, _cardStore.AttributeDatabase);
                Button attrCompatibilityButton = new Button(() => _attrCompatibilityModal.Show());
                attrCompatibilityButton.AddToClassList("attr-compatibility-button");
                attrCompatibilityButton.text = "相性";
                mainRoot.Add(attrCompatibilityButton);

                _cardDetailModal = new CardDetailModal(mainRoot, _cardStore.AttributeDatabase);
                _handView.OnCardClicked = card => _cardDetailModal.Show(card.Data);
                _playerFieldView.OnCardClicked = card => _cardDetailModal.Show(card.Data);
                _opponentFieldView.OnCardClicked = card =>
                {
                    if (!card.IsFaceDown)
                    {
                        _cardDetailModal.Show(card.Data);
                    }
                };
                _playerCharacterSlot.OnCardClicked = card => _cardDetailModal.Show(card.Data);
                _opponentCharacterSlot.OnCardClicked = card =>
                {
                    if (!card.IsFaceDown)
                    {
                        _cardDetailModal.Show(card.Data);
                    }
                };

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

                _costWarningLabel = new Label("コストを払うとデッキが0枚になります");
                _costWarningLabel.AddToClassList("main-cost-warning-label");
                _costWarningLabel.pickingMode = PickingMode.Ignore;
                _costWarningLabel.style.display = DisplayStyle.None;
                mainRoot.Add(_costWarningLabel);

                _gameEndOverlay = new VisualElement();
                _gameEndOverlay.AddToClassList("game-end-overlay");
                _gameEndOverlay.style.display = DisplayStyle.None;
                _gameEndLabel = new Label();
                _gameEndLabel.AddToClassList("game-end-label");
                _gameEndLabel.pickingMode = PickingMode.Ignore;
                _gameEndOverlay.Add(_gameEndLabel);
                _gameEndTitleButton = new Button();
                _gameEndTitleButton.text = "タイトルに戻る";
                _gameEndTitleButton.AddToClassList("game-end-button");
                _gameEndTitleButton.style.opacity = 0f;
                _gameEndTitleButton.clicked += () => _sceneTransitioner.Transit(Scenes.Title).Forget();
                _gameEndOverlay.Add(_gameEndTitleButton);
                mainRoot.Add(_gameEndOverlay);

                _playerDeckView = new DeckView(_cardStore.CardTemplate, playerDeckCards, _cardStore.CardBack, _cardStore.AttributeDatabase);
                deckArea.Add(_playerDeckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, cpuDeckCards, _cardStore.CardBack, _cardStore.AttributeDatabase);
                opponentDeckArea.Add(_opponentDeckView);

                _playerGraveyardView = new GraveyardView(_cardStore.CardTemplate, mainRoot);
                _playerGraveyardView.OnCardClicked = data => _cardDetailModal.Show(data);
                graveyardArea.Add(_playerGraveyardView);

                _opponentGraveyardView = new GraveyardView(_cardStore.CardTemplate, mainRoot);
                _opponentGraveyardView.OnCardClicked = data => _cardDetailModal.Show(data);
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

                if (!isOnline)
                {
                    await RunMulliganIfNeededAsync(playerDeckFull, _handView, _playerDeckView, handSize, ct);
                    await RunMulliganIfNeededAsync(allCards, _opponentHandView, _opponentDeckView, handSize, ct);

                    foreach (CardView cpuCard in _opponentHandView.Cards)
                    {
                        _cpuCards.Add(cpuCard);
                    }

                    RunGameAsync(ct).Forget();
                }
            }
            catch (System.OperationCanceledException)
            {
                _readyTcs.TrySetCanceled();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BuildAsync 例外: {e}");
            }
        }

        private async UniTask RunMulliganIfNeededAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            IReadOnlyList<CardView> cards = hand.Cards;
            bool hasCharacter = false;
            foreach (CardView card in cards)
            {
                if (card.Data is CharacterCardData)
                {
                    hasCharacter = true;
                    break;
                }
            }

            if (hasCharacter)
            {
                return;
            }

            await PlayAnnouncementAsync("マリガン", "turn-announcement-label--mulligan", ct);
            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = Shuffle((CardData[])fullDeck.Clone());
            CardData[] newHandCards = reshuffled.Take(handSize).ToArray();
            CardData[] newDeckCards = reshuffled.Skip(handSize).ToArray();

            deck.Rebuild(newDeckCards);

            await UniTask.NextFrame(ct);

            Rect deckRect = deck.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = hand.AddCardAnimatedAsync(newHandCards[i], deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }
    }
}

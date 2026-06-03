using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Deck;
using Common.GameSession;
using Common.Option;
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
        private OptionPresenter _optionPresenter;
        private OptionModel _optionModel;

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
        private VisualElement _mainRoot;
        private VisualElement _dragLayer;
        private Label _costWarningLabel;
        private VisualElement _gameEndOverlay;
        private Label _gameEndLabel;
        private Label _gameEndSubLabel;
        private Button _gameEndTitleButton;
        private CancellationTokenSource _surrenderCts;

        [SerializeField] private GameObject _fireworkPrefab;
        [SerializeField] private GameObject _costEffectPrefab;
        [SerializeField] private GameObject _atkBoostEffectPrefab;
        [SerializeField] private GameObject _defBoostEffectPrefab;
        [SerializeField] private GameObject _drawEffectPrefab;
        [SerializeField] private GameObject _banishCharEffectPrefab;
        [SerializeField] private GameObject _negateEffectPrefab;
        [SerializeField] private GameObject _recoverEffectPrefab;
        [SerializeField] private GameObject _switchEffectPrefab;
        [SerializeField] private GameObject _charDestroyEffectPrefab;
        [SerializeField] private GameObject _charDamageEffectPrefab;
        [SerializeField] private GameObject _evolveEffectPrefab;
        [SerializeField] private GameObject _rainDefeatEffectPrefab;
        [SerializeField] private Shader _fireworkAdditiveUIShader;

        private VisualElement _phaseRowDraw;
        private VisualElement _phaseRowCharacterSet;
        private VisualElement _phaseRowPreBattle1;
        private VisualElement _phaseRowPreBattle2;
        private VisualElement _phaseRowBattle;

        private CardDetailModal _cardDetailModal;
        private bool _isGameOver;
        private bool _isOnline;
        private bool _mulliganChoicePending;
        private VisualElement _mulliganOverlay;
        private VisualElement _waitingOverlay;
        private VisualElement _toastContainer;
        private Label _toastLabel;
        private CancellationTokenSource _toastCts;

        private int _playerAtkBoost;
        private int _opponentAtkBoost;
        private int _playerDefBoost;
        private int _opponentDefBoost;

        private GameObject _rainDefeatEffect;

        private bool _onlineIsLocalFirst;
        private bool _localHasPriority;
        private VisualElement _playerPriorityCoin;
        private VisualElement _opponentPriorityCoin;

        private readonly StagedInput _charSetInput = new StagedInput();
        private readonly StagedInput _prepInput = new StagedInput();
        private readonly StagedInput _preBattleInput = new StagedInput();
        private readonly StagedInput _switchInput = new StagedInput();
        private readonly StagedInput _evolveInput = new StagedInput();
        private int _evolveMinCost;
        private bool _isLocalPreBattleActive;

        private sealed class StagedInput
        {
            internal UniTaskCompletionSource<CardView> _tcs;
            internal CardView _card;
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
            NetworkGameService networkGameService,
            OptionPresenter optionPresenter,
            OptionModel optionModel)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _gameModel = gameModel;
            _sceneTransitioner = sceneTransitioner;
            _gameSessionModel = gameSessionModel;
            _networkGameService = networkGameService;
            _optionPresenter = optionPresenter;
            _optionModel = optionModel;
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

                if (this == null)
                {
                    return;
                }

                CancellationToken destroyCt = destroyCancellationToken;

                _optionPresenter.SetSurrenderHandler(Surrender);

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                _mainRoot = root.Q<VisualElement>("MainRoot");
                VisualElement mainRoot = _mainRoot;
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

                SpawnBattleFieldBackground(_cardStore.BattleField);
                _readyTcs.TrySetResult();

                _waitingOverlay = new VisualElement();
                _waitingOverlay.AddToClassList("waiting-overlay");
                Label waitingLabel = new Label("対戦相手を待っています...");
                waitingLabel.AddToClassList("waiting-label");
                waitingLabel.pickingMode = PickingMode.Ignore;
                _waitingOverlay.Add(waitingLabel);
                mainRoot.Add(_waitingOverlay);

                _dragLayer = new VisualElement();
                _dragLayer.AddToClassList("main-drag-layer");
                _dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(_dragLayer);

                _toastContainer = new VisualElement();
                _toastContainer.AddToClassList("main-toast-container");
                _toastContainer.pickingMode = PickingMode.Ignore;
                _toastContainer.style.display = DisplayStyle.None;
                _toastLabel = new Label();
                _toastLabel.AddToClassList("main-toast-label");
                _toastLabel.pickingMode = PickingMode.Ignore;
                _toastContainer.Add(_toastLabel);
                mainRoot.Add(_toastContainer);

                CardData[] allCards = _cardDatabase.AllCards.ToArray();
                bool isOnline = _gameSessionModel.HasSession;
                _isOnline = isOnline;

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
                    using CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, destroyCt);
                    OnlineInitialState state;
                    try
                    {
                        state = await _networkGameService.PrepareDecksAsync(deckIds, linkedCts.Token);
                    }
                    catch (OperationCanceledException) when (!destroyCt.IsCancellationRequested)
                    {
                        _waitingOverlay.style.display = DisplayStyle.None;
                        ShowMatchTimeoutModal(mainRoot);
                        return;
                    }
                    _onlineIsLocalFirst = state.IsLocalFirst;
                    handSize = state.LocalHand.Length;
                    playerHandCards = state.LocalHand;
                    playerDeckCards = state.LocalDeck;
                    CardData placeholder = allCards.Length > 0 ? allCards[0] : null;
                    cpuHandCards = new CardData[state.OpponentHandCount];
                    for (int i = 0; i < cpuHandCards.Length; i++) cpuHandCards[i] = placeholder;
                    cpuDeckCards = new CardData[state.OpponentDeckCount];
                    for (int i = 0; i < cpuDeckCards.Length; i++) cpuDeckCards[i] = placeholder;
                    WatchForOpponentSurrenderAsync(destroyCt).Forget();
                }
                else
                {
                    playerDeckFull = _deckModel.Count > 0
                        ? _cardDatabase.BuildDeck(_deckModel.CardIds)
                        : allCards;
                    handSize = Mathf.Min(InitialHandSize, playerDeckFull.Length);
                    CardData[] playerShuffled = CardArrayUtils.Shuffle((CardData[])playerDeckFull.Clone());
                    playerHandCards = playerShuffled.Take(handSize).ToArray();
                    playerDeckCards = playerShuffled.Skip(handSize).ToArray();
                    CpuDeckSO cpuDeckSo = _cardStore.CpuDeck;
                    bool cpuDeckEmpty = cpuDeckSo == null || cpuDeckSo.CardIds.Count == 0;
                    if (cpuDeckEmpty)
                    {
                        Debug.LogError("CPUのデッキが空です");
                    }
                    CardData[] cpuPool = !cpuDeckEmpty
                        ? _cardDatabase.BuildDeck(cpuDeckSo.CardIds)
                        : allCards;
                    CardData[] cpuShuffled = CardArrayUtils.Shuffle((CardData[])cpuPool.Clone());
                    cpuHandCards = cpuShuffled.Take(handSize).ToArray();
                    cpuDeckCards = cpuShuffled.Skip(handSize).ToArray();
                }

                _playerCharacterSlot = new CharacterSlotView();
                _playerCharacterSlot.OnCardDisplaced += card => _playerGraveyardView.AddCard(card);
                playerCharacterArea.Add(_playerCharacterSlot);

                _opponentCharacterSlot = new CharacterSlotView();
                _opponentCharacterSlot.OnCardDisplaced += card => _opponentGraveyardView.AddCard(card);
                opponentCharacterArea.Add(_opponentCharacterSlot);

                _opponentFieldView = new FieldView(isOpponent: true);
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
                    _cardStore.CardBack, _dragLayer, faceDown: true, interactive: false, attrIconDb: _cardStore.AttributeDatabase, isOpponent: true);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, attrIconDb: _cardStore.AttributeDatabase);
                handArea.Add(_handView);
                _handView.OnCardDropped = HandlePlayerCardDrop;
                _handView.CanDrag = CanPlayerDragCard;
                _handView.OnCardAddedBack = card => card.SetPlayableHighlight(IsCardPlayable(card));

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

                VisualElement phaseIndicator = root.Q<VisualElement>("PhaseIndicator");
                _phaseRowDraw = phaseIndicator.Q<VisualElement>("PhaseRowDraw");
                _phaseRowCharacterSet = phaseIndicator.Q<VisualElement>("PhaseRowCharacterSet");
                _phaseRowPreBattle1 = phaseIndicator.Q<VisualElement>("PhaseRowPreBattle1");
                _phaseRowPreBattle2 = phaseIndicator.Q<VisualElement>("PhaseRowPreBattle2");
                _phaseRowBattle = phaseIndicator.Q<VisualElement>("PhaseRowBattle");

                _gameEndOverlay = new VisualElement();
                _gameEndOverlay.AddToClassList("game-end-overlay");
                _gameEndOverlay.style.display = DisplayStyle.None;
                _gameEndLabel = new Label();
                _gameEndLabel.AddToClassList("game-end-label");
                _gameEndLabel.pickingMode = PickingMode.Ignore;
                _gameEndOverlay.Add(_gameEndLabel);
                _gameEndSubLabel = new Label();
                _gameEndSubLabel.AddToClassList("game-end-sub-label");
                _gameEndSubLabel.pickingMode = PickingMode.Ignore;
                _gameEndSubLabel.style.display = DisplayStyle.None;
                _gameEndOverlay.Add(_gameEndSubLabel);
                _gameEndTitleButton = new Button();
                _gameEndTitleButton.text = "ホームに戻る";
                _gameEndTitleButton.AddToClassList("game-end-button");
                _gameEndTitleButton.style.opacity = 0f;
                _gameEndTitleButton.clicked += () => LeaveSessionAndGoHomeAsync().Forget();

                _gameEndOverlay.Add(_gameEndTitleButton);
                mainRoot.Add(_gameEndOverlay);

                _playerDeckView = new DeckView(_cardStore.CardTemplate, playerDeckCards, _cardStore.CardBack, _cardStore.AttributeDatabase);
                deckArea.Add(_playerDeckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, cpuDeckCards, _cardStore.CardBack, _cardStore.AttributeDatabase);
                opponentDeckArea.Add(_opponentDeckView);

                // プレイヤー墓地エリア：コインを左・墓地を右でrow配置
                graveyardArea.style.flexDirection = FlexDirection.Row;
                graveyardArea.style.alignItems = Align.Center;
                _playerPriorityCoin = new VisualElement();
                _playerPriorityCoin.AddToClassList("priority-coin");
                _playerPriorityCoin.pickingMode = PickingMode.Ignore;
                _playerPriorityCoin.style.marginRight = 8f;
                if (_cardStore.CoinFront != null)
                {
                    _playerPriorityCoin.style.backgroundImage = Background.FromSprite(_cardStore.CoinFront);
                }
                _playerPriorityCoin.style.display = DisplayStyle.None;
                graveyardArea.Add(_playerPriorityCoin);

                _playerGraveyardView = new GraveyardView(_cardStore.CardTemplate, mainRoot);
                _playerGraveyardView.OnCardClicked = data => _cardDetailModal.Show(data);
                graveyardArea.Add(_playerGraveyardView);

                // 相手墓地エリア：墓地を左・コインを右でrow配置
                opponentGraveyardArea.style.flexDirection = FlexDirection.Row;
                opponentGraveyardArea.style.alignItems = Align.Center;
                _opponentGraveyardView = new GraveyardView(_cardStore.CardTemplate, mainRoot);
                _opponentGraveyardView.OnCardClicked = data => _cardDetailModal.Show(data);
                opponentGraveyardArea.Add(_opponentGraveyardView);

                _opponentPriorityCoin = new VisualElement();
                _opponentPriorityCoin.AddToClassList("priority-coin");
                _opponentPriorityCoin.pickingMode = PickingMode.Ignore;
                _opponentPriorityCoin.style.marginLeft = 8f;
                if (_cardStore.CoinFront != null)
                {
                    _opponentPriorityCoin.style.backgroundImage = Background.FromSprite(_cardStore.CoinFront);
                }
                _opponentPriorityCoin.style.display = DisplayStyle.None;
                opponentGraveyardArea.Add(_opponentPriorityCoin);

                CancellationToken ct = destroyCt;
                await UniTask.NextFrame(ct);

                _waitingOverlay.style.display = DisplayStyle.None;

                Rect deckWorldRect = _playerDeckView.worldBound;
                Rect opponentDeckWorldRect = _opponentDeckView.worldBound;
                UniTask[] drawTasks = new UniTask[handSize * 2];
                for (int i = 0; i < handSize; i++)
                {
                    drawTasks[i] = _handView.AddCardAnimatedAsync(playerHandCards[i], deckWorldRect, i * DrawStagger, ct);
                    drawTasks[handSize + i] = _opponentHandView.AddCardAnimatedAsync(cpuHandCards[i], opponentDeckWorldRect, i * DrawStagger, ct);
                }
                await UniTask.WhenAll(drawTasks);

                if (isOnline)
                {
                    CardData[] onlinePlayerFull = playerHandCards.Concat(playerDeckCards).ToArray();
                    CardData opponentPlaceholder = allCards.Length > 0 ? allCards[0] : null;
                    UniTask<bool> waitOpponentMulligan = _networkGameService.WaitForOpponentMulliganDecisionAsync(ct);
                    bool localChose = await RunPlayerMulliganAsync(onlinePlayerFull, _handView, _playerDeckView, handSize, ct);
                    _networkGameService.SendMulliganDecision(localChose);
                    _waitingOverlay.style.display = DisplayStyle.Flex;
                    bool opponentChose = await waitOpponentMulligan;
                    _waitingOverlay.style.display = DisplayStyle.None;
                    if (opponentChose)
                    {
                        await RunOpponentMulliganAnimationAsync(opponentPlaceholder, handSize, ct);
                    }
                }
                else
                {
                    await RunPlayerMulliganAsync(playerDeckFull, _handView, _playerDeckView, handSize, ct);
                    await RunCpuMulliganIfNeededAsync(allCards, _opponentHandView, _opponentDeckView, handSize, ct);
                }

                await InitializePriorityAsync(ct);

                _surrenderCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCt);
                RunGameAsync(_surrenderCts.Token).Forget();
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

        private void SpawnBattleFieldBackground(Texture2D texture)
        {
            Camera cam = Camera.main;
            float dist = Mathf.Abs(cam.transform.position.z);
            float halfHeight = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
            float halfWidth = halfHeight * cam.aspect;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

            GameObject bgObj = new GameObject("BattleFieldBackground");
            SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;
            bgObj.transform.SetPositionAndRotation(
                new Vector3(0f, 1.5f, -0.5f),
                Quaternion.Euler(10f, 0f, 0f));

            Vector2 spriteSize = sprite.bounds.size;
            float scaleX = halfWidth * 2f / spriteSize.x;
            float scaleY = halfHeight * 2f / spriteSize.y;
            float scale = Mathf.Max(scaleX, scaleY);
            bgObj.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ShowMatchTimeoutModal(VisualElement root)
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("match-timeout-overlay");

            VisualElement panel = new VisualElement();
            panel.AddToClassList("match-timeout-panel");

            Label title = new Label("対戦が成立しませんでした");
            title.AddToClassList("match-timeout-title");
            panel.Add(title);

            Button closeButton = new Button();
            closeButton.text = "閉じる";
            closeButton.AddToClassList("match-timeout-close-button");
            closeButton.clicked += () => _sceneTransitioner.Transit(Scenes.Matching).Forget();
            panel.Add(closeButton);

            overlay.Add(panel);
            root.Add(overlay);
        }

        private async UniTask RunOpponentMulliganAnimationAsync(CardData placeholder, int handSize, CancellationToken ct)
        {
            await PlayReturnHandToDeckAsync(_opponentHandView, _opponentDeckView, ct);

            await UniTask.NextFrame(ct);

            Rect deckRect = _opponentDeckView.worldBound;
            UniTask[] drawTasks = new UniTask[handSize];
            for (int i = 0; i < handSize; i++)
            {
                drawTasks[i] = _opponentHandView.AddCardAnimatedAsync(placeholder, deckRect, i * DrawStagger, ct);
            }
            await UniTask.WhenAll(drawTasks);
        }

        private async UniTask<bool> RunPlayerMulliganAsync(
            CardData[] fullDeck, HandView hand, DeckView deck, int handSize, CancellationToken ct)
        {
            bool chose = await WaitForMulliganChoiceAsync(ct);
            if (!chose)
            {
                return false;
            }

            await PlayAnnouncementAsync("マリガン", "turn-announcement-label--mulligan", ct);
            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
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
            return true;
        }

        private async UniTask<bool> WaitForMulliganChoiceAsync(CancellationToken ct)
        {
            _mulliganChoicePending = true;

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("mulligan-overlay");

            Label label = new Label("マリガンしますか？");
            label.AddToClassList("mulligan-label");
            label.pickingMode = PickingMode.Ignore;
            overlay.Add(label);

            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("mulligan-button-row");

            Button yesButton = new Button();
            yesButton.AddToClassList("mulligan-button");
            yesButton.style.backgroundImage = new StyleBackground(_cardStore.YesButtonImage);

            Button noButton = new Button();
            noButton.AddToClassList("mulligan-button");
            noButton.style.backgroundImage = new StyleBackground(_cardStore.NoButtonImage);

            buttonRow.Add(yesButton);
            buttonRow.Add(noButton);
            overlay.Add(buttonRow);
            _mainRoot.Add(overlay);
            _mulliganOverlay = overlay;

            UniTaskCompletionSource<bool> tcs = new UniTaskCompletionSource<bool>();
            yesButton.clicked += () => tcs.TrySetResult(true);
            noButton.clicked += () => tcs.TrySetResult(false);
            ct.Register(() => tcs.TrySetCanceled());

            bool result;
            try
            {
                result = await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                overlay.RemoveFromHierarchy();
                _mulliganOverlay = null;
                _mulliganChoicePending = false;
                return false;
            }

            overlay.RemoveFromHierarchy();
            _mulliganOverlay = null;
            _mulliganChoicePending = false;
            return result;
        }

        private async UniTask RunCpuMulliganIfNeededAsync(
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

            await PlayReturnHandToDeckAsync(hand, deck, ct);

            CardData[] reshuffled = CardArrayUtils.Shuffle((CardData[])fullDeck.Clone());
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

        private async UniTaskVoid LeaveSessionAndGoHomeAsync()
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            await _sceneTransitioner.Transit(Scenes.Home);
        }

        private void Surrender()
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _surrenderCts?.Cancel();
            if (_isOnline)
            {
                _networkGameService.SendSurrenderNotification();
            }
            OnGameEnd(playerWins: false);
        }

        private async UniTaskVoid WatchForOpponentSurrenderAsync(CancellationToken ct)
        {
            try
            {
                await _networkGameService.WaitForOpponentSurrenderAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _surrenderCts?.Cancel();
            OnGameEnd(playerWins: true, isSurrenderWin: true);
        }

        private void OnDestroy()
        {
            if (_optionPresenter != null)
            {
                _optionPresenter.ClearSurrenderHandler();
            }
            _surrenderCts?.Dispose();
            if (_rainDefeatEffect != null)
            {
                Destroy(_rainDefeatEffect);
            }
        }

        private void UpdatePhaseIndicator(TurnPhase phase)
        {
            VisualElement[] rows =
            {
                _phaseRowDraw, _phaseRowCharacterSet,
                _phaseRowPreBattle1, _phaseRowPreBattle2,
                _phaseRowBattle,
            };
            TurnPhase[] phases =
            {
                TurnPhase.Draw, TurnPhase.CharacterSet,
                TurnPhase.PreBattle1, TurnPhase.PreBattle2,
                TurnPhase.Battle,
            };
            for (int i = 0; i < rows.Length; i++)
            {
                if (phases[i] == phase)
                {
                    rows[i].AddToClassList("phase-row--active");
                }
                else
                {
                    rows[i].RemoveFromClassList("phase-row--active");
                }
            }
        }
    }
}

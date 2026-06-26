using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Cpu;
using Common.Deck;
using Common.GameSession;
using Common.Option;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Tutorial;
using Common.Username;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using Main.Network;
using Main.Sound;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Main
{
    public sealed partial class MainPresenter : MonoBehaviour, IStartable, ISceneReady
    {
        private const float DrawStagger = 0.12f;
        private const float CpuThinkSeconds = 0.8f;
        private const float CpuCardFlyDuration = 0.3f;

        private const float AnimationShortDelay = 0.25f;

        // カード効果のパーティクル演出が終わってから、次の処理（適用・カード移動など）へ移るまでの共通の余韻ディレイ。
        private const float EffectTrailingDelaySeconds = 0.25f;

        private const float AttackWindupDuration = 0.15f;
        private const float AttackWindupDistance = 50f;
        private const float AttackFlyDuration = 0.25f;
        private const float AttackKnockbackDistance = 35f;
        private const float AttackKnockbackDuration = 0.15f;
        private const float AttackChargeReturnDuration = 0.30f;

        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private DeckModel _deckModel;
        private GameModel _gameModel;
        private SceneTransitioner _sceneTransitioner;
        private GameSessionModel _gameSessionModel;
        private CpuRosterStore _cpuRosterStore;
        private CpuBattleModel _cpuBattleModel;
        private NetworkGameService _networkGameService;
        private OptionPresenter _optionPresenter;
        private OptionModel _optionModel;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;
        private FlavorVoiceStore _flavorVoiceStore;

        private HandView _handView;
        private HandView _opponentHandView;
        private FieldView _playerFieldView;
        private FieldView _opponentFieldView;
        private DeckView _playerDeckView;
        private DeckView _opponentDeckView;
        private GraveyardView _playerGraveyardView;
        private GraveyardView _opponentGraveyardView;
        private VictoryPointsView _playerVictoryPoints;    // 自分の勝利点（自分フィールド左下・常時表示）
        private VictoryPointsView _opponentVictoryPoints;  // 相手の勝利点（相手フィールド右上・常時表示）
        private TurnCounterView _turnCounter;              // 経過ターン表示（画面左下・自分の勝利点の上・常時表示）
        private TurnTimerView _turnTimerView;              // 自分の手番の残り時間表示（メインフェーズ中のみ表示）
        private bool _turnTimedOut;                        // このターンが時間切れになったか（時間切れ→自動パス用）
        private VisualElement _actionButtonsArea;
        private Button _okButton;
        private Button _backButton;
        private Button _passButton;
        private Button _endButton;
        private VisualElement _turnOverlay;
        private Label _turnLabel;
        private VisualElement _resolveOverlay;
        private Label _resolveLabel;
        private VisualElement _mainRoot;
        private VisualElement _dragLayer;
        private Label _costWarningLabel;
        private VisualElement _gameEndOverlay;
        private VisualElement _gameEndEmblem;
        private Label _gameEndLabel;
        private Label _gameEndSubLabel;
        private VisualElement _gameEndButtonRow;
        private Button _gameEndRematchButton;
        private Button _gameEndTitleButton;
        private Label _gameEndRematchStatusLabel;
        private CancellationTokenSource _surrenderCts;

        // 再戦ハンドシェイク状態
        private bool _localRematchRequested;
        private bool _opponentRematchRequested;
        private bool _rematchStarted;
        private bool _opponentLeft;

        [SerializeField] private GameObject _fireworkPrefab;
        [SerializeField] private GameObject _costEffectPrefab;
        [SerializeField] private GameObject _drawEffectPrefab;
        [SerializeField] private GameObject _banishCharEffectPrefab;
        [SerializeField] private GameObject _recoverEffectPrefab;
        [SerializeField] private GameObject _switchEffectPrefab;
        [SerializeField] private GameObject _charDestroyEffectPrefab;
        [SerializeField] private GameObject _hitEffectPrefab;
        [SerializeField] private GameObject _deadlyEffectPrefab;
        [SerializeField] private GameObject _evolveEffectPrefab;
        [SerializeField] private GameObject _bounceEffectPrefab;
        [SerializeField] private GameObject _bounceAllEffectPrefab;
        [SerializeField] private GameObject _areaDamageEffectPrefab;
        [SerializeField] private GameObject _rainDefeatEffectPrefab;
        [SerializeField] private Shader _fireworkAdditiveUIShader;

        private CardDetailModal _cardDetailModal;
        private bool _isGameOver;
        private bool _isOnline;
        // チュートリアル（誘導つきスクリプト対戦）モードか。Construct で TutorialModel から消費して確定する。
        private bool _isTutorial;
        // どのチュートリアルか（_isTutorial が true のときのみ有効）。
        private TutorialId _tutorialId;
        private bool _mulliganChoicePending;
        private VisualElement _mulliganOverlay;
        private VisualElement _waitingOverlay;
        private VisualElement _toastContainer;
        private Label _toastLabel;
        private CancellationTokenSource _toastCts;

        private GameObject _rainDefeatEffect;

        private bool _onlineIsLocalFirst;
        private UniTask _preDrawReceiveTask;
        private bool _hasPreDrawTask;
        private UniTask<NetworkGameService.MainActionData> _preMainActionReceiveTask;
        private bool _hasPreMainActionTask;
        private VisualElement _playerPriorityCoin;
        private VisualElement _opponentPriorityCoin;

        private readonly StagedInput _switchInput = new StagedInput();
        private readonly StagedInput _evolveInput = new StagedInput();
        private int _evolveMinCost;
        private int _switchTargetCost;

        private UniTaskCompletionSource<CardView> _fieldCharSelectionTcs;
        // SummonFromDeckByKeyword のデッキカード選択：選んだカードのデッキ内インデックスを返す
        private UniTaskCompletionSource<int> _deckCardSelectionTcs;
        private UniTaskCompletionSource<List<CardView>> _enemyCharSelectionTcs;
        private List<CardView> _enemyCharSelected;
        private int _enemyCharSelectTarget;
        // 敵キャラ選択中のトースト文言（DamageEnemy / Bounce などで共用）
        private string _enemyCharSelectPrompt;
        // 自キャラの複数選択（AtkBoost / HpBoost）。敵キャラ選択（_enemyCharSelect*）の自フィールド版
        private UniTaskCompletionSource<List<CardView>> _allyCharSelectionTcs;
        private List<CardView> _allyCharSelected;
        private int _allyCharSelectTarget;
        private string _allyCharSelectPrompt;
        private UniTaskCompletionSource<MainPhaseAction> _mainActionTcs;
        private CardView _mainStagedCard;
        private MainPhaseActionType _mainStagedType;

        // このターンすでに攻撃したキャラ（1キャラ1回まで）。各メインフェーズ開始時にクリア
        private readonly HashSet<CardView> _attackedThisTurn = new HashSet<CardView>();

        // ─── 行動予約（解決アニメ中に次の攻撃・ターン終了を予約） ───────────
        // 解決アニメ中にドラッグした攻撃を貯めるキュー。各解決の後に先頭から消化する。
        private readonly Queue<MainPhaseAction> _queuedAttacks = new Queue<MainPhaseAction>();
        // ターン終了（パス）の予約。解決アニメ中に End を押すと立ち、キュー消化後にターンを終える。
        private bool _endTurnQueued;
        // ローカルメインフェーズ中ずっと常駐する攻撃矢印マニピュレータ（解決アニメ中も生かして予約を受け付ける）。
        private readonly List<(CardView card, AttackArrowManipulator manip)> _attackManipulators = new List<(CardView, AttackArrowManipulator)>();
        // 現在ハイライト中の攻撃対象キャラ（クリーンアップ用）。
        private List<CardView> _highlightedAttackTargets = new List<CardView>();
        // 召喚酔いしていない（自メインフェーズ開始時から場にいる）キャラ。場に新規登場したキャラは含まれず攻撃不可
        private readonly HashSet<CardView> _playerSeasonedChars = new HashSet<CardView>();
        private readonly HashSet<CardView> _opponentSeasonedChars = new HashSet<CardView>();

        // NextCardCostFree 効果: 次にプレイするカード1枚のコストを0にする（使うまで持続）
        private bool _playerNextCardFree;
        private bool _opponentNextCardFree;

        // ExtraTurn 効果: アクティブプレイヤーがこのターン中に発動すると true。
        // ターン終了時に消費し、相手へターンを渡さず同じプレイヤーがもう一度ターンを行う。
        private bool _extraTurnPending;

        // 各プレイヤーの初手（このゲームで最初に迎えた自分のドローフェーズ）はドローなし。
        // 先攻有利の補正として、先攻・後攻の双方の初手をドローなしにする。各プレイヤーの最初の
        // ドローフェーズで true にして消費するため、ExtraTurn で通算ターン番号がずれても各自の初手だけが対象になる。
        private bool _playerHadFirstTurn;
        private bool _opponentHadFirstTurn;

        // DrawSkipNext 効果: 発動側の次のドローフェーズを1回スキップする。
        // 発動時に立て、そのプレイヤーの次の RunDrawPhaseAsync でドロー0枚にして消費する。
        private bool _playerSkipNextDraw;
        private bool _opponentSkipNextDraw;

        // オーバーリミット：デッキが初めて0枚になった瞬間に1回だけ「オーバーリミット！」を告知する。
        // デッキが0枚の間は true。Recover 等で再び1枚以上になれば false に戻り、次に0枚へ落ちたとき再度告知する。
        private bool _playerOverLimit;
        private bool _opponentOverLimit;

        // DrawNextTurnStart 効果: 発動側の次のターン開始時に追加でドローする予約枚数（累積）。
        // 発動時に加算し、そのプレイヤーの次の RunDrawPhaseAsync で通常ドローに上乗せして消費する。
        private int _playerPendingNextDraw;
        private int _opponentPendingNextDraw;

        // EventCardTrigger.OnTurnStart の永続イベント登録簿。プレイ（PlayEvent）された OnTurnStart イベントのみを
        // 登録し、自分のターン開始時に毎ターン発動する。コストとして捨てたカードは含めない（墓地を走査しない理由）
        private readonly List<EventCardData> _playerTurnStartEvents = new List<EventCardData>();
        private readonly List<EventCardData> _opponentTurnStartEvents = new List<EventCardData>();

        private UniTaskCompletionSource _costSelectionTcs;
        private readonly List<CardView> _selectedCostCards = new List<CardView>();
        private int _requiredCost;

        private enum MainPhaseActionType { None, PlaceChar, PlayEvent, Attack, Pass }

        private struct MainPhaseAction
        {
            internal MainPhaseActionType _actionType;
            internal CardView _card;
            internal CardView _attacker;
            internal CardView _target;
            internal bool _targetsDeck;
        }

        private sealed class StagedInput
        {
            internal UniTaskCompletionSource<CardView> _tcs;
            internal CardView _card;
        }

        private string _localUsername;
        private string _opponentUsername;
        // この対戦の CPU 難易度（相手選択時に CpuRoster から取得。チュートリアル・オンラインでは初級扱い）。
        private CpuDifficulty _cpuDifficulty = CpuDifficulty.Beginner;

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
            CpuRosterStore cpuRosterStore,
            CpuBattleModel cpuBattleModel,
            NetworkGameService networkGameService,
            OptionPresenter optionPresenter,
            OptionModel optionModel,
            SoundPlayer soundPlayer,
            SoundStore soundStore,
            FlavorVoiceStore flavorVoiceStore,
            TutorialModel tutorialModel,
            UsernameRepository usernameRepository)
        {
            // チュートリアルフラグは消費型：起動時に読み取って即 false に戻し、
            // 中断しても次の通常 CPU 戦・オンライン戦に持ち越さないようにする。
            _isTutorial = tutorialModel.IsActive;
            _tutorialId = tutorialModel.Id;
            tutorialModel.IsActive = false;
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _gameModel = gameModel;
            _sceneTransitioner = sceneTransitioner;
            _gameSessionModel = gameSessionModel;
            _cpuRosterStore = cpuRosterStore;
            _cpuBattleModel = cpuBattleModel;
            _networkGameService = networkGameService;
            _optionPresenter = optionPresenter;
            _optionModel = optionModel;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
            _flavorVoiceStore = flavorVoiceStore;
            _localUsername = usernameRepository.Load() ?? string.Empty;
        }

        void IStartable.Start()
        {
            BuildAsync().Forget();
        }

        // 通常 CPU 戦の相手デッキ（カード配列）を組む。
        // ロスターの相手にカードIDが設定されていればそれを使い、未設定（プレースホルダー）なら
        // 既存の CpuDeck.asset にフォールバックする。どちらも空なら全カードを使う。
        private CardData[] BuildCpuPool(CpuOpponentData opponent, CardData[] allCards)
        {
            if (opponent != null && opponent.CardIds != null && opponent.CardIds.Count > 0)
            {
                return _cardDatabase.BuildDeck(opponent.CardIds);
            }
            CpuDeckSO cpuDeckSo = _cardStore.CpuDeck;
            bool cpuDeckEmpty = cpuDeckSo == null || cpuDeckSo.CardIds.Count == 0;
            if (cpuDeckEmpty)
            {
                Debug.LogError("CPUのデッキが空です");
                return allCards;
            }
            return _cardDatabase.BuildDeck(cpuDeckSo.CardIds);
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded;
                await _cpuRosterStore.Loaded;

                if (this == null)
                {
                    return;
                }

                CancellationToken destroyCt = destroyCancellationToken;

                // チュートリアルは勝敗のない練習なので「降参」ではなく「ホームに戻る」を出す。
                if (_isTutorial)
                {
                    _optionPresenter.SetBackToHomeHandler(GoHomeFromTutorial);
                }
                else
                {
                    _optionPresenter.SetSurrenderHandler(Surrender);
                }

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

                SpawnBattleFieldBackground(_cardStore.BattleField);
                _readyTcs.TrySetResult();

                _waitingOverlay = new VisualElement();
                _waitingOverlay.AddToClassList("waiting-overlay");
                // 背景部分のクリックは下の手札・場のカードへ透過させ、相手待ちの間もカード詳細を開けるようにする
                _waitingOverlay.pickingMode = PickingMode.Ignore;
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
                // チュートリアルは必ずオフライン（固定デッキのスクリプト対戦）。
                bool isOnline = _gameSessionModel.HasSession && !_isTutorial;
                _isOnline = isOnline;

                CardData[] playerDeckFull = null;
                CardData[] playerHandCards;
                CardData[] playerDeckCards;
                CardData[] cpuHandCards;
                CardData[] cpuDeckCards;
                int playerHandSize;
                int cpuHandSize;
                bool isLocalFirst;

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
                    isLocalFirst = state.IsLocalFirst;
                    // 先攻/後攻に応じた手札枚数はホストが配り済み。配列長・OpponentHandCount で双方に伝わる。
                    playerHandSize = state.LocalHand.Length;
                    cpuHandSize = state.OpponentHandCount;
                    playerHandCards = state.LocalHand;
                    playerDeckCards = state.LocalDeck;
                    CardData placeholder = allCards.Length > 0 ? allCards[0] : null;
                    cpuHandCards = new CardData[cpuHandSize];
                    for (int i = 0; i < cpuHandCards.Length; i++) cpuHandCards[i] = placeholder;
                    cpuDeckCards = state.OpponentDeck;
                    _opponentUsername = string.IsNullOrEmpty(state.OpponentUsername) ? "ゲスト" : state.OpponentUsername;
                    WatchForOpponentSurrenderAsync(destroyCt).Forget();
                    WatchForOpponentSpecialWinAsync(destroyCt).Forget();
                }
                else if (_isTutorial)
                {
                    _opponentUsername = "CPU";
                    // チュートリアルは決定的セットアップ：先攻プレイヤー固定・固定デッキ・シャッフルなし。
                    isLocalFirst = true;
                    CardData[] playerOrdered = _cardDatabase.BuildDeck(TutorialPlayerDeckIds());
                    playerDeckFull = playerOrdered;
                    playerHandSize = Mathf.Min(MulliganRule.InitialHandSize(true), playerOrdered.Length);
                    playerHandCards = playerOrdered.Take(playerHandSize).ToArray();
                    playerDeckCards = playerOrdered.Skip(playerHandSize).ToArray();

                    CardData[] cpuOrdered = _cardDatabase.BuildDeck(TutorialCpuDeckIds());
                    cpuHandSize = Mathf.Min(MulliganRule.InitialHandSize(false), cpuOrdered.Length);
                    cpuHandCards = cpuOrdered.Take(cpuHandSize).ToArray();
                    cpuDeckCards = cpuOrdered.Skip(cpuHandSize).ToArray();
                }
                else
                {
                    // 通常 CPU 戦：相手は Home の相手選択で選ばれた CpuRoster の相手。
                    // 再戦（Main 再ロード）でも CpuBattleModel.OpponentIndex が残るため同じ相手になる。
                    int opponentIndex = _cpuBattleModel.OpponentIndex;
                    CpuOpponentData opponent = _cpuRosterStore.GetOpponent(opponentIndex);
                    _opponentUsername = _cpuRosterStore.DisplayName(opponentIndex);
                    _cpuDifficulty = opponent != null ? opponent.Difficulty : CpuDifficulty.Beginner;
                    // 先攻後攻を配牌前に決める（手札枚数が先攻3枚・後攻5枚で変わるため）。
                    isLocalFirst = UnityEngine.Random.value > 0.5f;
                    playerDeckFull = _deckModel.Count > 0
                        ? _cardDatabase.BuildDeck(_deckModel.CardIds)
                        : allCards;
                    playerHandSize = Mathf.Min(MulliganRule.InitialHandSize(isLocalFirst), playerDeckFull.Length);
                    CardData[] playerShuffled = CardArrayUtils.Shuffle((CardData[])playerDeckFull.Clone());
                    playerHandCards = playerShuffled.Take(playerHandSize).ToArray();
                    playerDeckCards = playerShuffled.Skip(playerHandSize).ToArray();
                    CardData[] cpuPool = BuildCpuPool(opponent, allCards);
                    cpuHandSize = Mathf.Min(MulliganRule.InitialHandSize(!isLocalFirst), cpuPool.Length);
                    CardData[] cpuShuffled = CardArrayUtils.Shuffle((CardData[])cpuPool.Clone());
                    cpuHandCards = cpuShuffled.Take(cpuHandSize).ToArray();
                    cpuDeckCards = cpuShuffled.Skip(cpuHandSize).ToArray();
                }

                _opponentFieldView = new FieldView(isOpponent: true);
                opponentFieldArea.Add(_opponentFieldView);

                _playerFieldView = new FieldView();
                playerFieldArea.Add(_playerFieldView);

                // 勝利点はゲーム共通の勝利条件のため、開始時から両者を常時表示する
                _opponentVictoryPoints = new VictoryPointsView(isOpponent: true);
                opponentFieldArea.Add(_opponentVictoryPoints);

                _playerVictoryPoints = new VictoryPointsView(isOpponent: false);
                playerFieldArea.Add(_playerVictoryPoints);

                // 経過ターン表示は両者共通の情報のため、画面左下（自分の勝利点の上）に常時表示する
                _turnCounter = new TurnCounterView();
                _turnCounter.SetTurn(_gameModel.TurnNumber);
                mainRoot.Add(_turnCounter);

                // 自分の手番の残り時間表示（ターン表示の上）。メインフェーズ中のみ表示する
                _turnTimerView = new TurnTimerView();
                mainRoot.Add(_turnTimerView);

                _opponentHandView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer, faceDown: true, interactive: false, isOpponent: true);
                opponentHandArea.Add(_opponentHandView);

                _handView = new HandView(
                    _cardStore.CardTemplate, new CardData[0],
                    _cardStore.CardBack, _dragLayer);
                handArea.Add(_handView);
                _handView.OnCardDropped = HandlePlayerCardDrop;
                _handView.CanDrag = CanPlayerDragCard;

                _cardDetailModal = new CardDetailModal(mainRoot);
                _cardDetailModal.OnHidden = () =>
                {
                    if (_isTutorial)
                    {
                        TutorialOnLocalCardDetailClosed();
                    }
                };
                _handView.OnCardClicked = card =>
                {
                    if (_costSelectionTcs != null)
                    {
                        HandleCostCardClick(card);
                        return;
                    }
                    if (IsStagingCard())
                    {
                        return;
                    }
                    _cardDetailModal.Show(card.Data);
                    if (_isTutorial)
                    {
                        TutorialOnLocalCardDetailOpened();
                    }
                };
                _playerFieldView.OnCardClicked = card =>
                {
                    if (_allyCharSelectionTcs != null)
                    {
                        HandleAllyCharSelectionClick(card);
                        return;
                    }
                    if (_fieldCharSelectionTcs != null)
                    {
                        _fieldCharSelectionTcs.TrySetResult(card);
                        return;
                    }
                    _cardDetailModal.Show(card.Data);
                };
                _opponentFieldView.OnCardClicked = card =>
                {
                    if (_enemyCharSelectionTcs != null)
                    {
                        HandleEnemyCharSelectionClick(card);
                        return;
                    }
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
                // コンテナ自体はポインタを拾わせない（透明な余白がカードのドラッグ＝攻撃矢印を
                // 横取りするのを防ぐ）。子のボタンは PickingMode.Position のままなのでクリックは効く。
                _actionButtonsArea.pickingMode = PickingMode.Ignore;
                _okButton = root.Q<Button>("OkButton");
                _backButton = root.Q<Button>("BackButton");
                _passButton = root.Q<Button>("PassButton");
                _endButton = root.Q<Button>("EndButton");
                _okButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Enter3SE);
                    OnOkClicked();
                };
                _backButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                    OnBackClicked();
                };
                _passButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                    OnPassClicked();
                };
                _endButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Enter3SE);
                    OnEndTurnClicked();
                };

                _costWarningLabel = new Label("手札が足りません");
                _costWarningLabel.AddToClassList("main-cost-warning-label");
                _costWarningLabel.pickingMode = PickingMode.Ignore;
                _costWarningLabel.style.display = DisplayStyle.None;
                mainRoot.Add(_costWarningLabel);

                _gameEndOverlay = new VisualElement();
                _gameEndOverlay.AddToClassList("game-end-overlay");
                _gameEndOverlay.style.display = DisplayStyle.None;
                // 暗幕自身はクリックを透過させ、勝敗演出後も下のフィールドカードをクリックして
                // カード詳細を開けるようにする（子のボタン行は Position のままクリック可能）
                _gameEndOverlay.pickingMode = PickingMode.Ignore;
                _gameEndEmblem = new VisualElement();
                _gameEndEmblem.AddToClassList("game-end-emblem");
                _gameEndEmblem.pickingMode = PickingMode.Ignore;
                _gameEndEmblem.style.display = DisplayStyle.None;
                _gameEndOverlay.Add(_gameEndEmblem);
                _gameEndLabel = new Label();
                _gameEndLabel.AddToClassList("game-end-label");
                _gameEndLabel.pickingMode = PickingMode.Ignore;
                _gameEndOverlay.Add(_gameEndLabel);
                _gameEndSubLabel = new Label();
                _gameEndSubLabel.AddToClassList("game-end-sub-label");
                _gameEndSubLabel.pickingMode = PickingMode.Ignore;
                _gameEndSubLabel.style.display = DisplayStyle.None;
                _gameEndOverlay.Add(_gameEndSubLabel);

                _gameEndRematchStatusLabel = new Label();
                _gameEndRematchStatusLabel.AddToClassList("game-end-sub-label");
                _gameEndRematchStatusLabel.pickingMode = PickingMode.Ignore;
                _gameEndRematchStatusLabel.style.display = DisplayStyle.None;
                _gameEndOverlay.Add(_gameEndRematchStatusLabel);

                _gameEndButtonRow = new VisualElement();
                _gameEndButtonRow.AddToClassList("game-end-button-row");
                _gameEndButtonRow.style.opacity = 0f;

                _gameEndRematchButton = new Button();
                _gameEndRematchButton.text = "再戦する";
                _gameEndRematchButton.AddToClassList("game-end-button");
                _gameEndRematchButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Enter3SE);
                    OnRematchClicked();
                };
                _gameEndButtonRow.Add(_gameEndRematchButton);

                _gameEndTitleButton = new Button();
                _gameEndTitleButton.text = "ホームに戻る";
                _gameEndTitleButton.AddToClassList("game-end-button");
                _gameEndTitleButton.clicked += () =>
                {
                    _soundPlayer.PlaySE(_soundStore.Enter3SE);
                    LeaveSessionAndGoHomeAsync().Forget();
                };
                _gameEndButtonRow.Add(_gameEndTitleButton);

                _gameEndOverlay.Add(_gameEndButtonRow);
                mainRoot.Add(_gameEndOverlay);

                // デッキは配る初期手札分を上に積んで構築し、枚数バッジを満杯から始める。
                // 配牌アニメで DealCardFromDeckAsync が手札分を引き切ると、残りは playerDeckCards / cpuDeckCards と一致する。
                _playerDeckView = new DeckView(_cardStore.CardTemplate, playerDeckCards.Concat(playerHandCards).ToArray(), _cardStore.CardBack);
                deckArea.Add(_playerDeckView);

                _opponentDeckView = new DeckView(_cardStore.CardTemplate, cpuDeckCards.Concat(cpuHandCards).ToArray(), _cardStore.CardBack, isOpponent: true);
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

                // チュートリアルでは VS 告知の演出をスキップする（先攻固定・誘導に集中させるため）。
                if (!_isTutorial)
                {
                    string localDisplayName = string.IsNullOrEmpty(_localUsername) ? "ゲスト" : _localUsername;
                    await PlayVsAnnouncementAsync(localDisplayName, _opponentUsername, ct);
                }

                // 先攻後攻を配牌前にコイントスで確定する（手札枚数が先攻3枚・後攻5枚で変わるため）。
                // チュートリアルではコイントス演出をスキップし、内部の先攻後攻設定だけ行う（InitializeFirstTurnAsync 内で分岐）。
                await InitializeFirstTurnAsync(isLocalFirst, ct);

                Rect deckWorldRect = _playerDeckView.worldBound;
                Rect opponentDeckWorldRect = _opponentDeckView.worldBound;
                UniTask[] drawTasks = new UniTask[playerHandSize + cpuHandSize];
                int drawTaskIndex = 0;
                for (int i = 0; i < playerHandSize; i++)
                {
                    drawTasks[drawTaskIndex++] = DealCardFromDeckAsync(_handView, _playerDeckView, playerHandCards[i], deckWorldRect, i * DrawStagger, ct);
                }
                for (int i = 0; i < cpuHandSize; i++)
                {
                    drawTasks[drawTaskIndex++] = DealCardFromDeckAsync(_opponentHandView, _opponentDeckView, cpuHandCards[i], opponentDeckWorldRect, i * DrawStagger, ct);
                }
                await UniTask.WhenAll(drawTasks);

                if (isOnline)
                {
                    CardData[] onlinePlayerFull = playerHandCards.Concat(playerDeckCards).ToArray();
                    CardData opponentPlaceholder = allCards.Length > 0 ? allCards[0] : null;
                    UniTask<NetworkGameService.MulliganResult> waitOpponentMulligan = _networkGameService.WaitForOpponentMulliganDecisionAsync(ct);
                    bool localChose = await RunPlayerMulliganAsync(onlinePlayerFull, _handView, _playerDeckView, playerHandSize, ct);
                    string[] newDeckIds = localChose ? _playerDeckView.GetCardIds() : null;
                    _networkGameService.SendMulliganDecision(localChose, newDeckIds);
                    _waitingOverlay.style.display = DisplayStyle.Flex;
                    NetworkGameService.MulliganResult opponentResult = await waitOpponentMulligan;
                    // マリガン同期（最後の通信同期点）直後に登録しておく。
                    // この後すぐ RunGame に入るため、先攻の相手が自分より先に DrawPhase へ入って
                    // NGS_Draw を送っても、ハンドラ未登録で捨てられるのを防ぐ。
                    _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                    _hasPreDrawTask = true;
                    _waitingOverlay.style.display = DisplayStyle.None;
                    if (opponentResult.Mulliganed)
                    {
                        await RunOpponentMulliganAnimationAsync(opponentPlaceholder, cpuHandSize, opponentResult.NewDeck, ct);
                    }
                }
                else if (_isTutorial)
                {
                    // チュートリアルはマリガンを行わない（固定の初期手札のまま進める）。
                    SetupTutorial(mainRoot);
                }
                else
                {
                    await RunPlayerMulliganAsync(playerDeckFull, _handView, _playerDeckView, playerHandSize, ct);
                    await RunCpuMulliganIfNeededAsync(allCards, _opponentHandView, _opponentDeckView, cpuHandSize, ct);
                }

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

        private void OnDestroy()
        {
            if (_optionPresenter != null)
            {
                _optionPresenter.ClearSurrenderHandler();
            }
            _surrenderCts?.Dispose();
            UnregisterRematchCallbacks();
            if (_rainDefeatEffect != null)
            {
                Destroy(_rainDefeatEffect);
            }
        }
    }
}

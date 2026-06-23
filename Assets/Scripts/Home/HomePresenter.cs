using System;
using System.Threading;
using Common.Cpu;
using Common.Deck;
using Common.GameSession;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Tutorial;
using Common.Username;
using Common.View;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Home
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HomePresenter : MonoBehaviour
    {
        [SerializeField] private HomeBackgroundPresenter _backgroundPresenter;
        [SerializeField] private GameObject _rainEffectPrefab;
        // 使用デッキ選択モーダルでお気に入りカードの画像を引くために使う。
        // DeckBuilder と同じ CardDatabase アセットをインスペクタで割り当てる（未割り当てなら背景は出ない）。
        [SerializeField] private CardDatabase _cardDatabase;
        // 使用デッキ選択モーダルで、シンボル未設定デッキに表示するカード裏面（Addressable Image/CardBack の Texture2D）。
        // 未割り当てなら裏面は出ない（Home には CardStore が無いためインスペクタで割り当てる）。
        [SerializeField] private Texture2D _cardBack;

        private SceneTransitioner _sceneTransitioner;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;
        private DeckModel _deckModel;
        private DeckRuleModel _deckRuleModel;
        private DeckRepository _deckRepository;
        private GameSessionModel _gameSessionModel;
        private TutorialModel _tutorialModel;
        private CpuRosterStore _cpuRosterStore;
        private CpuBattleModel _cpuBattleModel;
        private UIDocument _uiDocument;
        private Button _tutorialButton;
        private Button _deckBuilderButton;
        private Button _battleButton;
        private Button _matchingButton;
        private Button _deckSelectButton;
        private VisualElement _deckSelectOverlay;
        private Button _deckSelectCloseButton;
        private ScrollView _deckSelectList;
        private VisualElement _opponentSelectOverlay;
        private Button _opponentSelectCloseButton;
        private Button _opponentPrevButton;
        private Button _opponentNextButton;
        private Button _opponentFightButton;
        private VisualElement _opponentPortrait;
        private Label _opponentNameLabel;
        private VisualElement _opponentDots;
        private int _currentOpponentIndex;
        private Button _creditButton;
        private Button _creditCloseButton;
        private VisualElement _creditOverlay;
        private VisualElement _tutorialOverlay;
        private Button _tutorialCloseButton;
        private Button _tutorialEntryBasic;
        private Button _tutorialEntryDeckOut;
        private Button _tutorialEntryFieldChars;
        private Button _tutorialEntryVictoryPoints;
        private Button[] _tutorialTabs;
        private VisualElement[] _tutorialPages;
        private ScrollView _tutorialScroll;
        private Action[] _tutorialTabHandlers;
        private Button[] _tutorialKeywordEntries;
        private Action[] _tutorialKeywordHandlers;

        // キーワード能力タブの各エントリ（表示順）と対応する TutorialId。
        private static readonly string[] _keywordEntryNames =
        {
            "TutorialEntryGuardian", "TutorialEntryHaste", "TutorialEntryFlying",
            "TutorialEntrySakimori", "TutorialEntryAssault", "TutorialEntryNoDeckAttack",
        };
        private static readonly TutorialId[] _keywordTutorialIds =
        {
            TutorialId.GuardianKw, TutorialId.HasteKw, TutorialId.FlyingKw,
            TutorialId.SakimoriKw, TutorialId.AssaultKw, TutorialId.NoDeckAttackKw,
        };
        private Button _rulesButton;
        private Button _rulesCloseButton;
        private VisualElement _rulesOverlay;
        private Button[] _rulesTabs;
        private VisualElement[] _rulesPages;
        private Action[] _rulesTabHandlers;
        private ScrollView _rulesScroll;
        private Label _costOverToastLabel;
        private Label _usernameLabel;
        private UsernameRepository _usernameRepository;
        private Button _usernameEditButton;
        private VisualElement _usernameEditOverlay;
        private TextField _usernameEditField;
        private Label _usernameEditError;
        private Button _usernameEditConfirmButton;
        private Button _usernameEditCloseButton;
        private VisualElement _darkOverlay;
        private ToastController _deckToast;
#if UNITY_EDITOR
        // Editor 再生時のみ表示するデバッグ用トグル（同名カード3枚制限・デッキ枚数制限の ON/OFF）。
        // ビルドには存在しないため、ビルドでは制限は常に有効。
        private Toggle _sameCardLimitToggle;
        private Toggle _deckCountLimitToggle;
#endif

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner, SoundPlayer soundPlayer, SoundStore soundStore, DeckModel deckModel, DeckRuleModel deckRuleModel, DeckRepository deckRepository, GameSessionModel gameSessionModel, TutorialModel tutorialModel, CpuRosterStore cpuRosterStore, CpuBattleModel cpuBattleModel, UsernameRepository usernameRepository)
        {
            _sceneTransitioner = sceneTransitioner;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
            _deckModel = deckModel;
            _deckRuleModel = deckRuleModel;
            _deckRepository = deckRepository;
            _gameSessionModel = gameSessionModel;
            _tutorialModel = tutorialModel;
            _cpuRosterStore = cpuRosterStore;
            _cpuBattleModel = cpuBattleModel;
            _usernameRepository = usernameRepository;
            _deckModel.Clear();
            // 対戦には「選択中スロット」のデッキを使う。
            _deckRepository.Load(_deckModel, _deckRepository.SelectedIndex);
            UpdateDeckSelectButtonLabel();
            UpdateUsernameLabel();
            if (_backgroundPresenter != null && _gameSessionModel.ShouldRainOnNextHome)
            {
                _backgroundPresenter.IsRainy = true;
                _gameSessionModel.ShouldRainOnNextHome = false;
                HomeLive2DPresenter live2D = GetComponent<HomeLive2DPresenter>();
                if (live2D != null)
                {
                    live2D.IsRainy = true;
                }
                DogSpeechPresenter speech = GetComponent<DogSpeechPresenter>();
                if (speech != null)
                {
                    speech.IsRainy = true;
                }
            }
#if UNITY_EDITOR
            // 注入順は OnEnable → Start → Construct のため、_deckRuleModel と
            // rootVisualElement の両方が揃う Construct の最後でトグルを生成する。
            _sameCardLimitToggle = new Toggle("同名3枚制限");
            _sameCardLimitToggle.AddToClassList("home-debug-toggle");
            _sameCardLimitToggle.value = _deckRuleModel.LimitSameCards;
            _sameCardLimitToggle.RegisterValueChangedCallback(OnSameCardLimitToggled);
            _uiDocument.rootVisualElement.Add(_sameCardLimitToggle);

            _deckCountLimitToggle = new Toggle("デッキ枚数制限");
            _deckCountLimitToggle.AddToClassList("home-debug-toggle");
            _deckCountLimitToggle.AddToClassList("home-debug-toggle--deck-count");
            _deckCountLimitToggle.value = _deckRuleModel.LimitDeckCount;
            _deckCountLimitToggle.RegisterValueChangedCallback(OnDeckCountLimitToggled);
            _uiDocument.rootVisualElement.Add(_deckCountLimitToggle);
#endif
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            bool isRainy = _backgroundPresenter != null && _backgroundPresenter.IsRainy;
            if (isRainy)
            {
                // 敗北で戻ってきたときは Main から鳴り続けている戦闘 BGM を止め、雨音の演出にする
                _soundPlayer.StopBGM();
                if (_rainEffectPrefab != null)
                {
                    Instantiate(_rainEffectPrefab, new Vector3(0f, 15f, 0f), Quaternion.identity);
                }
                if (_darkOverlay != null)
                {
                    _darkOverlay.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                PlaySunnyBgmAsync(destroyCancellationToken).Forget();
            }
        }

        private async UniTaskVoid PlaySunnyBgmAsync(CancellationToken ct)
        {
            try
            {
                await _soundStore.Loaded.AttachExternalCancellation(ct);
                _soundPlayer.PlayBGM(_soundStore.MainBGM);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _tutorialButton = root.Q<Button>("TutorialButton");
            _deckBuilderButton = root.Q<Button>("DeckBuilderButton");
            _battleButton = root.Q<Button>("BattleButton");
            _matchingButton = root.Q<Button>("MatchingButton");
            _deckSelectButton = root.Q<Button>("DeckSelectButton");
            _deckSelectOverlay = root.Q<VisualElement>("DeckSelectOverlay");
            _deckSelectCloseButton = root.Q<Button>("DeckSelectCloseButton");
            _deckSelectList = root.Q<ScrollView>("DeckSelectList");
            _opponentSelectOverlay = root.Q<VisualElement>("OpponentSelectOverlay");
            _opponentSelectCloseButton = root.Q<Button>("OpponentSelectCloseButton");
            _opponentPrevButton = root.Q<Button>("OpponentPrevButton");
            _opponentNextButton = root.Q<Button>("OpponentNextButton");
            _opponentFightButton = root.Q<Button>("OpponentFightButton");
            _opponentPortrait = root.Q<VisualElement>("OpponentPortrait");
            _opponentNameLabel = root.Q<Label>("OpponentNameLabel");
            _opponentDots = root.Q<VisualElement>("OpponentDots");
            _costOverToastLabel = root.Q<Label>("CostOverToastLabel");
            _deckToast = new ToastController(_costOverToastLabel);
            _usernameLabel = root.Q<Label>("UsernameLabel");
            _usernameEditButton = root.Q<Button>("UsernameEditButton");
            _usernameEditOverlay = root.Q<VisualElement>("UsernameEditOverlay");
            _usernameEditField = root.Q<TextField>("UsernameEditField");
            _usernameEditError = root.Q<Label>("UsernameEditError");
            _usernameEditConfirmButton = root.Q<Button>("UsernameEditConfirmButton");
            _usernameEditCloseButton = root.Q<Button>("UsernameEditCloseButton");
            _darkOverlay = root.Q<VisualElement>("DarkOverlay");
            _creditButton = root.Q<Button>("CreditButton");
            _creditCloseButton = root.Q<Button>("CreditCloseButton");
            _creditOverlay = root.Q<VisualElement>("CreditOverlay");
            _tutorialOverlay = root.Q<VisualElement>("TutorialOverlay");
            _tutorialCloseButton = root.Q<Button>("TutorialCloseButton");
            _tutorialEntryBasic = root.Q<Button>("TutorialEntryBasic");
            _tutorialEntryDeckOut = root.Q<Button>("TutorialEntryDeckOut");
            _tutorialEntryFieldChars = root.Q<Button>("TutorialEntryFieldChars");
            _tutorialEntryVictoryPoints = root.Q<Button>("TutorialEntryVictoryPoints");
            _tutorialTabs = new Button[]
            {
                root.Q<Button>("TutorialTabBasic"),
                root.Q<Button>("TutorialTabKeyword"),
            };
            _tutorialPages = new VisualElement[]
            {
                root.Q<VisualElement>("TutorialPageBasic"),
                root.Q<VisualElement>("TutorialPageKeyword"),
            };
            _tutorialScroll = root.Q<ScrollView>("TutorialScroll");
            _tutorialKeywordEntries = new Button[_keywordEntryNames.Length];
            for (int i = 0; i < _keywordEntryNames.Length; i++)
            {
                _tutorialKeywordEntries[i] = root.Q<Button>(_keywordEntryNames[i]);
            }
            _rulesButton = root.Q<Button>("RulesButton");
            _rulesCloseButton = root.Q<Button>("RulesCloseButton");
            _rulesOverlay = root.Q<VisualElement>("RulesOverlay");
            _rulesScroll = root.Q<ScrollView>("RulesScroll");
            _rulesTabs = new Button[]
            {
                root.Q<Button>("RulesTabGoal"),
                root.Q<Button>("RulesTabFlow"),
                root.Q<Button>("RulesTabCard"),
                root.Q<Button>("RulesTabBattle"),
                root.Q<Button>("RulesTabKeyword"),
                root.Q<Button>("RulesTabDeck"),
            };
            _rulesPages = new VisualElement[]
            {
                root.Q<VisualElement>("RulesPageGoal"),
                root.Q<VisualElement>("RulesPageFlow"),
                root.Q<VisualElement>("RulesPageCard"),
                root.Q<VisualElement>("RulesPageBattle"),
                root.Q<VisualElement>("RulesPageKeyword"),
                root.Q<VisualElement>("RulesPageDeck"),
            };
            _tutorialButton.clicked += OnTutorialClicked;
            _tutorialCloseButton.clicked += OnTutorialCloseClicked;
            _tutorialEntryBasic.clicked += OnTutorialEntryBasicClicked;
            _tutorialEntryDeckOut.clicked += OnTutorialEntryDeckOutClicked;
            _tutorialEntryFieldChars.clicked += OnTutorialEntryFieldCharsClicked;
            _tutorialEntryVictoryPoints.clicked += OnTutorialEntryVictoryPointsClicked;
            _tutorialTabHandlers = new Action[_tutorialTabs.Length];
            for (int i = 0; i < _tutorialTabs.Length; i++)
            {
                int index = i;
                _tutorialTabHandlers[i] = () => OnTutorialTabClicked(index);
                _tutorialTabs[i].clicked += _tutorialTabHandlers[i];
            }
            _tutorialKeywordHandlers = new Action[_tutorialKeywordEntries.Length];
            for (int i = 0; i < _tutorialKeywordEntries.Length; i++)
            {
                int index = i;
                _tutorialKeywordHandlers[i] = () => StartTutorial(_keywordTutorialIds[index]);
                _tutorialKeywordEntries[i].clicked += _tutorialKeywordHandlers[i];
            }
            SelectTutorialTab(0);
            _deckBuilderButton.clicked += OnDeckBuilderClicked;
            _battleButton.clicked += OnBattleClicked;
            _deckSelectButton.clicked += OnDeckSelectClicked;
            _deckSelectCloseButton.clicked += OnDeckSelectCloseClicked;
            _opponentSelectCloseButton.clicked += OnOpponentSelectCloseClicked;
            _opponentPrevButton.clicked += OnOpponentPrevClicked;
            _opponentNextButton.clicked += OnOpponentNextClicked;
            _opponentFightButton.clicked += OnOpponentFightClicked;
            _matchingButton.clicked += OnMatchingClicked;
            _creditButton.clicked += OnCreditClicked;
            _creditCloseButton.clicked += OnCreditCloseClicked;
            _usernameEditButton.clicked += OnUsernameEditClicked;
            _usernameEditCloseButton.clicked += OnUsernameEditCloseClicked;
            _usernameEditConfirmButton.clicked += OnUsernameEditConfirmClicked;
            _usernameEditField.RegisterValueChangedCallback(OnUsernameEditFieldChanged);
            _rulesButton.clicked += OnRulesClicked;
            _rulesCloseButton.clicked += OnRulesCloseClicked;
            _rulesTabHandlers = new Action[_rulesTabs.Length];
            for (int i = 0; i < _rulesTabs.Length; i++)
            {
                int index = i;
                _rulesTabHandlers[i] = () => OnRulesTabClicked(index);
                _rulesTabs[i].clicked += _rulesTabHandlers[i];
            }
            SelectRulesTab(0);
        }

        private void OnDisable()
        {
            if (_tutorialButton != null)
            {
                _tutorialButton.clicked -= OnTutorialClicked;
            }
            if (_tutorialCloseButton != null)
            {
                _tutorialCloseButton.clicked -= OnTutorialCloseClicked;
            }
            if (_tutorialEntryBasic != null)
            {
                _tutorialEntryBasic.clicked -= OnTutorialEntryBasicClicked;
            }
            if (_tutorialEntryDeckOut != null)
            {
                _tutorialEntryDeckOut.clicked -= OnTutorialEntryDeckOutClicked;
            }
            if (_tutorialEntryFieldChars != null)
            {
                _tutorialEntryFieldChars.clicked -= OnTutorialEntryFieldCharsClicked;
            }
            if (_tutorialEntryVictoryPoints != null)
            {
                _tutorialEntryVictoryPoints.clicked -= OnTutorialEntryVictoryPointsClicked;
            }
            if (_tutorialTabs != null && _tutorialTabHandlers != null)
            {
                for (int i = 0; i < _tutorialTabs.Length; i++)
                {
                    if (_tutorialTabs[i] != null && _tutorialTabHandlers[i] != null)
                    {
                        _tutorialTabs[i].clicked -= _tutorialTabHandlers[i];
                    }
                }
            }
            if (_tutorialKeywordEntries != null && _tutorialKeywordHandlers != null)
            {
                for (int i = 0; i < _tutorialKeywordEntries.Length; i++)
                {
                    if (_tutorialKeywordEntries[i] != null && _tutorialKeywordHandlers[i] != null)
                    {
                        _tutorialKeywordEntries[i].clicked -= _tutorialKeywordHandlers[i];
                    }
                }
            }
            if (_deckBuilderButton != null)
            {
                _deckBuilderButton.clicked -= OnDeckBuilderClicked;
            }
            if (_battleButton != null)
            {
                _battleButton.clicked -= OnBattleClicked;
            }
            if (_opponentSelectCloseButton != null)
            {
                _opponentSelectCloseButton.clicked -= OnOpponentSelectCloseClicked;
            }
            if (_opponentPrevButton != null)
            {
                _opponentPrevButton.clicked -= OnOpponentPrevClicked;
            }
            if (_opponentNextButton != null)
            {
                _opponentNextButton.clicked -= OnOpponentNextClicked;
            }
            if (_opponentFightButton != null)
            {
                _opponentFightButton.clicked -= OnOpponentFightClicked;
            }
            if (_matchingButton != null)
            {
                _matchingButton.clicked -= OnMatchingClicked;
            }
            if (_deckSelectButton != null)
            {
                _deckSelectButton.clicked -= OnDeckSelectClicked;
            }
            if (_deckSelectCloseButton != null)
            {
                _deckSelectCloseButton.clicked -= OnDeckSelectCloseClicked;
            }
            if (_creditButton != null)
            {
                _creditButton.clicked -= OnCreditClicked;
            }
            if (_creditCloseButton != null)
            {
                _creditCloseButton.clicked -= OnCreditCloseClicked;
            }
            if (_usernameEditButton != null)
            {
                _usernameEditButton.clicked -= OnUsernameEditClicked;
            }
            if (_usernameEditCloseButton != null)
            {
                _usernameEditCloseButton.clicked -= OnUsernameEditCloseClicked;
            }
            if (_usernameEditConfirmButton != null)
            {
                _usernameEditConfirmButton.clicked -= OnUsernameEditConfirmClicked;
            }
            if (_usernameEditField != null)
            {
                _usernameEditField.UnregisterValueChangedCallback(OnUsernameEditFieldChanged);
            }
            if (_rulesButton != null)
            {
                _rulesButton.clicked -= OnRulesClicked;
            }
            if (_rulesCloseButton != null)
            {
                _rulesCloseButton.clicked -= OnRulesCloseClicked;
            }
            if (_rulesTabs != null && _rulesTabHandlers != null)
            {
                for (int i = 0; i < _rulesTabs.Length; i++)
                {
                    if (_rulesTabs[i] != null && _rulesTabHandlers[i] != null)
                    {
                        _rulesTabs[i].clicked -= _rulesTabHandlers[i];
                    }
                }
            }
            _tutorialButton = null;
            _tutorialOverlay = null;
            _tutorialCloseButton = null;
            _tutorialEntryBasic = null;
            _tutorialEntryDeckOut = null;
            _tutorialEntryFieldChars = null;
            _tutorialEntryVictoryPoints = null;
            _tutorialTabs = null;
            _tutorialPages = null;
            _tutorialScroll = null;
            _tutorialTabHandlers = null;
            _tutorialKeywordEntries = null;
            _tutorialKeywordHandlers = null;
            _deckBuilderButton = null;
            _battleButton = null;
            _opponentSelectOverlay = null;
            _opponentSelectCloseButton = null;
            _opponentPrevButton = null;
            _opponentNextButton = null;
            _opponentFightButton = null;
            _opponentPortrait = null;
            _opponentNameLabel = null;
            _opponentDots = null;
            _matchingButton = null;
            _deckSelectButton = null;
            _deckSelectOverlay = null;
            _deckSelectCloseButton = null;
            _deckSelectList = null;
            _creditButton = null;
            _creditCloseButton = null;
            _creditOverlay = null;
            _rulesButton = null;
            _rulesCloseButton = null;
            _rulesOverlay = null;
            _rulesTabs = null;
            _rulesPages = null;
            _rulesTabHandlers = null;
            _rulesScroll = null;
            _deckToast?.Dispose();
            _deckToast = null;
            _costOverToastLabel = null;
            _usernameLabel = null;
            _usernameEditButton = null;
            _usernameEditOverlay = null;
            _usernameEditField = null;
            _usernameEditError = null;
            _usernameEditConfirmButton = null;
            _usernameEditCloseButton = null;
            _darkOverlay = null;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (_sameCardLimitToggle != null)
            {
                _sameCardLimitToggle.UnregisterValueChangedCallback(OnSameCardLimitToggled);
                _sameCardLimitToggle.RemoveFromHierarchy();
                _sameCardLimitToggle = null;
            }
            if (_deckCountLimitToggle != null)
            {
                _deckCountLimitToggle.UnregisterValueChangedCallback(OnDeckCountLimitToggled);
                _deckCountLimitToggle.RemoveFromHierarchy();
                _deckCountLimitToggle = null;
            }
#endif
        }

#if UNITY_EDITOR
        private void OnSameCardLimitToggled(ChangeEvent<bool> evt)
        {
            _deckRuleModel.LimitSameCards = evt.newValue;
            _deckRuleModel.SaveLimitSameCards();
        }

        private void OnDeckCountLimitToggled(ChangeEvent<bool> evt)
        {
            _deckRuleModel.LimitDeckCount = evt.newValue;
            _deckRuleModel.SaveLimitDeckCount();
        }
#endif

        private void PlayEnterSE()
        {
            _soundPlayer.PlaySE(_soundStore.EnterSE);
        }

        private void OnDeckBuilderClicked()
        {
            PlayEnterSE();
            _sceneTransitioner.Transit(Scenes.DeckBuilder).Forget();
        }

        // 「使用デッキ」ボタン：対戦に使うデッキを選ぶモーダルを開く。
        private void OnDeckSelectClicked()
        {
            PlayEnterSE();
            BuildDeckSelectList();
            _deckSelectOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnDeckSelectCloseClicked()
        {
            PlayEnterSE();
            _deckSelectOverlay.style.display = DisplayStyle.None;
        }

        // 9 スロットの一覧（名前・枚数・完成状態・使用中マーク）を組む。行タップで使用デッキを切り替える。
        private void BuildDeckSelectList()
        {
            _deckSelectList.Clear();
            int selected = _deckRepository.SelectedIndex;
            for (int i = 0; i < DeckRepository.SlotCount; i++)
            {
                int slot = i;
                VisualElement row = new VisualElement();
                row.AddToClassList("home-deck-row");
                if (slot == selected)
                {
                    row.AddToClassList("home-deck-row--selected");
                }
                row.RegisterCallback<ClickEvent>(_ => OnDeckRowClicked(slot));

                // 左にデッキのシンボル（代表カード全体）を表示する。シンボル未設定はカード裏面で代替する。
                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("home-deck-row-favorite");
                thumbnail.pickingMode = PickingMode.Ignore;
                string favoriteId = _deckRepository.LoadFavorite(slot);
                if (_cardDatabase != null && !string.IsNullOrEmpty(favoriteId)
                    && _cardDatabase.TryGet(favoriteId, out CardData favorite) && favorite.Image != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(favorite.Image);
                }
                else if (_cardBack != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(_cardBack);
                }
                row.Add(thumbnail);

                Label nameLabel = new Label(_deckRepository.LoadName(slot));
                nameLabel.AddToClassList("home-deck-row-name");
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                Label badge = new Label("使用中");
                badge.AddToClassList("home-deck-row-badge");
                badge.style.display = slot == selected ? DisplayStyle.Flex : DisplayStyle.None;
                badge.pickingMode = PickingMode.Ignore;
                row.Add(badge);

                int count = _deckRepository.LoadCount(slot);
                Label countLabel = new Label($"{count}/{DeckModel.MaxCards}");
                countLabel.AddToClassList("home-deck-row-count");
                if (count == DeckModel.MaxCards)
                {
                    countLabel.AddToClassList("home-deck-row-count--ready");
                }
                countLabel.pickingMode = PickingMode.Ignore;
                row.Add(countLabel);

                _deckSelectList.Add(row);
            }
        }

        // 使用デッキを切り替えて対戦用の DeckModel を差し替える。
        private void OnDeckRowClicked(int slot)
        {
            PlayEnterSE();
            _deckRepository.SelectedIndex = slot;
            _deckRepository.Load(_deckModel, slot);
            UpdateDeckSelectButtonLabel();
            _deckSelectOverlay.style.display = DisplayStyle.None;
        }

        private void UpdateDeckSelectButtonLabel()
        {
            if (_deckSelectButton == null)
            {
                return;
            }
            _deckSelectButton.text = $"使用デッキ：{_deckRepository.LoadName(_deckRepository.SelectedIndex)}";
        }

        // 「CPU対戦」ボタン：デッキを検証してから相手選択オーバーレイを開く。
        private void OnBattleClicked()
        {
            PlayEnterSE();
            if (!IsDeckPlayable())
            {
                _deckToast.Show(GetDeckErrorMessage(), 1500, destroyCancellationToken);
                return;
            }
            OpenOpponentSelectAsync().Forget();
        }

        // ロスターのロード完了を待ってからカルーセルを初期化し、オーバーレイを表示する。
        // ロスターは Common で起動時にロードするため通常は即時完了する。
        private async UniTaskVoid OpenOpponentSelectAsync()
        {
            try
            {
                await _cpuRosterStore.Loaded.AttachExternalCancellation(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (_opponentSelectOverlay == null || _opponentPortrait == null)
            {
                return;
            }
            _currentOpponentIndex = 0;
            BuildOpponentDots();
            ShowOpponent(_currentOpponentIndex);
            _opponentSelectOverlay.style.display = DisplayStyle.Flex;
        }

        // ページドット（相手数ぶん）を生成する。現在位置は ShowOpponent でハイライトする。
        private void BuildOpponentDots()
        {
            if (_opponentDots == null)
            {
                return;
            }
            _opponentDots.Clear();
            int count = _cpuRosterStore.OpponentCount;
            for (int i = 0; i < count; i++)
            {
                VisualElement dot = new VisualElement();
                dot.AddToClassList("home-carousel-dot");
                dot.pickingMode = PickingMode.Ignore;
                _opponentDots.Add(dot);
            }
        }

        // 指定 index の相手をカルーセル中央に表示する（端で循環する）。
        private void ShowOpponent(int index)
        {
            int count = _cpuRosterStore.OpponentCount;
            if (count <= 0)
            {
                return;
            }
            _currentOpponentIndex = ((index % count) + count) % count;

            Texture2D texture = _cpuRosterStore.GetOpponent(_currentOpponentIndex)?.Portrait;
            if (texture != null)
            {
                _opponentPortrait.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                _opponentPortrait.style.backgroundImage = StyleKeyword.None;
            }
            _opponentPortrait.EnableInClassList("home-carousel-portrait--empty", texture == null);

            _opponentNameLabel.text = _cpuRosterStore.DisplayName(_currentOpponentIndex);
            UpdateOpponentDots();
        }

        private void UpdateOpponentDots()
        {
            if (_opponentDots == null)
            {
                return;
            }
            for (int i = 0; i < _opponentDots.childCount; i++)
            {
                _opponentDots[i].EnableInClassList("home-carousel-dot--active", i == _currentOpponentIndex);
            }
        }

        private void OnOpponentPrevClicked()
        {
            PlayEnterSE();
            ShowOpponent(_currentOpponentIndex - 1);
        }

        private void OnOpponentNextClicked()
        {
            PlayEnterSE();
            ShowOpponent(_currentOpponentIndex + 1);
        }

        // 「この相手と戦う」：表示中の相手の index を保存して対戦開始。再戦時もこの index が使われる。
        private void OnOpponentFightClicked()
        {
            PlayEnterSE();
            _cpuBattleModel.OpponentIndex = _currentOpponentIndex;
            _opponentSelectOverlay.style.display = DisplayStyle.None;
            StartCpuBattleAsync().Forget();
        }

        private void OnOpponentSelectCloseClicked()
        {
            PlayEnterSE();
            _opponentSelectOverlay.style.display = DisplayStyle.None;
        }

        private async UniTaskVoid StartCpuBattleAsync()
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            await _sceneTransitioner.Transit(Scenes.Main);
        }

        // 「チュートリアル」ボタン：選択モーダルを開く（どのチュートリアルを始めるか選ぶ）。
        private void OnTutorialClicked()
        {
            PlayEnterSE();
            SelectTutorialTab(0);
            _tutorialOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnTutorialTabClicked(int index)
        {
            PlayEnterSE();
            SelectTutorialTab(index);
        }

        // 指定したタブを選択状態にし、対応するページのみ表示する。
        private void SelectTutorialTab(int index)
        {
            if (_tutorialTabs == null || _tutorialPages == null)
            {
                return;
            }
            for (int i = 0; i < _tutorialTabs.Length; i++)
            {
                bool selected = i == index;
                _tutorialTabs[i].EnableInClassList("home-rules-tab--selected", selected);
                _tutorialPages[i].EnableInClassList("home-rules-page--active", selected);
            }
            // タブを切り替えるたびにスクロール位置を先頭へ戻す。
            if (_tutorialScroll != null)
            {
                _tutorialScroll.scrollOffset = Vector2.zero;
            }
        }

        private void OnTutorialCloseClicked()
        {
            PlayEnterSE();
            _tutorialOverlay.style.display = DisplayStyle.None;
        }

        private void OnTutorialEntryBasicClicked()
        {
            StartTutorial(TutorialId.BasicLoop);
        }

        private void OnTutorialEntryDeckOutClicked()
        {
            StartTutorial(TutorialId.DeckOutWin);
        }

        private void OnTutorialEntryFieldCharsClicked()
        {
            StartTutorial(TutorialId.FieldCharsWin);
        }

        private void OnTutorialEntryVictoryPointsClicked()
        {
            StartTutorial(TutorialId.VictoryPointsWin);
        }

        // 選択したチュートリアルを開始する。固定デッキで戦うため、通常対戦のデッキ30枚チェックは行わない。
        // フラグ・IDを立て、オンラインセッションを抜けて Main へ。
        private void StartTutorial(TutorialId id)
        {
            PlayEnterSE();
            _tutorialModel.IsActive = true;
            _tutorialModel.Id = id;
            StartTutorialAsync().Forget();
        }

        private async UniTaskVoid StartTutorialAsync()
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            await _sceneTransitioner.Transit(Scenes.Main);
        }

        private void OnMatchingClicked()
        {
            PlayEnterSE();
            if (!IsDeckPlayable())
            {
                _deckToast.Show(GetDeckErrorMessage(), 1500, destroyCancellationToken);
                return;
            }
            _sceneTransitioner.Transit(Scenes.Matching).Forget();
        }

        private void OnCreditClicked()
        {
            PlayEnterSE();
            _creditOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnCreditCloseClicked()
        {
            PlayEnterSE();
            _creditOverlay.style.display = DisplayStyle.None;
        }

        private void UpdateUsernameLabel()
        {
            _usernameLabel.text = $"ユーザーネーム：{_usernameRepository.Load() ?? string.Empty}";
        }

        private void OnUsernameEditClicked()
        {
            PlayEnterSE();
            _usernameEditField.value = _usernameRepository.Load() ?? string.Empty;
            ValidateUsernameEditField(_usernameEditField.value);
            _usernameEditOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnUsernameEditCloseClicked()
        {
            PlayEnterSE();
            _usernameEditOverlay.style.display = DisplayStyle.None;
        }

        private void OnUsernameEditFieldChanged(ChangeEvent<string> evt)
        {
            ValidateUsernameEditField(evt.newValue);
        }

        private void ValidateUsernameEditField(string value)
        {
            bool valid = UsernameValidator.IsValid(value, out string errorMessage);
            _usernameEditConfirmButton.SetEnabled(valid);
            _usernameEditError.text = errorMessage;
            _usernameEditError.style.display = string.IsNullOrEmpty(errorMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnUsernameEditConfirmClicked()
        {
            string name = _usernameEditField.value.Trim();
            if (!UsernameValidator.IsValid(name, out string _))
            {
                return;
            }
            PlayEnterSE();
            _usernameRepository.Save(name);
            UpdateUsernameLabel();
            _usernameEditOverlay.style.display = DisplayStyle.None;
        }

        private void OnRulesClicked()
        {
            PlayEnterSE();
            _rulesOverlay.style.display = DisplayStyle.Flex;
        }

        private void OnRulesCloseClicked()
        {
            PlayEnterSE();
            _rulesOverlay.style.display = DisplayStyle.None;
        }

        private void OnRulesTabClicked(int index)
        {
            PlayEnterSE();
            SelectRulesTab(index);
        }

        // 指定したタブを選択状態にし、対応するページのみ表示する。
        private void SelectRulesTab(int index)
        {
            for (int i = 0; i < _rulesTabs.Length; i++)
            {
                bool selected = i == index;
                _rulesTabs[i].EnableInClassList("home-rules-tab--selected", selected);
                _rulesPages[i].EnableInClassList("home-rules-page--active", selected);
            }
            // タブを切り替えるたびにスクロール位置を先頭へ戻す。
            if (_rulesScroll != null)
            {
                _rulesScroll.scrollOffset = Vector2.zero;
            }
        }

        // 対戦を開始できるデッキかどうか。通常はちょうど DeckModel.MaxCards 枚（IsValid）が必要だが、
        // Editor 再生時に「デッキ枚数制限」トグルを OFF にした場合は 1 枚以上であれば開始できる。
        private bool IsDeckPlayable()
        {
            if (_deckRuleModel != null && !_deckRuleModel.LimitDeckCount)
            {
                return _deckModel.Count > 0;
            }
            return _deckModel.IsValid;
        }

        private string GetDeckErrorMessage()
        {
            if (_deckRuleModel != null && !_deckRuleModel.LimitDeckCount)
            {
                return "デッキが空です";
            }
            if (_deckModel.IsOver)
            {
                return $"デッキが{DeckModel.MaxCards}枚を超えています";
            }
            return $"デッキが{DeckModel.MaxCards}枚になっていません";
        }
    }
}

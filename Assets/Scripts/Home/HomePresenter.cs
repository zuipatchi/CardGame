using System;
using System.Threading;
using Common.Deck;
using Common.GameSession;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Username;
using Common.View;
using Cysharp.Threading.Tasks;
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

        private SceneTransitioner _sceneTransitioner;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;
        private DeckModel _deckModel;
        private DeckRuleModel _deckRuleModel;
        private DeckRepository _deckRepository;
        private GameSessionModel _gameSessionModel;
        private UIDocument _uiDocument;
        private Button _deckBuilderButton;
        private Button _battleButton;
        private Button _matchingButton;
        private Button _creditButton;
        private Button _creditCloseButton;
        private VisualElement _creditOverlay;
        private Label _costOverToastLabel;
        private Label _usernameLabel;
        private VisualElement _darkOverlay;
        private ToastController _deckToast;
#if UNITY_EDITOR
        // Editor 再生時のみ表示するデバッグ用トグル（同名カード3枚制限・デッキ枚数制限の ON/OFF）。
        // ビルドには存在しないため、ビルドでは制限は常に有効。
        private Toggle _sameCardLimitToggle;
        private Toggle _deckCountLimitToggle;
#endif

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner, SoundPlayer soundPlayer, SoundStore soundStore, DeckModel deckModel, DeckRuleModel deckRuleModel, DeckRepository deckRepository, GameSessionModel gameSessionModel, UsernameRepository usernameRepository)
        {
            _sceneTransitioner = sceneTransitioner;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
            _deckModel = deckModel;
            _deckRuleModel = deckRuleModel;
            _deckRepository = deckRepository;
            _gameSessionModel = gameSessionModel;
            _deckModel.Clear();
            _deckRepository.Load(_deckModel);
            _usernameLabel.text = $"ユーザーネーム：{usernameRepository.Load() ?? string.Empty}";
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
            _deckBuilderButton = root.Q<Button>("DeckBuilderButton");
            _battleButton = root.Q<Button>("BattleButton");
            _matchingButton = root.Q<Button>("MatchingButton");
            _costOverToastLabel = root.Q<Label>("CostOverToastLabel");
            _deckToast = new ToastController(_costOverToastLabel);
            _usernameLabel = root.Q<Label>("UsernameLabel");
            _darkOverlay = root.Q<VisualElement>("DarkOverlay");
            _creditButton = root.Q<Button>("CreditButton");
            _creditCloseButton = root.Q<Button>("CreditCloseButton");
            _creditOverlay = root.Q<VisualElement>("CreditOverlay");
            _deckBuilderButton.clicked += OnDeckBuilderClicked;
            _battleButton.clicked += OnBattleClicked;
            _matchingButton.clicked += OnMatchingClicked;
            _creditButton.clicked += OnCreditClicked;
            _creditCloseButton.clicked += OnCreditCloseClicked;
        }

        private void OnDisable()
        {
            if (_deckBuilderButton != null)
            {
                _deckBuilderButton.clicked -= OnDeckBuilderClicked;
            }
            if (_battleButton != null)
            {
                _battleButton.clicked -= OnBattleClicked;
            }
            if (_matchingButton != null)
            {
                _matchingButton.clicked -= OnMatchingClicked;
            }
            if (_creditButton != null)
            {
                _creditButton.clicked -= OnCreditClicked;
            }
            if (_creditCloseButton != null)
            {
                _creditCloseButton.clicked -= OnCreditCloseClicked;
            }
            _deckBuilderButton = null;
            _battleButton = null;
            _matchingButton = null;
            _creditButton = null;
            _creditCloseButton = null;
            _creditOverlay = null;
            _deckToast?.Dispose();
            _deckToast = null;
            _costOverToastLabel = null;
            _usernameLabel = null;
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

        private void OnBattleClicked()
        {
            PlayEnterSE();
            if (!IsDeckPlayable())
            {
                _deckToast.Show(GetDeckErrorMessage(), 1500, destroyCancellationToken);
                return;
            }
            StartCpuBattleAsync().Forget();
        }

        private async UniTaskVoid StartCpuBattleAsync()
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

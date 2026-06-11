using System;
using System.Threading;
using Common.Deck;
using Common.GameSession;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Username;
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
        private DeckRepository _deckRepository;
        private GameSessionModel _gameSessionModel;
        private UIDocument _uiDocument;
        private Button _deckBuilderButton;
        private Button _battleButton;
        private Button _matchingButton;
        private Label _costOverToastLabel;
        private Label _usernameLabel;
        private VisualElement _darkOverlay;
        private CancellationTokenSource _toastCts;

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner, SoundPlayer soundPlayer, SoundStore soundStore, DeckModel deckModel, DeckRepository deckRepository, GameSessionModel gameSessionModel, UsernameRepository usernameRepository)
        {
            _sceneTransitioner = sceneTransitioner;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
            _deckModel = deckModel;
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
            _usernameLabel = root.Q<Label>("UsernameLabel");
            _darkOverlay = root.Q<VisualElement>("DarkOverlay");
            _deckBuilderButton.clicked += OnDeckBuilderClicked;
            _battleButton.clicked += OnBattleClicked;
            _matchingButton.clicked += OnMatchingClicked;
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
            _deckBuilderButton = null;
            _battleButton = null;
            _matchingButton = null;
            _costOverToastLabel = null;
            _usernameLabel = null;
            _darkOverlay = null;
        }

        private void OnDestroy()
        {
            _toastCts?.Dispose();
        }

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
            if (!_deckModel.IsValid)
            {
                ShowDeckToastAsync(GetDeckErrorMessage()).Forget();
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
            if (!_deckModel.IsValid)
            {
                ShowDeckToastAsync(GetDeckErrorMessage()).Forget();
                return;
            }
            _sceneTransitioner.Transit(Scenes.Matching).Forget();
        }

        private string GetDeckErrorMessage()
        {
            if (_deckModel.IsOver)
            {
                return $"デッキが{DeckModel.MaxCards}枚を超えています";
            }
            return $"デッキが{DeckModel.MaxCards}枚になっていません";
        }

        private async UniTaskVoid ShowDeckToastAsync(string message)
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _toastCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = _toastCts.Token;
            _costOverToastLabel.text = message;
            _costOverToastLabel.style.display = DisplayStyle.Flex;
            try
            {
                await UniTask.Delay(1500, cancellationToken: token);
                _costOverToastLabel.style.display = DisplayStyle.None;
            }
            catch (OperationCanceledException) { }
        }
    }
}

using System;
using System.Threading;
using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Home
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HomePresenter : MonoBehaviour
    {
        private SceneTransitioner _sceneTransitioner;
        private DeckModel _deckModel;
        private DeckRepository _deckRepository;
        private UIDocument _uiDocument;
        private Button _deckBuilderButton;
        private Button _battleButton;
        private Button _matchingButton;
        private Label _costOverToastLabel;
        private CancellationTokenSource _toastCts;

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner, DeckModel deckModel, DeckRepository deckRepository)
        {
            _sceneTransitioner = sceneTransitioner;
            _deckModel = deckModel;
            _deckRepository = deckRepository;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            _deckModel.Clear();
            _deckRepository.Load(_deckModel);
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _deckBuilderButton = root.Q<Button>("DeckBuilderButton");
            _battleButton = root.Q<Button>("BattleButton");
            _matchingButton = root.Q<Button>("MatchingButton");
            _costOverToastLabel = root.Q<Label>("CostOverToastLabel");
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
        }

        private void OnDestroy()
        {
            _toastCts?.Dispose();
        }

        private void OnDeckBuilderClicked()
        {
            _sceneTransitioner.Transit(Scenes.DeckBuilder).Forget();
        }

        private void OnBattleClicked()
        {
            if (_deckModel.IsOver)
            {
                ShowCostOverToastAsync().Forget();
                return;
            }
            _sceneTransitioner.Transit(Scenes.Main).Forget();
        }

        private void OnMatchingClicked()
        {
            if (_deckModel.IsOver)
            {
                ShowCostOverToastAsync().Forget();
                return;
            }
            _sceneTransitioner.Transit(Scenes.Matching).Forget();
        }

        private async UniTaskVoid ShowCostOverToastAsync()
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _toastCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = _toastCts.Token;
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

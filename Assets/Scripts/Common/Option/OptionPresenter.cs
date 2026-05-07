using Common.SceneManagement;
using Common.Store;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Common.Option
{

    public class OptionPresenter : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private OptionModel _optionModel;
        private ModalStore _modalStore;
        private SceneTransitioner _sceneTransitioner;
        private VisualTreeAsset _modal;
        private VisualElement _overlay;
        private VisualElement _host;
        private readonly CompositeDisposable _disposables = new();

        [Inject]
        public void Construct(ModalStore modalStore, OptionModel optionModel, SceneTransitioner sceneTransitioner)
        {
            _modalStore = modalStore;
            _optionModel = optionModel;
            _sceneTransitioner = sceneTransitioner;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
            {
                Debug.LogError("UIDocument が見つかりませんでした。");
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            Image optionSliders = root.Q<Image>("OptionSliders");
            _overlay = root.Q<VisualElement>("ModalOverlay");
            _host = root.Q<VisualElement>("ModalHost");

            optionSliders.RegisterCallback<ClickEvent>(_ => OpenModal());
        }

        private void Start()
        {
            SetupAsync().Forget();
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        private async UniTask SetupAsync()
        {
            if (_host == null)
            {
                return;
            }

            await _modalStore.Loaded;
            _modal = _modalStore.Modal;
            TemplateContainer modal = _modal.Instantiate();

            Button closeButton = modal.Q<Button>("CloseButton");
            closeButton.clicked += CloseModal;

            Button backToTitleButton = modal.Q<Button>("BackToTitleButton");
            backToTitleButton.clicked += BackToTitle;

            Slider bgmSlider = modal.Q<Slider>("BGMSlider");
            bgmSlider.value = _optionModel.BGMVolume.CurrentValue;

            _optionModel.BGMVolume
                .Subscribe(v => bgmSlider.SetValueWithoutNotify(v))
                .AddTo(_disposables);

            bgmSlider.RegisterValueChangedCallback(OnBGMSliderChange);

            Slider seSlider = modal.Q<Slider>("SESlider");
            seSlider.value = _optionModel.SEVolume.CurrentValue;

            _optionModel.SEVolume
                .Subscribe(v => seSlider.SetValueWithoutNotify(v))
                .AddTo(_disposables);

            seSlider.RegisterValueChangedCallback(OnSESliderChange);

            _host.Add(modal);
            _overlay.style.display = DisplayStyle.None;
        }

        private void OpenModal()
        {
            _overlay.style.display = DisplayStyle.Flex;
        }

        private void CloseModal()
        {
            _overlay.style.display = DisplayStyle.None;
        }

        private void BackToTitle()
        {
            CloseModal();
            _sceneTransitioner.Transit(Scenes.Title).Forget();
        }

        private void OnBGMSliderChange(ChangeEvent<float> evt)
        {
            _optionModel.SetBGMVolume(evt.newValue);
        }

        private void OnSESliderChange(ChangeEvent<float> evt)
        {
            _optionModel.SetSEVolume(evt.newValue);
        }
    }
}

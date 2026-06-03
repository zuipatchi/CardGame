using System;
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
        private Button _backToTitleButton;
        private Action _surrenderAction;
        private Action _pendingSurrenderAction;
        private VisualElement _surrenderConfirmOverlay;

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

            _backToTitleButton = modal.Q<Button>("BackToTitleButton");
            _backToTitleButton.clicked += BackToTitle;

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

            Toggle autoOkToggle = modal.Q<Toggle>("AutoOkToggle");
            autoOkToggle.value = _optionModel.AutoOk.CurrentValue;
            _optionModel.AutoOk
                .Subscribe(v => autoOkToggle.SetValueWithoutNotify(v))
                .AddTo(_disposables);
            autoOkToggle.RegisterValueChangedCallback(evt => _optionModel.SetAutoOk(evt.newValue));

            _host.Add(modal);

            _surrenderConfirmOverlay = BuildSurrenderConfirmOverlay();
            _overlay.Add(_surrenderConfirmOverlay);

            _overlay.style.display = DisplayStyle.None;

            if (_pendingSurrenderAction != null)
            {
                ApplySurrenderHandler(_pendingSurrenderAction);
                _pendingSurrenderAction = null;
            }
        }

        private VisualElement BuildSurrenderConfirmOverlay()
        {
            VisualElement overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.65f));
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.display = DisplayStyle.None;

            VisualElement panel = new VisualElement();
            panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.18f, 1f));
            panel.style.borderTopLeftRadius = 12f;
            panel.style.borderTopRightRadius = 12f;
            panel.style.borderBottomLeftRadius = 12f;
            panel.style.borderBottomRightRadius = 12f;
            panel.style.paddingTop = 32f;
            panel.style.paddingBottom = 32f;
            panel.style.paddingLeft = 48f;
            panel.style.paddingRight = 48f;
            panel.style.alignItems = Align.Center;

            Label label = new Label("本当に降参しますか？");
            label.style.fontSize = 22f;
            label.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 28f;
            panel.Add(label);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            Color yesBaseColor = new Color(0.7f, 0.15f, 0.15f, 1f);
            Button yesButton = new Button(OnSurrenderConfirmed);
            yesButton.text = "はい";
            StyleSurrenderConfirmButton(yesButton, yesBaseColor);
            AddButtonHoverEffect(yesButton, yesBaseColor);
            row.Add(yesButton);

            Color noBaseColor = new Color(0.25f, 0.25f, 0.4f, 1f);
            Button noButton = new Button(HideSurrenderConfirm);
            noButton.text = "いいえ";
            StyleSurrenderConfirmButton(noButton, noBaseColor);
            AddButtonHoverEffect(noButton, noBaseColor);
            noButton.style.marginLeft = 16f;
            row.Add(noButton);

            panel.Add(row);
            overlay.Add(panel);
            return overlay;
        }

        private static void AddButtonHoverEffect(Button button, Color baseColor)
        {
            Color hoverColor = new Color(
                Mathf.Clamp01(baseColor.r + 0.12f),
                Mathf.Clamp01(baseColor.g + 0.12f),
                Mathf.Clamp01(baseColor.b + 0.12f),
                baseColor.a);
            Color activeColor = new Color(
                Mathf.Clamp01(baseColor.r - 0.1f),
                Mathf.Clamp01(baseColor.g - 0.1f),
                Mathf.Clamp01(baseColor.b - 0.1f),
                baseColor.a);
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(baseColor));
            button.RegisterCallback<PointerDownEvent>(_ => button.style.backgroundColor = new StyleColor(activeColor));
            button.RegisterCallback<PointerUpEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
        }

        private static void StyleSurrenderConfirmButton(Button button, Color bgColor)
        {
            button.style.fontSize = 18f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = new StyleColor(Color.white);
            button.style.backgroundColor = new StyleColor(bgColor);
            button.style.paddingTop = 10f;
            button.style.paddingBottom = 10f;
            button.style.paddingLeft = 28f;
            button.style.paddingRight = 28f;
            button.style.borderTopLeftRadius = 8f;
            button.style.borderTopRightRadius = 8f;
            button.style.borderBottomLeftRadius = 8f;
            button.style.borderBottomRightRadius = 8f;
            button.style.borderTopWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
        }

        private void HideSurrenderConfirm()
        {
            _surrenderConfirmOverlay.style.display = DisplayStyle.None;
        }

        private void OnSurrenderConfirmed()
        {
            HideSurrenderConfirm();
            CloseModal();
            _surrenderAction?.Invoke();
        }

        private void OpenModal()
        {
            _overlay.style.display = DisplayStyle.Flex;
        }

        private void CloseModal()
        {
            if (_surrenderConfirmOverlay != null)
            {
                _surrenderConfirmOverlay.style.display = DisplayStyle.None;
            }
            _overlay.style.display = DisplayStyle.None;
        }

        public void SetSurrenderHandler(Action surrenderAction)
        {
            if (_backToTitleButton == null)
            {
                _pendingSurrenderAction = surrenderAction;
                return;
            }

            ApplySurrenderHandler(surrenderAction);
        }

        private void ApplySurrenderHandler(Action surrenderAction)
        {
            _surrenderAction = surrenderAction;
            _backToTitleButton.text = "降参";
            _backToTitleButton.clicked -= BackToTitle;
            _backToTitleButton.clicked -= OnSurrenderButtonClicked;
            _backToTitleButton.clicked += OnSurrenderButtonClicked;
        }

        public void ClearSurrenderHandler()
        {
            if (_backToTitleButton == null)
            {
                return;
            }

            _surrenderAction = null;
            _backToTitleButton.text = "タイトルへ戻る";
            _backToTitleButton.clicked -= OnSurrenderButtonClicked;
            _backToTitleButton.clicked -= BackToTitle;
            _backToTitleButton.clicked += BackToTitle;
        }

        private void OnSurrenderButtonClicked()
        {
            if (_surrenderConfirmOverlay == null)
            {
                return;
            }

            _surrenderConfirmOverlay.style.display = DisplayStyle.Flex;
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

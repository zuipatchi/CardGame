using Common.SoundManagement;
using Common.Store;
using Common.Username;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Title.Username
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class UsernameModalPresenter : MonoBehaviour
    {
        private UsernameRepository _usernameRepository;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private TextField _nameField;
        private Button _confirmButton;
        private Label _errorLabel;

        [Inject]
        public void Construct(UsernameRepository usernameRepository, SoundPlayer soundPlayer, SoundStore soundStore)
        {
            _usernameRepository = usernameRepository;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _overlay = root.Q<VisualElement>("Overlay");
            _nameField = root.Q<TextField>("NameField");
            _confirmButton = root.Q<Button>("ConfirmButton");
            _errorLabel = root.Q<Label>("ErrorLabel");

            _confirmButton.SetEnabled(false);
            _errorLabel.style.display = DisplayStyle.None;
            _nameField.RegisterValueChangedCallback(OnNameChanged);
            _confirmButton.RegisterCallback<ClickEvent>(OnConfirmClicked);
        }

        public void ShowModal()
        {
            _overlay.style.display = DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            _nameField?.UnregisterValueChangedCallback(OnNameChanged);
            _confirmButton?.UnregisterCallback<ClickEvent>(OnConfirmClicked);
            _overlay = null;
            _nameField = null;
            _confirmButton = null;
            _errorLabel = null;
        }

        private void OnNameChanged(ChangeEvent<string> evt)
        {
            bool valid = UsernameValidator.IsValid(evt.newValue, out string errorMessage);
            _confirmButton.SetEnabled(valid);
            _errorLabel.text = errorMessage;
            _errorLabel.style.display = string.IsNullOrEmpty(errorMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnConfirmClicked(ClickEvent evt)
        {
            string name = _nameField.value.Trim();
            if (!UsernameValidator.IsValid(name, out string _))
            {
                return;
            }
            _soundPlayer.PlaySE(_soundStore.EnterSE);
            _usernameRepository.Save(name);
            _overlay.style.display = DisplayStyle.None;
        }
    }
}

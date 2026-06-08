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

        private UIDocument _uiDocument;
        private VisualElement _overlay;
        private TextField _nameField;
        private Button _confirmButton;

        [Inject]
        public void Construct(UsernameRepository usernameRepository)
        {
            _usernameRepository = usernameRepository;
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

            _confirmButton.SetEnabled(false);
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
        }

        private void OnNameChanged(ChangeEvent<string> evt)
        {
            _confirmButton.SetEnabled(!string.IsNullOrWhiteSpace(evt.newValue?.Trim()));
        }

        private void OnConfirmClicked(ClickEvent evt)
        {
            string name = _nameField.value.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            _usernameRepository.Save(name);
            _overlay.style.display = DisplayStyle.None;
        }
    }
}

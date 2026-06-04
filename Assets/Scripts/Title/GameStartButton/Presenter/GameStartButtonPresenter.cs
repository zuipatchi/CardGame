using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Title.GameStartButton
{
    [RequireComponent(typeof(UIDocument))]
    public class GameStartButtonPresenter : MonoBehaviour
    {
        private SceneTransitioner _sceneTransitioner;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;
        [SerializeField] private Scenes _nextScene;

        private UIDocument _uiDocument;
        private VisualElement _touchArea;
        private Label _gameStartLabel;
        private IVisualElementScheduledItem _pulseTask;

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner, SoundPlayer soundPlayer, SoundStore soundStore)
        {
            _sceneTransitioner = sceneTransitioner;
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
            _touchArea = root.Q<VisualElement>("TouchArea");
            _gameStartLabel = root.Q<Label>("GameStartLabel");

            _touchArea.RegisterCallback<ClickEvent>(OnClick);
            _pulseTask = _gameStartLabel.schedule.Execute(TogglePulse).Every(900);
        }

        private void OnDisable()
        {
            _touchArea?.UnregisterCallback<ClickEvent>(OnClick);
            _pulseTask?.Pause();
            _touchArea = null;
            _gameStartLabel = null;
            _pulseTask = null;
        }

        private void TogglePulse()
        {
            _gameStartLabel.ToggleInClassList("game-start-label--dim");
        }

        private void OnClick(ClickEvent evt)
        {
            if (_soundStore.EnterSE != null)
            {
                _soundPlayer.PlaySE(_soundStore.EnterSE);
            }
            _sceneTransitioner.Transit(_nextScene).Forget();
        }
    }
}

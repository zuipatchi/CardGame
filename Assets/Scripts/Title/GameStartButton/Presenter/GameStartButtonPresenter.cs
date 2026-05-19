using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Title.GameStartButton
{
    // クリックされたらネクストシーンに遷移する
    [RequireComponent(typeof(UIDocument))]
    public class GameStartButtonPresenter : MonoBehaviour
    {
        private SceneTransitioner _sceneTransitioner;
        [SerializeField] private Scenes _nextScene;

        private UIDocument _uiDocument;
        private Button _gameStartButton;

        [Inject]
        public void Construct(SceneTransitioner sceneTransitioner)
        {
            _sceneTransitioner = sceneTransitioner;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _gameStartButton = root.Q<Button>("GameStartButton");
            if (_gameStartButton == null)
            {
                Debug.LogError("GameStartButton が見つかりませんでした。");
                return;
            }
            _gameStartButton.clicked += OnClickGameStart;

#if UNITY_EDITOR
            Button cpuDeckEditorButton = root.Q<Button>("CpuDeckEditorButton");
            if (cpuDeckEditorButton != null)
            {
                cpuDeckEditorButton.style.display = DisplayStyle.Flex;
                cpuDeckEditorButton.clicked += OnClickCpuDeckEditor;
            }
#endif
        }

        private void OnDisable()
        {
            if (_gameStartButton != null) _gameStartButton.clicked -= OnClickGameStart;
            _gameStartButton = null;
        }

        private void OnClickGameStart()
        {
            _gameStartButton.SetEnabled(false);
            _sceneTransitioner.Transit(_nextScene).Forget();
        }

#if UNITY_EDITOR
        private void OnClickCpuDeckEditor()
        {
            StartCoroutine(LoadCpuDeckEditorCoroutine());
        }

        private System.Collections.IEnumerator LoadCpuDeckEditorCoroutine()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                "CpuDeckBuilder", UnityEngine.SceneManagement.LoadSceneMode.Additive);

            UnityEngine.SceneManagement.Scene cpuScene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByName("CpuDeckBuilder");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(cpuScene);

            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(
                UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex((int)Scenes.Title));
        }
#endif
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Home
{
    public sealed class DogSpeechPresenter : MonoBehaviour, IAsyncStartable
    {
        [SerializeField]
        private DogSpeechLinesSO _lines;

        [SerializeField]
        private UIDocument _uiDocument;

        [SerializeField]
        private Transform _dogTransform;

        [SerializeField]
        private Vector3 _headOffset = new Vector3(0f, 2.5f, 0f);

        [SerializeField]
        private float _bubbleYOffset = 20f;

        [SerializeField]
        private float _charInterval = 0.05f;

        [SerializeField]
        private float _displayDuration = 3f;

        [SerializeField]
        private float _idleIntervalMin = 5f;

        [SerializeField]
        private float _idleIntervalMax = 15f;

        public bool IsRainy { get; set; }

        private VisualElement _speechBubble;
        private Label _speechLabel;
        private bool _isBubbleVisible;
        private CancellationTokenSource _speechCts;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _speechBubble = root.Q<VisualElement>("DogSpeechBubble");
            _speechLabel = root.Q<Label>("DogSpeechLabel");
        }

        private void OnDisable()
        {
            _speechBubble = null;
            _speechLabel = null;
        }

        private void OnDestroy()
        {
            _speechCts?.Cancel();
            _speechCts?.Dispose();
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (_lines == null || _lines.IdleMessages.Length == 0)
            {
                return;
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            string[] initialPool = IsRainy && _lines.RainMessages.Length > 0
                ? _lines.RainMessages
                : _lines.IdleMessages;
            ShowMessage(initialPool[UnityEngine.Random.Range(0, initialPool.Length)]);

            while (true)
            {
                float delay = UnityEngine.Random.Range(_idleIntervalMin, _idleIntervalMax);
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellation);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_isBubbleVisible)
                {
                    continue;
                }

                string[] pool = IsRainy && _lines.RainMessages.Length > 0
                    ? _lines.RainMessages
                    : _lines.IdleMessages;
                ShowMessage(pool[UnityEngine.Random.Range(0, pool.Length)]);
            }
        }

        public void ShowEatMessage()
        {
            if (_lines == null || _lines.EatMessages.Length == 0)
            {
                return;
            }
            ShowMessage(_lines.EatMessages[UnityEngine.Random.Range(0, _lines.EatMessages.Length)]);
        }

        public void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            _speechCts?.Cancel();
            _speechCts?.Dispose();
            _speechCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ShowMessageAsync(text, _speechCts.Token).Forget();
        }

        private async UniTaskVoid ShowMessageAsync(string text, CancellationToken token)
        {
            if (_speechLabel == null || _speechBubble == null)
            {
                return;
            }

            _speechLabel.text = "";
            ShowBubble();

            try
            {
                for (int i = 1; i <= text.Length; i++)
                {
                    _speechLabel.text = text.Substring(0, i);
                    await UniTask.Delay(TimeSpan.FromSeconds(_charInterval), cancellationToken: token);
                }
                _speechLabel.text = text;
                await UniTask.Delay(TimeSpan.FromSeconds(_displayDuration), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                HideBubble();
                return;
            }

            HideBubble();
        }

        private void Update()
        {
            if (_isBubbleVisible && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                _speechCts?.Cancel();
            }

            if (!_isBubbleVisible || _dogTransform == null || Camera.main == null)
            {
                return;
            }
            if (_speechBubble?.panel == null)
            {
                return;
            }

            Vector3 worldPos = _dogTransform.position + _headOffset;
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                _speechBubble.panel, worldPos, Camera.main);

            float bubbleWidth = _speechBubble.resolvedStyle.width;
            float bubbleHeight = _speechBubble.resolvedStyle.height;

            _speechBubble.style.left = panelPos.x - bubbleWidth * 0.5f;
            _speechBubble.style.top = panelPos.y - bubbleHeight - _bubbleYOffset;
        }

        private void ShowBubble()
        {
            if (_speechBubble == null)
            {
                return;
            }
            _speechBubble.style.display = DisplayStyle.Flex;
            _isBubbleVisible = true;
        }

        private void HideBubble()
        {
            if (_speechBubble == null)
            {
                return;
            }
            _speechBubble.style.display = DisplayStyle.None;
            _isBubbleVisible = false;
        }
    }
}

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Home
{
    public sealed class HomeBackgroundPresenter : MonoBehaviour
    {
        [SerializeField]
        private Camera _camera;

        [SerializeField]
        [Range(0f, 1f)]
        private float _alpha = 1f;

        private const string BackgroundAddress = "Image/HomeBackground";
        private const string RainBackgroundAddress = "Image/HomeBackgroundRain";
        private const float BackgroundDepth = 20f;

        public bool IsRainy { get; set; }

        private void Start()
        {
            LoadBackgroundAsync().Forget();
        }

        private async UniTaskVoid LoadBackgroundAsync()
        {
            if (_camera == null)
            {
                Debug.LogError("Camera が未アサインです");
                return;
            }

            string address = IsRainy ? RainBackgroundAddress : BackgroundAddress;
            Sprite sprite;
            try
            {
                sprite = await Addressables.LoadAssetAsync<Sprite>(address)
                    .ToUniTask(cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"HomeBackground ロード失敗: {e.Message}");
                return;
            }

            transform.position = _camera.transform.position + _camera.transform.forward * BackgroundDepth;
            transform.rotation = _camera.transform.rotation;

            float visibleHeight = _camera.orthographic
                ? _camera.orthographicSize * 2f
                : 2f * BackgroundDepth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * _camera.aspect;

            Vector3 spriteSize = sprite.bounds.size;
            transform.localScale = new Vector3(
                visibleWidth / spriteSize.x,
                visibleHeight / spriteSize.y,
                1f
            );

            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -100;
            sr.color = new Color(1f, 1f, 1f, _alpha);
        }
    }
}

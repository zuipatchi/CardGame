using System;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Title.Background
{
    public sealed class TitleBackgroundPresenter : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] [Range(0f, 1f)] private float _alpha = 0.5f;

        private const string BackgroundAddress = "Image/TitleBackground";

        private void Start()
        {
            LoadBackground().Forget();
        }

        private async UniTask LoadBackground()
        {
            if (_camera == null)
            {
                Debug.LogError("Camera が未アサインです");
                return;
            }

            Sprite sprite;
            try
            {
                sprite = await Addressables.LoadAssetAsync<Sprite>(BackgroundAddress).ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"TitleBackground ロード失敗: {e.Message}");
                return;
            }

            transform.position = _camera.transform.position + _camera.transform.forward * BackgroundConstants.Depth;
            transform.rotation = _camera.transform.rotation;

            float visibleHeight = _camera.orthographic
                ? _camera.orthographicSize * 2f
                : 2f * BackgroundConstants.Depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
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

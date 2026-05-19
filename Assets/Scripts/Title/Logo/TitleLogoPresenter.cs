using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Title.Logo
{
    public sealed class TitleLogoPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _sphereRoot;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _scale = 1f;

        private const string LogoAddress = "Image/Logo";

        private void Start()
        {
            LoadLogo().Forget();
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            transform.rotation = _camera.transform.rotation;
        }

        private async UniTask LoadLogo()
        {
            Sprite sprite;
            try
            {
                sprite = await Addressables.LoadAssetAsync<Sprite>(LogoAddress).ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"Logo ロード失敗: {e.Message}");
                return;
            }

            if (_sphereRoot != null)
            {
                transform.position = _sphereRoot.position;
            }

            transform.localScale = Vector3.one * _scale;

            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
        }
    }
}

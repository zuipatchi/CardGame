using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Common.Store
{
    public class ModalStore : IStartable
    {
        private readonly UniTaskCompletionSource _loadedTcs = new();
        public UniTask Loaded => _loadedTcs.Task;

        // アドレス
        private readonly string _modalAddressable = "Modal/OptionModal";

        // プロパティ
        public VisualTreeAsset Modal => _modal;

        // メンバー
        private VisualTreeAsset _modal;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _modal = await Addressables.LoadAssetAsync<VisualTreeAsset>(_modalAddressable).ToUniTask();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"モーダルアセットのロードに失敗: {e}");
                _loadedTcs.TrySetException(e);
            }
        }
    }
}

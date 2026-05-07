using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Common.Store
{
    public class SoundStore : IStartable
    {
        private readonly UniTaskCompletionSource _loadedTcs = new();
        public UniTask Loaded => _loadedTcs.Task;

        // アドレス
        private readonly string _mainBgmAddressable = "Sound/BGM/CatInPalmBeach";
        private readonly string _soleAddressable = "Sole";
        private readonly string _titleBGMAddressable = "Sound/BGM/Title";

        // プロパティ
        public AudioClip MainBGM => _mainBGM;
        public AudioClip Sole => _sole;
        public AudioClip TitleBGM => _titleBGM;

        // メンバー
        private AudioClip _mainBGM = null;
        private AudioClip _sole = null;
        private AudioClip _titleBGM = null;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _mainBGM = await Addressables.LoadAssetAsync<AudioClip>(_mainBgmAddressable).ToUniTask();
                _sole = await Addressables.LoadAssetAsync<AudioClip>(_soleAddressable).ToUniTask();
                _titleBGM = await Addressables.LoadAssetAsync<AudioClip>(_titleBGMAddressable).ToUniTask();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"サウンドアセットのロードに失敗: {e}");
                _loadedTcs.TrySetException(e);
            }
        }
    }
}

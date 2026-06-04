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
        private readonly string _titleBGMAddressable = "Sound/BGM/Title";
        private readonly string _enterSEAddressable = "Sound/SE/Enter";

        // プロパティ
        public AudioClip MainBGM => _mainBGM;
        public AudioClip TitleBGM => _titleBGM;
        public AudioClip EnterSE => _enterSE;

        // メンバー
        private AudioClip _mainBGM = null;
        private AudioClip _titleBGM = null;
        private AudioClip _enterSE = null;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _mainBGM = await Addressables.LoadAssetAsync<AudioClip>(_mainBgmAddressable).ToUniTask();
                _titleBGM = await Addressables.LoadAssetAsync<AudioClip>(_titleBGMAddressable).ToUniTask();
                _enterSE = await Addressables.LoadAssetAsync<AudioClip>(_enterSEAddressable).ToUniTask();
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

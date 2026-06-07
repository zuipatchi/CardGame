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
        private readonly string _maouOrchestraAddressable = "Sound/BGM/MaouOrchestra";
        private readonly string _koharuIzmAddressable = "Sound/BGM/KoharuIzm";
        private readonly string _enterSEAddressable = "Sound/SE/Enter1";
        private readonly string _enter2SEAddressable = "Sound/SE/Enter2";
        private readonly string _cancel1SEAddressable = "Sound/SE/Cancel1";
        private readonly string _resultSEAddressable = "Sound/SE/Result";

        // プロパティ
        public AudioClip MainBGM => _mainBGM;
        public AudioClip MaouOrchestra => _maouOrchestra;
        public AudioClip KoharuIzm => _koharuIzm;
        public AudioClip EnterSE => _enterSE;
        public AudioClip Enter2SE => _enter2SE;
        public AudioClip Cancel1SE => _cancel1SE;
        public AudioClip ResultSE => _resultSE;

        // メンバー
        private AudioClip _mainBGM = null;
        private AudioClip _maouOrchestra = null;
        private AudioClip _koharuIzm = null;
        private AudioClip _enterSE = null;
        private AudioClip _enter2SE = null;
        private AudioClip _cancel1SE = null;
        private AudioClip _resultSE = null;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _mainBGM = await Addressables.LoadAssetAsync<AudioClip>(_mainBgmAddressable).ToUniTask();
                _maouOrchestra = await Addressables.LoadAssetAsync<AudioClip>(_maouOrchestraAddressable).ToUniTask();
                _koharuIzm = await Addressables.LoadAssetAsync<AudioClip>(_koharuIzmAddressable).ToUniTask();
                _enterSE = await Addressables.LoadAssetAsync<AudioClip>(_enterSEAddressable).ToUniTask();
                _enter2SE = await Addressables.LoadAssetAsync<AudioClip>(_enter2SEAddressable).ToUniTask();
                _cancel1SE = await Addressables.LoadAssetAsync<AudioClip>(_cancel1SEAddressable).ToUniTask();
                _resultSE = await Addressables.LoadAssetAsync<AudioClip>(_resultSEAddressable).ToUniTask();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"サウンドアセットのロードに失敗: {e}");
                _loadedTcs.TrySetResult();
            }
        }
    }
}

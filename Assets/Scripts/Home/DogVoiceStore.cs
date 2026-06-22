using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Home
{
    /// <summary>
    /// 犬のセリフ読み上げ音声（事前生成した WAV）を Addressables アドレス "Voice/Dog/{key}" から
    /// オンデマンドでロード・キャッシュするクラス。カード用の FlavorVoiceStore と同じ方針。
    /// 音声が未生成（アドレス未登録）のセリフは null を返し、無音にする。
    /// </summary>
    public sealed class DogVoiceStore
    {
        // ロード済み（および未登録で null 確定した）クリップのキャッシュ。セリフのキーをキーにする。
        private readonly Dictionary<string, AudioClip> _cache = new();

        public async UniTask<AudioClip> LoadAsync(string rawLine)
        {
            string key = DogVoice.Key(rawLine);
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (_cache.TryGetValue(key, out AudioClip cached))
            {
                return cached;
            }

            string address = DogVoice.AddressPrefix + key;

            // アドレスが存在しないと LoadAssetAsync は例外を投げるため、先に存在を確認する。
            IList<IResourceLocation> locations =
                await Addressables.LoadResourceLocationsAsync(address, typeof(AudioClip)).ToUniTask();
            if (locations == null || locations.Count == 0)
            {
                _cache[key] = null;
                return null;
            }

            AudioClip clip = await Addressables.LoadAssetAsync<AudioClip>(address).ToUniTask();
            _cache[key] = clip;
            return clip;
        }
    }
}

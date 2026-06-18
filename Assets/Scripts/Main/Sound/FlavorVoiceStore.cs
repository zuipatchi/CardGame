using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Main.Sound
{
    /// <summary>
    /// カードのフレーバーテキスト読み上げ音声（事前生成した WAV）を
    /// Addressables アドレス "Voice/{CardId}" からオンデマンドでロード・キャッシュするクラス。
    /// 全カード分を起動時にロードすると無駄が大きいため、再生時に必要なものだけ読み込む。
    /// 音声が未生成（アドレス未登録）のカードは null を返し、無音にする。
    /// </summary>
    public sealed class FlavorVoiceStore
    {
        private const string AddressPrefix = "Voice/";

        // ロード済み（および未登録で null 確定した）クリップのキャッシュ。CardId をキーにする。
        private readonly Dictionary<string, AudioClip> _cache = new();

        public async UniTask<AudioClip> LoadAsync(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            if (_cache.TryGetValue(cardId, out AudioClip cached))
            {
                return cached;
            }

            string address = AddressPrefix + cardId;

            // アドレスが存在しないと LoadAssetAsync は例外を投げるため、先に存在を確認する。
            IList<IResourceLocation> locations =
                await Addressables.LoadResourceLocationsAsync(address, typeof(AudioClip)).ToUniTask();
            if (locations == null || locations.Count == 0)
            {
                _cache[cardId] = null;
                return null;
            }

            AudioClip clip = await Addressables.LoadAssetAsync<AudioClip>(address).ToUniTask();
            _cache[cardId] = clip;
            return clip;
        }
    }
}

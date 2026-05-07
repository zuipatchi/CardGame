using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Main.Card
{
    public sealed class CardStore : IStartable
    {
        private readonly UniTaskCompletionSource _loadedTcs = new();
        public UniTask Loaded => _loadedTcs.Task;

        private readonly string _cardTemplateAddressable = "Card/CardTemplate";
        private readonly string _cardBackAddressable = "Image/CardBack";

        public VisualTreeAsset CardTemplate => _cardTemplate;
        public Texture2D CardBack => _cardBack;

        private VisualTreeAsset _cardTemplate;
        private Texture2D _cardBack;

        public void Start()
        {
            LoadAssets().Forget();
        }

        private async UniTask LoadAssets()
        {
            try
            {
                _cardTemplate = await Addressables.LoadAssetAsync<VisualTreeAsset>(_cardTemplateAddressable).ToUniTask();
                _cardBack = await Addressables.LoadAssetAsync<Texture2D>(_cardBackAddressable).ToUniTask();
                _loadedTcs.TrySetResult();
            }
            catch (Exception e)
            {
                Debug.LogError($"カードアセットのロードに失敗: {e}");
                _loadedTcs.TrySetException(e);
            }
        }
    }
}

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
        private readonly string _battleFieldAddressable = "Image/BattleField";
        private readonly string _deckBuilderBackgroundAddressable = "Image/DeckBuilderBackground";
        private readonly string _attributeDatabaseAddressable = "Card/AttributeDatabase";
        private readonly string _coinFrontAddressable = "Image/Coin1";
        private readonly string _coinBackAddressable = "Image/Coin2";
        private readonly string _cpuDeckAddressable = "Card/CpuDeck";
        private readonly string _yesButtonAddressable = "Image/YesButton";
        private readonly string _noButtonAddressable = "Image/NoButton";

        public VisualTreeAsset CardTemplate => _cardTemplate;
        public Texture2D CardBack => _cardBack;
        public Texture2D BattleField => _battleField;
        public Texture2D DeckBuilderBackground => _deckBuilderBackground;
        public AttributeDatabaseSO AttributeDatabase => _attributeDatabase;
        public Sprite CoinFront => _coinFront;
        public Sprite CoinBack => _coinBack;
        public CpuDeckSO CpuDeck => _cpuDeck;
        public Texture2D YesButtonImage => _yesButtonImage;
        public Texture2D NoButtonImage => _noButtonImage;

        private VisualTreeAsset _cardTemplate;
        private Texture2D _cardBack;
        private Texture2D _battleField;
        private Texture2D _deckBuilderBackground;
        private AttributeDatabaseSO _attributeDatabase;
        private Sprite _coinFront;
        private Sprite _coinBack;
        private CpuDeckSO _cpuDeck;
        private Texture2D _yesButtonImage;
        private Texture2D _noButtonImage;

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
                _battleField = await Addressables.LoadAssetAsync<Texture2D>(_battleFieldAddressable).ToUniTask();
                _deckBuilderBackground = await Addressables.LoadAssetAsync<Texture2D>(_deckBuilderBackgroundAddressable).ToUniTask();

                try
                {
                    _attributeDatabase = await Addressables.LoadAssetAsync<AttributeDatabaseSO>(_attributeDatabaseAddressable).ToUniTask();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"属性アイコンデータベースのロードをスキップ: {e.Message}");
                }

                _coinFront = await Addressables.LoadAssetAsync<Sprite>(_coinFrontAddressable).ToUniTask();
                _coinBack = await Addressables.LoadAssetAsync<Sprite>(_coinBackAddressable).ToUniTask();

                try
                {
                    _cpuDeck = await Addressables.LoadAssetAsync<CpuDeckSO>(_cpuDeckAddressable).ToUniTask();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"CPUデッキデータのロードをスキップ: {e.Message}");
                }

                _yesButtonImage = await Addressables.LoadAssetAsync<Texture2D>(_yesButtonAddressable).ToUniTask();
                _noButtonImage = await Addressables.LoadAssetAsync<Texture2D>(_noButtonAddressable).ToUniTask();

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

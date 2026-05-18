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
        private readonly string _attributeIconDatabaseAddressable = "Card/AttributeIconDatabase";

        public VisualTreeAsset CardTemplate => _cardTemplate;
        public Texture2D CardBack => _cardBack;
        public Texture2D BattleField => _battleField;
        public Texture2D DeckBuilderBackground => _deckBuilderBackground;
        public AttributeDatabaseSO AttributeDatabase => _attributeDatabase;

        private VisualTreeAsset _cardTemplate;
        private Texture2D _cardBack;
        private Texture2D _battleField;
        private Texture2D _deckBuilderBackground;
        private AttributeDatabaseSO _attributeDatabase;

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
                    _attributeDatabase = await Addressables.LoadAssetAsync<AttributeDatabaseSO>(_attributeIconDatabaseAddressable).ToUniTask();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"属性アイコンデータベースのロードをスキップ: {e.Message}");
                }

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

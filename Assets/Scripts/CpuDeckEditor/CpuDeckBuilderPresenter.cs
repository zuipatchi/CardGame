using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using DeckBuilder;
using Main.Card;
using VContainer;

namespace CpuDeckBuilder
{
    public sealed class CpuDeckBuilderPresenter : DeckBuilderPresenterBase
    {
        private const string CpuDeckAssetPath = "Assets/Data/CpuDeck.asset";

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = new DeckModel();
            _sceneTransitioner = sceneTransitioner;
        }

        protected override void InitializeDeck()
        {
            _deckModel.Clear();
#if UNITY_EDITOR
            CpuDeckSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<CpuDeckSO>(CpuDeckAssetPath);
            if (so != null)
            {
                foreach (string id in so.CardIds)
                {
                    if (_cardDatabase.TryGet(id, out CardData card))
                    {
                        _deckModel.Add(id, card.Cost);
                    }
                }
            }
#endif
        }

        protected override void SaveDeck()
        {
#if UNITY_EDITOR
            CpuDeckSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<CpuDeckSO>(CpuDeckAssetPath);
            if (so == null)
            {
                return;
            }
            so.CardIds.Clear();
            so.CardIds.AddRange(_deckModel.CardIds);
            UnityEditor.EditorUtility.SetDirty(so);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        protected override void NavigateBack()
        {
            _sceneTransitioner.Transit(Scenes.Title).Forget();
        }
    }
}

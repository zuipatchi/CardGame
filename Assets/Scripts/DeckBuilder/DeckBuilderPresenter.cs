using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Card;
using VContainer;

namespace DeckBuilder
{
    public sealed class DeckBuilderPresenter : DeckBuilderPresenterBase
    {
        private DeckRepository _deckRepository;

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            DeckModel deckModel,
            DeckRuleModel deckRuleModel,
            DeckRepository deckRepository,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _deckRuleModel = deckRuleModel;
            _deckRepository = deckRepository;
            _sceneTransitioner = sceneTransitioner;
        }

        protected override void InitializeDeck()
        {
            _deckModel.Clear();
            _deckRepository.Load(_deckModel);
        }

        protected override void SaveDeck()
        {
            _deckRepository.Save(_deckModel);
        }

        protected override void NavigateBack()
        {
            _sceneTransitioner.Transit(Scenes.Home).Forget();
        }
    }
}

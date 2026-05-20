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
        private CpuDeckRepository _cpuDeckRepository;

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            CpuDeckRepository cpuDeckRepository,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = new DeckModel();
            _cpuDeckRepository = cpuDeckRepository;
            _sceneTransitioner = sceneTransitioner;
        }

        protected override void InitializeDeck()
        {
            _deckModel.Clear();
            _cpuDeckRepository.Load(_deckModel);
        }

        protected override void SaveDeck()
        {
            _cpuDeckRepository.Save(_deckModel);
        }

        protected override void NavigateBack()
        {
            _sceneTransitioner.Transit(Scenes.Title).Forget();
        }
    }
}

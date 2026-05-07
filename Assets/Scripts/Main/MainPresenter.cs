using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Main
{
    public sealed class MainPresenter : MonoBehaviour, IStartable
    {
        private CardStore _cardStore;
        private CardDatabase _cardDatabase;

        [Inject]
        public void Construct(CardStore cardStore, CardDatabase cardDatabase)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
        }

        void IStartable.Start()
        {
            BuildAsync().Forget();
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded;

                VisualElement deckArea = GetComponent<UIDocument>()
                    .rootVisualElement
                    .Q<VisualElement>("DeckArea");

                CardData[] deckCards = _cardDatabase.AllCards.ToArray();

                DeckView deckView = new DeckView(_cardStore.CardTemplate, deckCards, _cardStore.CardBack);
                deckArea.Add(deckView);
            }
            catch (OperationCanceledException) { }
        }
    }
}

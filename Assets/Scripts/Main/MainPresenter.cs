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
        private const int InitialHandSize = 5;

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

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                VisualElement mainRoot = root.Q<VisualElement>("MainRoot");
                VisualElement deckArea = root.Q<VisualElement>("DeckArea");
                VisualElement handArea = root.Q<VisualElement>("HandArea");
                VisualElement opponentFieldArea = root.Q<VisualElement>("OpponentFieldArea");
                VisualElement playerFieldArea = root.Q<VisualElement>("PlayerFieldArea");

                VisualElement dragLayer = new VisualElement();
                dragLayer.AddToClassList("main-drag-layer");
                dragLayer.pickingMode = PickingMode.Ignore;
                mainRoot.Add(dragLayer);

                CardData[] shuffled = Shuffle(_cardDatabase.AllCards.ToArray());
                int handSize = Mathf.Min(InitialHandSize, shuffled.Length);
                CardData[] handCards = shuffled.Take(handSize).ToArray();
                CardData[] deckCards = shuffled.Skip(handSize).ToArray();

                FieldView opponentFieldView = new FieldView();
                opponentFieldArea.Add(opponentFieldView);

                FieldView playerFieldView = new FieldView();
                playerFieldArea.Add(playerFieldView);

                HandView handView = new HandView(_cardStore.CardTemplate, handCards, _cardStore.CardBack, dragLayer);
                handArea.Add(handView);
                handView.OnCardDropped = (card, worldPos) => playerFieldView.TryPlace(card, worldPos);

                DeckView deckView = new DeckView(_cardStore.CardTemplate, deckCards, _cardStore.CardBack);
                deckArea.Add(deckView);
            }
            catch (OperationCanceledException) { }
        }

        private static CardData[] Shuffle(CardData[] cards)
        {
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }

            return cards;
        }
    }
}

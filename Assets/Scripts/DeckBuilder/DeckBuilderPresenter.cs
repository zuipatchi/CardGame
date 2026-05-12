using System;
using System.Collections.Generic;
using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace DeckBuilder
{
    public sealed class DeckBuilderPresenter : MonoBehaviour, IStartable
    {
        private CardStore _cardStore;
        private CardDatabase _cardDatabase;
        private DeckModel _deckModel;
        private DeckRepository _deckRepository;
        private SceneTransitioner _sceneTransitioner;

        private ScrollView _cardListScrollView;
        private ScrollView _deckListScrollView;
        private Label _deckCountLabel;
        private Button _startButton;

        [Inject]
        public void Construct(
            CardStore cardStore,
            CardDatabase cardDatabase,
            DeckModel deckModel,
            DeckRepository deckRepository,
            SceneTransitioner sceneTransitioner)
        {
            _cardStore = cardStore;
            _cardDatabase = cardDatabase;
            _deckModel = deckModel;
            _deckRepository = deckRepository;
            _sceneTransitioner = sceneTransitioner;
        }

        void IStartable.Start()
        {
            BuildAsync().Forget();
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded.AttachExternalCancellation(destroyCancellationToken);

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                _cardListScrollView = root.Q<ScrollView>("CardListScrollView");
                _deckListScrollView = root.Q<ScrollView>("DeckListScrollView");
                _deckCountLabel = root.Q<Label>("DeckCountLabel");
                _startButton = root.Q<Button>("StartButton");
                Button backButton = root.Q<Button>("BackButton");

                backButton.clicked += () => _sceneTransitioner.Transit(Scenes.Title).Forget();
                _startButton.clicked += OnStartClicked;

                _deckModel.Clear();
                _deckRepository.Load(_deckModel);

                _cardListScrollView.Clear();
                IReadOnlyList<CardData> allCards = _cardDatabase.AllCards;
                foreach (CardData cardData in allCards)
                {
                    CardView cardView = new CardView(_cardStore.CardTemplate, cardData);
                    cardView.AddToClassList("deckbuilder-card-item");
                    CardData captured = cardData;
                    cardView.RegisterCallback<ClickEvent>(_ => OnCardListItemClicked(captured));
                    _cardListScrollView.Add(cardView);
                }

                RefreshDeckPanel();
            }
            catch (OperationCanceledException) { }
        }

        private void OnCardListItemClicked(CardData cardData)
        {
            if (!_deckModel.TryAdd(cardData.Id))
            {
                return;
            }

            RefreshDeckPanel();
        }

        private void OnRemoveClicked(string id)
        {
            _deckModel.Remove(id);
            RefreshDeckPanel();
        }

        private void OnStartClicked()
        {
            _deckRepository.Save(_deckModel);
            _startButton.SetEnabled(false);
            _sceneTransitioner.Transit(Scenes.Main).Forget();
        }

        private void RefreshDeckPanel()
        {
            _deckListScrollView.Clear();
            _deckCountLabel.text = $"{_deckModel.Count}/{DeckModel.MaxSize}";
            _cardListScrollView.EnableInClassList("deckbuilder-card-scroll--full", _deckModel.IsFull);

            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (string id in _deckModel.CardIds)
            {
                if (!counts.ContainsKey(id))
                {
                    counts[id] = 0;
                    order.Add(id);
                }
                counts[id]++;
            }

            foreach (string id in order)
            {
                if (!_cardDatabase.TryGet(id, out CardData cardData))
                {
                    continue;
                }

                VisualElement row = new VisualElement();
                row.AddToClassList("deckbuilder-deck-row");

                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("deckbuilder-deck-row-thumbnail");
                if (cardData.Image != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(cardData.Image);
                }

                Label nameLabel = new Label(cardData.CardName);
                nameLabel.AddToClassList("deckbuilder-deck-row-name");

                Label countLabel = new Label($"×{counts[id]}");
                countLabel.AddToClassList("deckbuilder-deck-row-count");

                Button removeButton = new Button();
                removeButton.text = "×";
                removeButton.AddToClassList("deckbuilder-deck-row-remove");
                string capturedId = id;
                removeButton.clicked += () => OnRemoveClicked(capturedId);

                row.Add(thumbnail);
                row.Add(nameLabel);
                row.Add(countLabel);
                row.Add(removeButton);
                _deckListScrollView.Add(row);
            }

            _startButton.SetEnabled(_deckModel.IsReady);
        }
    }
}

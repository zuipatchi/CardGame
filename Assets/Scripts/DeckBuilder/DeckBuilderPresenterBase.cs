using System;
using System.Collections.Generic;
using System.Linq;
using Common.Deck;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace DeckBuilder
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class DeckBuilderPresenterBase : MonoBehaviour, IStartable
    {
        protected CardStore _cardStore;
        protected CardDatabase _cardDatabase;
        protected SceneTransitioner _sceneTransitioner;
        protected DeckModel _deckModel;

        private ScrollView _cardListScrollView;
        private ScrollView _deckListScrollView;
        private Label _deckCountLabel;
        private Label _deckCardCountLabel;
        private Button _clearDeckButton;
        private Label _costOverLabel;
        private VisualElement _cardListDragLayer;
        private CardDetailModal _cardDetailModal;
        private DeckAnalysisModal _deckAnalysisModal;
        private bool _started;

        void IStartable.Start()
        {
            if (_started)
            {
                return;
            }
            _started = true;
            BuildAsync().Forget();
        }

        private async UniTaskVoid BuildAsync()
        {
            try
            {
                await _cardStore.Loaded.AttachExternalCancellation(destroyCancellationToken);

                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                VisualElement deckBuilderRoot = root.Q<VisualElement>("DeckBuilderRoot");
                if (_cardStore.DeckBuilderBackground != null)
                {
                    deckBuilderRoot.style.backgroundImage = new StyleBackground(_cardStore.DeckBuilderBackground);
                }
                _cardListScrollView = root.Q<ScrollView>("CardListScrollView");
                _deckListScrollView = root.Q<ScrollView>("DeckListScrollView");
                _deckCountLabel = root.Q<Label>("DeckCountLabel");
                _deckCardCountLabel = root.Q<Label>("DeckCardCountLabel");
                _clearDeckButton = root.Q<Button>("ClearDeckButton");
                _costOverLabel = root.Q<Label>("CostOverLabel");
                Button backButton = root.Q<Button>("BackButton");

                backButton.clicked += NavigateBack;
                _clearDeckButton.clicked += OnClearDeckClicked;

                _cardListDragLayer = new VisualElement();
                _cardListDragLayer.AddToClassList("deckbuilder-drag-layer");
                _cardListDragLayer.pickingMode = PickingMode.Ignore;
                _cardDetailModal = new CardDetailModal(deckBuilderRoot, _cardStore.AttributeDatabase);
                _deckAnalysisModal = new DeckAnalysisModal(deckBuilderRoot, _cardDatabase);

                Button analyzeButton = root.Q<Button>("AnalyzeButton");
                analyzeButton.clicked += () => _deckAnalysisModal.Show(_deckModel);

                InitializeDeck();

                _cardListScrollView.Clear();
                IReadOnlyList<CardData> allCards = _cardDatabase.AllCards;
                AddCardSection("キャラ", allCards.OfType<CharacterCardData>(), "deckbuilder-section-header--character");
                AddCardSection("スキル", allCards.OfType<SkillCardData>(), "deckbuilder-section-header--skill");
                AddCardSection("イベント", allCards.OfType<EventCardData>(), "deckbuilder-section-header--event");

                RefreshDeckPanel();
                deckBuilderRoot.Add(_cardListDragLayer);
            }
            catch (OperationCanceledException) { }
        }

        protected abstract void InitializeDeck();
        protected abstract void SaveDeck();
        protected abstract void NavigateBack();

        private void AddCardSection(string title, IEnumerable<CardData> cards, string modifierClass)
        {
            List<CardData> cardList = new List<CardData>(cards);
            if (cardList.Count == 0)
            {
                return;
            }

            Label header = new Label(title);
            header.AddToClassList("deckbuilder-section-header");
            header.AddToClassList(modifierClass);
            _cardListScrollView.Add(header);

            VisualElement grid = new VisualElement();
            grid.AddToClassList("deckbuilder-section-cards");
            foreach (CardData cardData in cardList)
            {
                CardView cardView = new CardView(_cardStore.CardTemplate, cardData, attrIconDb: _cardStore.AttributeDatabase);
                cardView.AddToClassList("deckbuilder-card-item");
                CardData captured = cardData;
                CardDragManipulator manipulator = new CardDragManipulator(_cardListDragLayer);
                manipulator.CreateGhost = () =>
                {
                    CardView ghost = new CardView(_cardStore.CardTemplate, captured, attrIconDb: _cardStore.AttributeDatabase);
                    ghost.AddToClassList("deckbuilder-card-item");
                    ghost.style.opacity = 0.75f;
                    ghost.pickingMode = PickingMode.Ignore;
                    return ghost;
                };
                manipulator.OnDrop = worldPos =>
                {
                    if (_deckListScrollView.worldBound.Contains(worldPos))
                    {
                        _deckModel.Add(captured.Id, captured.Cost);
                        RefreshDeckPanel();
                        SaveDeck();
                    }
                    return false;
                };
                manipulator.OnClick = () => _cardDetailModal.Show(captured);
                cardView.AttachDragManipulator(manipulator);
                grid.Add(cardView);
            }
            _cardListScrollView.Add(grid);
        }

        private void OnRemoveClicked(string id)
        {
            _deckModel.Remove(id);
            RefreshDeckPanel();
            SaveDeck();
        }

        private void OnClearDeckClicked()
        {
            _deckModel.Clear();
            RefreshDeckPanel();
            SaveDeck();
        }

        private void RefreshDeckPanel()
        {
            _deckListScrollView.Clear();
            _deckCountLabel.text = $"{_deckModel.TotalCost}/{DeckModel.TargetCost}";
            _deckCardCountLabel.text = $"{_deckModel.Count}枚";

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

                Label countLabel = new Label($"{counts[id]}");
                countLabel.AddToClassList("deckbuilder-deck-row-count");

                Button subtractButton = new Button();
                subtractButton.text = "－";
                subtractButton.AddToClassList("deckbuilder-deck-row-subtract");
                string capturedId = id;
                subtractButton.clicked += () => OnRemoveClicked(capturedId);

                Button addButton = new Button();
                addButton.text = "＋";
                addButton.AddToClassList("deckbuilder-deck-row-add");
                int capturedCost = cardData.Cost;
                addButton.clicked += () =>
                {
                    _deckModel.Add(capturedId, capturedCost);
                    RefreshDeckPanel();
                    SaveDeck();
                };

                row.Add(thumbnail);
                row.Add(nameLabel);
                row.Add(subtractButton);
                row.Add(countLabel);
                row.Add(addButton);
                _deckListScrollView.Add(row);
            }

            _clearDeckButton.style.display = _deckModel.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            bool isOver = _deckModel.IsOver;
            _costOverLabel.style.display = isOver ? DisplayStyle.Flex : DisplayStyle.None;
            if (isOver)
            {
                _deckCountLabel.AddToClassList("deckbuilder-deck-count--over");
            }
            else
            {
                _deckCountLabel.RemoveFromClassList("deckbuilder-deck-count--over");
            }
        }
    }
}

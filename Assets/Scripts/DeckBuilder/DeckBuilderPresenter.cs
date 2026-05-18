using System;
using System.Collections.Generic;
using System.Linq;
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
        private Button _saveButton;
        private Button _startButton;
        private Button _clearDeckButton;
        private VisualElement _cardListDragLayer;
        private CardDetailModal _cardDetailModal;
        private bool _started;

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
                _saveButton = root.Q<Button>("SaveButton");
                _startButton = root.Q<Button>("StartButton");
                _clearDeckButton = root.Q<Button>("ClearDeckButton");
                Button backButton = root.Q<Button>("BackButton");

                backButton.clicked += () => _sceneTransitioner.Transit(Scenes.Title).Forget();
                _saveButton.clicked += OnSaveClicked;
                _startButton.clicked += OnStartClicked;
                _clearDeckButton.clicked += OnClearDeckClicked;

                _cardListDragLayer = new VisualElement();
                _cardListDragLayer.AddToClassList("deckbuilder-drag-layer");
                _cardListDragLayer.pickingMode = PickingMode.Ignore;
                _cardDetailModal = new CardDetailModal(deckBuilderRoot, _cardStore.AttributeIconDatabase);

                _deckModel.Clear();
                _deckRepository.Load(_deckModel);

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
                CardView cardView = new CardView(_cardStore.CardTemplate, cardData, attrIconDb: _cardStore.AttributeIconDatabase);
                cardView.AddToClassList("deckbuilder-card-item");
                CardData captured = cardData;
                CardDragManipulator manipulator = new CardDragManipulator(_cardListDragLayer);
                manipulator.CreateGhost = () =>
                {
                    CardView ghost = new CardView(_cardStore.CardTemplate, captured, attrIconDb: _cardStore.AttributeIconDatabase);
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
        }

        private void OnClearDeckClicked()
        {
            _deckModel.Clear();
            RefreshDeckPanel();
        }

        private void OnSaveClicked()
        {
            _deckRepository.Save(_deckModel);
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
            _deckCountLabel.text = $"{_deckModel.TotalCost}/{DeckModel.TargetCost}";

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

            _saveButton.SetEnabled(_deckModel.Count > 0);
            _startButton.SetEnabled(_deckModel.IsReady);
            _clearDeckButton.style.display = _deckModel.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

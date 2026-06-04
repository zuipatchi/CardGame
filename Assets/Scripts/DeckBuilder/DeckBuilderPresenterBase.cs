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

        private enum CardTypeFilter { All, Character, Skill, Event }

        private VisualElement _deckBuilderRoot;
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
        private readonly List<string> _deckRowOrder = new List<string>();
        private string _draggingId;
        private VisualElement _reorderDragHandle;
        private int _reorderDragPointerId = -1;
        private VisualElement _reorderGhost;
        private VisualElement _reorderIndicator;
        private int _reorderInsertIndex;
        private CardTypeFilter _cardTypeFilter = CardTypeFilter.All;
        private VisualElement _characterCardSection;
        private VisualElement _skillCardSection;
        private VisualElement _eventCardSection;
        private Button _filterCharacterButton;
        private Button _filterSkillButton;
        private Button _filterEventButton;

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
                VisualElement root = GetComponent<UIDocument>().rootVisualElement;
                _deckBuilderRoot = root.Q<VisualElement>("DeckBuilderRoot");

                VisualElement loadingOverlay = new VisualElement();
                loadingOverlay.AddToClassList("deckbuilder-loading-overlay");
                Label loadingLabel = new Label("読み込み中...");
                loadingLabel.AddToClassList("deckbuilder-loading-label");
                loadingLabel.pickingMode = PickingMode.Ignore;
                loadingOverlay.Add(loadingLabel);
                _deckBuilderRoot.Add(loadingOverlay);

                await _cardStore.Loaded.AttachExternalCancellation(destroyCancellationToken);

                loadingOverlay.style.display = DisplayStyle.None;
                if (_cardStore.DeckBuilderBackground != null)
                {
                    _deckBuilderRoot.style.backgroundImage = new StyleBackground(_cardStore.DeckBuilderBackground);
                }
                _cardListScrollView = root.Q<ScrollView>("CardListScrollView");
                _deckListScrollView = root.Q<ScrollView>("DeckListScrollView");
                _deckCountLabel = root.Q<Label>("DeckCountLabel");
                _deckCardCountLabel = root.Q<Label>("DeckCardCountLabel");
                _clearDeckButton = root.Q<Button>("ClearDeckButton");
                _costOverLabel = root.Q<Label>("CostOverLabel");
                Button backButton = root.Q<Button>("BackButton");
                Button sortDeckButton = root.Q<Button>("SortDeckButton");

                backButton.clicked += NavigateBack;
                _clearDeckButton.clicked += OnClearDeckClicked;
                sortDeckButton.clicked += OnSortDeckClicked;

                _cardListDragLayer = new VisualElement();
                _cardListDragLayer.AddToClassList("deckbuilder-drag-layer");
                _cardListDragLayer.pickingMode = PickingMode.Ignore;
                _cardDetailModal = new CardDetailModal(_deckBuilderRoot);
                _deckAnalysisModal = new DeckAnalysisModal(_deckBuilderRoot, _cardDatabase);

                Button analyzeButton = root.Q<Button>("AnalyzeButton");
                analyzeButton.clicked += () => _deckAnalysisModal.Show(_deckModel);

                VisualElement topLeftButtons = root.Q<VisualElement>(className: "deckbuilder-top-left-buttons");
                _filterCharacterButton = CreateFilterButton("キャラ", CardTypeFilter.Character);
                _filterSkillButton = CreateFilterButton("スキル", CardTypeFilter.Skill);
                _filterEventButton = CreateFilterButton("イベント", CardTypeFilter.Event);
                topLeftButtons.Add(_filterCharacterButton);
                topLeftButtons.Add(_filterSkillButton);
                topLeftButtons.Add(_filterEventButton);

                InitializeDeck();

                _cardListScrollView.Clear();
                IReadOnlyList<CardData> allCards = _cardDatabase.AllCards;
                _characterCardSection = AddCardSection("キャラ", allCards.OfType<CharacterCardData>(), "deckbuilder-section-header--character");
                _skillCardSection = AddCardSection("スキル", allCards.OfType<SkillCardData>(), "deckbuilder-section-header--skill");
                _eventCardSection = AddCardSection("イベント", allCards.OfType<EventCardData>(), "deckbuilder-section-header--event");

                RefreshDeckPanel();
                _deckBuilderRoot.Add(_cardListDragLayer);
            }
            catch (OperationCanceledException) { }
        }

        protected abstract void InitializeDeck();
        protected abstract void SaveDeck();
        protected abstract void NavigateBack();

        private VisualElement AddCardSection(string title, IEnumerable<CardData> cards, string modifierClass)
        {
            List<CardData> cardList = new List<CardData>(cards);
            if (cardList.Count == 0)
            {
                return null;
            }

            VisualElement wrapper = new VisualElement();

            Label header = new Label(title);
            header.AddToClassList("deckbuilder-section-header");
            header.AddToClassList(modifierClass);
            wrapper.Add(header);

            VisualElement grid = new VisualElement();
            grid.AddToClassList("deckbuilder-section-cards");
            foreach (CardData cardData in cardList)
            {
                CardView cardView = new CardView(_cardStore.CardTemplate, cardData);
                cardView.AddToClassList("deckbuilder-card-item");
                CardData captured = cardData;
                CardDragManipulator manipulator = new CardDragManipulator(_cardListDragLayer);
                manipulator.CreateGhost = () =>
                {
                    CardView ghost = new CardView(_cardStore.CardTemplate, captured);
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
                manipulator.OnRightClick = () =>
                {
                    _deckModel.Add(captured.Id, captured.Cost);
                    RefreshDeckPanel();
                    SaveDeck();
                };
                cardView.AttachDragManipulator(manipulator);
                grid.Add(cardView);
            }
            wrapper.Add(grid);
            _cardListScrollView.Add(wrapper);
            return wrapper;
        }

        private Button CreateFilterButton(string text, CardTypeFilter filterType)
        {
            Button btn = new Button();
            btn.text = text;
            btn.AddToClassList("deckbuilder-button--filter");
            btn.clicked += () =>
            {
                _cardTypeFilter = _cardTypeFilter == filterType ? CardTypeFilter.All : filterType;
                RefreshFilter();
            };
            return btn;
        }

        private void RefreshFilter()
        {
            bool showCharacter = _cardTypeFilter == CardTypeFilter.All || _cardTypeFilter == CardTypeFilter.Character;
            bool showSkill = _cardTypeFilter == CardTypeFilter.All || _cardTypeFilter == CardTypeFilter.Skill;
            bool showEvent = _cardTypeFilter == CardTypeFilter.All || _cardTypeFilter == CardTypeFilter.Event;

            if (_characterCardSection != null)
            {
                _characterCardSection.style.display = showCharacter ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_skillCardSection != null)
            {
                _skillCardSection.style.display = showSkill ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_eventCardSection != null)
            {
                _eventCardSection.style.display = showEvent ? DisplayStyle.Flex : DisplayStyle.None;
            }

            UpdateFilterButtonState(_filterCharacterButton, CardTypeFilter.Character, "deckbuilder-button--filter--character");
            UpdateFilterButtonState(_filterSkillButton, CardTypeFilter.Skill, "deckbuilder-button--filter--skill");
            UpdateFilterButtonState(_filterEventButton, CardTypeFilter.Event, "deckbuilder-button--filter--event");

            RefreshDeckPanel();
        }

        private void UpdateFilterButtonState(Button btn, CardTypeFilter filterType, string activeClass)
        {
            if (_cardTypeFilter == filterType)
            {
                btn.AddToClassList(activeClass);
            }
            else
            {
                btn.RemoveFromClassList(activeClass);
            }
        }

        private bool ShouldShowCardInDeck(CardData cardData)
        {
            return _cardTypeFilter switch
            {
                CardTypeFilter.Character => cardData is CharacterCardData,
                CardTypeFilter.Skill => cardData is SkillCardData,
                CardTypeFilter.Event => cardData is EventCardData,
                _ => true,
            };
        }

        private void OnRemoveClicked(string id)
        {
            _deckModel.Remove(id);
            RefreshDeckPanel();
            SaveDeck();
        }

        private void OnSortDeckClicked()
        {
            _deckModel.SortById();
            RefreshDeckPanel();
            SaveDeck();
        }

        private void OnClearDeckClicked()
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("deckbuilder-confirm-overlay");

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deckbuilder-confirm-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            Label message = new Label("本当にデッキを空にしますか？");
            message.AddToClassList("deckbuilder-confirm-message");

            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("deckbuilder-confirm-buttons");

            Button yesButton = new Button();
            yesButton.text = "はい";
            yesButton.AddToClassList("deckbuilder-confirm-button--yes");
            yesButton.clicked += () =>
            {
                overlay.RemoveFromHierarchy();
                _deckModel.Clear();
                RefreshDeckPanel();
                SaveDeck();
            };

            Button noButton = new Button();
            noButton.text = "いいえ";
            noButton.AddToClassList("deckbuilder-confirm-button--no");
            noButton.clicked += () => overlay.RemoveFromHierarchy();

            overlay.RegisterCallback<ClickEvent>(_ => overlay.RemoveFromHierarchy());

            buttons.Add(noButton);
            buttons.Add(yesButton);
            panel.Add(message);
            panel.Add(buttons);
            overlay.Add(panel);
            _deckBuilderRoot.Add(overlay);
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

            bool canReorder = _cardTypeFilter == CardTypeFilter.All;

            _deckRowOrder.Clear();
            foreach (string id in order)
            {
                if (!_cardDatabase.TryGet(id, out CardData cardData))
                {
                    continue;
                }

                if (!ShouldShowCardInDeck(cardData))
                {
                    continue;
                }

                _deckRowOrder.Add(id);

                VisualElement row = new VisualElement();
                row.AddToClassList("deckbuilder-deck-row");

                Label handle = new Label("≡");
                handle.AddToClassList("deckbuilder-deck-row-handle");
                handle.style.display = canReorder ? DisplayStyle.Flex : DisplayStyle.None;
                string capturedHandleId = id;
                handle.RegisterCallback<PointerDownEvent>(evt => OnReorderHandlePointerDown(evt, capturedHandleId));

                VisualElement thumbnail = new VisualElement();
                thumbnail.AddToClassList("deckbuilder-deck-row-thumbnail");
                if (cardData.Image != null)
                {
                    thumbnail.style.backgroundImage = new StyleBackground(cardData.Image);
                }
                CardData capturedCardData = cardData;
                thumbnail.RegisterCallback<ClickEvent>(_ => _cardDetailModal.Show(capturedCardData));

                Label costLabel = new Label($"{cardData.Cost}");
                costLabel.AddToClassList("deckbuilder-deck-row-cost");

                Label nameLabel = new Label(cardData.CardName);
                nameLabel.AddToClassList("deckbuilder-deck-row-name");
                nameLabel.RegisterCallback<ClickEvent>(_ => _cardDetailModal.Show(capturedCardData));

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

                row.Add(handle);
                row.Add(thumbnail);
                row.Add(costLabel);
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

        private void OnReorderHandlePointerDown(PointerDownEvent evt, string id)
        {
            if (evt.button != 0) { return; }
            if (_draggingId != null) { return; }

            _draggingId = id;
            _reorderInsertIndex = _deckRowOrder.IndexOf(id);

            _cardDatabase.TryGet(id, out CardData ghostCard);
            string ghostName = ghostCard != null ? ghostCard.CardName : id;

            _reorderGhost = new VisualElement();
            _reorderGhost.AddToClassList("deckbuilder-reorder-ghost");
            _reorderGhost.pickingMode = PickingMode.Ignore;
            Label ghostLabel = new Label(ghostName);
            ghostLabel.AddToClassList("deckbuilder-reorder-ghost-label");
            _reorderGhost.Add(ghostLabel);
            _cardListDragLayer.Add(_reorderGhost);
            UpdateReorderGhostPosition(evt.position);

            _reorderIndicator = new VisualElement();
            _reorderIndicator.AddToClassList("deckbuilder-reorder-indicator");
            _reorderIndicator.pickingMode = PickingMode.Ignore;
            _reorderIndicator.style.display = DisplayStyle.None;
            _cardListDragLayer.Add(_reorderIndicator);

            VisualElement handle = (VisualElement)evt.currentTarget;
            _reorderDragHandle = handle;
            _reorderDragPointerId = evt.pointerId;
            handle.CapturePointer(evt.pointerId);
            handle.RegisterCallback<PointerMoveEvent>(OnReorderPointerMove);
            handle.RegisterCallback<PointerUpEvent>(OnReorderPointerUp);
            handle.RegisterCallback<PointerCancelEvent>(OnReorderPointerCancel);
            handle.RegisterCallback<PointerCaptureOutEvent>(OnReorderPointerCaptureOut);

            evt.StopPropagation();
        }

        private void UpdateReorderGhostPosition(Vector2 worldPos)
        {
            Vector2 local = _cardListDragLayer.WorldToLocal(worldPos);
            _reorderGhost.style.left = local.x + 16f;
            _reorderGhost.style.top = local.y - 16f;
        }

        private void OnReorderPointerMove(PointerMoveEvent evt)
        {
            if (_draggingId == null) { return; }
            UpdateReorderGhostPosition(evt.position);
            UpdateReorderInsertIndex(evt.position);
            evt.StopPropagation();
        }

        private void UpdateReorderInsertIndex(Vector2 worldPos)
        {
            VisualElement content = _deckListScrollView.contentContainer;
            int count = content.childCount;
            _reorderInsertIndex = count;

            for (int i = 0; i < count; i++)
            {
                Rect rowBound = content[i].worldBound;
                if (worldPos.y < rowBound.yMin + rowBound.height * 0.5f)
                {
                    _reorderInsertIndex = i;
                    break;
                }
            }

            if (count == 0)
            {
                _reorderIndicator.style.display = DisplayStyle.None;
                return;
            }

            float indicatorWorldY;
            if (_reorderInsertIndex == 0)
            {
                indicatorWorldY = content[0].worldBound.yMin;
            }
            else
            {
                int prevIdx = _reorderInsertIndex - 1 < count ? _reorderInsertIndex - 1 : count - 1;
                indicatorWorldY = content[prevIdx].worldBound.yMax;
            }

            Rect scrollBound = _deckListScrollView.worldBound;
            Vector2 indicatorLocal = _cardListDragLayer.WorldToLocal(new Vector2(scrollBound.xMin, indicatorWorldY));

            _reorderIndicator.style.display = DisplayStyle.Flex;
            _reorderIndicator.style.left = indicatorLocal.x;
            _reorderIndicator.style.top = indicatorLocal.y;
            _reorderIndicator.style.width = scrollBound.width;
        }

        private void OnReorderPointerUp(PointerUpEvent evt)
        {
            ApplyReorder();
            EndReorder();
            evt.StopPropagation();
        }

        private void OnReorderPointerCancel(PointerCancelEvent evt)
        {
            EndReorder();
        }

        private void OnReorderPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            EndReorder();
        }

        private void ApplyReorder()
        {
            if (_draggingId == null) { return; }

            int currentIndex = _deckRowOrder.IndexOf(_draggingId);
            int targetIndex = _reorderInsertIndex;

            if (targetIndex == currentIndex || targetIndex == currentIndex + 1)
            {
                return;
            }

            List<string> newOrder = new List<string>(_deckRowOrder);
            newOrder.RemoveAt(currentIndex);
            int adjustedTarget = targetIndex > currentIndex ? targetIndex - 1 : targetIndex;
            adjustedTarget = Mathf.Clamp(adjustedTarget, 0, newOrder.Count);
            newOrder.Insert(adjustedTarget, _draggingId);

            _deckModel.Reorder(newOrder);
            RefreshDeckPanel();
            SaveDeck();
        }

        private void EndReorder()
        {
            if (_reorderDragHandle == null) { return; }

            VisualElement handle = _reorderDragHandle;
            _reorderDragHandle = null;

            if (handle.HasPointerCapture(_reorderDragPointerId))
            {
                handle.ReleasePointer(_reorderDragPointerId);
            }
            handle.UnregisterCallback<PointerMoveEvent>(OnReorderPointerMove);
            handle.UnregisterCallback<PointerUpEvent>(OnReorderPointerUp);
            handle.UnregisterCallback<PointerCancelEvent>(OnReorderPointerCancel);
            handle.UnregisterCallback<PointerCaptureOutEvent>(OnReorderPointerCaptureOut);

            _reorderDragPointerId = -1;
            _reorderGhost?.RemoveFromHierarchy();
            _reorderGhost = null;
            _reorderIndicator?.RemoveFromHierarchy();
            _reorderIndicator = null;
            _draggingId = null;
            _reorderInsertIndex = 0;
        }
    }
}

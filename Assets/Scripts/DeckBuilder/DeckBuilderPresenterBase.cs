using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        protected DeckRuleModel _deckRuleModel;

        private sealed class CardListItem
        {
            public VisualElement Element { get; }
            public CardAttribute Attribute { get; }

            public CardListItem(VisualElement element, CardAttribute attribute)
            {
                Element = element;
                Attribute = attribute;
            }
        }

        // 左から ID の属性番号順（White=1, Blue=2, Green=3, Yellow=4, Red=5, Black=6, Purple=7）に並べる。
        // CardIdAutoAssigner.AttributeNumber を参照。
        private static readonly CardAttribute[] FilterAttributes =
        {
            CardAttribute.White,
            CardAttribute.Blue,
            CardAttribute.Green,
            CardAttribute.Yellow,
            CardAttribute.Red,
            CardAttribute.Black,
            CardAttribute.Purple,
        };

        private VisualElement _deckBuilderRoot;
        private ScrollView _cardListScrollView;
        private ScrollView _deckListScrollView;
        private Label _deckCountLabel;
        private Button _clearDeckButton;
        private Label _costOverLabel;
        private Label _toastLabel;
        private CancellationTokenSource _toastCts;
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
        // フィルタは各ボタンの ON/OFF。各ディメンション（種別・属性）について
        // 「ひとつも ON でない（＝空）ならフィルタなしで全表示、ON があればその集合のみ表示」とする。
        // よって種別両方 ON と両方 OFF はどちらも全表示、属性全 ON と全 OFF もどちらも全表示になる。
        // シーン遷移ごとに presenter が生成され、すべて OFF（全表示）で始まる。
        private bool _showCharacterCards = false;
        private bool _showEventCards = false;
        private VisualElement _characterCardSection;
        private VisualElement _eventCardSection;
        private Button _filterCharacterButton;
        private Button _filterEventButton;
        // ON になっている属性の集合。初期状態は空（全表示）。
        private readonly HashSet<CardAttribute> _attributeFilter = new HashSet<CardAttribute>();
        private readonly Dictionary<CardAttribute, Button> _attributeFilterButtons = new Dictionary<CardAttribute, Button>();
        private readonly List<CardListItem> _characterCardItems = new List<CardListItem>();
        private readonly List<CardListItem> _eventCardItems = new List<CardListItem>();

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
                _clearDeckButton = root.Q<Button>("ClearDeckButton");
                _costOverLabel = root.Q<Label>("CostOverLabel");
                Button backButton = root.Q<Button>("BackButton");
                Button sortDeckButton = root.Q<Button>("SortDeckButton");
                Button costSortDeckButton = root.Q<Button>("CostSortDeckButton");

                backButton.clicked += NavigateBack;
                _clearDeckButton.clicked += OnClearDeckClicked;
                sortDeckButton.clicked += OnSortDeckClicked;
                costSortDeckButton.clicked += OnCostSortDeckClicked;

                _cardListDragLayer = new VisualElement();
                _cardListDragLayer.AddToClassList("deckbuilder-drag-layer");
                _cardListDragLayer.pickingMode = PickingMode.Ignore;
                _cardDetailModal = new CardDetailModal(_deckBuilderRoot);
                _deckAnalysisModal = new DeckAnalysisModal(_deckBuilderRoot, _cardDatabase);

                Button analyzeButton = root.Q<Button>("AnalyzeButton");
                analyzeButton.clicked += () => _deckAnalysisModal.Show(_deckModel);

                VisualElement topLeftButtons = root.Q<VisualElement>(className: "deckbuilder-top-left-buttons");
                _filterCharacterButton = CreateTypeFilterButton("キャラ", isCharacter: true);
                _filterEventButton = CreateTypeFilterButton("イベント", isCharacter: false);
                topLeftButtons.Add(_filterCharacterButton);
                topLeftButtons.Add(_filterEventButton);

                HashSet<CardAttribute> presentAttributes = new HashSet<CardAttribute>();
                foreach (CardData cardData in _cardDatabase.AllCards)
                {
                    presentAttributes.Add(cardData.Attribute);
                }

                bool isFirstAttributeButton = true;
                for (int i = 0; i < FilterAttributes.Length; i++)
                {
                    CardAttribute attribute = FilterAttributes[i];
                    // その属性のカードが1枚も無ければフィルタアイコンを表示しない
                    if (!presentAttributes.Contains(attribute))
                    {
                        continue;
                    }

                    Button attributeButton = CreateAttributeFilterButton(attribute);
                    if (isFirstAttributeButton)
                    {
                        attributeButton.style.marginLeft = 10;
                        isFirstAttributeButton = false;
                    }
                    _attributeFilterButtons[attribute] = attributeButton;
                    topLeftButtons.Add(attributeButton);
                }

                _deckCountLabel = new Label();
                _deckCountLabel.name = "DeckCountLabel";
                _deckCountLabel.AddToClassList("deckbuilder-deck-count");
                _deckCountLabel.style.marginLeft = 20;
                topLeftButtons.Add(_deckCountLabel);

                InitializeDeck();

                _cardListScrollView.Clear();
                IReadOnlyList<CardData> allCards = _cardDatabase.AllCards;
                _characterCardSection = AddCardSection("キャラ", allCards.OfType<CharacterCardData>(), "deckbuilder-section-header--character", _characterCardItems);
                _eventCardSection = AddCardSection("イベント", allCards.OfType<EventCardData>(), "deckbuilder-section-header--event", _eventCardItems);

                _toastLabel = new Label();
                _toastLabel.AddToClassList("deckbuilder-toast");
                _toastLabel.pickingMode = PickingMode.Ignore;
                _toastLabel.style.display = DisplayStyle.None;
                _deckBuilderRoot.Add(_toastLabel);

                // 全ボタン OFF の初期状態を反映（ボタンのハイライト・カード表示・デッキ一覧）
                RefreshFilter();
                _deckBuilderRoot.Add(_cardListDragLayer);
            }
            catch (OperationCanceledException) { }
        }

        protected abstract void InitializeDeck();
        protected abstract void SaveDeck();
        protected abstract void NavigateBack();

        private VisualElement AddCardSection(string title, IEnumerable<CardData> cards, string modifierClass, List<CardListItem> itemsOut)
        {
            List<CardData> cardList = new List<CardData>(cards);
            if (cardList.Count == 0)
            {
                return null;
            }

            // 属性順（FilterAttributes の並び）→ コスト順 → ID 順（属性番号×1000+連番）に並べる。
            cardList.Sort(CompareByAttributeCostId);

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
                        TryAddCard(captured.Id, captured.Cost);
                    }
                    return false;
                };
                manipulator.OnClick = () => _cardDetailModal.Show(captured);
                manipulator.OnRightClick = () => TryAddCard(captured.Id, captured.Cost);
                cardView.AttachDragManipulator(manipulator);
                grid.Add(cardView);
                itemsOut.Add(new CardListItem(cardView, cardData.Attribute));
            }
            wrapper.Add(grid);
            _cardListScrollView.Add(wrapper);
            return wrapper;
        }

        private Button CreateTypeFilterButton(string text, bool isCharacter)
        {
            Button btn = new Button();
            btn.text = text;
            btn.AddToClassList("deckbuilder-button--filter");
            btn.clicked += () =>
            {
                if (isCharacter)
                {
                    _showCharacterCards = !_showCharacterCards;
                }
                else
                {
                    _showEventCards = !_showEventCards;
                }
                RefreshFilter();
            };
            return btn;
        }

        private Button CreateAttributeFilterButton(CardAttribute attribute)
        {
            Button btn = new Button();
            btn.AddToClassList("deckbuilder-button--filter-attr");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("deckbuilder-filter-attr-icon");
            icon.AddToClassList(GetFilterAttributeIconClass(attribute));
            icon.pickingMode = PickingMode.Ignore;
            btn.Add(icon);

            CardAttribute captured = attribute;
            btn.clicked += () =>
            {
                if (_attributeFilter.Contains(captured))
                {
                    _attributeFilter.Remove(captured);
                }
                else
                {
                    _attributeFilter.Add(captured);
                }
                RefreshFilter();
            };
            return btn;
        }

        // 「整列」ボタン：カード一覧と同じ並び（キャラ→イベント → 属性 → コスト → ID）で比較する。
        private static int CompareByTypeAttributeCostId(CardData a, CardData b)
        {
            int typeComparison = TypeOrder(a).CompareTo(TypeOrder(b));
            if (typeComparison != 0)
            {
                return typeComparison;
            }
            return CompareByAttributeCostId(a, b);
        }

        // カード種別の並び位置を返す。キャラカードを先、イベントカードを後にする。
        private static int TypeOrder(CardData card)
        {
            return card is CharacterCardData ? 0 : 1;
        }

        private static int CompareByAttributeCostId(CardData a, CardData b)
        {
            int attributeComparison = AttributeOrder(a.Attribute).CompareTo(AttributeOrder(b.Attribute));
            if (attributeComparison != 0)
            {
                return attributeComparison;
            }
            int costComparison = a.Cost.CompareTo(b.Cost);
            if (costComparison != 0)
            {
                return costComparison;
            }
            return ParseCardIdNumber(a.Id).CompareTo(ParseCardIdNumber(b.Id));
        }

        // コスト昇順 → カード種別（キャラ→イベント）→ 属性順（FilterAttributes の並び）→ ID 昇順（属性番号×1000+連番）で比較する。
        private static int CompareByCostAttributeId(CardData a, CardData b)
        {
            int costComparison = a.Cost.CompareTo(b.Cost);
            if (costComparison != 0)
            {
                return costComparison;
            }
            int typeComparison = TypeOrder(a).CompareTo(TypeOrder(b));
            if (typeComparison != 0)
            {
                return typeComparison;
            }
            int attributeComparison = AttributeOrder(a.Attribute).CompareTo(AttributeOrder(b.Attribute));
            if (attributeComparison != 0)
            {
                return attributeComparison;
            }
            return ParseCardIdNumber(a.Id).CompareTo(ParseCardIdNumber(b.Id));
        }

        // FilterAttributes での並び位置を返す。未登録の属性は末尾に並べる。
        private static int AttributeOrder(CardAttribute attribute)
        {
            int index = Array.IndexOf(FilterAttributes, attribute);
            return index < 0 ? int.MaxValue : index;
        }

        // "C1001" / "E2003" などの ID から数値部分（属性番号×1000+連番）を取り出す。
        // 解析できない場合は末尾に並べる。
        private static int ParseCardIdNumber(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return int.MaxValue;
            }

            int start = 0;
            while (start < id.Length && !char.IsDigit(id[start]))
            {
                start++;
            }

            if (start >= id.Length || !int.TryParse(id.Substring(start), out int number))
            {
                return int.MaxValue;
            }

            return number;
        }

        private static string GetFilterAttributeIconClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "deckbuilder-filter-attr-icon--red",
                CardAttribute.Blue => "deckbuilder-filter-attr-icon--blue",
                CardAttribute.Green => "deckbuilder-filter-attr-icon--green",
                CardAttribute.Yellow => "deckbuilder-filter-attr-icon--yellow",
                CardAttribute.Black => "deckbuilder-filter-attr-icon--black",
                CardAttribute.Purple => "deckbuilder-filter-attr-icon--purple",
                CardAttribute.White => "deckbuilder-filter-attr-icon--white",
                _ => "deckbuilder-filter-attr-icon--white"
            };
        }

        private void RefreshFilter()
        {
            // 属性が ON のカードだけ表示（OFF 属性は非表示）。各セクションの表示中カード数を得る
            int characterVisible = ApplyAttributeFilterToItems(_characterCardItems);
            int eventVisible = ApplyAttributeFilterToItems(_eventCardItems);

            // 種別フィルタにマッチし、かつ表示中カードがあるセクションのみ表示する
            if (_characterCardSection != null)
            {
                _characterCardSection.style.display = TypeMatches(isCharacter: true) && characterVisible > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_eventCardSection != null)
            {
                _eventCardSection.style.display = TypeMatches(isCharacter: false) && eventVisible > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // ボタンの ON/OFF ハイライト
            SetButtonActive(_filterCharacterButton, _showCharacterCards, "deckbuilder-button--filter--character");
            SetButtonActive(_filterEventButton, _showEventCards, "deckbuilder-button--filter--event");
            foreach (KeyValuePair<CardAttribute, Button> pair in _attributeFilterButtons)
            {
                SetButtonActive(pair.Value, _attributeFilter.Contains(pair.Key), "deckbuilder-button--filter-attr--active");
            }

            RefreshDeckPanel();
        }

        private int ApplyAttributeFilterToItems(List<CardListItem> items)
        {
            int visible = 0;
            foreach (CardListItem item in items)
            {
                bool match = AttributeMatches(item.Attribute);
                item.Element.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
                if (match)
                {
                    visible++;
                }
            }
            return visible;
        }

        // 種別フィルタ判定。キャラ・イベントどちらも OFF なら種別での絞り込みをせず全マッチ。
        // いずれかが ON のときは、その種別が ON の場合のみマッチ。
        private bool TypeMatches(bool isCharacter)
        {
            if (!_showCharacterCards && !_showEventCards)
            {
                return true;
            }
            return isCharacter ? _showCharacterCards : _showEventCards;
        }

        // 属性フィルタ判定。ON の属性が無い（空集合）なら属性での絞り込みをせず全マッチ。
        private bool AttributeMatches(CardAttribute attribute)
        {
            if (_attributeFilter.Count == 0)
            {
                return true;
            }
            return _attributeFilter.Contains(attribute);
        }

        private static void SetButtonActive(Button btn, bool active, string activeClass)
        {
            if (active)
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
            bool typeMatch = TypeMatches(cardData is CharacterCardData);
            bool attributeMatch = AttributeMatches(cardData.Attribute);
            return typeMatch && attributeMatch;
        }

        // 同一カード（ID 基準）の枚数制限を考慮して追加する。追加できたら true。
        private bool TryAddCard(string id, int cost)
        {
            if (_deckRuleModel != null && _deckRuleModel.LimitSameCards
                && _deckModel.CountOf(id) >= DeckRuleModel.MaxCopiesPerId)
            {
                ShowToastAsync($"同名カードは{DeckRuleModel.MaxCopiesPerId}枚までです").Forget();
                return false;
            }
            _deckModel.Add(id, cost);
            RefreshDeckPanel();
            SaveDeck();
            return true;
        }

        private async UniTaskVoid ShowToastAsync(string message)
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _toastCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = _toastCts.Token;
            _toastLabel.text = message;
            _toastLabel.style.display = DisplayStyle.Flex;
            try
            {
                await UniTask.Delay(1500, cancellationToken: token);
                _toastLabel.style.display = DisplayStyle.None;
            }
            catch (OperationCanceledException) { }
        }

        private void OnDestroy()
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
        }

        private void OnRemoveClicked(string id)
        {
            _deckModel.Remove(id);
            RefreshDeckPanel();
            SaveDeck();
        }

        // 「整列」ボタン：カード一覧と同じ並び（キャラ→イベント → 属性 → コスト → ID）で並べ替える。
        private void OnSortDeckClicked()
        {
            SortDeckBy(CompareByTypeAttributeCostId);
        }

        // 「コスト順」ボタン：コスト→種別（キャラ→イベント）→属性→ID 順に並べ替える。
        private void OnCostSortDeckClicked()
        {
            SortDeckBy(CompareByCostAttributeId);
        }

        // デッキ内の各カード（同一 ID はまとめる）を指定の比較関数で並べ替える。
        // 属性・コストは ID/コストだけを持つ DeckModel では引きづらいため、CardDatabase から CardData を引いて比較する。
        private void SortDeckBy(Comparison<CardData> compare)
        {
            List<string> uniqueIds = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string id in _deckModel.CardIds)
            {
                if (seen.Add(id))
                {
                    uniqueIds.Add(id);
                }
            }

            uniqueIds.Sort((idA, idB) =>
            {
                bool hasA = _cardDatabase.TryGet(idA, out CardData a);
                bool hasB = _cardDatabase.TryGet(idB, out CardData b);
                // データベースに無い ID は末尾へ。
                if (!hasA || !hasB)
                {
                    return hasA == hasB ? 0 : (hasA ? -1 : 1);
                }
                return compare(a, b);
            });

            _deckModel.Reorder(uniqueIds);
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
            _deckCountLabel.text = $"枚数 {_deckModel.Count}/{DeckModel.MaxCards}";

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

            bool canReorder = true;

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
                addButton.clicked += () => TryAddCard(capturedId, capturedCost);

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

            if (isOver)
            {
                _costOverLabel.text = $"{DeckModel.MaxCards}枚を超えています";
                _costOverLabel.style.display = DisplayStyle.Flex;
            }
            else if (!_deckModel.IsReady && _deckModel.Count > 0)
            {
                _costOverLabel.text = $"デッキが{DeckModel.MaxCards}枚になっていません";
                _costOverLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _costOverLabel.style.display = DisplayStyle.None;
            }

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

            List<string> newVisibleOrder = new List<string>(_deckRowOrder);
            newVisibleOrder.RemoveAt(currentIndex);
            int adjustedTarget = targetIndex > currentIndex ? targetIndex - 1 : targetIndex;
            adjustedTarget = Mathf.Clamp(adjustedTarget, 0, newVisibleOrder.Count);
            newVisibleOrder.Insert(adjustedTarget, _draggingId);

            _deckModel.Reorder(MergeIntoFullOrder(newVisibleOrder));
            RefreshDeckPanel();
            SaveDeck();
        }

        // フィルタで一部の行のみ表示している場合でも並べ替えできるよう、
        // 表示行の新しい順序を全カードの順序へ差し込む。非表示カードは元の位置を保持する。
        private List<string> MergeIntoFullOrder(List<string> newVisibleOrder)
        {
            List<string> fullOrder = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string id in _deckModel.CardIds)
            {
                if (seen.Add(id))
                {
                    fullOrder.Add(id);
                }
            }

            HashSet<string> visibleSet = new HashSet<string>(newVisibleOrder);
            List<string> result = new List<string>(fullOrder.Count);
            int nextVisible = 0;
            foreach (string id in fullOrder)
            {
                if (visibleSet.Contains(id) && nextVisible < newVisibleOrder.Count)
                {
                    result.Add(newVisibleOrder[nextVisible]);
                    nextVisible++;
                }
                else
                {
                    result.Add(id);
                }
            }
            return result;
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

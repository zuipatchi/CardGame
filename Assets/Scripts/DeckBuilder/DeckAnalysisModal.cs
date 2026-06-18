using System.Collections.Generic;
using Common.Deck;
using Main.Card;
using UnityEngine.UIElements;

namespace DeckBuilder
{
    public sealed class DeckAnalysisModal
    {
        private readonly VisualElement _root;
        private readonly CardDatabase _cardDatabase;
        private VisualElement _overlay;

        private const float BarWrapperHeight = 170f;
        private const float BottomLabelSpace = 44f;
        private const float ChartMaxHeight = 100f;

        public DeckAnalysisModal(VisualElement root, CardDatabase cardDatabase)
        {
            _root = root;
            _cardDatabase = cardDatabase;
        }

        public void Show(DeckModel deckModel)
        {
            Hide();

            _overlay = new VisualElement();
            _overlay.AddToClassList("deck-analysis-overlay");
            _overlay.RegisterCallback<ClickEvent>(_ => Hide());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deck-analysis-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            VisualElement header = new VisualElement();
            header.AddToClassList("deck-analysis-header");

            Label title = new Label("デッキ分析");
            title.AddToClassList("deck-analysis-title");

            Button closeButton = new Button();
            closeButton.text = "×";
            closeButton.AddToClassList("deck-analysis-close");
            closeButton.clicked += Hide;

            header.Add(title);
            header.Add(closeButton);
            panel.Add(header);

            Dictionary<int, int> costDist = BuildCostDistribution(deckModel);
            Dictionary<CardAttribute, int> attrDist = BuildAttributeDistribution(deckModel);
            (int charCount, int eventCount) = BuildTypeDistribution(deckModel);

            panel.Add(BuildSection("コスト分布", BuildCostChart(costDist)));

            VisualElement secondRow = new VisualElement();
            secondRow.AddToClassList("deck-analysis-row");
            secondRow.Add(BuildSection("属性分布", BuildAttributeChart(attrDist)));
            secondRow.Add(BuildSection("種類分布", BuildTypeChart(charCount, eventCount)));
            panel.Add(secondRow);

            _overlay.Add(panel);
            _root.Add(_overlay);
        }

        public void Hide()
        {
            if (_overlay == null)
            {
                return;
            }
            _overlay.RemoveFromHierarchy();
            _overlay = null;
        }

        private Dictionary<int, int> BuildCostDistribution(DeckModel deckModel)
        {
            Dictionary<int, int> dist = new Dictionary<int, int>();
            foreach ((string _, int cost) in deckModel.Entries)
            {
                if (dist.TryGetValue(cost, out int current))
                {
                    dist[cost] = current + 1;
                }
                else
                {
                    dist[cost] = 1;
                }
            }
            return dist;
        }

        private Dictionary<CardAttribute, int> BuildAttributeDistribution(DeckModel deckModel)
        {
            Dictionary<CardAttribute, int> dist = new Dictionary<CardAttribute, int>();
            foreach ((string id, int _) in deckModel.Entries)
            {
                if (!_cardDatabase.TryGet(id, out CardData data))
                {
                    continue;
                }
                CardAttribute attr = data.Attribute;
                if (dist.TryGetValue(attr, out int current))
                {
                    dist[attr] = current + 1;
                }
                else
                {
                    dist[attr] = 1;
                }
            }
            return dist;
        }

        private (int charCount, int eventCount) BuildTypeDistribution(DeckModel deckModel)
        {
            int charCount = 0;
            int eventCount = 0;
            foreach ((string id, int _) in deckModel.Entries)
            {
                if (!_cardDatabase.TryGet(id, out CardData data))
                {
                    continue;
                }
                if (data is CharacterCardData)
                {
                    charCount++;
                }
                else if (data is EventCardData)
                {
                    eventCount++;
                }
            }
            return (charCount, eventCount);
        }

        private static VisualElement BuildSection(string sectionTitle, VisualElement chart)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("deck-analysis-section");

            Label label = new Label(sectionTitle);
            label.AddToClassList("deck-analysis-section-title");
            section.Add(label);
            section.Add(chart);
            return section;
        }

        private static VisualElement BuildCostChart(Dictionary<int, int> dist)
        {
            VisualElement chart = new VisualElement();
            chart.AddToClassList("deck-analysis-chart");

            if (dist.Count == 0)
            {
                Label empty = new Label("カードなし");
                empty.AddToClassList("deck-analysis-empty");
                chart.Add(empty);
                return chart;
            }

            int maxCount = 0;
            foreach (int count in dist.Values)
            {
                if (count > maxCount)
                {
                    maxCount = count;
                }
            }

            List<int> sortedCosts = new List<int>(dist.Keys);
            sortedCosts.Sort();

            foreach (int cost in sortedCosts)
            {
                int count = dist[cost];
                float ratio = maxCount > 0 ? (float)count / maxCount : 0f;
                chart.Add(BuildBarColumn(cost.ToString(), count, ratio, "deck-analysis-bar--cost"));
            }
            return chart;
        }

        private static VisualElement BuildAttributeChart(Dictionary<CardAttribute, int> dist)
        {
            VisualElement chart = new VisualElement();
            chart.AddToClassList("deck-analysis-chart");

            if (dist.Count == 0)
            {
                Label empty = new Label("カードなし");
                empty.AddToClassList("deck-analysis-empty");
                chart.Add(empty);
                return chart;
            }

            int maxCount = 0;
            foreach (int count in dist.Values)
            {
                if (count > maxCount)
                {
                    maxCount = count;
                }
            }

            // enum 定義順で、デッキに含まれる属性のみを表示する。
            foreach (CardAttribute attr in System.Enum.GetValues(typeof(CardAttribute)))
            {
                if (!dist.TryGetValue(attr, out int count))
                {
                    continue;
                }
                float ratio = maxCount > 0 ? (float)count / maxCount : 0f;
                chart.Add(BuildBarColumn(AttributeLabel(attr), count, ratio, AttributeBarClass(attr)));
            }
            return chart;
        }

        private static string AttributeLabel(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "赤",
                CardAttribute.Blue => "青",
                CardAttribute.Green => "緑",
                CardAttribute.Yellow => "黄",
                CardAttribute.Black => "黒",
                CardAttribute.Purple => "紫",
                CardAttribute.White => "白",
                _ => attribute.ToString()
            };
        }

        private static string AttributeBarClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "deck-analysis-bar--attr-red",
                CardAttribute.Blue => "deck-analysis-bar--attr-blue",
                CardAttribute.Green => "deck-analysis-bar--attr-green",
                CardAttribute.Yellow => "deck-analysis-bar--attr-yellow",
                CardAttribute.Black => "deck-analysis-bar--attr-black",
                CardAttribute.Purple => "deck-analysis-bar--attr-purple",
                CardAttribute.White => "deck-analysis-bar--attr-white",
                _ => "deck-analysis-bar--attr-white"
            };
        }

        private static VisualElement BuildTypeChart(int charCount, int eventCount)
        {
            VisualElement chart = new VisualElement();
            chart.AddToClassList("deck-analysis-chart");

            int maxCount = System.Math.Max(charCount, eventCount);
            bool hasCards = maxCount > 0;
            float charRatio = hasCards ? (float)charCount / maxCount : 0f;
            float eventRatio = hasCards ? (float)eventCount / maxCount : 0f;

            chart.Add(BuildBarColumn("キャラ", charCount, charRatio, "deck-analysis-bar--character"));
            chart.Add(BuildBarColumn("イベント", eventCount, eventRatio, "deck-analysis-bar--event"));
            return chart;
        }

        private static VisualElement BuildBarColumn(string labelText, int count, float ratio, string barColorClass)
        {
            VisualElement barWrapper = new VisualElement();
            barWrapper.AddToClassList("deck-analysis-bar-column");

            float barHeight = ChartMaxHeight * ratio;
            if (barHeight < 4f)
            {
                barHeight = 4f;
            }

            VisualElement bar = new VisualElement();
            bar.AddToClassList("deck-analysis-bar");
            bar.AddToClassList(barColorClass);
            bar.style.position = Position.Absolute;
            bar.style.bottom = BottomLabelSpace;
            bar.style.left = 4f;
            bar.style.right = 4f;
            bar.style.height = barHeight;
            barWrapper.Add(bar);

            Label countLabel = new Label(count.ToString());
            countLabel.AddToClassList("deck-analysis-bar-count");
            countLabel.style.position = Position.Absolute;
            countLabel.style.bottom = BottomLabelSpace + barHeight + 2f;
            countLabel.style.left = 0;
            countLabel.style.right = 0;
            barWrapper.Add(countLabel);

            Label bottomLabel = new Label(labelText);
            bottomLabel.AddToClassList("deck-analysis-bar-label");
            bottomLabel.style.position = Position.Absolute;
            bottomLabel.style.top = BarWrapperHeight - BottomLabelSpace;
            bottomLabel.style.bottom = 0;
            bottomLabel.style.left = 0;
            bottomLabel.style.right = 0;
            barWrapper.Add(bottomLabel);

            return barWrapper;
        }
    }
}

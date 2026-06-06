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

        private const float BarWrapperHeight = 240f;
        private const float BottomLabelSpace = 50f;
        private const float ChartMaxHeight = 160f;

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
            (int charCount, int eventCount) = BuildTypeDistribution(deckModel);

            panel.Add(BuildSection("コスト分布", BuildCostChart(costDist)));
            panel.Add(BuildSection("種類分布", BuildTypeChart(charCount, eventCount)));

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

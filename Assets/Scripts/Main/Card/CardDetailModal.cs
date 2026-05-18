using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardDetailModal
    {
        private readonly VisualElement _root;
        private VisualElement _overlay;

        public CardDetailModal(VisualElement root)
        {
            _root = root;
        }

        public void Show(CardData data)
        {
            Hide();

            _overlay = new VisualElement();
            _overlay.AddToClassList("card-detail-overlay");
            _overlay.RegisterCallback<ClickEvent>(_ => Hide());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("card-detail-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            VisualElement imageArea = new VisualElement();
            imageArea.AddToClassList("card-detail-image");
            if (data.Image != null)
            {
                imageArea.style.backgroundImage = new StyleBackground(data.Image);
            }

            string imageTypeClass = data is CharacterCardData ? "card-detail-image--character" :
                                    data is SkillCardData ? "card-detail-image--skill" :
                                    "card-detail-image--event";
            imageArea.AddToClassList(imageTypeClass);
            panel.Add(imageArea);

            VisualElement stats = new VisualElement();
            stats.AddToClassList("card-detail-stats");

            string typeName = data is CharacterCardData ? "CHARACTER" :
                              data is SkillCardData ? "SKILL" : "EVENT";
            string typeClass = data is CharacterCardData ? "card-detail-type--character" :
                               data is SkillCardData ? "card-detail-type--skill" :
                               "card-detail-type--event";
            Label typeLabel = new Label(typeName);
            typeLabel.AddToClassList("card-detail-type");
            typeLabel.AddToClassList(typeClass);
            stats.Add(typeLabel);

            Label nameLabel = new Label(data.CardName);
            nameLabel.AddToClassList("card-detail-name");
            stats.Add(nameLabel);

            VisualElement divider = new VisualElement();
            divider.AddToClassList("card-detail-divider");
            stats.Add(divider);

            AddStatRow(stats, "コスト", data.Cost.ToString());

            if (data is CharacterCardData)
            {
                AddStatRow(stats, "DEF", data.Defense.ToString());
            }
            else if (data is SkillCardData)
            {
                AddStatRow(stats, "ダメージ", data.Attack.ToString());
            }
            else if (data is EventCardData eventData)
            {
                AddStatRow(stats, "効果", FormatEffect(eventData.EffectType, eventData.EffectValue));
            }

            panel.Add(stats);
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

        private static void AddStatRow(VisualElement container, string labelText, string valueText)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("card-detail-row");

            Label label = new Label(labelText);
            label.AddToClassList("card-detail-row-label");

            Label value = new Label(valueText);
            value.AddToClassList("card-detail-row-value");

            row.Add(label);
            row.Add(value);
            container.Add(row);
        }

        private static string FormatEffect(EffectType effectType, int value)
        {
            return effectType switch
            {
                EffectType.AtkBoost => $"ATK Boost +{value}",
                EffectType.DefBoost => $"DEF Boost +{value}",
                EffectType.Draw => $"Draw ×{value}",
                _ => effectType.ToString(),
            };
        }
    }
}

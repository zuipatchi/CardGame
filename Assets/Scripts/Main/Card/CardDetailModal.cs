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

            VisualElement statsGrid = new VisualElement();
            statsGrid.AddToClassList("card-detail-stats-grid");

            AddIconStatRow(statsGrid, "card-detail-stat-icon--cost", data.Cost.ToString(), "コスト");

            if (data is CharacterCardData charData)
            {
                AddIconStatRow(statsGrid, "card-detail-stat-icon--hp", charData.Hp.ToString(), "体力");
                AddIconStatRow(statsGrid, "card-detail-stat-icon--atk", charData.Attack.ToString(), "攻撃力");
                AddIconStatRow(statsGrid, "card-detail-stat-icon--def", charData.Defense.ToString(), "防御");
                AddIconStatRow(statsGrid, "card-detail-stat-icon--spd", charData.Speed.ToString(), "素早さ");
            }
            else if (data is SkillCardData skillData)
            {
                if (skillData.SkillType == SkillType.Recover)
                {
                    AddIconStatRow(statsGrid, "card-detail-stat-icon--recover", skillData.SkillValue.ToString(), "回復");
                }
                else
                {
                    AddIconStatRow(statsGrid, "card-detail-stat-icon--atk", skillData.SkillValue.ToString(), "攻撃力");
                }
            }
            else if (data is EventCardData eventData)
            {
                if (!string.IsNullOrEmpty(eventData.Description))
                {
                    Label descLabel = new Label(eventData.Description);
                    descLabel.AddToClassList("card-detail-description");
                    stats.Add(descLabel);
                }
            }

            stats.Add(statsGrid);

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

        private static void AddIconStatRow(VisualElement container, string iconClass, string valueText, string labelText)
        {
            VisualElement block = new VisualElement();
            block.AddToClassList("card-detail-stat-block");

            Label label = new Label(labelText);
            label.AddToClassList("card-detail-row-label");
            block.Add(label);

            VisualElement row = new VisualElement();
            row.AddToClassList("card-detail-row");

            VisualElement icon = new VisualElement();
            icon.AddToClassList("card-detail-stat-icon");
            icon.AddToClassList(iconClass);

            Label value = new Label(valueText);
            value.AddToClassList("card-detail-row-value");

            row.Add(icon);
            row.Add(value);
            block.Add(row);
            container.Add(block);
        }

    }
}

using System.Collections.Generic;
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

            imageArea.AddToClassList(GetAttributeImageClass(data.Attribute));
            panel.Add(imageArea);

            VisualElement stats = new VisualElement();
            stats.AddToClassList("card-detail-stats");

            VisualElement headerRow = new VisualElement();
            headerRow.AddToClassList("card-detail-header-row");

            string typeName = data is CharacterCardData ? "CHARACTER" : "EVENT";
            string typeClass = data is CharacterCardData ? "card-detail-type--character" :
                               "card-detail-type--event";
            Label typeLabel = new Label(typeName);
            typeLabel.AddToClassList("card-detail-type");
            typeLabel.AddToClassList(typeClass);
            headerRow.Add(typeLabel);

            headerRow.Add(CreateAttributeBadge(data.Attribute));
            stats.Add(headerRow);

            Label nameLabel = new Label(data.CardName);
            nameLabel.AddToClassList("card-detail-name");
            stats.Add(nameLabel);


            VisualElement divider = new VisualElement();
            divider.AddToClassList("card-detail-divider");
            stats.Add(divider);

            List<VisualElement> statBlocks = new List<VisualElement>();
            statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--cost", data.Cost.ToString(), "コスト"));

            if (data is CharacterCardData charData)
            {
                statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--atk", charData.Attack.ToString(), "攻撃力"));
                statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--hp", charData.Hp.ToString(), "体力"));
                if (charData.Guardian)
                {
                    statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--guardian", string.Empty, "守護"));
                }
                if (charData.Haste)
                {
                    statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--haste", string.Empty, "速攻"));
                }
                if (charData.Flying)
                {
                    statBlocks.Add(CreateIconStatBlock("card-detail-stat-icon--flying", string.Empty, "飛行"));
                }
                if (!string.IsNullOrEmpty(charData.Description))
                {
                    Label descLabel = new Label(charData.Description);
                    descLabel.AddToClassList("card-detail-description");
                    stats.Add(descLabel);
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

            // 2列の明示的な行コンテナで組む（flex-wrap は折り返し時に高さを正しく報告せず、
            // 下に置いたフレーバーテキストと重なるため使用しない）
            VisualElement statsGrid = new VisualElement();
            statsGrid.AddToClassList("card-detail-stats-grid");
            for (int i = 0; i < statBlocks.Count; i += 2)
            {
                VisualElement gridRow = new VisualElement();
                gridRow.AddToClassList("card-detail-stats-row");
                gridRow.Add(statBlocks[i]);
                if (i + 1 < statBlocks.Count)
                {
                    gridRow.Add(statBlocks[i + 1]);
                }
                statsGrid.Add(gridRow);
            }
            stats.Add(statsGrid);

            if (!string.IsNullOrEmpty(data.FlavorText))
            {
                Label flavorLabel = new Label(data.FlavorText);
                flavorLabel.AddToClassList("card-detail-flavor");
                stats.Add(flavorLabel);
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

        private static VisualElement CreateAttributeBadge(CardAttribute attribute)
        {
            VisualElement icon = new VisualElement();
            icon.AddToClassList("card-detail-attr-icon");
            icon.AddToClassList(GetAttributeIconClass(attribute));
            return icon;
        }

        private static string GetAttributeIconClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "card-detail-attr-icon--red",
                CardAttribute.Blue => "card-detail-attr-icon--blue",
                CardAttribute.Green => "card-detail-attr-icon--green",
                CardAttribute.Yellow => "card-detail-attr-icon--yellow",
                CardAttribute.Black => "card-detail-attr-icon--black",
                CardAttribute.Purple => "card-detail-attr-icon--purple",
                CardAttribute.White => "card-detail-attr-icon--white",
                _ => "card-detail-attr-icon--white"
            };
        }

        private static string GetAttributeImageClass(CardAttribute attribute)
        {
            return attribute switch
            {
                CardAttribute.Red => "card-detail-image--attr-red",
                CardAttribute.Blue => "card-detail-image--attr-blue",
                CardAttribute.Green => "card-detail-image--attr-green",
                CardAttribute.Yellow => "card-detail-image--attr-yellow",
                CardAttribute.Black => "card-detail-image--attr-black",
                CardAttribute.Purple => "card-detail-image--attr-purple",
                CardAttribute.White => "card-detail-image--attr-white",
                _ => "card-detail-image--attr-white"
            };
        }

        private static VisualElement CreateIconStatBlock(string iconClass, string valueText, string labelText)
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
            return block;
        }

    }
}

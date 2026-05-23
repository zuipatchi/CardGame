using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class AttributeCompatibilityModal
    {
        private readonly VisualElement _root;
        private readonly AttributeDatabaseSO _attrDb;
        private VisualElement _overlay;

        private static readonly CardAttribute[] DisplayAttributes =
        {
            CardAttribute.Fire,
            CardAttribute.Poison,
            CardAttribute.Patchi,
        };

        public AttributeCompatibilityModal(VisualElement root, AttributeDatabaseSO attrDb)
        {
            _root = root;
            _attrDb = attrDb;
        }

        public void Show()
        {
            Hide();

            _overlay = new VisualElement();
            _overlay.AddToClassList("attr-compatibility-overlay");
            _overlay.RegisterCallback<ClickEvent>(_ => Hide());

            VisualElement panel = new VisualElement();
            panel.AddToClassList("attr-compatibility-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            Label title = new Label("属性相性表");
            title.AddToClassList("attr-compatibility-title");
            panel.Add(title);

            VisualElement titleDivider = new VisualElement();
            titleDivider.AddToClassList("attr-compatibility-separator");
            panel.Add(titleDivider);

            VisualElement header = new VisualElement();
            header.AddToClassList("attr-compatibility-row");
            header.Add(MakeHeaderLabel("属性", ""));
            header.Add(MakeHeaderLabel("得意", "attr-compatibility-header-label--strength"));
            header.Add(MakeHeaderLabel("苦手", "attr-compatibility-header-label--weakness"));
            panel.Add(header);

            VisualElement headerDivider = new VisualElement();
            headerDivider.AddToClassList("attr-compatibility-separator");
            panel.Add(headerDivider);

            for (int i = 0; i < DisplayAttributes.Length; i++)
            {
                CardAttribute attr = DisplayAttributes[i];

                VisualElement row = new VisualElement();
                row.AddToClassList("attr-compatibility-row");
                if (i % 2 == 1)
                {
                    row.AddToClassList("attr-compatibility-row--alt");
                }

                row.Add(BuildIconCell(attr));
                row.Add(BuildIconCell(_attrDb?.GetStrength(attr) ?? CardAttribute.None));
                row.Add(BuildIconCell(_attrDb?.GetWeakness(attr) ?? CardAttribute.None));
                panel.Add(row);
            }

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

        private static Label MakeHeaderLabel(string text, string modifierClass)
        {
            Label label = new Label(text);
            label.AddToClassList("attr-compatibility-header-label");
            if (!string.IsNullOrEmpty(modifierClass))
            {
                label.AddToClassList(modifierClass);
            }

            return label;
        }

        private VisualElement BuildIconCell(CardAttribute attribute)
        {
            VisualElement cell = new VisualElement();
            cell.AddToClassList("attr-compatibility-cell");

            if (attribute != CardAttribute.None)
            {
                Sprite icon = _attrDb?.GetIcon(attribute);
                if (icon != null)
                {
                    VisualElement iconEl = new VisualElement();
                    iconEl.AddToClassList("attr-compatibility-icon");
                    iconEl.style.backgroundImage = new StyleBackground(icon);
                    cell.Add(iconEl);
                    return cell;
                }
            }

            Label noneLabel = new Label("—");
            noneLabel.AddToClassList("attr-compatibility-none-label");
            cell.Add(noneLabel);
            return cell;
        }
    }
}

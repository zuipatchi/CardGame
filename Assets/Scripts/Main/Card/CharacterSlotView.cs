using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CharacterSlotView : VisualElement
    {
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        public event Action<CardView> OnCardDisplaced;
        public Action<CardView> OnCardClicked { get; set; }

        private CardView _current;
        private readonly VisualElement _atkOverlay;
        private readonly Label _atkLabel;
        private readonly VisualElement _defOverlay;
        private readonly Label _defLabel;

        public CardView CurrentCard => _current;
        public int Defense => _current?.Data.Defense ?? 0;
        public int Hp => _current?.Data.Hp ?? 0;
        public int Speed => _current?.Data.Speed ?? 0;

        public VisualElement DefOverlay => _defOverlay;

        public void SetAtkValue(int atk) => _atkLabel.text = atk.ToString();
        public void SetDefValue(int def) => _defLabel.text = def.ToString();
        public string AtkLabelText => _atkLabel.text;
        public bool IsAtkOverlayVisible => _atkOverlay.style.display == DisplayStyle.Flex;

        public void SetAtkOverlayVisible(bool visible)
        {
            if (visible)
            {
                _atkOverlay.BringToFront();
            }
            _atkOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public CharacterSlotView()
        {
            AddToClassList("character-slot-view");
            style.width = CardWidth;
            style.height = CardHeight;
            style.flexShrink = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.overflow = Overflow.Hidden;

            _atkOverlay = new VisualElement();
            _atkOverlay.AddToClassList("char-slot-atk-overlay");
            _atkOverlay.pickingMode = PickingMode.Ignore;
            _atkOverlay.style.display = DisplayStyle.None;

            VisualElement atkIcon = new VisualElement();
            atkIcon.AddToClassList("char-slot-atk-icon");
            atkIcon.pickingMode = PickingMode.Ignore;
            _atkOverlay.Add(atkIcon);

            _atkLabel = new Label("0");
            _atkLabel.AddToClassList("char-slot-atk-label");
            _atkLabel.pickingMode = PickingMode.Ignore;
            _atkOverlay.Add(_atkLabel);

            Add(_atkOverlay);

            _defOverlay = new VisualElement();
            _defOverlay.AddToClassList("char-slot-def-overlay");
            _defOverlay.pickingMode = PickingMode.Ignore;
            _defOverlay.style.display = DisplayStyle.None;

            VisualElement defIcon = new VisualElement();
            defIcon.AddToClassList("char-slot-def-icon");
            defIcon.pickingMode = PickingMode.Ignore;
            _defOverlay.Add(defIcon);

            _defLabel = new Label("0");
            _defLabel.AddToClassList("char-slot-def-label");
            _defLabel.pickingMode = PickingMode.Ignore;
            _defOverlay.Add(_defLabel);

            Add(_defOverlay);

            RegisterCallback<ClickEvent>(_ =>
            {
                if (_current != null)
                {
                    OnCardClicked?.Invoke(_current);
                }
            });
        }

        public void PlaceCard(CardView card)
        {
            if (_current != null)
            {
                CardView displaced = _current;
                displaced.RemoveFromHierarchy();
                _current = null;
                OnCardDisplaced?.Invoke(displaced);
            }

            card.RemoveDragManipulator();
            card.style.position = Position.Relative;
            card.style.left = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = StyleKeyword.Null;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _current = card;
            Add(card);
        }

        public void RemoveCard()
        {
            if (_current == null)
            {
                return;
            }

            _current.style.width = StyleKeyword.Null;
            _current.style.height = StyleKeyword.Null;
            _current.RemoveFromHierarchy();
            _current = null;
        }
    }
}

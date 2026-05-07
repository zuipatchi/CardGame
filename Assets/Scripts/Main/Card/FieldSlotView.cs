using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class FieldSlotView : VisualElement
    {
        public bool IsOccupied { get; private set; }

        public FieldSlotView()
        {
            AddToClassList("field-slot");
        }

        public void Place(CardView card)
        {
            if (IsOccupied)
            {
                return;
            }

            IsOccupied = true;
            AddToClassList("field-slot--occupied");
            card.style.position = Position.Relative;
            card.style.left = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            Add(card);
        }
    }
}

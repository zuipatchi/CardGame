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

        private CardView _current;

        public CardView CurrentCard => _current;
        public int Defense => _current?.Data.Defense ?? 0;

        public CharacterSlotView()
        {
            AddToClassList("character-slot-view");
            style.width = CardWidth;
            style.height = CardHeight;
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
            card.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            card.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
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

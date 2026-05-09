using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class FieldView : VisualElement
    {
        private const int MaxCards = 5;
        private readonly List<CardView> _cards = new List<CardView>();

        public bool IsFull => _cards.Count >= MaxCards;

        public IReadOnlyList<CardView> Cards => _cards;

        public FieldView()
        {
            AddToClassList("field-view");
        }

        public CardView TryGetCardAt(Vector2 worldPos)
        {
            foreach (CardView card in _cards)
            {
                if (card.worldBound.Contains(worldPos))
                {
                    return card;
                }
            }
            return null;
        }

        public void RemoveCard(CardView card)
        {
            _cards.Remove(card);
            card.RemoveFromHierarchy();
        }

        public bool TryPlace(CardView card, Vector2 worldPos)
        {
            if (IsFull || !worldBound.Contains(worldPos))
            {
                return false;
            }

            PlaceCard(card);
            return true;
        }

        public bool PlaceCard(CardView card)
        {
            if (IsFull)
            {
                return false;
            }

            card.RemoveDragManipulator();
            card.style.position = Position.Relative;
            card.style.left = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = 8;
            card.style.marginRight = 8;
            _cards.Add(card);
            Add(card);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class FieldView : VisualElement
    {
        private const int BaseCardCount = 5;
        private const float MinScale = 0.4f;
        private const float CardWidth = 160f;
        private const float BaseMargin = 4f;

        private readonly List<CardView> _cards = new List<CardView>();
        private readonly bool _isOpponent;

        public IReadOnlyList<CardView> Cards => _cards;

        public Action<CardView> OnCardClicked { get; set; }

        public float CurrentCardScale => _cards.Count <= BaseCardCount
            ? CardScaleConstants.FieldSlot
            : Mathf.Max(MinScale, CardScaleConstants.FieldSlot * BaseCardCount / _cards.Count);

        public FieldView(bool isOpponent = false)
        {
            _isOpponent = isOpponent;
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
            UpdateCardScales();
        }

        public bool TryPlace(CardView card, Vector2 worldPos)
        {
            if (!worldBound.Contains(worldPos))
            {
                return false;
            }

            PlaceCard(card);
            return true;
        }

        public void PlaceCard(CardView card)
        {
            card.RemoveDragManipulator();
            card.style.position = Position.Relative;
            card.style.left = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.transformOrigin = StyleKeyword.Null;
            _cards.Add(card);
            Add(card);

            CardView capturedCard = card;
            capturedCard.RegisterCallback<ClickEvent>(_ => OnCardClicked?.Invoke(capturedCard));

            UpdateCardScales();
        }

        private void UpdateCardScales()
        {
            float scale = CurrentCardScale;
            int margin = Mathf.RoundToInt((CardWidth / 2f) * (scale - CardScaleConstants.FieldSlot) + BaseMargin)
                         - Mathf.Max(0, _cards.Count - BaseCardCount);
            foreach (CardView card in _cards)
            {
                card.style.scale = new Scale(new Vector3(scale, scale, 1f));
                card.style.marginLeft = margin;
                card.style.marginRight = margin;
            }
        }
    }
}

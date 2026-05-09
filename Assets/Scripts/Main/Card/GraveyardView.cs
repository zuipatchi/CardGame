using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class GraveyardView : VisualElement
    {
        private const float StackOffsetX = 2f;
        private const float StackOffsetY = -2f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private readonly List<CardView> _cards = new List<CardView>();
        private readonly Label _countLabel;

        public int Count => _cards.Count;

        public GraveyardView()
        {
            AddToClassList("graveyard-view");
            style.position = Position.Relative;
            style.width = CardWidth;
            style.height = CardHeight;

            VisualElement badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("deck-count-badge-container");

            VisualElement badge = new VisualElement();
            badge.AddToClassList("deck-count-badge");

            VisualElement heartIcon = new VisualElement();
            heartIcon.AddToClassList("deck-count-heart-icon");
            badge.Add(heartIcon);

            _countLabel = new Label("0");
            _countLabel.AddToClassList("deck-count-label");
            badge.Add(_countLabel);

            badgeContainer.Add(badge);
            Add(badgeContainer);
        }

        public void AddCard(CardView card)
        {
            card.RemoveFromHierarchy();
            card.style.position = Position.Absolute;
            card.style.left = _cards.Count * StackOffsetX;
            card.style.top = _cards.Count * StackOffsetY;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            Insert(childCount - 1, card);
            _cards.Add(card);
            _countLabel.text = Count.ToString();
        }
    }
}

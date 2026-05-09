using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class DeckView : VisualElement
    {
        private const float StackOffsetX = 3f;
        private const float StackOffsetY = -3f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private readonly List<CardView> _deckCards = new List<CardView>();
        private readonly Label _countLabel;

        public int Count => _deckCards.Count;

        public DeckView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null)
        {
            style.position = Position.Relative;

            for (int i = 0; i < cards.Length; i++)
            {
                CardView card = new CardView(cardTemplate, cards[i], backImage, faceDown: true);
                card.style.position = Position.Absolute;
                card.style.left = (cards.Length - 1 - i) * StackOffsetX;
                card.style.top = (cards.Length - 1 - i) * StackOffsetY;
                _deckCards.Add(card);
                Add(card);
            }

            UpdateSize();

            VisualElement badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("deck-count-badge-container");

            VisualElement badge = new VisualElement();
            badge.AddToClassList("deck-count-badge");

            VisualElement heartIcon = new VisualElement();
            heartIcon.AddToClassList("deck-count-heart-icon");
            badge.Add(heartIcon);

            _countLabel = new Label(cards.Length.ToString());
            _countLabel.AddToClassList("deck-count-label");
            badge.Add(_countLabel);

            badgeContainer.Add(badge);
            Add(badgeContainer);
        }

        public void RemoveFromTop(int count)
        {
            int toRemove = Mathf.Min(count, _deckCards.Count);
            for (int i = 0; i < toRemove; i++)
            {
                CardView top = _deckCards[_deckCards.Count - 1];
                _deckCards.RemoveAt(_deckCards.Count - 1);
                top.RemoveFromHierarchy();
            }
            _countLabel.text = _deckCards.Count.ToString();
            UpdateSize();
        }

        private void UpdateSize()
        {
            int count = _deckCards.Count;
            style.width = count > 1 ? CardWidth + (count - 1) * StackOffsetX : CardWidth;
            style.height = count > 1 ? CardHeight + (count - 1) * Mathf.Abs(StackOffsetY) : CardHeight;
        }
    }
}

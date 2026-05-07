using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class DeckView : VisualElement
    {
        private const float StackOffsetX = 3f;
        private const float StackOffsetY = -3f;

        public DeckView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null)
        {
            style.position = Position.Relative;

            for (int i = 0; i < cards.Length; i++)
            {
                CardView card = new CardView(cardTemplate, cards[i], backImage, faceDown: true);
                card.style.position = Position.Absolute;
                card.style.left = (cards.Length - 1 - i) * StackOffsetX;
                card.style.top = (cards.Length - 1 - i) * StackOffsetY;
                Add(card);
            }

            if (cards.Length > 0)
            {
                style.width = 160 + (cards.Length - 1) * StackOffsetX;
                style.height = 220 + (cards.Length - 1) * Mathf.Abs(StackOffsetY);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class HandView : VisualElement
    {
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;
        private const float CardSpacing = 100f;
        private const float MaxAngleDeg = 20f;
        private const float ArcLiftMax = 20f;

        public HandView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null)
        {
            style.overflow = Overflow.Visible;

            if (cards.Length == 0)
            {
                return;
            }

            style.width = (cards.Length - 1) * CardSpacing + CardWidth;
            style.height = CardHeight + ArcLiftMax;

            float centerIndex = (cards.Length - 1) / 2f;

            for (int i = 0; i < cards.Length; i++)
            {
                float relativePos = cards.Length > 1 ? (i - centerIndex) / centerIndex : 0f;
                float angleDeg = relativePos * MaxAngleDeg;
                float arcLift = (1f - relativePos * relativePos) * ArcLiftMax;

                CardView card = new CardView(cardTemplate, cards[i], backImage);
                card.style.position = Position.Absolute;
                card.style.left = i * CardSpacing;
                card.style.bottom = arcLift;
                card.style.transformOrigin = new TransformOrigin(
                    new Length(50f, LengthUnit.Percent),
                    new Length(100f, LengthUnit.Percent)
                );
                card.style.rotate = new Rotate(angleDeg);

                Add(card);
            }
        }
    }
}

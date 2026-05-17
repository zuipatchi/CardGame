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

        private readonly VisualTreeAsset _cardTemplate;
        private readonly Texture2D _backImage;
        private readonly AttributeIconDatabaseSO _attrIconDb;
        private readonly List<CardView> _deckCards = new List<CardView>();
        private readonly Label _countLabel;

        public int Count => _deckCards.Count;

        public DeckView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null, AttributeIconDatabaseSO attrIconDb = null)
        {
            _cardTemplate = cardTemplate;
            _backImage = backImage;
            _attrIconDb = attrIconDb;
            AddToClassList("deck-view");
            style.position = Position.Relative;

            for (int i = 0; i < cards.Length; i++)
            {
                CardView card = new CardView(cardTemplate, cards[i], backImage, faceDown: true, attrIconDb);
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

        public CardData DrawTop()
        {
            if (_deckCards.Count == 0)
            {
                return null;
            }

            CardView top = _deckCards[_deckCards.Count - 1];
            _deckCards.RemoveAt(_deckCards.Count - 1);
            top.RemoveFromHierarchy();
            UpdateSize();
            return top.Data;
        }

        public void RefreshCount()
        {
            _countLabel.text = _deckCards.Count.ToString();
        }

        public List<CardView> TakeFromTop(int count)
        {
            int toTake = Mathf.Min(count, _deckCards.Count);
            List<CardView> taken = new List<CardView>(toTake);
            for (int i = 0; i < toTake; i++)
            {
                CardView top = _deckCards[_deckCards.Count - 1];
                _deckCards.RemoveAt(_deckCards.Count - 1);
                top.RemoveFromHierarchy();
                taken.Add(top);
            }
            UpdateSize();
            return taken;
        }

        public void DecrementDisplayCount()
        {
            if (int.TryParse(_countLabel.text, out int current))
            {
                _countLabel.text = Mathf.Max(0, current - 1).ToString();
            }
        }

        public void Rebuild(CardData[] cards)
        {
            foreach (CardView card in _deckCards)
            {
                card.RemoveFromHierarchy();
            }
            _deckCards.Clear();

            for (int i = 0; i < cards.Length; i++)
            {
                CardView card = new CardView(_cardTemplate, cards[i], _backImage, faceDown: true, _attrIconDb);
                card.style.position = Position.Absolute;
                card.style.left = (cards.Length - 1 - i) * StackOffsetX;
                card.style.top = (cards.Length - 1 - i) * StackOffsetY;
                _deckCards.Add(card);
                Insert(childCount - 1, card);
            }

            UpdateSize();
            RefreshCount();
        }

        private void UpdateSize()
        {
            int count = _deckCards.Count;
            style.width = count > 1 ? CardWidth + (count - 1) * StackOffsetX : CardWidth;
            style.height = count > 1 ? CardHeight + (count - 1) * Mathf.Abs(StackOffsetY) : CardHeight;
        }
    }
}

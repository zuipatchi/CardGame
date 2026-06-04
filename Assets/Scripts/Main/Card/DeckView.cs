using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class DeckView : VisualElement
    {
        private const float StackOffsetX = 1f;
        private const float StackOffsetY = -1f;
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;

        private readonly VisualTreeAsset _cardTemplate;
        private readonly Texture2D _backImage;
        private readonly List<CardView> _deckCards = new List<CardView>();
        private readonly Label _countLabel;
        private readonly VisualElement _defOverlay;
        private readonly Label _defLabel;
        private int _visualCount;

        public int Count => _deckCards.Count;
        public VisualElement DefOverlay => _defOverlay;
        public void SetDefValue(int def) => _defLabel.text = def.ToString();

        public DeckView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null)
        {
            _cardTemplate = cardTemplate;
            _backImage = backImage;
            AddToClassList("deck-view");
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

            _visualCount = cards.Length;
            UpdateSize();

            VisualElement badgeContainer = new VisualElement();
            badgeContainer.AddToClassList("deck-count-badge-container");
            badgeContainer.pickingMode = PickingMode.Ignore;

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

            _defOverlay = new VisualElement();
            _defOverlay.AddToClassList("deck-def-overlay");
            _defOverlay.pickingMode = PickingMode.Ignore;
            _defOverlay.style.display = DisplayStyle.None;
            _defOverlay.style.opacity = 0f;

            VisualElement defIcon = new VisualElement();
            defIcon.AddToClassList("deck-def-icon");
            defIcon.pickingMode = PickingMode.Ignore;
            _defOverlay.Add(defIcon);

            _defLabel = new Label("0");
            _defLabel.AddToClassList("deck-def-label");
            _defLabel.pickingMode = PickingMode.Ignore;
            _defOverlay.Add(_defLabel);

            Add(_defOverlay);
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
                // RemoveFromHierarchy は呼ばない。アニメーション側が1枚ずつ移動させる
                taken.Add(top);
            }
            return taken;
        }

        // アニメーションで1枚デッキから取り出したタイミングで呼ぶ（サイズ縮小 + カウント減算）
        public void OnCardRemovedVisually()
        {
            _visualCount = Mathf.Max(0, _visualCount - 1);
            UpdateSize(_visualCount);
            if (int.TryParse(_countLabel.text, out int current))
            {
                _countLabel.text = Mathf.Max(0, current - 1).ToString();
            }
        }

        public void AddCardsAndShuffle(IReadOnlyList<CardData> cards)
        {
            CardData[] combined = new CardData[_deckCards.Count + cards.Count];
            for (int i = 0; i < _deckCards.Count; i++)
            {
                combined[i] = _deckCards[i].Data;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                combined[_deckCards.Count + i] = cards[i];
            }
            for (int i = combined.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (combined[i], combined[j]) = (combined[j], combined[i]);
            }
            Rebuild(combined);
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
                CardView card = new CardView(_cardTemplate, cards[i], _backImage, faceDown: true);
                card.style.position = Position.Absolute;
                card.style.left = (cards.Length - 1 - i) * StackOffsetX;
                card.style.top = (cards.Length - 1 - i) * StackOffsetY;
                _deckCards.Add(card);
                Insert(i, card);
            }

            _visualCount = cards.Length;
            UpdateSize();
            RefreshCount();
        }

        private void UpdateSize() => UpdateSize(_deckCards.Count);

        private void UpdateSize(int count)
        {
            style.width = count > 1 ? CardWidth + (count - 1) * StackOffsetX : CardWidth;
            style.height = count > 1 ? CardHeight + (count - 1) * Mathf.Abs(StackOffsetY) : CardHeight;
        }
    }
}

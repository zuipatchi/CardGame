using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class DeckView : VisualElement
    {
        private const float StackOffsetX = 1f;
        private const float StackOffsetY = -1f;

        private readonly VisualTreeAsset _cardTemplate;
        private readonly Texture2D _backImage;
        private readonly bool _isOpponent;
        private readonly List<CardView> _deckCards = new List<CardView>();
        private readonly Label _countLabel;
        private readonly VisualElement _blueWinIcon;
        private int _visualCount;

        public int Count => _deckCards.Count;

        public DeckView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null, bool isOpponent = false)
        {
            _cardTemplate = cardTemplate;
            _backImage = backImage;
            _isOpponent = isOpponent;
            AddToClassList("deck-view");
            style.position = Position.Relative;

            for (int i = 0; i < cards.Length; i++)
            {
                CardView card = new CardView(cardTemplate, cards[i], backImage, faceDown: true, isOpponent: _isOpponent);
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

            // WaterIcon を背面に敷き、その上に枚数ラベルを重ねる（墓地アイコンと同様）
            _blueWinIcon = new VisualElement();
            _blueWinIcon.AddToClassList("deck-blue-win-icon");
            _blueWinIcon.style.display = DisplayStyle.None;
            badge.Add(_blueWinIcon);

            _countLabel = new Label(cards.Length.ToString());
            _countLabel.AddToClassList("deck-count-label");
            badge.Add(_countLabel);

            badgeContainer.Add(badge);
            Add(badgeContainer);

        }

        // 青属性の勝利条件を武装したことを示す WaterIcon を枚数表示に重ねて出す（一度出したら永続）
        public void ShowBlueWinIcon()
        {
            _blueWinIcon.style.display = DisplayStyle.Flex;
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

        public string[] GetCardIds()
        {
            string[] ids = new string[_deckCards.Count];
            for (int i = 0; i < _deckCards.Count; i++)
            {
                ids[i] = _deckCards[i].Data.Id;
            }
            return ids;
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
                CardView card = new CardView(_cardTemplate, cards[i], _backImage, faceDown: true, isOpponent: _isOpponent);
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
            style.width = count > 1 ? CardScaleConstants.CardWidth + (count - 1) * StackOffsetX : CardScaleConstants.CardWidth;
            style.height = count > 1 ? CardScaleConstants.CardHeight + (count - 1) * Mathf.Abs(StackOffsetY) : CardScaleConstants.CardHeight;
        }
    }
}

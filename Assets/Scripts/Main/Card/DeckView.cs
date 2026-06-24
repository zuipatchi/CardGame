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
        private readonly VisualElement _badgeContainer;
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

            // HeartIcon を背面に敷き、その上に枚数ラベルを重ねる（墓地アイコンと同様）
            VisualElement countIcon = new VisualElement();
            countIcon.AddToClassList("deck-count-icon");
            countIcon.pickingMode = PickingMode.Ignore;
            badge.Add(countIcon);

            _countLabel = new Label(cards.Length.ToString());
            _countLabel.AddToClassList("deck-count-label");
            badge.Add(_countLabel);

            badgeContainer.Add(badge);
            Add(badgeContainer);
            _badgeContainer = badgeContainer;

        }

        // 枚数バッジ（ハートアイコン）の直前にオーバーレイ要素を差し込む。
        // デッキカードより前面・ハートアイコンより背面に描画されるため、
        // シャッフル演出のフェイクパケットをハートの後ろに置くのに使う。
        public void InsertOverlayBehindBadge(VisualElement overlay)
        {
            int badgeIndex = IndexOf(_badgeContainer);
            if (badgeIndex < 0)
            {
                Add(overlay);
                return;
            }
            Insert(badgeIndex, overlay);
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

        // デッキ内のカードデータを並び順のスナップショットで返す（インデックスは _deckCards と一致）。
        // SummonFromDeckByKeyword の候補抽出・オンライン同期（インデックス指定）に使う。
        public IReadOnlyList<CardData> GetCardDataSnapshot()
        {
            CardData[] data = new CardData[_deckCards.Count];
            for (int i = 0; i < _deckCards.Count; i++)
            {
                data[i] = _deckCards[i].Data;
            }
            return data;
        }

        // 指定インデックスのカードをデッキから取り除いてそのデータを返す（範囲外なら null）。
        // 両クライアントのデッキ並び順は同期済みのため、同じインデックスで同じカードを取り除ける。
        // （基底 VisualElement.RemoveAt と区別するため Card 付きの名前にしている）
        public CardData RemoveCardAt(int index)
        {
            if (index < 0 || index >= _deckCards.Count)
            {
                return null;
            }
            CardView card = _deckCards[index];
            _deckCards.RemoveAt(index);
            card.RemoveFromHierarchy();
            UpdateSize();
            return card.Data;
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

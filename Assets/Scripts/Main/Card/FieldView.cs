using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class FieldView : VisualElement
    {
        // フィールドに並べられるキャラの上限（通常配置・召喚など全配置経路で共通）
        public const int MaxCharacters = 9;

        private const int BaseCardCount = 5;
        private const float MinScale = 0.4f;
        private const float BaseMargin = 4f;

        private readonly List<CardView> _cards = new List<CardView>();

        // Characters のキャッシュ。_cards が変化したときだけ作り直す（呼び出しごとの ToList アロケーションを避ける）。
        private readonly List<CardView> _characters = new List<CardView>();
        private bool _charactersDirty = true;

        private readonly bool _isOpponent;

        public IReadOnlyList<CardView> Cards => _cards;

        public IReadOnlyList<CardView> Characters
        {
            get
            {
                if (_charactersDirty)
                {
                    _characters.Clear();
                    foreach (CardView card in _cards)
                    {
                        if (card.Data is CharacterCardData)
                        {
                            _characters.Add(card);
                        }
                    }
                    _charactersDirty = false;
                }
                return _characters;
            }
        }

        // キャラ数が上限に達しているか（イベントカードは墓地行きのため対象外）
        public bool IsCharactersFull => Characters.Count >= MaxCharacters;

        // 制圧勝利（キャラ8体）のカウント対象キャラ数。
        // お邪魔トークン（CharacterCardData.ExcludeFromDomination）は除外する。
        public int CountCharsForDominationWin()
        {
            int count = 0;
            foreach (CardView card in Characters)
            {
                if (card.Data is CharacterCardData character && character.ExcludeFromDomination)
                {
                    continue;
                }
                count++;
            }
            return count;
        }

        public bool Contains(CardView card) => _cards.Contains(card);

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
            _charactersDirty = true;
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
            card.ResetCurrentHp();
            card.RemoveDragManipulator();
            card.style.position = Position.Relative;
            card.style.left = StyleKeyword.Null;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.transformOrigin = StyleKeyword.Null;
            _cards.Add(card);
            _charactersDirty = true;
            Add(card);

            CardView capturedCard = card;
            capturedCard.RegisterCallback<ClickEvent>(_ => OnCardClicked?.Invoke(capturedCard));

            UpdateCardScales();
        }

        private void UpdateCardScales()
        {
            float scale = CurrentCardScale;
            int margin = Mathf.RoundToInt((CardScaleConstants.CardWidth / 2f) * (scale - CardScaleConstants.FieldSlot) + BaseMargin)
                         - Mathf.Max(0, _cards.Count - BaseCardCount);
            foreach (CardView card in _cards)
            {
                card.SetFieldScale(scale);
                card.style.marginLeft = margin;
                card.style.marginRight = margin;
            }
        }
    }
}

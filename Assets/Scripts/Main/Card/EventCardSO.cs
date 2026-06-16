using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "EventCards", menuName = "Card/Event Cards")]
    public sealed class EventCardSO : ScriptableObject
    {
        // この SO が扱う属性。所属する全カードの属性をこの値に揃える
        [SerializeField] private CardAttribute _attribute;
        [SerializeField] private List<EventCardData> _cards;

        public IReadOnlyList<EventCardData> Cards => _cards;

#if UNITY_EDITOR
        // 所属カードの属性を SO の属性に揃えたうえで、属性ごとに E#### を自動採番する
        // （採番規則は CardIdAutoAssigner 参照）
        private void OnValidate()
        {
            bool changed = ApplyAttributeToCards();
            if (CardIdAutoAssigner.AssignIds(_cards, "E"))
            {
                changed = true;
            }
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private bool ApplyAttributeToCards()
        {
            if (_cards == null || _cards.Count == 0)
            {
                return false;
            }

            // 複数属性が混在する SO は移行前のレガシー（単一 EventCards.asset）とみなし、
            // 属性を上書きしない（再コンパイル時の OnValidate で属性が壊れるのを防ぐ）
            bool hasFirst = false;
            CardAttribute first = default;
            foreach (EventCardData card in _cards)
            {
                if (card == null)
                {
                    continue;
                }
                if (!hasFirst)
                {
                    first = card.Attribute;
                    hasFirst = true;
                }
                else if (card.Attribute != first)
                {
                    return false;
                }
            }

            bool changed = false;
            foreach (EventCardData card in _cards)
            {
                if (card == null)
                {
                    continue;
                }
                if (card.Attribute != _attribute)
                {
                    card.EditorSetAttribute(_attribute);
                    changed = true;
                }
            }
            return changed;
        }

        // 移行ツール用：属性とカードリストを設定し、属性適用＋ID採番を行う
        public void EditorSetCards(CardAttribute attribute, List<EventCardData> cards)
        {
            _attribute = attribute;
            _cards = cards;
            ApplyAttributeToCards();
            CardIdAutoAssigner.AssignIds(_cards, "E");
        }
#endif
    }
}

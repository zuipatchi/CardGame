using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CharacterCards", menuName = "Card/Character Cards")]
    public sealed class CharacterCardSO : ScriptableObject
    {
        // この SO が扱う属性。所属する全カードの属性をこの値に揃える
        [SerializeField] private CardAttribute _attribute;
        // この SO が扱うリリース弾（第N弾。1始まり）。所属する全カードの弾をこの値に揃える。
        // ID は属性×1000 +（弾-1）×100 + 連番で採番する（CardIdAutoAssigner 参照）。
        [SerializeField] private int _set = 1;
        [SerializeField] private List<CharacterCardData> _cards;

        public IReadOnlyList<CharacterCardData> Cards => _cards;

#if UNITY_EDITOR
        // この SO のリリース弾（カードエディタが追加先 SO の解決・自動生成に使う）
        public int EditorSet => _set;

        // 所属カードの属性・弾を SO の値に揃えたうえで、属性×弾ごとに C#### を自動採番する
        // （採番規則は CardIdAutoAssigner 参照）
        private void OnValidate()
        {
            bool changed = ApplyAttributeToCards();
            if (ApplySetToCards())
            {
                changed = true;
            }
            if (CardIdAutoAssigner.AssignIds(_cards, "C", _set))
            {
                changed = true;
            }
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        // 所属カードの弾を SO の弾に揃える（属性と同じく SO 側で一括管理する）
        private bool ApplySetToCards()
        {
            if (_cards == null || _cards.Count == 0)
            {
                return false;
            }

            bool changed = false;
            foreach (CharacterCardData card in _cards)
            {
                if (card == null)
                {
                    continue;
                }
                if (card.Set != (_set <= 0 ? 1 : _set))
                {
                    card.EditorSetSet(_set);
                    changed = true;
                }
            }
            return changed;
        }

        private bool ApplyAttributeToCards()
        {
            if (_cards == null || _cards.Count == 0)
            {
                return false;
            }

            // 複数属性が混在する SO は移行前のレガシー（単一 CharacterCard.asset）とみなし、
            // 属性を上書きしない（再コンパイル時の OnValidate で属性が壊れるのを防ぐ）
            bool hasFirst = false;
            CardAttribute first = default;
            foreach (CharacterCardData card in _cards)
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
            foreach (CharacterCardData card in _cards)
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

        // 移行ツール・カードエディタの SO 自動生成用：属性・弾・カードリストを設定し、属性／弾適用＋ID採番を行う
        public void EditorSetCards(CardAttribute attribute, List<CharacterCardData> cards, int set = 1)
        {
            _attribute = attribute;
            _set = set;
            _cards = cards;
            ApplyAttributeToCards();
            ApplySetToCards();
            CardIdAutoAssigner.AssignIds(_cards, "C", _set);
        }
#endif
    }
}

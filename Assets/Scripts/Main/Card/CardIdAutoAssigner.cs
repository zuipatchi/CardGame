#if UNITY_EDITOR
using System.Collections.Generic;

namespace Main.Card
{
    // カード SO のリスト要素にリスト順で ID を自動採番するエディタ専用ヘルパー。
    // 先頭から prefix + 3桁連番（C001, C002, …）を割り当て、並び替え時も常に振り直す。
    public static class CardIdAutoAssigner
    {
        // 採番を行った場合は true を返す（呼び出し側で SetDirty するため）
        public static bool AssignIds<T>(IReadOnlyList<T> cards, string prefix) where T : CardData
        {
            if (cards == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < cards.Count; i++)
            {
                T card = cards[i];
                if (card == null)
                {
                    continue;
                }

                string expected = $"{prefix}{i + 1:D3}";
                if (card.Id != expected)
                {
                    card.EditorSetId(expected);
                    changed = true;
                }
            }

            return changed;
        }
    }
}
#endif

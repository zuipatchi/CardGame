#if UNITY_EDITOR
using System.Collections.Generic;

namespace Main.Card
{
    // カード SO のリスト要素に ID を自動採番するエディタ専用ヘルパー。
    // 属性ごとに SO を分けて管理するため、ID は属性で 1000 番台を分ける：
    //   番号 = (属性番号)×1000 + リスト内連番（1始まり）   例: 赤(属性番号1)の1枚目 = C1001、青(2)の1枚目 = C2001
    //   属性番号 = (int)CardAttribute + 1（Red=1, Blue=2, …, White=7。最大9属性）
    //   1属性あたり最大999枚。各 SO は単一属性のカードのみを持つ前提（属性別 SO）。
    // これにより属性別 SO 間でも ID が一意になり、"C{番号}" 形式を保つ（SummonChar 等と互換）。
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

                int number = ((int)card.Attribute + 1) * 1000 + (i + 1);
                string expected = $"{prefix}{number}";
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

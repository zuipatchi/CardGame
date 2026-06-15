#if UNITY_EDITOR
using System.Collections.Generic;

namespace Main.Card
{
    // カード SO のリスト要素に ID を自動採番するエディタ専用ヘルパー。
    // 属性ごとに SO を分けて管理するため、ID は属性で 1000 番台を分ける：
    //   番号 = (属性番号)×1000 + リスト内連番（1始まり）   例: 白(属性番号1)の1枚目 = C1001、青(2)の1枚目 = C2001
    //   属性番号: White=1, Blue=2, Green=3, Yellow=4, Red=5, Black=6, Purple=7（AttributeNumber 参照）
    //   1属性あたり最大999枚。各 SO は単一属性のカードのみを持つ前提（属性別 SO）。
    // これにより属性別 SO 間でも ID が一意になり、"C{番号}" 形式を保つ（SummonChar 等と互換）。
    public static class CardIdAutoAssigner
    {
        // 属性 → ID の 1000 番台番号。enum 順とは独立した固定マッピング。
        public static int AttributeNumber(CardAttribute attribute)
        {
            switch (attribute)
            {
                case CardAttribute.White:
                    return 1;
                case CardAttribute.Blue:
                    return 2;
                case CardAttribute.Green:
                    return 3;
                case CardAttribute.Yellow:
                    return 4;
                case CardAttribute.Red:
                    return 5;
                case CardAttribute.Black:
                    return 6;
                case CardAttribute.Purple:
                    return 7;
                default:
                    return 9;
            }
        }

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

                int number = AttributeNumber(card.Attribute) * 1000 + (i + 1);
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

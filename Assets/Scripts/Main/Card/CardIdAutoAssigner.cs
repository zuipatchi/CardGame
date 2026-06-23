#if UNITY_EDITOR
using System.Collections.Generic;

namespace Main.Card
{
    // カード SO のリスト要素に ID を自動採番するエディタ専用ヘルパー。
    // 属性 × 弾（第N弾）ごとに SO を分けて管理するため、ID は属性で 1000 番台、弾で 100 番台ブロックに分ける：
    //   番号 = (属性番号)×1000 + (弾-1)×SetBlockSize + リスト内連番（1始まり）
    //   例: 白(属性番号1)第一弾の1枚目 = C1001、白第二弾の1枚目 = C1101、青(2)第一弾の1枚目 = C2001
    //   属性番号: White=1, Blue=2, Green=3, Yellow=4, Red=5, Black=6, Purple=7（AttributeNumber 参照）
    //   弾: 1始まり。0（未設定）は第一弾（オフセット0）として扱うため、既存アセットの ID は変わらない。
    //   1属性1弾あたり最大 SetBlockSize-1（=99）枚・最大9弾。各 SO は単一属性・単一弾のカードのみを持つ前提。
    // これにより属性別・弾別 SO 間でも ID が一意になり、"C{番号}" 形式を保つ（SummonChar 等と互換）。
    public static class CardIdAutoAssigner
    {
        // 弾ごとの ID ブロック幅。属性の 1000 番台を弾で分割する（弾1: 1-99、弾2: 101-199、…弾9: 801-899）。
        public const int SetBlockSize = 100;

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

        // 弾番号（第N弾）→ ID のブロックオフセット。0/1（未設定・第一弾）はオフセット 0（既存 ID を維持）。
        public static int SetOffset(int set)
        {
            return (set > 1 ? set - 1 : 0) * SetBlockSize;
        }

        // 採番を行った場合は true を返す（呼び出し側で SetDirty するため）。
        // set は所属 SO の弾番号（第N弾）。同一属性でも弾ごとに ID ブロックがずれるため衝突しない。
        public static bool AssignIds<T>(IReadOnlyList<T> cards, string prefix, int set) where T : CardData
        {
            if (cards == null)
            {
                return false;
            }

            int offset = SetOffset(set);
            bool changed = false;
            for (int i = 0; i < cards.Count; i++)
            {
                T card = cards[i];
                if (card == null)
                {
                    continue;
                }

                int number = AttributeNumber(card.Attribute) * 1000 + offset + (i + 1);
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

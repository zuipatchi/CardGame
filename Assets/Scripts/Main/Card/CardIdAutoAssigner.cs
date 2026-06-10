#if UNITY_EDITOR
using System.Collections.Generic;

namespace Main.Card
{
    // カード SO のリスト要素に一意の ID を自動採番するエディタ専用ヘルパー。
    // 空の ID と重複した ID のみ採番し、既存の一意な ID は変更しない。
    public static class CardIdAutoAssigner
    {
        // 採番を行った場合は true を返す（呼び出し側で SetDirty するため）
        public static bool AssignIds<T>(IReadOnlyList<T> cards, string prefix) where T : CardData
        {
            if (cards == null)
            {
                return false;
            }

            HashSet<string> seenIds = new HashSet<string>();
            HashSet<int> usedNumbers = new HashSet<int>();
            List<T> needsId = new List<T>();

            foreach (T card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                string id = card.Id;
                if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                {
                    // 空 ID、または先行要素と重複 → 採番対象
                    needsId.Add(card);
                    continue;
                }

                if (id.StartsWith(prefix) && int.TryParse(id.Substring(prefix.Length), out int number))
                {
                    usedNumbers.Add(number);
                }
            }

            if (needsId.Count == 0)
            {
                return false;
            }

            int next = 1;
            foreach (T card in needsId)
            {
                while (usedNumbers.Contains(next))
                {
                    next++;
                }
                usedNumbers.Add(next);
                card.EditorSetId($"{prefix}{next:D3}");
            }

            return true;
        }
    }
}
#endif

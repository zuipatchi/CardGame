using System;
using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class CpuAgent
    {
        // メインフェーズ（キャラを出す）：支払い可能なキャラカードのインデックスを返す。なければ -1（パス）。
        // canAfford(i) は hand[i] のコストを支払えるかの判定（null なら常に支払い可能扱い）
        public static int ChooseCharacterSetCardIndex(IReadOnlyList<CardData> hand, Func<int, bool> canAfford = null)
        {
            return FindFirst<CharacterCardData>(hand, canAfford);
        }

        // メインフェーズ（イベントを使う）：支払い可能なイベントカードのインデックスを返す。なければ -1（パス）。
        // canAfford(i) は hand[i] のコストを支払えるかの判定（null なら常に支払い可能扱い）
        public static int ChooseEventCardIndex(IReadOnlyList<CardData> hand, Func<int, bool> canAfford = null)
        {
            return FindFirst<EventCardData>(hand, canAfford);
        }

        // 進化効果：sacrificedCost より高コストのキャラカードの中で最高コストのインデックスを返す
        public static int ChooseEvolveCardIndex(IReadOnlyList<CardData> hand, int sacrificedCost)
        {
            int bestIdx = -1;
            int bestCost = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is CharacterCardData && hand[i].Cost > sacrificedCost && hand[i].Cost > bestCost)
                {
                    bestIdx = i;
                    bestCost = hand[i].Cost;
                }
            }
            return bestIdx;
        }

        // ハート攻撃：ATK 1 以上のキャラの中で最高 ATK のインデックスを返す。いなければ -1
        public static int ChooseHeartAttacker(IReadOnlyList<CardData> ownChars)
        {
            int bestIdx = -1;
            int bestAtk = 0;
            for (int i = 0; i < ownChars.Count; i++)
            {
                if (ownChars[i].Attack > bestAtk)
                {
                    bestAtk = ownChars[i].Attack;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static int FindFirst<T>(IReadOnlyList<CardData> hand, Func<int, bool> canAfford) where T : CardData
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is T && (canAfford == null || canAfford(i)))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

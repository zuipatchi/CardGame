using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class CpuAgent
    {
        // メインフェーズ（キャラを出す）：キャラカードのインデックスを返す。なければ -1（パス）
        public static int ChooseCharacterSetCardIndex(IReadOnlyList<CardData> hand)
        {
            return FindFirst<CharacterCardData>(hand);
        }

        // メインフェーズ（イベントを使う）：イベントカードのインデックスを返す。なければ -1（パス）
        public static int ChooseEventCardIndex(IReadOnlyList<CardData> hand)
        {
            return FindFirst<EventCardData>(hand);
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

        private static int FindFirst<T>(IReadOnlyList<CardData> hand) where T : CardData
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class CpuAgent
    {
        // キャラセットフェーズ：キャラカードのインデックスを返す。なければ -1（パス）
        public static int ChooseCharacterSetCardIndex(IReadOnlyList<CardData> hand)
        {
            return FindFirst<CharacterCardData>(hand);
        }

        // 戦闘前2フェーズ：イベントカードのインデックスを返す。なければ -1（パス）
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

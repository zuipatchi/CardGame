using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class CpuAgent
    {
        // 準備フェーズ：Ready にするカードのインデックスを返す。なければ -1（パス）
        public static int ChooseCardToReadyIndex(IReadOnlyList<CardData> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is CharacterCardData || hand[i] is EventCardData)
                {
                    return i;
                }
            }

            return -1;
        }

        // 戦闘前フェーズ：プレイする技カード1枚のインデックスを返す。なければ -1（パス）
        public static int ChooseSkillCardIndex(IReadOnlyList<CardData> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is SkillCardData)
                {
                    return i;
                }
            }

            return -1;
        }

        // 戦闘前フェーズ：プレイする技カードのインデックスリストを返す
        public static List<int> ChooseSkillCardIndices(IReadOnlyList<CardData> hand)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is SkillCardData)
                {
                    result.Add(i);
                }
            }

            return result;
        }
    }
}

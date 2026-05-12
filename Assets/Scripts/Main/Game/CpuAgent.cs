using System.Collections.Generic;
using Main.Card;

namespace Main.Game
{
    public sealed class CpuAgent
    {
        // 戦闘前1フェーズ：キャラカード優先、なければ技カードのインデックスを返す
        public static int ChoosePreBattle1CardIndex(IReadOnlyList<CardData> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is CharacterCardData)
                {
                    return i;
                }
            }

            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is SkillCardData)
                {
                    return i;
                }
            }

            return -1;
        }

        // 戦闘前2フェーズ：イベントカードのインデックスを返す。なければ -1（パス）
        public static int ChooseEventCardIndex(IReadOnlyList<CardData> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is EventCardData)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

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

        // 戦闘前1フェーズ：技カードのインデックスを返す（キャラカードは出せない）
        public static int ChoosePreBattle1CardIndex(IReadOnlyList<CardData> hand)
        {
            return FindFirst<SkillCardData>(hand);
        }

        // 戦闘前2フェーズ：イベントカードのインデックスを返す。なければ -1（パス）
        public static int ChooseEventCardIndex(IReadOnlyList<CardData> hand)
        {
            return FindFirst<EventCardData>(hand);
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

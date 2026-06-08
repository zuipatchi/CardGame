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

        // メインフェーズ攻撃：最高ATKキャラを選ぶ。相手キャラがいればATK最小を狙う。いなければデッキ直撃 (targetIdx = -1)
        public static (int attackerIdx, int targetIdx) ChooseBattleAttack(
            IReadOnlyList<CardData> ownChars, IReadOnlyList<CardData> opponentChars)
        {
            if (ownChars.Count == 0)
            {
                return (-1, -1);
            }

            int attackerIdx = 0;
            int bestAtk = ownChars[0].Attack;
            for (int i = 1; i < ownChars.Count; i++)
            {
                if (ownChars[i].Attack > bestAtk)
                {
                    bestAtk = ownChars[i].Attack;
                    attackerIdx = i;
                }
            }

            if (opponentChars.Count == 0)
            {
                return (attackerIdx, -1);
            }

            int targetIdx = 0;
            int minAtk = opponentChars[0].Attack;
            for (int i = 1; i < opponentChars.Count; i++)
            {
                if (opponentChars[i].Attack < minAtk)
                {
                    minAtk = opponentChars[i].Attack;
                    targetIdx = i;
                }
            }
            return (attackerIdx, targetIdx);
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

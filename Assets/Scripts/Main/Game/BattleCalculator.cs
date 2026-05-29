using System.Collections.Generic;
using System.Linq;
using Main.Card;

namespace Main.Game
{
    internal static class BattleCalculator
    {
        internal readonly struct SideBattleStats
        {
            internal readonly int ATK;

            internal SideBattleStats(int atk)
            {
                ATK = atk;
            }
        }

        internal static SideBattleStats Calculate(
            IReadOnlyList<CardView> skills,
            int atkBoost,
            bool hasAttackingChar,
            int charAttack)
        {
            int atk = hasAttackingChar
                ? charAttack + skills.Sum(c => c.Data.Attack) + atkBoost
                : 0;
            return new SideBattleStats(atk);
        }
    }
}

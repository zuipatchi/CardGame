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
            internal readonly bool TypeMatch;
            internal readonly bool WeaknessHit;
            internal readonly bool StrengthBlocked;

            internal SideBattleStats(int atk, bool typeMatch, bool weaknessHit, bool strengthBlocked)
            {
                ATK = atk;
                TypeMatch = typeMatch;
                WeaknessHit = weaknessHit;
                StrengthBlocked = strengthBlocked;
            }
        }

        internal static SideBattleStats Calculate(
            IReadOnlyList<CardView> skills,
            CardAttribute ownCharAttr,
            CardAttribute opponentCharAttr,
            int atkBoost,
            bool hasAttackingChar,
            AttributeDatabaseSO attrDb)
        {
            bool typeMatch = skills.Any(c => c.Data.Attribute != CardAttribute.None && c.Data.Attribute == ownCharAttr);
            CardAttribute opponentWeakness = attrDb != null ? attrDb.GetWeakness(opponentCharAttr) : CardAttribute.None;
            bool weaknessHit = opponentWeakness != CardAttribute.None && skills.Any(c => c.Data.Attribute == opponentWeakness);
            CardAttribute opponentStrength = attrDb != null ? attrDb.GetStrength(opponentCharAttr) : CardAttribute.None;
            bool strengthBlocked = opponentStrength != CardAttribute.None && skills.Any(c => c.Data.Attribute == opponentStrength);
            int atk = hasAttackingChar && !strengthBlocked
                ? (skills.Sum(c => c.Data.Attack) + atkBoost) * (typeMatch ? 2 : 1) * (weaknessHit ? 3 : 1)
                : 0;
            return new SideBattleStats(atk, typeMatch, weaknessHit, strengthBlocked);
        }
    }
}

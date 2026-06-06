namespace Main.Game
{
    internal static class BattleCalculator
    {
        internal static int Calculate(int atkBoost, bool hasAttackingChar, int charAttack)
        {
            return hasAttackingChar ? charAttack + atkBoost : 0;
        }
    }
}

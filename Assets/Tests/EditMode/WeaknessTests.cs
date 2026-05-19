using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class WeaknessTests
    {
        // MainPresenter の ATK 計算式を再現
        // ATK = (skillTotal + atkBoost) × (typeMatch ? 2 : 1) × (weaknessHit ? 3 : 1)
        private static int CalcATK(int skillTotal, int atkBoost, CardAttribute skillAttr, CardAttribute charAttr, CardAttribute opponentWeakness)
        {
            bool typeMatch = skillAttr != CardAttribute.None && skillAttr == charAttr;
            bool weaknessHit = opponentWeakness != CardAttribute.None && skillAttr == opponentWeakness;
            return (skillTotal + atkBoost) * (typeMatch ? 2 : 1) * (weaknessHit ? 3 : 1);
        }

        [Test]
        public void 弱点を突いた場合ダメージが3倍になる()
        {
            // Fire技(3) で Fire弱点キャラを攻撃 → 3×3 = 9
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.Poison, CardAttribute.Fire);
            Assert.AreEqual(9, result);
        }

        [Test]
        public void 弱点でない属性では弱点倍率なし()
        {
            // Poison技(3) で Fire弱点Patchiキャラを攻撃 → タイプ不一致・弱点不一致 → 3×1 = 3
            int result = CalcATK(3, 0, CardAttribute.Poison, CardAttribute.Patchi, CardAttribute.Fire);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void タイプ一致かつ弱点を突いた場合6倍になる()
        {
            // Fire技(3) で Fire属性かつFire弱点キャラを攻撃 → 3×2×3 = 18
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.Fire, CardAttribute.Fire);
            Assert.AreEqual(18, result);
        }

        [Test]
        public void 相手キャラの弱点がNoneの場合弱点ボーナスなし()
        {
            // Fire技(3) で弱点Noneキャラを攻撃 → 3×1 = 3
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.Poison, CardAttribute.None);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void AtkBoostを含む弱点攻撃は正しく計算される()
        {
            // Fire技(3) + AtkBoost(2) で Fire弱点キャラを攻撃 → (3+2)×3 = 15
            int result = CalcATK(3, 2, CardAttribute.Fire, CardAttribute.Poison, CardAttribute.Fire);
            Assert.AreEqual(15, result);
        }

        [Test]
        public void AtkBoostを含むタイプ一致かつ弱点は正しく計算される()
        {
            // Fire技(3) + AtkBoost(2) でFireかつFire弱点 → (3+2)×2×3 = 30
            int result = CalcATK(3, 2, CardAttribute.Fire, CardAttribute.Fire, CardAttribute.Fire);
            Assert.AreEqual(30, result);
        }
    }
}

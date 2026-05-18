using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class AttributeBonusTests
    {
        // MainPresenter の ATK 計算式を再現
        // ATK = (skillTotal + atkBoost) × (typeMatch ? 2 : 1)
        private static int CalcATK(int skillTotal, int atkBoost, CardAttribute skillAttr, CardAttribute charAttr)
        {
            bool typeMatch = skillAttr != CardAttribute.None && skillAttr == charAttr;
            return (skillTotal + atkBoost) * (typeMatch ? 2 : 1);
        }

        [Test]
        public void 属性が一致する技はダメージが2倍になる()
        {
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.Fire);
            Assert.AreEqual(6, result);
        }

        [Test]
        public void 属性が不一致の技はダメージが等倍になる()
        {
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.Poison);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void キャラの属性がNoneの場合はボーナスなし()
        {
            int result = CalcATK(3, 0, CardAttribute.Fire, CardAttribute.None);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void 技の属性がNoneの場合はボーナスなし()
        {
            int result = CalcATK(3, 0, CardAttribute.None, CardAttribute.Fire);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void AtkBoostはタイプ一致ボーナスの前に加算される()
        {
            // Fire技(3) + AtkBoost(2) → (3+2)×2 = 10
            int result = CalcATK(3, 2, CardAttribute.Fire, CardAttribute.Fire);
            Assert.AreEqual(10, result);
        }

        [Test]
        public void AtkBoostはタイプ不一致のとき倍率なしで加算される()
        {
            // Fire技(3) + AtkBoost(2) → 3+2 = 5
            int result = CalcATK(3, 2, CardAttribute.Fire, CardAttribute.Poison);
            Assert.AreEqual(5, result);
        }
    }
}

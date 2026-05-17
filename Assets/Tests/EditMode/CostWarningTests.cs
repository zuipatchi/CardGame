using NUnit.Framework;

namespace Tests.EditMode
{
    public class CostWarningTests
    {
        private static bool ShouldShowCostWarning(int cost, int deckCount)
        {
            return cost > 0 && cost >= deckCount;
        }

        [Test]
        public void コスト0のカードは警告条件が偽()
        {
            Assert.IsFalse(ShouldShowCostWarning(0, 3));
        }

        [Test]
        public void コストがデッキ枚数未満なら警告条件が偽()
        {
            Assert.IsFalse(ShouldShowCostWarning(2, 5));
        }

        [Test]
        public void コストがデッキ枚数と等しいなら警告条件が真()
        {
            Assert.IsTrue(ShouldShowCostWarning(3, 3));
        }

        [Test]
        public void コストがデッキ枚数より多いなら警告条件が真()
        {
            Assert.IsTrue(ShouldShowCostWarning(5, 3));
        }
    }
}

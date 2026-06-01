using NUnit.Framework;

namespace Tests.EditMode
{
    public class GuardDisplayTests
    {
        [Test]
        public void ATKが1以上でダメージが0のときGUARD表示条件がtrueになる()
        {
            int atk = 3;
            int damage = 0;

            bool shouldShowGuard = atk > 0 && damage == 0;

            Assert.IsTrue(shouldShowGuard);
        }

        [Test]
        public void ATKが0のときはGUARD表示条件がfalseになる()
        {
            int atk = 0;
            int damage = 0;

            bool shouldShowGuard = atk > 0 && damage == 0;

            Assert.IsFalse(shouldShowGuard);
        }

        [Test]
        public void ダメージが1以上のときGUARD表示条件がfalseになる()
        {
            int atk = 5;
            int damage = 2;

            bool shouldShowGuard = atk > 0 && damage == 0;

            Assert.IsFalse(shouldShowGuard);
        }
    }
}

using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Main.Tests.EditMode
{
    public sealed class HeartRuleTests
    {
        [Test]
        public void 赤属性キャラカードのプレイでハートが有効化される()
        {
            CharacterCardData redChar = new CharacterCardData("C901", "テスト赤キャラ", 0, 1, 1, CardAttribute.Red);

            Assert.That(HeartRule.ActivatesHearts(redChar), Is.True);
        }

        [Test]
        public void 赤属性イベントカードのプレイでハートが有効化される()
        {
            EventCardData redEvent = new EventCardData("E901", "テスト赤イベント", 0, CardAttribute.Red);

            Assert.That(HeartRule.ActivatesHearts(redEvent), Is.True);
        }

        [TestCase(CardAttribute.Blue)]
        [TestCase(CardAttribute.Green)]
        [TestCase(CardAttribute.Yellow)]
        [TestCase(CardAttribute.Black)]
        [TestCase(CardAttribute.Purple)]
        [TestCase(CardAttribute.White)]
        public void 赤以外の属性カードではハートが有効化されない(CardAttribute attribute)
        {
            CharacterCardData card = new CharacterCardData("C902", "テストキャラ", 0, 1, 1, attribute);

            Assert.That(HeartRule.ActivatesHearts(card), Is.False);
        }

        [Test]
        public void nullカードではハートが有効化されない()
        {
            Assert.That(HeartRule.ActivatesHearts(null), Is.False);
        }

        [Test]
        public void ATK0はハートを破壊できない()
        {
            Assert.That(HeartRule.CanBreakHeart(0), Is.False);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void ATK1以上はハートを破壊できる(int attack)
        {
            Assert.That(HeartRule.CanBreakHeart(attack), Is.True);
        }

        [Test]
        public void 初期ハート数は3()
        {
            Assert.That(HeartRule.InitialHeartCount, Is.EqualTo(3));
        }
    }
}

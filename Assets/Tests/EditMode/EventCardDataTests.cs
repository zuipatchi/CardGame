using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class EventCardDataTests
    {
        [Test]
        public void デフォルトのEffectTypeはNoneである()
        {
            EventCardData card = new EventCardData("e1", "テスト", 1);

            Assert.AreEqual(EffectType.None, card.EffectType);
        }

        [Test]
        public void AtkBoostを指定するとEffectTypeとEffectValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "強化", 1, EffectType.AtkBoost, 2);

            Assert.AreEqual(EffectType.AtkBoost, card.EffectType);
            Assert.AreEqual(2, card.EffectValue);
        }

        [Test]
        public void EffectTypeがNoneのときEffectValueは0である()
        {
            EventCardData card = new EventCardData("e1", "テスト", 1);

            Assert.AreEqual(0, card.EffectValue);
        }
    }
}

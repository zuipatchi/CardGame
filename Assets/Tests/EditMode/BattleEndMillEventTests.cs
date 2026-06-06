using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class BattleEndMillEventTests
    {
        [Test]
        public void BattleEndMillEventTypeを設定できる()
        {
            EventCardData card = new EventCardData("B001", "毒の呪い", 2, EventType.BattleEndMill, 1);

            Assert.AreEqual(EventType.BattleEndMill, card.EventType);
        }

        [Test]
        public void BattleEndMillカードのEventValueを保持する()
        {
            EventCardData card = new EventCardData("B001", "毒の呪い", 2, EventType.BattleEndMill, 3);

            Assert.AreEqual(3, card.EventValue);
        }

        [Test]
        public void BattleEndMillカードのTriggerOnGraveはデフォルトでfalse()
        {
            EventCardData card = new EventCardData("B001", "毒の呪い", 2, EventType.BattleEndMill, 1);

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void BattleEndMillカードはCardNameを保持する()
        {
            EventCardData card = new EventCardData("B001", "毒の呪い", 2, EventType.BattleEndMill, 1);

            Assert.AreEqual("毒の呪い", card.CardName);
        }

        [Test]
        public void BattleEndMillカードはCostを保持する()
        {
            EventCardData card = new EventCardData("B001", "毒の呪い", 2, EventType.BattleEndMill, 1);

            Assert.AreEqual(2, card.Cost);
        }
    }
}

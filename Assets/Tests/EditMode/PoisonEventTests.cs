using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class PoisonEventTests
    {
        [Test]
        public void PoisonEventTypeを設定できる()
        {
            EventCardData card = new EventCardData("P001", "毒イベント", 2, EventType.Poison, 0);

            Assert.AreEqual(EventType.Poison, card.EventType);
        }

        [Test]
        public void PoisonカードのEventValueは0()
        {
            EventCardData card = new EventCardData("P001", "毒イベント", 2, EventType.Poison, 0);

            Assert.AreEqual(0, card.EventValue);
        }

        [Test]
        public void PoisonカードのTriggerOnGraveはデフォルトでfalse()
        {
            EventCardData card = new EventCardData("P001", "毒イベント", 2, EventType.Poison, 0);

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void PoisonカードにTriggerOnGraveを設定できる()
        {
            EventCardData card = new EventCardData("P002", "墓地毒", 3, EventType.Poison, 0, "墓地毒", true);

            Assert.AreEqual(EventType.Poison, card.EventType);
            Assert.IsTrue(card.TriggerOnGrave);
        }

        [Test]
        public void PoisonカードはCardNameを保持する()
        {
            EventCardData card = new EventCardData("P001", "毒イベント", 2, EventType.Poison, 0);

            Assert.AreEqual("毒イベント", card.CardName);
        }

        [Test]
        public void PoisonカードはCostを保持する()
        {
            EventCardData card = new EventCardData("P001", "毒イベント", 2, EventType.Poison, 0);

            Assert.AreEqual(2, card.Cost);
        }
    }
}

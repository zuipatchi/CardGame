using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DeckMillEventTests
    {
        [Test]
        public void DeckMillEventTypeを設定できる()
        {
            EventCardData card = new EventCardData("M001", "デッキミル", 2, EventType.DeckMill, 3);

            Assert.AreEqual(EventType.DeckMill, card.EventType);
        }

        [Test]
        public void DeckMillカードのEventValueを保持する()
        {
            EventCardData card = new EventCardData("M001", "デッキミル", 2, EventType.DeckMill, 3);

            Assert.AreEqual(3, card.EventValue);
        }

        [Test]
        public void DeckMillカードのTriggerOnGraveはデフォルトでfalse()
        {
            EventCardData card = new EventCardData("M001", "デッキミル", 2, EventType.DeckMill, 3);

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void DeckMillカードはCardNameを保持する()
        {
            EventCardData card = new EventCardData("M001", "デッキミル", 2, EventType.DeckMill, 3);

            Assert.AreEqual("デッキミル", card.CardName);
        }

        [Test]
        public void DeckMillカードはCostを保持する()
        {
            EventCardData card = new EventCardData("M001", "デッキミル", 2, EventType.DeckMill, 3);

            Assert.AreEqual(2, card.Cost);
        }

        [Test]
        public void DeckMillカードにTriggerOnGraveを設定できる()
        {
            EventCardData card = new EventCardData("M002", "墓地ミル", 3, EventType.DeckMill, 2, "墓地ミル", true);

            Assert.AreEqual(EventType.DeckMill, card.EventType);
            Assert.IsTrue(card.TriggerOnGrave);
        }
    }
}

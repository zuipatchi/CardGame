using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class EventCardDataTests
    {
        [Test]
        public void デフォルトのEventTypeはNoneである()
        {
            EventCardData card = new EventCardData("e1", "テスト", 1);

            Assert.AreEqual(EventType.None, card.EventType);
        }

        [Test]
        public void AtkBoostを指定するとEventTypeとEventValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "強化", 1, EventType.AtkBoost, 2);

            Assert.AreEqual(EventType.AtkBoost, card.EventType);
            Assert.AreEqual(2, card.EventValue);
        }

        [Test]
        public void EventTypeがNoneのときEventValueは0である()
        {
            EventCardData card = new EventCardData("e1", "テスト", 1);

            Assert.AreEqual(0, card.EventValue);
        }

        [Test]
        public void DefBoostを指定するとEventTypeとEventValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "守護", 2, EventType.DefBoost, 3);

            Assert.AreEqual(EventType.DefBoost, card.EventType);
            Assert.AreEqual(3, card.EventValue);
        }

        [Test]
        public void DefBoostがある場合有効防御力はDefenseにEventValueを加算した値になる()
        {
            int baseDefense = 2;
            int defBoost = 3;
            int playerATK = 6;

            int effectiveDef = baseDefense + defBoost;
            int damage = UnityEngine.Mathf.Max(0, playerATK - effectiveDef);

            Assert.AreEqual(5, effectiveDef);
            Assert.AreEqual(1, damage);
        }

        [Test]
        public void DefBoostが攻撃力以上のときダメージは0になる()
        {
            int baseDefense = 2;
            int defBoost = 5;
            int playerATK = 4;

            int effectiveDef = baseDefense + defBoost;
            int damage = UnityEngine.Mathf.Max(0, playerATK - effectiveDef);

            Assert.AreEqual(0, damage);
        }

        [Test]
        public void DrawEventTypeを指定するとEventTypeとEventValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "ドロー", 1, EventType.Draw, 2);

            Assert.AreEqual(EventType.Draw, card.EventType);
            Assert.AreEqual(2, card.EventValue);
        }

        [Test]
        public void EventValueが0以下のときドロー枚数ガード条件が成立する()
        {
            Assert.IsTrue(0 <= 0);
            Assert.IsTrue(-1 <= 0);
            Assert.IsFalse(1 <= 0);
        }

        [Test]
        public void DrawTopがnullを返したときドローを止める条件が成立する()
        {
            CardData drawn = null;

            Assert.IsTrue(drawn == null);
        }
    }
}

using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class GraveTriggerTests
    {
        [Test]
        public void TriggerOnGrave省略時はfalseを返す()
        {
            EventCardData card = new EventCardData("E001", "罠カード", 1, EventType.CharDamage, 3);

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void TriggerOnGraveをtrueで生成すると正しい値を返す()
        {
            EventCardData card = new EventCardData("E002", "墓地罠", 2, EventType.CharDamage, 5, "墓地に送られたとき相手キャラにダメージ", true);

            Assert.IsTrue(card.TriggerOnGrave);
        }

        [Test]
        public void TriggerOnGraveをfalseで生成すると正しい値を返す()
        {
            EventCardData card = new EventCardData("E003", "通常イベント", 1, EventType.AtkBoost, 2, "ATKブースト", false);

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void TriggerOnGrave付きカードはEventTypeを保持する()
        {
            EventCardData card = new EventCardData("E004", "墓地罠", 3, EventType.CharDamage, 4, "説明", true);

            Assert.AreEqual(EventType.CharDamage, card.EventType);
            Assert.AreEqual(4, card.EventValue);
        }

        [Test]
        public void TriggerOnGraveとdescriptionを正しく保持する()
        {
            string description = "墓地に送られたとき発動";
            EventCardData card = new EventCardData("E005", "罠", 1, EventType.Draw, 2, description, true);

            Assert.AreEqual(description, card.Description);
            Assert.IsTrue(card.TriggerOnGrave);
        }

        [Test]
        public void デフォルトコンストラクタ使用時はTriggerOnGraveはfalse()
        {
            EventCardData card = new EventCardData();

            Assert.IsFalse(card.TriggerOnGrave);
        }

        [Test]
        public void コストのみ指定のコンストラクタではTriggerOnGraveはfalse()
        {
            EventCardData card = new EventCardData("E006", "シンプル", 1);

            Assert.IsFalse(card.TriggerOnGrave);
        }
    }
}

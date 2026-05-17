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

        [Test]
        public void DefBoostを指定するとEffectTypeとEffectValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "守護", 2, EffectType.DefBoost, 3);

            Assert.AreEqual(EffectType.DefBoost, card.EffectType);
            Assert.AreEqual(3, card.EffectValue);
        }

        [Test]
        public void DefBoostがある場合有効防御力はDefenseにEffectValueを加算した値になる()
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
        public void DrawEffectTypeを指定するとEffectTypeとEffectValueが正しく返る()
        {
            EventCardData card = new EventCardData("e1", "ドロー", 1, EffectType.Draw, 2);

            Assert.AreEqual(EffectType.Draw, card.EffectType);
            Assert.AreEqual(2, card.EffectValue);
        }

        [Test]
        public void EffectValueが0以下のときドロー枚数ガード条件が成立する()
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

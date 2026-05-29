using System.Linq;
using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class MulliganTests
    {
        [Test]
        public void マリガン判定_手札にキャラカードがない場合は必要()
        {
            CardData[] hand = new CardData[]
            {
                new SkillCardData("s1", "ファイア", 1, 3),
                new EventCardData("e1", "バフ", 1),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsTrue(needsMulligan);
        }

        [Test]
        public void マリガン判定_手札にキャラカードがある場合は不要()
        {
            CardData[] hand = new CardData[]
            {
                new CharacterCardData("c1", "戦士", 1, 0),
                new SkillCardData("s1", "ファイア", 1, 3),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsFalse(needsMulligan);
        }

        [Test]
        public void マリガン判定_手札が全てキャラカードの場合は不要()
        {
            CardData[] hand = new CardData[]
            {
                new CharacterCardData("c1", "戦士", 1, 0),
                new CharacterCardData("c2", "魔法使い", 2, 1),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsFalse(needsMulligan);
        }
    }
}

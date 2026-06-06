using System.Linq;
using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CardTypeGroupingTests
    {
        private static readonly CardData[] MixedCards = new CardData[]
        {
            new CharacterCardData("c1", "戦士", 1, 0),
            new EventCardData("e1", "バフ", 1),
            new CharacterCardData("c2", "魔法使い", 2, 1),
            new EventCardData("e2", "強化", 1),
        };

        [Test]
        public void キャラカードのみグループ化できる()
        {
            CardData[] characters = MixedCards.OfType<CharacterCardData>().Cast<CardData>().ToArray();
            Assert.AreEqual(2, characters.Length);
            Assert.IsTrue(characters.All(c => c is CharacterCardData));
        }

        [Test]
        public void イベントカードのみグループ化できる()
        {
            CardData[] events = MixedCards.OfType<EventCardData>().Cast<CardData>().ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.IsTrue(events.All(c => c is EventCardData));
        }

        [Test]
        public void 他カードがキャラグループに混入しない()
        {
            CardData[] characters = MixedCards.OfType<CharacterCardData>().Cast<CardData>().ToArray();
            Assert.AreEqual(2, characters.Length);
            Assert.AreEqual(MixedCards.Length - 2, MixedCards.OfType<EventCardData>().Count());
        }
    }
}

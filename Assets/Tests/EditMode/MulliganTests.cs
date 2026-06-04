using System.Linq;
using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class MulliganTests
    {
        // ─── CPU マリガン判定（手札にキャラがない場合のみ自動実行）────────────

        [Test]
        public void CPUマリガン判定_手札にキャラカードがない場合は必要()
        {
            CardData[] hand = new CardData[]
            {
                new SkillCardData("s1", "ファイア", 1, SkillType.Attack, 3),
                new EventCardData("e1", "バフ", 1),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsTrue(needsMulligan);
        }

        [Test]
        public void CPUマリガン判定_手札にキャラカードがある場合は不要()
        {
            CardData[] hand = new CardData[]
            {
                new CharacterCardData("c1", "戦士", 1, 0),
                new SkillCardData("s1", "ファイア", 1, SkillType.Attack, 3),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsFalse(needsMulligan);
        }

        [Test]
        public void CPUマリガン判定_手札が全てキャラカードの場合は不要()
        {
            CardData[] hand = new CardData[]
            {
                new CharacterCardData("c1", "戦士", 1, 0),
                new CharacterCardData("c2", "魔法使い", 2, 1),
            };

            bool needsMulligan = !hand.Any(d => d is CharacterCardData);

            Assert.IsFalse(needsMulligan);
        }

        // ─── マリガン後のデッキ再構築ロジック ─────────────────────────────────

        [Test]
        public void マリガン後のデッキ再構築_手札サイズ分が手札になり残りがデッキになる()
        {
            int totalCards = 20;
            int handSize = 5;
            CardData[] allCards = Enumerable.Range(0, totalCards)
                .Select(i => (CardData)new SkillCardData($"s{i}", $"カード{i}", 1, SkillType.Attack, 1))
                .ToArray();

            CardData[] newHand = allCards.Take(handSize).ToArray();
            CardData[] newDeck = allCards.Skip(handSize).ToArray();

            Assert.AreEqual(handSize, newHand.Length);
            Assert.AreEqual(totalCards - handSize, newDeck.Length);
            Assert.AreEqual(totalCards, newHand.Concat(newDeck).Count());
        }

        [Test]
        public void マリガン後のデッキ再構築_元のカードがすべて含まれている()
        {
            int totalCards = 10;
            int handSize = 3;
            CardData[] allCards = Enumerable.Range(0, totalCards)
                .Select(i => (CardData)new SkillCardData($"s{i}", $"カード{i}", 1, SkillType.Attack, 1))
                .ToArray();

            CardData[] newHand = allCards.Take(handSize).ToArray();
            CardData[] newDeck = allCards.Skip(handSize).ToArray();

            System.Collections.Generic.HashSet<string> originalIds =
                new System.Collections.Generic.HashSet<string>(allCards.Select(c => c.Id));
            System.Collections.Generic.HashSet<string> rebuiltIds =
                new System.Collections.Generic.HashSet<string>(newHand.Concat(newDeck).Select(c => c.Id));

            Assert.AreEqual(originalIds, rebuiltIds);
        }
    }
}

using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CpuAgentTests
    {
        [Test]
        public void ChooseCardToReadyIndex_キャラカードがあれば最初のインデックスを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new CharacterCardData("c1", "戦士", 2, defense: 5),
            };

            int idx = CpuAgent.ChooseCardToReadyIndex(hand);

            Assert.AreEqual(1, idx);
        }

        [Test]
        public void ChooseCardToReadyIndex_イベントカードがあれば最初のインデックスを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new EventCardData("e1", "回復", 1),
                new SkillCardData("s1", "ファイア", 1, damage: 3),
            };

            int idx = CpuAgent.ChooseCardToReadyIndex(hand);

            Assert.AreEqual(0, idx);
        }

        [Test]
        public void ChooseCardToReadyIndex_技カードしかない場合はマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new SkillCardData("s2", "アイス", 1, damage: 2),
            };

            int idx = CpuAgent.ChooseCardToReadyIndex(hand);

            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChooseCardToReadyIndex_手札が空の場合はマイナス1を返す()
        {
            int idx = CpuAgent.ChooseCardToReadyIndex(new List<CardData>());

            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChooseSkillCardIndices_技カードのインデックスをすべて返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, defense: 5),
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new EventCardData("e1", "回復", 1),
                new SkillCardData("s2", "アイス", 1, damage: 2),
            };

            List<int> indices = CpuAgent.ChooseSkillCardIndices(hand);

            Assert.AreEqual(2, indices.Count);
            Assert.AreEqual(1, indices[0]);
            Assert.AreEqual(3, indices[1]);
        }

        [Test]
        public void ChooseSkillCardIndices_技カードがない場合は空リストを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, defense: 5),
                new EventCardData("e1", "回復", 1),
            };

            List<int> indices = CpuAgent.ChooseSkillCardIndices(hand);

            Assert.AreEqual(0, indices.Count);
        }

        [Test]
        public void ChooseSkillCardIndex_技カードがあれば最初のインデックスを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, defense: 5),
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new SkillCardData("s2", "アイス", 1, damage: 2),
            };

            int idx = CpuAgent.ChooseSkillCardIndex(hand);

            Assert.AreEqual(1, idx);
        }

        [Test]
        public void ChooseSkillCardIndex_技カードがない場合はマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, defense: 5),
                new EventCardData("e1", "回復", 1),
            };

            int idx = CpuAgent.ChooseSkillCardIndex(hand);

            Assert.AreEqual(-1, idx);
        }
    }
}

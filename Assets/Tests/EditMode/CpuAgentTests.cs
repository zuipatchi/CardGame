using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CpuAgentTests
    {
        [Test]
        public void ChoosePreBattle1CardIndex_スキルカードを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new CharacterCardData("c1", "戦士", 2, 0),
            };

            int idx = CpuAgent.ChoosePreBattle1CardIndex(hand);

            Assert.AreEqual(0, idx);
        }

        [Test]
        public void ChoosePreBattle1CardIndex_キャラがなければ技カードを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new EventCardData("e1", "回復", 1),
                new SkillCardData("s1", "ファイア", 1, damage: 3),
            };

            int idx = CpuAgent.ChoosePreBattle1CardIndex(hand);

            Assert.AreEqual(1, idx);
        }

        [Test]
        public void ChoosePreBattle1CardIndex_イベントカードしかない場合はマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new EventCardData("e1", "回復", 1),
                new EventCardData("e2", "強化", 1),
            };

            int idx = CpuAgent.ChoosePreBattle1CardIndex(hand);

            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChoosePreBattle1CardIndex_手札が空の場合はマイナス1を返す()
        {
            int idx = CpuAgent.ChoosePreBattle1CardIndex(new List<CardData>());

            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChooseEventCardIndex_イベントカードがあれば最初のインデックスを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new SkillCardData("s1", "ファイア", 1, damage: 3),
                new EventCardData("e1", "回復", 1),
            };

            int idx = CpuAgent.ChooseEventCardIndex(hand);

            Assert.AreEqual(1, idx);
        }

        [Test]
        public void ChooseEventCardIndex_イベントカードがない場合はマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, 0),
                new SkillCardData("s1", "ファイア", 1, damage: 3),
            };

            int idx = CpuAgent.ChooseEventCardIndex(hand);

            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChooseEventCardIndex_手札が空の場合はマイナス1を返す()
        {
            int idx = CpuAgent.ChooseEventCardIndex(new List<CardData>());

            Assert.AreEqual(-1, idx);
        }
    }
}

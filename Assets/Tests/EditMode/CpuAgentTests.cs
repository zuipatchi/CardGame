using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CpuAgentTests
    {
        [Test]
        public void ChooseEventCardIndex_イベントカードがあれば最初のインデックスを返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("c1", "戦士", 2, 0),
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
                new CharacterCardData("c2", "魔法使い", 3, 1),
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

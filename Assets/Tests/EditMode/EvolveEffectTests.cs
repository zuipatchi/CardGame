using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class EvolveEffectTests
    {
        [Test]
        public void EvolveをEventTypeに指定するとEventTypeがEvolveになる()
        {
            EventCardData card = new EventCardData("E009", "進化", 1, EventType.Evolve, 0, "進化");
            Assert.AreEqual(EventType.Evolve, card.EventType);
        }

        [Test]
        public void CpuAgent_手札に適格キャラがなければマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("C001", "弱いキャラ", 2, 3),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 2);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void CpuAgent_コスト同値のキャラは選ばれない()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("C001", "同コストキャラ", 3, 3),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 3);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void CpuAgent_コスト1上のキャラが選ばれる()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("C001", "強いキャラ", 4, 3),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 3);
            Assert.AreEqual(0, idx);
        }

        [Test]
        public void CpuAgent_複数の適格キャラのうち最高コストが選ばれる()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("C001", "コスト4", 4, 3),
                new CharacterCardData("C002", "コスト6", 6, 3),
                new CharacterCardData("C003", "コスト5", 5, 3),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 3);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void CpuAgent_キャラカード以外は無視される()
        {
            List<CardData> hand = new List<CardData>
            {
                new SkillCardData("S001", "技", 5, SkillType.Attack, 3),
                new EventCardData("E001", "イベント", 5, EventType.Draw, 1, "ドロー"),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 3);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void CpuAgent_手札が空ならマイナス1を返す()
        {
            List<CardData> hand = new List<CardData>();
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 2);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void 自キャラスロットが空の場合は対象なし()
        {
            CharacterSlotView slot = new CharacterSlotView();
            bool hasChar = slot.CurrentCard != null;
            Assert.IsFalse(hasChar);
        }

        [Test]
        public void 犠牲コスト以下のキャラカードは進化対象外()
        {
            List<CardData> hand = new List<CardData>
            {
                new CharacterCardData("C001", "コスト3", 3, 3),
                new CharacterCardData("C002", "コスト2", 2, 3),
                new CharacterCardData("C003", "コスト1", 1, 3),
            };
            int idx = CpuAgent.ChooseEvolveCardIndex(hand, 3);
            Assert.AreEqual(-1, idx);
        }
    }
}

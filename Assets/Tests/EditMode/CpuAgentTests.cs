using System.Collections.Generic;
using Main.Card;
using Main.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CpuAgentTests
    {
        [Test]
        public void TryDecide_フィールドカードがあり倒せる相手がいる場合はフィールド攻撃を選ぶ()
        {
            List<CardData> cpuField = new List<CardData>
            {
                new CardData("a", "攻撃者", 1, 3, 1),
            };
            List<CardData> playerField = new List<CardData>
            {
                new CardData("b", "防御者", 1, 1, 2),
            };

            bool result = CpuAgent.TryDecide(cpuField, playerField, 0, out CpuDecision decision);

            Assert.IsTrue(result);
            Assert.AreEqual(CpuDecisionType.AttackField, decision.Type);
            Assert.AreEqual(0, decision.AttackerIndex);
            Assert.AreEqual(0, decision.FieldTargetIndex);
        }

        [Test]
        public void TryDecide_倒せる相手がいない場合はデッキ攻撃を選ぶ()
        {
            List<CardData> cpuField = new List<CardData>
            {
                new CardData("a", "攻撃者", 1, 1, 1),
            };
            List<CardData> playerField = new List<CardData>
            {
                new CardData("b", "堅牢", 1, 1, 3),
            };

            bool result = CpuAgent.TryDecide(cpuField, playerField, 0, out CpuDecision decision);

            Assert.IsTrue(result);
            Assert.AreEqual(CpuDecisionType.AttackDeck, decision.Type);
        }

        [Test]
        public void TryDecide_フィールドが空で手札がある場合はカードプレイを選ぶ()
        {
            bool result = CpuAgent.TryDecide(
                new List<CardData>(),
                new List<CardData>(),
                3,
                out CpuDecision decision);

            Assert.IsTrue(result);
            Assert.AreEqual(CpuDecisionType.PlayCard, decision.Type);
            Assert.AreEqual(0, decision.HandCardIndex);
        }

        [Test]
        public void TryDecide_フィールドも手札も空の場合はfalseを返す()
        {
            bool result = CpuAgent.TryDecide(
                new List<CardData>(),
                new List<CardData>(),
                0,
                out CpuDecision _);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryDecide_複数フィールドカードのうちATKが最も高いカードでデッキ攻撃する()
        {
            List<CardData> cpuField = new List<CardData>
            {
                new CardData("a", "弱", 1, 1, 1),
                new CardData("b", "強", 1, 4, 1),
                new CardData("c", "中", 1, 2, 1),
            };

            bool result = CpuAgent.TryDecide(
                cpuField,
                new List<CardData>(),
                0,
                out CpuDecision decision);

            Assert.IsTrue(result);
            Assert.AreEqual(CpuDecisionType.AttackDeck, decision.Type);
            Assert.AreEqual(1, decision.AttackerIndex);
        }
    }
}

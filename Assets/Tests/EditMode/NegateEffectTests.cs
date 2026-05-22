using System.Collections.Generic;
using Main.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class NegateEffectTests
    {
        // RunResolutionPhaseAsync の skipNextEffect ロジックを純粋にシミュレートする
        // 戻り値: 効果が適用されたカードのインデックスリスト
        private static List<int> SimulateResolution(IReadOnlyList<EventCardData> queue)
        {
            bool skipNextEffect = false;
            List<int> applied = new List<int>();

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                EventCardData data = queue[i];
                if (skipNextEffect)
                {
                    skipNextEffect = false;
                }
                else if (data.EventType == EventType.Negate)
                {
                    skipNextEffect = true;
                }
                else
                {
                    applied.Add(i);
                }
            }

            return applied;
        }

        [Test]
        public void NegateをEventTypeに指定するとEventTypeがNegateになる()
        {
            EventCardData card = new EventCardData("e1", "打ち消し", 1, EventType.Negate, 0);

            Assert.AreEqual(EventType.Negate, card.EventType);
        }

        [Test]
        public void NegateはEventValueが0であっても生成できる()
        {
            EventCardData card = new EventCardData("e1", "打ち消し", 1, EventType.Negate, 0);

            Assert.AreEqual(0, card.EventValue);
        }

        [Test]
        public void Negateの次に処理されるカードの効果がスキップされる()
        {
            // プレイ順: AtkBoost → Negate（Negate が最後にプレイ）
            // 解決順（LIFO）: Negate(1) → AtkBoost(0)
            // Negate が skipNextEffect = true をセット → AtkBoost はスキップ
            List<EventCardData> queue = new List<EventCardData>
            {
                new EventCardData("e1", "ATK強化", 1, EventType.AtkBoost, 3),
                new EventCardData("e2", "打ち消し", 1, EventType.Negate, 0),
            };

            List<int> applied = SimulateResolution(queue);

            CollectionAssert.IsEmpty(applied);
        }

        [Test]
        public void Negate_Negate_AtkBoostでAtkBoostが発動する()
        {
            // プレイ順: AtkBoost(0) → Negate2(1) → Negate1(2)
            // 解決順（LIFO）: Negate1(2) → Negate2(1) → AtkBoost(0)
            // Negate1 が skipNextEffect = true をセット
            // Negate2 はスキップされる（Negate2 の打ち消し効果が無効化）
            // AtkBoost は通常発動
            List<EventCardData> queue = new List<EventCardData>
            {
                new EventCardData("e1", "ATK強化", 1, EventType.AtkBoost, 3),
                new EventCardData("e2", "打ち消し2", 1, EventType.Negate, 0),
                new EventCardData("e3", "打ち消し1", 1, EventType.Negate, 0),
            };

            List<int> applied = SimulateResolution(queue);

            CollectionAssert.Contains(applied, 0);
            Assert.AreEqual(1, applied.Count);
        }

        [Test]
        public void Negateがキューの末尾で次のカードがない場合は何も打ち消さない()
        {
            // 解決順: Negate(0) のみ → skipNextEffect = true になるが次カードなし
            List<EventCardData> queue = new List<EventCardData>
            {
                new EventCardData("e1", "打ち消し", 1, EventType.Negate, 0),
            };

            List<int> applied = SimulateResolution(queue);

            CollectionAssert.IsEmpty(applied);
        }

        [Test]
        public void Negateはスキップした次のカードだけに影響しそれより前のカードは発動する()
        {
            // プレイ順: AtkBoost1(0) → AtkBoost2(1) → Negate(2)
            // 解決順（LIFO）: Negate(2) → AtkBoost2(1) → AtkBoost1(0)
            // Negate が AtkBoost2 をスキップ → AtkBoost1 は通常発動
            List<EventCardData> queue = new List<EventCardData>
            {
                new EventCardData("e1", "ATK強化1", 1, EventType.AtkBoost, 2),
                new EventCardData("e2", "ATK強化2", 1, EventType.AtkBoost, 3),
                new EventCardData("e3", "打ち消し", 1, EventType.Negate, 0),
            };

            List<int> applied = SimulateResolution(queue);

            CollectionAssert.Contains(applied, 0);
            CollectionAssert.DoesNotContain(applied, 1);
            Assert.AreEqual(1, applied.Count);
        }
    }
}

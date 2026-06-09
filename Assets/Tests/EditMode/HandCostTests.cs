using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class HandCostTests
    {
        private static List<int> SelectCpuCostCards(int cost, int handCount)
        {
            int take = Math.Min(cost, handCount);
            List<int> selected = new List<int>();
            for (int i = 0; i < take; i++)
            {
                selected.Add(i);
            }

            return selected;
        }

        private static bool ShouldShowCostWarning(int cardCost, int handCount)
        {
            return cardCost > handCount;
        }

        [Test]
        public void CPUコスト選択_手札5枚でコスト3_先頭3枚が選ばれる()
        {
            List<int> selected = SelectCpuCostCards(3, 5);

            Assert.AreEqual(3, selected.Count);
            Assert.AreEqual(0, selected[0]);
            Assert.AreEqual(1, selected[1]);
            Assert.AreEqual(2, selected[2]);
        }

        [Test]
        public void CPUコスト選択_手札2枚でコスト3_2枚すべて選ばれる()
        {
            List<int> selected = SelectCpuCostCards(3, 2);

            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual(0, selected[0]);
            Assert.AreEqual(1, selected[1]);
        }

        [Test]
        public void CPUコスト選択_コスト0_0枚選ばれる()
        {
            List<int> selected = SelectCpuCostCards(0, 5);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void CPUコスト選択_手札0枚でコスト2_0枚選ばれる()
        {
            List<int> selected = SelectCpuCostCards(2, 0);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void CPUコスト選択_手札枚数がコストと同数_全枚選ばれる()
        {
            List<int> selected = SelectCpuCostCards(3, 3);

            Assert.AreEqual(3, selected.Count);
        }

        [Test]
        public void コスト警告_手札がコストより少ない_警告表示()
        {
            Assert.IsTrue(ShouldShowCostWarning(cardCost: 3, handCount: 2));
        }

        [Test]
        public void コスト警告_手札がコストと同数_警告なし()
        {
            Assert.IsFalse(ShouldShowCostWarning(cardCost: 3, handCount: 3));
        }

        [Test]
        public void コスト警告_手札がコストより多い_警告なし()
        {
            Assert.IsFalse(ShouldShowCostWarning(cardCost: 2, handCount: 5));
        }

        [Test]
        public void コスト警告_コスト0_警告なし()
        {
            Assert.IsFalse(ShouldShowCostWarning(cardCost: 0, handCount: 0));
        }
    }
}

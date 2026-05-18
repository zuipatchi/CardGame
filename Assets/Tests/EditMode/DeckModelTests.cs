using Common.Deck;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DeckModelTests
    {
        [Test]
        public void Add_カードをデッキに追加できる()
        {
            DeckModel model = new DeckModel();

            model.Add("C001", 5);

            Assert.AreEqual(1, model.Count);
            Assert.AreEqual("C001", model.CardIds[0]);
        }

        [Test]
        public void Add_同名カードを複数枚追加できる()
        {
            DeckModel model = new DeckModel();

            model.Add("C001", 5);
            model.Add("C001", 5);

            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void Add_コスト合計が30を超えても追加できる()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < 7; i++)
            {
                model.Add("C001", 5);
            }

            model.Add("C001", 5);

            Assert.AreEqual(8, model.Count);
            Assert.AreEqual(40, model.TotalCost);
        }

        [Test]
        public void TotalCost_コストの合計が計算される()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);
            model.Add("S001", 5);
            model.Add("E001", 3);

            Assert.AreEqual(18, model.TotalCost);
        }

        [Test]
        public void IsReady_TotalCostが30のときtrue()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 15);
            model.Add("S001", 15);

            Assert.IsTrue(model.IsReady);
        }

        [Test]
        public void IsReady_TotalCostが30未満のときfalse()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void IsReady_TotalCostが30を超えるときfalse()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 20);
            model.Add("S001", 15);

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void Remove_カードをデッキから削除できる()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);

            bool result = model.Remove("C001");

            Assert.IsTrue(result);
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void Remove_複数枚あるとき1枚だけ削除される()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("C001", 5);

            model.Remove("C001");

            Assert.AreEqual(1, model.Count);
        }

        [Test]
        public void Remove_TotalCostが減る()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);
            model.Add("S001", 5);

            model.Remove("C001");

            Assert.AreEqual(5, model.TotalCost);
        }

        [Test]
        public void Clear_デッキが空になる()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("S001", 3);

            model.Clear();

            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void Clear_IsReadyがfalseになる()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 15);
            model.Add("S001", 15);

            model.Clear();

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void Clear_空のデッキで例外が発生しない()
        {
            DeckModel model = new DeckModel();

            Assert.DoesNotThrow(() => model.Clear());
            Assert.AreEqual(0, model.Count);
        }
    }
}

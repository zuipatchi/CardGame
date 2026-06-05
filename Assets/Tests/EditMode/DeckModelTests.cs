using System.Collections.Generic;
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
        public void Add_コスト合計が70を超えても追加できる()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < 14; i++)
            {
                model.Add("C001", 5);
            }

            model.Add("C001", 5);

            Assert.AreEqual(15, model.Count);
            Assert.AreEqual(75, model.TotalCost);
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
        public void IsReady_カードが30枚のときtrue()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxCards; i++)
            {
                model.Add("C001", 1);
            }

            Assert.IsTrue(model.IsReady);
        }

        [Test]
        public void IsReady_カードが30枚未満のときfalse()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void IsReady_カードが30枚を超えるときfalse()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxCards + 1; i++)
            {
                model.Add("C001", 1);
            }

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void IsOver_カードが30枚を超えるときtrue()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxCards + 1; i++)
            {
                model.Add("C001", 1);
            }

            Assert.IsTrue(model.IsOver);
        }

        [Test]
        public void IsOver_カードが30枚のときfalse()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxCards; i++)
            {
                model.Add("C001", 1);
            }

            Assert.IsFalse(model.IsOver);
        }

        [Test]
        public void IsOver_カードが30枚未満のときfalse()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);

            Assert.IsFalse(model.IsOver);
        }

        [Test]
        public void IsOver_デッキが空のときfalse()
        {
            DeckModel model = new DeckModel();

            Assert.IsFalse(model.IsOver);
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
        public void Remove_他カードが挟まれた複数枚のとき先頭エントリは残り順番が変わらない()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("S001", 3);
            model.Add("C001", 5);
            model.Add("E001", 2);

            model.Remove("C001");

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual(3, model.Count);
            Assert.AreEqual("C001", ids[0]);
            Assert.AreEqual("S001", ids[1]);
            Assert.AreEqual("E001", ids[2]);
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
            for (int i = 0; i < DeckModel.MaxCards; i++)
            {
                model.Add("C001", 1);
            }

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

        [Test]
        public void SortById_C系カードがS系より先に並ぶ()
        {
            DeckModel model = new DeckModel();
            model.Add("S001", 3);
            model.Add("C001", 5);

            model.SortById();

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("C001", ids[0]);
            Assert.AreEqual("S001", ids[1]);
        }

        [Test]
        public void SortById_S系カードがE系より先に並ぶ()
        {
            DeckModel model = new DeckModel();
            model.Add("E001", 2);
            model.Add("S001", 3);

            model.SortById();

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("S001", ids[0]);
            Assert.AreEqual("E001", ids[1]);
        }

        [Test]
        public void SortById_同一プレフィックス内はID文字列順()
        {
            DeckModel model = new DeckModel();
            model.Add("C003", 3);
            model.Add("C001", 5);
            model.Add("C002", 4);

            model.SortById();

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("C001", ids[0]);
            Assert.AreEqual("C002", ids[1]);
            Assert.AreEqual("C003", ids[2]);
        }

        [Test]
        public void SortById_TotalCostが変化しない()
        {
            DeckModel model = new DeckModel();
            model.Add("S001", 3);
            model.Add("C001", 10);
            model.Add("E001", 2);

            model.SortById();

            Assert.AreEqual(15, model.TotalCost);
        }

        [Test]
        public void SortById_枚数が変化しない()
        {
            DeckModel model = new DeckModel();
            model.Add("S001", 3);
            model.Add("C001", 5);
            model.Add("C001", 5);
            model.Add("E001", 2);

            model.SortById();

            Assert.AreEqual(4, model.Count);
        }

        [Test]
        public void SortById_複数枚の同一カードがソート後も隣接している()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("S001", 3);
            model.Add("C001", 5);

            model.SortById();

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("C001", ids[0]);
            Assert.AreEqual("C001", ids[1]);
            Assert.AreEqual("S001", ids[2]);
        }

        [Test]
        public void Reorder_グループの順序が入れ替わる()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("C001", 5);
            model.Add("S001", 3);
            model.Add("E001", 2);

            model.Reorder(new List<string> { "S001", "E001", "C001" });

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("S001", ids[0]);
            Assert.AreEqual("E001", ids[1]);
            Assert.AreEqual("C001", ids[2]);
            Assert.AreEqual("C001", ids[3]);
        }

        [Test]
        public void Reorder_指定外のIDは末尾に元の順序で残る()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("S001", 3);
            model.Add("E001", 2);

            model.Reorder(new List<string> { "E001" });

            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("E001", ids[0]);
            Assert.AreEqual("C001", ids[1]);
            Assert.AreEqual("S001", ids[2]);
        }

        [Test]
        public void Reorder_存在しないIDを指定しても例外が発生しない()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("S001", 3);

            Assert.DoesNotThrow(() => model.Reorder(new List<string> { "INVALID", "S001", "C001" }));
            IReadOnlyList<string> ids = model.CardIds;
            Assert.AreEqual("S001", ids[0]);
            Assert.AreEqual("C001", ids[1]);
        }

        [Test]
        public void Reorder_TotalCostが変化しない()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);
            model.Add("S001", 5);

            model.Reorder(new List<string> { "S001", "C001" });

            Assert.AreEqual(15, model.TotalCost);
        }

        [Test]
        public void Reorder_枚数が変化しない()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            model.Add("C001", 5);
            model.Add("S001", 3);

            model.Reorder(new List<string> { "S001", "C001" });

            Assert.AreEqual(3, model.Count);
        }
    }
}

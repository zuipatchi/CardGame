using Common.Deck;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class DeckModelTests
    {
        [Test]
        public void TryAdd_カードをデッキに追加できる()
        {
            DeckModel model = new DeckModel();

            bool result = model.TryAdd("c001");

            Assert.IsTrue(result);
            Assert.AreEqual(1, model.Count);
            Assert.AreEqual("c001", model.CardIds[0]);
        }

        [Test]
        public void TryAdd_同名カードを複数枚追加できる()
        {
            DeckModel model = new DeckModel();

            model.TryAdd("c001");
            model.TryAdd("c001");

            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void Remove_カードをデッキから削除できる()
        {
            DeckModel model = new DeckModel();
            model.TryAdd("c001");

            bool result = model.Remove("c001");

            Assert.IsTrue(result);
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void Remove_複数枚あるとき1枚だけ削除される()
        {
            DeckModel model = new DeckModel();
            model.TryAdd("c001");
            model.TryAdd("c001");

            model.Remove("c001");

            Assert.AreEqual(1, model.Count);
        }

        [Test]
        public void TryAdd_最大枚数を超えて追加できない()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxSize; i++)
            {
                model.TryAdd("c001");
            }

            bool result = model.TryAdd("c001");

            Assert.IsFalse(result);
            Assert.AreEqual(DeckModel.MaxSize, model.Count);
        }

        [Test]
        public void IsFull_最大枚数でtrue()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MaxSize; i++)
            {
                model.TryAdd("c001");
            }

            Assert.IsTrue(model.IsFull);
        }

        [Test]
        public void IsReady_最低枚数未満はfalse()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MinSize - 1; i++)
            {
                model.TryAdd("c001");
            }

            Assert.IsFalse(model.IsReady);
        }

        [Test]
        public void IsReady_最低枚数でtrue()
        {
            DeckModel model = new DeckModel();
            for (int i = 0; i < DeckModel.MinSize; i++)
            {
                model.TryAdd("c001");
            }

            Assert.IsTrue(model.IsReady);
        }

        [Test]
        public void Clear_デッキが空になる()
        {
            DeckModel model = new DeckModel();
            model.TryAdd("c001");
            model.TryAdd("s001");

            model.Clear();

            Assert.AreEqual(0, model.Count);
        }
    }
}

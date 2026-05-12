using Common.Deck;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DeckRepositoryTests
    {
        private const string SaveKey = "SavedDeck";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKey);
        }

        [Test]
        public void Save後にLoadするとカードIDが復元される()
        {
            DeckModel model = new DeckModel();
            model.TryAdd("C001");
            model.TryAdd("S001");
            model.TryAdd("E001");
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            repository.Load(loaded);

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("C001", loaded.CardIds[0]);
            Assert.AreEqual("S001", loaded.CardIds[1]);
            Assert.AreEqual("E001", loaded.CardIds[2]);
        }

        [Test]
        public void 空デッキをSaveしてLoadすると空のまま()
        {
            DeckModel model = new DeckModel();
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            loaded.TryAdd("C001");
            repository.Load(loaded);

            Assert.AreEqual(0, loaded.Count);
        }

        [Test]
        public void 保存データがない状態でLoadしてもDeckModelは空のまま()
        {
            DeckModel model = new DeckModel();
            DeckRepository repository = new DeckRepository();

            repository.Load(model);

            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void 同名カードを複数枚含むデッキが正しく復元される()
        {
            DeckModel model = new DeckModel();
            model.TryAdd("C001");
            model.TryAdd("C001");
            model.TryAdd("C001");
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            repository.Load(loaded);

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("C001", loaded.CardIds[0]);
            Assert.AreEqual("C001", loaded.CardIds[1]);
            Assert.AreEqual("C001", loaded.CardIds[2]);
        }
    }
}

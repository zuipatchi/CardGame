using Common.Deck;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DeckRepositoryTests
    {
        private const string SaveKey = "SavedDeck";

        private string _originalDeckJson;
        private bool _hadOriginalKey;

        [SetUp]
        public void SetUp()
        {
            _hadOriginalKey = PlayerPrefs.HasKey(SaveKey);
            _originalDeckJson = PlayerPrefs.GetString(SaveKey, null);
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadOriginalKey)
            {
                PlayerPrefs.SetString(SaveKey, _originalDeckJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveKey);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void Save後にLoadするとカードIDが復元される()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);
            model.Add("S001", 8);
            model.Add("E001", 5);
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
        public void Save後にLoadするとコストが復元される()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 10);
            model.Add("S001", 8);
            model.Add("E001", 5);
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            repository.Load(loaded);

            Assert.AreEqual(23, loaded.TotalCost);
            Assert.AreEqual(10, loaded.Entries[0].cost);
            Assert.AreEqual(8, loaded.Entries[1].cost);
            Assert.AreEqual(5, loaded.Entries[2].cost);
        }

        [Test]
        public void 空デッキをSaveしてLoadすると空のまま()
        {
            DeckModel model = new DeckModel();
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            loaded.Add("C001", 5);
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
            model.Add("C001", 7);
            model.Add("C001", 7);
            model.Add("C001", 7);
            DeckRepository repository = new DeckRepository();

            repository.Save(model);

            DeckModel loaded = new DeckModel();
            repository.Load(loaded);

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("C001", loaded.CardIds[0]);
            Assert.AreEqual("C001", loaded.CardIds[1]);
            Assert.AreEqual("C001", loaded.CardIds[2]);
            Assert.AreEqual(21, loaded.TotalCost);
        }
    }
}

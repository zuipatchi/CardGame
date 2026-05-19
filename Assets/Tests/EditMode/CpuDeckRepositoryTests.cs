using Common.Deck;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CpuDeckRepositoryTests
    {
        private CpuDeckRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _repository = new CpuDeckRepository();
            PlayerPrefs.DeleteKey("SavedCpuDeck");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("SavedCpuDeck");
        }

        [Test]
        public void 保存したCPUデッキが読み込み時に復元される()
        {
            DeckModel saveModel = new DeckModel();
            saveModel.Add("C001", 5);
            saveModel.Add("S001", 3);
            saveModel.Add("C001", 5);

            _repository.Save(saveModel);

            DeckModel loadModel = new DeckModel();
            _repository.Load(loadModel);

            Assert.AreEqual(3, loadModel.Count);
            Assert.AreEqual(13, loadModel.TotalCost);
        }

        [Test]
        public void 未保存状態ではLoadしてもデッキが空のまま()
        {
            DeckModel model = new DeckModel();
            model.Add("C001", 5);

            _repository.Load(model);

            Assert.AreEqual(1, model.Count);
        }

        [Test]
        public void HasSavedDeckは保存前はfalse保存後はtrue()
        {
            Assert.IsFalse(_repository.HasSavedDeck);

            DeckModel model = new DeckModel();
            model.Add("C001", 5);
            _repository.Save(model);

            Assert.IsTrue(_repository.HasSavedDeck);
        }
    }
}

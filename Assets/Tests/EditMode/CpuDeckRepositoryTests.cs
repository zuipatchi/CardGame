using Common.Deck;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CpuDeckRepositoryTests
    {
        private const string SaveKey = "SavedCpuDeck";

        private CpuDeckRepository _repository;
        private bool _hadOriginalKey;
        private string _originalJson;

        [SetUp]
        public void SetUp()
        {
            _repository = new CpuDeckRepository();
            _hadOriginalKey = PlayerPrefs.HasKey(SaveKey);
            _originalJson = PlayerPrefs.GetString(SaveKey, null);
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadOriginalKey)
            {
                PlayerPrefs.SetString(SaveKey, _originalJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveKey);
            }
            PlayerPrefs.Save();
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

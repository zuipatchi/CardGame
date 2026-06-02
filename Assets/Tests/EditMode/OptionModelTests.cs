using Common.Option;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Tests.EditMode
{
    public class OptionModelTests
    {
        private OptionModel _model;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("bgmVolume");
            PlayerPrefs.DeleteKey("seVolume");
            PlayerPrefs.DeleteKey("autoOk");
            _model = new OptionModel();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("bgmVolume");
            PlayerPrefs.DeleteKey("seVolume");
            PlayerPrefs.DeleteKey("autoOk");
        }

        [Test]
        public void BGM音量が0未満の場合は0にクランプされる()
        {
            _model.SetBGMVolume(-1f);
            Assert.AreEqual(0f, _model.BGMVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void BGM音量が1より大きい場合は1にクランプされる()
        {
            _model.SetBGMVolume(2f);
            Assert.AreEqual(1f, _model.BGMVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void BGM音量が正常範囲の値を設定できる()
        {
            _model.SetBGMVolume(0.7f);
            Assert.AreEqual(0.7f, _model.BGMVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void SE音量が0未満の場合は0にクランプされる()
        {
            _model.SetSEVolume(-0.5f);
            Assert.AreEqual(0f, _model.SEVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void SE音量が1より大きい場合は1にクランプされる()
        {
            _model.SetSEVolume(1.5f);
            Assert.AreEqual(1f, _model.SEVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void BGM音量変更でReactivePropertyが通知する()
        {
            float received = -1f;
            using System.IDisposable _ = _model.BGMVolume.Subscribe(v => received = v);

            _model.SetBGMVolume(0.3f);

            Assert.AreEqual(0.3f, received, 0.001f);
        }

        [Test]
        public void BGM音量未保存の場合はデフォルト値0_5が復元される()
        {
            _model.Start();
            Assert.AreEqual(0.5f, _model.BGMVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void PlayerPrefsに保存したBGM音量が起動時に復元される()
        {
            _model.SetBGMVolume(0.8f);

            OptionModel newModel = new OptionModel();
            newModel.Start();

            Assert.AreEqual(0.8f, newModel.BGMVolume.CurrentValue, 0.001f);
        }

        [Test]
        public void AutoOkをtrueに設定できる()
        {
            _model.SetAutoOk(true);
            Assert.IsTrue(_model.AutoOk.CurrentValue);
        }

        [Test]
        public void AutoOkをfalseに設定できる()
        {
            _model.SetAutoOk(true);
            _model.SetAutoOk(false);
            Assert.IsFalse(_model.AutoOk.CurrentValue);
        }

        [Test]
        public void AutoOk変更でReactivePropertyが通知する()
        {
            bool received = false;
            using System.IDisposable _ = _model.AutoOk.Subscribe(v => received = v);

            _model.SetAutoOk(true);

            Assert.IsTrue(received);
        }

        [Test]
        public void AutoOk未保存の場合はデフォルト値falseが復元される()
        {
            _model.Start();
            Assert.IsFalse(_model.AutoOk.CurrentValue);
        }

        [Test]
        public void PlayerPrefsに保存したAutoOkがtrueの場合起動時に復元される()
        {
            _model.SetAutoOk(true);

            OptionModel newModel = new OptionModel();
            newModel.Start();

            Assert.IsTrue(newModel.AutoOk.CurrentValue);
        }
    }
}

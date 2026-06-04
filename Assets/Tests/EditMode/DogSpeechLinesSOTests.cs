using Home;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DogSpeechLinesSOTests
    {
        [Test]
        public void デフォルトのアイドルセリフが存在する()
        {
            DogSpeechLinesSO so = ScriptableObject.CreateInstance<DogSpeechLinesSO>();
            Assert.Greater(so.IdleMessages.Length, 0);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void デフォルトの食べセリフが存在する()
        {
            DogSpeechLinesSO so = ScriptableObject.CreateInstance<DogSpeechLinesSO>();
            Assert.Greater(so.EatMessages.Length, 0);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void デフォルトの雨セリフが存在する()
        {
            DogSpeechLinesSO so = ScriptableObject.CreateInstance<DogSpeechLinesSO>();
            Assert.Greater(so.RainMessages.Length, 0);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void セリフが空文字列を含まない()
        {
            DogSpeechLinesSO so = ScriptableObject.CreateInstance<DogSpeechLinesSO>();
            foreach (string msg in so.IdleMessages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(msg));
            }
            foreach (string msg in so.EatMessages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(msg));
            }
            foreach (string msg in so.RainMessages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(msg));
            }
            Object.DestroyImmediate(so);
        }
    }
}

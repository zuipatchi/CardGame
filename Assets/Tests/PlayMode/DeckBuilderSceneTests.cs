using System.Collections;
using System.Reflection;
using Common.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.PlayMode
{
    public class DeckBuilderSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("DeckBuilder", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetSceneByName("Common").isLoaded);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            typeof(CommonSceneLoader)
                .GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator コストオーバーラベルがシーンに存在する()
        {
            Label label = FindLabel("CostOverLabel");
            Assert.IsNotNull(label, "CostOverLabel が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator コストオーバーラベルが初期状態で非表示()
        {
            Label label = FindLabel("CostOverLabel");
            Assert.IsNotNull(label, "CostOverLabel が見つかりません");
            Assert.AreEqual(DisplayStyle.None, label.resolvedStyle.display, "初期状態でコストオーバーラベルが表示されています");
            yield return null;
        }

        private static Label FindLabel(string name)
        {
            Scene scene = SceneManager.GetSceneByName("DeckBuilder");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    Label label = doc.rootVisualElement?.Q<Label>(name);
                    if (label != null)
                    {
                        return label;
                    }
                }
            }
            return null;
        }
    }
}

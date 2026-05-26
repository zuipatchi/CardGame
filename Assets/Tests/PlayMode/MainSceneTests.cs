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
    public class MainSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
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
        public IEnumerator トーストコンテナが初期状態で非表示()
        {
            yield return new WaitUntil(() => FindByClass("main-toast-container") != null);

            VisualElement toast = FindByClass("main-toast-container");
            Assert.IsNotNull(toast, "main-toast-container が見つかりません");
            Assert.AreEqual(DisplayStyle.None, toast.resolvedStyle.display, "トーストコンテナが初期状態で表示されています");
        }

        private static VisualElement FindByClass(string className)
        {
            Scene scene = SceneManager.GetSceneByName("Main");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    VisualElement element = doc.rootVisualElement?.Q<VisualElement>(className: className);
                    if (element != null)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }
}

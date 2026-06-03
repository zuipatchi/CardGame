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

        [UnityTest]
        public IEnumerator マリガンモーダルがゲーム開始時に表示される()
        {
            yield return new WaitUntil(() => FindByClass("mulligan-overlay") != null);

            VisualElement overlay = FindByClass("mulligan-overlay");
            Assert.IsNotNull(overlay, "mulligan-overlay が見つかりません");
        }

        [UnityTest]
        public IEnumerator マリガンNO選択後にモーダルが閉じる()
        {
            yield return new WaitUntil(() => FindByClass("mulligan-overlay") != null);

            VisualElement overlay = FindByClass("mulligan-overlay");
            Assert.IsNotNull(overlay, "mulligan-overlay が見つかりません");

            // NO ボタンは2番目の mulligan-button
            System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();
            overlay.Query<Button>(className: "mulligan-button").ForEach(b => buttons.Add(b));
            Assert.AreEqual(2, buttons.Count, "mulligan-button が2つ見つかりません");

            Button noButton = buttons[1];
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = noButton;
                noButton.SendEvent(evt);
            }

            yield return null;
            yield return null;

            VisualElement overlayAfter = FindByClass("mulligan-overlay");
            Assert.IsNull(overlayAfter, "NO 選択後もモーダルが残っています");
        }

        [UnityTest]
        public IEnumerator マリガンYES選択後にモーダルが閉じる()
        {
            yield return new WaitUntil(() => FindByClass("mulligan-overlay") != null);

            VisualElement overlay = FindByClass("mulligan-overlay");
            Assert.IsNotNull(overlay, "mulligan-overlay が見つかりません");

            // YES ボタンは1番目の mulligan-button
            System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();
            overlay.Query<Button>(className: "mulligan-button").ForEach(b => buttons.Add(b));
            Assert.AreEqual(2, buttons.Count, "mulligan-button が2つ見つかりません");

            Button yesButton = buttons[0];
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = yesButton;
                yesButton.SendEvent(evt);
            }

            yield return null;
            yield return null;

            VisualElement overlayAfter = FindByClass("mulligan-overlay");
            Assert.IsNull(overlayAfter, "YES 選択後もモーダルが残っています");
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

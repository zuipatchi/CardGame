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

        [UnityTest]
        public IEnumerator 分析ボタンがシーンに存在する()
        {
            Button button = FindButton("AnalyzeButton");
            Assert.IsNotNull(button, "AnalyzeButton が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 分析ボタン押下でオーバーレイが表示される()
        {
            yield return new WaitUntil(() => FindCardListScrollView()?.childCount > 0);

            VisualElement deckBuilderRoot = FindDeckBuilderRoot();
            Assert.IsNotNull(deckBuilderRoot, "DeckBuilderRoot が見つかりません");

            Button button = deckBuilderRoot.Q<Button>("AnalyzeButton");
            Assert.IsNotNull(button, "AnalyzeButton が見つかりません");

            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
            yield return null;

            VisualElement overlay = deckBuilderRoot.Q<VisualElement>(className: "deck-analysis-overlay");
            Assert.IsNotNull(overlay, "deck-analysis-overlay が表示されていません");
        }

        [UnityTest]
        public IEnumerator オーバーレイクリックでモーダルが閉じる()
        {
            yield return new WaitUntil(() => FindCardListScrollView()?.childCount > 0);

            VisualElement deckBuilderRoot = FindDeckBuilderRoot();
            Button button = deckBuilderRoot.Q<Button>("AnalyzeButton");

            using NavigationSubmitEvent openEvt = NavigationSubmitEvent.GetPooled();
            openEvt.target = button;
            button.SendEvent(openEvt);
            yield return null;

            VisualElement overlay = deckBuilderRoot.Q<VisualElement>(className: "deck-analysis-overlay");
            Assert.IsNotNull(overlay, "オーバーレイが開いていません");

            using ClickEvent closeEvt = ClickEvent.GetPooled();
            closeEvt.target = overlay;
            overlay.SendEvent(closeEvt);
            yield return null;

            VisualElement overlayAfter = deckBuilderRoot.Q<VisualElement>(className: "deck-analysis-overlay");
            Assert.IsNull(overlayAfter, "オーバーレイが閉じていません");
        }

        [UnityTest]
        public IEnumerator 分析モーダルにコスト分布と種類分布のセクションが表示される()
        {
            yield return new WaitUntil(() => FindCardListScrollView()?.childCount > 0);

            VisualElement deckBuilderRoot = FindDeckBuilderRoot();
            Button button = deckBuilderRoot.Q<Button>("AnalyzeButton");

            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
            yield return null;

            VisualElement overlay = deckBuilderRoot.Q<VisualElement>(className: "deck-analysis-overlay");
            Assert.IsNotNull(overlay, "オーバーレイが見つかりません");

            bool hasCostSection = false;
            bool hasTypeSection = false;
            foreach (Label sectionTitle in overlay.Query<Label>(className: "deck-analysis-section-title").ToList())
            {
                if (sectionTitle.text == "コスト分布")
                {
                    hasCostSection = true;
                }
                if (sectionTitle.text == "種類分布")
                {
                    hasTypeSection = true;
                }
            }
            Assert.IsTrue(hasCostSection, "コスト分布セクションが見つかりません");
            Assert.IsTrue(hasTypeSection, "種類分布セクションが見つかりません");
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

        private static Button FindButton(string name)
        {
            Scene scene = SceneManager.GetSceneByName("DeckBuilder");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    Button button = doc.rootVisualElement?.Q<Button>(name);
                    if (button != null)
                    {
                        return button;
                    }
                }
            }
            return null;
        }

        private static VisualElement FindDeckBuilderRoot()
        {
            Scene scene = SceneManager.GetSceneByName("DeckBuilder");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    VisualElement el = doc.rootVisualElement?.Q<VisualElement>("DeckBuilderRoot");
                    if (el != null)
                    {
                        return el;
                    }
                }
            }
            return null;
        }

        private static ScrollView FindCardListScrollView()
        {
            Scene scene = SceneManager.GetSceneByName("DeckBuilder");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    ScrollView sv = doc.rootVisualElement?.Q<ScrollView>("CardListScrollView");
                    if (sv != null)
                    {
                        return sv;
                    }
                }
            }
            return null;
        }
    }
}

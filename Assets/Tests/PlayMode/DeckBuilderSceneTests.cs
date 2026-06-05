using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private const string SaveKey = "SavedDeck";
        private string _savedDeckSnapshot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savedDeckSnapshot = PlayerPrefs.GetString(SaveKey, null);
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
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
            if (_savedDeckSnapshot != null)
            {
                PlayerPrefs.SetString(SaveKey, _savedDeckSnapshot);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveKey);
            }
            PlayerPrefs.Save();
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
        public IEnumerator 保存ボタンがシーンに存在しない()
        {
            Button button = FindButton("SaveButton");
            Assert.IsNull(button, "SaveButton が削除されていません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 保存トーストラベルがシーンに存在しない()
        {
            Label label = FindLabel("SaveToastLabel");
            Assert.IsNull(label, "SaveToastLabel が削除されていません");
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

            List<Label> sectionTitles = overlay.Query<Label>(className: "deck-analysis-section-title").ToList();
            Assert.IsTrue(sectionTitles.Any(l => l.text == "コスト分布"), "コスト分布セクションが見つかりません");
            Assert.IsTrue(sectionTitles.Any(l => l.text == "種類分布"), "種類分布セクションが見つかりません");
        }

        [UnityTest]
        public IEnumerator カード一覧の右クリックでデッキに1枚追加される()
        {
            yield return new WaitUntil(() => FindCardListScrollView()?.childCount > 0);

            Label deckCountLabel = FindElement<Label>("DeckCountLabel");
            Assert.IsNotNull(deckCountLabel, "DeckCountLabel が見つかりません");
            int beforeCount = int.Parse(deckCountLabel.text.Split(' ')[1].Split('/')[0]);

            ScrollView cardList = FindCardListScrollView();
            VisualElement cardItem = cardList.Q<VisualElement>(className: "deckbuilder-card-item");
            Assert.IsNotNull(cardItem, "deckbuilder-card-item が見つかりません");

            SendRightClick(cardItem);
            yield return null;

            int afterCount = int.Parse(deckCountLabel.text.Split(' ')[1].Split('/')[0]);
            Assert.AreEqual(beforeCount + 1, afterCount, "右クリック後にデッキ枚数が1枚増えていません");
        }

        [UnityTest]
        public IEnumerator カード一覧の右クリックを複数回でデッキ枚数が増える()
        {
            yield return new WaitUntil(() => FindCardListScrollView()?.childCount > 0);

            Label deckCountLabel = FindElement<Label>("DeckCountLabel");
            Assert.IsNotNull(deckCountLabel, "DeckCountLabel が見つかりません");
            int beforeCount = int.Parse(deckCountLabel.text.Split(' ')[1].Split('/')[0]);

            ScrollView cardList = FindCardListScrollView();
            VisualElement cardItem = cardList.Q<VisualElement>(className: "deckbuilder-card-item");
            Assert.IsNotNull(cardItem, "deckbuilder-card-item が見つかりません");

            for (int i = 0; i < 3; i++)
            {
                SendRightClick(cardItem);
                yield return null;
            }

            int afterCount = int.Parse(deckCountLabel.text.Split(' ')[1].Split('/')[0]);
            Assert.AreEqual(beforeCount + 3, afterCount, "右クリック3回後にデッキ枚数が3枚増えていません");
        }

        private static void SendRightClick(VisualElement target)
        {
            FieldInfo dragManipulatorField = target.GetType()
                .GetField("_dragManipulator", BindingFlags.NonPublic | BindingFlags.Instance);
            object manipulator = dragManipulatorField?.GetValue(target);
            if (manipulator == null)
            {
                return;
            }

            FieldInfo onRightClickField = manipulator.GetType()
                .GetField("OnRightClick", BindingFlags.Public | BindingFlags.Instance);
            System.Action action = onRightClickField?.GetValue(manipulator) as System.Action;
            action?.Invoke();
        }

        private static Label FindLabel(string name) => FindElement<Label>(name);
        private static Button FindButton(string name) => FindElement<Button>(name);
        private static VisualElement FindDeckBuilderRoot() => FindElement<VisualElement>("DeckBuilderRoot");
        private static ScrollView FindCardListScrollView() => FindElement<ScrollView>("CardListScrollView");

        private static T FindElement<T>(string name) where T : VisualElement
        {
            Scene scene = SceneManager.GetSceneByName("DeckBuilder");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    T element = doc.rootVisualElement?.Q<T>(name);
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

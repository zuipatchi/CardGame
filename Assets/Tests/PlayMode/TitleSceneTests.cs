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
    public class TitleSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            // Common シーンのロード完了を待つ
            yield return new WaitUntil(() => SceneManager.GetSceneByName("Common").isLoaded);
            // VContainer スコープビルド + DI 注入完了を待つ
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // static フィールドをリセットして次のテストで Common シーンが再ロードされるようにする
            typeof(CommonSceneLoader)
                .GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ゲームスタートラベルがシーンに存在する()
        {
            Label label = FindGameStartLabel();
            Assert.IsNotNull(label, "GameStartLabel が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator タッチエリアがシーンに存在する()
        {
            VisualElement touchArea = FindTouchArea();
            Assert.IsNotNull(touchArea, "TouchArea が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator タッチエリアが初期状態でクリックを受け付ける()
        {
            VisualElement touchArea = FindTouchArea();
            Assert.IsNotNull(touchArea, "TouchArea が見つかりません");
            Assert.AreNotEqual(PickingMode.Ignore, touchArea.pickingMode, "TouchArea がクリックを無視しています");
            yield return null;
        }

        private static Label FindGameStartLabel()
        {
            Scene titleScene = SceneManager.GetSceneByName("Title");
            foreach (GameObject root in titleScene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    Label label = doc.rootVisualElement?.Q<Label>("GameStartLabel");
                    if (label != null)
                    {
                        return label;
                    }
                }
            }
            return null;
        }

        private static VisualElement FindTouchArea()
        {
            Scene titleScene = SceneManager.GetSceneByName("Title");
            foreach (GameObject root in titleScene.GetRootGameObjects())
            {
                foreach (UIDocument doc in root.GetComponentsInChildren<UIDocument>())
                {
                    VisualElement touchArea = doc.rootVisualElement?.Q<VisualElement>("TouchArea");
                    if (touchArea != null)
                    {
                        return touchArea;
                    }
                }
            }
            return null;
        }
    }
}

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
    public class HomeSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Home", LoadSceneMode.Single);
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
        public IEnumerator デッキ構築ボタンがシーンに存在する()
        {
            Button button = FindButton("DeckBuilderButton");
            Assert.IsNotNull(button, "DeckBuilderButton が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 対戦ボタンがシーンに存在する()
        {
            Button button = FindButton("BattleButton");
            Assert.IsNotNull(button, "BattleButton が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator オンラインボタンがシーンに存在する()
        {
            Button button = FindButton("MatchingButton");
            Assert.IsNotNull(button, "MatchingButton が見つかりません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 全ボタンが初期状態で有効()
        {
            Button deckBuilder = FindButton("DeckBuilderButton");
            Button battle = FindButton("BattleButton");
            Button matching = FindButton("MatchingButton");
            Assert.IsTrue(deckBuilder?.enabledSelf, "DeckBuilderButton が無効です");
            Assert.IsTrue(battle?.enabledSelf, "BattleButton が無効です");
            Assert.IsTrue(matching?.enabledSelf, "MatchingButton が無効です");
            yield return null;
        }

        private static Button FindButton(string name)
        {
            Scene homeScene = SceneManager.GetSceneByName("Home");
            foreach (GameObject root in homeScene.GetRootGameObjects())
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
    }
}

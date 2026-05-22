using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Common.SceneManagement;
using Home;
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

        [UnityTest]
        public IEnumerator 食べ物通知後にキューに積まれる()
        {
            HomeLive2DPresenter presenter = FindInHomeScene<HomeLive2DPresenter>();
            Assume.That(presenter, Is.Not.Null, "HomeLive2DPresenter が見つかりません");

            Queue<(Vector3, Animator)> queue = (Queue<(Vector3, Animator)>)typeof(HomeLive2DPresenter)
                .GetField("_foodQueue", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(presenter);
            Assume.That(queue, Is.Not.Null);

            int before = queue.Count;
            presenter.NotifyFoodSpawned(new Vector3(1f, 0f, 0f), null);

            Assert.That(queue.Count, Is.EqualTo(before + 1), "キューに追加されていません");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 食べ物通知後に犬が目標位置に移動する()
        {
            HomeLive2DPresenter presenter = FindInHomeScene<HomeLive2DPresenter>();
            Assume.That(presenter, Is.Not.Null, "HomeLive2DPresenter が見つかりません");

            // Walk/Eat クリップが両方設定されていることを前提とする（片方でも欠けると犬がキューを処理しない）
            AnimationClip walkClip = (AnimationClip)typeof(HomeLive2DPresenter)
                .GetField("_walkClip", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(presenter);
            Assume.That(walkClip, Is.Not.Null, "_walkClip が未設定です（Inspector で設定してください）");

            AnimationClip eatClip = (AnimationClip)typeof(HomeLive2DPresenter)
                .GetField("_eatClip", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(presenter);
            Assume.That(eatClip, Is.Not.Null, "_eatClip が未設定です（Inspector で設定してください）");

            // 犬は _animator.transform で移動する（presenter.transform とは別 GameObject）
            Animator animator = (Animator)typeof(HomeLive2DPresenter)
                .GetField("_animator", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(presenter);
            Assume.That(animator, Is.Not.Null, "_animator が未設定です（Inspector で設定してください）");

            // 犬の真横 0.5f 先に食べ物を置く（短距離なので到達が速い）
            Vector3 initialPos = animator.transform.position;
            Vector3 foodPos = new Vector3(initialPos.x + 0.5f, initialPos.y, initialPos.z);
            presenter.NotifyFoodSpawned(foodPos, null);

            // 現在のモーション終了 + 移動 + Eat まで最大 15 秒待機
            float startTime = Time.time;
            yield return new WaitUntil(() =>
                Mathf.Abs(animator.transform.position.x - foodPos.x) < 0.15f ||
                Time.time - startTime > 15f
            );

            Assert.That(
                Mathf.Abs(animator.transform.position.x - foodPos.x),
                Is.LessThan(0.15f),
                "犬が食べ物の位置に移動しませんでした"
            );
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

        private static T FindInHomeScene<T>() where T : Component
        {
            Scene homeScene = SceneManager.GetSceneByName("Home");
            foreach (GameObject root in homeScene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }
    }
}

using System;
using System.Threading;
using Common.Transition;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Common.SceneManagement
{
    public enum Scenes
    {
        Common = 0,
        Title = 1,
        Matching = 2,
        Main = 3,
        DeckBuilder = 4,
        Home = 5
    }
    public sealed class SceneTransitioner : MonoBehaviour
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private TransitionPresenter _transitionPresenter;
        private CancellationToken _ct;

        private void Awake()
        {
            _ct = destroyCancellationToken;
        }

        [Inject]
        public void Construct(TransitionPresenter transitionPresenter)
        {
            _transitionPresenter = transitionPresenter;
        }

        // アクティブシーンを next に変更する
        // 遷移中に呼ばれた場合は無視する（二重遷移防止）
        // MPM では SceneManager が共有されるため、既にロード済みのシーンはロードをスキップし
        // 各プレイヤーが自分自身のスコープを確実にビルドする
        public async UniTask Transit(Scenes next)
        {
            if (next == Scenes.Common) return;

            // 遷移中なら無視（ゲートをすぐ取れない = 別の遷移が実行中）
            if (!await _gate.WaitAsync(0)) return;

            bool gateReleased = false;
            try
            {
                await _transitionPresenter.CoverAsync();

                Scene nextScene = SceneManager.GetSceneByBuildIndex((int)next);
                if (!nextScene.IsValid() || !nextScene.isLoaded)
                {
                    await SceneManager.LoadSceneAsync((int)next, LoadSceneMode.Additive)
                        .WithCancellation(_ct);

                    nextScene = SceneManager.GetSceneByBuildIndex((int)next);
                }

                nextScene.BuildLifetimeScopes();

                if (SceneManager.GetActiveScene().buildIndex != (int)next)
                {
                    SceneManager.SetActiveScene(nextScene);
                }

                await UnloadOldScenesAsync((int)next);

                foreach (GameObject rootGo in nextScene.GetRootGameObjects())
                {
                    ISceneReady sceneReady = rootGo.GetComponentInChildren<ISceneReady>(true);
                    if (sceneReady != null)
                    {
                        await sceneReady.ReadyAsync(_ct);
                        break;
                    }
                }

                // RevealAsync 前にゲートを解放して新シーンのボタンをすぐ有効化
                gateReleased = true;
                _gate.Release();
                await _transitionPresenter.RevealAsync();
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (!gateReleased)
                {
                    _gate.Release();
                }
            }
        }

        private async UniTask UnloadOldScenesAsync(int keepBuildIndex)
        {
            System.Collections.Generic.List<Scene> toUnload = new System.Collections.Generic.List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.buildIndex != (int)Scenes.Common && s.buildIndex != keepBuildIndex && s.isLoaded)
                {
                    toUnload.Add(s);
                }
            }
            foreach (Scene s in toUnload)
            {
                await SceneManager.UnloadSceneAsync(s);
            }
        }
    }
}

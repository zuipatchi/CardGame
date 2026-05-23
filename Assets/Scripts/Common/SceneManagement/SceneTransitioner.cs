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

        [Inject]
        public void Construct(TransitionPresenter transitionPresenter)
        {
            _transitionPresenter = transitionPresenter;
        }

        // アクティブシーンを next に変更する
        // 遷移中に呼ばれた場合は無視する（二重遷移防止）
        public async UniTask Transit(Scenes next)
        {
            if (next == Scenes.Common) return;

            // 遷移中なら無視（ゲートをすぐ取れない = 別の遷移が実行中）
            if (!await _gate.WaitAsync(0)) return;

            bool gateReleased = false;
            try
            {
                CancellationToken ct = this.GetCancellationTokenOnDestroy();

                Scene activeScene = SceneManager.GetActiveScene();

                // 同じシーンへの遷移は無視
                if (activeScene.buildIndex == (int)next) return;

                await _transitionPresenter.CoverAsync();

                Scene nextScene = SceneManager.GetSceneByBuildIndex((int)next);
                if (!nextScene.IsValid() || !nextScene.isLoaded)
                {
                    await SceneManager.LoadSceneAsync((int)next, LoadSceneMode.Additive)
                        .WithCancellation(ct);

                    nextScene = SceneManager.GetSceneByBuildIndex((int)next);
                }

                nextScene.BuildLifetimeScopes();

                SceneManager.SetActiveScene(nextScene);

                if (activeScene.buildIndex != (int)Scenes.Common)
                {
                    await SceneManager.UnloadSceneAsync(activeScene).WithCancellation(ct);
                }

                foreach (GameObject rootGo in nextScene.GetRootGameObjects())
                {
                    ISceneReady sceneReady = rootGo.GetComponentInChildren<ISceneReady>(true);
                    if (sceneReady != null)
                    {
                        await sceneReady.ReadyAsync(ct);
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
    }
}

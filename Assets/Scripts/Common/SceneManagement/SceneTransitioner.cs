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

        // 指定シーンを一度アンロードしてから再ロードし、まっさらな状態で作り直す。
        // オンライン再戦などで同じシーンを初期状態から開始したい場合に使う。
        // NGO セッションは Common 常駐の NetworkManager が保持するため、シーン再ロードでは切断されない。
        public async UniTask Reload(Scenes target)
        {
            if (target == Scenes.Common) return;

            // 遷移中なら無視（ゲートをすぐ取れない = 別の遷移が実行中）
            if (!await _gate.WaitAsync(0)) return;

            bool gateReleased = false;
            // 対象シーンを一旦アンロードしてから再ロードするため、その間カメラが 0 個になる。
            // Common シーンにカメラが無いと「No Cameras Rendering」が一瞬表示されるので、
            // 黒フェードで覆っている間だけ画面を黒く塗る一時カメラを立てて隙間を埋める。
            GameObject fallbackCameraGo = null;
            try
            {
                await _transitionPresenter.CoverAsync();

                fallbackCameraGo = CreateFallbackCamera();

                // アクティブシーンをアンロードできないため、対象がアクティブなら一旦 Common に移す
                Scene current = SceneManager.GetSceneByBuildIndex((int)target);
                if (current.IsValid() && current.isLoaded)
                {
                    if (SceneManager.GetActiveScene().buildIndex == (int)target)
                    {
                        Scene common = SceneManager.GetSceneByBuildIndex((int)Scenes.Common);
                        if (common.IsValid() && common.isLoaded)
                        {
                            SceneManager.SetActiveScene(common);
                        }
                    }
                    await SceneManager.UnloadSceneAsync(current).WithCancellation(_ct);
                }

                await SceneManager.LoadSceneAsync((int)target, LoadSceneMode.Additive)
                    .WithCancellation(_ct);
                Scene nextScene = SceneManager.GetSceneByBuildIndex((int)target);

                // 新シーンのカメラがロードされたので一時カメラは不要
                if (fallbackCameraGo != null)
                {
                    Destroy(fallbackCameraGo);
                    fallbackCameraGo = null;
                }

                nextScene.BuildLifetimeScopes();

                if (SceneManager.GetActiveScene().buildIndex != (int)target)
                {
                    SceneManager.SetActiveScene(nextScene);
                }

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
                if (fallbackCameraGo != null)
                {
                    Destroy(fallbackCameraGo);
                }
                if (!gateReleased)
                {
                    _gate.Release();
                }
            }
        }

        // Reload 中にカメラが 0 個になる瞬間の「No Cameras Rendering」を防ぐため、
        // 画面全体を黒で塗るだけの一時カメラを生成する。黒フェード中にしか存在しないため
        // depth は最背面（他シーンのカメラが現れたらその下に隠れる）でよい。
        private static GameObject CreateFallbackCamera()
        {
            GameObject go = new GameObject("ReloadFallbackCamera");
            // 生成時のアクティブシーンは再ロード対象（target）なので、そのままだと
            // UnloadSceneAsync で一緒に破棄されてしまう。DontDestroyOnLoad でアンロード対象から外す。
            DontDestroyOnLoad(go);
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -100f;
            return go;
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

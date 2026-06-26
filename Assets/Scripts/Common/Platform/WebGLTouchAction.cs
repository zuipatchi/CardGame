using System.Runtime.InteropServices;
using UnityEngine;

namespace Common.Platform
{
    // WebGL ビルドで、起動時に canvas へ touch-action: none を設定する。
    // 詳細は Assets/Plugins/WebGL/TouchAction.jslib のコメントを参照。
    // Unityroom は HTML/canvas を自前生成するためテンプレート CSS が効かない。実行時に canvas へ直接設定することで
    // ホスト側の HTML に依存せずスマホのタッチドラッグ（カードが途中で止まる／ゴーストが残る）を解消する。
    public static class WebGLTouchAction
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SetCanvasTouchActionNone();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetCanvasTouchActionNone();
#endif
        }
    }
}

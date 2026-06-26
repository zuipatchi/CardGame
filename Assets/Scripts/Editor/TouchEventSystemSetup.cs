using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GameEditor
{
    // Common シーン（常駐）に EventSystem + InputSystemUIInputModule を追加するエディタ専用ツール。
    //
    // UI Toolkit は「マウス入力だけ」EventSystem 無しでも動く特別扱いがあるため、エディタや PC ビルドの
    // マウス操作では問題が出ない。しかしタッチ入力は EventSystem + 入力モジュールが無いと正しく処理されず、
    // ポインターキャプチャや連続した PointerMove が機能しない。その結果、スマホ（WebGL）では
    // カードのドラッグが指に追従しない／少し動いて止まる／ScrollView と競合する、といった症状になる。
    //
    // Pointer Behavior を「Single Unified Pointer」にすると、マウスと各タッチを単一ポインターに統合し、
    // UI Toolkit のドラッグ（ポインターキャプチャ前提）がマウスと同じ挙動で安定する。
    public static class TouchEventSystemSetup
    {
        private const string CommonScenePath = "Assets/Scenes/Common.unity";

        [MenuItem("Card/Common シーンに EventSystem を追加（タッチ入力対応）")]
        public static void AddEventSystemToCommon()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(CommonScenePath, OpenSceneMode.Single);

            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                Debug.Log($"既に EventSystem が存在します（{existing.gameObject.name}）。追加は行いませんでした。");
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            module.pointerBehavior = UIPointerBehavior.SingleUnifiedPointer;

            Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("Common シーンに EventSystem（InputSystemUIInputModule / Single Unified Pointer）を追加し、保存しました。");
        }
    }
}

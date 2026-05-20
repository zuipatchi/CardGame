using Live2D.Cubism.Framework.MotionFade;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    public static class DogKidFadeMotionListSetup
    {
        private const string PrefabPath = "Assets/Live2D/Dog-kid/Dog-kid.prefab";
        private const string FadeMotionListPath = "Assets/Live2D/Dog-kid/Dog-kid.FadeMotionList.asset";
        private const string DogKidDir = "Assets/Live2D/Dog-kid";

        [MenuItem("Live2D/Setup Dog-kid FadeMotionList")]
        public static void Setup()
        {
            string[] fadeGuids = AssetDatabase.FindAssets("t:CubismFadeMotionData", new[] { DogKidDir });
            CubismFadeMotionData[] fadeMotions = new CubismFadeMotionData[fadeGuids.Length];
            int[] instanceIds = new int[fadeGuids.Length];

            for (int i = 0; i < fadeGuids.Length; i++)
            {
                string fadePath = AssetDatabase.GUIDToAssetPath(fadeGuids[i]);
                fadeMotions[i] = AssetDatabase.LoadAssetAtPath<CubismFadeMotionData>(fadePath);

                string motionName = System.IO.Path.GetFileNameWithoutExtension(fadePath).Replace(".fade", "");
                string animPath = $"{DogKidDir}/{motionName}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                instanceIds[i] = ReadInstanceId(clip, animPath);
            }

            CubismFadeMotionList list = AssetDatabase.LoadAssetAtPath<CubismFadeMotionList>(FadeMotionListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<CubismFadeMotionList>();
                AssetDatabase.CreateAsset(list, FadeMotionListPath);
            }

            list.CubismFadeMotionObjects = fadeMotions;
            list.MotionInstanceIds = instanceIds;
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();

            AssignToPrefab(list);
            Debug.Log($"Dog-kid FadeMotionList セットアップ完了（{fadeMotions.Length} モーション登録）");
        }

        private static int ReadInstanceId(AnimationClip clip, string path)
        {
            if (clip == null)
            {
                Debug.LogWarning($"AnimationClip が見つかりません: {path}");
                return -1;
            }

            foreach (AnimationEvent ev in clip.events)
            {
                if (ev.functionName == "InstanceId")
                {
                    return ev.intParameter;
                }
            }

            Debug.LogWarning($"InstanceId イベントが見つかりません: {path}");
            return -1;
        }

        private static void AssignToPrefab(CubismFadeMotionList list)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            CubismFadeController controller = prefab.GetComponent<CubismFadeController>();
            if (controller == null)
            {
                Debug.LogError("CubismFadeController が Dog-kid プレハブに見つかりません");
                PrefabUtility.UnloadPrefabContents(prefab);
                return;
            }

            controller.CubismFadeMotionList = list;
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }
}

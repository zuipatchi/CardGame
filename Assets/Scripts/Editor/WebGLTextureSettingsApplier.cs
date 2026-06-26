using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    // Assets/AddressableAssets/Image 配下のテクスチャに WebGL 用のプラットフォーム設定を一括適用するエディタ専用ツール。
    // モバイルの WebGL では GPU が DXT（S3TC）非対応のことが多く、圧縮テクスチャがランタイムで RGBA32 に展開されるため
    // 解像度がそのままメモリ使用量に直結する。maxTextureSize を抑えることで初回ロード後の OOM クラッシュを防ぐ。
    public static class WebGLTextureSettingsApplier
    {
        private const string TargetFolder = "Assets/AddressableAssets/Image";
        private const string WebGLPlatform = "WebGL";
        private const int MaxTextureSize = 512;

        [MenuItem("Card/WebGL テクスチャ設定を適用")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { TargetFolder });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"{TargetFolder} 配下にテクスチャが見つかりませんでした。");
                return;
            }

            List<string> changed = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("WebGL テクスチャ設定", path, (float)i / guids.Length);

                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(WebGLPlatform);
                    settings.overridden = true;
                    settings.maxTextureSize = MaxTextureSize;
                    settings.format = TextureImporterFormat.Automatic;
                    settings.textureCompression = TextureImporterCompression.Compressed;
                    settings.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed.Add(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"WebGL テクスチャ設定を {changed.Count} 件に適用しました（maxTextureSize={MaxTextureSize}）。");
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace GameEditor
{
    /// <summary>
    /// NotoSansJP-Bold を UI Toolkit のデフォルトフォントとして配線するセットアップツール。
    /// メニュー「Tools/UI/Setup Bold Japanese Font」から実行すると以下を自動で行う:
    ///  1. NotoSansJP-Bold.ttf から TextCore ダイナミック FontAsset を生成（全漢字を実行時レンダリング）
    ///  2. PanelTextSettings を生成し、生成した FontAsset をデフォルトに設定
    ///  3. Panel Settings.asset の textSettings に割り当て
    /// 再実行すると既存アセットを上書き再生成する（冪等）。
    /// </summary>
    public static class UiFontSetup
    {
        private const string SourceFontPath = "Assets/Font/NotoSansJP-Bold.ttf";
        private const string FontAssetPath = "Assets/Font/NotoSansJP-Bold SDF.asset";
        private const string TextSettingsPath = "Assets/Font/UI Panel Text Settings.asset";
        private const string PanelSettingsPath = "Assets/Scripts/Panel Settings.asset";

        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        [MenuItem("Tools/UI/Setup Bold Japanese Font")]
        public static void Setup()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[UiFontSetup] ソースフォントが見つかりません: {SourceFontPath}");
                return;
            }

            FontAsset fontAsset = CreateOrReplaceFontAsset(sourceFont);
            if (fontAsset == null)
            {
                return;
            }

            PanelTextSettings textSettings = CreateOrUpdateTextSettings(fontAsset);
            AssignToPanelSettings(textSettings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UiFontSetup] 完了。FontAsset={FontAssetPath} を Panel Settings に割り当てました。", textSettings);
        }

        private static FontAsset CreateOrReplaceFontAsset(Font sourceFont)
        {
            // 既存があれば一旦削除して作り直す（冪等性のため）。
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(FontAssetPath);
            }

            // ダイナミックアトラス: 使用された字形だけを実行時にレンダリングするので
            // 全漢字を事前ベイクする必要がなく、アトラスも小さく済む。
            FontAsset fontAsset = FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[UiFontSetup] FontAsset の生成に失敗しました。フォントの Include Font Data 設定を確認してください。");
                return null;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // アトラステクスチャ・マテリアルをサブアセットとして保存する。
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    Texture2D atlas = fontAsset.atlasTextures[i];
                    if (atlas != null)
                    {
                        atlas.name = $"Atlas Texture {i}";
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{fontAsset.name} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static PanelTextSettings CreateOrUpdateTextSettings(FontAsset fontAsset)
        {
            PanelTextSettings textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(textSettings, TextSettingsPath);
            }

            // 公開プロパティ経由で設定（シリアライズ名に依存しない）。
            // Noto Sans JP Bold は Latin も日本語も含むため defaultFontAsset だけで足りる。
            textSettings.defaultFontAsset = fontAsset;

            EditorUtility.SetDirty(textSettings);
            return textSettings;
        }

        private static void AssignToPanelSettings(PanelTextSettings textSettings)
        {
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Debug.LogError($"[UiFontSetup] Panel Settings が見つかりません: {PanelSettingsPath}");
                return;
            }

            SerializedObject serialized = new SerializedObject(panelSettings);
            serialized.FindProperty("textSettings").objectReferenceValue = textSettings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelSettings);
        }
    }
}

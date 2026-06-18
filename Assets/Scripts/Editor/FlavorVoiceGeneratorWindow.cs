using System.Collections.Generic;
using System.IO;
using Main.Card;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Networking;

namespace GameEditor
{
    // 全カードのフレーバーテキストを VOICEVOX エンジン（ローカル起動の HTTP サーバ）で読み上げ音声に変換し、
    // Assets/AddressableAssets/Voice/{CardId}.wav として保存して Addressables アドレス "Voice/{CardId}" に登録する
    // エディタ専用ツール。ランタイムは FlavorVoiceStore がこのアドレスからオンデマンドでロードする。
    //
    // 使い方：VOICEVOX アプリ（またはエンジン）を起動した状態で、メニュー「Card/フレーバー音声を一括生成」から実行。
    public sealed class FlavorVoiceGeneratorWindow : EditorWindow
    {
        private const string OutputDir = "Assets/AddressableAssets/Voice";
        private const string AddressPrefix = "Voice/";

        // VOICEVOX エンジンのエンドポイント。既定はローカル起動時のポート。
        private string _host = "http://localhost:50021";
        // 話者（スピーカー）ID。既定 3＝ずんだもん（ノーマル）。全カード共通で使う。
        private int _speaker = 3;
        // 既に音声がある（WAV が存在する）カードを再生成せずスキップするか。
        private bool _skipExisting = true;

        [MenuItem("Card/フレーバー音声を一括生成")]
        public static void Open()
        {
            FlavorVoiceGeneratorWindow window = GetWindow<FlavorVoiceGeneratorWindow>("フレーバー音声生成");
            window.minSize = new Vector2(420, 220);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VOICEVOX フレーバー音声 一括生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "VOICEVOX アプリ（またはエンジン）を起動した状態で実行してください。\n" +
                "全カードの FlavorText を音声化し、Addressables アドレス \"Voice/{ID}\" に登録します。",
                MessageType.Info);

            EditorGUILayout.Space();
            _host = EditorGUILayout.TextField("エンジン URL", _host);
            _speaker = EditorGUILayout.IntField("話者 ID（speaker）", _speaker);
            _skipExisting = EditorGUILayout.Toggle("既存をスキップ（差分のみ生成）", _skipExisting);

            EditorGUILayout.Space();
            if (GUILayout.Button("生成開始", GUILayout.Height(32)))
            {
                Generate();
            }
        }

        private void Generate()
        {
            List<CardData> targets = CollectCardsWithFlavor();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("フレーバー音声生成", "フレーバーテキストを持つカードが見つかりませんでした。", "OK");
                return;
            }

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
                // 生成先フォルダを AssetDatabase に登録してから個別アセットを取り込む
                AssetDatabase.Refresh();
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("フレーバー音声生成",
                    "Addressables の設定が見つかりません。Addressables Groups を初期化してください。", "OK");
                return;
            }

            int generated = 0;
            int skipped = 0;
            int failed = 0;
            List<string> failedIds = new List<string>();

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    CardData card = targets[i];
                    string id = card.Id;
                    string path = $"{OutputDir}/{id}.wav";

                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "フレーバー音声生成",
                        $"({i + 1}/{targets.Count}) {id} {card.CardName}",
                        (float)i / targets.Count);
                    if (cancel)
                    {
                        break;
                    }

                    // 既存スキップ：WAV があればアドレスだけ確実に登録して次へ。
                    if (_skipExisting && File.Exists(path))
                    {
                        RegisterAddressable(settings, path, id);
                        skipped++;
                        continue;
                    }

                    byte[] wav = Synthesize(card.FlavorText, out string error);
                    if (wav == null || wav.Length == 0)
                    {
                        Debug.LogError($"フレーバー音声生成に失敗 [{id}]: {error}");
                        failed++;
                        failedIds.Add(id);
                        continue;
                    }

                    File.WriteAllBytes(path, wav);
                    AssetDatabase.ImportAsset(path);
                    RegisterAddressable(settings, path, id);
                    generated++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string message = $"生成: {generated} 件 / スキップ: {skipped} 件 / 失敗: {failed} 件";
            if (failed > 0)
            {
                message += "\n失敗ID: " + string.Join(", ", failedIds);
            }
            Debug.Log($"フレーバー音声生成 完了。{message}");
            EditorUtility.DisplayDialog("フレーバー音声生成", message, "OK");
        }

        // 全 CharacterCardSO / EventCardSO を横断し、フレーバーテキストを持つカードを ID 重複なしで集める。
        // 対戦専用トークンも OnCardPlayed で読み上げ対象になるため、プール除外フラグに関係なく全カードを含める。
        private static List<CardData> CollectCardsWithFlavor()
        {
            List<CardData> result = new List<CardData>();
            HashSet<string> seen = new HashSet<string>();

            AddFrom<CharacterCardSO>(result, seen);
            AddFrom<EventCardSO>(result, seen);
            return result;
        }

        private static void AddFrom<TSo>(List<CardData> result, HashSet<string> seen) where TSo : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(TSo).Name);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                TSo so = AssetDatabase.LoadAssetAtPath<TSo>(assetPath);
                IReadOnlyList<CardData> cards = GetCards(so);
                if (cards == null)
                {
                    continue;
                }

                foreach (CardData card in cards)
                {
                    if (card == null || string.IsNullOrEmpty(card.Id) || string.IsNullOrEmpty(card.FlavorText))
                    {
                        continue;
                    }
                    if (seen.Add(card.Id))
                    {
                        result.Add(card);
                    }
                }
            }
        }

        private static IReadOnlyList<CardData> GetCards(ScriptableObject so)
        {
            switch (so)
            {
                case CharacterCardSO characterSo:
                    return characterSo.Cards;
                case EventCardSO eventSo:
                    return eventSo.Cards;
                default:
                    return null;
            }
        }

        private void RegisterAddressable(AddressableAssetSettings settings, string path, string id)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.SetAddress(AddressPrefix + id);
        }

        // VOICEVOX の audio_query → synthesis を順に呼び、WAV バイト列を返す。失敗時は null。
        private byte[] Synthesize(string text, out string error)
        {
            string queryUrl = $"{_host}/audio_query?text={System.Uri.EscapeDataString(text)}&speaker={_speaker}";
            byte[] query = Post(queryUrl, null, null, out error);
            if (query == null)
            {
                return null;
            }

            string synthUrl = $"{_host}/synthesis?speaker={_speaker}";
            return Post(synthUrl, query, "application/json", out error);
        }

        // エディタ専用のため同期的に（完了までブロックして）リクエストする。
        private static byte[] Post(string url, byte[] body, string contentType, out string error)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                if (body != null && body.Length > 0)
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                if (!string.IsNullOrEmpty(contentType))
                {
                    req.SetRequestHeader("Content-Type", contentType);
                }

                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (!op.isDone)
                {
                    // 完了待ち（ブロッキング）
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    error = $"{(long)req.responseCode} {req.error}";
                    return null;
                }

                error = null;
                return req.downloadHandler.data;
            }
        }
    }
}

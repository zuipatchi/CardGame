using System;
using System.Collections.Generic;
using Main.Card;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    // 既存の単一 CharacterCard.asset / EventCards.asset を属性ごとの SO に分割するエディタ専用ツール。
    // 各属性フォルダ（Assets/Data/{属性}/）に CharacterCards_{属性}.asset / EventCards_{属性}.asset を生成・格納し、
    // CardDatabase.asset の属性別 SO 配列へ割り当てる。ID は属性ごとに自動採番される（CardIdAutoAssigner 参照）。
    public static class CardSoAttributeSplitter
    {
        private const string DataDir = "Assets/Data";
        private const string CharacterSourcePath = "Assets/Data/CharacterCard.asset";
        private const string EventSourcePath = "Assets/Data/EventCards.asset";
        private const string CardDatabasePath = "Assets/Data/CardDatabase.asset";

        [MenuItem("Card/属性別SOに分割")]
        public static void Split()
        {
            CharacterCardSO charSource = AssetDatabase.LoadAssetAtPath<CharacterCardSO>(CharacterSourcePath);
            EventCardSO eventSource = AssetDatabase.LoadAssetAtPath<EventCardSO>(EventSourcePath);
            if (charSource == null && eventSource == null)
            {
                Debug.LogError($"元のカードSOが見つかりません（{CharacterSourcePath} / {EventSourcePath}）。");
                return;
            }

            List<CharacterCardSO> characterSets = new List<CharacterCardSO>();
            List<EventCardSO> eventSets = new List<EventCardSO>();

            foreach (CardAttribute attribute in (CardAttribute[])Enum.GetValues(typeof(CardAttribute)))
            {
                string folder = $"{DataDir}/{attribute}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(DataDir, attribute.ToString());
                }

                if (charSource != null)
                {
                    List<CharacterCardData> chars = new List<CharacterCardData>();
                    foreach (CharacterCardData card in charSource.Cards)
                    {
                        if (card != null && card.Attribute == attribute)
                        {
                            chars.Add(card);
                        }
                    }
                    if (chars.Count > 0)
                    {
                        CharacterCardSO so = CreateOrLoad<CharacterCardSO>($"{folder}/CharacterCards_{attribute}.asset");
                        so.EditorSetCards(attribute, chars);
                        EditorUtility.SetDirty(so);
                        characterSets.Add(so);
                    }
                }

                if (eventSource != null)
                {
                    List<EventCardData> events = new List<EventCardData>();
                    foreach (EventCardData card in eventSource.Cards)
                    {
                        if (card != null && card.Attribute == attribute)
                        {
                            events.Add(card);
                        }
                    }
                    if (events.Count > 0)
                    {
                        EventCardSO so = CreateOrLoad<EventCardSO>($"{folder}/EventCards_{attribute}.asset");
                        so.EditorSetCards(attribute, events);
                        EditorUtility.SetDirty(so);
                        eventSets.Add(so);
                    }
                }
            }

            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(CardDatabasePath);
            if (database != null)
            {
                database.EditorSetSets(characterSets.ToArray(), eventSets.ToArray());
                EditorUtility.SetDirty(database);
            }
            else
            {
                Debug.LogWarning($"CardDatabase.asset が見つかりません（{CardDatabasePath}）。生成された属性別SOを手動で割り当ててください。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"属性別SOへの分割が完了しました（キャラSO {characterSets.Count} 種 / イベントSO {eventSets.Count} 種）。"
                + $"内容を確認したら元の {CharacterSourcePath} と {EventSourcePath} は削除してください。");
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }
            T so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }
    }
}

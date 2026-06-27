using System.Collections.Generic;
using UnityEngine;

namespace Common.Deck
{
    // プレイヤーのデッキを 9 スロット（0〜8）で保存・読み込みする。各スロットは
    // カード列（PlayerPrefs `SavedDeck_{slot}`）とデッキ名（`DeckName_{slot}`）を持ち、
    // 対戦に使う選択中スロットは `SelectedDeckIndex` に保存する。
    // 旧バージョンの単一デッキ（キー `SavedDeck`）はスロット 0 へ自動移行する。
    public sealed class DeckRepository
    {
        public const int SlotCount = 9;

        private const string LegacySaveKey = "SavedDeck";
        private const string CardsKeyPrefix = "SavedDeck_";
        private const string NameKeyPrefix = "DeckName_";
        private const string FavoriteKeyPrefix = "FavoriteCard_";
        private const string SelectedIndexKey = "SelectedDeckIndex";
        private const string StarterSeededKey = "StarterDeckSeeded";

        public DeckRepository()
        {
            MigrateLegacyIfNeeded();
        }

        // 初期デッキの自動生成が一度でも行われたか。初回起動時に一度だけシードするためのフラグ。
        public bool IsStarterSeeded => PlayerPrefs.GetInt(StarterSeededKey, 0) != 0;

        // 初期デッキをシードせずにフラグだけ立てる（既にデッキを持つ既存プレイヤー向け）。
        public void MarkStarterSeeded()
        {
            PlayerPrefs.SetInt(StarterSeededKey, 1);
            PlayerPrefs.Save();
        }

        // いずれかのスロットに保存済みデッキがあるか（既存プレイヤーへの初期デッキ上書きを防ぐ判定に使う）。
        public bool HasAnyDeck()
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (LoadCount(slot) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // カードID配列だけからスロットへ直接保存する（初期デッキのシード用）。
        // コストは対戦・表示のいずれでも使われず（カードIDから CardData で再解決される）ため 0 で保存する。
        public void SeedSlotWithIds(int slot, IReadOnlyList<string> cardIds)
        {
            if (!IsValidSlot(slot) || cardIds == null)
            {
                return;
            }

            DeckSaveData data = new DeckSaveData();
            foreach (string id in cardIds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    data.Cards.Add(new CardEntry { Id = id, Cost = 0 });
                }
            }
            PlayerPrefs.SetString(CardsKeyPrefix + slot, JsonUtility.ToJson(data));
            PlayerPrefs.SetInt(StarterSeededKey, 1);
            PlayerPrefs.Save();
        }

        // 対戦に使う選択中スロット。範囲外は 0 にクランプして返す。
        public int SelectedIndex
        {
            get
            {
                return Mathf.Clamp(PlayerPrefs.GetInt(SelectedIndexKey, 0), 0, SlotCount - 1);
            }
            set
            {
                PlayerPrefs.SetInt(SelectedIndexKey, Mathf.Clamp(value, 0, SlotCount - 1));
                PlayerPrefs.Save();
            }
        }

        public void Save(DeckModel deckModel, int slot)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }

            DeckSaveData data = new DeckSaveData();
            foreach ((string id, int cost) in deckModel.Entries)
            {
                data.Cards.Add(new CardEntry { Id = id, Cost = cost });
            }
            PlayerPrefs.SetString(CardsKeyPrefix + slot, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load(DeckModel deckModel, int slot)
        {
            deckModel.Clear();
            if (!IsValidSlot(slot))
            {
                return;
            }

            string json = PlayerPrefs.GetString(CardsKeyPrefix + slot, null);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            DeckSaveData data = JsonUtility.FromJson<DeckSaveData>(json);
            if (data?.Cards == null)
            {
                return;
            }

            foreach (CardEntry entry in data.Cards)
            {
                deckModel.Add(entry.Id, entry.Cost);
            }
        }

        // DeckModel へ展開せずに、スロットの保存枚数だけを取り出す（スロット一覧の表示用）。
        public int LoadCount(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return 0;
            }

            string json = PlayerPrefs.GetString(CardsKeyPrefix + slot, null);
            if (string.IsNullOrEmpty(json))
            {
                return 0;
            }

            DeckSaveData data = JsonUtility.FromJson<DeckSaveData>(json);
            return data?.Cards?.Count ?? 0;
        }

        public string LoadName(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return string.Empty;
            }
            return PlayerPrefs.GetString(NameKeyPrefix + slot, DefaultName(slot));
        }

        public void SaveName(int slot, string name)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }
            PlayerPrefs.SetString(NameKeyPrefix + slot, name);
            PlayerPrefs.Save();
        }

        public static string DefaultName(int slot)
        {
            return $"デッキ{slot + 1}";
        }

        // デッキのシンボルカード（スロットに小さく表示する代表カードID）。未設定は空文字。
        public string LoadFavorite(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return string.Empty;
            }
            return PlayerPrefs.GetString(FavoriteKeyPrefix + slot, string.Empty);
        }

        public void SaveFavorite(int slot, string cardId)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }
            PlayerPrefs.SetString(FavoriteKeyPrefix + slot, cardId ?? string.Empty);
            PlayerPrefs.Save();
        }

        // 旧単一デッキ（キー `SavedDeck`）があり、スロット 0 が未保存ならスロット 0 へ移し替える。
        // 移行後は旧キーを削除し、二重移行を防ぐ。
        private void MigrateLegacyIfNeeded()
        {
            if (!PlayerPrefs.HasKey(LegacySaveKey))
            {
                return;
            }

            string slot0Key = CardsKeyPrefix + 0;
            if (!PlayerPrefs.HasKey(slot0Key))
            {
                PlayerPrefs.SetString(slot0Key, PlayerPrefs.GetString(LegacySaveKey, string.Empty));
            }
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SlotCount;
        }

        [System.Serializable]
        private sealed class CardEntry
        {
            public string Id;
            public int Cost;
        }

        [System.Serializable]
        private sealed class DeckSaveData
        {
            public List<CardEntry> Cards = new List<CardEntry>();
        }
    }
}

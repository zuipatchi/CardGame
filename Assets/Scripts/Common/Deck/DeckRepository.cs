using System.Collections.Generic;
using UnityEngine;

namespace Common.Deck
{
    public sealed class DeckRepository
    {
        private const string SaveKey = "SavedDeck";

        public void Save(DeckModel deckModel)
        {
            DeckSaveData data = new DeckSaveData();
            foreach ((string id, int cost) in deckModel.Entries)
            {
                data.Cards.Add(new CardEntry { Id = id, Cost = cost });
            }
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        public void Load(DeckModel deckModel)
        {
            string json = PlayerPrefs.GetString(SaveKey, null);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            DeckSaveData data = JsonUtility.FromJson<DeckSaveData>(json);
            if (data?.Cards == null)
            {
                return;
            }

            deckModel.Clear();
            foreach (CardEntry entry in data.Cards)
            {
                deckModel.Add(entry.Id, entry.Cost);
            }
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

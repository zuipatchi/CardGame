using System.Collections.Generic;
using UnityEngine;

namespace Common.Deck
{
    public sealed class DeckRepository
    {
        private const string SaveKey = "SavedDeck";

        public void Save(DeckModel deckModel)
        {
            DeckSaveData data = new DeckSaveData { CardIds = new List<string>(deckModel.CardIds) };
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
            if (data?.CardIds == null)
            {
                return;
            }

            deckModel.Clear();
            foreach (string id in data.CardIds)
            {
                deckModel.TryAdd(id);
            }
        }

        [System.Serializable]
        private sealed class DeckSaveData
        {
            public List<string> CardIds;
        }
    }
}

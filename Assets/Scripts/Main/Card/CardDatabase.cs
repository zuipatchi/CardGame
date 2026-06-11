using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Card/CardDatabase")]
    public sealed class CardDatabase : ScriptableObject
    {
        [SerializeField] private CharacterCardSO[] _characterCardSets;
        [SerializeField] private EventCardSO[] _eventCardSets;

        private Dictionary<string, CardData> _dict;

        private void OnEnable()
        {
            Build();
        }

        private void Build()
        {
            _dict = new Dictionary<string, CardData>();
            if (_characterCardSets != null)
            {
                foreach (CharacterCardSO so in _characterCardSets)
                {
                    Register(so != null ? so.Cards : null);
                }
            }
            if (_eventCardSets != null)
            {
                foreach (EventCardSO so in _eventCardSets)
                {
                    Register(so != null ? so.Cards : null);
                }
            }
        }

        private void Register<T>(IReadOnlyList<T> cards) where T : CardData
        {
            if (cards == null)
            {
                return;
            }

            foreach (T card in cards)
            {
                if (card != null && !string.IsNullOrEmpty(card.Id))
                {
                    _dict[card.Id] = card;
                }
            }
        }

        public void Initialize(IReadOnlyList<CardData> cards)
        {
            _dict = new Dictionary<string, CardData>(cards?.Count ?? 0);
            if (cards == null)
            {
                return;
            }

            foreach (CardData card in cards)
            {
                if (card != null && !string.IsNullOrEmpty(card.Id))
                {
                    _dict[card.Id] = card;
                }
            }
        }

        public IReadOnlyList<CardData> AllCards
        {
            get
            {
                List<CardData> all = new List<CardData>();
                if (_characterCardSets != null)
                {
                    foreach (CharacterCardSO so in _characterCardSets)
                    {
                        AddAll(all, so != null ? so.Cards : null);
                    }
                }
                if (_eventCardSets != null)
                {
                    foreach (EventCardSO so in _eventCardSets)
                    {
                        AddAll(all, so != null ? so.Cards : null);
                    }
                }
                return all;
            }
        }

        private static void AddAll<T>(List<CardData> target, IReadOnlyList<T> source) where T : CardData
        {
            if (source == null)
            {
                return;
            }

            foreach (T card in source)
            {
                if (card != null)
                {
                    target.Add(card);
                }
            }
        }

        public CardData[] BuildDeck(IEnumerable<string> ids)
        {
            List<CardData> result = new List<CardData>();
            foreach (string id in ids)
            {
                if (_dict.TryGetValue(id, out CardData card))
                {
                    result.Add(card);
                }
            }

            return result.ToArray();
        }

        public bool TryGet(string id, out CardData card)
        {
            return _dict.TryGetValue(id, out card);
        }

        public CardData this[string id] => _dict[id];

#if UNITY_EDITOR
        // 移行ツール用：属性別 SO 配列を差し替える
        public void EditorSetSets(CharacterCardSO[] characterSets, EventCardSO[] eventSets)
        {
            _characterCardSets = characterSets;
            _eventCardSets = eventSets;
        }
#endif
    }
}

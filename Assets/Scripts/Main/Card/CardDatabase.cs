using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Card/CardDatabase")]
    public sealed class CardDatabase : ScriptableObject
    {
        [SerializeField] private CharacterCardSO _characterCards;
        [SerializeField] private SkillCardSO _skillCards;
        [SerializeField] private EventCardSO _eventCards;

        private Dictionary<string, CardData> _dict;

        private void OnEnable()
        {
            Build();
        }

        private void Build()
        {
            _dict = new Dictionary<string, CardData>();
            Register(_characterCards != null ? _characterCards.Cards : null);
            Register(_skillCards != null ? _skillCards.Cards : null);
            Register(_eventCards != null ? _eventCards.Cards : null);
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
                AddAll(all, _characterCards != null ? _characterCards.Cards : null);
                AddAll(all, _skillCards != null ? _skillCards.Cards : null);
                AddAll(all, _eventCards != null ? _eventCards.Cards : null);
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
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Card/CardDatabase")]
    public sealed class CardDatabase : ScriptableObject
    {
        [SerializeField] private List<CardData> _cards;

        private Dictionary<string, CardData> _dict;

        private void OnEnable()
        {
            Build(_cards);
        }

        private void Build(IReadOnlyList<CardData> cards)
        {
            _dict = new Dictionary<string, CardData>(cards?.Count ?? 0);
            if (cards == null)
            {
                return;
            }

            foreach (CardData card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.Id))
                {
                    continue;
                }

                _dict[card.Id] = card;
            }
        }

        public void Initialize(IReadOnlyList<CardData> cards)
        {
            Build(cards);
        }

        public IReadOnlyList<CardData> AllCards => _cards;

        public bool TryGet(string id, out CardData card)
        {
            return _dict.TryGetValue(id, out card);
        }

        public CardData this[string id] => _dict[id];
    }
}

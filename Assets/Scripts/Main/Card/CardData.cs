using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CardData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _cardName;
        [SerializeField] private int _cost;
        [SerializeField, TextArea] private string _effectText;
        [SerializeField] private int _attack;
        [SerializeField] private int _defense;

        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public string EffectText => _effectText;
        public int Attack => _attack;
        public int Defense => _defense;

        public CardData() { }

        public CardData(string id, string cardName, int cost, string effectText, int attack, int defense)
        {
            _id = id;
            _cardName = cardName;
            _cost = cost;
            _effectText = effectText;
            _attack = attack;
            _defense = defense;
        }
    }
}

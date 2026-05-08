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
        [SerializeField] private int _attack;
        [SerializeField] private int _defense;
        [SerializeField] private Sprite _image;

        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public int Attack => _attack;
        public int Defense => _defense;
        public Sprite Image => _image;

        public CardData() { }

        public CardData(string id, string cardName, int cost, int attack, int defense, Sprite image = null)
        {
            _id = id;
            _cardName = cardName;
            _cost = cost;
            _attack = attack;
            _defense = defense;
            _image = image;
        }
    }
}

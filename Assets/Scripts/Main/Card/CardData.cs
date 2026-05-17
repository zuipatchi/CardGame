using System;
using UnityEngine;

namespace Main.Card
{
    public abstract class CardData
    {
        [SerializeField] protected string _id;
        [SerializeField] protected string _cardName;
        [SerializeField] protected int _cost;
        [SerializeField] protected Sprite _image;
        [SerializeField] protected CardAttribute _attribute;

        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;
        public CardAttribute Attribute => _attribute;

        public virtual int Attack => 0;
        public virtual int Defense => 0;

        protected CardData() { }

        protected CardData(string id, string name, int cost)
        {
            _id = id;
            _cardName = name;
            _cost = cost;
        }
    }
}

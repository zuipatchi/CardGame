using UnityEngine;

namespace Main.Card
{
    public abstract class CardData
    {
        [SerializeField] protected string _id;
        [SerializeField] protected string _cardName;
        [SerializeField] protected int _cost;
        [SerializeField] protected Sprite _image;
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;

        public virtual int Attack => 0;
        public virtual int Hp => 0;
        public virtual CardAttribute Attribute => CardAttribute.White;

        protected CardData() { }

        protected CardData(string id, string name, int cost)
        {
            _id = id;
            _cardName = name;
            _cost = cost;
        }
    }
}

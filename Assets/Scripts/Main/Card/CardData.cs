using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public struct CardStat
    {
        [SerializeField] private string _key;
        [SerializeField] private int _value;

        public string Key => _key;
        public int Value => _value;

        public CardStat(string key, int value)
        {
            _key = key;
            _value = value;
        }
    }

    public abstract class CardData
    {
        [SerializeField] protected string _id;
        [SerializeField] protected string _cardName;
        [SerializeField] protected int _cost;
        [SerializeField] protected Sprite _image;
        [SerializeField] protected List<CardStat> _stats;

        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public Sprite Image => _image;

        public virtual int Attack => 0;
        public virtual int Defense => 0;

        protected CardData() { }

        protected CardData(string id, string name, int cost, List<CardStat> stats = null)
        {
            _id = id;
            _cardName = name;
            _cost = cost;
            _stats = stats;
        }

        protected int GetStat(string key)
        {
            if (_stats == null)
            {
                return 0;
            }

            foreach (CardStat stat in _stats)
            {
                if (stat.Key == key)
                {
                    return stat.Value;
                }
            }

            return 0;
        }
    }
}

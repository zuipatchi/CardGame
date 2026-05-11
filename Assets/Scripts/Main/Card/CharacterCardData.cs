using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CharacterCardData : CardData
    {
        [SerializeField] private int _defense;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int defense)
            : base(id, name, cost)
        {
            _defense = defense;
        }

        public override int Defense => _defense;
    }
}

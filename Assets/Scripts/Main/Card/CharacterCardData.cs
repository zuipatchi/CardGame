using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CharacterCardData : CardData
    {
        [SerializeField] private int _attack;
        [SerializeField] private int _defense;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int attack, int defense, CardAttribute attribute = CardAttribute.None)
            : base(id, name, cost)
        {
            _attack = attack;
            _defense = defense;
            _attribute = attribute;
        }

        public override int Attack => _attack;
        public override int Defense => _defense;
    }
}

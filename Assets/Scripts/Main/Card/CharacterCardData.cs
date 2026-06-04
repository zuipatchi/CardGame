using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CharacterCardData : CardData
    {
        [SerializeField] private int _attack;
        [SerializeField] private int _defense;
        [SerializeField] private int _speed;
        [SerializeField] private int _hp;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int attack, int defense = 0, int speed = 0, int hp = 0)
            : base(id, name, cost)
        {
            _attack = attack;
            _defense = defense;
            _speed = speed;
            _hp = hp;
        }

        public override int Attack => _attack;
        public override int Defense => _defense;
        public override int Speed => _speed;
        public override int Hp => _hp;
    }
}

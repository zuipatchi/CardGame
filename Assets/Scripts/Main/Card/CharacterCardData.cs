using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CharacterCardData : CardData
    {
        [SerializeField] private int _attack;
        [SerializeField] private int _hp;
        [SerializeField] private CardAttribute _attribute;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int attack, int hp = 0, CardAttribute attribute = CardAttribute.White)
            : base(id, name, cost)
        {
            _attack = attack;
            _hp = hp;
            _attribute = attribute;
        }

        public override int Attack => _attack;
        public override int Hp => _hp;
        public override CardAttribute Attribute => _attribute;
    }
}

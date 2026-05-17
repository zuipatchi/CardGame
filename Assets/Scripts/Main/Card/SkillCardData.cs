using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class SkillCardData : CardData
    {
        [SerializeField] private int _damage;

        public SkillCardData() { }

        public SkillCardData(string id, string name, int cost, int damage, CardAttribute attribute = CardAttribute.None)
            : base(id, name, cost)
        {
            _damage = damage;
            _attribute = attribute;
        }

        public int Damage => _damage;
        public override int Attack => _damage;
    }
}

using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class SkillCardData : CardData
    {
        [SerializeField] private SkillType _skillType;
        [SerializeField] private int _skillValue;

        public SkillCardData() { }

        public SkillCardData(string id, string name, int cost, SkillType skillType, int skillValue)
            : base(id, name, cost)
        {
            _skillType = skillType;
            _skillValue = skillValue;
        }

        public SkillType SkillType => _skillType;
        public int SkillValue => _skillValue;
        public override int Attack => _skillType is SkillType.Attack or SkillType.Poison ? _skillValue : 0;
    }
}

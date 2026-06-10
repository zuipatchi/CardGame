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
        [SerializeField] private CharacterEffectTrigger _effectTrigger;
        [SerializeField] private EventType _effectType;
        [SerializeField] private int _effectValue;
        [SerializeField] private string _description;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int attack, int hp = 0, CardAttribute attribute = CardAttribute.White,
            CharacterEffectTrigger effectTrigger = CharacterEffectTrigger.None, EventType effectType = EventType.None, int effectValue = 0, string description = "")
            : base(id, name, cost)
        {
            _attack = attack;
            _hp = hp;
            _attribute = attribute;
            _effectTrigger = effectTrigger;
            _effectType = effectType;
            _effectValue = effectValue;
            _description = description;
        }

        public override int Attack => _attack;
        public override int Hp => _hp;
        public override CardAttribute Attribute => _attribute;
        public CharacterEffectTrigger EffectTrigger => _effectTrigger;
        public EventType EffectType => _effectType;
        public int EffectValue => _effectValue;
        public string Description => _description;

        // OnUsedAsCost + CostBoost のキャラは、コスト支払い時に EffectValue 分（最低1）として数える
        public override int CostPaymentValue =>
            _effectTrigger == CharacterEffectTrigger.OnUsedAsCost && _effectType == EventType.CostBoost
                ? Mathf.Max(1, _effectValue)
                : 1;
    }
}

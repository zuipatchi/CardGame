using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class EventCardData : CardData
    {
        [SerializeField] private EffectType _effectType;
        [SerializeField] private int _effectValue;

        public EffectType EffectType => _effectType;
        public int EffectValue => _effectValue;

        public EventCardData() { }

        public EventCardData(string id, string name, int cost)
            : base(id, name, cost) { }

        public EventCardData(string id, string name, int cost, EffectType effectType, int effectValue)
            : base(id, name, cost)
        {
            _effectType = effectType;
            _effectValue = effectValue;
        }
    }
}

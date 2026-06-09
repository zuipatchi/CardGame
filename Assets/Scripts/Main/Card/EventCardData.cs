using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class EventCardData : CardData
    {
        [SerializeField] private EventType _eventType;
        [SerializeField] private int _eventValue;
        [SerializeField] private string _description;
        [SerializeField] private bool _triggerOnGrave;
        [SerializeField] private CardAttribute _attribute;

        public EventType EventType => _eventType;
        public int EventValue => _eventValue;
        public string Description => _description;
        public bool TriggerOnGrave => _triggerOnGrave;
        public override CardAttribute Attribute => _attribute;

        public EventCardData() { }

        public EventCardData(string id, string name, int cost, CardAttribute attribute = CardAttribute.White)
            : base(id, name, cost)
        {
            _attribute = attribute;
        }

        public EventCardData(string id, string name, int cost, EventType eventType, int eventValue, string description = "", bool triggerOnGrave = false, CardAttribute attribute = CardAttribute.White)
            : base(id, name, cost)
        {
            _eventType = eventType;
            _eventValue = eventValue;
            _description = description;
            _triggerOnGrave = triggerOnGrave;
            _attribute = attribute;
        }
    }
}

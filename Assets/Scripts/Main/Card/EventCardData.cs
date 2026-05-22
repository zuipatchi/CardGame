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

        public EventType EventType => _eventType;
        public int EventValue => _eventValue;
        public string Description => _description;

        public EventCardData() { }

        public EventCardData(string id, string name, int cost)
            : base(id, name, cost) { }

        public EventCardData(string id, string name, int cost, EventType eventType, int eventValue, string description = "")
            : base(id, name, cost)
        {
            _eventType = eventType;
            _eventValue = eventValue;
            _description = description;
        }
    }
}

using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class EventCardData : CardData
    {
        [SerializeField] private EventType _eventType;
        [SerializeField] private int _eventValue;
        [SerializeField] private int _eventValue2;
        [SerializeField] private string _description;
        [SerializeField] private bool _triggerOnGrave;
        [SerializeField] private CardAttribute _attribute;

        public EventType EventType => _eventType;
        public int EventValue => _eventValue;
        // 効果ごとの2つ目の数値（例: SummonChar の召喚体数）。未使用の効果では 0
        public int EventValue2 => _eventValue2;
        public string Description => _description;
        public bool TriggerOnGrave => _triggerOnGrave;
        public override CardAttribute Attribute => _attribute;

        // CostBoost のイベントは、コスト支払い時に EventValue 分（最低1）として数える
        public override int CostPaymentValue =>
            _eventType == EventType.CostBoost ? Mathf.Max(1, _eventValue) : 1;

        public EventCardData() { }

        public EventCardData(string id, string name, int cost, CardAttribute attribute = CardAttribute.White)
            : base(id, name, cost)
        {
            _attribute = attribute;
        }

        public EventCardData(string id, string name, int cost, EventType eventType, int eventValue, string description = "", bool triggerOnGrave = false, CardAttribute attribute = CardAttribute.White, int eventValue2 = 0)
            : base(id, name, cost)
        {
            _eventType = eventType;
            _eventValue = eventValue;
            _eventValue2 = eventValue2;
            _description = description;
            _triggerOnGrave = triggerOnGrave;
            _attribute = attribute;
        }
    }
}

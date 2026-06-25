using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class EventCardData : CardData
    {
        // 発動タイミング。OnPlay（既定）はプレイ時に即時解決。OnTurnStart は墓地から毎ターン開始時に発動し続ける
        [SerializeField] private EventCardTrigger _eventTrigger;
        [SerializeField] private EventType _eventType;
        [SerializeField] private int _eventValue;
        [SerializeField] private int _eventValue2;
        [SerializeField] private string _description;
        // 属性は所属する EventCardSO（属性別 SO）が一括設定するため、インスペクタでは読み取り専用
        [SerializeField, ReadOnly] private CardAttribute _attribute;

        public EventType EventType => _eventType;
        public int EventValue => _eventValue;
        // 効果ごとの2つ目の数値（例: SummonChar の召喚体数）。未使用の効果では 0
        public int EventValue2 => _eventValue2;
        public string Description => _description;
        // 発動タイミング（OnPlay：プレイ時即時 / OnTurnStart：墓地から毎ターン開始時）
        public EventCardTrigger EventTrigger => _eventTrigger;
        public override CardAttribute Attribute => _attribute;

        // コスト素材にできない（お邪魔トークン）なら 0。それ以外で CostBoost のイベントは、
        // 支払い対象が自属性のとき EventValue 分（最低1）として数える。
        // それ以外の属性のコストに使うときは通常どおり1（白も一般属性として扱い、白CostBoostは白のコストのみ倍化）。
        public override int CostPaymentValue(CardAttribute payingForAttribute)
        {
            if (_cannotBeUsedAsCost)
            {
                return 0;
            }
            return _eventType == EventType.CostBoost && _attribute == payingForAttribute
                ? Mathf.Max(1, _eventValue)
                : 1;
        }

#if UNITY_EDITOR
        // 属性別 SO が所属カードの属性を一括設定するためのエディタ専用 setter
        public void EditorSetAttribute(CardAttribute attribute)
        {
            _attribute = attribute;
        }
#endif

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

using System;
using UnityEngine;

namespace Main.Card
{
    [Serializable]
    public sealed class CharacterCardData : CardData
    {
        [SerializeField] private int _attack;
        [SerializeField] private int _hp;
        // 属性は所属する CharacterCardSO（属性別 SO）が一括設定するため、インスペクタでは読み取り専用
        [SerializeField, ReadOnly] private CardAttribute _attribute;
        [SerializeField] private CharacterEffectTrigger _effectTrigger;
        [SerializeField] private EventType _effectType;
        [SerializeField] private int _effectValue;
        [SerializeField] private int _effectValue2;
        // 守護：このキャラが場にいる間、相手はこのキャラ（守護持ち）にしか攻撃できない
        [SerializeField] private bool _guardian;
        // 速攻：召喚酔いせず、場に出したターンから攻撃できる
        [SerializeField] private bool _haste;
        // 飛行：守護を無視して攻撃対象を選べ、飛行を持つキャラからしか攻撃されない
        [SerializeField] private bool _flying;
        // 防人：飛行への対空ガード。飛行はこのキャラを優先して攻撃しなければならず、このキャラは飛行に攻撃できる
        [SerializeField] private bool _sakimori;
        // 強襲：アンタップ状態のキャラにも攻撃できる（通常はタップ済みキャラにしか攻撃できない制限を無視する）
        [SerializeField] private bool _assault;
        // デッキ攻撃×：このキャラ自身は相手デッキを直接攻撃（ミル）できない。制限を受けるのはこの能力を持つキャラだけ
        [SerializeField] private bool _noDeckAttack;
        // 射手：飛行を持たない地上キャラながら、飛行を持つ相手キャラに攻撃できる（対空攻撃）
        [SerializeField] private bool _archer;
        // 必殺：相手キャラへの攻撃時、ダメージ計算を行わず対象を破壊する（HP に関係なく・ATK 0 でも破壊）
        [SerializeField] private bool _deadly;
        [SerializeField] private string _description;

        public CharacterCardData() { }

        public CharacterCardData(string id, string name, int cost, int attack, int hp = 0, CardAttribute attribute = CardAttribute.White,
            CharacterEffectTrigger effectTrigger = CharacterEffectTrigger.None, EventType effectType = EventType.None, int effectValue = 0, string description = "", int effectValue2 = 0, bool guardian = false, bool haste = false, bool flying = false, bool sakimori = false, bool assault = false, bool noDeckAttack = false, bool archer = false, bool deadly = false)
            : base(id, name, cost)
        {
            _attack = attack;
            _hp = hp;
            _attribute = attribute;
            _effectTrigger = effectTrigger;
            _effectType = effectType;
            _effectValue = effectValue;
            _effectValue2 = effectValue2;
            _guardian = guardian;
            _haste = haste;
            _flying = flying;
            _sakimori = sakimori;
            _assault = assault;
            _noDeckAttack = noDeckAttack;
            _archer = archer;
            _deadly = deadly;
            _description = description;
        }

        public override int Attack => _attack;
        public override int Hp => _hp;
        public override CardAttribute Attribute => _attribute;
        public CharacterEffectTrigger EffectTrigger => _effectTrigger;
        public EventType EffectType => _effectType;
        public int EffectValue => _effectValue;
        // 効果ごとの2つ目の数値（例: SummonChar の召喚体数）。未使用の効果では 0
        public int EffectValue2 => _effectValue2;
        // 守護：場にいる間、相手の攻撃をこのキャラに強制する
        public bool Guardian => _guardian;
        // 速攻：召喚酔いせず、出したターンから攻撃できる
        public bool Haste => _haste;
        // 飛行：守護を無視して攻撃対象を選べ、飛行を持つキャラからしか攻撃されない
        public bool Flying => _flying;
        // 防人：飛行はこのキャラを優先して攻撃しなければならず、このキャラは飛行に攻撃できる
        public bool Sakimori => _sakimori;
        // 強襲：アンタップ状態のキャラにも攻撃できる（タップ済み要件を無視する）
        public bool Assault => _assault;
        // デッキ攻撃×：このキャラ自身は相手デッキを直接攻撃（ミル）できない
        public bool NoDeckAttack => _noDeckAttack;
        // 射手：飛行を持たない地上キャラながら、飛行を持つ相手キャラに攻撃できる（対空攻撃）
        public bool Archer => _archer;
        // 必殺：相手キャラへの攻撃時、ダメージ計算を行わず対象を破壊する（HP に関係なく・ATK 0 でも破壊）
        public bool Deadly => _deadly;
        public string Description => _description;

        // コスト素材にできない（お邪魔トークン）なら 0。それ以外で OnUsedAsCost + CostBoost のキャラは、
        // 支払い対象が自属性のとき EffectValue 分（最低1）として数える。
        // それ以外の属性のコストに使うときは通常どおり1（白も一般属性として扱い、白CostBoostは白のコストのみ倍化）。
        public override int CostPaymentValue(CardAttribute payingForAttribute)
        {
            if (_cannotBeUsedAsCost)
            {
                return 0;
            }
            return _effectTrigger == CharacterEffectTrigger.OnUsedAsCost && _effectType == EventType.CostBoost
                && _attribute == payingForAttribute
                ? Mathf.Max(1, _effectValue)
                : 1;
        }

#if UNITY_EDITOR
        // 属性別 SO が所属カードの属性を一括設定するためのエディタ専用 setter
        public void EditorSetAttribute(CardAttribute attribute)
        {
            _attribute = attribute;
        }
#endif
    }
}

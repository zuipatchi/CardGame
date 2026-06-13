namespace Main.Card
{
    // キャラカードの効果発動タイミング。
    // OnEnter（登場時）と OnAttack（攻撃時）を実装済み。OnDestroy は将来の拡張用に定義のみ。
    // OnUsedAsCost（コストとして使用する）は EffectType.CostBoost と組み合わせ、
    // 手札からコストとして支払うときに EffectValue 分のコストとして数える（コスト倍化）。
    // OnTurnStart（自分のターン開始時）は、このキャラが場にいる限り自分のターン開始時（ドロー前）に毎ターン1回発動する。
    public enum CharacterEffectTrigger
    {
        None,
        OnEnter,
        OnAttack,
        OnDestroy,
        OnUsedAsCost,
        OnTurnStart,
    }
}

namespace Main.Card
{
    // キャラカードの効果発動タイミング。
    // OnEnter（登場時）と OnAttack（攻撃時）を実装済み。OnDestroy は将来の拡張用に定義のみ。
    // OnUsedAsCost（コストとして使用する）は EffectType.CostBoost と組み合わせ、
    // 手札からコストとして支払うときに EffectValue 分のコストとして数える（コスト倍化）。
    // OnTurnStart（自分のターン開始時）は、このキャラが場にいる限り自分のターン開始時（ドロー前）に毎ターン1回発動する。
    // OnAttacked（被攻撃時）は、相手キャラの攻撃の対象になったときに発動する（攻撃側 ATK0 の盾ブロック=ダメージ0でも発動。効果ダメージやハート攻撃では発動しない）。
    // OnKill（撃破時）は、このキャラの攻撃で相手キャラを破壊したときに発動する（戦闘のみ。効果破壊やハート攻撃では発動しない）。
    // ※ メンバーは末尾に追加すること（既存の serialized 整数値がズレるため並べ替え・中間削除をしない）。
    public enum CharacterEffectTrigger
    {
        None,
        OnEnter,
        OnAttack,
        OnDestroy,
        OnUsedAsCost,
        OnTurnStart,
        OnAttacked,
        OnKill,
    }
}

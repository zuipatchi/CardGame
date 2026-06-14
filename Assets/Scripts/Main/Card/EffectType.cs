namespace Main.Card
{
    public enum EventType
    {
        None,
        AtkBoost,
        DefBoost,
        Draw,
        Negate,
        BanishChar,
        Recover,
        Switch,
        Evolve,
        // 手札からコストとして支払うときに、コスト値（EventValue / EffectValue）分のコストとして数える。
        // キャラは CharacterEffectTrigger.OnUsedAsCost と併用、イベントは EventType 単体で判定。通常プレイ時は無効果。
        CostBoost,
        // 発動側から見た敵フィールドのキャラ全員に EventValue / EffectValue 分のダメージを与え、
        // HP が 0 以下になったキャラを破壊する。敵キャラがいなければ空振り。
        DamageAllEnemies,
        // 発動した側の勝利点（緑属性の勝利条件）に EventValue / EffectValue 分を加算する。
        GainVictoryPoints,
        // 発動した側の墓地にある緑属性カードの枚数だけ、自分の勝利点（緑属性の勝利条件）に加算する。
        // EventValue / EffectValue は不使用（0）。墓地に緑カードがなければ加点 0。
        GainVPPerGreenGrave,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // それぞれに EventValue2 / EffectValue2 分のダメージ（値2）を同時に与え、HP 0 以下を破壊する。
        // 対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        DamageEnemy,
        // 発動側の自フィールドに、EventValue / EffectValue が示すキャラ（数字部分→"C###"）を
        // EventValue2 / EffectValue2 体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。
        // 召喚キャラの OnEnter も発動する。
        SummonChar,
        // 発動した側が次にプレイするカード1枚のコストを0にする（使うまで持続。EventValue は不使用）。
        NextCardCostFree,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // 所有者（相手）の手札へ戻す。対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        Bounce,
        // 発動側から見た敵フィールドのキャラ全員を所有者（相手）の手札へ戻す（Bounce の全員対象版）。
        // 対象選択は不要。敵キャラ0体なら空振り。EventValue / EffectValue は不使用（0）。
        BounceAll,
        // 発動した（アクティブな）プレイヤーが、相手にターンを渡さずもう一度自分のターンを行う。
        // EventValue は不使用（0）。1ターン中に複数回発動しても追加ターンは1回（フラグ管理）。
        ExtraTurn,
    }
}

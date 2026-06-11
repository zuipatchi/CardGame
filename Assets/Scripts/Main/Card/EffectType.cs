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
        // 発動側から見た敵フィールドのキャラ1体に EventValue / EffectValue 分のダメージを与え、
        // HP が 0 以下になったキャラを破壊する。対象はプレイヤーが選択（敵が1体なら自動・0体なら空振り）。
        DamageEnemy,
    }
}

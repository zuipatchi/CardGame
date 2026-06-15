namespace Main.Card
{
    // シリアライズの安定性のため明示的な整数値を割り当てている。
    // メンバーを削除・並べ替えする場合も既存値を変えないこと（.asset が整数で参照しているため）。
    // 11 は旧 GainVictoryPoints（撤去・CardData.VictoryPointBonus 付帯値へ統合）の欠番。
    public enum EventType
    {
        None = 0,
        AtkBoost = 1,
        DefBoost = 2,
        Draw = 3,
        Negate = 4,
        BanishChar = 5,
        Recover = 6,
        Switch = 7,
        Evolve = 8,
        // 手札からコストとして支払うときに、コスト値（EventValue / EffectValue）分のコストとして数える。
        // キャラは CharacterEffectTrigger.OnUsedAsCost と併用、イベントは EventType 単体で判定。通常プレイ時は無効果。
        CostBoost = 9,
        // 発動側から見た敵フィールドのキャラ全員に EventValue / EffectValue 分のダメージを与え、
        // HP が 0 以下になったキャラを破壊する。敵キャラがいなければ空振り。
        DamageAllEnemies = 10,
        // 11 は欠番（旧 GainVictoryPoints）。勝利点の付与は CardData.VictoryPointBonus に統合した。
        // 発動した側の墓地にある緑属性カードの枚数だけ、自分の勝利点（勝利点の勝利条件への加点）に加算する。
        // EventValue / EffectValue は不使用（0）。墓地に緑カードがなければ加点 0。
        GainVPPerGreenGrave = 12,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // それぞれに EventValue2 / EffectValue2 分のダメージ（値2）を同時に与え、HP 0 以下を破壊する。
        // 対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        DamageEnemy = 13,
        // 発動側の自フィールドに、EventValue / EffectValue が示すキャラ（数字部分→"C###"）を
        // EventValue2 / EffectValue2 体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。
        // 召喚キャラの OnEnter も発動する。
        SummonChar = 14,
        // 発動した側が次にプレイするカード1枚のコストを0にする（使うまで持続。EventValue は不使用）。
        NextCardCostFree = 15,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // 所有者（相手）の手札へ戻す。対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        Bounce = 16,
        // 発動側から見た敵フィールドのキャラ全員を所有者（相手）の手札へ戻す（Bounce の全員対象版）。
        // 対象選択は不要。敵キャラ0体なら空振り。EventValue / EffectValue は不使用（0）。
        BounceAll = 17,
        // 発動した（アクティブな）プレイヤーが、相手にターンを渡さずもう一度自分のターンを行う。
        // EventValue は不使用（0）。1ターン中に複数回発動しても追加ターンは1回（フラグ管理）。
        ExtraTurn = 18,
        // 発動側の自フィールドのキャラ全員の HP を EventValue / EffectValue 分回復する（最大HPでクランプ）。
        // EventValue / EffectValue = 0 のときは最大HPまで全回復する。自キャラがいなければ空振り。
        HealAllAllies = 19,
        // 発動側が即座に EventValue / EffectValue 枚ドローし、そのプレイヤーの次のドローフェーズを1回スキップする。
        // EventValue / EffectValue = ドロー枚数（EventValue2 / EffectValue2 は不使用）。
        DrawSkipNext = 20,
        // 発動時には引かず、そのプレイヤーの次のターン開始時（次のドローフェーズ）に EventValue / EffectValue 枚ドローする。
        // 通常ドローに上乗せして引く。複数回発動すると枚数は累積する。EventValue2 / EffectValue2 は不使用。
        DrawNextTurnStart = 21,
        // 発動側の自フィールドにいる、発動キャラと同じ特徴（CardData.Keyword）を持つ他のキャラ（自分自身を除く）の
        // 攻撃力を EventValue / EffectValue 上げる（発動時に一度だけ永続加算。後から出たキャラには適用されない）。
        // 発動キャラの特徴が未設定（空）なら空振り。EventValue2 / EffectValue2 は不使用。
        BuffAttackByKeyword = 22,
        // BuffAttackByKeyword と同じ対象選択で、HP（現在HP・最大HP両方）を EventValue / EffectValue 上げる。
        BuffHpByKeyword = 23,
    }
}

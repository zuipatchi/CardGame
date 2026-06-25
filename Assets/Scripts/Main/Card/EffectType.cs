namespace Main.Card
{
    // シリアライズの安定性のため明示的な整数値を割り当てている。
    // メンバーを削除・並べ替えする場合も既存値を変えないこと（.asset が整数で参照しているため）。
    // メンバーの宣言順はアルファベット順（None は慣例で先頭）。整数値は連番でなくてよい。
    // 11 は旧 GainVictoryPoints（撤去・CardData.VictoryPointBonus 付帯値へ統合）の欠番。
    // 4 は旧 Negate（撤去）の欠番。新メンバーにこれらの整数値は再利用しないこと。
    public enum EventType
    {
        None = 0,
        // 発動側のデッキから、発動カード自身の特徴（CardData.Keyword）を持つカード（キャラ・イベント問わず）を
        // EventValue / EffectValue 枚（値1）選んで手札に加える。候補が値1以下なら全部・特徴未設定／一致カードなしなら空振り。
        // 候補が値1より多いときはプレイヤーがピッカーで選ぶ（CPU は高コスト順・オンラインはデッキ内インデックスで同期）。
        // 手札が上限（8枚）に達したら超過分は墓地へ送る（Draw と同じバーン）。EventValue2 / EffectValue2 は不使用。
        AddToHandFromDeckByKeyword = 30,
        // 発動側自身の墓地から、カード（キャラ・イベント問わず）を EventValue / EffectValue 枚（値1。未設定=0 は1枚）選んで手札に加える。
        // 墓地から消費する。候補が値1以下なら全部・墓地が空なら空振り。場には出さないため OnEnter は発動しない。
        // 候補が値1より多いときはプレイヤーがピッカーで選ぶ（CPU は高コスト順・オンラインは墓地内インデックスで同期）。
        // 手札が上限（8枚）に達したら超過分は墓地へ戻す（Draw と同じバーン）。EventValue2 / EffectValue2 は不使用。
        AddToHandFromGrave = 38,
        // 発動側が自フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // それぞれの攻撃力を EventValue2 / EffectValue2（値2）分、永続的に上げる（発動時に一度だけ加算）。
        // 対象数が味方の数以上なら全員。対象はプレイヤーが選択（CPU は攻撃力上位・オンラインはフィールド内インデックスで同期）。
        AtkBoost = 1,
        BanishChar = 5,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // 所有者（相手）の手札へ戻す。対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        Bounce = 16,
        // 発動側から見た敵フィールドのキャラ全員を所有者（相手）の手札へ戻す（Bounce の全員対象版）。
        // 対象選択は不要。敵キャラ0体なら空振り。EventValue / EffectValue は不使用（0）。
        BounceAll = 17,
        // 発動側の自フィールドにいる、発動キャラと同じ特徴（CardData.Keyword）を持つ他のキャラ（自分自身を除く）の
        // 攻撃力を EventValue / EffectValue 上げる（発動時に一度だけ永続加算。後から出たキャラには適用されない）。
        // 発動キャラの特徴が未設定（空）なら空振り。EventValue2 / EffectValue2 は不使用。
        BuffAttackByKeyword = 22,
        // BuffAttackByKeyword と同じ対象選択で、HP（現在HP・最大HP両方）を EventValue / EffectValue 上げる。
        BuffHpByKeyword = 23,
        // コインを振り、表が出るたびにカードを1枚引く（裏が出たら終了）。
        // EventValue / EffectValue・EventValue2 / EffectValue2 は不使用（0）。
        CoinDraw = 31,
        // 発動側が自フィールドのキャラを1体選び、そのキャラのコピーを EventValue / EffectValue 体（未設定=0 は1体）自フィールドに出す。
        // コピーはバフ・現在HP込みの状態を複製する。対象はプレイヤーが選択（CPU は攻撃力上位・オンラインはフィールド内インデックスで同期）。
        // 配置時に OnEnter も発動。フィールド満杯で打ち切り。自キャラが0体なら空振り。EventValue2 / EffectValue2 は不使用。
        CopyFieldChar = 25,
        // 手札からコストとして支払うときに、コスト値（EventValue / EffectValue）分のコストとして数える。
        // キャラは CharacterEffectTrigger.OnUsedAsCost と併用、イベントは EventType 単体で判定。通常プレイ時は無効果。
        CostBoost = 9,
        // 発動側から見た敵フィールドのキャラ全員に EventValue / EffectValue 分のダメージを与え、
        // HP が 0 以下になったキャラを破壊する。敵キャラがいなければ空振り。
        DamageAllEnemies = 10,
        // お互いのデッキの上から EventValue / EffectValue 枚ずつを墓地へ送る（デッキへのダメージ＝ミル）。
        // 自分 → 相手の順にミルする。ミルされたカードが「ダメージトリガー」なら持ち主がコストなしで使用する。
        // 自分のデッキを先に削り、その時点で0枚なら自分がデッキ切れで即敗北（続く相手のミルは行われない）。EventValue2 / EffectValue2 は不使用。
        DamageBothDecks = 27,
        // 発動側の自フィールドにいる、EventValue / EffectValue（値1）が示す属性のキャラの数 N を数え、
        // 相手デッキの上から N 枚を墓地へ送る（デッキへのダメージ＝ミル）。
        // さらに同じ N 枚を発動側自身の墓地の上から取り出してデッキへ戻しシャッフルする（Recover と同じ）。
        // 値1=属性番号（白1/青2/緑3/黄4/赤5/黒6/紫7。0=属性を問わず自フィールドの全キャラ）。範囲外なら空振り。
        // N は同期済み盤面から決定的に算出されるため追加同期は不要。EventValue2 / EffectValue2 は不使用（0）。
        DamageDeckRecoverByColorChars = 35,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。未設定=0 は1体）選び、
        // それぞれに EventValue2 / EffectValue2 分のダメージ（値2）を同時に与え、HP 0 以下を破壊する。
        // 対象はプレイヤーが選択（対象数が敵の数以上なら全員・0体なら空振り）。
        DamageEnemy = 13,
        // 発動側から見た敵フィールドのキャラを EventValue / EffectValue 体（値1。0=敵全員）選び、
        // それぞれの攻撃力を EventValue2 / EffectValue2（値2）分、永続的に下げる（発動時に一度だけ減算・0未満にはならない）。
        // 対象数が敵の数以上なら全員。対象はプレイヤーが選択（CPU は攻撃力上位・オンラインはフィールド内インデックスで同期）。
        // 敵キャラ0体／値2≤0なら空振り。AtkBoost の敵版・マイナス版。
        DebuffAttack = 36,
        // 相手のデッキの上から EventValue / EffectValue 枚を墓地へ送る（デッキへのダメージ＝ミル）。
        // ミルされたカードが「ダメージトリガー」なら持ち主（相手）がコストなしで使用する。
        // 相手デッキが0枚になればデッキ切れで相手が敗北。EventValue2 / EffectValue2 は不使用。
        DamageEnemyDeck = 28,
        // サイコロ（6面）を振り、出た目の数だけカードを引く。
        // EventValue / EffectValue・EventValue2 / EffectValue2 は不使用（0）。
        DiceDraw = 32,
        // ハンデス：相手プレイヤーは手札を EventValue / EffectValue 枚（値1）捨てる（捨てるカードは手札の持ち主が選ぶ）。
        // 手札枚数が値1未満なら手札全部。手札が0枚なら空振り。EventValue2 / EffectValue2 は不使用。
        Discard = 33,
        // 相手プレイヤーの勝利点を EventValue / EffectValue 分（値1）下げる（0未満にはならない＝0でクランプ）。
        // 相手の勝利点が既に0なら空振り。固定値のためオンラインでも決定的に解決される（追加同期不要）。EventValue2 / EffectValue2 は不使用。
        ReduceEnemyVP = 37,
        // デッキの上から EventValue / EffectValue 枚（値1）を手札に加える。
        // EventValue2 / EffectValue2（値2）＝1 でオーバーリミット指定：デッキが0枚でも敗北せず「オーバーリミット！」告知のみ行う（0=通常）。
        Draw = 3,
        // 発動時には引かず、そのプレイヤーの次のターン開始時（次のドローフェーズ）に EventValue / EffectValue 枚ドローする。
        // 通常ドローに上乗せして引く。複数回発動すると枚数は累積する。EventValue2 / EffectValue2 は不使用。
        DrawNextTurnStart = 21,
        // 発動側が即座に EventValue / EffectValue 枚ドローし、そのプレイヤーの次のドローフェーズを1回スキップする。
        // EventValue / EffectValue = ドロー枚数（EventValue2 / EffectValue2 は不使用）。
        DrawSkipNext = 20,
        Evolve = 8,
        // 発動した（アクティブな）プレイヤーが、相手にターンを渡さずもう一度自分のターンを行う。
        // EventValue は不使用（0）。1ターン中に複数回発動しても追加ターンは1回（フラグ管理）。
        ExtraTurn = 18,
        // 11 は欠番（旧 GainVictoryPoints）。勝利点の付与は CardData.VictoryPointBonus に統合した。
        // 発動した側の墓地にある緑属性カードの枚数だけ、自分の勝利点（勝利点の勝利条件への加点）に加算する。
        // EventValue / EffectValue は不使用（0）。墓地に緑カードがなければ加点 0。
        GainVPPerGreenGrave = 12,
        // 発動側が自フィールドのキャラを EventValue / EffectValue 体（値1。0=場の味方全員）選び、
        // EventValue2 / EffectValue2（値2）で指定したキーワード能力を永続付与する。
        // 値2: 1=守護 / 2=速攻 / 3=飛行 / 4=防人 / 5=強襲 / 6=デッキ攻撃×（それ以外は空振り）。AtkBoost と同じ対象選択。
        GrantKeyword = 26,
        // 太郎勝利（特殊勝利）。EffectParam（値1のテキスト）にカンマ区切りで書いた完全ID（例 "C1001,E2003"）の
        // カードが、効果発動時に発動側プレイヤーの手札にすべて（重複指定は枚数分）そろっていれば、その場で勝利する。
        // そろっていなければ空振り。EventValue / EffectValue・EventValue2 / EffectValue2 は不使用（値はテキストの EffectParam を使う）。
        HandCollectionWin = 29,
        // 発動側の自フィールドのキャラ全員の HP を EventValue / EffectValue 分回復する（最大HPでクランプ）。
        // EventValue / EffectValue = 0 のときは最大HPまで全回復する。自キャラがいなければ空振り。
        HealAllAllies = 19,
        // AtkBoost と同じ対象選択で、選んだ味方の HP（現在HP・最大HP両方）を
        // EventValue2 / EffectValue2（値2）分、永続的に上げる（発動時に一度だけ加算）。
        // 旧 DefBoost（防御という独立ステータスは無いため HP ブーストへ統合。整数値 2 を継続使用）。
        HpBoost = 2,
        // 発動した側が次にプレイするカード1枚のコストを0にする（使うまで持続。EventValue は不使用）。
        NextCardCostFree = 15,
        Recover = 6,
        // 発動側の自フィールドに、EventValue / EffectValue が示すキャラ（数字部分→"C###"）を
        // EventValue2 / EffectValue2 体（未設定=0 は1体）新規生成して配置する（手札・デッキは消費しない）。
        // 召喚キャラの OnEnter も発動する。
        SummonChar = 14,
        // 発動側のデッキから、発動カード自身の特徴（CardData.Keyword）を持つキャラを1枚選んで自フィールドに出す。
        // 対象はプレイヤーが選択（CPU は最高コスト・オンラインはデッキ内インデックスで同期）。デッキから消費し、
        // 配置時に OnEnter も発動する。特徴一致キャラがデッキにいない／フィールド満杯なら空振り。EventValue / EventValue2 は不使用。
        SummonFromDeckByKeyword = 24,
        // 発動側自身の墓地から、キャラカードを EventValue / EffectValue 体（値1。未設定=0 は1体）選んで自フィールドに出す。
        // 墓地から消費し、配置時に OnEnter も発動する。候補が値1以下なら全部・墓地にキャラがいない／フィールド満杯なら空振り。
        // 対象はプレイヤーが選択（CPU は高コスト順・オンラインは墓地内インデックスで同期）。EventValue2 / EffectValue2 は不使用。
        SummonFromGrave = 34,
        Switch = 7,
    }
}

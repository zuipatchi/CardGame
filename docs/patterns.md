# 実装パターン集

よく触る実装パターンのレシピ。新機能を追加するときはここを起点にする。

---

## 1. 新しいカード種別を追加する

### 手順

**① `CardData` を継承したデータクラスを作る**

```csharp
// Assets/Scripts/Main/Card/XxxCardData.cs
[Serializable]
public sealed class XxxCardData : CardData
{
    [SerializeField] private int _someValue;
    public int SomeValue => _someValue;

    public XxxCardData() { }
    public XxxCardData(string id, string name, int cost, int someValue)
        : base(id, name, cost)
    {
        _someValue = someValue;
    }
}
```

**② ScriptableObject を作る**

```csharp
// Assets/Scripts/Main/Card/XxxCardSO.cs
[CreateAssetMenu(fileName = "XxxCards", menuName = "Card/Xxx Cards")]
public sealed class XxxCardSO : ScriptableObject
{
    [SerializeField] private List<XxxCardData> _cards;
    public IReadOnlyList<XxxCardData> Cards => _cards;
}
```

**③ `CardDatabase` に追加する**

[CardDatabase.cs](../Assets/Scripts/Main/Card/CardDatabase.cs) は属性別 SO の配列 `_characterCardSets` / `_eventCardSets` を保持し、`Build()` と `AllCards` で全 SO を走査して集約する。新しいカード種別を増やす場合は同じ要領で配列フィールドを追加し、`Register` / `AddAll` を呼ぶ。

**④ SO アセットを作って CardDatabase にアサイン**

`CharacterCardSO` / `EventCardSO` は**属性ごとに分割**して管理する（`Assets/Data/{属性}/CharacterCards_{属性}.asset` 等）。Create → Card → Xxx Cards でアセットを作り、SO の Inspector で `Attribute`（その SO が扱う属性）を設定して、`CardDatabase` の対応する配列にアサインする。

**⑤ カードデータを実際に入力**

該当属性の SO の Inspector で `_cards` リストにカードを追加して入力する。各カードの `Attribute` は SO が一括設定するため**インスペクタでは読み取り専用（グレー表示）**で、SO の `Attribute` に自動追従する。

ID は SO の `OnValidate` で**自動採番**される（`CardIdAutoAssigner`）。採番規則は **`C{(属性番号)×1000 + リスト連番}`**（属性番号 = `(int)CardAttribute + 1`。赤=`C1001`/青=`C2001`/…、イベントは `E1001`…）。1属性あたり最大999枚・最大9属性で、属性別 SO 間でも一意・"C{番号}" 形式を保つ（SummonChar 互換）。要素の追加・削除・並び替えのたびに振り直されるため手入力不要。

> 既存の単一 SO を属性別へ分割する移行ツール：メニュー **`Card → 属性別SOに分割`**（[CardSoAttributeSplitter.cs](../Assets/Scripts/Editor/CardSoAttributeSplitter.cs)）。属性で振り分けて各フォルダに SO を生成し CardDatabase へアサインする。複数属性が混在する SO は属性を上書きしない（レガシー SO の破壊防止）。

**注意**: 保存済みデッキ・`CpuDeck.asset` はカード ID で参照しているため、マスターリストの並び替え・途中挿入で ID が変わると参照先のカードが変わる。並び替えた後はデッキ内容を確認すること。

---

## 2. 新しいイベント効果（EventType）を追加する

### 手順

**① `EventType` enum に値を追加する**

[EffectType.cs](../Assets/Scripts/Main/Card/EffectType.cs)（ファイル名は EffectType.cs のまま、enum 名は `EventType`）:
```csharp
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
    YourNewEffect,  // ← 追加
}
```

**② `ApplyEventEffectAsync` にケースを追加する**

[MainPresenter.Phases.Resolution.cs](../Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs) の `ApplyEventEffectAsync` メソッドに `case CardEventType.YourNewEffect:` を追加して処理を実装する（ファイル先頭に `using CardEventType = Main.Card.EventType;` エイリアスあり）。

- 同期処理のみなら `break` で完結
- 非同期処理が必要（Draw 相当）なら `await SomeHelperAsync(...)` を呼んで `break`

**Resolve 時に演出を追加する場合**

効果種別ごとの事前演出 ＋ `ApplyEventEffectAsync` は `ResolveEventCardEffectAsync`（[MainPresenter.Phases.Resolution.cs](../Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs)）に集約されており、`OnPlay` のプレイ時（`ResolveSingleCardAsync`）と `OnTurnStart` 永続イベントの毎ターン発動（`PlayGraveyardEventEffectAsync`）の両方から呼ばれる。演出のタイミングによって2パターンある（`ResolveEventCardEffectAsync` の `if/else if` ブランチ内に記述）：

- **効果適用前に演出**（Draw / BanishChar / GainVPPerGreenGrave）: `await ApplyEventEffectAsync(...)` の直前に `else if` で演出を先に呼ぶ。Draw はドロー前の予告、BanishChar は対象キャラ上に「BANISH!」ラベル+パーティクル、GainVPPerGreenGrave は MedalIcon フロートを表示してから効果適用。
- **勝利点付帯値（VictoryPointBonus）**: 効果（EventType）とは独立した加点。`ResolveEventCardEffectAsync` / `ResolveCharacterTriggeredEffectAsync` の効果解決後に `ApplyVictoryPointBonusAsync(bonus, isLocal, card, ct)` が MedalIcon 演出 ＋ `AddVictoryPoints` をまとめて実行する（bonus が 0 なら何もしない）。「効果＋勝利点」も「勝利点を得るだけ」（`EventType=None` ＋ 付帯値）もこれで賄う。
- **`ApplyEventEffectAsync` 内でアニメーション**（BanishChar）: 効果適用自体にアニメが必要な場合は `ApplyEventEffectAsync` のケース内で `await FlyCardToDestAsync(...)` 等を呼ぶ。`worldBound` はフィールドから除去前に記録すること。

> ここに演出を追加すると `OnPlay`・`OnTurnStart` 両方に自動で反映される（共通メソッドのため）。

パーティクルが必要なら `MainPresenter.cs` に `[SerializeField] private GameObject _xxxEffectPrefab;` を追加し、`PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct)` を呼ぶ。回転が必要な場合は `PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct, Quaternion.Euler(x, y, z))` のように4引数版を使う。カード以外の位置（フィールド中央など）で再生したい・拡大したい場合は `PlayParticleAtUiPositionAsync(panelRef, uiPos, prefab, ct, scale: 2f)` を直接呼ぶ（BounceAll はこれでフィールド中央に全体エフェクトを1度だけ再生している）。
フローティングラベルのみの場合は `PlayFloatingLabelAsync(text, cssClass, anchor, ct)` を呼ぶ。`anchor` には演出の基準となる `VisualElement`（カードやスロット等）を渡す。CSS クラス名で見た目をカスタマイズする。
ラベル + パーティクルの組み合わせは `PlayXxxEffectAsync` から `PlayFloatingLabelAsync` と `PlayParticleAtCardAsync` を `UniTask.WhenAll` で並列実行するパターンを使う。

> **演出後の共通ディレイ**: `PlayParticleAtUiPositionAsync`（パーティクル）・`PlayFloatingLabelAsync`・`PlayFloatingMedalAsync` はいずれも末尾で共通の余韻ディレイ `EffectTrailingDelaySeconds`（`MainPresenter.cs`、0.25秒）を待つ。これにより「演出終了 → 0.25秒 → 次の処理」のテンポが全カード効果で統一されるため、呼び出し側で個別に `UniTask.Delay` を挟む必要はない。パーティクルの待ち時間は Prefab の実再生時間（`max(duration, lifetime) / simulationSpeed`）に補正済み。詳細は [docs/effects.md](effects.md) のセクション7を参照。

**イベント効果解決中（メインフェーズ内の即時解決）にプレイヤー入力が必要な効果（Switch / Evolve 相当）の場合**

`MainPresenter.cs` に `private readonly StagedInput _xxxInput = new StagedInput();` と必要なら状態変数（例: `private int _evolveMinCost;`）を追加し、`MainPresenter.Input.cs` の `HandlePlayerCardDrop` / `TryTakeStagedInput` / `OnPassClicked` / `OnBackClicked` / `CanPlayerDragCard` / `IsCardPlayable` の各メソッドに `_xxxInput._tcs != null` のチェックを追加する。`_switchInput` / `_evolveInput` の実装が参考例。`WaitForPlayerXxxInputAsync` でボタン表示・TCS 完了待ちを行い、`ApplyXxxEffectAsync` から `await WaitForPlayerXxxInputAsync(ct)` で結果を受け取る。ドロップ時にすでに `PlaceCard` が呼ばれるため、TCS 完了後に再度 `PlaceCard` しないこと。

**フィールドのキャラをクリックで対象選択する効果（Switch / Evolve の自軍選択 / DamageEnemy の敵選択）の場合**

`MainPresenter.cs` に選択用 TCS（単数なら `UniTaskCompletionSource<CardView>`、複数選択なら `UniTaskCompletionSource<List<CardView>>` ＋選択リスト・必要数フィールド）を追加し、対象フィールドの `OnCardClicked`（`_playerFieldView` = 自軍 / `_opponentFieldView` = 敵）の先頭で「選択中なら専用ハンドラを呼んで `return`」するよう分岐する。待機ヘルパーでは対象キャラに `selectable-char` クラスを付与（金枠＋拡大ハイライト。スタイルは [Main.uss](../Assets/Scripts/Main/View/Main.uss) の `.selectable-char`）して `ShowToast(...)` で案内し、`finally` でクラスを除去する。

複数選択（DamageEnemy）の場合は、クリックハンドラで未選択キャラを選択リストへ追加して `selected-char`（赤枠）を付け、残り体数をトーストで更新し、必要数に達したら `TrySetResult(list)` で確定する。対象数が候補数以上なら選択不要で全員を対象にする。オンラインでは選んだ対象を**フィールドのインデックス配列**で相手へ送る（`NetworkGameService` の `SendDamageTargets` / `WaitForOpponentDamageTargetsAsync` と専用メッセージキー・ペイロード。同名カードが複数いても曖昧にならない）。選択が不要なケース（全員が対象）では送受信しない。`ResolveEnemyCharTargetsAsync` / `HandleEnemyCharSelectionClick` が参考例。

> **オンライン同期の注意**: 受信側のハンドラ登録がアニメーション後だと、相手の送信が先に届いたとき NGO が未登録メッセージを破棄して**永久待機になる**。`NGS_DamageTarget` は OnEnter / OnAttack / OnDestroy / イベント / Bounce と多数の箇所から呼ばれるため、対戦開始時（`PrepareDecksAsync`）にハンドラを永続登録しキューでバッファしている。新たに対象同期メッセージを追加するときは、**永続登録＋キュー** か **アニメーション開始前の事前登録** のどちらかにすること（[networking.md](networking.md) セクション11）。

---

## 2-B. キャラカードに登場時効果を追加する

キャラカードは `EffectTrigger`（[CharacterEffectTrigger.cs](../Assets/Scripts/Main/Card/CharacterEffectTrigger.cs)）に `OnEnter` を設定すると、通常配置でフィールドに出した瞬間に効果が発動する。効果種別は `EventType`（イベントと共通）を流用する。

**① カードデータに効果を設定する**

対象キャラが属する属性別 `CharacterCardSO`（`Assets/Data/{属性}/CharacterCards_{属性}.asset`）のインスペクターで、`Effect Trigger = OnEnter`、`Effect Type`（例: `Draw` / `BanishChar`）、`Effect Value`、`Description`（詳細モーダル表示用の説明テキスト）を設定する。任意で `Flavor Text`（世界観テキスト。効果には影響せず詳細モーダル最下部に表示）も設定できる。

**② 効果種別の解決処理を追加する**

[MainPresenter.Phases.Resolution.cs](../Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs) の `ResolveCharacterTriggeredEffectAsync`（OnEnter / OnAttack / OnDestroy / OnTurnStart 共通）の `switch` に `case CardEventType.Xxx:` を追加する。既存のイベント効果解決ヘルパー（`PlayDrawEffectAsync` / `ApplyDrawEffectAsync` / `PlayBanishCharEffectAsync` / `ApplyDamageAllEnemiesAsync` / `AddVictoryPoints` 等）を流用できる。現状は `Draw` / `BanishChar` / `DamageAllEnemies` / `DamageEnemy` / `SummonChar` / `GainVPPerGreenGrave` / `HealAllAllies` / `NextCardCostFree` / `Bounce` / `BounceAll` / `ExtraTurn` を実装済み（固定値の勝利点は `EventType` ではなく `VictoryPointBonus` 付帯値で付与する）。墓地など盤面状態から加点値を動的に算出する効果（`GainVPPerGreenGrave` は `CountGreenInGraveyard` → `GraveyardView.CountByAttribute`）は、`AddVictoryPoints` 等の既存ヘルパーに算出値を渡す形にする（墓地は同期済みのため決定的）。敵キャラを N 体選ぶ効果（`DamageEnemy` / `Bounce`）は対象選択を `ResolveEnemyCharTargetsAsync`（プレイヤー選択／CPU 自動／オンラインはインデックス同期。トースト文言は引数）で共用する。2つの数値が必要な効果（SummonChar の「ID」と「体数」など）は `EventValue2` / `EffectValue2` を使う（未使用の効果は 0）。コスト支払いに作用する効果（`NextCardCostFree`）はプレイヤーごとの永続フラグを持ち、`PayHandCostAsync` 側でコスト0化・消費する（[event.md](event.md)「効果ごとの注意点」参照）。ターン進行に作用する効果（`ExtraTurn`）はアクティブプレイヤーのフラグ（`_extraTurnPending`）を立て、`RunTurnAsync` 末尾で `GameModel.RepeatTurn()` を呼んで同じプレイヤーがもう一度ターンを行う（オンラインは Pass 時の相手ドロー待ち登録をスキップして lockstep を維持。[event.md](event.md)「効果ごとの注意点」参照）。

**発動箇所**: 通常配置パス（ローカル `ExecuteLocalMainResolveAsync` の `PlaceChar` ／ 相手 `ExecuteOpponentCardPlayAsync` のキャラ配置後）で `ResolveCharacterEnterEffectAsync` を呼んでいる。CPU・オンライン相手も同経路でカバーされ、効果はカードデータから導出されるため追加のネットワーク同期は不要。Switch / Evolve での配置は対象外。

**他のトリガー**: `OnAttack`（攻撃時）・`OnDestroy`（破壊時）・`OnTurnStart`（自分のターン開始時）も `ResolveCharacterTriggeredEffectAsync` を共用する。`OnDestroy` は戦闘での撃破（`ExecuteAttackAsync`）・`DamageEnemy` / `DamageAllEnemies` での撃破・`BanishChar` での除去の各破壊経路から `FireOnDestroyEffectAsync(destroyedCard, ownerIsLocal, ct)` を呼んで発動する（破壊が墓地への移動まで完了した後に解決。同時破壊は1体ずつ順番に発動）。破壊されたキャラの所有者は発動側の相手なので `ownerIsLocal = !isLocal` を渡す。新しい破壊経路を追加したときは同じ呼び出しを差し込む。`OnTurnStart` は `ResolveTurnStartEffectsAsync`（`RunTurnAsync` のターン開始演出後・ドロー前）から、アクティブプレイヤーの場のキャラを並び順に発動する。`OnAttacked`（被攻撃時）・`OnKill`（撃破時）は戦闘 `ExecuteAttackAsync` からのみ `FireOnAttackedEffectAsync(defender, !isLocal, ct)`／`FireOnKillEffectAsync(attacker, isLocal, ct)` を呼んで発動する。`OnAttacked` は対象が攻撃の対象になった直後・破壊判定の前に発動し（回復・反撃で生死が変わり得るため判定はこの後に再計算）、**攻撃側 ATK 0 の盾ブロック（ダメージ0）でも発動する**（`damage == 0` の早期 return 前にも `FireOnAttackedEffectAsync` を呼ぶ）。`OnKill` は対象の OnDestroy 解決後に攻撃側が場に残っていれば発動する。どちらもキャラの攻撃（戦闘）のみで、`DamageEnemy` 等の効果ダメージ・ハート攻撃では発動しない。

**永続イベント（EventCardTrigger.OnTurnStart）**: イベントカードは `Event Trigger`（[EventCardTrigger.cs](../Assets/Scripts/Main/Card/EventCardTrigger.cs)）に `OnTurnStart` を設定すると、プレイ時は即時解決せず登録簿（`_playerTurnStartEvents` / `_opponentTurnStartEvents`）に登録され、自分のターン開始時に毎ターン墓地からせり出して発動し続ける（`ResolveTurnStartEffectsAsync` → `PlayGraveyardEventEffectAsync` → `ResolveEventCardEffectAsync`）。コストとして捨てたカードは `ResolveSingleCardAsync` を通らないため登録されない（墓地は走査しない）。詳細は [event.md](event.md)「EventTrigger」。

---

## 2-C. コスト支払い時に作用する受動効果（CostBoost 相当）を追加する

`Draw` 等の「プレイ時に解決される効果」とは異なり、`CostBoost` は**手札からコストとして支払うときだけ作用する受動プロパティ**。`ApplyEventEffectAsync` / `ResolveCharacterEnterEffectAsync` の `switch` には**追加しない**（通常プレイ時は無効果）。

実装は「カードが何コスト分として数えられるか」を表す `CardData.CostPaymentValue(CardAttribute payingForAttribute)`（virtual、通常 1）で表現する。引数 `payingForAttribute` は**プレイするカードの属性**で、属性連動の受動効果（CostBoost）に使う。

- **判定箇所**: [CardData.cs](../Assets/Scripts/Main/Card/CardData.cs) の `public virtual int CostPaymentValue(CardAttribute payingForAttribute) => 1;` を、[CharacterCardData.cs](../Assets/Scripts/Main/Card/CharacterCardData.cs) / [EventCardData.cs](../Assets/Scripts/Main/Card/EventCardData.cs) で `override` してカードの種別/値から導出する（CostBoost なら、自属性が `payingForAttribute` と一致するとき `Max(1, 値)`、それ以外は 1。白も一般属性扱いで、白 CostBoost は白のコストのみ倍化）。
- **コスト計算**: [MainPresenter.Input.CostSelection.cs](../Assets/Scripts/Main/MainPresenter.Input.CostSelection.cs) の `CostCapacityExcluding(excluded, payingForAttribute)` / `SelectedCostValue`（`_playedCardAttribute` を使用）が `CostPaymentValue` を合算する。コスト判定は「枚数」ではなく「合計コスト値」で行うため、新しい受動効果を足すときもこの2メソッドが算出経路になる。配置可否判定（[MainPresenter.Input.cs](../Assets/Scripts/Main/MainPresenter.Input.cs)）もプレイするカードの属性を渡す。
- **CPU**: [MainPresenter.Animations.CostFly.cs](../Assets/Scripts/Main/MainPresenter.Animations.CostFly.cs) の `ChooseCpuCostCards` がプレイするカードの属性で合計コスト値ベースに自動選択する。
- **オンライン**: 払ったカードIDの送受信のみで整合（支払い側がローカルで確定し、受信側は再計算しない）。追加同期は不要。
- 説明は Description に手書きする（専用の表示UIは追加しない方針）。

---

## 3. 新しいターンフェーズを追加する

### 手順

**① `TurnPhase` enum に値を追加する**

[TurnPhase.cs](../Assets/Scripts/Main/Game/TurnPhase.cs):
```csharp
public enum TurnPhase
{
    Draw,
    Main,
    YourNewPhase,  // ← 追加
}
```

**② `GameModel` にフェーズ開始メソッドを追加する**

[GameModel.cs](../Assets/Scripts/Main/Game/GameModel.cs):
```csharp
public void BeginYourNewPhase() { Phase = TurnPhase.YourNewPhase; }
```

**③ `MainPresenter.Phases.cs` にフェーズ処理を追加する**

[MainPresenter.Phases.cs](../Assets/Scripts/Main/MainPresenter.Phases.cs) の `RunTurnAsync` 内の適切な位置に呼び出しを追加し、新しいファイル（例: `MainPresenter.Phases.YourNewPhase.cs`）に `partial class MainPresenter` として `RunYourNewPhaseAsync` を実装する:

```csharp
_gameModel.BeginYourNewPhase();
await RunYourNewPhaseAsync(ct);
if (_isGameOver) return;
```

---

## 4. 新しい Presenter を追加する（シーン単位）

### 手順

**① Presenter クラスを作る**

```csharp
// IAsyncStartable を実装してエントリポイントにする場合
public sealed class YourPresenter : IAsyncStartable, IDisposable
{
    public async UniTask StartAsync(CancellationToken ct)
    {
        try { /* 初期化・購読 */ }
        catch (OperationCanceledException) { }
    }

    public void Dispose() { /* 購読解除など */ }
}
```

MonoBehaviour として配置する場合は `RegisterComponentInHierarchy<YourPresenter>()` を使う。

**② LifetimeScope に登録する**

対象シーンの `LifetimeScope`（例: [MainLifetimeScope.cs](../Assets/Scripts/Main/Injector/MainLifetimeScope.cs)）の `Configure` に追加:

```csharp
// 純粋 C# クラス（エントリポイント）
builder.RegisterEntryPoint<YourPresenter>().AsSelf();

// MonoBehaviour（シーン内に配置済み）
builder.RegisterComponentInHierarchy<YourPresenter>().AsSelf().AsImplementedInterfaces();

// 依存を注入するだけで自動起動不要な場合
builder.Register<YourService>(Lifetime.Scoped);
```

---

## 5. CPU の判断ロジックを変更・追加する

[CpuAgent.cs](../Assets/Scripts/Main/Game/CpuAgent.cs) に静的メソッドを追加し、対応するフェーズファイル（`MainPresenter.Phases.*.cs`）の該当フェーズメソッドから呼ぶ。

`CpuAgent` はステートレスな静的クラス。手札（`IReadOnlyList<CardData>`）を受け取ってインデックスを返す設計。`-1` でパス・対象なしを表す。

```csharp
// CpuAgent.cs に追加
public static int ChooseXxxCardIndex(IReadOnlyList<CardData> hand)
{
    return FindFirst<XxxCardData>(hand);
}
```

---

## 6. async MonoBehaviour での destroyCancellationToken の扱い（Unity 6）

Unity 6 では `destroyCancellationToken` を **一度も参照しないまま MonoBehaviour が破棄される** と
`MissingReferenceException` が発生する（"DestroyCancellation token should be called atleast once before destroying the monobehaviour object"）。

### 対処パターン

async メソッド内で最初の `await` の後に `destroyCancellationToken` を参照する場合、
`await` 中に MonoBehaviour が破棄されると例外が出る。以下の2点を必ず守る:

**① `await` の直後に `this == null` ガードを入れる**

```csharp
private async UniTaskVoid BuildAsync()
{
    try
    {
        await _someTask;

        if (this == null) { return; }   // ← await 後は必ずガード

        CancellationToken ct = destroyCancellationToken;  // ← ガード後に一度だけキャプチャ
        // 以降は ct を使う
    }
    catch (OperationCanceledException) { }
}
```

**② キャプチャした `ct` を以降のすべての箇所で使う**

メソッド内で `destroyCancellationToken` を直接参照するのは最初のキャプチャ時のみ。
`CancellationTokenSource.CreateLinkedTokenSource` や他のメソッドへの引数も `ct` を渡す。

---

## 7. DOTween + UI Toolkit でのスタイル値ゲッター（フリーズ対策）

UI Toolkit のスタイルプロパティを DOTween ゲッターに直接渡すと、シーケンス開始フレームでの
値読み取りが不定になり `OnComplete` が発火しないケースがある。

### NG パターン

```csharp
DOTween.To(() => _overlay.style.opacity.value, v => _overlay.style.opacity = v, 1f, 0.25f)
```

スタイルプロパティの `.value` を毎フレーム読み取るため、前フレームの状態に依存して初期値が不正になることがある。

### OK パターン（ローカル float 変数）

```csharp
float opacity = 0f;
DOTween.To(
    () => opacity,
    v => { opacity = v; _overlay.style.opacity = v; },
    1f, 0.25f
)
```

ローカル float 変数を「仲介」として使うことで初期値が確定し、`OnComplete` が確実に発火する。

`PlayAnnouncementAsync` はこのパターンで実装済み。
同様の Sequence を新たに書く場合も必ずこの形式を使うこと。

---

## 共通ルール（抜粋）

- `var` は使わない。型を明示する
- フィールドは `_camelCase`、型・メソッドは `PascalCase`
- `Find()` / static 状態は使わない。DI で解決する
- UI は UXML + USS で構築。uGUI 禁止
- アセットロードは Addressables。`Resources.Load` 禁止
- USS では `gap` 禁止 → 子要素の `margin` で代替

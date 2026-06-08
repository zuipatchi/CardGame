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

[CardDatabase.cs](../Assets/Scripts/Main/Card/CardDatabase.cs) の `_characterCards` / `_skillCards` / `_eventCards` と同じ要領でフィールドを追加し、`Build()` と `AllCards` で `Register` / `AddAll` を呼ぶ。

**④ SO アセットを作って CardDatabase にアサイン**

Unity Editor → Project ウィンドウ → Create → Card → Xxx Cards でアセットを作成し、`CardDatabase` SO の Inspector にアサインする。

**⑤ カードデータを実際に入力**

SO の Inspector に `_cards` リストを追加してカードを入力。ID 命名規則: `X001`, `X002`, …（X = 種別頭文字）

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
    Negate,      // 廃止済み（EffectType は互換のため残存）
    BanishChar,
    Recover,
    Switch,
    CharDamage,
    Evolve,
    Poison,
    DeckMill,
    BattleEndMill,
    YourNewEffect,  // ← 追加
}
```

**② `ApplyEventEffectAsync` にケースを追加する**

[MainPresenter.Phases.Resolution.cs](../Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs) の `ApplyEventEffectAsync` メソッドに `case CardEventType.YourNewEffect:` を追加して処理を実装する（ファイル先頭に `using CardEventType = Main.Card.EventType;` エイリアスあり）。

- 同期処理のみ（AtkBoost / DefBoost 相当）なら `break` で完結
- 非同期処理が必要（Draw 相当）なら `await SomeHelperAsync(...)` を呼んで `break`
- ドローなどデッキを減らす処理の後は `CheckGameOver(); if (_isGameOver) break;` を入れること

**Resolve 時に演出を追加する場合**

演出のタイミングによって3パターンある（`ResolveSingleCardAsync` の `if/else if` ブランチ内に記述）：

- **効果適用後に演出**（AtkBoost / DefBoost）: `await ApplyEventEffectAsync(...)` の直後に `else if` で効果種別を判定して演出メソッドを呼ぶ。`PlayAtkBoostEffectAsync` が参考実装。
- **効果適用前に演出**（Draw / BanishChar）: `await ApplyEventEffectAsync(...)` の直前に `else if` で演出を先に呼ぶ。Draw はドロー前の予告、BanishChar は対象キャラスロット上に「BANISH!」ラベル+パーティクルを表示してから破壊する。
- **`ApplyEventEffectAsync` 内でアニメーション**（BanishChar）: 効果適用自体にアニメが必要な場合は `ApplyEventEffectAsync` のケース内で `await FlyCardToDestAsync(...)` 等を呼ぶ。`worldBound` はスロット除去前に記録すること。

パーティクルが必要なら `MainPresenter.cs` に `[SerializeField] private GameObject _xxxEffectPrefab;` を追加し、`PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct)` を呼ぶ。回転が必要な場合は `PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct, Quaternion.Euler(x, y, z))` のように4引数版を使う。
フローティングラベルのみの場合は `PlayFloatingLabelAsync(text, cssClass, anchor, ct)` を呼ぶ。`anchor` には演出の基準となる `VisualElement`（カードやスロット等）を渡す。CSS クラス名で見た目をカスタマイズする。
ラベル + パーティクルの組み合わせは `PlayXxxEffectAsync` から `PlayFloatingLabelAsync` と `PlayParticleAtCardAsync` を `UniTask.WhenAll` で並列実行するパターンを使う。

**解決フェーズ中にプレイヤー入力が必要な効果（Switch / Evolve 相当）の場合**

`MainPresenter.cs` に `private readonly StagedInput _xxxInput = new StagedInput();` と必要なら状態変数（例: `private int _evolveMinCost;`）を追加し、`MainPresenter.Input.cs` の `HandlePlayerCardDrop` / `TryTakeStagedInput` / `OnPassClicked` / `OnBackClicked` / `CanPlayerDragCard` / `IsCardPlayable` の各メソッドに `_xxxInput._tcs != null` のチェックを追加する。`_switchInput` / `_evolveInput` の実装が参考例。`WaitForPlayerXxxInputAsync` でボタン表示・TCS 完了待ちを行い、`ApplyXxxEffectAsync` から `await WaitForPlayerXxxInputAsync(ct)` で結果を受け取る。ドロップ時にすでに `PlaceCard` が呼ばれるため、TCS 完了後に再度 `PlaceCard` しないこと。

**解決後に速さを再評価する場合（Evolve 相当）**

解決フェーズでキャラが変わりうる効果を追加したら、`MainPresenter.Phases.cs` の `RunTurnAsync` 内で `RunPreBattle2PhaseAsync` の**後**に `DetermineFirstMover` を再度呼んで `_gameModel.SetInitialTurn` を更新することで、戦闘フェーズの先攻後攻に反映される。

---

## 3. グレイブトリガー（TriggerOnGrave）を持つカードを追加する

デッキからカードが墓地に送られたとき（コスト支払い・戦闘ダメージの両方）に自動で効果が発動するカード。

### 手順

**① `EventCardSO` の Inspector で設定する**

既存の `EventType` / `EventValue` / `Description` に加えて `_triggerOnGrave` チェックボックスを true にする。
効果内容は `EventType` で指定するため、新しい enum 値は不要。

**② コンストラクタ（コードで生成する場合）**

```csharp
// CharDamage を持つグレイブトリガーカードの例
EventCardData card = new EventCardData(
    "E_TRAP_01", "罠カード", cost: 2,
    EventType.CharDamage, eventValue: 3,
    description: "墓地に送られたとき相手キャラに3ダメージ",
    triggerOnGrave: true
);
```

**③ 発動フロー**

`PlayDeckDamageAsync` でカードが1枚ずつ墓地に到着した後に `TriggerOnGrave` をチェック。同一バッチ内で複数のトリガーカードが送られた場合は、送られた順に `FireGraveTriggerAsync` を呼ぶ。

```text
PlayDeckDamageAsync
  └─ 各カードの墓地到着後に TriggerOnGrave チェック
       └─ FireGraveTriggerAsync（Phases.Resolution.cs）
            ├─ PlayGraveTriggerDisplayAsync（発動プレイヤーの墓地近くにカードビジュアルを表示）
            └─ ApplyEventEffectAsync（既存の効果ロジックをそのまま使用）
```

**④ 対応済みの効果（既存 EventType をそのまま利用）**

CharDamage / Draw / AtkBoost / DefBoost / Recover / Switch / Evolve / Poison / DeckMill など、`ApplyEventEffectAsync` が対応している全効果が使用可能。

---

## 4. 毎ターン発動する永続効果を追加する（BattleEndMill 相当）

`Poison` はターン終了時にリセットされる一時効果だが、複数ターンにわたって持続する効果が必要な場合は以下のパターンを使う。

**① `MainPresenter.cs` に永続フィールドを追加する**

```csharp
// ResetBoosts() では絶対にリセットしない
private int _localXxxValue;
private int _opponentXxxValue;
```

**② `ApplyEventEffectAsync` でフィールドをセットする**

```csharp
case CardEventType.YourNewEffect:
    if (isLocal)
        _localXxxValue = data.EventValue;
    else
        _opponentXxxValue = data.EventValue;
    break;
```

③ 発動タイミングのフェーズ処理末尾でフィールドを参照する（例: 戦闘フェーズ終了後、`ResetBoosts()` の後）:

```csharp
if (!_isGameOver && (_localXxxValue > 0 || _opponentXxxValue > 0))
{
    await ApplyYourNewEffectEndAsync(ct);
}
```

④ ゲームオーバー判定を忘れない: 永続効果内でデッキを減らす操作をする場合は、処理後に `_opponentDeckView.Count == 0` / `_playerDeckView.Count == 0` を確認して `_isGameOver = true; OnGameEnd(...);` を呼ぶ。両プレイヤーの効果を連続処理する場合は各処理の間に `if (_isGameOver) return;` を挿入する。

---

## 5. 新しいターンフェーズを追加する

### 手順

**① `TurnPhase` enum に値を追加する**

[TurnPhase.cs](../Assets/Scripts/Main/Game/TurnPhase.cs):
```csharp
public enum TurnPhase
{
    CharacterSet,
    Draw,
    PreBattle2,
    Battle,
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

## 6. 新しい Presenter を追加する（シーン単位）

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

## 7. CPU の判断ロジックを変更・追加する

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

## 8. async MonoBehaviour での destroyCancellationToken の扱い（Unity 6）

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

## 9. DOTween + UI Toolkit でのスタイル値ゲッター（フリーズ対策）

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

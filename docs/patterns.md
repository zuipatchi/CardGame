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
    Negate,
    BanishChar,
    YourNewEffect,  // ← 追加
}
```

**② `ApplyEventEffectAsync` にケースを追加する**（効果が他カードに干渉しない場合）

[MainPresenter.Phases.cs](../Assets/Scripts/Main/MainPresenter.Phases.cs) の `ApplyEventEffectAsync` メソッドに `case CardEventType.YourNewEffect:` を追加して処理を実装する（ファイル先頭に `using CardEventType = Main.Card.EventType;` エイリアスあり）。

- 同期処理のみ（AtkBoost / DefBoost 相当）なら `break` で完結
- 非同期処理が必要（Draw 相当）なら `await SomeHelperAsync(...)` を呼んで `break`
- ドローなどデッキを減らす処理の後は `CheckGameOver(); if (_isGameOver) break;` を入れること

**他カードの処理に干渉する効果（Negate 相当）の場合**

`ApplyEventEffectAsync` を使わず、`RunResolutionPhaseAsync` 内の解決ループに直接フラグを追加する。`Negate` は `skipNextEffect` フラグで実装されており、`else if (eventData.EventType == CardEventType.Negate)` で `skipNextEffect = true` をセットし、次のカードの効果適用をスキップさせる。

**Resolve 時に演出を追加する場合**

演出のタイミングによって2パターンある：

- **効果適用後に演出**（AtkBoost / DefBoost）: `await ApplyEventEffectAsync(...)` の直後に `else if` で効果種別を判定して演出メソッドを呼ぶ。`PlayAtkBoostEffectAsync` が参考実装。
- **効果適用前に演出**（Draw）: `await ApplyEventEffectAsync(...)` の直前に `if (eventData.EventType == CardEventType.Draw)` で演出を先に呼ぶ。ドロー結果が見える前に予告演出を出したい場合に使う。

パーティクルが必要なら `MainPresenter.cs` に `[SerializeField] private GameObject _xxxEffectPrefab;` を追加し、`PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct)` を呼ぶ。
フローティングラベルのみの場合は `PlayAtkBoostLabelAsync` のパターンをコピーして `_dragLayer` に Label を追加する。

---

## 3. 新しいターンフェーズを追加する

### 手順

**① `TurnPhase` enum に値を追加する**

[TurnPhase.cs](../Assets/Scripts/Main/Game/TurnPhase.cs):
```csharp
public enum TurnPhase
{
    CharacterSet,
    Draw,
    PreBattle1,
    PreBattle2,
    Resolution,
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

[MainPresenter.Phases.cs](../Assets/Scripts/Main/MainPresenter.Phases.cs) の `RunTurnAsync` 内の適切な位置に以下を追加し、`RunYourNewPhaseAsync` を実装する:

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

[CpuAgent.cs](../Assets/Scripts/Main/Game/CpuAgent.cs) に静的メソッドを追加し、[MainPresenter.Phases.cs](../Assets/Scripts/Main/MainPresenter.Phases.cs) の対応フェーズメソッドから呼ぶ。

`CpuAgent` はステートレスな静的クラス。手札（`IReadOnlyList<CardData>`）を受け取ってインデックスを返す設計。`-1` でパス・対象なしを表す。

```csharp
// CpuAgent.cs に追加
public static int ChooseXxxCardIndex(IReadOnlyList<CardData> hand)
{
    return FindFirst<XxxCardData>(hand);
}
```

---

## 共通ルール（抜粋）

- `var` は使わない。型を明示する
- フィールドは `_camelCase`、型・メソッドは `PascalCase`
- `Find()` / static 状態は使わない。DI で解決する
- UI は UXML + USS で構築。uGUI 禁止
- アセットロードは Addressables。`Resources.Load` 禁止
- USS では `gap` 禁止 → 子要素の `margin` で代替

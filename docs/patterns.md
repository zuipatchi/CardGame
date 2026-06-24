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

[CardDatabase.cs](../Assets/Scripts/Main/Card/CardDatabase.cs) は属性 × 弾別 SO の配列 `_characterCardSets` / `_eventCardSets` を保持し、`Build()` と `AllCards` で全 SO を走査して集約する（全弾をマージ）。新しいカード種別を増やす場合は同じ要領で配列フィールドを追加し、`Register` / `AddAll` を呼ぶ。

**④ SO アセットを作って CardDatabase にアサイン**

`CharacterCardSO` / `EventCardSO` は**属性 × 弾（第N弾）ごとに分割**して管理する（`Assets/Data/Set{弾}/{属性}/CharacterCards_{属性}.asset` 等。弾1は無印・弾2以降は `_Set{N}` を付ける）。Create → Card → Xxx Cards でアセットを作り、SO の Inspector で `Attribute`（その SO が扱う属性）と `Set`（弾番号・既定1）を設定して、`CardDatabase` の対応する配列にアサインする。**カードエディタの「新規追加」で属性＋弾を指定すれば、該当弾の SO が無くても自動生成＋CardDatabase 登録まで行われる**（手動でアセットを作る必要はない）。

**⑤ カードデータを実際に入力**

該当属性の SO の Inspector で `_cards` リストにカードを追加して入力する。各カードの `Attribute` は SO が一括設定するため**インスペクタでは読み取り専用（グレー表示）**で、SO の `Attribute` に自動追従する。

ID は SO の `OnValidate` で**自動採番**される（`CardIdAutoAssigner`）。採番規則は **`C{(属性番号)×1000 + (弾-1)×100 + リスト連番}`**（属性番号 = 白1/青2/緑3/黄4/赤5/黒6/紫7。白第1弾=`C1001`/白第2弾=`C1101`/青第1弾=`C2001`/…、イベントは `E1001`…）。**弾1はオフセット0なので既存 ID は不変**（弾未設定=0 も弾1扱い）。1属性1弾あたり最大99枚・最大9弾で、属性×弾の SO 間でも一意・"C{番号}" 形式を保つ（SummonChar 互換）。要素の追加・削除・並び替えのたびに振り直されるため手入力不要（弾ごとに ID ブロックが独立するため、ある弾への追加が他の弾の ID をずらすことはない）。

> 既存の単一 SO を属性別へ分割する移行ツール：メニュー **`Card → 属性別SOに分割`**（[CardSoAttributeSplitter.cs](../Assets/Scripts/Editor/CardSoAttributeSplitter.cs)）。属性で振り分けて各フォルダに SO を生成し CardDatabase へアサインする（現在は属性別への一度きりの移行用。弾分割は上記カードエディタの自動生成で行う）。複数属性が混在する SO は属性を上書きしない（レガシー SO の破壊防止）。

> 調整中・未完成のカードはメニュー **`Card → カードエディタ`**（[CardEditorWindow.cs](../Assets/Scripts/Editor/CardEditorWindow.cs)）の「ゲームで使用」トグルを OFF にすると、`CardData._excludeFromGame` が立ち `CardDatabase` の集計（プール・対戦・ID 解決）から完全に除外される（一覧ではグレーアウト＋`(未使用)`表示）。SO からカードを削除せずに一時的に隠せる。

> **対戦専用トークン**: 「ゲームで使用」ON のまま「対戦専用（トークン）」トグルを ON にすると `CardData._excludeFromDeckBuilder` が立つ。対戦の ID 参照テーブル（`CardDatabase._dict`）には登録されるため SummonChar 等の効果から ID 召喚・参照できるが、デッキ構築のプール（`AllCards` → `InDeckPool`）には出ないためプレイヤーがデッキに入れられない（一覧では `(トークン)` 表示）。`InUse`＝対戦参照対象（トークン含む）/ `InDeckPool`＝`InUse && !_excludeFromDeckBuilder`＝デッキ構築プール対象、で2段階に分離している。

**注意**: 保存済みデッキ・`CpuDeck.asset`・`CpuRoster.asset`（各相手の `CardIds`）はカード ID で参照しているため、マスターリストの並び替え・途中挿入で ID が変わると参照先のカードが変わる。並び替えた後はデッキ内容を確認すること。

---

## 2. 新しいカード効果（EventType）を追加する

効果1種＝**`EffectHandler` 派生クラス1つ**にまとまっている（[Assets/Scripts/Main/Card/Effects/](../Assets/Scripts/Main/Card/Effects/)）。ハンドラは「効果の実行（演出＋盤面適用）」と「エディタ用メタデータ（効果テキスト・値ラベル）」の両方を持つ。[EffectCatalog](../Assets/Scripts/Main/Card/Effects/EffectCatalog.cs) が Main アセンブリを走査してハンドラを自動登録するため、**既存ファイルの `switch` を編集する必要はない**（起動時に「全 EventType にちょうど1つのハンドラがあるか」を検証し、欠落・重複があれば例外を投げる）。イベントカード・キャラカードの両方が同じハンドラを使う。

### 手順

**① `EventType` enum に値を追加する**

[EffectType.cs](../Assets/Scripts/Main/Card/EffectType.cs)（ファイル名は EffectType.cs のまま、enum 名は `EventType`）に**明示的な整数値**で1つ追加する（既存値は変えない・宣言はアルファベット順）。

```csharp
YourNewEffect = 26,  // ← 追加（次の空き整数値）
```

**② ハンドラクラスを1つ作る**

`Effects/Handlers/` 配下にファイルを追加し、`EffectHandler` を継承する（テーマの近い既存ファイルに足してもよい）。`[Preserve]` を必ず付ける（IL2CPP ビルドでの型ストリップ対策）。

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Main.Card.Effects.Handlers
{
    [Preserve]
    public sealed class YourNewEffectHandler : EffectHandler
    {
        public override EventType Type => EventType.YourNewEffect;

        // イベント／キャラのどちらで使えるか（既定は両方 true）。
        // public override bool ValidOnCharacter => false;  // イベント専用にするなら

        // エディタの値1/値2 ラベルとヒント。
        public override EffectValueInfo Values => new EffectValueInfo(
            true, "値1（…）", false, "値2（未使用）", "値1=…");

        // エディタの効果テキスト自動生成（効果なしは空文字）。
        public override string BuildBody(EffectTextContext ctx) => $"…{ctx.Value1}…";

        // 効果の実行：演出（Play*）＋ 盤面適用（Apply*）。盤面操作は MainPresenter の internal メソッドを呼ぶ。
        public override async UniTask ApplyAsync(MainPresenter p, EffectInvocation inv, CancellationToken ct)
        {
            await p.PlaySomeEffectAsync(inv.SourceCard, ct);   // 演出
            await p.ApplySomeEffectAsync(inv.Value1, inv.IsLocal, ct);  // 盤面適用
        }
    }
}
```

`EffectInvocation` は効果の入力を正規化したもの（`IsLocal`／`SourceCard`（演出アンカー）／`Value1`・`Value2`（=EventValue/EventValue2 または EffectValue/EffectValue2）／`Keyword`／`Param`）。キャラ・イベントどちらの経路でも同じ形で渡る。

> **値1を文字列で入力する効果（カンマ区切りIDなど）**: 値1/値2 は int のため、ID列のような文字列は持てない。文字列が必要な効果は `CardData.EffectParam`（全カード共通の文字列フィールド）に保存し、`EffectInvocation.Param` / `EffectTextContext.Param` で受け取る。ハンドラの `Values` で `EffectValueInfo(..., value1IsText: true)` を返すと、カードエディタは値1欄を int ではなく**テキスト入力欄**として描画する（`DrawValueFields`）。例: `HandCollectionWin`（太郎勝利）の勝利条件カードID（完全ID をカンマ区切り）。`BuildBody` では `ctx.Param` を解析してテキスト生成する。

> **カード効果でゲームを勝利させる効果**: 効果から勝敗を決めるには `_isGameOver = true` にして `OnGameEnd(playerWins, winReason)` を呼ぶ（共通3条件の `CheckFieldCharsWin` / `AddVictoryPoints` と同じ流れ）。新しい勝因は `WinReason` に追加し、`MainPresenter.Animations.Effects.cs` の `GetWinReasonText` / `GetWinReasonColor` / `ApplyEmblemReason` と紋章 USS（`Main.Overlays.uss` の `.game-end-emblem--*`）を足す。**手札など同期されない情報で勝敗が決まる**場合は、発動側が判定して相手へ一方向通知する（`HandCollectionWin` は `NGS_SpecialWin` ＋ `WatchForOpponentSpecialWinAsync`。投了と同じ方式。[networking.md](networking.md)「特殊勝利」参照）。

**③ 新しい盤面操作が必要なら MainPresenter に internal メソッドを足す**

既存の building-block（`ApplyDrawEffectAsync` / `ApplyDamageAllEnemiesAsync` / `ApplyBanishCharAsync` / `AddVictoryPoints` / `PlayFloatingLabelAsync` …）で足りる場合は**②だけで完結**する。新しい盤面操作が必要なときだけ、[MainPresenter.Phases.Resolution.cs](../Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs) 等に `internal` メソッドを追加してハンドラから `p.XxxAsync(...)` で呼ぶ（効果ハンドラは同一 Main アセンブリなので `internal` で参照できる）。盤面状態の参照には `p.FieldFor(isLocal)` などのヘルパーを使う。

> ハンドラの `ApplyAsync` は `OnPlay` のプレイ時（`ResolveSingleCardAsync`）と `OnTurnStart` 永続イベントの毎ターン発動（`PlayGraveyardEventEffectAsync`）、キャラの各トリガー（`ResolveCharacterTriggeredEffectAsync`）すべてから共通で呼ばれる。演出はハンドラ内に書けば全経路へ自動で反映される。

> **キャラ単位の実行時状態を増やす効果（バフ・能力付与など）**: 攻撃力/HP バフ（`BuffAttack/Hp`）やキーワード能力付与（`GrantKeyword`）のように、キャラ個体へ永続的に乗る状態は [CardView.cs](../Assets/Scripts/Main/Card/CardView.cs) に持たせる。①フィールド（例: `_attackBuff` / `_grantedGuardian`）を追加し、②**SO 固有値と実行時付与を合成した参照プロパティ**（`CurrentAttack` / `MaxHp` / `HasGuardian` 等）を公開して、戦闘判定や表示はこのプロパティ経由に統一する（SO の生フィールドを直接読まない）。③`CopyRuntimeStateFrom` にも複製を追加する（`CopyFieldChar` でコピー元の状態を引き継ぐため）。例として `GrantKeyword` は守護/速攻/飛行/防人/強襲/デッキ攻撃×の付与フラグと `HasGuardian` / `HasHaste` / `HasFlying` / `HasSakimori` / `HasAssault` / `HasNoDeckAttack` プロパティを追加し、`MainPresenter.Phases.Main` の `IsGuardian` 等の攻撃ルール判定を `card.Has*` 参照へ切り替えた。付与キーワードと値2の対応は [GrantableKeyword.cs](../Assets/Scripts/Main/Card/GrantableKeyword.cs)。

**勝利点付帯値（VictoryPointBonus）はハンドラに書かない**

効果（EventType）とは独立した加点で、`ResolveEventCardEffectAsync` / `ResolveCharacterTriggeredEffectAsync` が効果解決後に `ApplyVictoryPointBonusAsync(bonus, isLocal, card, ct)`（メダル演出 ＋ `AddVictoryPoints`）をまとめて実行する（bonus が 0 なら何もしない）。メダルフロート `PlayFloatingMedalAsync(anchor, amount, ct)` は得点数 1〜5 に応じて Medal1〜Medal5Icon を出し分ける（0 や 6 以上は Medal1Icon）。「効果＋勝利点」も「勝利点を得るだけ」（`EventType=None` ＋ 付帯値）もこれで賄う。

**演出ヘルパー**（ハンドラの `ApplyAsync` から呼ぶ）

パーティクルが必要なら `MainPresenter.cs` に `[SerializeField] private GameObject _xxxEffectPrefab;` を追加し（フィールドは private のまま、ハンドラから使う場合は internal の Apply ラッパー経由にする）、`PlayParticleAtCardAsync(card, _xxxEffectPrefab, ct)` を呼ぶ。回転が必要な場合は4引数版 `PlayParticleAtCardAsync(card, prefab, ct, Quaternion.Euler(x, y, z))`。カード以外の位置（フィールド中央など）で再生・拡大したい場合は `PlayParticleAtUiPositionAsync(panelRef, uiPos, prefab, ct, scale: 2f)`（BounceAll が使用）。
フローティングラベルのみは `PlayFloatingLabelAsync(text, cssClass, anchor, ct)`（`anchor` は基準 `VisualElement`）。ラベル + パーティクルの組み合わせは `PlayXxxEffectAsync` 内で `UniTask.WhenAll` 並列実行する。BanishChar のように適用自体にアニメが要る効果は `ApplyBanishCharAsync` のような専用 Apply メソッドにまとめる（`worldBound` はフィールド除去前に記録すること）。

> **演出後の共通ディレイ**: `PlayParticleAtUiPositionAsync`（パーティクル）・`PlayFloatingLabelAsync`・`PlayFloatingMedalAsync` はいずれも末尾で共通の余韻ディレイ `EffectTrailingDelaySeconds`（`MainPresenter.cs`、0.25秒）を待つ。これにより「演出終了 → 0.25秒 → 次の処理」のテンポが全カード効果で統一されるため、呼び出し側で個別に `UniTask.Delay` を挟む必要はない。パーティクルの待ち時間は Prefab の実再生時間（`max(duration, lifetime) / simulationSpeed`）に補正済み。詳細は [docs/effects.md](effects.md) のセクション7を参照。

**イベント効果解決中（メインフェーズ内の即時解決）にプレイヤー入力が必要な効果（Switch / Evolve 相当）の場合**

`MainPresenter.cs` に `private readonly StagedInput _xxxInput = new StagedInput();` と必要なら状態変数（例: `private int _evolveMinCost;`）を追加し、`MainPresenter.Input.cs` の `HandlePlayerCardDrop` / `TryTakeStagedInput` / `OnPassClicked` / `OnBackClicked` / `CanPlayerDragCard` / `IsCardPlayable` の各メソッドに `_xxxInput._tcs != null` のチェックを追加する。`_switchInput` / `_evolveInput` の実装が参考例。`WaitForPlayerXxxInputAsync` でボタン表示・TCS 完了待ちを行い、`ApplyXxxEffectAsync` から `await WaitForPlayerXxxInputAsync(ct)` で結果を受け取る。ドロップ時にすでに `PlaceCard` が呼ばれるため、TCS 完了後に再度 `PlaceCard` しないこと。

**フィールドのキャラをクリックで対象選択する効果（Switch / Evolve の自軍選択 / DamageEnemy の敵選択）の場合**

`MainPresenter.cs` に選択用 TCS（単数なら `UniTaskCompletionSource<CardView>`、複数選択なら `UniTaskCompletionSource<List<CardView>>` ＋選択リスト・必要数フィールド）を追加し、対象フィールドの `OnCardClicked`（`_playerFieldView` = 自軍 / `_opponentFieldView` = 敵）の先頭で「選択中なら専用ハンドラを呼んで `return`」するよう分岐する。待機ヘルパーでは対象キャラに `selectable-char` クラスを付与（金枠＋拡大ハイライト。スタイルは [Main.Highlights.uss](../Assets/Scripts/Main/View/Main.Highlights.uss) の `.selectable-char`）して `ShowToast(...)` で案内し、`finally` でクラスを除去する。

複数選択（DamageEnemy）の場合は、クリックハンドラで未選択キャラを選択リストへ追加して `selected-char`（赤枠）を付け、残り体数をトーストで更新し、必要数に達したら `TrySetResult(list)` で確定する。対象数が候補数以上なら選択不要で全員を対象にする。オンラインでは選んだ対象を**フィールドのインデックス配列**で相手へ送る（`NetworkGameService` の `SendDamageTargets` / `WaitForOpponentDamageTargetsAsync` と専用メッセージキー・ペイロード。同名カードが複数いても曖昧にならない）。選択が不要なケース（全員が対象）では送受信しない。敵フィールドの複数選択は `ResolveEnemyCharTargetsAsync` / `HandleEnemyCharSelectionClick`、**自フィールド（味方）の複数選択**は `ResolveAllyCharTargetsAsync` / `WaitForPlayerAllyCharsSelectionAsync` / `HandleAllyCharSelectionClick`（AtkBoost / HpBoost が使用。`_allyCharSelect*` フィールドと `_playerFieldView.OnCardClicked` の分岐）が参考例。どちらも `NGS_DamageTarget` チャネルを共用する。

> **オンライン同期の注意**: 受信側のハンドラ登録がアニメーション後だと、相手の送信が先に届いたとき NGO が未登録メッセージを破棄して**永久待機になる**。これを防ぐため、`NetworkGameService` の全ゲームプレイメッセージ（`k_GameplayChannels`）は対戦開始時（`PrepareDecksAsync` → `RegisterGameplayChannels`）にハンドラを永続登録し、受信値を per-channel のキューにバッファしている。`NGS_DamageTarget` も OnEnter / OnAttack / OnDestroy / イベント / Bounce と多数の箇所から呼ばれるが、この仕組みでタイミング非依存になっている。新たな同期メッセージを追加するときは、種別定数を `k_GameplayChannels` に足して `SendJson` / `WaitJsonAsync` を使うだけでよい（手動のハンドラ登録・解除は不要・禁止）（[networking.md](networking.md) セクション13）。

---

## 2-B. キャラカードに登場時効果を追加する

キャラカードは `EffectTrigger`（[CharacterEffectTrigger.cs](../Assets/Scripts/Main/Card/CharacterEffectTrigger.cs)）に `OnEnter` を設定すると、通常配置でフィールドに出した瞬間に効果が発動する。効果種別は `EventType`（イベントと共通）を流用する。

**① カードデータに効果を設定する**

対象キャラが属する `CharacterCardSO`（`Assets/Data/Set{弾}/{属性}/CharacterCards_{属性}.asset`）のインスペクターで、`Effect Trigger = OnEnter`、`Effect Type`（例: `Draw` / `BanishChar`）、`Effect Value`、`Description`（詳細モーダル表示用の説明テキスト）を設定する。任意で `Flavor Text`（世界観テキスト。効果には影響せず詳細モーダル最下部に表示）も設定できる。

**② 効果種別の解決処理（ハンドラは「2」で共有）**

キャラ効果は[「2. 新しいカード効果（EventType）を追加する」](#2-新しいカード効果eventtypeを追加する)で作る `EffectHandler` を**そのまま流用する**。`ResolveCharacterTriggeredEffectAsync`（OnEnter / OnAttack / OnDestroy / OnTurnStart / OnAttacked / OnKill / OnDealPlayerDamage 共通）は `EffectCatalog.Get(EffectType)` でハンドラを引いて `ApplyAsync` を呼ぶだけなので、**キャラ専用の switch 追加は不要**。イベント専用にしたい効果はハンドラで `ValidOnCharacter => false` を返す（Switch / Evolve / Recover がこの扱い）。現状の実装済み効果は [event.md](event.md)「効果一覧」を参照（固定値の勝利点は `EventType` ではなく `VictoryPointBonus` 付帯値で付与する）。

ハンドラ実装の指針（既存ハンドラが参考例）:
- 墓地など盤面状態から加点値を動的に算出する効果（`GainVPPerGreenGraveHandler` は `p.CountGreenInGraveyard(...)` → `GraveyardView.CountByAttribute` の結果を `p.AddVictoryPoints` へ渡す。墓地は同期済みで決定的）。
- 敵キャラを N 体選ぶ効果（`DamageEnemyHandler` / `BounceHandler` / `BanishCharHandler`）は対象選択を `ResolveEnemyCharTargetsAsync`（プレイヤー選択／CPU 自動／オンラインはインデックス同期。トースト文言は引数）で共用する MainPresenter 側メソッドを呼ぶ。
- 2つの数値が必要な効果（SummonChar の「ID」と「体数」など）は `EventValue2` / `EffectValue2`（=`inv.Value2`）を使う（未使用は 0）。`inv.Value2` を**種別セレクタ**として使うこともできる（`GrantKeywordHandler` は `Value2` で付与キーワードを選び＝1=守護/2=速攻/3=飛行…、`GrantableKeywordExtensions.FromValue` で変換。`Values` の `Value2Used` は `true`）。なお挙動が大きく異なるモードは**値2分岐より固有 EventType へ分けるほうが扱いやすい**（コインドロー／サイコロドローは旧 Draw 値2分岐から `EventType.CoinDraw` / `DiceDraw` に独立させた）。
- **乱数で結果が変わる効果はネットワーク同期が必要**。効果は通常カードデータ＋同期済み盤面から両クライアントで決定的に解決されるが、乱数を使うとクライアント間でズレる。発動側（`isLocal=true`／オフライン）だけが乱数で結果を確定して相手へ送信し、ミラー側（オンライン `!isLocal`）は受信値を使う（`SendJson` / `WaitJsonAsync` で専用チャンネルを追加。[networking.md](networking.md)「メッセージ一覧」）。コインドロー（`ApplyCoinDrawEffectAsync`）は「表＝ドロー」の回数を `NGS_CoinDraw` で、サイコロドロー（`ApplyDiceDrawEffectAsync`）は出目を `NGS_DiceDraw` で送る。どちらもデッキ順が同期済みのため両者が同じカードを引く。シャッフル結果を送る Recover（`NGS_RecoverDeck`）と同じ考え方。なお演出中の見せ数字など**結果に影響しない乱数は同期不要**（クライアント間でズレてよい）。
- **相手の非公開情報（手札など）を操作する効果は「逆方向同期」**。相手の手札は同期されない（裏向き・枚数のみ。`OnlineInitialState.OpponentHandCount`、`_opponentHandView` は同一プレースホルダ）ため、発動側は中身を知らない。手札を捨てさせる `DiscardHandler`（`ApplyDiscardAsync`）は **被害者（手札の持ち主）側が選び**、選んだ完全 ID を**発動側へ送る逆方向**（`NGS_Discard`。通常の `NGS_DamageTarget` 等は発動側→ミラーだが、これは被害者→発動側）。発動側（`isLocal=true`）は ID を受信してプレースホルダを実カードに置換し相手墓地へ、被害者（`isLocal=false`）は選んで送信する。手札の選択 UI は `WaitForPlayerHandDiscardSelectionAsync`（`deck-pick` ピッカー流用）、CPU 被害者は `CpuAgent.ChooseDiscardIndices`（低コスト順）。**手札→墓地はダメージトリガー（`TriggerOnGrave`）を発動しない**（デッキ→墓地のみ。下記「ダメージトリガー」）。
- デッキ／墓地などの**非公開ゾーンからカードを選んで動かす効果**（`SummonFromDeckByKeywordHandler` / `AddToHandFromDeckByKeywordHandler` / `SummonFromGraveHandler`）は、対象ゾーンの `GetCardDataSnapshot()`（インデックス付きコピー）で候補を抽出 → インデックスで `RemoveCardAt`（複数取り除くときはインデックスを詰めるため**降順**で）→ 配置・手札追加へ運ぶ。選択はプレイヤー＝ピッカー（単数は `WaitForPlayerDeckCardSelectionAsync`、複数選択は共通の **`WaitForPlayerCardsPickAsync`**＝タイトル/サブタイトル/追加CSSクラスを差し替えてデッキ・墓地で共用）／CPU＝高コスト順／オンライン＝選んだインデックス配列を `NGS_DamageTarget` チャネルで同期（ゾーンの並び順は両クライアントで同期済みのため決定的）。場へ出す系はフィールドの空きスロット数（`FieldView.MaxCharacters` − 現在キャラ数）で体数の上限を打ち、ゾーンから抜いたのに置けず消える事故を防ぐ。`GraveyardView` も `DeckView` と同じ `GetCardDataSnapshot` / `RemoveCardAt` を備える。
- デッキにダメージを与える（デッキ→墓地へミルする）効果（`DamageEnemyDeckHandler` / `DamageBothDecksHandler`）は `p.MillDeckAsync(deckOwnerIsLocal, count, ct)` を呼ぶ。デッキ攻撃と共通の building-block で、ミル・ダメージトリガー（`TriggerOnGrave`）起動・オーバーリミット判定（空デッキからさらにミルしようとした瞬間に持ち主が敗北）・オーバーリミット告知をまとめて行う。発動側から見た相手デッキは `deckOwnerIsLocal: !inv.IsLocal`、自分のデッキは `inv.IsLocal`。デッキ順は同期済みのため追加同期不要。
- **複数の building-block を1効果に合成する**例：`DamageDeckRecoverByColorCharsHandler` は盤面カウント（`p.CountCharsOnFieldByAttribute` ＝ `GainVPPerGreenGrave` の動的カウントと同型）→ ミル（`MillDeckAsync`）→ 墓地回収（`ApplyRecoverEffectAsync`）を `p.ApplyColorCharDeckDamageAndRecoverAsync` 1メソッドに並べる。**枚数 N は一度だけ算出して各処理で共有**し、ミルで持ち主がデッキ切れ敗北したら（`_isGameOver`）後続をスキップする。各 building-block が自前でオンライン同期するため合成側に追加同期は不要。なお属性を**値で指定する**ときは `CardAttributeNames.FromNumber`（白1/青2/緑3/黄4/赤5/黒6/紫7。0=全属性扱い・範囲外は null）で実行時に解決する（`CardIdAutoAssigner.AttributeNumber` は Editor 専用で同じ並び）。
- コスト支払いに作用する効果（`NextCardCostFreeHandler`）はプレイヤーごとの永続フラグを立て、`PayHandCostAsync` 側でコスト0化・消費する（[event.md](event.md)「効果ごとの注意点」参照）。
- ターン進行に作用する効果（`ExtraTurnHandler`）はアクティブプレイヤーのフラグ（`_extraTurnPending`）を立て、`RunTurnAsync` 末尾で `GameModel.RepeatTurn()` を呼ぶ（オンラインは Pass 時の相手ドロー待ち登録をスキップして lockstep を維持）。
- フィールドのキャラのステータスを永続変化させる効果（`BuffAttackByKeywordHandler` / `BuffHpByKeywordHandler`）は `CardView` の実行時バフ（`_attackBuff` / `_hpBuff` → `CurrentAttack` / `MaxHp`）を `ApplyBuffByKeywordAsync` で加算する。**攻撃力を実行時に変える効果を足すときは、戦闘ダメージ・CPU判断・対象選択の攻撃力参照を `Data.Attack` ではなく `CardView.CurrentAttack` に通すこと**。
- 任意の文字列で対象を絞る「特徴（キーワード）」は `CardData.Keyword`（=`inv.Keyword`。マスターは `CardKeywordSO`）で管理し、文字列一致で判定するため実行時の追加ロード・同期は不要（[event.md](event.md)「特徴（キーワード）」参照）。

**発動箇所**: 通常配置パス（ローカル `ExecuteLocalMainResolveAsync` の `PlaceChar` ／ 相手 `ExecuteOpponentCardPlayAsync` のキャラ配置後）で `ResolveCharacterEnterEffectAsync` を呼んでいる。Switch / Evolve で出し直したキャラ（`ApplySwitchEffectAsync` / `ApplyEvolveEffectAsync` のローカル／オンライン相手／CPU の各3経路）も配置後に同メソッドを対称に呼んで OnEnter を発動する。CPU・オンライン相手も同経路でカバーされ、効果はカードデータから導出されるため追加のネットワーク同期は不要（対象選択は永続キュー化されたチャンネルで取りこぼさない）。

**他のトリガー**: `OnAttack`（攻撃時）・`OnDestroy`（破壊時）・`OnTurnStart`（自分のターン開始時）も `ResolveCharacterTriggeredEffectAsync` を共用する。`OnDestroy` は戦闘での撃破（`ExecuteAttackAsync`）・`DamageEnemy` / `DamageAllEnemies` での撃破・`BanishChar` での除去の各破壊経路から `FireOnDestroyEffectAsync(destroyedCard, ownerIsLocal, ct)` を呼んで発動する（破壊が墓地への移動まで完了した後に解決。同時破壊は1体ずつ順番に発動）。破壊されたキャラの所有者は発動側の相手なので `ownerIsLocal = !isLocal` を渡す。新しい破壊経路を追加したときは同じ呼び出しを差し込む。`OnTurnStart` は `ResolveTurnStartEffectsAsync`（`RunTurnAsync` のターン開始演出後・ドロー前）から、アクティブプレイヤーの場のキャラを並び順に発動する。`OnAttacked`（被攻撃時）・`OnKill`（撃破時）は戦闘 `ExecuteAttackAsync` からのみ `FireOnAttackedEffectAsync(defender, !isLocal, ct)`／`FireOnKillEffectAsync(attacker, isLocal, ct)` を呼んで発動する。`OnAttacked` は対象が攻撃の対象になった直後・破壊判定の前に発動し（回復・反撃で生死が変わり得るため判定はこの後に再計算）、**攻撃側 ATK 0 の盾ブロック（ダメージ0）でも発動する**（`damage == 0` の早期 return 前にも `FireOnAttackedEffectAsync` を呼ぶ）。`OnKill` は対象の OnDestroy 解決後に攻撃側が場に残っていれば発動する。`OnAttacked` / `OnKill` はどちらもキャラ vs キャラの戦闘（`ExecuteAttackAsync`）のみで、`DamageEnemy` 等の効果ダメージやデッキへの直接攻撃（ミル）では発動しない。`OnDealPlayerDamage`（相手プレイヤーにダメージを与えた時）はデッキ攻撃 `ExecuteDeckAttackAsync` のミル完了後に `FireOnDealPlayerDamageEffectAsync(attacker, isLocal, ct)` を呼んで発動する（攻撃キャラが場に残り・デッキ切れで終局していない場合のみ、1回のデッキ攻撃につき1回）。デッキミル効果（`DamageEnemyDeck` / `DamageBothDecks`）からは発動しない。

**永続イベント（EventCardTrigger.OnTurnStart）**: イベントカードは `Event Trigger`（[EventCardTrigger.cs](../Assets/Scripts/Main/Card/EventCardTrigger.cs)）に `OnTurnStart` を設定すると、プレイ時は即時解決せず登録簿（`_playerTurnStartEvents` / `_opponentTurnStartEvents`）に登録され、自分のターン開始時に毎ターン墓地からせり出して発動し続ける（`ResolveTurnStartEffectsAsync` → `PlayGraveyardEventEffectAsync` → `ResolveEventCardEffectAsync`）。コストとして捨てたカードは `ResolveSingleCardAsync` を通らないため登録されない（墓地は走査しない）。詳細は [event.md](event.md)「EventTrigger」。

---

## 2-C. コスト支払い時に作用する受動効果（CostBoost 相当）を追加する

`Draw` 等の「プレイ時に解決される効果」とは異なり、`CostBoost` は**手札からコストとして支払うときだけ作用する受動プロパティ**。`CostBoostHandler` の `ApplyAsync` は **no-op（解決時は何もしない）**で、実体は下記のコスト計算側に持たせる（エディタ表示のため `Values` / `BuildBody` だけは持つ）。

実装は「カードが何コスト分として数えられるか」を表す `CardData.CostPaymentValue(CardAttribute payingForAttribute)`（virtual、通常 1）で表現する。引数 `payingForAttribute` は**プレイするカードの属性**で、属性連動の受動効果（CostBoost）に使う。

- **判定箇所**: [CardData.cs](../Assets/Scripts/Main/Card/CardData.cs) の `public virtual int CostPaymentValue(CardAttribute payingForAttribute) => 1;` を、[CharacterCardData.cs](../Assets/Scripts/Main/Card/CharacterCardData.cs) / [EventCardData.cs](../Assets/Scripts/Main/Card/EventCardData.cs) で `override` してカードの種別/値から導出する（CostBoost なら、自属性が `payingForAttribute` と一致するとき `Max(1, 値)`、それ以外は 1。白も一般属性扱いで、白 CostBoost は白のコストのみ倍化）。
- **コスト計算**: [MainPresenter.Input.CostSelection.cs](../Assets/Scripts/Main/MainPresenter.Input.CostSelection.cs) の `CostCapacityExcluding(excluded, payingForAttribute)` / `SelectedCostValue`（`_playedCardAttribute` を使用）が `CostPaymentValue` を合算する。コスト判定は「枚数」ではなく「合計コスト値」で行うため、新しい受動効果を足すときもこの2メソッドが算出経路になる。配置可否判定（[MainPresenter.Input.cs](../Assets/Scripts/Main/MainPresenter.Input.cs)）もプレイするカードの属性を渡す。
- **CPU**: [MainPresenter.Animations.CostFly.cs](../Assets/Scripts/Main/MainPresenter.Animations.CostFly.cs) の `ChooseCpuCostCards` がプレイするカードの属性で合計コスト値ベースに自動選択する。プレイ可否は事前に `MainPresenter.CpuCanAffordCost`（自身を除いた手札の `CostPaymentValue` 合計 ≥ コスト）で判定し、支払えないカードは出さない。
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

### 難易度（初級/中級/上級）で挙動を変える

難易度は相手ごとに `CpuRosterSO`（`CpuOpponentData.Difficulty`）で持ち、`MainPresenter` が対戦開始時に `_cpuDifficulty` へ確定する（`CpuAgent` 自体はステートレスのまま）。難易度依存の判断は `MainPresenter` 側のフックで分岐させる:

- **出すカードの除外**: `CpuAgent.Choose...Index(hand, canAfford)` に渡す述語で `CpuMayPlayToField(hand[i])` を AND する（[MainPresenter.Phases.Main.cs](../Assets/Scripts/Main/MainPresenter.Phases.Main.cs)）。中級以上は `IsCostOnlyCard()`（CostBoost／`TriggerOnGrave`）を場に出す候補から除外する。
- **コスト支払いの優先**: `ChooseCpuCostCards(played, hand, preferCostOnly)`（[MainPresenter.Animations.CostFly.cs](../Assets/Scripts/Main/MainPresenter.Animations.CostFly.cs)）に `preferCostOnly = _cpuDifficulty != Beginner` を渡し、コスト専用カードを先に充てる。

上級は当面中級と同じ。独自ロジックは `_cpuDifficulty == CpuDifficulty.Advanced` の分岐を上記フックに足して拡張する。

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

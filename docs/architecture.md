# 設計ドキュメント

## 設計方針

マルチプレイヤーカードゲーム「Card」の設計方針。以下を目標としている。

- **DI・リアクティブを標準** とし、`Find()` や static は使わない
- **非同期を UniTask** で統一し、キャンセル処理を明示的に行う
- **アセットは Addressables** で遅延ロードし、`Resources.Load` は使わない
- **UI は UI Toolkit（UXML）** で構築し、uGUI は使わない

---

## シーン構成

```
Common (常駐)
  ├── SoundPlayer
  ├── SceneTransitioner
  ├── TransitionPresenter
  ├── OptionPresenter / OptionModel
  ├── GameSessionModel
  ├── DeckModel
  └── Store 群（SoundStore / ModalStore）

Title (アディティブ)   →   DeckBuilder (アディティブ)   →   Main (アディティブ)
```

- `Common` シーンは起動時にロードされ、以降アンロードされない
- 他シーンは `Common` の上にアディティブでロード・アンロードされる
- シーン遷移は `SceneTransitioner.Transit(Scenes next)` を呼ぶだけでよい
- 遷移時は `TransitionPresenter` が画面をフェードアウト→ロード→フェードインの演出を行う

### なぜアディティブか

シーン単位で DontDestroyOnLoad を使わず、Common シーンを「永続レイヤー」として扱うことで
サウンド・オプション・シーン遷移を全シーンで共有できる。

---

## 依存性注入（VContainer）

```
CommonLifetimeScope   全シーン共通のシングルトンを登録
  ├── DeckModel
  ├── ModalStore
  ├── OptionPresenter
  ├── OptionModel
  ├── SoundPlayer
  ├── SoundStore
  ├── TransitionPresenter
  └── SceneTransitioner

TitleLifetimeScope         Title シーン固有のサービスを登録
DeckBuilderLifetimeScope   DeckBuilder シーン固有のサービスを登録
MainLifetimeScope          Main シーン固有のサービスを登録
```

- 各シーンの `Injector/` フォルダに `*LifetimeScope.cs` を置く
- 新しいサービスは LifetimeScope に登録してコンストラクタでインジェクト
- シーンロード後の LifetimeScope 構築は `SceneExtensions.BuildLifetimeScopes()` 拡張メソッドが担う（BootLoader / CommonSceneLoader / SceneTransitioner から呼ばれる）

---

## 状態管理（R3）

Model → Presenter の単方向データフロー + 双方向バインディング。

```
OptionModel
  BGMVolume: ReactiveProperty<float>
  SEVolume:  ReactiveProperty<float>

OptionPresenter
  → BGMVolume.Subscribe で Slider を更新
  → Slider の ValueChanged で SetBGMVolume() を呼ぶ
```

- サブスクリプションは `AddTo(_disposables)` または `AddTo(destroyCancellationToken)` で管理
- Model は PlayerPrefs を通じて永続化する

---

## 非同期処理（UniTask）

- `IAsyncStartable` を実装したクラスは VContainer が StartAsync を呼ぶ
- `Store` 系クラスは起動時に Addressables ロードを行い、`UniTask Loaded` プロパティで完了を通知する
- 使う側は `await _store.Loaded` で待機してから使用する

```csharp
// 例: AudioManager (Title シーン)
public async UniTask StartAsync(CancellationToken cancellation = default)
{
    await _soundStore.Loaded;
    _soundPlayer.PlayBGM(_soundStore.TitleBGM);
}
```

### シーン遷移のキャンセル処理

`SceneTransitioner` は `SemaphoreSlim` で同時遷移を防ぎ、
連打された場合は最後のリクエストのみ実行する（前の遷移は CancellationToken でキャンセル）。

---

## サウンド設計

- BGM: `AudioSource.loop = true`、`PlayBGM()` で差し替え
- SE: `PlayOneShot()` で重ね再生
- 音量は `OptionModel.BGMVolume / SEVolume` (0–1) を ReactiveProperty で管理
- `SoundPlayer` は音量変化を Subscribe して AudioSource に即時反映

> `_bgmAudioSource.volume = v / 2` としているのは、
> OptionModel の値 1.0 がデフォルトの AudioSource 最大音量の半分に相当するようにしているため。

---

## UI 設計（UI Toolkit）

### ファイル配置

```
Assets/Scripts/<Scene>/<Feature>/
  ├── *Presenter.cs   （UI ロジック）
  └── *.uxml          （見た目 / Addressables 経由でロードするものは AddressableAssets/ に配置）
```

### PanelSettings

`Assets/Scripts/Panel Settings.asset` の Scale Mode を **Scale With Screen Size** に設定済み。
基準解像度に対して UI 全体がスケールするため、固定 px 値で指定したサイズが解像度によらず適切な物理サイズになる。

### オプションモーダル

- アイコンクリックで表示、Close ボタンで非表示
- 「タイトルに戻る」ボタンでモーダルを閉じつつ Title シーンへ遷移
- オーバーレイ（`rgba(0,0,0,0.55)`）がゲーム画面を暗幕
- モーダルカードは画面中央に配置（`align-items: center; justify-content: center`）
- UIDocument の SortingOrder を 1000 にして他 UI より手前に表示

---

## アセンブリ構成

スクリプトは3つの Assembly Definition に分割されている。

| アセンブリ | パス | 依存 |
|---|---|---|
| `Common` | `Assets/Scripts/Common/` | VContainer / R3 / UniTask / DOTween |
| `Title` | `Assets/Scripts/Title/` | VContainer / UniTask / Common |
| `Matching` | `Assets/Scripts/Matching/` | VContainer / R3 / UniTask / Common / Unity.Services.Multiplayer |
| `Main` | `Assets/Scripts/Main/` | VContainer / R3 / UniTask / DOTween / Common |

- `Title` / `Matching` / `Main` は `Common` に依存し、逆方向の依存は禁止
- `autoReferenced: true` のため既存コードへの影響なし

---

## アセット管理（Addressables）

```
Assets/AddressableAssets/
  ├── Card/        Card.uxml（カードテンプレート）
  ├── Icon/        HeartIcon.png（コスト・デッキ枚数バッジ用）、AttackIcon.png（攻撃力バッジ用）、GraveIcon.png（墓地枚数バッジ用）、CharaIcon.png（キャラカード種別アイコン）、SkillIcon.png（技カード種別アイコン）
  ├── Image/       CardBack.png（カード裏面画像）、NamePlate.png（カード名プレート背景）、Card*.png（カードイラスト）、BattleField.png（盤面背景）、OKButtn.png（OKボタン画像）、ReturnButtn.png（戻るボタン画像）、PassButton.png（パスボタン画像）
  ├── Modal/       Modal.uxml
  └── Sound/       AudioClip
```

- `SoundStore` が BGM クリップをロード
- `ModalStore` が Option モーダルの VisualTreeAsset をロード
- `CardStore` がカードテンプレート（VisualTreeAsset）・裏面画像・盤面背景（Texture2D）をロード
- ロード完了は `UniTask Loaded` プロパティで通知

---

## DeckBuilder シーン（デッキ構築）

### 概要

Title → DeckBuilder → Main の遷移フローでプレイヤーがデッキを組む画面。

### DeckModel（Common）

```
DeckModel   シングルトン。シーンをまたいでデッキ内容を保持
  CardIds       IReadOnlyList<string>。選択済みカード ID の順序付きリスト
  Count         現在の枚数
  IsReady       Count >= MinSize (5) のとき true
  IsFull        Count >= MaxSize (20) のとき true
  TryAdd(id)    最大枚数未満なら追加して true を返す
  Remove(id)    先頭の該当 ID を1枚削除
  Clear()       デッキをリセット
```

### DeckBuilderPresenter のフロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. `DeckModel.Clear()` でデッキをリセット
3. `CardDatabase.AllCards` を全件スキャンして CardView を 0.6 倍スケールでグリッド表示
4. カードクリック → `DeckModel.TryAdd(id)` → デッキパネル更新
5. デッキパネルは同一カードを「カード名 ×枚数 + サムネイル」形式で1行にまとめて表示
6. × ボタン → `DeckModel.Remove(id)` → デッキパネル更新
7. 「はじめる」ボタン（`IsReady` のとき有効）→ Main シーンへ遷移

### MainPresenter との連携

Main シーンロード時、`DeckModel.Count > 0` なら `CardDatabase.BuildDeck(DeckModel.CardIds)` でプレイヤーデッキを構築。空の場合は `AllCards` 全体をフォールバックとして使用。CPU は常に `AllCards` シャッフルを使用。

---

## Main シーン（カードゲーム盤面）

### カードシステム

```
CardData              抽象基底クラス。id / name / cost / Attack / Defense / image(Sprite)
  CharacterCardData   キャラカード。Defense 値を保持。戦闘前1フェーズまたはキャラセットフェーズでスロット/フィールドに配置
  SkillCardData       技カード。Damage（= Attack）値を保持。戦闘前1フェーズで裏向きフィールドに配置
  EventCardData       イベントカード。EffectType（None/AtkBoost）と EffectValue を保持
                      戦闘前2フェーズで Ready 化、解決フェーズで効果を適用後に墓地へ

EffectType        enum。None / AtkBoost（解決フェーズで次の戦闘フェーズまで ATK を加算）

CharacterCardSO / SkillCardSO / EventCardSO   各カード種別の ScriptableObject（インスペクター編集用）

CardDatabase      ScriptableObject。List<CardData> をインスペクターで編集、Dictionary でルックアップ
CardStore         IStartable。Addressables から Card.uxml と CardBack.png を非同期ロード

TurnPhase         enum。CharacterSet / Draw / PreBattle1 / PreBattle2 / Resolution / Battle
```

### ビューコンポーネント

```
CardView          VisualElement サブクラス。Card.uxml をクローンしてデータをバインド
                  イラスト（Sprite）をカード全面背景の ImageArea に表示
                  カード情報は左上に縦一列表示：カード名（NamePlate背景・中央寄せ・全幅）→ コストバッジ → 攻撃力バッジ → 防御力バッジ
                  コスト：HeartIcon.png にコスト数字を重ねた 36×36px バッジ
                  攻撃力：AttackIcon.png に攻撃力数字を重ねた 36×36px バッジ
                  防御力：ShieldIcon.png に防御力数字を重ねた 36×36px バッジ
                  State（Normal/Ready/Resolve）を持ち、Ready 時はオレンジ枠、Resolve 時は黄色枠で表示
                  FlipAsync() で DOTween による裏返しアニメーション
                  AttachDragManipulator() / RemoveDragManipulator() でドラッグ着脱
CharacterSlotView VisualElement サブクラス。キャラカードを1枚配置するスロット
                  PlaceCard(CardView) で配置。既存カードがある場合は OnCardDisplaced イベントで通知してから差し替え
                  Defense プロパティで現在配置中カードの DEF を返す（いなければ 0）
HandView          VisualElement サブクラス。手札を扇状に表示（60% スケール）
                  各カードをボトムセンター軸に最大 ±20° 回転 + 放物線アーク
                  ホバー時に DOTween で 1.5 倍スケールアップ＋BringToFront() で最前面表示
                  ドラッグ開始時に DragLayer へ移動、ドロップ失敗でスナップバック
                  ドロップ成功後に残りカードを DOTween でアニメーションしながら詰める
                  faceDown: true で裏向き表示、interactive: false でホバー・ドラッグ無効化（相手手札用）
                  AddCardAnimatedAsync() でデッキ位置から手札へのドロー演出（飛翔→フリップ）
                  AddCardBackAsync() でフィールドから手札へ戻す飛翔アニメーション（戻るボタン用）
                  interactive: false のときフリップをスキップ（相手手札は裏向きのまま）
                  内部状態は HandCardEntry { Card, ScaleTween } の単一リストで管理（並列リスト廃止）
FieldView         VisualElement サブクラス。横長フィールドエリア（最大 5 枚、中央寄せ）
                  配置済みカードはドラッグ不可。TryGetCardAt() でワールド座標からカードを取得
DeckView          VisualElement サブクラス。デッキを積み重ねで表示（裏向き、60% スケール）
                  deck-view CSS クラス付き（GraveyardView と同じ背景・枠線スタイルを共有）
                  デッキ上方に HeartIcon.png + 残り枚数を重ねた 80×80px バッジを表示
                  DrawTop() で一番上の CardData を取り出してデッキから除去（ターン開始ドロー用）
                  RemoveFromTop(n) で上から n 枚を除去し、Count と枚数バッジを更新
CardDragManipulator  PointerManipulator サブクラス。DragLayer 対応のドラッグ実装
                     ドロップ成功判定は Func<Vector2, bool> OnDrop コールバックで外部委譲
AttackArrowManipulator  PointerManipulator サブクラス。フィールドカードに装着する攻撃矢印マニピュレーター
                        PointerDown で ArrowView を DragLayer に追加、Move で先端を更新、Up/CaptureOut で削除
                        Func<bool> CanStart で開始可否を判定（false なら矢印を出さない）
                        Func<Vector2, bool> OnAttackTarget でドロップ座標を通知（true = 矢印を残す）
                        ClearArrow() で矢印を手動削除
ArrowView            VisualElement サブクラス。Painter2D で攻撃矢印を描画（ベジェ曲線＋矢印先端）
                     StartPoint / EndPoint を更新するたびに再描画。PickingMode.Ignore で操作を透過
GraveyardView        VisualElement サブクラス。破壊されたカードを積み重ねで表示（表向き・60% スケール）
                     AddCard(CardView) で受け取り、最新カードが最前面になるよう重ねる
                     デッキ上方に GraveIcon.png + 枚数を重ねた 80×80px バッジを表示（初期値 0）
                     茶色系の枠線（rgba(160,100,50)）と暗いベージュ背景で空時でも位置を視認可能
```

### フィールドレイアウト

画面中央の BattleArea（top: 100px〜bottom: 144px）を上下に分割。

```
BattleArea
  ├── OpponentFieldArea（上半分、gap: 32px で分離）── FieldView（相手フィールド）
  └── PlayerFieldArea  （下半分）── FieldView（自分フィールド）
```

- 手札からドロップできるのは PlayerFieldArea のみ
- フィールドエリアは `overflow: visible`（カードが見切れないよう）

### 画面レイアウト全体

```
OpponentHandArea        （画面上部・180° 反転・裏向き・非インタラクティブ）
OpponentDeckArea        （左上・60% スケール・180° 反転）
OpponentGraveyardArea   （右上・60% スケール・180° 反転）
OpponentCharacterArea   （右上・90% スケール・180° 反転・BattleArea 上半分に縦中央揃え）CharacterSlotView
BattleArea              （top: 100px〜bottom: 144px）
  ├── OpponentFieldArea（上半分）
  └── PlayerFieldArea  （下半分）
PlayerCharacterArea     （左下・90% スケール・BattleArea 下半分に縦中央揃え）CharacterSlotView
DeckArea                （右下・60% スケール）
GraveyardArea           （左下・60% スケール）
HandArea                （画面下端・60% スケール）
ActionButtonsArea       （右下・bottom: 210px・アクションステージ中のみ表示・横並び）
  ├── BackButton        （左・ReturnButtn.png・直前の操作を取り消す）
  ├── PassButton        （PassButton.png・戦闘前フェーズで出さない選択）
  └── OkButton          （右・OKButtn.png・処理フェーズへ進む）
```

### MainPresenter の初期化フロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. DragLayer（全画面オーバーレイ、PickingMode.Ignore）を生成して MainRoot の最後尾に追加
3. `CardDatabase.AllCards` を Fisher-Yates シャッフルし、プレイヤー用・CPU 用に独立してコピー
4. 相手・自分ともに `HandView`・`DeckView`・`FieldView`・`GraveyardView`・`CharacterSlotView` を配置
5. ドロップハンドラを接続（プレイヤーフィールドへのドロップ、キャラスロットへのドロップ）
6. Resolve オーバーレイとターン告知オーバーレイを生成
7. OK/戻る/パスボタンにハンドラを接続
8. `UniTask.NextFrame` でレイアウト確定を待つ
9. 自分・相手ともに 5 枚を 0.12 秒ずつずらして `AddCardAnimatedAsync` を並走
10. 相手手札カードを `_cpuCards` HashSet に登録
11. `RunGameAsync()` でゲームループを開始

### MainPresenter のゲームループ（RunGameAsync）

ゲーム開始時に `RunCharacterSetPhaseAsync` を1度だけ実行し、その後ターンループに入る。

```
RunCharacterSetPhaseAsync  （ゲーム開始時1回のみ）
  → "SET CHARACTERS" 告知
  → 先攻・後攻の順にキャラカードを裏向きでキャラスロットへ配置（スキップ可）
  → Resolve アニメーション → 両スロットのキャラカードを同時に表向き
```

各ターンは以下のフェーズを順に実行する（`TurnPhase` enum に対応）。

```
RunDrawPhaseAsync
  → ターンプレイヤーのデッキから1枚ドロー（AddCardAnimatedAsync）
  → "YOUR TURN" / "ENEMY TURN" 告知

RunPreBattle1PhaseAsync
  → "SET CARDS" 告知
  → 先攻・後攻の順にキャラカードまたは技カードを1枚だけ裏向きでフィールドにセット（スキップ可）

RunPreBattle2PhaseAsync
  → "SET EVENTS" 告知
  → プレイヤーとCPUが交互にイベントカードをReadyにする（2連続パスで終了）

RunResolutionPhaseAsync
  → Readyカードを逆順で解決（"RESOLVE" 演出）

RunBattlePhaseAsync
  → 全フィールドカードを同時に表向き
  → キャラカードをキャラスロットへ FlyCharToSlotAsync で移動（既存キャラは墓地へ）
  → 技カードが1枚でもあれば "FIGHT" 演出
  → PlayAtkCounterAsync: 両フィールドに 0→ATK合計 のカウントアップ表示（両者同時）
  → PlaySkillCardsAttackAsync: 技カードが相手デッキへ突撃（両者同時、複数枚は1枚ずつ逐次）
  → ダメージ適用 → 両デッキ同時0なら引き分け、片方0なら勝敗確定 → 墓地へ
```

**攻撃アニメーション（PlaySkillCardsAttackAsync）:**
- Phase 1 予備動作（0.15秒）: 後退 50px しながらデッキ方向へ向く（`Atan2` で回転角計算）
- Phase 2 突撃（0.65秒、InCubic）: 直線でデッキ中央へ加速
- Phase 3 ノックバック（0.15秒、OutQuad）: 着弾後 35px 跳ね返る
- 複数枚は逐次実行（`Sequence.Append`）。1枚のアニメーション完了後に次のカードが攻撃開始

**フライアニメーション（FlyCardToDestAsync）:**
- `worldBound` をカード除去前にキャプチャ
- DragLayer に絶対配置して DOTween で目標 `dest.worldBound.center` へ飛翔（0.3 秒）
- 到着後に `dest.Add(card)` して配置完了

### プレイヤー入力待ち（MainPresenter）

プレイヤーの操作入力は `UniTaskCompletionSource<CardView>` で待機し、ボタンイベントで完了させる。

**キャラセットフェーズ（WaitForPlayerCharSetInputAsync）:**
```
_charSetInputTcs を作成して ShowActionButtons()
  → キャラカードをスロットへドロップ → _stagedCharSetCard にセット → FlipAsync → OK/戻るボタン表示
  → OK ボタン                       → _charSetInputTcs.TrySetResult(card)
  → 戻るボタン（ステージ中）         → カードを手札へ返却
  → パスボタン（ステージなし）       → _charSetInputTcs.TrySetResult(null)
```

**戦闘前1フェーズ（WaitForPlayerPreBattle1TurnAsync）:**
```
_preBattleInputTcs を作成して ShowActionButtons()
  → キャラ/技カードをドロップ      → _stagedPreBattleCard にセット → FlipAsync → OK/戻るボタン表示
  → OK ボタン                     → _preBattleInputTcs.TrySetResult(card)
  → 戻るボタン（ステージ中）       → カードを手札へ返却
  → パスボタン（ステージなし）     → _preBattleInputTcs.TrySetResult(null)
```

**戦闘前2フェーズ（WaitForPlayerPreBattle2InputAsync）:**
```
_prepInputTcs を作成して ShowActionButtons()
  → イベントカードをドロップ        → _stagedPrepCard にセット → OK/戻るボタン表示
  → OK ボタン                      → _prepInputTcs.TrySetResult(card)
  → 戻るボタン（ステージ中）        → カードを AddCardBackAsync で手札へ返却 → パスボタンに戻る
  → 戻る/パスボタン（ステージなし） → _prepInputTcs.TrySetResult(null) ← null = パス
```

ボタン表示状態は `UpdateStagedButtons(bool hasStaged)` で一元管理する。

### CPU 処理（MainPresenter + CpuAgent）

ゲームループの各フェーズで CPU の行動を決定する。

```
CpuAgent.ChoosePreBattle1CardIndex(hand)   戦闘前1フェーズ：キャラ優先→技カードのインデックスを返す（なければ -1）
CpuAgent.ChooseEventCardIndex(hand)        戦闘前2フェーズ：イベントカード1枚のインデックスを返す（なければ -1）
```

- キャラセットフェーズ: キャラカードを選択して `CharacterSlotView` へ裏向きで飛翔配置
- 戦闘前1フェーズ: `ChoosePreBattle1CardIndex` でキャラまたは技カードを選択し `FieldView` へ裏向きで飛翔配置（`RunCpuPreBattle1SubTurnAsync`）
- 戦闘前2フェーズ: `ChooseEventCardIndex` でイベントカードを選択し `FieldView` へ飛翔・Ready 化（`RunCpuPreBattle2SubTurnAsync`）

### ゲームロジック（GameModel）

ゲームルールの詳細は [docs/rules.md](rules.md) を参照。

```
GameModel         フェーズ遷移と戦闘前2フェーズの入力状態を管理
  Phase                    現在フェーズ（TurnPhase enum）
  IsLocalTurn              ローカルプレイヤーのターンか
  IsLocalPreparationTurn   戦闘前2フェーズでローカルが行動する番か
  ReadyQueue               IReadOnlyList<CardView>。Readyになった順のカード

  BeginCharacterSet()      Phase → CharacterSet
  BeginPreBattle1()        Phase → PreBattle1
  BeginPreBattle2()        Phase → PreBattle2。IsLocalPreparationTurn = IsLocalTurn
  ReadyCard(CardView)      キューに追加してターンを交代
  Pass()                   連続パスカウントを増やし、2回で true（フェーズ終了）を返す
  BeginResolution()        Phase → Resolution
  BeginBattle()            Phase → Battle
  EndTurn()                キューをクリア・IsLocalTurn を反転・Phase → Draw
```

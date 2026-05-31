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

Title (アディティブ)   →   Home (アディティブ)   →   DeckBuilder (アディティブ)
                                              →   Main (アディティブ)
                                              →   Matching (アディティブ)
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
  ├── GameSessionModel
  ├── ModalStore
  ├── OptionPresenter
  ├── OptionModel
  ├── SoundPlayer
  ├── SoundStore
  ├── TransitionPresenter
  └── SceneTransitioner

TitleLifetimeScope         Title シーン固有のサービスを登録
HomeLifetimeScope          Home シーン固有のサービスを登録
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

### シーン遷移の多重実行防止

`SceneTransitioner` は `SemaphoreSlim` で同時遷移を防ぐ。
遷移中に `Transit` が呼ばれた場合は即座に無視する（`WaitAsync(0)` でゲートを取れない場合はリターン）。
シーンロードを中途キャンセルすると壊れた状態になるため、実行中の遷移はキャンセルしない。
ゲートは `RevealAsync`（フェードイン）の**前**に解放する。これにより新シーンが見え始めた時点でボタンがすぐ有効になる。

### ISceneReady — シーン準備完了の通知

`RevealAsync`（フェードイン）の前に、`SceneTransitioner` は次シーンの root GameObject を検索し `ISceneReady` を実装したコンポーネントがあれば `ReadyAsync(ct)` を await する。

これにより Addressables の非同期ロード完了前にフェードインが走って背景が空白になる問題を防ぐ。現在 `MainPresenter` が実装しており、背景画像（`_cardStore.BattleField`）を `mainRoot.style.backgroundImage` にセットした直後に `TrySetResult()` で通知する。

新しいシーンで「表示前に完了させたい非同期処理」がある場合は、そのシーンの Presenter に `ISceneReady` を実装するだけでよい。

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
| `Title` | `Assets/Scripts/Title/` | VContainer / UniTask / Common / Main |
| `Home` | `Assets/Scripts/Home/` | VContainer / UniTask / Common |
| `Matching` | `Assets/Scripts/Matching/` | VContainer / R3 / UniTask / Common / Unity.Services.Multiplayer / Unity.Netcode.GameObjects |
| `Main` | `Assets/Scripts/Main/` | VContainer / R3 / UniTask / DOTween / Common / Unity.Netcode.GameObjects |

- `Title` / `Home` / `Matching` / `Main` は `Common` に依存し、逆方向の依存は禁止
- `autoReferenced: true` のため既存コードへの影響なし

---

## アセット管理（Addressables）

```
Assets/AddressableAssets/
  ├── Card/        Card.uxml（カードテンプレート）
  ├── Icon/        HeartIcon.png（コスト・デッキ枚数バッジ用）、AttackIcon.png（攻撃力バッジ用）、GraveIcon.png（墓地枚数バッジ用）、CharaIcon.png（キャラカード種別アイコン）、SkillIcon.png（技カード種別アイコン）
  ├── Image/       CardBack.png（カード裏面画像）、NamePlate.png（カード名プレート背景）、Card*.png（カードイラスト）、BattleField.png（盤面背景）、OKButtn.png（OKボタン画像）、ReturnButtn.png（戻るボタン画像）、PassButton.png（パスボタン画像）、HomeBackground.png（Home 画面背景・晴れ）、HomeBackgroundRain.png（Home 画面背景・雨）
  ├── Modal/       Modal.uxml
  └── Sound/       AudioClip
```

- `SoundStore` が BGM クリップをロード
- `ModalStore` が Option モーダルの VisualTreeAsset をロード
- `CardStore` がカードテンプレート（VisualTreeAsset）・裏面画像・盤面背景（Texture2D）をロード
- ロード完了は `UniTask Loaded` プロパティで通知

---

## Home シーン（ホーム画面）

### 概要

Title → Home の遷移後に表示されるメインハブ画面。デッキ構築・バトル・マッチングへの導線を提供する。

### Live2D キャラクター（Dog-kid）

`HomeLive2DPresenter` が `Animator.Play(clip.name)` で全モーションからランダムに再生する無限ループを管理する。

- `[DefaultExecutionOrder(-10000)]` で Cubism 系コンポーネントより先に `Awake` を実行し、`CubismFadeController` を reflection で無効化する
- FadeMotionList なしで運用するため、`DogKidFadeMotionListSetup`（エディタメニュー `Live2D/Setup Dog-kid FadeMotionList`）でセットアップ済みの FadeMotionList アセットを Dog-kid プレハブに割り当てている

### 食べ物スポーン・食事演出（HomeFoodSpawner / HomeLive2DPresenter）

画面左半分をクリック（New Input System: `Mouse.current.leftButton.wasPressedThisFrame`）すると、クリック座標に Food Live2D プレハブをインスタンス化する。

- インスタンス化直後に `CubismFadeController` と `CubismParameterStore` を無効化する（FadeMotionList 未設定による NullRef と、RestoreParameters によるアニメーション値上書きを防ぐため）
- 犬は食べ物の座標へ歩行（Walk アニメーション）し、到達後に犬と食べ物の Eat アニメーションを同時再生する（`UniTask.WhenAll` で犬 Eat 時間と Food Eat 終了を並行待機）
- Food の `normalizedTime >= 1f` を毎フレームポーリングして Eat アニメーション終了を検出し、`Destroy` で削除する
- Food プレハブは `CubismRenderer._localSortingOrder = 50` により Dog-kid より前面に描画される

### 背景・天気（HomeBackgroundPresenter / HomePresenter）

`HomeBackgroundPresenter.Awake()` で 50% の確率で晴れ／雨を決定し、`IsRainy` プロパティとして公開する。`HomePresenter` は `[SerializeField]` で `HomeBackgroundPresenter` を参照し、`IsRainy` を読んで雨エフェクトとオーバーレイを制御する。

**HomeBackgroundPresenter**：`IsRainy` に応じて Addressables キー `Image/HomeBackground`（晴れ）または `Image/HomeBackgroundRain`（雨）から Sprite を非同期ロードし、カメラの視野全体を覆うように `SpriteRenderer` でフィットさせる（`destroyCancellationToken` でキャンセル対応）。

**HomePresenter（天気エフェクト）**：

- **雨**: `_rainEffectPrefab`（`vfx_Rain_01` Particle System）を `(0, 15, 0)` に Instantiate し、DarkOverlay（`rgba(0,0,0,0.4)` の全画面オーバーレイ）を `DisplayStyle.Flex` で表示
- **晴れ**: DarkOverlay は `display: none`（デフォルト）のまま、エフェクトなし

`vfx_Rain_01` の Particle System Renderer は `Order in Layer: 19` を設定し、Live2D Dog-kid（最大 `SortingOrder: 18`）より前面に雨粒を描画する。

### デッキ読み込み（HomePresenter）

`Construct()`（VContainer の `[Inject]` メソッド）内で `DeckModel.Clear()` → `DeckRepository.Load()` を呼び出し、PlayerPrefs に保存されたデッキを復元する。これにより、DeckBuilder を経由しないルート（Title → Home → CPU対戦）でもカスタムデッキが正しく使用される。

`Start()` ではなく `Construct()` で行う理由: `CommonSceneLoader.Awake()` が Common シーンを **非同期** でロードするため、直接シーンロード（テスト・デバッグ）では `Start()` が `BuildLifetimeScopes()`（VContainer injection）より先に実行されてしまう。`Construct()` は injection 完了タイミングで呼ばれるため常に安全。

### CPU 対戦開始時のセッションリセット（HomePresenter）

「バトル」ボタンから CPU 対戦を開始する際、`HomePresenter.StartCpuBattleAsync()` がまず `GameSessionModel.LeaveCurrentSessionAsync()` を呼んでからシーン遷移する。

これにより、オンライン対戦後に Home へ戻らずそのまま CPU 対戦を始めた場合でも `GameSessionModel.HasSession` が確実に `false` になり、`MainPresenter._isOnline` が `true` になってネットワーク待機ループに入る問題を防ぐ。セッションが null なら `LeaveCurrentSessionAsync()` は即リターンするため、初回起動時も問題ない。

---

## DeckBuilder シーン（デッキ構築）

### 概要

Home → DeckBuilder → Home の遷移フローでプレイヤーがデッキを組む画面（デッキ編集専用、ゲーム開始は Home から行う）。

### DeckModel（Common）

```
DeckModel   シングルトン。シーンをまたいでデッキ内容を保持
  Entries       IReadOnlyList<(string id, int cost)>。選択済みカードの ID・コストペアリスト
  CardIds       IReadOnlyList<string>。選択済みカード ID の順序付きリスト
  Count         現在の枚数
  TotalCost     現在のコスト合計
  IsReady       TotalCost == TargetCost (50) のとき true
  IsOver        TotalCost > TargetCost (50) のとき true（コストオーバー判定）
  Add(id, cost) カードをリストの末尾に追加する
  Remove(id)    末尾の該当 ID を1枚削除（先頭エントリを残して表示順を保持）。成功時 true を返す
  Clear()       デッキをリセット
```

### DeckBuilderPresenter のフロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. `DeckModel.Clear()` でリセットし、`DeckRepository.Load()` で前回保存デッキを復元
3. `CardDatabase.AllCards` をキャラ・スキル・イベントのセクション別にグリッド表示
4. カードをデッキパネルへドロップ → `DeckModel.Add(id, cost)` → デッキパネル更新
5. デッキパネルは同一カードを「サムネイル + カード名 ×枚数」形式で1行にまとめて表示
6. × ボタン → `DeckModel.Remove(id)` → デッキパネル更新
7. 「空にする」ボタン（デッキ枚数 > 0 のとき表示）→ `DeckModel.Clear()`
8. 「保存」ボタン → `DeckRepository.Save()` → Home シーンへの戻るボタンで退出

### MainPresenter との連携

Main シーンロード時、`DeckModel.Count > 0` なら `CardDatabase.BuildDeck(DeckModel.CardIds)` でプレイヤーデッキを構築。空の場合は `AllCards` 全体をフォールバックとして使用。CPU デッキは `CardStore.CpuDeck`（`CpuDeckSO`・Addressables キー `"Card/CpuDeck"`）から読み込み、カードIDリストが 1 件以上あればそれを使用し、なければ `AllCards` をフォールバックとして使用。

---

## Main シーン（カードゲーム盤面）

### カードシステム

```
CardData              抽象基底クラス。id / name / cost / Attack / Defense / Speed / Hp / image(Sprite)
  CharacterCardData   キャラカード。Attack / Defense / Speed / Hp 値を保持。キャラセットフェーズでのみスロットに配置（戦闘中の再配置不可）
  SkillCardData       技カード。Damage（= Attack）値を保持。戦闘前1フェーズで裏向きフィールドに配置
  EventCardData       イベントカード。EventType（None/AtkBoost/DefBoost/Draw/Negate/BanishChar）と EventValue を保持
                      戦闘前2フェーズで Ready 化、解決フェーズで効果を適用後に墓地へ

EventType         enum（EffectType.cs）。
                  None / AtkBoost（ATK を加算）/ DefBoost（DEF を加算）→ 効果は次の戦闘フェーズまで有効
                  Draw（EventValue 枚ドロー）/ Negate（チェーン上の次の効果を無効化）/ BanishChar（相手キャラをスロットから墓地へ）
                  Recover（自分の墓地の上から EventValue 枚を取り出し自デッキに加えてシャッフル）

CharacterCardSO / SkillCardSO / EventCardSO   各カード種別の ScriptableObject（インスペクター編集用）

CardDatabase      ScriptableObject。List<CardData> をインスペクターで編集、Dictionary でルックアップ
CardStore         IStartable。Addressables から Card.uxml と CardBack.png を非同期ロード

TurnPhase         enum。CharacterSet / Draw / PreBattle1 / PreBattle2 / Battle
```

### ビューコンポーネント

```
CardView          VisualElement サブクラス。Card.uxml をクローンしてデータをバインド
                  イラスト（Sprite）をカード全面背景の ImageArea に表示
                  カード情報は左上に縦一列表示：カード名 → コスト → （キャラ）HP → 攻撃力 → 防御 → 素早さ / （技）攻撃力
                  コスト：CostIcon.png にコスト数字を重ねた 36×36px バッジ
                  HP：HeartIcon.png に HP 数字を重ねた 36×36px バッジ（キャラカードのみ表示）
                  速さ：SpeedIcon.png に速さ数字を重ねた 36×36px バッジ（キャラカードのみ表示）
                  防御：ShieldIcon.png に防御数字を重ねた 36×36px バッジ（キャラカードのみ表示）
                  攻撃力：AttackIcon.png に攻撃力数字を重ねた 36×36px バッジ（キャラ・技カードに表示。イベントは非表示）
                  属性アイコンは UI 上非表示（データ・ゲームロジックは維持）
                  カード種別フレーム（キャラ=青 / 技=赤 / イベント=黄の 2px ボーダー）を ApplyTypeFrame(bool) で制御。裏向き時は非表示、ImageArea も非表示になりカード内容が見えない
                  State（Normal/Resolve）を持ち、Resolve 時は黄色枠で表示
                  IsOpponent フラグ（コンストラクタで設定）で相手カードかどうかを表す。バトル解決フェーズの isLocal 判定に使用
                  FlipAsync() で DOTween による裏返しアニメーション（表向き時にフレーム・ImageArea を表示、裏向き時に非表示）
                  AttachDragManipulator() / RemoveDragManipulator() でドラッグ着脱
CharacterSlotView VisualElement サブクラス。キャラカードを1枚配置するスロット
                  PlaceCard(CardView) で配置。既存カードがある場合は OnCardDisplaced イベントで通知してから差し替え
                  Defense / Speed / Hp プロパティで現在配置中カードの各値を返す（いなければ 0）
HandView          VisualElement サブクラス。手札を扇状に表示（60% スケール）
                  各カードをボトムセンター軸に最大 ±20° 回転 + 放物線アーク
                  ホバー時に DOTween で 1.5 倍スケールアップ＋BringToFront() で最前面表示
                  ドラッグ開始時に DragLayer へ移動、ドロップ失敗でスナップバック
                  ドロップ成功後に残りカードを DOTween でアニメーションしながら詰める
                  faceDown: true で裏向き表示、interactive: false でホバー・ドラッグ無効化（相手手札用）
                  isOpponent: true を渡すと内部で作成する CardView すべてに IsOpponent = true が伝播
                  AddCardAnimatedAsync() でデッキ位置から手札へのドロー演出（飛翔→フリップ）※プレイヤー用
                  AcceptCard(CardView) で既存 CardView をそのまま手札に取り込む（CPU ドロー演出後の手札追加用）
                  AddCardBackAsync() でフィールドから手札へ戻す飛翔アニメーション（戻るボタン用）
                  interactive: false のときフリップをスキップ（相手手札は裏向きのまま）
                  内部状態は HandCardEntry { Card, ScaleTween } の単一リストで管理（並列リスト廃止）
FieldView         VisualElement サブクラス。横長フィールドエリア（最大 5 枚、中央寄せ）
                  配置済みカードはドラッグ不可。TryGetCardAt() でワールド座標からカードを取得
DeckView          VisualElement サブクラス。デッキを積み重ねで表示（裏向き、60% スケール）
                  deck-view CSS クラス付き（背景・枠線は透明）
                  デッキ上方に HeartIcon.png + 残り枚数を重ねた 80×80px バッジを表示
                  DrawTop() で一番上の CardData を取り出してデッキから除去（枚数ラベルは更新しない）
                  RefreshCount() で枚数ラベルを現在の _deckCards.Count に同期（RunDrawPhaseAsync がドロー直前に呼ぶ）
                  TakeFromTop(n) で上から n 枚を CardView リストとして取り出す（ダメージアニメーション連携用）。ビジュアル除去は行わずリストのみ更新
                  OnCardRemovedVisually() でアニメーションが1枚取り出すたびにデッキ表示サイズと枚数バッジを1減算
CardDragManipulator  PointerManipulator サブクラス。DragLayer 対応のドラッグ実装
                     ドロップ成功判定は Func<Vector2, bool> OnDrop コールバックで外部委譲
AttackArrowManipulator  PointerManipulator サブクラス。フィールドカードに装着する攻撃矢印マニピュレーター
                        PointerDown で ArrowView を DragLayer に追加、Move で先端を更新、Up/CaptureOut で削除
                        Func<bool> CanStart で開始可否を判定（false なら矢印を出さない）
                        Func<Vector2, bool> OnAttackTarget でドロップ座標を通知（true = 矢印を残す）
                        ClearArrow() で矢印を手動削除
ArrowView            VisualElement サブクラス。Painter2D で攻撃矢印を描画（ベジェ曲線＋矢印先端）
                     StartPoint / EndPoint を更新するたびに再描画。PickingMode.Ignore で操作を透過
GraveyardView        VisualElement サブクラス。80×80px のアイコンボタン（GraveIcon.png + 枚数ラベル）
                     クリックで墓地カード一覧モーダルを開く（横スクロール・トップ/ボトムラベル付き）
                     AddCard(CardView) でカードを階層から切り離し、CardData を内部リストで管理
                     モーダルは mainRoot に追加してオーバーレイ表示。背景クリックで閉じる
                     茶色系の枠線（rgba(160,100,50)）と暗いベージュ背景
AttributeCompatibilityModal  純粋 C# クラス（VisualElement 非継承）。属性相性表モーダル
                     Show() でオーバーレイ + パネルを mainRoot に追加して表示。背景クリックで Hide()
                     Fire / Poison / Patchi の3行 × 属性・得意・苦手の3列テーブルをアイコンで表示
                     AttributeDatabaseSO.GetStrength() / GetWeakness() から各セルのアイコンを取得
                     アイコン未設定 or Strength/Weakness が None の場合は「—」テキストを表示
                     MainPresenter.BuildAsync で生成し、画面左下の「相性」ボタンにクリックハンドラを接続
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
OpponentGraveyardArea   （右上・180° 反転）
OpponentCharacterArea   （右上・90% スケール・180° 反転・BattleArea 上半分に縦中央揃え）CharacterSlotView
BattleArea              （top: 100px〜bottom: 144px）
  ├── OpponentFieldArea（上半分）
  └── PlayerFieldArea  （下半分）
PlayerCharacterArea     （左下・90% スケール・BattleArea 下半分に縦中央揃え）CharacterSlotView
DeckArea                （右下・60% スケール）
GraveyardArea           （左下）
HandArea                （画面下端・60% スケール）
ActionButtonsArea       （右下・bottom: 210px・アクションステージ中のみ表示・横並び）
  ├── BackButton        （左・ReturnButtn.png・直前の操作を取り消す）
  ├── PassButton        （PassButton.png・戦闘前フェーズで出さない選択）
  └── OkButton          （右・OKButtn.png・処理フェーズへ進む）
AttrCompatibilityButton （左下・bottom: 60px・72×72px 円形ボタン・クリックで属性相性モーダルを開く）
```

### MainPresenter の初期化フロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. DragLayer（全画面オーバーレイ、PickingMode.Ignore）を生成して MainRoot の最後尾に追加
3. **オンライン/オフラインモード分岐**（`GameSessionModel.HasSession` で判定）
   - **オンライン**: `NetworkGameService.PrepareDecksAsync` でデッキ交換プロトコルを実行し `OnlineInitialState` を取得。20 秒タイムアウト付き CTS でラップし、タイムアウト時は「対戦が成立しませんでした」モーダルを表示して Matching シーンへ誘導。相手手札はプレースホルダーで埋める
   - **オフライン**: `DeckModel` / `CpuDeckSO` からローカルでデッキを生成・シャッフル
4. 相手・自分ともに `HandView`・`DeckView`・`FieldView`・`GraveyardView`・`CharacterSlotView` を配置
5. ドロップハンドラを接続（プレイヤーフィールドへのドロップ、キャラスロットへのドロップ）
6. Resolve オーバーレイとターン告知オーバーレイを生成
7. OK/戻る/パスボタンにハンドラを接続
8. `UniTask.NextFrame` でレイアウト確定を待つ
9. 自分・相手ともに 5 枚を 0.12 秒ずつずらして `AddCardAnimatedAsync` を並走
10. `RunGameAsync(ct)` でゲームループ開始。先攻後攻は毎ターン イベントフェーズ冒頭で Speed 比較により決定

### MainPresenter のゲームループ（RunGameAsync）

ゲーム開始時に `RunCharacterSetPhaseAsync` を1度だけ実行し、その後ターンループに入る。

```
RunCharacterSetPhaseAsync  （ゲーム開始時1回のみ）
  → "キャラセットフェーズ" 告知
  → 両プレイヤーが同時操作
      CPU戦:    UniTask.WhenAll で PlayerCharSetLocalAsync と CpuCharSetAsync を並列実行
      オンライン: OnlineCharSetAsync（ホスト・クライアント共通）
                  receiveTask（WaitForOpponentCharSetAsync）をホットタスクとして並列起動
                  自分の入力完了後に SendCharSetAction で送信 → await receiveTask で相手受信完了を待機
  → Resolve アニメーション → 両スロットのキャラカードを同時に表向き（FlipAsync）
```

各ターンは以下のフェーズを順に実行する（`TurnPhase` enum に対応）。

```
RunDrawPhaseAsync
  → 両プレイヤーのデッキが 0 枚ならゲームオーバー判定
  → 両プレイヤーが同時に1枚ドロー
      オフライン: UniTask.WhenAll で AddCardAnimatedAsync と PlayCpuDrawAsync を並列実行
      オンライン: WaitForOpponentDrawAsync をホットタスクで起動 → ローカルドロー → SendDrawNotification → 受信完了を await → 相手ドロー演出

RunPreBattle1PhaseAsync
  → "準備フェーズ" 告知
  → 両プレイヤーが同時に技カードを1枚だけ裏向きでフィールドにセット（キャラカード不可）。パス時は PlayPassAnimationAsync で PASS 表示
      オフライン: UniTask.WhenAll(PlayerPreBattle1LocalAsync, CpuPreBattle1Async) で並列実行
      オンライン: OnlinePreBattle1Async（CharSet と同様の対称プロトコル）

先攻後攻決定（RunTurnAsync 内）
  → DetermineFirstMover() でスロットの Speed を比較
      Speed 差あり: 高い方が先攻
      Speed 同値:   初回ターンはランダム（CPU戦）/ OnlineInitialState.IsLocalFirst（オンライン）、以降は交互
                    (_lastSpeedTieBreakerWasLocal フィールドで前回結果を管理)
  → GameModel.SetInitialTurn(isLocalFirst) で IsLocalTurn をセット

RunPreBattle2PhaseAsync
  → "イベントフェーズ" 告知
  → 「あなたが先です」（水色）/ 「相手が先です」（赤）を表示
  → 先攻プレイヤーから交互にイベントカードをReadyにする（2連続パスで終了）。パス時は PlayPassAnimationAsync で PASS 表示
      オフライン: 自分 → WaitForPlayerPreBattle2InputAsync / CPU → CpuAgent で選択
      オンライン: 自分のターン → WaitForPlayerPreBattle2InputAsync → SendPreBattle2Action で送信
                  相手のターン → WaitForOpponentPreBattle2Async で受信 → PlayOpponentPreBattle2OnlineAsync でアニメーション（FlipAsync・ReadyCard・PayCost）

RunResolutionPhaseAsync
  → Readyカードを逆順で解決（"RESOLVE" 演出）
  → イベントカード効果を効果種別に応じて演出＋適用
     Draw:       先に PlayDrawEffectAsync で「DRAW +{value}」フローティングラベル（緑・上昇フェード）+ パーティクルを再生し、その後 ApplyEventEffectAsync でドロー実行
     BanishChar: 先に PlayBanishCharEffectAsync で対象キャラスロット上に「BANISH!」フローティングラベル（赤・上昇フェード）+ パーティクルを再生。ApplyEventEffectAsync 内で FlyCardToDestAsync によりキャラカードを墓地へ飛翔させてから AddCard
     AtkBoost:   ApplyEventEffectAsync 適用後に「ATK +{value}」フローティングラベル（金色・上昇フェード）+ パーティクルを PlayAtkBoostEffectAsync で同時再生
     DefBoost:   ApplyEventEffectAsync 適用後に「DEF +{value}」フローティングラベル（水色・上昇フェード）+ パーティクルを PlayDefBoostEffectAsync で同時再生
     Negate:     skipNextEffect = true をセット後、打ち消し対象（queue[i-1]）に PlayNegateEffectAsync で「NEGATE!」フローティングラベル（青・上昇フェード）+ パーティクルを再生。スキップされる側（skipNextEffect == true の card）は演出なしでスキップ

RunBattlePhaseAsync
  → "FIGHT" 告知
  → 全フィールドカードを同時に表向き
  → ATK = 技カードの Damage 合計 + AtkBoost（属性倍率なし）。スロット空の場合は ATK = 0
  → PlayAtkCounterAsync: 両サイドに 0→ATK のカウントアップ表示（常時）、**キャラスロット**に DEF アイコン＆DEF値をフェードイン表示
  → PlaySkillsAttackCharacterAsync (先攻): 先攻技カードを後攻キャラスロットへ突撃 → PlayDeckDamageAsync で先攻ダメージ適用（後攻デッキが 0 なら OnGameEnd(isLocalFirst) で先攻勝利）
  → PlaySkillsAttackCharacterAsync (後攻): 後攻技カードを先攻キャラスロットへ突撃 → PlayDeckDamageAsync で後攻ダメージ適用（先攻デッキが 0 なら OnGameEnd(!isLocalFirst) で後攻勝利）
```

**技カードスロット移動アニメーション（FlySkillToSlotAsync）:**
- `FlyCardToDestAsync` でキャラスロットの worldBound 中央へ飛翔
- 到着後に `position: Absolute, left:0, top:0, 100%×100%` でスロットに `Insert(0, card)`（キャラカードの下に描画）

**キャラ攻撃アニメーション（PlayCharacterSlotAttackAsync）:**
- スロットからキャラを一時取り出し DragLayer に絶対配置
- Phase 1 予備動作（0.15秒）: 後退 50px しながらデッキ方向へ向く（`Atan2` で回転角計算）
- Phase 2 突撃（0.65秒、InCubic）: 直線でデッキ中央へ加速
- Phase 3 ノックバック（0.15秒、OutQuad）: 着弾後 35px 跳ね返る
- アニメーション後、FlyCharToSlotAsync でスロットへ帰還

**ダメージ墓地送りアニメーション（PlayDeckDamageAsync）:**
- `TakeFromTop(n)` で取り出した CardView を 1 枚ずつ逐次処理（0.06 秒間隔）
- DragLayer に移動（UI Toolkit が DeckView から自動除去）直後に `OnCardRemovedVisually()` を呼んでデッキ表示を縮小
- DragLayer に絶対配置してデッキ位置から墓地アイコン中央へ飛翔（0.3 秒、InQuad）しながらスケールを 0 に縮小
- 到着後に `graveyard.AddCard(card)` で登録
- 呼び出し側が先攻→後攻の順に逐次呼び出す

**フライアニメーション（FlyCardToDestAsync）:**
- `worldBound` をカード除去前にキャプチャ
- DragLayer に絶対配置して DOTween で目標 `dest.worldBound.center` へ飛翔（0.3 秒）
- 到着後に `dest.Add(card)` して配置完了

### プレイヤー入力待ち（MainPresenter）

プレイヤーの操作入力は `UniTaskCompletionSource<CardView>` で待機し、ボタンイベントで完了させる。

入力状態は `StagedInput` ネストクラス（`Tcs` + `Card` フィールド）で管理。フェーズごとに `_charSetInput` / `_preBattleInput` / `_prepInput` の3インスタンスを保持する。

**キャラセットフェーズ（WaitForPlayerCharSetInputAsync）:**
```
_charSetInput.Tcs を作成して ShowActionButtons()
  → キャラカードをスロットへドロップ → _charSetInput.Card にセット → 即座に FlipAsync（裏向き）→ OK/戻るボタン表示
  → OK ボタン（TryTakeStagedInput）  → _charSetInput.Tcs.TrySetResult(card)
  → 戻るボタン（ステージ中）         → カードを手札へ返却（ReturnStagedCardToHand）
  → パスボタン（ステージなし）       → _charSetInput.Tcs.TrySetResult(null)
```

**戦闘前1フェーズ（WaitForPlayerPreBattle1TurnAsync）:**
```
_preBattleInput.Tcs を作成して ShowActionButtons()
  → キャラ/技カードをドロップ      → _preBattleInput.Card にセット → FlipAsync → OK/戻るボタン表示
  → OK ボタン（TryTakeStagedInput）→ _preBattleInput.Tcs.TrySetResult(card)
  → 戻るボタン（ステージ中）       → カードを手札へ返却（ReturnStagedCardToHand）
  → パスボタン（ステージなし）     → _preBattleInput.Tcs.TrySetResult(null)
戻り値 UniTask<bool>: true = カード配置, false = パス（呼び出し元でパス時に PlayPassAnimationAsync を呼ぶ）
```

**戦闘前2フェーズ（WaitForPlayerPreBattle2InputAsync）:**
```
_prepInput.Tcs を作成して ShowActionButtons()
  → イベントカードをドロップ        → _prepInput.Card にセット → OK/戻るボタン表示
  → OK ボタン（TryTakeStagedInput） → _prepInput.Tcs.TrySetResult(card)
  → 戻るボタン（ステージ中）        → カードを AddCardBackAsync で手札へ返却 → パスボタンに戻る
  → 戻る/パスボタン（ステージなし） → _prepInput.Tcs.TrySetResult(null) ← null = パス
```

ボタン表示状態は `UpdateStagedButtons(bool hasStaged)` で一元管理する。

### CPU 処理（MainPresenter + CpuAgent）

ゲームループの各フェーズで CPU の行動を決定する。

```
CpuAgent.ChooseCharacterSetCardIndex(hand) キャラセットフェーズ：キャラカードのインデックスを返す（なければ -1）
CpuAgent.ChoosePreBattle1CardIndex(hand)   戦闘前1フェーズ：キャラ優先→技カードのインデックスを返す（なければ -1）
CpuAgent.ChooseEventCardIndex(hand)        戦闘前2フェーズ：イベントカード1枚のインデックスを返す（なければ -1）
```

- キャラセットフェーズ: `ChooseCharacterSetCardIndex` でキャラカードを選択して `CharacterSlotView` へ裏向きで飛翔配置
- 戦闘前1フェーズ: `ChoosePreBattle1CardIndex` でキャラまたは技カードを選択し `FieldView` へ裏向きで飛翔配置（`CpuPreBattle1Async`）
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
  BeginBattle()            Phase → Battle
  EndTurn()                キューをクリア・Phase → Draw・IsLocalTurn を反転（次ターン開始前に SetInitialTurn が Speed 比較で上書きする）
```

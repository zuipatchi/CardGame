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
- 同じシーンを初期状態から作り直したい場合は `SceneTransitioner.Reload(Scenes target)` を使う（対象シーンをアンロード→再ロード。オンライン再戦で利用。NGO セッションは Common 常駐の NetworkManager が保持するため切断されない）
- 遷移時は `TransitionPresenter` が画面をフェードアウト→ロード→フェードインの演出を行う

### なぜアディティブか

シーン単位で DontDestroyOnLoad を使わず、Common シーンを「永続レイヤー」として扱うことで
サウンド・オプション・シーン遷移を全シーンで共有できる。

---

## 依存性注入（VContainer）

```
CommonLifetimeScope   全シーン共通のシングルトンを登録
  ├── DeckModel
  ├── DeckRepository
  ├── GameSessionModel
  ├── ModalStore
  ├── OptionPresenter
  ├── OptionModel
  ├── SoundPlayer
  ├── SoundStore
  ├── TransitionPresenter
  ├── SceneTransitioner
  └── UsernameRepository

TitleLifetimeScope         Title シーン固有のサービスを登録
  ├── GameStartButtonPresenter  (RegisterComponentInHierarchy)
  ├── AudioManager              (RegisterEntryPoint)
  ├── UsernameModalPresenter    (RegisterComponentInHierarchy)
  └── UsernameModalService      (RegisterEntryPoint)
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
  AutoOk:    ReactiveProperty<bool>   カード配置後に自動で OK するか（デフォルト ON）
  AutoPass:  ReactiveProperty<bool>   プレイ可能カードがなければ自動パスするか（デフォルト ON）

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

### MonoBehaviour のインジェクションタイミング

`CommonSceneLoader.Awake()` は `async void` であり、`await UniTask.NextFrame()` の後に `BuildLifetimeScopes()` を呼ぶ。
そのため **MonoBehaviour の `Awake/OnEnable/Start` が呼ばれる時点ではインジェクションが完了していない**。

| コールバック | インジェクト済みフィールドを使えるか |
|---|---|
| `Awake` / `OnEnable` / `Start` | **不可**（injection 前） |
| `[Inject] Construct(...)` | 可（injection と同時に呼ばれる） |
| `IAsyncStartable.StartAsync()` | 可（Build 完了後に VContainer が呼ぶ） |
| ユーザー操作イベントコールバック | 可（injection 完了後に発火） |

「シーン起動時にインジェクト済みフィールドを使って何かしたい」場合は、`IAsyncStartable` を実装した純粋 C# サービスを `RegisterEntryPoint` で登録し、そこから MonoBehaviour の public メソッドを呼ぶ。
`UsernameModalService` がこのパターンの例（起動時にユーザーネーム未登録ならモーダルを表示）。

```csharp
// 例: AudioManager (Title シーン)
public async UniTask StartAsync(CancellationToken cancellation = default)
{
    await _soundStore.Loaded;
    _soundPlayer.PlayBGM(_soundStore.MaouOrchestra);
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
| `DeckBuilder` | `Assets/Scripts/DeckBuilder/` | VContainer / UniTask / Common / Main |
| `CpuDeckBuilder` | `Assets/Scripts/CpuDeckEditor/` | VContainer / UniTask / Common / Main / DeckBuilder（Editor 専用） |

- `Title` / `Home` / `Matching` / `Main` は `Common` に依存し、逆方向の依存は禁止
- `autoReferenced: true` のため既存コードへの影響なし

---

## アセット管理（Addressables）

```
Assets/AddressableAssets/
  ├── Card/        Card.uxml（カードテンプレート）
  ├── Icon/        HeartIcon.png（HP バッジ用）、AttackIcon.png（攻撃力バッジ・キャラ8体勝利の紋章用）、GraveIcon.png（墓地枚数バッジ・デッキ切れ勝利の紋章用）、MedalIcon.png（勝利点表示・勝利点勝利の紋章用）、CharaIcon.png（キャラカード種別アイコン）、SkillIcon.png（技カード種別アイコン）
  ├── Image/       CardBack.png（カード裏面画像）、NamePlate.png（カード名プレート背景）、Card*.png（カードイラスト）、BattleField.png（盤面背景）、OKButton.png（OKボタン画像）、returnButton.png（戻るボタン画像）、PassButton.png（パスボタン画像）、HomeBackground.png（Home 画面背景・晴れ）、HomeBackgroundRain.png（Home 画面背景・雨）
  ├── Modal/       Modal.uxml
  └── Sound/       AudioClip
```

- `SoundStore` が BGM・SE クリップをロード（`MaouOrchestra`, `MainBGM`, `KoharuIzm`, `EnterSE`, `Enter2SE`, `Cancel1SE`）
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
- FadeMotionList アセットは `DogKidFadeMotionListSetup`（エディタメニュー `Live2D/Setup Dog-kid FadeMotionList`）でセットアップし Dog-kid プレハブに割り当て済み（ただし `CubismFadeController` 自体を無効化しているため、モーション再生はフェードを介さず `Animator.Play` で直接行われる）

### 食べ物スポーン・食事演出（HomeFoodSpawner / HomeLive2DPresenter）

画面左半分をクリック（New Input System: `Mouse.current.leftButton.wasPressedThisFrame`）すると、クリック座標に Food Live2D プレハブをインスタンス化する。

- インスタンス化直後に `CubismFadeController` と `CubismParameterStore` を無効化する（FadeMotionList 未設定による NullRef と、RestoreParameters によるアニメーション値上書きを防ぐため）
- 犬は食べ物の座標へ歩行（Walk アニメーション）し、到達後に犬と食べ物の Eat アニメーションを同時再生する（`UniTask.WhenAll` で犬 Eat 時間と Food Eat 終了を並行待機）
- Food の `normalizedTime >= 1f` を毎フレームポーリングして Eat アニメーション終了を検出し、`Destroy` で削除する
- Food プレハブは `CubismRenderer._localSortingOrder = 50` により Dog-kid より前面に描画される

### 背景・天気（HomeBackgroundPresenter / HomePresenter）

天気は **対戦結果連動**。`GameSessionModel.ShouldRainOnNextHome`（bool）フラグをシーン間で共有し、`HomePresenter.Construct()` で読み取って `HomeBackgroundPresenter.IsRainy` にセットする。

**フラグのライフサイクル:**

- `MainPresenter.OnGameEnd(playerWins)`: `playerWins == false`（敗北）のとき `true`、それ以外（勝利・引き分け）は `false` にセット
- `HomePresenter.Construct()`: フラグが `true` のとき `IsRainy = true` にセットし、フラグを `false` にリセット（一度雨を表示したら次回以降は晴れ）

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
  TotalCost     現在のコスト合計（上限なし・参考値）
  MaxCards      定数 30。有効なデッキ枚数
  IsReady       Count == MaxCards (30) のとき true
  IsOver        Count > MaxCards (30) のとき true（枚数オーバー判定）
  IsValid       IsReady のとき true（デッキ有効判定。コスト上限はない）
  Add(id, cost)              カードをリストの末尾に追加する
  Remove(id)                 末尾の該当 ID を1枚削除する。成功時 true を返す
  Clear()                    デッキをリセット
  Reorder(orderedIds)        指定した ID 順にグループを並び替え。未指定 ID は末尾に元の相対順で残る
  SortById()                 C→S→E のプレフィックス順・同プレフィックス内は ID 文字列順でインプレースソート
```

### DeckBuilderPresenter のフロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. `DeckModel.Clear()` でリセットし、`DeckRepository.Load()` で前回保存デッキを復元
3. `CardDatabase.AllCards`（ゲームで使用しないカードを除いた集合）をキャラ・スキル・イベントのセクション別にグリッド表示
4. カードをデッキパネルへドロップ → `DeckModel.Add(id, cost)` → デッキパネル更新
5. デッキパネルは同一カードを「サムネイル + カード名 ×枚数」形式で1行にまとめて表示。左端の `≡` ハンドルをドラッグ&ドロップで行を並び替え可能（`DeckModel.Reorder` を呼び出し自動保存）
6. × ボタン → `DeckModel.Remove(id)` → デッキパネル更新
7. 「ID順」ボタン → `DeckModel.SortById()` → デッキパネル更新 → 自動保存
8. 「空にする」ボタン（デッキ枚数 > 0 のとき表示）→ 確認ダイアログを表示（「はい」で `DeckModel.Clear()` / 「いいえ」またはオーバーレイ背景クリックでキャンセル）
8. 「保存」ボタン → `DeckRepository.Save()` → Home シーンへの戻るボタンで退出

### MainPresenter との連携

Main シーンロード時、`DeckModel.Count > 0` なら `CardDatabase.BuildDeck(DeckModel.CardIds)` でプレイヤーデッキを構築。空の場合は `AllCards` 全体をフォールバックとして使用。CPU デッキは `CardStore.CpuDeck`（`CpuDeckSO`・Addressables キー `"Card/CpuDeck"`）から読み込み、カードIDリストが 1 件以上あればそれを使用し、なければ `AllCards` をフォールバックとして使用。

`CardData._excludeFromGame`（カードエディタの「ゲームで使用」トグルで OFF）が立ったカードは、`CardDatabase` の ID 辞書・`AllCards` の両方から除外され、デッキ構築のプール・対戦・`BuildDeck` での ID 解決すべてから外れる（調整中・未完成カードを隠す用途）。

---

## Main シーン（カードゲーム盤面）

### カードシステム

```
CardData              抽象基底クラス。id / name / cost / Attack / Hp / Attribute / image(Sprite) / FlavorText（世界観テキスト。効果に影響せず詳細モーダル最下部に表示）
  CharacterCardData   キャラカード。Attack / Hp / Attribute 値を保持。メインフェーズで表向きにフィールドへ配置
                      登場時効果として EffectTrigger / EffectType（EventType 流用）/ EffectValue / Description（説明テキスト）を保持
  EventCardData       イベントカード。EventType / EventValue / Attribute / Description を保持。メインフェーズで即時解決し墓地へ

CardAttribute     enum（CardAttribute.cs）。Red / Blue / Green / Yellow / Black / Purple / White

CharacterEffectTrigger  enum（CharacterEffectTrigger.cs）。キャラの効果発動タイミング。
                  None / OnEnter（登場時）/ OnAttack（攻撃時）/ OnDestroy（破壊時。戦闘・DamageEnemy/DamageAllEnemies での撃破・BanishChar での除去で発動）

EventType         enum（EffectType.cs）。
                  None / AtkBoost / DefBoost（enum に定義のみ。現在は未使用）
                  Draw（EventValue 枚ドロー）/ Negate（enum に定義のみ。現在は未使用）
                  BanishChar（相手キャラをフィールドから墓地へ）
                  Recover（自分の墓地の上から EventValue 枚を取り出し自デッキに加えてシャッフル）
                  Switch（自分のキャラを手札に戻して別キャラを配置）
                  Evolve（自分のキャラを墓地に送り上位キャラと交換）
                  CostBoost（コスト支払い時に倍化・属性連動）/ DamageAllEnemies（敵全体にダメージ）
                  DamageEnemy（敵を値1体選び値2ダメージ）/ SummonChar（指定キャラを召喚）
                  SummonFromDeckByKeyword（自身の特徴を持つキャラをデッキから1枚選んで召喚）
                  CopyFieldChar（自分の場のキャラを1体選び、そのコピーをN体出す。バフ・現在HP込み）
                  GainVPPerGreenGrave（自分の墓地の緑カード枚数だけ勝利点を加算）
                  HealAllAllies（自フィールド全キャラのHPを回復・値0で全回復）
                  NextCardCostFree（次の1枚を無料）
                  ※固定値の勝利点付与は EventType ではなく全カード共通の VictoryPointBonus 付帯値で行う
                  Bounce（敵を値1体選び所有者の手札へ戻す。DamageEnemy と対象選択を共用）

EffectHandler / EffectCatalog   効果1種＝EffectHandler 派生1クラス（Assets/Scripts/Main/Card/Effects/）。演出＋盤面適用（ApplyAsync）とエディタ用メタデータ（効果テキスト BuildBody・値ラベル Values・キャラ/イベント可否）を1クラスに集約。
                  EffectCatalog が Main アセンブリを走査してハンドラを自動登録（起動時に全 EventType の網羅を検証）。MainPresenter（ResolveEventCardEffectAsync / ResolveCharacterTriggeredEffectAsync）と CardEditorWindow はカタログ経由でハンドラを引くだけで、効果追加時に switch を編集しない。盤面操作は MainPresenter の internal building-block メソッドをハンドラから呼ぶ。

CharacterCardSO / EventCardSO   各カード種別の ScriptableObject（属性ごとに分割。Assets/Data/{属性}/ に配置）
                  SO ごとに Attribute を持ち、所属カードの属性を一括設定する（カードの Attribute はインスペクタで読み取り専用＝ReadOnly 属性）
                  ID は OnValidate で自動採番（CardIdAutoAssigner・エディタ専用）。規則は C{(属性番号)×1000+連番}（白=C1001/青=C2001…、属性番号=白1/青2/緑3/黄4/赤5/黒6/紫7。E も同様）。属性別 SO 間でも一意・"C{番号}" 形式（SummonChar 互換）。1属性最大999枚
                  既存の単一 SO を属性別へ分割する移行ツール：メニュー Card → 属性別SOに分割（CardSoAttributeSplitter）

CardDatabase      ScriptableObject。属性別 SO の配列 _characterCardSets / _eventCardSets を集約し、Dictionary でルックアップ
CardStore         IStartable。Addressables から Card.uxml と CardBack.png を非同期ロード

TurnPhase         enum。Draw / Main
WinRule           static クラス。共通の勝利条件の純ロジック（デッキ切れ=デッキ0 / 勝利点20 / キャラ8体）と定数
WinReason         enum。勝因の種別（DeckOut / VictoryPoints / FieldChars）。勝敗演出の勝因表示に使う
```

### ビューコンポーネント

```
CardView          VisualElement サブクラス。Card.uxml をクローンしてデータをバインド
                  イラスト（Sprite）をカード全面背景の ImageArea に表示
                  カード情報は左上に表示：カード名 → アイコン群（コスト → （キャラ）攻撃力 → HP → 守護 → 速攻 → 飛行）
                  アイコン群（game-card__icons）は縦並びだが、表示中のアイコンが4つを超えると flex-wrap で右隣の2列目へ折り返す（max-height 170px。display:none のアイコンは数えない）。カードの高さは伸びない
                  コスト：CostIcon.png にコスト数字を重ねた 36×36px バッジ
                  HP：HeartIcon.png に HP 数字を重ねた 36×36px バッジ（キャラカードのみ表示）。ランタイムの残 HP は `CurrentHp` フィールドで管理し、`TakeDamage(n)` で同期的に減算、`TakeDamageAsync(n, ct)` でアニメーション付き減算（ラベルは 0 未満表示しない）
                  攻撃力：AttackIcon.png に攻撃力数字を重ねた 36×36px バッジ（キャラカードに表示。イベントは非表示）

                  属性カラーフレーム（Red=赤 / Blue=青 / Green=緑 / Yellow=黄 / Black=黒紫 / Purple=紫 / White=白灰の 2px ボーダー）を ApplyTypeFrame(bool) で制御。裏向き時は非表示、ImageArea も非表示になりカード内容が見えない
                  State（Normal/Resolve）を持つ（Resolve はイベント効果処理中を示す内部状態）
                  IsOpponent フラグ（コンストラクタで設定）で相手カードかどうかを表す。メインフェーズの isLocal 判定に使用
                  FlipAsync() で DOTween による裏返しアニメーション（表向き時にフレーム・ImageArea を表示、裏向き時に非表示）
                  AttachDragManipulator() / RemoveDragManipulator() でドラッグ着脱
HandView          VisualElement サブクラス。手札を扇状に表示（60% スケール）
                  各カードをボトムセンター軸に最大 ±20° 回転 + 放物線アーク
                  ホバー時に BringToFront() で最前面表示（スケールアップなし）
                  ドラッグ開始時に DragLayer へ移動、ドロップ失敗でスナップバック
                  ドロップ成功後に残りカードを DOTween でアニメーションしながら詰める
                  faceDown: true で裏向き表示、interactive: false でホバー・ドラッグ無効化（相手手札用）
                  isOpponent: true を渡すと内部で作成する CardView すべてに IsOpponent = true が伝播
                  AddCardAnimatedAsync() でデッキ位置から手札へのドロー演出（飛翔→フリップ）※プレイヤー用
                  AcceptCard(CardView) で既存 CardView をそのまま手札に取り込む（CPU ドロー演出後の手札追加用）
                  AddCardBackAsync() でフィールドから手札へ戻す飛翔アニメーション（戻るボタン用）
                  interactive: false のときフリップをスキップ（相手手札は裏向きのまま）
                  内部状態は HandCardEntry { Card } の単一リストで管理（並列リスト廃止）
FieldView         VisualElement サブクラス。横長フィールドエリア（最大 5 枚、中央寄せ）
                  キャラカード・イベントカードを直接配置する。1ターンに1枚まで出せる（CharacterSlotView は廃止）
                  Characters プロパティで CharacterCardData のカードのみを抽出可能
                  PlaceCard(CardView) で配置（ドラッグ不可に設定・CurrentHp をリセット）、RemoveCard(CardView) で除去
                  TryGetCardAt(Vector2) でワールド座標から CardView を取得（戦闘攻撃対象選択用）
DeckView          VisualElement サブクラス。デッキを積み重ねで表示（裏向き、60% スケール）
                  deck-view CSS クラス付き（背景・枠線は透明）
                  デッキ中央に HeartIcon を背面に敷き、その上に残り枚数の数字（黒アウトライン付き）を 80×80px バッジ領域に重ねて表示
                  DrawTop() で一番上の CardData を取り出してデッキから除去（枚数ラベルは更新しない）
                  RefreshCount() で枚数ラベルを現在の _deckCards.Count に同期（ドロー処理・デッキ攻撃のミルが各除去後に呼ぶ）
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
VictoryPointsView    VisualElement サブクラス。勝利点表示（MedalIcon.png + 数字）。共通の勝利条件のため
                     ゲーム開始時から常時表示。相手用は OpponentFieldArea 右上・自分用は PlayerFieldArea 左下に絶対配置
                     AddPoints / SetDisplayedPoints で論理値と表示を分離（カウントアップ演出用）

```

### フィールドレイアウト

画面中央の BattleArea（top: 100px〜bottom: 144px）を上下に分割。

```
BattleArea
  ├── OpponentFieldArea（上半分、gap: 32px で分離）── FieldView（相手フィールド）＋ VictoryPointsView（右上・相手勝利点）
  └── PlayerFieldArea  （下半分）── FieldView（自分フィールド）＋ VictoryPointsView（左下・自分勝利点）
```

- 手札からドロップできるのは PlayerFieldArea のみ
- フィールドエリアは `overflow: visible`（カードが見切れないよう）

### 画面レイアウト全体

```
OpponentHandArea        （画面上部・180° 反転・裏向き・非インタラクティブ）
OpponentDeckArea        （左上・60% スケール・180° 反転）
OpponentGraveyardArea   （右上・180° 反転）
BattleArea              （top: 100px〜bottom: 144px）
  ├── OpponentFieldArea（上半分）── FieldView（相手フィールド）＋ VictoryPointsView（右上）
  └── PlayerFieldArea  （下半分）── FieldView（自分フィールド）＋ VictoryPointsView（左下）
DeckArea                （右下・60% スケール）
GraveyardArea           （左下）
HandArea                （画面下端・60% スケール）
ActionButtonsArea       （右下・bottom: 210px・アクションステージ中のみ表示・横並び。コンテナは PickingMode.Ignore＝透明な余白が下のカードのドラッグ（攻撃矢印）を奪わないようにし、子ボタンのみがクリックを拾う）
  ├── BackButton        （左・returnButton.png・直前の操作を取り消す）
  ├── PassButton        （PassButton.png・戦闘前フェーズで出さない選択）
  └── OkButton          （右・OKButton.png・処理フェーズへ進む）
AttrCompatibilityButton （左下・bottom: 60px・72×72px 円形ボタン・クリックで属性相性モーダルを開く）
```

### MainPresenter の初期化フロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ
2. DragLayer（全画面オーバーレイ、PickingMode.Ignore）を生成して MainRoot の最後尾に追加
3. **オンライン/オフラインモード分岐**（`GameSessionModel.HasSession` で判定）
   - **オンライン**: `NetworkGameService.PrepareDecksAsync` でデッキ交換プロトコルを実行し `OnlineInitialState` を取得。20 秒タイムアウト付き CTS でラップし、タイムアウト時は「対戦が成立しませんでした」モーダルを表示して Matching シーンへ誘導。相手手札はプレースホルダーで埋める
   - **オフライン**: `DeckModel` / `CpuDeckSO` からローカルでデッキを生成・シャッフル
4. 相手・自分ともに `HandView`・`DeckView`・`FieldView`・`GraveyardView`・`VictoryPointsView` を配置
5. ドロップハンドラを接続（プレイヤーフィールドへのドロップ）
6. Resolve オーバーレイとターン告知オーバーレイを生成
7. OK/戻る/パスボタンにハンドラを接続
8. `UniTask.NextFrame` でレイアウト確定を待つ
9. 自分・相手ともに 5 枚を 0.12 秒ずつずらして `AddCardAnimatedAsync` を並走
10. `RunGameAsync(ct)` でゲームループ開始。先攻後攻はゲーム開始時のコイントスで決定し、以降は1ターン交互に入れ替わる

### MainPresenter のゲームループ（RunGameAsync）

各ターンは Draw → Main の2フェーズを実行する（`TurnPhase` enum に対応）。

```
InitializeFirstTurnAsync  （ゲーム開始時に1度だけ実行）
  → コイントスアニメーション（PlayCoinTossAsync）で先攻後攻を決定
  （補正ドローなし。先攻有利は先攻初手のドローなしで補正する）

RunTurnAsync  （各ターンの先頭）
  → PlayTurnStartAnnouncementAsync: 自分の番は "YOUR TURN"、相手の番は "ENEMY TURN" を告知

RunDrawPhaseAsync
  → 手番プレイヤーのみ3枚ドロー（DrawPhaseCardCount）。ただし先攻の初手（GameModel.TurnNumber == 1）はドローなし
      ローカルターン:  ドローアニメーション → SendDrawNotification で相手に通知（0枚でも同期のため送信）
      相手ターン:     WaitForOpponentDrawAsync でドロー通知受信 → 相手ドロー演出

RunMainPhaseAsync  （手番プレイヤーは EndButton／Pass を出すまで制限なく行動を繰り返すループ）
  → 各メインフェーズ開始時に攻撃済み記録をクリアし、場のキャラを「召喚酔いなし」として記録（ReseasonChars）
  → ローカル(RunLocalMainLoopAsync): WaitForPlayerMainActionAsync で入力待ちを繰り返す
                  手札キャラ → フィールドへドロップ（PlaceChar）
                  手札イベント → フィールドへドロップ（PlayEvent）→ 即時解決
                  攻撃可能キャラ（attackable-char でハイライト） → AttackArrowManipulator で矢印を引いて攻撃（Attack）
                  EndButton → ターン終了（オンラインは Pass を相手へ送る）
      CPU(RunCpuMainLoopAsync):  CpuChooseMainAction() で攻撃優先・次にキャラ・イベント、無ければ終了を選択し繰り返す
      オンライン(RunOnlineOpponentMainLoopAsync): WaitForOpponentMainActionAsync で相手アクションを Pass 受信まで繰り返し受信
  → 各キャラの攻撃はターン1回まで（_attackedThisTurn）、このターン登場したキャラは攻撃不可（召喚酔い）

ExecuteAttackAsync  （Attack アクション実行時）
  → ResolveCharacterAttackEffectAsync: 攻撃キャラの OnAttack 効果を発動（Draw・BanishChar）
  → PlayCardChargeAsync: 攻撃キャラのコピーが「ウィンドアップ → 突撃 → ノックバック → 元位置へ戻る」演出
                          演出中は元カードを visibility: hidden で非表示
  → ダメージ = ATK。0 なら "NO DAMAGE" 表示
  → damage > 0: PlayParticleAtCardAsync（ヒットエフェクト）→ TakeDamageAsync（HP アニメーション）
  → キャラ破壊: PlayCharDestroyEffectAsync → FlyToGraveyardAsync
```

**フライアニメーション（FlyCardToDestAsync）:**
- `worldBound` をカード除去前にキャプチャ
- DragLayer に絶対配置して DOTween で目標 `dest.worldBound.center` へ飛翔（0.3 秒）
- 到着後に `dest.Add(card)` して配置完了

### プレイヤー入力待ち（MainPresenter）

プレイヤーの操作入力は `UniTaskCompletionSource` で待機し、ボタンイベントや UI 操作で完了させる。

**メインフェーズ（WaitForPlayerMainActionAsync）:**
```
_mainActionTcs を作成し、自軍フィールドキャラに AttackArrowManipulator を取り付けて ShowActionButtons()
  → 手札キャラをフィールドへドロップ  → _mainStagedCard にセット（PlaceChar）
                                        → OK ボタン → _mainActionTcs.TrySetResult(PlaceChar)
                                        → 戻るボタン → ReturnStagedCardToHand
  → 手札イベントをフィールドへドロップ → _mainStagedCard にセット（PlayEvent）
                                        → OK / 戻るボタン同様
  → AttackArrowManipulator でドラッグ  → 相手キャラ or デッキへ → _mainActionTcs.TrySetResult(Attack)
  → パスボタン（ステージなし）         → _mainActionTcs.TrySetResult(Pass)
finally: _mainActionTcs をクリア、AttackArrowManipulator を除去
```

**フィールドキャラ選択（WaitForPlayerFieldCharSelectionAsync）:**
```
_fieldCharSelectionTcs を作成し、自軍フィールドキャラに "selectable-char" CSS クラスを付与
  → プレイヤーがクリック → _playerFieldView.OnCardClicked → _fieldCharSelectionTcs.TrySetResult(card)
finally: "selectable-char" クラスをクリア
```
イベント効果（Switch / Evolve）でフィールドに複数キャラがいる場合に呼ばれる。

ボタン表示状態は `UpdateStagedButtons(bool hasStaged)` で一元管理する。

### CPU 処理（MainPresenter + CpuAgent）

メインフェーズで `CpuChooseMainAction()` が行動を選択する。

```
CpuAgent.ChooseCharacterSetCardIndex(hand, canAfford)   配置するキャラカードのインデックスを返す（なければ -1）
CpuAgent.ChooseEventCardIndex(hand, canAfford)          使うイベントカードのインデックスを返す（なければ -1）
```

`canAfford(i)` は `hand[i]` のコストを支払えるかの判定。`MainPresenter.CpuCanAffordCost()` を渡し、自身を除いた手札の支払い可能量（`CostPaymentValue` 合計）がコストに満たないカードは選ばない（ローカルプレイヤーと同じくコストの踏み倒しを禁止）。

キャラ攻撃・デッキ攻撃の対象選択は守護・飛行を考慮するため `MainPresenter.CpuChooseMainAction()` 側で `CanAttackChar` / `CanAttackDeck` を使って解決する（合法な対象を持つ攻撃者の中で最高ATK→対象は最低ATK）。

優先順位: lethal デッキ攻撃（ATK ≥ 相手デッキ枚数で引き切らせられる） → キャラ攻撃（合法な対象があれば） → チップミル（デッキ攻撃。キャラ攻撃対象がない場合） → キャラ配置 → イベント使用 → パス

### ゲームロジック（GameModel）

ゲームルールの詳細は [docs/rules.md](rules.md) を参照。

```
GameModel         フェーズ遷移と手番管理
  Phase           現在フェーズ（TurnPhase enum: Draw / Main）
  IsLocalTurn     ローカルプレイヤーの手番か

  SetInitialTurn(isLocalFirst)  ゲーム開始時に先攻後攻をセット
  BeginMain()                   Phase → Main
  EndTurn()                     Phase → Draw・IsLocalTurn を反転
```

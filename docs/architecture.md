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
- 同じシーンを初期状態から作り直したい場合は `SceneTransitioner.Reload(Scenes target)` を使う（対象シーンをアンロード→再ロード。オンライン再戦で利用。NGO セッションは Common 常駐の NetworkManager が保持するため切断されない）。`Transit` は新シーンをロードしてから旧シーンをアンロードするためカメラが途切れないが、`Reload` はアンロード→ロードの順でカメラが 0 個になる瞬間がある（Common にカメラが無いため「No Cameras Rendering」が一瞬出る）。これを防ぐため、黒フェード中だけ画面を黒く塗る一時カメラ（`DontDestroyOnLoad`・`cullingMask=0`・最背面）を立てて隙間を埋める
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
  ├── TutorialModel       チュートリアル起動情報（消費型フラグ＋TutorialId）
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

- BGM: `AudioSource.loop = true`、`PlayBGM()` で差し替え・`StopBGM()` で停止（シーン間で BGM を引き継ぐ／止め分けるために使う）。再生時に曲ごとの音量倍率（下記）を AudioSource の音量へ畳み込む
- SE: `PlayOneShot(clip, volumeScale)` で重ね再生。`volumeScale` には SE ごとのラウドネス正規化倍率（下記）を渡す
- ボイス（フレーバーテキスト読み上げ）: 専用 AudioSource で `PlayVoice()`（`PlayOneShot()`）。前の読み上げが終わる前でも止めずに重ねて鳴らす（一時的に複数同時に流れてもよい）
- 持続 SE（途中で止めたい効果音）: 専用 AudioSource（`loop = true`）で `PlayLoopSE()` 再生・`StopLoopSE()` 停止。`PlayOneShot` は途中停止できないため、演出の長さに合わせて鳴らし切る用途に使う（例: コイントス・コインドローの回転音を回転開始から回転終了まで鳴らす）。SE ラウドネス正規化倍率は再生時に AudioSource の音量へ畳み込む
- 音量は `OptionModel.MasterVolume / BGMVolume / SEVolume / VoiceVolume` (0–1) を ReactiveProperty で管理（ボイスは SE とは独立した音量）
- マスター音量は BGM/SE/ボイスすべてに掛かる全体音量。実効倍率は値×2（スライダー 0.5 で等倍）で、各チャンネル音量との積で実音量が決まる。`SoundPlayer` は `MasterVolume.CombineLatest(各チャンネル音量)` を Subscribe して反映する
- `SoundPlayer` は音量変化を Subscribe して AudioSource に即時反映
- **SE ラウドネス正規化**: SE クリップごとに収録音量がバラバラなので、`SoundStore` がロード完了時に各 SE の体感音量（約100msブロックごとの最大 RMS）とピークを解析し、全 SE の中央値を目標に「同じ聞こえ方になる音量倍率」を算出する（`NormalizeSeVolumes`）。ピークが 1.0 を超えないようヘッドルームで上限を制限し、最後に倍率を `[0.1, 4.0]` でクランプ。`SoundPlayer.PlaySE` が `SoundStore.GetSeVolumeScale(clip)` で倍率を取得して `PlayOneShot` の `volumeScale` に渡すため、SE スライダー（`SEVolume`）とは独立して乗算され、スライダー位置に関わらず SE 同士のバランスは保たれる
  - クリップの Load Type が `Decompress On Load` 以外（`Compressed In Memory` 等）だと `AudioClip.GetData` で波形を読めず、そのクリップは等倍（1.0）にフォールバックする（その場合は警告ログを出す）
  - 自動補正で合わない尖った単発音などは `SoundStore.ManualSeAdjust`（クリップ名 → 追加倍率）で耳に合わせて微調整する
- **BGM 曲ごと音量調整**: BGM は曲数が少なく長尺なので自動正規化（全波形のRMS解析＝メモリ消費大）はせず、`SoundStore.ManualBgmAdjust`（クリップ名 → 音量倍率・未登録は1.0）で曲間の音量差を耳合わせで揃える。`SoundPlayer.PlayBGM` が `SoundStore.GetBgmVolumeScale(clip)` で倍率を取得し、`BGMVolume / 2 × 倍率` を AudioSource に設定する（再生中に BGM スライダーを動かしても倍率を保ったまま追従）。キーは `AudioClip.name`（アセットのファイル名。Addressable のアドレス名とは異なる点に注意）
- 読み上げ音声は事前生成した WAV を Addressables アドレス `Voice/{CardId}` から `FlavorVoiceStore` がオンデマンドでロード・キャッシュ（未生成カードは null＝無音）
- 読み上げの話者は `CardData._voiceSpeaker`（VOICEVOX speaker ID。0＝生成ツールの既定話者）でカードごとに指定でき、声は生成時に WAV へ焼き込まれる（ランタイムは話者を意識しない）

> BGM/SE は `volume = v / 2` としている。OptionModel の値 1.0 がデフォルトの AudioSource
> 最大音量の半分に相当するように抑えるため。
> ボイス（読み上げ）だけは小さく埋もれがちなので半減せず `volume = v`（最大 1.0）で再生し、全体的に大きくしている。

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
  ├── Icon/        HeartIcon.png（HP バッジ用）、AttackIcon.png（攻撃力バッジ・キャラ8体勝利の紋章用）、GraveIcon.png（墓地枚数バッジ・デッキ切れ勝利の紋章用）、Medal1Icon.png〜Medal5Icon.png（勝利点表示・勝利点勝利の紋章用。勝利点獲得フロートは得点数 1〜5 に対応する画像を表示し、0 や 6 以上は Medal1Icon。カウンター・紋章など固定箇所は Medal1Icon）、CharaIcon.png（キャラカード種別アイコン）、SkillIcon.png（技カード種別アイコン）
  ├── Image/       CardBack.png（カード裏面画像）、NamePlate.png（カード名プレート背景）、Card*.png（カードイラスト）、BattleField.png（盤面背景）、OKButton.png（OKボタン画像）、returnButton.png（戻るボタン画像）、PassButton.png（パスボタン画像）、HomeBackground.png（Home 画面背景・晴れ）、HomeBackgroundRain.png（Home 画面背景・雨）
  ├── Modal/       Modal.uxml
  └── Sound/       AudioClip
```

- `SoundStore` が BGM・SE クリップをロード（BGM: `MainBGM`, `MaouOrchestra`, `MaouAcoustic`（デッキ構築シーン）, `MaouCyber`（マッチングシーン）, `KoharuIzm` / SE: `EnterSE`, `Enter2SE`, `Enter3SE`（マリガン YES/NO・OK・END ボタン・勝敗画面の再戦/ホームに戻るボタン）, `Cancel1SE`（Main の戻るボタン）, `ResultSE`, `AnalysisSE`（デッキ分析）, `WinSE`/`LoseSE`（勝敗決定時。オーバーレイ登場時に BGM を停止）, `BattleSE`（VS 告知）, `ReadySE`（コイントス結果表示）, `CoinSE`（コイントス・コインドローの回転中にループ再生し、回転終了で停止。`SoundPlayer.PlayLoopSE`/`StopLoopSE` で制御）, `CardSE`（ドロー時）, `CardUseSE`（カード使用時。コストエフェクトと同タイミング）, `AttackSE`（攻撃時）, `DeckDamageSE`（デッキダメージのミル時）, `VictoryPointSE`（勝利点獲得時）, `DownSE`（キャラ破壊時）, `OverLimitSE`（オーバーリミット告知時。アセットは `Sound/SE/OverLimit`）, `PlayerTurnSE`（自分の手番「YOUR TURN」告知時）。各 SE はロード時にラウドネス正規化される（上記「サウンド設計」）)
- `ModalStore` が Option モーダルの VisualTreeAsset をロード
- `CardStore` がカードテンプレート（VisualTreeAsset）・裏面画像・盤面背景（Texture2D）をロード
- ロード完了は `UniTask Loaded` プロパティで通知

### 既知の課題：カード画像は Addressables 例外（全カード一括ロード）

原則は「アセットは Addressables で遅延ロード」だが、**カード画像（`CardData._image`）だけは例外的に SO への直接参照（`Sprite`）**になっている。このため `CardDatabase`（→ 各 `CharacterCardSO`/`EventCardSO` → 全 `CardData`）を参照したシーンを開くと、**使う使わないに関わらず全カード画像がメモリに一括ロードされる**。

これは特に WebGL モバイルで問題になった。モバイルの GPU は DXT(S3TC) 非対応のことが多く、圧縮テクスチャがランタイムで RGBA32 に展開されるため、全カードぶんのテクスチャがメモリを圧迫し、初回ロード後に OOM でタブが落ちていた（タイトル画面が [TitleCardSpherePresenter](../Assets/Scripts/Title/CardSphere/TitleCardSpherePresenter.cs) で `CardDatabase` を丸ごと参照しているため、6 枚しか表示しないタイトルでも全カードがロードされる）。

- **暫定対処（実施済み）**：カード画像の WebGL 用 `maxTextureSize` を 512 に制限し、全カード同時展開でも収まるようにした（一括適用ツール [WebGLTextureSettingsApplier](../Assets/Scripts/Editor/WebGLTextureSettingsApplier.cs)、メニュー `Card → WebGL テクスチャ設定を適用`）。
- **根本対応の方向性（未実施）**：カード画像を Addressables 化し、手札・場など**今表示するカードだけ**オンデマンドで読み込み・解放する。あわせてタイトル球は `CardDatabase` 全体ではなく**小さなサブセット参照**に変える。これにより画質を上げつつカード枚数も増やせる。

### 既知の課題：スマホ（タッチ）でカードドラッグが効かない

**根本原因はシーンに `EventSystem` が無いこと。** UI Toolkit は「マウス入力だけ」EventSystem 無しでも動く特別扱いがあるため、エディタや PC ビルドのマウス操作では問題が出ない。しかし**タッチ入力は `EventSystem` + `InputSystemUIInputModule` が無いと正しく処理されず**、ポインターキャプチャや連続した `PointerMove` が機能しない。その結果、スマホ（WebGL）では UI Toolkit のドラッグ（[CardDragManipulator](../Assets/Scripts/Main/Card/CardDragManipulator.cs)）が、指がカードの上にある間だけ少し動いて止まる／追従しない／`ScrollView` のスクロールと競合する、といった症状になる。

- **対処（必須）**：常駐する `Common` シーンに `EventSystem` + `InputSystemUIInputModule` を1つ追加し、Pointer Behavior を **Single Unified Pointer**（マウスと各タッチを単一ポインターに統合）にする。これで UI Toolkit のドラッグがマウスと同じ挙動で安定する。エディタメニュー **`Card → Common シーンに EventSystem を追加（タッチ入力対応）`**（[TouchEventSystemSetup](../Assets/Scripts/Editor/TouchEventSystemSetup.cs)）で自動追加できる（手動で `Hierarchy → UI → Event System` 後に Pointer Behavior を変更してもよい）。
- **補助（任意のモバイル向けハイジーン）**：WebGL の canvas に `touch-action: none` を実行時設定し、スマホでページ自体のスクロール/ピンチズームが誤発火しないようにしている（[TouchAction.jslib](../Assets/Plugins/WebGL/TouchAction.jslib) を起動時に [WebGLTouchAction](../Assets/Scripts/Common/Platform/WebGLTouchAction.cs) から呼ぶ）。Unityroom は Build / StreamingAssets だけをアップロードし canvas は Unityroom 側が生成するため、テンプレートの CSS ではなく実行時設定で対応する。これは上記ドラッグ不具合の直接原因ではないが、モバイルでの誤操作防止として残している。

---

## Home シーン（ホーム画面）

### 概要

Title → Home の遷移後に表示されるメインハブ画面。デッキ構築・バトル・マッチングへの導線を提供する。

### クレジット・遊び方（ルール）モーダル（HomePresenter）

画面右下に「詳しいルール」「クレジット」の2ボタンを並べ、それぞれ全画面オーバーレイのモーダルを開閉する（開閉は `HomePresenter`、SE は `EnterSE` を共用）。いずれもクリックを透過しない暗幕の上にパネルを重ね、「閉じる」ボタンで `DisplayStyle.None` に戻す。2つのオーバーレイはボタンより後ろの兄弟要素として配置し、開くと下部のボタンを覆うため同時には開かない。

- **クレジット**：制作スタッフ・テスター・サウンド・使用OSS/アセットを `ScrollView` に列挙（内容は UXML 直書き）。
- **遊び方**：「詳しいルール」ボタンで開く、新規ユーザー向けのルール解説（モーダルのタイトルは「遊び方」）。`RulesTab*` の8ボタン（目的／流れ／カード／色／バトル／オーバーリミット／能力／デッキ）でタブを切り替え、対応する `RulesPage*` のみ `home-rules-page--active` で表示する。各ページの内容は UXML に直書き（追加アセット・Addressables 不要）。タブ選択時に `RulesScroll.scrollOffset = Vector2.zero` でスクロール位置を先頭へ戻す。タブのクリックハンドラはラムダを配列に保持して `OnDisable` で確実に購読解除する。

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

- **雨**（敗北で戻った）: `_rainEffectPrefab`（`vfx_Rain_01` Particle System）を `(0, 15, 0)` に Instantiate し、DarkOverlay（`rgba(0,0,0,0.4)` の全画面オーバーレイ）を `DisplayStyle.Flex` で表示。あわせて `SoundPlayer.StopBGM()` で **Main から鳴り続けている戦闘 BGM を止め**、晴れ時の Home BGM は再生しない（雨音だけの演出）
- **晴れ**（勝利・通常）: DarkOverlay は `display: none`（デフォルト）のまま、エフェクトなし。`SoundStore.MainBGM`（Home BGM）を再生する

`vfx_Rain_01` の Particle System Renderer は `Order in Layer: 19` を設定し、Live2D Dog-kid（最大 `SortingOrder: 18`）より前面に雨粒を描画する。

### デッキ読み込み（HomePresenter）

`Construct()`（VContainer の `[Inject]` メソッド）内で `DeckModel.Clear()` → `DeckRepository.Load(SelectedIndex)` を呼び出し、PlayerPrefs に保存された「対戦に使う選択中スロット」のデッキを復元する。これにより、DeckBuilder を経由しないルート（Title → Home → CPU対戦）でもカスタムデッキが正しく使用される。

「使用デッキ」ボタンで対戦に使うデッキを選ぶモーダル（`DeckSelectOverlay`）を開ける。9 スロットを名前・枚数・完成状態・使用中マークつきで一覧表示し、行をタップすると `DeckRepository.SelectedIndex` を更新して `DeckModel` をそのスロットで読み込み直す（CPU・オンライン両対戦が同じ `DeckModel` を使う）。各行の左にはそのデッキのシンボルカード（`DeckRepository.LoadFavorite` のカードIDを `CardData.Image` で解決）を小さく表示し、シンボル未設定のデッキはカード裏面を代わりに表示する。画像解決のため `HomePresenter` に `CardDatabase`（シンボル画像用）と `Texture2D _cardBack`（裏面用。`Image/CardBack` テクスチャを割り当てる。Home には `CardStore` が無いためシリアライズ参照する）をインスペクタで割り当てる（いずれも未割り当てなら該当画像が出ないだけで他は動作）。デッキの中身の編集は DeckBuilder シーンで行い、ここと DeckBuilder のスロット一覧上部では「どのデッキで戦うか」だけを選ぶ。

`Start()` ではなく `Construct()` で行う理由: `CommonSceneLoader.Awake()` が Common シーンを **非同期** でロードするため、直接シーンロード（テスト・デバッグ）では `Start()` が `BuildLifetimeScopes()`（VContainer injection）より先に実行されてしまう。`Construct()` は injection 完了タイミングで呼ばれるため常に安全。

### CPU 対戦開始時のセッションリセット（HomePresenter）

「バトル」ボタンから CPU 対戦を開始する際、`HomePresenter.StartCpuBattleAsync()` がまず `GameSessionModel.LeaveCurrentSessionAsync()` を呼んでからシーン遷移する。

これにより、オンライン対戦後に Home へ戻らずそのまま CPU 対戦を始めた場合でも `GameSessionModel.HasSession` が確実に `false` になり、`MainPresenter._isOnline` が `true` になってネットワーク待機ループに入る問題を防ぐ。セッションが null なら `LeaveCurrentSessionAsync()` は即リターンするため、初回起動時も問題ない。

---

## DeckBuilder シーン（デッキ構築）

### 概要

Home → DeckBuilder → Home の遷移フローでプレイヤーがデッキを組む画面（デッキ編集が主目的だが、対戦に使うデッキの選択もここから行える。ゲーム開始は Home から）。

プレイヤーデッキは 9 スロット制（`DeckRepository.SlotCount`）。1 シーン内で「スロット一覧（9枠）」と「編集」の 2 画面をフルスクリーンオーバーレイの表示／非表示で切り替える。スロット一覧はカード読み込み前フック（`OnBeforeLoad`）で組んで表示するため遷移直後から見え、枠をタップするとそのスロットの編集画面へ入る（読み込み未完なら従来の「読み込み中...」を経て表示）。各枠は横長で「シンボルカード（左）＋（デッキ名＋名前変更『編集』ボタンの行）＋（枚数＋使用中なら『★使用中』バッジの行）」で構成し、名前・カード列・シンボルカードIDはそれぞれ `DeckRepository` の `DeckName_{slot}` / `SavedDeck_{slot}` / `FavoriteCard_{slot}` に保存される。シンボルカードはカード詳細モーダルの「★ デッキのシンボルに設定」ボタンで指定する（`CardDetailModal.Show` のアクション引数を使い、`DeckBuilderPresenterBase` の `CardDetailActionLabel`/`OnCardDetailAction` フック経由でプレイヤーのビルダーのみ表示）。シンボル未設定のスロットは `CardStore.CardBack`（カード裏面）をサムネに表示する（裏面はカード読み込み後に得られるため、読み込み完了時 `OnDeckBuilderReady` で一覧を組み直して反映する）。

対戦に使うデッキ（`SelectedIndex`）は、スロット一覧画面上部中央の「使用デッキ：◯◯」ボタン → Home と同じ見た目の一覧モーダルからここでも選べる（行タップで `SelectedIndex` を更新。編集中デッキ `_deckModel` は触らず、グリッドの使用中表示だけ更新する）。Home 側の使用デッキ選択と同じ `SelectedIndex` を更新するため両画面の表示は一致する。編集画面でも「デッキ」見出しの横の「使用する」ボタン（`UseDeckButton`）で、いま編集中のスロットをそのまま対戦用デッキ（`SelectedIndex`）にできる。すでに使用中のスロットを編集中はボタンが「★使用中」表示で明るく強調され押せなくなる。

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
```

### DeckBuilderPresenter のフロー

1. `CardStore.Loaded` を await してアセットロード完了を待つ（読み込み前に `OnBeforeLoad` でスロット一覧を表示。枠タップで編集対象スロットを決め編集画面へ）
2. `DeckModel.Clear()` でリセットし、`DeckRepository.Load(_editingSlot)` で編集対象スロットを復元
3. `CardDatabase.AllCards`（`InDeckPool`＝ゲーム未使用カードと対戦専用トークンを除いた集合）をキャラ・イベントのセクション別にグリッド表示。各セクション内は属性順（`FilterAttributes` の並び：White→Blue→Green→Yellow→Red→Black→Purple）→ コスト昇順 → ID 昇順でソート
   - 種別／属性／コストのフィルタは**このカード一覧（プール）にのみ適用**する。デッキパネル（現在デッキに入っているカード）はフィルタの影響を受けず常に全件表示する
4. カードをデッキパネルへドロップ → `DeckModel.Add(id, cost)` → デッキパネル更新
5. デッキパネルは同一カードを「サムネイル + カード名 ×枚数」形式で1行にまとめて表示。左端の `≡` ハンドルをドラッグ&ドロップで行を並び替え可能（`DeckModel.Reorder` を呼び出し自動保存）
6. × ボタン → `DeckModel.Remove(id)` → デッキパネル更新
7. 並べ替えボタン → デッキ内の各 ID を指定順で並べ `DeckModel.Reorder()` を呼ぶ → デッキパネル更新 → 自動保存
   - 「整列」ボタン：カード一覧と同じ並び。種別（キャラ→イベント）→ 属性順（`FilterAttributes` の並び）→ コスト昇順 → ID 昇順
   - 「コスト順」ボタン（「整列」の左）：コスト昇順 → 種別（キャラ→イベント）→ 属性順 → ID 昇順
8. 「空にする」ボタン（デッキ枚数 > 0 のとき表示）→ 確認ダイアログを表示（「はい」で `DeckModel.Clear()` / 「いいえ」またはオーバーレイ背景クリックでキャンセル）
8. デッキ変更のたびに編集中スロットへ自動保存（`DeckRepository.Save(_editingSlot)`）。編集画面の「もどる」でスロット一覧へ、一覧の「もどる」で Home へ退出

### MainPresenter との連携

Main シーンロード時、`DeckModel.Count > 0` なら `CardDatabase.BuildDeck(DeckModel.CardIds)` でプレイヤーデッキを構築。空の場合は `AllCards` 全体をフォールバックとして使用。CPU デッキは Home の相手選択で選んだ相手（`CpuRosterSO` の `CpuOpponentData`・`CpuBattleModel.OpponentIndex` で渡る）のカードIDから構築する。相手にカードIDが未設定（プレースホルダー）の場合は `CardStore.CpuDeck`（`CpuDeckSO`・Addressables キー `"Card/CpuDeck"`）にフォールバックし、それも空なら `AllCards` を使用する。相手の `Difficulty`（初級/中級/上級）も同時に `_cpuDifficulty` に読み込む。

`CardData._excludeFromGame`（カードエディタの「ゲームで使用」トグルで OFF）が立ったカードは、`CardDatabase` の ID 辞書・`AllCards` の両方から除外され、デッキ構築のプール・対戦・`BuildDeck` での ID 解決すべてから外れる（調整中・未完成カードを隠す用途）。

`CardData._excludeFromDeckBuilder`（「対戦専用（トークン）」トグル）が立ったカードは、ID 辞書（`_dict`・`InUse`）には登録され対戦の ID 召喚・参照（SummonChar 等）に使えるが、`AllCards`（`InDeckPool`）からは除外されデッキ構築のプールに出ない。トークンカード用。

---

## Main シーン（カードゲーム盤面）

### カードシステム

```
CardData              抽象基底クラス。id / name / cost / Attack / Hp / Attribute / image(Sprite) / FlavorText（世界観テキスト。効果に影響せず詳細モーダル最下部に表示）/ VoiceSpeaker（フレーバー読み上げの VOICEVOX 話者。0＝共通設定）
  CharacterCardData   キャラカード。Attack / Hp / Attribute 値を保持。メインフェーズで表向きにフィールドへ配置
                      登場時効果として EffectTrigger / EffectType（EventType 流用）/ EffectValue / Description（説明テキスト）を保持
  EventCardData       イベントカード。EventType / EventValue / Attribute / Description を保持。メインフェーズで即時解決し墓地へ

CardAttribute     enum（CardAttribute.cs）。Red / Blue / Green / Yellow / Black / Purple / White

CharacterEffectTrigger  enum（CharacterEffectTrigger.cs）。キャラの効果発動タイミング。
                  None / OnEnter（登場時）/ OnAttack（攻撃時）/ OnDestroy（破壊時。戦闘・DamageEnemy/DamageAllEnemies での撃破・BanishChar での除去で発動）

EventType         enum（EffectType.cs）。
                  None / AtkBoost（自フィールドから値1体選び攻撃力を値2上げる）/ HpBoost（同・HP を上げる。旧 DefBoost）
                  Draw（EventValue 枚ドロー）
                  BanishChar（相手キャラをフィールドから墓地へ）
                  Recover（自分の墓地の上から EventValue 枚を取り出し自デッキに加えてシャッフル）
                  Switch（自分のキャラを手札に戻し同コストのキャラをコストなしで配置）
                  Evolve（自分のキャラを墓地に送り上位キャラと交換）
                  CostBoost（コスト支払い時に倍化・属性連動）/ DamageAllEnemies（敵全体にダメージ）
                  DamageEnemy（敵を値1体選び値2ダメージ）/ SummonChar（指定キャラを召喚）
                  SummonFromDeckByKeyword（自身の特徴を持つキャラをデッキから1枚選んで召喚）
                  SummonFromGrave（自分の墓地からキャラをN体選んで場に出す）
                  CopyFieldChar（自分の場のキャラを1体選び、そのコピーをN体出す。バフ・現在HP込み）
                  GainVPPerGreenGrave（自分の墓地の緑カード枚数だけ勝利点を加算）
                  HealAllAllies（自フィールド全キャラのHPを回復・値0で全回復）
                  NextCardCostFree（次の1枚を無料）
                  ※固定値の勝利点付与は EventType ではなく全カード共通の VictoryPointBonus 付帯値で行う
                  Bounce（敵を値1体選び所有者の手札へ戻す。DamageEnemy と対象選択を共用）

EffectHandler / EffectCatalog   効果1種＝EffectHandler 派生1クラス（Assets/Scripts/Main/Card/Effects/）。演出＋盤面適用（ApplyAsync）とエディタ用メタデータ（効果テキスト BuildBody・値ラベル Values・キャラ/イベント可否）を1クラスに集約。
                  EffectCatalog が Main アセンブリを走査してハンドラを自動登録（起動時に全 EventType の網羅を検証）。MainPresenter（ResolveEventCardEffectAsync / ResolveCharacterTriggeredEffectAsync）と CardEditorWindow はカタログ経由でハンドラを引くだけで、効果追加時に switch を編集しない。盤面操作は MainPresenter の internal building-block メソッドをハンドラから呼ぶ。

CharacterCardSO / EventCardSO   各カード種別の ScriptableObject（属性 × 弾＝第N弾ごとに分割。Assets/Data/Set{弾}/{属性}/ に配置）
                  SO ごとに Attribute と Set（弾番号・既定1）を持ち、所属カードの属性・弾を一括設定する（カードの Attribute はインスペクタで読み取り専用＝ReadOnly 属性）
                  ID は OnValidate で自動採番（CardIdAutoAssigner・エディタ専用）。規則は C{(属性番号)×1000+(弾-1)×100+連番}（白第1弾=C1001/白第2弾=C1101/青第1弾=C2001…、属性番号=白1/青2/緑3/黄4/赤5/黒6/紫7。E も同様）。弾1=オフセット0で既存ID維持・属性×弾の SO 間でも一意・"C{番号}" 形式（SummonChar 互換）。1属性1弾あたり最大99枚・最大9弾
                  カードエディタの新規追加で属性＋弾を指定すると、該当弾の SO が無ければ自動生成し CardDatabase へ自動登録する

CardDatabase      ScriptableObject。属性 × 弾別 SO の配列 _characterCardSets / _eventCardSets を集約し、Dictionary でルックアップ（全弾をマージ。弾は採番・管理上の軸で、対戦・デッキ構築プールでは区別しない）
CardStore         IStartable。Addressables から Card.uxml と CardBack.png を非同期ロード

TurnPhase         enum。Draw / Main
WinRule           static クラス。共通の勝利条件の純ロジック（デッキ切れ＝オーバーリミット: 空デッキから引く/ミルで敗北 / 勝利点20 / キャラ8体）と定数
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
                  IsTapped（タップ＝90°横向き）を持ち、SetTapped（即時）/SetTappedAsync（回転アニメ）で内側 _cardRoot を回転。タップ中は game-card--tapped クラスでネームを上辺・アイコンを底辺の横並びに逆回転して再配置する。バウンス/スイッチで手札に戻る ResetRuntimeState でアンタップにリセット
                  IsOpponent フラグ（コンストラクタで設定）で相手カードかどうかを表す。メインフェーズの isLocal 判定に使用
                  FlipAsync() で DOTween による裏返しアニメーション（表向き時にフレーム・ImageArea を表示、裏向き時に非表示）
                  AttachDragManipulator() / RemoveDragManipulator() でドラッグ着脱
HandView          VisualElement サブクラス。手札を扇状に表示（60% スケール）
                  各カードをボトムセンター軸に最大 ±20° 回転 + 放物線アーク
                  ホバー時に BringToFront() で最前面表示＋約5%拡大（重なっても狙ったカードを判別しやすくする。scale/translate のアニメは hand-card クラスで定義）
                  コスト選択したカードは cost-selected クラスで上方向へ持ち上げ、ホバーを外しても最前面に留める（RestoreCardOrder()/RestoreAllOrder() で z 順を復元）
                  バウンス等で手札に戻ったカードにもホバー処理を付与（hand-card クラスの有無で二重登録を防止）
                  ドラッグ開始時に DragLayer へ移動、ドロップ失敗でスナップバック
                  ドロップ成功後に残りカードを DOTween でアニメーションしながら詰める
                  faceDown: true で裏向き表示、interactive: false でホバー・ドラッグ無効化（相手手札用）
                  isOpponent: true を渡すと内部で作成する CardView すべてに IsOpponent = true が伝播
                  AddCardAnimatedAsync() でデッキ位置から手札へのドロー演出（飛翔→フリップ）※プレイヤー用
                  AcceptCard(CardView) で既存 CardView をそのまま手札に取り込む（CPU ドロー演出後の手札追加用）
                  AddCardBackAsync() でフィールドから手札へ戻す飛翔アニメーション（戻るボタン用）
                  interactive: false のときフリップをスキップ（相手手札は裏向きのまま）
                  内部状態は HandCardEntry { Card } の単一リストで管理（並列リスト廃止）
FieldView         VisualElement サブクラス。横長フィールドエリア（中央寄せ。基準 5 枚は全幅、6 枚以上は自動縮小、上限 9 体＝MaxCharacters）
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
                        ClearArrow() で矢印を手動削除。IsDragging でドラッグ中か公開（盤面再構築で引いている矢印を消さない判定に使う）
ArrowView            VisualElement サブクラス。Painter2D で攻撃矢印を描画（ベジェ曲線＋矢印先端）
                     StartPoint / EndPoint を更新するたびに再描画。PickingMode.Ignore で操作を透過
GraveyardView        VisualElement サブクラス。80×80px のアイコンボタン（GraveIcon.png + 枚数ラベル）
                     クリックで墓地カード一覧モーダルを開く（横スクロール・トップ/ボトムラベル付き）
                     AddCard(CardView) でカードを階層から切り離し、CardData を内部リストで管理
                     モーダルは mainRoot に追加してオーバーレイ表示。背景クリックで閉じる
                     茶色系の枠線（rgba(160,100,50)）と暗いベージュ背景
VictoryPointsView    VisualElement サブクラス。勝利点表示（Medal1Icon.png + 数字）。共通の勝利条件のため
                     ゲーム開始時から常時表示。相手用は OpponentFieldArea 右上・自分用は PlayerFieldArea 左下に絶対配置
                     AddPoints / SetDisplayedPoints で論理値と表示を分離（カウントアップ演出用）
TurnCounterView      VisualElement サブクラス。経過ターン表示（「TURN」+ GameModel.TurnNumber=通算ターン）。
                     mainRoot 直下の画面左下（自分の勝利点の上）に絶対配置で常時表示。SetTurn(int) で更新し、
                     各ターン開始時（RunTurnAsync 冒頭）に呼ぶ

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
TurnCounterView         （左下・bottom: 76px・自分の勝利点の上・経過ターン常時表示）
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
3. **オンライン/オフライン/チュートリアルモード分岐**（`GameSessionModel.HasSession` と `_isTutorial` で判定。`_isTutorial` は `Construct` で `TutorialModel` から消費して確定し、チュートリアルは必ずオフライン扱い）
   - **オンライン**: `NetworkGameService.PrepareDecksAsync` でデッキ交換プロトコルを実行し `OnlineInitialState` を取得。20 秒タイムアウト付き CTS でラップし、タイムアウト時は「対戦が成立しませんでした」モーダルを表示して Matching シーンへ誘導。相手手札はプレースホルダーで埋める
   - **チュートリアル**: 先攻プレイヤー固定・`TutorialId` 別の固定デッキ（`TutorialScript`）をシャッフルせず使用。VS告知/コイントス/マリガンを省略し、`SetupTutorial` で盤面プリセット（攻撃役・的・勝利点など）とコーチ吹き出しを用意。手番タイマー無効・CPU は台本（`RunTutorialOpponentMainLoopAsync`）で動く。詳細は [Main シーン（チュートリアル）](#main-シーンチュートリアル)
   - **オフライン（CPU 戦）**: プレイヤーは `DeckModel`、CPU は Home の相手選択で選んだ相手（`CpuRosterSO` の `CpuOpponentData`・未設定時は `CpuDeckSO`）からローカルでデッキを生成・シャッフル。相手の `Difficulty` を `_cpuDifficulty` に確定する
4. 相手・自分ともに `HandView`・`DeckView`・`FieldView`・`GraveyardView`・`VictoryPointsView` を配置し、経過ターン表示の `TurnCounterView` を画面左下に配置
5. ドロップハンドラを接続（プレイヤーフィールドへのドロップ）
6. Resolve オーバーレイとターン告知オーバーレイを生成
7. OK/戻る/パスボタンにハンドラを接続
8. `UniTask.NextFrame` でレイアウト確定を待つ
9. VS 告知の後、配牌前に `InitializeFirstTurnAsync` でコイントス演出を行い先攻後攻を確定する（手札枚数が先攻3枚・後攻5枚で変わるため配牌前に決める）
10. 先攻3枚・後攻5枚（`MulliganRule`）を 0.12 秒ずつずらして `AddCardAnimatedAsync` で配牌 → マリガンチェック
11. `RunGameAsync(ct)` でゲームループ開始。以降は1ターン交互に手番が入れ替わる

### MainPresenter のゲームループ（RunGameAsync）

各ターンは Draw → Main の2フェーズを実行する（`TurnPhase` enum に対応）。

```
InitializeFirstTurnAsync  （ゲーム開始時に1度だけ・配牌前に実行）
  → コイントスアニメーション（PlayCoinTossAsync）で先攻後攻を提示（決定済みの isLocalFirst を受け取る）
  （補正ドローなし。先攻有利は手札枚数差（先攻3/後攻5）と両プレイヤーの初手ドローなしで補正する）

RunTurnAsync  （各ターンの先頭）
  → ReseasonActivePlayerChars: ターン開始時効果より前に、アクティブプレイヤーの場のキャラを「召喚酔いなし」としてスナップショット（ReseasonChars）。ここで記録したキャラだけが今ターン攻撃でき、ターン開始時効果（OnTurnStart 召喚など）で出たキャラは含まれず召喚酔いする
  → PlayTurnStartAnnouncementAsync: 自分の番は "YOUR TURN"、相手の番は "ENEMY TURN" を告知

RunDrawPhaseAsync
  → 手番プレイヤーのみ3枚ドロー（DrawPhaseCardCount）。ただし両プレイヤーの初手（プレイヤーごとの初手フラグで判定）はドローなし
      ローカルターン:  ドローアニメーション → SendDrawNotification で相手に通知（0枚でも同期のため送信）
      相手ターン:     WaitForOpponentDrawAsync でドロー通知受信 → 相手ドロー演出

RunMainPhaseAsync  （手番プレイヤーは EndButton／Pass を出すまで制限なく行動を繰り返すループ）
  → 各メインフェーズ開始時に攻撃済み記録をクリア（_attackedThisTurn）し、アクティブプレイヤーの場の全キャラをアンタップする（UntapField）。召喚酔いのスナップショットは上述のとおり RunTurnAsync 冒頭で取得済み
  → ローカル(RunLocalMainLoopAsync): WaitForPlayerMainActionAsync で入力待ちを繰り返す
                  手札キャラ → フィールドへドロップ（PlaceChar）
                  手札イベント → フィールドへドロップ（PlayEvent）→ 即時解決
                  攻撃可能キャラ（attackable-char でハイライト） → AttackArrowManipulator で矢印を引いて攻撃（Attack）
                  EndButton → ターン終了（オンラインは Pass を相手へ送る）
      CPU(RunCpuMainLoopAsync):  CpuChooseMainAction() で攻撃優先・次にキャラ・イベント、無ければ終了を選択し繰り返す
      オンライン(RunOnlineOpponentMainLoopAsync): WaitForOpponentMainActionAsync で相手アクションを Pass 受信まで繰り返し受信
  → 各キャラの攻撃はターン1回まで（_attackedThisTurn）、このターン登場したキャラは攻撃不可（召喚酔い）
  → キャラへの攻撃対象は**タップ状態のキャラのみ**（攻撃したキャラはタップ→自分のターン開始時にアンタップ／守護・防人は毎ターン終了時に自動タップ＝AutoTapGuardiansAndSakimoriAsync）。判定は CanAttackChar が IsTapped を含めて中央化。ただし**強襲（Assault）**を持つ攻撃者はタップ要件を無視しアンタップのキャラも狙える。デッキ攻撃はタップ制限の対象外だが、**デッキ攻撃×（NoDeckAttack）**を持つキャラ自身は相手デッキを直接攻撃できない（CanAttackDeck で集約。制限を受けるのはその能力を持つキャラだけ）

ExecuteAttackAsync  （Attack アクション実行時）
  → ResolveCharacterAttackEffectAsync: 攻撃キャラの OnAttack 効果を発動（Draw・BanishChar）
  → PlayCardChargeAsync: 攻撃キャラのコピーが「ウィンドアップ → 突撃 → ノックバック → 元位置へ戻る」演出
                          演出中は元カードを visibility: hidden で非表示
  → TapAttackerAsync: 突進後に攻撃キャラをタップ（横向き）にする（デッキ攻撃 ExecuteDeckAttackAsync も同様）
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

対戦相手は Home の相手選択カルーセル（`CpuRosterStore` のロスター。`CpuRosterSO`・Addressables `Card/CpuRoster`）で選び、`CpuBattleModel.OpponentIndex` で Main へ渡す。Main 再ロードの再戦でも同じ相手・難易度になる。

メインフェーズで `CpuChooseMainAction()` が行動を選択する。

```
CpuAgent.ChooseCharacterSetCardIndex(hand, canAfford)   配置するキャラカードのインデックスを返す（なければ -1）
CpuAgent.ChooseEventCardIndex(hand, canAfford)          使うイベントカードのインデックスを返す（なければ -1）
```

`canAfford(i)` は `hand[i]` のコストを支払えるかの判定。`MainPresenter.CpuCanAffordCost()` を渡し、自身を除いた手札の支払い可能量（`CostPaymentValue` 合計）がコストに満たない、または同属性のコスト素材（`CostPaymentValue>0` の同属性カード）を1枚も持たないカードは選ばない（ローカルプレイヤーの `CostCapacityExcluding` / `IsCostAttributeSatisfied` と同じく、踏み倒しと属性制約違反の払い方を禁止）。

**CPU 難易度**（相手ごとに `CpuRosterSO` の `Difficulty` で設定。初級/中級/上級）:
- **初級**: 従来どおり、支払える順に手札先頭から出す。
- **中級以上**: `CpuMayPlayToField()` が CostBoost（キャラ＝`OnUsedAsCost`＋`CostBoost`／イベント＝`CostBoost`）・ダメージトリガー（`CardData.TriggerOnGrave`）持ちのカードを場に出す候補から除外し、`ChooseCpuCostCards()`（`MainPresenter.Animations.CostFly.cs`）がそれらをコスト支払いに優先的に充てる。
- **上級**: 当面は中級と同じ挙動（`_cpuDifficulty == Advanced` で将来分岐を追加できる構造）。

キャラ攻撃・デッキ攻撃の対象選択は守護・飛行を考慮するため `MainPresenter.CpuChooseMainAction()` 側で `CanAttackChar` / `CanAttackDeck` を使って解決する（合法な対象を持つ攻撃者の中で最高ATK→対象は最低ATK）。攻撃力0のキャラは与ダメージ・ミルともに0で攻撃しても無意味なため、攻撃者候補（`availableAttackers`）から除外する（キャラ攻撃・デッキ攻撃・lethal判定すべての起点）。

優先順位: lethal デッキ攻撃（ATK > 相手デッキ枚数で空デッキからさらにミルさせて敗北させられる） → キャラ攻撃（合法な対象があれば） → チップミル（デッキ攻撃。キャラ攻撃対象がない場合） → キャラ配置 → イベント使用 → パス

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

---

## Main シーン（チュートリアル）

Home の「チュートリアル」選択モーダルから起動する**誘導つきスクリプト対戦**。通常のオフライン CPU 戦の仕組みをそのまま使い、決定的セットアップとコーチ層を上から被せて実現する（本番のゲームロジック・入力系は触らない）。実装は [MainPresenter.Tutorial.cs](../Assets/Scripts/Main/MainPresenter.Tutorial.cs)（partial class）と [Main.Tutorial.uss](../Assets/Scripts/Main/View/Main.Tutorial.uss)。

- **起動・種別**: Home の `TutorialModel.IsActive` / `TutorialModel.Id`（Common 常駐）をセットして Main へ遷移。`MainPresenter.Construct` で読み取って `_isTutorial` / `_tutorialId` に確定し、**即 `IsActive=false`（消費型）**。中断しても通常 CPU 戦・オンライン戦に持ち越さない。`TutorialId` は `CardReading` / `BasicLoop` / `AttackBasics` / `DeckOutWin` / `FieldCharsWin` / `VictoryPointsWin` / `GuardianKw` / `HasteKw` / `FlyingKw` / `SakimoriKw` / `AssaultKw` / `NoDeckAttackKw` / `ArcherKw` / `DeadlyKw`。
- **決定的セットアップ**: `_isTutorial` のとき BuildAsync で先攻プレイヤー固定・`TutorialId` 別の固定デッキ（`TutorialScript` の ID 配列）をシャッフルせず使用。VS告知（`PlayVsAnnouncementAsync`）・コイントス（`InitializeFirstTurnAsync` 内で early-return）・マリガンを省略。`SetupTutorial` でコーチ吹き出しを生成し、必要なら盤面をプリセット（`PresetCharacter` で攻撃役・的を配置し相手はタップ済みに、勝利点・場のキャラ数も調整）。
- **本番への差し込み**: `if (_isTutorial)` のフックのみ。`RunLocalMainLoopAsync` で手番タイマー無効＋`TutorialBeginPlayerMainPhase`／`TutorialOnLocalPass`／`TutorialOnLocalActionResolved` 呼び出し、`HandlePlayerCardDrop` で `TutorialOnLocalStagedCost`（コスト選択開始時にコスト案内へ切替）、`RunMainPhaseAsync` の CPU 分岐で `RunTutorialOpponentMainLoopAsync`（台本どおりに動く相手）。手札カードの詳細モーダルを開いた/閉じた契機は `_handView.OnCardClicked`（`TutorialOnLocalCardDetailOpened`）と `CardDetailModal.OnHidden`（`TutorialOnLocalCardDetailClosed`）で拾う（`CardReading` 用）。
- **カードの見方（`CardReading`）**: 戦闘しない練習。盤面プリセットなしで、手札・デッキは全てキーワード能力持ちの実在カード（守護/飛行/速攻/防人/強襲/射手）で構成しトークンを入れない。step0=手札アイコンを解説し先頭カード（C1005）をハイライト→手札カードを開いて閉じると step2=カード詳細の読み方を解説＋END をハイライト→END（パス）で `CompleteTutorial`。step0/1 のうちに END を押しても完了せず次の自分の番で解説へ戻る。
- **コーチ吹き出し**: 画面上部のパネル（`tutorial-coach`）。タップで小さな「ヒントを見る」チップに畳み、再タップで展開（`SetCoachCollapsed`）。本文は左寄せ。ステップ進行は `ShowCoach` で差し替え、誘導は標準ハイライト（攻撃可能=シアン `attackable-char` / 攻撃対象=オレンジ `attack-target-char` / デッキ攻撃対象 `deck-view--attack-target`）＋手札・ボタンへの緑枠（`tutorial-highlight`）で行う。
- **クリア演出**: 勝ち方系（`DeckOutWin` / `FieldCharsWin` / `VictoryPointsWin`）は通常の勝利条件（`winReason` 付き）で決着し「デッキ切れ勝利／制圧勝利／勝利点勝利」を表示。基本・キーワード系は `CompleteTutorial` が `_tutorialCompleted=true` を立てて `OnGameEnd(playerWins:true)` を呼び、`PlayGameEndAsync` が「YOU WIN」ではなく **「チュートリアル完了」** を表示する。チュートリアルは再戦（同じ台本の再ロード）に意味がないため、`PlayGameEndAsync` は `_isTutorial` のとき再戦ボタンを隠し「ホームに戻る」のみ表示する。
- **失敗演出**: 誤った操作をしたら `FailTutorial` が `_tutorialFailed=true` を立てて `OnGameEnd(playerWins:false)` を呼び、`PlayGameEndAsync` が **「チュートリアル失敗」**（lose 表示）を出す。現状は飛行チュートリアルで守護持ちを攻撃したときに発生する（飛行は守護を無視して奥のキャラを攻撃するのが正解）。`IsTutorialForbiddenAttackTarget` が「攻撃を禁じる相手」を判定し、`HighlightAttackTargets` ではその相手を攻撃対象として強調せず（茶色＝`attack-target-char` を付けない。ただし `CanAttackChar` 自体は変えないので攻撃自体は可能＝攻撃すると失敗）、`TutorialOnLocalActionResolved` の攻撃分岐で対象が禁止相手なら `FailTutorial` を呼ぶ。
- **キーワード体験に使う実在カード**: 守護=C1005 / 速攻=C1009 / 飛行=C5003 / 防人=C3007（守護兼）/ 強襲・デッキ攻撃×=C3006（速攻兼）/ 射手=C6002 / 必殺=C7006。単一キーワードの専用カードが無いキーワードは複数キーワードを持つ既存カードで代用する。射手チュートリアルは攻撃役に射手の C6002、的に飛行の C5003（タップ済み）をプリセットし、地上キャラで飛行を攻撃する体験をさせる。必殺チュートリアルは攻撃役に必殺の C7006（攻1）、的に高HP（HP8）の C2008（タップ済み）をプリセットし、攻撃力1でも一撃で大型キャラを破壊する体験をさせる。

# CLAUDE.md

このファイルはリポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

Unity 6 (6000.3.11f1) で開発するマルチプレイヤーカードゲーム。BGM/SE 再生・カードのフレーバーテキスト読み上げ（VOICEVOX 事前生成音声）機能と、Commonシーンをベースとしたアディティブシーン管理を備える。UGS（Unity Gaming Services）を使ったオンラインマッチメイキング機能を実装する。

## Unity 開発

ビルドと実行は Unity Editor (Unity 6000.3.11f1) を通じて行う。独立したビルドスクリプトは存在しない。

- **ビルド**: Unity Editor → File → Build Settings → Build
- **自動テストは作成しない**: EditMode / PlayMode テストやテスト用 asmdef は作らないこと。動作確認は Unity Editor 上での手動確認（Play ボタン）で行い、その手順を案内する

## アーキテクチャ

詳細は [docs/architecture.md](docs/architecture.md) を参照。

要点:
- `Common` シーンが常駐し、他シーンはアディティブでロード
- DI は VContainer（`Find()` / static 禁止）
- 状態管理は R3 の `ReactiveProperty<T>`（Model → Presenter の単方向フロー）
- アセットは Addressables（`Resources.Load` 禁止）
- UI は UI Toolkit / UXML + USS（uGUI 禁止）。スタイルはインラインでなく USS ファイルに定義してクラスで適用する
- **USS 非サポートプロパティ（使用禁止）**: `gap` / `row-gap` / `column-gap` → 代わりに子要素の `margin` で対応。`-unity-background-scale-mode: scale-to-fill` は無効値 → `stretch-to-fill` を使用

## コーディング規約 (.editorconfig でエラーとして強制)

- **命名**: 型・メソッド・プロパティ・定数は `PascalCase`、フィールドは `_camelCase`、引数・ローカル変数は `camelCase`
- **明示的な型を優先** (`var` は使用しない。`csharp_style_var_*` はすべて false)
- **アクセス修飾子必須** (インターフェースメンバー以外のすべてのメンバー)
- **readonly フィールド**を可能な限り使用
- すべてのブロックに波括弧必須、開き波括弧は新しい行に配置
- `using` ディレクティブは名前空間の外側に記述し、System ディレクティブを先頭に並べる

## 主要ファイルの場所

| 用途 | パス |
|---|---|
| Common DI 登録 | [Assets/Scripts/Common/Injector/CommonLifeTimeScope.cs](Assets/Scripts/Common/Injector/CommonLifeTimeScope.cs) |
| シーン遷移ロジック | [Assets/Scripts/Common/SceneManagement/SceneTransitioner.cs](Assets/Scripts/Common/SceneManagement/SceneTransitioner.cs) |
| シーン準備完了通知 | [Assets/Scripts/Common/SceneManagement/ISceneReady.cs](Assets/Scripts/Common/SceneManagement/ISceneReady.cs) |
| 遷移演出 | [Assets/Scripts/Common/Transition/TransitionPresenter.cs](Assets/Scripts/Common/Transition/TransitionPresenter.cs) |
| サウンド再生（BGM/SE/ボイス読み上げ。PlayVoice は前の読み上げを止めず重ねて再生） | [Assets/Scripts/Common/SoundManagement/SoundPlayer.cs](Assets/Scripts/Common/SoundManagement/SoundPlayer.cs) |
| ボリューム状態モデル（BGM/SE/ボイス。ボイスは SE と独立） | [Assets/Scripts/Common/Option/OptionModel.cs](Assets/Scripts/Common/Option/OptionModel.cs) |
| フレーバー読み上げ音声ロード（Addressables `Voice/{CardId}` をオンデマンド・キャッシュ） | [Assets/Scripts/Main/Sound/FlavorVoiceStore.cs](Assets/Scripts/Main/Sound/FlavorVoiceStore.cs) |
| フレーバー音声一括生成ツール（VOICEVOX 連携・Addressables 自動登録・Editor 専用） | [Assets/Scripts/Editor/FlavorVoiceGeneratorWindow.cs](Assets/Scripts/Editor/FlavorVoiceGeneratorWindow.cs) |
| セッション保持（Common） | [Assets/Scripts/Common/GameSession/GameSessionModel.cs](Assets/Scripts/Common/GameSession/GameSessionModel.cs) |
| デッキ構築モデル | [Assets/Scripts/Common/Deck/DeckModel.cs](Assets/Scripts/Common/Deck/DeckModel.cs) |
| デッキ構築ルール（同名3枚制限・デッキ枚数制限。Editor 再生時のみトグルで解除可） | [Assets/Scripts/Common/Deck/DeckRuleModel.cs](Assets/Scripts/Common/Deck/DeckRuleModel.cs) |
| デッキ保存 | [Assets/Scripts/Common/Deck/DeckRepository.cs](Assets/Scripts/Common/Deck/DeckRepository.cs) |
| ユーザーネーム保存 | [Assets/Scripts/Common/Username/UsernameRepository.cs](Assets/Scripts/Common/Username/UsernameRepository.cs) |
| ユーザーネームバリデーション | [Assets/Scripts/Common/Username/UsernameValidator.cs](Assets/Scripts/Common/Username/UsernameValidator.cs) |
| CPU デッキ SO（Addressables） | [Assets/Scripts/Main/Card/CpuDeckSO.cs](Assets/Scripts/Main/Card/CpuDeckSO.cs) |
| デッキ構築 Presenter 基底クラス | [Assets/Scripts/DeckBuilder/DeckBuilderPresenterBase.cs](Assets/Scripts/DeckBuilder/DeckBuilderPresenterBase.cs) |
| デッキ構築 Presenter | [Assets/Scripts/DeckBuilder/DeckBuilderPresenter.cs](Assets/Scripts/DeckBuilder/DeckBuilderPresenter.cs) |
| デッキ分析モーダル | [Assets/Scripts/DeckBuilder/DeckAnalysisModal.cs](Assets/Scripts/DeckBuilder/DeckAnalysisModal.cs) |
| デッキ構築 DI 登録 | [Assets/Scripts/DeckBuilder/Injector/DeckBuilderLifetimeScope.cs](Assets/Scripts/DeckBuilder/Injector/DeckBuilderLifetimeScope.cs) |
| デッキ構築 UXML | [Assets/Scripts/DeckBuilder/View/DeckBuilder.uxml](Assets/Scripts/DeckBuilder/View/DeckBuilder.uxml) |
| CPU デッキ構築 Presenter（Editor 専用） | [Assets/Scripts/CpuDeckEditor/CpuDeckBuilderPresenter.cs](Assets/Scripts/CpuDeckEditor/CpuDeckBuilderPresenter.cs) |
| CPU デッキ構築 DI 登録（Editor 専用） | [Assets/Scripts/CpuDeckEditor/Injector/CpuDeckBuilderLifetimeScope.cs](Assets/Scripts/CpuDeckEditor/Injector/CpuDeckBuilderLifetimeScope.cs) |
| CPU デッキ構築 UXML（Editor 専用） | [Assets/Scripts/CpuDeckEditor/View/CpuDeckBuilder.uxml](Assets/Scripts/CpuDeckEditor/View/CpuDeckBuilder.uxml) |
| Home Presenter | [Assets/Scripts/Home/HomePresenter.cs](Assets/Scripts/Home/HomePresenter.cs) |
| Home Live2D Presenter | [Assets/Scripts/Home/HomeLive2DPresenter.cs](Assets/Scripts/Home/HomeLive2DPresenter.cs) |
| Home 背景 Presenter | [Assets/Scripts/Home/HomeBackgroundPresenter.cs](Assets/Scripts/Home/HomeBackgroundPresenter.cs) |
| Home 食べ物スポーン | [Assets/Scripts/Home/HomeFoodSpawner.cs](Assets/Scripts/Home/HomeFoodSpawner.cs) |
| Home 犬セリフ Presenter | [Assets/Scripts/Home/DogSpeechPresenter.cs](Assets/Scripts/Home/DogSpeechPresenter.cs) |
| Home 犬セリフ SO | [Assets/Scripts/Home/DogSpeechLinesSO.cs](Assets/Scripts/Home/DogSpeechLinesSO.cs) |
| Home DI 登録 | [Assets/Scripts/Home/Injector/HomeLifetimeScope.cs](Assets/Scripts/Home/Injector/HomeLifetimeScope.cs) |
| Home UXML | [Assets/Scripts/Home/View/Home.uxml](Assets/Scripts/Home/View/Home.uxml) |
| Home USS | [Assets/Scripts/Home/View/Home.uss](Assets/Scripts/Home/View/Home.uss) |
| Title DI 登録 | [Assets/Scripts/Title/Injector/TitleLifetimeScope.cs](Assets/Scripts/Title/Injector/TitleLifetimeScope.cs) |
| タイトル BGM/SE 管理 | [Assets/Scripts/Title/Sound/AudioManager.cs](Assets/Scripts/Title/Sound/AudioManager.cs) |
| Main BGM 管理 | [Assets/Scripts/Main/Sound/MainAudioManager.cs](Assets/Scripts/Main/Sound/MainAudioManager.cs) |
| ゲームスタートボタン Presenter | [Assets/Scripts/Title/GameStartButton/Presenter/GameStartButtonPresenter.cs](Assets/Scripts/Title/GameStartButton/Presenter/GameStartButtonPresenter.cs) |
| タイトルカード球 Presenter | [Assets/Scripts/Title/CardSphere/TitleCardSpherePresenter.cs](Assets/Scripts/Title/CardSphere/TitleCardSpherePresenter.cs) |
| タイトル背景 Presenter | [Assets/Scripts/Title/Background/TitleBackgroundPresenter.cs](Assets/Scripts/Title/Background/TitleBackgroundPresenter.cs) |
| タイトルロゴ Presenter | [Assets/Scripts/Title/Logo/TitleLogoPresenter.cs](Assets/Scripts/Title/Logo/TitleLogoPresenter.cs) |
| タイトル Ambient Glow Presenter | [Assets/Scripts/Title/AmbientGlow/TitleAmbientGlowPresenter.cs](Assets/Scripts/Title/AmbientGlow/TitleAmbientGlowPresenter.cs) |
| タイトル UXML | [Assets/Scripts/Title/GameStartButton/View/Title.uxml](Assets/Scripts/Title/GameStartButton/View/Title.uxml) |
| タイトル USS | [Assets/Scripts/Title/GameStartButton/View/Title.uss](Assets/Scripts/Title/GameStartButton/View/Title.uss) |
| ユーザーネームモーダル Presenter | [Assets/Scripts/Title/Username/UsernameModalPresenter.cs](Assets/Scripts/Title/Username/UsernameModalPresenter.cs) |
| ユーザーネームモーダル サービス | [Assets/Scripts/Title/Username/UsernameModalService.cs](Assets/Scripts/Title/Username/UsernameModalService.cs) |
| ユーザーネームモーダル UXML | [Assets/Scripts/Title/Username/View/UsernameModal.uxml](Assets/Scripts/Title/Username/View/UsernameModal.uxml) |
| マッチング Presenter | [Assets/Scripts/Matching/MatchingPresenter.cs](Assets/Scripts/Matching/MatchingPresenter.cs) |
| マッチング Presenter（ルーム一覧） | [Assets/Scripts/Matching/MatchingPresenter.RoomList.cs](Assets/Scripts/Matching/MatchingPresenter.RoomList.cs) |
| マッチングサービス | [Assets/Scripts/Matching/MatchingService.cs](Assets/Scripts/Matching/MatchingService.cs) |
| マッチング DI 登録 | [Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs](Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs) |
| オンライン対戦サービス（デッキ交換） | [Assets/Scripts/Main/Network/NetworkGameService.cs](Assets/Scripts/Main/Network/NetworkGameService.cs) |
| オンライン初期状態 | [Assets/Scripts/Main/Network/OnlineInitialState.cs](Assets/Scripts/Main/Network/OnlineInitialState.cs) |
| カード配列ユーティリティ（Shuffle） | [Assets/Scripts/Main/CardArrayUtils.cs](Assets/Scripts/Main/CardArrayUtils.cs) |
| カードデータ基底（フレーバー読み上げ話者 `_voiceSpeaker` 含む。0＝共通設定） | [Assets/Scripts/Main/Card/CardData.cs](Assets/Scripts/Main/Card/CardData.cs) |
| カード属性 enum | [Assets/Scripts/Main/Card/CardAttribute.cs](Assets/Scripts/Main/Card/CardAttribute.cs) |
| キャラカードデータ | [Assets/Scripts/Main/Card/CharacterCardData.cs](Assets/Scripts/Main/Card/CharacterCardData.cs) |
| キャラ効果トリガー enum | [Assets/Scripts/Main/Card/CharacterEffectTrigger.cs](Assets/Scripts/Main/Card/CharacterEffectTrigger.cs) |
| イベント効果トリガー enum | [Assets/Scripts/Main/Card/EventCardTrigger.cs](Assets/Scripts/Main/Card/EventCardTrigger.cs) |
| 効果ハンドラ基盤（基底・呼出パラメータ・値ラベル） | [Assets/Scripts/Main/Card/Effects/EffectHandler.cs](Assets/Scripts/Main/Card/Effects/EffectHandler.cs) / [EffectInvocation.cs](Assets/Scripts/Main/Card/Effects/EffectInvocation.cs) |
| 効果ハンドラ自動登録レジストリ | [Assets/Scripts/Main/Card/Effects/EffectCatalog.cs](Assets/Scripts/Main/Card/Effects/EffectCatalog.cs) |
| 効果ハンドラ（1効果1クラス。EventType ごとの演出＋適用＋エディタ用テキスト/値ラベル） | [Assets/Scripts/Main/Card/Effects/Handlers/](Assets/Scripts/Main/Card/Effects/Handlers/) |
| イベントカードデータ | [Assets/Scripts/Main/Card/EventCardData.cs](Assets/Scripts/Main/Card/EventCardData.cs) |
| キャラカードSO | [Assets/Scripts/Main/Card/CharacterCardSO.cs](Assets/Scripts/Main/Card/CharacterCardSO.cs) |
| イベントカードSO | [Assets/Scripts/Main/Card/EventCardSO.cs](Assets/Scripts/Main/Card/EventCardSO.cs) |
| カードDB（ScriptableObject） | [Assets/Scripts/Main/Card/CardDatabase.cs](Assets/Scripts/Main/Card/CardDatabase.cs) |
| 特徴（キーワード）マスターSO | [Assets/Scripts/Main/Card/CardKeywordSO.cs](Assets/Scripts/Main/Card/CardKeywordSO.cs) |
| 付与キーワード能力 enum（GrantKeyword の値2対応・守護/速攻/飛行/防人） | [Assets/Scripts/Main/Card/GrantableKeyword.cs](Assets/Scripts/Main/Card/GrantableKeyword.cs) |
| カードID自動採番（属性別・Editor 専用） | [Assets/Scripts/Main/Card/CardIdAutoAssigner.cs](Assets/Scripts/Main/Card/CardIdAutoAssigner.cs) |
| カード編集ウィンドウ（検索・編集・追加・削除・並び替え（ID/コスト順）・ID再採番・効果テキスト一括生成・ゲーム使用/不使用切替・読み上げ話者の個別指定。Editor 専用） | [Assets/Scripts/Editor/CardEditorWindow.cs](Assets/Scripts/Editor/CardEditorWindow.cs) |
| VOICEVOX 話者カタログ（読み上げ話者ドロップダウン用の静的一覧・Editor 専用） | [Assets/Scripts/Editor/VoiceSpeakerCatalog.cs](Assets/Scripts/Editor/VoiceSpeakerCatalog.cs) |
| 属性別SO分割ツール（Editor 専用） | [Assets/Scripts/Editor/CardSoAttributeSplitter.cs](Assets/Scripts/Editor/CardSoAttributeSplitter.cs) |
| インスペクタ読み取り専用属性 | [Assets/Scripts/Main/Card/ReadOnlyAttribute.cs](Assets/Scripts/Main/Card/ReadOnlyAttribute.cs) / [Assets/Scripts/Editor/ReadOnlyDrawer.cs](Assets/Scripts/Editor/ReadOnlyDrawer.cs) |
| カード UI（HP/ATK・バフ・付与キーワード・タップ状態 IsTapped） | [Assets/Scripts/Main/Card/CardView.cs](Assets/Scripts/Main/Card/CardView.cs) |
| カード詳細モーダル | [Assets/Scripts/Main/Card/CardDetailModal.cs](Assets/Scripts/Main/Card/CardDetailModal.cs) |
| カードスケール定数 | [Assets/Scripts/Main/Card/CardScaleConstants.cs](Assets/Scripts/Main/Card/CardScaleConstants.cs) |
| 手札 UI | [Assets/Scripts/Main/Card/HandView.cs](Assets/Scripts/Main/Card/HandView.cs) |
| フィールド UI | [Assets/Scripts/Main/Card/FieldView.cs](Assets/Scripts/Main/Card/FieldView.cs) |
| デッキ UI | [Assets/Scripts/Main/Card/DeckView.cs](Assets/Scripts/Main/Card/DeckView.cs) |
| 墓地 UI | [Assets/Scripts/Main/Card/GraveyardView.cs](Assets/Scripts/Main/Card/GraveyardView.cs) |
| 勝利点 UI（共通の勝利条件・常時表示） | [Assets/Scripts/Main/Card/VictoryPointsView.cs](Assets/Scripts/Main/Card/VictoryPointsView.cs) |
| 経過ターン UI（通算ターン・左下常時表示） | [Assets/Scripts/Main/Card/TurnCounterView.cs](Assets/Scripts/Main/Card/TurnCounterView.cs) |
| 手番の残り時間 UI（自分のメインフェーズ中のみ表示・残り少で赤警告） | [Assets/Scripts/Main/Card/TurnTimerView.cs](Assets/Scripts/Main/Card/TurnTimerView.cs) |
| Main Presenter（手番の制限時間・自動パス） | [Assets/Scripts/Main/MainPresenter.TurnTimer.cs](Assets/Scripts/Main/MainPresenter.TurnTimer.cs) |
| ドラッグ操作 | [Assets/Scripts/Main/Card/CardDragManipulator.cs](Assets/Scripts/Main/Card/CardDragManipulator.cs) |
| 攻撃矢印描画 | [Assets/Scripts/Main/Card/ArrowView.cs](Assets/Scripts/Main/Card/ArrowView.cs) |
| 攻撃矢印操作 | [Assets/Scripts/Main/Card/AttackArrowManipulator.cs](Assets/Scripts/Main/Card/AttackArrowManipulator.cs) |
| カードアセットロード | [Assets/Scripts/Main/Card/CardStore.cs](Assets/Scripts/Main/Card/CardStore.cs) |
| カード状態 | [Assets/Scripts/Main/Card/CardState.cs](Assets/Scripts/Main/Card/CardState.cs) |
| ゲームロジック | [Assets/Scripts/Main/Game/GameModel.cs](Assets/Scripts/Main/Game/GameModel.cs) |
| ターンフェーズ定義 | [Assets/Scripts/Main/Game/TurnPhase.cs](Assets/Scripts/Main/Game/TurnPhase.cs) |
| 共通の勝利条件ルール（デッキ切れ/勝利点20/キャラ8体） | [Assets/Scripts/Main/Game/WinRule.cs](Assets/Scripts/Main/Game/WinRule.cs) |
| 初期手札枚数ルール（先攻3枚/後攻5枚） | [Assets/Scripts/Main/Game/MulliganRule.cs](Assets/Scripts/Main/Game/MulliganRule.cs) |
| 勝因の種別 enum | [Assets/Scripts/Main/Game/WinReason.cs](Assets/Scripts/Main/Game/WinReason.cs) |
| CPU エージェント | [Assets/Scripts/Main/Game/CpuAgent.cs](Assets/Scripts/Main/Game/CpuAgent.cs) |
| Main Presenter（フィールド・BuildAsync） | [Assets/Scripts/Main/MainPresenter.cs](Assets/Scripts/Main/MainPresenter.cs) |
| Main Presenter（マリガン） | [Assets/Scripts/Main/MainPresenter.Mulligan.cs](Assets/Scripts/Main/MainPresenter.Mulligan.cs) |
| Main Presenter（投了・タイムアウト・退室） | [Assets/Scripts/Main/MainPresenter.Surrender.cs](Assets/Scripts/Main/MainPresenter.Surrender.cs) |
| Main Presenter（再戦） | [Assets/Scripts/Main/MainPresenter.Rematch.cs](Assets/Scripts/Main/MainPresenter.Rematch.cs) |
| Main Presenter（ゲームループ・InitializeFirstTurn・OnGameEnd・ターン終了時の守護/防人タップ） | [Assets/Scripts/Main/MainPresenter.Phases.cs](Assets/Scripts/Main/MainPresenter.Phases.cs) |
| Main Presenter（ドローフェーズ） | [Assets/Scripts/Main/MainPresenter.Phases.Draw.cs](Assets/Scripts/Main/MainPresenter.Phases.Draw.cs) |
| Main Presenter（メインフェーズ・CPU選択・タップ/アンタップ・攻撃可否判定） | [Assets/Scripts/Main/MainPresenter.Phases.Main.cs](Assets/Scripts/Main/MainPresenter.Phases.Main.cs) |
| Main Presenter（攻撃・デッキ攻撃・ミル・墓地飛行・攻撃時タップ） | [Assets/Scripts/Main/MainPresenter.Phases.Main.Attack.cs](Assets/Scripts/Main/MainPresenter.Phases.Main.Attack.cs) |
| Main Presenter（イベント効果即時解決・解決オーケストレーション） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.cs) |
| Main Presenter（効果適用：回復・バフ・キーワード付与＋味方対象選択） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.Buff.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.Buff.cs) |
| Main Presenter（効果適用：全体/単体ダメージ・消滅＋敵対象選択） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.Damage.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.Damage.cs) |
| Main Presenter（効果適用：バウンス・召喚・破壊・進化・スイッチ・ドロー） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.Board.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.Board.cs) |
| Main Presenter（デッキから特徴指定で召喚） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.SummonFromDeck.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.SummonFromDeck.cs) |
| Main Presenter（自キャラのコピーを出す） | [Assets/Scripts/Main/MainPresenter.Phases.Resolution.CopyChar.cs](Assets/Scripts/Main/MainPresenter.Phases.Resolution.CopyChar.cs) |
| Main Presenter（入力・UIヘルパー） | [Assets/Scripts/Main/MainPresenter.Input.cs](Assets/Scripts/Main/MainPresenter.Input.cs) |
| Main Presenter（コスト選択 UI） | [Assets/Scripts/Main/MainPresenter.Input.CostSelection.cs](Assets/Scripts/Main/MainPresenter.Input.CostSelection.cs) |
| Main Presenter（告知・OKフラッシュ・トースト） | [Assets/Scripts/Main/MainPresenter.Animations.cs](Assets/Scripts/Main/MainPresenter.Animations.cs) |
| Main Presenter（コイントス演出） | [Assets/Scripts/Main/MainPresenter.Animations.Coin.cs](Assets/Scripts/Main/MainPresenter.Animations.Coin.cs) |
| Main Presenter（攻撃演出） | [Assets/Scripts/Main/MainPresenter.Animations.Attack.cs](Assets/Scripts/Main/MainPresenter.Animations.Attack.cs) |
| Main Presenter（カード飛行・ドロー・召喚登場演出） | [Assets/Scripts/Main/MainPresenter.Animations.CardFly.cs](Assets/Scripts/Main/MainPresenter.Animations.CardFly.cs) |
| Main Presenter（コスト支払い演出） | [Assets/Scripts/Main/MainPresenter.Animations.CostFly.cs](Assets/Scripts/Main/MainPresenter.Animations.CostFly.cs) |
| Main Presenter（エフェクト・ゲームエンド・花火） | [Assets/Scripts/Main/MainPresenter.Animations.Effects.cs](Assets/Scripts/Main/MainPresenter.Animations.Effects.cs) |
| Main DI 登録 | [Assets/Scripts/Main/Injector/MainLifetimeScope.cs](Assets/Scripts/Main/Injector/MainLifetimeScope.cs) |
| Main UXML | [Assets/Scripts/Main/View/Main.uxml](Assets/Scripts/Main/View/Main.uxml) |
| Main USS（盤面レイアウト／演出別に4分割。Overlays=全画面モーダル・FloatingLabels=効果ラベル/トースト・Highlights=選択/攻撃枠） | [Assets/Scripts/Main/View/](Assets/Scripts/Main/View/) |
| カード UXML テンプレート | [Assets/AddressableAssets/Card/Card.uxml](Assets/AddressableAssets/Card/Card.uxml) |
| カード詳細モーダル共有 USS | [Assets/Scripts/Common/View/CardDetail.uss](Assets/Scripts/Common/View/CardDetail.uss) |
| 花火加算ブレンドシェーダー | [Assets/Shaders/FireworkAdditiveUI.shader](Assets/Shaders/FireworkAdditiveUI.shader) |
| 日本語フォント | [Assets/Font/](Assets/Font/) |

## ドキュメント

- [docs/architecture.md](docs/architecture.md): アーキテクチャドキュメント
- [docs/design-system.md](docs/design-system.md): UIデザインシステム（カラー・タイポグラフィ・コンポーネント）
- [docs/product.md](docs/product.md): プロダクトドキュメント
- [docs/matchmaking.md](docs/matchmaking.md): マッチメイキング設計（UGS Multiplayer Services）
- [docs/rules.md](docs/rules.md): ゲームルール（ターンフロー・カード状態・処理フェーズ）
- [docs/event.md](docs/event.md): カード効果（EventType）一覧とキャラ・イベントカードへの設定方法
- [docs/patterns.md](docs/patterns.md): よく触る実装パターン集（カード追加・フェーズ追加・DI登録など）
- [docs/Live2D.md](docs/Live2D.md): Live2D アニメーション実装ノウハウ（ハマりポイント・対処法）
- [docs/effects.md](docs/effects.md): パーティクル・VFX エフェクト実装ノウハウ（UI Toolkit との共存・加算ブレンド・レイヤー設定）
- [docs/networking.md](docs/networking.md): NGO + MPM ネットワーク実装ノウハウ（ハマりポイント・対処法）

## Asset Store アセット

- Asset Store からダウンロードしたものは `Assets/AssetStore/` に配置する。このディレクトリは Git の管理対象外。
- DoTween (Demigiant) は `Assets/Plugins/` に配置済み（Git 管理対象）。
- Live2D Cubism SDK は `Assets/Live2D/` に配置済み（Git 管理対象）。
  - **Live2D に関する機能を実装・修正する際は必ず [docs/Live2D.md](docs/Live2D.md) を読むこと。** アニメーション再生のハマりポイントと対処法を記載している。
  - `Assets/csc.rsp` に `-unsafe` フラグが必要（Cubism Core が unsafe コードを使用するため）。
  - `Assets/Live2D/Dog-kid/`: Dog-kid キャラクター（Home 画面の Live2D キャラ）
  - `Assets/Live2D/Food/`: Food キャラクター（Home 画面でクリック時にスポーン）
  - `Assets/Scripts/Editor/DogKidFadeMotionListSetup.cs`: メニュー `Live2D/Setup Dog-kid FadeMotionList` で FadeMotionList を自動セットアップするエディタスクリプト

# CLAUDE.md

このファイルはリポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

Unity 6 (6000.3.11f1) で開発するマルチプレイヤーカードゲーム。BGM/SE 再生機能と、Commonシーンをベースとしたアディティブシーン管理を備える。UGS（Unity Gaming Services）を使ったオンラインマッチメイキング機能を実装する。

## Unity 開発

ビルドと実行は Unity Editor (Unity 6000.3.11f1) を通じて行う。独立したビルドスクリプトは存在しない。

- **テスト実行 (EditMode)**: Unity Editor → Window → General → Test Runner → EditMode タブ → Run All
- **テスト実行 (PlayMode)**: Unity Editor → Window → General → Test Runner → PlayMode タブ → Run All
- **ビルド**: Unity Editor → File → Build Settings → Build

## テスト構成

| ディレクトリ | 種別 | 内容 |
|---|---|---|
| [Assets/Tests/PlayMode/](Assets/Tests/PlayMode/) | PlayMode | シーンロードを伴う統合テスト |
| [Assets/Tests/EditMode/](Assets/Tests/EditMode/) | EditMode | 純粋ロジックの単体テスト |

**PlayMode テストの注意点:**
- `CommonSceneLoader` が `static bool _loaded` を持つため、`[UnityTearDown]` で reflection リセットが必要
- `IAsyncStartable.StartAsync` は VContainer からキャンセルトークンを受け取るため `catch (OperationCanceledException)` で正常終了させること
- ボタンクリック模擬は `NavigationSubmitEvent`（`ClickEvent` では Clickable が反応しない）

**EditMode テストの注意点:**
- asmdef の `references` に、テスト対象クラスのアセンブリ GUID とその直接依存アセンブリ GUID を追加する（推移的参照は自動解決されない）
- `R3.dll` を `precompiledReferences` に追加が必要
- `ReadOnlyReactiveProperty<T>` の値は `.CurrentValue`（`.Value` は不可）。`ReactiveProperty<T>` は `.Value` で読み書き可
- R3 の Subscribe 拡張メソッドには `using R3;` が必要
- UniTask の同期完了タスクは `.GetAwaiter().GetResult()` でテスト可能（null ガード等の即完了ケース）

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
| 遷移演出 | [Assets/Scripts/Common/Transition/TransitionPresenter.cs](Assets/Scripts/Common/Transition/TransitionPresenter.cs) |
| サウンド再生 | [Assets/Scripts/Common/SoundManagement/SoundPlayer.cs](Assets/Scripts/Common/SoundManagement/SoundPlayer.cs) |
| ボリューム状態モデル | [Assets/Scripts/Common/Option/OptionModel.cs](Assets/Scripts/Common/Option/OptionModel.cs) |
| セッション保持（Common） | [Assets/Scripts/Common/GameSession/GameSessionModel.cs](Assets/Scripts/Common/GameSession/GameSessionModel.cs) |
| デッキ構築モデル | [Assets/Scripts/Common/Deck/DeckModel.cs](Assets/Scripts/Common/Deck/DeckModel.cs) |
| デッキ保存 | [Assets/Scripts/Common/Deck/DeckRepository.cs](Assets/Scripts/Common/Deck/DeckRepository.cs) |
| CPU デッキ保存 | [Assets/Scripts/Common/Deck/CpuDeckRepository.cs](Assets/Scripts/Common/Deck/CpuDeckRepository.cs) |
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
| Home DI 登録 | [Assets/Scripts/Home/Injector/HomeLifetimeScope.cs](Assets/Scripts/Home/Injector/HomeLifetimeScope.cs) |
| Home UXML | [Assets/Scripts/Home/View/Home.uxml](Assets/Scripts/Home/View/Home.uxml) |
| Home USS | [Assets/Scripts/Home/View/Home.uss](Assets/Scripts/Home/View/Home.uss) |
| Title DI 登録 | [Assets/Scripts/Title/Injector/TitleLifetimeScope.cs](Assets/Scripts/Title/Injector/TitleLifetimeScope.cs) |
| BGM/SE 管理 | [Assets/Scripts/Title/Sound/AudioManager.cs](Assets/Scripts/Title/Sound/AudioManager.cs) |
| ゲームスタートボタン Presenter | [Assets/Scripts/Title/GameStartButton/Presenter/GameStartButtonPresenter.cs](Assets/Scripts/Title/GameStartButton/Presenter/GameStartButtonPresenter.cs) |
| タイトルカード球 Presenter | [Assets/Scripts/Title/CardSphere/TitleCardSpherePresenter.cs](Assets/Scripts/Title/CardSphere/TitleCardSpherePresenter.cs) |
| タイトル背景 Presenter | [Assets/Scripts/Title/Background/TitleBackgroundPresenter.cs](Assets/Scripts/Title/Background/TitleBackgroundPresenter.cs) |
| タイトルロゴ Presenter | [Assets/Scripts/Title/Logo/TitleLogoPresenter.cs](Assets/Scripts/Title/Logo/TitleLogoPresenter.cs) |
| タイトル UXML | [Assets/Scripts/Title/GameStartButton/View/Title.uxml](Assets/Scripts/Title/GameStartButton/View/Title.uxml) |
| タイトル USS | [Assets/Scripts/Title/GameStartButton/View/Title.uss](Assets/Scripts/Title/GameStartButton/View/Title.uss) |
| マッチングサービス | [Assets/Scripts/Matching/MatchingService.cs](Assets/Scripts/Matching/MatchingService.cs) |
| マッチング DI 登録 | [Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs](Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs) |
| カード属性列挙型 | [Assets/Scripts/Main/Card/CardAttribute.cs](Assets/Scripts/Main/Card/CardAttribute.cs) |
| 属性DB（アイコン・弱点） | [Assets/Scripts/Main/Card/AttributeDatabaseSO.cs](Assets/Scripts/Main/Card/AttributeDatabaseSO.cs) |
| カードデータ基底 | [Assets/Scripts/Main/Card/CardData.cs](Assets/Scripts/Main/Card/CardData.cs) |
| キャラカードデータ | [Assets/Scripts/Main/Card/CharacterCardData.cs](Assets/Scripts/Main/Card/CharacterCardData.cs) |
| 技カードデータ | [Assets/Scripts/Main/Card/SkillCardData.cs](Assets/Scripts/Main/Card/SkillCardData.cs) |
| イベントカードデータ | [Assets/Scripts/Main/Card/EventCardData.cs](Assets/Scripts/Main/Card/EventCardData.cs) |
| キャラカードSO | [Assets/Scripts/Main/Card/CharacterCardSO.cs](Assets/Scripts/Main/Card/CharacterCardSO.cs) |
| 技カードSO | [Assets/Scripts/Main/Card/SkillCardSO.cs](Assets/Scripts/Main/Card/SkillCardSO.cs) |
| イベントカードSO | [Assets/Scripts/Main/Card/EventCardSO.cs](Assets/Scripts/Main/Card/EventCardSO.cs) |
| カードDB（ScriptableObject） | [Assets/Scripts/Main/Card/CardDatabase.cs](Assets/Scripts/Main/Card/CardDatabase.cs) |
| カード UI | [Assets/Scripts/Main/Card/CardView.cs](Assets/Scripts/Main/Card/CardView.cs) |
| カード詳細モーダル | [Assets/Scripts/Main/Card/CardDetailModal.cs](Assets/Scripts/Main/Card/CardDetailModal.cs) |
| キャラスロット UI | [Assets/Scripts/Main/Card/CharacterSlotView.cs](Assets/Scripts/Main/Card/CharacterSlotView.cs) |
| 手札 UI | [Assets/Scripts/Main/Card/HandView.cs](Assets/Scripts/Main/Card/HandView.cs) |
| フィールド UI | [Assets/Scripts/Main/Card/FieldView.cs](Assets/Scripts/Main/Card/FieldView.cs) |
| デッキ UI | [Assets/Scripts/Main/Card/DeckView.cs](Assets/Scripts/Main/Card/DeckView.cs) |
| 墓地 UI | [Assets/Scripts/Main/Card/GraveyardView.cs](Assets/Scripts/Main/Card/GraveyardView.cs) |
| ドラッグ操作 | [Assets/Scripts/Main/Card/CardDragManipulator.cs](Assets/Scripts/Main/Card/CardDragManipulator.cs) |
| 攻撃矢印描画 | [Assets/Scripts/Main/Card/ArrowView.cs](Assets/Scripts/Main/Card/ArrowView.cs) |
| 攻撃矢印操作 | [Assets/Scripts/Main/Card/AttackArrowManipulator.cs](Assets/Scripts/Main/Card/AttackArrowManipulator.cs) |
| カードアセットロード | [Assets/Scripts/Main/Card/CardStore.cs](Assets/Scripts/Main/Card/CardStore.cs) |
| カード状態 | [Assets/Scripts/Main/Card/CardState.cs](Assets/Scripts/Main/Card/CardState.cs) |
| ゲームロジック | [Assets/Scripts/Main/Game/GameModel.cs](Assets/Scripts/Main/Game/GameModel.cs) |
| ターンフェーズ定義 | [Assets/Scripts/Main/Game/TurnPhase.cs](Assets/Scripts/Main/Game/TurnPhase.cs) |
| CPU エージェント | [Assets/Scripts/Main/Game/CpuAgent.cs](Assets/Scripts/Main/Game/CpuAgent.cs) |
| Main Presenter（フィールド・BuildAsync） | [Assets/Scripts/Main/MainPresenter.cs](Assets/Scripts/Main/MainPresenter.cs) |
| Main Presenter（フェーズ処理） | [Assets/Scripts/Main/MainPresenter.Phases.cs](Assets/Scripts/Main/MainPresenter.Phases.cs) |
| Main Presenter（入力・UIヘルパー） | [Assets/Scripts/Main/MainPresenter.Input.cs](Assets/Scripts/Main/MainPresenter.Input.cs) |
| Main Presenter（アニメーション） | [Assets/Scripts/Main/MainPresenter.Animations.cs](Assets/Scripts/Main/MainPresenter.Animations.cs) |
| Main DI 登録 | [Assets/Scripts/Main/Injector/MainLifetimeScope.cs](Assets/Scripts/Main/Injector/MainLifetimeScope.cs) |
| カード UXML テンプレート | [Assets/AddressableAssets/Card/Card.uxml](Assets/AddressableAssets/Card/Card.uxml) |
| 日本語フォント | [Assets/Font/](Assets/Font/) |

## ドキュメント

- [docs/architecture.md](docs/architecture.md): アーキテクチャドキュメント
- [docs/design-system.md](docs/design-system.md): UIデザインシステム（カラー・タイポグラフィ・コンポーネント）
- [docs/product.md](docs/product.md): プロダクトドキュメント
- [docs/matchmaking.md](docs/matchmaking.md): マッチメイキング設計（UGS Multiplayer Services）
- [docs/rules.md](docs/rules.md): ゲームルール（ターンフロー・カード状態・処理フェーズ）
- [docs/patterns.md](docs/patterns.md): よく触る実装パターン集（カード追加・フェーズ追加・DI登録など）

## Asset Store アセット

- Asset Store からダウンロードしたものは `Assets/AssetStore/` に配置する。このディレクトリは Git の管理対象外。
- DoTween (Demigiant) は `Assets/Plugins/` に配置済み（Git 管理対象）。
- Live2D Cubism SDK は `Assets/Live2D/` に配置済み（Git 管理対象）。
  - `Assets/csc.rsp` に `-unsafe` フラグが必要（Cubism Core が unsafe コードを使用するため）。

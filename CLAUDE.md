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
| マッチングサービス | [Assets/Scripts/Matching/MatchingService.cs](Assets/Scripts/Matching/MatchingService.cs) |
| マッチング DI 登録 | [Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs](Assets/Scripts/Matching/Injector/MatchingLifetimeScope.cs) |
| カードデータ定義 | [Assets/Scripts/Main/Card/CardData.cs](Assets/Scripts/Main/Card/CardData.cs) |
| カードDB（ScriptableObject） | [Assets/Scripts/Main/Card/CardDatabase.cs](Assets/Scripts/Main/Card/CardDatabase.cs) |
| カード UI | [Assets/Scripts/Main/Card/CardView.cs](Assets/Scripts/Main/Card/CardView.cs) |
| 手札 UI | [Assets/Scripts/Main/Card/HandView.cs](Assets/Scripts/Main/Card/HandView.cs) |
| フィールド UI | [Assets/Scripts/Main/Card/FieldView.cs](Assets/Scripts/Main/Card/FieldView.cs) |
| デッキ UI | [Assets/Scripts/Main/Card/DeckView.cs](Assets/Scripts/Main/Card/DeckView.cs) |
| ドラッグ操作 | [Assets/Scripts/Main/Card/CardDragManipulator.cs](Assets/Scripts/Main/Card/CardDragManipulator.cs) |
| カードアセットロード | [Assets/Scripts/Main/Card/CardStore.cs](Assets/Scripts/Main/Card/CardStore.cs) |
| Main DI 登録 | [Assets/Scripts/Main/Injector/MainLifetimeScope.cs](Assets/Scripts/Main/Injector/MainLifetimeScope.cs) |
| カード UXML テンプレート | [Assets/AddressableAssets/Card/Card.uxml](Assets/AddressableAssets/Card/Card.uxml) |
| 日本語フォント | [Assets/Font/](Assets/Font/) |

## ドキュメント

- [docs/architecture.md](docs/architecture.md): アーキテクチャドキュメント
- [docs/design-system.md](docs/design-system.md): UIデザインシステム（カラー・タイポグラフィ・コンポーネント）
- [docs/product.md](docs/product.md): プロダクトドキュメント
- [docs/matchmaking.md](docs/matchmaking.md): マッチメイキング設計（UGS Multiplayer Services）

## Asset Store アセット

- Asset Store からダウンロードしたものは `Assets/AssetStore/` に配置する。このディレクトリは Git の管理対象外。
- DoTween (Demigiant) は `Assets/Plugins/` に配置済み（Git 管理対象）。
- Live2D Cubism SDK は `Assets/Live2D/` に配置済み（Git 管理対象）。
  - `Assets/csc.rsp` / `Assets/mcs.rsp` に `-unsafe` フラグが必要（Cubism Core が unsafe コードを使用するため）。

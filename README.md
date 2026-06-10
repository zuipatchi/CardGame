# Card
マルチプレイヤーカードゲーム
ここには開発者向けのメモを記載する

## 開発の進め方
1. `/feature` を実行して新機能を実装する（ヒアリング→実装→レビューまで自動で進む）
2. Unity Editor で Play して動作確認する（自動テストは作成しない方針。CLAUDE.md 参照）
3. 問題なければ `/ship` を実行してコミット・ドキュメント更新まで行う

## テストプレイの仕方
Multiplayer play mode を使用してオンライン対戦を行う


## 使用 Package
- Addressables
- R3
- UniTask
- VContainer
- DoTween
- UnityGamingService
- Live2D Cubism SDK（`Assets/Live2D/`、Git 管理対象）

## プラットフォーム
Windows MacOS

## 日本語フォント
- uxmlで日本語フォントを使用したい場合は /Assets/Font に入っている日本語フォントを使用する

## gitignore
- Asset Storeからダウンロードした物は AssetStore ディレクトリに入れるとGitに管理されない

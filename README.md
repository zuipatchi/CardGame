# Card
マルチプレイヤーカードゲーム
ここには開発者向けのメモを記載する

## 開発の進め方
1. `/feature` を実行して新機能を実装する（ヒアリング→実装→テストまで自動で進む）
2. PlayMode: Window → General → Test Runner → PlayMode タブ → Run All で自動テストを実行する
3. Unity Editor で Play して動作確認する
4. 問題なければ `/ship` を実行してコミット・ドキュメント更新まで行う

## テストプレイの仕方
決まったら記載する

## 機能一覧
- BGMとSEの再生機能
- シーン管理機能（Commonシーンをベースに他のシーンを使用する）
  - フェードイン/アウトによる画面遷移演出
- オプション機能
  - BGM音量
  - SE音量
  - タイトルに戻る
- オンラインマッチング機能（UGS Multiplayer Services）
  - クイックマッチ（空きルームへ自動参加 or 作成して待機）
  - ルーム一覧から手動参加
  - 20秒タイムアウト＋リトライ確認
- カードゲーム盤面（Main シーン）
  - カードテンプレート（UXML）+ カードデータベース（ScriptableObject）
  - 手札：初期5枚をシャッフル後に扇状表示（画面下部中央）、ホバーで拡大＋最前面表示
  - フィールド：横長エリアに最大5枚を中央寄せ表示（画面中央）、ドラッグ&ドロップで手札から配置
  - デッキ：残りのカードを裏向きで積み重ね表示（右下）
  - カード裏返しアニメーション（DOTween）

## 使用 Package
- Addressables
- R3
- UniTask
- VContainer
- DoTween
- UnityGamingService
- Live2D Cubism SDK（`Assets/Live2D/`、Git 管理対象）

## プラットフォーム
決まったら記載する

## 日本語フォント
- uxmlで日本語フォントを使用したい場合は /Assets/Font に入っている日本語フォントを使用する

## gitignore
- Asset Storeからダウンロードした物は AssetStore ディレクトリに入れるとGitに管理されない

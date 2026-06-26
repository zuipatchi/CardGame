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

## カードのフレーバー音声生成（VOICEVOX）
カードのフレーバーテキスト読み上げ音声は [VOICEVOX](https://voicevox.hiroshiba.jp/)（無料）で事前生成する。
- VOICEVOX アプリ（またはエンジン）を起動した状態で、Unity メニュー `Card → フレーバー音声を一括生成` を実行
- 全カードの FlavorText を WAV 化して `Assets/AddressableAssets/Voice/{CardId}.wav` に保存し、Addressables アドレス `Voice/{CardId}` に自動登録する（既定では全カードを生成。「既存をスキップ（差分のみ生成）」を ON にすると WAV があるカードを再生成せず差分のみ生成する）
- 読み上げる声は `Card → カードエディタ` の「読み上げ話者」でカードごとに指定できる。未指定のカードは生成ツールの「既定の話者 ID」（既定 3＝ずんだもん）で生成する。差分だけ素早く作りたいときは「既存をスキップ」を ON にする
- 話者一覧は `VoiceSpeakerCatalog.cs`（VOICEVOX 0.16.0 の `/speakers` を静的化）で管理。VOICEVOX 更新で ID が変わったら取り直して更新する
- 生成した音声 WAV は Git 管理対象（Addressables の GUID 参照を壊さないため）。再生成が必要なのはカード追加・文言変更・話者変更のときのみ
- 配布時は使用した話者分のクレジット表記（例 `VOICEVOX:ずんだもん`）が必要

## プラットフォーム
WebGL unityroom

## 日本語フォント
- uxmlで日本語フォントを使用したい場合は /Assets/Font に入っている日本語フォントを使用する

## gitignore
- Asset Storeからダウンロードした物は AssetStore ディレクトリに入れるとGitに管理されない

## クレジット
ゲーム内クレジット（Home シーン右下「クレジット」ボタン）と同じ内容。表示元は [Assets/Scripts/Home/View/Home.uxml](Assets/Scripts/Home/View/Home.uxml) なので、変更時は両方を更新する。

### 制作スタッフ
- 企画・制作：深田 光晴
- プログラム：Claude Code
- イラスト：Nano Banana

### テスター
- テトラマックス、有馬、どえむ、ザニザニマン

### サウンド
- BGM：魔王魂、ぱっち
- 効果音：効果音ラボ

### 使用OSS / アセット
- VOICEVOX（フレーバーテキスト読み上げ）
- Live2D Cubism SDK / Live2D Inc.
- DoTween / Demigiant
- Noto Sans JP / Google Fonts
- VContainer・R3・UniTask
- Free Quick Effects Vol.1 / Gabriel Aguiar Productions（Asset Store）
- Cartoon FX Remaster / Jean Moreno (JMO Assets)（Asset Store）

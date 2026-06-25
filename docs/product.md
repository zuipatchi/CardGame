# プロダクトドキュメント

実装済み機能の索引。各機能の詳細な挙動・仕様は右側のリンク先ドキュメントに集約する（このファイルは「何が実装されているか」の一覧に徹し、仕様の本文は持たない）。

## ゲーム概要

Unity 6 で開発するオンラインマルチプレイヤーカードゲーム。
UGS Multiplayer Services を使ったオンラインマッチメイキングに対応。

ターンフロー・カード状態・コスト・勝敗条件などルールの全体像は [docs/rules.md](rules.md) を参照。

## 実装済み機能

### 基盤・共通

- BGM / SE 再生（音量調整・永続化）→ [architecture.md](architecture.md)「サウンド設計」
- フレーバーテキスト読み上げ（事前生成 VOICEVOX 音声。話者はカードごとに指定可）→ [architecture.md](architecture.md)「サウンド設計」
- Common シーンをベースとしたアディティブシーン管理・フェード画面遷移演出 → [architecture.md](architecture.md)「シーン構成」
- オプションモーダル（BGM / SE / ボイス音量・Auto OK・タイトルに戻る／降参／ホームに戻る）→ [architecture.md](architecture.md)「オプションモーダル」

### ホーム

- Live2D キャラクター（Dog-kid）表示・犬のセリフ吹き出し・食べ物スポーン演出 → [architecture.md](architecture.md)「Home シーン」
- クレジットモーダル・詳しいルール（遊び方）モーダル（8タブ）→ [architecture.md](architecture.md)「クレジット・遊び方モーダル」
- ユーザーネーム表示・編集（左上・✎ボタン）
- CPU 相手選択カルーセル（名前・ポートレート・難易度・デッキを相手ごとに保持。再戦は同じ相手）
- 対戦ガード（デッキが 30 枚でないと CPU / オンライン対戦をブロック）

### チュートリアル

- 誘導つきスクリプト対戦（カードの見方／基本／攻撃／勝ち方3種／キーワード能力7種）→ [architecture.md](architecture.md)「チュートリアル」・[MainPresenter.Tutorial.cs](../Assets/Scripts/Main/MainPresenter.Tutorial.cs)

### マッチング・オンライン

- オンラインマッチング（クイックマッチ・ルーム一覧から手動参加）→ [docs/matchmaking.md](matchmaking.md)
- 対戦開始待機表示（「対戦相手を待っています...」）
- デッキ交換・ゲーム同期 → [docs/networking.md](networking.md)

### デッキ構築

- 9スロット制デッキ管理（シンボルカード・名前変更・自動保存・使用デッキ選択）→ [architecture.md](architecture.md)「DeckBuilder シーン」
- カード一覧の種別／属性／コストフィルタ・整列／コスト順ソート・デッキ分析モーダル → [architecture.md](architecture.md)「DeckBuilder シーン」
- デッキ構築ルール（同名3枚制限・30枚制限。Editor 再生時のみトグルで解除可）→ [docs/rules.md](rules.md)「デッキ構築ルール」

### 対戦（ゲームプレイ）

- 先攻・後攻決定（コイントス）・マリガン → [docs/rules.md](rules.md)「ゲーム開始時フロー」
- ターン管理・フェーズ構成（ドロー → メイン）・手番の制限時間（90秒・自動パス）・手札上限（8枚）→ [docs/rules.md](rules.md)「ターンフロー」
- メインフェーズの各アクション（キャラを出す／イベントを使う／攻撃する／デッキ攻撃＝ミル／パス）→ [docs/rules.md](rules.md)「メインフェーズの詳細」
- タップ・召喚酔い・攻撃可否判定 → [docs/rules.md](rules.md)「タップ」
- コストシステム・属性制約（7属性 Red / Blue / Green / Yellow / Black / Purple / White）→ [docs/rules.md](rules.md)「コストシステム」
- イベントカード効果・キャラ効果・コスト倍化（CostBoost）→ [docs/event.md](event.md)
- キーワード能力8種（守護・速攻・飛行・防人・強襲・デッキ攻撃×・射手・必殺）→ [docs/rules.md](rules.md)「攻撃の詳細」
- 共通の勝利条件4種（デッキ切れ＝オーバーリミット／勝利点20／キャラ8体／特殊勝利＝太郎勝利）・勝敗演出 → [docs/rules.md](rules.md)「勝敗条件」
- 再戦 → [docs/networking.md](networking.md)「再戦」
- CPU エージェント（難易度：初級／中級／上級）→ [architecture.md](architecture.md)「CPU 処理」
- ゲームプレイ演出（攻撃・ダメージ・ターン開始告知・PASS・ドロー・コイントス・キャラ破壊・勝敗紋章 など）→ [architecture.md](architecture.md)「Main シーン」

## 未実装（今後の課題）

- プレイヤープロフィール・ランキング

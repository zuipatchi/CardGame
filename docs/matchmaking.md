# マッチメイキング設計ドキュメント

## 概要

Unity Gaming Services (UGS) の **`com.unity.services.multiplayer`** を使ったオンラインマッチング機能。
クイックマッチ（自動マッチング）またはルーム一覧からの手動参加に対応。Relay による NAT 越え対応。

---

## 使用パッケージ

| パッケージ | バージョン | 用途 |
|---|---|---|
| `com.unity.services.multiplayer` | 2.1.3 | Session / Authentication / Relay 統合 SDK |
| `com.unity.netcode.gameobjects` | 2.9.2 | NGO ネットワーク通信 |
| `com.unity.multiplayer.playmode` | 2.0.2 | エディター MPM テスト |

---

## 事前セットアップ（必須）

1. `dashboard.unity3d.com` でプロジェクトを作成し Lobby サービスを有効化
2. **Edit → Project Settings → Services** でプロジェクト ID を紐付け
3. ⚠️ WebGL 非対応（QoS フェーズ未サポート）。Windows / Mac ビルドを使用すること

---

## シーン構成

```
Title → Matching → Main
```

- `Matching` シーンでルーム選択・接続を完了させてから `Main` へ遷移
- `Common` シーンは常駐（既存の構成を維持）

---

## フロー

```
1. Matching シーン起動
   → 匿名認証（UnityServices.Initialize + SignInAnonymously）
   → ルーム一覧を表示

2a. クイックマッチ（推奨）
   → QuerySessions で Name="QuickMatch" かつ AvailableSlots>0 を検索
   → 見つかった → JoinSessionByIdAsync → Main シーンへ遷移
   → 見つからない → CreateSessionAsync(Name="QuickMatch", MaxPlayers=2)
     → PlayerJoined イベント待機（30秒タイムアウト）
     → タイムアウト → 「閉じる」ボタンを表示（押すと再認証・ルーム一覧へ）

2b. ルームを手動作成
   → CreateSessionAsync(MaxPlayers=2)
   → PlayerJoined イベント待機（120秒タイムアウト）
   → 待機中は「2分で自動解散します」を表示
   → タイムアウト → 「閉じる」ボタンを表示（押すと再認証・ルーム一覧へ）

2c. ルームに手動参加
   → JoinSessionByIdAsync(sessionId)
   → Main シーンへ遷移

3. Main シーン開始（オンラインゲーム）
   → NetworkGameService.PrepareDecksAsync でデッキ交換プロトコルを実行
   → 初期手札 5 枚をドロー・ゲームループ開始
```

---

## アーキテクチャ

### 主要クラス

| クラス | 責務 |
|---|---|
| `MatchingModel` | マッチング状態を `ReactiveProperty` で管理 |
| `MatchingPresenter` | UI とマッチング状態のバインド（`IStartable` 実装） |
| `MatchingService` | UGS Session API 呼び出し |
| `MatchingLifetimeScope` | Matching シーン固有 DI 登録 |
| `GameSessionModel` | `ISession` を Common シーン跨ぎで保持（Singleton）。`HasSession` でオンライン/オフライン判定 |
| `NetworkGameService` | Main シーンでのホスト/クライアント間デッキ交換プロトコル |

### DI 登録

```
CommonLifetimeScope（Common シーン常駐）
  └── GameSessionModel（Singleton）

MatchingLifetimeScope（Matching シーン）
  ├── MatchingModel
  ├── MatchingService
  └── MatchingPresenter（IStartable）

MainLifetimeScope（Main シーン）
  └── NetworkGameService（Scoped）
```

### IStartable の理由

Matching シーンを直接再生した場合、`CommonSceneLoader` が Common シーンをアディティブロードする間に
Unity の `Start()` が先に呼ばれる。VContainer の `IStartable.Start()` はスコープビルド後に呼ばれるため
注入タイミングの問題を回避できる。

---

## MatchingState

| 状態 | 意味 |
|---|---|
| `Idle` | 初期状態 |
| `Authenticating` | UGS 初期化・認証中 |
| `BrowsingRooms` | ルーム一覧表示中（ボタン有効） |
| `CreatingRoom` | ルーム作成中 |
| `JoiningRoom` | ルーム参加中 |
| `WaitingForPlayer` | 相手待ち・クイックマッチホスト（30秒タイムアウト） |
| `WaitingInCreatedRoom` | 相手待ち・手動ルーム作成ホスト（120秒タイムアウト、「2分で自動解散します」表示） |
| `Starting` | Main シーンへ遷移中 |
| `Ready` | 全員準備完了、遷移待ち |
| `TimedOut` | タイムアウト（「閉じる」ボタン表示中） |
| `Error` | エラー発生（ネットワーク切断、UGS エラー） |

---

## エラーハンドリング

ネットワークエラーや UGS API エラーが発生した場合、`Error` 状態に遷移し、エラーオーバーレイが表示される。

### エラー状態の遷移

以下のいずれかでエラーが発生すると、catch ブロックで `_model.State.Value = MatchingState.Error` が設定される:

- `InitializeAsync`: 認証・ルーム検索 失敗
- `OnQuickMatchButtonClickedAsync`: クイックマッチ 失敗
- `OnCreateButtonClickedAsync`: ルーム作成 失敗
- `OnRoomSelectedAsync`: ルーム参加 失敗
- `CancelWaitAsync`: 待機キャンセル 失敗

### WaitingOverlay タイムアウト時

`TimedOut` 状態時に待機オーバーレイ内で表示が切り替わる:
- **メッセージ**: 「タイムアウトしました」
- **閉じるボタン**: `InitializeAsync` を呼び出し（再認証 → ルーム一覧取得 → `BrowsingRooms` に戻る）

### ErrorOverlay UI

`Error` 状態時に表示:
- **メッセージ**: 「ネットワークエラー」（赤色テキスト）
- **閉じるボタン**: `InitializeAsync` を呼び出し（再認証 → ルーム一覧取得 → `BrowsingRooms` に戻る）

---

## エディター MPM テスト

`Window → Multiplayer → Multiplayer Play Mode` で Virtual Player を追加して Enter Play Mode。

メインエディターとバーチャルプレイヤーの両方で「クイックマッチ」ボタンを押すとマッチングする。
先に起動した側がルームを作成して待機し、後から起動した側がルームを見つけて参加する。

---

## ネットワークプロトコル（NetworkGameService）

Main シーン遷移後、`MainPresenter.BuildAsync` から呼ばれる。NGO の `CustomMessagingManager` を使って JSON メッセージを送受信する。`_opponentClientId` は初期ハンドシェイク完了時に保存し、以降のフェーズ送信に使用する。

### デッキ交換（PrepareDecksAsync）

```
ホスト側                                         クライアント側
  ├─ k_ClientReady ハンドラ登録
  ├─ k_DeckSubmit  ハンドラ登録                   ├─ k_InitialState ハンドラ登録
  │                                               ├─ k_RequestDeck  ハンドラ登録
  │                                               └─ NGS_ClientReady を 200ms 間隔でリトライ送信
  │                                                   （NGS_RequestDeck 受信まで繰り返す）
  ├─ NGS_ClientReady 受信 → NGS_RequestDeck 送信 →
  │
  ←──────────────────────────── NGS_DeckSubmit 受信（クライアントのデッキ ID 配列）
  │
  ├─ 両デッキをシャッフル・初期手札(最大5枚)を決定・_opponentClientId 保存
  └─ NGS_InitialState 送信（クライアントの手札/デッキ・先攻後攻）→ クライアント受信・_opponentClientId 保存
```

- リトライ送信はリレートランスポートの安定化前にメッセージが届かないケースへの対応
- 先攻判定: ホストが `Random.value > 0.5f` でランダムに決定し、結果を `IsLocalFirst` に格納。クライアントへは反転値（`!hostGoesFirst`）を送信

### キャラセットフェーズ同期（SendCharSetAction / WaitForOpponentCharSetAsync）

```
自分のターン  → SendCharSetAction(cardId)   // null = パス
相手のターン  → WaitForOpponentCharSetAsync  // null を受け取ったらパス
```

ペイロード: `{ bool passed, string cardId }`
受信側は `cardId` で `CardDatabase` を引いた裏向き `CardView` を生成し、相手の手札から1枚除いてキャラスロットへ飛翔アニメーション。

### 戦闘前1フェーズ同期（SendPreBattle1Action / WaitForOpponentPreBattle1Async）

```
自分のターン  → SendPreBattle1Action(cardId)       // null = パス
相手のターン  → WaitForOpponentPreBattle1Async      // null を受け取ったらパス
```

ペイロード: `{ bool passed, string cardId }`（キャラセットと同構造）
受信側は `cardId` で `CardDatabase` を引いた裏向き `CardView` を生成し、相手の手札から1枚除いてフィールドへ飛翔アニメーション。

### 戦闘前2フェーズ同期（SendPreBattle2Action / WaitForOpponentPreBattle2Async）

```
自分のターン  → SendPreBattle2Action(cardId)       // null = パス
相手のターン  → WaitForOpponentPreBattle2Async      // null を受け取ったらパス
```

ペイロード: `{ bool passed, string cardId }`（同構造）
受信側は `cardId` で `CardDatabase` を引いた `CardView` を生成し、フィールドへ飛翔 → `FlipAsync`（表向き）→ `ReadyCard` → コスト払い。

### ドローフェーズ同期（SendDrawNotification / WaitForOpponentDrawAsync）

```
自分のターン  → AddCardAnimatedAsync 完了後 → SendDrawNotification()  // ペイロードなし
相手のターン  → WaitForOpponentDrawAsync → PlayCpuDrawAsync（裏向き飛翔）
```

ペイロードなし（相手のドローカードは常に裏向きのためデータ不要）。

---

## ファイル配置

```
Assets/Scripts/
  Common/
    GameSession/
      GameSessionModel.cs       # ISession 保持・全シーン共有（HasSession でオンライン判定）
  Matching/
    Injector/
      MatchingLifetimeScope.cs
    View/
      Matching.uxml
    LobbyInfo.cs                # ルーム情報の値型
    MatchingModel.cs
    MatchingPresenter.cs
    MatchingService.cs
    MatchingState.cs
  Main/
    Network/
      NetworkGameService.cs     # デッキ交換プロトコル
      OnlineInitialState.cs     # 初期状態データ（LocalHand / LocalDeck / OpponentHandCount 等）
Assets/Scenes/
  Matching.unity
```

---

## 未決事項

- [x] オンライン対戦のゲームループ（全フェーズ実装済み。バトルフェーズは既存フェーズ同期により両クライアントで決定論的に動作するため追加同期不要）

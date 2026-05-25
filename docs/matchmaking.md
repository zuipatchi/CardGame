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
     → タイムアウト → リトライ確認ダイアログ

2b. ルームを手動作成
   → CreateSessionAsync(MaxPlayers=2)
   → PlayerJoined イベント待機（120秒タイムアウト）
   → 待機中は「2分で自動解散します」を表示
   → タイムアウト → リトライ確認ダイアログ

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
| `TimedOut` | タイムアウト（リトライ確認中） |
| `Error` | エラー発生 |

---

## エディター MPM テスト

`Window → Multiplayer → Multiplayer Play Mode` で Virtual Player を追加して Enter Play Mode。

メインエディターとバーチャルプレイヤーの両方で「クイックマッチ」ボタンを押すとマッチングする。
先に起動した側がルームを作成して待機し、後から起動した側がルームを見つけて参加する。

---

## デッキ交換プロトコル（NetworkGameService）

Main シーン遷移後、`MainPresenter.BuildAsync` から呼ばれる。NGO の `CustomMessagingManager` を使って JSON メッセージを送受信する。

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
  ├─ 両デッキをシャッフル・初期手札(最大5枚)を決定
  └─ NGS_InitialState 送信（クライアントの手札/デッキ・先攻後攻）→ クライアント受信
```

- リトライ送信はリレートランスポートの安定化前にメッセージが届かないケースへの対応
- 先攻判定: ホストが `Random.value > 0.5f` でランダムに決定し、結果を `IsLocalFirst` に格納。クライアントへは反転値（`!hostGoesFirst`）を送信

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

- [ ] ゲームロール（ナビゲーター / アーティスト）の割り当て
- [ ] オンライン対戦のゲームループ（現状: デッキ交換・初期ドロー・コイントスまで実装済み。ドロー同期以降は未実装）

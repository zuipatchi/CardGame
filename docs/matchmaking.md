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

## UI

| 要素 | 名前 | 有効状態 |
|---|---|---|
| ホームへボタン（カード左下） | `BackButton` | `BrowsingRooms` / `Error` / `TimedOut` のみ |
| クイックマッチボタン | `QuickMatchButton` | `BrowsingRooms` のみ |
| ルーム作成ボタン | `CreateButton` | `BrowsingRooms` のみ |

ルーム一覧は `BrowsingRooms` 状態中に 2 秒間隔で自動更新される（手動更新ボタンなし）。

---

## フロー

```
1. Matching シーン起動
   → 匿名認証（UnityServices.Initialize + SignInAnonymously）
   → ルーム一覧を表示

2a. クイックマッチ（推奨）
   → ランダムジッター（0〜800ms）で同時押しの衝突頻度を下げる
   → QuerySessions で Name="QuickMatch" かつ AvailableSlots>0 かつ started!=1 を検索
   → 見つかった → JoinSessionByIdAsync → Main シーンへ遷移
   → 見つからない → CreateSessionAsync(Name="QuickMatch", MaxPlayers=2, started="0")
     → 【競合解決】同時に作られた別の QuickMatch ルームが無いか最大6秒ポーリングで再照会
        （ReconcileQuickMatchAsync）
        ・自分より小さい ID のルームを発見 → 自分のルームを離脱しそのルームへ Join → Main へ遷移
        ・自分より大きい ID のルームだけ発見（=自分がホスト）→ 即座に待機へ移行
        ・何も見つからない → ポーリング継続
     → PlayerJoined イベント待機（30秒タイムアウト。イベント取りこぼしに備え AvailableSlots を併用ポーリング）
     → マッチ成立 → MarkRoomStartedAsync（started="1" に更新）→ Main シーンへ遷移
     → タイムアウト → セッション離脱（ルーム破棄）→「閉じる」ボタンを表示（押すと再認証・ルーム一覧へ）

   ※ 同時押しで両者がルームを作ってしまった場合、ID の大小という決定論的な基準で
     必ず片方だけが入り直すため、確実に 1 組のマッチへ収束する。

2b. ルームを手動作成
   → CreateSessionAsync(MaxPlayers=2, started="0")
   → PlayerJoined イベント待機（120秒タイムアウト）
   → 待機中は「2分で自動解散します」を表示
   → マッチ成立 → MarkRoomStartedAsync（started="1" に更新）→ Main シーンへ遷移
   → タイムアウト → セッション離脱（ルーム破棄）→「閉じる」ボタンを表示（押すと再認証・ルーム一覧へ）

2c. ルームに手動参加
   → JoinSessionByIdAsync(sessionId)
   → Main シーンへ遷移

3. Main シーン開始（オンラインゲーム）
   → NetworkGameService.PrepareDecksAsync でデッキ交換プロトコルを実行
   → コイントスで先攻後攻決定 → 初期手札（先攻3枚・後攻5枚）をドロー・ゲームループ開始
```

---

## アーキテクチャ

### 主要クラス

| クラス | 責務 |
|---|---|
| `MatchingModel` | マッチング状態を `ReactiveProperty` で管理 |
| `MatchingPresenter` | UI とマッチング状態のバインド（`IStartable` 実装） |
| `MatchingService` | UGS Session API 呼び出し（検索・作成・参加・started フラグ管理）|
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

待機タイムアウト時はまずホストがセッションを離脱（`LeaveCurrentSessionAsync`）してルームを破棄し、その後 `TimedOut` 状態へ遷移する。これによりサーバー上にルームが残らず、他プレイヤーの一覧にも表示されなくなる。

`TimedOut` 状態時に待機オーバーレイ内で表示が切り替わる:
- **メッセージ**: 「タイムアウトしました」
- **閉じるボタン**: `InitializeAsync` を呼び出し（再認証 → ルーム一覧取得 → `BrowsingRooms` に戻る）。クリック時に Cancel1 SE

### ErrorOverlay UI

`Error` 状態時に表示:
- **メッセージ**: 「ネットワークエラー」（赤色テキスト）
- **閉じるボタン**: `InitializeAsync` を呼び出し（再認証 → ルーム一覧取得 → `BrowsingRooms` に戻る）。クリック時に Cancel1 SE

---

## エディター MPM テスト

`Window → Multiplayer → Multiplayer Play Mode` で Virtual Player を追加して Enter Play Mode。

メインエディターとバーチャルプレイヤーの両方で「クイックマッチ」ボタンを押すとマッチングする。
先に起動した側がルームを作成して待機し、後から起動した側がルームを見つけて参加する。

---

## ネットワークプロトコル（NetworkGameService）

マッチ成立後、Main シーンでの NGO 通信（デッキ交換ハンドシェイク・メインアクション/ドローなど各フェーズの同期・メッセージ種別一覧・メッセージロスト対策）は [docs/networking.md](networking.md) に集約している。マッチング側で関係するのは「`MatchingService` がセッションを確立 → `MainPresenter.BuildAsync` が `NetworkGameService.PrepareDecksAsync` を呼んでデッキ交換を開始する」という接続点のみ。

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

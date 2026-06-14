# NGO + MPM ネットワーク実装ノウハウ

Unity 6 + NGO (Netcode for GameObjects) + UGS Multiplayer Services + MPM (Multiplayer Play Mode) の組み合わせで発生したハマりポイントと解決策。

---

## ハマりポイントと対処法

### 1. NGO の NetworkSceneManager が Common シーンを破壊する

**症状**: クライアント側で Common シーンが消え、`SceneTransitioner` が `MissingReferenceException` で死ぬ。

**原因**: NGO はデフォルト (`EnableSceneManagement=true`) でホストのシーン操作をクライアントに同期する。ホストが Main シーンを Additive でロードすると、クライアント側には **Single モードでロード** される扱いになり、Common を含む既存シーンが全て破壊される。

**対処**: セッション作成・参加の前に `EnableSceneManagement` を無効化する。

```csharp
private static void DisableNgoSceneManagement()
{
    NetworkManager nm = NetworkManager.Singleton;
    if (nm != null)
    {
        nm.NetworkConfig.EnableSceneManagement = false;
    }
}

// CreateSessionAsync / JoinSessionByIdAsync の直前に呼ぶ
await _gameSessionModel.LeaveCurrentSessionAsync();
DisableNgoSceneManagement();
IHostSession session = await MultiplayerService.Instance.CreateSessionAsync(options)...;
```

---

### 2. MPM で VContainer の親スコープが見つからない (`VContainerParentTypeReferenceNotFound`)

**症状**: クライアント側のシーン遷移後に `VContainerParentTypeReferenceNotFound` 例外。

**原因**: VContainer の `LifetimeScope.FindAnyObjectByType` はデフォルトで **inactive なオブジェクトを除外** する。MPM では各プレイヤーが独立したシーンを持つため、別プレイヤーのシーンにある親スコープを `FindAnyObjectByType` で誤って拾う・または見つけられないケースが発生する。

**対処**: `SceneExtensions.ResolveParentReference` で全シーンを直接走査し、`Container != null`（ビルド済み）のスコープだけを親候補にする。

```csharp
private static void ResolveParentReference(LifetimeScope scope)
{
    if (scope.parentReference.Object != null) return;
    if (scope.parentReference.Type == null) return;

    Type parentType = scope.parentReference.Type;
    for (int i = 0; i < SceneManager.sceneCount; i++)
    {
        Scene s = SceneManager.GetSceneAt(i);
        foreach (GameObject root in s.GetRootGameObjects())
        {
            LifetimeScope candidate = root.GetComponentInChildren(parentType, true) as LifetimeScope;
            if (candidate != null && candidate.Container != null)
            {
                scope.parentReference.Object = candidate;
                return;
            }
        }
    }
}
```

`BuildLifetimeScopes()` 拡張メソッド内で `scope.Build()` 前に必ずこれを呼ぶ。また、`Container != null` のスコープは再 Build をスキップする（二重 Build 防止）。

---

### 3. MPM でのシーン遷移（既にロード済みのシーンへの対応）

**症状**: MPM では SceneManager がプレイヤー間で共有される。一方のプレイヤーが Main シーンをロード済みの状態でもう一方がロードしようとすると、ロードがスキップされるがスコープはビルドされていない。

**対処**: `SceneTransitioner.Transit` でシーンのロードとスコープのビルドを分離する。

```csharp
Scene nextScene = SceneManager.GetSceneByBuildIndex((int)next);
if (!nextScene.IsValid() || !nextScene.isLoaded)
{
    // まだロードされていない場合のみロード
    await SceneManager.LoadSceneAsync((int)next, LoadSceneMode.Additive).WithCancellation(_ct);
    nextScene = SceneManager.GetSceneByBuildIndex((int)next);
}

// ロード済みかどうかに関わらず、常にスコープをビルド（Container != null はスキップされる）
nextScene.BuildLifetimeScopes();
```

旧シーンのアンロードも、アクティブシーン1つを削除する単純な方法ではなく、Common とターゲットシーン以外を全てリストアップして順にアンロードする。

---

### 4. `CustomMessagingManager` が `JoinSessionByIdAsync` 直後に null になる

**症状**: `messaging.RegisterNamedMessageHandler(...)` で NullReferenceException。

**原因**: `JoinSessionByIdAsync` が返った時点では NGO の初期化が完了していない場合がある。`CustomMessagingManager` が null のまま `PrepareDecksAsync` が進んでしまう。

**対処**: `PrepareDecksAsync` の先頭で NGO の準備完了を待つ。

```csharp
NetworkManager nm = NetworkManager.Singleton;
bool isHost = _gameSessionModel.IsHost;

while (nm.CustomMessagingManager == null
       || (isHost ? !nm.IsListening : !nm.IsConnectedClient))
{
    await UniTask.NextFrame(cancellationToken: ct);
}
```

ホストは `IsListening`、クライアントは `IsConnectedClient` で確認する（条件が異なることに注意）。

---

### 5. `IsConnectedClient=true` でもメッセージが届かない

**症状**: ホストが `NGS_ClientReady` ハンドラを登録して待機中、クライアントが送信しても受信できない。ログのタイムスタンプが同秒なのに届かない。

**原因**: `IsConnectedClient=true` になった瞬間は NGO の Relay トランスポートが完全に双方向通信可能な状態ではないケースがある。最初のメッセージが輸送レイヤーで失われる。

**対処**: 受信確認が取れるまで一定間隔でリトライ送信する。

```csharp
// ハンドラ登録後、NGS_RequestDeck を受信するまで NGS_ClientReady をリトライ送信
bool requestReceived = false;

void OnRequestDeck(ulong senderId, FastBufferReader reader)
{
    messaging.UnregisterNamedMessageHandler(k_RequestDeck);
    requestReceived = true;
    requestTcs.TrySetResult();
}

messaging.RegisterNamedMessageHandler(k_RequestDeck, OnRequestDeck);

while (!requestReceived)
{
    using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
    {
        messaging.SendNamedMessage(k_ClientReady, NetworkManager.ServerClientId, writer);
    }
    await UniTask.Delay(200, cancellationToken: ct);
}
```

ホスト側のハンドラは `UnregisterNamedMessageHandler` で1回目受信後に解除されるため、複数回届いても問題ない。

---

### 6. `PlayerJoined` イベントの競合

**症状**: クライアントがルーム参加を完了した後にホストが `WaitForPlayerAsync` を呼ぶと、イベントが既に発火済みで永久に待ち続ける。

**原因**: `CreateRoomAsync` が返った直後にクライアントが参加した場合、`session.PlayerJoined` への登録前にイベントが発火して失われる。

**対処**: ハンドラを先に登録してから `AvailableSlots` で既に埋まっていないかを確認する。

```csharp
session.PlayerJoined += OnPlayerJoined;  // 先に登録

if (session.AvailableSlots == 0)          // 後から確認
{
    session.PlayerJoined -= OnPlayerJoined;
    return true;  // 既に参加済み
}

// 以降でタイムアウト付き待機
```

「登録 → 状態確認」の順を守ることで競合ウィンドウをゼロにできる。

---

### 7. フェーズ開始アナウンス中にメッセージが届いてハンドラ未登録で捨てられる

**症状**: オンライン対戦でドローフェーズに「進めない」プレイヤーが発生し、ゲームが永久に止まる。

**原因**: 各フェーズが「アナウンスアニメーション → ハンドラ登録 → 送受信」という順序だった。一方のプレイヤーがアナウンス再生中に、もう一方がすでにメッセージを送信すると、ハンドラが未登録のためメッセージが捨てられて永久待ちになる。

**対処**: ハンドラ登録を **アニメーション開始前** に移動する。解決フェーズの Recover 効果（`WaitForOpponentRecoverDeckOrderAsync` をアニメーション開始前に呼ぶ）で適用済み。ドロー・メインフェーズの受信は前の同期点での事前登録パターンを使う（セクション9・10参照）。

---

### 8. `QuerySessionsAsync` がセッション離脱直後に `SessionException` を投げる

**症状**: ルームを作ってキャンセル（`Session.LeaveAsync()`）した直後にクイックマッチを押すと
`SessionException: [Error: Unknown] [Message: Object reference not set to an instance of an object]` が発生し、エラー画面になる。

**原因**: `Session.LeaveAsync()` 完了後、UGS Multiplayer SDK 内部でリレー接続の後片付けが非同期で走る。
その過渡期（数秒以内）に `QuerySessionsAsync` を呼ぶと、SDK 内部の null 状態を踏んで例外が発生する。

**対処**: `GetRoomsAsync` 内で `SessionException` を捕捉して空リストを返す。
クイックマッチ側は「部屋なし → 新規作成」フローに入り正常動作する。
自動リフレッシュ（2秒ごと）が次のサイクルで正常なルーム一覧を取得し直す。

```csharp
try
{
    results = await MultiplayerService.Instance
        .QuerySessionsAsync(queryOptions)
        .AsUniTask()
        .AttachExternalCancellation(ct);
}
catch (SessionException)
{
    // UGS SDK がセッション離脱直後の過渡期に NullRef を投げるバグの回避。
    return Array.Empty<LobbyInfo>();
}
```

**合わせて**: `_isQuerying` フラグで `QuerySessionsAsync` の並行呼び出しも防止している
（auto-refresh キャンセル直後にクイックマッチが同じ API を呼ぶレース状態への対策）。

---

### 9. ターン間のローカル演出中に NGS_Draw がロストしてドローフェーズが停止する

**症状**: ターン終了後のドローフェーズで片方のプレイヤーがハングする。特に長い攻撃アニメーション（ダメージエフェクト・キャラ破壊）の後に発生しやすい。

**原因**: メインフェーズの行動送受信（最後のネットワーク同期点）から次ターンのドローフェーズ開始までローカル演出が挟まる。  
先にアニメーションが終わったプレイヤーがドローフェーズに入って `NGS_Draw` を送信したとき、  
相手はまだアニメーション中でハンドラ未登録 → `ReliableSequenced` で届いてもハンドラなしで捨てられる → 相手は永久にハング。

**対処**: 最後のネットワーク同期点（メインフェーズの行動送信直前、初回はマリガン同期直後）に `NGS_Draw` ハンドラを事前登録する。以降の全ローカル演出中も受信可能な状態を維持する。

```csharp
// RunMainPhaseAsync: SendMainAction の直前に事前登録
if (_isOnline)
{
    _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
    _hasPreDrawTask = true;
    _networkGameService.SendMainAction(ToNetworkAction(action, costCardIds));
}
```

```csharp
// BuildAsync: マリガン同期後（InitializeFirstTurn の前）に事前登録
NetworkGameService.MulliganResult opponentResult = await waitOpponentMulligan;
_preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
_hasPreDrawTask = true;
```

```csharp
// RunDrawPhaseAsync: 事前登録タスクがあれば使い、なければフォールバックで登録
if (_isOnline && _hasPreDrawTask)
{
    drawReceiveTask = _preDrawReceiveTask;
    _hasPreDrawTask = false;
}
else if (_isOnline)
{
    drawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
}
```

加えて `await receiveTask` を `await receiveTask.AttachExternalCancellation(ct)` に変更し、
事前登録タスクが `destroyCt` で作られていてもサレンダー (`_surrenderCts.Token`) でキャンセルされるようにしている。

---

### 10. 相手ドロー直後の NGS_MainAction がロストしてメインフェーズが停止する

**症状**: 相手ターンのメインフェーズで自分側がフリーズする。

**原因**: 相手ターンのドローフェーズで `NGS_Draw` を受信した後、自分側は相手のドローアニメーションを再生する。その間に相手がすでにメインフェーズの行動を送信すると、ハンドラ未登録でメッセージが捨てられる。

**対処**: 相手ターンのドローフェーズで、ドロー通知の `await` 前に `NGS_MainAction` のハンドラを事前登録する。`_preDrawReceiveTask` と同じパターンで `_preMainActionReceiveTask` / `_hasPreMainActionTask` フィールドを使う。

```csharp
// RunDrawPhaseAsync（相手ターン）: ドロー通知の await 前に事前登録
if (_isOnline && !_hasPreMainActionTask)
{
    _preMainActionReceiveTask = _networkGameService.WaitForOpponentMainActionAsync(ct);
    _hasPreMainActionTask = true;
}

await drawReceiveTask.AttachExternalCancellation(ct);
```

```csharp
// RunMainPhaseAsync（相手ターン）: 事前登録タスクがあれば使い、なければフォールバック
UniTask<NetworkGameService.MainActionData> receiveTask;
if (_hasPreMainActionTask)
{
    receiveTask = _preMainActionReceiveTask;
    _hasPreMainActionTask = false;
}
else
{
    receiveTask = _networkGameService.WaitForOpponentMainActionAsync(ct);
}
```

---

### 11. 対象選択（DamageEnemy / Evolve）のメッセージがアニメーション後の遅延登録でロストする ★致命的

**症状**: オンライン対戦でキャラ/イベントが **DamageEnemy 効果**（複数体のうち一部を対象に選ぶ場合）を使うとゲームが永久に止まる。Evolve 効果でも同条件で発生し得る。

**原因**: セクション 7・9・10 と同じ「受信側がアニメーション後にハンドラを遅延登録する」クラスのバグ。送受信が**リクエスト/レスポンス型**（手番側が対象を選んで送信 → 相手が受信）で、受信側のハンドラ登録が送信側の送信より遅れると、NGO が未登録の名前付きメッセージを破棄して受信側が永久待機する。

DamageEnemy が特に踏みやすい理由（他の効果より送受信の時間差が出やすい）:

- 手番側はカードを**ドラッグで即配置**するため飛行アニメーションがない。一方、相手側は `ExecuteOpponentCardPlayAsync` で**カード飛行 + コスト支払いアニメーション**を再生してから対象同期ハンドラを登録する（=相手側の登録が遅れる）。
- DamageEnemy の対象選択は**敵キャラを1クリックするだけ**で完了する＝人間操作の中で最速。0コストのカードならコスト選択の待ちもない。このため、手番側が素早くクリックすると相手側の登録前に送信が届いてしまう。

Switch/Evolve は対象選択に「手札から置き換えカードを選ぶ」操作が挟まり、かつイベントカード飛行ぶんの猶予があるため発生しにくいが、**同じ構造の潜在バグ**である。

**対処**: 2 種類の対策パターンがある。呼び出し箇所の数で使い分ける。

1. **永続ハンドラ + キュー（DamageTarget で採用）**: `k_DamageTarget` は OnEnter / OnAttack / OnDestroy / イベント / Bounce と**多数の箇所**から呼ばれ、各所で事前登録するのは非現実的。そこで対戦開始時（`PrepareDecksAsync`）にハンドラを永続登録し、受信値をキューにバッファする。待機開始前に届いたメッセージもキューに積まれるため、タイミングに依存せず取りこぼさない。

```csharp
// NetworkGameService: 対戦開始時に1度だけ永続登録
RegisterDamageTargetHandler(messaging);

// ハンドラ：待機中なら即解決、そうでなければキューに積む
void OnDamageTarget(ulong senderId, FastBufferReader reader)
{
    // ... indices を読む
    UniTaskCompletionSource<int[]> waiter = _damageTargetWaiter;
    _damageTargetWaiter = null;
    if (waiter != null && waiter.TrySetResult(indices)) { return; }
    _damageTargetQueue.Enqueue(indices);
}

// 待機側：キューにあれば即返す、なければ待つ
public async UniTask<int[]> WaitForOpponentDamageTargetsAsync(CancellationToken ct)
{
    if (_damageTargetQueue.Count > 0) { return _damageTargetQueue.Dequeue(); }
    _damageTargetWaiter = new UniTaskCompletionSource<int[]>();
    try { return await _damageTargetWaiter.Task.AttachExternalCancellation(ct); }
    finally { _damageTargetWaiter = null; }
}
```

2. **アニメーション前の事前登録（Switch / Evolve / Recover で採用）**: 呼び出し箇所が1つなら、受信タスクを**アニメーション開始前**に生成してハンドラを先行登録し、後で `await` する（セクション 9・10 と同じイディオム）。

```csharp
// ApplyEvolveEffectAsync: 生贄アニメーションの前にハンドラを事前登録
UniTask<string> evolveReceiveTask = (!isLocal && _isOnline)
    ? _networkGameService.WaitForOpponentEvolveAsync(ct)
    : default;
// ... 生贄アニメーション ...
string cardId = await evolveReceiveTask;  // 後で受信を待つ
```

**教訓**: 「受信側がアニメーションの後にハンドラを登録する」コードは、相手の送信タイミング次第で必ずメッセージロストの危険がある。新しいリクエスト/レスポンス型メッセージを追加するときは、**ハンドラ登録を受信側の最初のアニメーションより前**に置くか、**永続登録 + キュー**にすること。

---

## メッセージ種別一覧

| 定数 | 方向 | 内容 |
|---|---|---|
| `NGS_ClientReady` | Client → Host | ハンドシェイク開始 |
| `NGS_RequestDeck` | Host → Client | デッキ送信要求 |
| `NGS_DeckSubmit` | Client → Host | デッキ ID 配列送信 + クライアントのユーザーネーム |
| `NGS_InitialState` | Host → Client | 初期手札・デッキ・先攻後攻 + ホストのユーザーネーム |
| `NGS_Mulligan` | Both | マリガン実施有無 + マリガン後のデッキ順序（mulliganed / newDeckIds。マリガンしない場合 newDeckIds は null） |
| `NGS_Draw` | Both | ドロー完了通知（ペイロードなし） |
| `NGS_MainAction` | Both | メインフェーズ行動（actionType / cardId / attackerId / targetId / targetsHeart / costCardIds[]）。targetsHeart=true はハート攻撃（ハート勝利条件）。コスト支払いアニメーション完了直後（イベント効果解決アニメーション前）に送信することで相手側のアニメーション開始を早める |
| `NGS_RecoverDeck` | Both | Recover 効果後のシャッフル済みデッキ順序（string[] cardIds） |
| `NGS_DamageTarget` | Both | DamageEnemy / Bounce 効果の対象（敵フィールド上のインデックス配列 int[]）。対象数 < 敵数のとき手番側が選んで送信。**対戦開始時に永続登録 + キューでバッファ**（セクション 11） |
| `NGS_Switch` | Both | 解決フェーズ Switch 効果の新キャラ選択（passed / cardId）。アニメーション前に事前登録 |
| `NGS_Evolve` | Both | 解決フェーズ Evolve 効果の新キャラ選択（passed / cardId）。アニメーション前に事前登録 |
| `NGS_Surrender` | Both | 降参通知（ペイロードなし） |
| `NGS_Rematch` | Both | 再戦希望通知（ペイロードなし）。双方が送信し合うと両者が Main シーンを再ロードして新規対戦を開始 |

`NGS_Surrender` はペイロードなし（4 バイトの空 `FastBufferWriter`）。送信者は YOU LOSE + 「降参しました」を表示し、受信者は YOU WIN + 「対戦相手が降参しました」を表示する。受信監視は `PrepareDecksAsync` 完了直後（ゲーム画面表示前）に開始するため、コイントスや初期配布中の降参も検知できる。

### 再戦（NGS_Rematch）

ゲーム終了オーバーレイの「再戦する」ボタンで使う。`OnGameEnd`（オンライン時）で `WatchForOpponentRematchAsync` と `NetworkManager.OnClientDisconnectCallback` の監視を開始する。「再戦する」を押すと `NGS_Rematch` を送信して「対戦相手を待っています...」を表示し、**自分の希望と相手の希望の2フラグが揃ったら** 両者が `SceneTransitioner.Reload(Scenes.Main)` で Main シーンを再ロードして新規対戦を開始する。`Reload` は対象シーンをアンロードしてから再ロードするため `BuildAsync` → `PrepareDecksAsync` の再ハンドシェイクで再配牌される（NGO セッションは Common 常駐の `NetworkManager` が保持するため切断されない）。デッキは `DeckModel`（Common）から再取得するので同一、先攻後攻はホストが再抽選する。CPU 戦は相手待ちなしで即再ロードする。再戦フェーズ中に相手が退出（`OnClientDisconnectCallback` 発火）したら「対戦相手が退出しました」を表示し再戦ボタンを消す。

---

## デッキ交換プロトコルの設計メモ

NGS_ClientReady ハンドシェイクを入れた理由は「ホストがリクエストを送るタイミングをクライアントのハンドラ登録完了に同期させるため」。

```
ホスト                              クライアント
  ├─ k_ClientReady 登録             ├─ k_RequestDeck 登録
  ├─ k_DeckSubmit  登録             ├─ k_InitialState 登録
  └─ 待機                           └─ NGS_ClientReady をリトライ送信
                                          ↓（200ms ごと）
  ← NGS_ClientReady 受信
  ├─ NGS_RequestDeck 送信 ─────────→ 受信・送信ループ停止
  ←──────────── NGS_DeckSubmit 受信
  ├─ シャッフル・手札決定
  ├─ マリガン要否判定（両者分）
  └─ NGS_InitialState 送信 ────────→ 受信・ゲーム開始
```

`NGS_InitialState` のペイロード: `localHandIds`, `localDeckIds`, `opponentHandCount`, `opponentDeckIds`, `isLocalFirst`, `opponentUsername`。手札は相手に見せてはいけないため `opponentHandCount`（枚数のみ）。`opponentUsername` はホストの表示名をクライアントに伝えるために付加する（`NGS_DeckSubmit` でクライアントのユーザーネームがホストに届くのと対称）。デッキは `DeckView.DrawTop()` の順序が両クライアントで一致する必要があるため `opponentDeckIds: string[]`（シャッフル済み ID 配列）を送信し、受信側が `CardDatabase.BuildDeck` で `CardData[]` に復元する。

**マリガン・デッキ順序の同期**: 各プレイヤーは自分の手札を見て独立にマリガンの是非を決める。実施有無とリシャッフル後のデッキ順序は `NGS_Mulligan`（`MulliganPayload`: mulliganed / newDeckIds）で双方が交換する。マリガンを選択したプレイヤーは newDeckIds にシャッフル済み ID 配列を含めて送信し、受信側は `CardDatabase.BuildDeck` で復元して相手デッキのデータを更新する。これにより両クライアントで `DrawTop()` の返すカードが一致する。

**Recover 効果後のデッキ順序の同期**: `AddCardsAndShuffle` は `Random.Range` を使うため両クライアントで独立にシャッフルされ、デッキ順序が乖離する。効果の isLocal 側がシャッフル後の順序を `NGS_RecoverDeck`（string[]）で送信し、`!isLocal` 側は受信した順序で `DeckView.Rebuild()` する。`WaitForOpponentRecoverDeckOrderAsync` はアニメーション（`PlayRecoverFlyAsync`）**開始前** に呼んでハンドラを先行登録する（アニメーション後に登録すると相手の送信が先に届いてメッセージ消失する）。

**デッキ順序の送受信ヘルパー**: `SendDeckOrder(messageName, deckIds)` / `WaitForDeckOrderAsync(messageName, ct)` をプライベートヘルパーとして実装し、Recover 効果のデッキ同期で使用している。

メッセージは `JsonUtility` + `FastBufferWriter.WriteValueSafe(string)` で送受信する。JSON サイズを過小見積もりするとバッファ不足になるため、`json.Length * 2 + 8` でバッファを確保する（Unicode 文字の最大バイト数を考慮）。

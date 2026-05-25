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

`NGS_InitialState` には手札・デッキ情報に加えて `localNeedsMulligan` / `opponentNeedsMulligan` フラグが含まれる。ホストは双方の初期手札を見てマリガン要否を判定し、1 つのメッセージにまとめて送信する。

**マリガン判定を NGS_InitialState に束ねた理由**: 別途 `NGS_Mulligan` メッセージを交換する設計（「お互いにマリガン有無を送り合う」）は、片方がハンドラ登録前にメッセージが届くとハングする競合が発生しやすい。ホストがデッキを配る時点で両者の手札を持っているため、追加の往復通信なしに判定できる。

メッセージは `JsonUtility` + `FastBufferWriter.WriteValueSafe(string)` で送受信する。JSON サイズを過小見積もりするとバッファ不足になるため、`json.Length * 2 + 8` でバッファを確保する（Unicode 文字の最大バイト数を考慮）。

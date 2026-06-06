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

**症状**: オンライン対戦でドロー・キャラセット・準備フェーズに「進めない」プレイヤーが発生し、ゲームが永久に止まる。

**原因**: 各フェーズが「アナウンスアニメーション → ハンドラ登録 → 送受信」という順序だった。一方のプレイヤーがアナウンス再生中に、もう一方がすでにメッセージを送信すると、ハンドラが未登録のためメッセージが捨てられて永久待ちになる。

**対処**: ハンドラ登録を **フェーズアナウンスの前** に移動する。Draw / CharSet / PreBattle1 の 3 フェーズすべてで適用済み。

PreBattle2 はターン制ループのため別パターン（セクション10参照）。

```csharp
// RunDrawPhaseAsync の冒頭（アナウンス前）
UniTask drawReceiveTask = _isOnline && _hasPreDrawTask
    ? _preDrawReceiveTask   // 事前登録済みタスクを使用（セクション9参照）
    : _isOnline ? _networkGameService.WaitForOpponentDrawAsync(ct) : UniTask.CompletedTask;

await PlayAnnouncementAsync("ドローフェーズ", ..., ct);
```

```csharp
// RunCharacterSetPhaseAsync の冒頭（アナウンス前）
UniTask charSetReceiveTask = (_isOnline && !opponentHadChar)
    ? ReceiveAndPlaceOpponentCharSetAsync(ct)
    : UniTask.CompletedTask;

await PlayAnnouncementAsync("キャラセットフェーズ", ..., ct);
```

```csharp
// RunPreBattle1PhaseAsync の冒頭（アナウンス前）
UniTask preBattle1ReceiveTask = _isOnline
    ? ReceiveAndPlaceOpponentPreBattle1Async(ct)
    : UniTask.CompletedTask;

await PlayAnnouncementAsync("準備フェーズ", ..., ct);
```

`opponentHadChar=true` のとき受信しない理由: 相手はキャラを保持済みのため自フェーズをスキップする場合があり、`k_CharSet` を送信しないことがある。受信待ちにすると永久ブロックになる。

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

### 9. 戦闘フェーズ後の NGS_Draw ロストでドローフェーズが停止する

**症状**: ターン終了後のドローフェーズで片方のプレイヤーがハングし、「ドローフェーズからキャラセットフェーズに進まない」。特に長い戦闘アニメーション（ダメージエフェクト・キャラ破壊）の後に発生しやすい。

**原因**: 戦闘フェーズ (`RunBattlePhaseAsync`) はネットワークメッセージ交換なしのローカルアニメーションのみ。  
最後のネットワーク同期点（PreBattle2 ループ終了）から次ターンのドローフェーズ開始まで 5〜10 秒のローカル演出が挟まる。  
先にアニメーションが終わったプレイヤーがドローフェーズに入って `NGS_Draw` を送信したとき、  
相手はまだ戦闘アニメーション中でハンドラ未登録 → `ReliableSequenced` で届いてもハンドラなしで捨てられる → 相手は永久にハング。

**対処**: 最後のネットワーク同期点（PreBattle2 ループ終了、初回はマリガン同期）の直後に `NGS_Draw` ハンドラを事前登録する。以降の全ローカル演出中も受信可能な状態を維持する。

```csharp
// RunPreBattle2PhaseAsync: ループ終了後（解決フェーズの前）に事前登録
HideActionButtons();
if (_isOnline)
{
    _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
    _hasPreDrawTask = true;
}
await RunResolutionPhaseAsync(ct);
```

```csharp
// BuildAsync: マリガン同期後（InitializePriority の前）に事前登録
bool opponentChose = await waitOpponentMulligan;
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

### 10. PreBattle2 ループ中のアニメーション間で NGS_PreBattle2 がロストする

**症状**: イベントフェーズで片方がフリーズしてループが止まる。

**原因**: PreBattle2 はターン制ループのため、セクション7の「アナウンス前に事前登録」パターンだけでは不十分。  
自分がアクションを送信した後、パス/プレイアニメーションが終わるまでの間に相手が応答を送ると、ハンドラ未登録でメッセージが捨てられる。

```
自分: SendPreBattle2Action 送信
相手: 受信して即座に応答を NGS_PreBattle2 で返す
自分: PlayPassAnimationAsync などを await 中（ハンドラ未登録）
      → 届いたメッセージが捨てられる
自分: アニメーション終了後に WaitForOpponentPreBattle2Async 登録
      → 永久に待ち続ける
```

**対処**: `SendPreBattle2Action` の直後（アニメーション前）にハンドラを事前登録する。`_preDrawReceiveTask` と同じパターンで `_prePreBattle2ReceiveTask` / `_hasPrePreBattle2Task` フィールドを使う。

```csharp
// 自分のターン：送信直後に事前登録
_networkGameService.SendPreBattle2Action(readied?.Data.Id);
_prePreBattle2ReceiveTask = _networkGameService.WaitForOpponentPreBattle2Async(ct);
_hasPrePreBattle2Task = true;

// アニメーション（この間に相手が応答を送っても受信できる）
await PlayPassAnimationAsync(true, ct);
```

```csharp
// 相手のターン：事前登録タスクがあれば使い、なければフォールバック
string cardId = _hasPrePreBattle2Task
    ? await _prePreBattle2ReceiveTask.AttachExternalCancellation(ct)
    : await _networkGameService.WaitForOpponentPreBattle2Async(ct);
_hasPrePreBattle2Task = false;
```

ループが `break` するケース（両者パス）で事前登録タスクが使われないまま残っても、`Dispose()` でハンドラ解除されるため問題ない。

---

### 11. PreBattle2 フェーズ開始時に後攻側がデッドロックする

**症状**: イベントフェーズで後攻側がパスを押してもゲームが止まる。エラーなし。

**原因**: 解決・戦闘フェーズはローカルアニメーションのみで、両クライアントの処理時間が異なる。先に終わった先攻側が `RunPreBattle2PhaseAsync` へ入ってアナウンスアニメーション中にすでにメッセージを送信したとき、後攻側はまだ前フェーズのアニメーション中でハンドラ未登録 → メッセージ破棄 → 双方が互いの応答を待ち続けてデッドロック。

セクション9・10のパターンが適用されていたドローフェーズ・PreBattle2 ループ内は対処済みだったが、PreBattle2 の **ループ開始前**（アナウンス前）が未対処だった。

**対処**: `RunPreBattle2PhaseAsync` の先頭で、後攻側（`else` ブランチから始まる側）は NGS_Draw 事前登録と同パターンでハンドラを事前登録する。

```csharp
// RunPreBattle2PhaseAsync の先頭（アナウンス前）
if (_isOnline && !_gameModel.IsLocalPreparationTurn)
{
    _prePreBattle2ReceiveTask = _networkGameService.WaitForOpponentPreBattle2Async(ct);
    _hasPrePreBattle2Task = true;
}

await PlayAnnouncementAsync("イベントフェーズ", ..., ct);
```

先攻側はメッセージを送る側なのでここでの事前登録は不要。後攻側だけ対象。

---

## メッセージ種別一覧

| 定数 | 方向 | 内容 |
|---|---|---|
| `NGS_ClientReady` | Client → Host | ハンドシェイク開始 |
| `NGS_RequestDeck` | Host → Client | デッキ送信要求 |
| `NGS_DeckSubmit` | Client → Host | デッキ ID 配列送信 |
| `NGS_InitialState` | Host → Client | 初期手札・デッキ・先攻後攻・マリガン要否 |
| `NGS_CharSet` | Both | キャラセットフェーズ行動（passed / cardId） |
| `NGS_PreBattle1` | Both | 戦闘前1フェーズ行動（passed / cardId） |
| `NGS_PreBattle2` | Both | 戦闘前2フェーズ行動（passed / cardId） |
| `NGS_Draw` | Both | ドロー完了通知（ペイロードなし） |
| `NGS_Switch` | Both | 解決フェーズ Switch 効果の新キャラ選択（passed / cardId） |
| `NGS_Evolve` | Both | 解決フェーズ Evolve 効果の新キャラ選択（passed / cardId） |
| `NGS_Surrender` | Both | 降参通知（ペイロードなし） |

`NGS_Surrender` はペイロードなし（4 バイトの空 `FastBufferWriter`）。送信者は YOU LOSE + 「降参しました」を表示し、受信者は YOU WIN + 「対戦相手が降参しました」を表示する。受信監視は `PrepareDecksAsync` 完了直後（ゲーム画面表示前）に開始するため、コイントスや初期配布中の降参も検知できる。

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

**ペイロード内の相手デッキ表現について**: 当初は `opponentDeckCount: int`（枚数のみ）だったが、相手デッキのカードがプレースホルダーになり `TriggerOnGrave` などの墓地トリガーが攻撃側クライアントで発火しないバグが発生した。現在は `opponentDeckIds: string[]`（シャッフル済み ID 配列）を送信し、受信側が `CardDatabase.BuildDeck` で `CardData[]` に復元する。手札は相手に見せてはいけないため引き続きプレースホルダー（枚数のみ）。

**マリガン判定を NGS_InitialState に束ねた理由**: 別途 `NGS_Mulligan` メッセージを交換する設計（「お互いにマリガン有無を送り合う」）は、片方がハンドラ登録前にメッセージが届くとハングする競合が発生しやすい。ホストがデッキを配る時点で両者の手札を持っているため、追加の往復通信なしに判定できる。

メッセージは `JsonUtility` + `FastBufferWriter.WriteValueSafe(string)` で送受信する。JSON サイズを過小見積もりするとバッファ不足になるため、`json.Length * 2 + 8` でバッファを確保する（Unicode 文字の最大バイト数を考慮）。

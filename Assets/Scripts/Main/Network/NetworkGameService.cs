using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.GameSession;
using Common.Username;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Main.Network
{
    public sealed class NetworkGameService : IDisposable
    {
        private const string k_RequestDeck = "NGS_RequestDeck";
        private const string k_DeckSubmit = "NGS_DeckSubmit";
        private const string k_InitialState = "NGS_InitialState";
        private const string k_ClientReady = "NGS_ClientReady";
        private const string k_Draw = "NGS_Draw";
        private const string k_Surrender = "NGS_Surrender";
        private const string k_Rematch = "NGS_Rematch";
        private const string k_Mulligan = "NGS_Mulligan";
        private const string k_RecoverDeck = "NGS_RecoverDeck";
        private const string k_Switch = "NGS_Switch";
        private const string k_Evolve = "NGS_Evolve";
        private const string k_DamageTarget = "NGS_DamageTarget";
        private const string k_MainAction = "NGS_MainAction";

        private readonly GameSessionModel _gameSessionModel;
        private readonly CardDatabase _cardDatabase;
        private readonly string _localUsername;
        private ulong _opponentClientId;

        // DamageEnemy / Bounce の対象同期は、受信側がハンドラを登録する前に送信側が送ると
        // NGO に未登録メッセージとして破棄され、受信側が永久待機してゲームが停止する。
        // これを防ぐため k_DamageTarget はゲーム開始時に永続登録し、受信値をキューにバッファする。
        private readonly Queue<int[]> _damageTargetQueue = new Queue<int[]>();
        private UniTaskCompletionSource<int[]> _damageTargetWaiter;

        public enum MainActionType
        {
            Pass,
            PlaceChar,
            PlayEvent,
            Attack,
        }

        public readonly struct MainActionData
        {
            public readonly MainActionType ActionType;
            public readonly string CardId;
            public readonly string AttackerId;
            public readonly string TargetId;
            public readonly bool TargetsDeck;
            public readonly string[] CostCardIds;

            public bool IsPassed => ActionType == MainActionType.Pass;

            public MainActionData(MainActionType actionType, string cardId = null, string attackerId = null, string targetId = null, bool targetsDeck = false, string[] costCardIds = null)
            {
                ActionType = actionType;
                CardId = cardId;
                AttackerId = attackerId;
                TargetId = targetId;
                TargetsDeck = targetsDeck;
                CostCardIds = costCardIds ?? Array.Empty<string>();
            }

            public static MainActionData Pass() => new MainActionData(MainActionType.Pass);
            public static MainActionData PlaceChar(string cardId, string[] costCardIds = null) => new MainActionData(MainActionType.PlaceChar, cardId: cardId, costCardIds: costCardIds);
            public static MainActionData PlayEvent(string cardId, string[] costCardIds = null) => new MainActionData(MainActionType.PlayEvent, cardId: cardId, costCardIds: costCardIds);
            public static MainActionData Attack(string attackerId, string targetId, bool targetsDeck = false) => new MainActionData(MainActionType.Attack, attackerId: attackerId, targetId: targetId, targetsDeck: targetsDeck);
        }

        public NetworkGameService(GameSessionModel gameSessionModel, CardDatabase cardDatabase, UsernameRepository usernameRepository)
        {
            _gameSessionModel = gameSessionModel;
            _cardDatabase = cardDatabase;
            _localUsername = usernameRepository.Load() ?? string.Empty;
        }

        public async UniTask<OnlineInitialState> PrepareDecksAsync(IReadOnlyList<string> localDeckIds, CancellationToken ct)
        {
            // JoinSessionByIdAsync 直後はまだ CustomMessagingManager が null の場合がある
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                throw new InvalidOperationException("NetworkManager が見つかりません");
            }

            bool isHost = _gameSessionModel.IsHost;
            while (nm.CustomMessagingManager == null
                   || (isHost ? !nm.IsListening : !nm.IsConnectedClient))
            {
                await UniTask.NextFrame(cancellationToken: ct);
            }

            CustomMessagingManager messaging = nm.CustomMessagingManager;

            // 対象同期メッセージは取りこぼし防止のため、対戦開始時にハンドラを永続登録しておく
            RegisterDamageTargetHandler(messaging);

            return isHost
                ? await PrepareAsHostAsync(localDeckIds, messaging, ct)
                : await PrepareAsClientAsync(localDeckIds, messaging, ct);
        }

        private async UniTask<OnlineInitialState> PrepareAsHostAsync(
            IReadOnlyList<string> localDeckIds,
            CustomMessagingManager messaging,
            CancellationToken ct)
        {
            UniTaskCompletionSource receivedTcs = new UniTaskCompletionSource();
            string[] receivedIds = null;
            string receivedUsername = string.Empty;
            ulong remoteClientId = 0;

            void OnDeckSubmit(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_DeckSubmit);
                reader.ReadValueSafe(out string json);
                DeckSubmitPayload payload = JsonUtility.FromJson<DeckSubmitPayload>(json);
                receivedIds = payload.deckIds;
                receivedUsername = payload.username ?? string.Empty;
                remoteClientId = senderId;
                receivedTcs.TrySetResult();
            }

            UniTaskCompletionSource readyTcs = new UniTaskCompletionSource();
            ulong readyClientId = 0;

            void OnClientReady(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_ClientReady);
                readyClientId = senderId;
                readyTcs.TrySetResult();
            }

            messaging.RegisterNamedMessageHandler(k_DeckSubmit, OnDeckSubmit);
            messaging.RegisterNamedMessageHandler(k_ClientReady, OnClientReady);

            // クライアントのハンドラ登録完了を待ってからリクエストを送信（競合防止）
            await readyTcs.Task.AttachExternalCancellation(ct);

            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                messaging.SendNamedMessage(k_RequestDeck, readyClientId, writer);
            }

            await receivedTcs.Task.AttachExternalCancellation(ct);

            _opponentClientId = remoteClientId;

            CardData[] hostDeck = _cardDatabase.BuildDeck(localDeckIds);
            CardData[] clientDeck = _cardDatabase.BuildDeck(receivedIds);

            // 先攻後攻を先に決め、手札枚数を先攻3枚・後攻4枚で配る（双方の初手はドローなしで補正）。
            bool hostGoesFirst = UnityEngine.Random.value > 0.5f;
            int hostHandSize = Mathf.Min(MulliganRule.InitialHandSize(hostGoesFirst), hostDeck.Length);
            int clientHandSize = Mathf.Min(MulliganRule.InitialHandSize(!hostGoesFirst), clientDeck.Length);

            CardData[] hostShuffled = CardArrayUtils.Shuffle(hostDeck);
            CardData[] clientShuffled = CardArrayUtils.Shuffle(clientDeck);

            CardData[] hostHand = Slice(hostShuffled, 0, hostHandSize);
            CardData[] hostRemaining = Slice(hostShuffled, hostHandSize, hostShuffled.Length - hostHandSize);
            CardData[] clientHand = Slice(clientShuffled, 0, clientHandSize);
            CardData[] clientRemaining = Slice(clientShuffled, clientHandSize, clientShuffled.Length - clientHandSize);

            SendInitialStateToClient(messaging, remoteClientId,
                clientHand, clientRemaining,
                hostHand.Length, hostRemaining,
                isLocalFirst: !hostGoesFirst,
                opponentUsername: _localUsername);

            return new OnlineInitialState
            {
                LocalHand = hostHand,
                LocalDeck = hostRemaining,
                OpponentHandCount = clientHand.Length,
                OpponentDeck = clientRemaining,
                IsLocalFirst = hostGoesFirst,
                OpponentUsername = receivedUsername
            };
        }

        private async UniTask<OnlineInitialState> PrepareAsClientAsync(
            IReadOnlyList<string> localDeckIds,
            CustomMessagingManager messaging,
            CancellationToken ct)
        {
            UniTaskCompletionSource<OnlineInitialState> stateTcs = new UniTaskCompletionSource<OnlineInitialState>();

            void OnInitialState(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_InitialState);
                reader.ReadValueSafe(out string json);
                InitialStatePayload payload = JsonUtility.FromJson<InitialStatePayload>(json);
                stateTcs.TrySetResult(new OnlineInitialState
                {
                    LocalHand = _cardDatabase.BuildDeck(payload.localHandIds),
                    LocalDeck = _cardDatabase.BuildDeck(payload.localDeckIds),
                    OpponentHandCount = payload.opponentHandCount,
                    OpponentDeck = _cardDatabase.BuildDeck(payload.opponentDeckIds),
                    IsLocalFirst = payload.isLocalFirst,
                    OpponentUsername = payload.opponentUsername ?? string.Empty
                });
            }

            bool requestReceived = false;

            void OnRequestDeck(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_RequestDeck);
                requestReceived = true;
            }

            // ハンドラを先に登録してから ClientReady をホストに送信
            messaging.RegisterNamedMessageHandler(k_InitialState, OnInitialState);
            messaging.RegisterNamedMessageHandler(k_RequestDeck, OnRequestDeck);

            // NGS_RequestDeck を受信するまで NGS_ClientReady をリトライ送信
            // リレートランスポートの安定化前にメッセージが届かない場合に対応
            while (!requestReceived)
            {
                using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
                {
                    messaging.SendNamedMessage(k_ClientReady, NetworkManager.ServerClientId, writer);
                }
                await UniTask.Delay(200, cancellationToken: ct);
            }

            _opponentClientId = NetworkManager.ServerClientId;

            string[] ids = localDeckIds.ToArray();
            DeckSubmitPayload submitPayload = new DeckSubmitPayload { deckIds = ids, username = _localUsername };
            string submitJson = JsonUtility.ToJson(submitPayload);
            using (FastBufferWriter writer = new FastBufferWriter(submitJson.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(submitJson);
                messaging.SendNamedMessage(k_DeckSubmit, NetworkManager.ServerClientId, writer);
            }

            return await stateTcs.Task.AttachExternalCancellation(ct);
        }

        private static void SendInitialStateToClient(
            CustomMessagingManager messaging,
            ulong clientId,
            CardData[] clientHand,
            CardData[] clientDeck,
            int opponentHandCount,
            CardData[] opponentDeck,
            bool isLocalFirst,
            string opponentUsername)
        {
            InitialStatePayload payload = new InitialStatePayload
            {
                localHandIds = clientHand.Select(c => c.Id).ToArray(),
                localDeckIds = clientDeck.Select(c => c.Id).ToArray(),
                opponentHandCount = opponentHandCount,
                opponentDeckIds = opponentDeck.Select(c => c.Id).ToArray(),
                isLocalFirst = isLocalFirst,
                opponentUsername = opponentUsername
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_InitialState, clientId, writer);
            }
        }

        private static CustomMessagingManager GetMessagingManager()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return null;
            }

            return nm.CustomMessagingManager;
        }

        public void SendMulliganDecision(bool mulliganed, string[] newDeckIds = null)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            MulliganPayload payload = new MulliganPayload { mulliganed = mulliganed, newDeckIds = newDeckIds };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_Mulligan, _opponentClientId, writer);
            }
        }

        public async UniTask<MulliganResult> WaitForOpponentMulliganDecisionAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<MulliganResult> tcs = new UniTaskCompletionSource<MulliganResult>();

            void OnMulligan(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Mulligan);
                reader.ReadValueSafe(out string json);
                MulliganPayload payload = JsonUtility.FromJson<MulliganPayload>(json);
                CardData[] newDeck = payload.mulliganed && payload.newDeckIds != null
                    ? _cardDatabase.BuildDeck(payload.newDeckIds)
                    : null;
                tcs.TrySetResult(new MulliganResult(payload.mulliganed, newDeck));
            }

            messaging.RegisterNamedMessageHandler(k_Mulligan, OnMulligan);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public readonly struct MulliganResult
        {
            public readonly bool Mulliganed;
            public readonly CardData[] NewDeck;

            public MulliganResult(bool mulliganed, CardData[] newDeck)
            {
                Mulliganed = mulliganed;
                NewDeck = newDeck;
            }
        }

        private static CardData[] Slice(CardData[] arr, int start, int length)
        {
            CardData[] result = new CardData[length];
            Array.Copy(arr, start, result, 0, length);
            return result;
        }

        public void SendMainAction(MainActionData action)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            MainActionPayload payload = new MainActionPayload
            {
                actionType = (int)action.ActionType,
                cardId = action.CardId ?? string.Empty,
                attackerId = action.AttackerId ?? string.Empty,
                targetId = action.TargetId ?? string.Empty,
                targetsDeck = action.TargetsDeck,
                costCardIds = action.CostCardIds
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_MainAction, _opponentClientId, writer);
            }
        }

        public UniTask<MainActionData> WaitForOpponentMainActionAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<MainActionData> tcs = new UniTaskCompletionSource<MainActionData>();

            void OnMainAction(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_MainAction);
                reader.ReadValueSafe(out string json);
                MainActionPayload payload = JsonUtility.FromJson<MainActionPayload>(json);
                tcs.TrySetResult(new MainActionData(
                    (MainActionType)payload.actionType,
                    string.IsNullOrEmpty(payload.cardId) ? null : payload.cardId,
                    string.IsNullOrEmpty(payload.attackerId) ? null : payload.attackerId,
                    string.IsNullOrEmpty(payload.targetId) ? null : payload.targetId,
                    payload.targetsDeck,
                    payload.costCardIds));
            }

            messaging.RegisterNamedMessageHandler(k_MainAction, OnMainAction);
            return tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendDrawNotification()
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                messaging.SendNamedMessage(k_Draw, _opponentClientId, writer);
            }
        }

        public async UniTask WaitForOpponentDrawAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();

            void OnDraw(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Draw);
                tcs.TrySetResult();
            }

            messaging.RegisterNamedMessageHandler(k_Draw, OnDraw);
            await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendSwitchAction(string sacrificedCharId, string newCardId)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            bool passed = string.IsNullOrEmpty(newCardId);
            SwitchPayload payload = new SwitchPayload
            {
                passed = passed,
                sacrificedCharId = sacrificedCharId ?? string.Empty,
                newCardId = newCardId ?? string.Empty
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_Switch, _opponentClientId, writer);
            }
        }

        public async UniTask<(string sacrificedCharId, string newCardId)> WaitForOpponentSwitchAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<(string, string)> tcs = new UniTaskCompletionSource<(string, string)>();

            void OnSwitch(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Switch);
                reader.ReadValueSafe(out string json);
                SwitchPayload payload = JsonUtility.FromJson<SwitchPayload>(json);
                if (payload.passed)
                {
                    tcs.TrySetResult((null, null));
                }
                else
                {
                    tcs.TrySetResult((payload.sacrificedCharId, payload.newCardId));
                }
            }

            messaging.RegisterNamedMessageHandler(k_Switch, OnSwitch);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendEvolveAction(string cardId)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            EvolvePayload payload = new EvolvePayload
            {
                passed = string.IsNullOrEmpty(cardId),
                cardId = cardId ?? string.Empty
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_Evolve, _opponentClientId, writer);
            }
        }

        public async UniTask<string> WaitForOpponentEvolveAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<string> tcs = new UniTaskCompletionSource<string>();

            void OnEvolve(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Evolve);
                reader.ReadValueSafe(out string json);
                EvolvePayload payload = JsonUtility.FromJson<EvolvePayload>(json);
                tcs.TrySetResult(payload.passed ? null : payload.cardId);
            }

            messaging.RegisterNamedMessageHandler(k_Evolve, OnEvolve);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        // DamageEnemy 効果の対象を相手クライアントへ伝える。対象は敵フィールド上のインデックスの配列で送る
        // （同名カードが複数いても曖昧にならない）。複数体ダメージにも対応。
        public void SendDamageTargets(int[] indices)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            DamageTargetPayload payload = new DamageTargetPayload
            {
                indices = indices ?? Array.Empty<int>()
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_DamageTarget, _opponentClientId, writer);
            }
        }

        // 対象インデックスの配列を受信する。対象なしの場合は空配列を返す。
        // ハンドラは RegisterDamageTargetHandler で永続登録済みのため、待機開始前に届いた
        // メッセージもキューにバッファされており取りこぼさない。
        public async UniTask<int[]> WaitForOpponentDamageTargetsAsync(CancellationToken ct)
        {
            if (_damageTargetQueue.Count > 0)
            {
                return _damageTargetQueue.Dequeue();
            }

            _damageTargetWaiter = new UniTaskCompletionSource<int[]>();
            try
            {
                return await _damageTargetWaiter.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _damageTargetWaiter = null;
            }
        }

        // k_DamageTarget の永続ハンドラ。待機中なら即解決し、そうでなければキューに積む。
        private void RegisterDamageTargetHandler(CustomMessagingManager messaging)
        {
            void OnDamageTarget(ulong senderId, FastBufferReader reader)
            {
                reader.ReadValueSafe(out string json);
                DamageTargetPayload payload = JsonUtility.FromJson<DamageTargetPayload>(json);
                int[] indices = payload.indices ?? Array.Empty<int>();

                // 待機中なら即解決。短時間に複数届いても取りこぼさないよう、waiter を先に
                // 取り出して null 化し、解決できなければキューへ積む。
                UniTaskCompletionSource<int[]> waiter = _damageTargetWaiter;
                _damageTargetWaiter = null;
                if (waiter != null && waiter.TrySetResult(indices))
                {
                    return;
                }
                _damageTargetQueue.Enqueue(indices);
            }

            messaging.RegisterNamedMessageHandler(k_DamageTarget, OnDamageTarget);
        }

        public void SendRecoverDeckOrder(string[] deckIds)
        {
            SendDeckOrder(k_RecoverDeck, deckIds);
        }

        public UniTask<CardData[]> WaitForOpponentRecoverDeckOrderAsync(CancellationToken ct)
        {
            return WaitForDeckOrderAsync(k_RecoverDeck, ct);
        }

        private void SendDeckOrder(string messageName, string[] deckIds)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            DeckOrderPayload payload = new DeckOrderPayload { deckIds = deckIds };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(messageName, _opponentClientId, writer);
            }
        }

        private UniTask<CardData[]> WaitForDeckOrderAsync(string messageName, CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<CardData[]> tcs = new UniTaskCompletionSource<CardData[]>();

            void OnReceive(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(messageName);
                reader.ReadValueSafe(out string json);
                DeckOrderPayload payload = JsonUtility.FromJson<DeckOrderPayload>(json);
                tcs.TrySetResult(_cardDatabase.BuildDeck(payload.deckIds));
            }

            messaging.RegisterNamedMessageHandler(messageName, OnReceive);
            return tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendSurrenderNotification()
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                messaging.SendNamedMessage(k_Surrender, _opponentClientId, writer);
            }
        }

        public async UniTask WaitForOpponentSurrenderAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();

            void OnSurrender(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Surrender);
                tcs.TrySetResult();
            }

            messaging.RegisterNamedMessageHandler(k_Surrender, OnSurrender);
            await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendRematchRequest()
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                messaging.SendNamedMessage(k_Rematch, _opponentClientId, writer);
            }
        }

        public async UniTask WaitForOpponentRematchAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();

            void OnRematch(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Rematch);
                tcs.TrySetResult();
            }

            messaging.RegisterNamedMessageHandler(k_Rematch, OnRematch);
            await tcs.Task.AttachExternalCancellation(ct);
        }

        public void Dispose()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager m = nm.CustomMessagingManager;
            if (m == null)
            {
                return;
            }
            m.UnregisterNamedMessageHandler(k_ClientReady);
            m.UnregisterNamedMessageHandler(k_RequestDeck);
            m.UnregisterNamedMessageHandler(k_DeckSubmit);
            m.UnregisterNamedMessageHandler(k_InitialState);
            m.UnregisterNamedMessageHandler(k_MainAction);
            m.UnregisterNamedMessageHandler(k_Draw);
            m.UnregisterNamedMessageHandler(k_Surrender);
            m.UnregisterNamedMessageHandler(k_Rematch);
            m.UnregisterNamedMessageHandler(k_Mulligan);
            m.UnregisterNamedMessageHandler(k_RecoverDeck);
            m.UnregisterNamedMessageHandler(k_Switch);
            m.UnregisterNamedMessageHandler(k_Evolve);
            m.UnregisterNamedMessageHandler(k_DamageTarget);
        }

        [Serializable]
        private sealed class DeckSubmitPayload
        {
            public string[] deckIds;
            public string username;
        }

        [Serializable]
        private sealed class InitialStatePayload
        {
            public string[] localHandIds;
            public string[] localDeckIds;
            public int opponentHandCount;
            public string[] opponentDeckIds;
            public bool isLocalFirst;
            public string opponentUsername;
        }

        [Serializable]
        private sealed class MulliganPayload
        {
            public bool mulliganed;
            public string[] newDeckIds;
        }

        [Serializable]
        private sealed class DeckOrderPayload
        {
            public string[] deckIds;
        }

        [Serializable]
        private sealed class SwitchPayload
        {
            public bool passed;
            public string sacrificedCharId;
            public string newCardId;
        }

        [Serializable]
        private sealed class EvolvePayload
        {
            public bool passed;
            public string cardId;
        }

        [Serializable]
        private sealed class DamageTargetPayload
        {
            public int[] indices;
        }

        [Serializable]
        private sealed class MainActionPayload
        {
            public int actionType;
            public string cardId;
            public string attackerId;
            public string targetId;
            public bool targetsDeck;
            public string[] costCardIds;
        }
    }
}

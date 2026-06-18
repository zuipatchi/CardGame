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

        // 対戦中に双方向でやり取りするゲームプレイメッセージ。これらはすべて対戦開始時
        // （PrepareDecksAsync）にハンドラを永続登録し、受信値を per-channel のキューに
        // バッファする。受信側がアニメーション中などでハンドラ登録が遅れても NGO に
        // 破棄されず取りこぼさないため、送受信のタイミングに依存せず同期ずれ・ハングを防ぐ。
        // （ハンドシェイク用の k_ClientReady / k_RequestDeck / k_DeckSubmit / k_InitialState は
        //  一度きりで明示的に順序付けされているため、ここには含めず従来の登録方式を維持する）
        private static readonly string[] k_GameplayChannels =
        {
            k_Draw,
            k_MainAction,
            k_Mulligan,
            k_Switch,
            k_Evolve,
            k_RecoverDeck,
            k_DamageTarget,
            k_Surrender,
            k_Rematch,
        };

        private readonly GameSessionModel _gameSessionModel;
        private readonly CardDatabase _cardDatabase;
        private readonly string _localUsername;
        private ulong _opponentClientId;

        // メッセージ名 → 受信バッファ。RegisterGameplayChannels で対戦開始時に生成する。
        private readonly Dictionary<string, MessageChannel> _channels = new Dictionary<string, MessageChannel>();

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

            // 全ゲームプレイメッセージは取りこぼし防止のため、対戦開始時にハンドラを永続登録しておく
            RegisterGameplayChannels(messaging);

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

            // 先攻後攻を先に決め、手札枚数を先攻3枚・後攻5枚で配る（双方の初手はドローなしで補正）。
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

        // 全ゲームプレイチャンネルのハンドラを永続登録する。各ハンドラは受信した JSON を
        // 対応する MessageChannel に渡し、待機中なら即解決・そうでなければキューに積む。
        // これにより「受信側のハンドラ登録が送信側より遅れてメッセージがロストする」種類の
        // バグ（docs/networking.md セクション 7・9・10・11）を構造的に排除する。
        private void RegisterGameplayChannels(CustomMessagingManager messaging)
        {
            foreach (string name in k_GameplayChannels)
            {
                MessageChannel channel = new MessageChannel();
                _channels[name] = channel;
                messaging.RegisterNamedMessageHandler(name, (ulong senderId, FastBufferReader reader) =>
                {
                    reader.ReadValueSafe(out string json);
                    channel.Receive(json);
                });
            }
        }

        // ペイロードなしメッセージは空文字列を送る。受信ハンドラが一律に string を読むため、
        // チャンネル処理をメッセージ種別によらず統一できる。
        private void SendJson(string messageName, string json)
        {
            CustomMessagingManager messaging = GetMessagingManager();
            if (messaging == null)
            {
                return;
            }
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(messageName, _opponentClientId, writer);
            }
        }

        private UniTask<string> WaitJsonAsync(string messageName, CancellationToken ct)
        {
            return _channels[messageName].WaitAsync(ct);
        }

        public void SendMulliganDecision(bool mulliganed, string[] newDeckIds = null)
        {
            MulliganPayload payload = new MulliganPayload { mulliganed = mulliganed, newDeckIds = newDeckIds };
            SendJson(k_Mulligan, JsonUtility.ToJson(payload));
        }

        public async UniTask<MulliganResult> WaitForOpponentMulliganDecisionAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_Mulligan, ct);
            MulliganPayload payload = JsonUtility.FromJson<MulliganPayload>(json);
            CardData[] newDeck = payload.mulliganed && payload.newDeckIds != null
                ? _cardDatabase.BuildDeck(payload.newDeckIds)
                : null;
            return new MulliganResult(payload.mulliganed, newDeck);
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
            MainActionPayload payload = new MainActionPayload
            {
                actionType = (int)action.ActionType,
                cardId = action.CardId ?? string.Empty,
                attackerId = action.AttackerId ?? string.Empty,
                targetId = action.TargetId ?? string.Empty,
                targetsDeck = action.TargetsDeck,
                costCardIds = action.CostCardIds
            };
            SendJson(k_MainAction, JsonUtility.ToJson(payload));
        }

        public async UniTask<MainActionData> WaitForOpponentMainActionAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_MainAction, ct);
            MainActionPayload payload = JsonUtility.FromJson<MainActionPayload>(json);
            return new MainActionData(
                (MainActionType)payload.actionType,
                string.IsNullOrEmpty(payload.cardId) ? null : payload.cardId,
                string.IsNullOrEmpty(payload.attackerId) ? null : payload.attackerId,
                string.IsNullOrEmpty(payload.targetId) ? null : payload.targetId,
                payload.targetsDeck,
                payload.costCardIds);
        }

        public void SendDrawNotification()
        {
            SendJson(k_Draw, string.Empty);
        }

        public async UniTask WaitForOpponentDrawAsync(CancellationToken ct)
        {
            await WaitJsonAsync(k_Draw, ct);
        }

        public void SendSwitchAction(string sacrificedCharId, string newCardId)
        {
            bool passed = string.IsNullOrEmpty(newCardId);
            SwitchPayload payload = new SwitchPayload
            {
                passed = passed,
                sacrificedCharId = sacrificedCharId ?? string.Empty,
                newCardId = newCardId ?? string.Empty
            };
            SendJson(k_Switch, JsonUtility.ToJson(payload));
        }

        public async UniTask<(string sacrificedCharId, string newCardId)> WaitForOpponentSwitchAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_Switch, ct);
            SwitchPayload payload = JsonUtility.FromJson<SwitchPayload>(json);
            if (payload.passed)
            {
                return (null, null);
            }
            return (payload.sacrificedCharId, payload.newCardId);
        }

        public void SendEvolveAction(string cardId)
        {
            EvolvePayload payload = new EvolvePayload
            {
                passed = string.IsNullOrEmpty(cardId),
                cardId = cardId ?? string.Empty
            };
            SendJson(k_Evolve, JsonUtility.ToJson(payload));
        }

        public async UniTask<string> WaitForOpponentEvolveAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_Evolve, ct);
            EvolvePayload payload = JsonUtility.FromJson<EvolvePayload>(json);
            return payload.passed ? null : payload.cardId;
        }

        // DamageEnemy 効果の対象を相手クライアントへ伝える。対象は敵フィールド上のインデックスの配列で送る
        // （同名カードが複数いても曖昧にならない）。複数体ダメージにも対応。
        public void SendDamageTargets(int[] indices)
        {
            DamageTargetPayload payload = new DamageTargetPayload
            {
                indices = indices ?? Array.Empty<int>()
            };
            SendJson(k_DamageTarget, JsonUtility.ToJson(payload));
        }

        // 対象インデックスの配列を受信する。対象なしの場合は空配列を返す。
        // ハンドラは RegisterGameplayChannels で永続登録済みのため、待機開始前に届いた
        // メッセージもキューにバッファされており取りこぼさない。
        public async UniTask<int[]> WaitForOpponentDamageTargetsAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_DamageTarget, ct);
            DamageTargetPayload payload = JsonUtility.FromJson<DamageTargetPayload>(json);
            return payload.indices ?? Array.Empty<int>();
        }

        public void SendRecoverDeckOrder(string[] deckIds)
        {
            DeckOrderPayload payload = new DeckOrderPayload { deckIds = deckIds };
            SendJson(k_RecoverDeck, JsonUtility.ToJson(payload));
        }

        public async UniTask<CardData[]> WaitForOpponentRecoverDeckOrderAsync(CancellationToken ct)
        {
            string json = await WaitJsonAsync(k_RecoverDeck, ct);
            DeckOrderPayload payload = JsonUtility.FromJson<DeckOrderPayload>(json);
            return _cardDatabase.BuildDeck(payload.deckIds);
        }

        public void SendSurrenderNotification()
        {
            SendJson(k_Surrender, string.Empty);
        }

        public async UniTask WaitForOpponentSurrenderAsync(CancellationToken ct)
        {
            await WaitJsonAsync(k_Surrender, ct);
        }

        public void SendRematchRequest()
        {
            SendJson(k_Rematch, string.Empty);
        }

        public async UniTask WaitForOpponentRematchAsync(CancellationToken ct)
        {
            await WaitJsonAsync(k_Rematch, ct);
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
            foreach (string name in k_GameplayChannels)
            {
                m.UnregisterNamedMessageHandler(name);
            }
            _channels.Clear();
        }

        // 1メッセージ種別ぶんの受信バッファ。NGO のハンドラは受信時に呼ばれるが、待機側
        // （WaitAsync）はそれより前後どちらにも来うる。待機中なら即解決し、そうでなければ
        // キューに積むことで、送受信のタイミングに関係なく取りこぼさない。
        private sealed class MessageChannel
        {
            private readonly Queue<string> _queue = new Queue<string>();
            private UniTaskCompletionSource<string> _waiter;

            public void Receive(string json)
            {
                // 待機中なら即解決。短時間に複数届いても取りこぼさないよう、waiter を先に
                // 取り出して null 化し、解決できなければキューへ積む。
                UniTaskCompletionSource<string> waiter = _waiter;
                _waiter = null;
                if (waiter != null && waiter.TrySetResult(json))
                {
                    return;
                }
                _queue.Enqueue(json);
            }

            public UniTask<string> WaitAsync(CancellationToken ct)
            {
                if (_queue.Count > 0)
                {
                    return UniTask.FromResult(_queue.Dequeue());
                }
                _waiter = new UniTaskCompletionSource<string>();
                return _waiter.Task.AttachExternalCancellation(ct);
            }
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

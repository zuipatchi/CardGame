using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.GameSession;
using Cysharp.Threading.Tasks;
using Main.Card;
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
        private const string k_CharSet = "NGS_CharSet";
        private const string k_PreBattle2 = "NGS_PreBattle2";
        private const string k_Draw = "NGS_Draw";
        private const string k_Surrender = "NGS_Surrender";
        private const string k_Mulligan = "NGS_Mulligan";
        private const string k_Switch = "NGS_Switch";
        private const string k_Evolve = "NGS_Evolve";

        private readonly GameSessionModel _gameSessionModel;
        private readonly CardDatabase _cardDatabase;
        private ulong _opponentClientId;

        public NetworkGameService(GameSessionModel gameSessionModel, CardDatabase cardDatabase)
        {
            _gameSessionModel = gameSessionModel;
            _cardDatabase = cardDatabase;
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
            ulong remoteClientId = 0;

            void OnDeckSubmit(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_DeckSubmit);
                reader.ReadValueSafe(out string json);
                DeckSubmitPayload payload = JsonUtility.FromJson<DeckSubmitPayload>(json);
                receivedIds = payload.deckIds;
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

            int handSize = Mathf.Min(5, Mathf.Min(hostDeck.Length, clientDeck.Length));

            CardData[] hostShuffled = CardArrayUtils.Shuffle(hostDeck);
            CardData[] clientShuffled = CardArrayUtils.Shuffle(clientDeck);

            CardData[] hostHand = Slice(hostShuffled, 0, handSize);
            CardData[] hostRemaining = Slice(hostShuffled, handSize, hostShuffled.Length - handSize);
            CardData[] clientHand = Slice(clientShuffled, 0, handSize);
            CardData[] clientRemaining = Slice(clientShuffled, handSize, clientShuffled.Length - handSize);

            bool hostGoesFirst = UnityEngine.Random.value > 0.5f;

            SendInitialStateToClient(messaging, remoteClientId,
                clientHand, clientRemaining,
                hostHand.Length, hostRemaining,
                isLocalFirst: !hostGoesFirst);

            return new OnlineInitialState
            {
                LocalHand = hostHand,
                LocalDeck = hostRemaining,
                OpponentHandCount = clientHand.Length,
                OpponentDeck = clientRemaining,
                IsLocalFirst = hostGoesFirst
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
                    IsLocalFirst = payload.isLocalFirst
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
            DeckSubmitPayload submitPayload = new DeckSubmitPayload { deckIds = ids };
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
            bool isLocalFirst)
        {
            InitialStatePayload payload = new InitialStatePayload
            {
                localHandIds = clientHand.Select(c => c.Id).ToArray(),
                localDeckIds = clientDeck.Select(c => c.Id).ToArray(),
                opponentHandCount = opponentHandCount,
                opponentDeckIds = opponentDeck.Select(c => c.Id).ToArray(),
                isLocalFirst = isLocalFirst
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_InitialState, clientId, writer);
            }
        }

        public void SendMulliganDecision(bool mulliganed)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
            if (messaging == null)
            {
                return;
            }
            MulliganPayload payload = new MulliganPayload { mulliganed = mulliganed };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_Mulligan, _opponentClientId, writer);
            }
        }

        public async UniTask<bool> WaitForOpponentMulliganDecisionAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<bool> tcs = new UniTaskCompletionSource<bool>();

            void OnMulligan(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Mulligan);
                reader.ReadValueSafe(out string json);
                MulliganPayload payload = JsonUtility.FromJson<MulliganPayload>(json);
                tcs.TrySetResult(payload.mulliganed);
            }

            messaging.RegisterNamedMessageHandler(k_Mulligan, OnMulligan);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        private static CardData[] Slice(CardData[] arr, int start, int length)
        {
            CardData[] result = new CardData[length];
            Array.Copy(arr, start, result, 0, length);
            return result;
        }

        public void SendCharSetAction(string cardId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
            if (messaging == null)
            {
                return;
            }
            CharSetPayload payload = new CharSetPayload
            {
                passed = string.IsNullOrEmpty(cardId),
                cardId = cardId ?? string.Empty
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_CharSet, _opponentClientId, writer);
            }
        }

        public async UniTask<string> WaitForOpponentCharSetAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<string> tcs = new UniTaskCompletionSource<string>();

            void OnCharSet(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_CharSet);
                reader.ReadValueSafe(out string json);
                CharSetPayload payload = JsonUtility.FromJson<CharSetPayload>(json);
                tcs.TrySetResult(payload.passed ? null : payload.cardId);
            }

            messaging.RegisterNamedMessageHandler(k_CharSet, OnCharSet);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendDrawNotification()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
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

        public void SendPreBattle2Action(string cardId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
            if (messaging == null)
            {
                return;
            }
            CharSetPayload payload = new CharSetPayload
            {
                passed = string.IsNullOrEmpty(cardId),
                cardId = cardId ?? string.Empty
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_PreBattle2, _opponentClientId, writer);
            }
        }

        public async UniTask<string> WaitForOpponentPreBattle2Async(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<string> tcs = new UniTaskCompletionSource<string>();

            void OnPreBattle2(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_PreBattle2);
                reader.ReadValueSafe(out string json);
                CharSetPayload payload = JsonUtility.FromJson<CharSetPayload>(json);
                tcs.TrySetResult(payload.passed ? null : payload.cardId);
            }

            messaging.RegisterNamedMessageHandler(k_PreBattle2, OnPreBattle2);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendSwitchAction(string cardId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
            if (messaging == null)
            {
                return;
            }
            CharSetPayload payload = new CharSetPayload
            {
                passed = string.IsNullOrEmpty(cardId),
                cardId = cardId ?? string.Empty
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_Switch, _opponentClientId, writer);
            }
        }

        public async UniTask<string> WaitForOpponentSwitchAsync(CancellationToken ct)
        {
            CustomMessagingManager messaging = NetworkManager.Singleton.CustomMessagingManager;
            UniTaskCompletionSource<string> tcs = new UniTaskCompletionSource<string>();

            void OnSwitch(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_Switch);
                reader.ReadValueSafe(out string json);
                CharSetPayload payload = JsonUtility.FromJson<CharSetPayload>(json);
                tcs.TrySetResult(payload.passed ? null : payload.cardId);
            }

            messaging.RegisterNamedMessageHandler(k_Switch, OnSwitch);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendEvolveAction(string cardId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
            if (messaging == null)
            {
                return;
            }
            CharSetPayload payload = new CharSetPayload
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
                CharSetPayload payload = JsonUtility.FromJson<CharSetPayload>(json);
                tcs.TrySetResult(payload.passed ? null : payload.cardId);
            }

            messaging.RegisterNamedMessageHandler(k_Evolve, OnEvolve);
            return await tcs.Task.AttachExternalCancellation(ct);
        }

        public void SendSurrenderNotification()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            CustomMessagingManager messaging = nm.CustomMessagingManager;
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
            m.UnregisterNamedMessageHandler(k_CharSet);
            m.UnregisterNamedMessageHandler(k_PreBattle2);
            m.UnregisterNamedMessageHandler(k_Draw);
            m.UnregisterNamedMessageHandler(k_Surrender);
            m.UnregisterNamedMessageHandler(k_Mulligan);
            m.UnregisterNamedMessageHandler(k_Switch);
            m.UnregisterNamedMessageHandler(k_Evolve);
        }

        [Serializable]
        private sealed class DeckSubmitPayload
        {
            public string[] deckIds;
        }

        [Serializable]
        private sealed class CharSetPayload
        {
            public bool passed;
            public string cardId;
        }

        [Serializable]
        private sealed class InitialStatePayload
        {
            public string[] localHandIds;
            public string[] localDeckIds;
            public int opponentHandCount;
            public string[] opponentDeckIds;
            public bool isLocalFirst;
        }

        [Serializable]
        private sealed class MulliganPayload
        {
            public bool mulliganed;
        }
    }
}

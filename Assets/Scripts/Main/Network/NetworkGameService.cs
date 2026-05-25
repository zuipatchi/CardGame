using System;
using System.Collections.Generic;
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

        private readonly GameSessionModel _gameSessionModel;
        private readonly CardDatabase _cardDatabase;

        public NetworkGameService(GameSessionModel gameSessionModel, CardDatabase cardDatabase)
        {
            _gameSessionModel = gameSessionModel;
            _cardDatabase = cardDatabase;
        }

        public async UniTask<OnlineInitialState> PrepareDecksAsync(IReadOnlyList<string> localDeckIds, CancellationToken ct)
        {
            // NGO が起動して接続が確立するまで待機
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

            // 1. DeckSubmit と ClientReady 両方のハンドラを先に登録
            messaging.RegisterNamedMessageHandler(k_DeckSubmit, OnDeckSubmit);
            messaging.RegisterNamedMessageHandler(k_ClientReady, OnClientReady);

            // 2. クライアントが PrepareAsClientAsync に到達してハンドラ登録済みになるまで待つ
            await readyTcs.Task.AttachExternalCancellation(ct);

            // 3. クライアントに NGS_RequestDeck を送信
            using (FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp))
            {
                messaging.SendNamedMessage(k_RequestDeck, readyClientId, writer);
            }

            await receivedTcs.Task.AttachExternalCancellation(ct);

            CardData[] hostDeck = _cardDatabase.BuildDeck(localDeckIds);
            CardData[] clientDeck = _cardDatabase.BuildDeck(receivedIds);

            int handSize = Mathf.Min(5, Mathf.Min(hostDeck.Length, clientDeck.Length));

            CardData[] hostShuffled = Shuffle(hostDeck);
            CardData[] clientShuffled = Shuffle(clientDeck);

            CardData[] hostHand = Slice(hostShuffled, 0, handSize);
            CardData[] hostRemaining = Slice(hostShuffled, handSize, hostShuffled.Length - handSize);
            CardData[] clientHand = Slice(clientShuffled, 0, handSize);
            CardData[] clientRemaining = Slice(clientShuffled, handSize, clientShuffled.Length - handSize);

            SendInitialStateToClient(messaging, remoteClientId,
                clientHand, clientRemaining,
                hostHand.Length, hostRemaining.Length,
                isLocalFirst: false);

            return new OnlineInitialState
            {
                LocalHand = hostHand,
                LocalDeck = hostRemaining,
                OpponentHandCount = clientHand.Length,
                OpponentDeckCount = clientRemaining.Length,
                IsLocalFirst = true
            };
        }

        private async UniTask<OnlineInitialState> PrepareAsClientAsync(
            IReadOnlyList<string> localDeckIds,
            CustomMessagingManager messaging,
            CancellationToken ct)
        {
            UniTaskCompletionSource<OnlineInitialState> stateTcs = new UniTaskCompletionSource<OnlineInitialState>();
            UniTaskCompletionSource requestTcs = new UniTaskCompletionSource();

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
                    OpponentDeckCount = payload.opponentDeckCount,
                    IsLocalFirst = payload.isLocalFirst
                });
            }

            bool requestReceived = false;

            void OnRequestDeck(ulong senderId, FastBufferReader reader)
            {
                messaging.UnregisterNamedMessageHandler(k_RequestDeck);
                requestReceived = true;
                requestTcs.TrySetResult();
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

            await requestTcs.Task.AttachExternalCancellation(ct);

            string[] ids = new string[localDeckIds.Count];
            for (int i = 0; i < localDeckIds.Count; i++)
            {
                ids[i] = localDeckIds[i];
            }

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
            int opponentDeckCount,
            bool isLocalFirst)
        {
            InitialStatePayload payload = new InitialStatePayload
            {
                localHandIds = GetIds(clientHand),
                localDeckIds = GetIds(clientDeck),
                opponentHandCount = opponentHandCount,
                opponentDeckCount = opponentDeckCount,
                isLocalFirst = isLocalFirst
            };
            string json = JsonUtility.ToJson(payload);
            using (FastBufferWriter writer = new FastBufferWriter(json.Length * 2 + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                messaging.SendNamedMessage(k_InitialState, clientId, writer);
            }
        }

        private static string[] GetIds(CardData[] cards)
        {
            string[] ids = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                ids[i] = cards[i].Id;
            }
            return ids;
        }

        private static CardData[] Slice(CardData[] arr, int start, int length)
        {
            CardData[] result = new CardData[length];
            Array.Copy(arr, start, result, 0, length);
            return result;
        }

        private static CardData[] Shuffle(CardData[] cards)
        {
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards;
        }

        public void Dispose()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }
            nm.CustomMessagingManager?.UnregisterNamedMessageHandler(k_ClientReady);
            nm.CustomMessagingManager?.UnregisterNamedMessageHandler(k_RequestDeck);
            nm.CustomMessagingManager?.UnregisterNamedMessageHandler(k_DeckSubmit);
            nm.CustomMessagingManager?.UnregisterNamedMessageHandler(k_InitialState);
        }

        [Serializable]
        private sealed class DeckSubmitPayload
        {
            public string[] deckIds;
        }

        [Serializable]
        private sealed class InitialStatePayload
        {
            public string[] localHandIds;
            public string[] localDeckIds;
            public int opponentHandCount;
            public int opponentDeckCount;
            public bool isLocalFirst;
        }
    }
}

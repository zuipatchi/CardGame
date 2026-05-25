using System;
using System.Collections.Generic;
using System.Threading;
using Common.GameSession;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Matching
{
    public class MatchingService
    {
        public const string QuickMatchName = "QuickMatch";

        private readonly GameSessionModel _gameSessionModel;

        public MatchingService(GameSessionModel gameSessionModel)
        {
            _gameSessionModel = gameSessionModel;
        }

        public async UniTask AuthenticateAsync(CancellationToken ct = default)
        {
            await UnityServices.InitializeAsync().AsUniTask().AttachExternalCancellation(ct);
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync()
                    .AsUniTask()
                    .AttachExternalCancellation(ct);
            }
        }

        public async UniTask<IReadOnlyList<LobbyInfo>> GetRoomsAsync(CancellationToken ct = default)
        {
            QuerySessionsOptions queryOptions = new QuerySessionsOptions();
            QuerySessionsResults results = await MultiplayerService.Instance
                .QuerySessionsAsync(queryOptions)
                .AsUniTask()
                .AttachExternalCancellation(ct);

            List<LobbyInfo> rooms = new(results.Sessions.Count);
            foreach (ISessionInfo info in results.Sessions)
            {
                int playerCount = info.MaxPlayers - info.AvailableSlots;
                rooms.Add(new LobbyInfo(info.Id, info.Name, playerCount, info.MaxPlayers));
            }
            return rooms;
        }

        public async UniTask<IHostSession> CreateRoomAsync(string roomName, CancellationToken ct = default)
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            DisableNgoSceneManagement();

            SessionOptions options = new SessionOptions
            {
                Name = roomName,
                MaxPlayers = 2
            }.WithRelayNetwork();
            IHostSession session = await MultiplayerService.Instance
                .CreateSessionAsync(options)
                .AsUniTask()
                .AttachExternalCancellation(ct);

            _gameSessionModel.SetSession(session);
            return session;
        }

        public async UniTask JoinRoomAsync(string sessionId, CancellationToken ct = default)
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            DisableNgoSceneManagement();

            ISession session = await MultiplayerService.Instance
                .JoinSessionByIdAsync(sessionId)
                .AsUniTask()
                .AttachExternalCancellation(ct);

            _gameSessionModel.SetSession(session);
        }

        public async UniTask<LobbyInfo?> FindQuickMatchRoomAsync(CancellationToken ct = default)
        {
            IReadOnlyList<LobbyInfo> rooms = await GetRoomsAsync(ct);
            foreach (LobbyInfo room in rooms)
            {
                if (room.Name == QuickMatchName && room.PlayerCount < room.MaxPlayers)
                {
                    return room;
                }
            }
            return null;
        }

        private static void DisableNgoSceneManagement()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.NetworkConfig.EnableSceneManagement = false;
            }
        }

        public async UniTask<bool> WaitForPlayerAsync(ISession session, TimeSpan timeout, CancellationToken ct = default)
        {
            UniTaskCompletionSource tcs = new();

            void OnPlayerJoined(string playerId)
            {
                session.PlayerJoined -= OnPlayerJoined;
                tcs.TrySetResult();
            }

            // ハンドラを先に登録してからセッション状態を確認（競合防止）
            // CreateRoomAsync 返却直後に参加された場合、PlayerJoined が登録前に発火する
            session.PlayerJoined += OnPlayerJoined;

            if (session.AvailableSlots == 0)
            {
                session.PlayerJoined -= OnPlayerJoined;
                return true;
            }

            using CancellationTokenSource timeoutCts = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

            try
            {
                await tcs.Task.AttachExternalCancellation(linked.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                session.PlayerJoined -= OnPlayerJoined;
                await UniTask.SwitchToMainThread();
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                return false;
            }
        }
    }
}

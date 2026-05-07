using System;
using System.Collections.Generic;
using System.Threading;
using Common.GameSession;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Matching
{
    public class MatchingService
    {
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

            SessionOptions options = new SessionOptions
            {
                Name = roomName,
                MaxPlayers = 2
            };
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
                if (room.Name == "QuickMatch" && room.PlayerCount < room.MaxPlayers)
                {
                    return room;
                }
            }
            return null;
        }

        public async UniTask<bool> WaitForPlayerAsync(ISession session, TimeSpan timeout, CancellationToken ct = default)
        {
            UniTaskCompletionSource tcs = new();

            void OnPlayerJoined(string playerId)
            {
                session.PlayerJoined -= OnPlayerJoined;
                tcs.TrySetResult();
            }

            session.PlayerJoined += OnPlayerJoined;

            using CancellationTokenSource timeoutCts = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
            using CancellationTokenRegistration reg = linked.Token.Register(() =>
            {
                session.PlayerJoined -= OnPlayerJoined;
                tcs.TrySetCanceled();
            });

            try
            {
                await tcs.Task;
                return true;
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                return false;
            }
        }
    }
}

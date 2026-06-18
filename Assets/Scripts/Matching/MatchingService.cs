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
        private bool _isQuerying;

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
            if (_isQuerying)
            {
                return Array.Empty<LobbyInfo>();
            }

            _isQuerying = true;
            try
            {
                QuerySessionsOptions queryOptions = new QuerySessionsOptions();
                QuerySessionsResults results;
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
                    // 次のリフレッシュで再試行される。
                    return Array.Empty<LobbyInfo>();
                }

                List<LobbyInfo> rooms = new(results.Sessions.Count);
                foreach (ISessionInfo info in results.Sessions)
                {
                    if (info.AvailableSlots == 0)
                    {
                        continue;
                    }
                    if (info.Properties != null &&
                        info.Properties.TryGetValue("started", out SessionProperty startedProp) &&
                        startedProp.Value == "1")
                    {
                        continue;
                    }
                    int playerCount = info.MaxPlayers - info.AvailableSlots;
                    rooms.Add(new LobbyInfo(info.Id, info.Name, playerCount, info.MaxPlayers));
                }
                return rooms;
            }
            finally
            {
                _isQuerying = false;
            }
        }

        public async UniTask<IHostSession> CreateRoomAsync(string roomName, CancellationToken ct = default)
        {
            await _gameSessionModel.LeaveCurrentSessionAsync();
            DisableNgoSceneManagement();

            SessionOptions options = new SessionOptions
            {
                Name = roomName,
                MaxPlayers = 2,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { "started", new SessionProperty("0", VisibilityPropertyOptions.Public) }
                }
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

        public async UniTask MarkRoomStartedAsync(IHostSession session, CancellationToken ct = default)
        {
            session.SetProperty("started", new SessionProperty("1", VisibilityPropertyOptions.Public));
            await session.SavePropertiesAsync().AsUniTask().AttachExternalCancellation(ct);
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

        /// <summary>
        /// クイックマッチの同時作成を解消する。自分でルームを作成した直後に呼び、
        /// 同時に作られた別のクイックマッチルームが無いか一定時間ポーリングで探す。
        /// 見つかったルームの ID が自分のルーム ID より小さい場合だけそのルームを返す
        /// （ID の小さい方をホストとする決定論的タイブレーク）。両者が同じ基準で判断するため、
        /// 必ず片方だけが相手のルームへ入り直し、確実に 1 組へ収束する。
        /// </summary>
        public async UniTask<LobbyInfo?> ReconcileQuickMatchAsync(string myRoomId, TimeSpan duration, CancellationToken ct = default)
        {
            using CancellationTokenSource timeoutCts = new(duration);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
            try
            {
                while (!linked.Token.IsCancellationRequested)
                {
                    (LobbyInfo? lowerRival, bool sawHigherRival) = await ScanQuickMatchRivalsAsync(myRoomId, ct);
                    if (lowerRival.HasValue)
                    {
                        // ID が小さいルームが居る → 自分は入り直す側
                        return lowerRival;
                    }
                    if (sawHigherRival)
                    {
                        // 自分が一番小さい ID = ホスト。早期に待機へ移り、相手の参加イベントを取りこぼさない
                        return null;
                    }
                    await UniTask.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken: linked.Token);
                }
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
            }
            return null;
        }

        private async UniTask<(LobbyInfo? lowerRival, bool sawHigherRival)> ScanQuickMatchRivalsAsync(string myRoomId, CancellationToken ct)
        {
            IReadOnlyList<LobbyInfo> rooms = await GetRoomsAsync(ct);
            LobbyInfo? lower = null;
            bool sawHigher = false;
            foreach (LobbyInfo room in rooms)
            {
                if (room.Name != QuickMatchName || room.LobbyId == myRoomId)
                {
                    continue;
                }
                if (room.PlayerCount >= room.MaxPlayers)
                {
                    continue;
                }
                if (string.CompareOrdinal(room.LobbyId, myRoomId) < 0)
                {
                    if (!lower.HasValue || string.CompareOrdinal(room.LobbyId, lower.Value.LobbyId) < 0)
                    {
                        lower = room;
                    }
                }
                else
                {
                    sawHigher = true;
                }
            }
            return (lower, sawHigher);
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
            bool joined = false;

            void OnPlayerJoined(string playerId)
            {
                session.PlayerJoined -= OnPlayerJoined;
                joined = true;
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
                // PlayerJoined イベントが主経路。ただし ReconcileQuickMatch でハンドラ登録前に
                // 相手が参加したケースに備え、AvailableSlots も定期ポーリングで監視する。
                while (true)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (joined || session.AvailableSlots == 0)
                    {
                        session.PlayerJoined -= OnPlayerJoined;
                        return true;
                    }
                    await UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: linked.Token);
                }
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

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine.UIElements;

namespace Matching
{
    public partial class MatchingPresenter
    {
        // ─── ルーム一覧（取得・自動更新・リスト構築） ────────────────────

        private async UniTaskVoid AutoRefreshLoopAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);
                    await RefreshRoomsAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTask RefreshRoomsAsync(System.Threading.CancellationToken ct)
        {
            IReadOnlyList<LobbyInfo> rooms = await _matchingService.GetRoomsAsync(ct);
            _model.Rooms.Value = rooms;
            _model.State.Value = MatchingState.BrowsingRooms;
            RebuildRoomList(rooms);
        }

        private void RebuildRoomList(IReadOnlyList<LobbyInfo> rooms)
        {
            _roomList.Clear();
            bool hasVisible = false;
            foreach (LobbyInfo room in rooms)
            {
                if (room.Name == MatchingService.QuickMatchName)
                {
                    continue;
                }
                hasVisible = true;
                string sessionId = room.LobbyId;
                Button roomButton = new Button(() =>
                {
                    _soundPlayer.PlaySE(_soundStore.EnterSE);
                    OnRoomSelectedAsync(sessionId).Forget();
                })
                {
                    text = $"{room.Name}  {room.PlayerCount}/{room.MaxPlayers}"
                };
                roomButton.AddToClassList("room-item");
                _roomList.Add(roomButton);
            }
            if (!hasVisible)
            {
                Label emptyLabel = new Label { text = "ルームがありません" };
                emptyLabel.AddToClassList("empty-state");
                _roomList.Add(emptyLabel);
            }
        }
    }
}

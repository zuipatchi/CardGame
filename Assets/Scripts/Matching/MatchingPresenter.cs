using System;
using System.Collections.Generic;
using Common.GameSession;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Matching
{
    public class MatchingPresenter : MonoBehaviour, IStartable
    {
        private static readonly TimeSpan _quickMatchTimeoutDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _createRoomTimeoutDuration = TimeSpan.FromSeconds(120);

        private MatchingModel _model;
        private MatchingService _matchingService;
        private SceneTransitioner _sceneTransitioner;
        private GameSessionModel _gameSessionModel;

        private Button _backButton;
        private ScrollView _roomList;
        private Button _quickMatchButton;
        private Button _createButton;
        private VisualElement _loadingOverlay;
        private System.Threading.CancellationTokenSource _autoRefreshCts;
        private Label _loadingLabel;
        private VisualElement _waitingOverlay;
        private Label _waitingLabel;
        private Button _cancelWaitButton;
        private Button _timeoutCloseButton;
        private VisualElement _errorOverlay;
        private Button _errorCloseButton;

        [Inject]
        public void Construct(
            MatchingModel model,
            MatchingService matchingService,
            SceneTransitioner sceneTransitioner,
            GameSessionModel gameSessionModel)
        {
            _model = model;
            _matchingService = matchingService;
            _sceneTransitioner = sceneTransitioner;
            _gameSessionModel = gameSessionModel;
        }

        private void Awake()
        {
            UIDocument uiDocument = GetComponent<UIDocument>();
            VisualElement root = uiDocument.rootVisualElement;

            _backButton = root.Q<Button>("BackButton");
            _roomList = root.Q<ScrollView>("RoomList");
            _quickMatchButton = root.Q<Button>("QuickMatchButton");
            _createButton = root.Q<Button>("CreateButton");
            _loadingOverlay = root.Q<VisualElement>("LoadingOverlay");
            _loadingLabel = root.Q<Label>("LoadingLabel");
            _waitingOverlay = root.Q<VisualElement>("WaitingOverlay");
            _waitingLabel = root.Q<Label>("WaitingLabel");
            _cancelWaitButton = root.Q<Button>("CancelWaitButton");
            _timeoutCloseButton = root.Q<Button>("TimeoutCloseButton");
            _errorOverlay = root.Q<VisualElement>("ErrorOverlay");
            _errorCloseButton = root.Q<Button>("ErrorCloseButton");
        }

        void IStartable.Start()
        {
            _backButton.clicked += () => _sceneTransitioner.Transit(Scenes.Home).Forget();
            _quickMatchButton.clicked += () => OnQuickMatchButtonClickedAsync().Forget();
            _createButton.clicked += () => OnCreateButtonClickedAsync().Forget();
            _cancelWaitButton.clicked += () => CancelWaitAsync().Forget();
            _timeoutCloseButton.clicked += () => InitializeAsync(destroyCancellationToken).Forget();
            _errorCloseButton.clicked += () => InitializeAsync(destroyCancellationToken).Forget();

            _model.State
                .Subscribe(ApplyState)
                .AddTo(destroyCancellationToken);

            InitializeAsync(destroyCancellationToken).Forget();
        }

        private void ApplyState(MatchingState state)
        {
            bool isLoading = state is MatchingState.Authenticating
                or MatchingState.CreatingRoom
                or MatchingState.JoiningRoom
                or MatchingState.Starting;
            bool isWaiting = state is MatchingState.WaitingForPlayer or MatchingState.WaitingInCreatedRoom or MatchingState.TimedOut;
            bool isTimedOut = state == MatchingState.TimedOut;
            bool isError = state == MatchingState.Error;

            _loadingOverlay.style.display = isLoading ? DisplayStyle.Flex : DisplayStyle.None;
            _waitingOverlay.style.display = isWaiting ? DisplayStyle.Flex : DisplayStyle.None;
            _errorOverlay.style.display = isError ? DisplayStyle.Flex : DisplayStyle.None;
            _backButton.SetEnabled(state is MatchingState.BrowsingRooms or MatchingState.Error or MatchingState.TimedOut);
            _quickMatchButton.SetEnabled(state == MatchingState.BrowsingRooms);
            _createButton.SetEnabled(state == MatchingState.BrowsingRooms);

            if (state == MatchingState.BrowsingRooms)
            {
                _autoRefreshCts?.Cancel();
                _autoRefreshCts?.Dispose();
                _autoRefreshCts = new System.Threading.CancellationTokenSource();
                AutoRefreshLoopAsync(_autoRefreshCts.Token).Forget();
            }
            else
            {
                _autoRefreshCts?.Cancel();
                _autoRefreshCts?.Dispose();
                _autoRefreshCts = null;
            }

            _loadingLabel.text = state switch
            {
                MatchingState.Authenticating => "認証中...",
                MatchingState.CreatingRoom => "ルーム作成中...",
                MatchingState.JoiningRoom => "参加中...",
                MatchingState.Starting => "ゲーム開始...",
                _ => string.Empty
            };

            if (isWaiting)
            {
                _waitingLabel.text = state switch
                {
                    MatchingState.TimedOut => "タイムアウトしました",
                    MatchingState.WaitingInCreatedRoom => "プレイヤーを待っています...\n2分で自動解散します",
                    _ => "プレイヤーを待っています..."
                };
                _cancelWaitButton.style.display = isTimedOut ? DisplayStyle.None : DisplayStyle.Flex;
                _timeoutCloseButton.style.display = isTimedOut ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private async UniTaskVoid InitializeAsync(System.Threading.CancellationToken ct)
        {
            try
            {
                _model.State.Value = MatchingState.Authenticating;
                await _matchingService.AuthenticateAsync(ct);
                await RefreshRoomsAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"初期化に失敗: {e}");
                _model.State.Value = MatchingState.Error;
            }
        }

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
                Button roomButton = new Button(() => OnRoomSelectedAsync(sessionId).Forget())
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

        private async UniTaskVoid OnQuickMatchButtonClickedAsync()
        {
            try
            {
                _model.State.Value = MatchingState.JoiningRoom;
                LobbyInfo? room = await _matchingService.FindQuickMatchRoomAsync(destroyCancellationToken);

                if (room.HasValue)
                {
                    await _matchingService.JoinRoomAsync(room.Value.LobbyId);
                    _model.State.Value = MatchingState.Starting;
                    await TransitToMainAsync();
                }
                else
                {
                    _model.State.Value = MatchingState.CreatingRoom;
                    IHostSession session = await _matchingService.CreateRoomAsync(MatchingService.QuickMatchName, destroyCancellationToken);
                    _model.State.Value = MatchingState.WaitingForPlayer;

                    bool found = await _matchingService.WaitForPlayerAsync(session, _quickMatchTimeoutDuration, destroyCancellationToken);
                    if (found)
                    {
                        await _matchingService.MarkRoomStartedAsync(session, destroyCancellationToken);
                        _model.State.Value = MatchingState.Starting;
                        await TransitToMainAsync();
                    }
                    else
                    {
                        _model.State.Value = MatchingState.TimedOut;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"クイックマッチに失敗: {e}");
                if (this == null) return;
                _model.State.Value = MatchingState.Error;
            }
        }

        private async UniTaskVoid OnCreateButtonClickedAsync()
        {
            try
            {
                _model.State.Value = MatchingState.CreatingRoom;
                IHostSession session = await _matchingService.CreateRoomAsync("Room", destroyCancellationToken);
                _model.State.Value = MatchingState.WaitingInCreatedRoom;

                bool found = await _matchingService.WaitForPlayerAsync(session, _createRoomTimeoutDuration, destroyCancellationToken);
                if (found)
                {
                    await _matchingService.MarkRoomStartedAsync(session, destroyCancellationToken);
                    _model.State.Value = MatchingState.Starting;
                    await TransitToMainAsync();
                }
                else
                {
                    _model.State.Value = MatchingState.TimedOut;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"ルーム作成に失敗: {e}");
                _model.State.Value = MatchingState.Error;
            }
        }

        private async UniTaskVoid OnRoomSelectedAsync(string sessionId)
        {
            try
            {
                _model.State.Value = MatchingState.JoiningRoom;
                await _matchingService.JoinRoomAsync(sessionId);
                _model.State.Value = MatchingState.Starting;
                await TransitToMainAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"ルーム参加に失敗: {e}");
                _model.State.Value = MatchingState.Error;
            }
        }

        private async UniTask TransitToMainAsync()
        {
            await _sceneTransitioner.Transit(Scenes.Main);
        }

        private async UniTaskVoid CancelWaitAsync()
        {
            try
            {
                await _gameSessionModel.LeaveCurrentSessionAsync();
                await RefreshRoomsAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"キャンセルに失敗: {e}");
                _model.State.Value = MatchingState.Error;
            }
        }
    }
}

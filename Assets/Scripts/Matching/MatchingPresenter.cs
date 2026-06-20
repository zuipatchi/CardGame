using System;
using Common.GameSession;
using Common.SceneManagement;
using Common.SoundManagement;
using Common.Store;
using Common.Username;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Matching
{
    public partial class MatchingPresenter : MonoBehaviour, IStartable
    {
        private static readonly TimeSpan _quickMatchTimeoutDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _createRoomTimeoutDuration = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan _quickMatchReconcileDuration = TimeSpan.FromSeconds(6);

        private MatchingModel _model;
        private MatchingService _matchingService;
        private SceneTransitioner _sceneTransitioner;
        private GameSessionModel _gameSessionModel;
        private SoundPlayer _soundPlayer;
        private SoundStore _soundStore;
        private string _username;

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
            GameSessionModel gameSessionModel,
            SoundPlayer soundPlayer,
            SoundStore soundStore,
            UsernameRepository usernameRepository)
        {
            _model = model;
            _matchingService = matchingService;
            _sceneTransitioner = sceneTransitioner;
            _gameSessionModel = gameSessionModel;
            _soundPlayer = soundPlayer;
            _soundStore = soundStore;
            _username = usernameRepository.Load() ?? string.Empty;
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

        private void OnDestroy()
        {
            _autoRefreshCts?.Cancel();
            _autoRefreshCts?.Dispose();
            _autoRefreshCts = null;
        }

        void IStartable.Start()
        {
            _backButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Enter2SE);
                _sceneTransitioner.Transit(Scenes.Home).Forget();
            };
            _quickMatchButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.EnterSE);
                OnQuickMatchButtonClickedAsync().Forget();
            };
            _createButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.EnterSE);
                OnCreateButtonClickedAsync().Forget();
            };
            _cancelWaitButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                CancelWaitAsync().Forget();
            };
            _timeoutCloseButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                InitializeAsync(destroyCancellationToken).Forget();
            };
            _errorCloseButton.clicked += () =>
            {
                _soundPlayer.PlaySE(_soundStore.Cancel1SE);
                InitializeAsync(destroyCancellationToken).Forget();
            };

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

        private void HandleMatchingError(string operation, Exception e)
        {
            Debug.LogError($"{operation}に失敗: {e}");
            if (this == null) return;
            _model.State.Value = MatchingState.Error;
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
                HandleMatchingError("初期化", e);
            }
        }

        private async UniTaskVoid OnQuickMatchButtonClickedAsync()
        {
            try
            {
                _model.State.Value = MatchingState.JoiningRoom;

                // 同時押しの衝突頻度を下げ、照会の伝搬遅延を吸収するためのランダムジッター
                await UniTask.Delay(
                    TimeSpan.FromMilliseconds(UnityEngine.Random.Range(0, 800)),
                    cancellationToken: destroyCancellationToken);

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

                    // 同時に別のクイックマッチルームが作られていないか確認。
                    // ID の大きい側が自分の部屋を捨てて、ID の小さい側のルームへ入り直す。
                    LobbyInfo? rival = await _matchingService.ReconcileQuickMatchAsync(session.Id, _quickMatchReconcileDuration, destroyCancellationToken);
                    if (rival.HasValue)
                    {
                        // JoinRoomAsync 内の LeaveCurrentSessionAsync で自分の部屋は破棄される
                        await _matchingService.JoinRoomAsync(rival.Value.LobbyId);
                        _model.State.Value = MatchingState.Starting;
                        await TransitToMainAsync();
                        return;
                    }

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
                        await _gameSessionModel.LeaveCurrentSessionAsync();
                        _model.State.Value = MatchingState.TimedOut;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                HandleMatchingError("クイックマッチ", e);
            }
        }

        private async UniTaskVoid OnCreateButtonClickedAsync()
        {
            try
            {
                _model.State.Value = MatchingState.CreatingRoom;
                string roomName = string.IsNullOrEmpty(_username) ? "Room" : $"{_username}のルーム";
                IHostSession session = await _matchingService.CreateRoomAsync(roomName, destroyCancellationToken);
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
                    await _gameSessionModel.LeaveCurrentSessionAsync();
                    _model.State.Value = MatchingState.TimedOut;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                HandleMatchingError("ルーム作成", e);
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
                HandleMatchingError("ルーム参加", e);
            }
        }

        private async UniTask TransitToMainAsync()
        {
            _soundPlayer.PlaySE(_soundStore.ResultSE);
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
                HandleMatchingError("キャンセル", e);
            }
        }
    }
}

using System;
using System.Threading;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── 再戦 ───────────────────────────────────────────────────────
        // オンラインは双方が「再戦する」を押したら Main シーンを再ロードして新規対戦を開始する。
        // CPU 戦は待ち時間なしで即再ロードする。

        // OnGameEnd（オンライン時）から呼び、相手の再戦希望と退出を監視する。
        private void StartRematchWatch()
        {
            WatchForOpponentRematchAsync(destroyCancellationToken).Forget();

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientDisconnectCallback += OnNetworkClientDisconnected;
            }
        }

        private void UnregisterRematchCallbacks()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
            }
        }

        private void OnRematchClicked()
        {
            if (_localRematchRequested || _rematchStarted || _opponentLeft)
            {
                return;
            }
            _localRematchRequested = true;

            if (!_isOnline)
            {
                // CPU 戦：待ち時間なしで即再戦
                StartRematch();
                return;
            }

            _networkGameService.SendRematchRequest();
            ShowRematchWaiting();
            TryStartOnlineRematch();
        }

        private async UniTaskVoid WatchForOpponentRematchAsync(CancellationToken ct)
        {
            try
            {
                await _networkGameService.WaitForOpponentRematchAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_opponentLeft || _rematchStarted)
            {
                return;
            }

            _opponentRematchRequested = true;

            if (_localRematchRequested)
            {
                TryStartOnlineRematch();
            }
            else
            {
                ShowOpponentWantsRematch();
            }
        }

        private void OnNetworkClientDisconnected(ulong clientId)
        {
            // ゲーム終了後の再戦フェーズでの切断は相手の退出とみなす（2人セッション前提）。
            if (_rematchStarted || _opponentLeft)
            {
                return;
            }
            _opponentLeft = true;
            ShowOpponentLeft();
        }

        private void TryStartOnlineRematch()
        {
            if (_localRematchRequested && _opponentRematchRequested && !_rematchStarted && !_opponentLeft)
            {
                StartRematch();
            }
        }

        private void StartRematch()
        {
            if (_rematchStarted)
            {
                return;
            }
            _rematchStarted = true;
            _sceneTransitioner.Reload(Scenes.Main).Forget();
        }

        // ─── 再戦 UI 状態 ────────────────────────────────────────────────

        private void ShowRematchWaiting()
        {
            if (_gameEndButtonRow != null)
            {
                _gameEndButtonRow.style.display = DisplayStyle.None;
            }
            if (_gameEndRematchStatusLabel != null)
            {
                _gameEndRematchStatusLabel.text = "対戦相手を待っています...";
                _gameEndRematchStatusLabel.style.display = DisplayStyle.Flex;
            }
        }

        private void ShowOpponentWantsRematch()
        {
            if (_gameEndRematchStatusLabel != null)
            {
                _gameEndRematchStatusLabel.text = "対戦相手が再戦を希望しています";
                _gameEndRematchStatusLabel.style.display = DisplayStyle.Flex;
            }
        }

        private void ShowOpponentLeft()
        {
            // 再戦ボタンを消し、「ホームに戻る」のみ残す。待機表示中なら戻す。
            if (_gameEndRematchButton != null)
            {
                _gameEndRematchButton.style.display = DisplayStyle.None;
            }
            if (_gameEndButtonRow != null)
            {
                _gameEndButtonRow.style.display = DisplayStyle.Flex;
                _gameEndButtonRow.style.opacity = 1f;
            }
            if (_gameEndRematchStatusLabel != null)
            {
                _gameEndRematchStatusLabel.text = "対戦相手が退出しました";
                _gameEndRematchStatusLabel.style.display = DisplayStyle.Flex;
            }
        }
    }
}

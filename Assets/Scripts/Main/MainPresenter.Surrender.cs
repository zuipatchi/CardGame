using System;
using System.Threading;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── 投了・タイムアウト・退室 ────────────────────────────────────

        private void ShowMatchTimeoutModal(VisualElement root)
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("match-timeout-overlay");

            VisualElement panel = new VisualElement();
            panel.AddToClassList("match-timeout-panel");

            Label title = new Label("対戦が成立しませんでした");
            title.AddToClassList("match-timeout-title");
            panel.Add(title);

            Button closeButton = new Button();
            closeButton.text = "閉じる";
            closeButton.AddToClassList("match-timeout-close-button");
            closeButton.clicked += () => _sceneTransitioner.Transit(Scenes.Matching).Forget();
            panel.Add(closeButton);

            overlay.Add(panel);
            root.Add(overlay);
        }

        private async UniTaskVoid LeaveSessionAndGoHomeAsync()
        {
            // 自分でセッションを離脱すると自分の切断で OnClientDisconnectCallback が発火する。
            // 先にコールバックを解除しておかないと、相手の退出と誤判定して
            // ShowOpponentLeft() がゲーム終了ボタン行を再表示してしまう。
            UnregisterRematchCallbacks();
            await _gameSessionModel.LeaveCurrentSessionAsync();
            await _sceneTransitioner.Transit(Scenes.Home);
        }

        private void Surrender()
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _surrenderCts?.Cancel();
            if (_isOnline)
            {
                _networkGameService.SendSurrenderNotification();
            }
            OnGameEnd(playerWins: false, isPlayerSurrender: true);
        }

        private async UniTaskVoid WatchForOpponentSurrenderAsync(CancellationToken ct)
        {
            try
            {
                await _networkGameService.WaitForOpponentSurrenderAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _surrenderCts?.Cancel();
            OnGameEnd(playerWins: true, isSurrenderWin: true);
        }
    }
}

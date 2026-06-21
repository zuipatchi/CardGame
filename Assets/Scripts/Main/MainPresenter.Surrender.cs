using System;
using System.Threading;
using Common.SceneManagement;
using Cysharp.Threading.Tasks;
using Main.Game;
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

        // チュートリアル中にオプションから「ホームに戻る」を選んだとき。
        // 勝敗のない練習なので投了通知などは出さず、そのままホームへ戻る。
        private void GoHomeFromTutorial()
        {
            _sceneTransitioner.Transit(Scenes.Home).Forget();
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

        // オンライン：相手が HandCollectionWin（太郎勝利）の条件を満たして勝利したことの通知を待ち、
        // 受信したら自分の敗北として終了する。相手の手札は同期されないため、発動側の判定結果を一方的に受け取る。
        private async UniTaskVoid WatchForOpponentSpecialWinAsync(CancellationToken ct)
        {
            try
            {
                await _networkGameService.WaitForOpponentSpecialWinAsync(ct);
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
            OnGameEnd(playerWins: false, winReason: WinReason.HandCollection);
        }
    }
}

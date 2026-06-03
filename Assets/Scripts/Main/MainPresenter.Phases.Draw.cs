using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ドローフェーズ（両プレイヤーが毎ターン1枚ドロー）────────────────

        private async UniTask RunDrawPhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Draw);

            // アナウンス前にハンドラを登録してメッセージのロストを防ぐ
            UniTask drawReceiveTask = _isOnline
                ? _networkGameService.WaitForOpponentDrawAsync(ct)
                : UniTask.CompletedTask;

            await PlayAnnouncementAsync("ドローフェーズ", "turn-announcement-label--draw", ct);

            bool playerDeckEmpty = _playerDeckView.Count == 0;
            bool opponentDeckEmpty = _opponentDeckView.Count == 0;
            if (playerDeckEmpty || opponentDeckEmpty)
            {
                _isGameOver = true;
                if (playerDeckEmpty && opponentDeckEmpty)
                {
                    OnGameEnd(null);
                }
                else
                {
                    OnGameEnd(!playerDeckEmpty);
                }
                return;
            }

            Rect playerDeckRect = _playerDeckView.worldBound;
            Rect opponentDeckRect = _opponentDeckView.worldBound;
            CardData playerDrawn = _playerDeckView.DrawTop();
            CardData opponentDrawn = _opponentDeckView.DrawTop();
            _playerDeckView.RefreshCount();
            _opponentDeckView.RefreshCount();

            if (_isOnline)
            {
                await OnlineDrawAsync(playerDrawn, playerDeckRect, opponentDrawn, opponentDeckRect, drawReceiveTask, ct);
            }
            else
            {
                await LocalDrawAsync(playerDrawn, playerDeckRect, opponentDrawn, opponentDeckRect, ct);
            }
        }

        private async UniTask OnlineDrawAsync(
            CardData playerDrawn, Rect playerDeckRect, CardData opponentDrawn, Rect opponentDeckRect,
            UniTask receiveTask, CancellationToken ct)
        {
            if (playerDrawn != null)
            {
                await _handView.AddCardAnimatedAsync(playerDrawn, playerDeckRect, 0f, ct);
            }
            _networkGameService.SendDrawNotification();
            await ShowWaitingOverlayDuringAsync(receiveTask);
            if (opponentDrawn != null)
            {
                await PlayCpuDrawAsync(opponentDrawn, opponentDeckRect, ct);
            }
        }

        private async UniTask LocalDrawAsync(
            CardData playerDrawn, Rect playerDeckRect, CardData opponentDrawn, Rect opponentDeckRect, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            if (playerDrawn != null)
            {
                tasks.Add(_handView.AddCardAnimatedAsync(playerDrawn, playerDeckRect, 0f, ct));
            }
            if (opponentDrawn != null)
            {
                tasks.Add(PlayCpuDrawAsync(opponentDrawn, opponentDeckRect, ct));
            }
            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }
    }
}

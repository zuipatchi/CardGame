using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ドローフェーズ（アクティブプレイヤーのみ1枚ドロー）──────────────────

        private async UniTask RunDrawPhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Draw);

            bool isLocalTurn = _gameModel.IsLocalTurn;

            await PlayAnnouncementAsync("ドローフェーズ", "turn-announcement-label--draw", ct);

            DeckView activeDeck = isLocalTurn ? _playerDeckView : _opponentDeckView;
            if (activeDeck.Count == 0)
            {
                _isGameOver = true;
                // アクティブプレイヤーのデッキが切れたら、そのプレイヤーの負け
                OnGameEnd(isLocalTurn ? (bool?)false : true);
                return;
            }

            if (isLocalTurn)
            {
                CardData drawn = _playerDeckView.DrawTop();
                _playerDeckView.RefreshCount();
                if (drawn != null)
                {
                    Rect deckRect = _playerDeckView.worldBound;
                    await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                }
                if (_isOnline)
                {
                    _networkGameService.SendDrawNotification();
                }
            }
            else
            {
                // 相手ターン: ドロー通知を待ってからアニメーション
                UniTask drawReceiveTask;
                if (_isOnline && _hasPreDrawTask)
                {
                    drawReceiveTask = _preDrawReceiveTask;
                    _hasPreDrawTask = false;
                }
                else if (_isOnline)
                {
                    drawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                }
                else
                {
                    drawReceiveTask = UniTask.CompletedTask;
                }

                // メインアクション受信ハンドラを事前登録（ドロー通知受信後に相手がすぐ行動しても取りこぼさない）
                if (_isOnline && !_hasPreMainActionTask)
                {
                    _preMainActionReceiveTask = _networkGameService.WaitForOpponentMainActionAsync(ct);
                    _hasPreMainActionTask = true;
                }

                await drawReceiveTask.AttachExternalCancellation(ct);

                CardData drawn = _opponentDeckView.DrawTop();
                _opponentDeckView.RefreshCount();
                if (drawn != null)
                {
                    Rect deckRect = _opponentDeckView.worldBound;
                    await PlayCpuDrawAsync(drawn, deckRect, ct);
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }
    }
}

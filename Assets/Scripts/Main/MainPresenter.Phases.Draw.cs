using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── ドローフェーズ（アクティブプレイヤーのみドロー）──────────────────

        // 1ドローフェーズで引く枚数。ただし先攻の初手（ターン1）はドローなし。
        private const int DrawPhaseCardCount = 3;

        private async UniTask RunDrawPhaseAsync(CancellationToken ct)
        {
            bool isLocalTurn = _gameModel.IsLocalTurn;

            // ターン1は必ず先攻の初手。先攻の初手のみドローなし。
            int drawCount = _gameModel.TurnNumber == 1 ? 0 : DrawPhaseCardCount;

            if (isLocalTurn)
            {
                for (int i = 0; i < drawCount; i++)
                {
                    CardData drawn = _playerDeckView.DrawTop();
                    _playerDeckView.RefreshCount();
                    if (drawn != null)
                    {
                        Rect deckRect = _playerDeckView.worldBound;
                        await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                    }
                }
                // ドロー0枚でも同期のため通知は送る（両者の lockstep を崩さない）。
                if (_isOnline)
                {
                    _networkGameService.SendDrawNotification();
                }
                if (CheckBlueWin(isLocalDeck: true))
                {
                    return;
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

                for (int i = 0; i < drawCount; i++)
                {
                    CardData drawn = _opponentDeckView.DrawTop();
                    _opponentDeckView.RefreshCount();
                    if (drawn != null)
                    {
                        Rect deckRect = _opponentDeckView.worldBound;
                        await PlayCpuDrawAsync(drawn, deckRect, ct);
                    }
                }
                if (CheckBlueWin(isLocalDeck: false))
                {
                    return;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }
    }
}

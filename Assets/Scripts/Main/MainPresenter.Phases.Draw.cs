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

        // 1ドローフェーズで引く枚数。ただし各プレイヤーの初手はドローなし。
        private const int DrawPhaseCardCount = 3;

        private async UniTask RunDrawPhaseAsync(CancellationToken ct)
        {
            bool isLocalTurn = _gameModel.IsLocalTurn;

            // 各プレイヤーの初手（このゲームで最初の自分のドローフェーズ）はドローなし＝先攻有利の補正。
            // 先攻・後攻の双方が対象。ExtraTurn で通算ターン番号がずれても、各自の初手のみ正しく0枚になる。
            bool isFirstTurnForActive = isLocalTurn ? !_playerHadFirstTurn : !_opponentHadFirstTurn;
            if (isLocalTurn)
            {
                _playerHadFirstTurn = true;
            }
            else
            {
                _opponentHadFirstTurn = true;
            }
            int drawCount = isFirstTurnForActive ? 0 : DrawPhaseCardCount;

            // DrawSkipNext 効果でアクティブプレイヤーの次ドローがスキップ予約されていれば、ドロー0枚にして消費する。
            // 両クライアントとも自分側のフラグ（アクティブ側）を見るため、drawCount は対称に 0 になる。
            bool skipDraw = isLocalTurn ? _playerSkipNextDraw : _opponentSkipNextDraw;
            if (skipDraw)
            {
                drawCount = 0;
                if (isLocalTurn)
                {
                    _playerSkipNextDraw = false;
                }
                else
                {
                    _opponentSkipNextDraw = false;
                }
                ShowToast("ドロースキップ");
            }

            // DrawNextTurnStart 効果でアクティブプレイヤーに予約ドローがあれば、通常ドローに上乗せして消費する。
            // 両クライアントとも自分側（アクティブ側）の予約を見るため、drawCount は対称に決まる。
            int pendingDraw = isLocalTurn ? _playerPendingNextDraw : _opponentPendingNextDraw;
            if (pendingDraw > 0)
            {
                drawCount += pendingDraw;
                if (isLocalTurn)
                {
                    _playerPendingNextDraw = 0;
                }
                else
                {
                    _opponentPendingNextDraw = 0;
                }
            }

            if (isLocalTurn)
            {
                for (int i = 0; i < drawCount; i++)
                {
                    CardData drawn = _playerDeckView.DrawTop();
                    _playerDeckView.RefreshCount();
                    if (drawn != null)
                    {
                        Rect deckRect = _playerDeckView.worldBound;
                        if (_handView.IsFull)
                        {
                            ShowToast("手札が上限 → 墓地へ");
                            await BurnDrawnCardAsync(drawn, deckRect, _playerGraveyardView, isOpponent: false, ct);
                        }
                        else
                        {
                            await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                        }
                    }
                }
                // ドロー0枚でも同期のため通知は送る（両者の lockstep を崩さない）。
                if (_isOnline)
                {
                    _networkGameService.SendDrawNotification();
                }
                if (CheckDeckOutWin(isLocalDeck: true))
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
                        if (_opponentHandView.IsFull)
                        {
                            await BurnDrawnCardAsync(drawn, deckRect, _opponentGraveyardView, isOpponent: true, ct);
                        }
                        else
                        {
                            await PlayCpuDrawAsync(drawn, deckRect, ct);
                        }
                    }
                }
                if (CheckDeckOutWin(isLocalDeck: false))
                {
                    return;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }
    }
}

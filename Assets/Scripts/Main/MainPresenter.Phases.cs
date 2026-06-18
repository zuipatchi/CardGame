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
        // ─── ゲームループ ───────────────────────────────────────────────

        private async UniTaskVoid RunGameAsync(CancellationToken ct)
        {
            try
            {
                while (!_isGameOver)
                {
                    await RunTurnAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError($"RunGameAsync 例外: {e}");
            }
        }

        private async UniTask RunTurnAsync(CancellationToken ct)
        {
            _turnCounter.SetTurn(_gameModel.TurnNumber);

            await PlayTurnStartAnnouncementAsync(_gameModel.IsLocalTurn, ct);

            // ターン開始時効果（ドロー前）：場のキャラ（OnTurnStart）と墓地の永続イベント（OnTurnStart）を発動
            await ResolveTurnStartEffectsAsync(_gameModel.IsLocalTurn, ct);
            if (_isGameOver)
            {
                return;
            }

            await RunDrawPhaseAsync(ct);
            if (_isGameOver)
            {
                return;
            }

            _gameModel.BeginMain();
            await RunMainPhaseAsync(ct);
            if (_isGameOver)
            {
                return;
            }

            // ターン終了時：守護/防人を自動でタップ（横向き）にする（毎ターン終了時）
            await AutoTapGuardiansAndSakimoriAsync(_gameModel.IsLocalTurn, ct);

            // ExtraTurn 効果が発動していれば相手へターンを渡さず同じプレイヤーがもう一度ターンを行う。
            if (_extraTurnPending)
            {
                _extraTurnPending = false;
                _gameModel.RepeatTurn();
            }
            else
            {
                _gameModel.EndTurn();
            }
        }

        // ゲーム開始時の先攻後攻をコイントス演出で提示する（決定済みの isLocalFirst を受け取る）。
        // 配牌前に呼ぶ（手札枚数が先攻3枚・後攻5枚で変わるため）。先攻・後攻の双方の初手はドローなしで補正する。
        private async UniTask InitializeFirstTurnAsync(bool isLocalFirst, CancellationToken ct)
        {
            _gameModel.SetInitialTurn(isLocalFirst);

            string resultText = isLocalFirst ? "先攻！" : "後攻！";
            string resultClass = isLocalFirst ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayCoinTossAsync(isLocalFirst, resultText, resultClass, ct);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
        }

        // winReason: 共通の勝利条件で決着した場合の勝因（デッキ切れ / 勝利点 / キャラ8体）。
        // 降参・タイムアウトなど勝因を区別しない決着では null。
        private void OnGameEnd(bool? playerWins, bool isSurrenderWin = false, bool isPlayerSurrender = false, WinReason? winReason = null)
        {
            _optionPresenter.ClearSurrenderHandler();
            _gameSessionModel.ShouldRainOnNextHome = playerWins == false;
            if (_isOnline)
            {
                StartRematchWatch();
            }
            PlayGameEndAsync(playerWins, isSurrenderWin, isPlayerSurrender, winReason, destroyCancellationToken).Forget();
        }
    }
}

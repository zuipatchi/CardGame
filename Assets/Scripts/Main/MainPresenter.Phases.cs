using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
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

            _gameModel.EndTurn();
        }

        // ゲーム開始時に先攻後攻を決定し、後攻プレイヤーに補正ドローを与える
        private async UniTask InitializeFirstTurnAsync(CancellationToken ct)
        {
            bool isLocalFirst = _isOnline ? _onlineIsLocalFirst : UnityEngine.Random.value > 0.5f;
            _gameModel.SetInitialTurn(isLocalFirst);

            string resultText = isLocalFirst ? "先攻！" : "後攻！";
            string resultClass = isLocalFirst ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayCoinTossAsync(isLocalFirst, resultText, resultClass, ct);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // 後攻プレイヤーが1枚余分にドロー
            if (!isLocalFirst)
            {
                CardData drawn = _playerDeckView.DrawTop();
                if (drawn != null)
                {
                    _playerDeckView.RefreshCount();
                    Rect deckRect = _playerDeckView.worldBound;
                    await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                }
            }
            else
            {
                CardData drawn = _opponentDeckView.DrawTop();
                if (drawn != null)
                {
                    _opponentDeckView.RefreshCount();
                    Rect deckRect = _opponentDeckView.worldBound;
                    await PlayCpuDrawAsync(drawn, deckRect, ct);
                }
            }
        }

        // winAttribute: 色による勝利条件で決着した場合の勝因属性（赤=ハート / 青=デッキ0）。
        // 降参・タイムアウトなど色に依らない決着では null。
        private void OnGameEnd(bool? playerWins, bool isSurrenderWin = false, bool isPlayerSurrender = false, CardAttribute? winAttribute = null)
        {
            _optionPresenter.ClearSurrenderHandler();
            _gameSessionModel.ShouldRainOnNextHome = playerWins == false;
            PlayGameEndAsync(playerWins, isSurrenderWin, isPlayerSurrender, winAttribute, destroyCancellationToken).Forget();
        }
    }
}

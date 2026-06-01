using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

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
            if (_isGameOver) return;

            await RunCharacterSetPhaseAsync(ct);
            if (_isGameOver) return;

            _gameModel.BeginPreBattle1();
            await RunPreBattle1PhaseAsync(ct);
            if (_isGameOver) return;

            bool priorityUsed;
            bool isLocalFirst = DetermineFirstMover(out priorityUsed);
            _gameModel.SetInitialTurn(isLocalFirst);

            _gameModel.BeginPreBattle2();
            await RunPreBattle2PhaseAsync(isLocalFirst, ct);
            if (_isGameOver) return;

            _gameModel.BeginBattle();
            await RunBattlePhaseAsync(priorityUsed, ct);
            if (_isGameOver) return;

            _gameModel.EndTurn();
        }

        // Speed 比較で先攻後攻を決定する。同値の場合は攻撃優先権保持者が先攻
        private bool DetermineFirstMover(out bool priorityUsed)
        {
            priorityUsed = false;
            int localSpeed = _playerCharacterSlot.Speed;
            int opponentSpeed = _opponentCharacterSlot.Speed;
            if (localSpeed != opponentSpeed)
            {
                return localSpeed > opponentSpeed;
            }

            // 素早さ同値：優先権を行使（実際の移譲は戦闘フェーズ演出後に行う）
            priorityUsed = true;
            return _localHasPriority;
        }

        private void UpdatePriorityCoinUI()
        {
            _playerPriorityCoin.style.display = _localHasPriority ? DisplayStyle.Flex : DisplayStyle.None;
            _opponentPriorityCoin.style.display = _localHasPriority ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private async UniTask InitializePriorityAsync(CancellationToken ct)
        {
            _localHasPriority = _isOnline ? _onlineIsLocalFirst : UnityEngine.Random.value > 0.5f;

            string resultText = _localHasPriority ? "優先権 獲得！" : "優先権 なし";
            string resultClass = _localHasPriority ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayCoinTossAsync(_localHasPriority, resultText, resultClass, ct);

            UpdatePriorityCoinUI();

            // 優先権を得られなかったプレイヤーが1枚ドロー
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
            if (!_localHasPriority)
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

        private void OnGameEnd(bool? playerWins, bool isSurrenderWin = false)
        {
            PlayGameEndAsync(playerWins, isSurrenderWin, destroyCancellationToken).Forget();
        }
    }
}

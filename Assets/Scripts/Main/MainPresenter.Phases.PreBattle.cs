using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── 戦闘前2フェーズ（Event のみ・交互・2連続パス）──────────────────

        private async UniTask RunPreBattle2PhaseAsync(bool isLocalFirst, CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle2);

            // 後攻側（else ブランチから始まる側）は、解決・戦闘フェーズのローカルアニメーション差により
            // 先攻側が先に PreBattle2 へ到達してアナウンス中にメッセージを送ってくる場合がある。
            // NGS_Draw と同じパターンで、アナウンス前にハンドラを事前登録してロストを防ぐ。
            if (_isOnline && !_gameModel.IsLocalPreparationTurn)
            {
                _prePreBattle2ReceiveTask = _networkGameService.WaitForOpponentPreBattle2Async(ct);
                _hasPrePreBattle2Task = true;
            }

            await PlayAnnouncementAsync("イベントフェーズ", "turn-announcement-label--event", ct);
            string firstMoverText = isLocalFirst ? "あなたが先です" : "相手が先です";
            string firstMoverClass = isLocalFirst ? "turn-announcement-label--player" : "turn-announcement-label--enemy";
            await PlayAnnouncementAsync(firstMoverText, firstMoverClass, ct);

            while (true)
            {
                if (_gameModel.IsLocalPreparationTurn)
                {
                    CardView readied = await WaitForPlayerPreBattle2InputAsync(ct);
                    if (_isOnline)
                    {
                        _networkGameService.SendPreBattle2Action(readied?.Data.Id);
                        // 送信直後にハンドラを事前登録してアニメーション中の受信ロストを防ぐ
                        _prePreBattle2ReceiveTask = _networkGameService.WaitForOpponentPreBattle2Async(ct);
                        _hasPrePreBattle2Task = true;
                    }
                    if (readied == null)
                    {
                        await PlayPassAnimationAsync(true, ct);
                        if (_gameModel.Pass())
                        {
                            break;
                        }
                    }
                    else
                    {
                        _gameModel.ReadyCard(readied);
                        await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
                        await PayCostAsync(readied, _playerDeckView, _playerGraveyardView, ct);
                        if (_isGameOver) break;
                        readied.SetChainNumber(_gameModel.ReadyQueue.Count);
                    }
                }
                else
                {
                    if (_isOnline)
                    {
                        string cardId = _hasPrePreBattle2Task
                            ? await _prePreBattle2ReceiveTask.AttachExternalCancellation(ct)
                            : await _networkGameService.WaitForOpponentPreBattle2Async(ct);
                        _hasPrePreBattle2Task = false;
                        if (string.IsNullOrEmpty(cardId))
                        {
                            await PlayPassAnimationAsync(false, ct);
                            if (_gameModel.Pass())
                            {
                                break;
                            }
                        }
                        else
                        {
                            await PlayOpponentPreBattle2OnlineAsync(cardId, ct);
                            if (_isGameOver) break;
                        }
                    }
                    else
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                        IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                        int idx = CpuAgent.ChooseEventCardIndex(cpuHand.Select(c => c.Data).ToList());

                        if (idx >= 0)
                        {
                            CardView card = cpuHand[idx];
                            Rect fromRect = card.worldBound;
                            _opponentHandView.RemoveCard(card);
                            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
                            _opponentFieldView.PlaceCard(card);
                            await card.FlipAsync(ct);
                            _gameModel.ReadyCard(card);
                            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
                            await PayCostAsync(card, _opponentDeckView, _opponentGraveyardView, ct);
                            if (_isGameOver) break;
                            card.SetChainNumber(_gameModel.ReadyQueue.Count);
                        }
                        else
                        {
                            await PlayPassAnimationAsync(false, ct);
                            if (_gameModel.Pass())
                            {
                                break;
                            }
                        }
                    }
                }
            }

            HideActionButtons();

            // PreBattle2 ループ終了 = 最後のネットワーク同期点。
            // 以降の解決フェーズ・戦闘フェーズはローカルアニメーションのみで数秒かかる場合があり、
            // 先に終わった側が次ターンの DrawPhase でハンドラ未登録のまま NGS_Draw を受信して
            // メッセージが捨てられるのを防ぐため、ここで事前登録しておく。
            if (_isOnline)
            {
                _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                _hasPreDrawTask = true;
            }

            await RunResolutionPhaseAsync(ct);
        }

        private async UniTask PlayOpponentPreBattle2OnlineAsync(string cardId, CancellationToken ct)
        {
            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                return;
            }
            IReadOnlyList<CardView> hand = _opponentHandView.Cards;
            Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
            if (hand.Count > 0)
            {
                _opponentHandView.RemoveCard(hand[0]);
            }
            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, isOpponent: true);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
            await card.FlipAsync(ct);
            _gameModel.ReadyCard(card);
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
            await PayCostAsync(card, _opponentDeckView, _opponentGraveyardView, ct);
            card.SetChainNumber(_gameModel.ReadyQueue.Count);
        }

        private async UniTask<CardView> WaitForPlayerPreBattle2InputAsync(CancellationToken ct)
        {
            _prepInput._tcs = new UniTaskCompletionSource<CardView>();
            _prepInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            if (_optionModel.AutoPass.CurrentValue && !HasPlayableCards())
            {
                _prepInput._tcs.TrySetResult(null);
            }

            try
            {
                return await _prepInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _prepInput._tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }
        }
    }
}

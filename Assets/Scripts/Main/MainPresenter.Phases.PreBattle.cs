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
        // ─── 戦闘前1フェーズ（Skill のみ裏向き1枚）────────────────────────

        private async UniTask RunPreBattle1PhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle1);
            await PlayAnnouncementAsync("準備フェーズ", "turn-announcement-label--skill", ct);

            if (_isOnline)
            {
                await OnlinePreBattle1Async(ct);
            }
            else
            {
                await UniTask.WhenAll(
                    PlayerPreBattle1LocalAsync(ct),
                    CpuPreBattle1Async(ct)
                );
            }
        }

        private async UniTask PlayerPreBattle1LocalAsync(CancellationToken ct)
        {
            CardView placed = await WaitForPlayerPreBattle1TurnAsync(ct);
            if (placed == null)
            {
                await PlayPassAnimationAsync(true, ct);
            }
        }

        // オンライン：CharSet と同様の対称プロトコル。
        private async UniTask OnlinePreBattle1Async(CancellationToken ct)
        {
            UniTask receiveTask = ReceiveAndPlaceOpponentPreBattle1Async(ct);

            CardView placed = await WaitForPlayerPreBattle1TurnAsync(ct);
            if (placed == null)
            {
                await PlayPassAnimationAsync(true, ct);
                _networkGameService.SendPreBattle1Action(null);
            }
            else
            {
                _networkGameService.SendPreBattle1Action(placed.Data.Id);
            }

            await ShowWaitingOverlayDuringAsync(receiveTask);
        }

        private async UniTask ReceiveAndPlaceOpponentPreBattle1Async(CancellationToken ct)
        {
            string cardId = await _networkGameService.WaitForOpponentPreBattle1Async(ct);
            await PlayOpponentPreBattle1OnlineAsync(cardId, ct);
        }

        private async UniTask<CardView> WaitForPlayerPreBattle1TurnAsync(CancellationToken ct)
        {
            _isLocalPreBattleActive = true;
            _preBattleInput._tcs = new UniTaskCompletionSource<CardView>();
            _preBattleInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            CardView result = null;
            try
            {
                result = await _preBattleInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _isLocalPreBattleActive = false;
                _preBattleInput._tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }

            return result;
        }

        private async UniTask CpuPreBattle1Async(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChoosePreBattle1CardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx < 0)
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }

            CardView card = cpuHand[idx];
            Rect fromRect = card.worldBound;
            _opponentHandView.RemoveCard(card);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
        }

        private async UniTask PlayOpponentPreBattle1OnlineAsync(string cardId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }
            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                await PlayPassAnimationAsync(false, ct);
                return;
            }
            IReadOnlyList<CardView> hand = _opponentHandView.Cards;
            Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
            if (hand.Count > 0)
            {
                _opponentHandView.RemoveCard(hand[0]);
            }
            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
            await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(card);
        }

        // ─── 戦闘前2フェーズ（Event のみ・交互・2連続パス）──────────────────

        private async UniTask RunPreBattle2PhaseAsync(bool isLocalFirst, CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle2);
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
                        string cardId = await ShowWaitingOverlayDuringAsync(_networkGameService.WaitForOpponentPreBattle2Async(ct));
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
            CardView card = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: true, _cardStore.AttributeDatabase, isOpponent: true);
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

            try
            {
                return await _prepInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _prepInput._tcs = null;
                RefreshHandHighlights();
            }
        }
    }
}

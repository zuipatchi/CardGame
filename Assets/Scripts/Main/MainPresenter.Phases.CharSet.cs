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
        // ─── キャラセットフェーズ（両スロット埋まり時はスキップ・空スロットのみ配置可） ──

        private async UniTask RunCharacterSetPhaseAsync(CancellationToken ct)
        {
            bool playerHadChar = _playerCharacterSlot.CurrentCard != null;
            bool opponentHadChar = _opponentCharacterSlot.CurrentCard != null;

            if (playerHadChar && opponentHadChar)
            {
                return;
            }

            _gameModel.BeginCharacterSet();
            UpdatePhaseIndicator(TurnPhase.CharacterSet);
            await PlayAnnouncementAsync("キャラセットフェーズ", "turn-announcement-label--character", ct);

            if (_isOnline)
            {
                await OnlineCharSetAsync(ct);
            }
            else
            {
                await UniTask.WhenAll(
                    PlayerCharSetLocalAsync(playerHadChar, ct),
                    CpuCharSetLocalAsync(opponentHadChar, ct)
                );
            }

            await PlayResolveAnimationAsync(ct);

            if (!playerHadChar && _playerCharacterSlot.CurrentCard != null)
            {
                await _playerCharacterSlot.CurrentCard.FlipAsync(ct);
                await PayCostAsync(_playerCharacterSlot.CurrentCard, _playerDeckView, _playerGraveyardView, ct);
                if (_isGameOver) return;
            }
            if (!opponentHadChar && _opponentCharacterSlot.CurrentCard != null)
            {
                await _opponentCharacterSlot.CurrentCard.FlipAsync(ct);
                await PayCostAsync(_opponentCharacterSlot.CurrentCard, _opponentDeckView, _opponentGraveyardView, ct);
            }
        }

        private async UniTask PlayerCharSetLocalAsync(bool forcedPass, CancellationToken ct)
        {
            if (forcedPass)
            {
                return;
            }
            CardView placed = await WaitForPlayerCharSetInputAsync(ct);
            if (placed == null)
            {
                await PlayPassAnimationAsync(true, ct);
            }
        }

        private async UniTask CpuCharSetLocalAsync(bool forcedPass, CancellationToken ct)
        {
            if (forcedPass)
            {
                return;
            }
            await CpuCharSetAsync(ct);
        }

        private async UniTask OnlineCharSetAsync(CancellationToken ct)
        {
            UniTask receiveTask = ReceiveAndPlaceOpponentCharSetAsync(ct);

            if (_playerCharacterSlot.CurrentCard != null)
            {
                _networkGameService.SendCharSetAction(null);
            }
            else
            {
                CardView placed = await WaitForPlayerCharSetInputAsync(ct);
                if (placed == null)
                {
                    await PlayPassAnimationAsync(true, ct);
                    _networkGameService.SendCharSetAction(null);
                }
                else
                {
                    _networkGameService.SendCharSetAction(placed.Data.Id);
                }
            }

            await receiveTask;
        }

        private async UniTask CpuCharSetAsync(CancellationToken ct)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx >= 0)
            {
                CardView card = cpuHand[idx];
                Rect fromRect = card.worldBound;
                _opponentHandView.RemoveCard(card);
                await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
                _opponentCharacterSlot.PlaceCard(card);
            }
            else
            {
                await PlayPassAnimationAsync(false, ct);
            }
        }

        private async UniTask ReceiveAndPlaceOpponentCharSetAsync(CancellationToken ct)
        {
            string cardId = await _networkGameService.WaitForOpponentCharSetAsync(ct);
            await PlayOpponentCharSetOnlineAsync(cardId, ct);
        }

        private async UniTask<CardView> WaitForPlayerCharSetInputAsync(CancellationToken ct)
        {
            _charSetInput._tcs = new UniTaskCompletionSource<CardView>();
            _charSetInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _charSetInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _charSetInput._tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }
        }

        private async UniTask PlayOpponentCharSetOnlineAsync(string cardId, CancellationToken ct)
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
            await FlyCardToDestAsync(card, fromRect, _opponentCharacterSlot, ct);
            _opponentCharacterSlot.PlaceCard(card);
        }
    }
}

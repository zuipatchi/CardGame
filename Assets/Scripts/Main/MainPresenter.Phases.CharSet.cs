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
        // ─── キャラセットフェーズ（1ターンに1枚まで、フィールドに直接出す） ──

        private async UniTask RunCharacterSetPhaseAsync(CancellationToken ct)
        {
            _gameModel.BeginCharacterSet();
            UpdatePhaseIndicator(TurnPhase.CharacterSet);

            // アナウンス前にハンドラを登録してメッセージのロストを防ぐ。
            UniTask<CardView> opponentReceiveTask = _isOnline
                ? ReceiveAndPlaceOpponentCharSetAsync(ct)
                : UniTask.FromResult<CardView>(null);

            await PlayAnnouncementAsync("キャラセットフェーズ", "turn-announcement-label--character", ct);

            CardView playerPlaced;
            CardView opponentPlaced;

            if (_isOnline)
            {
                playerPlaced = await OnlinePlayerCharSetAsync(ct);
                opponentPlaced = await opponentReceiveTask;
            }
            else
            {
                (playerPlaced, opponentPlaced) = await UniTask.WhenAll(
                    PlayerCharSetLocalAsync(ct),
                    CpuCharSetAsync(ct)
                );
            }

            await PlayResolveAnimationAsync(ct);

            // コスト払い中に発動するグレイブトリガーが相手キャラを参照できるよう
            // 両方のキャラを先にフリップしてからコスト払いを行う。
            // 優先権保持プレイヤーを先にオープンする。
            CardView firstPlaced = _localHasPriority ? playerPlaced : opponentPlaced;
            CardView secondPlaced = _localHasPriority ? opponentPlaced : playerPlaced;
            DeckView firstDeck = _localHasPriority ? _playerDeckView : _opponentDeckView;
            GraveyardView firstGrave = _localHasPriority ? _playerGraveyardView : _opponentGraveyardView;
            DeckView secondDeck = _localHasPriority ? _opponentDeckView : _playerDeckView;
            GraveyardView secondGrave = _localHasPriority ? _opponentGraveyardView : _playerGraveyardView;

            if (firstPlaced != null && firstPlaced.IsFaceDown)
            {
                await firstPlaced.FlipAsync(ct);
            }
            if (secondPlaced != null && secondPlaced.IsFaceDown)
            {
                await secondPlaced.FlipAsync(ct);
            }

            if (firstPlaced != null)
            {
                await PayCostAsync(firstPlaced, firstDeck, firstGrave, ct);
                if (_isGameOver)
                {
                    return;
                }
            }
            if (secondPlaced != null)
            {
                await PayCostAsync(secondPlaced, secondDeck, secondGrave, ct);
            }
        }

        private async UniTask<CardView> PlayerCharSetLocalAsync(CancellationToken ct)
        {
            if (!_gameModel.CanPlayerSetChar)
            {
                return null;
            }
            CardView placed = await WaitForPlayerCharSetInputAsync(ct);
            if (placed == null)
            {
                await PlayPassAnimationAsync(true, ct);
            }
            else
            {
                _gameModel.RecordPlayerCharSet();
            }
            return placed;
        }

        private async UniTask<CardView> CpuCharSetAsync(CancellationToken ct)
        {
            if (!_gameModel.CanOpponentSetChar)
            {
                return null;
            }
            await UniTask.Delay(System.TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHand.Select(c => c.Data).ToList());

            if (idx >= 0)
            {
                CardView card = cpuHand[idx];
                Rect fromRect = card.worldBound;
                _opponentHandView.RemoveCard(card);
                await FlyCardToDestAsync(card, fromRect, _opponentFieldView, ct);
                _opponentFieldView.PlaceCard(card);
                _gameModel.RecordOpponentCharSet();
                return card;
            }
            else
            {
                await PlayPassAnimationAsync(false, ct);
                return null;
            }
        }

        private async UniTask<CardView> OnlinePlayerCharSetAsync(CancellationToken ct)
        {
            if (!_gameModel.CanPlayerSetChar)
            {
                _networkGameService.SendCharSetAction(null);
                return null;
            }
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
            return placed;
        }

        private async UniTask<CardView> ReceiveAndPlaceOpponentCharSetAsync(CancellationToken ct)
        {
            string cardId = await _networkGameService.WaitForOpponentCharSetAsync(ct);
            return await PlayOpponentCharSetOnlineAsync(cardId, ct);
        }

        private async UniTask<CardView> PlayOpponentCharSetOnlineAsync(string cardId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                await PlayPassAnimationAsync(false, ct);
                return null;
            }

            if (!_cardDatabase.TryGet(cardId, out CardData cardData))
            {
                await PlayPassAnimationAsync(false, ct);
                return null;
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
            _gameModel.RecordOpponentCharSet();
            return card;
        }

        private async UniTask<CardView> WaitForPlayerCharSetInputAsync(CancellationToken ct)
        {
            _charSetInput._tcs = new UniTaskCompletionSource<CardView>();
            _charSetInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            if (_optionModel.AutoPass.CurrentValue && !HasPlayableCards())
            {
                _charSetInput._tcs.TrySetResult(null);
            }

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
    }
}

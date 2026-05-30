using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using CardEventType = Main.Card.EventType;
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

            bool isLocalFirst = DetermineFirstMover();
            _gameModel.SetInitialTurn(isLocalFirst);
            await PlayTurnAnnouncementAsync(isLocalFirst, ct);

            _gameModel.BeginPreBattle2();
            await RunPreBattle2PhaseAsync(ct);
            if (_isGameOver) return;

            _gameModel.BeginBattle();
            await RunBattlePhaseAsync(ct);
            if (_isGameOver) return;

            _gameModel.EndTurn();
        }

        // Speed 比較で先攻後攻を決定する。同値の場合は初回ランダム・以降交互
        private bool DetermineFirstMover()
        {
            int localSpeed = _playerCharacterSlot.Speed;
            int opponentSpeed = _opponentCharacterSlot.Speed;
            if (localSpeed != opponentSpeed)
            {
                return localSpeed > opponentSpeed;
            }

            if (_lastSpeedTieBreakerWasLocal == null)
            {
                bool first = _isOnline ? _onlineIsLocalFirst : UnityEngine.Random.value > 0.5f;
                _lastSpeedTieBreakerWasLocal = first;
                return first;
            }

            bool alternate = !_lastSpeedTieBreakerWasLocal.Value;
            _lastSpeedTieBreakerWasLocal = alternate;
            return alternate;
        }


        private async UniTask CpuCharSetAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
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

        // ─── ドローフェーズ（両プレイヤーが毎ターン1枚ドロー）────────────────

        private async UniTask RunDrawPhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Draw);

            bool playerDeckEmpty = _playerDeckView.Count == 0;
            bool opponentDeckEmpty = _opponentDeckView.Count == 0;
            if (playerDeckEmpty || opponentDeckEmpty)
            {
                _isGameOver = true;
                if (playerDeckEmpty && opponentDeckEmpty)
                {
                    OnGameEnd(null);
                }
                else
                {
                    OnGameEnd(!playerDeckEmpty);
                }
                return;
            }

            Rect playerDeckRect = _playerDeckView.worldBound;
            Rect opponentDeckRect = _opponentDeckView.worldBound;
            CardData playerDrawn = _playerDeckView.DrawTop();
            CardData opponentDrawn = _opponentDeckView.DrawTop();
            _playerDeckView.RefreshCount();
            _opponentDeckView.RefreshCount();

            if (_isOnline)
            {
                await OnlineDrawAsync(playerDrawn, playerDeckRect, opponentDrawn, opponentDeckRect, ct);
            }
            else
            {
                await LocalDrawAsync(playerDrawn, playerDeckRect, opponentDrawn, opponentDeckRect, ct);
            }
        }

        private async UniTask OnlineDrawAsync(
            CardData playerDrawn, Rect playerDeckRect, CardData opponentDrawn, Rect opponentDeckRect, CancellationToken ct)
        {
            UniTask receiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
            if (playerDrawn != null)
            {
                await _handView.AddCardAnimatedAsync(playerDrawn, playerDeckRect, 0f, ct);
            }
            _networkGameService.SendDrawNotification();
            await receiveTask;
            if (opponentDrawn != null)
            {
                await PlayCpuDrawAsync(opponentDrawn, opponentDeckRect, ct);
            }
        }

        private async UniTask LocalDrawAsync(
            CardData playerDrawn, Rect playerDeckRect, CardData opponentDrawn, Rect opponentDeckRect, CancellationToken ct)
        {
            List<UniTask> tasks = new List<UniTask>();
            if (playerDrawn != null)
            {
                tasks.Add(_handView.AddCardAnimatedAsync(playerDrawn, playerDeckRect, 0f, ct));
            }
            if (opponentDrawn != null)
            {
                tasks.Add(PlayCpuDrawAsync(opponentDrawn, opponentDeckRect, ct));
            }
            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

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

            await receiveTask;
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

        private async UniTask RunPreBattle2PhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.PreBattle2);
            await PlayAnnouncementAsync("イベントフェーズ", "turn-announcement-label--event", ct);

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
                        string cardId = await _networkGameService.WaitForOpponentPreBattle2Async(ct);
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

        // ─── 解決フェーズ ────────────────────────────────────────────────

        private async UniTask RunResolutionPhaseAsync(CancellationToken ct)
        {
            IReadOnlyList<CardView> queue = _gameModel.ReadyQueue;
            if (queue.Count == 0)
            {
                return;
            }

            await PlayResolveAnimationAsync(ct);

            bool skipNextEffect = false;

            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CardView card = queue[i];
                card.SetChainNumber(0);
                card.SetState(CardState.Resolve);
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

                bool isLocal = !card.IsOpponent;

                if (card.Data is EventCardData eventData)
                {
                    if (skipNextEffect)
                    {
                        skipNextEffect = false;
                    }
                    else if (eventData.EventType == CardEventType.Negate)
                    {
                        skipNextEffect = true;
                        if (i > 0)
                        {
                            await PlayNegateEffectAsync(queue[i - 1], ct);
                        }
                    }
                    else
                    {
                        if (eventData.EventType == CardEventType.Draw)
                        {
                            await PlayDrawEffectAsync(card, eventData.EventValue, ct);
                        }
                        else if (eventData.EventType == CardEventType.BanishChar)
                        {
                            CharacterSlotView banishTarget = isLocal ? _opponentCharacterSlot : _playerCharacterSlot;
                            await PlayBanishCharEffectAsync(banishTarget, ct);
                        }
                        else if (eventData.EventType == CardEventType.Recover)
                        {
                            await PlayRecoverEffectAsync(card, eventData.EventValue, ct);
                        }
                        else if (eventData.EventType == CardEventType.Switch)
                        {
                            CharacterSlotView switchSlot = isLocal ? _playerCharacterSlot : _opponentCharacterSlot;
                            await PlaySwitchEffectAsync(card, switchSlot, ct);
                        }
                        await ApplyEventEffectAsync(eventData, isLocal, ct);
                        if (eventData.EventType == CardEventType.AtkBoost)
                        {
                            await PlayAtkBoostEffectAsync(card, eventData.EventValue, ct);
                        }
                        else if (eventData.EventType == CardEventType.DefBoost)
                        {
                            await PlayDefBoostEffectAsync(card, eventData.EventValue, ct);
                        }
                    }
                }

                if (_isGameOver)
                {
                    break;
                }

                FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
                field.RemoveCard(card);
                GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                graveyard.AddCard(card);

                card.SetState(CardState.Normal);

                if (_isGameOver)
                {
                    break;
                }
            }
        }

        private async UniTask ApplyEventEffectAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            switch (data.EventType)
            {
                case CardEventType.AtkBoost:
                    if (isLocal)
                    {
                        _playerAtkBoost += data.EventValue;
                    }
                    else
                    {
                        _opponentAtkBoost += data.EventValue;
                    }
                    break;
                case CardEventType.DefBoost:
                    if (isLocal)
                    {
                        _playerDefBoost += data.EventValue;
                    }
                    else
                    {
                        _opponentDefBoost += data.EventValue;
                    }
                    break;
                case CardEventType.Draw:
                    await ApplyDrawEffectAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.BanishChar:
                    {
                        CharacterSlotView targetSlot = isLocal ? _opponentCharacterSlot : _playerCharacterSlot;
                        GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
                        CardView charCard = targetSlot.CurrentCard;
                        if (charCard != null)
                        {
                            Rect fromRect = charCard.worldBound;
                            targetSlot.RemoveCard();
                            await FlyCardToDestAsync(charCard, fromRect, targetGraveyard, ct);
                            targetGraveyard.AddCard(charCard);
                        }
                        break;
                    }
                case CardEventType.Recover:
                    {
                        GraveyardView sourceGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                        DeckView targetDeck = isLocal ? _playerDeckView : _opponentDeckView;
                        List<CardData> recovered = sourceGraveyard.TakeFromTop(data.EventValue);
                        if (recovered.Count > 0)
                        {
                            await PlayRecoverFlyAsync(recovered, sourceGraveyard, targetDeck, ct);
                            targetDeck.AddCardsAndShuffle(recovered);
                            await PlayDeckShufflePulseAsync(targetDeck, ct);
                        }
                        break;
                    }
                case CardEventType.Switch:
                    await ApplySwitchEffectAsync(isLocal, ct);
                    break;
            }
        }

        private async UniTask ApplySwitchEffectAsync(bool isLocal, CancellationToken ct)
        {
            CharacterSlotView ownSlot = isLocal ? _playerCharacterSlot : _opponentCharacterSlot;
            CardView existingChar = ownSlot.CurrentCard;
            if (existingChar == null)
            {
                return;
            }

            Rect charRect = existingChar.worldBound;
            ownSlot.RemoveCard();

            if (isLocal)
            {
                await _handView.AddCardBackAsync(existingChar, charRect, ct);
                CardView newChar = await WaitForPlayerSwitchInputAsync(ct);
                if (newChar != null)
                {
                    await PayCostAsync(newChar, _playerDeckView, _playerGraveyardView, ct);
                }
            }
            else
            {
                await existingChar.FlipAsync(ct);
                await _opponentHandView.AddCardBackAsync(existingChar, charRect, ct);
                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHand.Select(c => c.Data).ToList());
                if (idx >= 0)
                {
                    CardView newChar = cpuHand[idx];
                    Rect fromRect = newChar.worldBound;
                    _opponentHandView.RemoveCard(newChar);
                    await FlyCardToDestAsync(newChar, fromRect, _opponentCharacterSlot, ct);
                    _opponentCharacterSlot.PlaceCard(newChar);
                    await newChar.FlipAsync(ct);
                    await PayCostAsync(newChar, _opponentDeckView, _opponentGraveyardView, ct);
                }
            }
        }

        private async UniTask<CardView> WaitForPlayerSwitchInputAsync(CancellationToken ct)
        {
            _switchInput._tcs = new UniTaskCompletionSource<CardView>();
            _switchInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _switchInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _switchInput._tcs = null;
                HideActionButtons();
                RefreshHandHighlights();
            }
        }

        private async UniTask ApplyDrawEffectAsync(int count, bool isLocal, CancellationToken ct)
        {
            if (count <= 0)
            {
                return;
            }

            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;

            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0)
                {
                    _isGameOver = true;
                    OnGameEnd(isLocal ? (bool?)false : true);
                    break;
                }

                Rect deckRect = deck.worldBound;
                CardData drawn = deck.DrawTop();
                deck.RefreshCount();

                if (drawn == null)
                {
                    break;
                }

                if (isLocal)
                {
                    await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                }
                else
                {
                    await PlayCpuDrawAsync(drawn, deckRect, ct);
                }
            }
        }

        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Battle);
            List<CardView> playerCards = _playerFieldView.Cards.ToList();
            List<CardView> opponentCards = _opponentFieldView.Cards.ToList();

            if (playerCards.Count == 0 && opponentCards.Count == 0
                && _playerCharacterSlot.CurrentCard == null && _opponentCharacterSlot.CurrentCard == null)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            UniTask[] flipTasks = playerCards.Concat(opponentCards).Select(c => c.FlipAsync(ct)).ToArray();
            await UniTask.WhenAll(flipTasks);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // コスト払い（先攻→後攻の順）
            bool isLocalFirst = _gameModel.IsLocalTurn;
            List<CardView> firstCards = isLocalFirst ? playerCards : opponentCards;
            DeckView firstDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView firstGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;
            List<CardView> secondCards = isLocalFirst ? opponentCards : playerCards;
            DeckView secondDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView secondGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            string firstCostClass = isLocalFirst ? "turn-announcement-label--cost" : "turn-announcement-label--cost-opponent";
            await PayBattleAtkCostsAsync(firstCards, firstDeck, firstGraveyard, firstCostClass, ct);
            if (_isGameOver) return;

            string secondCostClass = isLocalFirst ? "turn-announcement-label--cost-opponent" : "turn-announcement-label--cost";
            await PayBattleAtkCostsAsync(secondCards, secondDeck, secondGraveyard, secondCostClass, ct);
            if (_isGameOver) return;

            List<CardView> playerSkill = playerCards.Where(c => c.Data is SkillCardData).ToList();
            List<CardView> opponentSkill = opponentCards.Where(c => c.Data is SkillCardData).ToList();

            bool playerHasAttackingChar = _playerCharacterSlot.CurrentCard != null;
            bool opponentHasAttackingChar = _opponentCharacterSlot.CurrentCard != null;

            BattleCalculator.SideBattleStats playerStats = BattleCalculator.Calculate(
                playerSkill, _playerAtkBoost, playerHasAttackingChar, _playerCharacterSlot.Attack);
            BattleCalculator.SideBattleStats opponentStats = BattleCalculator.Calculate(
                opponentSkill, _opponentAtkBoost, opponentHasAttackingChar, _opponentCharacterSlot.Attack);

            int effectivePlayerDef = _playerCharacterSlot.Defense + _playerDefBoost;
            int effectiveOpponentDef = _opponentCharacterSlot.Defense + _opponentDefBoost;
            int damageToOpponent = Mathf.Max(0, playerStats.ATK - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentStats.ATK - effectivePlayerDef);

            await PlayAtkCounterAsync(playerStats.ATK, opponentStats.ATK, effectiveOpponentDef, effectivePlayerDef, ct);

            // 先攻→後攻の順で攻撃・ダメージを処理
            CharacterSlotView firstTarget = isLocalFirst ? _opponentCharacterSlot : _playerCharacterSlot;
            VisualElement firstAtkOverlay = isLocalFirst ? _playerAtkCounterOverlay : _opponentAtkCounterOverlay;
            int firstAtk = isLocalFirst ? playerStats.ATK : opponentStats.ATK;
            int firstDamage = isLocalFirst ? damageToOpponent : damageToPlayer;
            DeckView firstTargetDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView firstTargetGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            CharacterSlotView secondTarget = isLocalFirst ? _playerCharacterSlot : _opponentCharacterSlot;
            VisualElement secondAtkOverlay = isLocalFirst ? _opponentAtkCounterOverlay : _playerAtkCounterOverlay;
            int secondAtk = isLocalFirst ? opponentStats.ATK : playerStats.ATK;
            int secondDamage = isLocalFirst ? damageToPlayer : damageToOpponent;
            DeckView secondTargetDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView secondTargetGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;

            // 1人目（先攻）の攻撃
            if (firstAtk > 0)
            {
                await PlayAttackIconAsync(firstAtkOverlay, firstTarget, firstAtk, ct);
            }
            firstAtkOverlay.style.display = DisplayStyle.None;
            firstTarget.DefOverlay.style.display = DisplayStyle.None;

            CardView firstDestroyedChar = null;
            Rect firstDestroyedFromRect = default;
            if (firstTarget.CurrentCard != null && firstDamage >= firstTarget.Hp)
            {
                await PlayCharDestroyEffectAsync(firstTarget, ct);
                firstDestroyedChar = firstTarget.CurrentCard;
                firstDestroyedFromRect = firstDestroyedChar.worldBound;
                firstTarget.RemoveCard();
            }

            if (firstDamage > 0)
            {
                Rect firstTargetDeckRect = firstTargetDeck.worldBound;
                List<UniTask> flyTasks = new List<UniTask>
                {
                    PlayDamageNumberFlyAsync(firstDamage, firstTarget.worldBound.center, firstTargetDeck, ct)
                };
                if (firstDestroyedChar != null)
                {
                    flyTasks.Add(FlyToGraveyardAsync(firstDestroyedChar, firstDestroyedFromRect, firstTargetGraveyard, ct));
                }
                await UniTask.WhenAll(flyTasks);
                List<CardView> firstDamageCards = firstTargetDeck.TakeFromTop(firstDamage);
                await PlayDeckDamageAsync(firstDamageCards, firstTargetDeckRect, firstTargetGraveyard, firstTargetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);

                if (firstTargetDeck.Count == 0)
                {
                    SendSkillsToGraveyard(playerSkill, _playerFieldView, _playerGraveyardView);
                    SendSkillsToGraveyard(opponentSkill, _opponentFieldView, _opponentGraveyardView);
                    _isGameOver = true;
                    OnGameEnd(isLocalFirst);
                    ResetBoosts();
                    return;
                }
            }
            else if (firstDestroyedChar != null)
            {
                await FlyToGraveyardAsync(firstDestroyedChar, firstDestroyedFromRect, firstTargetGraveyard, ct);
            }

            if (firstDestroyedChar != null)
            {
                secondAtk = 0;
                secondDamage = 0;
            }

            // 2人目（後攻）の攻撃
            if (secondAtk > 0)
            {
                await PlayAttackIconAsync(secondAtkOverlay, secondTarget, secondAtk, ct);
            }
            secondAtkOverlay.style.display = DisplayStyle.None;
            secondTarget.DefOverlay.style.display = DisplayStyle.None;

            CardView secondDestroyedChar = null;
            Rect secondDestroyedFromRect = default;
            if (secondTarget.CurrentCard != null && secondDamage >= secondTarget.Hp)
            {
                await PlayCharDestroyEffectAsync(secondTarget, ct);
                secondDestroyedChar = secondTarget.CurrentCard;
                secondDestroyedFromRect = secondDestroyedChar.worldBound;
                secondTarget.RemoveCard();
            }

            if (secondDamage > 0)
            {
                Rect secondTargetDeckRect = secondTargetDeck.worldBound;
                List<UniTask> flyTasks = new List<UniTask>
                {
                    PlayDamageNumberFlyAsync(secondDamage, secondTarget.worldBound.center, secondTargetDeck, ct)
                };
                if (secondDestroyedChar != null)
                {
                    flyTasks.Add(FlyToGraveyardAsync(secondDestroyedChar, secondDestroyedFromRect, secondTargetGraveyard, ct));
                }
                await UniTask.WhenAll(flyTasks);
                List<CardView> secondDamageCards = secondTargetDeck.TakeFromTop(secondDamage);
                await PlayDeckDamageAsync(secondDamageCards, secondTargetDeckRect, secondTargetGraveyard, secondTargetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);

                if (secondTargetDeck.Count == 0)
                {
                    SendSkillsToGraveyard(playerSkill, _playerFieldView, _playerGraveyardView);
                    SendSkillsToGraveyard(opponentSkill, _opponentFieldView, _opponentGraveyardView);
                    _isGameOver = true;
                    OnGameEnd(!isLocalFirst);
                    ResetBoosts();
                    return;
                }
            }
            else if (secondDestroyedChar != null)
            {
                await FlyToGraveyardAsync(secondDestroyedChar, secondDestroyedFromRect, secondTargetGraveyard, ct);
            }

            SendSkillsToGraveyard(playerSkill, _playerFieldView, _playerGraveyardView);
            SendSkillsToGraveyard(opponentSkill, _opponentFieldView, _opponentGraveyardView);

            ResetBoosts();
        }

        private void OnGameEnd(bool? playerWins)
        {
            PlayGameEndAsync(playerWins, destroyCancellationToken).Forget();
        }

        // ─── 戦闘フェーズ ヘルパー ───────────────────────────────────────

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct);
            graveyard.AddCard(card);
        }

        private void SendSkillsToGraveyard(IEnumerable<CardView> skills, FieldView field, GraveyardView graveyard)
        {
            foreach (CardView c in skills)
            {
                field.RemoveCard(c);
                graveyard.AddCard(c);
            }
        }

        private void ResetBoosts()
        {
            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;
        }

        private async UniTask PayBattleAtkCostsAsync(
            List<CardView> cards, DeckView deck, GraveyardView graveyard, string costClass, CancellationToken ct)
        {
            int totalCost = cards.Sum(c => c.Data.Cost);
            List<UniTask> tasks = new List<UniTask>
            {
                UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct)
                    .ContinueWith(() => PlayAnnouncementAsync($"PAY {totalCost} COSTS", costClass, ct))
            };
            foreach (CardView c in cards)
            {
                tasks.Add(PayCostAsync(c, deck, graveyard, ct, announce: false));
            }
            await UniTask.WhenAll(tasks);
        }
    }
}

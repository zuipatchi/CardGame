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
                await card.FlashChainLabelAsync(ct);
                card.SetChainNumber(0);
                card.SetState(CardState.Resolve);
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: ct);

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
                        else if (eventData.EventType == CardEventType.Evolve)
                        {
                            CharacterSlotView evolveSlot = isLocal ? _playerCharacterSlot : _opponentCharacterSlot;
                            if (evolveSlot.CurrentCard != null)
                            {
                                await PlayFloatingLabelAsync("EVOLVE", "evolve-label", evolveSlot, ct);
                            }
                        }
                        else if (eventData.EventType == CardEventType.Poison)
                        {
                            CharacterSlotView poisonTarget = isLocal ? _opponentCharacterSlot : _playerCharacterSlot;
                            await PlayPoisonEffectAsync(poisonTarget, ct);
                        }
                        else if (eventData.EventType == CardEventType.DeckMill)
                        {
                            await PlayFloatingLabelAsync("DECK MILL", "deck-mill-label", _playerDeckView, ct);
                            await PlayFloatingLabelAsync("DECK MILL", "deck-mill-label", _opponentDeckView, ct);
                        }
                        else if (eventData.EventType == CardEventType.BattleEndMill)
                        {
                            DeckView battleMillTarget = isLocal ? _opponentDeckView : _playerDeckView;
                            if (_poisonEffectPrefab != null)
                            {
                                await PlayParticleAtUiPositionAsync(battleMillTarget, battleMillTarget.worldBound.center, _poisonEffectPrefab, ct);
                            }
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
                            if (!_isOnline || isLocal)
                            {
                                targetDeck.AddCardsAndShuffle(recovered);
                                if (_isOnline)
                                {
                                    _networkGameService.SendRecoverDeckOrder(targetDeck.GetCardIds());
                                }
                            }
                            else
                            {
                                CardData[] shuffledDeck = await _networkGameService.WaitForOpponentRecoverDeckOrderAsync(ct);
                                targetDeck.Rebuild(shuffledDeck);
                            }
                            await PlayDeckShufflePulseAsync(targetDeck, ct);
                        }
                        break;
                    }
                case CardEventType.Switch:
                    await ApplySwitchEffectAsync(isLocal, ct);
                    break;
                case CardEventType.CharDamage:
                    await ApplyCharDamageAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.Evolve:
                    await ApplyEvolveEffectAsync(isLocal, ct);
                    break;
                case CardEventType.Poison:
                    if (isLocal)
                    {
                        _opponentPoisoned = true;
                    }
                    else
                    {
                        _playerPoisoned = true;
                    }
                    break;
                case CardEventType.DeckMill:
                    await ApplyDeckMillEffectAsync(data.EventValue, ct);
                    break;
                case CardEventType.BattleEndMill:
                    if (isLocal)
                    {
                        _localBattleEndMillValue = data.EventValue;
                    }
                    else
                    {
                        _opponentBattleEndMillValue = data.EventValue;
                    }
                    break;
            }
        }

        private async UniTask ApplyDeckMillEffectAsync(int count, CancellationToken ct)
        {
            Rect playerDeckRect = _playerDeckView.worldBound;
            List<CardView> playerMillCards = _playerDeckView.TakeFromTop(count);
            if (playerMillCards.Count > 0)
            {
                await PlayDeckDamageAsync(playerMillCards, playerDeckRect, _playerGraveyardView, _playerDeckView, ct);
            }

            if (_playerDeckView.Count == 0)
            {
                _isGameOver = true;
                OnGameEnd(false);
                return;
            }

            if (_isGameOver)
            {
                return;
            }

            Rect opponentDeckRect = _opponentDeckView.worldBound;
            List<CardView> opponentMillCards = _opponentDeckView.TakeFromTop(count);
            if (opponentMillCards.Count > 0)
            {
                await PlayDeckDamageAsync(opponentMillCards, opponentDeckRect, _opponentGraveyardView, _opponentDeckView, ct);
            }

            if (_isGameOver)
            {
                return;
            }

            if (_opponentDeckView.Count == 0)
            {
                _isGameOver = true;
                OnGameEnd(true);
            }
        }

        private async UniTask ApplyCharDamageAsync(int baseAtk, bool isLocal, CancellationToken ct)
        {
            CharacterSlotView targetSlot = isLocal ? _opponentCharacterSlot : _playerCharacterSlot;

            if (targetSlot.CurrentCard != null && targetSlot.CurrentCard.IsFaceDown)
            {
                await UniTask.WaitUntil(
                    () => targetSlot.CurrentCard == null || !targetSlot.CurrentCard.IsFaceDown,
                    cancellationToken: ct
                );
            }

            DeckView targetDeck = isLocal ? _opponentDeckView : _playerDeckView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
            int defBoost = isLocal ? _opponentDefBoost : _playerDefBoost;
            int effectiveDef = targetSlot.CurrentCard != null ? targetSlot.Defense + defBoost : 0;
            int damage = Mathf.Max(0, baseAtk - effectiveDef);

            if (targetSlot.CurrentCard != null)
            {
                targetSlot.SetDefValue(effectiveDef);
                targetSlot.DefOverlay.BringToFront();
                targetSlot.DefOverlay.style.opacity = 1f;
                targetSlot.DefOverlay.style.display = DisplayStyle.Flex;
            }

            if (damage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", targetSlot, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                targetSlot.DefOverlay.style.display = DisplayStyle.None;
                return;
            }

            bool charWillBeDestroyed = targetSlot.CurrentCard != null && damage >= targetSlot.Hp;

            if (_charDamageEffectPrefab != null)
            {
                VisualElement particleAnchor = targetSlot.CurrentCard != null
                    ? (VisualElement)targetSlot.CurrentCard
                    : targetSlot;
                await PlayParticleAtUiPositionAsync(particleAnchor, targetSlot.worldBound.center, _charDamageEffectPrefab, ct);
            }

            Rect targetDeckRect = targetDeck.worldBound;
            await PlayDamageNumberFlyAsync(damage, targetSlot.worldBound.center, targetDeck, ct);
            List<CardView> damageCards = targetDeck.TakeFromTop(damage);
            await PlayDeckDamageAsync(damageCards, targetDeckRect, targetGraveyard, targetDeck, ct);
            targetSlot.DefOverlay.style.display = DisplayStyle.None;
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            if (charWillBeDestroyed && targetSlot.CurrentCard != null)
            {
                await PlayCharDestroyEffectAsync(targetSlot, ct);
                CardView destroyedChar = targetSlot.CurrentCard;
                Rect destroyedFromRect = destroyedChar.worldBound;
                targetSlot.RemoveCard();
                await FlyToGraveyardAsync(destroyedChar, destroyedFromRect, targetGraveyard, ct);
            }
        }

        private async UniTask ApplyEvolveEffectAsync(bool isLocal, CancellationToken ct)
        {
            CharacterSlotView ownSlot = isLocal ? _playerCharacterSlot : _opponentCharacterSlot;
            GraveyardView ownGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;

            if (ownSlot.CurrentCard == null)
            {
                return;
            }

            int sacrificedCost = ownSlot.CurrentCard.Data.Cost;
            CardView sacrificedCard = ownSlot.CurrentCard;
            Rect fromRect = sacrificedCard.worldBound;
            ownSlot.RemoveCard();
            await FlyCardToDestAsync(sacrificedCard, fromRect, ownGraveyard, ct);
            ownGraveyard.AddCard(sacrificedCard);

            if (isLocal)
            {
                CardView placed = await WaitForPlayerEvolveInputAsync(sacrificedCost, ct);
                if (_isOnline)
                {
                    _networkGameService.SendEvolveAction(placed?.Data.Id);
                }
                // カードはドロップ時点で HandlePlayerCardDrop が PlaceCard 済み
                if (ownSlot.CurrentCard != null && _evolveEffectPrefab != null)
                {
                    await PlayParticleAtCardAsync(ownSlot.CurrentCard, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f));
                }
            }
            else if (_isOnline)
            {
                string cardId = await _networkGameService.WaitForOpponentEvolveAsync(ct);
                if (!string.IsNullOrEmpty(cardId) && _cardDatabase.TryGet(cardId, out CardData cardData))
                {
                    IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                    Rect charFromRect = hand.Count > 0 ? hand[0].worldBound : ownSlot.worldBound;
                    if (hand.Count > 0)
                    {
                        _opponentHandView.RemoveCard(hand[0]);
                    }
                    CardView newChar = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(newChar, charFromRect, ownSlot, ct);
                    ownSlot.PlaceCard(newChar);
                    if (_evolveEffectPrefab != null)
                    {
                        await PlayParticleAtCardAsync(newChar, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f));
                    }
                }
            }
            else
            {
                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                int idx = CpuAgent.ChooseEvolveCardIndex(cpuHand.Select(c => c.Data).ToList(), sacrificedCost);
                if (idx >= 0)
                {
                    CardView newChar = cpuHand[idx];
                    Rect charFromRect = newChar.worldBound;
                    _opponentHandView.RemoveCard(newChar);
                    await FlyCardToDestAsync(newChar, charFromRect, ownSlot, ct);
                    ownSlot.PlaceCard(newChar);
                    if (_evolveEffectPrefab != null)
                    {
                        await PlayParticleAtCardAsync(newChar, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f));
                    }
                }
            }
        }

        private async UniTask<CardView> WaitForPlayerEvolveInputAsync(int minCost, CancellationToken ct)
        {
            _evolveMinCost = minCost;
            _evolveInput._tcs = new UniTaskCompletionSource<CardView>();
            _evolveInput._card = null;
            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _evolveInput._tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _evolveInput._tcs = null;
                _evolveMinCost = 0;
                HideActionButtons();
                RefreshHandHighlights();
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
                if (_isOnline)
                {
                    _networkGameService.SendSwitchAction(newChar?.Data.Id);
                }
                if (newChar != null)
                {
                    await PayCostAsync(newChar, _playerDeckView, _playerGraveyardView, ct);
                }
            }
            else if (_isOnline)
            {
                // アニメーション前にハンドラを事前登録してメッセージのロストを防ぐ
                UniTask<string> switchReceiveTask = _networkGameService.WaitForOpponentSwitchAsync(ct);
                await existingChar.FlipAsync(ct);
                await _opponentHandView.AddCardBackAsync(existingChar, charRect, ct);
                string cardId = await switchReceiveTask;
                if (!string.IsNullOrEmpty(cardId) && _cardDatabase.TryGet(cardId, out CardData cardData))
                {
                    IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                    Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentHandView.worldBound;
                    if (hand.Count > 0)
                    {
                        _opponentHandView.RemoveCard(hand[0]);
                    }
                    CardView newChar = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(newChar, fromRect, _opponentCharacterSlot, ct);
                    _opponentCharacterSlot.PlaceCard(newChar);
                    await PayCostAsync(newChar, _opponentDeckView, _opponentGraveyardView, ct);
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

        private async UniTask FireGraveTriggerAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            await PlayGraveTriggerDisplayAsync(data, isLocal, ct);
            await ApplyEventEffectAsync(data, isLocal, ct);
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
    }
}

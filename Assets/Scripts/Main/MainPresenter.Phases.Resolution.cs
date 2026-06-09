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
        // ─── 即時解決（1枚）────────────────────────────────────────────────

        private async UniTask ResolveSingleCardAsync(CardView card, CancellationToken ct)
        {
            bool isLocal = !card.IsOpponent;

            card.SetState(CardState.Resolve);
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: ct);

            if (card.Data is EventCardData eventData)
            {
                if (eventData.EventType == CardEventType.Draw)
                {
                    await PlayDrawEffectAsync(card, eventData.EventValue, ct);
                }
                else if (eventData.EventType == CardEventType.BanishChar)
                {
                    FieldView banishField = isLocal ? _opponentFieldView : _playerFieldView;
                    CardView banishTarget = banishField.Characters.Count > 0
                        ? banishField.Characters[0]
                        : null;
                    if (banishTarget != null)
                    {
                        await PlayBanishCharEffectAsync(banishTarget, ct);
                    }
                }
                else if (eventData.EventType == CardEventType.Recover)
                {
                    await PlayRecoverEffectAsync(card, eventData.EventValue, ct);
                }
                else if (eventData.EventType == CardEventType.Switch)
                {
                    FieldView switchField = isLocal ? _playerFieldView : _opponentFieldView;
                    CardView switchChar = switchField.Characters.Count > 0
                        ? switchField.Characters[0]
                        : null;
                    if (switchChar != null)
                    {
                        await PlaySwitchEffectAsync(card, switchChar, ct);
                    }
                }
                else if (eventData.EventType == CardEventType.Evolve)
                {
                    FieldView evolveField = isLocal ? _playerFieldView : _opponentFieldView;
                    CardView evolveChar = evolveField.Characters.Count > 0
                        ? evolveField.Characters[0]
                        : null;
                    if (evolveChar != null)
                    {
                        await PlayFloatingLabelAsync("EVOLVE", "evolve-label", evolveChar, ct);
                    }
                }
                else if (eventData.EventType == CardEventType.Poison)
                {
                    if (_poisonEffectPrefab != null)
                    {
                        await PlayParticleAtCardAsync(card, _poisonEffectPrefab, ct);
                    }
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
            }

            if (_isGameOver)
            {
                return;
            }

            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            field.RemoveCard(card);
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            graveyard.AddCard(card);

            card.SetState(CardState.Normal);
        }

        private async UniTask ApplyEventEffectAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            switch (data.EventType)
            {
                case CardEventType.Draw:
                    await ApplyDrawEffectAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.BanishChar:
                {
                    FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
                    GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
                    if (targetField.Characters.Count == 0)
                    {
                        break;
                    }
                    CardView charCard = targetField.Characters[0];
                    Rect fromRect = charCard.worldBound;
                    targetField.RemoveCard(charCard);
                    await FlyCardToDestAsync(charCard, fromRect, targetGraveyard, ct);
                    targetGraveyard.AddCard(charCard);
                    break;
                }
                case CardEventType.Recover:
                {
                    GraveyardView sourceGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
                    DeckView targetDeck = isLocal ? _playerDeckView : _opponentDeckView;
                    List<CardData> recovered = sourceGraveyard.TakeFromTop(data.EventValue);
                    if (recovered.Count > 0)
                    {
                        // アニメーション前にハンドラを登録してメッセージのロストを防ぐ
                        UniTask<CardData[]> recoverReceiveTask = (_isOnline && !isLocal)
                            ? _networkGameService.WaitForOpponentRecoverDeckOrderAsync(ct)
                            : default;
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
                            CardData[] shuffledDeck = await recoverReceiveTask;
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
                    await ApplyDeckMillEffectAsync(data.EventValue, isLocal, ct);
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

        private async UniTask ApplyDeckMillEffectAsync(int count, bool isLocal, CancellationToken ct)
        {
            DeckView firstDeck = isLocal ? _playerDeckView : _opponentDeckView;
            GraveyardView firstGrave = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            DeckView secondDeck = isLocal ? _opponentDeckView : _playerDeckView;
            GraveyardView secondGrave = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            if (_deckMillEffectPrefab != null)
            {
                await PlayParticleAtUiPositionAsync(firstDeck, firstDeck.worldBound.center, _deckMillEffectPrefab, ct);
            }

            Rect firstDeckRect = firstDeck.worldBound;
            List<CardView> firstMillCards = firstDeck.TakeFromTop(count);
            if (firstMillCards.Count > 0)
            {
                await PlayDeckDamageAsync(firstMillCards, firstDeckRect, firstGrave, firstDeck, ct);
            }

            if (firstMillCards.Count < count)
            {
                _isGameOver = true;
                OnGameEnd(!isLocal);
                return;
            }

            if (_isGameOver)
            {
                return;
            }

            if (_deckMillEffectPrefab != null)
            {
                await PlayParticleAtUiPositionAsync(secondDeck, secondDeck.worldBound.center, _deckMillEffectPrefab, ct);
            }

            Rect secondDeckRect = secondDeck.worldBound;
            List<CardView> secondMillCards = secondDeck.TakeFromTop(count);
            if (secondMillCards.Count > 0)
            {
                await PlayDeckDamageAsync(secondMillCards, secondDeckRect, secondGrave, secondDeck, ct);
            }

            if (_isGameOver)
            {
                return;
            }

            if (secondMillCards.Count < count)
            {
                _isGameOver = true;
                OnGameEnd(isLocal);
            }
        }

        private async UniTask ApplyCharDamageAsync(int baseAtk, bool isLocal, CancellationToken ct)
        {
            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            IReadOnlyList<CardView> targetChars = targetField.Characters;

            if (targetChars.Count == 0)
            {
                return;
            }

            CardView targetChar = targetChars[0];

            // 裏向きのキャラが表向きになるまで待つ
            if (targetChar.IsFaceDown)
            {
                await UniTask.WaitUntil(
                    () => !targetField.Contains(targetChar) || !targetChar.IsFaceDown,
                    cancellationToken: ct
                );
            }

            if (!targetField.Contains(targetChar))
            {
                return;
            }

            DeckView targetDeck = isLocal ? _opponentDeckView : _playerDeckView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
            int damage = Mathf.Max(0, baseAtk - targetChar.Data.Defense);

            if (damage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", targetChar, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                return;
            }

            bool charWillBeDestroyed = damage >= targetChar.Data.Hp;

            if (_charDamageEffectPrefab != null)
            {
                await PlayParticleAtUiPositionAsync(targetChar, targetChar.worldBound.center, _charDamageEffectPrefab, ct);
            }

            Rect targetDeckRect = targetDeck.worldBound;
            await PlayDamageNumberFlyAsync(damage, targetChar.worldBound.center, targetDeck, ct);
            List<CardView> damageCards = targetDeck.TakeFromTop(damage);
            await PlayDeckDamageAsync(damageCards, targetDeckRect, targetGraveyard, targetDeck, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            if (damageCards.Count < damage)
            {
                _isGameOver = true;
                OnGameEnd(isLocal);
                return;
            }

            if (charWillBeDestroyed && targetField.Contains(targetChar))
            {
                await PlayCharDestroyEffectAsync(targetChar, ct);
                Rect destroyedFromRect = targetChar.worldBound;
                targetField.RemoveCard(targetChar);
                await FlyToGraveyardAsync(targetChar, destroyedFromRect, targetGraveyard, ct);
            }
        }

        private async UniTask ApplyEvolveEffectAsync(bool isLocal, CancellationToken ct)
        {
            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            GraveyardView ownGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;

            if (ownField.Characters.Count == 0)
            {
                return;
            }

            CardView sacrificedCard;
            if (isLocal)
            {
                sacrificedCard = ownField.Characters.Count == 1
                    ? ownField.Characters[0]
                    : await WaitForPlayerFieldCharSelectionAsync(ct);
            }
            else
            {
                sacrificedCard = ownField.Characters[0];
            }

            if (sacrificedCard == null)
            {
                return;
            }

            int sacrificedCost = sacrificedCard.Data.Cost;
            Rect fromRect = sacrificedCard.worldBound;
            ownField.RemoveCard(sacrificedCard);
            await FlyCardToDestAsync(sacrificedCard, fromRect, ownGraveyard, ct);
            ownGraveyard.AddCard(sacrificedCard);

            if (isLocal)
            {
                CardView placed = await WaitForPlayerEvolveInputAsync(sacrificedCost, ct);
                if (_isOnline)
                {
                    _networkGameService.SendEvolveAction(placed?.Data.Id);
                }
                if (placed != null && _evolveEffectPrefab != null)
                {
                    await PlayParticleAtCardAsync(placed, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f));
                }
            }
            else if (_isOnline)
            {
                string cardId = await _networkGameService.WaitForOpponentEvolveAsync(ct);
                if (!string.IsNullOrEmpty(cardId) && _cardDatabase.TryGet(cardId, out CardData cardData))
                {
                    IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                    Rect charFromRect = hand.Count > 0 ? hand[0].worldBound : ownField.worldBound;
                    if (hand.Count > 0)
                    {
                        _opponentHandView.RemoveCard(hand[0]);
                    }
                    CardView newChar = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(newChar, charFromRect, ownField, ct);
                    ownField.PlaceCard(newChar);
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
                    await FlyCardToDestAsync(newChar, charFromRect, ownField, ct);
                    ownField.PlaceCard(newChar);
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
            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            GraveyardView ownGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;

            if (ownField.Characters.Count == 0)
            {
                return;
            }

            if (isLocal)
            {
                CardView existingChar = ownField.Characters.Count == 1
                    ? ownField.Characters[0]
                    : await WaitForPlayerFieldCharSelectionAsync(ct);

                if (existingChar == null)
                {
                    return;
                }

                Rect charRect = existingChar.worldBound;
                ownField.RemoveCard(existingChar);
                await _handView.AddCardBackAsync(existingChar, charRect, ct);

                CardView newChar = await WaitForPlayerSwitchInputAsync(ct);
                if (_isOnline)
                {
                    _networkGameService.SendSwitchAction(existingChar.Data.Id, newChar?.Data.Id);
                }
                if (newChar != null)
                {
                    await PayHandCostAsync(newChar, _handView, _playerGraveyardView, isLocalPlayer: true, ct);
                }
            }
            else if (_isOnline)
            {
                // アニメーション前にハンドラを事前登録してメッセージのロストを防ぐ
                UniTask<(string sacrificedCharId, string newCardId)> switchReceiveTask =
                    _networkGameService.WaitForOpponentSwitchAsync(ct);

                (string oppSacrificeId, string oppNewCardId) = await switchReceiveTask;

                CardView sacrificedChar = null;
                foreach (CardView c in _opponentFieldView.Characters)
                {
                    if (c.Data.Id == oppSacrificeId)
                    {
                        sacrificedChar = c;
                        break;
                    }
                }

                if (sacrificedChar != null)
                {
                    Rect sacrificedRect = sacrificedChar.worldBound;
                    _opponentFieldView.RemoveCard(sacrificedChar);
                    await sacrificedChar.FlipAsync(ct);
                    await _opponentHandView.AddCardBackAsync(sacrificedChar, sacrificedRect, ct);
                }

                if (!string.IsNullOrEmpty(oppNewCardId) && _cardDatabase.TryGet(oppNewCardId, out CardData cardData))
                {
                    IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                    Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentFieldView.worldBound;
                    if (hand.Count > 0)
                    {
                        _opponentHandView.RemoveCard(hand[0]);
                    }
                    CardView newChar = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(newChar, fromRect, _opponentFieldView, ct);
                    _opponentFieldView.PlaceCard(newChar);
                    await PayHandCostAsync(newChar, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct);
                }
            }
            else
            {
                CardView existingChar = ownField.Characters[0];
                Rect charRect = existingChar.worldBound;
                ownField.RemoveCard(existingChar);
                await existingChar.FlipAsync(ct);
                await _opponentHandView.AddCardBackAsync(existingChar, charRect, ct);

                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHand.Select(c => c.Data).ToList());
                if (idx >= 0)
                {
                    CardView newChar = cpuHand[idx];
                    Rect fromRect = newChar.worldBound;
                    _opponentHandView.RemoveCard(newChar);
                    await FlyCardToDestAsync(newChar, fromRect, ownField, ct);
                    ownField.PlaceCard(newChar);
                    await newChar.FlipAsync(ct);
                    await PayHandCostAsync(newChar, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct);
                }
            }
        }

        private async UniTask<CardView> WaitForPlayerFieldCharSelectionAsync(CancellationToken ct)
        {
            _fieldCharSelectionTcs = new UniTaskCompletionSource<CardView>();

            foreach (CardView c in _playerFieldView.Characters)
            {
                c.AddToClassList("selectable-char");
            }

            ShowActionButtons();
            UpdateStagedButtons(false);

            try
            {
                return await _fieldCharSelectionTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _fieldCharSelectionTcs = null;
                foreach (CardView c in _playerFieldView.Characters)
                {
                    c.RemoveFromClassList("selectable-char");
                }
                HideActionButtons();
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

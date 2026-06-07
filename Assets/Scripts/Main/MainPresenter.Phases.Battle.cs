using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── 戦闘フェーズ ────────────────────────────────────────────────

        private async UniTask RunBattlePhaseAsync(bool priorityUsed, CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Battle);

            bool playerHasChar = _playerCharacterSlot.CurrentCard != null;
            bool opponentHasChar = _opponentCharacterSlot.CurrentCard != null;

            if (!playerHasChar && !opponentHasChar)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            int playerAtk = BattleCalculator.Calculate(_playerAtkBoost, playerHasChar, _playerCharacterSlot.Attack);
            int opponentAtk = BattleCalculator.Calculate(_opponentAtkBoost, opponentHasChar, _opponentCharacterSlot.Attack);

            bool bothNoAttack = playerAtk == 0 && opponentAtk == 0;
            if (priorityUsed && !bothNoAttack)
            {
                bool localUsedPriority = _localHasPriority;
                await PlayPriorityCoinTransferAsync(localUsedPriority, ct);
                _localHasPriority = !_localHasPriority;
                UpdatePriorityCoinUI();
            }

            bool isLocalFirst = _gameModel.IsLocalTurn;

            int effectivePlayerDef = playerHasChar ? _playerCharacterSlot.Defense + _playerDefBoost : 0;
            int effectiveOpponentDef = opponentHasChar ? _opponentCharacterSlot.Defense + _opponentDefBoost : 0;
            int damageToOpponent = Mathf.Max(0, playerAtk - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentAtk - effectivePlayerDef);

            CharacterSlotView firstTarget = isLocalFirst ? _opponentCharacterSlot : _playerCharacterSlot;
            VisualElement firstAtkOverlay = isLocalFirst ? _playerAtkCounterOverlay : _opponentAtkCounterOverlay;
            Label firstAtkLabel = isLocalFirst ? _playerAtkCounterLabel : _opponentAtkCounterLabel;
            int firstAtk = isLocalFirst ? playerAtk : opponentAtk;
            int firstDamage = isLocalFirst ? damageToOpponent : damageToPlayer;
            int firstEffectiveDef = isLocalFirst ? effectiveOpponentDef : effectivePlayerDef;
            DeckView firstTargetDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView firstTargetGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            CharacterSlotView secondTarget = isLocalFirst ? _playerCharacterSlot : _opponentCharacterSlot;
            VisualElement secondAtkOverlay = isLocalFirst ? _opponentAtkCounterOverlay : _playerAtkCounterOverlay;
            Label secondAtkLabel = isLocalFirst ? _opponentAtkCounterLabel : _playerAtkCounterLabel;
            int secondAtk = isLocalFirst ? opponentAtk : playerAtk;
            int secondDamage = isLocalFirst ? damageToPlayer : damageToOpponent;
            int secondEffectiveDef = isLocalFirst ? effectivePlayerDef : effectiveOpponentDef;
            DeckView secondTargetDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView secondTargetGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;

            // ─── 先攻 ────────────────────────────────────────────────────

            await PlaySingleSideAtkCounterAsync(firstAtkOverlay, firstAtkLabel, firstAtk, firstTarget, firstEffectiveDef, ct);

            if (firstAtk > 0)
            {
                await PlayAttackIconAsync(firstAtkOverlay, firstTarget, firstAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", firstAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }
            firstAtkOverlay.style.display = DisplayStyle.None;
            firstTarget.DefOverlay.style.display = DisplayStyle.None;

            if (firstAtk > 0 && firstDamage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", firstTarget, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }

            bool firstCharWillBeDestroyed = firstTarget.CurrentCard != null && firstDamage >= firstTarget.Hp;

            if (firstDamage > 0)
            {
                Rect firstTargetDeckRect = firstTargetDeck.worldBound;
                await PlayDamageNumberFlyAsync(firstDamage, firstTarget.worldBound.center, firstTargetDeck, ct);
                List<CardView> firstDamageCards = firstTargetDeck.TakeFromTop(firstDamage);
                await PlayDeckDamageAsync(firstDamageCards, firstTargetDeckRect, firstTargetGraveyard, firstTargetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

                if (firstDamageCards.Count < firstDamage)
                {
                    _isGameOver = true;
                    OnGameEnd(isLocalFirst);
                    ResetBoosts();
                    return;
                }
            }

            CardView firstDestroyedChar = null;
            if (firstCharWillBeDestroyed)
            {
                await PlayCharDestroyEffectAsync(firstTarget, ct);
                firstDestroyedChar = firstTarget.CurrentCard;
                Rect firstDestroyedFromRect = firstDestroyedChar.worldBound;
                firstTarget.RemoveCard();
                await FlyToGraveyardAsync(firstDestroyedChar, firstDestroyedFromRect, firstTargetGraveyard, ct);
            }

            if (firstDestroyedChar != null)
            {
                secondAtk = 0;
                secondDamage = 0;
            }

            // ─── 後攻 ────────────────────────────────────────────────────

            await PlaySingleSideAtkCounterAsync(secondAtkOverlay, secondAtkLabel, secondAtk, secondTarget, secondEffectiveDef, ct);

            if (secondAtk > 0)
            {
                await PlayAttackIconAsync(secondAtkOverlay, secondTarget, secondAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", secondAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }
            secondAtkOverlay.style.display = DisplayStyle.None;
            secondTarget.DefOverlay.style.display = DisplayStyle.None;

            if (secondAtk > 0 && secondDamage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", secondTarget, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }

            bool secondCharWillBeDestroyed = secondTarget.CurrentCard != null && secondDamage >= secondTarget.Hp;

            if (secondDamage > 0)
            {
                Rect secondTargetDeckRect = secondTargetDeck.worldBound;
                await PlayDamageNumberFlyAsync(secondDamage, secondTarget.worldBound.center, secondTargetDeck, ct);
                List<CardView> secondDamageCards = secondTargetDeck.TakeFromTop(secondDamage);
                await PlayDeckDamageAsync(secondDamageCards, secondTargetDeckRect, secondTargetGraveyard, secondTargetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

                if (secondDamageCards.Count < secondDamage)
                {
                    _isGameOver = true;
                    OnGameEnd(!isLocalFirst);
                    ResetBoosts();
                    return;
                }
            }

            if (secondCharWillBeDestroyed)
            {
                await PlayCharDestroyEffectAsync(secondTarget, ct);
                CardView secondDestroyedChar = secondTarget.CurrentCard;
                Rect secondDestroyedFromRect = secondDestroyedChar.worldBound;
                secondTarget.RemoveCard();
                await FlyToGraveyardAsync(secondDestroyedChar, secondDestroyedFromRect, secondTargetGraveyard, ct);
            }

            // ─── 毒破壊（戦闘終了後に毒状態のキャラを破壊）──────────────────────
            bool firstTargetPoisoned = isLocalFirst ? _opponentPoisoned : _playerPoisoned;
            bool secondTargetPoisoned = isLocalFirst ? _playerPoisoned : _opponentPoisoned;

            if (!_isGameOver && firstTargetPoisoned && firstDamage > 0 && firstTarget.CurrentCard != null)
            {
                await PlayPoisonEffectAsync(firstTarget, ct);
                await PlayCharDestroyEffectAsync(firstTarget, ct);
                CardView poisonedChar = firstTarget.CurrentCard;
                Rect poisonedFromRect = poisonedChar.worldBound;
                firstTarget.RemoveCard();
                await FlyToGraveyardAsync(poisonedChar, poisonedFromRect, firstTargetGraveyard, ct);
            }

            if (!_isGameOver && secondTargetPoisoned && secondDamage > 0 && secondTarget.CurrentCard != null)
            {
                await PlayPoisonEffectAsync(secondTarget, ct);
                await PlayCharDestroyEffectAsync(secondTarget, ct);
                CardView poisonedChar = secondTarget.CurrentCard;
                Rect poisonedFromRect = poisonedChar.worldBound;
                secondTarget.RemoveCard();
                await FlyToGraveyardAsync(poisonedChar, poisonedFromRect, secondTargetGraveyard, ct);
            }

            ResetBoosts();

            if (!_isGameOver && (_localBattleEndMillValue > 0 || _opponentBattleEndMillValue > 0))
            {
                await ApplyBattleEndMillAsync(ct);
            }
        }

        private async UniTask ApplyBattleEndMillAsync(CancellationToken ct)
        {
            bool localFirst = _localHasPriority;

            DeckView firstDeck       = localFirst ? _opponentDeckView       : _playerDeckView;
            GraveyardView firstGrave = localFirst ? _opponentGraveyardView  : _playerGraveyardView;
            int firstValue           = localFirst ? _localBattleEndMillValue : _opponentBattleEndMillValue;

            DeckView secondDeck       = localFirst ? _playerDeckView        : _opponentDeckView;
            GraveyardView secondGrave = localFirst ? _playerGraveyardView   : _opponentGraveyardView;
            int secondValue           = localFirst ? _opponentBattleEndMillValue : _localBattleEndMillValue;

            if (!_isGameOver && firstValue > 0)
            {
                int firstMilled = await PlayPoisonMillAsync(firstDeck, firstGrave, firstValue, ct);
                if (firstMilled < firstValue)
                {
                    _isGameOver = true;
                    OnGameEnd(localFirst);
                    return;
                }
            }

            if (!_isGameOver && secondValue > 0)
            {
                int secondMilled = await PlayPoisonMillAsync(secondDeck, secondGrave, secondValue, ct);
                if (secondMilled < secondValue)
                {
                    _isGameOver = true;
                    OnGameEnd(!localFirst);
                }
            }
        }

        private async UniTask<int> PlayPoisonMillAsync(DeckView deck, GraveyardView graveyard, int count, CancellationToken ct)
        {
            if (_poisonEffectPrefab != null)
            {
                await PlayParticleAtUiPositionAsync(deck, deck.worldBound.center, _poisonEffectPrefab, ct);
            }

            Rect deckRect = deck.worldBound;
            List<CardView> millCards = deck.TakeFromTop(count);
            if (millCards.Count > 0)
            {
                await PlayDeckDamageAsync(millCards, deckRect, graveyard, deck, ct);
            }
            return millCards.Count;
        }

        // ─── 戦闘フェーズ ヘルパー ───────────────────────────────────────

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct, delay, duration);
            graveyard.AddCard(card);
        }

        private void ResetBoosts()
        {
            _playerAtkBoost = 0;
            _opponentAtkBoost = 0;
            _playerDefBoost = 0;
            _opponentDefBoost = 0;
            _playerPoisoned = false;
            _opponentPoisoned = false;
        }
    }
}

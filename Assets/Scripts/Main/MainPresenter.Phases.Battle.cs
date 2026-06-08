using System;
using System.Collections.Generic;
using System.Linq;
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

        private async UniTask RunBattlePhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Battle);

            bool playerHasChars = _playerFieldView.Characters.Count > 0;
            bool opponentHasChars = _opponentFieldView.Characters.Count > 0;

            if (!playerHasChars && !opponentHasChars)
            {
                return;
            }

            await PlayAnnouncementAsync("FIGHT", "turn-announcement-label--fight", ct);

            // ─── 攻撃選択 ───────────────────────────────────────────────────
            CardView localAttacker;
            CardView localTarget;
            CardView opponentAttacker;
            CardView opponentTarget;

            if (_isOnline)
            {
                // アナウンス前にハンドラ登録してメッセージのロストを防ぐ。
                UniTask<(string attackerId, string targetId)> opponentAttackReceiveTask =
                    _networkGameService.WaitForOpponentBattleAttackAsync(ct);

                (CardView playerA, CardView playerT) = await WaitForPlayerBattleAttackAsync(ct);
                localAttacker = playerA;
                localTarget = playerT;

                _networkGameService.SendBattleAttackAction(localAttacker?.Data.Id, localTarget?.Data.Id);

                (string oppAtkId, string oppTgtId) = await opponentAttackReceiveTask;

                opponentAttacker = null;
                opponentTarget = null;
                if (oppAtkId != null && oppTgtId != null)
                {
                    foreach (CardView c in _opponentFieldView.Characters)
                    {
                        if (c.Data.Id == oppAtkId)
                        {
                            opponentAttacker = c;
                            break;
                        }
                    }
                    foreach (CardView c in _playerFieldView.Characters)
                    {
                        if (c.Data.Id == oppTgtId)
                        {
                            opponentTarget = c;
                            break;
                        }
                    }
                }
            }
            else
            {
                (CardView playerA, CardView playerT) = await WaitForPlayerBattleAttackAsync(ct);
                localAttacker = playerA;
                localTarget = playerT;
                (opponentAttacker, opponentTarget) = CpuChooseBattleAttack();
            }

            bool playerAttacks = localAttacker != null;
            bool opponentAttacks = opponentAttacker != null;

            if (!playerAttacks && !opponentAttacks)
            {
                return;
            }

            // ─── 速さ比較で先攻後攻を決定 ────────────────────────────────────
            bool isLocalFirst;
            bool priorityUsed = false;
            if (playerAttacks && opponentAttacks)
            {
                int localSpeed = localAttacker.Data.Speed;
                int opponentSpeed = opponentAttacker.Data.Speed;
                if (localSpeed != opponentSpeed)
                {
                    isLocalFirst = localSpeed > opponentSpeed;
                }
                else
                {
                    priorityUsed = true;
                    isLocalFirst = _localHasPriority;
                }
            }
            else
            {
                isLocalFirst = playerAttacks;
            }
            _gameModel.SetInitialTurn(isLocalFirst);

            int playerAtk = playerAttacks ? BattleCalculator.Calculate(_playerAtkBoost, true, localAttacker.Data.Attack) : 0;
            int opponentAtk = opponentAttacks ? BattleCalculator.Calculate(_opponentAtkBoost, true, opponentAttacker.Data.Attack) : 0;

            if (priorityUsed)
            {
                bool localUsedPriority = _localHasPriority;
                await PlayPriorityCoinTransferAsync(localUsedPriority, ct);
                _localHasPriority = !_localHasPriority;
                UpdatePriorityCoinUI();
            }

            int effectiveLocalTargetDef = localTarget != null ? localTarget.Data.Defense + _opponentDefBoost : 0;
            int effectiveOpponentTargetDef = opponentTarget != null ? opponentTarget.Data.Defense + _playerDefBoost : 0;
            int damageToOpponent = Mathf.Max(0, playerAtk - effectiveLocalTargetDef);
            int damageToPlayer = Mathf.Max(0, opponentAtk - effectiveOpponentTargetDef);

            CardView firstTargetChar = isLocalFirst ? localTarget : opponentTarget;
            FieldView firstTargetField = isLocalFirst ? _opponentFieldView : _playerFieldView;
            VisualElement firstAtkOverlay = isLocalFirst ? _playerAtkCounterOverlay : _opponentAtkCounterOverlay;
            Label firstAtkLabel = isLocalFirst ? _playerAtkCounterLabel : _opponentAtkCounterLabel;
            int firstAtk = isLocalFirst ? playerAtk : opponentAtk;
            int firstDamage = isLocalFirst ? damageToOpponent : damageToPlayer;
            DeckView firstTargetDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView firstTargetGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            CardView secondTargetChar = isLocalFirst ? opponentTarget : localTarget;
            FieldView secondTargetField = isLocalFirst ? _playerFieldView : _opponentFieldView;
            VisualElement secondAtkOverlay = isLocalFirst ? _opponentAtkCounterOverlay : _playerAtkCounterOverlay;
            Label secondAtkLabel = isLocalFirst ? _opponentAtkCounterLabel : _playerAtkCounterLabel;
            int secondAtk = isLocalFirst ? opponentAtk : playerAtk;
            int secondDamage = isLocalFirst ? damageToPlayer : damageToOpponent;
            DeckView secondTargetDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView secondTargetGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;

            // ─── 先攻 ────────────────────────────────────────────────────

            if (firstAtk > 0)
            {
                await PlaySingleSideAtkCounterAsync(firstAtkOverlay, firstAtkLabel, firstAtk, ct);
                VisualElement firstTargetVE = firstTargetChar ?? (VisualElement)firstTargetDeck;
                await PlayAttackIconAsync(firstAtkOverlay, firstTargetVE, firstAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", firstAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }
            firstAtkOverlay.style.display = DisplayStyle.None;

            if (firstAtk > 0 && firstDamage == 0 && firstTargetChar != null)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", firstTargetChar, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }

            bool firstCharWillBeDestroyed = firstTargetChar != null && firstDamage >= firstTargetChar.Data.Hp;

            if (firstDamage > 0)
            {
                Vector2 firstDamageOrigin = firstTargetChar != null
                    ? firstTargetChar.worldBound.center
                    : firstTargetDeck.worldBound.center;
                Rect firstTargetDeckRect = firstTargetDeck.worldBound;
                await PlayDamageNumberFlyAsync(firstDamage, firstDamageOrigin, firstTargetDeck, ct);
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

            if (firstCharWillBeDestroyed && firstTargetField.Contains(firstTargetChar))
            {
                await PlayCharDestroyEffectAsync(firstTargetChar, ct);
                Rect firstDestroyedFromRect = firstTargetChar.worldBound;
                firstTargetField.RemoveCard(firstTargetChar);
                await FlyToGraveyardAsync(firstTargetChar, firstDestroyedFromRect, firstTargetGraveyard, ct);
            }

            // ─── 後攻 ────────────────────────────────────────────────────

            if (secondAtk > 0)
            {
                await PlaySingleSideAtkCounterAsync(secondAtkOverlay, secondAtkLabel, secondAtk, ct);
                VisualElement secondTargetVE = secondTargetChar ?? (VisualElement)secondTargetDeck;
                await PlayAttackIconAsync(secondAtkOverlay, secondTargetVE, secondAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", secondAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }
            secondAtkOverlay.style.display = DisplayStyle.None;

            if (secondAtk > 0 && secondDamage == 0 && secondTargetChar != null)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", secondTargetChar, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
            }

            bool secondCharWillBeDestroyed = secondTargetChar != null && secondDamage >= secondTargetChar.Data.Hp;

            if (secondDamage > 0)
            {
                Vector2 secondDamageOrigin = secondTargetChar != null
                    ? secondTargetChar.worldBound.center
                    : secondTargetDeck.worldBound.center;
                Rect secondTargetDeckRect = secondTargetDeck.worldBound;
                await PlayDamageNumberFlyAsync(secondDamage, secondDamageOrigin, secondTargetDeck, ct);
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

            if (secondCharWillBeDestroyed && secondTargetField.Contains(secondTargetChar))
            {
                await PlayCharDestroyEffectAsync(secondTargetChar, ct);
                Rect secondDestroyedFromRect = secondTargetChar.worldBound;
                secondTargetField.RemoveCard(secondTargetChar);
                await FlyToGraveyardAsync(secondTargetChar, secondDestroyedFromRect, secondTargetGraveyard, ct);
            }

            // ─── 毒破壊（戦闘終了後に毒状態のキャラを破壊）──────────────────────
            bool firstTargetPoisoned = isLocalFirst ? _opponentPoisoned : _playerPoisoned;
            bool secondTargetPoisoned = isLocalFirst ? _playerPoisoned : _opponentPoisoned;

            if (!_isGameOver && firstTargetPoisoned && firstDamage > 0
                && firstTargetChar != null && firstTargetField.Contains(firstTargetChar))
            {
                await PlayPoisonEffectAsync(firstTargetChar, ct);
                await PlayCharDestroyEffectAsync(firstTargetChar, ct);
                Rect firstPoisonedFromRect = firstTargetChar.worldBound;
                firstTargetField.RemoveCard(firstTargetChar);
                await FlyToGraveyardAsync(firstTargetChar, firstPoisonedFromRect, firstTargetGraveyard, ct);
            }

            if (!_isGameOver && secondTargetPoisoned && secondDamage > 0
                && secondTargetChar != null && secondTargetField.Contains(secondTargetChar))
            {
                await PlayPoisonEffectAsync(secondTargetChar, ct);
                await PlayCharDestroyEffectAsync(secondTargetChar, ct);
                Rect secondPoisonedFromRect = secondTargetChar.worldBound;
                secondTargetField.RemoveCard(secondTargetChar);
                await FlyToGraveyardAsync(secondTargetChar, secondPoisonedFromRect, secondTargetGraveyard, ct);
            }

            ResetBoosts();

            if (!_isGameOver && (_localBattleEndMillValue > 0 || _opponentBattleEndMillValue > 0))
            {
                await ApplyBattleEndMillAsync(ct);
            }
        }

        private (CardView attacker, CardView target) CpuChooseBattleAttack()
        {
            IReadOnlyList<CardView> cpuChars = _opponentFieldView.Characters;
            IReadOnlyList<CardView> playerChars = _playerFieldView.Characters;

            if (cpuChars.Count == 0)
            {
                return (null, null);
            }

            // プレイヤーにキャラがいない → 最高ATKでデッキ直撃（target=null）
            if (playerChars.Count == 0)
            {
                CardView bestCpu = cpuChars[0];
                foreach (CardView c in cpuChars)
                {
                    if (c.Data.Attack > bestCpu.Data.Attack) { bestCpu = c; }
                }
                return (bestCpu, null);
            }

            (int attackerIdx, int targetIdx) = CpuAgent.ChooseBattleAttack(
                cpuChars.Select(c => c.Data).ToList(),
                playerChars.Select(c => c.Data).ToList()
            );

            if (attackerIdx < 0 || targetIdx < 0)
            {
                return (null, null);
            }
            return (cpuChars[attackerIdx], playerChars[targetIdx]);
        }

        private async UniTask<(CardView attacker, CardView target)> WaitForPlayerBattleAttackAsync(CancellationToken ct)
        {
            _battleAttackTcs = new UniTaskCompletionSource<(CardView, CardView)>();

            List<(CardView card, AttackArrowManipulator manip)> manipulators =
                new List<(CardView, AttackArrowManipulator)>();

            foreach (CardView charCard in _playerFieldView.Characters)
            {
                CardView capturedChar = charCard;
                AttackArrowManipulator arrowManip = new AttackArrowManipulator(_dragLayer);
                arrowManip.CanStart = () => _gameModel.Phase == TurnPhase.Battle;
                arrowManip.OnAttackTarget = (worldPos) =>
                {
                    CardView targetChar = _opponentFieldView.TryGetCardAt(worldPos);
                    if (targetChar != null && targetChar.Data is CharacterCardData && !targetChar.IsFaceDown)
                    {
                        _battleAttackTcs?.TrySetResult((capturedChar, targetChar));
                        return true;
                    }
                    return false;
                };
                charCard.AddManipulator(arrowManip);
                manipulators.Add((charCard, arrowManip));
            }

            ShowActionButtons();
            UpdateStagedButtons(false);

            if (_playerFieldView.Characters.Count == 0)
            {
                _battleAttackTcs.TrySetResult((null, null));
            }
            else if (_opponentFieldView.Characters.Count == 0)
            {
                // 相手にキャラがいない → 最高ATKのキャラでデッキ直撃（target=null）
                CardView bestAttacker = _playerFieldView.Characters[0];
                foreach (CardView c in _playerFieldView.Characters)
                {
                    if (c.Data.Attack > bestAttacker.Data.Attack)
                    {
                        bestAttacker = c;
                    }
                }
                _battleAttackTcs.TrySetResult((bestAttacker, null));
            }

            try
            {
                return await _battleAttackTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _battleAttackTcs = null;
                foreach ((CardView card, AttackArrowManipulator manip) in manipulators)
                {
                    manip.ClearArrow();
                    card.RemoveManipulator(manip);
                }
                HideActionButtons();
            }
        }

        private async UniTask ApplyBattleEndMillAsync(CancellationToken ct)
        {
            bool localFirst = _localHasPriority;

            DeckView firstDeck = localFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView firstGrave = localFirst ? _opponentGraveyardView : _playerGraveyardView;
            int firstValue = localFirst ? _localBattleEndMillValue : _opponentBattleEndMillValue;

            DeckView secondDeck = localFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView secondGrave = localFirst ? _playerGraveyardView : _opponentGraveyardView;
            int secondValue = localFirst ? _opponentBattleEndMillValue : _localBattleEndMillValue;

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

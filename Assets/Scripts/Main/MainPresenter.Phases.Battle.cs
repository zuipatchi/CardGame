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

        private async UniTask RunBattlePhaseAsync(bool priorityUsed, CancellationToken ct)
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

            // 素早さ同値で優先権を行使した場合：演出後に優先権を移譲
            if (priorityUsed)
            {
                bool localUsedPriority = _localHasPriority;
                await PlayPriorityCoinTransferAsync(localUsedPriority, ct);
                _localHasPriority = !_localHasPriority;
                UpdatePriorityCoinUI();
            }

            bool isLocalFirst = _gameModel.IsLocalTurn;
            List<CardView> firstCards = isLocalFirst ? playerCards : opponentCards;
            DeckView firstDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView firstGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;
            List<CardView> secondCards = isLocalFirst ? opponentCards : playerCards;
            DeckView secondDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView secondGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            UniTask[] firstFlipTasks = firstCards.Select(c => c.FlipAsync(ct)).ToArray();
            await UniTask.WhenAll(firstFlipTasks);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            List<CardView> playerSkill = playerCards.Where(c => c.Data is SkillCardData).ToList();
            List<CardView> opponentSkill = opponentCards.Where(c => c.Data is SkillCardData).ToList();

            bool playerHasAttackingChar = _playerCharacterSlot.CurrentCard != null;
            bool opponentHasAttackingChar = _opponentCharacterSlot.CurrentCard != null;

            BattleCalculator.SideBattleStats playerStats = BattleCalculator.Calculate(
                playerSkill, _playerAtkBoost, playerHasAttackingChar, _playerCharacterSlot.Attack);
            BattleCalculator.SideBattleStats opponentStats = BattleCalculator.Calculate(
                opponentSkill, _opponentAtkBoost, opponentHasAttackingChar, _opponentCharacterSlot.Attack);

            int effectivePlayerDef = playerHasAttackingChar ? _playerCharacterSlot.Defense + _playerDefBoost : 0;
            int effectiveOpponentDef = opponentHasAttackingChar ? _opponentCharacterSlot.Defense + _opponentDefBoost : 0;
            int damageToOpponent = Mathf.Max(0, playerStats.ATK - effectiveOpponentDef);
            int damageToPlayer = Mathf.Max(0, opponentStats.ATK - effectivePlayerDef);

            CharacterSlotView firstTarget = isLocalFirst ? _opponentCharacterSlot : _playerCharacterSlot;
            VisualElement firstAtkOverlay = isLocalFirst ? _playerAtkCounterOverlay : _opponentAtkCounterOverlay;
            Label firstAtkLabel = isLocalFirst ? _playerAtkCounterLabel : _opponentAtkCounterLabel;
            int firstAtk = isLocalFirst ? playerStats.ATK : opponentStats.ATK;
            int firstDamage = isLocalFirst ? damageToOpponent : damageToPlayer;
            int firstEffectiveDef = isLocalFirst ? effectiveOpponentDef : effectivePlayerDef;
            DeckView firstTargetDeck = isLocalFirst ? _opponentDeckView : _playerDeckView;
            GraveyardView firstTargetGraveyard = isLocalFirst ? _opponentGraveyardView : _playerGraveyardView;

            CharacterSlotView secondTarget = isLocalFirst ? _playerCharacterSlot : _opponentCharacterSlot;
            VisualElement secondAtkOverlay = isLocalFirst ? _opponentAtkCounterOverlay : _playerAtkCounterOverlay;
            Label secondAtkLabel = isLocalFirst ? _opponentAtkCounterLabel : _playerAtkCounterLabel;
            int secondAtk = isLocalFirst ? opponentStats.ATK : playerStats.ATK;
            int secondDamage = isLocalFirst ? damageToPlayer : damageToOpponent;
            int secondEffectiveDef = isLocalFirst ? effectivePlayerDef : effectiveOpponentDef;
            DeckView secondTargetDeck = isLocalFirst ? _playerDeckView : _opponentDeckView;
            GraveyardView secondTargetGraveyard = isLocalFirst ? _playerGraveyardView : _opponentGraveyardView;

            // 先攻のコスト払い → ATKカウントアップ
            if (firstAtk > 0)
            {
                string firstCostClass = isLocalFirst ? "turn-announcement-label--cost" : "turn-announcement-label--cost-opponent";
                await PayBattleAtkCostsAsync(firstCards, firstDeck, firstGraveyard, firstCostClass, ct);
                if (_isGameOver) return;
            }

            await PlaySingleSideAtkCounterAsync(firstAtkOverlay, firstAtkLabel, firstAtk, firstTarget, firstEffectiveDef, ct);

            // 1人目（先攻）の攻撃
            if (firstAtk > 0)
            {
                await PlayAttackIconAsync(firstAtkOverlay, firstTarget, firstAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", firstAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
            }
            firstAtkOverlay.style.display = DisplayStyle.None;
            firstTarget.DefOverlay.style.display = DisplayStyle.None;

            if (firstAtk > 0 && firstDamage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", firstTarget, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
            }

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
                List<UniTask> flyTasks = new List<UniTask>();
                if (firstDestroyedChar != null)
                {
                    flyTasks.Add(FlyToGraveyardAsync(firstDestroyedChar, firstDestroyedFromRect, firstTargetGraveyard, ct,
                        DamageIconAppearDuration + DamageIconHoldDuration, DamageIconFlyDuration));
                }
                flyTasks.Add(PlayDamageNumberFlyAsync(firstDamage, firstTarget.worldBound.center, firstTargetDeck, ct));
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

            if (secondCards.Count > 0)
            {
                UniTask[] secondFlipTasks = secondCards.Select(c => c.FlipAsync(ct)).ToArray();
                await UniTask.WhenAll(secondFlipTasks);
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
            }

            // 後攻のコスト払い → ATKカウントアップ
            if (secondAtk > 0)
            {
                string secondCostClass = isLocalFirst ? "turn-announcement-label--cost-opponent" : "turn-announcement-label--cost";
                await PayBattleAtkCostsAsync(secondCards, secondDeck, secondGraveyard, secondCostClass, ct);
                if (_isGameOver) return;
            }

            await PlaySingleSideAtkCounterAsync(secondAtkOverlay, secondAtkLabel, secondAtk, secondTarget, secondEffectiveDef, ct);

            // 2人目（後攻）の攻撃
            if (secondAtk > 0)
            {
                await PlayAttackIconAsync(secondAtkOverlay, secondTarget, secondAtk, ct);
            }
            else
            {
                await PlayFloatingLabelAsync("NO ATTACK", "no-attack-label", secondAtkOverlay, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
            }
            secondAtkOverlay.style.display = DisplayStyle.None;
            secondTarget.DefOverlay.style.display = DisplayStyle.None;

            if (secondAtk > 0 && secondDamage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", secondTarget, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
            }

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
                List<UniTask> flyTasks = new List<UniTask>();
                if (secondDestroyedChar != null)
                {
                    flyTasks.Add(FlyToGraveyardAsync(secondDestroyedChar, secondDestroyedFromRect, secondTargetGraveyard, ct,
                        DamageIconAppearDuration + DamageIconHoldDuration, DamageIconFlyDuration));
                }
                flyTasks.Add(PlayDamageNumberFlyAsync(secondDamage, secondTarget.worldBound.center, secondTargetDeck, ct));
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

        // ─── 戦闘フェーズ ヘルパー ───────────────────────────────────────

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct, delay, duration);
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

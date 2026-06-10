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

        }

        // ─── キャラ登場時効果 ────────────────────────────────────────────────
        // 通常配置（ローカル PlaceChar / 相手カードプレイ）で配置確定したキャラの
        // EffectTrigger == OnEnter 効果を発動する。Switch / Evolve 配置は対象外。
        private UniTask ResolveCharacterEnterEffectAsync(CardView placedChar, bool isLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(placedChar, CharacterEffectTrigger.OnEnter, isLocal, ct);
        }

        // ─── キャラ攻撃時効果 ────────────────────────────────────────────────
        // キャラ攻撃・ハート攻撃の攻撃宣言時に、攻撃側キャラの EffectTrigger == OnAttack 効果を発動する。
        private UniTask ResolveCharacterAttackEffectAsync(CardView attacker, bool isLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(attacker, CharacterEffectTrigger.OnAttack, isLocal, ct);
        }

        // 指定トリガーのキャラ効果を発動する。既存のイベント効果解決処理（演出 + 適用）を流用する。
        private async UniTask ResolveCharacterTriggeredEffectAsync(CardView sourceCard, CharacterEffectTrigger trigger, bool isLocal, CancellationToken ct)
        {
            if (sourceCard == null || sourceCard.Data is not CharacterCardData charData)
            {
                return;
            }

            if (charData.EffectTrigger != trigger || charData.EffectType == CardEventType.None)
            {
                return;
            }

            switch (charData.EffectType)
            {
                case CardEventType.Draw:
                    await PlayDrawEffectAsync(sourceCard, charData.EffectValue, ct);
                    await ApplyDrawEffectAsync(charData.EffectValue, isLocal, ct);
                    break;
                case CardEventType.BanishChar:
                {
                    FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
                    GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;
                    if (targetField.Characters.Count == 0)
                    {
                        break;
                    }
                    CardView banishTarget = targetField.Characters[0];
                    await PlayBanishCharEffectAsync(banishTarget, ct);
                    Rect fromRect = banishTarget.worldBound;
                    targetField.RemoveCard(banishTarget);
                    await FlyCardToDestAsync(banishTarget, fromRect, targetGraveyard, ct);
                    targetGraveyard.AddCard(banishTarget);
                    break;
                }
                case CardEventType.DamageAllEnemies:
                    await ApplyDamageAllEnemiesAsync(charData.EffectValue, isLocal, ct);
                    break;
            }
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
                case CardEventType.Evolve:
                    await ApplyEvolveEffectAsync(isLocal, ct);
                    break;
                case CardEventType.DamageAllEnemies:
                    await ApplyDamageAllEnemiesAsync(data.EventValue, isLocal, ct);
                    break;
            }
        }

        // 発動側から見た敵フィールドのキャラ全員に同時にダメージを与え、HP 0 以下のキャラを破壊する
        private async UniTask ApplyDamageAllEnemiesAsync(int damage, bool isLocal, CancellationToken ct)
        {
            if (damage <= 0)
            {
                return;
            }

            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            // 破壊で Characters が変化するためスナップショットを取る
            List<CardView> targets = new List<CardView>(targetField.Characters);

            // 敵フィールド中央に AoE パーティクル演出を再生（敵キャラがいなくても再生）。
            // 同時に全敵へダメージ数値＋HP揺れを適用する
            List<UniTask> hitTasks = new List<UniTask>();
            hitTasks.Add(PlayAreaDamageEffectAsync(targetField, ct));
            foreach (CardView target in targets)
            {
                hitTasks.Add(ApplyDamageToCharAsync(target, damage, ct));
            }
            await UniTask.WhenAll(hitTasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            // HP 0 以下になったキャラをまとめて破壊
            List<UniTask> destroyTasks = new List<UniTask>();
            foreach (CardView target in targets)
            {
                if (target.CurrentHp <= 0 && targetField.Contains(target))
                {
                    destroyTasks.Add(DestroyCharToGraveyardAsync(target, targetField, targetGraveyard, ct));
                }
            }
            await UniTask.WhenAll(destroyTasks);
        }

        private async UniTask ApplyDamageToCharAsync(CardView target, int damage, CancellationToken ct)
        {
            await PlayHitDamageEffectAsync(target, damage, ct);
            await target.TakeDamageAsync(damage, ct);
        }

        private async UniTask DestroyCharToGraveyardAsync(CardView target, FieldView field, GraveyardView graveyard, CancellationToken ct)
        {
            await PlayCharDestroyEffectAsync(target, ct);
            Rect fromRect = target.worldBound;
            field.RemoveCard(target);
            await FlyToGraveyardAsync(target, fromRect, graveyard, ct);
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
                if (placed != null)
                {
                    OnCardPlayed(placed.Data, playedByLocal: true);
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
                    OnCardPlayed(cardData, playedByLocal: false);
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
                    OnCardPlayed(newChar.Data, playedByLocal: false);
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
                    OnCardPlayed(newChar.Data, playedByLocal: true);
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
                    OnCardPlayed(cardData, playedByLocal: false);
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
                    OnCardPlayed(newChar.Data, playedByLocal: false);
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

            // ドロー演出完了後、次の処理へ進む前に少し待つ
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);

            CheckBlueWin(isLocalDeck: isLocal);
        }
    }
}

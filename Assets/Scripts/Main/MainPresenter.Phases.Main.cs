using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using Main.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── メインフェーズ ──────────────────────────────────────────────

        private async UniTask RunMainPhaseAsync(CancellationToken ct)
        {
            UpdatePhaseIndicator(TurnPhase.Main);

            bool isLocalTurn = _gameModel.IsLocalTurn;

            await PlayAnnouncementAsync("メインフェーズ", "turn-announcement-label--main", ct);

            if (isLocalTurn)
            {
                MainPhaseAction action = await WaitForPlayerMainActionAsync(ct);
                string[] costCardIds = await ExecuteLocalMainActionAsync(action, ct);

                // コスト支払い完了後に送信（相手ドロー通知はロスト防止のため送信前に登録）
                if (_isOnline)
                {
                    _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                    _hasPreDrawTask = true;
                    _networkGameService.SendMainAction(ToNetworkAction(action, costCardIds));
                }
            }
            else if (_isOnline)
            {
                UniTask<NetworkGameService.MainActionData> receiveTask;
                if (_hasPreMainActionTask)
                {
                    receiveTask = _preMainActionReceiveTask;
                    _hasPreMainActionTask = false;
                }
                else
                {
                    receiveTask = _networkGameService.WaitForOpponentMainActionAsync(ct);
                }

                NetworkGameService.MainActionData networkAction = await receiveTask.AttachExternalCancellation(ct);
                await ExecuteOnlineOpponentMainActionAsync(networkAction, ct);
            }
            else
            {
                MainPhaseAction action = CpuChooseMainAction();
                await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);
                await ExecuteCpuMainActionAsync(action, ct);
            }
        }

        // ─── アクション実行（ローカル） ──────────────────────────────────

        private async UniTask<string[]> ExecuteLocalMainActionAsync(MainPhaseAction action, CancellationToken ct)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                    return await PayHandCostAsync(action._card, _handView, _playerGraveyardView, isLocalPlayer: true, ct);
                case MainPhaseActionType.PlayEvent:
                {
                    string[] costIds = await PayHandCostAsync(action._card, _handView, _playerGraveyardView, isLocalPlayer: true, ct);
                    await ResolveSingleCardAsync(action._card, ct);
                    return costIds;
                }
                case MainPhaseActionType.Attack:
                    await ExecuteAttackAsync(action._attacker, action._target, isLocal: true, ct);
                    return Array.Empty<string>();
                default:
                    await PlayAnnouncementAsync("パス", "turn-announcement-label--pass", ct);
                    return Array.Empty<string>();
            }
        }

        // ─── アクション実行（CPU） ───────────────────────────────────────

        private async UniTask ExecuteCpuMainActionAsync(MainPhaseAction action, CancellationToken ct)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                {
                    CardData cardData = action._card.Data;
                    Rect fromRect = action._card.worldBound;
                    _opponentHandView.RemoveCard(action._card);
                    CardView fieldCard = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(fieldCard, fromRect, _opponentFieldView, ct);
                    _opponentFieldView.PlaceCard(fieldCard);
                    await PayHandCostAsync(fieldCard, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct);
                    break;
                }
                case MainPhaseActionType.PlayEvent:
                {
                    CardData cardData = action._card.Data;
                    Rect fromRect = action._card.worldBound;
                    _opponentHandView.RemoveCard(action._card);
                    CardView eventCard = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                    await FlyCardToDestAsync(eventCard, fromRect, _opponentFieldView, ct);
                    _opponentFieldView.PlaceCard(eventCard);
                    await PayHandCostAsync(eventCard, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct);
                    await ResolveSingleCardAsync(eventCard, ct);
                    break;
                }
                case MainPhaseActionType.Attack:
                    await ExecuteAttackAsync(action._attacker, action._target, isLocal: false, ct);
                    break;
                default:
                    await PlayAnnouncementAsync("パス", "turn-announcement-label--pass", ct);
                    break;
            }
        }

        // ─── アクション実行（オンライン相手） ───────────────────────────

        private async UniTask ExecuteOnlineOpponentMainActionAsync(NetworkGameService.MainActionData networkAction, CancellationToken ct)
        {
            switch (networkAction.ActionType)
            {
                case NetworkGameService.MainActionType.PlaceChar:
                {
                    if (_cardDatabase.TryGet(networkAction.CardId, out CardData cardData))
                    {
                        IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                        Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentFieldView.worldBound;
                        if (hand.Count > 0)
                        {
                            _opponentHandView.RemoveCard(hand[0]);
                        }
                        CardView fieldCard = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                        await FlyCardToDestAsync(fieldCard, fromRect, _opponentFieldView, ct);
                        _opponentFieldView.PlaceCard(fieldCard);
                        await PayHandCostAsync(fieldCard, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct, costCardIds: networkAction.CostCardIds);
                    }
                    break;
                }
                case NetworkGameService.MainActionType.PlayEvent:
                {
                    if (_cardDatabase.TryGet(networkAction.CardId, out CardData cardData))
                    {
                        IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                        Rect fromRect = hand.Count > 0 ? hand[0].worldBound : _opponentFieldView.worldBound;
                        if (hand.Count > 0)
                        {
                            _opponentHandView.RemoveCard(hand[0]);
                        }
                        CardView eventCard = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                        await FlyCardToDestAsync(eventCard, fromRect, _opponentFieldView, ct);
                        _opponentFieldView.PlaceCard(eventCard);
                        await PayHandCostAsync(eventCard, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct, costCardIds: networkAction.CostCardIds);
                        await ResolveSingleCardAsync(eventCard, ct);
                    }
                    break;
                }
                case NetworkGameService.MainActionType.Attack:
                {
                    CardView attacker = _opponentFieldView.Characters
                        .FirstOrDefault(c => c.Data.Id == networkAction.AttackerId);
                    CardView target = networkAction.TargetId != null
                        ? _playerFieldView.Characters.FirstOrDefault(c => c.Data.Id == networkAction.TargetId)
                        : null;
                    await ExecuteAttackAsync(attacker, target, isLocal: false, ct);
                    break;
                }
                default:
                    await PlayAnnouncementAsync("パス", "turn-announcement-label--pass", ct);
                    break;
            }
        }

        // ─── 攻撃実行（一方向のみ） ──────────────────────────────────────

        private async UniTask ExecuteAttackAsync(CardView attacker, CardView target, bool isLocal, CancellationToken ct)
        {
            if (attacker == null)
            {
                return;
            }

            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            DeckView targetDeck = isLocal ? _opponentDeckView : _playerDeckView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            VisualElement targetVE = target != null ? (VisualElement)target : targetDeck;
            await PlayCardChargeAsync(attacker, targetVE, ct);

            int atk = attacker.Data.Attack;
            int def = target?.Data.Defense ?? 0;
            int damage = Mathf.Max(0, atk - def);

            if (damage == 0 && target != null)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", target, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                ResetTurnState();
                return;
            }

            bool charWillBeDestroyed = target != null && damage >= target.Data.Hp;

            if (damage > 0)
            {
                Vector2 damageOrigin = target?.worldBound.center ?? targetDeck.worldBound.center;
                Rect deckRect = targetDeck.worldBound;
                await PlayDamageNumberFlyAsync(damage, damageOrigin, targetDeck, ct);
                List<CardView> damageCards = targetDeck.TakeFromTop(damage);
                await PlayDeckDamageAsync(damageCards, deckRect, targetGraveyard, targetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

                if (damageCards.Count < damage)
                {
                    _isGameOver = true;
                    OnGameEnd(isLocal);
                    ResetTurnState();
                    return;
                }
            }

            if (charWillBeDestroyed && targetField.Contains(target))
            {
                await PlayCharDestroyEffectAsync(target, ct);
                Rect destroyedFromRect = target.worldBound;
                targetField.RemoveCard(target);
                await FlyToGraveyardAsync(target, destroyedFromRect, targetGraveyard, ct);
            }

            // 毒チェック（ダメージを受けたキャラが毒状態なら破壊）
            bool targetPoisoned = isLocal ? _opponentPoisoned : _playerPoisoned;
            if (!_isGameOver && targetPoisoned && damage > 0 && target != null && targetField.Contains(target))
            {
                await PlayPoisonEffectAsync(target, ct);
                await PlayCharDestroyEffectAsync(target, ct);
                Rect poisonedFromRect = target.worldBound;
                targetField.RemoveCard(target);
                await FlyToGraveyardAsync(target, poisonedFromRect, targetGraveyard, ct);
            }

            ResetTurnState();

            if (!_isGameOver && (_localBattleEndMillValue > 0 || _opponentBattleEndMillValue > 0))
            {
                await ApplyBattleEndMillAsync(isLocal, ct);
            }
        }

        // ─── バトルエンドミル ─────────────────────────────────────────────

        private async UniTask ApplyBattleEndMillAsync(bool isLocalAttacker, CancellationToken ct)
        {
            DeckView firstDeck = isLocalAttacker ? _opponentDeckView : _playerDeckView;
            GraveyardView firstGrave = isLocalAttacker ? _opponentGraveyardView : _playerGraveyardView;
            int firstValue = isLocalAttacker ? _localBattleEndMillValue : _opponentBattleEndMillValue;

            DeckView secondDeck = isLocalAttacker ? _playerDeckView : _opponentDeckView;
            GraveyardView secondGrave = isLocalAttacker ? _playerGraveyardView : _opponentGraveyardView;
            int secondValue = isLocalAttacker ? _opponentBattleEndMillValue : _localBattleEndMillValue;

            if (!_isGameOver && firstValue > 0)
            {
                int firstMilled = await PlayPoisonMillAsync(firstDeck, firstGrave, firstValue, ct);
                if (firstMilled < firstValue)
                {
                    _isGameOver = true;
                    OnGameEnd(isLocalAttacker);
                    return;
                }
            }

            if (!_isGameOver && secondValue > 0)
            {
                int secondMilled = await PlayPoisonMillAsync(secondDeck, secondGrave, secondValue, ct);
                if (secondMilled < secondValue)
                {
                    _isGameOver = true;
                    OnGameEnd(!isLocalAttacker);
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

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct, delay, duration);
            graveyard.AddCard(card);
        }

        private void ResetTurnState()
        {
            _playerPoisoned = false;
            _opponentPoisoned = false;
            _localBattleEndMillValue = 0;
            _opponentBattleEndMillValue = 0;
        }

        // ─── CPU アクション選択 ───────────────────────────────────────────

        private MainPhaseAction CpuChooseMainAction()
        {
            IReadOnlyList<CardView> cpuChars = _opponentFieldView.Characters;
            IReadOnlyList<CardView> playerChars = _playerFieldView.Characters;

            // キャラがいれば攻撃
            if (cpuChars.Count > 0)
            {
                (int atkIdx, int tgtIdx) = CpuAgent.ChooseBattleAttack(
                    cpuChars.Select(c => c.Data).ToList(),
                    playerChars.Select(c => c.Data).ToList());
                if (atkIdx >= 0)
                {
                    CardView attacker = cpuChars[atkIdx];
                    CardView target = tgtIdx >= 0 && tgtIdx < playerChars.Count ? playerChars[tgtIdx] : null;
                    return new MainPhaseAction
                    {
                        _actionType = MainPhaseActionType.Attack,
                        _attacker = attacker,
                        _target = target
                    };
                }
            }

            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            List<CardData> handData = cpuHand.Select(c => c.Data).ToList();

            // キャラカードを出す
            int charIdx = CpuAgent.ChooseCharacterSetCardIndex(handData);
            if (charIdx >= 0)
            {
                return new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.PlaceChar,
                    _card = cpuHand[charIdx]
                };
            }

            // イベントカードを使う
            int eventIdx = CpuAgent.ChooseEventCardIndex(handData);
            if (eventIdx >= 0)
            {
                return new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.PlayEvent,
                    _card = cpuHand[eventIdx]
                };
            }

            return new MainPhaseAction { _actionType = MainPhaseActionType.Pass };
        }

        // ─── プレイヤー入力待ち ────────────────────────────────────────────

        private async UniTask<MainPhaseAction> WaitForPlayerMainActionAsync(CancellationToken ct)
        {
            _mainActionTcs = new UniTaskCompletionSource<MainPhaseAction>();
            _mainStagedCard = null;
            _mainStagedType = MainPhaseActionType.None;

            List<(CardView card, AttackArrowManipulator manip)> manipulators =
                new List<(CardView, AttackArrowManipulator)>();

            foreach (CardView charCard in _playerFieldView.Characters)
            {
                CardView capturedChar = charCard;
                AttackArrowManipulator arrowManip = new AttackArrowManipulator(_dragLayer);
                arrowManip.CanStart = () => _gameModel.Phase == TurnPhase.Main && _mainStagedCard == null;
                arrowManip.OnAttackTarget = (worldPos) =>
                {
                    CardView targetChar = _opponentFieldView.TryGetCardAt(worldPos);
                    if (targetChar != null && targetChar.Data is CharacterCardData && !targetChar.IsFaceDown)
                    {
                        _mainActionTcs?.TrySetResult(new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = capturedChar,
                            _target = targetChar
                        });
                        return true;
                    }
                    if (_opponentDeckView.worldBound.Contains(worldPos))
                    {
                        _mainActionTcs?.TrySetResult(new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = capturedChar,
                            _target = null
                        });
                        return true;
                    }
                    return false;
                };
                charCard.AddManipulator(arrowManip);
                manipulators.Add((charCard, arrowManip));
            }

            ShowActionButtons();
            UpdateStagedButtons(false);
            RefreshHandHighlights();

            try
            {
                return await _mainActionTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _mainActionTcs = null;
                _mainStagedCard = null;
                _mainStagedType = MainPhaseActionType.None;
                HideActionButtons();
                RefreshHandHighlights();
                foreach ((CardView card, AttackArrowManipulator manip) in manipulators)
                {
                    manip.ClearArrow();
                    card.RemoveManipulator(manip);
                }
            }
        }

        // ─── ネットワークアクション変換 ─────────────────────────────────────

        private static NetworkGameService.MainActionData ToNetworkAction(MainPhaseAction action, string[] costCardIds = null)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                    return NetworkGameService.MainActionData.PlaceChar(action._card.Data.Id, costCardIds);
                case MainPhaseActionType.PlayEvent:
                    return NetworkGameService.MainActionData.PlayEvent(action._card.Data.Id, costCardIds);
                case MainPhaseActionType.Attack:
                    return NetworkGameService.MainActionData.Attack(
                        action._attacker?.Data.Id,
                        action._target?.Data.Id);
                default:
                    return NetworkGameService.MainActionData.Pass();
            }
        }
    }
}

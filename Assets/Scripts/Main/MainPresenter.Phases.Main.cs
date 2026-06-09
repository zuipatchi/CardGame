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
                string[] costCardIds = await ExecuteLocalMainCostAsync(action, ct);

                // コスト支払い完了後すぐに送信（解決アニメーション前に相手へ通知することでテンポ改善）
                // 相手ドロー通知はロスト防止のため送信前に登録
                if (_isOnline)
                {
                    _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                    _hasPreDrawTask = true;
                    _networkGameService.SendMainAction(ToNetworkAction(action, costCardIds));
                }

                await ExecuteLocalMainResolveAsync(action, ct);
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

        private async UniTask<string[]> ExecuteLocalMainCostAsync(MainPhaseAction action, CancellationToken ct)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                case MainPhaseActionType.PlayEvent:
                    return await PayHandCostAsync(action._card, _handView, _playerGraveyardView, isLocalPlayer: true, ct);
                default:
                    return Array.Empty<string>();
            }
        }

        private async UniTask ExecuteLocalMainResolveAsync(MainPhaseAction action, CancellationToken ct)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlayEvent:
                    await ResolveSingleCardAsync(action._card, ct);
                    break;
                case MainPhaseActionType.Attack:
                    await ExecuteAttackAsync(action._attacker, action._target, isLocal: true, ct);
                    break;
                case MainPhaseActionType.PlaceChar:
                    break;
                default:
                    await PlayAnnouncementAsync("パス", "turn-announcement-label--pass", ct);
                    break;
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
            if (attacker == null || target == null)
            {
                return;
            }

            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            await PlayCardChargeAsync(attacker, target, ct);

            int atk = attacker.Data.Attack;
            int damage = atk;

            if (damage == 0)
            {
                await PlayFloatingLabelAsync("NO DAMAGE", "guard-label", target, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                return;
            }

            if (damage > 0)
            {
                target?.TakeDamage(damage);
            }

            bool charWillBeDestroyed = target != null && target.CurrentHp <= 0;

            if (charWillBeDestroyed && targetField.Contains(target))
            {
                await PlayCharDestroyEffectAsync(target, ct);
                Rect destroyedFromRect = target.worldBound;
                targetField.RemoveCard(target);
                await FlyToGraveyardAsync(target, destroyedFromRect, targetGraveyard, ct);
            }
        }

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct, delay, duration);
            graveyard.AddCard(card);
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
                    return false;
                };
                charCard.AddManipulator(arrowManip);
                manipulators.Add((charCard, arrowManip));
            }

            ShowActionButtons();
            UpdateStagedButtons(false);

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

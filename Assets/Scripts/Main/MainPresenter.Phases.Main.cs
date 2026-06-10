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
                    OnCardPlayed(action._card.Data, playedByLocal: true);
                    await ResolveSingleCardAsync(action._card, ct);
                    break;
                case MainPhaseActionType.Attack:
                    if (action._targetsHeart)
                    {
                        await ExecuteHeartAttackAsync(action._attacker, isLocal: true, ct);
                    }
                    else
                    {
                        await ExecuteAttackAsync(action._attacker, action._target, isLocal: true, ct);
                    }
                    break;
                case MainPhaseActionType.PlaceChar:
                    OnCardPlayed(action._card.Data, playedByLocal: true);
                    await ResolveCharacterEnterEffectAsync(action._card, isLocal: true, ct);
                    break;
                default:
                    await PlayPassAnnouncementAsync(ct);
                    break;
            }
        }

        // ─── アクション実行（CPU） ───────────────────────────────────────

        private async UniTask ExecuteCpuMainActionAsync(MainPhaseAction action, CancellationToken ct)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                    await ExecuteOpponentCardPlayAsync(action._card.Data, action._card, isEvent: false, costCardIds: null, ct);
                    break;
                case MainPhaseActionType.PlayEvent:
                    await ExecuteOpponentCardPlayAsync(action._card.Data, action._card, isEvent: true, costCardIds: null, ct);
                    break;
                case MainPhaseActionType.Attack:
                    if (action._targetsHeart)
                    {
                        await ExecuteHeartAttackAsync(action._attacker, isLocal: false, ct);
                    }
                    else
                    {
                        await ExecuteAttackAsync(action._attacker, action._target, isLocal: false, ct);
                    }
                    break;
                default:
                    await PlayPassAnnouncementAsync(ct);
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
                        CardView handCard = hand.Count > 0 ? hand[0] : null;
                        await ExecuteOpponentCardPlayAsync(cardData, handCard, isEvent: false, costCardIds: networkAction.CostCardIds, ct);
                    }
                    break;
                }
                case NetworkGameService.MainActionType.PlayEvent:
                {
                    if (_cardDatabase.TryGet(networkAction.CardId, out CardData cardData))
                    {
                        IReadOnlyList<CardView> hand = _opponentHandView.Cards;
                        CardView handCard = hand.Count > 0 ? hand[0] : null;
                        await ExecuteOpponentCardPlayAsync(cardData, handCard, isEvent: true, costCardIds: networkAction.CostCardIds, ct);
                    }
                    break;
                }
                case NetworkGameService.MainActionType.Attack:
                {
                    CardView attacker = _opponentFieldView.Characters
                        .FirstOrDefault(c => c.Data.Id == networkAction.AttackerId);
                    if (networkAction.TargetsHeart)
                    {
                        await ExecuteHeartAttackAsync(attacker, isLocal: false, ct);
                        break;
                    }
                    CardView target = networkAction.TargetId != null
                        ? _playerFieldView.Characters.FirstOrDefault(c => c.Data.Id == networkAction.TargetId)
                        : null;
                    await ExecuteAttackAsync(attacker, target, isLocal: false, ct);
                    break;
                }
                default:
                    await PlayPassAnnouncementAsync(ct);
                    break;
            }
        }

        // ─── 相手カード配置・イベント実行（CPU / オンライン共通） ────────

        private async UniTask ExecuteOpponentCardPlayAsync(CardData cardData, CardView handCard, bool isEvent, string[] costCardIds, CancellationToken ct)
        {
            Rect fromRect = handCard != null ? handCard.worldBound : _opponentFieldView.worldBound;
            if (handCard != null)
            {
                _opponentHandView.RemoveCard(handCard);
            }
            CardView playedCard = new CardView(_cardStore.CardTemplate, cardData, _cardStore.CardBack, faceDown: false, isOpponent: true);
            await FlyCardToDestAsync(playedCard, fromRect, _opponentFieldView, ct);
            _opponentFieldView.PlaceCard(playedCard);
            OnCardPlayed(cardData, playedByLocal: false);
            await PayHandCostAsync(playedCard, _opponentHandView, _opponentGraveyardView, isLocalPlayer: false, ct, costCardIds: costCardIds);
            if (isEvent)
            {
                await ResolveSingleCardAsync(playedCard, ct);
            }
            else
            {
                await ResolveCharacterEnterEffectAsync(playedCard, isLocal: false, ct);
            }
        }

        private async UniTask PlayPassAnnouncementAsync(CancellationToken ct)
        {
            await PlayAnnouncementAsync("パス", "turn-announcement-label--pass", ct);
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

            await ResolveCharacterAttackEffectAsync(attacker, isLocal, ct);

            await PlayCardChargeAsync(attacker, target, ct);

            int atk = attacker.Data.Attack;
            int damage = atk;

            if (damage == 0)
            {
                await PlayShieldBlockEffectAsync(target, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                return;
            }

            if (damage > 0)
            {
                if (_hitEffectPrefab != null)
                {
                    await PlayParticleAtCardAsync(target, _hitEffectPrefab, ct);
                }
                await PlayHitDamageEffectAsync(target, damage, ct);
                await target.TakeDamageAsync(damage, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
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

        // ─── プレイ時トリガー（勝利条件の武装）──────────────────────────────

        // 青属性の勝利条件（自デッキ0で勝利）の武装状態
        private bool _playerBlueWinArmed;
        private bool _opponentBlueWinArmed;

        // カードのプレイ（キャラ配置 or イベント使用）時に呼ぶ。赤＝ハート出現、青＝デッキ0勝利の武装
        private void OnCardPlayed(CardData playedCard, bool playedByLocal)
        {
            // 赤属性: プレイした側の攻撃対象ハートを出現させる（一度出たら永続）
            if (HeartRule.ActivatesHearts(playedCard))
            {
                LifeHeartsView hearts = playedByLocal ? _opponentLifeHearts : _playerLifeHearts;
                hearts.Activate();
            }

            // 青属性: プレイした側の「自デッキ0で勝利」を武装する（一度武装したら永続）
            if (BlueRule.ActivatesBlueWin(playedCard))
            {
                if (playedByLocal && !_playerBlueWinArmed)
                {
                    _playerBlueWinArmed = true;
                    _playerDeckView.ShowBlueWinIcon();
                }
                else if (!playedByLocal && !_opponentBlueWinArmed)
                {
                    _opponentBlueWinArmed = true;
                    _opponentDeckView.ShowBlueWinIcon();
                }
            }
        }

        // 青属性の勝利条件判定：武装済みでデッキが0枚なら、そのプレイヤーの勝利。
        // デッキが減る各ドロー処理の直後に呼ぶ。勝利が成立したら true を返す。
        private bool CheckBlueWin(bool isLocalDeck)
        {
            if (_isGameOver)
            {
                return false;
            }

            bool armed = isLocalDeck ? _playerBlueWinArmed : _opponentBlueWinArmed;
            DeckView deck = isLocalDeck ? _playerDeckView : _opponentDeckView;
            if (!BlueRule.IsBlueWin(armed, deck.Count))
            {
                return false;
            }

            _isGameOver = true;
            OnGameEnd(playerWins: isLocalDeck, winAttribute: CardAttribute.Blue);
            return true;
        }

        // ハート攻撃：突進 → パーティクル → ハート削除。3個目を破壊した側が勝利
        private async UniTask ExecuteHeartAttackAsync(CardView attacker, bool isLocal, CancellationToken ct)
        {
            LifeHeartsView targetHearts = isLocal ? _opponentLifeHearts : _playerLifeHearts;
            if (attacker == null || !targetHearts.CanBeAttacked)
            {
                return;
            }

            await ResolveCharacterAttackEffectAsync(attacker, isLocal, ct);

            VisualElement targetHeart = targetHearts.PeekNextHeart();
            await PlayCardChargeAsync(attacker, targetHeart, ct);

            if (!HeartRule.CanBreakHeart(attacker.Data.Attack))
            {
                await PlayShieldBlockEffectAsync(targetHeart, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                return;
            }

            if (_charDestroyEffectPrefab != null)
            {
                // ハートはカードより小さいためパーティクルを縮小して再生する
                const float HeartEffectScale = 0.4f;
                await PlayParticleAtUiPositionAsync(targetHeart, targetHeart.worldBound.center, _charDestroyEffectPrefab, ct, scale: HeartEffectScale);
            }
            targetHearts.RemoveHeart();
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            if (targetHearts.Remaining == 0 && !_isGameOver)
            {
                _isGameOver = true;
                OnGameEnd(playerWins: isLocal, winAttribute: CardAttribute.Red);
            }
        }

        // ─── CPU アクション選択 ───────────────────────────────────────────

        private MainPhaseAction CpuChooseMainAction()
        {
            IReadOnlyList<CardView> cpuChars = _opponentFieldView.Characters;
            IReadOnlyList<CardView> playerChars = _playerFieldView.Characters;

            // プレイヤーのハートが攻撃可能なら最優先で攻撃（勝利に直結）
            if (_playerLifeHearts.CanBeAttacked && cpuChars.Count > 0)
            {
                int heartAttackerIdx = CpuAgent.ChooseHeartAttacker(cpuChars.Select(c => c.Data).ToList());
                if (heartAttackerIdx >= 0)
                {
                    return new MainPhaseAction
                    {
                        _actionType = MainPhaseActionType.Attack,
                        _attacker = cpuChars[heartAttackerIdx],
                        _targetsHeart = true
                    };
                }
            }

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
                    if (_opponentLifeHearts.ContainsPoint(worldPos))
                    {
                        _mainActionTcs?.TrySetResult(new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = capturedChar,
                            _targetsHeart = true
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
                        action._target?.Data.Id,
                        action._targetsHeart);
                default:
                    return NetworkGameService.MainActionData.Pass();
            }
        }
    }
}

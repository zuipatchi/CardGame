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

        // メインフェーズ：アクティブプレイヤーは EndButton（オンラインは Pass）を押すまで
        // 枚数・攻撃回数の制限なく行動できる。各キャラの攻撃はターンに1回まで、
        // このターンに登場したキャラ（召喚酔い）は攻撃できない。
        private async UniTask RunMainPhaseAsync(CancellationToken ct)
        {
            bool isLocalTurn = _gameModel.IsLocalTurn;

            _attackedThisTurn.Clear();

            if (isLocalTurn)
            {
                // ターン開始時に場にいるキャラは召喚酔いが明ける
                ReseasonChars(_playerFieldView, _playerSeasonedChars);
                await RunLocalMainLoopAsync(ct);
            }
            else if (_isOnline)
            {
                ReseasonChars(_opponentFieldView, _opponentSeasonedChars);
                await RunOnlineOpponentMainLoopAsync(ct);
            }
            else
            {
                ReseasonChars(_opponentFieldView, _opponentSeasonedChars);
                await RunCpuMainLoopAsync(ct);
            }
        }

        // ─── メインフェーズ ループ（ローカル） ─────────────────────────────
        private async UniTask RunLocalMainLoopAsync(CancellationToken ct)
        {
            while (!_isGameOver)
            {
                MainPhaseAction action = await WaitForPlayerMainActionAsync(ct);
                if (action._actionType == MainPhaseActionType.Pass)
                {
                    // ターン終了。オンラインは Pass を終了の合図として相手へ送る
                    if (_isOnline)
                    {
                        // 相手ターンのドロー通知をロストしないよう送信前に登録。
                        // ただし ExtraTurn 発動時は次も自分のターンが続き相手のドローを待たないため登録しない。
                        if (!_extraTurnPending)
                        {
                            _preDrawReceiveTask = _networkGameService.WaitForOpponentDrawAsync(ct);
                            _hasPreDrawTask = true;
                        }
                        _networkGameService.SendMainAction(NetworkGameService.MainActionData.Pass());
                    }
                    break;
                }

                string[] costCardIds = await ExecuteLocalMainCostAsync(action, ct);

                if (_isOnline)
                {
                    _networkGameService.SendMainAction(ToNetworkAction(action, costCardIds));
                }

                if (action._actionType == MainPhaseActionType.Attack && action._attacker != null)
                {
                    _attackedThisTurn.Add(action._attacker);
                }

                await ExecuteLocalMainResolveAsync(action, ct);
            }
        }

        // ─── メインフェーズ ループ（オンライン相手） ───────────────────────
        private async UniTask RunOnlineOpponentMainLoopAsync(CancellationToken ct)
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

            while (!_isGameOver)
            {
                NetworkGameService.MainActionData networkAction = await receiveTask.AttachExternalCancellation(ct);
                if (networkAction.IsPassed)
                {
                    break;
                }

                // 連続送信を取りこぼさないよう、解決アニメーションの前に次の受信を登録する
                receiveTask = _networkGameService.WaitForOpponentMainActionAsync(ct);
                await ExecuteOnlineOpponentMainActionAsync(networkAction, ct);
            }
        }

        // ─── メインフェーズ ループ（CPU） ──────────────────────────────────
        private async UniTask RunCpuMainLoopAsync(CancellationToken ct)
        {
            while (!_isGameOver)
            {
                MainPhaseAction action = CpuChooseMainAction();
                if (action._actionType == MainPhaseActionType.Pass)
                {
                    break;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(CpuThinkSeconds), cancellationToken: ct);

                if (action._actionType == MainPhaseActionType.Attack && action._attacker != null)
                {
                    _attackedThisTurn.Add(action._attacker);
                }

                await ExecuteCpuMainActionAsync(action, ct);
            }
        }

        // ─── 召喚酔い・攻撃済み 管理 ───────────────────────────────────────

        // ターン開始時に場にいる全キャラを「召喚酔いなし」として記録する。
        // このターンに登場したキャラはここに含まれず、攻撃できない（召喚酔い）。
        private void ReseasonChars(FieldView field, HashSet<CardView> seasoned)
        {
            seasoned.Clear();
            foreach (CardView c in field.Characters)
            {
                seasoned.Add(c);
            }
        }

        // キャラが攻撃可能か：このターン未攻撃 かつ（召喚酔いしていない または 速攻持ち）
        private bool CanCharAttack(CardView card, FieldView ownerField)
        {
            if (_attackedThisTurn.Contains(card))
            {
                return false;
            }
            if (IsHaste(card))
            {
                return true;
            }
            HashSet<CardView> seasoned = ownerField == _playerFieldView ? _playerSeasonedChars : _opponentSeasonedChars;
            return seasoned.Contains(card);
        }

        // 速攻持ちの表向きキャラかどうか：召喚酔いせず出したターンから攻撃できる
        private static bool IsHaste(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData characterData && characterData.Haste;
        }

        // ─── 守護（タウント）─────────────────────────────────────────────────

        // 表向きの守護持ちキャラかどうか
        private static bool IsGuardian(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData characterData && characterData.Guardian;
        }

        // フィールドに守護持ちキャラがいるか。いる場合、相手の攻撃はその守護キャラに限定される
        private static bool HasGuardian(FieldView field)
        {
            foreach (CardView card in field.Characters)
            {
                if (IsGuardian(card))
                {
                    return true;
                }
            }
            return false;
        }

        // ─── 飛行 ─────────────────────────────────────────────────────────

        // 表向きの飛行持ちキャラかどうか：守護を無視して攻撃でき、飛行キャラからしか攻撃されない
        private static bool IsFlying(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData characterData && characterData.Flying;
        }

        // 攻撃者 attacker が防御側フィールドのキャラ target を攻撃できるか（飛行・守護を考慮）。
        // ・飛行を持つ target は、飛行を持つ attacker からしか攻撃されない
        // ・守護がいる場合は守護持ちにしか攻撃できないが、飛行を持つ attacker は守護を無視できる
        private bool CanAttackChar(CardView attacker, CardView target, FieldView defenderField)
        {
            if (target == null || target.IsFaceDown || target.Data is not CharacterCardData)
            {
                return false;
            }
            if (IsFlying(target) && !IsFlying(attacker))
            {
                return false;
            }
            if (!IsFlying(attacker) && HasGuardian(defenderField) && !IsGuardian(target))
            {
                return false;
            }
            return true;
        }

        // 攻撃者 attacker が防御側のハートを攻撃できるか。守護がいるとハートは狙えないが、飛行を持つ attacker は守護を無視できる
        private bool CanAttackHeart(CardView attacker, FieldView defenderField)
        {
            return IsFlying(attacker) || !HasGuardian(defenderField);
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
                // 撃破されたキャラの OnDestroy を発動（攻撃対象の所有者は攻撃側の相手 = !isLocal）
                await FireOnDestroyEffectAsync(target, !isLocal, ct);
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

            // 緑属性: プレイした側の勝利点表示を出現させる（一度出たら永続）
            if (GreenRule.ActivatesVictoryPoints(playedCard))
            {
                VictoryPointsView victoryPoints = playedByLocal ? _playerVictoryPoints : _opponentVictoryPoints;
                victoryPoints.Activate();
            }
        }

        // 緑属性の勝利条件：プレイした側の勝利点に加算し、WinPoints 到達でそのプレイヤーの勝利。
        // GainVictoryPoints 効果の解決時に呼ぶ。
        // 勝利点表示のアクティブ化は緑カードのプレイ（OnCardPlayed）のみが行う。ここでは Activate しない。
        private async UniTask AddVictoryPoints(int amount, bool toLocal, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }

            VictoryPointsView victoryPoints = toLocal ? _playerVictoryPoints : _opponentVictoryPoints;
            int from = victoryPoints.Points;
            victoryPoints.AddPoints(amount);

            // 数字カウントアップ + 「+N」フローティング + メダルの弾み演出
            await PlayVictoryPointGainAsync(victoryPoints, from, victoryPoints.Points, amount, ct);

            if (!_isGameOver && GreenRule.IsGreenWin(victoryPoints.Points))
            {
                _isGameOver = true;
                OnGameEnd(playerWins: toLocal, winAttribute: CardAttribute.Green);
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

            // このターンまだ攻撃でき、召喚酔いしていないキャラのみが攻撃の選択肢
            List<CardView> availableAttackers = cpuChars.Where(c => CanCharAttack(c, _opponentFieldView)).ToList();

            // プレイヤーのハートが攻撃可能なら最優先で攻撃（勝利に直結）。
            // 守護がいるとハートは狙えないが、飛行を持つ攻撃者は守護を無視してハートを攻撃できる
            if (_playerLifeHearts.CanBeAttacked)
            {
                List<CardView> heartAttackers = availableAttackers
                    .Where(a => CanAttackHeart(a, _playerFieldView)).ToList();
                if (heartAttackers.Count > 0)
                {
                    int heartAttackerIdx = CpuAgent.ChooseHeartAttacker(heartAttackers.Select(c => c.Data).ToList());
                    if (heartAttackerIdx >= 0)
                    {
                        return new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = heartAttackers[heartAttackerIdx],
                            _targetsHeart = true
                        };
                    }
                }
            }

            // キャラ攻撃：飛行・守護を考慮し、合法な対象を持つ攻撃者の中で最高ATK→対象は最低ATKを選ぶ
            CardView battleAttacker = null;
            CardView battleTarget = null;
            foreach (CardView attacker in availableAttackers)
            {
                CardView lowestTarget = null;
                foreach (CardView candidate in playerChars)
                {
                    if (!CanAttackChar(attacker, candidate, _playerFieldView))
                    {
                        continue;
                    }
                    if (lowestTarget == null || candidate.Data.Attack < lowestTarget.Data.Attack)
                    {
                        lowestTarget = candidate;
                    }
                }
                if (lowestTarget == null)
                {
                    continue;
                }
                if (battleAttacker == null || attacker.Data.Attack > battleAttacker.Data.Attack)
                {
                    battleAttacker = attacker;
                    battleTarget = lowestTarget;
                }
            }
            if (battleAttacker != null)
            {
                return new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.Attack,
                    _attacker = battleAttacker,
                    _target = battleTarget
                };
            }

            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            List<CardData> handData = cpuHand.Select(c => c.Data).ToList();

            // キャラカードを出す（フィールドが満杯のときは出さない）
            if (!_opponentFieldView.IsCharactersFull)
            {
                int charIdx = CpuAgent.ChooseCharacterSetCardIndex(handData);
                if (charIdx >= 0)
                {
                    return new MainPhaseAction
                    {
                        _actionType = MainPhaseActionType.PlaceChar,
                        _card = cpuHand[charIdx]
                    };
                }
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
                // 攻撃済み・召喚酔いのキャラには攻撃矢印を付けない
                if (!CanCharAttack(charCard, _playerFieldView))
                {
                    continue;
                }
                // このターン攻撃できるキャラをハイライト
                charCard.AddToClassList("attackable-char");
                CardView capturedChar = charCard;
                AttackArrowManipulator arrowManip = new AttackArrowManipulator(_dragLayer);
                arrowManip.CanStart = () => _gameModel.Phase == TurnPhase.Main && _mainStagedCard == null
                    && CanCharAttack(capturedChar, _playerFieldView);
                arrowManip.OnAttackTarget = (worldPos) =>
                {
                    CardView targetChar = _opponentFieldView.TryGetCardAt(worldPos);
                    if (targetChar != null && targetChar.Data is CharacterCardData && !targetChar.IsFaceDown)
                    {
                        // 飛行を持つキャラは飛行を持つキャラからしか攻撃されない
                        if (IsFlying(targetChar) && !IsFlying(capturedChar))
                        {
                            ShowToast("飛行を持つキャラには飛行でしか攻撃できません");
                            return false;
                        }
                        // 相手フィールドに守護がいる場合は守護持ちキャラにしか攻撃できない（飛行の攻撃者は無視）
                        if (!CanAttackChar(capturedChar, targetChar, _opponentFieldView))
                        {
                            ShowToast("守護を持つキャラを攻撃してください");
                            return false;
                        }
                        _mainActionTcs?.TrySetResult(new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = capturedChar,
                            _target = targetChar
                        });
                        return true;
                    }
                    if (_opponentLifeHearts.ContainsWorldPoint(worldPos))
                    {
                        if (!CanAttackHeart(capturedChar, _opponentFieldView))
                        {
                            ShowToast("守護を持つキャラを攻撃してください");
                            return false;
                        }
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

            // 攻撃できる自キャラが1体以上いる場合のみ、攻撃可能な相手キャラ・ハートをハイライト
            List<CardView> attackers = manipulators.Select(m => m.card).ToList();
            List<CardView> highlightedTargets = attackers.Count > 0
                ? HighlightAttackTargets(attackers)
                : new List<CardView>();

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
                    card.RemoveFromClassList("attackable-char");
                }
                foreach (CardView target in highlightedTargets)
                {
                    target.RemoveFromClassList("attack-target-char");
                }
                _opponentLifeHearts.SetAttackTargetHighlight(false);
            }
        }

        // 攻撃可能な相手キャラ・ハートをハイライトし、ハイライトした相手キャラのリストを返す（クリーンアップ用）。
        // 「いずれかの攻撃可能な自キャラ（attackers）が攻撃できる対象」をハイライトする（守護・飛行を考慮）。
        private List<CardView> HighlightAttackTargets(IReadOnlyList<CardView> attackers)
        {
            List<CardView> highlighted = new List<CardView>();
            foreach (CardView enemyChar in _opponentFieldView.Characters)
            {
                if (enemyChar.IsFaceDown || enemyChar.Data is not CharacterCardData)
                {
                    continue;
                }
                bool anyCanAttack = false;
                foreach (CardView attacker in attackers)
                {
                    if (CanAttackChar(attacker, enemyChar, _opponentFieldView))
                    {
                        anyCanAttack = true;
                        break;
                    }
                }
                if (anyCanAttack)
                {
                    enemyChar.AddToClassList("attack-target-char");
                    highlighted.Add(enemyChar);
                }
            }
            bool anyCanHitHeart = false;
            foreach (CardView attacker in attackers)
            {
                if (CanAttackHeart(attacker, _opponentFieldView))
                {
                    anyCanHitHeart = true;
                    break;
                }
            }
            if (anyCanHitHeart && _opponentLifeHearts.CanBeAttacked)
            {
                _opponentLifeHearts.SetAttackTargetHighlight(true);
            }
            return highlighted;
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

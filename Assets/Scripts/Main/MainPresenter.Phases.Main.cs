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
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasHaste;
        }

        // ─── 守護（タウント）─────────────────────────────────────────────────

        // 表向きの守護持ちキャラかどうか
        private static bool IsGuardian(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasGuardian;
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
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasFlying;
        }

        // ─── 防人（対空ガード＋守護）─────────────────────────────────────

        // 表向きの防人持ちキャラかどうか。防人は守護も兼ねる：飛行はこのキャラを優先して攻撃せねばならず、
        // 非飛行も守護同様このキャラを優先する。さらにこのキャラ自身は飛行に攻撃できる
        private static bool IsSakimori(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasSakimori;
        }

        // フィールドに防人持ちキャラがいるか。いる場合、飛行を持つ相手の攻撃はその防人キャラに限定される
        private static bool HasSakimori(FieldView field)
        {
            foreach (CardView card in field.Characters)
            {
                if (IsSakimori(card))
                {
                    return true;
                }
            }
            return false;
        }

        // 攻撃者 attacker が防御側フィールドのキャラ target を攻撃できるか（飛行・守護・防人を考慮）。
        // ・飛行を持つ target は、飛行か防人を持つ attacker からしか攻撃されない
        // ・飛行を持つ attacker は、相手フィールドに防人がいる間は防人を優先して攻撃しなければならない（守護は無視）
        // ・飛行を持たない attacker は、相手フィールドに守護か防人がいる間はそのいずれかを優先して攻撃しなければならない
        private bool CanAttackChar(CardView attacker, CardView target, FieldView defenderField)
        {
            if (target == null || target.IsFaceDown || target.Data is not CharacterCardData)
            {
                return false;
            }
            if (IsFlying(target) && !IsFlying(attacker) && !IsSakimori(attacker))
            {
                return false;
            }
            if (IsFlying(attacker) && HasSakimori(defenderField) && !IsSakimori(target))
            {
                return false;
            }
            if (!IsFlying(attacker) && (HasGuardian(defenderField) || HasSakimori(defenderField))
                && !IsGuardian(target) && !IsSakimori(target))
            {
                return false;
            }
            return true;
        }

        // 攻撃者 attacker が防御側のデッキを直接攻撃できるか。
        // ・飛行を持つ attacker は守護を無視できるが、相手フィールドに防人がいる間はデッキを直接狙えない
        // ・飛行を持たない attacker は守護か防人がいるとデッキを狙えない
        private bool CanAttackDeck(CardView attacker, FieldView defenderField)
        {
            if (IsFlying(attacker))
            {
                return !HasSakimori(defenderField);
            }
            return !HasGuardian(defenderField) && !HasSakimori(defenderField);
        }

        // 守護/防人によって攻撃対象が強制されたときの案内トースト文言を、攻撃者種別と相手フィールドの状況から決める
        private string ForcedTargetMessage(CardView attacker, FieldView defenderField)
        {
            // 飛行は防人のみを優先する（守護は無視できる）
            if (IsFlying(attacker))
            {
                return "防人を持つキャラを攻撃してください";
            }
            bool hasGuardian = HasGuardian(defenderField);
            bool hasSakimori = HasSakimori(defenderField);
            if (hasGuardian && hasSakimori)
            {
                return "守護か防人を持つキャラを攻撃してください";
            }
            return hasSakimori ? "防人を持つキャラを攻撃してください" : "守護を持つキャラを攻撃してください";
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
                    if (action._targetsDeck)
                    {
                        await ExecuteDeckAttackAsync(action._attacker, isLocal: true, ct);
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
                    if (action._targetsDeck)
                    {
                        await ExecuteDeckAttackAsync(action._attacker, isLocal: false, ct);
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
                    if (networkAction.TargetsDeck)
                    {
                        await ExecuteDeckAttackAsync(attacker, isLocal: false, ct);
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

        // ─── 共通の勝利条件 ──────────────────────────────────────────────

        // カードのプレイ（キャラ配置 or イベント使用）時に呼ぶ。
        // キャラを配置した直後にフィールドのキャラ数勝利（8体）を判定する。
        private void OnCardPlayed(CardData playedCard, bool playedByLocal)
        {
            CheckFieldCharsWin(playedByLocal);
        }

        // フィールドのキャラ数勝利条件：自フィールドにキャラを規定数（WinRule.FieldCharsToWin）
        // 同時に並べた側の勝利。キャラ配置・召喚の直後（OnCardPlayed）に呼ぶ。
        private void CheckFieldCharsWin(bool isLocalField)
        {
            if (_isGameOver)
            {
                return;
            }

            FieldView field = isLocalField ? _playerFieldView : _opponentFieldView;
            if (!WinRule.IsFieldCharsWin(field.Characters.Count))
            {
                return;
            }

            _isGameOver = true;
            OnGameEnd(playerWins: isLocalField, winReason: WinReason.FieldChars);
        }

        // 勝利点勝利条件：加算した側の勝利点が規定値（WinRule.VictoryPointsToWin）に到達したら勝利。
        // 勝利点付帯値（VictoryPointBonus）や GainVPPerGreenGrave の解決時に呼ぶ。
        internal async UniTask AddVictoryPoints(int amount, bool toLocal, CancellationToken ct)
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

            if (!_isGameOver && WinRule.IsVictoryPointsWin(victoryPoints.Points))
            {
                _isGameOver = true;
                OnGameEnd(playerWins: toLocal, winReason: WinReason.VictoryPoints);
            }
        }

        // デッキ切れ勝利条件：あるプレイヤーがデッキを引き切って0枚になったら、その相手が勝利。
        // デッキが減る各ドロー処理の直後に呼ぶ。勝敗が成立したら true を返す。
        private bool CheckDeckOutWin(bool isLocalDeck)
        {
            if (_isGameOver)
            {
                return false;
            }

            DeckView deck = isLocalDeck ? _playerDeckView : _opponentDeckView;
            if (!WinRule.IsDeckOut(deck.Count))
            {
                return false;
            }

            _isGameOver = true;
            // 引き切った側（isLocalDeck）が敗北 → 相手が勝利
            OnGameEnd(playerWins: !isLocalDeck, winReason: WinReason.DeckOut);
            return true;
        }

        // ─── CPU アクション選択 ───────────────────────────────────────────

        private MainPhaseAction CpuChooseMainAction()
        {
            IReadOnlyList<CardView> cpuChars = _opponentFieldView.Characters;
            IReadOnlyList<CardView> playerChars = _playerFieldView.Characters;

            // このターンまだ攻撃でき、召喚酔いしていないキャラのみが攻撃の選択肢
            List<CardView> availableAttackers = cpuChars.Where(c => CanCharAttack(c, _opponentFieldView)).ToList();

            // 相手デッキを直接攻撃できる攻撃者（守護・飛行を考慮）。デッキが空なら対象外
            int playerDeckCount = _playerDeckView.Count;
            List<CardView> deckAttackers = playerDeckCount > 0
                ? availableAttackers.Where(a => CanAttackDeck(a, _playerFieldView)).ToList()
                : new List<CardView>();

            // lethal：1回の攻撃で相手デッキを引き切らせられるなら、デッキ攻撃で勝利を狙う
            CardView lethalDeckAttacker = null;
            foreach (CardView a in deckAttackers)
            {
                if (a.CurrentAttack >= playerDeckCount
                    && (lethalDeckAttacker == null || a.CurrentAttack > lethalDeckAttacker.CurrentAttack))
                {
                    lethalDeckAttacker = a;
                }
            }
            if (lethalDeckAttacker != null)
            {
                return new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.Attack,
                    _attacker = lethalDeckAttacker,
                    _targetsDeck = true
                };
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
                    if (lowestTarget == null || candidate.CurrentAttack < lowestTarget.CurrentAttack)
                    {
                        lowestTarget = candidate;
                    }
                }
                if (lowestTarget == null)
                {
                    continue;
                }
                if (battleAttacker == null || attacker.CurrentAttack > battleAttacker.CurrentAttack)
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

            // 合法なキャラ攻撃対象がいない場合、相手デッキを削る（chip mill。最高ATKの攻撃者で）
            if (deckAttackers.Count > 0)
            {
                CardView deckAttacker = deckAttackers.OrderByDescending(a => a.CurrentAttack).First();
                return new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.Attack,
                    _attacker = deckAttacker,
                    _targetsDeck = true
                };
            }

            IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
            List<CardData> handData = cpuHand.Select(c => c.Data).ToList();

            // キャラカードを出す（フィールドが満杯のときは出さない）。
            // コストを支払えないカードは選ばない（ローカルプレイヤーと同じく踏み倒し禁止）
            if (!_opponentFieldView.IsCharactersFull)
            {
                int charIdx = CpuAgent.ChooseCharacterSetCardIndex(handData, i => CpuCanAffordCost(handData, i));
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
            int eventIdx = CpuAgent.ChooseEventCardIndex(handData, i => CpuCanAffordCost(handData, i));
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

        // CPU が hand[playedIndex] のカードをプレイするコストを支払えるか。
        // 0コスト or NextCardCostFree 武装時は常に支払い可能。
        // それ以外は、自身を除いた手札の支払い可能量（各カードの CostPaymentValue 合計）がコスト以上なら支払い可能。
        // ローカルプレイヤーの CostCapacityExcluding と同じ判定で、コストの踏み倒しを防ぐ。
        private bool CpuCanAffordCost(IReadOnlyList<CardData> hand, int playedIndex)
        {
            CardData played = hand[playedIndex];
            if (played.Cost <= 0 || _opponentNextCardFree)
            {
                return true;
            }

            int capacity = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (i != playedIndex)
                {
                    capacity += hand[i].CostPaymentValue(played.Attribute);
                }
            }
            return capacity >= played.Cost;
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
                charCard.SetAttackHighlighted(true);
                CardView capturedChar = charCard;
                AttackArrowManipulator arrowManip = new AttackArrowManipulator(_dragLayer);
                arrowManip.CanStart = () => _gameModel.Phase == TurnPhase.Main && _mainStagedCard == null
                    && CanCharAttack(capturedChar, _playerFieldView);
                arrowManip.OnAttackTarget = (worldPos) =>
                {
                    CardView targetChar = _opponentFieldView.TryGetCardAt(worldPos);
                    if (targetChar != null && targetChar.Data is CharacterCardData && !targetChar.IsFaceDown)
                    {
                        // 飛行を持つキャラは飛行か防人を持つキャラからしか攻撃されない
                        if (IsFlying(targetChar) && !IsFlying(capturedChar) && !IsSakimori(capturedChar))
                        {
                            ShowToast("飛行を持つキャラには飛行か防人でしか攻撃できません");
                            return false;
                        }
                        // 守護・防人による対象強制：飛行は防人を、非飛行は守護か防人を優先して攻撃しなければならない
                        if (!CanAttackChar(capturedChar, targetChar, _opponentFieldView))
                        {
                            ShowToast(ForcedTargetMessage(capturedChar, _opponentFieldView));
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
                    // 相手デッキへの直接攻撃（ATK 枚をミル）
                    if (_opponentDeckView.Count > 0 && _opponentDeckView.worldBound.Contains(worldPos))
                    {
                        if (!CanAttackDeck(capturedChar, _opponentFieldView))
                        {
                            ShowToast(ForcedTargetMessage(capturedChar, _opponentFieldView));
                            return false;
                        }
                        _mainActionTcs?.TrySetResult(new MainPhaseAction
                        {
                            _actionType = MainPhaseActionType.Attack,
                            _attacker = capturedChar,
                            _targetsDeck = true
                        });
                        return true;
                    }
                    return false;
                };
                charCard.AddManipulator(arrowManip);
                manipulators.Add((charCard, arrowManip));
            }

            // 攻撃できる自キャラが1体以上いる場合のみ、攻撃可能な相手キャラをハイライト
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
                    card.SetAttackHighlighted(false);
                }
                foreach (CardView target in highlightedTargets)
                {
                    target.RemoveFromClassList("attack-target-char");
                    target.SetAttackHighlighted(false);
                }
                _opponentDeckView.RemoveFromClassList("deck-view--attack-target");
            }
        }

        // 攻撃可能な相手キャラ・相手デッキをハイライトし、ハイライトした相手キャラのリストを返す（クリーンアップ用）。
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
                    enemyChar.SetAttackHighlighted(true);
                    highlighted.Add(enemyChar);
                }
            }

            // 相手デッキを直接攻撃できる自キャラが1体以上いればデッキもハイライト
            bool anyCanHitDeck = false;
            foreach (CardView attacker in attackers)
            {
                if (CanAttackDeck(attacker, _opponentFieldView))
                {
                    anyCanHitDeck = true;
                    break;
                }
            }
            if (anyCanHitDeck && _opponentDeckView.Count > 0)
            {
                _opponentDeckView.AddToClassList("deck-view--attack-target");
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
                        action._targetsDeck);
                default:
                    return NetworkGameService.MainActionData.Pass();
            }
        }
    }
}

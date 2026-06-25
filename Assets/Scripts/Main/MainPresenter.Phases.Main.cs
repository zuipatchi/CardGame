using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Cpu;
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

            // 召喚酔いのスナップショット（ReseasonActivePlayerChars）はターン開始時効果より前に
            // RunTurnAsync で取得済み。ここではアクティブプレイヤーの全キャラをアンタップするだけ。
            if (isLocalTurn)
            {
                UntapField(_playerFieldView);
                await RunLocalMainLoopAsync(ct);
            }
            else if (_isOnline)
            {
                UntapField(_opponentFieldView);
                await RunOnlineOpponentMainLoopAsync(ct);
            }
            else if (_isTutorial)
            {
                UntapField(_opponentFieldView);
                await RunTutorialOpponentMainLoopAsync(ct);
            }
            else
            {
                UntapField(_opponentFieldView);
                await RunCpuMainLoopAsync(ct);
            }
        }

        // ─── メインフェーズ ループ（ローカル） ─────────────────────────────
        // 行動予約：攻撃矢印はフェーズ中ずっと常駐し（RefreshAttackInput）、解決アニメ中でも
        // 次の攻撃をドラッグすれば _queuedAttacks に積まれ、現在の解決が終わり次第順に処理する。
        // 解決アニメ中に End を押すと _endTurnQueued が立ち、キュー消化後にターンを終える。
        private async UniTask RunLocalMainLoopAsync(CancellationToken ct)
        {
            _queuedAttacks.Clear();
            _endTurnQueued = false;

            // 手番の制限時間カウントダウンを開始する（ターン終了時に finally で停止）。
            // チュートリアル中は急かさないようタイマーを動かさない。
            using CancellationTokenSource timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (!_isTutorial)
            {
                StartTurnTimer(timerCts.Token);
            }

            // チュートリアル：自分のメインフェーズ開始時にステップ案内（コーチ吹き出し）を出す。
            if (_isTutorial)
            {
                TutorialBeginPlayerMainPhase();
            }

            // アクションボタン領域と攻撃入力（矢印・対象ハイライト）をフェーズ中ずっと常駐させる。
            // 解決アニメ中も End（ターン終了予約）を押せるよう領域は出しっぱなしにする。
            ShowActionButtons();
            RefreshAttackInput();

            try
            {
                while (!_isGameOver)
                {
                    // 1. 予約された攻撃を先に消化する（解決のたびに盤面を貼り直す）
                    if (_queuedAttacks.Count > 0)
                    {
                        MainPhaseAction queued = _queuedAttacks.Dequeue();
                        ShowActionButtons();
                        UpdateResolvingButtons();
                        await ResolveQueuedAttackAsync(queued, ct);
                        RefreshAttackInput();
                        continue;
                    }

                    // 2. ターン終了が予約されていれば、キューを消化しきった今ターンを終える
                    if (_endTurnQueued)
                    {
                        await EndLocalTurnAsync(ct);
                        break;
                    }

                    // 3. プレイヤー入力を待つ
                    MainPhaseAction action = await WaitForPlayerMainActionAsync(ct);
                    if (action._actionType == MainPhaseActionType.Pass)
                    {
                        await EndLocalTurnAsync(ct);
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

                    // 即時アクションの解決中も攻撃入力を生かす（行動予約）。攻撃者の矢印を消し、
                    // End ボタン（ターン終了予約）を出してから解決アニメを再生する。
                    RefreshAttackInput();
                    ShowActionButtons();
                    UpdateResolvingButtons();
                    await ExecuteLocalMainResolveAsync(action, ct);

                    if (_isTutorial)
                    {
                        TutorialOnLocalActionResolved(action);
                    }

                    RefreshAttackInput();
                }
            }
            finally
            {
                timerCts.Cancel();
                _turnTimerView.Hide();
                TeardownAttackInput();
                HideActionButtons();
                _queuedAttacks.Clear();
                _endTurnQueued = false;
            }
        }

        // ローカルプレイヤーのターンを終了（パス）する。通常パスと予約ターン終了の両方から呼ぶ。
        private async UniTask EndLocalTurnAsync(CancellationToken ct)
        {
            if (_isTutorial)
            {
                TutorialOnLocalPass();
            }

            // 時間切れによるパスは「時間切れ！」を告知してから終了する
            if (_turnTimedOut)
            {
                await PlayAnnouncementAsync("時間切れ！", "turn-announcement-label--enemy", ct);
            }

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
        }

        // 予約された攻撃を1件解決する。実行直前に盤面を再検証し、無効なら静かに破棄（送信もしない）。
        private async UniTask ResolveQueuedAttackAsync(MainPhaseAction action, CancellationToken ct)
        {
            if (!IsQueuedAttackValid(action))
            {
                return;
            }

            // オンラインは実行直前に送る（インデックスはこの時点の盤面で算出するため、
            // 相手側の逐次解決と攻守の並びが一致する）。
            if (_isOnline)
            {
                _networkGameService.SendMainAction(ToNetworkAction(action, Array.Empty<string>()));
            }

            if (action._targetsDeck)
            {
                await ExecuteDeckAttackAsync(action._attacker, isLocal: true, ct);
            }
            else
            {
                await ExecuteAttackAsync(action._attacker, action._target, isLocal: true, ct);
            }
        }

        // 予約攻撃が今も実行可能か（攻撃者が場に残っている・対象が今も合法か）。
        private bool IsQueuedAttackValid(MainPhaseAction action)
        {
            CardView attacker = action._attacker;
            if (attacker == null || !_playerFieldView.Contains(attacker))
            {
                return false;
            }
            if (action._targetsDeck)
            {
                return CanAttackDeck(attacker, _opponentFieldView);
            }
            CardView target = action._target;
            if (target == null || !_opponentFieldView.Contains(target))
            {
                return false;
            }
            return CanAttackChar(attacker, target, _opponentFieldView);
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

        // ターン開始時（ターン開始時効果の発動前）に、アクティブプレイヤーの場のキャラを
        // 「召喚酔いなし」としてスナップショットする。ターン開始時効果（OnTurnStart 召喚など）で
        // 新規に登場したキャラはこの時点では場におらず、seasoned に含まれないため召喚酔いする。
        private void ReseasonActivePlayerChars()
        {
            if (_gameModel.IsLocalTurn)
            {
                ReseasonChars(_playerFieldView, _playerSeasonedChars);
            }
            else
            {
                ReseasonChars(_opponentFieldView, _opponentSeasonedChars);
            }
        }

        // 場にいる全キャラを「召喚酔いなし」として記録する。
        // このターンに登場したキャラはここに含まれず、攻撃できない（召喚酔い）。
        private void ReseasonChars(FieldView field, HashSet<CardView> seasoned)
        {
            seasoned.Clear();
            foreach (CardView c in field.Characters)
            {
                seasoned.Add(c);
            }
        }

        // ─── タップ／アンタップ ───────────────────────────────────────────

        // ターン開始時：アクティブプレイヤーの場の全キャラをアンタップ（縦に戻す）する。
        private static void UntapField(FieldView field)
        {
            foreach (CardView card in field.Characters)
            {
                card.SetTapped(false);
            }
        }

        // ターン終了時：アクティブプレイヤーの場の守護/防人を自動でタップ（横向き）にする。
        // 守護・防人は毎ターン終了時に横向きになり、アンタップは自分のターン開始時のため、
        // 相手ターン中は常にタップ済み＝常に攻撃対象になる。既にタップ済みなら演出をスキップ。
        private async UniTask AutoTapGuardiansAndSakimoriAsync(bool isLocalTurn, CancellationToken ct)
        {
            FieldView field = isLocalTurn ? _playerFieldView : _opponentFieldView;
            List<UniTask> taps = new List<UniTask>();
            foreach (CardView card in field.Characters)
            {
                if (!card.IsTapped && (IsGuardian(card) || IsSakimori(card)))
                {
                    taps.Add(card.SetTappedAsync(true, ct));
                }
            }
            if (taps.Count > 0)
            {
                await UniTask.WhenAll(taps);
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

        // ─── 強襲 ─────────────────────────────────────────────────────────

        // 表向きの強襲持ちキャラかどうか：アンタップ状態のキャラにも攻撃できる（タップ済み要件を無視する）
        private static bool IsAssault(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasAssault;
        }

        // ─── デッキ攻撃× ───────────────────────────────────────────────────

        // 表向きの「デッキ攻撃×」持ちキャラかどうか：このキャラ自身は相手デッキを直接攻撃（ミル）できない。
        // 制限を受けるのはこの能力を持つキャラだけで、他の味方キャラのデッキ攻撃には影響しない。
        private static bool IsNoDeckAttack(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasNoDeckAttack;
        }

        // ─── 射手 ─────────────────────────────────────────────────────────

        // 表向きの射手持ちキャラかどうか：飛行を持たない地上キャラながら飛行を持つ相手キャラを攻撃できる（対空攻撃）。
        // 防人と違い対空ガード（飛行に優先して狙われる壁）ではなく、純粋に飛行を殴れるだけの攻撃的能力。
        private static bool IsArcher(CardView card)
        {
            return card != null && !card.IsFaceDown && card.Data is CharacterCardData && card.HasArcher;
        }

        // ─── 防人（対空ガード）───────────────────────────────────────────

        // 表向きの防人持ちキャラかどうか。防人は対空ガード：飛行を持つ攻撃者はこのキャラを優先して攻撃せねばならない。
        // 非飛行の攻撃者は防人だけでは強制されない（地上の壁にはならない）。さらにこのキャラ自身は飛行に攻撃できる
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

        // 攻撃者 attacker が防御側フィールドのキャラ target を攻撃できるか（飛行・守護・防人・射手を考慮）。
        // ・飛行を持つ target は、飛行か防人か射手を持つ attacker からしか攻撃されない
        // ・飛行を持つ attacker は、相手フィールドに防人がいる間は防人を優先して攻撃しなければならない（守護は無視）
        // ・飛行を持たない attacker は、相手フィールドに守護がいる間は守護を優先して攻撃しなければならない
        //   （防人だけでは地上攻撃者を縛らない。守護がいるときも狙えるのは守護のみで、防人は対象に取れない）
        private bool CanAttackChar(CardView attacker, CardView target, FieldView defenderField)
        {
            if (target == null || target.IsFaceDown || target.Data is not CharacterCardData)
            {
                return false;
            }
            // タップ状態のキャラにしか攻撃できない（未タップ＝未行動のキャラは攻撃対象にならない）。
            // ただし強襲を持つ攻撃者はこの制限を無視し、アンタップのキャラにも攻撃できる。
            if (!target.IsTapped && !IsAssault(attacker))
            {
                return false;
            }
            if (IsFlying(target) && !IsFlying(attacker) && !IsSakimori(attacker) && !IsArcher(attacker))
            {
                return false;
            }
            if (IsFlying(attacker) && HasSakimori(defenderField) && !IsSakimori(target))
            {
                return false;
            }
            if (!IsFlying(attacker) && HasGuardian(defenderField)
                && !IsGuardian(target))
            {
                return false;
            }
            return true;
        }

        // 攻撃者 attacker が防御側のデッキを直接攻撃できるか。
        // ・「デッキ攻撃×」を持つ attacker 自身はデッキを直接攻撃できない（このキャラだけの制限）
        // ・飛行を持つ attacker は守護を無視できるが、相手フィールドに防人がいる間はデッキを直接狙えない
        // ・飛行を持たない attacker は守護がいるとデッキを狙えない（防人だけでは縛られない）
        private bool CanAttackDeck(CardView attacker, FieldView defenderField)
        {
            if (IsNoDeckAttack(attacker))
            {
                return false;
            }
            if (IsFlying(attacker))
            {
                return !HasSakimori(defenderField);
            }
            return !HasGuardian(defenderField);
        }

        // 守護/防人によって攻撃対象が強制されたときの案内トースト文言を、攻撃者種別と相手フィールドの状況から決める
        private string ForcedTargetMessage(CardView attacker, FieldView defenderField)
        {
            // 飛行は防人のみを優先する（守護は無視できる）
            if (IsFlying(attacker))
            {
                return "防人を持つキャラを攻撃してください";
            }
            // 非飛行は守護のみに縛られ、狙えるのも守護のみ（防人は対象に取れない）。
            return "守護を持つキャラを攻撃してください";
        }

        // デッキ攻撃が拒否されたときの案内トースト文言。
        // 攻撃者が「デッキ攻撃×」を持つ場合はその旨を、それ以外（守護・防人による対象強制）は対象強制メッセージを返す。
        private string DeckAttackBlockedMessage(CardView attacker, FieldView defenderField)
        {
            if (IsNoDeckAttack(attacker))
            {
                return "このキャラはデッキを攻撃できません";
            }
            return ForcedTargetMessage(attacker, defenderField);
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
                    // 攻撃者・対象はフィールドのインデックスで特定する（CardId だと同名カードを区別できない）。
                    // 攻撃側＝相手フィールド、対象＝自フィールド。並び順は両クライアントで同期されている。
                    CardView attacker = CharAtIndex(_opponentFieldView, networkAction.AttackerIndex);
                    if (networkAction.TargetsDeck)
                    {
                        await ExecuteDeckAttackAsync(attacker, isLocal: false, ct);
                        break;
                    }
                    CardView target = CharAtIndex(_playerFieldView, networkAction.TargetIndex);
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
            PlayFlavorVoiceAsync(playedCard).Forget();
        }

        // プレイされたカードのフレーバーテキスト読み上げ音声を再生する（自分・相手どちらのカードでも鳴らす）。
        // 音声は事前生成した WAV を Addressables からオンデマンドでロードする。
        // フレーバーが空、または音声が未生成のカードは無音（ロードは null を返す）。
        private async UniTaskVoid PlayFlavorVoiceAsync(CardData playedCard)
        {
            if (playedCard == null || string.IsNullOrEmpty(playedCard.FlavorText))
            {
                return;
            }

            AudioClip clip = await _flavorVoiceStore.LoadAsync(playedCard.Id);
            if (clip != null)
            {
                _soundPlayer.PlayVoice(clip);
            }
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

        // 相手の勝利点を amount 分下げる（ReduceEnemyVP の解決時に呼ぶ）。0未満にはしない（0でクランプ）。
        // isLocal=発動側が自分側か。下げる対象は発動側から見た相手プレイヤーの勝利点。
        // 固定値かつ同期済みの勝利点に対する操作のため、オンラインでも決定的に対称解決される（追加同期不要）。
        internal async UniTask ReduceEnemyVictoryPointsAsync(int amount, bool isLocal, CardView sourceCard, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }

            VictoryPointsView victoryPoints = isLocal ? _opponentVictoryPoints : _playerVictoryPoints;
            int from = victoryPoints.Points;
            // 勝利点は0未満にならないため、実際に下げられるのは現在値まで。相手が既に0点なら空振り。
            int reduced = Math.Min(amount, from);
            if (reduced <= 0)
            {
                return;
            }

            // 発動カード上に「勝利点低下 N」フローティングラベル（実際に下がった分を表示）
            await PlayFloatingLabelAsync($"勝利点低下 {reduced}", "vp-reduce-label", sourceCard, ct);

            victoryPoints.AddPoints(-reduced);

            // 相手の勝利点カウンターを数字カウントダウン + 「-N」フローティング + メダルの弾み演出
            await PlayVictoryPointLossAsync(victoryPoints, from, victoryPoints.Points, reduced, ct);
        }

        // デッキ切れ勝利条件（オーバーリミット）：デッキが0枚の状態でカードを引く／ミルしようとした瞬間に、
        // その本人が敗北＝相手が勝利。カードを1枚引く／ミルする「直前」に呼び、デッキが空なら敗北を成立させる。
        // デッキを0枚にした引き／ミルそのものでは負けない（その時点ではまだ空ではないため false を返す）。
        // 勝敗が成立したら true を返す。
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

        // オーバーリミット検出：デッキが初めて0枚になった瞬間（>0→0 の down-crossing）を検出し、
        // 新たに0枚へ落ちたら true を返す。デッキが減る各処理（ドロー・ミル）でカードを抜いた直後に呼ぶ。
        // デッキが1枚以上残っていればフラグを戻すため、Recover 等で補充された後に再び0枚へ落ちたとき再度検出する。
        // 告知はここでは行わず、呼び出し側が一連のドロー/ミル処理の最後に PlayOverLimitAnnouncementAsync で行う。
        // オーバーリミット直後に同じ処理内で敗北する場合（残り1枚に2ダメージ等）は告知しないため、判定と告知を分離する。
        private bool UpdateOverLimit(bool isLocalDeck)
        {
            DeckView deck = isLocalDeck ? _playerDeckView : _opponentDeckView;
            bool isEmpty = WinRule.IsDeckOut(deck.Count);

            if (isLocalDeck)
            {
                if (!isEmpty)
                {
                    _playerOverLimit = false;
                    return false;
                }
                if (_playerOverLimit)
                {
                    return false;
                }
                _playerOverLimit = true;
                return true;
            }

            if (!isEmpty)
            {
                _opponentOverLimit = false;
                return false;
            }
            if (_opponentOverLimit)
            {
                return false;
            }
            _opponentOverLimit = true;
            return true;
        }

        // ─── CPU アクション選択 ───────────────────────────────────────────

        private MainPhaseAction CpuChooseMainAction()
        {
            IReadOnlyList<CardView> cpuChars = _opponentFieldView.Characters;
            IReadOnlyList<CardView> playerChars = _playerFieldView.Characters;

            // このターンまだ攻撃でき、召喚酔いしていないキャラのみが攻撃の選択肢
            List<CardView> availableAttackers = cpuChars.Where(c => CanCharAttack(c, _opponentFieldView)).ToList();

            // 相手デッキを直接攻撃できる攻撃者（守護・飛行を考慮）。
            // オーバーリミットでは相手デッキが空（0枚）でもデッキ攻撃で finish できる（空デッキへのミル＝敗北）ため、
            // デッキ枚数では絞らない。
            int playerDeckCount = _playerDeckView.Count;
            List<CardView> deckAttackers = availableAttackers.Where(a => CanAttackDeck(a, _playerFieldView)).ToList();

            // lethal：1回の攻撃で相手を敗北させられるなら、デッキ攻撃で勝利を狙う。
            // オーバーリミットでは「0枚の状態でミルした瞬間」に敗北するため、ATK が相手デッキ枚数を
            // 「超える」必要がある（ちょうど引き切る ATK == 枚数 はデッキを0にするだけで負けない）。
            CardView lethalDeckAttacker = null;
            foreach (CardView a in deckAttackers)
            {
                if (a.CurrentAttack > playerDeckCount
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

            // 合法なキャラ攻撃対象がいない場合、相手デッキを削る（chip mill。最高ATKの攻撃者で）。
            // 空デッキ（0枚）への攻撃は lethal 側で処理済みのため、ここでは相手デッキにカードが残っている場合のみ。
            // （0枚を ATK 0 の攻撃者で無駄に殴って盾ブロックさせない）
            if (playerDeckCount > 0 && deckAttackers.Count > 0)
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
                int charIdx = CpuAgent.ChooseCharacterSetCardIndex(handData, i => CpuCanAffordCost(handData, i) && CpuMayPlayToField(handData[i]));
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
            int eventIdx = CpuAgent.ChooseEventCardIndex(handData, i => CpuCanAffordCost(handData, i) && CpuMayPlayToField(handData[i]));
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

        // 中級以上の CPU は、CostBoost／ダメージトリガー持ちのカードを場に出さず、コスト支払いに回す。
        // 初級はこの制限なしで、従来どおり支払える順に出す。
        private bool CpuMayPlayToField(CardData data)
        {
            if (_cpuDifficulty == CpuDifficulty.Beginner)
            {
                return true;
            }
            return !IsCostOnlyCard(data);
        }

        // CostBoost（コスト支払いで真価を発揮）またはダメージトリガー（デッキから墓地で無料発動）のカードか。
        // 中級以上の CPU はこれらを場に出さず、コスト専用として扱う。
        private static bool IsCostOnlyCard(CardData data)
        {
            if (data == null)
            {
                return false;
            }
            // ダメージトリガー：場に出すより、コストで墓地に送る／ミルを待つほうが噛み合う。
            if (data.TriggerOnGrave)
            {
                return true;
            }
            // CostBoost：コストとして使うと自属性のコストを倍化する。場に出す価値が薄い。
            if (data is CharacterCardData character)
            {
                return character.EffectTrigger == CharacterEffectTrigger.OnUsedAsCost
                    && character.EffectType == Main.Card.EventType.CostBoost;
            }
            if (data is EventCardData eventCard)
            {
                return eventCard.EventType == Main.Card.EventType.CostBoost;
            }
            return false;
        }

        // ─── プレイヤー入力待ち ────────────────────────────────────────────

        private async UniTask<MainPhaseAction> WaitForPlayerMainActionAsync(CancellationToken ct)
        {
            // 解決アニメーション中に時間切れになっていた場合は、入力を待たず即パスする
            if (_turnTimedOut)
            {
                return new MainPhaseAction { _actionType = MainPhaseActionType.Pass };
            }

            _mainActionTcs = new UniTaskCompletionSource<MainPhaseAction>();
            _mainStagedCard = null;
            _mainStagedType = MainPhaseActionType.None;

            // 攻撃矢印・対象ハイライトはフェーズ中ずっと常駐している（RefreshAttackInput）。
            // ここでは入力待ち中のボタン表示だけを整える。
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
            }
        }

        // ─── 攻撃入力（行動予約） ─────────────────────────────────────────

        // 攻撃矢印マニピュレータと対象ハイライトをローカルメインフェーズ中ずっと常駐させる。
        // 解決アニメ中も生かしておくことで、次の攻撃をドラッグして予約できる（CommitOrQueueAttack）。
        // 盤面が変わるたび（各解決の後・予約のたび）に呼び、攻撃可能なキャラ・対象を貼り直す。
        private void RefreshAttackInput()
        {
            // ドラッグ中の矢印は壊さない。他キャラの攻撃が解決して盤面を貼り直すときも、
            // 今引いている矢印はそのまま残す（残った分は _attackManipulators に保持される）。
            TeardownAttackInput(preserveDragging: true);

            // ターン終了予約済み・自分のターンでない・ゲーム終了時は攻撃入力を出さない
            if (_isGameOver || !_gameModel.IsLocalTurn || _endTurnQueued)
            {
                return;
            }

            foreach (CardView charCard in _playerFieldView.Characters)
            {
                // ドラッグ中で維持された矢印のキャラには、二重にマニピュレータを付けない
                if (_attackManipulators.Any(m => m.card == charCard))
                {
                    continue;
                }
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
                arrowManip.CanStart = () => _gameModel.Phase == TurnPhase.Main
                    && _gameModel.IsLocalTurn
                    && !_endTurnQueued
                    && _mainStagedCard == null
                    && !IsInteractiveSubInputActive()
                    && CanCharAttack(capturedChar, _playerFieldView);
                arrowManip.OnAttackTarget = (worldPos) => HandleAttackDrop(capturedChar, worldPos);
                charCard.AddManipulator(arrowManip);
                _attackManipulators.Add((charCard, arrowManip));
            }

            // 攻撃できる自キャラが1体以上いる場合のみ、攻撃可能な相手キャラをハイライト
            List<CardView> attackers = _attackManipulators.Select(m => m.card).ToList();
            _highlightedAttackTargets = attackers.Count > 0
                ? HighlightAttackTargets(attackers)
                : new List<CardView>();
        }

        // 常駐中の攻撃矢印マニピュレータ・対象ハイライトをすべて外す。
        // preserveDragging が true のときは、いま矢印をドラッグ中のマニピュレータだけは
        // 壊さずに残す（他キャラの攻撃解決後の盤面貼り直しで、引いている矢印を消さないため）。
        private void TeardownAttackInput(bool preserveDragging = false)
        {
            List<(CardView card, AttackArrowManipulator manip)> retained =
                new List<(CardView, AttackArrowManipulator)>();
            foreach ((CardView card, AttackArrowManipulator manip) in _attackManipulators)
            {
                if (preserveDragging && manip.IsDragging)
                {
                    // ドラッグ中の矢印・マニピュレータ・攻撃元ハイライトはそのまま維持する
                    retained.Add((card, manip));
                    continue;
                }
                manip.ClearArrow();
                card.RemoveManipulator(manip);
                card.RemoveFromClassList("attackable-char");
                card.SetAttackHighlighted(false);
            }
            _attackManipulators.Clear();
            _attackManipulators.AddRange(retained);

            foreach (CardView target in _highlightedAttackTargets)
            {
                target.RemoveFromClassList("attack-target-char");
                target.SetAttackHighlighted(false);
            }
            _highlightedAttackTargets.Clear();

            _opponentDeckView.RemoveFromClassList("deck-view--attack-target");
        }

        // 攻撃矢印のドロップ処理。対象が合法なら攻撃アクションを生成し、アイドル中なら即実行・
        // 解決アニメ中なら予約する（CommitOrQueueAttack）。戻り値は矢印を残すか（keepArrow）。
        private bool HandleAttackDrop(CardView attacker, Vector2 worldPos)
        {
            CardView targetChar = _opponentFieldView.TryGetCardAt(worldPos);
            if (targetChar != null && targetChar.Data is CharacterCardData && !targetChar.IsFaceDown)
            {
                // タップ状態のキャラにしか攻撃できない（強襲を持つ攻撃者はこの制限を無視できる）
                if (!targetChar.IsTapped && !IsAssault(attacker))
                {
                    ShowToast("タップ状態のキャラにしか攻撃できません");
                    return false;
                }
                // 飛行を持つキャラは飛行・防人・射手を持つキャラからしか攻撃されない
                if (IsFlying(targetChar) && !IsFlying(attacker) && !IsSakimori(attacker) && !IsArcher(attacker))
                {
                    ShowToast("飛行を持つキャラには飛行・防人・射手でしか攻撃できません");
                    return false;
                }
                // 守護・防人による対象強制：飛行は防人を、非飛行は守護を優先して攻撃しなければならない（非飛行は防人を対象に取れない）
                if (!CanAttackChar(attacker, targetChar, _opponentFieldView))
                {
                    ShowToast(ForcedTargetMessage(attacker, _opponentFieldView));
                    return false;
                }
                return CommitOrQueueAttack(attacker, new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.Attack,
                    _attacker = attacker,
                    _target = targetChar
                });
            }

            // 相手デッキへの直接攻撃（ATK 枚をミル）。オーバーリミットでは空デッキ（0枚）も対象にできる（ミル＝敗北）
            if (_opponentDeckView.worldBound.Contains(worldPos))
            {
                if (!CanAttackDeck(attacker, _opponentFieldView))
                {
                    ShowToast(DeckAttackBlockedMessage(attacker, _opponentFieldView));
                    return false;
                }
                return CommitOrQueueAttack(attacker, new MainPhaseAction
                {
                    _actionType = MainPhaseActionType.Attack,
                    _attacker = attacker,
                    _targetsDeck = true
                });
            }

            return false;
        }

        // アイドル中（入力待ち）なら即実行、解決アニメ中なら予約する。
        private bool CommitOrQueueAttack(CardView attacker, MainPhaseAction action)
        {
            if (_mainActionTcs != null)
            {
                // アイドル：従来どおり即実行（描いた矢印は直後の再構築で消える）
                _mainActionTcs.TrySetResult(action);
                return false;
            }

            // チュートリアルは台本ペース維持のため予約しない（アニメ中のドラッグは無視）
            if (_isTutorial)
            {
                return false;
            }

            // 解決アニメ中：予約。攻撃者を即「攻撃済み」にして矢印/ハイライトを消す（二重予約防止）。
            _queuedAttacks.Enqueue(action);
            _attackedThisTurn.Add(attacker);
            RefreshAttackInput();
            return false;
        }

        // 効果のターゲット選択など、攻撃ドラッグと競合する対話的サブ入力が待機中か。
        // これらの最中は攻撃ドラッグの開始を抑止する（複数選択ピッカーは全画面オーバーレイが
        // フィールドを覆うため、ここに含めなくてもドラッグ自体が始まらない）。
        private bool IsInteractiveSubInputActive()
        {
            return _costSelectionTcs != null
                || _evolveInput._tcs != null
                || _switchInput._tcs != null
                || _fieldCharSelectionTcs != null
                || _deckCardSelectionTcs != null
                || _enemyCharSelectionTcs != null
                || _allyCharSelectionTcs != null
                || _mulliganChoicePending;
        }

        // 解決アニメ中のボタン表示：End（ターン終了予約用）だけを出し、ステージ用ボタンは隠す。
        private void UpdateResolvingButtons()
        {
            bool showEnd = !_endTurnQueued && _mainStagedCard == null && !IsInteractiveSubInputActive();
            _endButton.style.display = showEnd ? DisplayStyle.Flex : DisplayStyle.None;
            _okButton.style.display = DisplayStyle.None;
            _backButton.style.display = DisplayStyle.None;
            _passButton.style.display = DisplayStyle.None;
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
                // チュートリアルで攻撃を禁じる相手（飛行チュートリアルの守護持ち）は強調しない。
                if (anyCanAttack && !IsTutorialForbiddenAttackTarget(enemyChar))
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
            // オーバーリミットでは空デッキ（0枚）もデッキ攻撃で finish できるためハイライトする
            if (anyCanHitDeck)
            {
                _opponentDeckView.AddToClassList("deck-view--attack-target");
            }

            return highlighted;
        }

        // ─── ネットワークアクション変換 ─────────────────────────────────────

        private NetworkGameService.MainActionData ToNetworkAction(MainPhaseAction action, string[] costCardIds = null)
        {
            switch (action._actionType)
            {
                case MainPhaseActionType.PlaceChar:
                    return NetworkGameService.MainActionData.PlaceChar(action._card.Data.Id, costCardIds);
                case MainPhaseActionType.PlayEvent:
                    return NetworkGameService.MainActionData.PlayEvent(action._card.Data.Id, costCardIds);
                case MainPhaseActionType.Attack:
                    // 攻撃者・対象はフィールドのインデックスで送る（CardId だと同名カードを区別できない）。
                    // 攻撃者＝自フィールド、対象＝相手フィールド。受信側では攻守が反転して解決される。
                    return NetworkGameService.MainActionData.Attack(
                        IndexOfChar(_playerFieldView, action._attacker),
                        IndexOfChar(_opponentFieldView, action._target),
                        action._targetsDeck);
                default:
                    return NetworkGameService.MainActionData.Pass();
            }
        }

        // フィールドのキャラ一覧における card のインデックス。見つからない／null なら -1。
        private static int IndexOfChar(FieldView field, CardView card)
        {
            if (card == null)
            {
                return -1;
            }
            IReadOnlyList<CardView> chars = field.Characters;
            for (int i = 0; i < chars.Count; i++)
            {
                if (chars[i] == card)
                {
                    return i;
                }
            }
            return -1;
        }

        // フィールドのキャラ一覧における index 番目のキャラ。範囲外なら null。
        private static CardView CharAtIndex(FieldView field, int index)
        {
            IReadOnlyList<CardView> chars = field.Characters;
            if (index < 0 || index >= chars.Count)
            {
                return null;
            }
            return chars[index];
        }
    }
}

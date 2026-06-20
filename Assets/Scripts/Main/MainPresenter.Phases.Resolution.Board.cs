using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Card.Effects;
using Main.Game;
using CardEventType = Main.Card.EventType;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // バウンス：発動側から見た敵フィールドのキャラを count 体（値1。未設定=0 は1体）選び、
        // 所有者（相手）の手札へ戻す。対象数が敵の数以上なら全員・0体なら空振り。
        // 対象選択は DamageEnemy と同じ仕組み（プレイヤー選択／CPU 自動／オンライン同期）を共用する。
        private async UniTask ApplyBounceAsync(int count, bool isLocal, GameObject effectPrefab, bool simultaneous, CancellationToken ct)
        {
            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            HandView targetHand = isLocal ? _opponentHandView : _handView;

            int enemyCount = targetField.Characters.Count;
            if (enemyCount == 0)
            {
                return;
            }

            int targetCount = Mathf.Min(count <= 0 ? 1 : count, enemyCount);
            List<CardView> targets = await ResolveEnemyCharTargetsAsync(targetField, targetCount, enemyCount, isLocal, "手札に戻す相手キャラを選択", ct);
            if (targets.Count == 0)
            {
                return;
            }

            // 一括モード：全対象の現在位置を先に確定し、フィールドから一斉に外してから
            // まとめて手札へ戻す（1枚ずつではなく同時に飛ぶ演出）。effectPrefab は再生しない。
            if (simultaneous)
            {
                List<(CardView target, Rect fromRect)> moves = new List<(CardView, Rect)>();
                foreach (CardView target in targets)
                {
                    if (targetField.Contains(target))
                    {
                        moves.Add((target, target.worldBound));
                    }
                }
                // 同時実行では各カードの追加完了が並行するため、IsFull の判定が競合する。
                // 事前に空きスロット数を確定し、超過分はバーンへ振り分ける。
                bool toOpponentHand = targetHand == _opponentHandView;
                GraveyardView ownerGraveyard = toOpponentHand ? _opponentGraveyardView : _playerGraveyardView;
                int freeSlots = Mathf.Max(0, HandView.MaxCards - targetHand.Count);
                List<UniTask> moveTasks = new List<UniTask>(moves.Count);
                for (int i = 0; i < moves.Count; i++)
                {
                    (CardView target, Rect fromRect) = moves[i];
                    targetField.RemoveCard(target);
                    if (i < freeSlots)
                    {
                        moveTasks.Add(BounceCardToHandAsync(target, targetHand, fromRect, ct));
                    }
                    else
                    {
                        moveTasks.Add(BurnCardToGraveyardAsync(target, fromRect, ownerGraveyard, ct));
                    }
                }
                await UniTask.WhenAll(moveTasks);
                return;
            }

            // 個別モード：各対象を順番に所有者の手札へ戻す（対象ごとに effectPrefab を再生）
            foreach (CardView target in targets)
            {
                if (!targetField.Contains(target))
                {
                    continue;
                }
                if (effectPrefab != null)
                {
                    // PlayParticleAtCardAsync が演出終了後の共通ディレイ（EffectTrailingDelaySeconds）まで待つ
                    await PlayParticleAtCardAsync(target, effectPrefab, ct);
                }
                Rect fromRect = target.worldBound;
                targetField.RemoveCard(target);
                await BounceCardToHandAsync(target, targetHand, fromRect, ct);
            }
        }

        // 1体を所有者の手札へ戻す。相手の手札（裏向き表示）に戻すときは裏返してから加える。
        // 戻し先の手札が満杯なら所有者の墓地へ送る（バーン）。
        private UniTask BounceCardToHandAsync(CardView target, HandView targetHand, Rect fromRect, CancellationToken ct)
        {
            bool toOpponentHand = targetHand == _opponentHandView;
            GraveyardView ownerGraveyard = toOpponentHand ? _opponentGraveyardView : _playerGraveyardView;
            return ReturnCardToHandOrBurnAsync(target, targetHand, ownerGraveyard, fromRect, toOpponentHand, ct);
        }

        // バウンス（全体）：発動側から見た敵フィールドのキャラ全員を所有者（相手）の手札へ戻す。
        // 個別バウンス（ApplyBounceAsync）と異なり、対象ごとにエフェクトは再生せず、
        // フィールド中央で全体用エフェクトを1度だけ再生してから一括バウンスする。
        // ApplyBounceAsync は対象数が敵の数以上のとき選択 UI なしで全員を対象にするため、
        // 敵の全数を渡して全体バウンスを実現する（per-target エフェクトは null で無効化）。
        internal async UniTask ApplyBounceAllAsync(bool isLocal, CancellationToken ct)
        {
            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            if (targetField.Characters.Count == 0)
            {
                return;
            }
            if (_bounceAllEffectPrefab != null)
            {
                // PlayParticleAtUiPositionAsync が演出終了後の共通ディレイ（EffectTrailingDelaySeconds）まで待つ
                await PlayParticleAtUiPositionAsync(targetField, targetField.worldBound.center, _bounceAllEffectPrefab, ct, scale: 2f);
            }
            await ApplyBounceAsync(targetField.Characters.Count, isLocal, null, true, ct);
        }

        // 発動側の自フィールドに、charNumber が示すキャラ（"C###"）を count 体新規生成して配置する。
        // count が 0 以下なら1体扱い。手札・デッキは消費しない。召喚キャラの OnEnter も発動する。
        // フィールドが満杯（FieldView.MaxCharacters）になったら打ち切る。満杯で新キャラが出ないため
        // OnEnter 連鎖もここで自然に停止する（無限ループにならない）。
        internal async UniTask ApplySummonCharAsync(int charNumber, int count, bool isLocal, CancellationToken ct)
        {
            if (charNumber <= 0)
            {
                return;
            }

            string id = $"C{charNumber:D3}";
            if (!_cardDatabase.TryGet(id, out CardData data) || data is not CharacterCardData)
            {
                return;
            }

            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            int summonCount = count <= 0 ? 1 : count;
            for (int i = 0; i < summonCount; i++)
            {
                // キャラ8体勝利が成立したら以降の召喚は打ち切る
                if (field.IsCharactersFull || _isGameOver)
                {
                    break;
                }
                await SummonSingleCharAsync(data, field, isLocal, ct);
            }
        }

        // SummonChar の1体分：配置 → キャラ8体勝利判定（OnCardPlayed）→ 登場演出（その場で出現）→ 登場時効果（OnEnter）。
        // stateSource を渡すと、そのキャラのランタイム状態（バフ・現在HP）をコピーして生成する（CopyFieldChar 用）。
        private async UniTask SummonSingleCharAsync(CardData data, FieldView field, bool isLocal, CancellationToken ct, CardView stateSource = null)
        {
            CardView newChar = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: !isLocal);
            if (stateSource != null)
            {
                newChar.CopyRuntimeStateFrom(stateSource);
            }
            field.PlaceCard(newChar);
            OnCardPlayed(data, playedByLocal: isLocal);
            await PlaySummonAppearAsync(newChar, ct);

            await ResolveCharacterEnterEffectAsync(newChar, isLocal, ct);
        }

        private async UniTask ApplyDamageToCharAsync(CardView target, int damage, CancellationToken ct)
        {
            await target.TakeDamageAsync(damage, ct);
        }

        private async UniTask DestroyCharToGraveyardAsync(CardView target, FieldView field, GraveyardView graveyard, CancellationToken ct)
        {
            await PlayCharDestroyEffectAsync(target, ct);
            Rect fromRect = target.worldBound;
            field.RemoveCard(target);
            await FlyToGraveyardAsync(target, fromRect, graveyard, ct);
        }

        // 破壊されたキャラの OnDestroy 効果を発動する。ownerIsLocal = 破壊されたキャラの所有者が自分側か。
        // 効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも両クライアントで対称に発動する（追加同期不要）。
        private UniTask FireOnDestroyEffectAsync(CardView destroyedCard, bool ownerIsLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(destroyedCard, CharacterEffectTrigger.OnDestroy, ownerIsLocal, ct);
        }

        // 相手キャラの攻撃でダメージを受けたキャラの OnAttacked 効果を発動する。ownerIsLocal = 防御側（被攻撃キャラ）の所有者が自分側か。
        // 戦闘（ExecuteAttackAsync）からのみ呼ぶ。効果はカードデータと同期済み盤面から決定的に解決される（追加同期不要）。
        private UniTask FireOnAttackedEffectAsync(CardView defenderCard, bool ownerIsLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(defenderCard, CharacterEffectTrigger.OnAttacked, ownerIsLocal, ct);
        }

        // 攻撃で相手キャラを破壊したキャラの OnKill 効果を発動する。ownerIsLocal = 攻撃側キャラの所有者が自分側か。
        // 戦闘（ExecuteAttackAsync）からのみ呼ぶ。効果はカードデータと同期済み盤面から決定的に解決される（追加同期不要）。
        private UniTask FireOnKillEffectAsync(CardView attackerCard, bool ownerIsLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(attackerCard, CharacterEffectTrigger.OnKill, ownerIsLocal, ct);
        }

        // デッキ攻撃で相手プレイヤー（相手デッキ）にダメージを与えた攻撃キャラの OnDealPlayerDamage 効果を発動する。
        // ownerIsLocal = 攻撃側キャラの所有者が自分側か。デッキ攻撃（ExecuteDeckAttackAsync）からのみ呼ぶ。
        // 効果はカードデータと同期済み盤面から決定的に解決される（追加同期不要）。
        private UniTask FireOnDealPlayerDamageEffectAsync(CardView attackerCard, bool ownerIsLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(attackerCard, CharacterEffectTrigger.OnDealPlayerDamage, ownerIsLocal, ct);
        }

        internal async UniTask ApplyEvolveEffectAsync(bool isLocal, CancellationToken ct)
        {
            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            GraveyardView ownGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;

            if (ownField.Characters.Count == 0)
            {
                return;
            }

            // 受信側はアニメーション前にハンドラを事前登録してメッセージのロストを防ぐ
            // （Switch / Recover と同じ対策。生贄アニメーション後に登録すると、相手の送信が
            //   先に届いた場合に NGO が未登録メッセージを破棄して永久待機になる）
            UniTask<string> evolveReceiveTask = (!isLocal && _isOnline)
                ? _networkGameService.WaitForOpponentEvolveAsync(ct)
                : default;

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
                string cardId = await evolveReceiveTask;
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
                        // PlaceCard 直後はレイアウト未確定で worldBound が(0,0)を返すため、
                        // 1フレーム待って確定させてからパーティクルを正しいカード位置に出す。
                        await UniTask.NextFrame(ct);
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
                        // PlaceCard 直後はレイアウト未確定で worldBound が(0,0)を返すため、
                        // 1フレーム待って確定させてからパーティクルを正しいカード位置に出す。
                        await UniTask.NextFrame(ct);
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

        internal async UniTask ApplySwitchEffectAsync(bool isLocal, CancellationToken ct)
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

                await PlaySwitchEffectAsync(existingChar, ct);

                int targetCost = existingChar.Data.Cost;
                Rect charRect = existingChar.worldBound;
                ownField.RemoveCard(existingChar);
                await ReturnCardToHandOrBurnAsync(existingChar, _handView, ownGraveyard, charRect, toOpponentHand: false, ct);

                CardView newChar = await WaitForPlayerSwitchInputAsync(targetCost, ct);
                if (_isOnline)
                {
                    _networkGameService.SendSwitchAction(existingChar.Data.Id, newChar?.Data.Id);
                }
                if (newChar != null)
                {
                    OnCardPlayed(newChar.Data, playedByLocal: true);
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
                    await PlaySwitchEffectAsync(sacrificedChar, ct);
                    Rect sacrificedRect = sacrificedChar.worldBound;
                    _opponentFieldView.RemoveCard(sacrificedChar);
                    await ReturnCardToHandOrBurnAsync(sacrificedChar, _opponentHandView, ownGraveyard, sacrificedRect, toOpponentHand: true, ct);
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
                }
            }
            else
            {
                CardView existingChar = ownField.Characters[0];
                await PlaySwitchEffectAsync(existingChar, ct);
                int targetCost = existingChar.Data.Cost;
                Rect charRect = existingChar.worldBound;
                ownField.RemoveCard(existingChar);
                await ReturnCardToHandOrBurnAsync(existingChar, _opponentHandView, ownGraveyard, charRect, toOpponentHand: true, ct);

                IReadOnlyList<CardView> cpuHand = _opponentHandView.Cards;
                List<CardData> cpuHandData = cpuHand.Select(c => c.Data).ToList();
                int idx = CpuAgent.ChooseSwitchCardIndex(cpuHandData, targetCost);
                if (idx >= 0)
                {
                    CardView newChar = cpuHand[idx];
                    Rect fromRect = newChar.worldBound;
                    _opponentHandView.RemoveCard(newChar);
                    await FlyCardToDestAsync(newChar, fromRect, ownField, ct);
                    ownField.PlaceCard(newChar);
                    OnCardPlayed(newChar.Data, playedByLocal: false);
                    await newChar.FlipAsync(ct);
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

        private async UniTask<CardView> WaitForPlayerSwitchInputAsync(int targetCost, CancellationToken ct)
        {
            _switchTargetCost = targetCost;
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
                _switchTargetCost = 0;
                HideActionButtons();
            }
        }

        internal async UniTask ApplyDrawEffectAsync(int count, bool isLocal, CancellationToken ct)
        {
            if (count <= 0)
            {
                return;
            }

            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;

            // オーバーリミット：このドローでデッキが0枚へ落ちたら、生存を確認したうえで最後に1回告知する
            bool limitBroke = false;

            for (int i = 0; i < count; i++)
            {
                // オーバーリミット：1枚引く直前にデッキが空なら、その時点で敗北（カードを引き切った次の引きで負ける）。
                if (_isGameOver || CheckDeckOutWin(isLocalDeck: isLocal))
                {
                    return;
                }

                Rect deckRect = deck.worldBound;
                CardData drawn = deck.DrawTop();
                deck.RefreshCount();

                if (drawn == null)
                {
                    break;
                }

                PlayDrawSe();

                if (isLocal)
                {
                    if (_handView.IsFull)
                    {
                        ShowToast("手札が上限 → 墓地へ");
                        await BurnDrawnCardAsync(drawn, deckRect, _playerGraveyardView, isOpponent: false, ct);
                    }
                    else
                    {
                        await _handView.AddCardAnimatedAsync(drawn, deckRect, 0f, ct);
                    }
                }
                else
                {
                    if (_opponentHandView.IsFull)
                    {
                        await BurnDrawnCardAsync(drawn, deckRect, _opponentGraveyardView, isOpponent: true, ct);
                    }
                    else
                    {
                        await PlayCpuDrawAsync(drawn, deckRect, ct);
                    }
                }

                // オーバーリミット：このドローでデッキが0枚になったら告知予約（直後の引きで敗北する場合は告知しない）
                limitBroke |= UpdateLimitBreak(isLocalDeck: isLocal);
            }

            // オーバーリミット：一連のドローを生き残った場合のみ「リミットブレイク！」告知
            if (!_isGameOver && limitBroke)
            {
                await PlayLimitBreakAnnouncementAsync(ct);
            }

            // ドロー演出完了後、次の処理へ進む前に少し待つ
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
        }

        // コインドロー（Draw の値2フラグ）：コインを振り、表が出るたびに1枚引き、裏が出たら終了する。
        // 引く枚数は乱数で決まるため、オンラインでは発動側が回数を確定して送信し、ミラー側は受信した回数を使う。
        // デッキ順は両クライアントで同期済みのため、同じ枚数引けば同じカードになる（追加同期は回数のみで足りる）。
        // 表の回数＋最後の裏を両クライアントで同じ順に再生して、見た目も対称にする。
        internal async UniTask ApplyCoinDrawEffectAsync(CardView source, bool isLocal, CancellationToken ct)
        {
            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;
            int cap = deck.Count;

            int headsCount;
            if (_isOnline && !isLocal)
            {
                // ミラー側：発動側が確定した表（＝ドロー）の回数を受信する。デッキ枚数を超えないようクランプ。
                headsCount = Mathf.Min(await _networkGameService.WaitForOpponentCoinDrawCountAsync(ct), cap);
            }
            else
            {
                // 発動側／オフライン：裏が出るまで（またはデッキ切れまで）コインを振り、表の連続回数を数える。
                headsCount = 0;
                while (headsCount < cap && UnityEngine.Random.value < 0.5f)
                {
                    headsCount++;
                }
                if (_isOnline && isLocal)
                {
                    _networkGameService.SendCoinDrawCount(headsCount);
                }
            }

            // 表の回数がデッキ枚数未満なら、最後に裏が出て終了したことを意味する（デッキ切れで止まった場合は裏演出なし）。
            bool endedWithTails = headsCount < cap;

            for (int i = 0; i < headsCount; i++)
            {
                if (_isGameOver)
                {
                    return;
                }
                await PlayCoinFlipAsync(source, true, ct);
                await ApplyDrawEffectAsync(1, isLocal, ct);
            }

            if (!_isGameOver && endedWithTails)
            {
                await PlayCoinFlipAsync(source, false, ct);
            }
        }

        // サイコロドロー（Draw の値2=2）：6面サイコロを振り、出た目の数だけドローする。
        // 出目は乱数で決まるため、オンラインでは発動側が出目を確定して送信し、ミラー側は受信した出目を使う。
        // デッキ順は両クライアントで同期済みのため、同じ枚数引けば同じカードになる（追加同期は出目のみ）。
        // 引く枚数は確定値なので通常 Draw と同じ扱い（出目がデッキ残りを超えればオーバーリミット敗北あり）。
        internal async UniTask ApplyDiceDrawEffectAsync(CardView source, bool isLocal, CancellationToken ct)
        {
            int roll;
            if (_isOnline && !isLocal)
            {
                // ミラー側：発動側が確定した出目を受信する。
                roll = await _networkGameService.WaitForOpponentDiceDrawAsync(ct);
            }
            else
            {
                // 発動側／オフライン：1〜6 を振る（Range の上限は排他なので 7）。
                roll = UnityEngine.Random.Range(1, 7);
                if (_isOnline && isLocal)
                {
                    _networkGameService.SendDiceDrawResult(roll);
                }
            }

            await PlayDiceRollAsync(source, roll, ct);
            if (_isGameOver)
            {
                return;
            }
            await ApplyDrawEffectAsync(roll, isLocal, ct);
        }
    }
}

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

            // OnTurnStart の永続イベントはプレイ時に即時解決せず、登録簿に加えて毎ターン開始時に発動し続ける。
            // （コストとして捨てたカードは ResolveSingleCardAsync を通らないため登録されない＝墓地を走査しない理由）
            // コスト演出は支払い時に再生済みのため、ここでは登録のみ行い下の墓地移動処理へ合流する。
            if (card.Data is EventCardData turnStartEvent && turnStartEvent.EventTrigger == EventCardTrigger.OnTurnStart)
            {
                List<EventCardData> registry = isLocal ? _playerTurnStartEvents : _opponentTurnStartEvents;
                registry.Add(turnStartEvent);
            }
            else if (card.Data is EventCardData eventData)
            {
                await ResolveEventCardEffectAsync(eventData, card, isLocal, ct);
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

        // イベント効果の解決（種別ごとの事前演出 ＋ ApplyEventEffectAsync の適用）。
        // OnPlay のプレイ時と、OnTurnStart 永続イベントの毎ターン発動の両方から呼ぶ。
        // card は演出のアンカー（プレイ時はプレイしたカード、OnTurnStart は墓地からせり出した一時カード）。
        private async UniTask ResolveEventCardEffectAsync(EventCardData eventData, CardView card, bool isLocal, CancellationToken ct)
        {
            if (eventData.EventType == CardEventType.Draw || eventData.EventType == CardEventType.DrawSkipNext)
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
            else if (eventData.EventType == CardEventType.GainVPPerGreenGrave)
            {
                await PlayFloatingMedalAsync(card, ct);
            }
            else if (eventData.EventType == CardEventType.DrawNextTurnStart)
            {
                await PlayFloatingLabelAsync($"次ターン DRAW {eventData.EventValue}", "draw-label", card, ct);
            }
            await ApplyEventEffectAsync(eventData, isLocal, card, ct);

            if (_isGameOver)
            {
                return;
            }

            // 効果とは独立した勝利点付帯値（VictoryPointBonus）を加算する。
            await ApplyVictoryPointBonusAsync(eventData.VictoryPointBonus, isLocal, card, ct);
        }

        // ─── ターン開始時効果 ────────────────────────────────────────────────
        // 自分のターン開始時（ドロー前）に、アクティブプレイヤーの場のキャラ（CharacterEffectTrigger.OnTurnStart）と
        // プレイ済みの永続イベント（EventCardTrigger.OnTurnStart の登録簿）の効果を順に発動する。
        // isLocal = アクティブプレイヤーが自分側か（_gameModel.IsLocalTurn）。
        // 効果はカードデータと同期済みの盤面・登録簿から決定的に解決されるため、オンラインでも対称に発動する（追加同期不要）。
        private async UniTask ResolveTurnStartEffectsAsync(bool isLocal, CancellationToken ct)
        {
            // 1. 場のキャラの OnTurnStart（ターン開始時点のスナップショットを並び順に1体ずつ発動）
            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            List<CardView> characters = new List<CardView>(field.Characters);
            foreach (CardView character in characters)
            {
                if (_isGameOver)
                {
                    return;
                }
                // 先行する効果で破壊・除去されていたらスキップ
                if (!field.Contains(character))
                {
                    continue;
                }
                await ResolveCharacterTriggeredEffectAsync(character, CharacterEffectTrigger.OnTurnStart, isLocal, ct);
            }

            // 2. プレイ済みの永続イベント OnTurnStart（墓地から一時カードをせり出させて発動し、墓地へ戻す）。
            //    登録簿はプレイ（PlayEvent）された OnTurnStart イベントのみを保持する（コストで捨てたカードは含まれない）
            List<EventCardData> registry = isLocal ? _playerTurnStartEvents : _opponentTurnStartEvents;
            List<EventCardData> turnStartEvents = new List<EventCardData>(registry);
            foreach (EventCardData eventData in turnStartEvents)
            {
                if (_isGameOver)
                {
                    return;
                }
                await PlayGraveyardEventEffectAsync(eventData, isLocal, ct);
            }
        }

        // ─── キャラ登場時効果 ────────────────────────────────────────────────
        // 通常配置（ローカル PlaceChar / 相手カードプレイ）で配置確定したキャラの
        // EffectTrigger == OnEnter 効果を発動する。Switch / Evolve 配置は対象外。
        private UniTask ResolveCharacterEnterEffectAsync(CardView placedChar, bool isLocal, CancellationToken ct)
        {
            return ResolveCharacterTriggeredEffectAsync(placedChar, CharacterEffectTrigger.OnEnter, isLocal, ct);
        }

        // ─── キャラ攻撃時効果 ────────────────────────────────────────────────
        // キャラ攻撃・デッキ攻撃の攻撃宣言時に、攻撃側キャラの EffectTrigger == OnAttack 効果を発動する。
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

            if (charData.EffectTrigger != trigger)
            {
                return;
            }

            // EffectType=None でも VictoryPointBonus があれば付帯値だけ発動する。
            if (charData.EffectType == CardEventType.None && charData.VictoryPointBonus <= 0)
            {
                return;
            }

            switch (charData.EffectType)
            {
                case CardEventType.Draw:
                    await PlayDrawEffectAsync(sourceCard, charData.EffectValue, ct);
                    await ApplyDrawEffectAsync(charData.EffectValue, isLocal, ct);
                    break;
                case CardEventType.DrawSkipNext:
                    await PlayDrawEffectAsync(sourceCard, charData.EffectValue, ct);
                    await ApplyDrawEffectAsync(charData.EffectValue, isLocal, ct);
                    SetSkipNextDraw(isLocal);
                    break;
                case CardEventType.DrawNextTurnStart:
                    await PlayFloatingLabelAsync($"次ターン DRAW {charData.EffectValue}", "draw-label", sourceCard, ct);
                    AddPendingNextDraw(charData.EffectValue, isLocal);
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
                    // 除去されたキャラの OnDestroy を発動（所有者は発動側の相手 = !isLocal）
                    await FireOnDestroyEffectAsync(banishTarget, !isLocal, ct);
                    break;
                }
                case CardEventType.DamageAllEnemies:
                    await ApplyDamageAllEnemiesAsync(charData.EffectValue, isLocal, ct);
                    break;
                case CardEventType.DamageEnemy:
                    // 値1=対象数、値2=ダメージ
                    await ApplyDamageEnemyAsync(charData.EffectValue2, charData.EffectValue, isLocal, ct);
                    break;
                case CardEventType.Bounce:
                    await ApplyBounceAsync(charData.EffectValue, isLocal, _bounceEffectPrefab, false, ct);
                    break;
                case CardEventType.BounceAll:
                    await ApplyBounceAllAsync(isLocal, ct);
                    break;
                case CardEventType.SummonChar:
                    await ApplySummonCharAsync(charData.EffectValue, charData.EffectValue2, isLocal, sourceCard.worldBound, ct);
                    break;
                case CardEventType.GainVPPerGreenGrave:
                    await PlayFloatingMedalAsync(sourceCard, ct);
                    await AddVictoryPoints(CountGreenInGraveyard(isLocal), toLocal: isLocal, ct);
                    break;
                case CardEventType.HealAllAllies:
                    await ApplyHealAllAlliesAsync(charData.EffectValue, isLocal, ct);
                    break;
                case CardEventType.NextCardCostFree:
                    await ApplyNextCardCostFreeAsync(isLocal, sourceCard, ct);
                    break;
                case CardEventType.ExtraTurn:
                    await ApplyExtraTurnAsync(isLocal, sourceCard, ct);
                    break;
            }

            if (_isGameOver)
            {
                return;
            }

            // 効果とは独立した勝利点付帯値（VictoryPointBonus）を加算する。
            await ApplyVictoryPointBonusAsync(charData.VictoryPointBonus, isLocal, sourceCard, ct);
        }

        // 勝利点付帯値（VictoryPointBonus）の共通適用：bonus > 0 なら MedalIcon 演出 ＋ 勝利点加算。
        // キャラ・イベントの効果解決後に呼び、効果（EventType）とは独立して発動側へ加点する。
        private async UniTask ApplyVictoryPointBonusAsync(int bonus, bool isLocal, CardView sourceCard, CancellationToken ct)
        {
            if (bonus <= 0)
            {
                return;
            }
            await PlayFloatingMedalAsync(sourceCard, ct);
            await AddVictoryPoints(bonus, toLocal: isLocal, ct);
        }

        // 発動した側の墓地にある緑属性カードの枚数を返す（GainVPPerGreenGrave の動的な加点値）。
        // 墓地は両クライアントで同期済みのため、オンラインでも対称に解決される（追加同期不要）。
        private int CountGreenInGraveyard(bool isLocal)
        {
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            return graveyard.CountByAttribute(CardAttribute.Green);
        }

        private async UniTask ApplyEventEffectAsync(EventCardData data, bool isLocal, CardView sourceCard, CancellationToken ct)
        {
            switch (data.EventType)
            {
                case CardEventType.Draw:
                    await ApplyDrawEffectAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.DrawSkipNext:
                    await ApplyDrawEffectAsync(data.EventValue, isLocal, ct);
                    SetSkipNextDraw(isLocal);
                    break;
                case CardEventType.DrawNextTurnStart:
                    AddPendingNextDraw(data.EventValue, isLocal);
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
                    // 除去されたキャラの OnDestroy を発動（所有者は発動側の相手 = !isLocal）
                    await FireOnDestroyEffectAsync(charCard, !isLocal, ct);
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
                case CardEventType.DamageEnemy:
                    // 値1=対象数、値2=ダメージ
                    await ApplyDamageEnemyAsync(data.EventValue2, data.EventValue, isLocal, ct);
                    break;
                case CardEventType.Bounce:
                    await ApplyBounceAsync(data.EventValue, isLocal, _bounceEffectPrefab, false, ct);
                    break;
                case CardEventType.BounceAll:
                    await ApplyBounceAllAsync(isLocal, ct);
                    break;
                case CardEventType.SummonChar:
                    await ApplySummonCharAsync(data.EventValue, data.EventValue2, isLocal, sourceCard.worldBound, ct);
                    break;
                case CardEventType.GainVPPerGreenGrave:
                    await AddVictoryPoints(CountGreenInGraveyard(isLocal), toLocal: isLocal, ct);
                    break;
                case CardEventType.HealAllAllies:
                    await ApplyHealAllAlliesAsync(data.EventValue, isLocal, ct);
                    break;
                case CardEventType.NextCardCostFree:
                    await ApplyNextCardCostFreeAsync(isLocal, sourceCard, ct);
                    break;
                case CardEventType.ExtraTurn:
                    await ApplyExtraTurnAsync(isLocal, sourceCard, ct);
                    break;
            }
        }

        // ExtraTurn 効果：アクティブプレイヤーが発動したときのみ、相手にターンを渡さず
        // もう一度自分のターンを行う。ターン終了時に消費する（RunTurnAsync）。
        // isLocal は効果の発動側。アクティブプレイヤー（_gameModel.IsLocalTurn）と一致するときだけ有効化し、
        // 相手ターン中の OnDestroy 等（発動側 != アクティブ）では発動しない。
        // 1ターン中に複数回発動しても追加ターンは1回（フラグは bool）。
        private async UniTask ApplyExtraTurnAsync(bool isLocal, CardView sourceCard, CancellationToken ct)
        {
            if (isLocal != _gameModel.IsLocalTurn)
            {
                return;
            }
            _extraTurnPending = true;
            await PlayFloatingLabelAsync("もう一度！", "extra-turn-label", sourceCard, ct);
        }

        // DrawSkipNext 効果：発動側の次のドローフェーズを1回スキップするフラグを立てる。
        // フラグは次に来るそのプレイヤーの RunDrawPhaseAsync で消費される。
        // 効果はカードデータから決定的に解決されるため、各クライアントが自分側のフラグを立てれば対称になる（追加同期不要）。
        private void SetSkipNextDraw(bool isLocal)
        {
            if (isLocal)
            {
                _playerSkipNextDraw = true;
            }
            else
            {
                _opponentSkipNextDraw = true;
            }
        }

        // DrawNextTurnStart 効果：発動側の次のターン開始時に追加でドローする予約枚数を加算する。
        // 予約は次に来るそのプレイヤーの RunDrawPhaseAsync で通常ドローに上乗せして消費される（複数回発動で累積）。
        // 効果はカードデータから決定的に解決されるため、各クライアントが自分側のカウントを加算すれば対称になる（追加同期不要）。
        private void AddPendingNextDraw(int count, bool isLocal)
        {
            if (count <= 0)
            {
                return;
            }
            if (isLocal)
            {
                _playerPendingNextDraw += count;
            }
            else
            {
                _opponentPendingNextDraw += count;
            }
        }

        // 次にプレイするカード1枚のコストを0にする（使うまで持続）。発動側のフラグを立て、発動カード上に告知を出す
        private async UniTask ApplyNextCardCostFreeAsync(bool isLocal, CardView sourceCard, CancellationToken ct)
        {
            if (isLocal)
            {
                _playerNextCardFree = true;
            }
            else
            {
                _opponentNextCardFree = true;
            }
            await PlayFloatingLabelAsync("コスト0", "cost-free-label", sourceCard, ct);
        }

        // 発動側の自フィールドのキャラ全員の HP を amount 回復する（最大HPでクランプ）。
        // amount <= 0 は最大HPまで全回復。自キャラがいなければ空振り。
        // 効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも対称に発動する（追加同期不要）。
        private async UniTask ApplyHealAllAlliesAsync(int amount, bool isLocal, CancellationToken ct)
        {
            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            List<CardView> targets = new List<CardView>(ownField.Characters);
            if (targets.Count == 0)
            {
                return;
            }

            List<UniTask> healTasks = new List<UniTask>(targets.Count);
            foreach (CardView target in targets)
            {
                healTasks.Add(target.HealAsync(amount, ct));
            }
            await UniTask.WhenAll(healTasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
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
            List<CardView> destroyed = new List<CardView>();
            foreach (CardView target in targets)
            {
                if (target.CurrentHp <= 0 && targetField.Contains(target))
                {
                    destroyed.Add(target);
                    destroyTasks.Add(DestroyCharToGraveyardAsync(target, targetField, targetGraveyard, ct));
                }
            }
            await UniTask.WhenAll(destroyTasks);

            // 破壊された各キャラの OnDestroy を順番に発動（破壊されたキャラの所有者は発動側の相手 = !isLocal）
            foreach (CardView destroyedChar in destroyed)
            {
                await FireOnDestroyEffectAsync(destroyedChar, !isLocal, ct);
            }
        }

        // 発動側から見た敵キャラを count 体（値1。未設定=0 は1体）対象に選び、それぞれに damage（値2）を与えて
        // HP 0 以下なら破壊する。対象数が敵の数以上なら全員が対象。
        private async UniTask ApplyDamageEnemyAsync(int damage, int count, bool isLocal, CancellationToken ct)
        {
            if (damage <= 0)
            {
                return;
            }

            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            int enemyCount = targetField.Characters.Count;
            // 敵キャラがいなければ空振り。両クライアントで盤面は同期されており対称に判定されるため追加同期は不要
            if (enemyCount == 0)
            {
                return;
            }

            int targetCount = Mathf.Min(count <= 0 ? 1 : count, enemyCount);
            List<CardView> targets = await ResolveEnemyCharTargetsAsync(targetField, targetCount, enemyCount, isLocal, "相手キャラを選択", ct);
            if (targets.Count == 0)
            {
                return;
            }

            // 選んだ対象すべてに同時にダメージを与える
            List<UniTask> hitTasks = new List<UniTask>();
            foreach (CardView target in targets)
            {
                hitTasks.Add(HitCharWithParticleAsync(target, damage, ct));
            }
            await UniTask.WhenAll(hitTasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            // HP 0 以下になった対象をまとめて破壊
            List<UniTask> destroyTasks = new List<UniTask>();
            List<CardView> destroyed = new List<CardView>();
            foreach (CardView target in targets)
            {
                if (target.CurrentHp <= 0 && targetField.Contains(target))
                {
                    destroyed.Add(target);
                    destroyTasks.Add(DestroyCharToGraveyardAsync(target, targetField, targetGraveyard, ct));
                }
            }
            await UniTask.WhenAll(destroyTasks);

            // 破壊された各キャラの OnDestroy を順番に発動（破壊されたキャラの所有者は発動側の相手 = !isLocal）
            foreach (CardView destroyedChar in destroyed)
            {
                await FireOnDestroyEffectAsync(destroyedChar, !isLocal, ct);
            }
        }

        private async UniTask HitCharWithParticleAsync(CardView target, int damage, CancellationToken ct)
        {
            if (_hitEffectPrefab != null)
            {
                await PlayParticleAtCardAsync(target, _hitEffectPrefab, ct);
            }
            await ApplyDamageToCharAsync(target, damage, ct);
        }

        // 対象決定：対象数が敵の数以上なら全員。そうでなければ、ローカルはプレイヤーが選択し、
        // オンライン相手はインデックス配列を受信、CPU は攻撃力上位を選ぶ。
        // prompt = プレイヤー選択時のトースト文言（DamageEnemy / Bounce で共用）。
        private async UniTask<List<CardView>> ResolveEnemyCharTargetsAsync(FieldView targetField, int targetCount, int enemyCount, bool isLocal, string prompt, CancellationToken ct)
        {
            List<CardView> chars = new List<CardView>(targetField.Characters);

            // 全員が対象なら選択不要（両クライアントで決定的なので追加同期も不要）
            if (targetCount >= enemyCount)
            {
                return chars;
            }

            if (isLocal)
            {
                List<CardView> selected = await WaitForPlayerEnemyCharsSelectionAsync(targetCount, prompt, ct);
                if (_isOnline)
                {
                    int[] indices = new int[selected.Count];
                    for (int i = 0; i < selected.Count; i++)
                    {
                        indices[i] = chars.IndexOf(selected[i]);
                    }
                    _networkGameService.SendDamageTargets(indices);
                }
                return selected;
            }

            if (_isOnline)
            {
                int[] indices = await _networkGameService.WaitForOpponentDamageTargetsAsync(ct);
                List<CardView> result = new List<CardView>();
                foreach (int index in indices)
                {
                    if (index >= 0 && index < chars.Count)
                    {
                        result.Add(chars[index]);
                    }
                }
                return result;
            }

            // CPU：攻撃力の高い順に targetCount 体を狙う
            return chars.OrderByDescending(c => c.Data.Attack).Take(targetCount).ToList();
        }

        // 敵フィールドのキャラをハイライトし、プレイヤーが targetCount 体をクリックで選ぶのを待つ
        private async UniTask<List<CardView>> WaitForPlayerEnemyCharsSelectionAsync(int targetCount, string prompt, CancellationToken ct)
        {
            _enemyCharSelected = new List<CardView>();
            _enemyCharSelectTarget = targetCount;
            _enemyCharSelectPrompt = prompt;
            _enemyCharSelectionTcs = new UniTaskCompletionSource<List<CardView>>();

            foreach (CardView c in _opponentFieldView.Characters)
            {
                c.AddToClassList("selectable-char");
            }
            ShowToast($"{prompt}（あと{targetCount}体）");

            try
            {
                return await _enemyCharSelectionTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _enemyCharSelectionTcs = null;
                _enemyCharSelectTarget = 0;
                _enemyCharSelected = null;
                _enemyCharSelectPrompt = null;
                foreach (CardView c in _opponentFieldView.Characters)
                {
                    c.RemoveFromClassList("selectable-char");
                    c.RemoveFromClassList("selected-char");
                }
            }
        }

        // 敵キャラ選択中のクリック処理：未選択のキャラを選択リストに加え、必要数に達したら確定する
        private void HandleEnemyCharSelectionClick(CardView card)
        {
            if (_enemyCharSelectionTcs == null || _enemyCharSelected == null)
            {
                return;
            }
            if (card.IsFaceDown || card.Data is not CharacterCardData)
            {
                return;
            }
            if (_enemyCharSelected.Contains(card))
            {
                return;
            }

            _enemyCharSelected.Add(card);
            card.AddToClassList("selected-char");

            int remaining = _enemyCharSelectTarget - _enemyCharSelected.Count;
            if (remaining > 0)
            {
                ShowToast($"{_enemyCharSelectPrompt}（あと{remaining}体）");
            }
            else
            {
                _enemyCharSelectionTcs.TrySetResult(new List<CardView>(_enemyCharSelected));
            }
        }

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
                List<UniTask> moveTasks = new List<UniTask>(moves.Count);
                foreach ((CardView target, Rect fromRect) in moves)
                {
                    targetField.RemoveCard(target);
                    moveTasks.Add(BounceCardToHandAsync(target, targetHand, fromRect, ct));
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
        private async UniTask BounceCardToHandAsync(CardView target, HandView targetHand, Rect fromRect, CancellationToken ct)
        {
            if (targetHand == _opponentHandView && !target.IsFaceDown)
            {
                await target.FlipAsync(ct);
            }
            await targetHand.AddCardBackAsync(target, fromRect, ct);
        }

        // バウンス（全体）：発動側から見た敵フィールドのキャラ全員を所有者（相手）の手札へ戻す。
        // 個別バウンス（ApplyBounceAsync）と異なり、対象ごとにエフェクトは再生せず、
        // フィールド中央で全体用エフェクトを1度だけ再生してから一括バウンスする。
        // ApplyBounceAsync は対象数が敵の数以上のとき選択 UI なしで全員を対象にするため、
        // 敵の全数を渡して全体バウンスを実現する（per-target エフェクトは null で無効化）。
        private async UniTask ApplyBounceAllAsync(bool isLocal, CancellationToken ct)
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
        private async UniTask ApplySummonCharAsync(int charNumber, int count, bool isLocal, Rect fromRect, CancellationToken ct)
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
                await SummonSingleCharAsync(data, field, isLocal, fromRect, ct);
            }
        }

        // SummonChar の1体分：飛来 → 配置 → キャラ8体勝利判定（OnCardPlayed）→ 演出 → 登場時効果（OnEnter）
        private async UniTask SummonSingleCharAsync(CardData data, FieldView field, bool isLocal, Rect fromRect, CancellationToken ct)
        {
            CardView newChar = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: !isLocal);
            await FlyCardToDestAsync(newChar, fromRect, field, ct);
            field.PlaceCard(newChar);
            OnCardPlayed(data, playedByLocal: isLocal);
            if (_evolveEffectPrefab != null)
            {
                await PlayParticleAtCardAsync(newChar, _evolveEffectPrefab, ct, Quaternion.Euler(90f, 0f, 0f));
            }

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

        private async UniTask ApplyEvolveEffectAsync(bool isLocal, CancellationToken ct)
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
                List<CardData> cpuHandData = cpuHand.Select(c => c.Data).ToList();
                int idx = CpuAgent.ChooseCharacterSetCardIndex(cpuHandData, i => CpuCanAffordCost(cpuHandData, i));
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

            CheckDeckOutWin(isLocalDeck: isLocal);
        }
    }
}

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

        // イベント効果の解決：効果種別ごとのハンドラ（演出 ＋ 盤面適用）を発動し、最後に勝利点付帯値を加算する。
        // OnPlay のプレイ時と、OnTurnStart 永続イベントの毎ターン発動の両方から呼ぶ。
        // card は演出のアンカー（プレイ時はプレイしたカード、OnTurnStart は墓地からせり出した一時カード）。
        // 事前演出は各 EffectHandler.ApplyAsync 内に含まれる（EffectCatalog 経由で取得）。
        private async UniTask ResolveEventCardEffectAsync(EventCardData eventData, CardView card, bool isLocal, CancellationToken ct)
        {
            EffectHandler handler = EffectCatalog.Get(eventData.EventType);
            if (handler != null && handler.ValidOnEvent)
            {
                EffectInvocation inv = new EffectInvocation(isLocal, card, eventData.EventValue, eventData.EventValue2, eventData.Keyword);
                await handler.ApplyAsync(this, inv, ct);
            }

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

        // 指定トリガーのキャラ効果を発動する。効果種別ごとの解決は EffectCatalog のハンドラ（演出 ＋ 適用）に委譲する。
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

            // 効果種別ごとのハンドラ（演出 ＋ 盤面適用）を発動する。
            // キャラに無効な効果（Switch / Evolve / Recover など ValidOnCharacter=false）は適用しない。
            EffectHandler handler = EffectCatalog.Get(charData.EffectType);
            if (handler != null && handler.ValidOnCharacter)
            {
                EffectInvocation inv = new EffectInvocation(isLocal, sourceCard, charData.EffectValue, charData.EffectValue2, charData.Keyword);
                await handler.ApplyAsync(this, inv, ct);
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
            await PlayFloatingMedalAsync(sourceCard, bonus, ct);
            await AddVictoryPoints(bonus, toLocal: isLocal, ct);
        }

        // 発動した側の墓地にある緑属性カードの枚数を返す（GainVPPerGreenGrave の動的な加点値）。
        // 墓地は両クライアントで同期済みのため、オンラインでも対称に解決される（追加同期不要）。
        internal int CountGreenInGraveyard(bool isLocal)
        {
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            return graveyard.CountByAttribute(CardAttribute.Green);
        }

        // Recover 効果（RecoverHandler から呼ぶ）：墓地の上から value 枚を回収してデッキへ戻し、シャッフルする。
        // オンラインでは戻したデッキ順をホスト基準で同期する（受信側はアニメ前にハンドラ登録してロストを防ぐ）。
        internal async UniTask ApplyRecoverEffectAsync(int value, bool isLocal, CancellationToken ct)
        {
            GraveyardView sourceGraveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            DeckView targetDeck = isLocal ? _playerDeckView : _opponentDeckView;
            List<CardData> recovered = sourceGraveyard.TakeFromTop(value);
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
        }

        // 効果ハンドラ用：発動側の自フィールドを返す（Switch / Evolve の事前演出で使う）。
        internal FieldView FieldFor(bool isLocal)
        {
            return isLocal ? _playerFieldView : _opponentFieldView;
        }

        // Bounce 効果（BounceHandler から呼ぶ）：個別バウンス（per-target エフェクト付き）の既定オーバーロード。
        internal UniTask ApplyBounceAsync(int count, bool isLocal, CancellationToken ct)
        {
            return ApplyBounceAsync(count, isLocal, _bounceEffectPrefab, false, ct);
        }

        // ExtraTurn 効果：アクティブプレイヤーが発動したときのみ、相手にターンを渡さず
        // もう一度自分のターンを行う。ターン終了時に消費する（RunTurnAsync）。
        // isLocal は効果の発動側。アクティブプレイヤー（_gameModel.IsLocalTurn）と一致するときだけ有効化し、
        // 相手ターン中の OnDestroy 等（発動側 != アクティブ）では発動しない。
        // 1ターン中に複数回発動しても追加ターンは1回（フラグは bool）。
        internal async UniTask ApplyExtraTurnAsync(bool isLocal, CardView sourceCard, CancellationToken ct)
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
        internal void SetSkipNextDraw(bool isLocal)
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
        internal void AddPendingNextDraw(int count, bool isLocal)
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
        internal async UniTask ApplyNextCardCostFreeAsync(bool isLocal, CardView sourceCard, CancellationToken ct)
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

    }
}

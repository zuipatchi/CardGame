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

            // 攻撃したらタップ（突進後に横向きへ倒れる）
            await TapAttackerAsync(attacker, isLocal, ct);

            int atk = attacker.CurrentAttack;
            int damage = atk;

            if (damage == 0)
            {
                await PlayShieldBlockEffectAsync(target, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                // 攻撃力0の盾ブロックでも「攻撃を受けた」ので OnAttacked を発動する（ダメージ0・破壊判定なし）
                if (targetField.Contains(target))
                {
                    await FireOnAttackedEffectAsync(target, !isLocal, ct);
                }
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

                // 攻撃を受けたキャラの OnAttacked を発動（防御側の所有者は攻撃側の相手 = !isLocal）。
                // OnAttacked で回復・反撃して生死が変わり得るため、破壊判定はこの後に再計算する。
                if (targetField.Contains(target))
                {
                    await FireOnAttackedEffectAsync(target, !isLocal, ct);
                }
            }

            if (_isGameOver)
            {
                return;
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

                if (_isGameOver)
                {
                    return;
                }

                // 攻撃で撃破した攻撃側キャラの OnKill を発動（攻撃側の所有者 = isLocal）。
                // 反撃（OnAttacked / OnDestroy）等で攻撃側が場を離れている場合は発動しない。
                FieldView attackerField = isLocal ? _playerFieldView : _opponentFieldView;
                if (attackerField.Contains(attacker))
                {
                    await FireOnKillEffectAsync(attacker, isLocal, ct);
                }
            }
        }

        // 攻撃を宣言した攻撃側キャラをタップ（横向き）にする。「攻撃したらタップ」ルール。
        // 既にタップ済み、または攻撃前効果で場を離れた場合は何もしない。
        private async UniTask TapAttackerAsync(CardView attacker, bool isLocal, CancellationToken ct)
        {
            if (attacker == null || attacker.IsTapped)
            {
                return;
            }
            FieldView attackerField = isLocal ? _playerFieldView : _opponentFieldView;
            if (attackerField.Contains(attacker))
            {
                await attacker.SetTappedAsync(true, ct);
            }
        }

        private async UniTask FlyToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            await FlyCardToDestAsync(card, fromRect, graveyard, ct, delay, duration);
            graveyard.AddCard(card);
        }

        // デッキへの直接攻撃：突進 → 相手デッキ上から ATK 枚を墓地へ送る（ミル）→ オーバーリミット判定。
        // ATK 0 はシールドブロックで不発。ATK が相手デッキ枚数を超えると（0枚からさらにミルしようとして）相手が敗北（攻撃側の勝利）。
        private async UniTask ExecuteDeckAttackAsync(CardView attacker, bool isLocal, CancellationToken ct)
        {
            if (attacker == null)
            {
                return;
            }

            DeckView targetDeck = isLocal ? _opponentDeckView : _playerDeckView;

            await ResolveCharacterAttackEffectAsync(attacker, isLocal, ct);

            await PlayCardChargeAsync(attacker, targetDeck, ct);

            // デッキ攻撃も攻撃したらタップ（突進後に横向きへ倒れる）
            await TapAttackerAsync(attacker, isLocal, ct);

            int atk = attacker.CurrentAttack;
            if (atk <= 0)
            {
                await PlayShieldBlockEffectAsync(targetDeck, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
                return;
            }

            // ATK 枚を守る側（デッキの持ち主＝!isLocal）のデッキ上から墓地へ送る
            await MillDeckAsync(deckOwnerIsLocal: !isLocal, atk, ct);

            // 相手プレイヤー（デッキ）にダメージを与えた → 攻撃キャラの OnDealPlayerDamage 効果を発動。
            // 相手デッキが空（0枚）への攻撃はオーバーリミットで即敗北し _isGameOver が立つため、ここはスキップされる。
            if (!_isGameOver)
            {
                FieldView attackerField = isLocal ? _playerFieldView : _opponentFieldView;
                if (attackerField.Contains(attacker))
                {
                    await FireOnDealPlayerDamageEffectAsync(attacker, isLocal, ct);
                }
            }
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }

        // デッキミル共通処理：deckOwnerIsLocal のデッキ上から count 枚を1枚ずつ墓地へ送る（デッキ枚数が少なければ全枚数）。
        // 「ダメージトリガー」カードは墓地行きの代わりに、デッキの持ち主がコストを支払わずに使用する（ResolveGraveTriggerFromDeckAsync）。
        // オーバーリミット：1枚ミルする直前にデッキが空なら、その時点で持ち主がデッキ切れで敗北する（デッキを0枚に
        // したミルそのものでは負けず、さらにミルしようとした瞬間に負ける）。デッキ攻撃（ExecuteDeckAttackAsync）と
        // デッキダメージ効果（DamageEnemyDeck / DamageBothDecks）の共通の building-block。
        // デッキ順・カードデータは同期済みのため両クライアントで決定的に動作する（追加同期不要）。
        internal async UniTask MillDeckAsync(bool deckOwnerIsLocal, int count, CancellationToken ct)
        {
            DeckView deck = deckOwnerIsLocal ? _playerDeckView : _opponentDeckView;
            GraveyardView graveyard = deckOwnerIsLocal ? _playerGraveyardView : _opponentGraveyardView;

            // オーバーリミット：このミルでデッキが0枚へ落ちたら、生存を確認したうえで最後に1回告知する
            bool limitBroke = false;

            for (int i = 0; i < count; i++)
            {
                // オーバーリミット：1枚ミルする直前にデッキが空なら、その時点で持ち主が敗北
                // （デッキを0枚にしたミルそのものでは負けず、さらにミルしようとした瞬間に負ける）。
                if (_isGameOver || CheckDeckOutWin(isLocalDeck: deckOwnerIsLocal))
                {
                    return;
                }
                Rect deckRect = deck.worldBound;
                CardData milled = deck.DrawTop();
                deck.RefreshCount();
                if (milled == null)
                {
                    break;
                }
                // 「ダメージトリガー」カードは墓地行きの代わりに、デッキの持ち主がコストなしで使用する
                if (milled.TriggerOnGrave)
                {
                    await ResolveGraveTriggerFromDeckAsync(milled, ownerIsLocal: deckOwnerIsLocal, fromRect: deckRect, ct);
                    // オーバーリミット：このミルでデッキが0枚になったら告知予約（直後のミルで敗北する場合は告知しない）
                    limitBroke |= UpdateLimitBreak(isLocalDeck: deckOwnerIsLocal);
                    continue;
                }
                CardView milledCard = new CardView(_cardStore.CardTemplate, milled, _cardStore.CardBack, faceDown: false, isOpponent: !deckOwnerIsLocal);
                _soundPlayer.PlaySE(_soundStore.DeckDamageSE);
                await FlyToGraveyardAsync(milledCard, deckRect, graveyard, ct);
                // オーバーリミット：このミルでデッキが0枚になったら告知予約（直後のミルで敗北する場合は告知しない）
                limitBroke |= UpdateLimitBreak(isLocalDeck: deckOwnerIsLocal);
            }

            // オーバーリミット：一連のミルを生き残った場合のみ「リミットブレイク！」告知
            if (!_isGameOver && limitBroke)
            {
                await PlayLimitBreakAnnouncementAsync(ct);
            }
        }
    }
}

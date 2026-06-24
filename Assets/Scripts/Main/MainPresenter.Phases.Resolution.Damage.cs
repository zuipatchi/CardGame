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
        // 発動側から見た敵フィールドのキャラ全員に同時にダメージを与え、HP 0 以下のキャラを破壊する
        internal async UniTask ApplyDamageAllEnemiesAsync(int damage, bool isLocal, CancellationToken ct)
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
        internal async UniTask ApplyDamageEnemyAsync(int damage, int count, bool isLocal, CancellationToken ct)
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

        // 発動側から見た敵キャラを count 体（値1。未設定=0 は1体）対象に選び、それぞれを破壊して墓地へ送る。
        // 対象数が敵の数以上なら全員が対象。対象選択は DamageEnemy / Bounce と同じ仕組み（プレイヤー選択／CPU 自動／オンライン同期）を共用する。
        internal async UniTask ApplyBanishCharAsync(int count, bool isLocal, CancellationToken ct)
        {
            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            int enemyCount = targetField.Characters.Count;
            // 敵キャラがいなければ空振り。両クライアントで盤面は同期されており対称に判定されるため追加同期は不要
            if (enemyCount == 0)
            {
                return;
            }

            int targetCount = Mathf.Min(count <= 0 ? 1 : count, enemyCount);
            List<CardView> targets = await ResolveEnemyCharTargetsAsync(targetField, targetCount, enemyCount, isLocal, "破壊する相手キャラを選択", ct);
            if (targets.Count == 0)
            {
                return;
            }

            // 選んだ対象を順番に破壊して墓地へ送る
            List<CardView> destroyed = new List<CardView>();
            foreach (CardView target in targets)
            {
                if (!targetField.Contains(target))
                {
                    continue;
                }
                await PlayBanishCharEffectAsync(target, ct);
                Rect fromRect = target.worldBound;
                targetField.RemoveCard(target);
                await FlyCardToDestAsync(target, fromRect, targetGraveyard, ct);
                targetGraveyard.AddCard(target);
                destroyed.Add(target);
            }

            // 破壊された各キャラの OnDestroy を順番に発動（破壊されたキャラの所有者は発動側の相手 = !isLocal）
            foreach (CardView destroyedChar in destroyed)
            {
                await FireOnDestroyEffectAsync(destroyedChar, !isLocal, ct);
            }
        }

        // DebuffAttack：発動側から見た敵フィールドから count 体（値1。0=敵全員）選び、それぞれの攻撃力を
        // amount（値2）永続的に下げる（0未満にはならない）。対象数が敵の数以上なら全員。
        // 対象選択は DamageEnemy と同じ仕組み（プレイヤー選択／CPU 攻撃力上位／オンライン同期）を共用する。
        internal async UniTask ApplyDebuffSelectedEnemiesAsync(int amount, int count, bool isLocal, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }

            FieldView targetField = isLocal ? _opponentFieldView : _playerFieldView;
            int enemyCount = targetField.Characters.Count;
            if (enemyCount == 0)
            {
                return;
            }

            // 値1=0 は「敵キャラ全員」を対象にする
            int targetCount = count <= 0 ? enemyCount : Mathf.Min(count, enemyCount);
            List<CardView> targets = await ResolveEnemyCharTargetsAsync(targetField, targetCount, enemyCount, isLocal, "攻撃力を下げる相手キャラを選択", ct);
            if (targets.Count == 0)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>(targets.Count);
            foreach (CardView target in targets)
            {
                // 各対象で ATK 減算パルスと「攻撃ダウンN」フローティングラベルを同時に再生する
                tasks.Add(target.DebuffAttackAsync(amount, ct));
                tasks.Add(PlayFloatingLabelAsync($"攻撃ダウン{amount}", "debuff-attack-label", target, ct));
            }
            await UniTask.WhenAll(tasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
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
            return chars.OrderByDescending(c => c.CurrentAttack).Take(targetCount).ToList();
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
    }
}

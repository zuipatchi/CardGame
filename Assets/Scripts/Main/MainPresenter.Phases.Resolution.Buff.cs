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
        // 発動側の自フィールドのキャラ全員の HP を amount 回復する（最大HPでクランプ）。
        // amount <= 0 は最大HPまで全回復。自キャラがいなければ空振り。
        // 効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも対称に発動する（追加同期不要）。
        internal async UniTask ApplyHealAllAlliesAsync(int amount, bool isLocal, CancellationToken ct)
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

        // 発動側の自フィールドにいる、source と同じ特徴（Keyword）を持つキャラ（source 自身を除く）の
        // 攻撃力（isAttack=true）または HP（false）を amount 上げる（発動時に一度だけ永続加算）。
        // source の特徴が未設定（空）なら空振り。後から場に出たキャラには適用されない。
        // 効果はカードデータと同期済み盤面から決定的に解決されるため、オンラインでも対称に発動する（追加同期不要）。
        internal async UniTask ApplyBuffByKeywordAsync(CardView source, int amount, bool isAttack, bool isLocal, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }
            string keyword = source?.Data.Keyword;
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            List<CardView> targets = new List<CardView>();
            foreach (CardView c in ownField.Characters)
            {
                // source 自身は対象外（イベントカードは場のキャラに含まれないため自然に除外される）
                if (c == source)
                {
                    continue;
                }
                if (c.Data.Keyword == keyword)
                {
                    targets.Add(c);
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>(targets.Count);
            foreach (CardView target in targets)
            {
                tasks.Add(isAttack ? target.BuffAttackAsync(amount, ct) : target.BuffHpAsync(amount, ct));
            }
            await UniTask.WhenAll(tasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }

        // AtkBoost / HpBoost：発動側が自フィールドから count 体（値1。0=1体）選び、それぞれの
        // 攻撃力（isAttack=true）または HP（現在・最大。isAttack=false）を amount（値2）永続的に上げる。
        // 対象数が味方の数以上なら全員。対象選択はプレイヤー選択／CPU 自動／オンライン同期で分岐する。
        internal async UniTask ApplyBuffSelectedAlliesAsync(int amount, int count, bool isAttack, bool isLocal, CancellationToken ct)
        {
            if (amount <= 0)
            {
                return;
            }

            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            int ownCount = ownField.Characters.Count;
            if (ownCount == 0)
            {
                return;
            }

            // 値1=0 は「場の味方キャラ全員」を対象にする
            int targetCount = count <= 0 ? ownCount : Mathf.Min(count, ownCount);
            string prompt = isAttack ? "攻撃力を上げる味方を選択" : "HPを上げる味方を選択";
            List<CardView> targets = await ResolveAllyCharTargetsAsync(ownField, targetCount, ownCount, isLocal, prompt, ct);
            if (targets.Count == 0)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>(targets.Count);
            foreach (CardView target in targets)
            {
                tasks.Add(isAttack ? target.BuffAttackAsync(amount, ct) : target.BuffHpAsync(amount, ct));
            }
            await UniTask.WhenAll(tasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }

        // GrantKeyword：発動側が自フィールドから count 体（値1。0=場の味方全員）選び、
        // keywordValue（値2）が示すキーワード能力（1=守護/2=速攻/3=飛行/4=防人）を永続付与する。
        // 対象選択は AtkBoost / HpBoost と同じ仕組み（プレイヤー選択／CPU 自動／オンライン同期）を共用する。
        // 付与はカードデータ（値2）と同期済み盤面から決定的に解決されるため、オンラインでも対称に発動する。
        internal async UniTask ApplyGrantKeywordAsync(int keywordValue, int count, bool isLocal, CancellationToken ct)
        {
            GrantableKeyword keyword = GrantableKeywordExtensions.FromValue(keywordValue);
            if (keyword == GrantableKeyword.None)
            {
                return;
            }

            FieldView ownField = isLocal ? _playerFieldView : _opponentFieldView;
            int ownCount = ownField.Characters.Count;
            if (ownCount == 0)
            {
                return;
            }

            // 値1=0 は「場の味方キャラ全員」を対象にする
            int targetCount = count <= 0 ? ownCount : Mathf.Min(count, ownCount);
            string prompt = $"{keyword.DisplayName()}を付与する味方を選択";
            List<CardView> targets = await ResolveAllyCharTargetsAsync(ownField, targetCount, ownCount, isLocal, prompt, ct);
            if (targets.Count == 0)
            {
                return;
            }

            List<UniTask> tasks = new List<UniTask>(targets.Count);
            foreach (CardView target in targets)
            {
                tasks.Add(target.GrantKeywordAsync(keyword, ct));
            }
            await UniTask.WhenAll(tasks);
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);
        }

        // 対象決定：対象数が味方の数以上なら全員。そうでなければ、ローカルはプレイヤーが選択し、
        // オンライン相手はインデックス配列を受信、CPU は攻撃力上位を選ぶ。ResolveEnemyCharTargetsAsync の自フィールド版。
        private async UniTask<List<CardView>> ResolveAllyCharTargetsAsync(FieldView ownField, int targetCount, int ownCount, bool isLocal, string prompt, CancellationToken ct)
        {
            List<CardView> chars = new List<CardView>(ownField.Characters);

            // 全員が対象なら選択不要（両クライアントで決定的なので追加同期も不要）
            if (targetCount >= ownCount)
            {
                return chars;
            }

            if (isLocal)
            {
                List<CardView> selected = await WaitForPlayerAllyCharsSelectionAsync(targetCount, prompt, ct);
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

            // CPU：攻撃力の高い順に targetCount 体を選ぶ
            return chars.OrderByDescending(c => c.CurrentAttack).Take(targetCount).ToList();
        }

        // 自フィールドのキャラをハイライトし、プレイヤーが targetCount 体をクリックで選ぶのを待つ。
        // WaitForPlayerEnemyCharsSelectionAsync の自フィールド版（_allyCharSelect* を使う）。
        private async UniTask<List<CardView>> WaitForPlayerAllyCharsSelectionAsync(int targetCount, string prompt, CancellationToken ct)
        {
            _allyCharSelected = new List<CardView>();
            _allyCharSelectTarget = targetCount;
            _allyCharSelectPrompt = prompt;
            _allyCharSelectionTcs = new UniTaskCompletionSource<List<CardView>>();

            foreach (CardView c in _playerFieldView.Characters)
            {
                c.AddToClassList("selectable-char");
            }
            ShowToast($"{prompt}（あと{targetCount}体）");

            try
            {
                return await _allyCharSelectionTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _allyCharSelectionTcs = null;
                _allyCharSelectTarget = 0;
                _allyCharSelected = null;
                _allyCharSelectPrompt = null;
                foreach (CardView c in _playerFieldView.Characters)
                {
                    c.RemoveFromClassList("selectable-char");
                    c.RemoveFromClassList("selected-char");
                }
            }
        }

        // 自キャラ選択中のクリック処理：未選択のキャラを選択リストに加え、必要数に達したら確定する
        private void HandleAllyCharSelectionClick(CardView card)
        {
            if (_allyCharSelectionTcs == null || _allyCharSelected == null)
            {
                return;
            }
            if (card.Data is not CharacterCardData)
            {
                return;
            }
            if (_allyCharSelected.Contains(card))
            {
                return;
            }

            _allyCharSelected.Add(card);
            card.AddToClassList("selected-char");

            int remaining = _allyCharSelectTarget - _allyCharSelected.Count;
            if (remaining > 0)
            {
                ShowToast($"{_allyCharSelectPrompt}（あと{remaining}体）");
            }
            else
            {
                _allyCharSelectionTcs.TrySetResult(new List<CardView>(_allyCharSelected));
            }
        }
    }
}

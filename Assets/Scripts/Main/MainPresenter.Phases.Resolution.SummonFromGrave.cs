using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // SummonFromGrave 効果：発動側自身の墓地からキャラカードを count 体（0=1体）選んで自フィールドに出す（墓地から消費）。
        // 配置時に OnEnter も発動する。墓地にキャラがいない／フィールド満杯なら空振り。候補が count 以下なら全部。
        // 選択はプレイヤー（ピッカー）／CPU（高コスト順）／オンライン相手（墓地内インデックスを受信）で分岐する。
        // 墓地の並び順は両クライアントで同期済み（Recover と同じ前提）のため、インデックス指定で同じカードを取り除ける
        // （DamageEnemy と同じ NGS_DamageTarget チャネルを流用）。
        internal async UniTask ApplySummonFromGraveAsync(int count, bool isLocal, CancellationToken ct)
        {
            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;

            if (field.IsCharactersFull || _isGameOver)
            {
                return;
            }

            int wantCount = count <= 0 ? 1 : count;

            // 墓地内からキャラカードを抽出（インデックスは graveyard と一致）
            IReadOnlyList<CardData> graveCards = graveyard.GetCardDataSnapshot();
            List<int> candidates = new List<int>();
            for (int i = 0; i < graveCards.Count; i++)
            {
                if (graveCards[i] is CharacterCardData)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            // フィールドの空きスロット数で上限を打つ（墓地から抜いたのに場に出せず消える事故を防ぐ）。
            int availableSlots = FieldView.MaxCharacters - field.Characters.Count;
            int targetCount = Math.Min(Math.Min(wantCount, candidates.Count), availableSlots);
            if (targetCount <= 0)
            {
                return;
            }
            List<int> chosen = await ResolveGraveSummonTargetsAsync(candidates, graveCards, targetCount, isLocal, ct);
            if (chosen == null || chosen.Count == 0)
            {
                return;
            }

            // 墓地から抜く。RemoveCardAt はインデックスを詰めるため、降順で取り除いてから場へ出す。
            Dictionary<int, CardData> removed = new Dictionary<int, CardData>();
            List<int> descending = new List<int>(chosen);
            descending.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in descending)
            {
                CardData data = graveyard.RemoveCardAt(idx);
                if (data != null)
                {
                    removed[idx] = data;
                }
            }

            // 選んだ順に1体ずつ場へ出す（SummonChar と同じ配置・登場演出・OnEnter 経路を使う）。
            foreach (int idx in chosen)
            {
                if (_isGameOver || field.IsCharactersFull)
                {
                    return;
                }
                if (!removed.TryGetValue(idx, out CardData data))
                {
                    continue;
                }
                await SummonSingleCharAsync(data, field, isLocal, ct);
            }
        }

        // 召喚する墓地キャラのインデックス群を決定する。
        // ローカル：候補が targetCount 以下なら全部・多ければピッカーで選ぶ（選んだらオンラインへインデックス送信）。
        // オンライン相手：インデックス配列を受信。CPU：高コスト順に targetCount 体を選ぶ。
        private async UniTask<List<int>> ResolveGraveSummonTargetsAsync(List<int> candidates, IReadOnlyList<CardData> graveCards, int targetCount, bool isLocal, CancellationToken ct)
        {
            if (isLocal)
            {
                List<int> chosen = candidates.Count <= targetCount
                    ? new List<int>(candidates)
                    : await WaitForPlayerCardsPickAsync(candidates, graveCards, "墓地から召喚", "墓地から場に出すキャラを選ぶ", targetCount, ct, "deck-pick-card--no-hover");
                if (_isOnline)
                {
                    _networkGameService.SendDamageTargets(chosen.ToArray());
                }
                return chosen;
            }

            if (_isOnline)
            {
                int[] indices = await _networkGameService.WaitForOpponentDamageTargetsAsync(ct);
                return (indices != null && indices.Length > 0) ? new List<int>(indices) : new List<int>();
            }

            // CPU：高コスト順に targetCount 体を選ぶ
            List<int> sorted = new List<int>(candidates);
            sorted.Sort((a, b) => graveCards[b].Cost.CompareTo(graveCards[a].Cost));
            if (sorted.Count > targetCount)
            {
                sorted.RemoveRange(targetCount, sorted.Count - targetCount);
            }
            return sorted;
        }
    }
}

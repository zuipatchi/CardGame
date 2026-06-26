using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // AddToHandFromGrave 効果：発動側自身の墓地からカード（キャラ・イベント問わず）を count 枚（0=1枚）
        // 選んで手札に加える（墓地から消費）。場には出さないため OnEnter は発動しない。墓地が空なら空振り。
        // 候補が count 以下なら全部・多ければプレイヤーが選ぶ（CPU は高コスト順／オンライン相手は墓地内インデックスを受信）。
        // 手札が上限（8枚）に達したら超過分は墓地へ送る（Draw と同じバーン）。
        // 墓地の並び順は両クライアントで同期済み（SummonFromGrave / Recover と同じ前提）のため、
        // インデックス指定で同じカードを取り除ける（DamageEnemy と同じ NGS_DamageTarget チャネルを流用）。
        internal async UniTask ApplyAddToHandFromGraveAsync(int count, bool isLocal, CancellationToken ct)
        {
            if (_isGameOver)
            {
                return;
            }

            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            int wantCount = count <= 0 ? 1 : count;

            // 墓地内の全カードを候補にする（インデックスは graveyard と一致）。
            IReadOnlyList<CardData> graveCards = graveyard.GetCardDataSnapshot();
            List<int> candidates = new List<int>();
            for (int i = 0; i < graveCards.Count; i++)
            {
                if (graveCards[i] != null)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int targetCount = Math.Min(wantCount, candidates.Count);
            List<int> chosen = await ResolveGraveAddToHandTargetsAsync(candidates, graveCards, targetCount, isLocal, ct);
            if (chosen == null || chosen.Count == 0)
            {
                return;
            }

            // 墓地から抜く。RemoveCardAt はインデックスを詰めるため、降順で取り除いてから手札へ運ぶ。
            Dictionary<int, CardData> removed = new Dictionary<int, CardData>();
            List<int> descending = new List<int>(chosen);
            descending.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in descending)
            {
                if (idx < 0 || idx >= graveCards.Count)
                {
                    continue;
                }
                CardData data = graveyard.RemoveCardAt(idx);
                if (data != null)
                {
                    removed[idx] = data;
                }
            }

            // 選んだ順に1枚ずつ手札へ飛ばす（手札上限なら墓地へバーン）。
            HandView hand = isLocal ? _handView : _opponentHandView;
            foreach (int idx in chosen)
            {
                if (_isGameOver)
                {
                    return;
                }
                if (!removed.TryGetValue(idx, out CardData data))
                {
                    continue;
                }

                UnityEngine.Rect graveRect = graveyard.worldBound;
                PlayDrawSe();

                if (hand.IsFull)
                {
                    if (isLocal)
                    {
                        ShowToast("手札が上限 → 墓地へ");
                    }
                    await BurnDrawnCardAsync(data, graveRect, graveyard, isOpponent: !isLocal, ct);
                }
                else if (isLocal)
                {
                    await hand.AddCardAnimatedAsync(data, graveRect, 0f, ct);
                }
                else
                {
                    await PlayCpuDrawAsync(data, graveRect, ct);
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
        }

        // 手札に加える墓地カードのインデックス群を決定する。
        // ローカル：候補が targetCount 以下なら全部・多ければピッカーで選ぶ（選んだらオンラインへインデックス送信）。
        // オンライン相手：インデックス配列を受信。CPU：高コスト順に targetCount 枚を選ぶ。
        private async UniTask<List<int>> ResolveGraveAddToHandTargetsAsync(List<int> candidates, IReadOnlyList<CardData> graveCards, int targetCount, bool isLocal, CancellationToken ct)
        {
            if (isLocal)
            {
                List<int> chosen = candidates.Count <= targetCount
                    ? new List<int>(candidates)
                    : await WaitForPlayerCardsPickAsync(candidates, graveCards, "墓地から手札に加える", targetCount, ct, "deck-pick-card--no-hover");
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

            // CPU：高コスト順に targetCount 枚を選ぶ
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

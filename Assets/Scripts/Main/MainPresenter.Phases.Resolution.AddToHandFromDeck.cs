using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // AddToHandFromDeckByKeyword 効果：発動側のデッキから keyword と同じ特徴を持つカード（キャラ・イベント問わず）を
        // count 枚選んで手札に加える（デッキから消費）。候補が count 以下なら全部・特徴未設定／一致カードなしなら空振り。
        // 候補が count より多いときはプレイヤーが選ぶ（CPU は高コスト順／オンライン相手はデッキ内インデックスを受信）。
        // 手札が上限（8枚）に達したら超過分は墓地へ送る（Draw と同じバーン）。
        internal async UniTask ApplyAddToHandFromDeckByKeywordAsync(string keyword, int count, bool isLocal, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            DeckView deck = isLocal ? _playerDeckView : _opponentDeckView;
            int wantCount = count <= 0 ? 1 : count;

            // デッキ内から特徴一致カードを抽出（インデックスは deck と一致）
            IReadOnlyList<CardData> deckCards = deck.GetCardDataSnapshot();
            List<int> candidates = new List<int>();
            for (int i = 0; i < deckCards.Count; i++)
            {
                if (deckCards[i] != null && deckCards[i].Keyword == keyword)
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int targetCount = Math.Min(wantCount, candidates.Count);
            List<int> chosen = await ResolveDeckAddToHandTargetsAsync(candidates, deckCards, keyword, targetCount, isLocal, ct);
            if (chosen == null || chosen.Count == 0)
            {
                return;
            }

            // デッキから抜く。RemoveCardAt はインデックスを詰めるため、降順で取り除いてから手札へ運ぶ。
            Dictionary<int, CardData> removed = new Dictionary<int, CardData>();
            List<int> descending = new List<int>(chosen);
            descending.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in descending)
            {
                if (idx < 0 || idx >= deckCards.Count)
                {
                    continue;
                }
                CardData data = deck.RemoveCardAt(idx);
                if (data != null)
                {
                    removed[idx] = data;
                }
            }
            deck.RefreshCount();

            // 選んだ順に1枚ずつ手札へ飛ばす（手札上限なら墓地へバーン）。
            HandView hand = isLocal ? _handView : _opponentHandView;
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
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

                UnityEngine.Rect deckRect = deck.worldBound;
                PlayDrawSe();

                if (hand.IsFull)
                {
                    if (isLocal)
                    {
                        ShowToast("手札が上限 → 墓地へ");
                    }
                    await BurnDrawnCardAsync(data, deckRect, graveyard, isOpponent: !isLocal, ct);
                }
                else if (isLocal)
                {
                    await hand.AddCardAnimatedAsync(data, deckRect, 0f, ct);
                }
                else
                {
                    await PlayCpuDrawAsync(data, deckRect, ct);
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct);
        }

        // 手札に加えるデッキカードのインデックス群を決定する。
        // ローカル：候補が targetCount 以下なら全部・多ければピッカーで選ぶ（選んだらオンラインへインデックス送信）。
        // オンライン相手：インデックス配列を受信。CPU：高コスト順に targetCount 枚を選ぶ。
        private async UniTask<List<int>> ResolveDeckAddToHandTargetsAsync(List<int> candidates, IReadOnlyList<CardData> deckCards, string keyword, int targetCount, bool isLocal, CancellationToken ct)
        {
            if (isLocal)
            {
                List<int> chosen = candidates.Count <= targetCount
                    ? new List<int>(candidates)
                    : await WaitForPlayerDeckCardsSelectionAsync(candidates, deckCards, keyword, targetCount, ct);
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
            sorted.Sort((a, b) => deckCards[b].Cost.CompareTo(deckCards[a].Cost));
            if (sorted.Count > targetCount)
            {
                sorted.RemoveRange(targetCount, sorted.Count - targetCount);
            }
            return sorted;
        }

        // 候補カードを並べたオーバーレイを表示し、プレイヤーが count 枚タップで選ぶのを待つ。
        // タップで選択／再タップで解除、count 枚に達した時点で確定する。選んだデッキ内インデックスを返す。
        private async UniTask<List<int>> WaitForPlayerDeckCardsSelectionAsync(List<int> candidates, IReadOnlyList<CardData> deckCards, string keyword, int count, CancellationToken ct)
        {
            UniTaskCompletionSource<List<int>> tcs = new UniTaskCompletionSource<List<int>>();
            List<int> selected = new List<int>();

            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("deck-pick-overlay");

            VisualElement panel = new VisualElement();
            panel.AddToClassList("deck-pick-panel");

            VisualElement header = new VisualElement();
            header.AddToClassList("deck-pick-header");
            header.pickingMode = PickingMode.Ignore;

            Label title = new Label(string.IsNullOrEmpty(keyword) ? "カードを選択" : $"『{keyword}』を選択");
            title.AddToClassList("deck-pick-title");
            header.Add(title);

            Label subtitle = new Label("デッキから手札に加えるカードを選ぶ");
            subtitle.AddToClassList("deck-pick-subtitle");
            header.Add(subtitle);
            panel.Add(header);

            VisualElement divider = new VisualElement();
            divider.AddToClassList("deck-pick-divider");
            divider.pickingMode = PickingMode.Ignore;
            panel.Add(divider);

            VisualElement stage = new VisualElement();
            stage.AddToClassList("deck-pick-stage");

            ScrollView scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("deck-pick-scroll");
            scroll.contentContainer.AddToClassList("deck-pick-row");

            Label hint = new Label();
            hint.AddToClassList("deck-pick-hint");
            hint.pickingMode = PickingMode.Ignore;
            hint.text = $"カードをタップして選択（あと {count} 枚）";

            foreach (int idx in candidates)
            {
                CardData data = deckCards[idx];
                CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: false);
                card.AddToClassList("deck-pick-card");
                int captured = idx;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (selected.Contains(captured))
                    {
                        selected.Remove(captured);
                        card.RemoveFromClassList("deck-pick-card--selected");
                    }
                    else
                    {
                        if (selected.Count >= count)
                        {
                            return;
                        }
                        selected.Add(captured);
                        card.AddToClassList("deck-pick-card--selected");
                    }
                    hint.text = $"カードをタップして選択（あと {count - selected.Count} 枚）";
                    if (selected.Count == count)
                    {
                        tcs.TrySetResult(new List<int>(selected));
                    }
                });
                scroll.Add(card);
            }
            stage.Add(scroll);
            panel.Add(stage);
            panel.Add(hint);

            overlay.Add(panel);
            _mainRoot.Add(overlay);

            try
            {
                return await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                overlay.RemoveFromHierarchy();
            }
        }
    }
}

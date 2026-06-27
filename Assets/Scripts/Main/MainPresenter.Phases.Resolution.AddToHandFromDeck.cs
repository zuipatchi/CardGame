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

        // 候補カードを並べたオーバーレイを表示し、プレイヤーが count 枚選ぶのを待つ（共通実装へタイトルだけ渡す）。
        // 選んだデッキ内インデックスを返す。
        private UniTask<List<int>> WaitForPlayerDeckCardsSelectionAsync(List<int> candidates, IReadOnlyList<CardData> deckCards, string keyword, int count, CancellationToken ct)
        {
            string title = string.IsNullOrEmpty(keyword) ? "デッキから手札に加える" : $"『{keyword}』を手札に加える";
            return WaitForPlayerCardsPickAsync(candidates, deckCards, title, count, ct, "deck-pick-card--no-hover");
        }

        // 複数選択ピッカーのカードをタップしたとき、詳細モーダルを開く。
        // 詳細パネル下部の決定ボタンで選択／解除をトグルする。上限到達かつ未選択なら閲覧のみ（先に他を解除する必要がある）。
        private void ShowPickerCardDetailForToggle(CardData data, bool isSelected, bool canSelect, Action toggle)
        {
            if (isSelected)
            {
                _cardDetailModal.Show(data, "選択を解除", _ => toggle());
            }
            else if (canSelect)
            {
                _cardDetailModal.Show(data, "選択する", _ => toggle());
            }
            else
            {
                _cardDetailModal.Show(data);
            }
        }

        // ピッカー表示中に盤面を確認するためのトグルを付ける。モーダル（パネル）の右上に配置する。
        // 押すとパネル本体と暗幕を隠して盤面を見せ、もう一度押すと選択に戻る。
        // パネルは visibility:hidden で隠す（レイアウトは保つ）ことで、トグル自身はパネル右上の位置に残り続け押せる。
        private void AddPickerPeekToggle(VisualElement overlay, VisualElement panel)
        {
            Button toggle = new Button();
            toggle.AddToClassList("deck-pick-peek-toggle");
            toggle.text = "盤面を見る";
            bool peeking = false;
            toggle.clicked += () =>
            {
                peeking = !peeking;
                panel.EnableInClassList("deck-pick-panel--peek", peeking);
                overlay.EnableInClassList("deck-pick-overlay--peek", peeking);
                toggle.text = peeking ? "選択に戻る" : "盤面を見る";
            };
            panel.Add(toggle);
        }

        // 複数選択ピッカーの共通実装：候補カードを並べたオーバーレイを表示し、count 枚選ぶのを待つ。
        // カードのタップは詳細モーダルを開き、詳細パネルの決定ボタンで選択／解除をトグルする。
        // count 枚そろうと下部の「決定」ボタンが押せるようになり、押下で確定する。選んだ candidates 由来のインデックスを返す。
        // タイトルだけ差し替えてデッキ／墓地など供給元の異なる選択で共用する。
        // cardExtraClass を渡すと各候補カードに追加クラスを付ける（呼び出し元ごとにホバー演出などを変えるため）。
        private async UniTask<List<int>> WaitForPlayerCardsPickAsync(List<int> candidates, IReadOnlyList<CardData> sourceCards, string title, int count, CancellationToken ct, string cardExtraClass = null)
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

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("deck-pick-title");
            header.Add(titleLabel);
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
            hint.text = $"カードをタップして詳細を開き選択（あと {count} 枚）";

            // 必要枚数に達すると押せるようになる確定ボタン。
            Button confirmButton = new Button();
            confirmButton.AddToClassList("deck-pick-confirm");
            confirmButton.text = $"決定（0 / {count}）";
            confirmButton.SetEnabled(false);
            confirmButton.clicked += () =>
            {
                if (selected.Count == count)
                {
                    tcs.TrySetResult(new List<int>(selected));
                }
            };

            // カード1枚の選択/解除をトグルし、ヒントと決定ボタンの状態を更新する。
            void Toggle(int captured, CardView card)
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
                hint.text = $"カードをタップして詳細を開き選択（あと {count - selected.Count} 枚）";
                confirmButton.text = $"決定（{selected.Count} / {count}）";
                confirmButton.SetEnabled(selected.Count == count);
            }

            foreach (int idx in candidates)
            {
                CardData data = sourceCards[idx];
                CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: false);
                card.AddToClassList("deck-pick-card");
                if (!string.IsNullOrEmpty(cardExtraClass))
                {
                    card.AddToClassList(cardExtraClass);
                }
                int captured = idx;
                // クリックは詳細モーダルを開く。選択／解除は詳細パネル下部の決定ボタンで行う（誤タップ防止）。
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    bool isSelected = selected.Contains(captured);
                    bool canSelect = isSelected || selected.Count < count;
                    ShowPickerCardDetailForToggle(data, isSelected, canSelect, () => Toggle(captured, card));
                });
                scroll.Add(card);
            }
            stage.Add(scroll);
            panel.Add(stage);
            panel.Add(hint);
            panel.Add(confirmButton);

            overlay.Add(panel);
            AddPickerPeekToggle(overlay, panel);
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

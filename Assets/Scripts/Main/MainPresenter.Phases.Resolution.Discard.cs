using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Main.Card;
using Main.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ハンデス（EventType.Discard）：発動側から見た相手プレイヤーが手札を count 枚（値1。未設定=0 は1枚）捨てる。
        // 手札枚数が count 未満なら手札全部・手札が0枚なら空振り。捨てるカードは「手札の持ち主」が選ぶ。
        // ・オフラインで相手が CPU（isLocal=true）：CPU が低コスト順に自動で捨てる。
        // ・自分が被害者（isLocal=false。CPU 戦で相手＝CPU が発動／オンラインで相手が発動）：自分がピッカーで選ぶ。
        // ・オンラインで相手が被害者（isLocal=true）：相手が選んだ ID を受信し、相手の墓地へ送る（手札は裏向きで中身を持たないため）。
        internal async UniTask ApplyDiscardAsync(int count, bool isLocal, CancellationToken ct)
        {
            HandView targetHand = isLocal ? _opponentHandView : _handView;
            GraveyardView targetGraveyard = isLocal ? _opponentGraveyardView : _playerGraveyardView;

            int handCount = targetHand.Count;
            // 相手の手札が0枚なら空振り。盤面（手札枚数）は同期されているため両クライアントで対称に判定される。
            if (handCount == 0)
            {
                return;
            }

            int targetCount = Mathf.Min(count <= 0 ? 1 : count, handCount);

            if (isLocal)
            {
                // 発動側＝自分。相手（手札の持ち主）が捨てるカードを決める。
                if (_isOnline)
                {
                    // オンライン相手：相手（被害者）が選んで送ってきた実カード ID を受信し、相手の墓地へ送る。
                    string[] ids = await _networkGameService.WaitForOpponentDiscardedCardsAsync(ct);
                    await DiscardOpponentPlaceholdersByIdsAsync(ids, targetHand, targetGraveyard, ct);
                }
                else
                {
                    // オフライン相手＝CPU：低コスト順に自動で捨てる。
                    IReadOnlyList<CardView> cpuHand = targetHand.Cards;
                    List<CardData> cpuHandData = new List<CardData>(cpuHand.Count);
                    foreach (CardView c in cpuHand)
                    {
                        cpuHandData.Add(c.Data);
                    }
                    List<int> chosen = CpuAgent.ChooseDiscardIndices(cpuHandData, targetCount);
                    await DiscardHandCardsAsync(MapIndicesToCards(cpuHand, chosen), targetHand, targetGraveyard, ct);
                }
                return;
            }

            // 発動側＝相手。自分（手札の持ち主・ローカル人間）が捨てるカードを選ぶ。
            IReadOnlyList<CardView> myHand = targetHand.Cards;
            List<int> selectedIndices;
            if (targetCount >= handCount)
            {
                // 手札全部が対象 → 選択不要
                selectedIndices = new List<int>(handCount);
                for (int i = 0; i < handCount; i++)
                {
                    selectedIndices.Add(i);
                }
            }
            else
            {
                List<CardData> handData = new List<CardData>(myHand.Count);
                foreach (CardView c in myHand)
                {
                    handData.Add(c.Data);
                }
                selectedIndices = await WaitForPlayerHandDiscardSelectionAsync(handData, targetCount, ct);
            }

            List<CardView> toDiscard = MapIndicesToCards(myHand, selectedIndices);

            if (_isOnline)
            {
                // 選んだカードの ID を発動側へ送る（発動側は受信 ID のカードを相手の墓地へ送る）。
                string[] ids = new string[toDiscard.Count];
                for (int i = 0; i < toDiscard.Count; i++)
                {
                    ids[i] = toDiscard[i].Data.Id;
                }
                _networkGameService.SendDiscardedCards(ids);
            }

            await DiscardHandCardsAsync(toDiscard, targetHand, targetGraveyard, ct);
        }

        private static List<CardView> MapIndicesToCards(IReadOnlyList<CardView> cards, List<int> indices)
        {
            List<CardView> result = new List<CardView>(indices.Count);
            foreach (int idx in indices)
            {
                if (idx >= 0 && idx < cards.Count)
                {
                    result.Add(cards[idx]);
                }
            }
            return result;
        }

        // 手札にある実カード（CardView）を1枚ずつ墓地へ捨てる。
        private async UniTask DiscardHandCardsAsync(List<CardView> cards, HandView hand, GraveyardView graveyard, CancellationToken ct)
        {
            foreach (CardView card in cards)
            {
                if (_isGameOver)
                {
                    return;
                }
                Rect fromRect = card.worldBound;
                hand.RemoveCard(card);
                await BurnCardToGraveyardAsync(card, fromRect, graveyard, ct);
            }
        }

        // オンライン発動側：相手の手札は裏向きプレースホルダで中身を持たないため、受信した実カード ID 1件につき
        // プレースホルダを1枚除去し、その位置から実カードを生成して相手の墓地へ送る。
        private async UniTask DiscardOpponentPlaceholdersByIdsAsync(string[] ids, HandView placeholderHand, GraveyardView graveyard, CancellationToken ct)
        {
            if (ids == null)
            {
                return;
            }
            foreach (string id in ids)
            {
                if (_isGameOver)
                {
                    return;
                }
                IReadOnlyList<CardView> cards = placeholderHand.Cards;
                if (cards.Count == 0)
                {
                    break;
                }
                CardView placeholder = cards[cards.Count - 1];
                Rect fromRect = placeholder.worldBound;
                placeholderHand.RemoveCard(placeholder);

                if (_cardDatabase.TryGet(id, out CardData data))
                {
                    await BurnDrawnCardAsync(data, fromRect, graveyard, isOpponent: true, ct);
                }
            }
        }

        // 手札から捨てるカードを count 枚選ぶオーバーレイ（deck-pick スタイルを流用）。
        // カードのタップは詳細モーダルを開き、詳細パネルの決定ボタンで選択／解除をトグルする。
        // count 枚そろうと下部の「決定」ボタンが押せるようになり、押下で確定し、選んだ手札内インデックスを返す。
        private async UniTask<List<int>> WaitForPlayerHandDiscardSelectionAsync(IReadOnlyList<CardData> handCards, int count, CancellationToken ct)
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

            Label title = new Label("手札から捨てる");
            title.AddToClassList("deck-pick-title");
            header.Add(title);
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

            for (int i = 0; i < handCards.Count; i++)
            {
                CardData data = handCards[i];
                CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: false);
                card.AddToClassList("deck-pick-card");
                card.AddToClassList("deck-pick-card--no-scale");
                int captured = i;
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

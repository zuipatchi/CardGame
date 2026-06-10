using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Main.Card;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main
{
    public sealed partial class MainPresenter
    {
        // ─── コスト払い（手札から選択→墓地）─────────────────────────────

        private async UniTask<string[]> PayHandCostAsync(CardView card, HandView hand, GraveyardView graveyard, bool isLocalPlayer, CancellationToken ct, string[] costCardIds = null)
        {
            if (_isGameOver)
            {
                return Array.Empty<string>();
            }

            int cost = card.Data.Cost;
            if (cost <= 0)
            {
                return Array.Empty<string>();
            }

            List<(CardView costCard, Rect fromRect, Action beforeAnimate)> costEntries;

            if (isLocalPlayer)
            {
                List<CardView> selected = await WaitForPlayerCostSelectionAsync(cost, card.Data.Attribute, ct);
                costEntries = new List<(CardView, Rect, Action)>();
                foreach (CardView c in selected)
                {
                    CardView captured = c;
                    costEntries.Add((c, c.worldBound, () => hand.RemoveCard(captured)));
                }
            }
            else
            {
                costEntries = new List<(CardView, Rect, Action)>();
                IReadOnlyList<CardView> handCards = hand.Cards;

                if (costCardIds != null && costCardIds.Length > 0)
                {
                    // オンライン相手：手札はプレースホルダーなのでIDからデータを引いて新規CardViewを作る
                    // 手札の先頭N枚を視覚的に減らす（どのカードかは不明なので先頭から除去）
                    int toRemove = Mathf.Min(costCardIds.Length, handCards.Count);
                    List<Rect> fromRects = new List<Rect>();
                    List<CardView> placeholders = new List<CardView>();
                    for (int i = 0; i < costCardIds.Length; i++)
                    {
                        fromRects.Add(i < handCards.Count ? handCards[i].worldBound : hand.worldBound);
                    }
                    for (int i = 0; i < toRemove; i++)
                    {
                        placeholders.Add(handCards[i]);
                    }
                    for (int i = 0; i < costCardIds.Length; i++)
                    {
                        if (_cardDatabase.TryGet(costCardIds[i], out CardData costData))
                        {
                            CardView costCard = new CardView(_cardStore.CardTemplate, costData, _cardStore.CardBack, faceDown: false, isOpponent: true);
                            CardView placeholder = i < placeholders.Count ? placeholders[i] : null;
                            HandView capturedHand = hand;
                            Action beforeAnimate = placeholder != null
                                ? () => capturedHand.RemoveCard(placeholder)
                                : (Action)(() => { });
                            costEntries.Add((costCard, fromRects[i], beforeAnimate));
                        }
                    }
                }
                else
                {
                    // CPU：属性制約を満たしつつ、コスト値の合計が cost に達するまで手札を選ぶ
                    foreach (CardView c in ChooseCpuCostCards(card.Data, handCards))
                    {
                        costEntries.Add(MakeCpuCostEntry(c, hand));
                    }
                }
            }

            if (costEntries.Count == 0)
            {
                return Array.Empty<string>();
            }

            await UniTask.WhenAll(
                PlayHandCostFlyAsync(costEntries, graveyard, ct),
                PlayCostEffectAtCardAsync(card, ct));
            await UniTask.Delay(TimeSpan.FromSeconds(AnimationShortDelay), cancellationToken: ct);

            return isLocalPlayer
                ? costEntries.Select(e => e.costCard.Data.Id).ToArray()
                : Array.Empty<string>();
        }

        private (CardView costCard, Rect fromRect, Action beforeAnimate) MakeCpuCostEntry(CardView handCard, HandView hand)
        {
            CardView faceUpCard = new CardView(_cardStore.CardTemplate, handCard.Data, _cardStore.CardBack, faceDown: false, isOpponent: true);
            Rect from = handCard.worldBound;
            return (faceUpCard, from, () => hand.RemoveCard(handCard));
        }

        // CPU のコストカード選択：属性制約用に同属性 or White を1枚確保し、
        // 残りはコスト値（CostPaymentValue）の合計が cost に達するまで手札先頭から選ぶ。
        private static List<CardView> ChooseCpuCostCards(CardData played, IReadOnlyList<CardView> handCards)
        {
            int cost = played.Cost;
            CardAttribute neededAttr = played.Attribute;
            List<CardView> chosen = new List<CardView>();
            HashSet<CardView> used = new HashSet<CardView>();
            int paid = 0;

            if (neededAttr != CardAttribute.White)
            {
                foreach (CardView c in handCards)
                {
                    if (c.Data.Attribute == neededAttr || c.Data.Attribute == CardAttribute.White)
                    {
                        chosen.Add(c);
                        used.Add(c);
                        paid += c.Data.CostPaymentValue;
                        break;
                    }
                }
            }

            foreach (CardView c in handCards)
            {
                if (paid >= cost)
                {
                    break;
                }
                if (used.Contains(c))
                {
                    continue;
                }
                chosen.Add(c);
                used.Add(c);
                paid += c.Data.CostPaymentValue;
            }

            return chosen;
        }

        // ─── 手札コストカード飛翔演出 ─────────────────────────────────────

        private async UniTask PlayHandCostFlyAsync(List<(CardView card, Rect fromRect, Action beforeAnimate)> costEntries, GraveyardView graveyard, CancellationToken ct)
        {
            if (costEntries.Count == 0)
            {
                return;
            }

            const float FlyDuration = 0.3f;
            const float CardInterval = 0.06f;

            Rect toRect = graveyard.worldBound;
            float targetLeft = toRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = toRect.center.y - CardScaleConstants.CardHeight / 2f;

            for (int i = 0; i < costEntries.Count; i++)
            {
                (CardView c, Rect fromRect, Action beforeAnimate) = costEntries[i];

                beforeAnimate();

                float startLeft = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
                float startTop = fromRect.center.y - CardScaleConstants.CardHeight / 2f;

                c.style.position = Position.Absolute;
                c.style.left = startLeft;
                c.style.top = startTop;
                c.style.bottom = StyleKeyword.Null;
                c.style.width = StyleKeyword.Null;
                c.style.height = StyleKeyword.Null;
                c.style.rotate = new Rotate(0);
                c.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
                c.style.transformOrigin = StyleKeyword.Null;
                c.style.marginLeft = StyleKeyword.Null;
                c.style.marginRight = StyleKeyword.Null;
                _dragLayer.Add(c);

                if (c.IsFaceDown)
                {
                    await c.FlipAsync(ct);
                }

                float curLeft = startLeft;
                float curTop = startTop;
                float curScale = CardScaleConstants.HandDeck;
                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                Sequence seq = DOTween.Sequence()
                    .Join(DOTween.To(() => curLeft, v => { curLeft = v; c.style.left = v; }, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(() => curTop, v => { curTop = v; c.style.top = v; }, targetTop, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                        () => curScale,
                        s => { curScale = s; c.style.scale = new Scale(new Vector3(s, s, 1f)); },
                        0f, FlyDuration).SetEase(Ease.InQuad))
                    .OnComplete(() => tcs.TrySetResult());

                ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

                try { await tcs.Task; }
                catch (OperationCanceledException) { }

                if (c.parent == _dragLayer)
                {
                    _dragLayer.Remove(c);
                }
                graveyard.AddCard(c);

                if (i < costEntries.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CardInterval), cancellationToken: ct);
                }
            }
        }
    }
}

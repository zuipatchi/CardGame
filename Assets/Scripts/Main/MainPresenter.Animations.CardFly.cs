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
        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, isOpponent: true);
            card.style.position = Position.Absolute;
            card.style.left = deckRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = deckRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            Rect handRect = _opponentHandView.worldBound;
            float targetLeft = handRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = handRect.yMax - CardScaleConstants.CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                _dragLayer.Remove(card);
                return;
            }

            _dragLayer.Remove(card);
            card.pickingMode = PickingMode.Position;
            _opponentHandView.AcceptCard(card);
        }

        // ─── カード移動ヘルパー ──────────────────────────────────────────

        private async UniTask FlyCardToDestAsync(CardView card, Rect fromWorldRect, VisualElement dest, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromWorldRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromWorldRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            Rect destRect = dest.worldBound;
            float targetLeft = destRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = destRect.center.y - CardScaleConstants.CardHeight / 2f;

            if (delay > 0f)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    _dragLayer.Remove(card);
                    card.pickingMode = PickingMode.Position;
                    return;
                }
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            _dragLayer.Remove(card);
            card.pickingMode = PickingMode.Position;
        }

        // ─── Recover 飛翔アニメーション（墓地 → デッキへ同時飛翔）─────────────

        private async UniTask PlayRecoverFlyAsync(
            IReadOnlyList<CardData> cards, GraveyardView sourceGraveyard, DeckView targetDeck, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;
            const float CardInterval = 0.05f;

            Rect fromRect = sourceGraveyard.worldBound;
            float startLeft = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            float startTop = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
            Rect destRect = targetDeck.worldBound;
            float targetLeft = destRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = destRect.center.y - CardScaleConstants.CardHeight / 2f;

            List<CardView> tempCards = new List<CardView>(cards.Count);
            List<UniTaskCompletionSource> tcsList = new List<UniTaskCompletionSource>(cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                CardView tempCard = new CardView(_cardStore.CardTemplate, cards[i], _cardStore.CardBack, faceDown: false);
                tempCard.style.position = Position.Absolute;
                tempCard.style.left = startLeft;
                tempCard.style.top = startTop;
                tempCard.style.width = StyleKeyword.Null;
                tempCard.style.height = StyleKeyword.Null;
                tempCard.style.scale = new Scale(Vector3.one);
                _dragLayer.Add(tempCard);
                tempCards.Add(tempCard);

                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                tcsList.Add(tcs);

                float delay = i * CardInterval;
                CardView captured = tempCard;
                Sequence seq = DOTween.Sequence()
                    .AppendInterval(delay)
                    .Append(DOTween.To(() => captured.style.left.value.value, v => captured.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(() => captured.style.top.value.value, v => captured.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                        () => captured.style.scale.value.value.x,
                        s => captured.style.scale = new Scale(new Vector3(s, s, 1f)),
                        0f, FlyDuration).SetEase(Ease.InQuad))
                    .OnComplete(() => tcs.TrySetResult());

                ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });
            }

            try
            {
                await UniTask.WhenAll(tcsList.Select(t => t.Task));
            }
            catch (OperationCanceledException) { }

            foreach (CardView tempCard in tempCards)
            {
                if (tempCard.parent == _dragLayer)
                {
                    _dragLayer.Remove(tempCard);
                }
            }
        }

        // ─── デッキシャッフルパルス（デッキがスケールアップしてから戻る）────────

        private async UniTask PlayDeckShufflePulseAsync(DeckView deck, CancellationToken ct)
        {
            const float PulseDuration = 0.15f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Append(DOTween.To(
                    () => deck.style.scale.value.value.x,
                    s => deck.style.scale = new Scale(new Vector3(s, s, 1f)),
                    1.15f, PulseDuration).SetEase(Ease.OutQuad))
                .Append(DOTween.To(
                    () => deck.style.scale.value.value.x,
                    s => deck.style.scale = new Scale(new Vector3(s, s, 1f)),
                    1f, PulseDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            deck.style.scale = new Scale(Vector3.one);
        }

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
                    // CPU：属性制約を満たすコスト選択（同属性 or White を優先して1枚確保）
                    CardAttribute neededAttr = card.Data.Attribute;
                    int take = Mathf.Min(cost, handCards.Count);

                    if (neededAttr != CardAttribute.White)
                    {
                        int matchIdx = -1;
                        for (int i = 0; i < handCards.Count; i++)
                        {
                            CardAttribute a = handCards[i].Data.Attribute;
                            if (a == neededAttr || a == CardAttribute.White)
                            {
                                matchIdx = i;
                                break;
                            }
                        }

                        if (matchIdx >= 0)
                        {
                            CardView mc = handCards[matchIdx];
                            costEntries.Add(MakeCpuCostEntry(mc, hand));
                            for (int i = 0; i < handCards.Count && costEntries.Count < take; i++)
                            {
                                if (i != matchIdx)
                                {
                                    costEntries.Add(MakeCpuCostEntry(handCards[i], hand));
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < take; i++)
                            {
                                costEntries.Add(MakeCpuCostEntry(handCards[i], hand));
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < take; i++)
                        {
                            costEntries.Add(MakeCpuCostEntry(handCards[i], hand));
                        }
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

        // ─── マリガン：手札をデッキへ返すアニメーション ─────────────────────────

        private async UniTask PlayReturnHandToDeckAsync(HandView hand, DeckView deck, CancellationToken ct)
        {
            const float FlyDuration = 0.25f;
            const float Stagger = 0.06f;

            IReadOnlyList<CardView> snapshot = hand.Cards;
            if (snapshot.Count == 0)
            {
                return;
            }

            Rect deckRect = deck.worldBound;
            float targetLeft = deckRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = deckRect.center.y - CardScaleConstants.CardHeight / 2f;

            List<(CardView card, Rect fromRect)> entries = new List<(CardView, Rect)>();
            foreach (CardView c in snapshot)
            {
                entries.Add((c, c.worldBound));
            }

            foreach ((CardView c, Rect _) in entries)
            {
                hand.RemoveCard(c);
            }

            List<UniTask> tasks = new List<UniTask>();
            for (int i = 0; i < entries.Count; i++)
            {
                (CardView c, Rect fromRect) = entries[i];
                tasks.Add(FlyCardToDeckPositionAsync(c, fromRect, targetLeft, targetTop, i * Stagger, FlyDuration, ct));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask FlyCardToDeckPositionAsync(
            CardView card, Rect fromRect,
            float targetLeft, float targetTop,
            float delay, float duration,
            CancellationToken ct)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _dragLayer.Add(card);

            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }

            if (card.parent == _dragLayer)
            {
                _dragLayer.Remove(card);
            }
        }
    }
}

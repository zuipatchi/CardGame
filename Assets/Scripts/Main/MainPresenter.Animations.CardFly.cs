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
        // ─── ダメージ数字飛翔演出 ──────────────────────────────────────────

        private const float DamageIconAppearDuration = 0.3f;
        private const float DamageIconHoldDuration = 0.3f;
        private const float DamageIconFlyDuration = 0.75f;

        private async UniTask PlayDamageNumberFlyAsync(
            int damage, Vector2 fromWorldCenter, DeckView targetDeck, CancellationToken ct)
        {
            const float AppearDuration = DamageIconAppearDuration;
            const float HoldDuration = DamageIconHoldDuration;
            const float FlyDuration = DamageIconFlyDuration;
            const float ContainerSize = 220f;

            // ATKアイコン + ダメージ数字を重ねたコンテナ
            VisualElement container = new VisualElement();
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.width = ContainerSize;
            container.style.height = ContainerSize;
            container.style.opacity = 0f;
            container.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

            Vector2 fromLocal = _dragLayer.WorldToLocal(fromWorldCenter);
            float left = fromLocal.x - ContainerSize / 2f;
            float top = fromLocal.y - ContainerSize / 2f;
            container.style.left = left;
            container.style.top = top;

            VisualElement iconWrapper = new VisualElement();
            iconWrapper.pickingMode = PickingMode.Ignore;
            iconWrapper.AddToClassList("atk-counter-icon-wrapper");
            container.Add(iconWrapper);

            VisualElement icon = new VisualElement();
            icon.pickingMode = PickingMode.Ignore;
            icon.AddToClassList("damage-fly-icon");
            iconWrapper.Add(icon);

            // ダメージ数字をアイコンに重ねて表示
            Label label = new Label(damage.ToString());
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("damage-number-label");
            label.style.position = Position.Absolute;
            label.style.left = 0;
            label.style.right = 0;
            label.style.top = new StyleLength(new Length(35f, LengthUnit.Percent));
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(label);

            _dragLayer.Add(container);

            Vector2 deckLocal = _dragLayer.WorldToLocal(targetDeck.worldBound.center);
            float targetLeft = deckLocal.x - ContainerSize / 2f;
            float targetTop = deckLocal.y - ContainerSize / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => container.style.opacity.value, v => container.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => container.style.scale.value.value.x, v => container.style.scale = new Scale(new Vector3(v, v, 1f)), 1f, AppearDuration).SetEase(Ease.OutBack))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => left, v => { left = v; container.style.left = v; }, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => top, v => { top = v; container.style.top = v; }, targetTop, FlyDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            container.RemoveFromHierarchy();
        }

        // ─── CPU ドロー演出 ──────────────────────────────────────────────

        private async UniTask PlayCpuDrawAsync(CardData data, Rect deckRect, CancellationToken ct)
        {
            const float FlyDuration = 0.35f;

            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: true, isOpponent: true);
            card.style.position = Position.Absolute;
            card.style.left = deckRect.center.x - CardWidth / 2f;
            card.style.top = deckRect.center.y - CardHeight / 2f;
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            Rect handRect = _opponentHandView.worldBound;
            float targetLeft = handRect.center.x - CardWidth / 2f;
            float targetTop = handRect.yMax - CardHeight / 2f;

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

        private async UniTask FlyCharToSlotAsync(CardView card, Rect fromRect, CharacterSlotView slot, CancellationToken ct)
        {
            await FlyCardToDestAsync(card, fromRect, slot, ct);
            slot.PlaceCard(card);
        }

        private async UniTask FlySkillToSlotAsync(CardView card, Rect fromRect, CharacterSlotView slot, CancellationToken ct)
        {
            await FlyCardToDestAsync(card, fromRect, slot, ct);
            card.style.position = Position.Absolute;
            card.style.left = 0;
            card.style.top = 0;
            card.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            card.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            slot.Insert(0, card);
        }

        private async UniTask FlyCardToDestAsync(CardView card, Rect fromWorldRect, VisualElement dest, CancellationToken ct, float delay = 0f, float duration = CpuCardFlyDuration)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromWorldRect.center.x - CardWidth / 2f;
            card.style.top = fromWorldRect.center.y - CardHeight / 2f;
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
            float targetLeft = destRect.center.x - CardWidth / 2f;
            float targetTop = destRect.center.y - CardHeight / 2f;

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
            float startLeft = fromRect.center.x - CardWidth / 2f;
            float startTop = fromRect.center.y - CardHeight / 2f;
            Rect destRect = targetDeck.worldBound;
            float targetLeft = destRect.center.x - CardWidth / 2f;
            float targetTop = destRect.center.y - CardHeight / 2f;

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

        // ─── コスト払い（デッキ上から→墓地）───────────────────────────────

        private async UniTask PayCostAsync(CardView card, DeckView deck, GraveyardView graveyard, CancellationToken ct, bool announce = true)
        {
            if (_isGameOver)
            {
                return;
            }

            int cost = card.Data.Cost;
            if (cost <= 0)
            {
                return;
            }

            bool willLose = cost > deck.Count;

            Rect deckRect = deck.worldBound;
            List<CardView> costCards = deck.TakeFromTop(cost);

            List<UniTask> tasks = new List<UniTask>();
            tasks.Add(PlayDeckDamageAsync(costCards, deckRect, graveyard, deck, ct));
            tasks.Add(PlayCostEffectAtCardAsync(card, ct));
            if (announce)
            {
                string costClass = deck == _playerDeckView
                    ? "turn-announcement-label--cost"
                    : "turn-announcement-label--cost-opponent";
                tasks.Add(UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: ct)
                    .ContinueWith(() => PlayAnnouncementAsync($"PAY {cost} COSTS", costClass, ct)));
            }
            await UniTask.WhenAll(tasks);

            if (willLose)
            {
                _isGameOver = true;
                OnGameEnd(deck == _playerDeckView ? (bool?)false : true);
            }
        }

        // ─── デッキダメージ→墓地アニメーション ─────────────────────────────

        private async UniTask PlayDeckDamageAsync(
            List<CardView> cards, Rect fromRect, GraveyardView graveyard, DeckView sourceDeck, CancellationToken ct)
        {
            if (cards.Count == 0)
            {
                return;
            }

            const float FlyDuration = 0.3f;
            const float CardInterval = 0.06f;

            Rect toRect = graveyard.worldBound;
            float startLeft = fromRect.center.x - CardWidth / 2f;
            float startTop = fromRect.center.y - CardHeight / 2f;
            float targetLeft = toRect.center.x - CardWidth / 2f;
            float targetTop = toRect.center.y - CardHeight / 2f;

            List<(EventCardData data, bool isLocal)> graveTriggers = null;

            for (int i = 0; i < cards.Count; i++)
            {
                CardView c = cards[i];
                c.style.position = Position.Absolute;
                c.style.left = startLeft;
                c.style.top = startTop;
                c.style.width = StyleKeyword.Null;
                c.style.height = StyleKeyword.Null;
                c.style.rotate = new Rotate(0);
                c.style.scale = new Scale(Vector3.one);
                c.style.transformOrigin = StyleKeyword.Null;
                c.style.marginLeft = StyleKeyword.Null;
                c.style.marginRight = StyleKeyword.Null;
                _dragLayer.Add(c);           // UI Toolkit が DeckView から自動除去
                sourceDeck.OnCardRemovedVisually(); // デッキ表示を1枚分縮小
                c.FaceUp();

                UniTaskCompletionSource tcs = new UniTaskCompletionSource();
                Sequence seq = DOTween.Sequence()
                    .Join(DOTween.To(() => c.style.left.value.value, v => c.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(() => c.style.top.value.value, v => c.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                        () => c.style.scale.value.value.x,
                        s => c.style.scale = new Scale(new Vector3(s, s, 1f)),
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

                if (c.Data is EventCardData evData && evData.TriggerOnGrave)
                {
                    graveTriggers ??= new List<(EventCardData, bool)>();
                    graveTriggers.Add((evData, !c.IsOpponent));
                }

                if (i < cards.Count - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(CardInterval), cancellationToken: ct);
                }
            }

            if (graveTriggers != null)
            {
                foreach ((EventCardData data, bool isLocal) in graveTriggers)
                {
                    if (_isGameOver)
                    {
                        break;
                    }
                    await FireGraveTriggerAsync(data, isLocal, ct);
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
            float targetLeft = deckRect.center.x - CardWidth / 2f;
            float targetTop = deckRect.center.y - CardHeight / 2f;

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

        // ─── グレイブトリガー発動演出（カード＋カード名を画面中央に表示）────────────

        private async UniTask PlayGraveTriggerDisplayAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            const float AppearDuration = 0.25f;
            const float HoldDuration = 0.8f;
            const float FadeDuration = 0.25f;

            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            Rect graveBound = graveyard.worldBound;
            float cardLeft = graveBound.center.x - CardWidth / 2f;
            // プレイヤーの墓地（画面下）は上方向、相手の墓地（画面上）は下方向へ配置
            float cardTop = isLocal
                ? graveBound.yMin - CardHeight - 4f
                : graveBound.yMax + 4f;

            CardView tempCard = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false);
            tempCard.style.position = Position.Absolute;
            tempCard.style.left = cardLeft;
            tempCard.style.top = cardTop;
            tempCard.style.width = StyleKeyword.Null;
            tempCard.style.height = StyleKeyword.Null;
            tempCard.style.opacity = 0f;
            tempCard.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(tempCard);

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => tempCard.style.opacity.value, v => tempCard.style.opacity = v, 1f, AppearDuration).SetEase(Ease.OutQuad))
                .AppendInterval(HoldDuration)
                .Append(DOTween.To(() => tempCard.style.opacity.value, v => tempCard.style.opacity = v, 0f, FadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try { await tcs.Task; }
            catch (OperationCanceledException) { }

            if (tempCard.parent == _dragLayer)
            {
                _dragLayer.Remove(tempCard);
            }
        }

        private async UniTask FlyCardToDeckPositionAsync(
            CardView card, Rect fromRect,
            float targetLeft, float targetTop,
            float delay, float duration,
            CancellationToken ct)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromRect.center.x - CardWidth / 2f;
            card.style.top = fromRect.center.y - CardHeight / 2f;
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

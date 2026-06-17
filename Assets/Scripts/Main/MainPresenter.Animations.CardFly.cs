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

        // ─── 手札上限バーン（満杯時に手札へ入るはずだったカードを墓地へ送る）──────

        // 既存の CardView を fromRect から墓地へ飛ばし、表向きにして墓地へ追加する。
        private async UniTask BurnCardToGraveyardAsync(CardView card, Rect fromRect, GraveyardView graveyard, CancellationToken ct)
        {
            const float FlyDuration = 0.3f;

            card.style.position = Position.Absolute;
            card.style.left = fromRect.center.x - CardScaleConstants.CardWidth / 2f;
            card.style.top = fromRect.center.y - CardScaleConstants.CardHeight / 2f;
            card.style.bottom = StyleKeyword.Null;
            card.style.width = StyleKeyword.Null;
            card.style.height = StyleKeyword.Null;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(new Vector3(CardScaleConstants.HandDeck, CardScaleConstants.HandDeck, 1f));
            card.style.transformOrigin = StyleKeyword.Null;
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            card.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(card);

            // 墓地は公開情報のため表向きにする
            if (card.IsFaceDown)
            {
                await card.FlipAsync(ct);
            }

            Rect toRect = graveyard.worldBound;
            float targetLeft = toRect.center.x - CardScaleConstants.CardWidth / 2f;
            float targetTop = toRect.center.y - CardScaleConstants.CardHeight / 2f;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.InQuad))
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
            graveyard.AddCard(card);
        }

        // ドロー由来（手札に入るはずだったカードデータ）を CardView 化してバーンする。
        private UniTask BurnDrawnCardAsync(CardData data, Rect fromRect, GraveyardView graveyard, bool isOpponent, CancellationToken ct)
        {
            CardView card = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent);
            return BurnCardToGraveyardAsync(card, fromRect, graveyard, ct);
        }

        // キャラ等を所有者の手札へ戻す。手札が満杯なら墓地へ送る（バーン）。
        // toOpponentHand=true のときは相手の手札（裏向き表示）へ戻すため、戻す前に裏返す。
        private async UniTask ReturnCardToHandOrBurnAsync(CardView card, HandView hand, GraveyardView graveyard, Rect fromRect, bool toOpponentHand, CancellationToken ct)
        {
            if (hand.IsFull)
            {
                await BurnCardToGraveyardAsync(card, fromRect, graveyard, ct);
                return;
            }

            if (toOpponentHand && !card.IsFaceDown)
            {
                await card.FlipAsync(ct);
            }
            await hand.AddCardBackAsync(card, fromRect, ct);
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

        // ─── 墓地の永続イベント（OnTurnStart）発動演出 ─────────────────────────
        // 墓地のイベントカードデータから一時カードを生成し、墓地 → フィールド中央へせり出させて
        // 効果を発動し、墓地へ戻す。墓地のデータ自体は減らさない（毎ターン発動し続ける）。
        private async UniTask PlayGraveyardEventEffectAsync(EventCardData data, bool isLocal, CancellationToken ct)
        {
            GraveyardView graveyard = isLocal ? _playerGraveyardView : _opponentGraveyardView;
            FieldView field = isLocal ? _playerFieldView : _opponentFieldView;

            Rect graveRect = graveyard.worldBound;
            Rect fieldRect = field.worldBound;

            float graveLeft = graveRect.center.x - CardScaleConstants.CardWidth / 2f;
            float graveTop = graveRect.center.y - CardScaleConstants.CardHeight / 2f;
            float displayLeft = fieldRect.center.x - CardScaleConstants.CardWidth / 2f;
            float displayTop = fieldRect.center.y - CardScaleConstants.CardHeight / 2f;

            CardView temp = new CardView(_cardStore.CardTemplate, data, _cardStore.CardBack, faceDown: false, isOpponent: !isLocal);
            temp.style.position = Position.Absolute;
            temp.style.left = graveLeft;
            temp.style.top = graveTop;
            temp.style.scale = new Scale(Vector3.zero);
            temp.pickingMode = PickingMode.Ignore;
            _dragLayer.Add(temp);

            // 墓地 → フィールド中央へせり出す
            await TweenCardAbsoluteAsync(temp, displayLeft, displayTop, 1f, 0.3f, Ease.OutBack, ct);

            // 効果発動（temp.worldBound が有効な状態で、種別ごとの演出込みで解決）
            await ResolveEventCardEffectAsync(data, temp, isLocal, ct);

            // フィールド → 墓地へ戻る（データは墓地に残ったまま）
            if (temp.parent == _dragLayer)
            {
                await TweenCardAbsoluteAsync(temp, graveLeft, graveTop, 0f, 0.25f, Ease.InQuad, ct);
                if (temp.parent == _dragLayer)
                {
                    _dragLayer.Remove(temp);
                }
            }
        }

        // _dragLayer 上の絶対配置カードの left / top / scale を同時にトゥイーンする（カードは layer に残す）
        private async UniTask TweenCardAbsoluteAsync(CardView card, float targetLeft, float targetTop, float targetScale, float duration, Ease ease, CancellationToken ct)
        {
            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence seq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, duration).SetEase(ease))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, duration).SetEase(ease))
                .Join(DOTween.To(
                    () => card.style.scale.value.value.x,
                    s => card.style.scale = new Scale(new Vector3(s, s, 1f)),
                    targetScale, duration).SetEase(ease))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() => { seq.Kill(); tcs.TrySetCanceled(); });

            try
            {
                await tcs.Task;
            }
            catch (OperationCanceledException) { }
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

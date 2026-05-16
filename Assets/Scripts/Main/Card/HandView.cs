using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class HandView : VisualElement
    {
        private const float CardWidth = 160f;
        private const float CardHeight = 220f;
        private const float CardSpacing = 100f;
        private const float MaxAngleDeg = 20f;
        private const float ArcLiftMax = 20f;
        private const float HoverScaleTarget = 1.5f;
        private const float HoverDuration = 0.15f;
        private const float RelayoutDuration = 0.2f;
        private const float FlyDuration = 0.3f;

        private struct HandCardEntry
        {
            public CardView Card;
            public Tween ScaleTween;
        }

        public Func<CardView, Vector2, bool> OnCardDropped;

        private readonly VisualTreeAsset _cardTemplate;
        private readonly Texture2D _backImage;
        private readonly VisualElement _dragLayer;
        private readonly bool _interactive;
        private readonly List<HandCardEntry> _entries = new List<HandCardEntry>();

        public HandView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null, VisualElement dragLayer = null, bool faceDown = false, bool interactive = true)
        {
            _cardTemplate = cardTemplate;
            _backImage = backImage;
            _dragLayer = dragLayer;
            _interactive = interactive;

            style.overflow = Overflow.Visible;
            style.height = CardHeight + ArcLiftMax;

            if (cards.Length == 0)
            {
                return;
            }

            foreach (CardData data in cards)
            {
                CardView card = new CardView(cardTemplate, data, backImage, faceDown);
                SetupCardInHand(card);
                _entries.Add(new HandCardEntry { Card = card });
                Add(card);
            }

            ApplyPositions(animate: false);
            if (interactive)
            {
                RegisterCallbacks();
            }
        }

        public async UniTask AddCardAnimatedAsync(CardData data, Rect deckWorldRect, float startDelay = 0f, CancellationToken ct = default)
        {
            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: ct);
            }

            CardView card = new CardView(_cardTemplate, data, _backImage, faceDown: true);
            card.style.position = Position.Absolute;
            card.style.left = deckWorldRect.x;
            card.style.top = deckWorldRect.y;
            _dragLayer.Add(card);

            Rect handRect = worldBound;
            float targetLeft = handRect.center.x - CardWidth / 2f;
            float targetTop = handRect.yMin;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence flySeq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() =>
            {
                flySeq.Kill();
                tcs.TrySetCanceled();
            });

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
            SetupCardInHand(card);
            _entries.Add(new HandCardEntry { Card = card });
            Add(card);
            ApplyPositions(animate: true);

            if (_interactive)
            {
                await card.FlipAsync(ct);
                RegisterCardCallbacks(card);
            }
        }

        public IReadOnlyList<CardView> Cards
        {
            get
            {
                List<CardView> result = new List<CardView>(_entries.Count);
                foreach (HandCardEntry entry in _entries)
                {
                    result.Add(entry.Card);
                }
                return result;
            }
        }

        public void RemoveCard(CardView card)
        {
            int idx = IndexOf(card);
            if (idx < 0)
            {
                return;
            }

            HandCardEntry entry = _entries[idx];
            entry.ScaleTween?.Kill();
            DOTween.Kill(card);
            _entries.RemoveAt(idx);
            card.RemoveFromHierarchy();
            ApplyPositions(animate: true);
        }

        public async UniTask AddCardBackAsync(CardView card, Rect fromWorldRect, CancellationToken ct = default)
        {
            card.style.position = Position.Absolute;
            card.style.left = fromWorldRect.x;
            card.style.top = fromWorldRect.y;
            card.style.rotate = new Rotate(0);
            card.style.scale = new Scale(Vector3.one);
            card.style.marginLeft = StyleKeyword.Null;
            card.style.marginRight = StyleKeyword.Null;
            _dragLayer.Add(card);

            Rect handRect = worldBound;
            float targetLeft = handRect.center.x - CardWidth / 2f;
            float targetTop = handRect.yMin;

            UniTaskCompletionSource tcs = new UniTaskCompletionSource();
            Sequence flySeq = DOTween.Sequence()
                .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, FlyDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(() => card.style.top.value.value, v => card.style.top = v, targetTop, FlyDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => tcs.TrySetResult());

            ct.Register(() =>
            {
                flySeq.Kill();
                tcs.TrySetCanceled();
            });

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
            SetupCardInHand(card);
            _entries.Add(new HandCardEntry { Card = card });
            Add(card);
            ApplyPositions(animate: true);

            if (_interactive)
            {
                ReattachDragManipulator(card);
            }
        }

        public void AcceptCard(CardView card)
        {
            SetupCardInHand(card);
            _entries.Add(new HandCardEntry { Card = card });
            Add(card);
            ApplyPositions(animate: true);
        }

        private static void SetupCardInHand(CardView card)
        {
            card.style.position = Position.Absolute;
            card.style.left = 0;
            card.style.top = StyleKeyword.Null;
            card.style.bottom = 0;
            card.style.scale = new Scale(Vector3.one);
            card.style.transformOrigin = new TransformOrigin(
                new Length(50f, LengthUnit.Percent),
                new Length(100f, LengthUnit.Percent)
            );
        }

        private void ApplyPositions(bool animate)
        {
            int count = _entries.Count;
            style.width = count > 0 ? (count - 1) * CardSpacing + CardWidth : 0;

            float centerIndex = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float relativePos = count > 1 ? (i - centerIndex) / centerIndex : 0f;
                float targetLeft = i * CardSpacing;
                float targetBottom = (1f - relativePos * relativePos) * ArcLiftMax;
                float targetAngle = relativePos * MaxAngleDeg;

                CardView card = _entries[i].Card;
                if (!animate)
                {
                    card.style.left = targetLeft;
                    card.style.bottom = targetBottom;
                    card.style.rotate = new Rotate(targetAngle);
                }
                else
                {
                    DOTween.Kill(card);
                    DOTween.Sequence()
                        .SetTarget(card)
                        .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, RelayoutDuration).SetEase(Ease.OutQuad))
                        .Join(DOTween.To(() => card.style.bottom.value.value, v => card.style.bottom = v, targetBottom, RelayoutDuration).SetEase(Ease.OutQuad))
                        .Join(DOTween.To(() => card.style.rotate.value.angle.value, v => card.style.rotate = new Rotate(v), targetAngle, RelayoutDuration).SetEase(Ease.OutQuad));
                }
            }
        }

        private void RegisterCallbacks()
        {
            foreach (HandCardEntry entry in _entries)
            {
                RegisterCardCallbacks(entry.Card);
            }
        }

        private void RegisterCardCallbacks(CardView card)
        {
            CardView capturedCard = card;

            capturedCard.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (capturedCard.parent != this)
                {
                    return;
                }

                int idx = IndexOf(capturedCard);
                if (idx < 0)
                {
                    return;
                }

                HandCardEntry entry = _entries[idx];
                entry.ScaleTween?.Kill();
                capturedCard.BringToFront();
                entry.ScaleTween = DOTween.To(
                    () => capturedCard.style.scale.value.value.x,
                    s => capturedCard.style.scale = new Scale(new Vector3(s, s, 1f)),
                    HoverScaleTarget,
                    HoverDuration
                ).SetEase(Ease.OutQuad);
                _entries[idx] = entry;
            });

            capturedCard.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (capturedCard.parent != this)
                {
                    return;
                }

                int idx = IndexOf(capturedCard);
                if (idx < 0)
                {
                    return;
                }

                HandCardEntry entry = _entries[idx];
                entry.ScaleTween?.Kill();
                Insert(Math.Min(idx, childCount - 1), capturedCard);
                entry.ScaleTween = DOTween.To(
                    () => capturedCard.style.scale.value.value.x,
                    s => capturedCard.style.scale = new Scale(new Vector3(s, s, 1f)),
                    1f,
                    HoverDuration
                ).SetEase(Ease.OutQuad);
                _entries[idx] = entry;
            });

            if (_dragLayer == null)
            {
                return;
            }

            capturedCard.RegisterCallback<PointerDownEvent>(_ =>
            {
                int idx = IndexOf(capturedCard);
                if (idx >= 0)
                {
                    HandCardEntry entry = _entries[idx];
                    entry.ScaleTween?.Kill();
                    entry.ScaleTween = null;
                    _entries[idx] = entry;
                }
            });

            ReattachDragManipulator(capturedCard);
        }

        private void ReattachDragManipulator(CardView card)
        {
            if (_dragLayer == null)
            {
                return;
            }

            CardView capturedCard = card;
            CardDragManipulator manipulator = new CardDragManipulator(_dragLayer);
            manipulator.OnDrop = worldPos =>
            {
                bool placed = OnCardDropped?.Invoke(capturedCard, worldPos) ?? false;
                if (placed)
                {
                    int idx = IndexOf(capturedCard);
                    if (idx >= 0)
                    {
                        _entries[idx].ScaleTween?.Kill();
                        _entries.RemoveAt(idx);
                    }

                    ApplyPositions(animate: true);
                }

                return placed;
            };
            capturedCard.AttachDragManipulator(manipulator);
        }

        private int IndexOf(CardView card)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Card == card)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

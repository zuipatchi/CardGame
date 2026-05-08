using System;
using System.Collections.Generic;
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

        public Func<CardView, Vector2, bool> OnCardDropped;

        private readonly List<CardView> _cards = new List<CardView>();
        private readonly List<Tween> _scaleTweens = new List<Tween>();

        public HandView(VisualTreeAsset cardTemplate, CardData[] cards, Texture2D backImage = null, VisualElement dragLayer = null, bool faceDown = false, bool interactive = true)
        {
            style.overflow = Overflow.Visible;
            style.height = CardHeight + ArcLiftMax;

            if (cards.Length == 0)
            {
                return;
            }

            foreach (CardData data in cards)
            {
                CardView card = new CardView(cardTemplate, data, backImage, faceDown);
                card.style.position = Position.Absolute;
                card.style.scale = new Scale(Vector3.one);
                card.style.transformOrigin = new TransformOrigin(
                    new Length(50f, LengthUnit.Percent),
                    new Length(100f, LengthUnit.Percent)
                );
                _cards.Add(card);
                _scaleTweens.Add(null);
                Add(card);
            }

            ApplyPositions(animate: false);
            if (interactive)
            {
                RegisterCallbacks(dragLayer);
            }
        }

        private void ApplyPositions(bool animate)
        {
            int count = _cards.Count;
            style.width = count > 0 ? (count - 1) * CardSpacing + CardWidth : 0;

            float centerIndex = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float relativePos = count > 1 ? (i - centerIndex) / centerIndex : 0f;
                float targetLeft = i * CardSpacing;
                float targetBottom = (1f - relativePos * relativePos) * ArcLiftMax;
                float targetAngle = relativePos * MaxAngleDeg;

                CardView card = _cards[i];
                if (!animate)
                {
                    card.style.left = targetLeft;
                    card.style.bottom = targetBottom;
                    card.style.rotate = new Rotate(targetAngle);
                }
                else
                {
                    DOTween.Sequence()
                        .Join(DOTween.To(() => card.style.left.value.value, v => card.style.left = v, targetLeft, RelayoutDuration).SetEase(Ease.OutQuad))
                        .Join(DOTween.To(() => card.style.bottom.value.value, v => card.style.bottom = v, targetBottom, RelayoutDuration).SetEase(Ease.OutQuad))
                        .Join(DOTween.To(() => card.style.rotate.value.angle.value, v => card.style.rotate = new Rotate(v), targetAngle, RelayoutDuration).SetEase(Ease.OutQuad));
                }
            }
        }

        private void RegisterCallbacks(VisualElement dragLayer)
        {
            foreach (CardView card in _cards)
            {
                CardView capturedCard = card;

                capturedCard.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (capturedCard.parent != this)
                    {
                        return;
                    }

                    int idx = _cards.IndexOf(capturedCard);
                    if (idx < 0)
                    {
                        return;
                    }

                    _scaleTweens[idx]?.Kill();
                    capturedCard.BringToFront();
                    _scaleTweens[idx] = DOTween.To(
                        () => capturedCard.style.scale.value.value.x,
                        s => capturedCard.style.scale = new Scale(new Vector3(s, s, 1f)),
                        HoverScaleTarget,
                        HoverDuration
                    ).SetEase(Ease.OutQuad);
                });

                capturedCard.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (capturedCard.parent != this)
                    {
                        return;
                    }

                    int idx = _cards.IndexOf(capturedCard);
                    if (idx < 0)
                    {
                        return;
                    }

                    _scaleTweens[idx]?.Kill();
                    Insert(Math.Min(idx, childCount - 1), capturedCard);
                    _scaleTweens[idx] = DOTween.To(
                        () => capturedCard.style.scale.value.value.x,
                        s => capturedCard.style.scale = new Scale(new Vector3(s, s, 1f)),
                        1f,
                        HoverDuration
                    ).SetEase(Ease.OutQuad);
                });

                if (dragLayer == null)
                {
                    continue;
                }

                capturedCard.RegisterCallback<PointerDownEvent>(_ =>
                {
                    int idx = _cards.IndexOf(capturedCard);
                    if (idx >= 0)
                    {
                        _scaleTweens[idx]?.Kill();
                        _scaleTweens[idx] = null;
                    }
                });

                CardDragManipulator manipulator = new CardDragManipulator(dragLayer);
                manipulator.OnDrop = worldPos =>
                {
                    bool placed = OnCardDropped?.Invoke(capturedCard, worldPos) ?? false;
                    if (placed)
                    {
                        int idx = _cards.IndexOf(capturedCard);
                        if (idx >= 0)
                        {
                            _scaleTweens[idx]?.Kill();
                            _scaleTweens.RemoveAt(idx);
                            _cards.RemoveAt(idx);
                        }

                        ApplyPositions(animate: true);
                    }

                    return placed;
                };
                capturedCard.AttachDragManipulator(manipulator);
            }
        }
    }
}

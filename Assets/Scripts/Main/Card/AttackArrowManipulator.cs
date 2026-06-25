using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class AttackArrowManipulator : PointerManipulator
    {
        public Func<Vector2, bool> OnAttackTarget;
        public Func<bool> CanStart;

        private readonly VisualElement _dragLayer;
        private ArrowView _arrowView;
        private bool _isDragging;

        // 矢印をドラッグ中か。盤面再構築（RefreshAttackInput）で他キャラの攻撃解決後に
        // この矢印を壊さないよう、ドラッグ中の矢印は維持するための判定に使う。
        public bool IsDragging => _isDragging;

        public AttackArrowManipulator(VisualElement dragLayer)
        {
            _dragLayer = dragLayer;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (CanStart != null && !CanStart())
            {
                return;
            }

            _isDragging = true;
            _arrowView = new ArrowView();
            _arrowView.StartPoint = target.worldBound.center;
            _arrowView.EndPoint = evt.position;
            _dragLayer.Add(_arrowView);
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            _arrowView.EndPoint = evt.position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            _isDragging = false;
            target.ReleasePointer(evt.pointerId);
            bool keepArrow = OnAttackTarget?.Invoke(evt.position) ?? false;
            if (!keepArrow)
            {
                RemoveArrow();
            }
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                RemoveArrow();
            }
        }

        public void ClearArrow()
        {
            RemoveArrow();
        }

        private void RemoveArrow()
        {
            _arrowView?.RemoveFromHierarchy();
            _arrowView = null;
        }
    }
}

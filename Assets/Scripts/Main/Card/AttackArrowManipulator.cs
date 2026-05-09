using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class AttackArrowManipulator : PointerManipulator
    {
        public Action<Vector2> OnAttackTarget;

        private readonly VisualElement _dragLayer;
        private ArrowView _arrowView;
        private bool _isDragging;

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
            RemoveArrow();
            OnAttackTarget?.Invoke(evt.position);
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

        private void RemoveArrow()
        {
            _arrowView?.RemoveFromHierarchy();
            _arrowView = null;
        }
    }
}

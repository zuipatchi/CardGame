using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardDragManipulator : PointerManipulator
    {
        public Func<Vector2, bool> OnDrop;

        private readonly VisualElement _dragLayer;
        private VisualElement _originalParent;
        private int _originalIndex;
        private StyleLength _originalLeft;
        private StyleLength _originalBottom;
        private StyleRotate _originalRotate;
        private StyleScale _originalScale;
        private Vector2 _startPointerPosition;
        private Vector2 _startElementPosition;
        private bool _isDragging;

        public CardDragManipulator(VisualElement dragLayer)
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
            _startPointerPosition = evt.position;
            _startElementPosition = target.worldBound.position;

            _originalParent = target.parent;
            _originalIndex = _originalParent.IndexOf(target);
            _originalLeft = target.style.left;
            _originalBottom = target.style.bottom;
            _originalRotate = target.style.rotate;
            _originalScale = target.style.scale;

            _dragLayer.Add(target);
            target.style.position = Position.Absolute;
            target.style.left = _startElementPosition.x;
            target.style.top = _startElementPosition.y;
            target.style.bottom = StyleKeyword.Null;
            target.style.rotate = new Rotate(0);
            target.style.scale = new Scale(Vector3.one);

            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector2 delta = (Vector2)evt.position - _startPointerPosition;
            target.style.left = _startElementPosition.x + delta.x;
            target.style.top = _startElementPosition.y + delta.y;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            _isDragging = false;
            target.ReleasePointer(evt.pointerId);

            bool placed = OnDrop?.Invoke(evt.position) ?? false;
            if (!placed)
            {
                SnapBack();
            }

            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SnapBack();
            }
        }

        private void SnapBack()
        {
            _originalParent.Insert(_originalIndex, target);
            target.style.position = Position.Absolute;
            target.style.left = _originalLeft;
            target.style.top = StyleKeyword.Null;
            target.style.bottom = _originalBottom;
            target.style.rotate = _originalRotate;
            target.style.scale = _originalScale;
        }
    }
}

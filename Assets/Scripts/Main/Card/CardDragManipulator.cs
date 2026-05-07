using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardDragManipulator : PointerManipulator
    {
        private Vector2 _startPointerPosition;
        private Vector2 _startElementPosition;
        private bool _isDragging;

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
            // worldBound で現在の画面上の位置を記録してから Absolute に切り替える
            _startElementPosition = target.worldBound.position;
            target.style.position = Position.Absolute;
            target.style.left = _startElementPosition.x;
            target.style.top = _startElementPosition.y;
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
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _isDragging = false;
        }
    }
}

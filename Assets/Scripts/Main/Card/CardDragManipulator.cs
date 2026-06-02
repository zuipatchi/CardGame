using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class CardDragManipulator : PointerManipulator
    {
        public Func<Vector2, bool> OnDrop;
        public Action OnClick;
        public Action OnRightClick;
        public Func<VisualElement> CreateGhost;
        public Func<bool> CanDrag;

        private const float ClickMovementThreshold = 8f;

        private readonly VisualElement _dragLayer;
        private VisualElement _originalParent;
        private int _originalIndex;
        private StyleEnum<Position> _originalPosition;
        private StyleLength _originalLeft;
        private StyleLength _originalBottom;
        private StyleLength _originalTop;
        private StyleRotate _originalRotate;
        private StyleScale _originalScale;
        private StyleTransformOrigin _originalTransformOrigin;
        private Vector2 _startPointerPosition;
        private Vector2 _startElementPosition;
        private bool _isDragging;
        private bool _moved;
        private bool _clickTrackingOnly;
        private VisualElement _ghost;

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
            if (evt.button == 1)
            {
                OnRightClick?.Invoke();
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
            {
                evt.StopPropagation();
                return;
            }

            _moved = false;
            _startPointerPosition = evt.position;
            _startElementPosition = target.worldBound.position;

            if (CanDrag != null && !CanDrag())
            {
                _clickTrackingOnly = true;
                _isDragging = false;
                target.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            _clickTrackingOnly = false;
            StartDrag();

            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_clickTrackingOnly && target.HasPointerCapture(evt.pointerId))
            {
                Vector2 delta = (Vector2)evt.position - _startPointerPosition;
                if (delta.magnitude > ClickMovementThreshold)
                {
                    _moved = true;
                }

                if (_moved && CanDrag != null && CanDrag())
                {
                    _clickTrackingOnly = false;
                    _startPointerPosition = evt.position;
                    StartDrag();
                }
                else
                {
                    return;
                }
            }

            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector2 moveDelta = (Vector2)evt.position - _startPointerPosition;
            if (moveDelta.magnitude > ClickMovementThreshold)
            {
                _moved = true;
            }

            VisualElement dragTarget = _ghost ?? target;
            dragTarget.style.left = _startElementPosition.x + moveDelta.x;
            dragTarget.style.top = _startElementPosition.y + moveDelta.y;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_clickTrackingOnly && target.HasPointerCapture(evt.pointerId))
            {
                _clickTrackingOnly = false;
                target.ReleasePointer(evt.pointerId);
                if (!_moved)
                {
                    OnClick?.Invoke();
                }
                evt.StopPropagation();
                return;
            }

            if (!_isDragging || !target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            _isDragging = false;
            target.ReleasePointer(evt.pointerId);

            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
                OnDrop?.Invoke(evt.position);
                if (!_moved)
                {
                    OnClick?.Invoke();
                }
            }
            else
            {
                bool placed = OnDrop?.Invoke(evt.position) ?? false;
                if (!placed)
                {
                    SnapBack();
                    if (!_moved)
                    {
                        OnClick?.Invoke();
                    }
                }
            }

            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_clickTrackingOnly)
            {
                _clickTrackingOnly = false;
                return;
            }

            if (_isDragging)
            {
                _isDragging = false;
                if (_ghost != null)
                {
                    _ghost.RemoveFromHierarchy();
                    _ghost = null;
                }
                else
                {
                    SnapBack();
                }
            }
        }

        private void StartDrag()
        {
            _isDragging = true;

            if (CreateGhost != null)
            {
                _ghost = CreateGhost();
                _ghost.style.position = Position.Absolute;
                _ghost.style.left = _startElementPosition.x;
                _ghost.style.top = _startElementPosition.y;
                _dragLayer.Add(_ghost);
            }
            else
            {
                _originalParent = target.parent;
                _originalIndex = _originalParent.IndexOf(target);
                _originalPosition = target.style.position;
                _originalLeft = target.style.left;
                _originalTop = target.style.top;
                _originalBottom = target.style.bottom;
                _originalRotate = target.style.rotate;
                _originalScale = target.style.scale;
                _originalTransformOrigin = target.style.transformOrigin;

                float cardWidth = target.layout.width;
                float cardHeight = target.layout.height;

                _dragLayer.Add(target);
                target.style.position = Position.Absolute;
                target.style.bottom = StyleKeyword.Null;
                target.style.rotate = new Rotate(0);
                target.style.scale = new Scale(new Vector3(CardScaleConstants.FieldSlot, CardScaleConstants.FieldSlot, 1f));
                target.style.transformOrigin = new TransformOrigin(new Length(50f, LengthUnit.Percent), new Length(50f, LengthUnit.Percent));

                _startElementPosition = new Vector2(
                    _startPointerPosition.x - cardWidth / 2f,
                    _startPointerPosition.y - cardHeight / 2f
                );
                target.style.left = _startElementPosition.x;
                target.style.top = _startElementPosition.y;
            }
        }

        private void SnapBack()
        {
            _originalParent.Insert(_originalIndex, target);
            target.style.position = _originalPosition;
            target.style.left = _originalLeft;
            target.style.top = _originalTop;
            target.style.bottom = _originalBottom;
            target.style.rotate = _originalRotate;
            target.style.scale = _originalScale;
            target.style.transformOrigin = _originalTransformOrigin;
        }
    }
}

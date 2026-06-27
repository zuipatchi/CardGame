using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class AttackArrowManipulator : PointerManipulator
    {
        public Func<Vector2, bool> OnAttackTarget;
        public Func<bool> CanStart;
        // ドラッグ中の座標から、ドロップ先の状態（合法/非合法/中立）を返す。矢印の色分けに使う。
        public Func<Vector2, ArrowTargetState> EvaluateTarget;

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
            // すでにドラッグ中（別ポインタ＝マルチタッチや重複イベント）なら無視する。
            // ここでガードしないと _arrowView が上書きされ、前の矢印が _dragLayer に取り残されて消えなくなる。
            if (_isDragging)
            {
                evt.StopPropagation();
                return;
            }

            if (CanStart != null && !CanStart())
            {
                return;
            }

            // 念のため、何らかの理由で前の矢印が残っていれば確実に消してから作り直す。
            RemoveArrow();
            _isDragging = true;
            _arrowView = new ArrowView();
            _arrowView.StartPoint = target.worldBound.center;
            _arrowView.EndPoint = evt.position;
            _arrowView.TargetState = EvaluateTarget?.Invoke(evt.position) ?? ArrowTargetState.Neutral;
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
            _arrowView.TargetState = EvaluateTarget?.Invoke(evt.position) ?? ArrowTargetState.Neutral;
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

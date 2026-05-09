using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    public sealed class ArrowView : VisualElement
    {
        private const float LineWidth = 4f;
        private const float ArrowHeadSize = 20f;

        private Vector2 _startPoint;
        private Vector2 _endPoint;

        public Vector2 StartPoint
        {
            get => _startPoint;
            set
            {
                _startPoint = value;
                MarkDirtyRepaint();
            }
        }

        public Vector2 EndPoint
        {
            get => _endPoint;
            set
            {
                _endPoint = value;
                MarkDirtyRepaint();
            }
        }

        public ArrowView()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Vector2 dir = _endPoint - _startPoint;
            if (dir.sqrMagnitude < 1f)
            {
                return;
            }

            Vector2 origin = worldBound.position;
            Vector2 start = _startPoint - origin;
            Vector2 end = _endPoint - origin;

            // 進行方向の左直角方向にオフセット（常に一定の弧方向を保ちつつ真上でも破綻しない）
            Vector2 normalized = dir.normalized;
            Vector2 perpDir = new Vector2(-normalized.y, normalized.x);
            float arcOffset = dir.magnitude * 0.35f;
            Vector2 cp1 = Vector2.Lerp(start, end, 0.25f) + perpDir * arcOffset;
            Vector2 cp2 = Vector2.Lerp(start, end, 0.75f) + perpDir * arcOffset;

            // ベジェ曲線終端の接線方向を矢印の向きに使う
            Vector2 arrowDir = (end - cp2).normalized;
            Vector2 lineEnd = end - arrowDir * ArrowHeadSize;
            Vector2 arrowPerp = new Vector2(-arrowDir.y, arrowDir.x);
            Color arrowColor = new Color(1f, 0.6f, 0f, 0.9f);

            Painter2D painter = ctx.painter2D;

            painter.strokeColor = arrowColor;
            painter.lineWidth = LineWidth;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.BezierCurveTo(cp1, cp2, lineEnd);
            painter.Stroke();

            painter.fillColor = arrowColor;
            painter.BeginPath();
            painter.MoveTo(end);
            painter.LineTo(lineEnd + arrowPerp * (ArrowHeadSize * 0.5f));
            painter.LineTo(lineEnd - arrowPerp * (ArrowHeadSize * 0.5f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}

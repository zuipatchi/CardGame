using UnityEngine;
using UnityEngine.UIElements;

namespace Main.Card
{
    // 攻撃矢印のドロップ先の状態。色分けに使う。
    public enum ArrowTargetState
    {
        Neutral, // 対象の上にいない（中立色）
        Valid,   // 合法な攻撃対象の上（緑）
        Invalid, // 何か対象の上だが攻撃できない（赤）
    }

    public sealed class ArrowView : VisualElement
    {
        // 曲線をこの数だけ分割し、ダッシュ判定・色・太さをセグメント単位で扱う。
        // ダッシュをなめらかに見せるため、各セグメントがダッシュ長より十分短くなる数にする。
        private const int SegmentCount = 72;
        private const float ArrowHeadSize = 22f;

        // 線幅。根元は細く先端ほど太いテーパー。
        private const float WidthRoot = 3.5f;
        private const float WidthTip = 6.5f;

        // 流れるダッシュ：点線のパターン長と流れる速さ（px/秒）。正の速度で根元→先端へ流れる。
        private const float DashLength = 18f;
        private const float GapLength = 13f;
        private const float FlowSpeed = 70f;

        // 状態ごとのグラデーション色（根元 → 先端）。
        private static readonly Color NeutralRoot = new Color(1f, 0.85f, 0.3f);
        private static readonly Color NeutralTip = new Color(1f, 0.5f, 0f);
        private static readonly Color ValidRoot = new Color(0.6f, 1f, 0.55f);
        private static readonly Color ValidTip = new Color(0.1f, 0.85f, 0.3f);
        private static readonly Color InvalidRoot = new Color(1f, 0.5f, 0.45f);
        private static readonly Color InvalidTip = new Color(0.85f, 0.12f, 0.12f);

        private Vector2 _startPoint;
        private Vector2 _endPoint;
        private ArrowTargetState _targetState = ArrowTargetState.Neutral;

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

        // ドロップ先の状態。色分けに使う。変化したら再描画する。
        public ArrowTargetState TargetState
        {
            get => _targetState;
            set
            {
                if (_targetState != value)
                {
                    _targetState = value;
                    MarkDirtyRepaint();
                }
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

            // ダッシュを毎フレーム流すために定期的に再描画する。
            // パネルから外れるとスケジューラも止まるのでリークしない。
            schedule.Execute(MarkDirtyRepaint).Every(16);
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

            Painter2D painter = ctx.painter2D;
            painter.lineCap = LineCap.Round;

            // 3次ベジェ（P0=start, P1=cp1, P2=cp2, P3=lineEnd）を分割し、
            // 弧長ベースでダッシュの ON/OFF を決めながら短い線を描く。
            // flow を時間で進めることでダッシュが根元→先端へ流れる。
            float flow = Time.time * FlowSpeed;
            float cycle = DashLength + GapLength;
            Vector2 prev = start;
            float distAccum = 0f;
            for (int i = 1; i <= SegmentCount; i++)
            {
                float t = (float)i / SegmentCount;
                Vector2 point = Bezier(start, cp1, cp2, lineEnd, t);
                float segLen = Vector2.Distance(prev, point);
                float midDist = distAccum + segLen * 0.5f;

                // midDist - flow を周期で折り返し、ダッシュ区間（< DashLength）なら描く
                if (Mathf.Repeat(midDist - flow, cycle) < DashLength)
                {
                    float tMid = t - 0.5f / SegmentCount;
                    painter.strokeColor = GradientAt(tMid);
                    painter.lineWidth = Mathf.Lerp(WidthRoot, WidthTip, tMid);
                    painter.BeginPath();
                    painter.MoveTo(prev);
                    painter.LineTo(point);
                    painter.Stroke();
                }

                distAccum += segLen;
                prev = point;
            }

            // 矢じりは常に塗りで描く（先端色）。
            painter.fillColor = GradientAt(1f);
            painter.BeginPath();
            painter.MoveTo(end);
            painter.LineTo(lineEnd + arrowPerp * (ArrowHeadSize * 0.5f));
            painter.LineTo(lineEnd - arrowPerp * (ArrowHeadSize * 0.5f));
            painter.ClosePath();
            painter.Fill();
        }

        private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * p0
                + 3f * uu * t * p1
                + 3f * u * tt * p2
                + tt * t * p3;
        }

        // 0=根元 → 1=先端 を、現在の対象状態に応じた2色で補間する。
        private Color GradientAt(float t)
        {
            switch (_targetState)
            {
                case ArrowTargetState.Valid:
                    return Color.Lerp(ValidRoot, ValidTip, t);
                case ArrowTargetState.Invalid:
                    return Color.Lerp(InvalidRoot, InvalidTip, t);
                default:
                    return Color.Lerp(NeutralRoot, NeutralTip, t);
            }
        }
    }
}

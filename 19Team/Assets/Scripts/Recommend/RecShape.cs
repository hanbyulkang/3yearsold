using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Recommend
{
    // 시안에 나오는 박스는 전부 "둥근 사각형 + (세로 그라데이션) 채움 + (실선/점선) 테두리" 하나로 표현된다.
    // 카드·AI 점선박스·pill·칩·금색 버튼·상단바·사진 자리 모두 이 컴포넌트다.
    //
    // 스프라이트를 쓰지 않고 메시로 직접 그리는 이유
    //  - 9-slice 로는 점선 테두리를 늘릴 때 대시 간격이 같이 늘어나 시안과 달라진다.
    //  - PNG 를 임포트하면 .meta·9-slice 설정이 생겨 이 신 밖의 파일을 건드리게 된다.
    //  - 크기가 콘텐츠에 따라 변해도(레이아웃 그룹) 항상 정확한 모서리·대시가 나온다.
    // RequireComponent 는 추상 기반 클래스(Graphic)에서 상속돼 오지 않는다 —
    // 여기서 다시 선언하지 않으면 CanvasRenderer 없이 붙어 아무것도 그려지지 않는다.
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class RecShape : MaskableGraphic
    {
        [SerializeField] float _radius = 22f;
        [SerializeField] bool _filled = true;
        [SerializeField] Color _fillTop = Color.white;
        [SerializeField] Color _fillBottom = Color.white;
        [SerializeField] float _borderWidth;
        [SerializeField] Color _borderColor = Color.clear;
        [SerializeField] float _dashLength;   // 0 이면 실선
        [SerializeField] float _dashGap;
        [SerializeField] int _arcSegments = 10;

        public float Radius       { get => _radius;       set { _radius = value; SetVerticesDirty(); } }
        public bool Filled        { get => _filled;       set { _filled = value; SetVerticesDirty(); } }
        public Color FillTop      { get => _fillTop;      set { _fillTop = value; SetVerticesDirty(); } }
        public Color FillBottom   { get => _fillBottom;   set { _fillBottom = value; SetVerticesDirty(); } }
        public float BorderWidth  { get => _borderWidth;  set { _borderWidth = value; SetVerticesDirty(); } }
        public Color BorderColor  { get => _borderColor;  set { _borderColor = value; SetVerticesDirty(); } }
        public float DashLength   { get => _dashLength;   set { _dashLength = value; SetVerticesDirty(); } }
        public float DashGap      { get => _dashGap;      set { _dashGap = value; SetVerticesDirty(); } }

        /// <summary>단색 채움.</summary>
        public void SetFill(Color c) { _filled = true; _fillTop = _fillBottom = c; SetVerticesDirty(); }

        /// <summary>세로 그라데이션 채움 (시안의 linear-gradient(180deg, top, bottom)).</summary>
        public void SetGradient(Color top, Color bottom) { _filled = true; _fillTop = top; _fillBottom = bottom; SetVerticesDirty(); }

        /// <summary>실선 테두리.</summary>
        public void SetBorder(float width, Color c) { _borderWidth = width; _borderColor = c; _dashLength = 0f; SetVerticesDirty(); }

        /// <summary>점선 테두리. dash/gap 은 시안의 stroke-dasharray 값.</summary>
        public void SetDashedBorder(float width, Color c, float dash, float gap)
        { _borderWidth = width; _borderColor = c; _dashLength = dash; _dashGap = gap; SetVerticesDirty(); }

        // 메시 색으로만 그리므로 텍스처가 필요 없다 — 흰 1x1 을 쓴다.
        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0f || h <= 0f) return;

            float rad = Mathf.Clamp(_radius, 0f, Mathf.Min(w, h) * 0.5f);

            if (_filled) AddFill(vh, r, rad);

            if (_borderWidth > 0f && _borderColor.a > 0f)
            {
                float bw = Mathf.Min(_borderWidth, Mathf.Min(w, h) * 0.5f);
                // CSS 처럼 테두리는 박스 안쪽에 그린다 — 스트로크 중심선을 bw/2 만큼 안으로 들인다.
                var inner = Rect.MinMaxRect(r.xMin + bw * 0.5f, r.yMin + bw * 0.5f,
                                            r.xMax - bw * 0.5f, r.yMax - bw * 0.5f);
                float innerRad = Mathf.Max(0f, rad - bw * 0.5f);
                var path = BuildPath(inner, innerRad);
                if (_dashLength > 0f) AddDashedStroke(vh, path, bw);
                else AddStroke(vh, path, bw);
            }
        }

        // ---- 채움: 가로 띠(band)로 쌓는다 ----
        // 삼각형 팬으로 만들면 세로 그라데이션이 중심점을 거치며 뭉개진다.
        // y 를 따라 좌우 폭을 정확히 구해 띠로 쌓으면 모서리 실루엣과 그라데이션이 둘 다 정확하다.
        void AddFill(VertexHelper vh, Rect r, float rad)
        {
            var ys = SampleYs(r, rad);
            int startIndex = vh.currentVertCount;

            for (int i = 0; i < ys.Count; i++)
            {
                float y = ys[i];
                float inset = InsetAt(r, rad, y);
                var c = ColorAtY(r, y);
                vh.AddVert(new Vector3(r.xMin + inset, y), c, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax - inset, y), c, Vector2.zero);
            }

            for (int i = 0; i < ys.Count - 1; i++)
            {
                int a = startIndex + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        // 모서리 호 구간은 촘촘히, 직선 구간은 양 끝만 — 필요한 y 좌표만 뽑는다.
        List<float> SampleYs(Rect r, float rad)
        {
            var ys = new List<float>(_arcSegments * 2 + 4);
            int seg = Mathf.Max(2, _arcSegments);

            // 아래 모서리 (yMin → yMin+rad)
            for (int i = 0; i <= seg; i++) ys.Add(r.yMin + rad * i / seg);
            // 직선 구간
            if (r.yMax - rad > r.yMin + rad) ys.Add(r.yMax - rad);
            // 위 모서리 (yMax-rad → yMax)
            for (int i = 1; i <= seg; i++) ys.Add(r.yMax - rad + rad * i / seg);

            return ys;
        }

        // 해당 y 에서 좌우로 얼마나 들어가야 하는지 (모서리 곡선).
        static float InsetAt(Rect r, float rad, float y)
        {
            if (rad <= 0f) return 0f;
            float dy = 0f;
            if (y < r.yMin + rad) dy = (r.yMin + rad) - y;
            else if (y > r.yMax - rad) dy = y - (r.yMax - rad);
            else return 0f;
            dy = Mathf.Min(dy, rad);
            return rad - Mathf.Sqrt(Mathf.Max(0f, rad * rad - dy * dy));
        }

        Color ColorAtY(Rect r, float y)
        {
            // 시안의 180deg 그라데이션은 위 → 아래. Unity 는 y 가 위로 증가한다.
            float t = r.height > 0f ? Mathf.InverseLerp(r.yMax, r.yMin, y) : 0f;
            return Color.Lerp(_fillTop, _fillBottom, t) * color;
        }

        // ---- 테두리: 경로를 따라 두께 bw 의 쿼드 스트립 ----

        List<Vector2> BuildPath(Rect r, float rad)
        {
            var pts = new List<Vector2>((_arcSegments + 1) * 4);
            int seg = Mathf.Max(1, _arcSegments);
            rad = Mathf.Clamp(rad, 0f, Mathf.Min(r.width, r.height) * 0.5f);

            // 좌상 → 우상 → 우하 → 좌하 (시계방향)
            AddArc(pts, new Vector2(r.xMin + rad, r.yMax - rad), rad, 180f, 90f, seg);
            AddArc(pts, new Vector2(r.xMax - rad, r.yMax - rad), rad, 90f, 0f, seg);
            AddArc(pts, new Vector2(r.xMax - rad, r.yMin + rad), rad, 0f, -90f, seg);
            AddArc(pts, new Vector2(r.xMin + rad, r.yMin + rad), rad, -90f, -180f, seg);
            return pts;
        }

        static void AddArc(List<Vector2> pts, Vector2 c, float rad, float fromDeg, float toDeg, int seg)
        {
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)i / seg);
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                // 호가 이어지는 지점은 중복되므로 건너뛴다
                if (pts.Count > 0 && (pts[pts.Count - 1] - p).sqrMagnitude < 0.0001f) continue;
                pts.Add(p);
            }
        }

        void AddStroke(VertexHelper vh, List<Vector2> path, float bw)
        {
            for (int i = 0; i < path.Count; i++)
                AddSegment(vh, path[i], path[(i + 1) % path.Count], bw);
        }

        // stroke-dasharray 재현.
        //
        // 경로를 조금씩 "걸어가며" while 로 대시를 찍는 방식은 쓰지 않는다 —
        // 남은 거리가 0 으로 수렴하면 부동소수 오차로 진행이 멈춰 무한 루프가 된다.
        // 대신 대시 개수를 먼저 확정하고, k번째 대시가 차지하는 길이 구간 [s0,s1] 을
        // 경로에서 잘라내는 방식으로 바꾼다. 반복 횟수가 처음부터 정해져 있어 멈추지 않는다.
        void AddDashedStroke(VertexHelper vh, List<Vector2> path, float bw)
        {
            // 각 꼭짓점까지의 누적 길이
            int n = path.Count;
            if (n < 2) return;
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++)
                cum[i + 1] = cum[i] + Vector2.Distance(path[i], path[(i + 1) % n]);
            float total = cum[n];
            if (total <= 0.01f) return;

            float dash = Mathf.Max(0.5f, _dashLength);
            float gap = Mathf.Max(0.5f, _dashGap);

            // 둘레를 주기의 정수배로 맞춰 시작점과 끝점에서 대시가 어긋나지 않게 한다.
            int periods = Mathf.Clamp(Mathf.RoundToInt(total / (dash + gap)), 1, 512);
            float period = total / periods;
            float ratio = dash / (dash + gap);
            dash = period * ratio;

            // 대시가 선폭보다도 잘게 쪼개질 상황이면 점선이 의미가 없다 — 실선으로 그린다.
            if (period < bw || dash < 0.5f) { AddStroke(vh, path, bw); return; }

            for (int k = 0; k < periods; k++)
                AddSubPath(vh, path, cum, k * period, k * period + dash, bw);
        }

        // 경로에서 누적길이 [s0, s1] 구간만 잘라 그린다.
        void AddSubPath(VertexHelper vh, List<Vector2> path, float[] cum, float s0, float s1, float bw)
        {
            int n = path.Count;
            for (int i = 0; i < n; i++)
            {
                float segStart = cum[i], segEnd = cum[i + 1];
                if (segEnd <= s0 || segStart >= s1) continue;   // 구간 밖
                float segLen = segEnd - segStart;
                if (segLen <= 0.0001f) continue;

                float a = Mathf.Max(s0, segStart), b = Mathf.Min(s1, segEnd);
                Vector2 p0 = path[i], p1 = path[(i + 1) % n];
                AddSegment(vh,
                    Vector2.Lerp(p0, p1, (a - segStart) / segLen),
                    Vector2.Lerp(p0, p1, (b - segStart) / segLen), bw);
            }
        }

        void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float bw)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) return;
            // 이음매가 벌어지지 않도록 양 끝을 두께의 절반만큼 늘려 겹친다 (라운드 조인 근사).
            dir.Normalize();
            a -= dir * (bw * 0.5f);
            b += dir * (bw * 0.5f);
            Vector2 n = new Vector2(-dir.y, dir.x) * (bw * 0.5f);

            var c = _borderColor * color;
            int i0 = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x + n.x, a.y + n.y), c, Vector2.zero);
            vh.AddVert(new Vector3(b.x + n.x, b.y + n.y), c, Vector2.zero);
            vh.AddVert(new Vector3(b.x - n.x, b.y - n.y), c, Vector2.zero);
            vh.AddVert(new Vector3(a.x - n.x, a.y - n.y), c, Vector2.zero);
            vh.AddTriangle(i0, i0 + 1, i0 + 2);
            vh.AddTriangle(i0, i0 + 2, i0 + 3);
        }
    }
}

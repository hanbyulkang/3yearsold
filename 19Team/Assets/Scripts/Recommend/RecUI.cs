using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Recommend
{
    // 위에서 아래로 요소를 쌓는 커서.
    //
    // LayoutGroup / ContentSizeFitter 를 쓰지 않는다.
    // 부모 폭이 자식 높이에 의존하고 자식 높이가 부모 폭에 의존하는 배치가 여러 곳에 나오는데,
    // 그 조합은 CanvasUpdateRegistry 가 리빌드를 반복하다 멈추지 않는다
    // (ScrollRect.EnsureLayoutHasRebuilt 가 매 프레임 강제로 돌려서 그대로 락업된다).
    // MG1 도 같은 이유로 좌표를 직접 계산한다 — 여기서도 그 방식을 따른다.
    public class RecCol
    {
        public readonly RectTransform Parent;
        public readonly float Width;   // 이 열에서 쓸 수 있는 가로 폭
        public readonly float Gap;
        public float Y;                // 다음 요소가 놓일 위치 (위에서부터)

        public RecCol(RectTransform parent, float width, float gap, float top = 0f)
        {
            Parent = parent; Width = width; Gap = gap; Y = top;
        }

        /// <summary>요소 하나를 놓고 커서를 내린다.</summary>
        public void Advance(float height) { Y += height + Gap; }

        /// <summary>마지막 간격을 뺀 실제 높이.</summary>
        public float Height => Mathf.Max(0f, Y - Gap);
    }

    // 시안(Desktop/recomend.html)의 반복 요소를 만드는 빌더.
    // 캔버스 referenceResolution 이 686×1220 이라 아래 수치는 시안의 px 과 1:1이다.
    public static class RecUI
    {
        public static TMP_FontAsset Font;

        // 이 프로젝트의 한글 폰트는 NotoSansKR-Black(굵기 900) 하나뿐이다.
        // 본문까지 900 으로 찍으면 획이 뭉쳐 읽기 힘들다. SDF 의 FaceDilate 를 음수로 줘서
        // 글자 획을 깎아 본문용 굵기를 만든다 — 머티리얼 하나를 공유하므로 배칭도 유지된다.
        // 라이트 웨이트 한글 폰트(Regular/Medium)가 들어오면 이 우회는 지우고 폰트를 나누면 된다.
        const float BodyDilate = -0.16f;
        static Material _bodyMaterial;

        /// <summary>
        /// true 면 제목까지 전부 얇은 획으로 그린다.
        /// 폰트가 Black 하나뿐이라 제목만 원본 굵기로 두면 본문과 대비가 너무 세다.
        /// 가독성 비교용 스위치 — 라이트 웨이트 폰트가 들어오면 이 우회는 통째로 지운다.
        /// </summary>
        public static bool ThinAllText = true;

        static Material BodyMaterial
        {
            get
            {
                if (_bodyMaterial == null && Font != null && Font.material != null)
                {
                    _bodyMaterial = new Material(Font.material) { name = "RecBodyThin" };
                    _bodyMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, BodyDilate);
                }
                return _bodyMaterial;
            }
        }

        // ---- 기본 ----

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            // 좌상단 기준 좌표계 — CSS 와 같은 방향이라 시안을 그대로 옮기기 쉽다.
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        /// <summary>부모 좌상단 기준으로 배치. y 는 아래로 증가한다.</summary>
        public static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // Graphic 계열은 CanvasRenderer 가 없으면 아무것도 그려지지 않는다.
        // 런타임 AddComponent 에서는 기반 클래스의 RequireComponent 가 보장되지 않으므로 직접 붙인다.
        public static RecShape AddShape(GameObject go)
        {
            if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
            return go.AddComponent<RecShape>();
        }

        public static RecShape Shape(string name, Transform parent)
        {
            var s = AddShape(Node(name, parent).gameObject);
            s.raycastTarget = false;
            return s;
        }

        // ---- 글자 ----

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            bool bold = false,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
            float lineHeight = RecTheme.LineTight)
        {
            var rt = Node(name, parent);
            if (rt.gameObject.GetComponent<CanvasRenderer>() == null) rt.gameObject.AddComponent<CanvasRenderer>();
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            // 폰트 자체가 이미 Black 이라 Bold 를 얹으면 TMP 가 한 번 더 부풀려 뭉갠다.
            // 강조는 굵기 대신 색으로 준다. 본문은 획을 깎은 머티리얼을 쓴다.
            t.fontStyle = FontStyles.Normal;
            if (!bold || ThinAllText)
            {
                var m = BodyMaterial;
                if (m != null) t.fontSharedMaterial = m;
            }
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            // 시안의 line-height 는 1.6~1.75. TMP 의 lineSpacing 은 "추가 간격"을 % 로 받는다.
            t.lineSpacing = (lineHeight - 1f) * 100f;
            // TextWrappingModes.NoWrap 이 0 이라, 코드로 AddComponent 한 TMP 는 줄바꿈이 꺼진 채 생성된다
            // (에디터에서 만들면 TMP_Settings 기본값을 받아오지만 이 경로는 거치지 않는다).
            // 명시하지 않으면 문단이 한 줄로 뻗어나가 상자 밖으로 흘러넘친다.
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>주어진 폭으로 줄바꿈했을 때의 높이. 배치 전에 미리 잰다.</summary>
        /// <remarks>
        /// 반드시 활성(active) 상태에서 불러야 한다. 비활성 GameObject 아래의 TMP 는
        /// Awake 가 돌지 않아 폰트가 로드되지 않았고, 그때 재면 여백값(≈3)만 돌아온다.
        /// </remarks>
        public static float MeasureH(TMP_Text t, float width)
        {
            return Mathf.Ceil(t.GetPreferredValues(width, 0f).y);
        }

        public static float MeasureW(TMP_Text t)
        {
            return Mathf.Ceil(t.GetPreferredValues().x);
        }

        /// <summary>문단 하나를 열에 쌓는다. 폭에 맞춰 높이를 재서 배치한다.</summary>
        public static TextMeshProUGUI Para(RecCol col, string name, string text, float size, Color color,
            bool bold = false, float lineHeight = RecTheme.LineNormal,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Text(name, col.Parent, text, size, color, bold, align, lineHeight);
            float h = MeasureH(t, col.Width);
            SetRect(t.rectTransform, 0f, col.Y, col.Width, h);
            col.Advance(h);
            return t;
        }

        // ---- 상자 ----
        // 상자 계열은 "안쪽 열을 채우는 콜백"을 받아 내용 높이를 먼저 구한 뒤 자기 크기를 정한다.

        /// <summary>흰 카드: #FFF, 2px #EDE4D2 테두리, radius 22, 아래로 깔리는 그림자.</summary>
        public static RectTransform Card(RecCol col, string name, Action<RecCol> fill,
            float innerGap = 16f, float padH = 24f, float padV = 22f)
        {
            var root = Node(name, col.Parent);

            // 그림자 → 카드면 → 콘텐츠 순서로 그려야 한다.
            // 카드면을 root 에 직접 붙이면 안 된다: uGUI 는 부모 Graphic 을 자식보다 먼저 그리므로
            // 그림자를 자식으로 두면 그림자가 카드 위를 덮는다.
            var shadow = Shape("Shadow", root);
            shadow.Radius = RecTheme.Radius + 2f;
            shadow.SetFill(RecTheme.CardShadow);
            Stretch(shadow.rectTransform, -3f, -3f, 2f, -6f);

            var face = Shape("Face", root);
            face.Radius = RecTheme.Radius;
            face.SetFill(RecTheme.White);
            face.SetBorder(2f, RecTheme.CardBorder);
            Stretch(face.rectTransform);

            var inner = new RecCol(root, col.Width - padH * 2f, innerGap, padV);
            fill(inner);
            float h = inner.Height + padV;

            SetRect(root, 0f, col.Y, col.Width, h);
            col.Advance(h);
            // 안쪽 요소들은 padH 만큼 오른쪽으로 밀어준다
            ShiftChildrenX(root, padH, 2);
            return root;
        }

        /// <summary>점선 테두리 상자 (AI 영역 · 안내 · 빈 상태 · 사진 자리).</summary>
        public static RectTransform DashedBox(RecCol col, string name, Action<RecCol> fill,
            float radius, Color fillColor, Color stroke, float strokeW, float dash, float gap,
            float innerGap = 12f, float padH = 24f, float padV = 22f)
        {
            var root = Node(name, col.Parent);
            var s = AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = radius;
            s.SetFill(fillColor);
            s.SetDashedBorder(strokeW, stroke, dash, gap);

            var inner = new RecCol(root, col.Width - padH * 2f, innerGap, padV);
            fill(inner);
            float h = inner.Height + padV;

            SetRect(root, 0f, col.Y, col.Width, h);
            col.Advance(h);
            ShiftChildrenX(root, padH, 0);
            return root;
        }

        /// <summary>실선 테두리 상자 (D-04 '지금 추천' 강조 카드).</summary>
        public static RectTransform SolidBox(RecCol col, string name, Action<RecCol> fill,
            float radius, Color fillColor, Color stroke, float strokeW,
            float innerGap = 12f, float padH = 24f, float padV = 22f)
        {
            var root = Node(name, col.Parent);
            var s = AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = radius;
            s.SetFill(fillColor);
            s.SetBorder(strokeW, stroke);

            var inner = new RecCol(root, col.Width - padH * 2f, innerGap, padV);
            fill(inner);
            float h = inner.Height + padV;

            SetRect(root, 0f, col.Y, col.Width, h);
            col.Advance(h);
            ShiftChildrenX(root, padH, 0);
            return root;
        }

        // 상자의 콘텐츠 자식들을 좌우 패딩만큼 민다.
        // (배경·그림자처럼 stretch 로 붙은 자식은 건드리면 안 되므로 skipFirst 로 건너뛴다)
        static void ShiftChildrenX(RectTransform root, float dx, int skipFirst)
        {
            for (int i = skipFirst; i < root.childCount; i++)
            {
                var c = (RectTransform)root.GetChild(i);
                c.anchoredPosition += new Vector2(dx, 0f);
            }
        }

        // ---- 버튼 ----

        /// <summary>금색 주요 버튼. 시안의 0 5px 0 #7d4f0a 그림자 + 누르면 내려앉는 동작.</summary>
        public static RectTransform GoldButton(Transform parent, string name, string label, Action onClick,
            float x, float y, float w, float height = 54f,
            float fontSize = RecTheme.FsBtnGold, float radius = 16f, float borderW = 2.5f, float dropY = 5f)
        {
            var root = Node(name, parent);
            SetRect(root, x, y, w, height + dropY);

            var deep = Shape("Deep", root);
            deep.Radius = radius;
            deep.SetFill(RecTheme.GoldDeep);
            Stretch(deep.rectTransform, 0, 0, dropY, 0);

            var face = Shape("Face", root);
            face.Radius = radius;
            face.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
            face.SetBorder(borderW, RecTheme.GoldBorder);
            face.raycastTarget = true;
            Stretch(face.rectTransform, 0, 0, 0, dropY);

            var t = Text("Label", face.transform, label, fontSize, RecTheme.GoldText, true, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);

            Wire(face, dropY, onClick);
            return root;
        }

        /// <summary>열에 꽉 차는 금색 버튼.</summary>
        public static RectTransform GoldButton(RecCol col, string name, string label, Action onClick,
            float height = 54f, float fontSize = RecTheme.FsBtnGold, float radius = 16f,
            float borderW = 2.5f, float dropY = 5f)
        {
            var b = GoldButton(col.Parent, name, label, onClick, 0f, col.Y, col.Width, height, fontSize, radius, borderW, dropY);
            col.Advance(height + dropY);
            return b;
        }

        /// <summary>갈색 보조 버튼. width 를 0 이하로 주면 글자 길이에 맞춘다.</summary>
        public static RectTransform BrownButton(Transform parent, string name, string label, Action onClick,
            float x, float y, float w = 0f, float height = 40f,
            float fontSize = RecTheme.FsBtnBrown, float padH = 18f, float radius = 12f, float dropY = 3f)
        {
            var root = Node(name, parent);

            var deep = Shape("Deep", root);
            deep.Radius = radius;
            deep.SetFill(RecTheme.BrownDeep);
            Stretch(deep.rectTransform, 0, 0, dropY, 0);

            var face = Shape("Face", root);
            face.Radius = radius;
            face.SetGradient(RecTheme.BrownTop, RecTheme.BrownBottom);
            face.SetBorder(2f, RecTheme.BrownBorder);
            face.raycastTarget = true;
            Stretch(face.rectTransform, 0, 0, 0, dropY);

            var t = Text("Label", face.transform, label, fontSize, RecTheme.OnDark, true, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);

            if (w <= 0f) w = MeasureW(t) + padH * 2f;
            SetRect(root, x, y, w, height + dropY);

            Wire(face, dropY, onClick);
            return root;
        }

        static void Wire(RecShape face, float dropY, Action onClick)
        {
            var btn = face.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            face.gameObject.AddComponent<RecPressable>().Init(face.rectTransform, dropY);
        }

        // ---- pill · 칩 · 태그 ----

        /// <summary>정보 pill (지역·상태). 클릭 불가. 폭은 글자에 맞춘다.</summary>
        public static RectTransform Pill(Transform parent, string name, string label,
            Color fill, Color stroke, Color textColor, float x, float y,
            float fontSize = RecTheme.FsTiny, float padH = 14f, float height = 30f, bool gradient = false)
        {
            var root = Node(name, parent);
            var s = AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = height * 0.5f;
            if (gradient) s.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
            else s.SetFill(fill);
            s.SetBorder(1.5f, stroke);

            var t = Text("Label", root, label, fontSize, textColor, true, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);

            SetRect(root, x, y, MeasureW(t) + padH * 2f, height);
            return root;
        }

        /// <summary>필터 칩 (D-02). 선택 시 금색.</summary>
        public static RecChip Chip(Transform parent, string name, string label, bool on,
            float x, float y, Action<RecChip> onClick, float height = 42f)
        {
            var root = Node(name, parent);
            var s = AddShape(root.gameObject);
            s.raycastTarget = true;
            s.Radius = height * 0.5f;

            var t = Text("Label", root, label, RecTheme.Fs(17f), RecTheme.Sub, true, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);

            SetRect(root, x, y, MeasureW(t) + 40f, height);

            var chip = root.gameObject.AddComponent<RecChip>();
            chip.Init(s, t, on, onClick);
            return chip;
        }

        /// <summary>사진 자리 — 점선 사각형 + 가운데 표시.</summary>
        public static RectTransform Slot(Transform parent, string name, float x, float y, float w, float h,
            float radius, string caption, float captionSize)
        {
            var root = Node(name, parent);
            var s = AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = radius;
            s.SetFill(RecTheme.SlotFill);
            s.SetDashedBorder(2f, RecTheme.SlotStroke, 8f, 6f);
            SetRect(root, x, y, w, h);

            if (!string.IsNullOrEmpty(caption))
            {
                var t = Text("Caption", root, caption, captionSize, RecTheme.GoldInk, false, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
            }
            return root;
        }

        // ---- 목록 행 ----

        /// <summary>키-값 한 줄 + 아래 구분선 (D-03 보호소 정보 / D-05 신청 요약).</summary>
        public static void KvRow(RecCol col, string k, string v, bool divider, float minRowH = 56f)
        {
            var row = Node("Row_" + k, col.Parent);

            // 값이 길면 두 줄이 된다. 행 높이를 고정하면 넘친 줄이 아래 행을 침범하므로
            // 양쪽을 재서 큰 쪽에 맞춘다.
            const float gap = 16f;
            float kW = col.Width * 0.34f;
            float vW = col.Width - kW - gap;

            var kt = Text("K", row, k, RecTheme.FsBody, RecTheme.Sub, false, TextAlignmentOptions.MidlineLeft);
            var vt = Text("V", row, v, RecTheme.Fs(17f), RecTheme.Ink, true, TextAlignmentOptions.MidlineRight);
            float h = Mathf.Max(minRowH, Mathf.Max(MeasureH(kt, kW), MeasureH(vt, vW)) + 26f);

            SetRect(row, 0f, col.Y, col.Width, h);
            SetRect(kt.rectTransform, 0f, 0f, kW, h);
            SetRect(vt.rectTransform, kW + gap, 0f, vW, h);

            if (divider) Divider(row, col.Width, h);
            col.Advance(h);
        }

        public static void Divider(RectTransform parent, float width, float y)
        {
            var line = Shape("Divider", parent);
            line.Radius = 0f;
            line.SetFill(RecTheme.Divider);
            SetRect(line.rectTransform, 0f, y - 1.5f, width, 1.5f);
        }
    }

    // 시안의 :active { transform: translateY(Npx) } — 누르면 면이 그림자 위로 내려앉는다.
    public class RecPressable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        RectTransform _face;
        float _drop;
        Vector2 _restMin, _restMax;

        public void Init(RectTransform face, float drop)
        {
            _face = face; _drop = drop;
            _restMin = face.offsetMin; _restMax = face.offsetMax;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_face == null) return;
            _face.offsetMin = _restMin - new Vector2(0f, _drop);
            _face.offsetMax = _restMax - new Vector2(0f, _drop);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_face == null) return;
            _face.offsetMin = _restMin;
            _face.offsetMax = _restMax;
        }
    }

    // 필터 칩 — 같은 그룹 안에서 하나만 켜진다.
    public class RecChip : MonoBehaviour, IPointerClickHandler
    {
        RecShape _shape;
        TextMeshProUGUI _label;
        Action<RecChip> _onClick;

        public void Init(RecShape shape, TextMeshProUGUI label, bool on, Action<RecChip> onClick)
        {
            _shape = shape; _label = label; _onClick = onClick;
            SetOn(on);
        }

        public void SetOn(bool on)
        {
            if (on)
            {
                _shape.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
                _shape.SetBorder(2f, RecTheme.GoldBorder);
                _label.color = RecTheme.GoldText;
            }
            else
            {
                _shape.SetFill(RecTheme.White);
                _shape.SetBorder(2f, RecTheme.CardBorder);
                _label.color = RecTheme.Sub;
            }
        }

        public void OnPointerClick(PointerEventData e) => _onClick?.Invoke(this);
    }
}

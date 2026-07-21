using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Recommend
{
    // 화면 뼈대 하나. 시안의 프레임 구조 그대로:
    //   상단바(고정) + 본문(스크롤) + 하단 CTA(선택)
    public class RecFrame
    {
        public string Id;
        public RectTransform Root;      // 화면 전체 (stretch)
        public RectTransform AppBar;
        public RectTransform Content;   // 스크롤되는 본문 — 여기에 카드를 쌓는다
        public RectTransform Footer;    // 하단 고정 CTA 자리 (없으면 null)
        public ScrollRect Scroll;
        public RecCol Col;              // 본문에 쌓을 때 쓰는 커서
        public float ViewportH;         // 본문에 실제로 보이는 세로 길이 (기기 기준)
    }

    // 화면 스택 네비게이션. 백엔드 없음 — 보여주고 되돌아가는 것만 한다.
    public class RecNav : MonoBehaviour
    {
        readonly Dictionary<string, RecFrame> _frames = new Dictionary<string, RecFrame>();
        readonly Stack<string> _history = new Stack<string>();
        string _current;

        public const float BarH = 74f;

        public string Current => _current;

        // 여기서 화면을 끄면 안 된다.
        // 화면 내용은 CreateFrame 이 끝난 뒤에 채워지는데, 비활성 GameObject 아래에서는
        // TMP 의 Awake 가 돌지 않아 폰트·textInfo 가 초기화되지 않는다. 그 상태로 글자 높이를 재면
        // 실제 값 대신 여백값(≈3)이 나와 문단이 한 줄 높이로 찌그러지고 제목과 겹친다.
        // 전부 만든 뒤 Show() 가 나머지를 끈다.
        public void Register(RecFrame f)
        {
            _frames[f.Id] = f;
        }

        public void Show(string id, bool push = true)
        {
            if (!_frames.ContainsKey(id) || id == _current) return;
            if (push && _current != null) _history.Push(_current);

            foreach (var kv in _frames) kv.Value.Root.gameObject.SetActive(kv.Key == id);
            _current = id;

            // 화면을 열 때는 항상 맨 위부터
            var f = _frames[id];
            if (f.Scroll != null) f.Scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>모든 화면을 감춘다 (로딩 오버레이만 보이게 할 때).</summary>
        public void HideAll()
        {
            foreach (var kv in _frames) kv.Value.Root.gameObject.SetActive(false);
            _current = null;
        }

        public void Back()
        {
            if (_history.Count == 0) return;
            Show(_history.Pop(), false);
        }

        void Update()
        {
            // 안드로이드 뒤로가기 / 에디터 ESC.
            // 이 프로젝트는 Input System 패키지를 쓰므로 레거시 Input 을 부르면 예외가 난다.
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
#endif
        }

        // ---- 화면 뼈대 ----

        public RecFrame CreateFrame(Transform canvas, string id, string title,
            bool showBack, string leadIcon = null, float footerHeight = 0f, float bodyGap = RecTheme.Gap)
        {
            var f = new RecFrame { Id = id };
            const float pad = RecTheme.Pad;
            float contentW = RecTheme.FrameW - pad * 2f;

            f.Root = RecUI.Node(id, canvas);
            RecUI.Stretch(f.Root);

            // ---- 상단바 ----
            var appbar = RecUI.Node("AppBar", f.Root);
            var barShape = RecUI.AddShape(appbar.gameObject);
            barShape.raycastTarget = false;
            barShape.Radius = RecTheme.Radius;
            barShape.SetGradient(RecTheme.BarTop, RecTheme.BarBottom);
            barShape.SetBorder(3f, RecTheme.BrownDeep);
            appbar.anchorMin = appbar.anchorMax = appbar.pivot = new Vector2(0f, 1f);
            appbar.anchoredPosition = new Vector2(pad, -pad);
            appbar.sizeDelta = new Vector2(contentW, BarH);

            float titleX = 20f;
            if (showBack)
            {
                RecUI.BrownButton(appbar, "Back", "←", Back, 20f, (BarH - 45f) * 0.5f, 42f, 42f, RecTheme.Fs(20f), 0f, 12f, 3f);
                titleX = 20f + 42f + 14f;
            }
            else if (!string.IsNullOrEmpty(leadIcon))
            {
                var tile = RecUI.Node("Icon", appbar);
                var ts = RecUI.AddShape(tile.gameObject);
                ts.raycastTarget = false;
                ts.Radius = 12f;
                ts.SetGradient(RecTheme.TileTop, RecTheme.TileBottom);
                ts.SetBorder(2f, RecTheme.BrownBorder);
                RecUI.SetRect(tile, 20f, (BarH - 44f) * 0.5f, 44f, 44f);
                var e = RecUI.Text("Icon", tile, leadIcon, RecTheme.Fs(22f), RecTheme.OnDark, false, TextAlignmentOptions.Center);
                RecUI.Stretch(e.rectTransform);
                titleX = 20f + 44f + 14f;
            }

            var t = RecUI.Text("Title", appbar, title,
                showBack ? RecTheme.FsAppBar : RecTheme.FsAppBarLg, RecTheme.OnDark,
                true, TextAlignmentOptions.MidlineLeft);
            t.characterSpacing = 4f; // 시안 letter-spacing:1px
            RecUI.SetRect(t.rectTransform, titleX, 0f, contentW - titleX - 20f, BarH);
            f.AppBar = appbar;

            // ---- 하단 고정 CTA ----
            float bottomInset = pad;
            if (footerHeight > 0f)
            {
                f.Footer = RecUI.Node("Footer", f.Root);
                f.Footer.anchorMin = new Vector2(0f, 0f);
                f.Footer.anchorMax = new Vector2(1f, 0f);
                f.Footer.pivot = new Vector2(0.5f, 0f);
                f.Footer.offsetMin = new Vector2(pad, 0f);
                f.Footer.offsetMax = new Vector2(-pad, 0f);
                f.Footer.sizeDelta = new Vector2(f.Footer.sizeDelta.x, footerHeight);
                f.Footer.anchoredPosition = new Vector2(0f, pad);
                bottomInset = pad + footerHeight + RecTheme.Gap;
            }

            // ---- 본문 (스크롤) ----
            var scroll = RecUI.Node("Body", f.Root);
            RecUI.Stretch(scroll, pad, pad, pad + BarH + RecTheme.Gap, bottomInset);
            f.Scroll = scroll.gameObject.AddComponent<ScrollRect>();
            f.Scroll.horizontal = false;
            f.Scroll.movementType = ScrollRect.MovementType.Elastic;
            f.Scroll.scrollSensitivity = 40f;

            var viewport = RecUI.Node("Viewport", scroll);
            RecUI.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            f.Scroll.viewport = viewport;

            // Content 는 폭만 뷰포트에 맞추고 높이는 쌓은 뒤 직접 넣는다 (ContentSizeFitter 안 씀).
            f.Content = RecUI.Node("Content", viewport);
            f.Content.anchorMin = new Vector2(0f, 1f);
            f.Content.anchorMax = new Vector2(1f, 1f);
            f.Content.pivot = new Vector2(0f, 1f);
            f.Content.anchoredPosition = Vector2.zero;
            f.Content.sizeDelta = new Vector2(0f, 0f);
            f.Scroll.content = f.Content;

            f.Col = new RecCol(f.Content, contentW, bodyGap);

            // 본문에 실제로 보이는 세로 길이.
            // CanvasScaler 가 가로에만 맞추므로(matchWidthOrHeight = 0) 배율은 화면 가로에서 나온다.
            // 레이아웃이 아직 안 돌아 RectTransform 크기를 읽을 수 없는 시점이라 직접 계산한다.
            float scale = Screen.width > 0 ? Screen.width / RecTheme.FrameW : 1f;
            float frameH = scale > 0.0001f ? Screen.height / scale : RecTheme.FrameH;
            f.ViewportH = frameH - pad - BarH - RecTheme.Gap - bottomInset;

            Register(f);
            return f;
        }

        /// <summary>본문을 다 쌓은 뒤 호출 — 스크롤 높이를 확정한다.</summary>
        public static void FinishFrame(RecFrame f)
        {
            float h = f.Col.Height + RecTheme.Pad; // 마지막 아래 여백
            f.Content.sizeDelta = new Vector2(0f, h);
        }

        /// <summary>
        /// 화면 아래에 남는 공간을 지정한 요소들에 몰아준다.
        /// 시안 프레임(1220)은 콘텐츠가 다 안 차서 실기기에서 아래가 휑하게 비는데,
        /// 그 여백을 사진처럼 늘려도 되는 영역이 흡수하게 한다.
        /// grow 에는 바깥→안쪽 순서로 넘긴다 (예: 카드, 그리드, 사진 3개).
        /// 같은 부모끼리 묶어 처리하므로 아래 형제를 여러 번 밀지 않는다.
        ///
        /// maxDelta 로 상한을 둔다. 남는 공간을 전부 밀어넣으면 사진이 폭의 두 배까지 늘어나
        /// 화면이 숨 쉴 틈 없이 꽉 차 보인다. 채우다 남는 건 그냥 아래 여백으로 둔다.
        /// </summary>
        public static void GrowToFill(RecFrame f, float maxDelta, params RectTransform[] grow)
        {
            float delta = Mathf.Min(maxDelta, f.ViewportH - (f.Col.Height + RecTheme.Pad));
            if (grow == null || grow.Length == 0 || delta <= 1f) { FinishFrame(f); return; }

            var byParent = new Dictionary<Transform, List<RectTransform>>();
            foreach (var rt in grow)
            {
                if (rt == null || rt.parent == null) continue;
                if (!byParent.TryGetValue(rt.parent, out var list))
                    byParent[rt.parent] = list = new List<RectTransform>();
                list.Add(rt);
            }

            foreach (var kv in byParent)
            {
                // 늘어나는 요소들의 가장 아래 지점 — 이보다 아래에 있는 형제만 내린다
                float bottom = 0f;
                foreach (var rt in kv.Value)
                    bottom = Mathf.Max(bottom, -rt.anchoredPosition.y + rt.sizeDelta.y);

                for (int i = 0; i < kv.Key.childCount; i++)
                {
                    var c = (RectTransform)kv.Key.GetChild(i);
                    if (kv.Value.Contains(c)) continue;
                    // stretch 로 붙은 배경·그림자는 부모 크기를 따라가므로 건드리지 않는다
                    if (c.anchorMin != c.anchorMax) continue;
                    if (-c.anchoredPosition.y >= bottom - 0.5f)
                        c.anchoredPosition -= new Vector2(0f, delta);
                }

                foreach (var rt in kv.Value)
                    rt.sizeDelta += new Vector2(0f, delta);
            }

            f.Col.Y += delta;
            FinishFrame(f);
        }
    }
}

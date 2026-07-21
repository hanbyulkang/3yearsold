using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Recommend
{
    // "추천 중입니다..." 로딩 오버레이.
    //
    // D 추천 전용이 아니라 어디서든 얹을 수 있게 독립시켰다 — 캔버스만 넘기면 된다.
    // 화면 위에 덮이므로 RecNav 의 화면 스택과 얽히지 않는다.
    //
    // 사용법
    //     var l = RecLoading.Create(canvas, "추천 중입니다", subs);
    //     l.RunForSeconds(2.5f, () => nav.Show("d01"));
    //
    // ─────────────────────────────────────────────────────────────
    // TODO(백엔드 연결): 지금은 실제로 아무것도 기다리지 않고 n초만 흘려보낸다.
    //   추천 API 가 붙으면 RunForSeconds 대신 이렇게 쓴다:
    //
    //     var l = RecLoading.Create(canvas, "추천 중입니다", subs);
    //     l.WaitForApi(() => nav.Show("d01"));            // 응답을 기다리는 상태로 시작
    //     StartCoroutine(api.GetNextStep(res => {         // 요청은 호출하는 쪽에서
    //         RecData.Apply(res);
    //         l.Finish();                                 // 응답이 오면 여기서 종료
    //     }));
    //
    //   MinDuration 은 그대로 두는 편이 좋다. 응답이 너무 빨리 오면 로딩이 한 프레임
    //   깜빡이고 사라져서 오히려 뭔가 잘못된 것처럼 보인다.
    // ─────────────────────────────────────────────────────────────
    public class RecLoading : MonoBehaviour
    {
        /// <summary>응답이 아무리 빨라도 이 시간만큼은 보여준다 (깜빡임 방지).</summary>
        public float MinDuration = 1.2f;

        const float TrackH = 26f;
        const float FillRatio = 0.34f;   // 진행 막대에서 움직이는 조각의 비율
        const float SweepSpeed = 0.55f;  // 왕복 속도
        const float DotInterval = 0.35f;
        const float SubInterval = 1.4f;

        TextMeshProUGUI _title, _sub;
        RectTransform _fill;
        string _titleBase;
        string[] _subs;
        float _trackW, _fillW;

        float _elapsed;
        float _autoFinishAt = -1f;   // > 0 이면 그 시간에 자동 종료 (백엔드 붙기 전 임시)
        bool _apiDone;
        bool _finished;
        Action _onDone;

        /// <summary>로딩 오버레이를 만들어 즉시 띄운다.</summary>
        public static RecLoading Create(Transform canvas, string title, string[] subs)
        {
            var root = RecUI.Node("Loading", canvas);
            RecUI.Stretch(root);
            root.SetAsLastSibling();

            var self = root.gameObject.AddComponent<RecLoading>();
            self.Build(root, title, subs);
            return self;
        }

        void Build(RectTransform root, string title, string[] subs)
        {
            _titleBase = title;
            _subs = subs != null && subs.Length > 0 ? subs : new[] { "" };

            float w = RecTheme.FrameW - RecTheme.Pad * 2f;

            // 시안의 AI 영역과 같은 언어 — 금색 반투명 + 점선 테두리.
            // 화면 세로 가운데에 놓는다.
            var box = RecUI.Node("Box", root);
            var s = RecUI.AddShape(box.gameObject);
            s.raycastTarget = true;            // 로딩 중 아래 화면이 눌리지 않게 막는다
            s.Radius = RecTheme.Radius;
            s.SetFill(RecTheme.AiFill);
            s.SetDashedBorder(2.5f, RecTheme.AiStroke, 9f, 7f);

            var inner = new RecCol(box, w - 48f, 14f, 26f);

            RecUI.Para(inner, "Cap", "AI · 추천 준비 중", RecTheme.FsAiCap, RecTheme.GoldInk,
                true, RecTheme.LineTight, TextAlignmentOptions.Top);

            _title = RecUI.Para(inner, "Title", title, RecTheme.Fs(26f), RecTheme.Ink,
                true, RecTheme.LineTight, TextAlignmentOptions.Top);

            // 진행 막대 — 실제 진행률을 모르므로 좌우로 왕복하는 형태(indeterminate)
            _trackW = inner.Width;
            _fillW = _trackW * FillRatio;

            var track = RecUI.Node("Track", box);
            var ts = RecUI.AddShape(track.gameObject);
            ts.raycastTarget = false;
            ts.Radius = TrackH * 0.5f;
            ts.SetFill(new Color(90 / 255f, 58 / 255f, 32 / 255f, 0.15f));
            ts.SetBorder(2f, new Color(90 / 255f, 58 / 255f, 32 / 255f, 0.25f));
            RecUI.SetRect(track, 0f, inner.Y, _trackW, TrackH);
            track.gameObject.AddComponent<RectMask2D>();

            var fill = RecUI.Shape("Fill", track);
            fill.Radius = TrackH * 0.5f;
            fill.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
            _fill = fill.rectTransform;
            RecUI.SetRect(_fill, 0f, 0f, _fillW, TrackH);

            inner.Advance(TrackH);

            _sub = RecUI.Para(inner, "Sub", _subs[0], RecTheme.FsBody, RecTheme.Sub,
                false, RecTheme.LineNormal, TextAlignmentOptions.Top);

            float h = inner.Height + 26f;

            // 박스를 가운데 정렬 (좌우 패딩 24, 세로 중앙)
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(w, h);
            box.anchoredPosition = Vector2.zero;

            // 안쪽 요소는 좌우 패딩만큼 민다 (RecUI 의 상자들과 같은 규칙)
            for (int i = 0; i < box.childCount; i++)
            {
                var c = (RectTransform)box.GetChild(i);
                c.anchoredPosition += new Vector2(24f, 0f);
            }
        }

        /// <summary>지금 방식 — 그냥 n초 뒤에 넘어간다. API 가 붙으면 WaitForApi 로 교체한다.</summary>
        public void RunForSeconds(float seconds, Action onDone)
        {
            _autoFinishAt = Mathf.Max(0.1f, seconds);
            _onDone = onDone;
        }

        /// <summary>API 응답을 기다리는 상태로 시작. 응답이 오면 Finish() 를 부른다.</summary>
        public void WaitForApi(Action onDone)
        {
            _autoFinishAt = -1f;
            _onDone = onDone;
        }

        /// <summary>API 응답 도착. MinDuration 이 남았으면 그만큼 더 보여주고 닫는다.</summary>
        public void Finish() => _apiDone = true;

        void Update()
        {
            if (_finished) return;
            _elapsed += Time.deltaTime;

            Animate();

            bool timeUp = _autoFinishAt > 0f && _elapsed >= _autoFinishAt;
            bool apiReady = _apiDone && _elapsed >= MinDuration;
            if (!timeUp && !apiReady) return;

            _finished = true;
            var cb = _onDone;
            _onDone = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        void Animate()
        {
            // 제목 뒤 점 — "추천 중입니다" → "..." 까지 반복
            int dots = Mathf.FloorToInt(_elapsed / DotInterval) % 4;
            _title.text = _titleBase + new string('.', dots);

            // 진행 막대 왕복. PingPong 이라 끝에서 잠깐 멈춰 보이는 걸 피하려고
            // 사인 곡선으로 부드럽게 왕복시킨다.
            float t = (Mathf.Sin(_elapsed * SweepSpeed * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
            _fill.anchoredPosition = new Vector2((_trackW - _fillW) * t, 0f);

            // 안내 문구 순환
            if (_subs.Length > 1)
            {
                int i = Mathf.FloorToInt(_elapsed / SubInterval) % _subs.Length;
                if (_sub.text != _subs[i]) _sub.text = _subs[i];
            }
        }
    }
}

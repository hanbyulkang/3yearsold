using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Recommend
{
    // D. 추천 루프 (D-01~D-06) 진입점.
    // MG1 과 같은 규칙: 씬에는 이 컴포넌트 하나만 두고 UI 는 전부 코드로 만든다.
    //
    // 디자인 원본: Desktop/recomend.html (design-doc canvas, 프레임 686×1220)
    // 캔버스 referenceResolution 을 시안 프레임과 같게 두어 모든 수치를 1:1로 옮겼다.
    public class RecBootstrap : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset koreanFont;

        [Header("배경 (없으면 크림색 단색으로 폴백)")]
        [SerializeField] Sprite background;

        [Header("로딩 — 추천 API 가 붙기 전까지 쓰는 임시 대기 시간(초)")]
        [SerializeField] bool showLoading = true;
        [SerializeField] float loadingSeconds = 2.5f;

        RecNav _nav;

        /// <summary>다른 화면으로 이동 (D-06 처럼 D 안에 진입 경로가 없는 화면 확인용).</summary>
        public void Go(string id) => _nav.Show(id);

        void Awake()
        {
            RecUI.Font = koreanFont != null ? koreanFont : TMP_Settings.defaultFontAsset;

            var canvas = BuildCanvas();
            EnsureEventSystem();

            // 화면은 전부 활성 상태로 만든 뒤(글자 높이를 재야 하므로) Show 가 나머지를 끈다.
            _nav = gameObject.AddComponent<RecNav>();
            RecScreens.BuildAll(_nav, canvas);
            _nav.Show("d01", false);

            if (!showLoading) return;

            // 추천은 결과가 나오기까지 기다림이 생기는 지점이라, 첫 화면은 로딩으로 연다.
            // 지금은 그냥 n초를 흘려보낸다 — 실제 추천 API 가 붙으면 RecLoading 위쪽 주석대로
            // WaitForApi + Finish() 로 바꾸면 화면은 그대로 두고 대기 조건만 교체된다.
            _nav.HideAll();
            var loading = RecLoading.Create(canvas, "추천 중입니다", new[]
            {
                "설문에 남겨주신 답변을 살펴보고 있어요",
                "최근 산책·돌봄 기록을 함께 보고 있어요",
                "가까운 보호소의 보호견을 맞춰보고 있어요",
            });
            loading.RunForSeconds(loadingSeconds, () => _nav.Show("d01", false));
        }

        Transform BuildCanvas()
        {
            var go = new GameObject("RecCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RecTheme.FrameW, RecTheme.FrameH);
            // 시안은 가로 686 기준으로 그려졌다. 세로는 기기마다 다르므로 가로에만 맞춘다.
            scaler.matchWidthOrHeight = 0f;

            // 배경
            var bg = RecUI.Node("Background", go.transform);
            RecUI.Stretch(bg);
            if (background != null)
            {
                var img = bg.gameObject.AddComponent<Image>();
                img.sprite = background;
                img.color = Color.white;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.raycastTarget = false;
            }
            else
            {
                var s = RecUI.AddShape(bg.gameObject);
                s.raycastTarget = false;
                s.Radius = 0f;
                s.SetFill(RecTheme.Cream);
            }

            return go.transform;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

    }
}

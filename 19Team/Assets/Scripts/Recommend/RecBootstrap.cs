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

        [Header("로딩 — 끄면 서버 없이 목업으로 즉시 연다 (오프라인 개발용)")]
        [SerializeField] bool showLoading = true;
#pragma warning disable 0414   // 씬 직렬화 호환용으로 남겨둠 (서버 연동 후 미사용)
        [SerializeField] float loadingSeconds = 2.5f;
#pragma warning restore 0414

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

            if (!showLoading)
            {
                // 오프라인 개발 경로 — 서버 없이 목업으로 즉시 연다.
                RecScreens.BuildAll(_nav, canvas);
                _nav.Show("d01", false);
                return;
            }

            // 백엔드 연결 (RecLoading 주석의 WaitForApi 패턴 그대로).
            // 서버 추천을 RecData에 덮어쓴 뒤 화면을 만든다 — 실패하면 목업이 그대로 보인다.
            var loading = RecLoading.Create(canvas, "추천 중입니다", new[]
            {
                "설문에 남겨주신 답변을 살펴보고 있어요",
                "최근 산책·돌봄 기록을 함께 보고 있어요",
                "가까운 보호소의 보호견을 맞춰보고 있어요",
            });
            loading.WaitForApi(() => _nav.Show("d01", false));
            Boot(canvas, loading);
        }

        async void Boot(Transform canvas, RecLoading loading)
        {
            try
            {
                await Backend.RecApi.LoadIntoRecData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Rec] 서버 추천 실패 — 목업 데이터로 표시: {e.Message}");
            }
            RecScreens.BuildAll(_nav, canvas);
            loading.Finish();
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

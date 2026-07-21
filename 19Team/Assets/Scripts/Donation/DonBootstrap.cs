using Recommend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Donation
{
    // E. 후원 (E-01~E-04) 진입점.
    // MG1·D 추천과 같은 규칙: 씬에는 이 컴포넌트 하나만 두고 UI 는 전부 코드로 만든다.
    //
    // 디자인 원본: Desktop/donation.dc.html + svg-donate/ (design-doc canvas, 프레임 686×1220)
    // 캔버스 referenceResolution 을 시안 프레임과 같게 두어 모든 수치를 1:1로 옮겼다.
    //
    // 화면 뼈대(RecNav)·상자·버튼·글자(RecUI)·도형(RecShape)은 D 추천 루프와 같은 디자인
    // 시스템이라 Recommend 폴더의 것을 그대로 읽어 쓴다. 이 폴더 밖의 파일은 고치지 않는다.
    public class DonBootstrap : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset koreanFont;

        [Header("배경 (없으면 크림색 단색으로 폴백)")]
        [SerializeField] Sprite background;

        [Header("뼈다귀 아이콘 (없으면 상단바에 '뼈다귀' 글자로 표시)")]
        [SerializeField] Sprite boneIcon;

        RecNav _nav;

        /// <summary>다른 화면으로 이동 (E-04 처럼 진입 경로가 하나뿐인 화면 확인용).</summary>
        public void Go(string id) => _nav.Show(id);

        void Awake()
        {
            RecUI.Font = koreanFont != null ? koreanFont : TMP_Settings.defaultFontAsset;
            DonUI.BoneIcon = boneIcon;

            var canvas = BuildCanvas();
            EnsureEventSystem();

            // 화면은 전부 활성 상태로 만든 뒤(글자 높이를 재야 하므로) Show 가 나머지를 끈다.
            _nav = gameObject.AddComponent<RecNav>();
            Boot(canvas);
        }

        /// <summary>
        /// 서버 값을 DonData에 덮은 뒤 화면을 만든다 (D 추천과 같은 방식).
        /// 실패하면 DonData의 목업이 그대로 보인다 — 데모가 네트워크에 인질 잡히지 않게.
        /// </summary>
        async void Boot(Transform canvas)
        {
            try
            {
                await Backend.DonApi.LoadIntoDonData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Don] 서버 후원 데이터 실패 — 목업으로 표시: {e.Message}");
            }
            DonScreens.BuildAll(_nav, canvas);
            _nav.Show("e01", false);
        }

        Transform BuildCanvas()
        {
            var go = new GameObject("DonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RecTheme.FrameW, RecTheme.FrameH);
            // 시안은 가로 686 기준으로 그려졌다. 세로는 기기마다 다르므로 가로에만 맞춘다.
            scaler.matchWidthOrHeight = 0f;

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

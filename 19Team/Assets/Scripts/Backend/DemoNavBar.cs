using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Backend
{
    /// <summary>
    /// 데모용 씬 이동 바 + 앱 부트스트랩.
    ///
    /// 지금은 씬들이 서로 연결돼 있지 않다 (마당·MG1·2048·추천이 각각 고립).
    /// 정식 탭바(B-01 하단 5탭)가 생기기 전까지, 어느 씬에서든 화면 상단의
    /// 작은 바로 오갈 수 있게 한다. 씬 파일은 건드리지 않는다 —
    /// 첫 씬 로드 때 코드로 만들어 DontDestroyOnLoad로 유지한다.
    ///
    /// 함께 하는 일: 앱 시작 시 데모 로그인(AppSession)을 걸어
    /// 어떤 씬에서 시작해도 서버 연동이 준비되게 한다.
    /// </summary>
    public class DemoNavBar : MonoBehaviour
    {
        static bool _spawned;

        // (라벨, 씬 이름) — EditorBuildSettings에 등록돼 있어야 한다
        static readonly (string label, string scene)[] Tabs =
        {
            ("마당",  "SampleScene"),
            ("퍼즐",  "mini-game-1"),
            ("2048", "minigame02"),
            ("추천",  "d-recommend"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_spawned) return;
            _spawned = true;

            // 데모 로그인 — 어느 씬에서 시작해도 백엔드가 준비된다
            _ = AppSession.EnsureSignedIn();

            var go = new GameObject("DemoNavBar", typeof(DemoNavBar));
            DontDestroyOnLoad(go);
        }

        void Start()
        {
            var canvasGo = new GameObject("NavCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // 게임 UI 위, 클릭은 바 영역만 가로챈다

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(686, 1220);
            scaler.matchWidthOrHeight = 0f;

            // 상단 우측의 얇은 바 — 게임 하단 UI를 가리지 않는 위치
            var bar = new GameObject("Bar", typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(canvasGo.transform, false);
            bar.color = new Color(0f, 0f, 0f, 0.45f);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(Tabs.Length * 92f + 12f, 46f);
            rt.anchoredPosition = new Vector2(-8f, -8f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 5, 5);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (var (label, scene) in Tabs) MakeButton(bar.transform, label, scene);
        }

        void MakeButton(Transform parent, string label, string scene)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

            var textGo = new GameObject("Text", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.GetComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 22;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.2f, 0.15f, 0.1f);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            bool isCurrent = SceneManager.GetActiveScene().name == scene;
            if (isCurrent) go.GetComponent<Image>().color = new Color(0.95f, 0.7f, 0.3f);

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (SceneManager.GetActiveScene().name == scene) return;
                SceneManager.LoadScene(scene);
            });
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            // 현재 씬 표시를 갱신하기 위해 바를 다시 그린다
            var old = transform.Find("NavCanvas");
            if (old != null) Destroy(old.gameObject);
            Start();
        }
    }
}

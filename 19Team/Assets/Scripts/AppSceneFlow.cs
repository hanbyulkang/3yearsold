using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AppSceneFlow : MonoBehaviour
{
    private static AppSceneFlow _instance;
    private CanvasGroup _fadeGroup;
    private bool _transitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;
        GameObject root = new GameObject("App Scene Flow");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<AppSceneFlow>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start() => WireScene(SceneManager.GetActiveScene());
    private void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => WireScene(scene);

    private void WireScene(Scene scene)
    {
        if (scene.name.Equals("Main", StringComparison.OrdinalIgnoreCase))
        {
            Button start = FindButtonByObjectName(scene, "TapToStart");
            if (start != null)
            {
                start.onClick.RemoveListener(GoToSurvey);
                start.onClick.AddListener(GoToSurvey);
            }
        }
        else if (scene.name.Equals("Survey", StringComparison.OrdinalIgnoreCase))
        {
            Button village = FindButtonByText(scene, "마당으로 들어가기");
            if (village != null)
            {
                village.onClick.RemoveListener(GoToVillage);
                village.onClick.AddListener(GoToVillage);
            }
        }
        else if (scene.name.Equals("Suntail Village", StringComparison.OrdinalIgnoreCase))
        {
            Button shop = FindButtonByObjectName(scene, "Shop");
            if (shop != null)
            {
                shop.onClick.RemoveListener(GoToShop);
                shop.onClick.AddListener(GoToShop);
            }

            Button adoption = FindButtonByObjectName(scene, "Skill");
            if (adoption != null)
            {
                adoption.onClick.RemoveListener(GoToAdoption);
                adoption.onClick.AddListener(GoToAdoption);
            }

            WireNamedChildren(scene, "Meet", "Plus", GoToShop);
            WireNamedChildren(scene, "Detail", "Shop", GoToShop);

            Button meetPlus = FindButtonByObjectName(scene, "Plus");
            if (meetPlus != null)
            {
                meetPlus.onClick.RemoveListener(GoToShop);
                meetPlus.onClick.AddListener(GoToShop);
            }
        }
    }

    private static void GoToSurvey() => SceneManager.LoadScene("Survey");
    private static void GoToVillage() => TransitionTo("Suntail Village");
    public static void GoHome() => TransitionTo("Suntail Village");
    private static void GoToShop() => TransitionTo("marketFlow");
    private static void GoToAdoption() => TransitionTo("d-recommend");

    private static void TransitionTo(string sceneName)
    {
        if (_instance != null) _instance.StartSceneTransition(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    private void BuildFadeOverlay()
    {
        GameObject canvasObject = new GameObject("White Scene Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);

        GameObject white = new GameObject("White", typeof(RectTransform), typeof(Image));
        white.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)white.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = white.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        _fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        _fadeGroup.alpha = 0f;
        _fadeGroup.blocksRaycasts = false;
        canvasObject.SetActive(false);
    }

    private void StartSceneTransition(string sceneName)
    {
        if (_transitioning) return;
        StartCoroutine(SceneTransition(sceneName));
    }

    private IEnumerator SceneTransition(string sceneName)
    {
        _transitioning = true;
        _fadeGroup.gameObject.SetActive(true);
        _fadeGroup.blocksRaycasts = true;
        yield return Fade(0f, 1f, 0.28f);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (!load.isDone) yield return null;
        _fadeGroup.alpha = 1f;
        yield return null;
        yield return null;

        yield return Fade(1f, 0f, 0.9f);
        _fadeGroup.blocksRaycasts = false;
        _fadeGroup.gameObject.SetActive(false);
        _transitioning = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        _fadeGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            _fadeGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _fadeGroup.alpha = to;
    }

    private static Button FindButtonByObjectName(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    Button button = child.GetComponent<Button>();
                    if (button == null) button = child.gameObject.AddComponent<Button>();
                    if (button.targetGraphic == null) button.targetGraphic = child.GetComponent<Graphic>();
                    return button;
                }
        return null;
    }

    private static Button FindButtonByText(Scene scene, string wantedText)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && text.text.IndexOf(wantedText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Button button = text.GetComponentInParent<Button>(true);
                    if (button != null) return button;
                    Transform target = text.transform.parent;
                    button = target.GetComponent<Button>();
                    if (button == null) button = target.gameObject.AddComponent<Button>();
                    if (button.targetGraphic == null) button.targetGraphic = target.GetComponent<Graphic>();
                    return button;
                }
        return null;
    }

    private static void WireNamedChildren(Scene scene, string parentName, string buttonName, UnityEngine.Events.UnityAction action)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform parent in root.GetComponentsInChildren<Transform>(true))
            {
                if (!parent.name.Equals(parentName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.name.Equals(buttonName, StringComparison.OrdinalIgnoreCase)) continue;
                    Button button = child.GetComponent<Button>();
                    if (button == null) button = child.gameObject.AddComponent<Button>();
                    if (button.targetGraphic == null) button.targetGraphic = child.GetComponent<Graphic>();
                    button.onClick.RemoveListener(action);
                    button.onClick.AddListener(action);
                }
            }
        }
}

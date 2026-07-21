using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AppSceneFlow : MonoBehaviour
{
    private static AppSceneFlow _instance;

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
    }

    private static void GoToSurvey() => SceneManager.LoadScene("Survey");
    private static void GoToVillage() => SceneManager.LoadScene("Suntail Village");

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
}

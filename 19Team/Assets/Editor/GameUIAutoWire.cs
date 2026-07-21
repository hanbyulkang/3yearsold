#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameUIAutoWire
{
    private const string DataPath = "Assets/Data/GameCurrencyDataSet.asset";

    static GameUIAutoWire()
    {
        EditorApplication.delayCall += Wire;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => EditorApplication.delayCall += Wire;

    [MenuItem("Tools/Game UI/Wire Game UI")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;
        Transform foot = FindDirect(canvas.transform, "Foot");
        Transform top = FindDirect(canvas.transform, "TOP");
        Transform bone = top != null ? FindDirect(top, "Bone") : null;
        Transform gamePanel = FindDirect(canvas.transform, "Game");
        Transform horizontal = FindDirect(canvas.transform, "Horizontal");
        if (foot == null || horizontal == null) return;
        Transform homeTab = horizontal != null ? FindDirect(horizontal, "Main") : null;
        Transform gameTab = horizontal != null ? FindDirect(horizontal, "Game") : null;
        Transform inventoryTab = horizontal != null ? FindDirect(horizontal, "Inventory") : null;

        GameCurrencyDataSet data = AssetDatabase.LoadAssetAtPath<GameCurrencyDataSet>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GameCurrencyDataSet>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        GameUIController controller = canvas.GetComponent<GameUIController>();
        if (controller == null) controller = Undo.AddComponent<GameUIController>(canvas.gameObject);
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_dataSet").objectReferenceValue = data;

        TMP_Text boneText = bone != null ? bone.GetComponentInChildren<TMP_Text>(true) : null;
        TMP_Text pawText = FindText(gamePanel, "PawCount", "FootCount", "발바닥 갯수", "발바닥 개수");
        TMP_Text timerText = FindText(gamePanel, "Recovery", "Timer", "회복", "초시계");
        if (pawText == null && foot != null) pawText = EnsureText(foot, "PawCountText", new Vector2(170f, 12f), new Vector2(100f, 30f), "5/5");
        if (timerText == null && foot != null) timerText = EnsureText(foot, "RecoveryTimerText", new Vector2(170f, -16f), new Vector2(110f, 28f), "10:00");
        so.FindProperty("_pawCountText").objectReferenceValue = pawText;
        so.FindProperty("_boneCountText").objectReferenceValue = boneText;
        so.FindProperty("_recoveryTimerText").objectReferenceValue = timerText;

        var paws = new List<Image>();
        if (foot != null)
            for (int i = 0; i < foot.childCount; i++)
            {
                Image image = foot.GetChild(i).GetComponent<Image>();
                if (image != null && paws.Count < GameCurrencyStore.MaxPaws) paws.Add(image);
            }
        SerializedProperty pawProp = so.FindProperty("_pawImages");
        pawProp.arraySize = paws.Count;
        for (int i = 0; i < paws.Count; i++) pawProp.GetArrayElementAtIndex(i).objectReferenceValue = paws[i];

        var playButtons = new List<Button>();
        if (gamePanel != null)
        {
            foreach (Button button in gamePanel.GetComponentsInChildren<Button>(true))
                if (IsPlayButton(button)) playButtons.Add(button);
            foreach (Transform child in gamePanel.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("PlayBtn", StringComparison.OrdinalIgnoreCase) < 0 || child.GetComponent<Button>() != null) continue;
                Button button = Undo.AddComponent<Button>(child.gameObject);
                button.targetGraphic = child.GetComponent<Graphic>();
                button.interactable = true;
                playButtons.Add(button);
            }
        }
        SerializedProperty entries = so.FindProperty("_playEntries");
        entries.arraySize = playButtons.Count;
        for (int i = 0; i < playButtons.Count; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("button").objectReferenceValue = playButtons[i];
            entry.FindPropertyRelative("sceneName").stringValue = ResolveMiniGameScene(playButtons[i]);
        }

        Button gameButton = gameTab != null ? gameTab.GetComponent<Button>() : null;
        Button homeButton = homeTab != null ? homeTab.GetComponent<Button>() : null;
        Image gameImage = gameTab != null ? gameTab.GetComponent<Image>() : null;
        Image homeImage = homeTab != null ? homeTab.GetComponent<Image>() : null;
        Image inventoryImage = inventoryTab != null ? inventoryTab.GetComponent<Image>() : null;
        so.FindProperty("_homeTabButton").objectReferenceValue = homeButton;
        so.FindProperty("_gameTabButton").objectReferenceValue = gameButton;
        so.FindProperty("_gameTabImage").objectReferenceValue = gameImage;
        so.FindProperty("_gameTabRect").objectReferenceValue = gameTab as RectTransform;
        so.FindProperty("_gameNormalSprite").objectReferenceValue = gameImage != null ? gameImage.sprite : null;
        so.FindProperty("_gameSelectedSprite").objectReferenceValue = homeImage != null ? homeImage.sprite : null;
        so.FindProperty("_homeTabImage").objectReferenceValue = homeImage;
        so.FindProperty("_homeTabRect").objectReferenceValue = homeTab as RectTransform;
        so.FindProperty("_homeNormalSprite").objectReferenceValue = inventoryImage != null ? inventoryImage.sprite : null;
        if (homeTab != null) so.FindProperty("_selectedScale").floatValue = homeTab.localScale.x;
        so.FindProperty("_gameView").objectReferenceValue = gamePanel != null ? gamePanel.gameObject : null;
        string[] floatingNames = { "DogHeart", "DogFood", "DogPoop" };
        SerializedProperty floatingUis = so.FindProperty("_dogFloatingUis");
        floatingUis.arraySize = floatingNames.Length;
        for (int i = 0; i < floatingNames.Length; i++)
        {
            Transform floating = FindDirect(canvas.transform, floatingNames[i]);
            floatingUis.GetArrayElementAtIndex(i).objectReferenceValue = floating != null ? floating.gameObject : null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path) && scene.path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Game UI] Wired 5 paws, {playButtons.Count} Play buttons, currency texts and Game tab animation.", controller);
    }

    private static bool IsPlayButton(Button button)
    {
        if (button.name.IndexOf("play", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null || string.IsNullOrWhiteSpace(text.text)) return false;
        string value = text.text.Trim();
        return value.IndexOf("플레이", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("입장", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("시작", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ResolveMiniGameScene(Button button)
    {
        Transform card = button.transform.parent;
        if (card != null)
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && text.text.IndexOf("2048", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "minigame02";
        return "mini-game-1";
    }

    private static TMP_Text FindText(Transform root, params string[] tokens)
    {
        if (root == null) return null;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            foreach (string token in tokens)
                if (text.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(text.text) && text.text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)) return text;
        return null;
    }

    private static TMP_Text EnsureText(Transform parent, string name, Vector2 position, Vector2 size, string value)
    {
        Transform existing = FindDirect(parent, name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "Create Game currency text");
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 18f;
            text.color = Color.white;
            text.raycastTarget = false;
        }
        RectTransform rect = (RectTransform)text.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.text = value;
        return text;
    }

    private static Transform FindDirect(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent.GetChild(i);
        return null;
    }
}
#endif

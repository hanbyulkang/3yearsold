#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DecorationPlacementAutoWire
{
    const string ScenePath = "Assets/Raygeas/Suntail Village/Demo/Suntail Village.unity";
    const string DataPath = "Assets/Data/DecorationPlacementDataSet.asset";

    static DecorationPlacementAutoWire()
    {
        EditorApplication.delayCall += Wire;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += Wire;
    }

    [MenuItem("Tools/Suntail Village/Wire Decoration Placement")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform detail = FindExact("Detail");
        if (detail == null) return;
        Canvas canvas = detail.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        var frames = new List<Transform>();
        foreach (Transform child in detail.GetComponentsInChildren<Transform>(true))
            if ((child.name == "Frame" || child.name == "Frame (1)") && FindDirectTitle(child) != null) frames.Add(child);
        if (frames.Count != 2) { Debug.LogError("[Decoration] Expected two Detail item Frames."); return; }

        Transform sourceApply = FindByText(detail, "적용하기")?.parent;
        Transform sourceApplied = FindByText(detail, "적용 중")?.parent;
        if (sourceApply == null || sourceApplied == null) return;

        DecorationPlacementDataSet data = AssetDatabase.LoadAssetAtPath<DecorationPlacementDataSet>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DecorationPlacementDataSet>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        DecorationPlacementController controller = canvas.GetComponent<DecorationPlacementController>();
        if (controller == null) controller = Undo.AddComponent<DecorationPlacementController>(canvas.gameObject);
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_detailUI").objectReferenceValue = detail.gameObject;
        so.FindProperty("_detailAnimator").objectReferenceValue = detail.GetComponent<DetailPanelAnimator>();
        so.FindProperty("_dataSet").objectReferenceValue = data;
        SerializedProperty items = so.FindProperty("_items");
        items.arraySize = frames.Count;

        frames.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        for (int i = 0; i < frames.Count; i++)
        {
            Transform frame = frames[i];
            string title = FindDirectTitle(frame)?.text ?? string.Empty;
            bool apple = title.Contains("사과");
            string id = apple ? "apple_box" : "well";
            string prefabPath = apple
                ? "Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Box_1.prefab"
                : "Assets/Raygeas/Suntail Village/Assets/Prefabs/Environment/Well_1.prefab";

            Transform apply = FindDirect(frame, "ApplyButton");
            Transform applied = FindDirect(frame, "AppliedButton");
            Transform existingConfirm = FindDirect(frame, "Confirm");
            if (apple && applied == null && existingConfirm != null) { applied = existingConfirm; applied.name = "AppliedButton"; }
            if (!apple && apply == null && existingConfirm != null) { apply = existingConfirm; apply.name = "ApplyButton"; }
            if (apply == null) apply = CloneButton(sourceApply, frame, "ApplyButton");
            if (applied == null) applied = CloneButton(sourceApplied, frame, "AppliedButton");

            Button applyButton = apply.GetComponent<Button>();
            if (applyButton == null) applyButton = Undo.AddComponent<Button>(apply.gameObject);
            applyButton.targetGraphic = apply.GetComponent<Graphic>();
            apply.gameObject.SetActive(true);
            applied.gameObject.SetActive(false);

            SerializedProperty item = items.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("id").stringValue = id;
            item.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            item.FindPropertyRelative("applyButton").objectReferenceValue = applyButton;
            item.FindPropertyRelative("appliedButton").objectReferenceValue = applied.gameObject;
            data.ConfigureItem(i, id);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scene.path.StartsWith("Temp/", StringComparison.OrdinalIgnoreCase) ? ScenePath : scene.path);
        Debug.Log("[Decoration] Detail buttons, placement prefabs, emission preview, and save data were connected.", controller);
    }

    static Transform CloneButton(Transform source, Transform parent, string name)
    {
        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
        clone.name = name;
        Undo.RegisterCreatedObjectUndo(clone, "Create decoration state button");
        return clone.transform;
    }

    static TMP_Text FindDirectTitle(Transform frame)
    {
        foreach (Transform child in frame)
            if (child.name.StartsWith("Title", StringComparison.Ordinal) && child.GetComponent<TMP_Text>() != null)
                return child.GetComponent<TMP_Text>();
        return null;
    }

    static Transform FindByText(Transform root, string value)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.text == value) return text.transform;
        return null;
    }

    static Transform FindDirect(Transform root, string name)
    {
        foreach (Transform child in root) if (child.name == name) return child;
        return null;
    }

    static Transform FindExact(string name)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class DetailTabAutoWire
{
    static DetailTabAutoWire() { EditorApplication.delayCall += Wire; }

    [MenuItem("Tools/Suntail Village/Wire Detail Tabs")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform detail = FindExact("Detail");
        Transform cloths = FindChild(detail, "Cloths");
        Transform yard = FindChild(detail, "Yard");
        Transform clothsUIs = FindChild(detail, "ClothsUIs");
        Transform yardUIs = FindChild(detail, "YardUIs");
        if (detail == null || cloths == null || yard == null || clothsUIs == null || yardUIs == null) return;

        Button clothsButton = EnsureButton(cloths);
        Button yardButton = EnsureButton(yard);
        DetailTabController controller = detail.GetComponent<DetailTabController>();
        if (controller == null) controller = Undo.AddComponent<DetailTabController>(detail.gameObject);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_clothsButton").objectReferenceValue = clothsButton;
        so.FindProperty("_yardButton").objectReferenceValue = yardButton;
        so.FindProperty("_clothsUIs").objectReferenceValue = clothsUIs.gameObject;
        so.FindProperty("_yardUIs").objectReferenceValue = yardUIs.gameObject;
        Image clothsImage = cloths.GetComponent<Image>();
        Image yardImage = yard.GetComponent<Image>();
        so.FindProperty("_clothsImage").objectReferenceValue = clothsImage;
        so.FindProperty("_yardImage").objectReferenceValue = yardImage;
        so.FindProperty("_clothsText").objectReferenceValue = cloths.GetComponentInChildren<TMP_Text>(true);
        so.FindProperty("_yardText").objectReferenceValue = yard.GetComponentInChildren<TMP_Text>(true);
        so.FindProperty("_selectedSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Inventory/svg-decorate/tab-pill-active.png");
        so.FindProperty("_unselectedSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Inventory/svg-decorate/tab-pill-inactive.png");
        so.ApplyModifiedPropertiesWithoutUndo();
        clothsUIs.gameObject.SetActive(true);
        yardUIs.gameObject.SetActive(false);

        EditorUtility.SetDirty(controller);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DetailTabs] Cloths and Yard buttons were connected.", controller);
    }

    static Button EnsureButton(Transform target)
    {
        Button button = target.GetComponent<Button>();
        if (button == null) button = Undo.AddComponent<Button>(target.gameObject);
        button.targetGraphic = target.GetComponent<Graphic>();
        return button;
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        return null;
    }

    static Transform FindExact(string name)
    {
        foreach (Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.name == name) return item;
        return null;
    }
}
#endif

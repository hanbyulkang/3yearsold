#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DetailNavigationAutoWire
{
    static DetailNavigationAutoWire() { EditorApplication.delayCall += Wire; }

    [MenuItem("Tools/Suntail Village/Wire Detail Animation And Bottom Navigation")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform detail = FindExact("Detail");
        if (detail == null) return;

        BottomNavigationController oldNavigation = UnityEngine.Object.FindFirstObjectByType<BottomNavigationController>(FindObjectsInactive.Include);
        if (oldNavigation != null) Undo.DestroyObjectImmediate(oldNavigation);

        DetailPanelAnimator animator = detail.GetComponent<DetailPanelAnimator>();
        if (animator == null) animator = Undo.AddComponent<DetailPanelAnimator>(detail.gameObject);
        Transform close = FindChild(detail, "X (1)");
        Button closeButton = close != null ? EnsureButton(close) : null;
        SerializedObject animatorSO = new SerializedObject(animator);
        animatorSO.FindProperty("_panel").objectReferenceValue = detail as RectTransform;
        animatorSO.FindProperty("_closeButton").objectReferenceValue = closeButton;
        animatorSO.ApplyModifiedPropertiesWithoutUndo();

        DecorationPlacementController placement = detail.GetComponentInParent<Canvas>(true)?.GetComponent<DecorationPlacementController>();
        if (placement != null)
        {
            SerializedObject placementSO = new SerializedObject(placement);
            placementSO.FindProperty("_detailAnimator").objectReferenceValue = animator;
            placementSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placement);
        }

        EditorUtility.SetDirty(animator);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[DetailNavigation] Detail slide animation was connected.", animator);
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
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        return null;
    }

    static Transform FindExact(string name)
    {
        foreach (Transform child in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (child.name == name) return child;
        return null;
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DogFoodAutoWire
{
    private const string SceneName = "Suntail Village";

    static DogFoodAutoWire()
    {
        EditorApplication.delayCall += TryWire;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += TryWire;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryWire;
    }

    [MenuItem("Tools/Suntail Village/Wire Dog Food UI")]
    public static void TryWire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Equals(SceneName, StringComparison.OrdinalIgnoreCase))
            return;

        RectTransform dogFood = null;
        Transform plate = null;
        Transform plateFallback = null;
        foreach (Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.name.Equals("DogFood", StringComparison.OrdinalIgnoreCase))
                dogFood = item as RectTransform;
            if (!(item is RectTransform) && item.name.Equals("Plate", StringComparison.OrdinalIgnoreCase))
                plate = item;
            else if (!(item is RectTransform) && item.name.IndexOf("plate", StringComparison.OrdinalIgnoreCase) >= 0)
                plateFallback = item;
        }

        plate ??= plateFallback;

        if (dogFood == null || plate == null)
        {
            Debug.LogWarning("[DogFood] DogFood UI or Plate object was not found in the active scene.");
            return;
        }

        Canvas canvas = dogFood.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;

        DogFoodInteraction controller = canvas.GetComponent<DogFoodInteraction>();
        if (controller == null)
            controller = Undo.AddComponent<DogFoodInteraction>(canvas.gameObject);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("_dogFood").objectReferenceValue = dogFood;
        serialized.FindProperty("_dogFoodButton").objectReferenceValue = dogFood.GetComponent<Button>();
        serialized.FindProperty("_plate").objectReferenceValue = plate;
        DogWanderAI dog = UnityEngine.Object.FindFirstObjectByType<DogWanderAI>(FindObjectsInactive.Include);
        serialized.FindProperty("_dog").objectReferenceValue = dog;
        serialized.FindProperty("_boneRewardSource").objectReferenceValue = canvas.GetComponent<DogHeartInteraction>();
        SerializedProperty approachDistance = serialized.FindProperty("_approachDistance");
        if (approachDistance.floatValue < 1.2f)
            approachDistance.floatValue = 1.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Animator wrongAnimator = dogFood.GetComponent<Animator>();
        if (wrongAnimator != null)
        {
            Undo.RecordObject(wrongAnimator, "Disable DogFood UI Animator");
            wrongAnimator.enabled = false;
            EditorUtility.SetDirty(wrongAnimator);
        }

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DogFood] Connected DogFood to {plate.name} on Canvas.", controller);
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DogPoopAutoWire
{
    const string TargetScenePath = "Assets/Raygeas/Suntail Village/Demo/Suntail Village.unity";
    static DogPoopAutoWire()
    {
        EditorApplication.delayCall += Wire;
        EditorApplication.update += RetryUntilWired;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += Wire;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Wire; };
    }

    static void RetryUntilWired()
    {
        if (EditorApplication.timeSinceStartup < 1d || EditorApplication.isCompiling) return;
        Wire();
    }

    [MenuItem("Tools/Suntail Village/Wire Dog Poop System")]
    public static void Wire()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() ||
            (!activeScene.name.Equals("Suntail Village", StringComparison.OrdinalIgnoreCase) &&
             !activeScene.path.Equals(TargetScenePath, StringComparison.OrdinalIgnoreCase)))
            return;

        RectTransform dogPoop = FindExact("DogPoop") as RectTransform;
        Transform poop = FindWorldExact("Poop");
        Transform dogRoot = FindWorldExact("Dog");
        DogWanderAI dog = dogRoot != null ? dogRoot.GetComponent<DogWanderAI>() : null;
        if (dog == null)
        {
            if (dogRoot != null)
                dog = Application.isPlaying
                    ? dogRoot.gameObject.AddComponent<DogWanderAI>()
                    : Undo.AddComponent<DogWanderAI>(dogRoot.gameObject);
        }
        if (dogPoop == null || poop == null || dog == null)
        {
            Debug.LogError($"[DogPoop] Auto-wire missing: UI={dogPoop != null}, Poop={poop != null}, Dog={dog != null}");
            EditorApplication.update -= RetryUntilWired;
            return;
        }
        Canvas canvas = dogPoop.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        DogPoopInteraction controller = canvas.GetComponent<DogPoopInteraction>();
        if (controller == null)
            controller = Application.isPlaying
                ? canvas.gameObject.AddComponent<DogPoopInteraction>()
                : Undo.AddComponent<DogPoopInteraction>(canvas.gameObject);
        MissionUIController mission = UnityEngine.Object.FindFirstObjectByType<MissionUIController>(FindObjectsInactive.Include);
        DogHeartInteraction heart = canvas.GetComponent<DogHeartInteraction>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_dogPoop").objectReferenceValue = dogPoop;
        so.FindProperty("_dogPoopButton").objectReferenceValue = dogPoop.GetComponent<Button>();
        so.FindProperty("_dog").objectReferenceValue = dog;
        so.FindProperty("_dogWorldAnchor").objectReferenceValue = dogRoot;
        so.FindProperty("_poopTemplate").objectReferenceValue = poop;
        so.FindProperty("_boneRewardSource").objectReferenceValue = heart;
        so.FindProperty("_missionController").objectReferenceValue = mission;
        so.ApplyModifiedPropertiesWithoutUndo();
        controller.enabled = true;
        controller.Configure(dogPoop, dogPoop.GetComponent<Button>(), dog, poop, heart, mission, dogRoot);

        AssignMission(heart, mission);
        AssignMission(canvas.GetComponent<DogFoodInteraction>(), mission);
        DogFoodInteraction food = canvas.GetComponent<DogFoodInteraction>();
        if (food != null)
        {
            SerializedObject foodObject = new SerializedObject(food);
            foodObject.FindProperty("_dog").objectReferenceValue = dog;
            foodObject.ApplyModifiedPropertiesWithoutUndo();
            food.SetDog(dog);
            EditorUtility.SetDirty(food);
        }
        Animator uiAnimator = dogPoop.GetComponent<Animator>();
        if (uiAnimator != null) uiAnimator.enabled = false;

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            string savePath = activeScene.path.StartsWith("Temp/", StringComparison.OrdinalIgnoreCase)
                ? TargetScenePath
                : activeScene.path;
            EditorSceneManager.SaveScene(activeScene, savePath);
        }
        Debug.Log("[DogPoop] Dog, Poop template, UI, rewards, and missions were connected.", controller);
        EditorApplication.update -= RetryUntilWired;
    }

    static void AssignMission(UnityEngine.Object target, MissionUIController mission)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty("_missionController");
        if (property == null) return;
        property.objectReferenceValue = mission;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    static Transform FindExact(string name)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    static Transform FindWorldExact(string name)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!(t is RectTransform) && t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }
}
#endif

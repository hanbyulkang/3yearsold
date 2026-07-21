using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for wiring <see cref="DogWanderAI"/> onto a Dog Package NPC:
/// adds the movement components and creates the fenced wander area.
/// </summary>
public static class DogWanderSetup
{
    [MenuItem("Tools/Dog Package/Setup Wander AI on Selection")]
    static void SetupOnSelection()
    {
        var targets = Selection.gameObjects;
        if (targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Dog Wander AI",
                "Select the dog GameObject(s) in the Hierarchy first.", "OK");
            return;
        }

        int done = 0;
        foreach (var go in targets)
        {
            if (go.GetComponent<Animator>() == null)
            {
                Debug.LogWarning($"[DogWanderAI] '{go.name}' has no Animator — skipped.", go);
                continue;
            }

            var body = go.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(go);
                body.mass = 20f;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (go.GetComponent<DogWanderAI>() == null)
                Undo.AddComponent<DogWanderAI>(go);

            EditorUtility.SetDirty(go);
            done++;
            Debug.Log($"[DogWanderAI] Set up on '{go.name}'. Assign a Wander Area to fence it in.", go);
        }

        if (done > 0)
            Selection.activeGameObject = targets[0];
    }

    [MenuItem("Tools/Dog Package/Create Wander Area for Selection")]
    static void CreateWanderArea()
    {
        var dogs = new System.Collections.Generic.List<DogWanderAI>();
        foreach (var go in Selection.gameObjects)
        {
            var ai = go.GetComponent<DogWanderAI>();
            if (ai != null)
                dogs.Add(ai);
        }

        var area = new GameObject("Dog Wander Area");
        Undo.RegisterCreatedObjectUndo(area, "Create Dog Wander Area");

        // Centre it on the first selected dog so it lands next to the pen, not at the origin.
        area.transform.position = dogs.Count > 0
            ? dogs[0].transform.position
            : (Selection.activeTransform != null ? Selection.activeTransform.position : Vector3.zero);

        var box = area.AddComponent<BoxCollider>();
        box.isTrigger = true;   // the dog's raycasts ignore triggers, so it never blocks movement
        box.size = new Vector3(10f, 2f, 10f);
        box.center = new Vector3(0f, 1f, 0f);

        foreach (var dog in dogs)
        {
            var so = new SerializedObject(dog);
            so.FindProperty("_wanderArea").objectReferenceValue = box;
            so.ApplyModifiedProperties();
            Debug.Log($"[DogWanderAI] Assigned wander area to '{dog.name}'.", dog);
        }

        Selection.activeGameObject = area;
        Debug.Log("[DogWanderAI] Scale/position the 'Dog Wander Area' box to match the inside of the fence.", area);
    }

}

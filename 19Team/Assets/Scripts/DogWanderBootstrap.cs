using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically wires a hierarchy object named Dog to the fenced wandering AI.
/// This also works when the hierarchy was created in the editor but has not yet
/// been saved into the scene asset.
/// </summary>
public static class DogWanderBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SetupSceneDog()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.name.Equals("Suntail Village", StringComparison.OrdinalIgnoreCase))
            return;

        Transform fence = FindNamedTransform("Fence");
        BoxCollider area = fence != null ? BuildAreaFromFence(fence) : null;

        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Animator animator in animators)
        {
            if (!IsDog(animator.transform))
                continue;

            GameObject dog = animator.gameObject;

            // The package's keyboard controller writes to the same Animator and
            // Rigidbody, so only one controller may be active at a time.
            DogCharacterController playerController = dog.GetComponent<DogCharacterController>();
            if (playerController != null)
                playerController.enabled = false;

            DogWanderAI ai = dog.GetComponent<DogWanderAI>();
            if (ai == null)
                ai = dog.AddComponent<DogWanderAI>();

            if (area != null)
                ai.SetWanderArea(area);
        }

    }

    private static bool IsDog(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            if (current.name.Equals("Dog", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Dog Package prefab roots are named e.g. "NPC Husky". Restrict this
        // fallback to objects using the supplied dog animator controller.
        RuntimeAnimatorController controller = candidate.GetComponent<Animator>().runtimeAnimatorController;
        return controller != null && controller.name.IndexOf("DogAnimator", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform FindNamedTransform(string wantedName)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindInChildren(root.transform, wantedName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindInChildren(Transform current, string wantedName)
    {
        if (current.name.Equals(wantedName, StringComparison.OrdinalIgnoreCase))
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindInChildren(current.GetChild(i), wantedName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static BoxCollider BuildAreaFromFence(Transform fence)
    {
        if (!TryGetFenceBounds(fence, out Bounds bounds))
            return null;

        GameObject areaObject = new GameObject("Dog Wander Area (Runtime)");
        areaObject.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        BoxCollider area = areaObject.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = Vector3.zero;
        area.size = new Vector3(
            Mathf.Max(1f, bounds.size.x),
            Mathf.Max(2f, bounds.size.y),
            Mathf.Max(1f, bounds.size.z));
        return area;
    }

    private static bool TryGetFenceBounds(Transform fence, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(fence.position, Vector3.zero);

        foreach (Renderer renderer in fence.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        if (hasBounds)
            return true;

        foreach (Collider collider in fence.GetComponentsInChildren<Collider>())
        {
            if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return hasBounds;
    }
}

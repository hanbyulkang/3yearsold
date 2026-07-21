#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayerLevelAutoWire
{
    private const string SceneName = "Suntail Village";
    private const string DataPath = "Assets/Data/PlayerLevelDataSet.asset";

    static PlayerLevelAutoWire()
    {
        EditorApplication.delayCall += Wire;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += Wire;
    }

    [MenuItem("Tools/Suntail Village/Wire Player Level")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Equals(SceneName, StringComparison.OrdinalIgnoreCase)) return;
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;

        Transform top = FindDirect(canvas.transform, "TOP");
        Transform profile = FindDirect(top, "Profile");
        TMP_Text levelText = profile != null ? profile.GetComponentInChildren<TMP_Text>(true) : null;
        Slider slider = profile != null ? profile.GetComponentInChildren<Slider>(true) : null;
        if (levelText == null || slider == null) return;

        PlayerLevelDataSet data = AssetDatabase.LoadAssetAtPath<PlayerLevelDataSet>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<PlayerLevelDataSet>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        PlayerLevelUIController controller = canvas.GetComponent<PlayerLevelUIController>();
        if (controller == null) controller = Undo.AddComponent<PlayerLevelUIController>(canvas.gameObject);
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_dataSet").objectReferenceValue = data;
        so.FindProperty("_levelText").objectReferenceValue = levelText;
        so.FindProperty("_experienceSlider").objectReferenceValue = slider;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Player Level] Lv text and experience slider were wired.", controller);
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

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
public static class MissionAutoWire
{
    static MissionAutoWire() { EditorApplication.delayCall += Wire; }
    [MenuItem("Tools/Mission/Wire Mission UI")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform mission = FindExact("Mission");
        if (mission == null) return;
        MissionUIController controller = mission.GetComponent<MissionUIController>();
        if (controller == null) controller = Undo.AddComponent<MissionUIController>(mission.gameObject);

        const string assetPath = "Assets/Data/MissionDataSet.asset";
        MissionDataSet data = AssetDatabase.LoadAssetAtPath<MissionDataSet>(assetPath);
        if (data == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            data = ScriptableObject.CreateInstance<MissionDataSet>(); data.EnsureCount(5);
            AssetDatabase.CreateAsset(data, assetPath); AssetDatabase.SaveAssets();
        }

        var groups = new List<Transform>();
        foreach (Transform go in mission.GetComponentsInChildren<Transform>(true))
        {
            if (!go.name.Equals("Go", StringComparison.OrdinalIgnoreCase)) continue;
            Transform group = go.parent;
            while (group != mission && (CountNamed(group, "Go") != 1 || CountNamed(group, "Confirm") != 1 || CountNamed(group, "Confirm (1)") != 1))
                group = group.parent;
            if (group != null && group != mission && !groups.Contains(group)) groups.Add(group);
        }
        groups.Sort((a, b) => b.position.y.CompareTo(a.position.y));

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("_dataSet").objectReferenceValue = data;
        SerializedProperty items = so.FindProperty("_missions"); items.arraySize = groups.Count;
        for (int i = 0; i < groups.Count; i++)
        {
            Transform group = groups[i]; SerializedProperty item = items.GetArrayElementAtIndex(i);
            string id = "Mission" + (i + 1).ToString("00");
            item.FindPropertyRelative("id").stringValue = id;
            TMP_Text title = FindChild(group, "Title")?.GetComponent<TMP_Text>();
            DetectMission(title != null ? title.text : string.Empty, out MissionAction action, out int requiredCount);
            data.ConfigureMission(i, id, action, requiredCount);
            Transform incomplete = FindChild(group, "Confirm (1)");
            Transform complete = FindChild(group, "Confirm");
            Transform deco = FindChild(group, "ConfirmDeco");
            Transform go = FindChild(group, "Go");
            item.FindPropertyRelative("confirmIncomplete").objectReferenceValue = incomplete != null ? incomplete.gameObject : null;
            item.FindPropertyRelative("confirmComplete").objectReferenceValue = complete != null ? complete.gameObject : null;
            item.FindPropertyRelative("confirmDeco").objectReferenceValue = deco != null ? deco.gameObject : null;
            item.FindPropertyRelative("go").objectReferenceValue = go != null ? go.gameObject : null;
            Button button = go != null ? go.GetComponent<Button>() : null;
            if (go != null && button == null) button = Undo.AddComponent<Button>(go.gameObject);
            if (button != null) button.targetGraphic = go.GetComponent<Graphic>();
            item.FindPropertyRelative("goButton").objectReferenceValue = button;
            Image quest = group.GetComponent<Image>();
            item.FindPropertyRelative("questImage").objectReferenceValue = quest;
            if (quest != null) item.FindPropertyRelative("incompleteQuestSprite").objectReferenceValue = quest.sprite;
        }
        Transform count = FindChild(mission, "Count");
        so.FindProperty("_countText").objectReferenceValue = count != null ? count.GetComponentInChildren<TMP_Text>(true) : null;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[Mission] Mission UI and MissionDataSet were wired and saved.", controller);
    }

    static int CountNamed(Transform root, string name) { int n = 0; foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) n++; return n; }
    static Transform FindChild(Transform root, string name) { foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
    static Transform FindExact(string name) { foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == name) return t; return null; }
    static void DetectMission(string title, out MissionAction action, out int requiredCount)
    {
        requiredCount = 1;
        if (title.Contains("밥")) action = MissionAction.Feed;
        else if (title.Contains("산책")) { action = MissionAction.Walk; requiredCount = 2; }
        else if (title.Contains("똥")) action = MissionAction.CleanPoop;
        else if (title.Contains("쓰다듬") || title.Contains("놀아주기")) action = MissionAction.PetOrPlay;
        else if (title.Contains("미니게임")) action = MissionAction.MiniGame;
        else action = MissionAction.None;
    }
}
#endif

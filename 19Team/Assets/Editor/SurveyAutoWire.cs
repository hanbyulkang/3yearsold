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
public static class SurveyAutoWire
{
    static SurveyAutoWire() { EditorApplication.delayCall += Wire; }

    [MenuItem("Tools/Survey/Wire Survey Scene")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Equals("Survey", StringComparison.OrdinalIgnoreCase)) return;
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;

        SurveyFlowController controller = canvas.GetComponent<SurveyFlowController>();
        if (controller == null) controller = Undo.AddComponent<SurveyFlowController>(canvas.gameObject);
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty pages = so.FindProperty("_pages");
        pages.arraySize = 6;
        string[] names = { "Start", "Question01", "Question02", "Question03", "Question04", "Question05" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform root = FindExact(canvas.transform, names[i]);
            SerializedProperty page = pages.GetArrayElementAtIndex(i);
            page.FindPropertyRelative("id").stringValue = names[i];
            page.FindPropertyRelative("root").objectReferenceValue = root != null ? root.gameObject : null;
            page.FindPropertyRelative("selectedAnswer").intValue = -1;
            page.FindPropertyRelative("requireAnswer").boolValue = i > 0;
            if (root == null) continue;
            page.FindPropertyRelative("back").objectReferenceValue = FindButton(root, "Back");
            page.FindPropertyRelative("next").objectReferenceValue = FindButton(root, "Next");

            var answers = new List<Button>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals("Answer", StringComparison.OrdinalIgnoreCase)) answers.Add(EnsureButton(child));
            SerializedProperty answerProp = page.FindPropertyRelative("answers");
            answerProp.arraySize = answers.Count;
            for (int a = 0; a < answers.Count; a++) answerProp.GetArrayElementAtIndex(a).objectReferenceValue = answers[a];

            TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            SerializedProperty inputProp = page.FindPropertyRelative("inputFields");
            inputProp.arraySize = inputs.Length;
            for (int f = 0; f < inputs.Length; f++) inputProp.GetArrayElementAtIndex(f).objectReferenceValue = inputs[f];
            root.gameObject.SetActive(i == 0);
        }

        so.FindProperty("_progressSlider").objectReferenceValue = canvas.GetComponentInChildren<Slider>(true);
        Transform sliderRoot = FindExact(canvas.transform, "Slider");
        Transform fill = sliderRoot != null ? FindExact(sliderRoot, "Fill") : null;
        so.FindProperty("_progressFill").objectReferenceValue = fill != null ? fill.GetComponent<Image>() : null;
        TMP_Text progress = null;
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (text.text != null && System.Text.RegularExpressions.Regex.IsMatch(text.text.Trim(), @"^\d\s*/\s*5$")) { progress = text; break; }
        so.FindProperty("_progressText").objectReferenceValue = progress;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Survey] All pages, navigation, answers, inputs and progress were wired and saved.", controller);
    }

    static Button FindButton(Transform root, string name)
    {
        Transform found = FindExact(root, name);
        return found != null ? EnsureButton(found) : null;
    }

    static Button EnsureButton(Transform target)
    {
        Button button = target.GetComponent<Button>();
        if (button == null) button = Undo.AddComponent<Button>(target.gameObject);
        button.targetGraphic = target.GetComponent<Graphic>();
        return button;
    }

    static Transform FindExact(Transform root, string name)
    {
        if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindExact(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SurveyOpenAIAnalysisAutoWire
{
    static SurveyOpenAIAnalysisAutoWire()
    {
        EditorApplication.delayCall += Wire;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += Wire;
    }

    [MenuItem("Tools/Survey/Wire OpenAI Analysis")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Equals("Survey", StringComparison.OrdinalIgnoreCase)) return;

        SurveyFlowController flow = UnityEngine.Object.FindFirstObjectByType<SurveyFlowController>(FindObjectsInactive.Include);
        if (flow == null) return;

        SurveyOpenAIAnalysis analysis = flow.GetComponent<SurveyOpenAIAnalysis>();
        if (analysis == null) analysis = Undo.AddComponent<SurveyOpenAIAnalysis>(flow.gameObject);

        GameObject first = FindSceneObject(scene, "First");
        GameObject find = FindSceneObject(scene, "Find");
        GameObject dog = FindSceneObject(scene, "Dog");
        SerializedObject so = new SerializedObject(analysis);
        so.FindProperty("_surveyFlow").objectReferenceValue = flow;
        so.FindProperty("_first").objectReferenceValue = first;
        so.FindProperty("_find").objectReferenceValue = find;
        so.FindProperty("_dog").objectReferenceValue = dog;
        // 모델·API 키 필드는 제거됐다. LLM 호출이 Edge Function으로 넘어가면서
        // 모델 선택은 서버 환경변수(LLM_MODEL)가 정한다 — 클라가 정하지 않는다.
        so.ApplyModifiedPropertiesWithoutUndo();

        if (first != null) first.SetActive(true);
        if (find != null) find.SetActive(false);
        if (dog != null) dog.SetActive(false);
        EditorUtility.SetDirty(analysis);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Survey OpenAI] First, Find, Dog and the Question05 completion event are wired.", analysis);
    }

    private static GameObject FindSceneObject(Scene scene, string exactName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindExact(root.transform, exactName);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindExact(Transform root, string exactName)
    {
        if (root.name.Equals(exactName, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindExact(root.GetChild(i), exactName);
            if (found != null) return found;
        }
        return null;
    }
}
#endif

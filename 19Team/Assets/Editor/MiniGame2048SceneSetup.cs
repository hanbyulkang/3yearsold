using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniGame2048SceneSetup
{
    private const string ScenePath = "Assets/Scenes/minigame02.unity";

    [MenuItem("Tools/19Team/Build 2048 Mini Game")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find("MiniGame2048");
        if (existing != null) Object.DestroyImmediate(existing);
        GameObject root = new GameObject("MiniGame2048");
        MiniGame2048 game = root.AddComponent<MiniGame2048>();
        game.pawSprite = LoadSprite("Assets/UI/제목 없는 디자인-4.png");
        game.boneSprite = LoadSprite("Assets/UI/22.png");
        game.jerkySprite = LoadSprite("Assets/UI/24.png");
        game.dogSprite = LoadSprite("Assets/UI/5.png");
        if (game.pawSprite == null || game.boneSprite == null || game.jerkySprite == null || game.dogSprite == null)
            throw new System.Exception("2048 UI sprite assets could not be loaded from Assets/UI");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[19TEAM] Built 2048 mini-game in minigame02.unity");
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void EnterPlayMode()
    {
        EditorApplication.isPlaying = true;
    }

    public static void ValidateRuntime()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MiniGame2048 game = Object.FindFirstObjectByType<MiniGame2048>();
        if (game == null) throw new System.Exception("MiniGame2048 component is missing from minigame02");
        MethodInfo start = typeof(MiniGame2048).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        if (start == null) throw new System.Exception("MiniGame2048.Start was not found");
        start.Invoke(game, null);
        Transform canvas = game.transform.Find("2048 Canvas");
        Transform board = canvas == null ? null : canvas.Find("2048 Board");
        if (canvas == null || board == null) throw new System.Exception("Runtime UI did not build Canvas and Board");
        if (board.childCount != 17) throw new System.Exception("Expected 16 board slots plus board surface, got " + board.childCount);
        MethodInfo move = typeof(MiniGame2048).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
        if (move == null) throw new System.Exception("MiniGame2048.Move was not found");
        move.Invoke(game, new object[] { Vector2Int.left });
        move.Invoke(game, new object[] { Vector2Int.up });
        move.Invoke(game, new object[] { Vector2Int.right });
        move.Invoke(game, new object[] { Vector2Int.down });
        Debug.Log("[19TEAM] Runtime validation passed: Canvas + Board + 16 tile slots + four-direction moves");
        Object.DestroyImmediate(game.gameObject);
    }
}

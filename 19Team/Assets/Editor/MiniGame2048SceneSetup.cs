using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniGame2048SceneSetup
{
    private const string ScenePath = "Assets/Scenes/minigame02.unity";
    private const string ArtDir = "Assets/UI/MiniGame2";
    private const string KoreanFontGuid = "f5a76bc7fc64647309ea7f723bc25dc7"; // NotoSansKR SDF (MG1과 동일)

    // 타일 순서: lv1 x1/x2/x4, lv2 x1/x2/x4, lv3 x1/x2/x4, lv4(강아지) — MiniGame2048.tileSprites와 같은 순서
    private static readonly string[] TileFiles =
    {
        "mg2-tile-lv1-x1", "mg2-tile-lv1-x2", "mg2-tile-lv1-x4",
        "mg2-tile-lv2-x1", "mg2-tile-lv2-x2", "mg2-tile-lv2-x4",
        "mg2-tile-lv3-x1", "mg2-tile-lv3-x2", "mg2-tile-lv3-x4",
        "mg2-tile-lv4-dog",
    };

    [MenuItem("Tools/19Team/Build 2048 Mini Game")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find("MiniGame2048");
        if (existing != null) Object.DestroyImmediate(existing);
        GameObject root = new GameObject("MiniGame2048");
        MiniGame2048 game = root.AddComponent<MiniGame2048>();

        // 필드가 [SerializeField] private 이라 SerializedObject로 넣는다
        var so = new SerializedObject(game);
        so.FindProperty("koreanFont").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(KoreanFontGuid));
        SetSprite(so, "boardBg", "mg2-board-bg");
        var tiles = so.FindProperty("tileSprites");
        tiles.arraySize = TileFiles.Length;
        for (int i = 0; i < TileFiles.Length; i++)
            tiles.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(TileFiles[i]);
        SetSprite(so, "headerBar", "mg2-header-bar");
        SetSprite(so, "chipPill", "mg2-chip-pill");
        SetSprite(so, "closeBtn", "mg2-close-btn");
        SetSprite(so, "medalSprite", "mg2-medal");
        SetSprite(so, "btnGold", "mg2-btn-gold");
        SetSprite(so, "btnDark", "mg2-btn-dark");
        SetSprite(so, "rewardCard", "mg2-reward-card");
        SetSprite(so, "coachCard", "mg2-coach-card");
        SetSprite(so, "iconBone", "mg2-icon-bone");
        SetSprite(so, "iconDogface", "mg2-icon-dogface");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[19TEAM] Built 2048 mini-game in minigame02.unity");
    }

    private static void SetSprite(SerializedObject so, string field, string file)
    {
        var prop = so.FindProperty(field);
        if (prop == null) throw new System.Exception($"MiniGame2048.{field} 필드가 없습니다");
        prop.objectReferenceValue = LoadSprite(file);
    }

    private static Sprite LoadSprite(string file)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{file}.png");
        if (sprite == null) throw new System.Exception($"{ArtDir}/{file}.png 스프라이트를 찾을 수 없습니다");
        return sprite;
    }

    public static void EnterPlayMode()
    {
        EditorApplication.isPlaying = true;
    }

    public static void ValidateRuntime()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MiniGame2048 game = Object.FindFirstObjectByType<MiniGame2048>();
        if (game == null) throw new System.Exception("MiniGame2048 component is missing from minigame02");
        MethodInfo build = typeof(MiniGame2048).GetMethod("BuildUI", BindingFlags.Instance | BindingFlags.NonPublic);
        if (build == null) throw new System.Exception("MiniGame2048.BuildUI was not found");
        build.Invoke(game, null);
        Transform canvas = game.transform.Find("MG2Canvas");
        Transform board = canvas == null ? null : canvas.Find("PlayPanel/Board");
        if (canvas == null || board == null) throw new System.Exception("Runtime UI did not build MG2Canvas and Board");
        if (board.childCount != 16) throw new System.Exception("Expected 16 tile slots, got " + board.childCount);
        Debug.Log("[19TEAM] Runtime validation passed: MG2Canvas + Board + 16 tiles");
        Object.DestroyImmediate(game.gameObject);
    }
}

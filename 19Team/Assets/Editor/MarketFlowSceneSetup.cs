using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MarketFlowSceneSetup
{
    private const string ScenePath = "Assets/Scenes/marketFlow.unity";
    private const string FontPath = "Assets/Font/NotoSansKR-Black.ttf";
    private const string SvgRoot = "Assets/MarketFlow/svg-shop/";
    private const string GeneratedRoot = "Assets/MarketFlow/svg-shop/Generated/";
    private const string PreviewPath = "/tmp/marketFlow-preview.png";
    private static int previewFrames;

    [MenuItem("Tools/19Team/Build Market Flow")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject existing = GameObject.Find("MarketFlow");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(26, 20, 15, 255);
        camera.transform.position = new Vector3(0f, 0f, -10f);

        GameObject root = new GameObject("MarketFlow");
        MarketFlow flow = root.AddComponent<MarketFlow>();
        flow.marketFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (flow.marketFont == null)
            throw new InvalidOperationException("MarketFlow font could not be loaded from " + FontPath);
        flow.productCardSprite = LoadSprite("product-card.png");
        flow.productCardDonateSprite = LoadSprite("product-card-donate.png");
        flow.previewFrameSprite = LoadSprite("preview-frame.png");
        flow.gaugeCardSprite = LoadSprite("gauge-card.png");
        flow.donateBannerSprite = LoadSprite("donate-banner.png");
        flow.qrSlotSprite = LoadSprite("qr-slot.png");
        flow.statusPillGoldSprite = LoadSprite("status-pill-gold.png");
        flow.statusPillWaitSprite = LoadSprite("status-pill-wait.png");
        flow.stepCardActiveSprite = LoadSprite("step-card-active.png");
        flow.stepCardDoneSprite = LoadSprite("step-card-done.png");
        flow.stepCardWaitingSprite = LoadSprite("step-card-waiting.png");
        flow.boneGoldSprite = LoadSprite("icon-bone-gold.png");
        flow.jerkySprite = LoadSprite("icon-jerky.png");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[19TEAM] Built one-scene F-commerce flow at " + ScenePath);
    }

    public static void ValidateRuntime()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MarketFlow flow = UnityEngine.Object.FindFirstObjectByType<MarketFlow>();
        if (flow == null) throw new InvalidOperationException("MarketFlow component is missing from marketFlow.unity");
        if (flow.marketFont == null) throw new InvalidOperationException("MarketFlow font reference is missing");

        flow.ShowShop();
        AssertScreen(flow, "Shop");
        InvokeButton(flow, "노란 우비");
        AssertScreen(flow, "Skin");
        InvokeButton(flow, "← Button");
        AssertScreen(flow, "Shop");
        InvokeButton(flow, "겨울 패딩 세트");
        AssertScreen(flow, "Set");
        InvokeButton(flow, "자사몰에서 구매 (새 탭) Button");
        AssertScreen(flow, "Checkout");

        Debug.Log("[19TEAM] MarketFlow runtime validation passed: one scene + F-01/F-02/F-03/F-04 transitions");
        EditorSceneManager.CloseScene(scene, true);
    }

    public static void CapturePreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.playModeStateChanged += BeginPreviewCapture;
        EditorApplication.isPlaying = true;
    }

    public static void EnterPlayMode()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void BeginPreviewCapture(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= BeginPreviewCapture;
        previewFrames = 5;
        EditorApplication.update += CapturePreviewFrame;
    }

    private static void CapturePreviewFrame()
    {
        if (previewFrames-- > 0) return;
        EditorApplication.update -= CapturePreviewFrame;
        ScreenCapture.CaptureScreenshot(PreviewPath);
        Debug.Log("[19TEAM] Captured MarketFlow preview at " + PreviewPath);
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += ExitAfterPreview;
    }

    private static void ExitAfterPreview()
    {
        EditorApplication.Exit(0);
    }

    private static Sprite LoadSprite(string filename)
    {
        string path = GeneratedRoot + filename;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        throw new InvalidOperationException("Generated SVG-derived sprite could not be imported: " + path + " (source: " + SvgRoot + filename.Replace(".png", ".svg") + ")");
    }

    private static void InvokeButton(MarketFlow flow, string objectName)
    {
        Button[] buttons = flow.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (!string.Equals(button.gameObject.name, objectName, StringComparison.Ordinal)) continue;
            button.onClick.Invoke();
            return;
        }

        throw new InvalidOperationException("Button was not found in MarketFlow runtime UI: " + objectName);
    }

    private static void AssertScreen(MarketFlow flow, string expected)
    {
        string actual = flow.CurrentScreenName();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("Expected MarketFlow screen " + expected + ", got " + actual);
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Converts the Dog Package materials from the Built-in Standard shader to
/// URP/Simple Lit, carrying over albedo texture, tint, smoothness and blend mode.
/// Safe to re-run: materials already on Simple Lit are read through their
/// URP property names instead of the Standard ones.
/// </summary>
public static class DogPackageShaderConverter
{
    const string MaterialFolder = "Assets/Dog Package/Models/Materials";

    [MenuItem("Tools/Dog Package/Convert Materials to URP Simple Lit")]
    static void Convert()
    {
        var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
        {
            Debug.LogError("URP/Simple Lit shader not found. Is the Universal RP package installed?");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
        int converted = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                continue;

            // Read the source values before the shader swap discards unknown properties.
            var albedo = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null
                ? mat.GetTexture("_BaseMap")
                : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
            var scale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            var offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
            var tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                     : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
            var normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            var bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            var emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            var emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            var cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            var smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness")
                           : (mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f);

            // Standard's _Mode: 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent.
            // A material already on a URP shader reports the same intent via _Surface/_AlphaClip.
            bool transparent, alphaClip;
            if (mat.HasProperty("_Surface"))
            {
                transparent = mat.GetFloat("_Surface") > 0.5f;
                alphaClip = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f;
            }
            else
            {
                var mode = mat.HasProperty("_Mode") ? (int)mat.GetFloat("_Mode") : 0;
                transparent = mode >= 2;
                alphaClip = mode == 1;
            }

            Undo.RecordObject(mat, "Convert to URP Simple Lit");
            mat.shader = shader;

            mat.SetTexture("_BaseMap", albedo);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.SetTexture("_EmissionMap", emissionMap);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetFloat("_Cutoff", cutoff);

            // Simple Lit takes specular from _SpecColor, with smoothness in its alpha
            // (because _SmoothnessSource is SpecularAlpha).
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_SmoothnessSource", 0f);
            mat.SetFloat("_SpecularHighlights", 0f); // 0 = SpecularTextureAndColor
            mat.SetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f, smoothness));
            CoreUtils.SetKeyword(mat, "_SPECULAR_COLOR", true);
            CoreUtils.SetKeyword(mat, "_SPECGLOSSMAP", false);
            CoreUtils.SetKeyword(mat, "_GLOSSINESS_FROM_BASE_ALPHA", false);

            CoreUtils.SetKeyword(mat, "_NORMALMAP", normal != null);
            CoreUtils.SetKeyword(mat, "_EMISSION", emissionColor.maxColorComponent > 0f);

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);               // Alpha
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_AlphaToMask", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_BlendModePreserveSpecular", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
                CoreUtils.SetKeyword(mat, "_SURFACE_TYPE_TRANSPARENT", true);
                CoreUtils.SetKeyword(mat, "_ALPHAPREMULTIPLY_ON", true);
                CoreUtils.SetKeyword(mat, "_ALPHATEST_ON", false);
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
                mat.SetFloat("_AlphaToMask", alphaClip ? 1f : 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : -1;
                mat.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                CoreUtils.SetKeyword(mat, "_SURFACE_TYPE_TRANSPARENT", false);
                CoreUtils.SetKeyword(mat, "_ALPHAPREMULTIPLY_ON", false);
                CoreUtils.SetKeyword(mat, "_ALPHATEST_ON", alphaClip);
            }

            EditorUtility.SetDirty(mat);
            converted++;
            Debug.Log($"[DogPackage] {mat.name}: Simple Lit, albedo={(albedo ? albedo.name : "none")}, " +
                      $"smoothness={smoothness}, {(transparent ? "Transparent" : alphaClip ? "Cutout" : "Opaque")}", mat);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DogPackage] Converted {converted} material(s) to URP/Simple Lit.");
    }
}

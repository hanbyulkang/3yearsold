#if UNITY_EDITOR
using UnityEditor;

public class MissionPngImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        bool isMissionPng = assetPath.StartsWith("Assets/UI/Mission/") && assetPath.EndsWith(".png");
        bool isBowlPng = assetPath == "Assets/UI/block_bowl.png";
        bool isPoopPng = assetPath == "Assets/Poop/icon_poop.png";
        bool isInventoryDecoratePng = assetPath.StartsWith("Assets/UI/Inventory/svg-decorate/") && assetPath.EndsWith(".png");
        bool isRankingPng = assetPath.StartsWith("Assets/UI/Ranking/") && assetPath.EndsWith(".png");
        bool isGamePng = assetPath.StartsWith("Assets/UI/Game/") && assetPath.EndsWith(".png");
        if (!isMissionPng && !isBowlPng && !isPoopPng && !isInventoryDecoratePng && !isRankingPng && !isGamePng)
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
#endif

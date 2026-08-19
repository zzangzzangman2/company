using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class PlayerNaturalWalkTextureImporter : AssetPostprocessor
    {
        private const string Root =
            "Assets/Resources/FamilyCompany/PlayerNaturalWalkV1/Frames/";
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit =
                assetPath.Contains("_east_", System.StringComparison.Ordinal) ||
                assetPath.Contains("_west_", System.StringComparison.Ordinal)
                    ? 314f
                    : 324f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
        }
    }
}

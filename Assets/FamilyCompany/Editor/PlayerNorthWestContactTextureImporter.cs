using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class PlayerNorthWestContactTextureImporter : AssetPostprocessor
    {
        private const string NorthRoot =
            "Assets/Resources/FamilyCompany/PlayerNorthContactV1/Frames/";
        private const string WestRoot =
            "Assets/Resources/FamilyCompany/PlayerWestContactV1/Frames/";

        private void OnPreprocessTexture()
        {
            bool supported = assetPath.StartsWith(NorthRoot, System.StringComparison.Ordinal) ||
                             assetPath.StartsWith(WestRoot, System.StringComparison.Ordinal);
            if (!supported || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = assetPath.StartsWith(NorthRoot, System.StringComparison.Ordinal)
                ? 324f
                : 314f;
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

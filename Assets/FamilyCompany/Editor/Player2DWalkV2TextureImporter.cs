using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class Player2DWalkV2TextureImporter : AssetPostprocessor
    {
        private const string FrameRoot =
            "Assets/Resources/FamilyCompany/Player2DWalkV2/Frames/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FrameRoot, System.StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 180f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
        }
    }
}

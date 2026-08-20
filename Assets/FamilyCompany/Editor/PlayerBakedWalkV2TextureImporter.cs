using System;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class PlayerBakedWalkV2TextureImporter : AssetPostprocessor
    {
        public const string FrameRoot =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkV2/Frames/";
        public const float PixelsPerUnit = 324f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FrameRoot, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
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

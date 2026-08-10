using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class OfficeVisualV2AssetImporter : AssetPostprocessor
    {
        public const string AssetFolder = "Assets/Art/Office/Resources/OfficeVisualV2";

        [MenuItem("Family Company/Office Visual V2/Configure Imported Assets")]
        public static void ConfigureExistingAssets()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AssetFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsOfficeVisualPath(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                Configure(importer, path);
                importer.SaveAndReimport();
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsOfficeVisualPath(assetPath)) return;
            Configure((TextureImporter)assetImporter, assetPath);
        }

        private static bool IsOfficeVisualPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(AssetFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
        }

        private static void Configure(TextureImporter importer, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            var isGuide = name.IndexOf("guide", StringComparison.OrdinalIgnoreCase) >= 0;
            importer.textureType = isGuide ? TextureImporterType.Default : TextureImporterType.Sprite;
            if (!isGuide)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
            }

            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
        }
    }
}

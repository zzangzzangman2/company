using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public sealed class MainNavigationV2AssetImporter : AssetPostprocessor
    {
        public const string AssetRoot = "Assets/Art/UI/Resources/MainNavigationV2/";
        public const float PixelsPerUnit = 100f;

        private static readonly IReadOnlyDictionary<string, Vector4> Borders =
            new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                { "top_hud_backplate_v2.png", Border(80f, 52f) },
                { "company_badge_v2.png", new Vector4(250f, 80f, 120f, 80f) },
                { "time_badge_v2.png", new Vector4(170f, 82f, 116f, 82f) },
                { "speed_normal_v2.png", Border(70f, 44f) },
                { "speed_hover_v2.png", Border(70f, 46f) },
                { "speed_selected_v2.png", Border(70f, 36f) },
                { "speed_pressed_v2.png", Border(70f, 46f) },
                { "bottom_dock_v2.png", Border(120f, 82f) },
                { "tab_normal_v2.png", Border(104f, 70f) },
                { "tab_hover_v2.png", Border(104f, 92f) },
                { "tab_selected_v2.png", Border(104f, 70f) },
                { "tab_pressed_v2.png", Border(104f, 66f) },
                { "modal_frame_v2.png", Border(132f, 132f) },
                { "modal_header_v2.png", Border(150f, 92f) },
                { "card_normal_v2.png", Border(142f, 112f) },
                { "card_hover_v2.png", Border(142f, 112f) },
                { "card_disabled_v2.png", Border(142f, 112f) },
                { "card_featured_v2.png", Border(188f, 132f) },
                { "card_featured_hover_v2.png", Border(188f, 132f) },
                { "close_normal_v2.png", Border(110f, 110f) },
                { "close_hover_v2.png", Border(110f, 110f) },
                { "close_pressed_v2.png", Border(110f, 110f) },
                { "notification_badge_v2.png", Border(82f, 54f) },
                { "coming_soon_ribbon_v2.png", Border(102f, 54f) }
            };

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetRoot, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Contains("/Reference/", StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = assetPath.Contains("/Icons/", StringComparison.Ordinal) ? 512 : 2048;

            var filename = System.IO.Path.GetFileName(assetPath);
            importer.spriteBorder = Borders.TryGetValue(filename, out var border) ? border : Vector4.zero;
        }

        public static bool TryGetExpectedBorder(string assetPath, out Vector4 border)
        {
            return Borders.TryGetValue(System.IO.Path.GetFileName(assetPath), out border);
        }

        private static Vector4 Border(float horizontal, float vertical)
        {
            return new Vector4(horizontal, vertical, horizontal, vertical);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.UIRemaster
{
    public sealed class UiRemasterV3AssetPostprocessor : AssetPostprocessor
    {
        private const string AssetRoot = "Assets/Art/UI/Resources/UiRemasterV3/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetRoot, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            var background = assetPath.EndsWith("title_hero_background_v3.png", System.StringComparison.Ordinal) ||
                             assetPath.EndsWith("loading_background_v3.png", System.StringComparison.Ordinal);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = background ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = !background;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = background || assetPath.Contains("/Common/") ? 4096 :
                assetPath.Contains("/Icons/") || assetPath.Contains("loading_work_icon_v") ? 512 : 2048;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = ResolveSpriteBorder(assetPath);
        }

        private static Vector4 ResolveSpriteBorder(string path)
        {
            if (path.EndsWith("card_compact_normal_v5.png", System.StringComparison.Ordinal))
                return new Vector4(120f, 120f, 120f, 120f);
            if (path.Contains("/Common/card_") && path.EndsWith("_v4.png", System.StringComparison.Ordinal))
                return new Vector4(140f, 140f, 140f, 140f);
            if (path.Contains("/Common/card_")) return new Vector4(230f, 170f, 230f, 170f);
            if (path.Contains("title_button_")) return new Vector4(230f, 170f, 230f, 170f);
            if (path.Contains("save_slot_")) return new Vector4(250f, 190f, 250f, 190f);
            if (path.EndsWith("title_logo_frame_v3.png")) return new Vector4(230f, 170f, 230f, 170f);
            if (path.EndsWith("loading_panel_v4.png")) return new Vector4(120f, 100f, 120f, 100f);
            if (path.EndsWith("progress_track_v4.png")) return new Vector4(150f, 60f, 150f, 60f);
            if (path.EndsWith("progress_fill_v4.png")) return new Vector4(140f, 80f, 180f, 80f);
            if (path.EndsWith("loading_panel_v3.png")) return new Vector4(120f, 100f, 120f, 100f);
            if (path.EndsWith("progress_track_v3.png")) return new Vector4(150f, 100f, 150f, 100f);
            if (path.EndsWith("progress_fill_v3.png")) return new Vector4(140f, 90f, 180f, 90f);
            if (path.EndsWith("modal_frame_v3.png")) return new Vector4(120f, 120f, 120f, 120f);
            return Vector4.zero;
        }
    }
}

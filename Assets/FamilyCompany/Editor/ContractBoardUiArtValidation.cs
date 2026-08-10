#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class ContractBoardUiArtValidation
    {
        private const string Root = "Assets/Art/UI/Resources/ContractBoardV2";
        private const float PixelsPerUnit = 100f;

        private static readonly AssetSpec[] FinalSkinAssets =
        {
            new AssetSpec("Skin/Final/Panels/contract_board_panel_9slice_v2.png", 1024, 512, 64),
            new AssetSpec("Skin/Final/Panels/contract_board_request_card_9slice_v2.png", 512, 640, 48),
            new AssetSpec("Skin/Final/Panels/contract_board_family_card_9slice_v2.png", 640, 320, 48),
            new AssetSpec("Skin/Final/Panels/contract_board_contract_tray_9slice_v2.png", 1024, 320, 48),
            new AssetSpec("Skin/Final/Panels/contract_board_tab_normal_9slice_v2.png", 512, 160, 40),
            new AssetSpec("Skin/Final/Panels/contract_board_tab_selected_9slice_v2.png", 512, 160, 40),
            new AssetSpec("Skin/Final/Buttons/contract_board_button_normal_9slice_v2.png", 384, 128, 36),
            new AssetSpec("Skin/Final/Buttons/contract_board_button_hover_9slice_v2.png", 384, 128, 36),
            new AssetSpec("Skin/Final/Buttons/contract_board_button_pressed_9slice_v2.png", 384, 128, 36),
            new AssetSpec("Skin/Final/Buttons/contract_board_button_disabled_9slice_v2.png", 384, 128, 36),
            new AssetSpec("Skin/Final/Buttons/contract_board_button_selected_9slice_v2.png", 384, 128, 36),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_seal_blank_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_clock_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_coins_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_progress_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_folder_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_pin_v2.png", 256, 256, 0),
            new AssetSpec("Skin/Final/Icons/contract_board_icon_family_v2.png", 256, 256, 0)
        };

        private static readonly string[] Opaque2048By1152Assets =
        {
            "Mockups/Final/contract_board_mockup_a_2048x1152_v2.png",
            "Mockups/Final/contract_board_mockup_b_2048x1152_v2.png",
            "Background/Final/contract_board_background_2048x1152_v2.png",
            "QA/contract_board_skin_kit_contact_v2.png"
        };

        [MenuItem("Family Company/Art/Contract Board UI/Validate V2")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Debug.Log("FAMILY_COMPANY_CONTRACT_BOARD_UI_ART_V2: PASS");
        }

        public static void ValidateOrThrow()
        {
            Require(FinalSkinAssets.Length == 18, "Expected 18 final skin assets.");
            var guids = new HashSet<string>(StringComparer.Ordinal);
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in FinalSkinAssets)
                ValidateFinalSkinAsset(spec, guids, hashes);
            foreach (var relative in Opaque2048By1152Assets)
                ValidateOpaqueAsset(relative, 2048, 1152, guids);

            Require(File.Exists(Path.GetFullPath(
                $"{Root}/Generation/contract_board_ui_art_ledger_v2.json")), "Generation ledger is missing.");
            foreach (var source in new[] { "panels", "buttons", "icons" })
            {
                Require(File.Exists(Path.GetFullPath(
                    $"{Root}/Skin/SourceOriginal/contract_board_{source}_chroma_original_v2.png")),
                    $"{source}: chroma original is missing.");
                Require(File.Exists(Path.GetFullPath(
                    $"{Root}/Skin/SourceAlpha/contract_board_{source}_alpha_v2.png")),
                    $"{source}: alpha source is missing.");
            }
            Require(!File.Exists(Path.GetFullPath(
                $"{Root}/Skin/Final/Icons/contract_board_icon_alert_rejected_v2.png")),
                "Rejected glyph-like alert motif must not enter the final kit.");
        }

        private static void ValidateFinalSkinAsset(
            AssetSpec spec,
            HashSet<string> guids,
            HashSet<string> hashes)
        {
            var path = $"{Root}/{spec.RelativePath}";
            ValidateImporter(path, spec.Border);
            RegisterGuid(path, guids);
            var bytes = File.ReadAllBytes(Path.GetFullPath(path));
            using (var sha = SHA256.Create())
            {
                var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
                Require(hashes.Add(hash), $"{path}: exact duplicate final asset.");
            }

            var texture = LoadTexture(bytes, path);
            try
            {
                Require(texture.width == spec.Width && texture.height == spec.Height,
                    $"{path}: expected {spec.Width}x{spec.Height}, found {texture.width}x{texture.height}.");
                var pixels = texture.GetPixels32();
                var transparent = 0;
                var partial = 0;
                var visible = 0;
                foreach (var pixel in pixels)
                {
                    if (pixel.a == 0) transparent++;
                    else
                    {
                        visible++;
                        if (pixel.a < 255) partial++;
                        var looksLikeKey = pixel.r > 180 && pixel.b > 180 && pixel.g < 80;
                        Require(!looksLikeKey, $"{path}: visible chroma-key spill detected.");
                    }
                }
                Require(visible > 0 && transparent > 0, $"{path}: expected visible pixels and transparent padding.");
                Require(partial > 0, $"{path}: expected antialiased edge alpha.");
                Require(pixels[0].a == 0 && pixels[spec.Width - 1].a == 0 &&
                        pixels[(spec.Height - 1) * spec.Width].a == 0 && pixels[pixels.Length - 1].a == 0,
                    $"{path}: transparent canvas corners are required.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateOpaqueAsset(
            string relative,
            int width,
            int height,
            HashSet<string> guids)
        {
            var path = $"{Root}/{relative}";
            ValidateImporter(path, 0);
            RegisterGuid(path, guids);
            var texture = LoadTexture(File.ReadAllBytes(Path.GetFullPath(path)), path);
            try
            {
                Require(texture.width == width && texture.height == height,
                    $"{path}: expected {width}x{height}.");
                foreach (var pixel in texture.GetPixels32())
                    Require(pixel.a == 255, $"{path}: reference/background assets must be opaque.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateImporter(string path, int border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, $"{path}: TextureImporter is missing.");
            Require(importer.textureType == TextureImporterType.Sprite, $"{path}: must import as Sprite.");
            Require(importer.spriteImportMode == SpriteImportMode.Single, $"{path}: must be a single sprite.");
            Require(importer.filterMode == FilterMode.Bilinear, $"{path}: filter must be Bilinear.");
            Require(!importer.mipmapEnabled, $"{path}: mipmaps must be disabled.");
            Require(importer.textureCompression == TextureImporterCompression.Uncompressed,
                $"{path}: texture compression must be Uncompressed.");
            Require(Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit),
                $"{path}: PPU must be {PixelsPerUnit}.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Require(settings.spriteAlignment == (int)SpriteAlignment.Center,
                $"{path}: pivot alignment must be Center.");
            Require(Vector2.Distance(settings.spritePivot, new Vector2(0.5f, 0.5f)) < 0.0001f,
                $"{path}: pivot must be centered.");
            var expectedBorder = new Vector4(border, border, border, border);
            Require(Vector4.Distance(settings.spriteBorder, expectedBorder) < 0.0001f,
                $"{path}: sprite border must be {border} on all sides.");
        }

        private static Texture2D LoadTexture(byte[] bytes, string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (texture.LoadImage(bytes, true)) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException($"{path}: PNG decode failed.");
        }

        private static void RegisterGuid(string path, HashSet<string> guids)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            Require(!string.IsNullOrWhiteSpace(guid) && guids.Add(guid), $"{path}: missing or duplicate GUID.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct AssetSpec
        {
            public AssetSpec(string relativePath, int width, int height, int border)
            {
                RelativePath = relativePath;
                Width = width;
                Height = height;
                Border = border;
            }
            public string RelativePath { get; }
            public int Width { get; }
            public int Height { get; }
            public int Border { get; }
        }
    }
}
#endif

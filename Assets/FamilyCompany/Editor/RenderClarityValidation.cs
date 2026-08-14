using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Editor
{
    public static class RenderClarityValidation
    {
        private const string ProfilePath =
            "Assets/FamilyCompany/Presentation.Unity/Resources/PixelClarityDefault.asset";
        private const string ReportPath = "Artifacts/RenderClarityQa/editor-render-audit.txt";

        private static readonly string[] PixelRuntimeRoots =
        {
            "Assets/Art/Office/Tiles/Floor",
            "Assets/Art/Office/Tiles/Furniture/Runtime",
            "Assets/Art/Characters/Player/Pixel/HighMotion/Frames",
            "Assets/Art/Characters/OlderSister/Pixel/HighMotion/Frames",
            "Assets/Art/Characters/Father/Pixel/HighMotion/Frames",
            "Assets/Art/Characters/Mother/Pixel/HighMotion/Frames",
            "Assets/Art/Characters/Player/Pixel/OfficeSeatingV1/Frames",
            "Assets/Art/Characters/Family/OlderSister/Pixel/OfficeSeatingV1/Frames",
            "Assets/Art/Characters/Family/Father/Pixel/OfficeSeatingV1/Frames",
            "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames",
            "Assets/Art/Characters/Player/Pixel/OfficeWorkActionsV1/Frames",
            "Assets/Art/Characters/Family/OlderSister/Pixel/OfficeWorkActionsV1/Frames",
            "Assets/Art/Characters/Family/Father/Pixel/OfficeWorkActionsV1/Frames",
            "Assets/Art/Characters/Family/Mother/Pixel/OfficeWorkActionsV1/Frames",
            "Assets/Art/Characters/Family/LocomotionTransitionsV1/player/Frames",
            "Assets/Art/Characters/Family/LocomotionTransitionsV1/older_sister/Frames",
            "Assets/Art/Characters/Family/LocomotionTransitionsV1/father/Frames",
            "Assets/Art/Characters/Family/LocomotionTransitionsV1/mother/Frames"
        };

        private static readonly string[] PaintedUiSamples =
        {
            "Assets/Art/UI/Resources/OfficeManagementDashboard_v1.png",
            "Assets/Art/UI/Resources/Title/MoneyRain/money_rain_tycoon_background_v2.png",
            "Assets/Art/UI/Resources/ContractBoardV2/Background/Final/contract_board_background_2048x1152_v2.png",
            "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Icons/contract_board_icon_clock_v2.png"
        };

        [MenuItem("FamilyCompany/Validate Render Clarity")]
        public static void Run()
        {
            var lines = new List<string>();
            ValidateProfile(lines);
            ValidatePipelineAndPlayerSettings(lines);
            ValidatePixelRuntimeImporters(lines);
            ValidatePaintedUiPolicy(lines);
            ValidateAtlasBoundary(lines);
            WriteReport(lines);
            Debug.Log("RENDER_CLARITY_EDITOR_VALIDATION: PASS | " + string.Join(" | ", lines));
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "RENDER_CLARITY_EDITOR_VALIDATION: FAIL | " +
                    exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateProfile(ICollection<string> lines)
        {
            PixelClarityProfile profile = AssetDatabase.LoadAssetAtPath<PixelClarityProfile>(ProfilePath);
            Require(profile != null, "Default pixel clarity profile is missing.");
            profile.ValidateOrThrow();
            Require(Math.Abs(profile.NativeRenderScale - 1f) < 0.0001f, "Native render scale is not 1.0.");
            Require(profile.AntiAliasingSamples == 0, "Pixel clarity profile must disable MSAA.");
            Require(profile.GlobalTextureMipmapLimit == 0, "Global mipmap limit must preserve full resolution.");
            lines.Add(
                $"profile={profile.ReferenceWidth}x{profile.ReferenceHeight}/native-{profile.NativeRenderScale:F2}/" +
                $"snap-camera+actors/PPU-{profile.PixelArtPixelsPerUnit:F0}/AA-{profile.AntiAliasingSamples}");
        }

        private static void ValidatePipelineAndPlayerSettings(ICollection<string> lines)
        {
            string projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            string qualitySettings = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            string graphicsSettings = File.ReadAllText("ProjectSettings/GraphicsSettings.asset");
            string manifest = File.ReadAllText("Packages/manifest.json");

            Require(projectSettings.Contains("defaultScreenWidth: 1920"), "Default screen width is not 1920.");
            Require(projectSettings.Contains("defaultScreenHeight: 1080"), "Default screen height is not 1080.");
            Require(projectSettings.Contains("defaultIsNativeResolution: 1"), "Native resolution default is disabled.");
            Require(projectSettings.Contains("fullscreenMode: 1"), "Default fullscreen mode is not borderless fullscreen.");
            Require(projectSettings.Contains("resolutionScalingMode: 0"), "Player resolution scaling mode is not fixed DPI.");
            Require(qualitySettings.Contains("resolutionScalingFixedDPIFactor: 1"),
                "Quality settings do not preserve a 1.0 fixed DPI render factor.");
            Require(graphicsSettings.Contains("m_CustomRenderPipeline: {fileID: 0}"),
                "A custom render pipeline is unexpectedly active.");
            Require(GraphicsSettings.defaultRenderPipeline == null, "Built-in render pipeline is not active.");
            Require(!manifest.Contains("com.unity.render-pipelines"), "URP/HDRP package unexpectedly exists.");
            Require(!manifest.Contains("com.unity.postprocessing"), "Post-processing package unexpectedly exists.");
            Require(!manifest.Contains("com.unity.2d.pixel-perfect"), "Pixel Perfect package unexpectedly exists.");
            lines.Add("pipeline=built-in/no-urp/no-upscaler/no-post/no-pixel-perfect-package");
            lines.Add("player=1920x1080/native/borderless/fixed-dpi-1.0");
        }

        private static void ValidatePixelRuntimeImporters(ICollection<string> lines)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", PixelRuntimeRoots);
            Require(guids.Length > 0, "No runtime pixel textures were found.");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Require(paths.Length >= 1000, "Runtime pixel importer coverage is unexpectedly small: " + paths.Length);

            int minWidth = int.MaxValue, minHeight = int.MaxValue;
            int maxWidth = 0, maxHeight = 0;
            var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, path + ": TextureImporter is missing.");
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                minWidth = Math.Min(minWidth, width);
                minHeight = Math.Min(minHeight, height);
                maxWidth = Math.Max(maxWidth, width);
                maxHeight = Math.Max(maxHeight, height);

                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                int effectiveMaxSize = standalone.overridden ? standalone.maxTextureSize : importer.maxTextureSize;
                TextureImporterCompression effectiveCompression = standalone.overridden
                    ? standalone.textureCompression
                    : importer.textureCompression;
                Require(importer.textureType == TextureImporterType.Sprite, path + ": must be a Sprite.");
                Require(importer.filterMode == FilterMode.Point, path + ": must use Point filtering.");
                Require(!importer.mipmapEnabled, path + ": mipmaps must be disabled.");
                Require(Math.Abs(importer.spritePixelsPerUnit - 180f) < 0.0001f,
                    path + ": PPU must remain 180.");
                Require(effectiveCompression == TextureImporterCompression.Uncompressed,
                    path + ": Standalone texture must be uncompressed.");
                Require(effectiveMaxSize >= Math.Max(width, height),
                    path + $": max size {effectiveMaxSize} downsizes {width}x{height} source.");

                string category = ResolvePixelCategory(path);
                categoryCounts.TryGetValue(category, out int count);
                categoryCounts[category] = count + 1;
            }

            string categories = string.Join(
                ",",
                categoryCounts.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => item.Key + "=" + item.Value.ToString(CultureInfo.InvariantCulture)));
            lines.Add(
                $"pixel-importers={paths.Length}/point/no-mips/uncompressed/max-size-preserved/180-PPU/" +
                $"source-range={minWidth}x{minHeight}..{maxWidth}x{maxHeight}");
            lines.Add("pixel-categories=" + categories);
        }

        private static void ValidatePaintedUiPolicy(ICollection<string> lines)
        {
            foreach (string path in PaintedUiSamples)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, path + ": painted UI importer is missing.");
                Require(importer.filterMode == FilterMode.Bilinear,
                    path + ": painted/high-resolution UI must retain Bilinear filtering.");
            }
            lines.Add("painted-ui=bilinear-policy-preserved/samples-4");
        }

        private static void ValidateAtlasBoundary(ICollection<string> lines)
        {
            string[] atlasFiles = Directory.GetFiles("Assets", "*.spriteatlas", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets", "*.spriteatlasv2", SearchOption.AllDirectories))
                .ToArray();
            Require(atlasFiles.Length == 0,
                "Sprite Atlas assets require an explicit Point/uncompressed/padding audit before use.");
            lines.Add("sprite-atlas=none/no-hidden-atlas-compression-or-padding");
        }

        private static string ResolvePixelCategory(string path)
        {
            if (path.Contains("/Office/Tiles/Floor/")) return "floor";
            if (path.Contains("/Office/Tiles/Furniture/Runtime/")) return "furniture+walls";
            if (path.Contains("/HighMotion/Frames/")) return "walking";
            if (path.Contains("/OfficeSeatingV1/Frames/")) return "seating";
            if (path.Contains("/OfficeWorkActionsV1/Frames/")) return "work-actions";
            if (path.Contains("/LocomotionTransitionsV1/")) return "locomotion-transitions";
            return "other";
        }

        private static void WriteReport(IEnumerable<string> lines)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllLines(
                ReportPath,
                new[] { "RENDER_CLARITY_EDITOR_VALIDATION: PASS" }.Concat(lines));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

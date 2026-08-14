using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.UIRemaster;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class TitleMoneyRainValidation
    {
        public const string ArtifactFolder = "Artifacts/UiRemasterV3";
        public const string PlayerPath = ArtifactFolder + "/Player/FamilyCompanyUiRemasterQa.exe";
        private const string ScenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
        private const string OfficePreviewScenePath =
            "Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity";
        private const string BootstrapPath = "Assets/FamilyCompany/Presentation.Unity/PrototypeBootstrap.cs";
        private const string RendererPath = "Assets/FamilyCompany/Presentation.Unity/TitleMoneyRainRenderer.cs";
        private const string LoadingPath = "Assets/FamilyCompany/Presentation.Unity/ScenePreviewJump.cs";

        private static readonly string[] RuntimeTexturePaths =
        {
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_hero_background_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_logo_frame_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_button_normal_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_button_hover_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_button_pressed_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/title_button_disabled_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/save_slot_normal_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/save_slot_selected_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/Icons/new_company_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/Icons/continue_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/Icons/load_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/Icons/settings_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Title/Icons/exit_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Loading/loading_background_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Loading/loading_panel_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Loading/progress_track_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Loading/progress_fill_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Loading/loading_work_icon_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/modal_frame_v3.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/card_normal_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/card_hover_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/card_featured_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/card_disabled_v4.png",
            "Assets/Art/UI/Resources/UiRemasterV3/Common/card_compact_normal_v5.png"
        };

        private static readonly HashSet<string> OpaqueBackgrounds = new HashSet<string>(StringComparer.Ordinal)
        {
            RuntimeTexturePaths[0], RuntimeTexturePaths[13]
        };

        [MenuItem("Family Company/Validate UI Remaster V3 Phase 1")]
        public static void Run()
        {
            ValidateRuntimeSourceContract();
            ValidateTypographyContract();
            ValidateResponsiveLayout();
            InspectAssets();
            Debug.Log("FAMILY_COMPANY_UI_REMASTER_V3_VALIDATION: PASS | assets=24 fonts=MaplestoryLight,MaplestoryBold resolutions=1280x720,1392x768,1920x1080,2560x1440,3440x1440");
        }

        [MenuItem("Family Company/Build UI Remaster V3 QA Player")]
        public static void BuildQaPlayer()
        {
            Run();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PlayerPath)) ?? ArtifactFolder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, OfficePreviewScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"UI Remaster V3 release QA player build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            }

            Debug.Log($"FAMILY_COMPANY_UI_REMASTER_V3_BUILD: PASS | release=true path={Path.GetFullPath(PlayerPath)}");
        }

        public static void InspectAssets()
        {
            foreach (var assetPath in RuntimeTexturePaths)
            {
                Assert(File.Exists(Path.GetFullPath(assetPath)), "Generated UI texture is missing: " + assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert(importer != null, "Generated UI texture has no TextureImporter: " + assetPath);
                var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var importedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (importedTexture == null && importedSprite == null)
                {
                    var imported = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    Debug.LogError($"UI_REMASTER_ASSET_DIAGNOSTIC path={assetPath} " +
                                   $"mainType={AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.FullName ?? "null"} " +
                                   $"representations={imported.Length} names={string.Join(",", Array.ConvertAll(imported, asset => asset == null ? "null" : asset.GetType().FullName + ":" + asset.name))}");
                }
                Assert(importedTexture != null || importedSprite != null,
                    "Generated UI texture has no loadable Texture2D or Sprite: " + assetPath);
                Assert(!importer.mipmapEnabled, "UI texture mipmaps must be disabled: " + assetPath);
                Assert(importer.textureCompression == TextureImporterCompression.Uncompressed,
                    "UI texture compression must be disabled: " + assetPath);
                if (OpaqueBackgrounds.Contains(assetPath)) continue;
                Assert(importedSprite != null, "Transparent UI texture has no runtime-loadable Sprite: " + assetPath);
                Assert(importer.alphaIsTransparency, "Transparent UI texture alpha handling is disabled: " + assetPath);
                Assert(importer.textureType == TextureImporterType.Sprite,
                    "Transparent UI texture is not imported as a Sprite: " + assetPath);
            }
        }

        private static void ValidateRuntimeSourceContract()
        {
            Assert(File.Exists(Path.GetFullPath(OfficePreviewScenePath)),
                "Office preview scene required by the loading route is missing.");
            var bootstrap = File.ReadAllText(Path.GetFullPath(BootstrapPath));
            var renderer = File.ReadAllText(Path.GetFullPath(RendererPath));
            var loading = File.ReadAllText(Path.GetFullPath(LoadingPath));
            Assert(renderer.Contains("UiRemasterV3/Title/title_hero_background_v3"),
                "Title renderer is not bound to the generated V3 hero.");
            Assert(TitleMoneyRainRenderer.BundleInstanceCount == 0,
                "Rejected floating-money layer is still active.");
            Assert(!renderer.Contains("money_bundle_"), "Rejected money-bundle resources are still referenced.");
            Assert(!renderer.Contains("money_rain_tycoon_background"), "Rejected title background is still referenced.");
            Assert(!bootstrap.Contains("BuildRoundedUiTexture"), "Title still renders code-generated flat boxes.");
            Assert(bootstrap.Contains("UiRemasterV3/Title/"), "Title buttons are not bound to V3 generated assets.");
            Assert(loading.Contains("UiRemasterV3/Loading/"), "Loading UI is not bound to V3 generated assets.");
            Assert(!loading.Contains("ModernTealProgress"), "Rejected flat loading skin is still active.");
        }

        private static void ValidateTypographyContract()
        {
            Assert(UiRemasterTypography.PanelTitlePixels >= 24, "Panel title is below the 720p minimum.");
            Assert(UiRemasterTypography.CardTitlePixels >= 18, "Card title is below the 720p minimum.");
            Assert(UiRemasterTypography.BodyPixels >= 14, "Body text is below the 720p minimum.");
            Assert(UiRemasterTypography.TopHudPixels >= 16, "Top HUD text is below the 720p minimum.");
            Assert(UiRemasterTypography.BottomNavigationPixels >= 15, "Bottom navigation text is below the 720p minimum.");
            Assert(UiRemasterTypography.ButtonPixels >= 14, "Button text is below the 720p minimum.");
            Assert(Mathf.Approximately(UiRemasterTypography.CalculateScale(1280, 720), 1f),
                "1280x720 must never autoshrink the UI.");
            Assert(UiRemasterTypography.CalculateScale(1252, 745) >= 1f,
                "Rejected 1252x745 case must never autoshrink typography.");
        }

        private static void ValidateResponsiveLayout()
        {
            ValidateLayout(1280, 720);
            ValidateLayout(1392, 768);
            ValidateLayout(1920, 1080);
            ValidateLayout(2560, 1440);
            ValidateLayout(3440, 1440);
        }

        private static void ValidateLayout(int width, int height)
        {
            var title = UiRemasterLayout.CalculateTitle(width, height);
            var screen = new Rect(0f, 0f, width, height);
            Assert(Contains(screen, title.Logo), $"Title logo is clipped at {width}x{height}.");
            Assert(Contains(screen, title.Subtitle), $"Title subtitle is clipped at {width}x{height}.");
            Assert(Contains(screen, title.Footer), $"Title footer is clipped at {width}x{height}.");
            Assert(title.Buttons.Length == 5, "Title must expose exactly five menu hit targets.");
            for (var index = 0; index < title.Buttons.Length; index++)
            {
                Assert(Contains(screen, title.Buttons[index]), $"Title button {index} is clipped at {width}x{height}.");
                Assert(IsIntegral(title.Buttons[index]), $"Title button {index} is not pixel-snapped at {width}x{height}.");
                if (index > 0) Assert(!title.Buttons[index].Overlaps(title.Buttons[index - 1]),
                    $"Title buttons overlap at {width}x{height}.");
            }

            var loading = UiRemasterLayout.CalculateLoading(width, height);
            Assert(Contains(screen, loading.Panel), $"Loading panel is clipped at {width}x{height}.");
            Assert(Contains(loading.Panel, loading.Icon) && Contains(loading.Panel, loading.Title) &&
                   Contains(loading.Panel, loading.Status) && Contains(loading.Panel, loading.Track) &&
                   Contains(loading.Panel, loading.Percent) && Contains(loading.Panel, loading.Detail),
                $"Loading content escapes its generated panel at {width}x{height}.");
            Assert(!loading.Icon.Overlaps(loading.Title), $"Loading icon collides with title at {width}x{height}.");
            Assert(IsIntegral(loading.Panel) && IsIntegral(loading.Track),
                $"Loading layout is not pixel-snapped at {width}x{height}.");
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin && inner.yMin >= outer.yMin &&
                   inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        }

        private static bool IsIntegral(Rect rect)
        {
            return Mathf.Approximately(rect.x, Mathf.Round(rect.x)) &&
                   Mathf.Approximately(rect.y, Mathf.Round(rect.y)) &&
                   Mathf.Approximately(rect.width, Mathf.Round(rect.width)) &&
                   Mathf.Approximately(rect.height, Mathf.Round(rect.height));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

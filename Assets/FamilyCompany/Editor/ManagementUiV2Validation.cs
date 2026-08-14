using System;
using System.IO;
using System.Text.RegularExpressions;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Simulation.ManagementUi;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class ManagementUiV2Validation
    {
        private const string BootstrapPath = "Assets/FamilyCompany/Presentation.Unity/PrototypeBootstrap.cs";
        private const string PresenterPath = "Assets/FamilyCompany/Presentation.Unity/ManagementUI/ManagementUiV2Presenter.cs";
        private const string CameraFollowPath = "Assets/FamilyCompany/Presentation.Unity/IsometricCameraFollow.cs";
        private const string ObservationStatusPath = "Assets/FamilyCompany/Presentation.Unity/ManagementUI/IOfficeObservationStatusSource.cs";
        private const string FontCatalogPath = "Assets/FamilyCompany/Presentation.Unity/Resources/ManagementUI/ManagementUiFontCatalog_v1.asset";
        private const string SkinCatalogPath = "Assets/FamilyCompany/Presentation.Unity/Resources/ManagementUI/ManagementUiSkin_v1.asset";
        private const string TextMeshProSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string BackgroundPath = "Assets/Art/UI/Resources/ManagementUIP0/office_management_background_v1.png";
        private const string PretendardLicensePath = "Assets/Fonts/Licenses/LICENSE-Pretendard.txt";
        private const string NotoLicensePath = "Assets/Fonts/Licenses/LICENSE-NotoSansCJK.txt";
        private const string ButtonNormalPath = "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Buttons/contract_board_button_normal_9slice_v2.png";
        private const string ButtonDisabledPath = "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Buttons/contract_board_button_disabled_9slice_v2.png";
        private const string TabPath = "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Panels/contract_board_tab_normal_9slice_v2.png";
        private const string PanelPath = "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Panels/contract_board_panel_9slice_v2.png";
        private const string CardPath = "Assets/Art/UI/Resources/ContractBoardV2/Skin/Final/Panels/contract_board_request_card_9slice_v2.png";

        [MenuItem("Family Company/Validate Management UI V2")]
        public static void Run()
        {
            try
            {
                ValidateLayoutMetrics();
                ValidateRuntimeStructure();
                ValidateFonts();
                ValidateSkinContract();
                ValidateBackgroundContract();
                ValidateR1RegressionFixture();
                Debug.Log("MANAGEMENT_UI_V2_VALIDATION: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("MANAGEMENT_UI_V2_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        private static void ValidateLayoutMetrics()
        {
            ManagementUiAccessibility.Validate();
            foreach (var resolution in new[]
                     {
                         new Vector2Int(1280, 720),
                         new Vector2Int(1920, 1080),
                         new Vector2Int(2048, 1152),
                         new Vector2Int(2560, 1440)
                     })
            {
                var layout = ManagementUiLayoutMetrics.Calculate(
                    resolution.x,
                    resolution.y,
                    UiSafeInsets.None);
                ManagementUiLayoutMetrics.Validate(layout);
                Debug.Log(
                    $"MANAGEMENT_UI_V2_LAYOUT: {resolution.x}x{resolution.y} " +
                    $"scale={layout.ScaleFactor:0.####} top={layout.TopHud} " +
                    $"family={layout.FamilyRail} center={layout.ManagementCenter} " +
                    $"quick={layout.QuickActions} cards={layout.OfferCards[0]}|{layout.OfferCards[1]}|{layout.OfferCards[2]}");
            }

            if (ManagementUiV2Presenter.ReferenceResolution != new Vector2(1920f, 1080f) ||
                Math.Abs(ManagementUiV2Presenter.CanvasMatchWidthOrHeight - 0.5f) > 0.0001f)
            {
                throw new InvalidOperationException("CanvasScaler contract is not 1920x1080 with match 0.5.");
            }
        }

        private static void ValidateRuntimeStructure()
        {
            var packageManifest = File.ReadAllText(Path.GetFullPath("Packages/manifest.json"));
            if (!packageManifest.Contains("\"com.unity.ugui\": \"2.0.0\""))
                throw new InvalidOperationException("uGUI 2.0.0 is not pinned in Packages/manifest.json.");

            var presenter = File.ReadAllText(Path.GetFullPath(PresenterPath));
            foreach (var requiredToken in new[]
                     {
                         "CanvasScaler.ScaleMode.ScaleWithScreenSize",
                         "CanvasScaler.ScreenMatchMode.MatchWidthOrHeight",
                         "VerticalLayoutGroup",
                         "HorizontalLayoutGroup",
                         "Screen.safeArea",
                         "TextMeshProUGUI",
                         "IOfficeObservationStatusSource",
                         "canvas.pixelPerfect = true",
                         "CanvasGroup",
                         "WarmHiddenManagementPresentation"
                     })
            {
                if (!presenter.Contains(requiredToken))
                    throw new InvalidOperationException($"Management presenter is missing required structure token: {requiredToken}");
            }
            if (presenter.Contains("CreateDynamicFontFromOSFont"))
                throw new InvalidOperationException("Management UI must not depend on an installed operating-system font.");
            if (presenter.Contains(".Normalize("))
                throw new InvalidOperationException("Management UI glyph proof must preserve decomposed Jamo instead of hiding it with NFC normalization.");
            foreach (var requiredToken in new[]
                     {
                         "ManagementUiLayoutMetrics.FontCatalogResourcePath",
                         "ManagementUiLayoutMetrics.BackgroundResourcePath",
                         "KoreanGlyphQaSample",
                         "Management Panel Tight Slice P0",
                         "Management Card Tight Slice P0",
                         "MANAGEMENT_UI_LOADING_PREWARM",
                         "ReferenceEquals(_prewarmedState, _bootstrap.State)",
                         "ManagementButtonListenerHostCountForQa",
                         "FormatCodePoints",
                         "사무실 보기 · C"
                     })
            {
                if (!presenter.Contains(requiredToken))
                    throw new InvalidOperationException($"Office UI P0 presenter token is missing: {requiredToken}");
            }

            var bootstrap = StripDisabledBlocks(File.ReadAllText(Path.GetFullPath(BootstrapPath)));
            if (!bootstrap.Contains("PrototypeUiScreen.Management") ||
                !bootstrap.Contains("ShowManagementNow") ||
                !bootstrap.Contains("CloseManagementNow") ||
                !bootstrap.Contains("ApplyOfficeObservationCamera(true)"))
            {
                throw new InvalidOperationException("Office-first management overlay state transitions are incomplete.");
            }
            if (bootstrap.Contains("OfficeManagementDashboard_v1") ||
                bootstrap.Contains("BusinessExpansionDashboard_v1") ||
                bootstrap.Contains("DashboardRect("))
            {
                throw new InvalidOperationException("Active bootstrap code still depends on the baked dashboard or absolute dashboard rectangles.");
            }

            var cameraFollow = File.ReadAllText(Path.GetFullPath(CameraFollowPath));
            if (!cameraFollow.Contains("SetOfficeObservationForced") ||
                !cameraFollow.Contains("snapImmediately"))
            {
                throw new InvalidOperationException("New/load games cannot enter the real OfficeVisual observation camera immediately.");
            }

            var observationStatus = File.ReadAllText(Path.GetFullPath(ObservationStatusPath));
            foreach (var requiredStatus in new[] { "Moving", "Seated", "Typing", "Mouse", "Drinking" })
            {
                if (!observationStatus.Contains(requiredStatus))
                    throw new InvalidOperationException($"Office observation status contract is missing: {requiredStatus}");
            }
        }

        private static void ValidateFonts()
        {
            if (!File.Exists(Path.GetFullPath(TextMeshProSettingsPath)))
                throw new InvalidOperationException("TMP Settings resource is missing; dynamic Korean fonts cannot initialize in PlayMode or player builds.");
            var catalog = AssetDatabase.LoadAssetAtPath<ManagementUiFontCatalog>(FontCatalogPath);
            if (catalog == null || !catalog.IsComplete)
                throw new InvalidOperationException("Bundled Korean management font catalog is missing or incomplete.");
            foreach (var sample in ManagementUiV2Presenter.KoreanGlyphQaSample)
            {
                if (char.IsWhiteSpace(sample)) continue;
                if (!catalog.BodySource.HasCharacter(sample) && !catalog.FallbackSource.HasCharacter(sample))
                    throw new InvalidOperationException($"Bundled management fonts do not contain Korean glyph '{sample}'.");
            }
            var license = File.ReadAllText(Path.GetFullPath(PretendardLicensePath));
            if (!license.Contains("SIL OPEN FONT LICENSE Version 1.1") || !license.Contains("Pretendard"))
                throw new InvalidOperationException("Pretendard OFL license file is missing or incomplete.");
            var notoLicense = File.ReadAllText(Path.GetFullPath(NotoLicensePath));
            if (!notoLicense.Contains("SIL OPEN FONT LICENSE Version 1.1"))
                throw new InvalidOperationException("Noto Sans CJK OFL license file is missing or incomplete.");
            if (catalog.FallbackSource.name.IndexOf("NotoSansKR", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Management UI decomposed-Jamo fallback is not bundled Noto Sans KR.");
            foreach (var jamo in new[] { '\u1100', '\u1161', '\u11A8' })
                if (!catalog.FallbackSource.HasCharacter(jamo))
                    throw new InvalidOperationException($"Noto Sans KR fallback lacks decomposed Jamo U+{(int)jamo:X4}.");
            var runtimeFallback = TMP_FontAsset.CreateFontAsset(catalog.FallbackSource);
            if (runtimeFallback == null)
                throw new InvalidOperationException("Noto Sans KR could not create a runtime TMP font asset.");
            try
            {
                runtimeFallback.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                runtimeFallback.isMultiAtlasTexturesEnabled = true;
                foreach (var jamo in new[] { '\u1100', '\u1161', '\u11A8' })
                    if (!runtimeFallback.HasCharacter(jamo, true, true))
                        throw new InvalidOperationException(
                            $"Runtime TMP fallback could not rasterize decomposed Jamo U+{(int)jamo:X4}.");
                Debug.Log("MANAGEMENT_UI_RUNTIME_TMP_JAMO: PASS U+1100,U+1161,U+11A8");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeFallback);
            }
        }

        private static void ValidateSkinContract()
        {
            var skin = AssetDatabase.LoadAssetAtPath<ManagementUiSkinCatalog>(SkinCatalogPath);
            if (skin == null)
            {
                Debug.Log("MANAGEMENT_UI_V2_SKIN: FALLBACK_READY (versioned ImageGen 9-slice asset not integrated)");
                return;
            }
            if (!skin.IsComplete)
                throw new InvalidOperationException("ManagementUiSkin_v1 exists but violates the versioned five-sprite 9-slice contract.");
            ValidateVerticalBorder("panel", skin.Panel, ManagementUiLayoutMetrics.TopHudHeight);
            ValidateVerticalBorder("button", skin.Button, ManagementUiLayoutMetrics.MinimumClickTarget);
            ValidateVerticalBorder("disabled button", skin.ButtonDisabled, ManagementUiLayoutMetrics.MinimumClickTarget);
            ValidateVerticalBorder("tab", skin.Tab, ManagementUiLayoutMetrics.TabsHeight);
            ValidateVerticalBorder("card", skin.Card, ManagementUiLayoutMetrics.FamilyCardHeight);
            ValidateTransparentGutterCrop("panel", PanelPath, new RectInt(18, 10, 988, 492));
            ValidateTransparentGutterCrop("card", CardPath, new RectInt(15, 10, 481, 620));
            var normal = MeasureOpaqueCrop(ButtonNormalPath, new RectInt(98, 8, 188, 112));
            var disabled = MeasureOpaqueCrop(ButtonDisabledPath, new RectInt(98, 8, 187, 112));
            var tab = MeasureOpaqueCrop(TabPath, new RectInt(10, 24, 492, 112));
            ValidateOpaqueCoverage("button", normal);
            ValidateOpaqueCoverage("disabled button", disabled);
            ValidateOpaqueCoverage("tab", tab);
            Debug.Log(
                "MANAGEMENT_UI_V2_SKIN: V1_READY " +
                $"panelVertical={skin.Panel.border.y + skin.Panel.border.w:0}/{ManagementUiLayoutMetrics.TopHudHeight:0} " +
                $"buttonVertical={skin.Button.border.y + skin.Button.border.w:0}/{ManagementUiLayoutMetrics.MinimumClickTarget:0} " +
                $"tabVertical={skin.Tab.border.y + skin.Tab.border.w:0}/{ManagementUiLayoutMetrics.TabsHeight:0} " +
                $"tightButton=area:{normal.AreaRatio:P1},width:{normal.WidthRatio:P1},height:{normal.HeightRatio:P1}");
        }

        private static void ValidateVerticalBorder(string label, Sprite sprite, double minimumRenderedHeight)
        {
            if (sprite == null) throw new InvalidOperationException($"Management {label} sprite is missing.");
            var borderHeight = sprite.border.y + sprite.border.w;
            if (borderHeight >= minimumRenderedHeight)
                throw new InvalidOperationException(
                    $"Management {label} 9-slice vertical border {borderHeight:0.##}px leaves no stretch center at " +
                    $"the {minimumRenderedHeight:0.##}px design-token height.");
        }

        private static OpaqueCropMetrics MeasureOpaqueCrop(string path, RectInt crop)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("9-slice source is missing.", fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(fullPath), false))
                    throw new InvalidOperationException("Could not decode 9-slice source: " + path);
                if (crop.xMin < 0 || crop.yMin < 0 || crop.xMax > texture.width || crop.yMax > texture.height)
                    throw new InvalidOperationException("Tight-slice crop escapes source texture: " + path);
                var pixels = texture.GetPixels32();
                var opaque = 0;
                var minimumX = crop.xMax;
                var maximumX = crop.xMin - 1;
                var minimumY = crop.yMax;
                var maximumY = crop.yMin - 1;
                for (var y = crop.yMin; y < crop.yMax; y++)
                for (var x = crop.xMin; x < crop.xMax; x++)
                {
                    if (pixels[y * texture.width + x].a < 200) continue;
                    opaque++;
                    minimumX = Math.Min(minimumX, x);
                    maximumX = Math.Max(maximumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumY = Math.Max(maximumY, y);
                }
                var width = maximumX >= minimumX ? maximumX - minimumX + 1 : 0;
                var height = maximumY >= minimumY ? maximumY - minimumY + 1 : 0;
                return new OpaqueCropMetrics(
                    opaque / (double)(crop.width * crop.height),
                    width / (double)crop.width,
                    height / (double)crop.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateTransparentGutterCrop(string label, string path, RectInt expected)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("9-slice source is missing.", fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(fullPath), false))
                    throw new InvalidOperationException("Could not decode 9-slice source: " + path);
                var pixels = texture.GetPixels32();
                var minimumX = texture.width;
                var minimumY = texture.height;
                var maximumX = -1;
                var maximumY = -1;
                for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a == 0) continue;
                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
                var actual = new RectInt(
                    minimumX,
                    minimumY,
                    maximumX >= minimumX ? maximumX - minimumX + 1 : 0,
                    maximumY >= minimumY ? maximumY - minimumY + 1 : 0);
                if (actual != expected)
                    throw new InvalidOperationException(
                        $"Management {label} transparent-gutter crop drifted: expected={expected}, actual={actual}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateOpaqueCoverage(string label, OpaqueCropMetrics metrics)
        {
            var minimum = ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage;
            if (metrics.AreaRatio < minimum || metrics.WidthRatio < minimum || metrics.HeightRatio < minimum)
                throw new InvalidOperationException(
                    $"Management {label} tight surface does not fill at least {minimum:P0} of hit area: " +
                    $"area={metrics.AreaRatio:P1}, width={metrics.WidthRatio:P1}, height={metrics.HeightRatio:P1}.");
        }

        private static void ValidateBackgroundContract()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (texture == null || sprite == null)
                throw new InvalidOperationException("Office UI P0 background is not imported as a Sprite.");
            if (texture.width != 2560 || texture.height != 1440)
                throw new InvalidOperationException($"Office UI P0 background must be 2560x1440; found {texture.width}x{texture.height}.");
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
                throw new InvalidOperationException("Office UI P0 background importer must be a non-mipmapped Sprite.");
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            if (!standalone.overridden || standalone.maxTextureSize < 4096 || standalone.textureCompression != TextureImporterCompression.Uncompressed)
                throw new InvalidOperationException("Standalone office background must preserve 2560x1440 without compression blur.");
            Debug.Log("MANAGEMENT_UI_P0_BACKGROUND: PASS 2560x1440 sprite standalone=uncompressed");
        }

        private static void ValidateR1RegressionFixture()
        {
            var fixtureSpread = ManagementUiR1RegressionFixture.CalculateSpread(
                ManagementUiR1RegressionFixture.OfferBorderWidths);
            if (fixtureSpread != ManagementUiR1RegressionFixture.OfferBorderWidthSpread)
                throw new InvalidOperationException("R1 offer-card border-spread fixture changed unexpectedly.");
            if (ManagementUiR1RegressionFixture.SurfaceRatio(
                    ManagementUiR1RegressionFixture.SpeedOpaqueWidth,
                    ManagementUiR1RegressionFixture.SpeedHitWidth) >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage ||
                ManagementUiR1RegressionFixture.SurfaceRatio(
                    ManagementUiR1RegressionFixture.SaveOpaqueWidth,
                    ManagementUiR1RegressionFixture.SaveHitWidth) >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage ||
                ManagementUiR1RegressionFixture.SurfaceRatio(
                    ManagementUiR1RegressionFixture.MenuOpaqueWidth,
                    ManagementUiR1RegressionFixture.MenuHitWidth) >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage)
            {
                throw new InvalidOperationException("R1 transparent-gutter fixture no longer demonstrates the rejected button surfaces.");
            }
            Debug.Log(
                "MANAGEMENT_UI_R1_REGRESSION_FIXTURE: PASS " +
                "offerWidths=385,358,394 spread=36 textSafeDeficit=13..16 " +
                "speedSurface=22x32/75x48 saveSurface=34x32/116x48 menuSurface=94x32/240x48");
        }

        private readonly struct OpaqueCropMetrics
        {
            public OpaqueCropMetrics(double areaRatio, double widthRatio, double heightRatio)
            {
                AreaRatio = areaRatio;
                WidthRatio = widthRatio;
                HeightRatio = heightRatio;
            }

            public double AreaRatio { get; }
            public double WidthRatio { get; }
            public double HeightRatio { get; }
        }

        private static string StripDisabledBlocks(string source)
        {
            return Regex.Replace(source, @"#if false[\s\S]*?#endif", string.Empty, RegexOptions.CultureInvariant);
        }
    }
}

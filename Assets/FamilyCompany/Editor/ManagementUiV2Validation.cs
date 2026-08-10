using System;
using System.IO;
using System.Text.RegularExpressions;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Simulation.ManagementUi;
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

        [MenuItem("Family Company/Validate Management UI V2")]
        public static void Run()
        {
            try
            {
                ValidateLayoutMetrics();
                ValidateRuntimeStructure();
                ValidateFonts();
                ValidateSkinContract();
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
                         new Vector2Int(2048, 1152)
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
                         "IOfficeObservationStatusSource"
                     })
            {
                if (!presenter.Contains(requiredToken))
                    throw new InvalidOperationException($"Management presenter is missing required structure token: {requiredToken}");
            }
            if (presenter.Contains("CreateDynamicFontFromOSFont"))
                throw new InvalidOperationException("Management UI must not depend on an installed operating-system font.");

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
            foreach (var sample in new[] { '가', '족', '회', '사', '관', '리', '화', '면' })
            {
                if (!catalog.BodySource.HasCharacter(sample) && !catalog.FallbackSource.HasCharacter(sample))
                    throw new InvalidOperationException($"Bundled management fonts do not contain Korean glyph '{sample}'.");
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
            Debug.Log("MANAGEMENT_UI_V2_SKIN: V1_READY");
        }

        private static string StripDisabledBlocks(string source)
        {
            return Regex.Replace(source, @"#if false[\s\S]*?#endif", string.Empty, RegexOptions.CultureInvariant);
        }
    }
}

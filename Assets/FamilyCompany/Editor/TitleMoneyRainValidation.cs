using System;
using System.IO;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class TitleMoneyRainValidation
    {
        public const string ArtifactFolder = "Artifacts/TitleMoneyRainQa";
        public const string PlayerPath = ArtifactFolder + "/Player/FamilyCompanyTitleMoneyRainQa.exe";
        private const string ScenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
        private const string BootstrapPath = "Assets/FamilyCompany/Presentation.Unity/PrototypeBootstrap.cs";
        private const string RendererPath = "Assets/FamilyCompany/Presentation.Unity/TitleMoneyRainRenderer.cs";
        private const string PreviewGifPath = "Assets/Art/UI/Resources/Title/family_company_title_money_rain_v1.gif";

        private static readonly string[] RuntimeTexturePaths =
        {
            "Assets/Art/UI/Resources/Title/MoneyRain/money_rain_office_background_v1.png",
            "Assets/Art/UI/Resources/Title/MoneyRain/money_bundle_mint_v1.png",
            "Assets/Art/UI/Resources/Title/MoneyRain/money_bundle_coral_v1.png",
            "Assets/Art/UI/Resources/Title/MoneyRain/money_bundle_sky_v1.png"
        };

        [MenuItem("Family Company/Validate Title Money Rain")]
        public static void Run()
        {
            ValidateRuntimeSourceContract();
            ValidateMotionContract();
            ValidateResponsiveLayout();
            var assetStatus = InspectAssets();
            Debug.Log("FAMILY_COMPANY_TITLE_MONEY_RAIN_VALIDATION: PASS · " + assetStatus);
        }

        [MenuItem("Family Company/Build Title Money Rain QA Player")]
        public static void BuildQaPlayer()
        {
            Run();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PlayerPath)) ?? ArtifactFolder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Title money-rain QA player build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            }

            Debug.Log($"FAMILY_COMPANY_TITLE_MONEY_RAIN_BUILD: PASS · {Path.GetFullPath(PlayerPath)}");
        }

        public static string InspectAssets()
        {
            var availableRuntimeTextures = 0;
            for (var index = 0; index < RuntimeTexturePaths.Length; index++)
            {
                var assetPath = RuntimeTexturePaths[index];
                if (!File.Exists(Path.GetFullPath(assetPath))) continue;
                availableRuntimeTextures++;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                {
                    throw new InvalidOperationException("Money-rain texture has not imported as Texture2D: " + assetPath);
                }

                if (index == 0) continue;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException("Money bundle has no TextureImporter: " + assetPath);
                }

                if (!importer.alphaIsTransparency)
                {
                    throw new InvalidOperationException("Money bundle transparency is disabled: " + assetPath);
                }
            }

            var previewAvailable = File.Exists(Path.GetFullPath(PreviewGifPath));
            var status = $"runtime textures {availableRuntimeTextures}/{RuntimeTexturePaths.Length}, preview GIF {(previewAvailable ? "1/1" : "0/1")}";
            if (availableRuntimeTextures != RuntimeTexturePaths.Length || !previewAvailable)
            {
                Debug.LogWarning("FAMILY_COMPANY_TITLE_MONEY_RAIN_ASSETS_PENDING: " + status +
                                 " · missing textures use the bright non-art fallback; the GIF is never loaded at runtime.");
            }

            return status;
        }

        private static void ValidateRuntimeSourceContract()
        {
            var bootstrap = File.ReadAllText(Path.GetFullPath(BootstrapPath));
            var renderer = File.ReadAllText(Path.GetFullPath(RendererPath));
            Assert(!bootstrap.Contains("family_company_title_hero_v1"), "Bootstrap still references the retired title hero resource.");
            Assert(!renderer.Contains("family_company_title_hero_v1"), "Money-rain renderer references the retired title hero resource.");
            Assert(renderer.Contains("Time.unscaledTime"), "Money rain must advance from Time.unscaledTime while menus pause timeScale.");
            Assert(renderer.Contains("finally"), "Money-rain GUI state restoration must be protected by finally.");
            Assert(renderer.Contains("GUI.matrix = previousMatrix"), "Money-rain renderer must restore GUI.matrix.");
            Assert(!renderer.Contains("family_company_title_money_rain_v1"), "Preview GIF must not be loaded by runtime code.");
            Assert(TitleMoneyRainRenderer.BundleInstanceCount >= 8 && TitleMoneyRainRenderer.BundleInstanceCount <= 14,
                "Visible money-bundle instance count must remain within 8..14.");
        }

        private static void ValidateMotionContract()
        {
            var seenTexture = new bool[3];
            var signature0 = FrameSignature(0f, 1920f, 1080f);
            var signature1 = FrameSignature(TitleMoneyRainRenderer.LoopDuration / 3f, 1920f, 1080f);
            var signature2 = FrameSignature(TitleMoneyRainRenderer.LoopDuration * 2f / 3f, 1920f, 1080f);
            Assert(!Approximately(signature0, signature1) && !Approximately(signature1, signature2) && !Approximately(signature0, signature2),
                "Three sampled money-rain frames must be observably different.");

            for (var index = 0; index < TitleMoneyRainRenderer.BundleInstanceCount; index++)
            {
                var duration = TitleMoneyRainRenderer.GetBundleLoopDuration(index);
                Assert(Mathf.Abs(duration - 2.8f) < 0.0001f,
                    $"Bundle {index} duration {duration:0.###} is not the required 2.8 seconds.");
                var first = TitleMoneyRainRenderer.CalculateBundlePose(index, 0.314f, 1920f, 1080f);
                var looped = TitleMoneyRainRenderer.CalculateBundlePose(index, 0.314f + duration, 1920f, 1080f);
                Assert(Vector2.Distance(first.Rect.center, looped.Rect.center) < 0.02f,
                    $"Bundle {index} position does not wrap seamlessly.");
                Assert(Mathf.Abs(Mathf.DeltaAngle(first.RotationDegrees, looped.RotationDegrees)) < 0.02f,
                    $"Bundle {index} rotation does not wrap seamlessly.");
                Assert(Mathf.Abs(first.Alpha - looped.Alpha) < 0.002f,
                    $"Bundle {index} alpha does not wrap seamlessly.");
                Assert(first.TextureIndex >= 0 && first.TextureIndex < seenTexture.Length,
                    $"Bundle {index} has an invalid texture variant.");
                seenTexture[first.TextureIndex] = true;
            }

            for (var index = 0; index < seenTexture.Length; index++)
            {
                Assert(seenTexture[index], "Money-bundle texture variant is unused: " + index);
            }
        }

        private static void ValidateResponsiveLayout()
        {
            ValidateLayout(1280f, 720f);
            ValidateLayout(1920f, 1080f);
            ValidateLayout(3440f, 1080f);
        }

        private static void ValidateLayout(float width, float height)
        {
            var layout = TitleMoneyRainRenderer.CalculateLayout(width, height);
            var menu = layout.MenuSafeArea;
            var panel = layout.ReadabilityPanel;
            Assert(menu.x >= 0f && menu.y >= 0f && menu.xMax <= width && menu.yMax <= height + 0.01f,
                $"Menu safe area is clipped at {width:0}x{height:0}.");
            Assert(panel.x <= menu.x && panel.xMax >= menu.xMax + 20f && panel.y <= menu.y && panel.yMax >= menu.yMax,
                $"Readability panel does not protect the menu at {width:0}x{height:0}.");
            Assert(menu.width >= 450f && menu.height >= 545f,
                $"Menu controls have insufficient space at {width:0}x{height:0}.");
            Assert(panel.width / width <= 0.51f,
                $"Readability panel consumes excessive ultrawide space at {width:0}x{height:0}.");
        }

        private static float FrameSignature(float time, float width, float height)
        {
            var signature = 0f;
            for (var index = 0; index < TitleMoneyRainRenderer.BundleInstanceCount; index++)
            {
                var pose = TitleMoneyRainRenderer.CalculateBundlePose(index, time, width, height);
                signature += pose.Rect.x * (index + 1f) + pose.Rect.y * (index + 2f) + pose.RotationDegrees * 0.1f;
            }

            return signature;
        }

        private static bool Approximately(float first, float second)
        {
            return Mathf.Abs(first - second) < 0.01f;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

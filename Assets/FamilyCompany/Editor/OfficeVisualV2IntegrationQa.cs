using System;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeVisualV2IntegrationQa
    {
        private const string ArtifactFolder = "Artifacts/OfficeVisualV2Qa";

        [MenuItem("Family Company/Office Visual V2/Validate Fallback And Scene Preparation")]
        public static void ValidatePreparationMenu()
        {
            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
            ValidateScenePreparation();
            Debug.Log("OFFICE_VISUAL_V2_QA: PREPARATION PASS");
        }

        public static void ValidatePreparationBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
                ValidateScenePreparation();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateScenePreparation()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<OfficeVisualV2Presenter>();
            Require(presenter != null, "OfficeVisualV2Presenter is missing from the office scene.");
            Require(Mathf.Abs(presenter.CharacterVisualScale - OfficeVisualV2Presenter.DefaultCharacterScale) < 0.001f,
                $"Unexpected character visual scale: {presenter.CharacterVisualScale:F3}");

            var camera = Camera.main;
            Require(camera != null && camera.orthographic, "The office main camera must remain orthographic.");
            var follow = camera.GetComponent<IsometricCameraFollow>();
            Require(follow != null && follow.OfficeFramingEnabled, "Office camera framing is not configured.");
            Require(Mathf.Abs(follow.OfficeOrthographicSize - 6.6f) < 0.001f,
                $"Office camera size must be 6.6, found {follow.OfficeOrthographicSize:F3}.");
            var pixelEffect = camera.GetComponent<PixelatedCameraEffect>();
            Require(pixelEffect != null && pixelEffect.AdaptiveHalfOutputHeight,
                "Adaptive 720p/1080p point rendering is not configured.");
            Require(pixelEffect.MinimumAdaptiveHeight == 360 && pixelEffect.MaximumAdaptiveHeight == 540,
                "Adaptive render height must be 360 at 720p and 540 at 1080p.");

            var animators = UnityEngine.Object.FindObjectsByType<DirectionalSpriteAnimator>(FindObjectsSortMode.None);
            Require(animators.Length == 4, $"Initial office must contain four family animators; found {animators.Length}.");
            foreach (var animator in animators)
            {
                var renderer = animator.GetComponentInChildren<SpriteRenderer>(true);
                Require(renderer != null, $"Character SpriteRenderer is missing on {animator.name}.");
                Require(Mathf.Abs(renderer.transform.localScale.x - OfficeVisualV2Presenter.DefaultCharacterScale) < 0.001f &&
                        Mathf.Abs(renderer.transform.localScale.y - OfficeVisualV2Presenter.DefaultCharacterScale) < 0.001f,
                    $"Character visual scale was not applied on {animator.name}.");
                Require(renderer.spriteSortPoint == SpriteSortPoint.Pivot,
                    $"Character foot-pivot sorting is missing on {animator.name}.");
            }

            var office = GameObject.Find("FAMILY OFFICE V0.2");
            Require(office != null, "Office blockout root is missing.");
            var colliders = office.GetComponentsInChildren<Collider>(true);
            var blockoutRenderers = office.GetComponentsInChildren<MeshRenderer>(true);
            Require(colliders.Length >= 12 && colliders.All(item => item.enabled),
                $"Office collision layout is incomplete or disabled: {colliders.Length} colliders.");
            Require(blockoutRenderers.Length >= 12,
                $"Office fallback blockout is incomplete: {blockoutRenderers.Length} renderers.");

            var projectedOfficeWidth = (presenter.OfficeSize.x + presenter.OfficeSize.y) / Mathf.Sqrt(2f);
            var visibleWidth = follow.OfficeOrthographicSize * 2f * (16f / 9f);
            var occupancy = projectedOfficeWidth / visibleWidth;
            Require(occupancy >= 0.82f && occupancy <= 0.96f,
                $"Office framing occupancy is outside the safe full-screen range: {occupancy:P1}.");

            var basePath = FindLayerAssetPath(false);
            if (string.IsNullOrEmpty(basePath))
            {
                Require(blockoutRenderers.All(item => item.enabled),
                    "Blockout renderers must remain visible while OfficeVisualV2 base is absent.");
                Debug.Log(
                    $"OFFICE_VISUAL_V2_FALLBACK_PASS | colliders={colliders.Length} | " +
                    $"renderers={blockoutRenderers.Length} | occupancy={occupancy:P1} | assets=absent");
                return;
            }

            ValidateLayerImporter(basePath, "base");
            var foregroundPath = FindLayerAssetPath(true);
            if (!string.IsNullOrEmpty(foregroundPath)) ValidateLayerImporter(foregroundPath, "foreground");
            Debug.Log(
                $"OFFICE_VISUAL_V2_ASSET_READY_PASS | base={basePath} | foreground={foregroundPath ?? "none"} | " +
                $"colliders={colliders.Length} | occupancy={occupancy:P1}");
        }

        public static string CaptureResolutionPair(string label)
        {
            var camera = Camera.main;
            Require(camera != null, "Office Visual V2 capture requires the main camera.");
            ValidateRuntimeFallbackOrVisual();
            Capture(camera, 1280, 720, label);
            return Capture(camera, 1920, 1080, label);
        }

        private static void ValidateRuntimeFallbackOrVisual()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<OfficeVisualV2Presenter>();
            Require(presenter != null, "Runtime OfficeVisualV2Presenter is missing.");
            var office = GameObject.Find("FAMILY OFFICE V0.2");
            Require(office != null && office.GetComponentsInChildren<Collider>(true).All(item => item.enabled),
                "Office colliders must remain enabled in both fallback and enhanced rendering.");
            var blockoutRenderers = office.GetComponentsInChildren<MeshRenderer>(true);
            if (presenter.HasBaseVisual)
            {
                Require(presenter.IsEnhancedPresentationActive,
                    "OfficeVisualV2 base loaded but enhanced presentation did not activate.");
                Require(blockoutRenderers.All(item => !item.enabled),
                    "Primitive renderers must be hidden while the enhanced office base is active.");
            }
            else
            {
                Require(!presenter.IsEnhancedPresentationActive,
                    "Enhanced presentation cannot be active without a base image.");
                Require(blockoutRenderers.All(item => item.enabled),
                    "Fallback blockout must remain visible when the base image is absent.");
            }
        }

        private static string FindLayerAssetPath(bool foreground)
        {
            if (!AssetDatabase.IsValidFolder(OfficeVisualV2AssetImporter.AssetFolder)) return null;
            return AssetDatabase.FindAssets("t:Texture2D", new[] { OfficeVisualV2AssetImporter.AssetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(item => item, StringComparer.Ordinal)
                .FirstOrDefault(item => IsLayerFileName(Path.GetFileNameWithoutExtension(item), foreground));
        }

        private static bool IsLayerFileName(string name, bool foreground)
        {
            var normalized = (name ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("guide")) return false;
            return foreground
                ? normalized.Contains("foreground") || normalized.EndsWith("_fg", StringComparison.Ordinal)
                : normalized.Contains("base") && !normalized.Contains("foreground");
        }

        private static void ValidateLayerImporter(string path, string label)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, $"Office {label} texture importer is missing: {path}");
            Require(importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point &&
                    !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.npotScale == TextureImporterNPOTScale.None,
                $"Office {label} importer is not Sprite/Single/Point/no-mip/uncompressed/NPOT-none: {path}");
        }

        private static string Capture(Camera camera, int width, int height, string label)
        {
            Directory.CreateDirectory(ArtifactFolder);
            var safeLabel = string.IsNullOrWhiteSpace(label) ? "capture" : label;
            var path = Path.GetFullPath($"{ArtifactFolder}/office-v2-{width}x{height}-{safeLabel}.png");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                ValidateOfficeCoverage(texture, camera.backgroundColor, width, height);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            Debug.Log($"OFFICE_VISUAL_V2_CAPTURE_PASS | {width}x{height} | {path} | bytes={new FileInfo(path).Length}");
            return path;
        }

        private static void ValidateOfficeCoverage(Texture2D texture, Color clearColor, int width, int height)
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<OfficeVisualV2Presenter>();
            if (presenter == null || !presenter.HasBaseVisual) return;

            var covered = 0;
            var sampled = 0;
            const int stride = 12;
            for (var y = stride / 2; y < height; y += stride)
            {
                for (var x = stride / 2; x < width; x += stride)
                {
                    var pixel = texture.GetPixel(x, y);
                    var distance = Mathf.Abs(pixel.r - clearColor.r) +
                                   Mathf.Abs(pixel.g - clearColor.g) +
                                   Mathf.Abs(pixel.b - clearColor.b);
                    if (distance > 0.12f) covered++;
                    sampled++;
                }
            }

            var coverage = covered / (float)Mathf.Max(1, sampled);
            Require(coverage >= 0.85f,
                $"Enhanced office covers only {coverage:P1} of the {width}x{height} frame.");
            Debug.Log($"OFFICE_VISUAL_V2_COVERAGE_PASS | {width}x{height} | nonClear={coverage:P1}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

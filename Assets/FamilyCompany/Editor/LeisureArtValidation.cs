using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Simulation.Leisure;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class LeisureArtValidation
    {
        public const string LeisureArtFolder = "Assets/Art/Leisure";
        public const int RequiredTextureCount = 12;
        public const int MinimumWidth = 1_600;
        public const int MinimumHeight = 900;
        public const int AspectToleranceBasisPoints = 200;

        [MenuItem("Family Company/Validate Leisure Art")]
        public static void Run()
        {
            try
            {
                var errors = new List<string>();
                ValidateCatalogAndTextures(errors);
                if (errors.Count > 0)
                {
                    for (var index = 0; index < errors.Count; index++)
                    {
                        Debug.LogError("LEISURE_ART_VALIDATION: " + errors[index]);
                    }

                    throw new InvalidOperationException(
                        $"Leisure art validation found {errors.Count} error(s). See the preceding error messages.");
                }

                Debug.Log($"FAMILY_COMPANY_LEISURE_ART_VALIDATION: PASS ({RequiredTextureCount}/{RequiredTextureCount})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_LEISURE_ART_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateCatalogAndTextures(List<string> errors)
        {
            var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
            var catalogIds = new HashSet<string>(StringComparer.Ordinal);
            var seenAssetGuids = new HashSet<string>(StringComparer.Ordinal);
            if (LeisureActivityCatalog.All.Count != RequiredTextureCount)
            {
                errors.Add(
                    $"LeisureActivityCatalog must contain exactly {RequiredTextureCount} activities; " +
                    $"found {LeisureActivityCatalog.All.Count}.");
            }

            for (var index = 0; index < LeisureActivityCatalog.All.Count; index++)
            {
                var activity = LeisureActivityCatalog.All[index];
                if (!catalogIds.Add(activity.Id))
                {
                    errors.Add("Duplicate LeisureActivityCatalog ID: " + activity.Id);
                    continue;
                }

                var expectedPath = ExpectedPath(activity.Id);
                if (!expectedPaths.Add(expectedPath))
                {
                    errors.Add("Multiple activity IDs resolve to the same leisure art path: " + expectedPath);
                    continue;
                }

                ValidateTexture(activity.Id, expectedPath, seenAssetGuids, errors);
            }

            var actualPaths = FindVersionOneLeisurePngPaths();
            foreach (var actualPath in actualPaths)
            {
                if (!expectedPaths.Contains(actualPath))
                {
                    errors.Add("Leisure v1 texture has no matching catalog activity ID: " + actualPath);
                }
            }

            foreach (var expectedPath in expectedPaths)
            {
                if (!actualPaths.Contains(expectedPath))
                {
                    errors.Add("Missing required leisure texture: " + expectedPath);
                }
            }

            if (actualPaths.Count != RequiredTextureCount)
            {
                errors.Add(
                    $"Expected exactly {RequiredTextureCount} leisure_*_v1.png textures in {LeisureArtFolder}; " +
                    $"found {actualPaths.Count}.");
            }
        }

        private static void ValidateTexture(
            string activityId,
            string assetPath,
            HashSet<string> seenAssetGuids,
            List<string> errors)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) return;

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetGuid))
            {
                errors.Add("Texture has no asset GUID: " + assetPath);
            }
            else if (!seenAssetGuids.Add(assetGuid))
            {
                errors.Add("Duplicate texture asset is mapped to more than one activity: " + assetPath);
            }

            if (texture.width <= texture.height)
            {
                errors.Add(
                    $"Portrait or square leisure image is forbidden: {assetPath} " +
                    $"({texture.width}x{texture.height}); a landscape 16:9 scene is required.");
            }

            if (texture.width < MinimumWidth || texture.height < MinimumHeight)
            {
                errors.Add(
                    $"Leisure image is below {MinimumWidth}x{MinimumHeight}: {assetPath} " +
                    $"({texture.width}x{texture.height}).");
            }

            if (!IsWithinSixteenByNineTolerance(texture.width, texture.height))
            {
                errors.Add(
                    $"Leisure image is outside the 16:9 ±{AspectToleranceBasisPoints / 100.0:0.##}% tolerance: " +
                    $"{assetPath} ({texture.width}x{texture.height}).");
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                errors.Add("TextureImporter is missing: " + assetPath);
                return;
            }

            if (importer.isReadable)
            {
                errors.Add("Leisure texture must not be CPU-readable: " + assetPath);
            }

            if (importer.textureType != TextureImporterType.Default)
            {
                errors.Add("Leisure scene texture must use TextureImporterType.Default: " + assetPath);
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                errors.Add("Leisure scene texture must use TextureImporterShape.Texture2D: " + assetPath);
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                errors.Add("Leisure texture must preserve its source dimensions with NPOT scale None: " + assetPath);
            }

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            var effectiveCompression = standalone.overridden
                ? standalone.textureCompression
                : importer.textureCompression;
            if (!IsAcceptedCompression(effectiveCompression))
            {
                errors.Add(
                    "Leisure texture must be Uncompressed or CompressedHQ for PC: " + assetPath +
                    " (effective setting: " + effectiveCompression + ").");
            }

            var effectiveMaxSize = standalone.overridden
                ? standalone.maxTextureSize
                : importer.maxTextureSize;
            var requiredMaxSize = Math.Max(texture.width, texture.height);
            if (effectiveMaxSize < requiredMaxSize)
            {
                errors.Add(
                    $"PC max texture size {effectiveMaxSize} is below imported width {requiredMaxSize}: {assetPath}");
            }

            if (standalone.overridden && standalone.crunchedCompression)
            {
                errors.Add("Crunched compression is not accepted for the PC leisure scene texture: " + assetPath);
            }

            var expectedFileName = "leisure_" + activityId + "_v1.png";
            if (!string.Equals(Path.GetFileName(assetPath), expectedFileName, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Activity-to-texture filename mismatch for {activityId}: expected {expectedFileName}, " +
                    $"found {Path.GetFileName(assetPath)}.");
            }
        }

        private static HashSet<string> FindVersionOneLeisurePngPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(LeisureArtFolder)) return paths;

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { LeisureArtFolder });
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith("leisure_", StringComparison.Ordinal) &&
                    fileName.EndsWith("_v1.png", StringComparison.Ordinal))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private static bool IsWithinSixteenByNineTolerance(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            var crossProductDifference = Math.Abs((long)width * 9 - (long)height * 16);
            var reference = (long)height * 16;
            return crossProductDifference * 10_000 <= reference * AspectToleranceBasisPoints;
        }

        private static bool IsAcceptedCompression(TextureImporterCompression compression)
        {
            return compression == TextureImporterCompression.Uncompressed ||
                   compression == TextureImporterCompression.CompressedHQ;
        }

        private static string ExpectedPath(string activityId)
        {
            return LeisureArtFolder + "/leisure_" + activityId + "_v1.png";
        }
    }
}

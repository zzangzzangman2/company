using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Guards the canonical NorthWest chair foreground independently of wall/door validation.
    /// The mask is a strict subset of the complete chair Sprite and must survive a furniture rebuild.
    /// </summary>
    public static class OfficeChairForegroundValidation
    {
        private const string ChairBasePath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_v3.png";
        private const string ChairFrontPath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_front_v3.png";
        private const string ChairFrontGuid = "765e8e592ac1dbe46a89bf68ff564944";
        private const int ForegroundMinimumRuntimeX = 317;
        private const int ForegroundMinimumRuntimeY = 98;
        private const int ExpectedForegroundPixelCount = 9881;

        [MenuItem("Family Company/Validate Office Chair Foreground Integrity")]
        public static void Validate()
        {
            OfficeFurnitureVisualCatalog catalog = OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog();
            OfficeFurnitureVisualDefinition chair = catalog.Resolve(
                OfficeGridLayouts.SwivelChairKind,
                OfficeFurnitureFacing.NorthWest);
            Sprite expectedBase = RequiredSprite(ChairBasePath);
            Sprite expectedFront = RequiredSprite(ChairFrontPath);

            Require(chair.BaseSprite == expectedBase, "Canonical chair base Sprite is not catalogued.");
            Require(chair.FrontOverlaySprite == expectedFront,
                "Canonical chair foreground Sprite is not catalogued.");
            Require(chair.FrontOverlayWhenOccupied,
                "Canonical chair foreground is not enabled for an occupied seat.");
            Require(string.Equals(AssetDatabase.AssetPathToGUID(ChairFrontPath), ChairFrontGuid,
                    StringComparison.OrdinalIgnoreCase),
                "Canonical chair foreground GUID changed.");
            Require(expectedFront.rect.size == expectedBase.rect.size,
                "Chair foreground canvas differs from the chair base.");
            Require(Mathf.Approximately(expectedFront.pixelsPerUnit, expectedBase.pixelsPerUnit),
                "Chair foreground PPU differs from the chair base.");
            Require(Vector2.Distance(expectedFront.pivot, expectedBase.pivot) <= 0.01f,
                "Chair foreground pivot differs from the chair base.");

            ValidateLimitedSubset(expectedBase);
            Debug.Log(
                $"OFFICE_CHAIR_FOREGROUND_VALIDATION: PASS sourcePixels={ExpectedForegroundPixelCount} " +
                $"lowerOccluderPixels={OfficeOccupiedChairForegroundRules.ExpectedOpaquePixelCount} " +
                $"cutoff=({ForegroundMinimumRuntimeX},{ForegroundMinimumRuntimeY}) " +
                "pivotAlignment=exact catalog=linked occupiedMode=canonical-continuous");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateLimitedSubset(Sprite expectedBase)
        {
            Texture2D chairBase = ReadTexture(ChairBasePath);
            Texture2D chairFront = ReadTexture(ChairFrontPath);
            try
            {
                Require(chairBase.width == OfficeFurnitureAssetBuilder.CanvasWidth &&
                        chairBase.height == OfficeFurnitureAssetBuilder.CanvasHeight,
                    "Canonical chair base canvas is invalid.");
                Require(chairFront.width == chairBase.width && chairFront.height == chairBase.height,
                    "Chair foreground canvas differs from the chair base texture.");

                Color32[] basePixels = chairBase.GetPixels32();
                Color32[] frontPixels = chairFront.GetPixels32();
                var visibleForegroundPixels = 0;
                for (var y = 0; y < chairBase.height; y++)
                for (var x = 0; x < chairBase.width; x++)
                {
                    int index = y * chairBase.width + x;
                    Color32 basePixel = basePixels[index];
                    Color32 frontPixel = frontPixels[index];
                    bool expectedForeground = basePixel.a > 0 &&
                                               x >= ForegroundMinimumRuntimeX &&
                                               y >= ForegroundMinimumRuntimeY;
                    bool actualForeground = frontPixel.a > 0;
                    if (actualForeground != expectedForeground)
                    {
                        throw new InvalidOperationException(
                            $"Chair foreground mask mismatch at runtime pixel ({x},{y}).");
                    }

                    if (!actualForeground) continue;
                    visibleForegroundPixels++;
                    if (frontPixel.r != basePixel.r || frontPixel.g != basePixel.g ||
                        frontPixel.b != basePixel.b || frontPixel.a != basePixel.a)
                    {
                        throw new InvalidOperationException(
                            $"Chair foreground is not an exact base subset at runtime pixel ({x},{y}).");
                    }
                }

                Require(visibleForegroundPixels == ExpectedForegroundPixelCount,
                    $"Chair foreground pixel count {visibleForegroundPixels} != {ExpectedForegroundPixelCount}.");
                var lowerOccluderPixels = 0;
                for (var y = 0; y < chairBase.height; y++)
                for (var x = 0; x < chairBase.width; x++)
                {
                    int index = y * chairBase.width + x;
                    if (basePixels[index].a > 0 &&
                        OfficeOccupiedChairForegroundRules.IncludesSourcePixel(x, y))
                        lowerOccluderPixels++;
                }
                Require(
                    lowerOccluderPixels == OfficeOccupiedChairForegroundRules.ExpectedOpaquePixelCount,
                    $"Chair lower occluder pixel count {lowerOccluderPixels} != " +
                    $"{OfficeOccupiedChairForegroundRules.ExpectedOpaquePixelCount}.");
                ValidateLowerOccluderAlignment(expectedBase);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chairBase);
                UnityEngine.Object.DestroyImmediate(chairFront);
            }
        }

        private static void ValidateLowerOccluderAlignment(Sprite baseSprite)
        {
            Rect crop = OfficeOccupiedChairForegroundRules.TextureRect(baseSprite);
            Vector2 normalizedPivot = OfficeOccupiedChairForegroundRules.NormalizedPivot(baseSprite);
            Vector3 localPosition = OfficeOccupiedChairForegroundRules.LocalPosition(baseSprite);
            var croppedPivotPx = new Vector2(
                normalizedPivot.x * crop.width,
                normalizedPivot.y * crop.height);
            Vector2[] samples =
            {
                new Vector2(OfficeOccupiedChairForegroundRules.MinimumSourceX,
                    OfficeOccupiedChairForegroundRules.MinimumSourceY),
                new Vector2(baseSprite.rect.width - 1f,
                    OfficeOccupiedChairForegroundRules.MaximumSourceY),
                new Vector2(baseSprite.pivot.x,
                    OfficeOccupiedChairForegroundRules.MinimumSourceY)
            };
            foreach (Vector2 sourcePixel in samples)
            {
                Vector2 canonicalLocal = (sourcePixel - baseSprite.pivot) / baseSprite.pixelsPerUnit;
                Vector2 croppedPixel = sourcePixel - new Vector2(
                    OfficeOccupiedChairForegroundRules.MinimumSourceX,
                    OfficeOccupiedChairForegroundRules.MinimumSourceY);
                Vector2 croppedLocal =
                    (croppedPixel - croppedPivotPx) / baseSprite.pixelsPerUnit +
                    new Vector2(localPosition.x, localPosition.y);
                Require(
                    Vector2.Distance(canonicalLocal, croppedLocal) <= 0.000001f,
                    $"Chair lower occluder pivot drifted at source pixel {sourcePixel}.");
            }
        }

        private static Sprite RequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException("Chair Sprite is missing.", path);
            return sprite;
        }

        private static Texture2D ReadTexture(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Chair texture is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (texture.LoadImage(File.ReadAllBytes(path), false)) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException("Could not decode chair texture: " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

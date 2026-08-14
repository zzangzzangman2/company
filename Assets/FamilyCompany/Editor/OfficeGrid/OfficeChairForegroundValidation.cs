using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Guards the open-back NorthWest chair independently of wall/door validation. The foreground
    /// is a strict lower-body subset; the back-post gap must remain visibly open after every rebuild.
    /// </summary>
    public static class OfficeChairForegroundValidation
    {
        private const string ChairBasePath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_v4.png";
        private const string ChairFrontPath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_front_v4.png";
        private const string ChairFrontGuid = "ce3ae497c54a66742a5f58cbd32522ac";
        private const int ForegroundMinimumRuntimeX = 267;
        private const int ForegroundMaximumRuntimeX = 371;
        private const int ForegroundMinimumRuntimeY = 25;
        private const int ForegroundMaximumRuntimeY = 93;
        private const int ExpectedForegroundPixelCount = 4161;
        private const int OpenGapMinimumRuntimeX = 326;
        private const int OpenGapMaximumRuntimeX = 356;
        private const int OpenGapMinimumRuntimeY = 140;
        private const int OpenGapMaximumRuntimeY = 195;

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

            ValidateOpenBackSubset(expectedBase);
            Debug.Log(
                $"OFFICE_CHAIR_FOREGROUND_VALIDATION: PASS sourcePixels={ExpectedForegroundPixelCount} " +
                $"lowerOccluderPixels=" +
                $"{OfficeSeatedUpperBodyProtectionRules.ExpectedChairLowerOpaquePixelCount} " +
                $"frontBounds=({ForegroundMinimumRuntimeX},{ForegroundMinimumRuntimeY})-" +
                $"({ForegroundMaximumRuntimeX},{ForegroundMaximumRuntimeY}) " +
                "openGap=31x56 pivotAlignment=exact catalog=linked occupiedMode=lower-only " +
                "upperBodyProtection=pose-pelvis-split");
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

        private static void ValidateOpenBackSubset(Sprite expectedBase)
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
                var foregroundMinX = chairBase.width;
                var foregroundMaxX = -1;
                var foregroundMinY = chairBase.height;
                var foregroundMaxY = -1;
                for (var y = 0; y < chairBase.height; y++)
                for (var x = 0; x < chairBase.width; x++)
                {
                    int index = y * chairBase.width + x;
                    Color32 basePixel = basePixels[index];
                    Color32 frontPixel = frontPixels[index];
                    bool actualForeground = frontPixel.a > 0;
                    if (!actualForeground) continue;
                    visibleForegroundPixels++;
                    foregroundMinX = Mathf.Min(foregroundMinX, x);
                    foregroundMaxX = Mathf.Max(foregroundMaxX, x);
                    foregroundMinY = Mathf.Min(foregroundMinY, y);
                    foregroundMaxY = Mathf.Max(foregroundMaxY, y);
                    if (frontPixel.r != basePixel.r || frontPixel.g != basePixel.g ||
                        frontPixel.b != basePixel.b || frontPixel.a != basePixel.a)
                    {
                        throw new InvalidOperationException(
                            $"Chair foreground is not an exact base subset at runtime pixel ({x},{y}).");
                    }
                }

                Require(visibleForegroundPixels == ExpectedForegroundPixelCount,
                    $"Chair foreground pixel count {visibleForegroundPixels} != {ExpectedForegroundPixelCount}.");
                Require(
                    foregroundMinX == ForegroundMinimumRuntimeX &&
                    foregroundMaxX == ForegroundMaximumRuntimeX &&
                    foregroundMinY == ForegroundMinimumRuntimeY &&
                    foregroundMaxY == ForegroundMaximumRuntimeY,
                    $"Chair foreground bounds changed: ({foregroundMinX},{foregroundMinY})-" +
                    $"({foregroundMaxX},{foregroundMaxY}).");
                for (var y = OpenGapMinimumRuntimeY; y <= OpenGapMaximumRuntimeY; y++)
                for (var x = OpenGapMinimumRuntimeX; x <= OpenGapMaximumRuntimeX; x++)
                {
                    Require(
                        basePixels[y * chairBase.width + x].a == 0,
                        $"Open chair back was filled at runtime pixel ({x},{y}).");
                }
                var lowerOccluderPixels = 0;
                for (var y = 0; y < chairBase.height; y++)
                for (var x = 0; x < chairBase.width; x++)
                {
                    int index = y * chairBase.width + x;
                    if (basePixels[index].a > 0 &&
                        OfficeSeatedUpperBodyProtectionRules.IncludesChairLowerSourcePixel(x, y))
                        lowerOccluderPixels++;
                }
                Require(
                    lowerOccluderPixels ==
                    OfficeSeatedUpperBodyProtectionRules.ExpectedChairLowerOpaquePixelCount,
                    $"Chair lower occluder pixel count {lowerOccluderPixels} != " +
                    $"{OfficeSeatedUpperBodyProtectionRules.ExpectedChairLowerOpaquePixelCount}.");
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
            Rect crop = OfficeSeatedUpperBodyProtectionRules.ChairLowerTextureRect(baseSprite);
            Vector2 normalizedPivot =
                OfficeSeatedUpperBodyProtectionRules.ChairLowerNormalizedPivot(baseSprite);
            Vector3 localPosition =
                OfficeSeatedUpperBodyProtectionRules.ChairLowerLocalPosition(baseSprite);
            var croppedPivotPx = new Vector2(
                normalizedPivot.x * crop.width,
                normalizedPivot.y * crop.height);
            Vector2[] samples =
            {
                new Vector2(
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMinimumSourceX,
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMinimumSourceY),
                new Vector2(
                    baseSprite.rect.width - 1f,
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMaximumSourceY),
                new Vector2(
                    baseSprite.pivot.x,
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMinimumSourceY)
            };
            foreach (Vector2 sourcePixel in samples)
            {
                Vector2 canonicalLocal =
                    (sourcePixel - baseSprite.pivot) / baseSprite.pixelsPerUnit;
                Vector2 croppedPixel = sourcePixel - new Vector2(
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMinimumSourceX,
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerMinimumSourceY);
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

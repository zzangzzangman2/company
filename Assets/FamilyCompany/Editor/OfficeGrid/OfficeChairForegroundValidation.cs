using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Guards the authored NorthWest chair foreground independently of wall/door validation.
    /// The mask contains the complete curved seat-front rim; it is never inferred from an x/y
    /// canvas crop at runtime.
    /// </summary>
    public static class OfficeChairForegroundValidation
    {
        private const string ChairBasePath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_v3.png";
        private const string ChairFrontPath =
            "Assets/Art/Office/Tiles/Furniture/Runtime/office_swivel_chair_front_v3.png";
        private const string ChairBaseGuid = "5d642bde97f1a844f9aefcc7c4c95a08";
        private const string ChairFrontGuid = "765e8e592ac1dbe46a89bf68ff564944";
        private const string ExpectedBaseFileSha256 =
            "ABD07ED0AF918A35107D139B5164A0BAF8BB5069BF512DF9588D10DF85D176CB";
        private const string ExpectedForegroundFileSha256 =
            "7E79AB5E0071629E60D794D122FD97688EF429328DE07FF40DA88FC68B4FA8EB";
        private const int ExpectedBaseOpaquePixelCount = 23428;
        private const int ExpectedForegroundPixelCount = 1841;
        private const int LongStraightCutThresholdPx = 20;
        private const int ExpectedMaximumVerticalInternalRunPx = 29;
        private const int ExpectedMaximumHorizontalInternalRunPx = 16;
        private const int ExpectedMaximumVerticalArtificialRunPx = 1;
        private const int ExpectedMaximumHorizontalArtificialRunPx = 4;

        // PIL/top-down source coordinates. These authored contours name physical chair parts;
        // unlike the removed x>=317/y<=413 crop, they are not used to construct the mask.
        private static readonly Vector2[] NearArmContour =
        {
            new Vector2(227, 324), new Vector2(251, 324), new Vector2(267, 333),
            new Vector2(301, 345), new Vector2(304, 357), new Vector2(297, 369),
            new Vector2(285, 372), new Vector2(284, 392), new Vector2(278, 402),
            new Vector2(260, 401), new Vector2(252, 389), new Vector2(252, 376),
            new Vector2(238, 371), new Vector2(226, 355)
        };

        private static readonly Vector2[] FarArmContour =
        {
            new Vector2(304, 284), new Vector2(327, 283), new Vector2(364, 296),
            new Vector2(364, 308), new Vector2(348, 316), new Vector2(329, 312),
            new Vector2(326, 325), new Vector2(314, 325), new Vector2(312, 308),
            new Vector2(304, 304)
        };

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
            Require(string.Equals(AssetDatabase.AssetPathToGUID(ChairBasePath), ChairBaseGuid,
                    StringComparison.OrdinalIgnoreCase),
                "Canonical chair base GUID changed.");
            Require(string.Equals(AssetDatabase.AssetPathToGUID(ChairFrontPath), ChairFrontGuid,
                    StringComparison.OrdinalIgnoreCase),
                "Canonical chair foreground GUID changed.");
            Require(expectedFront.rect.size == expectedBase.rect.size,
                "Chair foreground canvas differs from the chair base.");
            Require(Mathf.Approximately(expectedFront.pixelsPerUnit, expectedBase.pixelsPerUnit),
                "Chair foreground PPU differs from the chair base.");
            Require(Vector2.Distance(expectedFront.pivot, expectedBase.pivot) <= 0.01f,
                "Chair foreground pivot differs from the chair base.");
            Require(string.Equals(FileSha256(ChairBasePath), ExpectedBaseFileSha256,
                    StringComparison.Ordinal),
                "Canonical chair base file hash changed without foreground re-approval.");
            Require(string.Equals(FileSha256(ChairFrontPath), ExpectedForegroundFileSha256,
                    StringComparison.Ordinal),
                "Canonical authored chair foreground file hash changed.");

            AuthoredMaskMetrics metrics = ValidateAuthoredSubset();
            Debug.Log(
                "OFFICE_CHAIR_FOREGROUND_VALIDATION: PASS " +
                $"sourcePixels={metrics.ForegroundPixels} sourceSha256={ExpectedForegroundFileSha256} " +
                $"components={metrics.ComponentCount} longStraightCut20px=0 " +
                $"maxNaturalContour={metrics.MaximumVerticalRun}/{metrics.MaximumHorizontalRun} " +
                $"maxArtificialAxisSeam={metrics.MaximumVerticalArtificialRun}/" +
                $"{metrics.MaximumHorizontalArtificialRun} " +
                $"farNorthArmForeground={metrics.FarArmPixels} mechanismForeground={metrics.MechanismPixels} " +
                "mask=authored-curved-seat-rim pivotAlignment=exact catalog=linked " +
                "occupiedMode=canonical-continuous upperBodyProtection=pose-pelvis-split");
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

        private static AuthoredMaskMetrics ValidateAuthoredSubset()
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
                var baseOpaquePixels = 0;
                var foregroundPixels = 0;
                var farArmPixels = 0;
                var mechanismPixels = 0;
                var selected = new bool[frontPixels.Length];
                var baseOpaque = new bool[basePixels.Length];
                for (var y = 0; y < chairBase.height; y++)
                for (var x = 0; x < chairBase.width; x++)
                {
                    int index = y * chairBase.width + x;
                    Color32 basePixel = basePixels[index];
                    Color32 frontPixel = frontPixels[index];
                    bool hasBase = basePixel.a > 0;
                    bool hasForeground = frontPixel.a > 0;
                    baseOpaque[index] = hasBase;
                    selected[index] = hasForeground;
                    if (hasBase) baseOpaquePixels++;
                    if (!hasForeground) continue;
                    foregroundPixels++;
                    Require(hasBase,
                        $"Chair foreground escaped the base silhouette at runtime pixel ({x},{y}).");
                    Require(frontPixel.r == basePixel.r && frontPixel.g == basePixel.g &&
                            frontPixel.b == basePixel.b && frontPixel.a == basePixel.a,
                        $"Chair foreground is not an exact base subset at runtime pixel ({x},{y}).");

                    int topDownY = chairBase.height - 1 - y;
                    var sourcePoint = new Vector2(x + 0.5f, topDownY + 0.5f);
                    bool upholstery = IsUpholstery(basePixel);
                    bool nearArm = PointInPolygon(sourcePoint, NearArmContour);
                    if (PointInPolygon(sourcePoint, FarArmContour) && !upholstery)
                        farArmPixels++;
                    // Every wheel, caster, stem, and under-seat mechanism pixel is
                    // below the authored seat-front contour in the canonical art.
                    // Do not mistake the rim's dark right-hand upholstery outline
                    // for the rear support merely because its x coordinate is high.
                    if (!upholstery && !nearArm && topDownY >= 414)
                        mechanismPixels++;
                }

                Require(baseOpaquePixels == ExpectedBaseOpaquePixelCount,
                    $"Chair base opaque pixel count {baseOpaquePixels} != {ExpectedBaseOpaquePixelCount}.");
                Require(foregroundPixels == ExpectedForegroundPixelCount,
                    $"Chair foreground pixel count {foregroundPixels} != {ExpectedForegroundPixelCount}.");
                Require(farArmPixels == 0,
                    $"Far/north arm leaked onto the foreground plane: {farArmPixels}px.");
                Require(mechanismPixels == 0,
                    $"Stem/wheel/back-support pixels leaked onto the foreground plane: {mechanismPixels}px.");

                int components = CountConnectedComponents(selected, chairBase.width, chairBase.height);
                Require(components == 1,
                    $"Authored chair foreground split into {components} connected components.");
                int maximumVertical = MaximumInternalBoundaryRun(
                    selected, baseOpaque, chairBase.width, chairBase.height, true);
                int maximumHorizontal = MaximumInternalBoundaryRun(
                    selected, baseOpaque, chairBase.width, chairBase.height, false);
                int maximumArtificialVertical = MaximumArtificialBoundaryRun(
                    selected, baseOpaque, basePixels, chairBase.width, chairBase.height, true);
                int maximumArtificialHorizontal = MaximumArtificialBoundaryRun(
                    selected, baseOpaque, basePixels, chairBase.width, chairBase.height, false);
                Require(maximumVertical == ExpectedMaximumVerticalInternalRunPx,
                    $"Vertical authored contour run {maximumVertical} != " +
                    $"{ExpectedMaximumVerticalInternalRunPx}.");
                Require(maximumHorizontal == ExpectedMaximumHorizontalInternalRunPx,
                    $"Horizontal authored contour run {maximumHorizontal} != " +
                    $"{ExpectedMaximumHorizontalInternalRunPx}.");
                Require(maximumArtificialVertical == ExpectedMaximumVerticalArtificialRunPx,
                    $"Vertical artificial contour run {maximumArtificialVertical} != " +
                    $"{ExpectedMaximumVerticalArtificialRunPx}.");
                Require(maximumArtificialHorizontal == ExpectedMaximumHorizontalArtificialRunPx,
                    $"Horizontal artificial contour run {maximumArtificialHorizontal} != " +
                    $"{ExpectedMaximumHorizontalArtificialRunPx}.");
                Require(maximumArtificialVertical < LongStraightCutThresholdPx &&
                        maximumArtificialHorizontal < LongStraightCutThresholdPx,
                    "An internal long straight crop boundary reappeared in the chair foreground.");

                // Explicitly reject the removed rectangle even if its constants are reintroduced
                // elsewhere under a different name.
                Require(!selected[ToIndex(250, chairBase.height - 1 - 340, chairBase.width)],
                    "Near/south arm incorrectly moved in front of the seated actor.");
                Require(!selected[ToIndex(342, chairBase.height - 1 - 288, chairBase.width)],
                    "Far/north arm reappeared in the authored foreground.");
                Require(selected[ToIndex(300, chairBase.height - 1 - 380, chairBase.width)],
                    "The authored curved seat-front rim is absent.");

                return new AuthoredMaskMetrics(
                    foregroundPixels,
                    components,
                    maximumVertical,
                    maximumHorizontal,
                    maximumArtificialVertical,
                    maximumArtificialHorizontal,
                    farArmPixels,
                    mechanismPixels);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chairBase);
                UnityEngine.Object.DestroyImmediate(chairFront);
            }
        }

        private static int CountConnectedComponents(bool[] selected, int width, int height)
        {
            var visited = new bool[selected.Length];
            var pending = new Queue<int>();
            var components = 0;
            for (var index = 0; index < selected.Length; index++)
            {
                if (!selected[index] || visited[index]) continue;
                components++;
                visited[index] = true;
                pending.Enqueue(index);
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    int x = current % width;
                    int y = current / width;
                    for (var offsetY = -1; offsetY <= 1; offsetY++)
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighbourX = x + offsetX;
                        int neighbourY = y + offsetY;
                        if (neighbourX < 0 || neighbourY < 0 ||
                            neighbourX >= width || neighbourY >= height) continue;
                        int neighbour = ToIndex(neighbourX, neighbourY, width);
                        if (!selected[neighbour] || visited[neighbour]) continue;
                        visited[neighbour] = true;
                        pending.Enqueue(neighbour);
                    }
                }
            }
            return components;
        }

        private static int MaximumInternalBoundaryRun(
            bool[] selected,
            bool[] baseOpaque,
            int width,
            int height,
            bool vertical)
        {
            int outer = vertical ? width : height;
            int inner = vertical ? height : width;
            var maximum = 0;
            for (var fixedCoordinate = 0; fixedCoordinate < outer; fixedCoordinate++)
            {
                var run = 0;
                for (var changingCoordinate = 0; changingCoordinate < inner; changingCoordinate++)
                {
                    int x = vertical ? fixedCoordinate : changingCoordinate;
                    int y = vertical ? changingCoordinate : fixedCoordinate;
                    int index = ToIndex(x, y, width);
                    bool internalBoundary = selected[index] && HasBaseOnlyNeighbour(
                        selected, baseOpaque, width, height, x, y, vertical);
                    run = internalBoundary ? run + 1 : 0;
                    maximum = Math.Max(maximum, run);
                }
            }
            return maximum;
        }

        private static int MaximumArtificialBoundaryRun(
            bool[] selected,
            bool[] baseOpaque,
            IReadOnlyList<Color32> basePixels,
            int width,
            int height,
            bool vertical)
        {
            int outer = vertical ? width : height;
            int inner = vertical ? height : width;
            var maximum = 0;
            for (var fixedCoordinate = 0; fixedCoordinate < outer; fixedCoordinate++)
            {
                var run = 0;
                for (var changingCoordinate = 0; changingCoordinate < inner; changingCoordinate++)
                {
                    int x = vertical ? fixedCoordinate : changingCoordinate;
                    int y = vertical ? changingCoordinate : fixedCoordinate;
                    int index = ToIndex(x, y, width);
                    Color32 source = basePixels[index];
                    bool sourceIsPartOutline =
                        Math.Max(source.r, Math.Max(source.g, source.b)) < 48;
                    bool artificialBoundary = selected[index] && !sourceIsPartOutline &&
                                              HasBaseOnlyNeighbour(
                                                  selected, baseOpaque, width, height,
                                                  x, y, vertical);
                    run = artificialBoundary ? run + 1 : 0;
                    maximum = Math.Max(maximum, run);
                }
            }
            return maximum;
        }

        private static bool HasBaseOnlyNeighbour(
            bool[] selected,
            bool[] baseOpaque,
            int width,
            int height,
            int x,
            int y,
            bool vertical)
        {
            int firstX = vertical ? x - 1 : x;
            int firstY = vertical ? y : y - 1;
            int secondX = vertical ? x + 1 : x;
            int secondY = vertical ? y : y + 1;
            return IsBaseOnly(firstX, firstY, selected, baseOpaque, width, height) ||
                   IsBaseOnly(secondX, secondY, selected, baseOpaque, width, height);
        }

        private static bool IsBaseOnly(
            int x,
            int y,
            bool[] selected,
            bool[] baseOpaque,
            int width,
            int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            int index = ToIndex(x, y, width);
            return baseOpaque[index] && !selected[index];
        }

        private static int ToIndex(int x, int y, int width) => y * width + x;

        private static bool IsUpholstery(Color32 pixel) =>
            pixel.a > 0 && pixel.g >= 42 &&
            pixel.g >= pixel.r + 10 && pixel.g >= pixel.b + 8;

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            var inside = false;
            for (int current = 0, previous = polygon.Count - 1;
                 current < polygon.Count;
                 previous = current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                               point.x < (b.x - a.x) * (point.y - a.y) /
                               (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static string FileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(File.ReadAllBytes(path));
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) builder.Append(value.ToString("X2"));
                return builder.ToString();
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

        private readonly struct AuthoredMaskMetrics
        {
            public AuthoredMaskMetrics(
                int foregroundPixels,
                int componentCount,
                int maximumVerticalRun,
                int maximumHorizontalRun,
                int maximumVerticalArtificialRun,
                int maximumHorizontalArtificialRun,
                int farArmPixels,
                int mechanismPixels)
            {
                ForegroundPixels = foregroundPixels;
                ComponentCount = componentCount;
                MaximumVerticalRun = maximumVerticalRun;
                MaximumHorizontalRun = maximumHorizontalRun;
                MaximumVerticalArtificialRun = maximumVerticalArtificialRun;
                MaximumHorizontalArtificialRun = maximumHorizontalArtificialRun;
                FarArmPixels = farArmPixels;
                MechanismPixels = mechanismPixels;
            }

            public int ForegroundPixels { get; }
            public int ComponentCount { get; }
            public int MaximumVerticalRun { get; }
            public int MaximumHorizontalRun { get; }
            public int MaximumVerticalArtificialRun { get; }
            public int MaximumHorizontalArtificialRun { get; }
            public int FarArmPixels { get; }
            public int MechanismPixels { get; }
        }
    }
}

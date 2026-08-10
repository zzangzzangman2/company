using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Converts the authored office XZ plane to the 1920x1080 OfficeVisualV2 art plane.
    /// The four desk centers are the shared control points visible in the development guide.
    /// </summary>
    public static class OfficeVisualV2Calibration
    {
        public const float ArtWidth = 1920f;
        public const float ArtHeight = 1080f;
        public const float ArtCropLeft = 254f;
        public const float ArtCropTop = 79f;
        public const float ArtCropWidth = 1666f;
        public const float ArtCropHeight = 937f;
        public const float ArtCropAspect = ArtCropWidth / ArtCropHeight;

        public static readonly Vector2 DeskAWorldCenter = new Vector2(11.4f, 4.4f);
        public static readonly Vector2 DeskBWorldCenter = new Vector2(14.6f, 4.4f);
        public static readonly Vector2 DeskCWorldCenter = new Vector2(11.4f, -2.3f);
        public static readonly Vector2 DeskDWorldCenter = new Vector2(14.6f, -2.3f);

        public static readonly Vector2 DeskAArtCenter = new Vector2(813.5f, 376.5f);
        public static readonly Vector2 DeskBArtCenter = new Vector2(1103.5f, 377.5f);
        public static readonly Vector2 DeskCArtCenter = new Vector2(800.5f, 702f);
        public static readonly Vector2 DeskDArtCenter = new Vector2(1105.5f, 703f);

        public static readonly Rect DeskAArtBounds = Rect.MinMaxRect(697f, 329f, 931f, 425f);
        public static readonly Rect DeskBArtBounds = Rect.MinMaxRect(974f, 330f, 1234f, 426f);
        public static readonly Rect DeskCArtBounds = Rect.MinMaxRect(671f, 626f, 931f, 779f);
        public static readonly Rect DeskDArtBounds = Rect.MinMaxRect(976f, 627f, 1236f, 780f);

        // Independently measured canonical anchors. Standing work uses approach, never sit.
        public static readonly Vector2 DeskAApproachArt = new Vector2(814f, 500f);
        public static readonly Vector2 DeskBApproachArt = new Vector2(1103f, 500f);
        public static readonly Vector2 DeskCApproachArt = new Vector2(650f, 820f);
        public static readonly Vector2 DeskDApproachArt = new Vector2(1105f, 850f);
        public static readonly Vector2 DeskASitArt = new Vector2(814f, 416f);
        public static readonly Vector2 DeskBSitArt = new Vector2(1103f, 417f);
        public static readonly Vector2 DeskCSitArt = new Vector2(800f, 770f);
        public static readonly Vector2 DeskDSitArt = new Vector2(1105f, 771f);
        public static readonly Vector2 DeskAMonitorArt = new Vector2(816f, 223f);
        public static readonly Vector2 DeskBMonitorArt = new Vector2(1085f, 224f);
        public static readonly Vector2 DeskCMonitorArt = new Vector2(801f, 578f);
        public static readonly Vector2 DeskDMonitorArt = new Vector2(1102f, 579f);
        public static readonly Vector2 PrinterArt = new Vector2(600f, 400f);
        public static readonly Vector2 MeetingArt = new Vector2(1325f, 670f);
        public static readonly Vector2 ReceptionArt = new Vector2(1050f, 910f);
        public static readonly Vector2 ExitArt = new Vector2(348f, 310f);

        // The guide cross at (1615,925) is the coffee-table center, not a standing point.
        // The safe lounge foot point is on the clear floor immediately left of the sofa/table.
        public static readonly Vector2 LoungeFurnitureCenterArt = new Vector2(1615f, 925f);
        public static readonly Vector2 LoungeSafeArt = new Vector2(1325f, 850f);

        public static readonly Vector2 CorridorWestArt = new Vector2(620f, 493f);
        public static readonly Vector2 CorridorCenterArt = new Vector2(1080f, 493f);
        public static readonly Vector2 CorridorEastArt = new Vector2(1450f, 575f);

        private static readonly Vector2[] WorldControlPoints =
        {
            DeskAWorldCenter,
            DeskBWorldCenter,
            DeskCWorldCenter,
            DeskDWorldCenter
        };

        private static readonly Vector2[] ArtControlPoints =
        {
            DeskAArtCenter,
            DeskBArtCenter,
            DeskCArtCenter,
            DeskDArtCenter
        };

        private static readonly double[] WorldToArt = SolveHomography(WorldControlPoints, ArtControlPoints);
        private static readonly double[] ArtToWorld = SolveHomography(ArtControlPoints, WorldControlPoints);

        public static Vector2 WorldToArtPixel(Vector3 worldPosition)
        {
            return Project(WorldToArt, new Vector2(worldPosition.x, worldPosition.z));
        }

        public static Vector2 WorldToArtPixel(Vector2 worldXZ)
        {
            return Project(WorldToArt, worldXZ);
        }

        public static Vector3 ArtPixelToWorld(Vector2 artPixel, float worldY = 0.05f)
        {
            var worldXZ = Project(ArtToWorld, artPixel);
            return new Vector3(worldXZ.x, worldY, worldXZ.y);
        }

        public static Vector2 ArtPixelToViewport(Vector2 artPixel, float cameraAspect)
        {
            var safeAspect = Mathf.Max(0.01f, cameraAspect);
            var targetWidthFraction = ArtCropAspect / safeAspect;
            return new Vector2(
                0.5f + ((artPixel.x - ArtCropLeft) / ArtCropWidth - 0.5f) * targetWidthFraction,
                1f - (artPixel.y - ArtCropTop) / ArtCropHeight);
        }

        public static Vector2 ViewportToArtPixel(Vector2 viewport, float cameraAspect)
        {
            var safeAspect = Mathf.Max(0.01f, cameraAspect);
            var artU = 0.5f + (viewport.x - 0.5f) * safeAspect / ArtCropAspect;
            return new Vector2(
                ArtCropLeft + artU * ArtCropWidth,
                ArtCropTop + (1f - viewport.y) * ArtCropHeight);
        }

        public static float MaximumControlPointErrorPixels()
        {
            var maximum = 0f;
            for (var index = 0; index < WorldControlPoints.Length; index++)
            {
                maximum = Mathf.Max(
                    maximum,
                    Vector2.Distance(WorldToArtPixel(WorldControlPoints[index]), ArtControlPoints[index]));
            }

            return maximum;
        }

        private static Vector2 Project(double[] matrix, Vector2 point)
        {
            var denominator = matrix[6] * point.x + matrix[7] * point.y + 1d;
            if (Math.Abs(denominator) < 1e-9d)
                throw new InvalidOperationException("OfficeVisualV2 calibration produced a singular projection.");
            return new Vector2(
                (float)((matrix[0] * point.x + matrix[1] * point.y + matrix[2]) / denominator),
                (float)((matrix[3] * point.x + matrix[4] * point.y + matrix[5]) / denominator));
        }

        private static double[] SolveHomography(Vector2[] source, Vector2[] destination)
        {
            if (source == null || destination == null || source.Length != 4 || destination.Length != 4)
                throw new ArgumentException("A homography requires exactly four source and destination points.");

            var augmented = new double[8, 9];
            for (var index = 0; index < 4; index++)
            {
                var x = source[index].x;
                var y = source[index].y;
                var u = destination[index].x;
                var v = destination[index].y;
                var first = index * 2;
                var second = first + 1;

                augmented[first, 0] = x;
                augmented[first, 1] = y;
                augmented[first, 2] = 1d;
                augmented[first, 6] = -u * x;
                augmented[first, 7] = -u * y;
                augmented[first, 8] = u;

                augmented[second, 3] = x;
                augmented[second, 4] = y;
                augmented[second, 5] = 1d;
                augmented[second, 6] = -v * x;
                augmented[second, 7] = -v * y;
                augmented[second, 8] = v;
            }

            for (var pivot = 0; pivot < 8; pivot++)
            {
                var bestRow = pivot;
                var bestValue = Math.Abs(augmented[pivot, pivot]);
                for (var row = pivot + 1; row < 8; row++)
                {
                    var candidate = Math.Abs(augmented[row, pivot]);
                    if (candidate <= bestValue) continue;
                    bestValue = candidate;
                    bestRow = row;
                }

                if (bestValue < 1e-9d)
                    throw new InvalidOperationException("OfficeVisualV2 control points do not define a homography.");
                if (bestRow != pivot)
                {
                    for (var column = pivot; column < 9; column++)
                    {
                        var temporary = augmented[pivot, column];
                        augmented[pivot, column] = augmented[bestRow, column];
                        augmented[bestRow, column] = temporary;
                    }
                }

                var divisor = augmented[pivot, pivot];
                for (var column = pivot; column < 9; column++) augmented[pivot, column] /= divisor;
                for (var row = 0; row < 8; row++)
                {
                    if (row == pivot) continue;
                    var factor = augmented[row, pivot];
                    if (Math.Abs(factor) < 1e-12d) continue;
                    for (var column = pivot; column < 9; column++)
                    {
                        augmented[row, column] -= factor * augmented[pivot, column];
                    }
                }
            }

            var result = new double[8];
            for (var row = 0; row < 8; row++) result[row] = augmented[row, 8];
            return result;
        }
    }
}

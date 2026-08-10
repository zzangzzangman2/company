using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Navigation
{
    public readonly struct OfficeNavPoint : IEquatable<OfficeNavPoint>
    {
        public OfficeNavPoint(float x, float z)
        {
            if (!IsFinite(x) || !IsFinite(z))
                throw new ArgumentOutOfRangeException(nameof(x), "Navigation points must be finite.");
            X = x;
            Z = z;
        }

        public float X { get; }
        public float Z { get; }
        public float SqrMagnitude => X * X + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public OfficeNavPoint Normalized
        {
            get
            {
                var magnitude = Magnitude;
                return magnitude <= 0.00001f
                    ? new OfficeNavPoint(0f, 0f)
                    : new OfficeNavPoint(X / magnitude, Z / magnitude);
            }
        }

        public static OfficeNavPoint operator +(OfficeNavPoint left, OfficeNavPoint right) =>
            new OfficeNavPoint(left.X + right.X, left.Z + right.Z);

        public static OfficeNavPoint operator -(OfficeNavPoint left, OfficeNavPoint right) =>
            new OfficeNavPoint(left.X - right.X, left.Z - right.Z);

        public static OfficeNavPoint operator *(OfficeNavPoint value, float scale) =>
            new OfficeNavPoint(value.X * scale, value.Z * scale);

        public static float Distance(OfficeNavPoint left, OfficeNavPoint right) => (left - right).Magnitude;
        public static float Dot(OfficeNavPoint left, OfficeNavPoint right) => left.X * right.X + left.Z * right.Z;

        public bool Equals(OfficeNavPoint other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is OfficeNavPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Z.GetHashCode());
        public override string ToString() => $"({X:F3}, {Z:F3})";

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct OfficeNavBounds
    {
        public OfficeNavBounds(float minX, float minZ, float maxX, float maxZ)
        {
            if (!IsFinite(minX) || !IsFinite(minZ) || !IsFinite(maxX) || !IsFinite(maxZ) ||
                maxX <= minX || maxZ <= minZ)
                throw new ArgumentOutOfRangeException(nameof(maxX), "Navigation bounds must be finite and non-empty.");
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxZ { get; }
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;

        public bool Contains(OfficeNavPoint point) =>
            point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct OfficeNavObstacle
    {
        public OfficeNavObstacle(string obstacleId, float minX, float minZ, float maxX, float maxZ)
        {
            if (string.IsNullOrWhiteSpace(obstacleId))
                throw new ArgumentException("Obstacle ID is required.", nameof(obstacleId));
            if (!IsFinite(minX) || !IsFinite(minZ) || !IsFinite(maxX) || !IsFinite(maxZ) ||
                maxX < minX || maxZ < minZ)
                throw new ArgumentOutOfRangeException(nameof(maxX), "Obstacle bounds must be finite and ordered.");
            ObstacleId = obstacleId;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public string ObstacleId { get; }
        public float MinX { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxZ { get; }

        public OfficeNavObstacle Expanded(float amount)
        {
            if (amount < 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));
            return new OfficeNavObstacle(
                ObstacleId,
                MinX - amount,
                MinZ - amount,
                MaxX + amount,
                MaxZ + amount);
        }

        public bool Intersects(float minX, float minZ, float maxX, float maxZ) =>
            MaxX >= minX && MinX <= maxX && MaxZ >= minZ && MinZ <= maxZ;

        public bool Contains(OfficeNavPoint point) =>
            point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class OfficeNavPath
    {
        private readonly OfficeNavPoint[] _waypoints;

        internal OfficeNavPath(
            OfficeNavPoint[] waypoints,
            bool startProjected,
            bool goalProjected,
            int expandedNodes,
            int rawNodeCount,
            float rawGridCostMetres)
        {
            _waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
            StartProjected = startProjected;
            GoalProjected = goalProjected;
            ExpandedNodes = expandedNodes;
            RawNodeCount = rawNodeCount;
            RawGridCostMetres = rawGridCostMetres;
            LengthMetres = CalculateLength(_waypoints);
        }

        public IReadOnlyList<OfficeNavPoint> Waypoints => _waypoints;
        public bool StartProjected { get; }
        public bool GoalProjected { get; }
        public int ExpandedNodes { get; }
        public int RawNodeCount { get; }
        public float RawGridCostMetres { get; }
        public float LengthMetres { get; }

        private static float CalculateLength(IReadOnlyList<OfficeNavPoint> points)
        {
            var result = 0f;
            for (var index = 1; index < points.Count; index++)
                result += OfficeNavPoint.Distance(points[index - 1], points[index]);
            return result;
        }
    }

    public static class OfficeNavigationLimits
    {
        public const int MaxGridCells = 16384;
        public const int MaxObstacles = 512;
        public const int MaxExpandedNodes = MaxGridCells;
        public const float MinimumCellSize = 0.10f;
        public const float MaximumCellSize = 1.00f;
    }
}

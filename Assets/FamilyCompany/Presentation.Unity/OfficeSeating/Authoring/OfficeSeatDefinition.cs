using System;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.Authoring
{
    // Numeric values intentionally match DirectionalSpriteAnimator's octant order.
    public enum OfficeSeatFacing8
    {
        South = 0,
        Southwest = 1,
        West = 2,
        Northwest = 3,
        North = 4,
        Northeast = 5,
        East = 6,
        Southeast = 7
    }

    public enum OfficeSeatForegroundOcclusionMode
    {
        Default = 0,
        BehindForeground = 1,
        InFrontOfForeground = 2
    }

    public readonly struct OfficeSeatPosition
    {
        public OfficeSeatPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public bool IsFinite =>
            IsFiniteNumber(X) && IsFiniteNumber(Y) && IsFiniteNumber(Z);

        public float FlatDistanceTo(OfficeSeatPosition other)
        {
            var deltaX = X - other.X;
            var deltaZ = Z - other.Z;
            return (float)Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private static bool IsFiniteNumber(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class OfficeSeatDefinition
    {
        public OfficeSeatDefinition(
            string seatId,
            OfficeSeatPosition approachPosition,
            OfficeSeatPosition sitPosition,
            OfficeSeatPosition computerLookPosition,
            float lookDirectionX,
            float lookDirectionZ,
            OfficeSeatFacing8 resolvedFacing,
            OfficeSeatForegroundOcclusionMode foregroundOcclusionMode,
            bool hasExpectedFacing,
            OfficeSeatFacing8 expectedFacing)
        {
            SeatId = string.IsNullOrWhiteSpace(seatId)
                ? throw new ArgumentException("Seat ID is required.", nameof(seatId))
                : seatId;
            ApproachPosition = approachPosition;
            SitPosition = sitPosition;
            ComputerLookPosition = computerLookPosition;
            LookDirectionX = lookDirectionX;
            LookDirectionZ = lookDirectionZ;
            ResolvedFacing = resolvedFacing;
            ForegroundOcclusionMode = foregroundOcclusionMode;
            HasExpectedFacing = hasExpectedFacing;
            ExpectedFacing = expectedFacing;
        }

        public string SeatId { get; }
        public OfficeSeatPosition ApproachPosition { get; }
        public OfficeSeatPosition SitPosition { get; }
        public OfficeSeatPosition ComputerLookPosition { get; }
        public float LookDirectionX { get; }
        public float LookDirectionZ { get; }
        public OfficeSeatFacing8 ResolvedFacing { get; }
        public OfficeSeatForegroundOcclusionMode ForegroundOcclusionMode { get; }
        public bool HasExpectedFacing { get; }
        public OfficeSeatFacing8 ExpectedFacing { get; }
    }

    public static class OfficeSeatGeometryRules
    {
        public const float MinimumComputerLookDistance = 0.20f;
        public const float MinimumApproachToSitDistance = 0.20f;
        public const float MaximumApproachToSitDistance = 2.50f;

        public static bool TryResolveLookDirection(
            OfficeSeatPosition sitPosition,
            OfficeSeatPosition lookPosition,
            out float normalizedX,
            out float normalizedZ,
            out OfficeSeatFacing8 facing)
        {
            normalizedX = 0f;
            normalizedZ = 0f;
            facing = OfficeSeatFacing8.South;
            if (!sitPosition.IsFinite || !lookPosition.IsFinite) return false;

            var deltaX = lookPosition.X - sitPosition.X;
            var deltaZ = lookPosition.Z - sitPosition.Z;
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            if (distance < MinimumComputerLookDistance) return false;
            normalizedX = (float)(deltaX / distance);
            normalizedZ = (float)(deltaZ / distance);
            facing = QuantizeDirection(normalizedX, normalizedZ);
            return true;
        }

        public static OfficeSeatFacing8 QuantizeDirection(float directionX, float directionZ)
        {
            if (float.IsNaN(directionX) || float.IsInfinity(directionX) ||
                float.IsNaN(directionZ) || float.IsInfinity(directionZ))
                throw new ArgumentOutOfRangeException(nameof(directionX), "Facing direction must be finite.");

            var magnitude = Math.Sqrt(directionX * directionX + directionZ * directionZ);
            if (magnitude < 0.0001d)
                throw new ArgumentOutOfRangeException(nameof(directionX), "Facing direction cannot be zero.");
            var normalizedX = directionX / magnitude;
            var normalizedZ = directionZ / magnitude;
            var angleFromSouth = Math.Atan2(-normalizedX, -normalizedZ) * 180d / Math.PI;
            var octant = (int)Math.Round(angleFromSouth / 45d, MidpointRounding.ToEven);
            octant = (octant % 8 + 8) % 8;
            return (OfficeSeatFacing8)octant;
        }

        public static bool IsValidFacing(OfficeSeatFacing8 value)
        {
            return (int)value >= 0 && (int)value < 8;
        }

        public static bool IsValidOcclusionMode(OfficeSeatForegroundOcclusionMode value)
        {
            return (int)value >= 0 && (int)value <= 2;
        }
    }
}

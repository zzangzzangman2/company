using System;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    public readonly struct OfficeSeatPrecisionStep
    {
        public OfficeSeatPrecisionStep(double x, double z, bool arrived)
        {
            X = x;
            Z = z;
            Arrived = arrived;
        }

        public double X { get; }
        public double Z { get; }
        public bool Arrived { get; }
    }

    public static class OfficeSeatPrecisionMotion
    {
        public const double ApproachSpeedMetersPerSecond = 1.20d;
        public const double SitSpeedMetersPerSecond = 0.85d;

        public static OfficeSeatPrecisionStep Advance(
            double currentX,
            double currentZ,
            double targetX,
            double targetZ,
            double speedMetersPerSecond,
            double deltaSeconds)
        {
            if (!IsFinite(currentX) || !IsFinite(currentZ) ||
                !IsFinite(targetX) || !IsFinite(targetZ))
            {
                throw new ArgumentOutOfRangeException(nameof(currentX), "Seat precision positions must be finite.");
            }
            if (!IsFinite(speedMetersPerSecond) || speedMetersPerSecond <= 0d)
                throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            var deltaX = targetX - currentX;
            var deltaZ = targetZ - currentZ;
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            if (distance <= 1e-9d)
                return new OfficeSeatPrecisionStep(targetX, targetZ, true);

            var maximumStep = speedMetersPerSecond * deltaSeconds;
            if (maximumStep >= distance)
                return new OfficeSeatPrecisionStep(targetX, targetZ, true);
            if (maximumStep <= 0d)
                return new OfficeSeatPrecisionStep(currentX, currentZ, false);

            var scale = maximumStep / distance;
            return new OfficeSeatPrecisionStep(
                currentX + deltaX * scale,
                currentZ + deltaZ * scale,
                false);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

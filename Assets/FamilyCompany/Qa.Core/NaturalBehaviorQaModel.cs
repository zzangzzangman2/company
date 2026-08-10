using System;
using System.Collections.Generic;

namespace FamilyCompany.Qa.NaturalBehavior
{
    [Flags]
    public enum NaturalBehaviorQaCapability
    {
        None = 0,
        SpatialSafety = 1 << 0,
        PathQuality = 1 << 1,
        MotionContinuity = 1 << 2,
        Seating = 1 << 3,
        WorkActions = 1 << 4,
        NavigationRebuild = 1 << 5,
        All = SpatialSafety | PathQuality | MotionContinuity | Seating | WorkActions | NavigationRebuild
    }

    public enum QaMotionPhase
    {
        Other = 0,
        Walking = 1,
        Approach = 2,
        SittingDown = 3,
        Work = 4,
        StandingUp = 5
    }

    public enum QaSeatingPhase
    {
        Approach = 0,
        SitDown = 1,
        Work = 2,
        StandUp = 3,
        Complete = 4
    }

    public enum QaWorkVisualAction
    {
        Typing = 0,
        Mouse = 1,
        Drink = 2
    }

    public enum QaSeatingPixelExpectation
    {
        FootAnchor = 0,
        CharacterBody = 1,
        ChairForeground = 2,
        DeskForeground = 3
    }

    public enum QaSeatingPixelObservedRole
    {
        Background = 0,
        CharacterBody = 1,
        ChairForeground = 2,
        DeskForeground = 3,
        Unknown = 4
    }

    internal static class QaValue
    {
        public static double Finite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "A finite value is required.");
            return value;
        }

        public static double NonNegative(double value, string parameterName)
        {
            value = Finite(value, parameterName);
            if (value < 0d) throw new ArgumentOutOfRangeException(parameterName, "A non-negative value is required.");
            return value;
        }

        public static double Positive(double value, string parameterName)
        {
            value = Finite(value, parameterName);
            if (value <= 0d) throw new ArgumentOutOfRangeException(parameterName, "A positive value is required.");
            return value;
        }

        public static int NonNegative(int value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName, "A non-negative value is required.");
            return value;
        }

        public static int Positive(int value, string parameterName)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, "A positive value is required.");
            return value;
        }

        public static int RepeatIndex(int value, string parameterName)
        {
            return NonNegative(value, parameterName);
        }

        public static T DefinedEnum<T>(T value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        public static string Sha256(string value, string parameterName)
        {
            var normalized = NaturalBehaviorQaPlan.NormalizeId(value, parameterName).ToLowerInvariant();
            if (normalized.Length != 64) throw new ArgumentException("A 64-character SHA-256 hex digest is required.", parameterName);
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                    throw new ArgumentException("A SHA-256 hex digest is required.", parameterName);
            }
            return normalized;
        }
    }

    public readonly struct QaPoint2
    {
        public QaPoint2(double x, double y)
        {
            X = QaValue.Finite(x, nameof(x));
            Y = QaValue.Finite(y, nameof(y));
        }

        public double X { get; }
        public double Y { get; }

        public double DistanceTo(QaPoint2 other)
        {
            var x = X - other.X;
            var y = Y - other.Y;
            return Math.Sqrt(x * x + y * y);
        }

    }

    public sealed class QaPolygon2
    {
        public QaPolygon2(IEnumerable<QaPoint2> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            Vertices = new List<QaPoint2>(vertices).ToArray();
            if (Vertices.Count < 3) throw new ArgumentException("A footprint needs at least three vertices.", nameof(vertices));
            var twiceArea = 0d;
            for (var index = 0; index < Vertices.Count; index++)
            {
                var next = (index + 1) % Vertices.Count;
                twiceArea += Vertices[index].X * Vertices[next].Y - Vertices[next].X * Vertices[index].Y;
            }
            if (double.IsNaN(twiceArea) || double.IsInfinity(twiceArea) || Math.Abs(twiceArea) <= 0.000000000001d)
                throw new ArgumentException("A footprint needs finite non-zero polygon area.", nameof(vertices));
        }

        public IReadOnlyList<QaPoint2> Vertices { get; }
    }

    public static class NaturalBehaviorQaScenarioIds
    {
        public const string SemanticRoundTrip = "semantic-roundtrip";
        public const string RandomFurniture = "random-furniture";
    }

    public sealed class NaturalBehaviorQaPlan
    {
        public NaturalBehaviorQaPlan(
            IEnumerable<string> expectedMemberIds,
            IEnumerable<string> semanticDestinationIds,
            IEnumerable<int> randomLayoutSeeds,
            double observationGameSeconds,
            double maximumWallClockSeconds,
            string semanticOriginId)
        {
            ExpectedMemberIds = NormalizeIds(expectedMemberIds, nameof(expectedMemberIds));
            SemanticDestinationIds = NormalizeIds(semanticDestinationIds, nameof(semanticDestinationIds));
            if (randomLayoutSeeds == null) throw new ArgumentNullException(nameof(randomLayoutSeeds));
            RandomLayoutSeeds = new List<int>(randomLayoutSeeds).ToArray();
            if (RandomLayoutSeeds.Count == 0) throw new ArgumentException("At least one layout seed is required.", nameof(randomLayoutSeeds));
            if (new HashSet<int>(RandomLayoutSeeds).Count != RandomLayoutSeeds.Count)
                throw new ArgumentException("Layout seeds must be unique.", nameof(randomLayoutSeeds));
            ObservationGameSeconds = QaValue.Positive(observationGameSeconds, nameof(observationGameSeconds));
            MaximumWallClockSeconds = QaValue.Positive(maximumWallClockSeconds, nameof(maximumWallClockSeconds));
            SemanticOriginId = NormalizeId(semanticOriginId, nameof(semanticOriginId));
        }

        public IReadOnlyList<string> ExpectedMemberIds { get; }
        public IReadOnlyList<string> SemanticDestinationIds { get; }
        public IReadOnlyList<int> RandomLayoutSeeds { get; }
        public double ObservationGameSeconds { get; }
        public double MaximumWallClockSeconds { get; }
        public string SemanticOriginId { get; }

        public static NaturalBehaviorQaPlan CreateCanonical()
        {
            var seeds = new int[100];
            for (var index = 0; index < seeds.Length; index++) seeds[index] = 20000103 + index;
            return new NaturalBehaviorQaPlan(
                new[] { "player", "older_sister", "father", "mother" },
                new[] { "reception", "desk", "printer", "meeting", "lounge", "exit" },
                seeds,
                30d * 60d,
                15d * 60d,
                "office-origin");
        }

        private static IReadOnlyList<string> NormalizeIds(IEnumerable<string> values, string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var normalized = NormalizeId(value, parameterName);
                if (!seen.Add(normalized)) throw new ArgumentException("IDs must be unique.", parameterName);
                result.Add(normalized);
            }
            if (result.Count == 0) throw new ArgumentException("At least one ID is required.", parameterName);
            return result.ToArray();
        }

        internal static string NormalizeId(string value, string parameterName)
        {
            var normalized = value == null ? string.Empty : value.Trim();
            if (normalized.Length == 0) throw new ArgumentException("A stable non-empty ID is required.", parameterName);
            return normalized;
        }

    }

    public sealed class WorkActionCooldownContract
    {
        public WorkActionCooldownContract(QaWorkVisualAction action, double minimumWorkSeconds, double maximumWorkSeconds)
        {
            minimumWorkSeconds = QaValue.NonNegative(minimumWorkSeconds, nameof(minimumWorkSeconds));
            maximumWorkSeconds = QaValue.Positive(maximumWorkSeconds, nameof(maximumWorkSeconds));
            if (minimumWorkSeconds > maximumWorkSeconds)
                throw new ArgumentOutOfRangeException(nameof(minimumWorkSeconds));
            Action = QaValue.DefinedEnum(action, nameof(action));
            MinimumWorkSeconds = minimumWorkSeconds;
            MaximumWorkSeconds = maximumWorkSeconds;
        }

        public QaWorkVisualAction Action { get; }
        public double MinimumWorkSeconds { get; }
        public double MaximumWorkSeconds { get; }
    }

    public sealed class NaturalBehaviorQualityBar
    {
        public int RequiredRandomLayoutSeeds { get; set; } = 100;
        public int RequiredFurniturePerRandomLayout { get; set; } = 100;
        public int RequiredDeterminismRepeats { get; set; } = 2;
        public double MaximumPathStretchP95 { get; set; } = 1.35d;
        public double MaximumPathStretch { get; set; } = 1.60d;
        public int MaximumReplansPerRoute { get; set; } = 3;
        public double MaximumDeadlockSeconds { get; set; } = 0.75d;
        public double MaximumMotionSampleGapSeconds { get; set; } = 0.10d;
        public double MaximumFrameDeltaMeters { get; set; } = 0.10d;
        public double MaximumSpeedMetersPerSecond { get; set; } = 2.25d;
        public double MaximumAccelerationMetersPerSecondSquared { get; set; } = 16d;
        public double DirectionFlipWindowSeconds { get; set; } = 0.15d;
        public double MinimumDirectionFlipSpeed { get; set; } = 0.15d;
        public double CornerJitterWindowSeconds { get; set; } = 0.75d;
        public double CornerJitterRadiusMeters { get; set; } = 0.18d;
        public double MaximumSeatFootErrorPixels1920 { get; set; } = 1d;
        public int SitDownFrameCount { get; set; } = 4;
        public int WorkFrameCount { get; set; } = 6;
        public int StandUpFrameCount { get; set; } = 4;
        public double RequiredObservationGameSeconds { get; set; } = 1800d;
        public double MaximumNavigationRebuildSeconds { get; set; } = 12d;
        public double NumericTolerance { get; set; } = 0.000001d;

        public IReadOnlyList<WorkActionCooldownContract> WorkActionCooldowns { get; set; } =
            new[]
            {
                new WorkActionCooldownContract(QaWorkVisualAction.Typing, 0.55d, 2.40d),
                new WorkActionCooldownContract(QaWorkVisualAction.Mouse, 2.50d, 8d),
                new WorkActionCooldownContract(QaWorkVisualAction.Drink, 45d, 180d)
            };
    }

    public sealed class LayoutObservation
    {
        public LayoutObservation(string scenarioId, int layoutSeed, int repeatIndex, int furnitureCount, bool succeeded, string stableHash)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            FurnitureCount = QaValue.NonNegative(furnitureCount, nameof(furnitureCount));
            Succeeded = succeeded;
            StableHash = QaValue.Sha256(stableHash, nameof(stableHash));
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public int FurnitureCount { get; }
        public bool Succeeded { get; }
        public string StableHash { get; }
    }

    public sealed class FurnitureFootprintObservation
    {
        public FurnitureFootprintObservation(
            string scenarioId,
            int layoutSeed,
            int repeatIndex,
            string furnitureId,
            QaPolygon2 footprint,
            bool blocksMovement,
            bool isPlaceable)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            FurnitureId = NaturalBehaviorQaPlan.NormalizeId(furnitureId, nameof(furnitureId));
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            BlocksMovement = blocksMovement;
            IsPlaceable = isPlaceable;
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public string FurnitureId { get; }
        public QaPolygon2 Footprint { get; }
        public bool BlocksMovement { get; }
        public bool IsPlaceable { get; }
    }

    public sealed class FootpointSample
    {
        public FootpointSample(
            string scenarioId,
            int layoutSeed,
            int repeatIndex,
            string memberId,
            double timeSeconds,
            QaPoint2 position,
            double radiusMeters,
            bool visible)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            TimeSeconds = QaValue.NonNegative(timeSeconds, nameof(timeSeconds));
            Position = position;
            RadiusMeters = QaValue.Positive(radiusMeters, nameof(radiusMeters));
            Visible = visible;
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public string MemberId { get; }
        public double TimeSeconds { get; }
        public QaPoint2 Position { get; }
        public double RadiusMeters { get; }
        public bool Visible { get; }
    }

    public sealed class MotionSample
    {
        public MotionSample(
            string scenarioId,
            int layoutSeed,
            int repeatIndex,
            string memberId,
            double timeSeconds,
            QaPoint2 position,
            int directionIndex,
            QaMotionPhase phase,
            bool visible)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            if (directionIndex < 0 || directionIndex >= 8) throw new ArgumentOutOfRangeException(nameof(directionIndex));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            TimeSeconds = QaValue.NonNegative(timeSeconds, nameof(timeSeconds));
            Position = position;
            DirectionIndex = directionIndex;
            Phase = QaValue.DefinedEnum(phase, nameof(phase));
            Visible = visible;
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public string MemberId { get; }
        public double TimeSeconds { get; }
        public QaPoint2 Position { get; }
        public int DirectionIndex { get; }
        public QaMotionPhase Phase { get; }
        public bool Visible { get; }
    }

    public sealed class PathObservation
    {
        public PathObservation(
            string scenarioId,
            int layoutSeed,
            int repeatIndex,
            string memberId,
            string fromDestinationId,
            string toDestinationId,
            bool succeeded,
            double directDistanceMeters,
            double travelledDistanceMeters,
            int replanCount,
            double deadlockSeconds,
            string stablePathHash,
            int unsafeTraversalCount)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            FromDestinationId = NaturalBehaviorQaPlan.NormalizeId(fromDestinationId, nameof(fromDestinationId));
            ToDestinationId = NaturalBehaviorQaPlan.NormalizeId(toDestinationId, nameof(toDestinationId));
            StablePathHash = QaValue.Sha256(stablePathHash, nameof(stablePathHash));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            Succeeded = succeeded;
            DirectDistanceMeters = QaValue.Positive(directDistanceMeters, nameof(directDistanceMeters));
            TravelledDistanceMeters = QaValue.NonNegative(travelledDistanceMeters, nameof(travelledDistanceMeters));
            ReplanCount = QaValue.NonNegative(replanCount, nameof(replanCount));
            DeadlockSeconds = QaValue.NonNegative(deadlockSeconds, nameof(deadlockSeconds));
            UnsafeTraversalCount = QaValue.NonNegative(unsafeTraversalCount, nameof(unsafeTraversalCount));
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public string MemberId { get; }
        public string FromDestinationId { get; }
        public string ToDestinationId { get; }
        public bool Succeeded { get; }
        public double DirectDistanceMeters { get; }
        public double TravelledDistanceMeters { get; }
        public int ReplanCount { get; }
        public double DeadlockSeconds { get; }
        public string StablePathHash { get; }
        public int UnsafeTraversalCount { get; }
    }

    public sealed class SeatingPixelObservation
    {
        public SeatingPixelObservation(
            int x,
            int y,
            QaSeatingPixelExpectation expectation,
            QaSeatingPixelObservedRole observedRole)
        {
            X = QaValue.NonNegative(x, nameof(x));
            Y = QaValue.NonNegative(y, nameof(y));
            Expectation = QaValue.DefinedEnum(expectation, nameof(expectation));
            ObservedRole = QaValue.DefinedEnum(observedRole, nameof(observedRole));
        }

        public int X { get; }
        public int Y { get; }
        public QaSeatingPixelExpectation Expectation { get; }
        public QaSeatingPixelObservedRole ObservedRole { get; }
    }

    public sealed class SeatingCaptureEvidence
    {
        public SeatingCaptureEvidence(
            string captureLabel,
            string captureSha256,
            int width,
            int height,
            IEnumerable<SeatingPixelObservation> pixelObservations)
        {
            CaptureLabel = NaturalBehaviorQaPlan.NormalizeId(captureLabel, nameof(captureLabel));
            CaptureSha256 = QaValue.Sha256(captureSha256, nameof(captureSha256));
            Width = QaValue.Positive(width, nameof(width));
            Height = QaValue.Positive(height, nameof(height));
            if (pixelObservations == null) throw new ArgumentNullException(nameof(pixelObservations));
            PixelObservations = new List<SeatingPixelObservation>(pixelObservations).ToArray();
            if (PixelObservations.Count == 0)
                throw new ArgumentException("At least one observed capture pixel is required.", nameof(pixelObservations));
            for (var index = 0; index < PixelObservations.Count; index++)
            {
                var sample = PixelObservations[index] ?? throw new ArgumentException("Capture pixel observations cannot contain null.", nameof(pixelObservations));
                if (sample.X >= Width || sample.Y >= Height)
                    throw new ArgumentOutOfRangeException(nameof(pixelObservations), "A capture pixel lies outside the declared image dimensions.");
            }
        }

        public string CaptureLabel { get; }
        public string CaptureSha256 { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<SeatingPixelObservation> PixelObservations { get; }
    }

    public sealed class NaturalBehaviorQaCaptureArtifact
    {
        public NaturalBehaviorQaCaptureArtifact(string label, string sha256, int width, int height)
        {
            Label = NaturalBehaviorQaPlan.NormalizeId(label, nameof(label));
            Sha256 = QaValue.Sha256(sha256, nameof(sha256));
            Width = QaValue.Positive(width, nameof(width));
            Height = QaValue.Positive(height, nameof(height));
        }

        public string Label { get; }
        public string Sha256 { get; }
        public int Width { get; }
        public int Height { get; }
    }

    public sealed class SeatingFrameObservation
    {
        public SeatingFrameObservation(
            string sessionId,
            string memberId,
            double timeSeconds,
            QaSeatingPhase phase,
            int frameIndex,
            QaPoint2 footPixel1920,
            SeatingCaptureEvidence captureEvidence)
        {
            SessionId = NaturalBehaviorQaPlan.NormalizeId(sessionId, nameof(sessionId));
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            SessionTimeSeconds = QaValue.NonNegative(timeSeconds, nameof(timeSeconds));
            Phase = QaValue.DefinedEnum(phase, nameof(phase));
            FrameIndex = frameIndex;
            FootPixel1920 = footPixel1920;
            CaptureEvidence = captureEvidence ?? throw new ArgumentNullException(nameof(captureEvidence));
        }

        public string SessionId { get; }
        public string MemberId { get; }
        public double SessionTimeSeconds { get; }
        public QaSeatingPhase Phase { get; }
        public int FrameIndex { get; }
        public QaPoint2 FootPixel1920 { get; }
        public SeatingCaptureEvidence CaptureEvidence { get; }
    }

    public sealed class WorkActionObservation
    {
        public WorkActionObservation(
            string memberId,
            QaWorkVisualAction action,
            double timeSeconds,
            double accumulatedWorkSeconds,
            double workSecondsSincePreviousSameAction,
            QaMotionPhase phase,
            bool visualVisible)
        {
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            Action = QaValue.DefinedEnum(action, nameof(action));
            TimeSeconds = QaValue.NonNegative(timeSeconds, nameof(timeSeconds));
            AccumulatedWorkSeconds = QaValue.NonNegative(accumulatedWorkSeconds, nameof(accumulatedWorkSeconds));
            WorkSecondsSincePreviousSameAction = QaValue.Positive(workSecondsSincePreviousSameAction, nameof(workSecondsSincePreviousSameAction));
            Phase = QaValue.DefinedEnum(phase, nameof(phase));
            VisualVisible = visualVisible;
        }

        public string MemberId { get; }
        public QaWorkVisualAction Action { get; }
        public double TimeSeconds { get; }
        public double AccumulatedWorkSeconds { get; }
        public double WorkSecondsSincePreviousSameAction { get; }
        public QaMotionPhase Phase { get; }
        public bool VisualVisible { get; }
    }

    public sealed class WorkWindowObservation
    {
        public WorkWindowObservation(string memberId, double observationGameSeconds, double accumulatedWorkSeconds)
        {
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            ObservationGameSeconds = QaValue.Positive(observationGameSeconds, nameof(observationGameSeconds));
            AccumulatedWorkSeconds = QaValue.NonNegative(accumulatedWorkSeconds, nameof(accumulatedWorkSeconds));
            if (AccumulatedWorkSeconds > ObservationGameSeconds)
                throw new ArgumentOutOfRangeException(nameof(accumulatedWorkSeconds), "Work time cannot exceed its observation window.");
        }

        public string MemberId { get; }
        public double ObservationGameSeconds { get; }
        public double AccumulatedWorkSeconds { get; }
    }

    public sealed class ProductivityObservation
    {
        public ProductivityObservation(string memberId, double timeSeconds, QaMotionPhase phase, double productivityDelta)
        {
            MemberId = NaturalBehaviorQaPlan.NormalizeId(memberId, nameof(memberId));
            TimeSeconds = QaValue.NonNegative(timeSeconds, nameof(timeSeconds));
            Phase = QaValue.DefinedEnum(phase, nameof(phase));
            ProductivityDelta = QaValue.Finite(productivityDelta, nameof(productivityDelta));
        }

        public string MemberId { get; }
        public double TimeSeconds { get; }
        public QaMotionPhase Phase { get; }
        public double ProductivityDelta { get; }
    }

    public sealed class NavigationRebuildObservation
    {
        public NavigationRebuildObservation(
            string scenarioId,
            int layoutSeed,
            int repeatIndex,
            double requestedTimeSeconds,
            double completedTimeSeconds,
            IEnumerable<string> activePathIds,
            IEnumerable<string> safelyReplannedPathIds,
            IEnumerable<string> unsafeTraversalPathIds,
            double progressWhileUnsafeSeconds)
        {
            ScenarioId = NaturalBehaviorQaPlan.NormalizeId(scenarioId, nameof(scenarioId));
            LayoutSeed = layoutSeed;
            RepeatIndex = QaValue.RepeatIndex(repeatIndex, nameof(repeatIndex));
            RequestedTimeSeconds = QaValue.NonNegative(requestedTimeSeconds, nameof(requestedTimeSeconds));
            CompletedTimeSeconds = QaValue.NonNegative(completedTimeSeconds, nameof(completedTimeSeconds));
            ActivePathIds = NormalizePathIds(activePathIds, nameof(activePathIds), true);
            SafelyReplannedPathIds = NormalizePathIds(safelyReplannedPathIds, nameof(safelyReplannedPathIds), false);
            UnsafeTraversalPathIds = NormalizePathIds(unsafeTraversalPathIds, nameof(unsafeTraversalPathIds), false);
            ProgressWhileUnsafeSeconds = QaValue.NonNegative(progressWhileUnsafeSeconds, nameof(progressWhileUnsafeSeconds));
        }

        public string ScenarioId { get; }
        public int LayoutSeed { get; }
        public int RepeatIndex { get; }
        public double RequestedTimeSeconds { get; }
        public double CompletedTimeSeconds { get; }
        public IReadOnlyList<string> ActivePathIds { get; }
        public IReadOnlyList<string> SafelyReplannedPathIds { get; }
        public IReadOnlyList<string> UnsafeTraversalPathIds { get; }
        public int ActivePathCount => ActivePathIds.Count;
        public int SafelyReplannedPathCount => SafelyReplannedPathIds.Count;
        public int UnsafeTraversalCount => UnsafeTraversalPathIds.Count;
        public double ProgressWhileUnsafeSeconds { get; }

        private static IReadOnlyList<string> NormalizePathIds(
            IEnumerable<string> values,
            string parameterName,
            bool requireAny)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var normalized = NaturalBehaviorQaPlan.NormalizeId(value, parameterName);
                if (!seen.Add(normalized)) throw new ArgumentException("Path IDs must be unique.", parameterName);
                result.Add(normalized);
            }
            if (requireAny && result.Count == 0)
                throw new ArgumentException("At least one active path ID is required.", parameterName);
            return result.ToArray();
        }
    }

    public sealed class NaturalBehaviorQaRun
    {
        private double _observationGameSeconds;

        public NaturalBehaviorQaRun(NaturalBehaviorQaPlan plan, NaturalBehaviorQaCapability capabilities)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Capabilities = capabilities;
        }

        public NaturalBehaviorQaPlan Plan { get; }
        public NaturalBehaviorQaCapability Capabilities { get; }
        public double ObservationGameSeconds
        {
            get => _observationGameSeconds;
            set => _observationGameSeconds = QaValue.NonNegative(value, nameof(value));
        }
        public List<LayoutObservation> Layouts { get; } = new List<LayoutObservation>();
        public List<FurnitureFootprintObservation> FurnitureFootprints { get; } = new List<FurnitureFootprintObservation>();
        public List<FootpointSample> Footpoints { get; } = new List<FootpointSample>();
        public List<MotionSample> MotionSamples { get; } = new List<MotionSample>();
        public List<PathObservation> Paths { get; } = new List<PathObservation>();
        public List<SeatingFrameObservation> SeatingFrames { get; } = new List<SeatingFrameObservation>();
        public List<NaturalBehaviorQaCaptureArtifact> CaptureArtifacts { get; } = new List<NaturalBehaviorQaCaptureArtifact>();
        public List<WorkWindowObservation> WorkWindows { get; } = new List<WorkWindowObservation>();
        public List<WorkActionObservation> WorkActions { get; } = new List<WorkActionObservation>();
        public List<ProductivityObservation> Productivity { get; } = new List<ProductivityObservation>();
        public List<NavigationRebuildObservation> NavigationRebuilds { get; } = new List<NavigationRebuildObservation>();
    }

    public interface INaturalBehaviorQaRecorder
    {
        void SetObservationGameSeconds(double seconds);
        void Record(LayoutObservation observation);
        void Record(FurnitureFootprintObservation observation);
        void Record(FootpointSample observation);
        void Record(MotionSample observation);
        void Record(PathObservation observation);
        void Record(SeatingFrameObservation observation);
        void Record(WorkWindowObservation observation);
        void Record(WorkActionObservation observation);
        void Record(ProductivityObservation observation);
        void Record(NavigationRebuildObservation observation);
    }

    public sealed class NaturalBehaviorQaRecorder : INaturalBehaviorQaRecorder
    {
        private readonly NaturalBehaviorQaRun _run;

        public NaturalBehaviorQaRecorder(NaturalBehaviorQaPlan plan, NaturalBehaviorQaCapability capabilities)
        {
            _run = new NaturalBehaviorQaRun(plan, capabilities);
        }

        public void SetObservationGameSeconds(double seconds) => _run.ObservationGameSeconds = seconds;
        public void Record(LayoutObservation observation) => _run.Layouts.Add(Require(observation));
        public void Record(FurnitureFootprintObservation observation) => _run.FurnitureFootprints.Add(Require(observation));
        public void Record(FootpointSample observation) => _run.Footpoints.Add(Require(observation));
        public void Record(MotionSample observation) => _run.MotionSamples.Add(Require(observation));
        public void Record(PathObservation observation) => _run.Paths.Add(Require(observation));
        public void Record(SeatingFrameObservation observation) => _run.SeatingFrames.Add(Require(observation));
        public void RecordCaptureArtifact(NaturalBehaviorQaCaptureArtifact artifact) =>
            _run.CaptureArtifacts.Add(Require(artifact));
        public void Record(WorkWindowObservation observation) => _run.WorkWindows.Add(Require(observation));
        public void Record(WorkActionObservation observation) => _run.WorkActions.Add(Require(observation));
        public void Record(ProductivityObservation observation) => _run.Productivity.Add(Require(observation));
        public void Record(NavigationRebuildObservation observation) => _run.NavigationRebuilds.Add(Require(observation));

        public NaturalBehaviorQaRun Build() => _run;

        private static T Require<T>(T value) where T : class
        {
            return value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public interface INaturalBehaviorQaRuntimeHook
    {
        string ProviderId { get; }
        NaturalBehaviorQaCapability Capabilities { get; }
        bool IsComplete { get; }
        void Begin(NaturalBehaviorQaPlan plan, INaturalBehaviorQaRecorder recorder);
        void Tick(double unscaledDeltaSeconds);
        bool TryTakeCaptureRequest(out string label);
        void OnCaptureCompleted(NaturalBehaviorQaCaptureArtifact artifact);
        void End();
    }

    public static class NaturalBehaviorQaLifecycleGuard
    {
        public static void RequireCanStart(bool sessionActive, bool isPlayingOrWillChangePlaymode)
        {
            if (sessionActive)
                throw new InvalidOperationException("A natural behavior QA session is already active.");
            if (isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Natural behavior QA must start from stable Edit Mode.");
        }

        public static bool IsAbandonedPreparation(
            bool sessionActive,
            int stage,
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            return sessionActive && stage == 1 && !isPlaying && !isPlayingOrWillChangePlaymode;
        }
    }
}

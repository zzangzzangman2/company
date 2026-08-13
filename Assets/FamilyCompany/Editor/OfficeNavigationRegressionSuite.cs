using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Navigation;

namespace FamilyCompany.Editor
{
    public sealed class OfficeNavigationRegressionReport
    {
        internal OfficeNavigationRegressionReport(
            int seeds,
            int paths,
            int replans,
            int segmentChecks,
            int oracleSegmentChecks,
            int counterexampleChecks,
            int facingPresentationChecks,
            int gaitPresentationChecks,
            int collisionSlideChecks,
            int motionPartitionChecks,
            int trafficPermutationChecks,
            float maximumStretch,
            int maximumExpandedNodes,
            int deadlockTicks)
        {
            Seeds = seeds;
            Paths = paths;
            Replans = replans;
            SegmentChecks = segmentChecks;
            OracleSegmentChecks = oracleSegmentChecks;
            CounterexampleChecks = counterexampleChecks;
            FacingPresentationChecks = facingPresentationChecks;
            GaitPresentationChecks = gaitPresentationChecks;
            CollisionSlideChecks = collisionSlideChecks;
            MotionPartitionChecks = motionPartitionChecks;
            TrafficPermutationChecks = trafficPermutationChecks;
            MaximumStretch = maximumStretch;
            MaximumExpandedNodes = maximumExpandedNodes;
            DeadlockTicks = deadlockTicks;
        }

        public int Seeds { get; }
        public int Paths { get; }
        public int Replans { get; }
        public int SegmentChecks { get; }
        public int OracleSegmentChecks { get; }
        public int CounterexampleChecks { get; }
        public int FacingPresentationChecks { get; }
        public int GaitPresentationChecks { get; }
        public int CollisionSlideChecks { get; }
        public int MotionPartitionChecks { get; }
        public int TrafficPermutationChecks { get; }
        public float MaximumStretch { get; }
        public int MaximumExpandedNodes { get; }
        public int DeadlockTicks { get; }
    }

    public static class OfficeNavigationRegressionSuite
    {
        private const float CellSize = 0.25f;
        private const float AgentRadius = 0.30f;
        private static readonly OfficeNavBounds Bounds = new OfficeNavBounds(0f, 0f, 20f, 14f);
        private static readonly OfficeNavPoint[] SemanticDestinations =
        {
            new OfficeNavPoint(1.0f, 7.0f),
            new OfficeNavPoint(3.0f, 5.25f),
            new OfficeNavPoint(7.0f, 5.25f),
            new OfficeNavPoint(11.0f, 8.75f),
            new OfficeNavPoint(15.0f, 8.75f),
            new OfficeNavPoint(19.0f, 7.0f),
            new OfficeNavPoint(5.0f, 12.5f),
            new OfficeNavPoint(15.0f, 1.5f),
            new OfficeNavPoint(1.0f, 6.25f)
        };
        private static readonly OfficeNavPoint[] Starts =
        {
            new OfficeNavPoint(2.0f, 7.0f),
            new OfficeNavPoint(10.0f, 7.0f),
            new OfficeNavPoint(18.0f, 7.0f)
        };

        public static OfficeNavigationRegressionReport Run(int seedCount = 128)
        {
            if (seedCount < 100) throw new ArgumentOutOfRangeException(nameof(seedCount), "At least 100 seeds are required.");
            var pathCount = 0;
            var replanCount = 0;
            var segmentChecks = 0;
            var oracleSegmentChecks = 0;
            var maximumStretch = 0f;
            var maximumExpanded = 0;
            for (var seed = 0; seed < seedCount; seed++)
            {
                var obstacles = CreateFurniture(seed);
                var pathfinder = new DeterministicOfficePathfinder(
                    Bounds,
                    CellSize,
                    obstacles,
                    AgentRadius);
                Require(pathfinder.CellCount <= OfficeNavigationLimits.MaxGridCells, "grid cell cap");
                var start = Starts[seed % Starts.Length];
                for (var destinationIndex = 0; destinationIndex < SemanticDestinations.Length; destinationIndex++)
                {
                    var destination = SemanticDestinations[destinationIndex];
                    Require(pathfinder.TryFindPath(start, destination, out var first),
                        $"seed {seed} destination {destinationIndex} path");
                    Require(pathfinder.TryFindPath(start, destination, out var second),
                        $"seed {seed} destination {destinationIndex} deterministic replay path");
                    Require(PathsEqual(first, second),
                        $"seed {seed} destination {destinationIndex} deterministic replay equality");
                    ValidateCollisionFree(
                        pathfinder,
                        obstacles,
                        first,
                        seed,
                        destinationIndex,
                        ref segmentChecks,
                        ref oracleSegmentChecks);
                    var stretch = first.RawGridCostMetres <= 0.0001f
                        ? 1f
                        : first.LengthMetres / first.RawGridCostMetres;
                    Require(first.LengthMetres <= first.RawGridCostMetres + CellSize * 3.0f,
                        $"seed {seed} destination {destinationIndex} path stretch {stretch:F3}");
                    maximumStretch = Math.Max(maximumStretch, stretch);
                    maximumExpanded = Math.Max(maximumExpanded, first.ExpandedNodes);
                    pathCount++;
                }

                var reversedObstacles = new List<OfficeNavObstacle>(obstacles);
                reversedObstacles.Reverse();
                var reversedPathfinder = new DeterministicOfficePathfinder(
                    Bounds,
                    CellSize,
                    reversedObstacles,
                    AgentRadius);
                var deterministicDestination = SemanticDestinations[seed % SemanticDestinations.Length];
                Require(pathfinder.TryFindPath(start, deterministicDestination, out var orderedPath),
                    $"seed {seed} ordered obstacle determinism path");
                Require(reversedPathfinder.TryFindPath(start, deterministicDestination, out var reversedPath),
                    $"seed {seed} reversed obstacle determinism path");
                Require(PathsEqual(orderedPath, reversedPath),
                    $"seed {seed} obstacle-order independent tie-break");

                var changed = new List<OfficeNavObstacle>(obstacles)
                {
                    new OfficeNavObstacle($"placed-{seed:D3}", 9.1f, 6.35f, 10.2f, 7.25f)
                };
                var changedPathfinder = new DeterministicOfficePathfinder(
                    Bounds,
                    CellSize,
                    changed,
                    AgentRadius);
                Require(changedPathfinder.TryFindPath(Starts[0], Starts[2], out var replanA),
                    $"seed {seed} placement replan A");
                Require(changedPathfinder.TryFindPath(Starts[0], Starts[2], out var replanB),
                    $"seed {seed} placement replan B");
                Require(PathsEqual(replanA, replanB), $"seed {seed} placement replan determinism");
                ValidateCollisionFree(
                    changedPathfinder,
                    changed,
                    replanA,
                    seed,
                    -1,
                    ref segmentChecks,
                    ref oracleSegmentChecks);
                replanCount++;
            }

            var counterexampleChecks = ValidateCounterexamples();
            var facingPresentationChecks = ValidateFacingPresentation();
            var gaitPresentationChecks = ValidateGaitPresentation();
            var collisionSlideChecks = ValidateCollisionSlideSelection();
            var motionPartitionChecks = ValidateMotionPartitioning();
            var trafficPermutationChecks = ValidateTrafficPermutationIndependence();
            var deadlockTicks = ValidateDeadlockRecovery();
            return new OfficeNavigationRegressionReport(
                seedCount,
                pathCount,
                replanCount,
                segmentChecks,
                oracleSegmentChecks,
                counterexampleChecks,
                facingPresentationChecks,
                gaitPresentationChecks,
                collisionSlideChecks,
                motionPartitionChecks,
                trafficPermutationChecks,
                maximumStretch,
                maximumExpanded,
                deadlockTicks);
        }

        private static List<OfficeNavObstacle> CreateFurniture(int seed)
        {
            var random = new StableTestRandom((uint)(seed + 1));
            var obstacles = new List<OfficeNavObstacle>();
            for (var index = 0; index < 22; index++)
            {
                var placed = false;
                for (var attempt = 0; attempt < 80 && !placed; attempt++)
                {
                    var width = random.Range(0.45f, 1.35f);
                    var depth = random.Range(0.40f, 1.10f);
                    var x = random.Range(1.5f + width, 18.5f - width);
                    var upper = (index & 1) == 0;
                    var z = upper
                        ? random.Range(9.25f, 12.75f)
                        : random.Range(1.25f, 4.75f);
                    var candidate = new OfficeNavObstacle(
                        $"furniture-{seed:D3}-{index:D2}",
                        x - width * 0.5f,
                        z - depth * 0.5f,
                        x + width * 0.5f,
                        z + depth * 0.5f);
                    if (BlocksGuaranteedAccess(candidate)) continue;
                    obstacles.Add(candidate);
                    placed = true;
                }

                Require(placed, $"seed {seed} furniture placement {index}");
            }

            return obstacles;
        }

        private static bool BlocksGuaranteedAccess(OfficeNavObstacle candidate)
        {
            var expanded = candidate.Expanded(AgentRadius + 0.18f);
            if (expanded.Intersects(0f, 5.95f, 20f, 8.05f)) return true;
            for (var index = 0; index < SemanticDestinations.Length; index++)
            {
                var destination = SemanticDestinations[index];
                if (expanded.Contains(destination)) return true;
                var minZ = Math.Min(7f, destination.Z);
                var maxZ = Math.Max(7f, destination.Z);
                if (expanded.Intersects(destination.X - 0.70f, minZ, destination.X + 0.70f, maxZ))
                    return true;
            }

            return false;
        }

        private static void ValidateCollisionFree(
            DeterministicOfficePathfinder pathfinder,
            IReadOnlyList<OfficeNavObstacle> obstacles,
            OfficeNavPath path,
            int seed,
            int destinationIndex,
            ref int segmentChecks,
            ref int oracleSegmentChecks)
        {
            Require(path.Waypoints.Count > 0, $"seed {seed} destination {destinationIndex} non-empty path");
            for (var index = 0; index < path.Waypoints.Count; index++)
            {
                Require(pathfinder.IsPointWalkable(path.Waypoints[index]),
                    $"seed {seed} destination {destinationIndex} waypoint {index} overlap");
                if (index == 0) continue;
                Require(pathfinder.IsSegmentWalkable(path.Waypoints[index - 1], path.Waypoints[index]),
                    $"seed {seed} destination {destinationIndex} segment {index - 1}->{index} overlap");
                Require(IndependentSegmentIsClear(
                        path.Waypoints[index - 1],
                        path.Waypoints[index],
                        pathfinder.Bounds,
                        obstacles,
                        AgentRadius + 0.04f),
                    $"seed {seed} destination {destinationIndex} independent oracle segment {index - 1}->{index}");
                segmentChecks++;
                oracleSegmentChecks++;
            }
        }

        private static int ValidateCounterexamples()
        {
            var checks = 0;
            ValidateExactWorldSegmentCounterexample();
            checks++;
            ValidatePartialBoundaryCell();
            checks++;
            ValidateDiagonalCornerCutting();
            checks++;
            ValidateProjectionPolicy();
            checks += 3;
            ValidateObstacleEscapeDirection();
            checks += 3;
            ValidateCapacityLimits();
            checks += 3;
            return checks;
        }

        private static void ValidateExactWorldSegmentCounterexample()
        {
            var pathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 4f, 4f),
                1f,
                new[] { new OfficeNavObstacle("blocked-cell", 1.10f, 1.10f, 1.90f, 1.90f) },
                0f,
                0f);
            var start = new OfficeNavPoint(3.971f, 1.834f);
            var end = new OfficeNavPoint(1.061f, 0.981f);
            Require(!pathfinder.IsSegmentWalkable(start, end), "exact world segment counterexample forward");
            Require(!pathfinder.IsSegmentWalkable(end, start), "exact world segment counterexample reverse");
            Require(!IndependentSegmentIsClear(
                    start,
                    end,
                    pathfinder.Bounds,
                    new[] { new OfficeNavObstacle("blocked-cell", 1f, 1f, 2f, 2f) },
                    0f),
                "exact world segment independent oracle detects blocked cell");
        }

        private static void ValidatePartialBoundaryCell()
        {
            var bounds = new OfficeNavBounds(0f, 0f, 1.05f, 1f);
            var goal = new OfficeNavPoint(1.025f, 0.5f);
            var pathfinder = new DeterministicOfficePathfinder(
                bounds,
                0.20f,
                new[] { new OfficeNavObstacle("tail", 1.01f, 0.20f, 1.04f, 0.80f) },
                0f,
                0f);
            Require(pathfinder.Width == 6, "partial boundary ceil width");
            Require(!pathfinder.IsPointWalkable(goal), "partial boundary obstacle blocks tail point");
            if (pathfinder.TryFindPath(new OfficeNavPoint(0.5f, 0.5f), goal, out var path))
                Require(path.GoalProjected, "partial boundary goal cannot be accepted without projection");
        }

        private static void ValidateDiagonalCornerCutting()
        {
            var pathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 2f, 2f),
                1f,
                new[]
                {
                    new OfficeNavObstacle("east-block", 1.10f, 0.10f, 1.90f, 0.90f),
                    new OfficeNavObstacle("north-block", 0.10f, 1.10f, 0.90f, 1.90f)
                },
                0f,
                0f);
            Require(!pathfinder.TryFindPath(
                    new OfficeNavPoint(0.5f, 0.5f),
                    new OfficeNavPoint(1.5f, 1.5f),
                    out _),
                "diagonal movement cannot cut between two blocked cardinal cells");
        }

        private static void ValidateProjectionPolicy()
        {
            var smallObstacle = new OfficeNavObstacle("projection-blocker", 4.70f, 4.70f, 5.30f, 5.30f);
            var pathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 10f, 10f),
                CellSize,
                new[] { smallObstacle },
                AgentRadius);
            Require(pathfinder.TryFindPath(new OfficeNavPoint(3f, 5f), new OfficeNavPoint(5f, 5f), out var projected),
                "near blocked endpoint can produce recovery path");
            Require(projected.GoalProjected, "near blocked endpoint is explicitly projected");
            Require(!OfficeNavigationPathAcceptance.CanUseForSemanticDestination(projected),
                "projected goal is rejected for semantic completion");

            var largePathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 10f, 10f),
                CellSize,
                new[] { new OfficeNavObstacle("large", 2f, 2f, 8f, 8f) },
                AgentRadius);
            Require(!largePathfinder.TryFindPath(
                    new OfficeNavPoint(1f, 5f),
                    new OfficeNavPoint(5f, 5f),
                    out _),
                "far blocked endpoint projection is rejected");

            var disconnected = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 10f, 10f),
                CellSize,
                new[] { new OfficeNavObstacle("wall", 4.8f, 0f, 5.2f, 10f) },
                AgentRadius);
            Require(!disconnected.TryFindPath(
                    new OfficeNavPoint(3f, 5f),
                    new OfficeNavPoint(7f, 5f),
                    out _),
                "projection cannot cross unreachable component");
        }

        private static void ValidateObstacleEscapeDirection()
        {
            var start = new OfficeNavPoint(0.25f, 1f);
            Require(OfficeNavigationGeometryQueries.MovesTowardNearestBoundary(
                    start,
                    new OfficeNavPoint(-0.10f, 1.20f),
                    0f,
                    0f,
                    2f,
                    2f),
                "embedded agent can move monotonically through its nearest boundary");
            Require(!OfficeNavigationGeometryQueries.MovesTowardNearestBoundary(
                    start,
                    new OfficeNavPoint(2.25f, 1f),
                    0f,
                    0f,
                    2f,
                    2f),
                "embedded agent cannot tunnel through the far side of an obstacle");
            Require(OfficeNavigationGeometryQueries.MovesTowardNearestBoundary(
                    new OfficeNavPoint(0f, 1f),
                    new OfficeNavPoint(-0.10f, 1f),
                    0f,
                    0f,
                    2f,
                    2f),
                "agent touching a closed obstacle boundary can escape outward");
        }

        private static void ValidateCapacityLimits()
        {
            var tooMany = new List<OfficeNavObstacle>();
            for (var index = 0; index <= OfficeNavigationLimits.MaxObstacles; index++)
                tooMany.Add(new OfficeNavObstacle($"cap-{index:D3}", 1f, 1f, 1.1f, 1.1f));
            RequireThrows<ArgumentOutOfRangeException>(
                () => new DeterministicOfficePathfinder(Bounds, CellSize, tooMany, AgentRadius),
                "obstacle cap rejects unbounded allocation");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new DeterministicOfficePathfinder(
                    new OfficeNavBounds(0f, 0f, 100f, 100f),
                    OfficeNavigationLimits.MinimumCellSize,
                    Array.Empty<OfficeNavObstacle>(),
                    AgentRadius),
                "grid cap rejects unbounded allocation");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new DeterministicOfficePathfinder(
                    new OfficeNavBounds(-float.MaxValue, -1f, float.MaxValue, 1f),
                    OfficeNavigationLimits.MinimumCellSize,
                    Array.Empty<OfficeNavObstacle>(),
                    AgentRadius),
                "grid dimension overflow fails closed before allocation");
        }

        private static int ValidateFacingPresentation()
        {
            var checks = 0;
            OfficeLocomotionFacingState state = OfficeLocomotionFacingState.Initial(0);
            OfficeNavPoint heading24 = HeadingFromSouthAngle(24f);
            OfficeLocomotionFacingResult result = OfficeLocomotionPresentationRules.ResolveFacing(
                state, heading24, heading24, 0.04f, false);
            Require(result.State.VisualDirection == 0, "small facing hysteresis holds south at 24 degrees");
            checks++;

            OfficeNavPoint heading27 = HeadingFromSouthAngle(27f);
            result = OfficeLocomotionPresentationRules.ResolveFacing(
                result.State, heading27, heading27, 0.04f, false);
            Require(result.State.VisualDirection == 1,
                "actual walking direction commits without a backwards-facing stabilization frame");
            checks++;

            OfficeNavPoint heading23 = HeadingFromSouthAngle(23f);
            result = OfficeLocomotionPresentationRules.ResolveFacing(
                result.State, heading23, heading23, 0.04f, false);
            Require(result.State.VisualDirection == 1, "small return jitter does not flip immediately");
            checks++;

            state = OfficeLocomotionFacingState.Initial(6);
            OfficeNavPoint semanticEast = new OfficeNavPoint(1f, 0f);
            OfficeNavPoint projectedNorth = new OfficeNavPoint(0f, 1f);
            result = OfficeLocomotionPresentationRules.ResolveFacing(
                state, semanticEast, projectedNorth, 0.05f, true);
            state = result.State;
            Require(state.VisualDirection == 4 && !result.UsedSemanticHeading,
                "a collision slide immediately faces its actual north motion");
            checks++;

            state = OfficeLocomotionFacingState.Initial(0);
            OfficeNavPoint semanticNorth = new OfficeNavPoint(0f, 1f);
            OfficeNavPoint inertiaSouth = new OfficeNavPoint(0f, -1f);
            result = OfficeLocomotionPresentationRules.ResolveFacing(
                state, semanticNorth, inertiaSouth, 0.04f, false);
            Require(result.State.VisualDirection == 0 && !result.UsedSemanticHeading,
                "residual south motion never renders as a north-facing backwards step");
            result = OfficeLocomotionPresentationRules.ResolveFacing(
                result.State, semanticNorth, new OfficeNavPoint(0f, 0f), 0.04f, false);
            Require(result.State.VisualDirection == 4 && result.UsedSemanticHeading,
                "a stopped actor can turn in place toward the requested north heading");
            checks += 2;
            return checks;
        }

        private static int ValidateCollisionSlideSelection()
        {
            var checks = 0;
            OfficeNavPoint intended = new OfficeNavPoint(1f, 1f);
            OfficeNavPoint xPreferred = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(2f, 0.5f),
                new OfficeNavPoint(0f, 0f),
                true,
                true,
                "agent");
            Require(Math.Abs(xPreferred.X) > 0.9f && Math.Abs(xPreferred.Z) < 0.0001f,
                "collision slide maximizes semantic X progress");
            checks++;

            OfficeNavPoint zPreferred = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(0.5f, 2f),
                new OfficeNavPoint(0f, 0f),
                true,
                true,
                "agent");
            Require(Math.Abs(zPreferred.Z) > 0.9f && Math.Abs(zPreferred.X) < 0.0001f,
                "collision slide maximizes semantic Z progress");
            checks++;

            OfficeNavPoint continuous = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(1f, 1f),
                new OfficeNavPoint(0f, 0.5f),
                true,
                true,
                "agent");
            Require(Math.Abs(continuous.Z) > 0.9f && Math.Abs(continuous.X) < 0.0001f,
                "collision slide keeps the previous stable axis on a semantic tie");
            checks++;

            OfficeNavPoint stableA = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(1f, 1f),
                new OfficeNavPoint(0f, 0f),
                true,
                true,
                "stable-agent");
            OfficeNavPoint stableB = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(1f, 1f),
                new OfficeNavPoint(0f, 0f),
                true,
                true,
                "stable-agent");
            Require(stableA.Equals(stableB), "collision slide tie-break is deterministic");
            checks++;

            OfficeNavPoint none = OfficeCollisionSlideRules.SelectBestAxisSlide(
                intended,
                new OfficeNavPoint(1f, 1f),
                new OfficeNavPoint(0f, 0f),
                false,
                false,
                "agent");
            Require(none.SqrMagnitude <= 0.000001f, "collision slide fails closed when both axes are blocked");
            checks++;
            return checks;
        }

        private static int ValidateGaitPresentation()
        {
            var checks = 0;
            const float stride = OfficeLocomotionGaitRules.DefaultStrideLength;
            OfficeLocomotionGaitState at30 = SimulateDistance(1.394f, 30, 1.7f, 0);
            OfficeLocomotionGaitState at60 = SimulateDistance(1.394f, 60, 1.7f, 0);
            OfficeLocomotionGaitState at120 = SimulateDistance(1.394f, 120, 1.7f, 0);
            Require(at30.Frame == at60.Frame && at60.Frame == at120.Frame,
                "gait phase is identical at 30/60/120fps for the same distance");
            Require(Math.Abs(at30.AccumulatedDistance - at120.AccumulatedDistance) <= 0.0001f,
                "gait distance is render-partition independent");
            checks += 2;

            OfficeLocomotionGaitState slow = SimulateDistance(0.91f, 46, 0.91f / 1.15f, 0);
            OfficeLocomotionGaitState fast = SimulateDistance(0.91f, 28, 0.91f / 1.65f, 0);
            float slowPhase = OfficeLocomotionGaitRules.Phase01(slow.AccumulatedDistance, stride);
            float fastPhase = OfficeLocomotionGaitRules.Phase01(fast.AccumulatedDistance, stride);
            float circularPhaseError = Math.Min(Math.Abs(slowPhase - fastPhase),
                1f - Math.Abs(slowPhase - fastPhase));
            Require(circularPhaseError <= 0.0001f,
                "1.15 and 1.65 movement speeds use the same phase at the same distance");
            checks++;

            OfficeLocomotionGaitState moving = SimulateDistance(0.58f, 20, 0.40f, 0);
            int frameBeforeStop = moving.Frame;
            OfficeLocomotionGaitState briefStop = OfficeLocomotionGaitRules.Resolve(
                moving, 0f, 0.05f, false, 0, stride);
            Require(briefStop.Phase == OfficeLocomotionPhase.Stopping && briefStop.Frame == frameBeforeStop,
                "a brief stop preserves the current distance phase");
            OfficeLocomotionGaitState restarted = OfficeLocomotionGaitRules.Resolve(
                briefStop, 0.04f, 0.05f, true, 0, stride);
            Require(restarted.Phase != OfficeLocomotionPhase.Idle,
                "movement restarted within 100ms does not snap to idle");
            checks += 2;

            OfficeLocomotionGaitState shortMove = OfficeLocomotionGaitRules.Resolve(
                OfficeLocomotionGaitState.Initial(0), 0.10f, 0.06f, true, 0, stride);
            OfficeLocomotionGaitState shortStop = OfficeLocomotionGaitRules.Resolve(
                shortMove, 0f, 0.05f, false, 0, stride);
            Require(shortStop.Phase == OfficeLocomotionPhase.ShortShuffle,
                "movement under 0.3 stride settles as a short shuffle");
            OfficeLocomotionGaitState settled = OfficeLocomotionGaitRules.Resolve(
                shortStop, 0f, 0.06f, false, 0, stride);
            Require(settled.Phase == OfficeLocomotionPhase.Idle,
                "short shuffle reaches idle after the 100ms settle window");
            checks += 2;

            OfficeLocomotionGaitState forward = SimulateDistance(0.45f, 12, 0.30f, 0);
            OfficeLocomotionGaitState movingReverse = OfficeLocomotionGaitRules.Resolve(
                forward, 0.02f, 0.03f, true, 4, stride);
            Require(movingReverse.Phase != OfficeLocomotionPhase.Pivot &&
                    movingReverse.DisplayDirection == 4,
                "actual reverse displacement never keeps the old forward-facing sprite");
            OfficeLocomotionGaitState pivot = OfficeLocomotionGaitRules.Resolve(
                forward, 0f, 0.03f, true, 4, stride);
            Require(pivot.Phase == OfficeLocomotionPhase.Pivot && pivot.DisplayDirection == 0,
                "a stopped 180-degree reversal enters a planted-foot pivot");
            pivot = OfficeLocomotionGaitRules.Resolve(pivot, 0f, 0.05f, true, 4, stride);
            Require(pivot.DisplayDirection == 4 && pivot.Phase != OfficeLocomotionPhase.Pivot,
                "pivot commits the new direction after its short transition");
            OfficeLocomotionGaitState quarterTurn = OfficeLocomotionGaitRules.Resolve(
                OfficeLocomotionGaitState.Initial(0), 0f, 0.02f, true, 2, stride);
            Require(quarterTurn.Phase == OfficeLocomotionPhase.Pivot &&
                    quarterTurn.DisplayDirection == 0,
                "a stopped 90-degree turn enters a planted-foot pivot");
            checks += 4;
            return checks;
        }

        private static OfficeLocomotionGaitState SimulateDistance(
            float distance,
            int steps,
            float duration,
            int direction)
        {
            OfficeLocomotionGaitState state = OfficeLocomotionGaitState.Initial(direction);
            for (var step = 0; step < steps; step++)
            {
                state = OfficeLocomotionGaitRules.Resolve(
                    state,
                    distance / steps,
                    duration / steps,
                    true,
                    direction);
            }
            return state;
        }

        private static int ValidateDeadlockRecovery()
        {
            const float deltaTime = 0.05f;
            const float speed = 1.0f;
            const float radius = 0.28f;
            var aPosition = new OfficeNavPoint(2f, 1f);
            var bPosition = new OfficeNavPoint(8f, 1f);
            var aGoal = new OfficeNavPoint(8.5f, 1f);
            var bGoal = new OfficeNavPoint(1.5f, 1f);
            var aStuck = 0f;
            var bStuck = 0f;
            var aReached = false;
            var bReached = false;
            var recoverySide = 0f;
            var observedRecovery = false;
            for (var tick = 1; tick <= 800; tick++)
            {
                var aDesired = aReached ? new OfficeNavPoint(0f, 0f) : (aGoal - aPosition).Normalized * speed;
                var bDesired = bReached ? new OfficeNavPoint(0f, 0f) : (bGoal - bPosition).Normalized * speed;
                var aState = new OfficeTrafficAgentState("agent_a", aPosition, aDesired, radius, aStuck);
                var bState = new OfficeTrafficAgentState("agent_b", bPosition, bDesired, radius, bStuck);
                var aDecision = OfficeNavigationTrafficRules.Resolve(aState, new[] { bState });
                var bDecision = OfficeNavigationTrafficRules.Resolve(bState, new[] { aState });
                if (bDecision.RecoveryWeight > 0f && Math.Abs(bDecision.RecoveryDirection.Z) > 0.0001f)
                {
                    observedRecovery = true;
                    var currentSide = Math.Sign(bDecision.RecoveryDirection.Z);
                    if (Math.Abs(recoverySide) > 0.1f)
                        Require(Math.Abs(currentSide - recoverySide) <= 0.1f,
                            "deadlock recovery does not oscillate between lateral sides");
                    recoverySide = currentSide;
                }
                var aVelocity = ResolveVelocity(aDesired, aDecision, speed);
                var bVelocity = ResolveVelocity(bDesired, bDecision, speed);
                var nextA = ClampCorridor(aPosition + aVelocity * deltaTime, radius);
                var nextB = ClampCorridor(bPosition + bVelocity * deltaTime, radius);
                var overlap = OfficeNavPoint.Distance(nextA, nextB) < radius * 2f + 0.01f;
                if (overlap)
                {
                    nextB = bPosition;
                    bStuck += deltaTime;
                }
                else
                {
                    bStuck = bDecision.IsYielding ? bStuck + deltaTime : Math.Max(0f, bStuck - deltaTime);
                }

                aStuck = aDecision.IsYielding ? aStuck + deltaTime : Math.Max(0f, aStuck - deltaTime);
                Require(OfficeNavPoint.Distance(aPosition, nextA) <= speed * deltaTime + 0.0001f,
                    "deadlock recovery does not teleport A");
                Require(OfficeNavPoint.Distance(bPosition, nextB) <= speed * deltaTime + 0.0001f,
                    "deadlock recovery does not teleport B");
                aPosition = nextA;
                bPosition = nextB;
                Require(OfficeNavPoint.Distance(aPosition, bPosition) >= radius * 2f - 0.02f,
                    "deadlock recovery agent overlap");
                aReached = aReached || OfficeNavPoint.Distance(aPosition, aGoal) <= 0.12f;
                bReached = bReached || OfficeNavPoint.Distance(bPosition, bGoal) <= 0.12f;
                if (aReached && bReached)
                {
                    Require(observedRecovery, "deadlock regression exercised lateral recovery");
                    return tick;
                }
            }

            throw new InvalidOperationException(
                $"Deadlock recovery did not complete: A={aPosition}, B={bPosition}, stuck={aStuck:F2}/{bStuck:F2}.");
        }

        private static OfficeNavPoint ResolveVelocity(
            OfficeNavPoint desired,
            OfficeTrafficDecision decision,
            float speed)
        {
            var velocity = desired * decision.ForwardScale +
                           decision.RecoveryDirection * (speed * decision.RecoveryWeight);
            return velocity.Magnitude <= speed ? velocity : velocity.Normalized * speed;
        }

        private static OfficeNavPoint ClampCorridor(OfficeNavPoint value, float radius)
        {
            return new OfficeNavPoint(
                Math.Max(radius, Math.Min(10f - radius, value.X)),
                Math.Max(radius, Math.Min(2f - radius, value.Z)));
        }

        private static int ValidateMotionPartitioning()
        {
            var checks = 0;
            var current = new OfficeNavPoint(0.25f, -0.10f);
            var target = new OfficeNavPoint(1.60f, 0.35f);
            const float rate = 3.25f;
            const float total = 0.80f;
            var one = OfficeNavigationMotionIntegrator.IntegrateVelocity(current, target, rate, total);
            var splitA = OfficeNavigationMotionIntegrator.IntegrateVelocity(current, target, rate, 0.30f);
            var splitB = OfficeNavigationMotionIntegrator.IntegrateVelocity(splitA.Velocity, target, rate, 0.50f);
            Require(OfficeNavPoint.Distance(one.Velocity, splitB.Velocity) <= 0.00001f,
                "motion velocity is partition independent");
            Require(OfficeNavPoint.Distance(
                        one.Displacement,
                        splitA.Displacement + splitB.Displacement) <= 0.00001f,
                "motion displacement is partition independent");
            checks += 2;

            var largeDelta = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                new OfficeNavPoint(0f, 0f),
                new OfficeNavPoint(2f, 0f),
                6f,
                2f);
            var clamped = OfficeNavigationMotionIntegrator.ClampDisplacement(largeDelta.Displacement, 0.35f);
            Require(clamped.Magnitude <= 0.350001f, "large delta displacement clamps to waypoint distance");
            checks++;

            var largeStepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(2f);
            Require(largeStepCount == 40, "large delta is divided into deterministic bounded steps");
            var reconstructedDelta = 0f;
            for (var index = 0; index < largeStepCount; index++)
            {
                var step = OfficeNavigationMotionIntegrator.ResolveStepDelta(2f, index, largeStepCount);
                Require(step > 0f && step <= OfficeNavigationMotionIntegrator.MaximumStableStepSeconds + 0.000001f,
                    "large delta step remains within collision probe horizon");
                reconstructedDelta += step;
            }

            Require(Math.Abs(reconstructedDelta - 2f) <= 0.00001f,
                "large delta slicing preserves elapsed time");
            checks += 2;

            float playerReverseRate = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(1f, 0f),
                new OfficeNavPoint(-1f, 0f),
                7.5f,
                true);
            float npcReverseRate = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(1f, 0f),
                new OfficeNavPoint(-1f, 0f),
                7.5f,
                false);
            float playerStopRate = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(1f, 0f),
                new OfficeNavPoint(0f, 0f),
                7.5f,
                true);
            Require(Math.Abs(playerReverseRate - 13.5f) <= 0.00001f,
                "direct player reversal decelerates before changing direction");
            Require(Math.Abs(npcReverseRate - 7.5f) <= 0.00001f,
                "NPC steering retains the canonical acceleration");
            Require(Math.Abs(playerStopRate - 12.75f) <= 0.00001f,
                "direct player release stops faster than acceleration");
            checks += 3;

            float farArrival = OfficeNavigationMotionIntegrator.ResolveArrivalSpeedScale(
                OfficeNavigationMotionIntegrator.FinalApproachSlowRadius);
            float middleArrival = OfficeNavigationMotionIntegrator.ResolveArrivalSpeedScale(
                OfficeNavigationMotionIntegrator.FinalApproachSlowRadius * 0.5f);
            float nearArrival = OfficeNavigationMotionIntegrator.ResolveArrivalSpeedScale(0f);
            Require(Math.Abs(farArrival - 1f) <= 0.00001f,
                "final approach keeps full speed outside the easing radius");
            Require(middleArrival > nearArrival && middleArrival < farArrival,
                "final approach speed eases monotonically");
            Require(Math.Abs(nearArrival - OfficeNavigationMotionIntegrator.MinimumArrivalSpeedScale) <= 0.00001f,
                "final approach retains a non-zero crawl speed");
            checks += 3;
            return checks;
        }

        private static int ValidateTrafficPermutationIndependence()
        {
            var self = new OfficeTrafficAgentState(
                "agent_m",
                new OfficeNavPoint(5f, 1f),
                new OfficeNavPoint(1f, 0f),
                0.28f,
                0.95f);
            var left = new OfficeTrafficAgentState(
                "agent_a",
                new OfficeNavPoint(5.7f, 1f),
                new OfficeNavPoint(-1f, 0f),
                0.28f,
                0.1f);
            var right = new OfficeTrafficAgentState(
                "agent_z",
                new OfficeNavPoint(4.4f, 1.2f),
                new OfficeNavPoint(0.8f, -0.1f),
                0.28f,
                0.2f);
            var forward = OfficeNavigationTrafficRules.Resolve(self, new[] { left, right });
            var reverse = OfficeNavigationTrafficRules.Resolve(self, new[] { right, left });
            Require(TrafficDecisionsEqual(forward, reverse), "traffic decision is peer-order independent");
            return 1;
        }

        private static bool TrafficDecisionsEqual(OfficeTrafficDecision left, OfficeTrafficDecision right)
        {
            return Math.Abs(left.ForwardScale - right.ForwardScale) <= 0.000001f &&
                   OfficeNavPoint.Distance(left.RecoveryDirection, right.RecoveryDirection) <= 0.000001f &&
                   Math.Abs(left.RecoveryWeight - right.RecoveryWeight) <= 0.000001f &&
                   left.IsYielding == right.IsYielding &&
                   left.ShouldReplan == right.ShouldReplan;
        }

        private static bool IndependentSegmentIsClear(
            OfficeNavPoint start,
            OfficeNavPoint end,
            OfficeNavBounds bounds,
            IReadOnlyList<OfficeNavObstacle> obstacles,
            float inflation)
        {
            if (!bounds.Contains(start) || !bounds.Contains(end)) return false;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width / CellSize));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Depth / CellSize));
            var minCellX = Math.Max(0, Math.Min(width - 1,
                (int)Math.Floor((Math.Min(start.X, end.X) - bounds.MinX) / CellSize)));
            var maxCellX = Math.Max(0, Math.Min(width - 1,
                (int)Math.Floor((Math.Max(start.X, end.X) - bounds.MinX) / CellSize)));
            var minCellZ = Math.Max(0, Math.Min(height - 1,
                (int)Math.Floor((Math.Min(start.Z, end.Z) - bounds.MinZ) / CellSize)));
            var maxCellZ = Math.Max(0, Math.Min(height - 1,
                (int)Math.Floor((Math.Max(start.Z, end.Z) - bounds.MinZ) / CellSize)));
            for (var z = minCellZ; z <= maxCellZ; z++)
            {
                for (var x = minCellX; x <= maxCellX; x++)
                {
                    var cellMinX = bounds.MinX + x * CellSize;
                    var cellMinZ = bounds.MinZ + z * CellSize;
                    var cellMaxX = Math.Min(bounds.MaxX, cellMinX + CellSize);
                    var cellMaxZ = Math.Min(bounds.MaxZ, cellMinZ + CellSize);
                    var blocked = false;
                    for (var obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
                    {
                        var obstacle = obstacles[obstacleIndex];
                        if (obstacle.MaxX + inflation >= cellMinX &&
                            obstacle.MinX - inflation <= cellMaxX &&
                            obstacle.MaxZ + inflation >= cellMinZ &&
                            obstacle.MinZ - inflation <= cellMaxZ)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked && IndependentSegmentIntersectsRectangle(
                            start, end, cellMinX, cellMinZ, cellMaxX, cellMaxZ))
                        return false;
                }
            }

            return true;
        }

        private static bool IndependentSegmentIntersectsRectangle(
            OfficeNavPoint start,
            OfficeNavPoint end,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            return IndependentPointInside(start, minX, minZ, maxX, maxZ) ||
                   IndependentPointInside(end, minX, minZ, maxX, maxZ) ||
                   IndependentSegmentsIntersect(start, end, new OfficeNavPoint(minX, minZ), new OfficeNavPoint(maxX, minZ)) ||
                   IndependentSegmentsIntersect(start, end, new OfficeNavPoint(maxX, minZ), new OfficeNavPoint(maxX, maxZ)) ||
                   IndependentSegmentsIntersect(start, end, new OfficeNavPoint(maxX, maxZ), new OfficeNavPoint(minX, maxZ)) ||
                   IndependentSegmentsIntersect(start, end, new OfficeNavPoint(minX, maxZ), new OfficeNavPoint(minX, minZ));
        }

        private static bool IndependentPointInside(
            OfficeNavPoint point,
            float minX,
            float minZ,
            float maxX,
            float maxZ)
        {
            return point.X >= minX && point.X <= maxX && point.Z >= minZ && point.Z <= maxZ;
        }

        private static bool IndependentSegmentsIntersect(
            OfficeNavPoint firstStart,
            OfficeNavPoint firstEnd,
            OfficeNavPoint secondStart,
            OfficeNavPoint secondEnd)
        {
            const float epsilon = 0.000001f;
            var firstSideA = IndependentCross(firstStart, firstEnd, secondStart);
            var firstSideB = IndependentCross(firstStart, firstEnd, secondEnd);
            var secondSideA = IndependentCross(secondStart, secondEnd, firstStart);
            var secondSideB = IndependentCross(secondStart, secondEnd, firstEnd);
            if ((firstSideA > epsilon && firstSideB < -epsilon || firstSideA < -epsilon && firstSideB > epsilon) &&
                (secondSideA > epsilon && secondSideB < -epsilon || secondSideA < -epsilon && secondSideB > epsilon))
                return true;
            return Math.Abs(firstSideA) <= epsilon && IndependentOnSegment(firstStart, firstEnd, secondStart) ||
                   Math.Abs(firstSideB) <= epsilon && IndependentOnSegment(firstStart, firstEnd, secondEnd) ||
                   Math.Abs(secondSideA) <= epsilon && IndependentOnSegment(secondStart, secondEnd, firstStart) ||
                   Math.Abs(secondSideB) <= epsilon && IndependentOnSegment(secondStart, secondEnd, firstEnd);
        }

        private static float IndependentCross(OfficeNavPoint start, OfficeNavPoint end, OfficeNavPoint point)
        {
            return (end.X - start.X) * (point.Z - start.Z) -
                   (end.Z - start.Z) * (point.X - start.X);
        }

        private static bool IndependentOnSegment(
            OfficeNavPoint start,
            OfficeNavPoint end,
            OfficeNavPoint point)
        {
            const float epsilon = 0.000001f;
            return point.X >= Math.Min(start.X, end.X) - epsilon &&
                   point.X <= Math.Max(start.X, end.X) + epsilon &&
                   point.Z >= Math.Min(start.Z, end.Z) - epsilon &&
                   point.Z <= Math.Max(start.Z, end.Z) + epsilon;
        }

        private static void AxesFromSouthAngle(float degrees, out float horizontal, out float vertical)
        {
            var radians = degrees * Math.PI / 180d;
            horizontal = (float)-Math.Sin(radians);
            vertical = (float)-Math.Cos(radians);
        }

        private static OfficeNavPoint HeadingFromSouthAngle(float degrees)
        {
            AxesFromSouthAngle(degrees, out float horizontal, out float vertical);
            return new OfficeNavPoint(horizontal, vertical);
        }

        private static bool PathsEqual(OfficeNavPath left, OfficeNavPath right)
        {
            if (left.StartProjected != right.StartProjected || left.GoalProjected != right.GoalProjected ||
                left.ExpandedNodes != right.ExpandedNodes || left.Waypoints.Count != right.Waypoints.Count)
                return false;
            for (var index = 0; index < left.Waypoints.Count; index++)
            {
                if (!left.Waypoints[index].Equals(right.Waypoints[index])) return false;
            }

            return true;
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Office navigation regression failed: " + label + ".");
        }

        private static void RequireThrows<TException>(Action action, string label)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Office navigation regression failed: " + label + ".");
        }

        private struct StableTestRandom
        {
            private uint _state;

            public StableTestRandom(uint seed)
            {
                _state = seed == 0 ? 0x9E3779B9u : seed;
            }

            public float Range(float minimum, float maximum)
            {
                _state = unchecked(_state * 1664525u + 1013904223u);
                var unit = (_state >> 8) / 16777216f;
                return minimum + (maximum - minimum) * unit;
            }
        }
    }
}

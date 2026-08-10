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
            float maximumStretch,
            int maximumExpandedNodes,
            int deadlockTicks)
        {
            Seeds = seeds;
            Paths = paths;
            Replans = replans;
            SegmentChecks = segmentChecks;
            MaximumStretch = maximumStretch;
            MaximumExpandedNodes = maximumExpandedNodes;
            DeadlockTicks = deadlockTicks;
        }

        public int Seeds { get; }
        public int Paths { get; }
        public int Replans { get; }
        public int SegmentChecks { get; }
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
                    ValidateCollisionFree(pathfinder, first, seed, destinationIndex, ref segmentChecks);
                    var stretch = first.RawGridCostMetres <= 0.0001f
                        ? 1f
                        : first.LengthMetres / first.RawGridCostMetres;
                    Require(first.LengthMetres <= first.RawGridCostMetres + CellSize * 3.0f,
                        $"seed {seed} destination {destinationIndex} path stretch {stretch:F3}");
                    maximumStretch = Math.Max(maximumStretch, stretch);
                    maximumExpanded = Math.Max(maximumExpanded, first.ExpandedNodes);
                    pathCount++;
                }

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
                ValidateCollisionFree(changedPathfinder, replanA, seed, -1, ref segmentChecks);
                replanCount++;
            }

            ValidateBlockedEndpointProjection();
            ValidateFacingHysteresis();
            var deadlockTicks = ValidateDeadlockRecovery();
            return new OfficeNavigationRegressionReport(
                seedCount,
                pathCount,
                replanCount,
                segmentChecks,
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
            OfficeNavPath path,
            int seed,
            int destinationIndex,
            ref int segmentChecks)
        {
            Require(path.Waypoints.Count > 0, $"seed {seed} destination {destinationIndex} non-empty path");
            for (var index = 0; index < path.Waypoints.Count; index++)
            {
                Require(pathfinder.IsPointWalkable(path.Waypoints[index]),
                    $"seed {seed} destination {destinationIndex} waypoint {index} overlap");
                if (index == 0) continue;
                Require(pathfinder.IsSegmentWalkable(path.Waypoints[index - 1], path.Waypoints[index]),
                    $"seed {seed} destination {destinationIndex} segment {index - 1}->{index} overlap");
                segmentChecks++;
            }
        }

        private static void ValidateBlockedEndpointProjection()
        {
            var obstacle = new OfficeNavObstacle("projection-blocker", 4f, 4f, 6f, 6f);
            var pathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 10f, 10f),
                CellSize,
                new[] { obstacle },
                AgentRadius);
            Require(pathfinder.TryFindPath(new OfficeNavPoint(5f, 5f), new OfficeNavPoint(4.5f, 4.5f), out var path),
                "blocked endpoint projection path");
            Require(path.StartProjected && path.GoalProjected, "blocked endpoints projected");
            Require(path.Waypoints.Count > 0 &&
                    pathfinder.IsPointWalkable(path.Waypoints[0]) &&
                    pathfinder.IsPointWalkable(path.Waypoints[path.Waypoints.Count - 1]),
                "blocked endpoint projection targets are walkable");
        }

        private static void ValidateFacingHysteresis()
        {
            var direction = 0;
            foreach (var degrees in new[] { 20f, 24f, 27f, 23f, 29f })
            {
                AxesFromSouthAngle(degrees, out var horizontal, out var vertical);
                direction = OfficeFacingHysteresisRules.ResolveDirection(horizontal, vertical, direction, 7.5f);
                Require(direction == 0, $"facing hysteresis held south at {degrees:F1}");
            }

            AxesFromSouthAngle(31f, out var switchHorizontal, out var switchVertical);
            direction = OfficeFacingHysteresisRules.ResolveDirection(
                switchHorizontal,
                switchVertical,
                direction,
                7.5f);
            Require(direction == 1, "facing hysteresis commits after margin");
            AxesFromSouthAngle(24f, out var returnHorizontal, out var returnVertical);
            direction = OfficeFacingHysteresisRules.ResolveDirection(
                returnHorizontal,
                returnVertical,
                direction,
                7.5f);
            Require(direction == 1, "facing hysteresis prevents immediate flip back");
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
            for (var tick = 1; tick <= 800; tick++)
            {
                var aDesired = aReached ? new OfficeNavPoint(0f, 0f) : (aGoal - aPosition).Normalized * speed;
                var bDesired = bReached ? new OfficeNavPoint(0f, 0f) : (bGoal - bPosition).Normalized * speed;
                var aState = new OfficeTrafficAgentState("agent_a", aPosition, aDesired, radius, aStuck);
                var bState = new OfficeTrafficAgentState("agent_b", bPosition, bDesired, radius, bStuck);
                var aDecision = OfficeNavigationTrafficRules.Resolve(aState, new[] { bState });
                var bDecision = OfficeNavigationTrafficRules.Resolve(bState, new[] { aState });
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
                if (aReached && bReached) return tick;
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

        private static void AxesFromSouthAngle(float degrees, out float horizontal, out float vertical)
        {
            var radians = degrees * Math.PI / 180d;
            horizontal = (float)-Math.Sin(radians);
            vertical = (float)-Math.Cos(radians);
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

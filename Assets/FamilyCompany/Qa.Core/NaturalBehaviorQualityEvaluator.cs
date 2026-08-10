using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FamilyCompany.Qa.NaturalBehavior
{
    public sealed class NaturalBehaviorQaMetric
    {
        public NaturalBehaviorQaMetric(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Name { get; }
        public string Value { get; }
    }

    public sealed class NaturalBehaviorQaGateResult
    {
        public NaturalBehaviorQaGateResult(
            string gateId,
            string name,
            IEnumerable<string> issues,
            IEnumerable<NaturalBehaviorQaMetric> metrics)
        {
            GateId = gateId;
            Name = name;
            Issues = new List<string>(issues ?? Array.Empty<string>()).ToArray();
            Metrics = new List<NaturalBehaviorQaMetric>(metrics ?? Array.Empty<NaturalBehaviorQaMetric>()).ToArray();
        }

        public string GateId { get; }
        public string Name { get; }
        public IReadOnlyList<string> Issues { get; }
        public IReadOnlyList<NaturalBehaviorQaMetric> Metrics { get; }
        public bool Passed => Issues.Count == 0;
        public string Marker => $"NATURAL_BEHAVIOR_QA_GATE_{GateId}: {(Passed ? "PASS" : "FAIL")}";
    }

    public sealed class NaturalBehaviorQaResult
    {
        public NaturalBehaviorQaResult(string runHash, IEnumerable<NaturalBehaviorQaGateResult> gates)
        {
            RunHash = runHash ?? string.Empty;
            Gates = new List<NaturalBehaviorQaGateResult>(gates ?? throw new ArgumentNullException(nameof(gates))).ToArray();
        }

        public string RunHash { get; }
        public IReadOnlyList<NaturalBehaviorQaGateResult> Gates { get; }
        public bool Passed => Gates.Count == 6 && Gates.All(item => item.Passed);
        public string Marker => $"NATURAL_BEHAVIOR_QUALITY_GATE: {(Passed ? "PASS" : "FAIL")}";
    }

    public static class NaturalBehaviorQualityEvaluator
    {
        public static NaturalBehaviorQaResult Evaluate(NaturalBehaviorQaRun run, NaturalBehaviorQualityBar qualityBar = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var bar = qualityBar ?? new NaturalBehaviorQualityBar();
            var gates = new[]
            {
                EvaluateSpatialSafety(run, bar),
                EvaluatePaths(run, bar),
                EvaluateMotion(run, bar),
                EvaluateSeating(run, bar),
                EvaluateWorkActions(run, bar),
                EvaluateNavigationRebuild(run, bar)
            };
            return new NaturalBehaviorQaResult(NaturalBehaviorQaHash.Compute(run), gates);
        }

        private static NaturalBehaviorQaGateResult EvaluateSpatialSafety(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.SpatialSafety, issues);

            var footprintCoverageFailures = 0;
            var missingMemberCoverage = 0;
            foreach (var layout in DistinctLayouts(run))
            {
                var footprints = FootprintsFor(run, layout.ScenarioId, layout.LayoutSeed).ToArray();
                var footprintCount = footprints.Select(item => item.FurnitureId).Distinct(StringComparer.Ordinal).Count();
                if (footprintCount != layout.FurnitureCount)
                {
                    footprintCoverageFailures++;
                    AddIssue(issues,
                        $"{layout.ScenarioId}/{layout.LayoutSeed} declares {layout.FurnitureCount} furniture footprints but recorded {footprintCount}.");
                }

                foreach (var memberId in run.Plan.ExpectedMemberIds)
                {
                    if (run.Footpoints.Any(item => SameLayout(item.ScenarioId, item.LayoutSeed, layout) &&
                                                   string.Equals(item.MemberId, memberId, StringComparison.Ordinal))) continue;
                    missingMemberCoverage++;
                    AddIssue(issues, $"No footpoint coverage for {memberId} in {layout.ScenarioId}/{layout.LayoutSeed}.");
                }
            }

            var overlapCount = 0;
            foreach (var sample in run.Footpoints.Where(item => item.Visible))
            {
                foreach (var furniture in FootprintsFor(run, sample.ScenarioId, sample.LayoutSeed))
                {
                    if (!CircleOverlapsPolygon(sample.Position, sample.RadiusMeters, furniture.Footprint, bar.NumericTolerance))
                        continue;
                    overlapCount++;
                    AddIssue(issues,
                        $"Footpoint overlap: {sample.MemberId} with {furniture.FurnitureId} at {sample.TimeSeconds:F3}s " +
                        $"({sample.ScenarioId}/{sample.LayoutSeed}).");
                }
            }

            var wallCrossingCount = 0;
            foreach (var group in MotionGroups(run))
            {
                var samples = group.OrderBy(item => item.TimeSeconds).ToArray();
                var walls = FootprintsFor(run, group.Key.ScenarioId, group.Key.LayoutSeed)
                    .Where(item => item.BlocksMovement)
                    .ToArray();
                for (var index = 1; index < samples.Length; index++)
                {
                    var previous = samples[index - 1];
                    var current = samples[index];
                    if (!previous.Visible || !current.Visible) continue;
                    foreach (var wall in walls)
                    {
                        if (!SegmentCrossesPolygonInterior(previous.Position, current.Position, wall.Footprint, bar.NumericTolerance))
                            continue;
                        wallCrossingCount++;
                        AddIssue(issues,
                            $"Movement crossed {wall.FurnitureId}: {current.MemberId} at {current.TimeSeconds:F3}s " +
                            $"({current.ScenarioId}/{current.LayoutSeed}).");
                    }
                }
            }

            metrics.Add(Metric("recordedFootpoints", run.Footpoints.Count));
            metrics.Add(Metric("footprintCoverageFailures", footprintCoverageFailures));
            metrics.Add(Metric("missingMemberCoverage", missingMemberCoverage));
            metrics.Add(Metric("footpointOverlaps", overlapCount));
            metrics.Add(Metric("wallOrPartitionCrossings", wallCrossingCount));
            return Gate("01_SPATIAL", "footprints, footpoints, walls and partitions", issues, metrics);
        }

        private static NaturalBehaviorQaGateResult EvaluatePaths(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.PathQuality, issues);

            RequireRoundTrips(run, NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, bar, issues);
            var expectedSeeds = run.Plan.RandomLayoutSeeds.Take(bar.RequiredRandomLayoutSeeds).ToArray();
            var randomLayouts = run.Layouts
                .Where(item => string.Equals(item.ScenarioId, NaturalBehaviorQaScenarioIds.RandomFurniture,
                    StringComparison.Ordinal))
                .ToArray();
            var presentSeeds = new HashSet<int>(randomLayouts.Select(item => item.LayoutSeed));
            foreach (var seed in expectedSeeds)
            {
                if (!presentSeeds.Contains(seed))
                {
                    AddIssue(issues, $"Missing required random furniture seed {seed}.");
                    continue;
                }

                var layouts = randomLayouts.Where(item => item.LayoutSeed == seed).ToArray();
                if (layouts.Length < bar.RequiredDeterminismRepeats)
                    AddIssue(issues, $"Random layout {seed} has {layouts.Length} repeats; expected {bar.RequiredDeterminismRepeats}.");
                if (layouts.Any(item => !item.Succeeded || item.FurnitureCount < bar.RequiredFurniturePerRandomLayout))
                    AddIssue(issues, $"Random layout {seed} failed placement or recorded fewer than {bar.RequiredFurniturePerRandomLayout} furniture items.");
                if (layouts.Select(item => item.StableHash).Distinct(StringComparer.Ordinal).Count() != 1)
                    AddIssue(issues, $"Random layout {seed} produced a non-deterministic layout hash.");
                RequireRoundTrips(run, NaturalBehaviorQaScenarioIds.RandomFurniture, seed, bar, issues);
            }

            var allPaths = run.Paths.ToArray();
            foreach (var path in allPaths)
            {
                if (!path.Succeeded) AddIssue(issues, $"Path failed: {PathKey(path)}.");
                if (path.DirectDistanceMeters <= bar.NumericTolerance || path.TravelledDistanceMeters < 0d)
                    AddIssue(issues, $"Path has invalid distance values: {PathKey(path)}.");
                if (path.ReplanCount < 0 || path.ReplanCount > bar.MaximumReplansPerRoute)
                    AddIssue(issues, $"Path replanned {path.ReplanCount} times: {PathKey(path)}.");
                if (path.DeadlockSeconds > bar.MaximumDeadlockSeconds + bar.NumericTolerance)
                    AddIssue(issues, $"Path deadlocked for {path.DeadlockSeconds:F3}s: {PathKey(path)}.");
                if (path.UnsafeTraversalCount != 0)
                    AddIssue(issues, $"Path recorded {path.UnsafeTraversalCount} unsafe traversals: {PathKey(path)}.");
            }

            foreach (var group in allPaths.GroupBy(item =>
                         $"{item.ScenarioId}|{item.LayoutSeed}|{item.MemberId}|{item.FromDestinationId}|{item.ToDestinationId}",
                         StringComparer.Ordinal))
            {
                if (group.Count() < bar.RequiredDeterminismRepeats)
                    AddIssue(issues, $"Path {group.Key} lacks deterministic repeats.");
                if (group.Select(item => item.StablePathHash).Distinct(StringComparer.Ordinal).Count() != 1)
                    AddIssue(issues, $"Path {group.Key} produced different stable hashes.");
            }

            var stretches = allPaths
                .Where(item => item.Succeeded && item.DirectDistanceMeters > bar.NumericTolerance)
                .Select(item => item.TravelledDistanceMeters / item.DirectDistanceMeters)
                .OrderBy(item => item)
                .ToArray();
            var p95 = Percentile(stretches, 0.95d);
            var maximum = stretches.Length == 0 ? double.PositiveInfinity : stretches[stretches.Length - 1];
            if (p95 > bar.MaximumPathStretchP95 + bar.NumericTolerance)
                AddIssue(issues, $"Path stretch p95 {p95:F3} exceeds {bar.MaximumPathStretchP95:F3}.");
            if (maximum > bar.MaximumPathStretch + bar.NumericTolerance)
                AddIssue(issues, $"Maximum path stretch {maximum:F3} exceeds {bar.MaximumPathStretch:F3}.");

            metrics.Add(Metric("randomLayoutSeeds", presentSeeds.Count));
            metrics.Add(Metric("pathObservations", allPaths.Length));
            metrics.Add(Metric("pathSuccesses", allPaths.Count(item => item.Succeeded)));
            metrics.Add(Metric("pathStretchP95", p95));
            metrics.Add(Metric("pathStretchMaximum", maximum));
            metrics.Add(Metric("maximumReplans", allPaths.Length == 0 ? -1 : allPaths.Max(item => item.ReplanCount)));
            metrics.Add(Metric("maximumDeadlockSeconds", allPaths.Length == 0 ? double.PositiveInfinity : allPaths.Max(item => item.DeadlockSeconds)));
            return Gate("02_PATHS", "semantic round trips and 100 seeded furniture layouts", issues, metrics);
        }

        private static NaturalBehaviorQaGateResult EvaluateMotion(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.MotionContinuity, issues);

            var maximumGap = 0d;
            var maximumDelta = 0d;
            var maximumSpeed = 0d;
            var maximumAcceleration = 0d;
            var directionFlips = 0;
            var cornerJitters = 0;
            foreach (var group in MotionGroups(run))
            {
                var samples = group.OrderBy(item => item.TimeSeconds).ToArray();
                if (samples.Length < 3)
                {
                    AddIssue(issues,
                        $"Motion trace {group.Key.ScenarioId}/{group.Key.LayoutSeed}/{group.Key.MemberId} has fewer than three samples.");
                    continue;
                }

                var previousSpeed = double.NaN;
                var directionChanges = new List<DirectionChange>();
                for (var index = 1; index < samples.Length; index++)
                {
                    var previous = samples[index - 1];
                    var current = samples[index];
                    if (!previous.Visible || !current.Visible) continue;
                    var deltaTime = current.TimeSeconds - previous.TimeSeconds;
                    if (deltaTime <= 0d)
                    {
                        AddIssue(issues, $"Non-increasing motion timestamp for {current.MemberId}.");
                        continue;
                    }

                    var delta = previous.Position.DistanceTo(current.Position);
                    var speed = delta / deltaTime;
                    maximumGap = Math.Max(maximumGap, deltaTime);
                    maximumDelta = Math.Max(maximumDelta, delta);
                    maximumSpeed = Math.Max(maximumSpeed, speed);
                    if (deltaTime > bar.MaximumMotionSampleGapSeconds + bar.NumericTolerance)
                        AddIssue(issues, $"Motion sample gap {deltaTime:F3}s exceeds the limit for {current.MemberId}.");
                    if (delta > bar.MaximumFrameDeltaMeters + bar.NumericTolerance)
                        AddIssue(issues, $"Position delta {delta:F3}m indicates a pop/teleport for {current.MemberId}.");
                    if (speed > bar.MaximumSpeedMetersPerSecond + bar.NumericTolerance)
                        AddIssue(issues, $"Speed {speed:F3}m/s exceeds the cap for {current.MemberId}.");

                    if (!double.IsNaN(previousSpeed))
                    {
                        var acceleration = Math.Abs(speed - previousSpeed) / deltaTime;
                        maximumAcceleration = Math.Max(maximumAcceleration, acceleration);
                        if (acceleration > bar.MaximumAccelerationMetersPerSecondSquared + bar.NumericTolerance)
                            AddIssue(issues, $"Acceleration {acceleration:F3}m/s^2 exceeds the cap for {current.MemberId}.");
                    }
                    previousSpeed = speed;

                    var directionDistance = CircularDirectionDistance(previous.DirectionIndex, current.DirectionIndex);
                    if (directionDistance == 4 && deltaTime <= bar.DirectionFlipWindowSeconds + bar.NumericTolerance &&
                        speed >= bar.MinimumDirectionFlipSpeed)
                    {
                        directionFlips++;
                        AddIssue(issues, $"180-degree direction flip detected for {current.MemberId} at {current.TimeSeconds:F3}s.");
                    }

                    var signedDirectionDelta = SignedDirectionDelta(previous.DirectionIndex, current.DirectionIndex);
                    if (signedDirectionDelta != 0)
                    {
                        directionChanges.Add(new DirectionChange(current.TimeSeconds, Math.Sign(signedDirectionDelta), current.Position));
                        if (IsCornerJitter(directionChanges, bar))
                        {
                            cornerJitters++;
                            AddIssue(issues, $"Corner direction jitter detected for {current.MemberId} at {current.TimeSeconds:F3}s.");
                            directionChanges.Clear();
                        }
                    }
                }
            }

            metrics.Add(Metric("motionSamples", run.MotionSamples.Count));
            metrics.Add(Metric("maximumSampleGapSeconds", maximumGap));
            metrics.Add(Metric("maximumFrameDeltaMeters", maximumDelta));
            metrics.Add(Metric("maximumSpeedMetersPerSecond", maximumSpeed));
            metrics.Add(Metric("maximumAccelerationMetersPerSecondSquared", maximumAcceleration));
            metrics.Add(Metric("directionFlips", directionFlips));
            metrics.Add(Metric("cornerJitters", cornerJitters));
            return Gate("03_MOTION", "teleport, pop, speed, acceleration, direction and corner continuity", issues, metrics);
        }

        private static NaturalBehaviorQaGateResult EvaluateSeating(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.Seating, issues);

            var completeMembers = new HashSet<string>(StringComparer.Ordinal);
            var maximumFootError = 0d;
            var clippingPixels = 0;
            var occlusionFailures = 0;
            foreach (var group in run.SeatingFrames.GroupBy(item => item.SessionId, StringComparer.Ordinal))
            {
                var samples = group.OrderBy(item => item.SessionTimeSeconds).ToArray();
                if (samples.Length == 0) continue;
                var memberId = samples[0].MemberId;
                if (samples.Any(item => !string.Equals(item.MemberId, memberId, StringComparison.Ordinal)))
                {
                    AddIssue(issues, $"Seating session {group.Key} contains multiple members.");
                    continue;
                }

                var approach = samples.LastOrDefault(item => item.Phase == QaSeatingPhase.Approach);
                var firstSit = samples.FirstOrDefault(item => item.Phase == QaSeatingPhase.SitDown);
                if (approach == null || firstSit == null)
                {
                    AddIssue(issues, $"Seating session {group.Key} lacks approach or sit-down samples.");
                }
                else
                {
                    var error = approach.FootPixel1920.DistanceTo(firstSit.FootPixel1920);
                    maximumFootError = Math.Max(maximumFootError, error);
                    if (error > bar.MaximumSeatFootErrorPixels1920 + bar.NumericTolerance)
                        AddIssue(issues, $"Seat footpoint error {error:F3}px exceeds 1920p tolerance in {group.Key}.");
                }

                foreach (var sample in samples.Where(item => item.Phase == QaSeatingPhase.SitDown ||
                                                               item.Phase == QaSeatingPhase.Work ||
                                                               item.Phase == QaSeatingPhase.StandUp))
                {
                    clippingPixels += Math.Max(0, sample.ChairBodyClipPixelCount) +
                                      Math.Max(0, sample.DeskBodyClipPixelCount);
                    if (!sample.HasOcclusionMeasurement || !sample.ChairForegroundOrderCorrect ||
                        !sample.DeskForegroundOrderCorrect)
                    {
                        occlusionFailures++;
                        AddIssue(issues, $"Missing/incorrect chair-desk foreground occlusion in {group.Key} at {sample.SessionTimeSeconds:F3}s.");
                    }
                    if (sample.ChairBodyClipPixelCount != 0 || sample.DeskBodyClipPixelCount != 0)
                        AddIssue(issues,
                            $"Body clipping measured chair={sample.ChairBodyClipPixelCount}px, " +
                            $"desk={sample.DeskBodyClipPixelCount}px in {group.Key}.");
                }

                if (ValidateSeatingSequence(samples, bar, issues, group.Key)) completeMembers.Add(memberId);
            }

            foreach (var memberId in run.Plan.ExpectedMemberIds)
            {
                if (!completeMembers.Contains(memberId))
                    AddIssue(issues, $"No complete seating sequence was recorded for {memberId}.");
            }

            metrics.Add(Metric("seatingSessions", run.SeatingFrames.Select(item => item.SessionId).Distinct(StringComparer.Ordinal).Count()));
            metrics.Add(Metric("completeMembers", completeMembers.Count));
            metrics.Add(Metric("maximumFootErrorPixels1920", maximumFootError));
            metrics.Add(Metric("bodyClippingPixels", clippingPixels));
            metrics.Add(Metric("occlusionFailures", occlusionFailures));
            return Gate("04_SEATING", "seat footpoint, transition frames, foreground occlusion and clipping", issues, metrics);
        }

        private static NaturalBehaviorQaGateResult EvaluateWorkActions(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.WorkActions, issues);
            if (run.ObservationGameSeconds + bar.NumericTolerance < bar.RequiredObservationGameSeconds)
                AddIssue(issues,
                    $"Observed {run.ObservationGameSeconds:F1} game seconds; expected at least {bar.RequiredObservationGameSeconds:F1}.");

            foreach (var memberId in run.Plan.ExpectedMemberIds)
            {
                var workWindows = run.WorkWindows
                    .Where(item => string.Equals(item.MemberId, memberId, StringComparison.Ordinal))
                    .ToArray();
                if (workWindows.Length != 1)
                {
                    AddIssue(issues, $"Expected one Work observation window for {memberId}; found {workWindows.Length}.");
                    continue;
                }
                var workWindow = workWindows[0];
                if (workWindow.ObservationGameSeconds + bar.NumericTolerance < bar.RequiredObservationGameSeconds ||
                    workWindow.AccumulatedWorkSeconds + bar.NumericTolerance < bar.RequiredObservationGameSeconds)
                {
                    AddIssue(issues,
                        $"{memberId} recorded observation={workWindow.ObservationGameSeconds:F1}s, " +
                        $"Work={workWindow.AccumulatedWorkSeconds:F1}s; both must cover {bar.RequiredObservationGameSeconds:F1}s.");
                }

                foreach (var contract in bar.WorkActionCooldowns)
                {
                    var events = run.WorkActions
                        .Where(item => string.Equals(item.MemberId, memberId, StringComparison.Ordinal) && item.Action == contract.Action)
                        .OrderBy(item => item.TimeSeconds)
                        .ToArray();
                    if (events.Length == 0)
                    {
                        AddIssue(issues, $"{memberId} never displayed {contract.Action} during the observation window.");
                        continue;
                    }

                    var previousAccumulatedWork = 0d;
                    for (var index = 0; index < events.Length; index++)
                    {
                        var item = events[index];
                        if (item.Phase != QaMotionPhase.Work || !item.VisualVisible)
                            AddIssue(issues, $"{memberId} displayed {contract.Action} outside visible Work phase.");
                        var elapsed = item.AccumulatedWorkSeconds - previousAccumulatedWork;
                        if (item.AccumulatedWorkSeconds <= previousAccumulatedWork)
                            AddIssue(issues, $"{memberId}/{contract.Action} has non-increasing accumulated Work time.");
                        if (Math.Abs(elapsed - item.WorkSecondsSincePreviousSameAction) > bar.NumericTolerance)
                            AddIssue(issues,
                                $"{memberId}/{contract.Action} reported cooldown {item.WorkSecondsSincePreviousSameAction:F3}s " +
                                $"but accumulated Work delta is {elapsed:F3}s.");
                        if (elapsed <= 0d || elapsed > contract.MaximumWorkSeconds + bar.NumericTolerance)
                            AddIssue(issues, $"{memberId}/{contract.Action} exceeded its maximum cooldown ({elapsed:F3}s).");
                        if (index > 0 && elapsed + bar.NumericTolerance < contract.MinimumWorkSeconds)
                            AddIssue(issues, $"{memberId}/{contract.Action} violated its minimum cooldown ({elapsed:F3}s).");
                        previousAccumulatedWork = item.AccumulatedWorkSeconds;
                    }
                    var tail = workWindow.AccumulatedWorkSeconds - previousAccumulatedWork;
                    if (tail < -bar.NumericTolerance || tail > contract.MaximumWorkSeconds + bar.NumericTolerance)
                        AddIssue(issues,
                            $"{memberId}/{contract.Action} left an uncovered Work tail of {tail:F3}s.");
                }

                var productivity = run.Productivity
                    .Where(item => string.Equals(item.MemberId, memberId, StringComparison.Ordinal))
                    .ToArray();
                if (!productivity.Any(item => item.Phase == QaMotionPhase.Work && item.ProductivityDelta > bar.NumericTolerance))
                    AddIssue(issues, $"No positive Work productivity was observed for {memberId}.");
            }

            foreach (var sample in run.Productivity)
            {
                if (sample.Phase == QaMotionPhase.Work || Math.Abs(sample.ProductivityDelta) <= bar.NumericTolerance) continue;
                AddIssue(issues,
                    $"Productivity delta {sample.ProductivityDelta:F6} occurred during {sample.Phase} for {sample.MemberId}.");
            }

            metrics.Add(Metric("observationGameSeconds", run.ObservationGameSeconds));
            metrics.Add(Metric("workWindows", run.WorkWindows.Count));
            metrics.Add(Metric("minimumMemberWorkSeconds",
                run.WorkWindows.Count == 0 ? 0d : run.WorkWindows.Min(item => item.AccumulatedWorkSeconds)));
            metrics.Add(Metric("workActionEvents", run.WorkActions.Count));
            metrics.Add(Metric("typingEvents", run.WorkActions.Count(item => item.Action == QaWorkVisualAction.Typing)));
            metrics.Add(Metric("mouseEvents", run.WorkActions.Count(item => item.Action == QaWorkVisualAction.Mouse)));
            metrics.Add(Metric("drinkEvents", run.WorkActions.Count(item => item.Action == QaWorkVisualAction.Drink)));
            metrics.Add(Metric("nonWorkProductivityEvents",
                run.Productivity.Count(item => item.Phase != QaMotionPhase.Work && Math.Abs(item.ProductivityDelta) > bar.NumericTolerance)));
            return Gate("05_WORK", "30-minute Typing/Mouse/Drink contracts and Work-only productivity", issues, metrics);
        }

        private static NaturalBehaviorQaGateResult EvaluateNavigationRebuild(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQualityBar bar)
        {
            var issues = new List<string>();
            var metrics = new List<NaturalBehaviorQaMetric>();
            RequireCapability(run, NaturalBehaviorQaCapability.NavigationRebuild, issues);

            var expectedSeeds = run.Plan.RandomLayoutSeeds.Take(bar.RequiredRandomLayoutSeeds).ToArray();
            foreach (var seed in expectedSeeds)
            {
                var observations = run.NavigationRebuilds
                    .Where(item => string.Equals(item.ScenarioId, NaturalBehaviorQaScenarioIds.RandomFurniture,
                                       StringComparison.Ordinal) && item.LayoutSeed == seed)
                    .ToArray();
                if (observations.Length == 0)
                {
                    AddIssue(issues, $"No navigation rebuild observation for random layout seed {seed}.");
                    continue;
                }

                foreach (var observation in observations)
                {
                    var duration = observation.CompletedTimeSeconds - observation.RequestedTimeSeconds;
                    if (duration < 0d || duration > bar.MaximumNavigationRebuildSeconds + bar.NumericTolerance)
                        AddIssue(issues, $"Navigation rebuild for seed {seed} took {duration:F3}s.");
                    if (observation.ActivePathCount <= 0)
                        AddIssue(issues, $"Navigation rebuild for seed {seed} did not exercise an in-progress path.");
                    if (observation.SafelyReplannedPathCount != observation.ActivePathCount)
                        AddIssue(issues,
                            $"Navigation rebuild for seed {seed} safely replanned {observation.SafelyReplannedPathCount}/" +
                            $"{observation.ActivePathCount} active paths.");
                    if (observation.UnsafeTraversalCount != 0 ||
                        observation.ProgressWhileUnsafeSeconds > bar.NumericTolerance)
                        AddIssue(issues, $"Unsafe progress occurred while rebuilding navigation for seed {seed}.");
                }
            }

            var durations = run.NavigationRebuilds
                .Select(item => item.CompletedTimeSeconds - item.RequestedTimeSeconds)
                .ToArray();
            metrics.Add(Metric("navigationRebuilds", run.NavigationRebuilds.Count));
            metrics.Add(Metric("maximumRebuildSeconds", durations.Length == 0 ? double.PositiveInfinity : durations.Max()));
            metrics.Add(Metric("unsafeTraversals", run.NavigationRebuilds.Sum(item => item.UnsafeTraversalCount)));
            metrics.Add(Metric("activePaths", run.NavigationRebuilds.Sum(item => item.ActivePathCount)));
            metrics.Add(Metric("safelyReplannedPaths", run.NavigationRebuilds.Sum(item => item.SafelyReplannedPathCount)));
            return Gate("06_REBUILD", "navigation rebuild latency and safe in-progress replanning", issues, metrics);
        }

        private static bool ValidateSeatingSequence(
            IReadOnlyList<SeatingFrameObservation> samples,
            NaturalBehaviorQualityBar bar,
            ICollection<string> issues,
            string sessionId)
        {
            var stage = 0;
            var sitFrame = -1;
            var workFrame = -1;
            var standFrame = -1;
            var workFramesSeen = new HashSet<int>();
            var hadApproach = false;
            var completed = false;
            foreach (var sample in samples)
            {
                switch (sample.Phase)
                {
                    case QaSeatingPhase.Approach:
                        if (stage != 0) AddIssue(issues, $"Approach re-entered after transition start in {sessionId}.");
                        hadApproach = true;
                        break;
                    case QaSeatingPhase.SitDown:
                        if (!hadApproach || stage > 1)
                        {
                            AddIssue(issues, $"SitDown appeared out of order in {sessionId}.");
                            break;
                        }
                        stage = 1;
                        if (!AdvanceLinearFrame(ref sitFrame, sample.FrameIndex, bar.SitDownFrameCount))
                            AddIssue(issues, $"Invalid SitDown frame order {sample.FrameIndex} in {sessionId}.");
                        break;
                    case QaSeatingPhase.Work:
                        if (sitFrame != bar.SitDownFrameCount - 1 || stage > 2)
                        {
                            AddIssue(issues, $"Work began before all SitDown frames in {sessionId}.");
                            break;
                        }
                        stage = 2;
                        if (!AdvanceLoopFrame(ref workFrame, sample.FrameIndex, bar.WorkFrameCount))
                            AddIssue(issues, $"Invalid Work loop frame order {sample.FrameIndex} in {sessionId}.");
                        workFramesSeen.Add(sample.FrameIndex);
                        break;
                    case QaSeatingPhase.StandUp:
                        if (workFramesSeen.Count != bar.WorkFrameCount || stage < 2 || stage > 3)
                        {
                            AddIssue(issues, $"StandUp began before a complete Work loop in {sessionId}.");
                            break;
                        }
                        stage = 3;
                        if (!AdvanceLinearFrame(ref standFrame, sample.FrameIndex, bar.StandUpFrameCount))
                            AddIssue(issues, $"Invalid StandUp frame order {sample.FrameIndex} in {sessionId}.");
                        break;
                    case QaSeatingPhase.Complete:
                        if (standFrame != bar.StandUpFrameCount - 1)
                            AddIssue(issues, $"Seating completed before all StandUp frames in {sessionId}.");
                        else
                            completed = true;
                        stage = 4;
                        break;
                    default:
                        AddIssue(issues, $"Unknown seating phase in {sessionId}.");
                        break;
                }
            }

            if (!completed) AddIssue(issues, $"Seating session {sessionId} did not complete.");
            return completed && hadApproach && sitFrame == bar.SitDownFrameCount - 1 &&
                   workFramesSeen.Count == bar.WorkFrameCount && standFrame == bar.StandUpFrameCount - 1;
        }

        private static bool AdvanceLinearFrame(ref int current, int next, int count)
        {
            if (next < 0 || next >= count) return false;
            if (current == next) return true;
            if (next != current + 1) return false;
            current = next;
            return true;
        }

        private static bool AdvanceLoopFrame(ref int current, int next, int count)
        {
            if (next < 0 || next >= count) return false;
            if (current == next) return true;
            var expected = current < 0 ? 0 : (current + 1) % count;
            if (next != expected) return false;
            current = next;
            return true;
        }

        private static void RequireRoundTrips(
            NaturalBehaviorQaRun run,
            string scenarioId,
            int layoutSeed,
            NaturalBehaviorQualityBar bar,
            ICollection<string> issues)
        {
            foreach (var memberId in run.Plan.ExpectedMemberIds)
            {
                foreach (var destinationId in run.Plan.SemanticDestinationIds)
                {
                    RequireRepeatedLeg(run, scenarioId, layoutSeed, memberId,
                        run.Plan.SemanticOriginId, destinationId, bar, issues);
                    RequireRepeatedLeg(run, scenarioId, layoutSeed, memberId,
                        destinationId, run.Plan.SemanticOriginId, bar, issues);
                }
            }
        }

        private static void RequireRepeatedLeg(
            NaturalBehaviorQaRun run,
            string scenarioId,
            int layoutSeed,
            string memberId,
            string from,
            string to,
            NaturalBehaviorQualityBar bar,
            ICollection<string> issues)
        {
            var legs = run.Paths.Where(item =>
                    string.Equals(item.ScenarioId, scenarioId, StringComparison.Ordinal) &&
                    item.LayoutSeed == layoutSeed &&
                    string.Equals(item.MemberId, memberId, StringComparison.Ordinal) &&
                    string.Equals(item.FromDestinationId, from, StringComparison.Ordinal) &&
                    string.Equals(item.ToDestinationId, to, StringComparison.Ordinal))
                .ToArray();
            if (legs.Length < bar.RequiredDeterminismRepeats)
                AddIssue(issues, $"Missing repeated route {scenarioId}/{layoutSeed}/{memberId}/{from}->{to}.");
        }

        private static IEnumerable<LayoutObservation> DistinctLayouts(NaturalBehaviorQaRun run)
        {
            return run.Layouts
                .GroupBy(item => $"{item.ScenarioId}|{item.LayoutSeed}", StringComparer.Ordinal)
                .Select(item => item.OrderBy(value => value.RepeatIndex).First());
        }

        private static IEnumerable<FurnitureFootprintObservation> FootprintsFor(
            NaturalBehaviorQaRun run,
            string scenarioId,
            int layoutSeed)
        {
            return run.FurnitureFootprints.Where(item =>
                string.Equals(item.ScenarioId, scenarioId, StringComparison.Ordinal) && item.LayoutSeed == layoutSeed);
        }

        private static bool SameLayout(string scenarioId, int layoutSeed, LayoutObservation layout)
        {
            return layoutSeed == layout.LayoutSeed && string.Equals(scenarioId, layout.ScenarioId, StringComparison.Ordinal);
        }

        private static IEnumerable<IGrouping<MotionGroupKey, MotionSample>> MotionGroups(NaturalBehaviorQaRun run)
        {
            return run.MotionSamples.GroupBy(item => new MotionGroupKey(item.ScenarioId, item.LayoutSeed, item.MemberId));
        }

        private static bool CircleOverlapsPolygon(
            QaPoint2 center,
            double radius,
            QaPolygon2 polygon,
            double tolerance)
        {
            if (PointInPolygon(center, polygon)) return true;
            var vertices = polygon.Vertices;
            var threshold = Math.Max(0d, radius - tolerance);
            if (threshold <= 0d) return false;
            for (var index = 0; index < vertices.Count; index++)
            {
                var next = (index + 1) % vertices.Count;
                if (DistancePointToSegment(center, vertices[index], vertices[next]) < threshold) return true;
            }
            return false;
        }

        private static bool SegmentCrossesPolygonInterior(
            QaPoint2 start,
            QaPoint2 end,
            QaPolygon2 polygon,
            double tolerance)
        {
            if (start.DistanceTo(end) <= tolerance) return false;
            if (PointInPolygon(start, polygon) || PointInPolygon(end, polygon)) return true;
            var intersections = 0;
            var vertices = polygon.Vertices;
            for (var index = 0; index < vertices.Count; index++)
            {
                var next = (index + 1) % vertices.Count;
                if (SegmentsIntersect(start, end, vertices[index], vertices[next], tolerance)) intersections++;
            }
            if (intersections >= 2) return true;
            var midpoint = new QaPoint2((start.X + end.X) * 0.5d, (start.Y + end.Y) * 0.5d);
            return PointInPolygon(midpoint, polygon);
        }

        private static bool PointInPolygon(QaPoint2 point, QaPolygon2 polygon)
        {
            var inside = false;
            var vertices = polygon.Vertices;
            for (int current = 0, previous = vertices.Count - 1; current < vertices.Count; previous = current++)
            {
                var a = vertices[current];
                var b = vertices[previous];
                var crosses = (a.Y > point.Y) != (b.Y > point.Y) &&
                              point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static double DistancePointToSegment(QaPoint2 point, QaPoint2 start, QaPoint2 end)
        {
            var x = end.X - start.X;
            var y = end.Y - start.Y;
            var lengthSquared = x * x + y * y;
            if (lengthSquared <= double.Epsilon) return point.DistanceTo(start);
            var t = ((point.X - start.X) * x + (point.Y - start.Y) * y) / lengthSquared;
            t = Math.Max(0d, Math.Min(1d, t));
            return point.DistanceTo(new QaPoint2(start.X + t * x, start.Y + t * y));
        }

        private static bool SegmentsIntersect(QaPoint2 a, QaPoint2 b, QaPoint2 c, QaPoint2 d, double tolerance)
        {
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            return ((abC > tolerance && abD < -tolerance) || (abC < -tolerance && abD > tolerance)) &&
                   ((cdA > tolerance && cdB < -tolerance) || (cdA < -tolerance && cdB > tolerance));
        }

        private static double Cross(QaPoint2 a, QaPoint2 b, QaPoint2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        private static int CircularDirectionDistance(int left, int right)
        {
            var delta = Math.Abs(left - right) % 8;
            return Math.Min(delta, 8 - delta);
        }

        private static int SignedDirectionDelta(int left, int right)
        {
            var delta = (right - left + 8) % 8;
            if (delta > 4) delta -= 8;
            return delta;
        }

        private static bool IsCornerJitter(IReadOnlyList<DirectionChange> changes, NaturalBehaviorQualityBar bar)
        {
            if (changes.Count < 3) return false;
            var last = changes[changes.Count - 1];
            for (var start = changes.Count - 3; start >= 0; start--)
            {
                if (last.TimeSeconds - changes[start].TimeSeconds > bar.CornerJitterWindowSeconds) break;
                var alternating = true;
                for (var index = start + 1; index < changes.Count; index++)
                {
                    if (changes[index - 1].Sign == changes[index].Sign)
                    {
                        alternating = false;
                        break;
                    }
                }
                if (alternating && changes.Count - start >= 3 &&
                    changes[start].Position.DistanceTo(last.Position) <= bar.CornerJitterRadiusMeters)
                    return true;
            }
            return false;
        }

        private static double Percentile(IReadOnlyList<double> sorted, double percentile)
        {
            if (sorted == null || sorted.Count == 0) return double.PositiveInfinity;
            var rank = Math.Max(0, (int)Math.Ceiling(sorted.Count * percentile) - 1);
            return sorted[Math.Min(sorted.Count - 1, rank)];
        }

        private static string PathKey(PathObservation path)
        {
            return $"{path.ScenarioId}/{path.LayoutSeed}/{path.MemberId}/{path.FromDestinationId}->{path.ToDestinationId}";
        }

        private static void RequireCapability(
            NaturalBehaviorQaRun run,
            NaturalBehaviorQaCapability capability,
            ICollection<string> issues)
        {
            if ((run.Capabilities & capability) != capability)
                AddIssue(issues, $"Runtime hook does not declare required capability {capability}.");
        }

        private static NaturalBehaviorQaGateResult Gate(
            string id,
            string name,
            IEnumerable<string> issues,
            IEnumerable<NaturalBehaviorQaMetric> metrics)
        {
            return new NaturalBehaviorQaGateResult(id, name, issues, metrics);
        }

        private static NaturalBehaviorQaMetric Metric(string name, object value)
        {
            string text;
            if (value is IFormattable formattable)
                text = formattable.ToString(null, CultureInfo.InvariantCulture);
            else
                text = value?.ToString() ?? string.Empty;
            return new NaturalBehaviorQaMetric(name, text);
        }

        private static void AddIssue(ICollection<string> issues, string issue)
        {
            const int maximumReportedIssues = 256;
            if (issues.Count < maximumReportedIssues) issues.Add(issue);
        }

        private readonly struct MotionGroupKey : IEquatable<MotionGroupKey>
        {
            public MotionGroupKey(string scenarioId, int layoutSeed, string memberId)
            {
                ScenarioId = scenarioId;
                LayoutSeed = layoutSeed;
                MemberId = memberId;
            }

            public string ScenarioId { get; }
            public int LayoutSeed { get; }
            public string MemberId { get; }

            public bool Equals(MotionGroupKey other)
            {
                return LayoutSeed == other.LayoutSeed &&
                       string.Equals(ScenarioId, other.ScenarioId, StringComparison.Ordinal) &&
                       string.Equals(MemberId, other.MemberId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is MotionGroupKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = StringComparer.Ordinal.GetHashCode(ScenarioId ?? string.Empty);
                    hash = hash * 397 ^ LayoutSeed;
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(MemberId ?? string.Empty);
                    return hash;
                }
            }
        }

        private readonly struct DirectionChange
        {
            public DirectionChange(double timeSeconds, int sign, QaPoint2 position)
            {
                TimeSeconds = timeSeconds;
                Sign = sign;
                Position = position;
            }

            public double TimeSeconds { get; }
            public int Sign { get; }
            public QaPoint2 Position { get; }
        }
    }

    public static class NaturalBehaviorQaHash
    {
        public static string Compute(NaturalBehaviorQaRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var builder = new StringBuilder();
            builder.Append("cap=").Append((int)run.Capabilities).Append('|');
            builder.Append("seconds=").Append(F(run.ObservationGameSeconds)).AppendLine();
            foreach (var item in run.Layouts.OrderBy(LayoutKey, StringComparer.Ordinal))
                builder.Append("L|").Append(LayoutKey(item)).Append('|').Append(item.Succeeded ? 1 : 0).Append('|').Append(item.StableHash).AppendLine();
            foreach (var item in run.FurnitureFootprints.OrderBy(FurnitureKey, StringComparer.Ordinal))
            {
                builder.Append("F|").Append(FurnitureKey(item)).Append('|').Append(item.BlocksMovement ? 1 : 0)
                    .Append('|').Append(item.IsPlaceable ? 1 : 0);
                foreach (var vertex in item.Footprint.Vertices) builder.Append('|').Append(F(vertex.X)).Append(',').Append(F(vertex.Y));
                builder.AppendLine();
            }
            foreach (var item in run.Footpoints.OrderBy(FootpointKey, StringComparer.Ordinal))
                builder.Append("P|").Append(FootpointKey(item)).Append('|').Append(F(item.Position.X)).Append(',')
                    .Append(F(item.Position.Y)).Append('|').Append(F(item.RadiusMeters)).Append('|').Append(item.Visible ? 1 : 0).AppendLine();
            foreach (var item in run.MotionSamples.OrderBy(MotionKey, StringComparer.Ordinal))
                builder.Append("M|").Append(MotionKey(item)).Append('|').Append(F(item.Position.X)).Append(',')
                    .Append(F(item.Position.Y)).Append('|').Append(item.DirectionIndex).Append('|').Append((int)item.Phase)
                    .Append('|').Append(item.Visible ? 1 : 0).AppendLine();
            foreach (var item in run.Paths.OrderBy(PathKey, StringComparer.Ordinal))
                builder.Append("R|").Append(PathKey(item)).Append('|').Append(item.Succeeded ? 1 : 0).Append('|')
                    .Append(F(item.DirectDistanceMeters)).Append('|').Append(F(item.TravelledDistanceMeters)).Append('|')
                    .Append(item.ReplanCount).Append('|').Append(F(item.DeadlockSeconds)).Append('|')
                    .Append(item.StablePathHash).Append('|').Append(item.UnsafeTraversalCount).AppendLine();
            foreach (var item in run.SeatingFrames.OrderBy(SeatKey, StringComparer.Ordinal))
                builder.Append("S|").Append(SeatKey(item)).Append('|').Append((int)item.Phase).Append('|')
                    .Append(item.FrameIndex).Append('|').Append(F(item.FootPixel1920.X)).Append(',')
                    .Append(F(item.FootPixel1920.Y)).Append('|').Append(item.HasOcclusionMeasurement ? 1 : 0)
                    .Append('|').Append(item.ChairForegroundOrderCorrect ? 1 : 0)
                    .Append('|').Append(item.DeskForegroundOrderCorrect ? 1 : 0)
                    .Append('|').Append(item.ChairBodyClipPixelCount)
                    .Append('|').Append(item.DeskBodyClipPixelCount).AppendLine();
            foreach (var item in run.WorkWindows.OrderBy(WorkWindowKey, StringComparer.Ordinal))
                builder.Append("O|").Append(WorkWindowKey(item)).Append('|').Append(F(item.ObservationGameSeconds))
                    .Append('|').Append(F(item.AccumulatedWorkSeconds)).AppendLine();
            foreach (var item in run.WorkActions.OrderBy(ActionKey, StringComparer.Ordinal))
                builder.Append("A|").Append(ActionKey(item)).Append('|').Append((int)item.Action).Append('|')
                    .Append(F(item.AccumulatedWorkSeconds)).Append('|')
                    .Append(F(item.WorkSecondsSincePreviousSameAction)).Append('|').Append((int)item.Phase)
                    .Append('|').Append(item.VisualVisible ? 1 : 0).AppendLine();
            foreach (var item in run.Productivity.OrderBy(ProductivityKey, StringComparer.Ordinal))
                builder.Append("W|").Append(ProductivityKey(item)).Append('|').Append((int)item.Phase).Append('|')
                    .Append(F(item.ProductivityDelta)).AppendLine();
            foreach (var item in run.NavigationRebuilds.OrderBy(RebuildKey, StringComparer.Ordinal))
                builder.Append("N|").Append(RebuildKey(item)).Append('|').Append(F(item.RequestedTimeSeconds)).Append('|')
                    .Append(F(item.CompletedTimeSeconds)).Append('|').Append(item.ActivePathCount).Append('|')
                    .Append(item.SafelyReplannedPathCount).Append('|').Append(item.UnsafeTraversalCount).Append('|')
                    .Append(F(item.ProgressWhileUnsafeSeconds)).AppendLine();

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            foreach (var value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static string LayoutKey(LayoutObservation item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{item.RepeatIndex:D4}|{item.FurnitureCount:D6}";
        private static string FurnitureKey(FurnitureFootprintObservation item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{item.FurnitureId}";
        private static string FootpointKey(FootpointSample item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{item.MemberId}|{F(item.TimeSeconds)}";
        private static string MotionKey(MotionSample item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{item.MemberId}|{F(item.TimeSeconds)}";
        private static string PathKey(PathObservation item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{item.MemberId}|{item.FromDestinationId}|{item.ToDestinationId}|{item.RepeatIndex:D4}";
        private static string SeatKey(SeatingFrameObservation item) =>
            $"{item.SessionId}|{item.MemberId}|{F(item.SessionTimeSeconds)}";
        private static string ActionKey(WorkActionObservation item) =>
            $"{item.MemberId}|{F(item.TimeSeconds)}|{(int)item.Action:D2}";
        private static string WorkWindowKey(WorkWindowObservation item) => item.MemberId;
        private static string ProductivityKey(ProductivityObservation item) =>
            $"{item.MemberId}|{F(item.TimeSeconds)}|{(int)item.Phase:D2}";
        private static string RebuildKey(NavigationRebuildObservation item) =>
            $"{item.ScenarioId}|{item.LayoutSeed:D10}|{F(item.RequestedTimeSeconds)}";
        private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    public static class NaturalBehaviorQaReportFormatter
    {
        public static string ToText(NaturalBehaviorQaResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            builder.AppendLine(result.Marker);
            builder.Append("RUN_HASH: ").AppendLine(result.RunHash);
            foreach (var gate in result.Gates)
            {
                builder.AppendLine(gate.Marker);
                foreach (var metric in gate.Metrics)
                    builder.Append("METRIC | ").Append(gate.GateId).Append(" | ").Append(metric.Name).Append('=').AppendLine(metric.Value);
                foreach (var issue in gate.Issues)
                    builder.Append("ISSUE | ").Append(gate.GateId).Append(" | ").AppendLine(issue);
            }
            return builder.ToString();
        }

        public static string ToJson(NaturalBehaviorQaResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            builder.Append("{\n  \"marker\": \"").Append(Escape(result.Marker)).Append("\",\n")
                .Append("  \"passed\": ").Append(result.Passed ? "true" : "false").Append(",\n")
                .Append("  \"runHash\": \"").Append(Escape(result.RunHash)).Append("\",\n")
                .Append("  \"gates\": [\n");
            for (var gateIndex = 0; gateIndex < result.Gates.Count; gateIndex++)
            {
                var gate = result.Gates[gateIndex];
                builder.Append("    {\"id\":\"").Append(Escape(gate.GateId)).Append("\",\"name\":\"")
                    .Append(Escape(gate.Name)).Append("\",\"marker\":\"").Append(Escape(gate.Marker))
                    .Append("\",\"passed\":").Append(gate.Passed ? "true" : "false").Append(",\"metrics\":[");
                for (var metricIndex = 0; metricIndex < gate.Metrics.Count; metricIndex++)
                {
                    var metric = gate.Metrics[metricIndex];
                    if (metricIndex > 0) builder.Append(',');
                    builder.Append("{\"name\":\"").Append(Escape(metric.Name)).Append("\",\"value\":\"")
                        .Append(Escape(metric.Value)).Append("\"}");
                }
                builder.Append("],\"issues\":[");
                for (var issueIndex = 0; issueIndex < gate.Issues.Count; issueIndex++)
                {
                    if (issueIndex > 0) builder.Append(',');
                    builder.Append('\"').Append(Escape(gate.Issues[issueIndex])).Append('\"');
                }
                builder.Append("]}");
                if (gateIndex + 1 < result.Gates.Count) builder.Append(',');
                builder.AppendLine();
            }
            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(value.Length + 8);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}

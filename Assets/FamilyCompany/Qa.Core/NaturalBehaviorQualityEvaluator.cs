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
            ValidateQualityBar(bar);
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

            long footprintCoverageFailures = 0;
            long missingMemberCoverage = 0;
            var expectedLayouts = ExpectedLayouts(run, bar).ToArray();
            foreach (var identity in expectedLayouts)
            {
                var layouts = LayoutsFor(run, identity).ToArray();
                if (layouts.Length != 1)
                {
                    footprintCoverageFailures++;
                    AddIssue(issues, $"Expected exactly one layout record for {identity}; found {layouts.Length}.");
                    continue;
                }

                var layout = layouts[0];
                var footprints = FootprintsFor(run, identity).ToArray();
                var footprintCount = footprints.Select(item => item.FurnitureId).Distinct(StringComparer.Ordinal).Count();
                var placeableCount = footprints.Where(item => item.IsPlaceable)
                    .Select(item => item.FurnitureId).Distinct(StringComparer.Ordinal).Count();
                if (footprintCount != footprints.Length || placeableCount != layout.FurnitureCount)
                {
                    footprintCoverageFailures++;
                    AddIssue(issues,
                        $"{identity} declares {layout.FurnitureCount} placeable furniture footprints but recorded " +
                        $"placeable={placeableCount}, unique={footprintCount}, total={footprints.Length}.");
                }

                foreach (var memberId in run.Plan.ExpectedMemberIds)
                {
                    var memberSamples = run.Footpoints.Where(item => SameLayout(item, identity) &&
                            string.Equals(item.MemberId, memberId, StringComparison.Ordinal))
                        .OrderBy(item => item.TimeSeconds)
                        .ToArray();
                    if (memberSamples.Length < 3 || memberSamples.Any(item => !item.Visible))
                    {
                        missingMemberCoverage++;
                        AddIssue(issues, $"No complete visible footpoint coverage for {memberId} in {identity}.");
                    }
                    for (var index = 1; index < memberSamples.Length; index++)
                        if (memberSamples[index].TimeSeconds <= memberSamples[index - 1].TimeSeconds)
                            AddIssue(issues, $"Footpoint trace for {memberId} in {identity} has non-increasing timestamps.");
                }
            }

            foreach (var item in run.FurnitureFootprints)
                if (!expectedLayouts.Any(identity => SameLayout(item, identity)))
                    AddIssue(issues, $"Unexpected furniture footprint identity {item.ScenarioId}/{item.LayoutSeed}/r{item.RepeatIndex}.");
            foreach (var item in run.Footpoints)
                if (!expectedLayouts.Any(identity => SameLayout(item, identity)))
                    AddIssue(issues, $"Unexpected footpoint identity {item.ScenarioId}/{item.LayoutSeed}/r{item.RepeatIndex}.");
            foreach (var layoutGroup in expectedLayouts.GroupBy(
                         item => $"{item.ScenarioId}|{item.LayoutSeed}", StringComparer.Ordinal))
            {
                var signatures = layoutGroup
                    .Select(identity => FootprintSignature(FootprintsFor(run, identity)))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (signatures.Length != 1)
                    AddIssue(issues, $"Furniture footprint geometry differs across repeats for {layoutGroup.Key}.");
            }

            long overlapCount = 0;
            foreach (var sample in run.Footpoints.Where(item => item.Visible))
            {
                var identity = new LayoutIdentity(sample.ScenarioId, sample.LayoutSeed, sample.RepeatIndex);
                foreach (var furniture in FootprintsFor(run, identity).Where(item => item.BlocksMovement))
                {
                    if (!CircleOverlapsPolygon(sample.Position, sample.RadiusMeters, furniture.Footprint, bar.NumericTolerance))
                        continue;
                    overlapCount++;
                    AddIssue(issues,
                        $"Footpoint overlap: {sample.MemberId} with {furniture.FurnitureId} at {sample.TimeSeconds:F3}s " +
                        $"({identity}).");
                }
            }

            long wallCrossingCount = 0;
            foreach (var group in MotionGroups(run))
            {
                var samples = group.OrderBy(item => item.TimeSeconds).ToArray();
                var identity = new LayoutIdentity(group.Key.ScenarioId, group.Key.LayoutSeed, group.Key.RepeatIndex);
                var walls = FootprintsFor(run, identity)
                    .Where(item => item.BlocksMovement)
                    .ToArray();
                var radius = run.Footpoints
                    .Where(item => SameLayout(item, identity) && string.Equals(item.MemberId, group.Key.MemberId, StringComparison.Ordinal))
                    .Select(item => item.RadiusMeters)
                    .DefaultIfEmpty(0d)
                    .Max();
                for (var index = 1; index < samples.Length; index++)
                {
                    var previous = samples[index - 1];
                    var current = samples[index];
                    if (!previous.Visible || !current.Visible) continue;
                    foreach (var wall in walls)
                    {
                        if (!SweptCircleOverlapsPolygon(previous.Position, current.Position, radius, wall.Footprint, bar.NumericTolerance))
                            continue;
                        wallCrossingCount++;
                        AddIssue(issues,
                            $"Movement crossed {wall.FurnitureId}: {current.MemberId} at {current.TimeSeconds:F3}s " +
                            $"({identity}).");
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
            var expectedSeeds = run.Plan.RandomLayoutSeeds.ToArray();
            if (expectedSeeds.Length != bar.RequiredRandomLayoutSeeds)
                AddIssue(issues, $"Plan declares {expectedSeeds.Length} random seeds; expected exactly {bar.RequiredRandomLayoutSeeds}.");
            ValidateLayoutRepeats(run, NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, null, bar, issues);
            var expectedLayoutIdentities = new HashSet<LayoutIdentity>(ExpectedLayouts(run, bar));
            foreach (var layout in run.Layouts)
                if (!expectedLayoutIdentities.Contains(new LayoutIdentity(layout.ScenarioId, layout.LayoutSeed, layout.RepeatIndex)))
                    AddIssue(issues, $"Unexpected layout identity {layout.ScenarioId}/{layout.LayoutSeed}/r{layout.RepeatIndex}.");
            var randomLayouts = run.Layouts
                .Where(item => string.Equals(item.ScenarioId, NaturalBehaviorQaScenarioIds.RandomFurniture,
                    StringComparison.Ordinal))
                .ToArray();
            var presentSeeds = new HashSet<int>(randomLayouts.Select(item => item.LayoutSeed));
            foreach (var unexpectedSeed in presentSeeds.Except(expectedSeeds).OrderBy(item => item))
                AddIssue(issues, $"Unexpected random furniture seed {unexpectedSeed}.");
            foreach (var seed in expectedSeeds)
            {
                if (!presentSeeds.Contains(seed))
                {
                    AddIssue(issues, $"Missing required random furniture seed {seed}.");
                    continue;
                }

                ValidateLayoutRepeats(run, NaturalBehaviorQaScenarioIds.RandomFurniture, seed,
                    bar.RequiredFurniturePerRandomLayout, bar, issues);
                RequireRoundTrips(run, NaturalBehaviorQaScenarioIds.RandomFurniture, seed, bar, issues);
            }

            var allPaths = run.Paths.ToArray();
            var expectedPathLayouts = new HashSet<LayoutIdentity>(ExpectedLayouts(run, bar));
            foreach (var path in allPaths)
            {
                if (!expectedPathLayouts.Contains(new LayoutIdentity(path.ScenarioId, path.LayoutSeed, path.RepeatIndex)))
                    AddIssue(issues, $"Unexpected path identity: {PathKey(path)}.");
                if (!path.Succeeded) AddIssue(issues, $"Path failed: {PathKey(path)}.");
                if (!IsFinite(path.DirectDistanceMeters) || !IsFinite(path.TravelledDistanceMeters) ||
                    path.DirectDistanceMeters <= bar.NumericTolerance || path.TravelledDistanceMeters < 0d)
                    AddIssue(issues, $"Path has invalid distance values: {PathKey(path)}.");
                if (path.ReplanCount < 0 || path.ReplanCount > bar.MaximumReplansPerRoute)
                    AddIssue(issues, $"Path replanned {path.ReplanCount} times: {PathKey(path)}.");
                if (!IsFinite(path.DeadlockSeconds) || path.DeadlockSeconds < 0d ||
                    path.DeadlockSeconds > bar.MaximumDeadlockSeconds + bar.NumericTolerance)
                    AddIssue(issues, $"Path deadlocked for {path.DeadlockSeconds:F3}s: {PathKey(path)}.");
                if (path.UnsafeTraversalCount != 0)
                    AddIssue(issues, $"Path recorded {path.UnsafeTraversalCount} unsafe traversals: {PathKey(path)}.");
            }

            foreach (var group in allPaths.GroupBy(item =>
                         $"{item.ScenarioId}|{item.LayoutSeed}|{item.MemberId}|{item.FromDestinationId}|{item.ToDestinationId}",
                         StringComparer.Ordinal))
            {
                var repeats = group.Select(item => item.RepeatIndex).OrderBy(item => item).ToArray();
                if (group.Count() != bar.RequiredDeterminismRepeats ||
                    !repeats.SequenceEqual(Enumerable.Range(0, bar.RequiredDeterminismRepeats)))
                    AddIssue(issues, $"Path {group.Key} must contain exactly repeats 0..{bar.RequiredDeterminismRepeats - 1}.");
                if (group.Select(item => item.StablePathHash).Distinct(StringComparer.Ordinal).Count() != 1)
                    AddIssue(issues, $"Path {group.Key} produced different stable hashes.");
            }

            var stretches = allPaths
                .Where(item => item.Succeeded && IsFinite(item.DirectDistanceMeters) &&
                               IsFinite(item.TravelledDistanceMeters) && item.DirectDistanceMeters > bar.NumericTolerance)
                .Select(item => item.TravelledDistanceMeters / item.DirectDistanceMeters)
                .OrderBy(item => item)
                .ToArray();
            var p95 = Percentile(stretches, 0.95d);
            var maximum = stretches.Length == 0 ? double.PositiveInfinity : stretches[stretches.Length - 1];
            if (!IsFinite(p95) || p95 > bar.MaximumPathStretchP95 + bar.NumericTolerance)
                AddIssue(issues, $"Path stretch p95 {p95:F3} exceeds {bar.MaximumPathStretchP95:F3}.");
            if (!IsFinite(maximum) || maximum > bar.MaximumPathStretch + bar.NumericTolerance)
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
            long directionFlips = 0;
            long cornerJitters = 0;
            var expectedKeys = ExpectedLayouts(run, bar)
                .SelectMany(identity => run.Plan.ExpectedMemberIds.Select(memberId =>
                    new MotionGroupKey(identity.ScenarioId, identity.LayoutSeed, identity.RepeatIndex, memberId)))
                .ToArray();
            foreach (var key in expectedKeys)
            {
                var samples = run.MotionSamples.Where(item =>
                        string.Equals(item.ScenarioId, key.ScenarioId, StringComparison.Ordinal) &&
                        item.LayoutSeed == key.LayoutSeed && item.RepeatIndex == key.RepeatIndex &&
                        string.Equals(item.MemberId, key.MemberId, StringComparison.Ordinal))
                    .OrderBy(item => item.TimeSeconds)
                    .ToArray();
                if (samples.Length < 3)
                {
                    AddIssue(issues,
                        $"Motion trace {key} has fewer than three samples.");
                    continue;
                }
                if (samples.Any(item => !item.Visible))
                    AddIssue(issues, $"Motion trace {key} contains invisible/partial samples.");

                var hasPreviousVelocity = false;
                var previousVelocityX = 0d;
                var previousVelocityY = 0d;
                var previousDeltaTime = 0d;
                var directionChanges = new List<DirectionChange>();
                for (var index = 1; index < samples.Length; index++)
                {
                    var previous = samples[index - 1];
                    var current = samples[index];
                    if (!previous.Visible || !current.Visible)
                    {
                        hasPreviousVelocity = false;
                        continue;
                    }
                    var deltaTime = current.TimeSeconds - previous.TimeSeconds;
                    if (!IsFinite(deltaTime) || deltaTime <= 0d)
                    {
                        AddIssue(issues, $"Non-increasing motion timestamp for {current.MemberId}.");
                        continue;
                    }

                    var velocityX = (current.Position.X - previous.Position.X) / deltaTime;
                    var velocityY = (current.Position.Y - previous.Position.Y) / deltaTime;
                    var speed = Magnitude(velocityX, velocityY);
                    var delta = speed * deltaTime;
                    if (!IsFinite(delta) || !IsFinite(speed))
                    {
                        AddIssue(issues, $"Non-finite motion math for {current.MemberId}.");
                        hasPreviousVelocity = false;
                        continue;
                    }
                    maximumGap = Math.Max(maximumGap, deltaTime);
                    maximumDelta = Math.Max(maximumDelta, delta);
                    maximumSpeed = Math.Max(maximumSpeed, speed);
                    if (deltaTime > bar.MaximumMotionSampleGapSeconds + bar.NumericTolerance)
                        AddIssue(issues, $"Motion sample gap {deltaTime:F3}s exceeds the limit for {current.MemberId}.");
                    if (delta > bar.MaximumFrameDeltaMeters + bar.NumericTolerance)
                        AddIssue(issues, $"Position delta {delta:F3}m indicates a pop/teleport for {current.MemberId}.");
                    if (speed > bar.MaximumSpeedMetersPerSecond + bar.NumericTolerance)
                        AddIssue(issues, $"Speed {speed:F3}m/s exceeds the cap for {current.MemberId}.");

                    if (hasPreviousVelocity)
                    {
                        var velocityDeltaX = velocityX - previousVelocityX;
                        var velocityDeltaY = velocityY - previousVelocityY;
                        var velocitySampleGap = (previousDeltaTime + deltaTime) * 0.5d;
                        var acceleration = Magnitude(velocityDeltaX, velocityDeltaY) / velocitySampleGap;
                        maximumAcceleration = Math.Max(maximumAcceleration, acceleration);
                        if (!IsFinite(acceleration) || acceleration > bar.MaximumAccelerationMetersPerSecondSquared + bar.NumericTolerance)
                            AddIssue(issues, $"Acceleration {acceleration:F3}m/s^2 exceeds the cap for {current.MemberId}.");
                    }
                    previousVelocityX = velocityX;
                    previousVelocityY = velocityY;
                    previousDeltaTime = deltaTime;
                    hasPreviousVelocity = true;

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

            var expectedKeySet = new HashSet<MotionGroupKey>(expectedKeys);
            foreach (var group in MotionGroups(run))
                if (!expectedKeySet.Contains(group.Key))
                    AddIssue(issues, $"Unexpected motion trace {group.Key}.");

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
            long clippingPixels = 0;
            long occlusionFailures = 0;
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
                for (var index = 1; index < samples.Length; index++)
                    if (samples[index].SessionTimeSeconds <= samples[index - 1].SessionTimeSeconds)
                        AddIssue(issues, $"Seating session {group.Key} has non-increasing capture timestamps.");

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

                foreach (var sample in samples)
                {
                    var evidence = sample.CaptureEvidence;
                    var matchingArtifacts = run.CaptureArtifacts
                        .Where(item => string.Equals(item.Label, evidence.CaptureLabel, StringComparison.Ordinal) &&
                                       string.Equals(item.Sha256, evidence.CaptureSha256, StringComparison.Ordinal))
                        .ToArray();
                    if (matchingArtifacts.Length != 1 || matchingArtifacts[0].Width != evidence.Width ||
                        matchingArtifacts[0].Height != evidence.Height)
                    {
                        occlusionFailures++;
                        AddIssue(issues,
                            $"Seating capture {evidence.CaptureSha256} is not backed by exactly one harness-recorded capture artifact.");
                    }
                    if (evidence.Width != 1920 || evidence.Height != 1080)
                    {
                        occlusionFailures++;
                        AddIssue(issues, $"Seating capture {evidence.CaptureSha256} is {evidence.Width}x{evidence.Height}; 1920x1080 is required.");
                    }

                    foreach (QaSeatingPixelExpectation expectation in Enum.GetValues(typeof(QaSeatingPixelExpectation)))
                    {
                        var observed = evidence.PixelObservations.Where(item => item.Expectation == expectation).ToArray();
                        if (observed.Length == 0)
                        {
                            if (expectation == QaSeatingPixelExpectation.ChairForeground ||
                                expectation == QaSeatingPixelExpectation.DeskForeground) occlusionFailures++;
                            else clippingPixels++;
                            AddIssue(issues, $"Capture {evidence.CaptureSha256} lacks {expectation} pixel evidence.");
                            continue;
                        }

                        var expectedRole = ExpectedObservedRole(expectation);
                        var failures = observed.Count(item => item.ObservedRole != expectedRole);
                        if (failures == 0) continue;
                        if (expectation == QaSeatingPixelExpectation.ChairForeground ||
                            expectation == QaSeatingPixelExpectation.DeskForeground) occlusionFailures += failures;
                        else clippingPixels += failures;
                        AddIssue(issues,
                            $"Capture {evidence.CaptureSha256} has {failures} {expectation} pixel classification failures.");
                    }

                    var footSamples = evidence.PixelObservations
                        .Where(item => item.Expectation == QaSeatingPixelExpectation.FootAnchor &&
                                       item.ObservedRole == QaSeatingPixelObservedRole.CharacterBody)
                        .ToArray();
                    if (!footSamples.Any(item =>
                            Math.Abs(item.X - sample.FootPixel1920.X) <= bar.NumericTolerance &&
                            Math.Abs(item.Y - sample.FootPixel1920.Y) <= bar.NumericTolerance))
                    {
                        clippingPixels++;
                        AddIssue(issues, $"Foot anchor in {group.Key} is not backed by a matching capture pixel.");
                    }
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
            if (!IsFinite(run.ObservationGameSeconds) ||
                run.ObservationGameSeconds + bar.NumericTolerance < bar.RequiredObservationGameSeconds)
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
                if (!IsFinite(workWindow.ObservationGameSeconds) || !IsFinite(workWindow.AccumulatedWorkSeconds) ||
                    Math.Abs(workWindow.ObservationGameSeconds - run.ObservationGameSeconds) > bar.NumericTolerance ||
                    workWindow.ObservationGameSeconds + bar.NumericTolerance < bar.RequiredObservationGameSeconds ||
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
                        if (!IsFinite(item.TimeSeconds) || item.TimeSeconds < 0d ||
                            item.TimeSeconds > run.ObservationGameSeconds + bar.NumericTolerance ||
                            (index > 0 && item.TimeSeconds <= events[index - 1].TimeSeconds))
                            AddIssue(issues, $"{memberId}/{contract.Action} has an invalid or non-increasing event timestamp.");
                        if (item.Phase != QaMotionPhase.Work || !item.VisualVisible)
                            AddIssue(issues, $"{memberId} displayed {contract.Action} outside visible Work phase.");
                        var elapsed = item.AccumulatedWorkSeconds - previousAccumulatedWork;
                        if (!IsFinite(item.AccumulatedWorkSeconds) || !IsFinite(item.WorkSecondsSincePreviousSameAction) ||
                            item.AccumulatedWorkSeconds <= previousAccumulatedWork ||
                            item.AccumulatedWorkSeconds > workWindow.AccumulatedWorkSeconds + bar.NumericTolerance)
                            AddIssue(issues, $"{memberId}/{contract.Action} has non-increasing accumulated Work time.");
                        if (Math.Abs(elapsed - item.WorkSecondsSincePreviousSameAction) > bar.NumericTolerance)
                            AddIssue(issues,
                                $"{memberId}/{contract.Action} reported cooldown {item.WorkSecondsSincePreviousSameAction:F3}s " +
                                $"but accumulated Work delta is {elapsed:F3}s.");
                        if (elapsed <= 0d || elapsed > contract.MaximumWorkSeconds + bar.NumericTolerance)
                            AddIssue(issues, $"{memberId}/{contract.Action} exceeded its maximum cooldown ({elapsed:F3}s).");
                        if (elapsed + bar.NumericTolerance < contract.MinimumWorkSeconds)
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
                if (!IsFinite(sample.TimeSeconds) || sample.TimeSeconds < 0d ||
                    sample.TimeSeconds > run.ObservationGameSeconds + bar.NumericTolerance)
                    AddIssue(issues, $"Productivity sample for {sample.MemberId} has an invalid observation timestamp.");
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

            var expectedSeeds = run.Plan.RandomLayoutSeeds.ToArray();
            if (expectedSeeds.Length != bar.RequiredRandomLayoutSeeds)
                AddIssue(issues, $"Plan declares {expectedSeeds.Length} rebuild seeds; expected exactly {bar.RequiredRandomLayoutSeeds}.");
            foreach (var unexpected in run.NavigationRebuilds.Where(item =>
                         !string.Equals(item.ScenarioId, NaturalBehaviorQaScenarioIds.RandomFurniture, StringComparison.Ordinal) ||
                         !expectedSeeds.Contains(item.LayoutSeed)))
                AddIssue(issues, $"Unexpected navigation rebuild identity {unexpected.ScenarioId}/{unexpected.LayoutSeed}/r{unexpected.RepeatIndex}.");
            foreach (var seed in expectedSeeds)
            {
                var observations = run.NavigationRebuilds
                    .Where(item => string.Equals(item.ScenarioId, NaturalBehaviorQaScenarioIds.RandomFurniture,
                                       StringComparison.Ordinal) && item.LayoutSeed == seed)
                    .ToArray();
                var repeats = observations.Select(item => item.RepeatIndex).OrderBy(item => item).ToArray();
                if (observations.Length != bar.RequiredDeterminismRepeats ||
                    !repeats.SequenceEqual(Enumerable.Range(0, bar.RequiredDeterminismRepeats)))
                {
                    AddIssue(issues,
                        $"Navigation rebuild seed {seed} must contain exactly repeats 0..{bar.RequiredDeterminismRepeats - 1}.");
                    continue;
                }

                foreach (var observation in observations)
                {
                    var duration = observation.CompletedTimeSeconds - observation.RequestedTimeSeconds;
                    if (!IsFinite(duration) || duration < 0d ||
                        duration > bar.MaximumNavigationRebuildSeconds + bar.NumericTolerance)
                        AddIssue(issues, $"Navigation rebuild for seed {seed} took {duration:F3}s.");
                    if (observation.ActivePathCount != run.Plan.ExpectedMemberIds.Count)
                        AddIssue(issues,
                            $"Navigation rebuild for seed {seed}/r{observation.RepeatIndex} exercised " +
                            $"{observation.ActivePathCount} active paths; expected {run.Plan.ExpectedMemberIds.Count}.");
                    var activePaths = new HashSet<string>(observation.ActivePathIds, StringComparer.Ordinal);
                    var safelyReplannedPaths = new HashSet<string>(observation.SafelyReplannedPathIds, StringComparer.Ordinal);
                    if (!activePaths.SetEquals(safelyReplannedPaths))
                        AddIssue(issues,
                            $"Navigation rebuild for seed {seed}/r{observation.RepeatIndex} did not safely replan every active path ID.");
                    if (observation.UnsafeTraversalCount != 0 ||
                        !IsFinite(observation.ProgressWhileUnsafeSeconds) ||
                        observation.ProgressWhileUnsafeSeconds > bar.NumericTolerance)
                        AddIssue(issues, $"Unsafe progress occurred while rebuilding navigation for seed {seed}.");
                }
            }

            var durations = run.NavigationRebuilds
                .Select(item => item.CompletedTimeSeconds - item.RequestedTimeSeconds)
                .ToArray();
            metrics.Add(Metric("navigationRebuilds", run.NavigationRebuilds.Count));
            metrics.Add(Metric("maximumRebuildSeconds", durations.Length == 0 ? double.PositiveInfinity : durations.Max()));
            metrics.Add(Metric("unsafeTraversals", run.NavigationRebuilds.Sum(item => (long)item.UnsafeTraversalCount)));
            metrics.Add(Metric("activePaths", run.NavigationRebuilds.Sum(item => (long)item.ActivePathCount)));
            metrics.Add(Metric("safelyReplannedPaths", run.NavigationRebuilds.Sum(item => (long)item.SafelyReplannedPathCount)));
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
            var repeats = legs.Select(item => item.RepeatIndex).OrderBy(item => item).ToArray();
            if (legs.Length != bar.RequiredDeterminismRepeats ||
                !repeats.SequenceEqual(Enumerable.Range(0, bar.RequiredDeterminismRepeats)))
                AddIssue(issues,
                    $"Route {scenarioId}/{layoutSeed}/{memberId}/{from}->{to} must contain exactly repeats " +
                    $"0..{bar.RequiredDeterminismRepeats - 1}.");
        }

        private static void ValidateLayoutRepeats(
            NaturalBehaviorQaRun run,
            string scenarioId,
            int layoutSeed,
            int? requiredFurnitureCount,
            NaturalBehaviorQualityBar bar,
            ICollection<string> issues)
        {
            var layouts = run.Layouts.Where(item =>
                    string.Equals(item.ScenarioId, scenarioId, StringComparison.Ordinal) && item.LayoutSeed == layoutSeed)
                .ToArray();
            var repeats = layouts.Select(item => item.RepeatIndex).OrderBy(item => item).ToArray();
            if (layouts.Length != bar.RequiredDeterminismRepeats ||
                !repeats.SequenceEqual(Enumerable.Range(0, bar.RequiredDeterminismRepeats)))
                AddIssue(issues,
                    $"Layout {scenarioId}/{layoutSeed} must contain exactly repeats 0..{bar.RequiredDeterminismRepeats - 1}.");
            if (layouts.Any(item => !item.Succeeded))
                AddIssue(issues, $"Layout {scenarioId}/{layoutSeed} contains a failed placement repeat.");
            if (requiredFurnitureCount.HasValue &&
                layouts.Any(item => item.FurnitureCount != requiredFurnitureCount.Value))
                AddIssue(issues,
                    $"Layout {scenarioId}/{layoutSeed} must record exactly {requiredFurnitureCount.Value} placeable furniture items per repeat.");
            if (layouts.Length > 0 && layouts.Select(item => item.StableHash).Distinct(StringComparer.Ordinal).Count() != 1)
                AddIssue(issues, $"Layout {scenarioId}/{layoutSeed} produced a non-deterministic layout hash.");
        }

        private static IEnumerable<LayoutIdentity> ExpectedLayouts(NaturalBehaviorQaRun run, NaturalBehaviorQualityBar bar)
        {
            for (var repeat = 0; repeat < bar.RequiredDeterminismRepeats; repeat++)
                yield return new LayoutIdentity(NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, repeat);
            foreach (var seed in run.Plan.RandomLayoutSeeds)
                for (var repeat = 0; repeat < bar.RequiredDeterminismRepeats; repeat++)
                    yield return new LayoutIdentity(NaturalBehaviorQaScenarioIds.RandomFurniture, seed, repeat);
        }

        private static IEnumerable<LayoutObservation> LayoutsFor(NaturalBehaviorQaRun run, LayoutIdentity identity)
        {
            return run.Layouts.Where(item => SameLayout(item, identity));
        }

        private static string FootprintSignature(IEnumerable<FurnitureFootprintObservation> footprints)
        {
            var rows = footprints.Select(item =>
                    item.FurnitureId + "|" + (item.BlocksMovement ? "1" : "0") + "|" +
                    (item.IsPlaceable ? "1" : "0") + "|" +
                    string.Join(";", item.Footprint.Vertices.Select(vertex =>
                        Quantized(vertex.X) + "," + Quantized(vertex.Y))))
                .OrderBy(item => item, StringComparer.Ordinal);
            return NaturalBehaviorQaHash.Sha256Hex(string.Join("\n", rows));
        }

        private static string Quantized(double value)
        {
            var quantized = Math.Round(value, 6, MidpointRounding.AwayFromZero);
            if (quantized == 0d) quantized = 0d;
            return quantized.ToString("0.000000", CultureInfo.InvariantCulture);
        }

        private static IEnumerable<FurnitureFootprintObservation> FootprintsFor(
            NaturalBehaviorQaRun run,
            LayoutIdentity identity)
        {
            return run.FurnitureFootprints.Where(item => SameLayout(item, identity));
        }

        private static bool SameLayout(LayoutObservation item, LayoutIdentity identity)
        {
            return item.LayoutSeed == identity.LayoutSeed && item.RepeatIndex == identity.RepeatIndex &&
                   string.Equals(item.ScenarioId, identity.ScenarioId, StringComparison.Ordinal);
        }

        private static bool SameLayout(FurnitureFootprintObservation item, LayoutIdentity identity)
        {
            return item.LayoutSeed == identity.LayoutSeed && item.RepeatIndex == identity.RepeatIndex &&
                   string.Equals(item.ScenarioId, identity.ScenarioId, StringComparison.Ordinal);
        }

        private static bool SameLayout(FootpointSample item, LayoutIdentity identity)
        {
            return item.LayoutSeed == identity.LayoutSeed && item.RepeatIndex == identity.RepeatIndex &&
                   string.Equals(item.ScenarioId, identity.ScenarioId, StringComparison.Ordinal);
        }

        private static IEnumerable<IGrouping<MotionGroupKey, MotionSample>> MotionGroups(NaturalBehaviorQaRun run)
        {
            return run.MotionSamples.GroupBy(item =>
                new MotionGroupKey(item.ScenarioId, item.LayoutSeed, item.RepeatIndex, item.MemberId));
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

        private static bool SweptCircleOverlapsPolygon(
            QaPoint2 start,
            QaPoint2 end,
            double radius,
            QaPolygon2 polygon,
            double tolerance)
        {
            if (SegmentCrossesPolygonInterior(start, end, polygon, tolerance)) return true;
            var clearance = radius - tolerance;
            if (clearance <= 0d) return false;
            var vertices = polygon.Vertices;
            for (var index = 0; index < vertices.Count; index++)
            {
                var next = (index + 1) % vertices.Count;
                if (DistanceSegmentToSegment(start, end, vertices[index], vertices[next]) < clearance) return true;
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

        private static double DistanceSegmentToSegment(QaPoint2 a, QaPoint2 b, QaPoint2 c, QaPoint2 d)
        {
            if (SegmentCrossesPolygonEdge(a, b, c, d)) return 0d;
            return Math.Min(
                Math.Min(DistancePointToSegment(a, c, d), DistancePointToSegment(b, c, d)),
                Math.Min(DistancePointToSegment(c, a, b), DistancePointToSegment(d, a, b)));
        }

        private static bool SegmentCrossesPolygonEdge(QaPoint2 a, QaPoint2 b, QaPoint2 c, QaPoint2 d)
        {
            const double epsilon = 0.000000000001d;
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            return Math.Max(Math.Min(abC, abD), Math.Min(cdA, cdB)) <= epsilon &&
                   Math.Min(Math.Max(abC, abD), Math.Max(cdA, cdB)) >= -epsilon &&
                   Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <=
                   Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + epsilon &&
                   Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <=
                   Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + epsilon;
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

        private static QaSeatingPixelObservedRole ExpectedObservedRole(QaSeatingPixelExpectation expectation)
        {
            switch (expectation)
            {
                case QaSeatingPixelExpectation.FootAnchor:
                case QaSeatingPixelExpectation.CharacterBody:
                    return QaSeatingPixelObservedRole.CharacterBody;
                case QaSeatingPixelExpectation.ChairForeground:
                    return QaSeatingPixelObservedRole.ChairForeground;
                case QaSeatingPixelExpectation.DeskForeground:
                    return QaSeatingPixelObservedRole.DeskForeground;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expectation));
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double Magnitude(double x, double y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            var maximum = Math.Max(x, y);
            if (maximum == 0d) return 0d;
            if (!IsFinite(maximum)) return double.PositiveInfinity;
            var normalizedX = x / maximum;
            var normalizedY = y / maximum;
            return maximum * Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
        }

        private static void ValidateQualityBar(NaturalBehaviorQualityBar bar)
        {
            if (bar.RequiredRandomLayoutSeeds <= 0 || bar.RequiredFurniturePerRandomLayout <= 0 ||
                bar.RequiredDeterminismRepeats <= 0 || bar.MaximumReplansPerRoute < 0 ||
                bar.SitDownFrameCount <= 0 || bar.WorkFrameCount <= 0 || bar.StandUpFrameCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(bar), "Quality-bar integer limits are invalid.");
            var values = new[]
            {
                bar.MaximumPathStretchP95, bar.MaximumPathStretch, bar.MaximumDeadlockSeconds,
                bar.MaximumMotionSampleGapSeconds, bar.MaximumFrameDeltaMeters,
                bar.MaximumSpeedMetersPerSecond, bar.MaximumAccelerationMetersPerSecondSquared,
                bar.DirectionFlipWindowSeconds, bar.MinimumDirectionFlipSpeed,
                bar.CornerJitterWindowSeconds, bar.CornerJitterRadiusMeters,
                bar.MaximumSeatFootErrorPixels1920, bar.RequiredObservationGameSeconds,
                bar.MaximumNavigationRebuildSeconds, bar.NumericTolerance
            };
            if (values.Any(item => !IsFinite(item) || item < 0d) || bar.NumericTolerance <= 0d)
                throw new ArgumentOutOfRangeException(nameof(bar), "Quality-bar numeric limits must be finite and non-negative.");
            if (bar.WorkActionCooldowns == null ||
                bar.WorkActionCooldowns.Count != Enum.GetValues(typeof(QaWorkVisualAction)).Length ||
                bar.WorkActionCooldowns.Select(item => item.Action).Distinct().Count() != bar.WorkActionCooldowns.Count)
                throw new ArgumentException("Exactly one cooldown contract per work action is required.", nameof(bar));
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
            return $"{path.ScenarioId}/{path.LayoutSeed}/r{path.RepeatIndex}/{path.MemberId}/" +
                   $"{path.FromDestinationId}->{path.ToDestinationId}";
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

        private readonly struct LayoutIdentity : IEquatable<LayoutIdentity>
        {
            public LayoutIdentity(string scenarioId, int layoutSeed, int repeatIndex)
            {
                ScenarioId = scenarioId;
                LayoutSeed = layoutSeed;
                RepeatIndex = repeatIndex;
            }

            public string ScenarioId { get; }
            public int LayoutSeed { get; }
            public int RepeatIndex { get; }

            public bool Equals(LayoutIdentity other) =>
                LayoutSeed == other.LayoutSeed && RepeatIndex == other.RepeatIndex &&
                string.Equals(ScenarioId, other.ScenarioId, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is LayoutIdentity other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = StringComparer.Ordinal.GetHashCode(ScenarioId ?? string.Empty);
                    hash = hash * 397 ^ LayoutSeed;
                    hash = hash * 397 ^ RepeatIndex;
                    return hash;
                }
            }
            public override string ToString() => $"{ScenarioId}/{LayoutSeed}/r{RepeatIndex}";
        }

        private readonly struct MotionGroupKey : IEquatable<MotionGroupKey>
        {
            public MotionGroupKey(string scenarioId, int layoutSeed, int repeatIndex, string memberId)
            {
                ScenarioId = scenarioId;
                LayoutSeed = layoutSeed;
                RepeatIndex = repeatIndex;
                MemberId = memberId;
            }

            public string ScenarioId { get; }
            public int LayoutSeed { get; }
            public int RepeatIndex { get; }
            public string MemberId { get; }

            public bool Equals(MotionGroupKey other)
            {
                return LayoutSeed == other.LayoutSeed && RepeatIndex == other.RepeatIndex &&
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
                    hash = hash * 397 ^ RepeatIndex;
                    hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(MemberId ?? string.Empty);
                    return hash;
                }
            }
            public override string ToString() => $"{ScenarioId}/{LayoutSeed}/r{RepeatIndex}/{MemberId}";
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
            var rows = new List<string>
            {
                Row("HEADER", (int)run.Capabilities, run.ObservationGameSeconds,
                    run.Plan.ObservationGameSeconds, run.Plan.MaximumWallClockSeconds, run.Plan.SemanticOriginId),
                Row("MEMBERS", run.Plan.ExpectedMemberIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()),
                Row("DESTINATIONS", run.Plan.SemanticDestinationIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()),
                Row("SEEDS", run.Plan.RandomLayoutSeeds.OrderBy(item => item).Cast<object>().ToArray())
            };

            rows.AddRange(run.Layouts.Select(item => Row("L", item.ScenarioId, item.LayoutSeed, item.RepeatIndex,
                item.FurnitureCount, item.Succeeded, item.StableHash)));
            rows.AddRange(run.FurnitureFootprints.Select(item => Row("F", item.ScenarioId, item.LayoutSeed,
                item.RepeatIndex, item.FurnitureId, item.BlocksMovement, item.IsPlaceable,
                string.Join(";", item.Footprint.Vertices.Select(vertex => F(vertex.X) + "," + F(vertex.Y))))));
            rows.AddRange(run.Footpoints.Select(item => Row("P", item.ScenarioId, item.LayoutSeed, item.RepeatIndex,
                item.MemberId, item.TimeSeconds, item.Position.X, item.Position.Y, item.RadiusMeters, item.Visible)));
            rows.AddRange(run.MotionSamples.Select(item => Row("M", item.ScenarioId, item.LayoutSeed, item.RepeatIndex,
                item.MemberId, item.TimeSeconds, item.Position.X, item.Position.Y, item.DirectionIndex,
                (int)item.Phase, item.Visible)));
            rows.AddRange(run.Paths.Select(item => Row("R", item.ScenarioId, item.LayoutSeed, item.RepeatIndex,
                item.MemberId, item.FromDestinationId, item.ToDestinationId, item.Succeeded,
                item.DirectDistanceMeters, item.TravelledDistanceMeters, item.ReplanCount,
                item.DeadlockSeconds, item.StablePathHash, item.UnsafeTraversalCount)));
            rows.AddRange(run.CaptureArtifacts.Select(item =>
                Row("C", item.Label, item.Sha256, item.Width, item.Height)));
            rows.AddRange(run.SeatingFrames.Select(item => Row("S", item.SessionId, item.MemberId,
                item.SessionTimeSeconds, (int)item.Phase, item.FrameIndex, item.FootPixel1920.X,
                item.FootPixel1920.Y, item.CaptureEvidence.CaptureLabel,
                item.CaptureEvidence.CaptureSha256, item.CaptureEvidence.Width,
                item.CaptureEvidence.Height,
                string.Join(";", item.CaptureEvidence.PixelObservations
                    .Select(pixel => Row("PX", pixel.X, pixel.Y, (int)pixel.Expectation, (int)pixel.ObservedRole))
                    .OrderBy(value => value, StringComparer.Ordinal)))));
            rows.AddRange(run.WorkWindows.Select(item => Row("O", item.MemberId,
                item.ObservationGameSeconds, item.AccumulatedWorkSeconds)));
            rows.AddRange(run.WorkActions.Select(item => Row("A", item.MemberId, (int)item.Action,
                item.TimeSeconds, item.AccumulatedWorkSeconds, item.WorkSecondsSincePreviousSameAction,
                (int)item.Phase, item.VisualVisible)));
            rows.AddRange(run.Productivity.Select(item => Row("W", item.MemberId, item.TimeSeconds,
                (int)item.Phase, item.ProductivityDelta)));
            rows.AddRange(run.NavigationRebuilds.Select(item => Row("N", item.ScenarioId, item.LayoutSeed,
                item.RepeatIndex, item.RequestedTimeSeconds, item.CompletedTimeSeconds,
                string.Join(";", item.ActivePathIds.OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(";", item.SafelyReplannedPathIds.OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(";", item.UnsafeTraversalPathIds.OrderBy(value => value, StringComparer.Ordinal)),
                item.ProgressWhileUnsafeSeconds)));

            return Sha256Hex(string.Join("\n", rows.OrderBy(item => item, StringComparer.Ordinal)));
        }

        public static string Sha256Hex(string stableText)
        {
            if (stableText == null) throw new ArgumentNullException(nameof(stableText));
            return Sha256Hex(Encoding.UTF8.GetBytes(stableText));
        }

        public static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var result = new StringBuilder(hash.Length * 2);
            foreach (var value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static string Row(string prefix, params object[] values)
        {
            var builder = new StringBuilder(prefix ?? string.Empty);
            foreach (var value in values ?? Array.Empty<object>())
            {
                string text;
                if (value is double number) text = F(number);
                else if (value is bool flag) text = flag ? "1" : "0";
                else text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                builder.Append('|').Append(text.Length).Append(':').Append(text);
            }
            return builder.ToString();
        }

        private static string F(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Run hashes require finite numeric observations.");
            var quantized = Math.Round(value, 6, MidpointRounding.AwayFromZero);
            if (quantized == 0d) quantized = 0d;
            return quantized.ToString("0.000000", CultureInfo.InvariantCulture);
        }
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

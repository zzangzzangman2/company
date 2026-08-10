using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Qa.NaturalBehavior
{
    public static class NaturalBehaviorQaSelfTest
    {
        public const string PassMarker = "NATURAL_BEHAVIOR_QA_METRIC_LIBRARY: PASS";

        public static string Run()
        {
            var passing = BuildPassingRun();
            var result = NaturalBehaviorQualityEvaluator.Evaluate(passing);
            Require(result.Passed, "Canonical passing fixture did not pass all quality gates.\n" +
                                   NaturalBehaviorQaReportFormatter.ToText(result));
            Require(result.Gates.Count == 6, "Evaluator must always return exactly six gates.");

            ValidateHashStability(result.RunHash);
            ValidateFiniteModelBoundary();
            ValidateLifecycleGuard();
            ValidatePassableDecorationFlag();

            AssertGateFailure(RemoveOneRepeatFootprint(), 0, "placeable furniture footprints");
            AssertGateFailure(MakeOneFootpointTraceInvisible(), 0, "complete visible footpoint coverage");
            AssertGateFailure(InjectSweptRadiusCollision(), 0, "Movement crossed");
            AssertGateFailure(DuplicateOnePathRepeat(), 1, "must contain exactly repeats");
            AssertGateFailure(InjectPathStretch(), 1, "Maximum path stretch");
            AssertGateFailure(InjectReplanOverflow(), 1, "Path replanned");
            AssertGateFailure(InjectDeadlock(), 1, "Path deadlocked");
            AssertGateFailure(MakeOneMotionTraceInvisible(), 2, "invisible/partial samples");
            AssertGateFailure(RemoveOneMotionTrace(), 2, "fewer than three samples");
            AssertGateFailure(InjectVectorAcceleration(), 2, "Acceleration");
            AssertGateFailure(InjectDirectionFlip(), 2, "180-degree direction flip");
            AssertGateFailure(InjectCornerJitter(), 2, "Corner direction jitter");
            AssertGateFailure(InjectWrongCaptureDimensions(), 3, "1920x1080 is required");
            AssertGateFailure(RemoveOneCaptureArtifact(), 3, "harness-recorded capture artifact");
            AssertGateFailure(InjectCapturePixelMismatch(), 3, "pixel classification failures");
            AssertGateFailure(RemoveOneSeatingFrame(), 3, "Invalid Work loop frame order");
            AssertGateFailure(InjectEarlyFirstWorkAction(), 4, "violated its minimum cooldown");
            AssertGateFailure(InjectDuplicateWorkTimestamp(), 4, "invalid or non-increasing event timestamp");
            AssertGateFailure(InjectNonWorkProductivity(), 4, "Productivity delta");
            AssertGateFailure(RemoveOneRebuildRepeat(), 5, "must contain exactly repeats");
            AssertGateFailure(InjectPartialSafeReplan(), 5, "did not safely replan every active path ID");

            Require(NaturalBehaviorQaReportFormatter.ToText(result).Contains(result.Marker),
                "Text report omitted the aggregate marker.");
            Require(NaturalBehaviorQaReportFormatter.ToJson(result).Contains("\"passed\": true"),
                "JSON report omitted its passing state.");
            return PassMarker;
        }

        private static NaturalBehaviorQaRun BuildPassingRun()
        {
            var plan = NaturalBehaviorQaPlan.CreateCanonical();
            var run = new NaturalBehaviorQaRun(plan, NaturalBehaviorQaCapability.All)
            {
                ObservationGameSeconds = 1800d
            };

            AddLayout(run, NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 1);
            AddRoundTrips(run, NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0);
            foreach (var seed in plan.RandomLayoutSeeds)
            {
                AddLayout(run, NaturalBehaviorQaScenarioIds.RandomFurniture, seed, 100);
                AddRoundTrips(run, NaturalBehaviorQaScenarioIds.RandomFurniture, seed);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var active = new[]
                    {
                        $"{seed}-r{repeat}-player",
                        $"{seed}-r{repeat}-older_sister",
                        $"{seed}-r{repeat}-father",
                        $"{seed}-r{repeat}-mother"
                    };
                    run.NavigationRebuilds.Add(new NavigationRebuildObservation(
                        NaturalBehaviorQaScenarioIds.RandomFurniture,
                        seed,
                        repeat,
                        seed + repeat * 20d,
                        seed + repeat * 20d + 4d,
                        active,
                        active,
                        Array.Empty<string>(),
                        0d));
                }
            }

            foreach (var memberId in plan.ExpectedMemberIds)
            {
                AddSeating(run, memberId);
                AddWorkActions(run, memberId);
            }
            return run;
        }

        private static void AddLayout(NaturalBehaviorQaRun run, string scenarioId, int seed, int furnitureCount)
        {
            var layoutHash = Sha($"layout/{scenarioId}/{seed}");
            for (var repeat = 0; repeat < 2; repeat++)
            {
                run.Layouts.Add(new LayoutObservation(scenarioId, seed, repeat, furnitureCount, true, layoutHash));
                for (var index = 0; index < furnitureCount; index++)
                {
                    var x = 100d + index * 2d;
                    run.FurnitureFootprints.Add(new FurnitureFootprintObservation(
                        scenarioId,
                        seed,
                        repeat,
                        $"furniture-{index:D3}",
                        Square(x, 100d, 0.75d),
                        index % 2 == 0,
                        true));
                }

                for (var memberIndex = 0; memberIndex < run.Plan.ExpectedMemberIds.Count; memberIndex++)
                {
                    var memberId = run.Plan.ExpectedMemberIds[memberIndex];
                    for (var sampleIndex = 0; sampleIndex < 3; sampleIndex++)
                    {
                        var time = sampleIndex * 0.04d;
                        var position = new QaPoint2(sampleIndex * 0.02d, memberIndex * 0.5d);
                        run.Footpoints.Add(new FootpointSample(
                            scenarioId, seed, repeat, memberId, time, position, 0.01d, true));
                        run.MotionSamples.Add(new MotionSample(
                            scenarioId, seed, repeat, memberId, time, position, 6,
                            QaMotionPhase.Walking, true));
                    }
                }
            }
        }

        private static void AddRoundTrips(NaturalBehaviorQaRun run, string scenarioId, int seed)
        {
            foreach (var memberId in run.Plan.ExpectedMemberIds)
            foreach (var destination in run.Plan.SemanticDestinationIds)
            {
                AddRepeatedLeg(run, scenarioId, seed, memberId, run.Plan.SemanticOriginId, destination);
                AddRepeatedLeg(run, scenarioId, seed, memberId, destination, run.Plan.SemanticOriginId);
            }
        }

        private static void AddRepeatedLeg(
            NaturalBehaviorQaRun run,
            string scenarioId,
            int seed,
            string memberId,
            string from,
            string to)
        {
            var pathHash = Sha($"path/{scenarioId}/{seed}/{memberId}/{from}/{to}");
            for (var repeat = 0; repeat < 2; repeat++)
                run.Paths.Add(new PathObservation(
                    scenarioId, seed, repeat, memberId, from, to, true,
                    10d, 12d, 1, 0.1d, pathHash, 0));
        }

        private static void AddSeating(NaturalBehaviorQaRun run, string memberId)
        {
            var sessionId = $"seat-{memberId}";
            var time = 0d;
            run.SeatingFrames.Add(Seat(run, sessionId, memberId, time, QaSeatingPhase.Approach, -1, 500));
            for (var frame = 0; frame < 4; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(run, sessionId, memberId, time, QaSeatingPhase.SitDown, frame, 501));
            }
            for (var frame = 0; frame < 6; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(run, sessionId, memberId, time, QaSeatingPhase.Work, frame, 501));
            }
            for (var frame = 0; frame < 4; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(run, sessionId, memberId, time, QaSeatingPhase.StandUp, frame, 501));
            }
            time += 0.1d;
            run.SeatingFrames.Add(Seat(run, sessionId, memberId, time, QaSeatingPhase.Complete, -1, 500));
        }

        private static SeatingFrameObservation Seat(
            NaturalBehaviorQaRun run,
            string sessionId,
            string memberId,
            double time,
            QaSeatingPhase phase,
            int frame,
            int footX,
            int width = 1920,
            int height = 1080)
        {
            var pixels = new[]
            {
                new SeatingPixelObservation(footX, 700, QaSeatingPixelExpectation.FootAnchor,
                    QaSeatingPixelObservedRole.CharacterBody),
                new SeatingPixelObservation(footX, 699, QaSeatingPixelExpectation.CharacterBody,
                    QaSeatingPixelObservedRole.CharacterBody),
                new SeatingPixelObservation(Math.Max(0, footX - 2), 698, QaSeatingPixelExpectation.ChairForeground,
                    QaSeatingPixelObservedRole.ChairForeground),
                new SeatingPixelObservation(Math.Min(width - 1, footX + 2), 697, QaSeatingPixelExpectation.DeskForeground,
                    QaSeatingPixelObservedRole.DeskForeground)
            };
            var captureLabel = $"{sessionId}-{phase}-{frame}-{time:R}";
            var capture = new SeatingCaptureEvidence(
                captureLabel,
                Sha($"capture/{sessionId}/{time:R}/{phase}/{frame}/{width}x{height}"),
                width,
                height,
                pixels);
            run.CaptureArtifacts.Add(new NaturalBehaviorQaCaptureArtifact(
                captureLabel, capture.CaptureSha256, width, height));
            return new SeatingFrameObservation(
                sessionId, memberId, time, phase, frame, new QaPoint2(footX, 700d), capture);
        }

        private static void AddWorkActions(NaturalBehaviorQaRun run, string memberId)
        {
            run.WorkWindows.Add(new WorkWindowObservation(memberId, 1800d, 1800d));
            AddRecurringActions(run, memberId, QaWorkVisualAction.Typing, 1d, 1800d);
            AddRecurringActions(run, memberId, QaWorkVisualAction.Mouse, 4d, 1800d);
            AddRecurringActions(run, memberId, QaWorkVisualAction.Drink, 60d, 1800d);
            run.Productivity.Add(new ProductivityObservation(memberId, 0d, QaMotionPhase.SittingDown, 0d));
            run.Productivity.Add(new ProductivityObservation(memberId, 1d, QaMotionPhase.Work, 1d));
            run.Productivity.Add(new ProductivityObservation(memberId, 2d, QaMotionPhase.StandingUp, 0d));
        }

        private static void AddRecurringActions(
            NaturalBehaviorQaRun run,
            string memberId,
            QaWorkVisualAction action,
            double cooldown,
            double workWindowSeconds)
        {
            for (var workSeconds = cooldown; workSeconds <= workWindowSeconds; workSeconds += cooldown)
                run.WorkActions.Add(new WorkActionObservation(
                    memberId, action, workSeconds, workSeconds, cooldown, QaMotionPhase.Work, true));
        }

        private static NaturalBehaviorQaRun RemoveOneRepeatFootprint()
        {
            var run = BuildPassingRun();
            var seed = run.Plan.RandomLayoutSeeds[0];
            run.FurnitureFootprints.RemoveAll(item =>
                item.ScenarioId == NaturalBehaviorQaScenarioIds.RandomFurniture && item.LayoutSeed == seed &&
                item.RepeatIndex == 1 && item.FurnitureId == "furniture-099");
            return run;
        }

        private static NaturalBehaviorQaRun InjectSweptRadiusCollision()
        {
            var run = BuildPassingRun();
            var index = run.FurnitureFootprints.FindIndex(item =>
                item.ScenarioId == NaturalBehaviorQaScenarioIds.SemanticRoundTrip && item.LayoutSeed == 0 &&
                item.RepeatIndex == 0 && item.FurnitureId == "furniture-000");
            run.FurnitureFootprints[index] = new FurnitureFootprintObservation(
                NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 0, "furniture-000",
                Square(0.01d, 0.01d, 0.002d), true, true);
            return run;
        }

        private static NaturalBehaviorQaRun MakeOneFootpointTraceInvisible()
        {
            var run = BuildPassingRun();
            for (var index = 0; index < run.Footpoints.Count; index++)
            {
                var item = run.Footpoints[index];
                if (item.ScenarioId != NaturalBehaviorQaScenarioIds.SemanticRoundTrip || item.LayoutSeed != 0 ||
                    item.RepeatIndex != 0 || item.MemberId != "player") continue;
                run.Footpoints[index] = new FootpointSample(
                    item.ScenarioId, item.LayoutSeed, item.RepeatIndex, item.MemberId,
                    item.TimeSeconds, item.Position, item.RadiusMeters, false);
            }
            return run;
        }

        private static NaturalBehaviorQaRun DuplicateOnePathRepeat()
        {
            var run = BuildPassingRun();
            var index = run.Paths.FindIndex(item => item.RepeatIndex == 1);
            var item = run.Paths[index];
            run.Paths[index] = CopyPath(item, repeatIndex: 0);
            return run;
        }

        private static NaturalBehaviorQaRun InjectPathStretch()
        {
            var run = BuildPassingRun();
            run.Paths[0] = CopyPath(run.Paths[0], travelledDistance: 20d);
            return run;
        }

        private static NaturalBehaviorQaRun InjectReplanOverflow()
        {
            var run = BuildPassingRun();
            run.Paths[0] = CopyPath(run.Paths[0], replanCount: 4);
            return run;
        }

        private static NaturalBehaviorQaRun InjectDeadlock()
        {
            var run = BuildPassingRun();
            run.Paths[0] = CopyPath(run.Paths[0], deadlockSeconds: 0.8d);
            return run;
        }

        private static PathObservation CopyPath(
            PathObservation item,
            int? repeatIndex = null,
            double? travelledDistance = null,
            int? replanCount = null,
            double? deadlockSeconds = null)
        {
            return new PathObservation(
                item.ScenarioId, item.LayoutSeed, repeatIndex ?? item.RepeatIndex, item.MemberId,
                item.FromDestinationId, item.ToDestinationId, item.Succeeded, item.DirectDistanceMeters,
                travelledDistance ?? item.TravelledDistanceMeters, replanCount ?? item.ReplanCount,
                deadlockSeconds ?? item.DeadlockSeconds, item.StablePathHash, item.UnsafeTraversalCount);
        }

        private static NaturalBehaviorQaRun MakeOneMotionTraceInvisible()
        {
            var run = BuildPassingRun();
            ReplaceMotionTrace(run, item => new MotionSample(
                item.ScenarioId, item.LayoutSeed, item.RepeatIndex, item.MemberId, item.TimeSeconds,
                item.Position, item.DirectionIndex, item.Phase, false));
            return run;
        }

        private static NaturalBehaviorQaRun RemoveOneMotionTrace()
        {
            var run = BuildPassingRun();
            run.MotionSamples.RemoveAll(IsTargetMotion);
            return run;
        }

        private static NaturalBehaviorQaRun InjectVectorAcceleration()
        {
            var run = BuildPassingRun();
            run.MotionSamples.RemoveAll(IsTargetMotion);
            run.MotionSamples.Add(new MotionSample(NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 0,
                "player", 0d, new QaPoint2(0d, 0d), 0, QaMotionPhase.Walking, true));
            run.MotionSamples.Add(new MotionSample(NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 0,
                "player", 0.05d, new QaPoint2(0.09d, 0d), 0, QaMotionPhase.Walking, true));
            run.MotionSamples.Add(new MotionSample(NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 0,
                "player", 0.10d, new QaPoint2(0.09d, 0.09d), 2, QaMotionPhase.Walking, true));
            return run;
        }

        private static NaturalBehaviorQaRun InjectDirectionFlip()
        {
            var run = BuildPassingRun();
            var target = run.MotionSamples.Where(IsTargetMotion).OrderBy(item => item.TimeSeconds).ToArray();
            var changed = target[1];
            run.MotionSamples[run.MotionSamples.IndexOf(changed)] = new MotionSample(
                changed.ScenarioId, changed.LayoutSeed, changed.RepeatIndex, changed.MemberId,
                changed.TimeSeconds, changed.Position, 2, changed.Phase, true);
            return run;
        }

        private static NaturalBehaviorQaRun InjectCornerJitter()
        {
            var run = BuildPassingRun();
            run.MotionSamples.RemoveAll(IsTargetMotion);
            var directions = new[] { 0, 1, 0, 1 };
            for (var index = 0; index < directions.Length; index++)
                run.MotionSamples.Add(new MotionSample(
                    NaturalBehaviorQaScenarioIds.SemanticRoundTrip, 0, 0, "player", index * 0.04d,
                    new QaPoint2(index * 0.02d, 0d), directions[index], QaMotionPhase.Walking, true));
            return run;
        }

        private static void ReplaceMotionTrace(NaturalBehaviorQaRun run, Func<MotionSample, MotionSample> transform)
        {
            for (var index = 0; index < run.MotionSamples.Count; index++)
                if (IsTargetMotion(run.MotionSamples[index])) run.MotionSamples[index] = transform(run.MotionSamples[index]);
        }

        private static bool IsTargetMotion(MotionSample item) =>
            item.ScenarioId == NaturalBehaviorQaScenarioIds.SemanticRoundTrip && item.LayoutSeed == 0 &&
            item.RepeatIndex == 0 && item.MemberId == "player";

        private static NaturalBehaviorQaRun InjectWrongCaptureDimensions()
        {
            var run = BuildPassingRun();
            var index = run.SeatingFrames.FindIndex(item => item.MemberId == "player" && item.Phase == QaSeatingPhase.Work);
            var item = run.SeatingFrames[index];
            run.SeatingFrames[index] = Seat(run, item.SessionId, item.MemberId, item.SessionTimeSeconds,
                item.Phase, item.FrameIndex, (int)item.FootPixel1920.X, 1280, 720);
            return run;
        }

        private static NaturalBehaviorQaRun RemoveOneSeatingFrame()
        {
            var run = BuildPassingRun();
            var index = run.SeatingFrames.FindIndex(item =>
                item.MemberId == "player" && item.Phase == QaSeatingPhase.Work && item.FrameIndex == 3);
            run.SeatingFrames.RemoveAt(index);
            return run;
        }

        private static NaturalBehaviorQaRun RemoveOneCaptureArtifact()
        {
            var run = BuildPassingRun();
            var hash = run.SeatingFrames.First(item => item.MemberId == "player").CaptureEvidence.CaptureSha256;
            run.CaptureArtifacts.RemoveAll(item => item.Sha256 == hash);
            return run;
        }

        private static NaturalBehaviorQaRun InjectCapturePixelMismatch()
        {
            var run = BuildPassingRun();
            var index = run.SeatingFrames.FindIndex(item => item.MemberId == "player" && item.Phase == QaSeatingPhase.Work);
            var item = run.SeatingFrames[index];
            var pixels = item.CaptureEvidence.PixelObservations.Select(pixel =>
                pixel.Expectation == QaSeatingPixelExpectation.ChairForeground
                    ? new SeatingPixelObservation(pixel.X, pixel.Y, pixel.Expectation, QaSeatingPixelObservedRole.CharacterBody)
                    : pixel).ToArray();
            var evidence = new SeatingCaptureEvidence(
                item.CaptureEvidence.CaptureLabel,
                item.CaptureEvidence.CaptureSha256,
                item.CaptureEvidence.Width,
                item.CaptureEvidence.Height,
                pixels);
            run.SeatingFrames[index] = new SeatingFrameObservation(
                item.SessionId, item.MemberId, item.SessionTimeSeconds, item.Phase,
                item.FrameIndex, item.FootPixel1920, evidence);
            return run;
        }

        private static NaturalBehaviorQaRun InjectEarlyFirstWorkAction()
        {
            var run = BuildPassingRun();
            var index = run.WorkActions.FindIndex(item =>
                item.MemberId == "player" && item.Action == QaWorkVisualAction.Typing);
            run.WorkActions[index] = new WorkActionObservation(
                "player", QaWorkVisualAction.Typing, 0.1d, 0.1d, 0.1d, QaMotionPhase.Work, true);
            return run;
        }

        private static NaturalBehaviorQaRun InjectDuplicateWorkTimestamp()
        {
            var run = BuildPassingRun();
            var items = run.WorkActions.Where(item =>
                item.MemberId == "player" && item.Action == QaWorkVisualAction.Mouse).Take(2).ToArray();
            var secondIndex = run.WorkActions.IndexOf(items[1]);
            run.WorkActions[secondIndex] = new WorkActionObservation(
                items[1].MemberId, items[1].Action, items[0].TimeSeconds,
                items[1].AccumulatedWorkSeconds, items[1].WorkSecondsSincePreviousSameAction,
                items[1].Phase, items[1].VisualVisible);
            return run;
        }

        private static NaturalBehaviorQaRun InjectNonWorkProductivity()
        {
            var run = BuildPassingRun();
            var index = run.Productivity.FindIndex(item => item.MemberId == "player" && item.Phase != QaMotionPhase.Work);
            var item = run.Productivity[index];
            run.Productivity[index] = new ProductivityObservation(item.MemberId, item.TimeSeconds, item.Phase, 1d);
            return run;
        }

        private static NaturalBehaviorQaRun RemoveOneRebuildRepeat()
        {
            var run = BuildPassingRun();
            var seed = run.Plan.RandomLayoutSeeds[0];
            run.NavigationRebuilds.RemoveAll(item => item.LayoutSeed == seed && item.RepeatIndex == 1);
            return run;
        }

        private static NaturalBehaviorQaRun InjectPartialSafeReplan()
        {
            var run = BuildPassingRun();
            var item = run.NavigationRebuilds[0];
            run.NavigationRebuilds[0] = new NavigationRebuildObservation(
                item.ScenarioId, item.LayoutSeed, item.RepeatIndex,
                item.RequestedTimeSeconds, item.CompletedTimeSeconds,
                item.ActivePathIds, item.SafelyReplannedPathIds.Take(item.SafelyReplannedPathIds.Count - 1),
                item.UnsafeTraversalPathIds, item.ProgressWhileUnsafeSeconds);
            return run;
        }

        private static void ValidateHashStability(string canonicalHash)
        {
            var reordered = BuildPassingRun();
            ReverseAll(reordered);
            Require(string.Equals(canonicalHash, NaturalBehaviorQaHash.Compute(reordered), StringComparison.Ordinal),
                "Equivalent observation ordering changed the deterministic run hash.");

            var quantized = BuildPassingRun();
            var item = quantized.Footpoints[0];
            quantized.Footpoints[0] = new FootpointSample(
                item.ScenarioId, item.LayoutSeed, item.RepeatIndex, item.MemberId,
                item.TimeSeconds + 0.0000004d,
                new QaPoint2(item.Position.X + 0.0000004d, item.Position.Y),
                item.RadiusMeters, item.Visible);
            Require(string.Equals(canonicalHash, NaturalBehaviorQaHash.Compute(quantized), StringComparison.Ordinal),
                "Sub-quantum numeric noise changed the deterministic run hash.");

            var first = BuildPassingRun();
            var second = BuildPassingRun();
            first.Productivity.Add(new ProductivityObservation("player", 10d, QaMotionPhase.Work, 2d));
            first.Productivity.Add(new ProductivityObservation("player", 10d, QaMotionPhase.Work, 3d));
            second.Productivity.Add(new ProductivityObservation("player", 10d, QaMotionPhase.Work, 3d));
            second.Productivity.Add(new ProductivityObservation("player", 10d, QaMotionPhase.Work, 2d));
            Require(string.Equals(NaturalBehaviorQaHash.Compute(first), NaturalBehaviorQaHash.Compute(second), StringComparison.Ordinal),
                "Equal-key observations lack a total deterministic hash ordering.");
        }

        private static void ReverseAll(NaturalBehaviorQaRun run)
        {
            run.Layouts.Reverse();
            run.FurnitureFootprints.Reverse();
            run.Footpoints.Reverse();
            run.MotionSamples.Reverse();
            run.Paths.Reverse();
            run.SeatingFrames.Reverse();
            run.CaptureArtifacts.Reverse();
            run.WorkWindows.Reverse();
            run.WorkActions.Reverse();
            run.Productivity.Reverse();
            run.NavigationRebuilds.Reverse();
        }

        private static void ValidateFiniteModelBoundary()
        {
            RequireThrows<ArgumentOutOfRangeException>(() =>
                new PathObservation("scenario", 1, 0, "member", "from", "to", true,
                    double.NaN, 1d, 0, 0d, Sha("path"), 0), "finite path values must be rejected");
            RequireThrows<ArgumentOutOfRangeException>(() =>
                new NavigationRebuildObservation("scenario", 1, 0, 0d, double.PositiveInfinity,
                    new[] { "path" }, new[] { "path" }, Array.Empty<string>(), 0d),
                "finite rebuild values must be rejected");
            RequireThrows<ArgumentOutOfRangeException>(() =>
            {
                var run = new NaturalBehaviorQaRun(NaturalBehaviorQaPlan.CreateCanonical(), NaturalBehaviorQaCapability.All);
                run.ObservationGameSeconds = double.NaN;
            }, "finite observation duration must be rejected");
        }

        private static void ValidateLifecycleGuard()
        {
            RequireThrows<InvalidOperationException>(() =>
                    NaturalBehaviorQaLifecycleGuard.RequireCanStart(true, false),
                "an active QA session must reject re-entry");
            RequireThrows<InvalidOperationException>(() =>
                    NaturalBehaviorQaLifecycleGuard.RequireCanStart(false, true),
                "play-mode transition must reject QA start");
            Require(NaturalBehaviorQaLifecycleGuard.IsAbandonedPreparation(true, 1, false, false),
                "abandoned preparation was not detected.");
        }

        private static void ValidatePassableDecorationFlag()
        {
            var run = BuildPassingRun();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var index = run.FurnitureFootprints.FindIndex(item =>
                    item.ScenarioId == NaturalBehaviorQaScenarioIds.SemanticRoundTrip &&
                    item.RepeatIndex == repeat);
                var item = run.FurnitureFootprints[index];
                run.FurnitureFootprints[index] = new FurnitureFootprintObservation(
                    item.ScenarioId, item.LayoutSeed, item.RepeatIndex, item.FurnitureId,
                    Square(0d, 0d, 0.05d), false, true);
            }
            var result = NaturalBehaviorQualityEvaluator.Evaluate(run);
            Require(result.Passed, "Explicit pass-through decoration incorrectly blocked spatial QA.\n" +
                                   NaturalBehaviorQaReportFormatter.ToText(result));
        }

        private static void AssertGateFailure(NaturalBehaviorQaRun run, int gateIndex, string expectedIssue)
        {
            var result = NaturalBehaviorQualityEvaluator.Evaluate(run);
            Require(!result.Gates[gateIndex].Passed,
                $"Injected defect did not trip gate {result.Gates[gateIndex].GateId}.");
            Require(result.Gates[gateIndex].Issues.Any(item =>
                    item.IndexOf(expectedIssue, StringComparison.OrdinalIgnoreCase) >= 0),
                $"Gate {result.Gates[gateIndex].GateId} failed for the wrong reason; expected '{expectedIssue}'.\n" +
                string.Join("\n", result.Gates[gateIndex].Issues));
            for (var index = 0; index < result.Gates.Count; index++)
            {
                if (index == gateIndex) continue;
                Require(result.Gates[index].Passed,
                    $"Injected defect for gate {result.Gates[gateIndex].GateId} also tripped " +
                    $"{result.Gates[index].GateId}: {string.Join(" | ", result.Gates[index].Issues)}");
            }
        }

        private static QaPolygon2 Square(double centerX, double centerY, double halfSize)
        {
            return new QaPolygon2(new[]
            {
                new QaPoint2(centerX - halfSize, centerY - halfSize),
                new QaPoint2(centerX + halfSize, centerY - halfSize),
                new QaPoint2(centerX + halfSize, centerY + halfSize),
                new QaPoint2(centerX - halfSize, centerY + halfSize)
            });
        }

        private static string Sha(string value) => NaturalBehaviorQaHash.Sha256Hex(value);

        private static void RequireThrows<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

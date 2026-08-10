using System;
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

            var repeatedHash = NaturalBehaviorQaHash.Compute(BuildPassingRun());
            Require(string.Equals(result.RunHash, repeatedHash, StringComparison.Ordinal),
                "Equivalent observations did not produce the same deterministic run hash.");
            Require(NaturalBehaviorQaReportFormatter.ToText(result).Contains(result.Marker),
                "Text report omitted the aggregate marker.");
            Require(NaturalBehaviorQaReportFormatter.ToJson(result).Contains("\"passed\": true"),
                "JSON report omitted its passing state.");

            for (var gateIndex = 0; gateIndex < 6; gateIndex++)
            {
                var failing = BuildPassingRun();
                InjectFailure(failing, gateIndex);
                var failedResult = NaturalBehaviorQualityEvaluator.Evaluate(failing);
                Require(!failedResult.Gates[gateIndex].Passed,
                    $"Injected failure did not trip gate {failedResult.Gates[gateIndex].GateId}.");
            }

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
                run.NavigationRebuilds.Add(new NavigationRebuildObservation(
                    NaturalBehaviorQaScenarioIds.RandomFurniture,
                    seed,
                    seed,
                    seed + 4d,
                    4,
                    4,
                    0,
                    0d));
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
            for (var repeat = 0; repeat < 2; repeat++)
                run.Layouts.Add(new LayoutObservation(scenarioId, seed, repeat, furnitureCount, true, $"layout-{seed}"));

            for (var index = 0; index < furnitureCount; index++)
            {
                var x = 100d + index * 2d;
                run.FurnitureFootprints.Add(new FurnitureFootprintObservation(
                    scenarioId,
                    seed,
                    $"furniture-{index:D3}",
                    Square(x, 100d, 0.75d),
                    true,
                    index % 2 == 0));
            }

            for (var memberIndex = 0; memberIndex < run.Plan.ExpectedMemberIds.Count; memberIndex++)
            {
                var memberId = run.Plan.ExpectedMemberIds[memberIndex];
                for (var sampleIndex = 0; sampleIndex < 3; sampleIndex++)
                {
                    var time = sampleIndex * 0.04d;
                    var position = new QaPoint2(sampleIndex * 0.02d, memberIndex * 0.5d);
                    run.Footpoints.Add(new FootpointSample(scenarioId, seed, memberId, time, position, 0.01d, true));
                    run.MotionSamples.Add(new MotionSample(
                        scenarioId,
                        seed,
                        memberId,
                        time,
                        position,
                        6,
                        QaMotionPhase.Walking,
                        true));
                }
            }
        }

        private static void AddRoundTrips(NaturalBehaviorQaRun run, string scenarioId, int seed)
        {
            foreach (var memberId in run.Plan.ExpectedMemberIds)
            {
                foreach (var destination in run.Plan.SemanticDestinationIds)
                {
                    AddRepeatedLeg(run, scenarioId, seed, memberId, run.Plan.SemanticOriginId, destination);
                    AddRepeatedLeg(run, scenarioId, seed, memberId, destination, run.Plan.SemanticOriginId);
                }
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
            for (var repeat = 0; repeat < 2; repeat++)
            {
                run.Paths.Add(new PathObservation(
                    scenarioId,
                    seed,
                    repeat,
                    memberId,
                    from,
                    to,
                    true,
                    10d,
                    12d,
                    1,
                    0.1d,
                    $"path-{scenarioId}-{seed}-{memberId}-{from}-{to}",
                    0));
            }
        }

        private static void AddSeating(NaturalBehaviorQaRun run, string memberId)
        {
            var sessionId = $"seat-{memberId}";
            var time = 0d;
            run.SeatingFrames.Add(Seat(sessionId, memberId, time, QaSeatingPhase.Approach, -1, 500d, false));
            for (var frame = 0; frame < 4; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(sessionId, memberId, time, QaSeatingPhase.SitDown, frame, 500.5d, true));
            }
            for (var frame = 0; frame < 6; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(sessionId, memberId, time, QaSeatingPhase.Work, frame, 500.5d, true));
            }
            for (var frame = 0; frame < 4; frame++)
            {
                time += 0.1d;
                run.SeatingFrames.Add(Seat(sessionId, memberId, time, QaSeatingPhase.StandUp, frame, 500.5d, true));
            }
            time += 0.1d;
            run.SeatingFrames.Add(Seat(sessionId, memberId, time, QaSeatingPhase.Complete, -1, 500d, false));
        }

        private static SeatingFrameObservation Seat(
            string sessionId,
            string memberId,
            double time,
            QaSeatingPhase phase,
            int frame,
            double x,
            bool measureOcclusion)
        {
            return new SeatingFrameObservation(
                sessionId,
                memberId,
                time,
                phase,
                frame,
                new QaPoint2(x, 700d),
                measureOcclusion,
                true,
                true,
                0,
                0);
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
            {
                run.WorkActions.Add(new WorkActionObservation(
                    memberId,
                    action,
                    workSeconds,
                    workSeconds,
                    cooldown,
                    QaMotionPhase.Work,
                    true));
            }
        }

        private static void InjectFailure(NaturalBehaviorQaRun run, int gateIndex)
        {
            switch (gateIndex)
            {
                case 0:
                    run.Footpoints.Add(new FootpointSample(
                        NaturalBehaviorQaScenarioIds.SemanticRoundTrip,
                        0,
                        "player",
                        9d,
                        new QaPoint2(100d, 100d),
                        0.1d,
                        true));
                    break;
                case 1:
                    run.Paths.Add(new PathObservation(
                        NaturalBehaviorQaScenarioIds.SemanticRoundTrip,
                        0,
                        99,
                        "player",
                        run.Plan.SemanticOriginId,
                        run.Plan.SemanticDestinationIds[0],
                        false,
                        10d,
                        10d,
                        0,
                        0d,
                        "failed-path",
                        0));
                    break;
                case 2:
                    run.MotionSamples.Add(new MotionSample(
                        NaturalBehaviorQaScenarioIds.SemanticRoundTrip,
                        0,
                        "player",
                        0.12d,
                        new QaPoint2(5d, 0d),
                        2,
                        QaMotionPhase.Walking,
                        true));
                    break;
                case 3:
                    run.SeatingFrames.Add(new SeatingFrameObservation(
                        "incomplete-seat",
                        "player",
                        0d,
                        QaSeatingPhase.Approach,
                        -1,
                        new QaPoint2(1d, 1d),
                        false,
                        false,
                        false,
                        0,
                        0));
                    break;
                case 4:
                    run.WorkActions.Add(new WorkActionObservation(
                        "player",
                        QaWorkVisualAction.Typing,
                        100d,
                        100d,
                        1d,
                        QaMotionPhase.SittingDown,
                        true));
                    break;
                case 5:
                    var seed = run.Plan.RandomLayoutSeeds[0];
                    run.NavigationRebuilds.Add(new NavigationRebuildObservation(
                        NaturalBehaviorQaScenarioIds.RandomFurniture,
                        seed,
                        0d,
                        13d,
                        1,
                        0,
                        1,
                        1d));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gateIndex));
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

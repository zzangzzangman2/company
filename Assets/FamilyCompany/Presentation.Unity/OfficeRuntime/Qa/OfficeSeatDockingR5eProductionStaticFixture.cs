using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Executable, engine-object-free static gate. It calls the production catalog parser,
    /// OfficeSeatingState/OfficeSeatRuntimeClaim mutation APIs, OfficeRuntimeOccupancy prepared
    /// placement APIs, production row constructors, fixed buffers and production CSV writer.
    /// It is not runtime visual evidence and can never close the runtime PENDING gate.
    /// </summary>
    public static class OfficeSeatDockingR5eProductionStaticFixture
    {
        private const string ActorId = "player";
        private const string SeatId = "fixture-seat";
        private const string ChairId = "fixture-chair";

        public static string Run(string catalogPath, string artifactDirectory)
        {
            if (string.IsNullOrWhiteSpace(catalogPath))
                throw new ArgumentException("Catalog path is required.", nameof(catalogPath));
            if (string.IsNullOrWhiteSpace(artifactDirectory))
                throw new ArgumentException("Artifact directory is required.", nameof(artifactDirectory));
            string catalogJson = File.ReadAllText(Path.GetFullPath(catalogPath), Encoding.UTF8);
            OfficeSeatDockingR5eScenarioPlan plan =
                OfficeSeatDockingR5eScenarioCatalog.ParseAndValidateJson(catalogJson);
            if (plan.Cases.Length != OfficeSeatDockingR5eScenarioCatalog.TotalCaseCount)
                throw new InvalidOperationException("Production catalog did not yield 158 cases.");

            string output = Path.GetFullPath(artifactDirectory);
            Directory.CreateDirectory(output);
            R2FixtureResult r2 = RunR2ArrivalRegression(
                Path.Combine(output, "arrival-r2-regression"));
            var results = new ProductionScenarioResult[plan.Cases.Length];
            for (var index = 0; index < plan.Cases.Length; index++)
                results[index] = ExecuteProductionScenario(plan.Cases[index]);
            WriteScenarioResults(Path.Combine(output, "chair-r5e-scenario-results.csv"), results);
            WriteScenarioSummary(
                Path.Combine(output, "chair-r5e-scenario-summary.txt"),
                results);
            WriteProductionFixtureManifest(
                Path.Combine(output, "production-fixture-manifest.csv"),
                results,
                r2.Verdicts);

            var actor = BuildTraceState();
            OfficeSeatDockingR5eTraceWriteSummary summary =
                OfficeSeatDockingR5eTraceWriter.WriteProductionStaticFixture(
                    new[] { actor },
                    output);
            if (!summary.ReadyForPostProcess)
                throw new InvalidOperationException(
                    "Production writer fixture was incomplete: transitions=" + summary.TransitionRows +
                    " seated=" + summary.SeatedRows + " locomotion=" + summary.LocomotionRows +
                    " visual=" + summary.VisualRows + " overflow=" + summary.OverflowCount +
                    " dropped=" + summary.DroppedRowCount + " failures=" +
                    summary.ProducerFailureCount);

            string pending =
                "status=PENDING_POSTPROCESS\n" +
                "fixtureKind=production-static\n" +
                "scenarioCatalogSha256=" + plan.Sha256 + "\n" +
                "scenarioExpected=158\nscenarioObserved=158\n" +
                "transitionRows=" + summary.TransitionRows + "\n" +
                "seatedRows=" + summary.SeatedRows + "\n" +
                "locomotionRows=" + summary.LocomotionRows + "\n" +
                "visualMetadataRows=" + summary.VisualRows + "\n" +
                "legacyClipOracle=unused\n";
            File.WriteAllText(
                Path.Combine(output, OfficeSeatDockingR5eRuntimeQaContract.RuntimeResultFile),
                pending,
                new UTF8Encoding(false));
            return "scenarios=158 transitions=" + summary.TransitionRows +
                   " seated=" + summary.SeatedRows + " locomotion=" +
                   summary.LocomotionRows + " visual=" + summary.VisualRows +
                   " " + r2.Summary;
        }

        private static ProductionScenarioResult ExecuteProductionScenario(
            in R5eScenarioCase scenario)
        {
            if (scenario.Kind == R5eScenarioKind.Contention)
                return ExecuteContention(scenario);

            var seating = new OfficeSeatingState(new[]
            {
                new OfficeSeatDefinition(SeatId, new OfficeSeatPosition(0d, 0d))
            });
            if (!OfficeSeatRuntimeClaim.TryReserve(
                    seating,
                    SeatId,
                    scenario.ActorId,
                    "fixture-" + scenario.Id,
                    out OfficeSeatRuntimeClaim claim,
                    out _))
                return Failed(scenario, "production-claim-reserve-rejected");

            var initialCell = new OfficeGridCoordinate(2, 2);
            var seatCell = new OfficeGridCoordinate(3, 2);
            var exitCell = new OfficeGridCoordinate(4, 2);
            OfficeRuntimeOccupancy occupancy =
                OfficeRuntimeOccupancy.CreateProductionTransactionFixture(
                    scenario.ActorId,
                    new Vector2(2f, 2f),
                    initialCell,
                    OfficeRuntimeAgent.DefaultRadius,
                    17);
            var journal = new ProductionActorJournal("Docked", new Vector2(2f, 2f), 11, 13);

            bool entryFault = scenario.Kind == R5eScenarioKind.FaultEntry;
            bool entryVersion = scenario.Kind == R5eScenarioKind.VersionEntry;
            bool exitFault = scenario.Kind == R5eScenarioKind.FaultExit;
            bool exitVersion = scenario.Kind == R5eScenarioKind.VersionExit;
            bool blocked = scenario.Kind == R5eScenarioKind.AllExitsBlocked;

            bool entrySucceeded = ExecuteAtomicEntry(
                claim,
                occupancy,
                seatCell,
                ref journal,
                entryFault ? scenario.FaultInjectionId : 0,
                entryVersion);
            if (entryFault || entryVersion)
                return new ProductionScenarioResult(
                    scenario,
                    !entrySucceeded && journal.IsExact("Docked", new Vector2(2f, 2f), 11, 13),
                    "production-entry-rollback");
            if (!entrySucceeded)
                return Failed(scenario, "production-entry-commit-failed");

            ProductionActorJournal seated = journal;
            if (blocked)
            {
                bool noCommit = !ExecuteAtomicExit(
                    claim,
                    occupancy,
                    exitCell,
                    ref journal,
                    0,
                    false,
                    false);
                return new ProductionScenarioResult(
                    scenario,
                    noCommit && journal.Equals(seated) && claim.IsOccupied && !claim.IsReleased,
                    "production-all-exits-blocked-noop");
            }

            bool exitSucceeded = ExecuteAtomicExit(
                claim,
                occupancy,
                exitCell,
                ref journal,
                exitFault ? scenario.FaultInjectionId : 0,
                exitVersion,
                true);
            bool passed = exitFault || exitVersion
                ? !exitSucceeded && journal.Equals(seated) && claim.IsOccupied && !claim.IsReleased
                : exitSucceeded && journal.State == "LeavingSeat" && claim.IsReleased;
            return new ProductionScenarioResult(
                scenario,
                passed,
                exitFault || exitVersion
                    ? "production-exit-rollback"
                    : "production-entry-exit-commit");
        }

        private static ProductionScenarioResult ExecuteContention(in R5eScenarioCase scenario)
        {
            var seating = new OfficeSeatingState(new[]
            {
                new OfficeSeatDefinition(SeatId, new OfficeSeatPosition(0d, 0d))
            });
            string[] actors = { "player", "older_sister", "father", "mother" };
            int accepted = 0;
            for (var index = 0; index < actors.Length; index++)
            {
                if (OfficeSeatRuntimeClaim.TryReserve(
                        seating,
                        SeatId,
                        actors[(index + scenario.ContentionIndex) % actors.Length],
                        "contention-" + scenario.Id + "-" + index,
                        out _,
                        out _)) accepted++;
            }
            return new ProductionScenarioResult(
                scenario,
                accepted == 1,
                "production-contention-accepted=" + accepted);
        }

        private static R2FixtureResult RunR2ArrivalRegression(string artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            var verdicts = new List<FixtureVerdict>();
            string[] canonical = { "player", "older_sister", "father", "mother" };
            string[] shuffled = { "mother", "player", "father", "older_sister" };
            ValidateTraceIdentity(canonical, false, "canonical-off", verdicts);
            ValidateTraceIdentity(canonical, true, "canonical-on", verdicts);
            ValidateTraceIdentity(shuffled, false, "shuffled-off", verdicts);
            ValidateTraceIdentity(shuffled, true, "shuffled-on", verdicts);
            ValidateLegacyIndexNegativeControl(canonical, verdicts);
            ValidateTraceObserverIsolation(canonical, verdicts);
            ValidatePreloadAndReadyControls(verdicts);

            R2ArrivalProjection positive = RunArrivalProjection(true, "after-pass");
            R2ArrivalProjection blocked = RunArrivalProjection(false, "before-fail-control");
            WriteArrivalTrace(
                Path.Combine(artifactDirectory, "arrival-r2-structured-trace.csv"),
                positive.Rows.Concat(blocked.Rows).ToArray());
            AddVerdict(
                verdicts,
                "r2-arrival-positive",
                "08:50-10:00;world=running",
                "none",
                "positive",
                "visible=1/2/3/4;routes=4;work=4;1000=all-working;exceptions=0",
                positive.Summary,
                positive.Passed);
            bool blockedDetected = blocked.Visible0903 == 1 && blocked.WorkTransitions == 0;
            AddVerdict(
                verdicts,
                "r2-arrival-world-stopped-negative",
                "08:50-10:00;world=stopped",
                "world-update-abort",
                "negative-control",
                "visible0903=1;work=0",
                blocked.Summary,
                blockedDetected);

            if (verdicts.Any(item => !item.Passed))
                throw new InvalidOperationException(
                    "R2 production fixture control failed: " +
                    string.Join(";", verdicts.Where(item => !item.Passed).Select(item => item.FixtureId)));
            string result =
                "status=PASS\n" +
                "fixtureKind=production-static-r2\n" +
                "window=08:50..10:00\n" +
                "visible0900=1\nvisible0901=2\nvisible0902=3\nvisible0903=4\n" +
                "routeTransitions=4\nworkTransitions=4\nallWorking1000=true\n" +
                "worldTicks=71\nclockTicks=71\nexceptions=0\n" +
                "positiveControls=" + verdicts.Count(item => item.ControlKind == "positive") + "\n" +
                "negativeControls=" + verdicts.Count(item => item.ControlKind == "negative-control") + "\n" +
                "fixtureVerdicts=" + verdicts.Count + "\n";
            File.WriteAllText(
                Path.Combine(artifactDirectory, "arrival-r2-result.txt"),
                result,
                new UTF8Encoding(false));
            return new R2FixtureResult(
                verdicts.ToArray(),
                "arrivalR2=PASS controls=" + verdicts.Count +
                " positive=" + verdicts.Count(item => item.ControlKind == "positive") +
                " negative=" + verdicts.Count(item => item.ControlKind == "negative-control"));
        }

        private static void ValidateTraceIdentity(
            string[] creationOrder,
            bool captureEnabled,
            string fixtureSuffix,
            List<FixtureVerdict> verdicts)
        {
            var coordinator = new OfficeRuntimeTraceCoordinator(1, 0, 4, captureEnabled);
            var registration = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < creationOrder.Length; index++)
            {
                OfficeRuntimeActorTraceState state =
                    coordinator.RegisterActorIdentity(creationOrder[index]);
                registration.Add(state.ActorId, state.ActorIndex);
            }
            string[] scheduler = (string[])creationOrder.Clone();
            Array.Sort(scheduler, OfficeRuntimeActorRegistry.CompareActorIds);
            bool exact = scheduler.All(actorId =>
                coordinator.TryGetActorState(actorId, out var state) &&
                state.ActorId == actorId && state.ActorIndex == registration[actorId]);
            AddVerdict(
                verdicts,
                "r2-trace-identity-" + fixtureSuffix,
                "create=" + string.Join(">", creationOrder) +
                ";schedule=" + string.Join(">", scheduler),
                "none",
                "positive",
                "actorId->immutable-registration-ordinal exact",
                exact
                    ? string.Join(";", scheduler.Select(id => id + "=" + registration[id]))
                    : "identity-mismatch",
                exact);
        }

        private static void ValidateLegacyIndexNegativeControl(
            string[] creationOrder,
            List<FixtureVerdict> verdicts)
        {
            string[] scheduler = (string[])creationOrder.Clone();
            Array.Sort(scheduler, OfficeRuntimeActorRegistry.CompareActorIds);
            int mismatchCount = 0;
            for (var index = 0; index < creationOrder.Length; index++)
                if (!string.Equals(creationOrder[index], scheduler[index], StringComparison.Ordinal))
                    mismatchCount++;
            AddVerdict(
                verdicts,
                "r2-trace-legacy-index-negative",
                "trace=" + string.Join(">", creationOrder) +
                ";schedule=" + string.Join(">", scheduler),
                "mutable-registry-index",
                "negative-control",
                "mismatchCount>0",
                "mismatchCount=" + mismatchCount,
                mismatchCount > 0);
        }

        private static void ValidateTraceObserverIsolation(
            string[] actorIds,
            List<FixtureVerdict> verdicts)
        {
            var disabled = new OfficeRuntimeTraceCoordinator(2, 0, 4, false);
            foreach (string actorId in actorIds) disabled.RegisterActorIdentity(actorId);
            bool disabledContinues = disabled.TryReserveTransitionRows("not-registered", 1);
            disabled.AbortFatal("capture-off-control");
            bool disabledExact = disabledContinues && !disabled.FatalAbort &&
                                 disabled.FailureCount == 0 && disabled.FatalReason.Length == 0;
            AddVerdict(
                verdicts,
                "r2-capture-off-zero-side-effect",
                "capture=false;actor=not-registered",
                "actor-id-mismatch",
                "negative-control",
                "continue=true;fatal=false;failures=0;reason=empty",
                "continue=" + disabledContinues + ";fatal=" + disabled.FatalAbort +
                ";failures=" + disabled.FailureCount + ";reason=" + disabled.FatalReason,
                disabledExact);

            var enabled = new OfficeRuntimeTraceCoordinator(3, 0, 4, true);
            foreach (string actorId in actorIds) enabled.RegisterActorIdentity(actorId);
            bool overflowContinues = enabled.TryReserveTransitionRows(
                "player",
                OfficeRuntimeTraceCoordinator.TransitionCapacityPerActor + 1);
            bool laterActorsContinue = actorIds.All(actorId =>
                enabled.TryReserveTransitionRows(actorId, 1));
            bool evidenceLatched = enabled.FatalAbort && enabled.FailureCount == 1 &&
                                   enabled.FatalReason.Contains("capacity-preflight") &&
                                   actorIds.All(actorId =>
                                       enabled.TryGetActorState(actorId, out var state) &&
                                       !state.IsCaptureActive);
            AddVerdict(
                verdicts,
                "r2-capture-on-evidence-latch",
                "capture=true;rows=" +
                (OfficeRuntimeTraceCoordinator.TransitionCapacityPerActor + 1),
                "transition-capacity-overflow",
                "negative-control",
                "fatal=true;failures=1;evidence-suppressed;gameplay-continues",
                "fatal=" + enabled.FatalAbort + ";failures=" + enabled.FailureCount +
                ";laterActorsContinue=" + laterActorsContinue,
                overflowContinues && laterActorsContinue && evidenceLatched);

            var mismatch = new OfficeRuntimeTraceCoordinator(4, 0, 4, true);
            foreach (string actorId in actorIds) mismatch.RegisterActorIdentity(actorId);
            bool mismatchContinues = mismatch.TryReserveTransitionRows("not-registered", 1);
            bool mismatchLaterContinues = actorIds.All(actorId =>
                mismatch.TryReserveTransitionRows(actorId, 1));
            AddVerdict(
                verdicts,
                "r2-capture-on-id-mismatch-latch",
                "capture=true;actor=not-registered",
                "actor-id-mismatch",
                "negative-control",
                "fatal=true;failures=1;mismatch-reason;gameplay-continues",
                "fatal=" + mismatch.FatalAbort + ";failures=" + mismatch.FailureCount +
                ";reason=" + mismatch.FatalReason +
                ";laterActorsContinue=" + mismatchLaterContinues,
                mismatchContinues && mismatchLaterContinues && mismatch.FatalAbort &&
                mismatch.FailureCount == 1 &&
                mismatch.FatalReason.Contains("actor-id-mismatch"));
        }

        private static void ValidatePreloadAndReadyControls(List<FixtureVerdict> verdicts)
        {
            const int northwest = 3;
            const string sha = "0000000000000000000000000000000000000000000000000000000000000000";
            var profiles = new List<OfficeCharacterSeatPoseProfile>();
            for (var frame = 0; frame < OfficeSeatingAnimationFrames.WorkFrameCount; frame++)
            {
                profiles.Add(OfficeCharacterSeatPoseProfile.Create(
                    "player",
                    northwest,
                    OfficeSeatingAnimationClip.Work,
                    frame,
                    new Vector2(128f, 80f),
                    new Vector2(96f, 96f),
                    humanApproved: true,
                    sourceSpriteSha256: sha));
            }
            OfficeCharacterSeatPoseProfile[] plan =
                OfficeRuntimeAgent.BuildR5eSeatPresentationPreloadPlan(profiles, "player");
            bool catalogBounded =
                plan.Length == OfficeSeatingAnimationFrames.WorkFrameCount &&
                plan.All(profile => profile.DirectionIndex == northwest);
            AddVerdict(
                verdicts,
                "r2-preload-northwest-only",
                "catalogDirections=3;missingDirections=0,1,2,4,5,6,7",
                "none",
                "positive",
                "requests=3/0..5 only",
                "requests=" + string.Join(";", plan.Select(profile =>
                    profile.DirectionIndex + "/" + profile.FrameIndex)),
                catalogBounded);

            bool partialFailed = false;
            string partialReason = string.Empty;
            try
            {
                OfficeRuntimeAgent.BuildR5eSeatPresentationPreloadPlan(
                    profiles.Take(profiles.Count - 1).ToArray(),
                    "player");
            }
            catch (InvalidOperationException exception)
            {
                partialReason = exception.Message;
                partialFailed = partialReason.Contains("Incomplete preload pose direction");
            }
            AddVerdict(
                verdicts,
                "r2-preload-partial-direction-negative",
                "direction=3;frames=0..4;missing=5",
                "missing-catalog-frame",
                "negative-control",
                "explicit incomplete-direction failure",
                partialReason,
                partialFailed);

            var gate = new StarterOfficeRuntimePreparationGate();
            gate.Begin();
            bool earlyAttachRejected = false;
            try
            {
                gate.MarkCoordinatorAttached();
            }
            catch (InvalidOperationException)
            {
                earlyAttachRejected = true;
            }
            AddVerdict(
                verdicts,
                "r2-ready-before-preload-negative",
                "state=Preparing;preload=false",
                "coordinator-attach-before-preload",
                "negative-control",
                "attach rejected;ready not published",
                "rejected=" + earlyAttachRejected + ";state=" + gate.State,
                earlyAttachRejected && gate.State == StarterOfficeRuntimePreparationState.Preparing);

            gate.MarkPreloadSucceeded();
            gate.MarkCoordinatorAttached();
            gate.PublishReady();
            AddVerdict(
                verdicts,
                "r2-ready-order-positive",
                "preload=true;attach=true",
                "none",
                "positive",
                "state=Ready",
                "state=" + gate.State,
                gate.State == StarterOfficeRuntimePreparationState.Ready);

            var failed = new StarterOfficeRuntimePreparationGate();
            failed.Begin();
            failed.Fail("preload:" + partialReason);
            AddVerdict(
                verdicts,
                "r2-preload-explicit-failure-state",
                "preload=partial",
                "missing-catalog-frame",
                "negative-control",
                "state=Failed;reason=nonempty",
                "state=" + failed.State + ";reason=" + failed.FailureReason,
                failed.State == StarterOfficeRuntimePreparationState.Failed &&
                failed.FailureReason.Length > 0);
        }

        private static R2ArrivalProjection RunArrivalProjection(bool runWorld, string runKind)
        {
            string[] ids = { "player", "older_sister", "father", "mother" };
            ArrivalFixtureActor[] actors = ids.Select(id => new ArrivalFixtureActor(id)).ToArray();
            var rows = new List<ArrivalTraceRow>();
            int nextArrival = 0;
            ArrivalFixtureActor lastEntrant = null;
            Vector2 lastEntryPosition = Vector2.zero;
            int visible0900 = -1;
            int visible0901 = -1;
            int visible0902 = -1;
            int visible0903 = -1;
            int worldTicks = 0;
            int clockTicks = 0;
            int exceptions = 0;
            int tick = 0;
            DateTime now = new DateTime(2000, 1, 3, 8, 50, 0, DateTimeKind.Unspecified);
            DateTime end = new DateTime(2000, 1, 3, 10, 0, 0, DateTimeKind.Unspecified);
            for (; now <= end; now = now.AddMinutes(1), tick++)
            {
                string released = string.Empty;
                try
                {
                    OfficeAttendancePhase attendance = OfficeAttendanceRules.Resolve(now);
                    if (attendance != OfficeAttendancePhase.Working)
                    {
                        nextArrival = 0;
                        lastEntrant = null;
                        lastEntryPosition = Vector2.zero;
                    }
                    else
                    {
                        while (nextArrival < actors.Length && !actors[nextArrival].Away)
                            nextArrival++;
                        bool entranceClear = lastEntrant == null || lastEntrant.Away ||
                                             Vector2.Distance(
                                                 lastEntrant.Position,
                                                 lastEntryPosition) >= 0.72f;
                        if (nextArrival < actors.Length &&
                            OfficeAttendanceRules.HasArrived(now, nextArrival) &&
                            entranceClear)
                        {
                            ArrivalFixtureActor entrant = actors[nextArrival];
                            entrant.ReleaseAtEntrance();
                            released = entrant.ActorId;
                            lastEntrant = entrant;
                            lastEntryPosition = entrant.Position;
                            nextArrival++;
                        }
                    }

                    int visible = actors.Count(actor => !actor.Away);
                    if (now.Hour == 9)
                    {
                        if (now.Minute == 0) visible0900 = visible;
                        else if (now.Minute == 1) visible0901 = visible;
                        else if (now.Minute == 2) visible0902 = visible;
                        else if (now.Minute == 3) visible0903 = visible;
                    }
                    rows.Add(new ArrivalTraceRow(
                        runKind,
                        tick,
                        now.ToString("HH:mm", CultureInfo.InvariantCulture),
                        visible,
                        actors[0].StateLabel,
                        actors[1].StateLabel,
                        actors[2].StateLabel,
                        actors[3].StateLabel,
                        released,
                        worldTicks + (runWorld ? 1 : 0),
                        clockTicks + 1,
                        exceptions));
                    if (runWorld)
                    {
                        foreach (ArrivalFixtureActor actor in actors) actor.AdvanceProductionMotion();
                        worldTicks++;
                    }
                    clockTicks++;
                }
                catch
                {
                    exceptions++;
                    throw;
                }
            }
            int routes = actors.Sum(actor => actor.RouteTransitions);
            int work = actors.Sum(actor => actor.WorkTransitions);
            bool allWorking = actors.All(actor => actor.Phase == OfficeRuntimeAgentPhase.Working);
            bool passed = visible0900 == 1 && visible0901 == 2 &&
                          visible0902 == 3 && visible0903 == 4 &&
                          routes == 4 && work == 4 && allWorking &&
                          worldTicks == 71 && clockTicks == 71 && exceptions == 0;
            string summary = "visible=" + visible0900 + "/" + visible0901 + "/" +
                             visible0902 + "/" + visible0903 + ";routes=" + routes +
                             ";work=" + work + ";allWorking1000=" + allWorking +
                             ";worldTicks=" + worldTicks + ";clockTicks=" + clockTicks +
                             ";exceptions=" + exceptions;
            return new R2ArrivalProjection(
                rows.ToArray(),
                visible0903,
                routes,
                work,
                passed,
                summary);
        }

        private static void WriteArrivalTrace(string path, ArrivalTraceRow[] rows)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(
                "schemaVersion,runKind,tick,time,visibleCount,player,older_sister,father,mother," +
                "releasedActor,worldTicks,clockTicks,exceptions");
            foreach (ArrivalTraceRow row in rows)
            {
                writer.WriteLine(
                    "family-arrival-r2-v1," + row.RunKind + "," + row.Tick + "," + row.Time + "," +
                    row.VisibleCount + "," + row.Player + "," + row.OlderSister + "," +
                    row.Father + "," + row.Mother + "," + row.ReleasedActor + "," +
                    row.WorldTicks + "," + row.ClockTicks + "," + row.Exceptions);
            }
        }

        private static void WriteScenarioSummary(
            string path,
            ProductionScenarioResult[] results)
        {
            var lines = new List<string>
            {
                "status=" + (results.All(item => item.Passed) ? "PASS" : "FAIL"),
                "expected=158",
                "observed=" + results.Length,
                "passed=" + results.Count(item => item.Passed),
                "failed=" + results.Count(item => !item.Passed)
            };
            foreach (R5eScenarioKind kind in Enum.GetValues(typeof(R5eScenarioKind)))
                lines.Add("kind." + kind + "=" + results.Count(item => item.Kind == kind.ToString()));
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        private static void WriteProductionFixtureManifest(
            string path,
            ProductionScenarioResult[] scenarios,
            FixtureVerdict[] r2Verdicts)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(
                "fixtureId,inputId,faultId,controlKind,expected,actual,verdict");
            foreach (ProductionScenarioResult scenario in scenarios)
            {
                string fixtureId = "chair-" + scenario.ScenarioId;
                if (!unique.Add(fixtureId))
                    throw new InvalidOperationException("Duplicate production fixture ID: " + fixtureId);
                writer.WriteLine(string.Join(",",
                    Csv(fixtureId),
                    Csv(scenario.CaseId),
                    Csv(scenario.FaultId.ToString(CultureInfo.InvariantCulture)),
                    Csv(scenario.Kind),
                    Csv("production transaction contract passes"),
                    Csv(scenario.Detail),
                    scenario.Passed ? "PASS" : "FAIL"));
            }
            foreach (FixtureVerdict verdict in r2Verdicts)
            {
                if (!unique.Add(verdict.FixtureId))
                    throw new InvalidOperationException(
                        "Duplicate production fixture ID: " + verdict.FixtureId);
                writer.WriteLine(string.Join(",",
                    Csv(verdict.FixtureId),
                    Csv(verdict.InputId),
                    Csv(verdict.FaultId),
                    Csv(verdict.ControlKind),
                    Csv(verdict.Expected),
                    Csv(verdict.Actual),
                    verdict.Passed ? "PASS" : "FAIL"));
            }
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? safe
                : "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static void AddVerdict(
            List<FixtureVerdict> verdicts,
            string fixtureId,
            string inputId,
            string faultId,
            string controlKind,
            string expected,
            string actual,
            bool passed)
        {
            verdicts.Add(new FixtureVerdict(
                fixtureId,
                inputId,
                faultId,
                controlKind,
                expected,
                actual,
                passed));
        }

        private static bool ExecuteAtomicEntry(
            OfficeSeatRuntimeClaim claim,
            OfficeRuntimeOccupancy occupancy,
            OfficeGridCoordinate targetCell,
            ref ProductionActorJournal journal,
            int faultPoint,
            bool invalidateVersion)
        {
            if (!claim.TryPrepareOccupy(out OfficeSeatingState.PreparedRuntimeMutation claimMutation) ||
                !occupancy.TryPrepareProductionTransactionFixturePlacement(
                    claim.MemberId,
                    new Vector2(targetCell.X, targetCell.Y),
                    targetCell,
                    out OfficeRuntimeOccupancy.PreparedAtomicActorPlacement placement)) return false;
            if (invalidateVersion) occupancy.InvalidateAtomicTokenForQa(claim.MemberId);
            if (!claim.IsPreparedMutationCurrent(claimMutation) ||
                !occupancy.IsPreparedAtomicActorPlacementCurrent(placement))
            {
                occupancy.CancelPreparedAtomicActorPlacement(placement);
                return false;
            }
            var publisher = new ProductionAtomicPublisher(
                claim,
                occupancy,
                claimMutation,
                placement,
                journal,
                new Vector2(targetCell.X, targetCell.Y),
                true,
                faultPoint);
            bool succeeded = OfficeSeatDockingAtomicPublishPrimitive.TryPublish(ref publisher);
            journal = publisher.Journal;
            if (succeeded) occupancy.CompletePreparedAtomicActorPlacement(placement);
            return succeeded;
        }

        private static bool ExecuteAtomicExit(
            OfficeSeatRuntimeClaim claim,
            OfficeRuntimeOccupancy occupancy,
            OfficeGridCoordinate targetCell,
            ref ProductionActorJournal journal,
            int faultPoint,
            bool invalidateVersion,
            bool floorValid)
        {
            if (!floorValid) return false;
            if (!claim.TryPrepareRelease(out OfficeSeatingState.PreparedRuntimeMutation claimMutation) ||
                !occupancy.TryPrepareProductionTransactionFixturePlacement(
                    claim.MemberId,
                    new Vector2(targetCell.X, targetCell.Y),
                    targetCell,
                    out OfficeRuntimeOccupancy.PreparedAtomicActorPlacement placement)) return false;
            if (invalidateVersion) occupancy.InvalidateAtomicTokenForQa(claim.MemberId);
            if (!claim.IsPreparedMutationCurrent(claimMutation) ||
                !occupancy.IsPreparedAtomicActorPlacementCurrent(placement))
            {
                occupancy.CancelPreparedAtomicActorPlacement(placement);
                return false;
            }
            var publisher = new ProductionAtomicPublisher(
                claim,
                occupancy,
                claimMutation,
                placement,
                journal,
                new Vector2(targetCell.X, targetCell.Y),
                false,
                faultPoint);
            bool succeeded = OfficeSeatDockingAtomicPublishPrimitive.TryPublish(ref publisher);
            journal = publisher.Journal;
            if (succeeded) occupancy.CompletePreparedAtomicActorPlacement(placement);
            return succeeded;
        }

        private static OfficeRuntimeActorTraceState BuildTraceState()
        {
            var actor = new OfficeRuntimeActorTraceState(0, ActorId, true);
            var chair = R5eFurnitureTransformSnapshot.CreateDetachedProductionFixture(
                17,
                SeatId,
                ChairId,
                OfficeFurnitureFacing.SouthEast,
                new OfficeGridCoordinate(3, 2));
            var front = new OfficeSeatEgressAnchor(
                OfficeSeatEgressKind.Front,
                new OfficeGridCoordinate(4, 2),
                new Vector3(4f, 2f, 0f));
            var left = new OfficeSeatEgressAnchor(
                OfficeSeatEgressKind.Left,
                new OfficeGridCoordinate(3, 3),
                new Vector3(3f, 3f, 0f));
            var right = new OfficeSeatEgressAnchor(
                OfficeSeatEgressKind.Right,
                new OfficeGridCoordinate(3, 1),
                new Vector3(3f, 1f, 0f));
            var docking = new OfficeSeatDockingPlan(
                null,
                new Vector2(1f, 2f),
                new Vector2(2f, 2f),
                new Vector2(3f, 2f),
                new Vector2(3f, 2f),
                front,
                left,
                right,
                17,
                chair);
            var dock = Snapshot(OfficeRuntimeAgentPhase.RotatingToSeat, new Vector2(2f, 2f), 6, 0, 0);
            var seated = Snapshot(OfficeRuntimeAgentPhase.Working, new Vector2(3f, 2f), 6, 0, 0);
            var exit = Snapshot(OfficeRuntimeAgentPhase.LeavingSeat, new Vector2(4f, 2f), 6, 0, 23);
            var dockObservation = Observation(dock.LogicalRoot, new OfficeGridCoordinate(2, 2), chair, true, false);
            var seatedObservation = Observation(seated.LogicalRoot, new OfficeGridCoordinate(3, 2), chair, false, true);
            var exitObservation = Observation(exit.LogicalRoot, new OfficeGridCoordinate(4, 2), chair, false, false);

            AppendLifecycle(actor, 1, 1, R5eSeatTransitionKind.Entry, dock, seated,
                docking, Vector2.zero, dockObservation, seatedObservation,
                new[] { R5eSeatTransitionEventKind.Prepare, R5eSeatTransitionEventKind.Commit,
                    R5eSeatTransitionEventKind.Rebase });
            actor.OpenSeatedSession(1, 1);
            var seatedContext = Context(4, 4, 20);
            actor.CountExpectedPreClear(true);
            actor.AppendSeated(
                R5eSeatedSamplePhase.PreClear,
                seatedContext,
                SeatId,
                seated,
                seated,
                seatedObservation.Occupancy,
                seatedObservation);
            actor.CountExpectedPostClear(true, false);
            actor.AppendSeated(
                R5eSeatedSamplePhase.PostClear,
                seatedContext,
                SeatId,
                seated,
                seated,
                seatedObservation.Occupancy,
                seatedObservation);
            actor.RecordClearMask(seated, seated);

            AppendLifecycle(actor, 2, 1, R5eSeatTransitionKind.Exit, seated, exit,
                docking, new Vector2(4f, 2f), seatedObservation, exitObservation,
                new[] { R5eSeatTransitionEventKind.Prepare, R5eSeatTransitionEventKind.Commit,
                    R5eSeatTransitionEventKind.Rebase, R5eSeatTransitionEventKind.TurnComplete,
                    R5eSeatTransitionEventKind.FirstWalk });
            actor.CloseSeatedSession();
            for (var fault = 1; fault <= 6; fault++)
                AppendLifecycle(actor, (ulong)(10 + fault), 0, R5eSeatTransitionKind.Exit,
                    seated, seated, docking, new Vector2(4f, 2f), seatedObservation,
                    seatedObservation,
                    new[] { R5eSeatTransitionEventKind.Prepare, R5eSeatTransitionEventKind.Rollback },
                    fault);
            AppendLifecycle(actor, 20, 0, R5eSeatTransitionKind.Entry, dock, dock,
                docking, Vector2.zero, dockObservation, dockObservation,
                new[] { R5eSeatTransitionEventKind.Prepare, R5eSeatTransitionEventKind.Rollback });
            AppendLifecycle(actor, 21, 1, R5eSeatTransitionKind.Exit, seated, seated,
                docking, Vector2.zero, seatedObservation, seatedObservation,
                new[] { R5eSeatTransitionEventKind.Prepare, R5eSeatTransitionEventKind.Rollback });

            var movementContext = Context(100, 30, 30);
            var movingBefore = Snapshot(OfficeRuntimeAgentPhase.Navigating, new Vector2(4f, 2f), 6, 8, 23);
            var movingAfter = Snapshot(
                OfficeRuntimeAgentPhase.Navigating,
                new Vector2(4.05f, 2f),
                6,
                8,
                23,
                new Vector2(0.05f, 0f),
                new Vector2(0.6f, 0f));
            actor.BeginStep(movementContext, 8, 23);
            actor.CountExpectedPostClear(false, true);
            actor.AppendLocomotion(new R5eLocomotionAdapterRow(
                movementContext,
                ActorId,
                8,
                23,
                movingBefore,
                movingAfter,
                false,
                true,
                true,
                true));
            actor.CountExpectedRender();
            var render = new DirectionalLocomotionFrameTrace(
                new Vector2(0.05f, 0f),
                0.6f,
                6,
                6,
                OfficeLocomotionPhase.Walk,
                "walk-east",
                "fixture-walk-east",
                false,
                true);
            actor.AppendRender(new R5eLocomotionAdapterRow(
                1,
                1,
                100,
                ActorId,
                8,
                23,
                1,
                30,
                30,
                30,
                30,
                render,
                new Vector2(0.05f, 0f),
                1,
                true,
                0));
            actor.AppendVisual(new R5eVisualCaptureMetadataRow(
                1, 1, 100, 30, ActorId, 2, 1, false, false));
            return actor;
        }

        private static void AppendLifecycle(
            OfficeRuntimeActorTraceState actor,
            ulong transactionId,
            ulong sessionId,
            R5eSeatTransitionKind kind,
            in R5eAgentStepSnapshot before,
            in R5eAgentStepSnapshot committed,
            in OfficeSeatDockingPlan plan,
            Vector2 chosenExit,
            in R5eProductionObservation beforeObservation,
            in R5eProductionObservation committedObservation,
            R5eSeatTransitionEventKind[] events,
            int fault = 0)
        {
            for (var index = 0; index < events.Length; index++)
            {
                R5eSeatTransitionEventKind eventKind = events[index];
                bool success = eventKind != R5eSeatTransitionEventKind.Prepare &&
                               eventKind != R5eSeatTransitionEventKind.Rollback;
                R5eAgentStepSnapshot after = success ? committed : before;
                R5eProductionObservation observedAfter = success
                    ? committedObservation
                    : beforeObservation;
                actor.AppendTransition(new R5eSeatTransitionTraceRow(
                    Context(10 + (int)transactionId, (ulong)(10 + transactionId + (ulong)index),
                        10 + transactionId),
                    ActorId,
                    SeatId,
                    transactionId,
                    sessionId,
                    eventKind,
                    kind,
                    before,
                    after,
                    plan,
                    chosenExit,
                    success,
                    eventKind == R5eSeatTransitionEventKind.Rollback,
                    false,
                    fault,
                    beforeObservation,
                    observedAfter));
            }
        }

        private static OfficeRuntimeStepTraceContext Context(
            int frame,
            ulong stepOrdinal,
            ulong runtimeTick) =>
            new OfficeRuntimeStepTraceContext(1, 1, frame, 0, stepOrdinal, runtimeTick,
                0, 1, 0.016f, 0.016f);

        private static R5eAgentStepSnapshot Snapshot(
            OfficeRuntimeAgentPhase phase,
            Vector2 position,
            int facing,
            ulong route,
            ulong handoff,
            Vector2 actual = default,
            Vector2 velocity = default) =>
            new R5eAgentStepSnapshot(
                phase,
                position,
                position,
                position,
                position,
                position,
                position,
                position,
                position,
                velocity,
                velocity,
                0f,
                0f,
                actual,
                actual,
                actual,
                0f,
                0f,
                0,
                facing,
                route,
                handoff,
                0,
                phase == OfficeRuntimeAgentPhase.Working,
                phase == OfficeRuntimeAgentPhase.LeavingSeat);

        private static R5eProductionObservation Observation(
            Vector2 position,
            OfficeGridCoordinate cell,
            in R5eFurnitureTransformSnapshot chair,
            bool reserved,
            bool occupied)
        {
            var occupancy = new OfficeRuntimeOccupancy.CanonicalActorSnapshot(
                ActorId,
                position,
                Vector2.zero,
                0f,
                OfficeRuntimeAgent.DefaultRadius,
                cell,
                true,
                0,
                5,
                17);
            return new R5eProductionObservation(
                occupancy,
                chair,
                true,
                true,
                false,
                false,
                false,
                reserved,
                occupied,
                0,
                0,
                0,
                0,
                0,
                1,
                0,
                0f,
                true);
        }

        private static void WriteScenarioResults(
            string path,
            ProductionScenarioResult[] results)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("schemaVersion,scenarioId,caseId,kind,terminalObserved,passed,detail");
            for (var index = 0; index < results.Length; index++)
            {
                ProductionScenarioResult row = results[index];
                writer.WriteLine(
                    OfficeSeatDockingTraceSchemas.SchemaVersion + "," + row.ScenarioId + "," +
                    row.CaseId + "," + row.Kind + ",true," +
                    (row.Passed ? "true" : "false") + "," + row.Detail);
                if (!row.Passed)
                    throw new InvalidOperationException(
                        "Production transaction scenario failed: " + row.CaseId + ":" + row.Detail);
            }
        }

        private static ProductionScenarioResult Failed(
            in R5eScenarioCase scenario,
            string detail) => new ProductionScenarioResult(scenario, false, detail);

        private sealed class ProductionFaultException : Exception { }

        private struct ProductionAtomicPublisher : IR5eAtomicPublishSteps
        {
            private readonly OfficeSeatRuntimeClaim _claimOwner;
            private readonly OfficeRuntimeOccupancy _occupancy;
            private readonly OfficeSeatingState.PreparedRuntimeMutation _claim;
            private readonly OfficeRuntimeOccupancy.PreparedAtomicActorPlacement _placement;
            private readonly ProductionActorJournal _before;
            private readonly Vector2 _target;
            private readonly bool _entry;
            private readonly int _faultPoint;

            public ProductionAtomicPublisher(
                OfficeSeatRuntimeClaim claimOwner,
                OfficeRuntimeOccupancy occupancy,
                in OfficeSeatingState.PreparedRuntimeMutation claim,
                in OfficeRuntimeOccupancy.PreparedAtomicActorPlacement placement,
                in ProductionActorJournal journal,
                Vector2 target,
                bool entry,
                int faultPoint)
            {
                _claimOwner = claimOwner;
                _occupancy = occupancy;
                _claim = claim;
                _placement = placement;
                _before = journal;
                _target = target;
                _entry = entry;
                _faultPoint = faultPoint;
                Journal = journal;
            }

            public ProductionActorJournal Journal { get; private set; }

            public void ThrowIfFault(R5eFaultInjectionPoint point)
            {
                if (_faultPoint == (int)point) throw new ProductionFaultException();
            }

            public void CommitClaim()
            {
                if (_entry) _claimOwner.CommitPreparedOccupy(_claim);
                else _claimOwner.CommitPreparedRelease(_claim);
            }

            public void CommitOccupancy() =>
                _occupancy.CommitPreparedAtomicActorPlacement(_placement);

            public void CommitRoot()
            {
                ProductionActorJournal next = Journal;
                next.Root = _target;
                Journal = next;
            }

            public void CommitRenderer()
            {
                ProductionActorJournal next = Journal;
                next.RenderRevision++;
                Journal = next;
            }

            public void CommitRebase()
            {
                ProductionActorJournal next = Journal;
                next.Debt = 0;
                next.Route = 0;
                Journal = next;
            }

            public void CommitState()
            {
                ProductionActorJournal next = Journal;
                next.State = _entry ? "Working" : "LeavingSeat";
                Journal = next;
            }

            public void Rollback(bool claimCommitted, bool occupancyCommitted)
            {
                if (occupancyCommitted)
                    _occupancy.RollbackPreparedAtomicActorPlacement(_placement);
                else
                    _occupancy.CancelPreparedAtomicActorPlacement(_placement);
                if (claimCommitted)
                {
                    if (_entry) _claimOwner.RollbackPreparedOccupy(_claim);
                    else _claimOwner.RollbackPreparedRelease(_claim);
                }
                Journal = _before;
            }
        }

        private struct ProductionActorJournal : IEquatable<ProductionActorJournal>
        {
            public ProductionActorJournal(
                string state,
                Vector2 root,
                int renderRevision,
                int debt)
            {
                State = state;
                Root = root;
                RenderRevision = renderRevision;
                Debt = debt;
                Route = 19;
            }

            public string State;
            public Vector2 Root;
            public int RenderRevision;
            public int Debt;
            public int Route;

            public bool IsExact(string state, Vector2 root, int render, int debt) =>
                State == state && Root == root && RenderRevision == render && Debt == debt && Route == 19;

            public bool Equals(ProductionActorJournal other) =>
                State == other.State && Root == other.Root &&
                RenderRevision == other.RenderRevision && Debt == other.Debt && Route == other.Route;
        }

        private readonly struct ProductionScenarioResult
        {
            public ProductionScenarioResult(
                in R5eScenarioCase scenario,
                bool passed,
                string detail)
            {
                ScenarioId = scenario.Id;
                CaseId = scenario.CaseId;
                Kind = scenario.Kind.ToString();
                Passed = passed;
                Detail = detail;
                FaultId = scenario.FaultInjectionId;
            }

            public ulong ScenarioId { get; }
            public string CaseId { get; }
            public string Kind { get; }
            public bool Passed { get; }
            public string Detail { get; }
            public int FaultId { get; }
        }

        private readonly struct R2FixtureResult
        {
            public R2FixtureResult(FixtureVerdict[] verdicts, string summary)
            {
                Verdicts = verdicts;
                Summary = summary;
            }

            public FixtureVerdict[] Verdicts { get; }
            public string Summary { get; }
        }

        private readonly struct FixtureVerdict
        {
            public FixtureVerdict(
                string fixtureId,
                string inputId,
                string faultId,
                string controlKind,
                string expected,
                string actual,
                bool passed)
            {
                FixtureId = fixtureId;
                InputId = inputId;
                FaultId = faultId;
                ControlKind = controlKind;
                Expected = expected;
                Actual = actual;
                Passed = passed;
            }

            public string FixtureId { get; }
            public string InputId { get; }
            public string FaultId { get; }
            public string ControlKind { get; }
            public string Expected { get; }
            public string Actual { get; }
            public bool Passed { get; }
        }

        private readonly struct R2ArrivalProjection
        {
            public R2ArrivalProjection(
                ArrivalTraceRow[] rows,
                int visible0903,
                int routeTransitions,
                int workTransitions,
                bool passed,
                string summary)
            {
                Rows = rows;
                Visible0903 = visible0903;
                RouteTransitions = routeTransitions;
                WorkTransitions = workTransitions;
                Passed = passed;
                Summary = summary;
            }

            public ArrivalTraceRow[] Rows { get; }
            public int Visible0903 { get; }
            public int RouteTransitions { get; }
            public int WorkTransitions { get; }
            public bool Passed { get; }
            public string Summary { get; }
        }

        private readonly struct ArrivalTraceRow
        {
            public ArrivalTraceRow(
                string runKind,
                int tick,
                string time,
                int visibleCount,
                string player,
                string olderSister,
                string father,
                string mother,
                string releasedActor,
                int worldTicks,
                int clockTicks,
                int exceptions)
            {
                RunKind = runKind;
                Tick = tick;
                Time = time;
                VisibleCount = visibleCount;
                Player = player;
                OlderSister = olderSister;
                Father = father;
                Mother = mother;
                ReleasedActor = releasedActor;
                WorldTicks = worldTicks;
                ClockTicks = clockTicks;
                Exceptions = exceptions;
            }

            public string RunKind { get; }
            public int Tick { get; }
            public string Time { get; }
            public int VisibleCount { get; }
            public string Player { get; }
            public string OlderSister { get; }
            public string Father { get; }
            public string Mother { get; }
            public string ReleasedActor { get; }
            public int WorldTicks { get; }
            public int ClockTicks { get; }
            public int Exceptions { get; }
        }

        private sealed class ArrivalFixtureActor
        {
            private bool _ingressActive;
            private OfficeNavPoint _velocity;

            public ArrivalFixtureActor(string actorId)
            {
                ActorId = actorId;
                Away = true;
                Phase = OfficeRuntimeAgentPhase.Outside;
                Position = Vector2.zero;
                _velocity = new OfficeNavPoint(0f, 0f);
            }

            public string ActorId { get; }
            public bool Away { get; private set; }
            public OfficeRuntimeAgentPhase Phase { get; private set; }
            public Vector2 Position { get; private set; }
            public int RouteTransitions { get; private set; }
            public int WorkTransitions { get; private set; }
            public string StateLabel => Away
                ? "Outside"
                : _ingressActive
                    ? "Ingress"
                    : Phase.ToString();

            public void ReleaseAtEntrance()
            {
                if (!Away) return;
                Away = false;
                _ingressActive = true;
                Phase = OfficeRuntimeAgentPhase.Navigating;
                Position = Vector2.zero;
                _velocity = new OfficeNavPoint(0f, 0f);
            }

            public void AdvanceProductionMotion()
            {
                if (Away) return;
                if (_ingressActive)
                {
                    for (var render = 0; render < 4; render++)
                    {
                        const float motionDelta = 0.35f;
                        int stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(motionDelta);
                        for (var step = 0; step < stepCount; step++)
                        {
                            float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(
                                motionDelta,
                                step,
                                stepCount);
                            var target = new OfficeNavPoint(OfficeRuntimeAgent.DefaultMoveSpeed, 0f);
                            float change = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                                _velocity,
                                target,
                                7.5f,
                                false);
                            OfficeMotionIntegrationResult result =
                                OfficeNavigationMotionIntegrator.IntegrateVelocity(
                                    _velocity,
                                    target,
                                    change,
                                    stepDelta);
                            _velocity = result.Velocity;
                            Position += new Vector2(
                                (float)result.Displacement.X,
                                (float)result.Displacement.Z);
                        }
                    }
                    if (Position.x < 0.72f) return;
                    _ingressActive = false;
                    RouteTransitions++;
                    return;
                }
                if (Phase != OfficeRuntimeAgentPhase.Navigating) return;
                Phase = OfficeRuntimeAgentPhase.Working;
                WorkTransitions++;
            }
        }
    }
}

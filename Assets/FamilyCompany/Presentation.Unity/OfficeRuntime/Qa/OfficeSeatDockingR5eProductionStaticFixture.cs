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
            R3FixtureResult r3 = RunR3ProductionControls(
                Path.Combine(output, "arrival-r3-regression"),
                Path.GetFullPath(catalogPath));
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
                r3.Verdicts);

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
                   " " + r3.Summary;
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

        private static R3FixtureResult RunR3ProductionControls(
            string artifactDirectory,
            string scenarioCatalogPath)
        {
            Directory.CreateDirectory(artifactDirectory);
            var verdicts = new List<FixtureVerdict>();
            ValidateAllRegistrationPermutations(verdicts);
            ValidateProductionTickEdges(verdicts);
            ValidateBootstrapPreparationEdges(verdicts);
            ValidateCheckedInPreloadCatalog(scenarioCatalogPath, verdicts);

            if (verdicts.Any(item => !item.Passed))
                throw new InvalidOperationException(
                    "R3 production call-edge control failed: " +
                    string.Join(";", verdicts.Where(item => !item.Passed).Select(item => item.FixtureId)));
            int positive = verdicts.Count(item => item.ControlKind == "positive");
            int negative = verdicts.Count(item => item.ControlKind == "negative-control");
            string result =
                "status=PASS\n" +
                "fixtureKind=production-call-edge-r3\n" +
                "selfModelArrivalProjection=unused\n" +
                "registryPermutations=24\n" +
                "tickEdges=7\n" +
                "bootstrapEdges=4\n" +
                "catalogEdges=3\n" +
                "positiveControls=" + positive + "\n" +
                "negativeControls=" + negative + "\n" +
                "fixtureVerdicts=" + verdicts.Count + "\n";
            File.WriteAllText(
                Path.Combine(artifactDirectory, "arrival-r3-result.txt"),
                result,
                new UTF8Encoding(false));
            return new R3FixtureResult(
                verdicts.ToArray(),
                "arrivalR3=PASS controls=" + verdicts.Count +
                " positive=" + positive + " negative=" + negative);
        }

        private static void ValidateAllRegistrationPermutations(List<FixtureVerdict> verdicts)
        {
            string[] canonical = { "player", "older_sister", "father", "mother" };
            var permutations = new List<string[]>();
            BuildPermutations(canonical, 0, permutations);
            int exact = 0;
            foreach (string[] registrationOrder in permutations)
            {
                OfficeRuntimeActorRegistry.ValidateCanonicalActorIds(registrationOrder);
                var coordinator = new OfficeRuntimeTraceCoordinator(1, 0, 4, true);
                var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string actorId in registrationOrder)
                {
                    OfficeRuntimeActorTraceState state = coordinator.RegisterActorIdentity(actorId);
                    ordinals.Add(actorId, state.ActorIndex);
                }
                string[] scheduler = (string[])registrationOrder.Clone();
                Array.Sort(scheduler, OfficeRuntimeActorRegistry.CompareActorIds);
                if (scheduler.All(actorId =>
                        coordinator.TryGetActorState(actorId, out var state) &&
                        state.ActorId == actorId && state.ActorIndex == ordinals[actorId])) exact++;
            }
            AddVerdict(
                verdicts,
                "r3-registry-24-permutations",
                "canonical4;registrationPermutations=" + permutations.Count,
                "none",
                "positive",
                "production registry validation/comparator and trace actorId authority exact=24",
                "exact=" + exact,
                permutations.Count == 24 && exact == 24);
        }

        private static void BuildPermutations(string[] values, int start, List<string[]> output)
        {
            if (start == values.Length)
            {
                output.Add((string[])values.Clone());
                return;
            }
            for (var index = start; index < values.Length; index++)
            {
                (values[start], values[index]) = (values[index], values[start]);
                BuildPermutations(values, start + 1, output);
                (values[start], values[index]) = (values[index], values[start]);
            }
        }

        private static void ValidateProductionTickEdges(List<FixtureVerdict> verdicts)
        {
            ValidateProductionTickEdge("valid-off", "valid", false, false, verdicts);
            ValidateProductionTickEdge("null-off", "null", false, false, verdicts);
            ValidateProductionTickEdge("wrong-off", "wrong", false, false, verdicts);
            ValidateProductionTickEdge("valid-on", "valid", true, false, verdicts);
            ValidateProductionTickEdge("null-on", "null", true, false, verdicts);
            ValidateProductionTickEdge("wrong-on", "wrong", true, false, verdicts);
            ValidateProductionTickEdge("observer-fault-on", "valid", true, true, verdicts);
        }

        private static void ValidateProductionTickEdge(
            string fixtureSuffix,
            string bindingKind,
            bool captureEnabled,
            bool throwFromPreClearObserver,
            List<FixtureVerdict> verdicts)
        {
            string[] actorIds = { "player", "older_sister", "father", "mother" };
            var coordinator = new OfficeRuntimeTraceCoordinator(2, 0, 4, captureEnabled);
            foreach (string actorId in actorIds) coordinator.RegisterActorIdentity(actorId);
            bool valid = bindingKind == "valid";
            bool traceContextValid = captureEnabled && valid;
            bool mismatchContinue = !captureEnabled || valid || coordinator.TryReserveTransitionRows(
                bindingKind == "null" ? null : "wrong-actor-id",
                1);
            int observedBegin = 0;
            int unobservedBegin = 0;
            int gameplayTicks = 0;
            int observedPreClear = 0;
            int gameplayEpilogues = 0;
            int observedPostClear = 0;
            int aborts = 0;
            OfficeRuntimeWorld.ExecuteActorRuntimeStep(
                traceContextValid,
                () => observedBegin++,
                () => unobservedBegin++,
                () => gameplayTicks++,
                () =>
                {
                    observedPreClear++;
                    if (throwFromPreClearObserver)
                        throw new InvalidOperationException("fixture-observer-fault");
                },
                () => gameplayEpilogues++,
                () => observedPostClear++,
                () => aborts++,
                exception => coordinator.AbortFatal(
                    "fixture-gameplay-failure:" + exception.GetType().Name),
                exception => coordinator.AbortFatal(
                    "fixture-observer-failure:" + exception.GetType().Name),
                captureEnabled);

            bool invalidBinding = !valid;
            bool observerFault = throwFromPreClearObserver;
            bool countsExact = gameplayTicks == 1 && gameplayEpilogues == 1 &&
                               observedBegin == (traceContextValid ? 1 : 0) &&
                               unobservedBegin == (traceContextValid ? 0 : 1) &&
                               observedPreClear == (traceContextValid ? 1 : 0) &&
                               observedPostClear == (traceContextValid && !observerFault ? 1 : 0) &&
                               aborts == (observerFault ? 1 : 0);
            bool evidenceExact = captureEnabled && (invalidBinding || observerFault)
                ? coordinator.FatalAbort && coordinator.FailureCount == 1
                : !coordinator.FatalAbort && coordinator.FailureCount == 0 &&
                  coordinator.FatalReason.Length == 0;
            AddVerdict(
                verdicts,
                "r3-tick-" + fixtureSuffix,
                "binding=" + bindingKind + ";capture=" + captureEnabled +
                ";observerFault=" + observerFault,
                invalidBinding ? "trace-binding-mismatch" :
                observerFault ? "trace-append-exception" : "none",
                invalidBinding || observerFault ? "negative-control" : "positive",
                "gameplayTick=1;epilogue=1;trace callbacks only with valid context;evidence isolated",
                "tick=" + gameplayTicks + ";epilogue=" + gameplayEpilogues +
                ";observed=" + observedBegin + "/" + observedPreClear + "/" +
                observedPostClear + ";unobserved=" + unobservedBegin +
                ";fatal=" + coordinator.FatalAbort + ";failures=" +
                coordinator.FailureCount,
                mismatchContinue && countsExact && evidenceExact);
        }

        private static void ValidateBootstrapPreparationEdges(List<FixtureVerdict> verdicts)
        {
            string[] canonical = { "player", "older_sister", "father", "mother" };
            bool[] routes4 = { true, true, true, true };
            bool[] routes3 = { true, true, true, false };

            var success = new StarterOfficeRuntimePreparationGate();
            success.Begin();
            int successPreload = 0;
            int successBind = 0;
            int successCleanup = 0;
            bool successPassed = success.TryComplete(
                () => successPreload++,
                () => StarterOfficeRuntimeBootstrap.RequireCompleteAttendancePreparation(
                    canonical,
                    routes4),
                () => successBind++,
                () => successCleanup++,
                "fixture-routes4",
                out Exception successFailure);
            AddVerdict(
                verdicts,
                "r3-bootstrap-routes4",
                "canonical=4;prepared=4;routes=4",
                "none",
                "positive",
                "preload=1;bind=1;cleanup=0;Ready",
                "preload=" + successPreload + ";bind=" + successBind +
                ";cleanup=" + successCleanup + ";state=" + success.State,
                successPassed && successFailure == null && successPreload == 1 &&
                successBind == 1 && successCleanup == 0 &&
                success.State == StarterOfficeRuntimePreparationState.Ready);

            var retry = new StarterOfficeRuntimePreparationGate();
            retry.Begin();
            int failedBind = 0;
            int cleanup = 0;
            bool route3Passed = retry.TryComplete(
                () => { },
                () => StarterOfficeRuntimeBootstrap.RequireCompleteAttendancePreparation(
                    canonical,
                    routes3),
                () => failedBind++,
                () => cleanup++,
                "fixture-routes3",
                out Exception route3Failure);
            AddVerdict(
                verdicts,
                "r3-bootstrap-routes3-failure",
                "canonical=4;prepared=3;routes=3",
                "attendance-route-missing",
                "negative-control",
                "bind=0;cleanup=1;Failed",
                "passed=" + route3Passed + ";bind=" + failedBind +
                ";cleanup=" + cleanup + ";state=" + retry.State +
                ";failure=" + route3Failure?.Message,
                !route3Passed && failedBind == 0 && cleanup == 1 &&
                retry.State == StarterOfficeRuntimePreparationState.Failed &&
                route3Failure != null);

            retry.Begin();
            int retryBind = 0;
            bool retryPassed = retry.TryComplete(
                () => { },
                () => StarterOfficeRuntimeBootstrap.RequireCompleteAttendancePreparation(
                    canonical,
                    routes4),
                () => retryBind++,
                () => cleanup++,
                "fixture-retry",
                out Exception retryFailure);
            AddVerdict(
                verdicts,
                "r3-bootstrap-retry-same-gate",
                "faultRemoved=true;canonical=4;routes=4",
                "none",
                "positive",
                "same preparation gate retries to Ready;bind=1;extraCleanup=0",
                "passed=" + retryPassed + ";bind=" + retryBind +
                ";cleanupTotal=" + cleanup + ";state=" + retry.State,
                retryPassed && retryFailure == null && retryBind == 1 && cleanup == 1 &&
                retry.State == StarterOfficeRuntimePreparationState.Ready);

            var safeStatic = new StarterOfficeRuntimePreparationGate();
            safeStatic.Begin();
            int safeBind = 0;
            int safeCleanup = 0;
            bool safePassed = safeStatic.TryComplete(
                () => throw new InvalidOperationException("safe-static-validation"),
                () => StarterOfficeRuntimeBootstrap.RequireCompleteAttendancePreparation(
                    canonical,
                    routes4),
                () => safeBind++,
                () => safeCleanup++,
                "fixture-safe-static",
                out Exception safeFailure);
            AddVerdict(
                verdicts,
                "r3-bootstrap-safe-static-failure",
                "preload=safe-static-validation-exception",
                "safe-static-validation",
                "negative-control",
                "bind=0;cleanup=1;Failed",
                "passed=" + safePassed + ";bind=" + safeBind +
                ";cleanup=" + safeCleanup + ";state=" + safeStatic.State +
                ";failure=" + safeFailure?.Message,
                !safePassed && safeBind == 0 && safeCleanup == 1 &&
                safeStatic.State == StarterOfficeRuntimePreparationState.Failed &&
                safeFailure != null && safeFailure.Message.Contains("safe-static-validation"));
        }

        private static void ValidateCheckedInPreloadCatalog(
            string scenarioCatalogPath,
            List<FixtureVerdict> verdicts)
        {
            string poseCatalogPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(scenarioCatalogPath) ?? string.Empty,
                "..",
                "OfficeGrid",
                "Authoring",
                "OfficeCharacterSeatPoseCatalog.asset"));
            OfficeCharacterSeatPoseProfile[] profiles = LoadCheckedInPoseProfiles(poseCatalogPath);
            string[] canonical = { "player", "older_sister", "father", "mother" };
            bool exact = profiles.Length == 56;
            var planSummary = new List<string>();
            foreach (string actorId in canonical)
            {
                OfficeCharacterSeatPoseProfile[] plan =
                    OfficeRuntimeAgent.BuildR5eSeatPresentationPreloadPlan(profiles, actorId);
                exact &= plan.Length == OfficeSeatingAnimationFrames.WorkFrameCount &&
                         plan.All(profile => profile.DirectionIndex ==
                                             OfficeRuntimeAgent.RequiredR5eSeatPreloadDirection) &&
                         plan.Select(profile => profile.FrameIndex)
                             .SequenceEqual(Enumerable.Range(
                                 0,
                                 OfficeSeatingAnimationFrames.WorkFrameCount));
                planSummary.Add(actorId + "=" + string.Join("/", plan.Select(item =>
                    item.DirectionIndex + ":" + item.FrameIndex)));
            }
            AddVerdict(
                verdicts,
                "r3-preload-checked-in-northwest",
                "path=" + poseCatalogPath,
                "none",
                "positive",
                "actual catalog profiles=56;canonical4 each Northwest Work 0..5",
                string.Join(";", planSummary),
                exact);

            OfficeCharacterSeatPoseProfile[] missingNorthwest = profiles.Where(profile =>
                !string.Equals(profile.MemberId, "player", StringComparison.Ordinal) ||
                profile.Clip != OfficeSeatingAnimationClip.Work).ToArray();
            ValidateMissingPreloadClone(
                "r3-preload-missing-northwest",
                missingNorthwest,
                "No approved Northwest Work preload poses",
                verdicts);

            OfficeCharacterSeatPoseProfile[] missingFrame = profiles.Where(profile =>
                !string.Equals(profile.MemberId, "player", StringComparison.Ordinal) ||
                profile.Clip != OfficeSeatingAnimationClip.Work ||
                profile.FrameIndex != OfficeSeatingAnimationFrames.WorkFrameCount - 1).ToArray();
            ValidateMissingPreloadClone(
                "r3-preload-missing-frame",
                missingFrame,
                "Incomplete preload pose direction",
                verdicts);
        }

        private static void ValidateMissingPreloadClone(
            string fixtureId,
            OfficeCharacterSeatPoseProfile[] profiles,
            string requiredReason,
            List<FixtureVerdict> verdicts)
        {
            string actual = string.Empty;
            bool rejected = false;
            try
            {
                OfficeRuntimeAgent.BuildR5eSeatPresentationPreloadPlan(profiles, "player");
            }
            catch (InvalidOperationException exception)
            {
                actual = exception.Message;
                rejected = actual.Contains(requiredReason);
            }
            AddVerdict(
                verdicts,
                fixtureId,
                "checked-in catalog in-memory clone",
                "missing-northwest-or-frame",
                "negative-control",
                "production preload planner rejects before bind/Ready",
                actual,
                rejected);
        }

        private static OfficeCharacterSeatPoseProfile[] LoadCheckedInPoseProfiles(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Checked-in seat pose catalog is missing.", path);
            var profiles = new List<OfficeCharacterSeatPoseProfile>();
            string memberId = null;
            int direction = -1;
            int clip = -1;
            int frame = -1;
            bool approved = false;
            string sha = string.Empty;

            void Flush()
            {
                if (memberId == null) return;
                profiles.Add(OfficeCharacterSeatPoseProfile.Create(
                    memberId,
                    direction,
                    (OfficeSeatingAnimationClip)clip,
                    frame,
                    Vector2.zero,
                    Vector2.zero,
                    1f,
                    0f,
                    approved,
                    sha));
            }

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.StartsWith("- memberId: ", StringComparison.Ordinal))
                {
                    Flush();
                    memberId = line.Substring("- memberId: ".Length).Trim();
                    direction = -1;
                    clip = -1;
                    frame = -1;
                    approved = false;
                    sha = string.Empty;
                }
                else if (line.StartsWith("directionIndex: ", StringComparison.Ordinal))
                    direction = int.Parse(line.Substring("directionIndex: ".Length), CultureInfo.InvariantCulture);
                else if (line.StartsWith("clip: ", StringComparison.Ordinal))
                    clip = int.Parse(line.Substring("clip: ".Length), CultureInfo.InvariantCulture);
                else if (line.StartsWith("frameIndex: ", StringComparison.Ordinal))
                    frame = int.Parse(line.Substring("frameIndex: ".Length), CultureInfo.InvariantCulture);
                else if (line.StartsWith("humanApproved: ", StringComparison.Ordinal))
                    approved = line.EndsWith("1", StringComparison.Ordinal);
                else if (line.StartsWith("sourceSpriteSha256: ", StringComparison.Ordinal))
                    sha = line.Substring("sourceSpriteSha256: ".Length).Trim();
            }
            Flush();
            return profiles.ToArray();
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
            FixtureVerdict[] r3Verdicts)
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
            foreach (FixtureVerdict verdict in r3Verdicts)
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

        private readonly struct R3FixtureResult
        {
            public R3FixtureResult(FixtureVerdict[] verdicts, string summary)
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

    }
}

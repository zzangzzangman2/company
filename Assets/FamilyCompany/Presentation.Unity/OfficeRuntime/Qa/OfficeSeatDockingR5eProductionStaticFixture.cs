using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeSeating;
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
            var results = new ProductionScenarioResult[plan.Cases.Length];
            for (var index = 0; index < plan.Cases.Length; index++)
                results[index] = ExecuteProductionScenario(plan.Cases[index]);
            WriteScenarioResults(Path.Combine(output, "chair-r5e-scenario-results.csv"), results);

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
                   summary.LocomotionRows + " visual=" + summary.VisualRows;
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
            }

            public ulong ScenarioId { get; }
            public string CaseId { get; }
            public string Kind { get; }
            public bool Passed { get; }
            public string Detail { get; }
        }
    }
}

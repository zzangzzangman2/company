using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    internal readonly struct OfficeSeatDockingR5eTraceWriteSummary
    {
        public OfficeSeatDockingR5eTraceWriteSummary(
            int transitions,
            int seatedRows,
            int locomotionRows,
            int visualRows,
            int overflowCount,
            int droppedRowCount,
            int producerFailureCount)
        {
            TransitionRows = transitions;
            SeatedRows = seatedRows;
            LocomotionRows = locomotionRows;
            VisualRows = visualRows;
            OverflowCount = overflowCount;
            DroppedRowCount = droppedRowCount;
            ProducerFailureCount = producerFailureCount;
        }

        public int TransitionRows { get; }
        public int SeatedRows { get; }
        public int LocomotionRows { get; }
        public int VisualRows { get; }
        public int OverflowCount { get; }
        public int DroppedRowCount { get; }
        public int ProducerFailureCount { get; }
        public bool ReadyForPostProcess => TransitionRows > 0 && SeatedRows > 0 &&
                               LocomotionRows > 0 && VisualRows > 0 &&
                               OverflowCount == 0 && DroppedRowCount == 0 && ProducerFailureCount == 0;
        public bool Passed => false;
    }

    /// <summary>
    /// Post-window only serializer. Gameplay paths only append structs to preallocated buffers;
    /// strings, dictionaries and file IO begin after the runtime measurement owner closes it.
    /// </summary>
    internal static class OfficeSeatDockingR5eTraceWriter
    {
        public static OfficeSeatDockingR5eTraceWriteSummary Write(
            OfficeRuntimeTraceCoordinator coordinator,
            string directory)
        {
            if (coordinator == null) throw new ArgumentNullException(nameof(coordinator));
            var actorStates = new List<OfficeRuntimeActorTraceState>(coordinator.RegisteredActorCount);
            for (var index = 0; index < coordinator.RegisteredActorCount; index++)
                actorStates.Add(coordinator.ActorStateAt(index));
            return WriteStates(actorStates, coordinator.FailureCount, directory);
        }

        public static OfficeSeatDockingR5eTraceWriteSummary WriteMany(
            IReadOnlyList<OfficeRuntimeTraceCoordinator> coordinators,
            string directory)
        {
            if (coordinators == null) throw new ArgumentNullException(nameof(coordinators));
            var actorStates = new List<OfficeRuntimeActorTraceState>(coordinators.Count * 4);
            int coordinatorFailures = 0;
            for (var coordinatorIndex = 0; coordinatorIndex < coordinators.Count; coordinatorIndex++)
            {
                OfficeRuntimeTraceCoordinator coordinator = coordinators[coordinatorIndex];
                if (coordinator == null) throw new InvalidOperationException(
                    "R5e scenario coordinator packet contains a null entry.");
                coordinatorFailures += coordinator.FailureCount;
                for (var actorIndex = 0; actorIndex < coordinator.RegisteredActorCount; actorIndex++)
                    actorStates.Add(coordinator.ActorStateAt(actorIndex));
            }
            return WriteStates(actorStates, coordinatorFailures, directory);
        }

        public static OfficeSeatDockingR5eTraceWriteSummary WriteArchive(
            OfficeRuntimeTraceArchive archive,
            string directory)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            return WriteStates(archive.States, archive.FailureCount, directory);
        }

        internal static OfficeSeatDockingR5eTraceWriteSummary WriteProductionStaticFixture(
            IReadOnlyList<OfficeRuntimeActorTraceState> actorStates,
            string directory)
        {
            if (actorStates == null || actorStates.Count == 0)
                throw new ArgumentException("Production fixture requires observed actor rows.", nameof(actorStates));
            return WriteStates(actorStates, 0, directory);
        }

        private static OfficeSeatDockingR5eTraceWriteSummary WriteStates(
            IReadOnlyList<OfficeRuntimeActorTraceState> actorStates,
            int coordinatorFailures,
            string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Artifact directory is required.", nameof(directory));
            Directory.CreateDirectory(directory);

            string[] transitionHeader = OfficeSeatDockingTraceSchemas.TransitionHeader.Split(',');
            string[] seatedHeader = OfficeSeatDockingTraceSchemas.SeatedSessionHeader.Split(',');
            string[] locomotionHeader = OfficeSeatDockingTraceSchemas.LocomotionAdapterHeader.Split(',');

            int transitionRows = WriteTransitions(
                Path.Combine(directory, "seat-transition-events-r5e.csv"),
                transitionHeader,
                actorStates);
            int seatedRows = WriteSeated(
                Path.Combine(directory, "seat-session-samples-r5e.csv"),
                seatedHeader,
                actorStates);
            int locomotionRows = WriteLocomotion(
                Path.Combine(directory, "locomotion-step-adapter-r5e.csv"),
                locomotionHeader,
                actorStates);
            int visualRows = WriteVisualMetadata(
                Path.Combine(directory, "visual-capture-metadata-r5e.csv"),
                actorStates);

            int overflow = actorStates.Sum(state =>
                state.TransitionRows.OverflowCount + state.SeatedRows.OverflowCount +
                state.LocomotionRows.OverflowCount + state.VisualRows.OverflowCount);
            int dropped = actorStates.Sum(state =>
                state.TransitionRows.DroppedRowCount + state.SeatedRows.DroppedRowCount +
                state.LocomotionRows.DroppedRowCount + state.VisualRows.DroppedRowCount);
            int failures = coordinatorFailures + actorStates.Count(state => state.Failed);
            return new OfficeSeatDockingR5eTraceWriteSummary(
                transitionRows,
                seatedRows,
                locomotionRows,
                visualRows,
                overflow,
                dropped,
                failures);
        }

        private static int WriteTransitions(
            string path,
            string[] header,
            IReadOnlyList<OfficeRuntimeActorTraceState> actors)
        {
            var sequences = new Dictionary<string, int>(StringComparer.Ordinal);
            var sessionCounts = BuildSessionCounts(actors);
            var lines = new List<string>();
            foreach (OfficeRuntimeActorTraceState actor in actors)
            {
                for (var index = 0; index < actor.TransitionRows.Count; index++)
                {
                    R5eSeatTransitionTraceRow row = actor.TransitionRows.Rows[index];
                    var values = NewRow(header);
                    R5eAgentStepSnapshot before = row.Before;
                    R5eAgentStepSnapshot after = row.After;
                    OfficeRuntimeStepTraceContext context = row.Context;
                    R5eProductionObservation observedBefore = row.BeforeObservation;
                    R5eProductionObservation observedAfter = row.AfterObservation;
                    R5eFurnitureTransformSnapshot chair = observedBefore.ChairSnapshotValid
                        ? observedBefore.Chair
                        : row.Plan.ChairSnapshot;
                    R5eFurnitureTransformSnapshot chairAfter = observedAfter.ChairSnapshotValid
                        ? observedAfter.Chair
                        : default;
                    string transactionKey = context.RunId + "|" + row.ActorId + "|" + row.TransactionId;
                    sequences.TryGetValue(transactionKey, out int eventSequence);
                    sequences[transactionKey] = ++eventSequence;

                    Set(values, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
                    Set(values, "runId", context.RunId);
                    Set(values, "tick", context.ActorRuntimeTick);
                    Set(values, "frame", context.RenderFrame);
                    Set(values, "actorId", row.ActorId);
                    Set(values, "seatId", row.SeatId);
                    Set(values, "transactionId", row.TransactionId);
                    Set(values, "event", row.EventKind);
                    Set(values, "transitionKind", row.TransitionKind);
                    Set(values, "locomotionSample", row.LocomotionSample);
                    Set(values, "stateBefore", before.Phase);
                    Set(values, "stateAfter", after.Phase);
                    Set(values, "claimBefore", ClaimState(observedBefore));
                    Set(values, "claimAfter", ClaimState(observedAfter));
                    Set(values, "occupancyBefore", observedBefore.Occupancy.CurrentCell);
                    Set(values, "occupancyAfter", observedAfter.Occupancy.CurrentCell);
                    Set(values, "chairSnapshotVersion", chair.LayoutRevision);
                    Set(values, "chairCommitVersion", observedAfter.ChairSnapshotValid
                        ? chairAfter.LayoutRevision : -1);
                    SetVector3(values, "chairPosBefore", chair.SemanticPosition);
                    SetQuaternion(values, "chairRotBefore", chair.SemanticRotation);
                    SetVector3(values, "chairScaleBefore", chair.SemanticScale);
                    if (observedAfter.ChairSnapshotValid)
                    {
                        SetVector3(values, "chairPosAfter", chairAfter.SemanticPosition);
                        SetQuaternion(values, "chairRotAfter", chairAfter.SemanticRotation);
                        SetVector3(values, "chairScaleAfter", chairAfter.SemanticScale);
                    }
                    SetVector(values, "dock", row.Plan.DockWorld);
                    SetVector(values, "seat", row.Plan.SeatPelvisWorld);
                    SetVector(values, "exit", row.ChosenExit);
                    SetSnapshot(values, before, "Before");
                    SetSnapshot(values, after, "After");
                    Set(values, "renderedFacing", after.RenderedFacing);
                    Set(values, "quantizedVelocityFacing", ResolveFacing(after.CurrentVelocity, after.RenderedFacing));
                    Set(values, "forwardDot", ForwardDot(after.CurrentVelocity, after.RenderedFacing));
                    bool commit = row.CommitSucceeded;
                    Set(values, "floorValid", observedAfter.FloorValid);
                    Set(values, "staticOverlap", observedAfter.StaticOverlap);
                    Set(values, "chairOverlap",
                        observedAfter.StaticOverlap || observedAfter.DynamicOverlap);
                    Set(values, "exitReservationOwner", observedAfter.ExitReserved ? row.ActorId : string.Empty);
                    Set(values, "preconditionMask", observedAfter.ProducerValid
                        ? (commit ? "measured" : "measured-no-commit") : "PENDING");
                    Set(values, "faultInjectionId", row.FaultInjectionId);
                    Set(values, "commitSucceeded", row.CommitSucceeded);
                    Set(values, "rollbackSucceeded", row.RollbackSucceeded);
                    Set(values, "gcAllocBytes", observedAfter.AllocationBytes);
                    Set(values, "frameMs", observedAfter.FrameMs);
                    Set(values, "render_frame", context.RenderFrame);
                    Set(values, "sim_step", context.ActorStepOrdinal);
                    Set(values, "member", row.ActorId);
                    Set(values, "transition_id", row.TransactionId);
                    Set(values, "event_kind", row.EventKind);
                    Set(values, "state", after.Phase);
                    Set(values, "commit_result", row.CommitSucceeded);
                    Set(values, "fault_point", row.FaultInjectionId);
                    Set(values, "transition_dx", after.LogicalRoot.x - before.LogicalRoot.x);
                    Set(values, "transition_dy", after.LogicalRoot.y - before.LogicalRoot.y);
                    Set(values, "routeIdBefore", before.RouteGenerationId);
                    Set(values, "routeIdAfter", after.RouteGenerationId);
                    Set(values, "pathIndexBefore", before.PathIndex);
                    Set(values, "pathIndexAfter", after.PathIndex);
                    SetVector(values, "sweepOriginBefore", before.CollisionSweepOrigin);
                    SetVector(values, "sweepOriginAfter", after.CollisionSweepOrigin);
                    Set(values, "visibleMotionDebtSecondsBefore", before.VisibleMotionDebtSeconds);
                    Set(values, "visibleMotionDebtSecondsAfter", after.VisibleMotionDebtSeconds);
                    Set(values, "movementBudgetBefore", before.MovementBudgetWorld);
                    Set(values, "movementBudgetAfter", after.MovementBudgetWorld);
                    SetDisplacements(values, before, "Before");
                    SetDisplacements(values, after, "After");
                    Set(values, "gaitDistanceBefore", before.GaitDistance);
                    Set(values, "gaitDistanceAfter", after.GaitDistance);
                    Set(values, "gaitPhaseBefore", before.GaitPhase);
                    Set(values, "gaitPhaseAfter", after.GaitPhase);
                    Set(values, "walkFrameBefore", before.WalkFrame);
                    Set(values, "walkFrameAfter", after.WalkFrame);
                    Set(values, "chairId", chair.ChairId);
                    Set(values, "chairKind", chair.ChairKind);
                    Set(values, "chairFacing", chair.ChairFacing);
                    Set(values, "chairRotation", chair.ChairFacing);
                    Set(values, "footprintRevision", chair.LayoutRevision);
                    Set(values, "layoutRevisionBefore", observedBefore.Occupancy.Revision);
                    Set(values, "layoutRevisionPrecommit", observedAfter.Occupancy.Revision);
                    Set(values, "occupancyRevisionBefore", observedBefore.Occupancy.Revision);
                    Set(values, "occupancyRevisionPrecommit", observedAfter.Occupancy.Revision);
                    Set(values, "anchorRevisionBefore", row.Plan.AnchorRevision);
                    Set(values, "anchorRevisionPrecommit", row.Plan.AnchorRevision);
                    Set(values, "chairParentIdBefore", chair.SemanticParentId);
                    Set(values, "chairParentIdPrecommit", observedAfter.ChairSnapshotValid
                        ? chairAfter.SemanticParentId : 0);
                    Set(values, "chairVisualParentIdBefore", chair.VisualParentId);
                    Set(values, "chairVisualParentIdPrecommit", observedAfter.ChairSnapshotValid
                        ? chairAfter.VisualParentId : 0);
                    if (observedAfter.ChairSnapshotValid)
                    {
                        SetVector3(values, "chairPosPrecommit", chairAfter.SemanticPosition);
                        SetQuaternion(values, "chairRotPrecommit", chairAfter.SemanticRotation);
                        SetVector3(values, "chairScalePrecommit", chairAfter.SemanticScale);
                    }
                    Set(values, "chairSnapshotHashBefore", chair.Hash);
                    Set(values, "chairSnapshotHashPrecommit", observedAfter.ChairSnapshotValid
                        ? chairAfter.Hash : 0UL);
                    SetVector(values, "approach", row.Plan.ApproachWorld);
                    SetVector(values, "seatRoot", row.Plan.SeatRootWorld);
                    Set(values, "staticClearance", observedAfter.StaticOverlap ? 0f : 1f);
                    Set(values, "dynamicClearance", observedAfter.DynamicOverlap ? 0f : 1f);
                    Set(values, "seatReservedBefore", observedBefore.SeatReserved);
                    Set(values, "seatReservedAfter", observedAfter.SeatReserved);
                    Set(values, "seatOccupiedBefore", observedBefore.SeatOccupied);
                    Set(values, "seatOccupiedAfter", observedAfter.SeatOccupied);
                    Set(values, "exitReservedBefore", observedBefore.ExitReserved);
                    Set(values, "exitReservedAfter", observedAfter.ExitReserved);
                    Set(values, "forbiddenColliderCount", observedAfter.ForbiddenColliderCount);
                    Set(values, "forbiddenCollider2DCount", observedAfter.ForbiddenCollider2DCount);
                    Set(values, "forbiddenRigidbodyCount", observedAfter.ForbiddenRigidbodyCount);
                    Set(values, "forbiddenRigidbody2DCount", observedAfter.ForbiddenRigidbody2DCount);
                    Set(values, "forbiddenNavMeshAgentCount", observedAfter.ForbiddenNavMeshAgentCount);
                    Set(values, "visibleBodyCount", observedAfter.VisibleBodyCount);
                    Set(values, "preSnapshotHash", SnapshotHash(before));
                    Set(values, "postSnapshotHash", SnapshotHash(after));
                    Set(values, "eventSequence", eventSequence);
                    Set(values, "seatedSessionId", row.SeatedSessionId);
                    Set(values, "scenarioId", context.ScenarioId);
                    Set(values, "traceCapacity", actor.TransitionRows.Capacity);
                    Set(values, "traceWriteCount", actor.TransitionRows.Count);
                    Set(values, "droppedRowCount", actor.TransitionRows.DroppedRowCount);
                    Set(values, "overflowCount", actor.TransitionRows.OverflowCount);
                    Set(values, "overflowed", actor.TransitionRows.Overflowed);
                    Set(values, "scenarioMaximumTransactionsPerActor", 64);
                    Set(values, "scenarioMaximumRowsPerTransaction", 5);
                    SessionCount session = SessionFor(sessionCounts, context.RunId, row.ActorId, row.SeatedSessionId);
                    Set(values, "expectedSeatedTickCount", session.ExpectedPairs);
                    Set(values, "observedSeatedTickCount", session.ObservedPairs);
                    Set(values, "seatedViolationCount", actor.SeatedViolationCount);
                    Set(values, "expectedTransactionCount", sequences.Count);
                    Set(values, "transactionComplete", IsTerminal(row.EventKind, row.TransitionKind));
                    Set(values, "producerCoverageValid", !actor.Failed);
                    Set(values, "traceWindowExceeded", actor.TransitionRows.Overflowed);
                    Set(values, "runtimeStepIndex", context.ActorStepIndex);
                    Set(values, "runtimeStepCount", context.ActorStepCount);
                    Set(values, "gcProfilerValid", observedAfter.ProducerValid);
                    Set(values, "mainThreadProfilerValid", observedAfter.ProducerValid);
                    Set(values, "profilerFrame", context.RenderFrame);
                    Set(values, "traceProducerAllocBytes", observedAfter.AllocationBytes);
                    Set(values, "floorCellX", observedAfter.Occupancy.CurrentCell.X);
                    Set(values, "floorCellY", observedAfter.Occupancy.CurrentCell.Y);
                    Set(values, "chairClearance", observedAfter.StaticOverlap ? 0f : 1f);
                    Set(values, "actualFurnitureOcclusionEvidenceValid", false);
                    Set(values, "actorTransactionSnapshotHashBefore",
                        TransactionSnapshotHash(before, observedBefore));
                    Set(values, "actorTransactionSnapshotHashAfter",
                        TransactionSnapshotHash(after, observedAfter));
                    Set(values, "observedChairSnapshotHashBefore", observedBefore.ChairSnapshotValid
                        ? observedBefore.Chair.Hash : 0UL);
                    Set(values, "observedChairSnapshotHashPrecommit", observedAfter.ChairSnapshotValid
                        ? observedAfter.Chair.Hash : 0UL);
                    Set(values, "observedChairMutation",
                        observedBefore.ChairSnapshotValid && observedAfter.ChairSnapshotValid &&
                        observedBefore.Chair.Hash != observedAfter.Chair.Hash);
                    Set(values, "candidateKind", row.TransitionKind == R5eSeatTransitionKind.Exit ? ResolveExitKind(row.Plan, row.ChosenExit).ToString() : "Dock");
                    Set(values, "turnCompleted", row.EventKind == R5eSeatTransitionEventKind.TurnComplete || row.EventKind == R5eSeatTransitionEventKind.FirstWalk);
                    Set(values, "turnTargetFacing", after.RenderedFacing);
                    Set(values, "turnDisplacement", row.EventKind == R5eSeatTransitionEventKind.TurnComplete ? after.ActualDisplacement.magnitude : 0f);
                    Set(values, "movementHandoffId", after.MovementHandoffId);
                    Set(values, "locomotionTraceRowId", context.ActorStepOrdinal);
                    Set(values, "locomotionJoinFound", HasLocomotionStep(
                        actor,
                        context.RunId,
                        context.ActorStepOrdinal));
                    Set(values, "movingTickExpectedCount", actor.ExpectedMovingCount);
                    Set(values, "movingTickObservedCount", actor.ObservedMovingCount);
                    Set(values, "movingTickMissingCount", Math.Max(0, actor.ExpectedMovingCount - actor.ObservedMovingCount));
                    R5eLocomotionCounts motionCounts = CountLocomotion(actor);
                    Set(values, "wrongFacingCount", motionCounts.WrongFacing);
                    Set(values, "strafeCount", motionCounts.Strafe);
                    Set(values, "frontFacingLateralCount", motionCounts.FrontLateral);
                    Set(values, "backwardLookingCount", motionCounts.Backward);
                    // Pixel/mask/video fields are deliberately PENDING until the reachable
                    // post-process writes decoded and human-review packets.
                    Set(values, "defaultOnlyFieldMask", observedAfter.ProducerValid ? 511 : 1023);
                    lines.Add(CsvLine(header, values));
                }
            }
            WriteLines(path, header, lines);
            return lines.Count;
        }

        private static int WriteSeated(
            string path,
            string[] header,
            IReadOnlyList<OfficeRuntimeActorTraceState> actors)
        {
            var lines = new List<string>();
            foreach (OfficeRuntimeActorTraceState actor in actors)
            {
                for (var index = 0; index < actor.SeatedRows.Count; index++)
                {
                    R5eSeatedSessionSampleRow row = actor.SeatedRows.Rows[index];
                    var values = NewRow(header);
                    Set(values, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
                    Set(values, "runId", row.Context.RunId);
                    Set(values, "scenarioId", row.Context.ScenarioId);
                    Set(values, "rowKind", "Sample");
                    Set(values, "samplePhase", row.SamplePhase);
                    Set(values, "frame", row.Context.RenderFrame);
                    Set(values, "actorIndex", row.Context.ActorIndex);
                    Set(values, "actorStepOrdinal", row.Context.ActorStepOrdinal);
                    Set(values, "runtimeStepIndex", row.Context.ActorStepIndex);
                    Set(values, "runtimeStepCount", row.Context.ActorStepCount);
                    Set(values, "runtimeTick", row.Context.ActorRuntimeTick);
                    Set(values, "sampleSequence", index + 1);
                    Set(values, "actorId", row.ActorId);
                    Set(values, "seatedSessionId", row.SeatedSessionId);
                    Set(values, "entryTransactionId", row.EntryTransactionId);
                    Set(values, "seatId", row.SeatId);
                    Set(values, "phase", row.Sample.Phase);
                    Set(values, "seatEgressWaiting", false);
                    SetSeatedSnapshot(values, row.PreStep, "preStep");
                    SetSeatedSnapshot(values, row.Sample, string.Empty);
                    SetVector(values, "occupancyPosition", row.Occupancy.Position);
                    Set(values, "occupancyCellX", row.Occupancy.CurrentCell.X);
                    Set(values, "occupancyCellY", row.Occupancy.CurrentCell.Y);
                    Set(values, "chairSnapshotVersion", row.Observation.ChairSnapshotValid
                        ? row.Observation.Chair.LayoutRevision : 0);
                    Set(values, "chairSnapshotHash", row.Observation.ChairSnapshotValid
                        ? row.Observation.Chair.Hash : 0UL);
                    Set(values, "claimState", row.Observation.SeatOccupied
                        ? "occupied"
                        : row.Observation.SeatReserved ? "reserved" : "released");
                    Set(values, "occupancyState", row.Observation.Occupancy.IsPresent
                        ? (row.Observation.SeatOccupied ? "seat" : "floor")
                        : "absent");
                    Set(values, "seatReserved", row.Observation.SeatReserved);
                    Set(values, "seatOccupied", row.Observation.SeatOccupied);
                    Set(values, "locomotionSample", false);
                    Set(values, "expectedOrdinal", row.ExpectedOrdinal);
                    Set(values, "observedOrdinal", row.ObservedOrdinal);
                    Set(values, "phasePairValid", row.ExpectedOrdinal == row.ObservedOrdinal);
                    Set(values, "producerValid", row.ProducerValid &&
                        row.Observation.ChairSnapshotValid &&
                        row.Observation.Occupancy.IsPresent);
                    Set(values, "aggregateUpdated", row.AggregateUpdated);
                    Set(values, "traceProducerAllocBytes", row.Observation.AllocationBytes);
                    Set(values, "expectedPreClearSampleCount", actor.ExpectedPreClearCount);
                    Set(values, "observedPreClearSampleCount", actor.ObservedPreClearCount);
                    Set(values, "expectedPostClearSampleCount", actor.ExpectedPostClearCount);
                    Set(values, "observedPostClearSampleCount", actor.ObservedPostClearCount);
                    Set(values, "expectedStepPairCount", Math.Min(actor.ExpectedPreClearCount, actor.ExpectedPostClearCount));
                    Set(values, "observedStepPairCount", Math.Min(actor.ObservedPreClearCount, actor.ObservedPostClearCount));
                    Set(values, "missingPhasePairCount", Math.Abs(actor.ExpectedPreClearCount - actor.ObservedPreClearCount) + Math.Abs(actor.ExpectedPostClearCount - actor.ObservedPostClearCount));
                    Set(values, "duplicatePhasePairCount", 0);
                    Set(values, "firstTick", row.Context.ActorRuntimeTick);
                    Set(values, "lastTick", row.Context.ActorRuntimeTick);
                    Set(values, "sequenceGapCount", 0);
                    Set(values, "droppedRowCount", actor.SeatedRows.DroppedRowCount);
                    Set(values, "overflowCount", actor.SeatedRows.OverflowCount);
                    Set(values, "violationCount", actor.SeatedViolationCount);
                    Set(values, "clearMaskedViolationCount", actor.ClearMaskedViolationCount);
                    Set(values, "maxAbsVelocity", row.Sample.CurrentVelocity.magnitude);
                    Set(values, "maxAbsDebt", Mathf.Abs(row.Sample.VisibleMotionDebtSeconds));
                    lines.Add(CsvLine(header, values));
                }

                foreach (IGrouping<ulong, R5eSeatedSessionSampleRow> group in actor.SeatedRows.Rows
                             .Take(actor.SeatedRows.Count).GroupBy(row => row.SeatedSessionId))
                {
                    R5eSeatedSessionSampleRow first = group.First();
                    var summary = NewRow(header);
                    Set(summary, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
                    Set(summary, "runId", first.Context.RunId);
                    Set(summary, "scenarioId", first.Context.ScenarioId);
                    Set(summary, "rowKind", "Summary");
                    Set(summary, "actorId", first.ActorId);
                    Set(summary, "seatedSessionId", group.Key);
                    Set(summary, "entryTransactionId", first.EntryTransactionId);
                    Set(summary, "seatId", first.SeatId);
                    int pre = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PreClear);
                    int post = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PostClear);
                    int expectedPre = group
                        .Where(row => row.SamplePhase == R5eSeatedSamplePhase.PreClear)
                        .Select(row => row.ExpectedOrdinal)
                        .DefaultIfEmpty(0)
                        .Max();
                    int expectedPost = group
                        .Where(row => row.SamplePhase == R5eSeatedSamplePhase.PostClear)
                        .Select(row => row.ExpectedOrdinal)
                        .DefaultIfEmpty(0)
                        .Max();
                    int ordinalMismatch = group.Count(row => row.ExpectedOrdinal != row.ObservedOrdinal);
                    int duplicatePairs = group.GroupBy(row =>
                            row.Context.ActorRuntimeTick + ":" + row.SamplePhase)
                        .Sum(rows => Math.Max(0, rows.Count() - 1));
                    Set(summary, "expectedPreClearSampleCount", expectedPre);
                    Set(summary, "observedPreClearSampleCount", pre);
                    Set(summary, "expectedPostClearSampleCount", expectedPost);
                    Set(summary, "observedPostClearSampleCount", post);
                    Set(summary, "expectedStepPairCount", Math.Min(expectedPre, expectedPost));
                    Set(summary, "observedStepPairCount", Math.Min(pre, post));
                    Set(summary, "missingPhasePairCount",
                        Math.Abs(expectedPre - pre) + Math.Abs(expectedPost - post));
                    Set(summary, "duplicatePhasePairCount", duplicatePairs);
                    Set(summary, "firstTick", group.Min(row => row.Context.ActorRuntimeTick));
                    Set(summary, "lastTick", group.Max(row => row.Context.ActorRuntimeTick));
                    Set(summary, "sequenceGapCount", ordinalMismatch);
                    Set(summary, "droppedRowCount", actor.SeatedRows.DroppedRowCount);
                    Set(summary, "overflowCount", actor.SeatedRows.OverflowCount);
                    Set(summary, "violationCount", actor.SeatedViolationCount);
                    Set(summary, "clearMaskedViolationCount", actor.ClearMaskedViolationCount);
                    Set(summary, "maxAbsVelocity", group.Max(row => row.Sample.CurrentVelocity.magnitude));
                    Set(summary, "maxAbsDebt", group.Max(row => Mathf.Abs(row.Sample.VisibleMotionDebtSeconds)));
                    Set(summary, "producerValid", group.All(row =>
                        row.ProducerValid && row.Observation.ChairSnapshotValid &&
                        row.Observation.Occupancy.IsPresent &&
                        row.Observation.AllocationBytes == 0));
                    Set(summary, "aggregateUpdated", group.All(row => row.AggregateUpdated));
                    Set(summary, "phasePairValid",
                        expectedPre == pre && expectedPost == post && pre == post && pre > 0 &&
                        ordinalMismatch == 0 && duplicatePairs == 0);
                    lines.Add(CsvLine(header, summary));
                }
            }
            WriteLines(path, header, lines);
            return lines.Count;
        }

        private static int WriteLocomotion(
            string path,
            string[] header,
            IReadOnlyList<OfficeRuntimeActorTraceState> actors)
        {
            var lines = new List<string>();
            foreach (OfficeRuntimeActorTraceState actor in actors)
            {
                for (var index = 0; index < actor.LocomotionRows.Count; index++)
                {
                    R5eLocomotionAdapterRow row = actor.LocomotionRows.Rows[index];
                    var values = NewRow(header);
                    Set(values, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
                    Set(values, "runId", row.Context.RunId);
                    Set(values, "scenarioId", row.Context.ScenarioId);
                    Set(values, "rowKind", row.IsRenderRow ? "Render" : "Step");
                    Set(values, "frame", row.Context.RenderFrame);
                    Set(values, "actorIndex", row.Context.ActorIndex);
                    Set(values, "actorStepOrdinal", row.Context.ActorStepOrdinal);
                    Set(values, "runtimeStepIndex", row.Context.ActorStepIndex);
                    Set(values, "runtimeStepCount", row.Context.ActorStepCount);
                    Set(values, "runtimeTick", row.Context.ActorRuntimeTick);
                    Set(values, "actorId", row.ActorId);
                    Set(values, "routeGenerationId", row.RouteGenerationId);
                    Set(values, "movementHandoffId", row.HandoffId);
                    Set(values, "phaseBefore", row.Before.Phase);
                    Set(values, "phaseAfter", row.After.Phase);
                    Set(values, "stepDelta", row.Context.StepDelta);
                    SetVector(values, "positionBefore", row.Before.LogicalRoot);
                    SetVector(values, "positionAfter", row.After.LogicalRoot);
                    Vector2 delta = row.IsRenderRow
                        ? row.StepDisplacementSum
                        : row.After.LogicalRoot - row.Before.LogicalRoot;
                    SetVector(values, "rootDelta", delta);
                    Set(values, "atomicPlacement", row.AtomicPlacement);
                    SetVector(values, "agentLastActualDisplacement", row.After.ActualDisplacement);
                    Set(values, "rootDeltaMatchesAgentActual", row.AtomicPlacement || Vector2.Distance(delta, row.After.ActualDisplacement) <= 0.000001f);
                    SetVector(values, "currentVelocity", row.After.CurrentVelocity);
                    SetVector(values, "desiredVelocity", row.After.DesiredVelocity);
                    Set(values, "expectedMoving", row.ExpectedMoving);
                    Set(values, "observedMoving", row.ObservedMoving);
                    Set(values, "firstWalk", row.FirstWalk);
                    Vector2 facingMotion = row.IsRenderRow
                        ? row.RenderTrace.ActualDisplacement
                        : delta;
                    Vector2 facingAxes = OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(
                        facingMotion);
                    int renderedFacing = row.IsRenderRow
                        ? row.RenderTrace.DisplayDirection
                        : row.After.RenderedFacing;
                    int quantized = ResolveFacing(facingAxes, renderedFacing);
                    Set(values, "quantizedVelocityFacing", quantized);
                    Set(values, "renderedFacing", renderedFacing);
                    Set(values, "forwardDot", ForwardDot(facingAxes, renderedFacing));
                    Set(values, "renderAdapterOrdinal", row.RenderOrdinal);
                    Set(values, "renderFirstActorStepOrdinal", row.FirstStepOrdinal);
                    Set(values, "renderLastActorStepOrdinal", row.LastStepOrdinal);
                    Set(values, "renderFirstRuntimeTick", row.FirstRuntimeTick);
                    Set(values, "renderLastRuntimeTick", row.LastRuntimeTick);
                    SetVector(values, "renderActualDisplacement", row.RenderTrace.ActualDisplacement);
                    Set(values, "renderActualSpeed", row.RenderTrace.ActualSpeed);
                    Set(values, "renderMotionDirection", row.RenderTrace.MotionDirection);
                    Set(values, "renderDisplayDirection", row.RenderTrace.DisplayDirection);
                    Set(values, "renderLocomotionPhase", row.RenderTrace.Phase);
                    Set(values, "renderClip", row.RenderTrace.Clip);
                    Set(values, "renderSprite", row.RenderTrace.SpriteName);
                    Set(values, "renderFlipX", row.RenderTrace.FlipX);
                    Set(values, "renderIsMoving", row.RenderTrace.IsMoving);
                    SetVector(values, "stepDisplacementSum", row.StepDisplacementSum);
                    Set(values, "renderDisplacementMatchesStepSum", !row.IsRenderRow ||
                        Vector2.Distance(row.RenderTrace.ActualDisplacement, row.StepDisplacementSum) <= 0.000001f);
                    Set(values, "renderJoinValid", !row.IsRenderRow || row.RenderJoinValid);
                    Set(values, "expectedMovingCount", actor.ExpectedMovingCount);
                    Set(values, "observedMovingCount", actor.ObservedMovingCount);
                    Set(values, "joinedMovingCount", actor.ObservedMovingCount);
                    Set(values, "missingMovingCount", Math.Max(0, actor.ExpectedMovingCount - actor.ObservedMovingCount));
                    Set(values, "expectedRenderedTraceCount", actor.ExpectedRenderedTraceCount);
                    Set(values, "observedRenderedTraceCount", actor.ObservedRenderedTraceCount);
                    Set(values, "missingRenderedTraceCount", Math.Max(0, actor.ExpectedRenderedTraceCount - actor.ObservedRenderedTraceCount));
                    Set(values, "duplicateRenderedTraceCount", row.DuplicateJoinCount);
                    Set(values, "acceptedTraceOneToOneValid",
                        actor.ExpectedRenderedTraceCount == actor.ObservedRenderedTraceCount &&
                        (!row.IsRenderRow || row.RenderJoinValid));
                    bool moving = row.IsRenderRow ? row.RenderTrace.IsMoving : row.ObservedMoving;
                    float forwardDot = ForwardDot(facingAxes, renderedFacing);
                    bool wrong = moving && (quantized != renderedFacing || forwardDot < 0.92f);
                    Set(values, "wrongFacingCount", wrong ? 1 : 0);
                    Set(values, "strafeCount", wrong ? 1 : 0);
                    Set(values, "frontFacingLateralCount",
                        moving && (renderedFacing == 0 || renderedFacing == 4) &&
                        Mathf.Abs(facingAxes.x) > Mathf.Abs(facingAxes.y) ? 1 : 0);
                    Set(values, "backwardLookingCount", moving && forwardDot < 0f ? 1 : 0);
                    Set(values, "producerValid", !actor.Failed);
                    Set(values, "droppedRowCount", actor.LocomotionRows.DroppedRowCount);
                    Set(values, "overflowCount", actor.LocomotionRows.OverflowCount);
                    lines.Add(CsvLine(header, values));
                }
            }
            WriteLines(path, header, lines);
            return lines.Count;
        }

        private static int WriteVisualMetadata(
            string path,
            IReadOnlyList<OfficeRuntimeActorTraceState> actors)
        {
            string[] header =
            {
                "schemaVersion", "runId", "scenarioId", "frame", "runtimeTick", "actorId",
                "transactionId", "seatedSessionId", "cleanFrameObserved",
                "evidenceAtlasObserved", "postProcessStatus"
            };
            var lines = new List<string>();
            foreach (OfficeRuntimeActorTraceState actor in actors)
            {
                for (var index = 0; index < actor.VisualRows.Count; index++)
                {
                    R5eVisualCaptureMetadataRow row = actor.VisualRows.Rows[index];
                    var values = NewRow(header);
                    Set(values, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
                    Set(values, "runId", row.RunId);
                    Set(values, "scenarioId", row.ScenarioId);
                    Set(values, "frame", row.Frame);
                    Set(values, "runtimeTick", row.RuntimeTick);
                    Set(values, "actorId", row.ActorId);
                    Set(values, "transactionId", row.TransactionId);
                    Set(values, "seatedSessionId", row.SeatedSessionId);
                    Set(values, "cleanFrameObserved", row.CleanFrameObserved);
                    Set(values, "evidenceAtlasObserved", row.EvidenceAtlasObserved);
                    Set(values, "postProcessStatus",
                        row.CleanFrameObserved && row.EvidenceAtlasObserved ? "READY" : "PENDING");
                    lines.Add(CsvLine(header, values));
                }
            }
            WriteLines(path, header, lines);
            return lines.Count;
        }

        private static Dictionary<string, SessionCount> BuildSessionCounts(
            IReadOnlyList<OfficeRuntimeActorTraceState> actors)
        {
            var result = new Dictionary<string, SessionCount>(StringComparer.Ordinal);
            foreach (OfficeRuntimeActorTraceState actor in actors)
            {
                foreach (IGrouping<ulong, R5eSeatedSessionSampleRow> group in actor.SeatedRows.Rows
                             .Take(actor.SeatedRows.Count).GroupBy(row => row.SeatedSessionId))
                {
                    R5eSeatedSessionSampleRow first = group.First();
                    int pre = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PreClear);
                    int post = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PostClear);
                    result[SessionKey(first.Context.RunId, first.ActorId, group.Key)] =
                        new SessionCount(Math.Min(pre, post), pre == post ? pre : 0);
                }
            }
            return result;
        }

        private static SessionCount SessionFor(
            IReadOnlyDictionary<string, SessionCount> counts,
            ulong runId,
            string actorId,
            ulong sessionId) =>
            sessionId != 0 && counts.TryGetValue(SessionKey(runId, actorId, sessionId), out SessionCount count)
                ? count
                : default;

        private static string ClaimState(in R5eProductionObservation observation)
        {
            if (observation.SeatOccupied) return "occupied";
            if (observation.SeatReserved) return "reserved";
            return "released";
        }

        private static bool HasLocomotionStep(
            OfficeRuntimeActorTraceState actor,
            ulong runId,
            ulong ordinal)
        {
            if (ordinal == 0) return false;
            for (var index = 0; index < actor.LocomotionRows.Count; index++)
            {
                R5eLocomotionAdapterRow row = actor.LocomotionRows.Rows[index];
                if (!row.IsRenderRow && row.Context.RunId == runId &&
                    row.Context.ActorStepOrdinal == ordinal) return true;
            }
            return false;
        }

        private static R5eLocomotionCounts CountLocomotion(OfficeRuntimeActorTraceState actor)
        {
            var result = new R5eLocomotionCounts();
            for (var index = 0; index < actor.LocomotionRows.Count; index++)
            {
                R5eLocomotionAdapterRow row = actor.LocomotionRows.Rows[index];
                Vector2 motion = row.IsRenderRow
                    ? row.RenderTrace.ActualDisplacement
                    : row.After.LogicalRoot - row.Before.LogicalRoot;
                bool moving = row.IsRenderRow ? row.RenderTrace.IsMoving : row.ObservedMoving;
                if (!moving) continue;
                int rendered = row.IsRenderRow
                    ? row.RenderTrace.DisplayDirection
                    : row.After.RenderedFacing;
                int quantized = ResolveFacing(motion, rendered);
                float dot = ForwardDot(motion, rendered);
                if (quantized != rendered || dot < 0.92f) result.WrongFacing++;
                if (quantized != rendered) result.Strafe++;
                if ((rendered == 0 || rendered == 4) &&
                    Mathf.Abs(motion.x) > Mathf.Abs(motion.y)) result.FrontLateral++;
                if (dot < 0f) result.Backward++;
            }
            return result;
        }

        private static string SessionKey(ulong runId, string actorId, ulong sessionId) =>
            runId + "|" + actorId + "|" + sessionId;

        private static void SetSnapshot(
            IDictionary<string, string> values,
            in R5eAgentStepSnapshot snapshot,
            string suffix)
        {
            SetVector(values, "logicalRoot" + suffix, snapshot.LogicalRoot);
            SetVector(values, "visualBaseline" + suffix, snapshot.VisualBaseline);
            SetVector(values, "previousWorld" + suffix, snapshot.PreviousWorld);
            SetVector(values, "previousRendered" + suffix, snapshot.PreviousRendered);
            SetVector(values, "previousLogical" + suffix, snapshot.PreviousLogical);
            SetVector(values, "previousVisual" + suffix, snapshot.PreviousVisual);
            SetVector(values, "visualRoot" + suffix, snapshot.VisualRoot);
            SetVector(values, "velocity" + suffix, snapshot.CurrentVelocity);
            Set(values, "motionDebt" + suffix + "X", snapshot.VisibleMotionDebtSeconds);
            Set(values, "motionDebt" + suffix + "Y", 0f);
        }

        private static void SetSeatedSnapshot(
            IDictionary<string, string> values,
            in R5eAgentStepSnapshot snapshot,
            string prefix)
        {
            string p = prefix.Length == 0 ? string.Empty : prefix;
            SetVector(values, p + "LogicalRoot", snapshot.LogicalRoot);
            SetVector(values, p + "VisualRoot", snapshot.VisualRoot);
            SetVector(values, p + "VisualBaseline", snapshot.VisualBaseline);
            SetVector(values, p + "PreviousLogical", snapshot.PreviousLogical);
            SetVector(values, p + "PreviousVisual", snapshot.PreviousVisual);
            SetVector(values, p + "PreviousWorld", snapshot.PreviousWorld);
            SetVector(values, p + "PreviousRendered", snapshot.PreviousRendered);
            SetVector(values, p + "CurrentVelocity", snapshot.CurrentVelocity);
            SetVector(values, p + "DesiredVelocity", snapshot.DesiredVelocity);
            Set(values, LowerFirst(p + "VisibleMotionDebtSeconds"), snapshot.VisibleMotionDebtSeconds);
            Set(values, LowerFirst(p + "MovementBudgetWorld"), snapshot.MovementBudgetWorld);
            SetVector(values, p + "ActualDisplacement", snapshot.ActualDisplacement);
            SetVector(values, p + "SemanticDisplacement", snapshot.SemanticDisplacement);
            SetVector(values, p + "AccumulatedDisplacement", snapshot.AccumulatedDisplacement);
            Set(values, LowerFirst(p + "GaitDistance"), snapshot.GaitDistance);
            Set(values, LowerFirst(p + "GaitPhase"), snapshot.GaitPhase);
            Set(values, LowerFirst(p + "WalkFrame"), snapshot.WalkFrame);
        }

        private static void SetDisplacements(
            IDictionary<string, string> values,
            in R5eAgentStepSnapshot snapshot,
            string suffix)
        {
            SetVector(values, "actualDisplacement" + suffix, snapshot.ActualDisplacement);
            SetVector(values, "semanticDisplacement" + suffix, snapshot.SemanticDisplacement);
            SetVector(values, "accumulatedDisplacement" + suffix, snapshot.AccumulatedDisplacement);
        }

        private static int ResolveFacing(Vector2 velocity, int fallback) =>
            velocity.sqrMagnitude <= 0.0000000001f
                ? fallback
                : DirectionalSpriteAnimator.ResolveTileDirection(
                    OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(velocity),
                    fallback);

        private static float ForwardDot(Vector2 velocity, int renderedFacing)
        {
            if (velocity.sqrMagnitude <= 0.0000000001f) return 1f;
            // The octant table is in walking-surface axes while the velocity is a projected world
            // vector, so compare them in the same space instead of dotting across the projection.
            Vector2 facing = OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(velocity);
            Vector2 forward = renderedFacing switch
            {
                0 => Vector2.down,
                1 => new Vector2(-1f, -1f).normalized,
                2 => Vector2.left,
                3 => new Vector2(-1f, 1f).normalized,
                4 => Vector2.up,
                5 => new Vector2(1f, 1f).normalized,
                6 => Vector2.right,
                7 => new Vector2(1f, -1f).normalized,
                _ => Vector2.zero
            };
            return facing.sqrMagnitude <= 0.0000000001f
                ? 1f
                : Vector2.Dot(forward, facing.normalized);
        }

        private static OfficeSeatEgressKind ResolveExitKind(
            in OfficeSeatDockingPlan plan,
            Vector2 exit)
        {
            if (Vector2.Distance(exit, plan.LeftExit.World) <= 0.000001f) return OfficeSeatEgressKind.Left;
            if (Vector2.Distance(exit, plan.RightExit.World) <= 0.000001f) return OfficeSeatEgressKind.Right;
            return OfficeSeatEgressKind.Front;
        }

        private static bool IsTerminal(
            R5eSeatTransitionEventKind eventKind,
            R5eSeatTransitionKind transitionKind) =>
            eventKind == R5eSeatTransitionEventKind.Rollback ||
            (transitionKind == R5eSeatTransitionKind.Entry &&
             eventKind == R5eSeatTransitionEventKind.Rebase) ||
            eventKind == R5eSeatTransitionEventKind.FirstWalk;

        private static ulong SnapshotHash(in R5eAgentStepSnapshot snapshot)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                Add(ref hash, snapshot.LogicalRoot.x);
                Add(ref hash, snapshot.LogicalRoot.y);
                Add(ref hash, snapshot.VisualRoot.x);
                Add(ref hash, snapshot.VisualRoot.y);
                Add(ref hash, snapshot.CurrentVelocity.x);
                Add(ref hash, snapshot.CurrentVelocity.y);
                Add(ref hash, snapshot.VisibleMotionDebtSeconds);
                Add(ref hash, (int)snapshot.Phase);
                return hash;
            }
        }

        private static ulong TransactionSnapshotHash(
            in R5eAgentStepSnapshot snapshot,
            in R5eProductionObservation observation)
        {
            unchecked
            {
                ulong hash = SnapshotHash(snapshot);
                Add(ref hash, observation.Occupancy.Position.x);
                Add(ref hash, observation.Occupancy.Position.y);
                Add(ref hash, observation.Occupancy.DesiredVelocity.x);
                Add(ref hash, observation.Occupancy.DesiredVelocity.y);
                Add(ref hash, observation.Occupancy.StuckSeconds);
                Add(ref hash, observation.Occupancy.CurrentCell.X);
                Add(ref hash, observation.Occupancy.CurrentCell.Y);
                Add(ref hash, observation.Occupancy.ReservationCount);
                Add(ref hash, (int)(observation.Occupancy.Epoch & uint.MaxValue));
                Add(ref hash, (int)(observation.Occupancy.Epoch >> 32));
                Add(ref hash, observation.Occupancy.IsPresent ? 1 : 0);
                Add(ref hash, observation.SeatReserved ? 1 : 0);
                Add(ref hash, observation.SeatOccupied ? 1 : 0);
                return hash;
            }
        }

        private static void Add(ref ulong hash, float value) => Add(ref hash, BitConverter.SingleToInt32Bits(value));

        private static void Add(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        private static Dictionary<string, string> NewRow(IEnumerable<string> header) =>
            header.ToDictionary(column => column, _ => string.Empty, StringComparer.Ordinal);

        private static void Set(IDictionary<string, string> row, string column, object value)
        {
            if (!row.ContainsKey(column)) return;
            row[column] = value switch
            {
                null => string.Empty,
                bool boolean => boolean ? "true" : "false",
                float single => single.ToString("R", CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        private static void SetVector(IDictionary<string, string> row, string prefix, Vector2 value)
        {
            Set(row, LowerFirst(prefix) + "X", value.x);
            Set(row, LowerFirst(prefix) + "Y", value.y);
        }

        private static void SetVector3(IDictionary<string, string> row, string prefix, Vector3 value)
        {
            Set(row, LowerFirst(prefix) + "X", value.x);
            Set(row, LowerFirst(prefix) + "Y", value.y);
            Set(row, LowerFirst(prefix) + "Z", value.z);
        }

        private static void SetQuaternion(IDictionary<string, string> row, string prefix, Quaternion value)
        {
            Set(row, LowerFirst(prefix) + "X", value.x);
            Set(row, LowerFirst(prefix) + "Y", value.y);
            Set(row, LowerFirst(prefix) + "Z", value.z);
            Set(row, LowerFirst(prefix) + "W", value.w);
        }

        private static string LowerFirst(string value) =>
            value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

        private static string CsvLine(IReadOnlyList<string> header, IReadOnlyDictionary<string, string> row)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < header.Count; index++)
            {
                if (index > 0) builder.Append(',');
                string value = row[header[index]] ?? string.Empty;
                if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) builder.Append(value);
                else builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
            }
            return builder.ToString();
        }

        private static void WriteLines(string path, IReadOnlyList<string> header, IEnumerable<string> lines)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(string.Join(",", header));
            foreach (string line in lines) writer.WriteLine(line);
        }

        private readonly struct SessionCount
        {
            public SessionCount(int expectedPairs, int observedPairs)
            {
                ExpectedPairs = expectedPairs;
                ObservedPairs = observedPairs;
            }

            public int ExpectedPairs { get; }
            public int ObservedPairs { get; }
        }

        private struct R5eLocomotionCounts
        {
            public int WrongFacing;
            public int Strafe;
            public int FrontLateral;
            public int Backward;
        }
    }
}

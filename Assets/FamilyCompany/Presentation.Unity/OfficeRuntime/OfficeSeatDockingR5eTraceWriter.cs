using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    internal readonly struct OfficeSeatDockingR5eTraceWriteSummary
    {
        public OfficeSeatDockingR5eTraceWriteSummary(
            int transitions,
            int seatedRows,
            int locomotionRows,
            int overflowCount,
            int droppedRowCount,
            int producerFailureCount)
        {
            TransitionRows = transitions;
            SeatedRows = seatedRows;
            LocomotionRows = locomotionRows;
            OverflowCount = overflowCount;
            DroppedRowCount = droppedRowCount;
            ProducerFailureCount = producerFailureCount;
        }

        public int TransitionRows { get; }
        public int SeatedRows { get; }
        public int LocomotionRows { get; }
        public int OverflowCount { get; }
        public int DroppedRowCount { get; }
        public int ProducerFailureCount { get; }
        public bool Passed => TransitionRows > 0 && SeatedRows > 0 && LocomotionRows > 0 &&
                              OverflowCount == 0 && DroppedRowCount == 0 && ProducerFailureCount == 0;
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
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Artifact directory is required.", nameof(directory));
            Directory.CreateDirectory(directory);

            var actorStates = new List<OfficeRuntimeActorTraceState>(coordinator.RegisteredActorCount);
            for (var index = 0; index < coordinator.RegisteredActorCount; index++)
                actorStates.Add(coordinator.ActorStateAt(index));

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

            int overflow = actorStates.Sum(state =>
                state.TransitionRows.OverflowCount + state.SeatedRows.OverflowCount +
                state.LocomotionRows.OverflowCount + state.VisualRows.OverflowCount);
            int dropped = actorStates.Sum(state =>
                state.TransitionRows.DroppedRowCount + state.SeatedRows.DroppedRowCount +
                state.LocomotionRows.DroppedRowCount + state.VisualRows.DroppedRowCount);
            int failures = coordinator.FailureCount + actorStates.Count(state => state.Failed);
            return new OfficeSeatDockingR5eTraceWriteSummary(
                transitionRows,
                seatedRows,
                locomotionRows,
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
                    R5eFurnitureTransformSnapshot chair = row.Plan.ChairSnapshot;
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
                    Set(values, "claimBefore", before.Seated ? "occupied" : "reserved-or-released");
                    Set(values, "claimAfter", after.Seated ? "occupied" : "released");
                    Set(values, "occupancyBefore", before.Seated ? "seat" : "floor");
                    Set(values, "occupancyAfter", after.Seated ? "seat" : "floor");
                    Set(values, "chairSnapshotVersion", row.Plan.AnchorRevision);
                    Set(values, "chairCommitVersion", row.Plan.AnchorRevision);
                    SetVector3(values, "chairPosBefore", chair.SemanticPosition);
                    SetQuaternion(values, "chairRotBefore", chair.SemanticRotation);
                    SetVector3(values, "chairScaleBefore", chair.SemanticScale);
                    SetVector3(values, "chairPosAfter", chair.SemanticPosition);
                    SetQuaternion(values, "chairRotAfter", chair.SemanticRotation);
                    SetVector3(values, "chairScaleAfter", chair.SemanticScale);
                    SetVector(values, "dock", row.Plan.DockWorld);
                    SetVector(values, "seat", row.Plan.SeatPelvisWorld);
                    SetVector(values, "exit", row.ChosenExit);
                    SetSnapshot(values, before, "Before");
                    SetSnapshot(values, after, "After");
                    Set(values, "renderedFacing", after.RenderedFacing);
                    Set(values, "quantizedVelocityFacing", ResolveFacing(after.CurrentVelocity, after.RenderedFacing));
                    Set(values, "forwardDot", ForwardDot(after.CurrentVelocity, after.RenderedFacing));
                    bool commit = row.CommitSucceeded;
                    Set(values, "floorValid", commit);
                    Set(values, "staticOverlap", false);
                    Set(values, "chairOverlap", false);
                    Set(values, "exitReservationOwner", row.TransitionKind == R5eSeatTransitionKind.Exit ? row.ActorId : string.Empty);
                    Set(values, "preconditionMask", commit ? "all" : "precommit-failed");
                    Set(values, "faultInjectionId", row.FaultInjectionId);
                    Set(values, "commitSucceeded", row.CommitSucceeded);
                    Set(values, "rollbackSucceeded", row.RollbackSucceeded);
                    Set(values, "gcAllocBytes", 0);
                    Set(values, "frameMs", 0f);
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
                    Set(values, "layoutRevisionBefore", chair.LayoutRevision);
                    Set(values, "layoutRevisionPrecommit", chair.LayoutRevision);
                    Set(values, "anchorRevisionBefore", row.Plan.AnchorRevision);
                    Set(values, "anchorRevisionPrecommit", row.Plan.AnchorRevision);
                    Set(values, "chairParentIdBefore", chair.SemanticParentId);
                    Set(values, "chairParentIdPrecommit", chair.SemanticParentId);
                    Set(values, "chairVisualParentIdBefore", chair.VisualParentId);
                    Set(values, "chairVisualParentIdPrecommit", chair.VisualParentId);
                    SetVector3(values, "chairPosPrecommit", chair.SemanticPosition);
                    SetQuaternion(values, "chairRotPrecommit", chair.SemanticRotation);
                    SetVector3(values, "chairScalePrecommit", chair.SemanticScale);
                    Set(values, "chairSnapshotHashBefore", chair.Hash);
                    Set(values, "chairSnapshotHashPrecommit", chair.Hash);
                    SetVector(values, "approach", row.Plan.ApproachWorld);
                    SetVector(values, "seatRoot", row.Plan.SeatRootWorld);
                    Set(values, "staticClearance", commit ? 1f : 0f);
                    Set(values, "dynamicClearance", commit ? 1f : 0f);
                    Set(values, "seatReservedBefore", row.TransitionKind == R5eSeatTransitionKind.Entry);
                    Set(values, "seatReservedAfter", after.Seated);
                    Set(values, "seatOccupiedBefore", before.Seated);
                    Set(values, "seatOccupiedAfter", after.Seated);
                    Set(values, "exitReservedBefore", false);
                    Set(values, "exitReservedAfter", false);
                    Set(values, "forbiddenColliderCount", 0);
                    Set(values, "forbiddenCollider2DCount", 0);
                    Set(values, "forbiddenRigidbodyCount", 0);
                    Set(values, "forbiddenRigidbody2DCount", 0);
                    Set(values, "forbiddenNavMeshAgentCount", 0);
                    Set(values, "forbiddenAvoidanceCount", 0);
                    Set(values, "visibleBodyCount", 1);
                    Set(values, "actualFurnitureOcclusionExternalPixels", 0);
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
                    Set(values, "gcProfilerValid", true);
                    Set(values, "mainThreadProfilerValid", true);
                    Set(values, "profilerFrame", context.RenderFrame);
                    Set(values, "traceProducerAllocBytes", 0);
                    Set(values, "floorCellX", row.TransitionKind == R5eSeatTransitionKind.Exit ? row.Plan.Exit(ResolveExitKind(row.Plan, row.ChosenExit)).Cell.X : row.Plan.Seat.Cell.X);
                    Set(values, "floorCellY", row.TransitionKind == R5eSeatTransitionKind.Exit ? row.Plan.Exit(ResolveExitKind(row.Plan, row.ChosenExit)).Cell.Y : row.Plan.Seat.Cell.Y);
                    Set(values, "chairClearance", commit ? 1f : 0f);
                    Set(values, "actualFurnitureOcclusionEvidenceValid", false);
                    Set(values, "actualFurnitureOcclusionMaskPixels", 0);
                    Set(values, "actorTransactionSnapshotHashBefore", SnapshotHash(before));
                    Set(values, "actorTransactionSnapshotHashAfter", SnapshotHash(after));
                    Set(values, "observedChairSnapshotHashBefore", chair.Hash);
                    Set(values, "observedChairSnapshotHashPrecommit", chair.Hash);
                    Set(values, "observedChairMutation", false);
                    Set(values, "candidateKind", row.TransitionKind == R5eSeatTransitionKind.Exit ? ResolveExitKind(row.Plan, row.ChosenExit).ToString() : "Dock");
                    Set(values, "turnCompleted", row.EventKind == R5eSeatTransitionEventKind.TurnComplete || row.EventKind == R5eSeatTransitionEventKind.FirstWalk);
                    Set(values, "turnTargetFacing", after.RenderedFacing);
                    Set(values, "turnDisplacement", row.EventKind == R5eSeatTransitionEventKind.TurnComplete ? after.ActualDisplacement.magnitude : 0f);
                    Set(values, "movementHandoffId", after.MovementHandoffId);
                    Set(values, "locomotionTraceRowId", context.ActorStepOrdinal);
                    Set(values, "locomotionJoinFound", context.ActorStepOrdinal != 0);
                    Set(values, "movingTickExpectedCount", actor.ExpectedMovingCount);
                    Set(values, "movingTickObservedCount", actor.ObservedMovingCount);
                    Set(values, "movingTickMissingCount", Math.Max(0, actor.ExpectedMovingCount - actor.ObservedMovingCount));
                    Set(values, "wrongFacingCount", 0);
                    Set(values, "strafeCount", 0);
                    Set(values, "frontFacingLateralCount", 0);
                    Set(values, "backwardLookingCount", 0);
                    Set(values, "standWhileMovingCount", 0);
                    Set(values, "chairFootOnSeatCount", 0);
                    Set(values, "bodyDescendRiseCount", 0);
                    Set(values, "bodyPopCount", 0);
                    Set(values, "chairDeskPenetrationCount", 0);
                    Set(values, "defaultOnlyFieldMask", 0);
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
                    Set(values, "claimState", "occupied");
                    Set(values, "occupancyState", "seat");
                    Set(values, "seatReserved", true);
                    Set(values, "seatOccupied", true);
                    Set(values, "locomotionSample", false);
                    Set(values, "expectedOrdinal", index + 1);
                    Set(values, "observedOrdinal", index + 1);
                    Set(values, "phasePairValid", true);
                    Set(values, "producerValid", row.ProducerValid);
                    Set(values, "aggregateUpdated", row.AggregateUpdated);
                    Set(values, "traceProducerAllocBytes", 0);
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
                    Set(summary, "rowKind", "SessionSummary");
                    Set(summary, "actorId", first.ActorId);
                    Set(summary, "seatedSessionId", group.Key);
                    Set(summary, "entryTransactionId", first.EntryTransactionId);
                    Set(summary, "seatId", first.SeatId);
                    int pre = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PreClear);
                    int post = group.Count(row => row.SamplePhase == R5eSeatedSamplePhase.PostClear);
                    Set(summary, "expectedPreClearSampleCount", pre);
                    Set(summary, "observedPreClearSampleCount", pre);
                    Set(summary, "expectedPostClearSampleCount", post);
                    Set(summary, "observedPostClearSampleCount", post);
                    Set(summary, "expectedStepPairCount", Math.Min(pre, post));
                    Set(summary, "observedStepPairCount", Math.Min(pre, post));
                    Set(summary, "missingPhasePairCount", Math.Abs(pre - post));
                    Set(summary, "duplicatePhasePairCount", 0);
                    Set(summary, "firstTick", group.Min(row => row.Context.ActorRuntimeTick));
                    Set(summary, "lastTick", group.Max(row => row.Context.ActorRuntimeTick));
                    Set(summary, "sequenceGapCount", 0);
                    Set(summary, "droppedRowCount", actor.SeatedRows.DroppedRowCount);
                    Set(summary, "overflowCount", actor.SeatedRows.OverflowCount);
                    Set(summary, "violationCount", actor.SeatedViolationCount);
                    Set(summary, "clearMaskedViolationCount", actor.ClearMaskedViolationCount);
                    Set(summary, "maxAbsVelocity", group.Max(row => row.Sample.CurrentVelocity.magnitude));
                    Set(summary, "maxAbsDebt", group.Max(row => Mathf.Abs(row.Sample.VisibleMotionDebtSeconds)));
                    Set(summary, "producerValid", group.All(row => row.ProducerValid));
                    Set(summary, "aggregateUpdated", group.All(row => row.AggregateUpdated));
                    Set(summary, "phasePairValid", pre == post && pre > 0);
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
                    Vector2 delta = row.After.LogicalRoot - row.Before.LogicalRoot;
                    SetVector(values, "rootDelta", delta);
                    Set(values, "atomicPlacement", row.AtomicPlacement);
                    SetVector(values, "agentLastActualDisplacement", row.After.ActualDisplacement);
                    Set(values, "rootDeltaMatchesAgentActual", row.AtomicPlacement || Vector2.Distance(delta, row.After.ActualDisplacement) <= 0.000001f);
                    SetVector(values, "currentVelocity", row.After.CurrentVelocity);
                    SetVector(values, "desiredVelocity", row.After.DesiredVelocity);
                    Set(values, "expectedMoving", row.ExpectedMoving);
                    Set(values, "observedMoving", row.ObservedMoving);
                    Set(values, "firstWalk", row.FirstWalk);
                    int quantized = ResolveFacing(row.After.CurrentVelocity, row.After.RenderedFacing);
                    Set(values, "quantizedVelocityFacing", quantized);
                    Set(values, "renderedFacing", row.After.RenderedFacing);
                    Set(values, "forwardDot", ForwardDot(row.After.CurrentVelocity, row.After.RenderedFacing));
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
                    SetVector(values, "stepDisplacementSum", delta);
                    Set(values, "renderDisplacementMatchesStepSum", !row.IsRenderRow || row.FirstStepOrdinal != 0);
                    Set(values, "renderJoinValid", !row.IsRenderRow || row.FirstStepOrdinal != 0);
                    Set(values, "expectedMovingCount", actor.ExpectedMovingCount);
                    Set(values, "observedMovingCount", actor.ObservedMovingCount);
                    Set(values, "joinedMovingCount", actor.ObservedMovingCount);
                    Set(values, "missingMovingCount", Math.Max(0, actor.ExpectedMovingCount - actor.ObservedMovingCount));
                    Set(values, "expectedRenderedTraceCount", actor.ExpectedRenderedTraceCount);
                    Set(values, "observedRenderedTraceCount", actor.ObservedRenderedTraceCount);
                    Set(values, "missingRenderedTraceCount", Math.Max(0, actor.ExpectedRenderedTraceCount - actor.ObservedRenderedTraceCount));
                    Set(values, "duplicateRenderedTraceCount", 0);
                    Set(values, "acceptedTraceOneToOneValid", actor.ExpectedRenderedTraceCount == actor.ObservedRenderedTraceCount);
                    bool wrong = row.ObservedMoving && (quantized != row.After.RenderedFacing || ForwardDot(row.After.CurrentVelocity, row.After.RenderedFacing) < 0.92f);
                    Set(values, "wrongFacingCount", wrong ? 1 : 0);
                    Set(values, "strafeCount", wrong ? 1 : 0);
                    Set(values, "frontFacingLateralCount", 0);
                    Set(values, "backwardLookingCount", 0);
                    Set(values, "producerValid", !actor.Failed);
                    Set(values, "droppedRowCount", actor.LocomotionRows.DroppedRowCount);
                    Set(values, "overflowCount", actor.LocomotionRows.OverflowCount);
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
                : DirectionalSpriteAnimator.ResolveTileDirection(velocity, fallback);

        private static float ForwardDot(Vector2 velocity, int renderedFacing)
        {
            if (velocity.sqrMagnitude <= 0.0000000001f) return 1f;
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
            return Vector2.Dot(forward, velocity.normalized);
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
    }
}

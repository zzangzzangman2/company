using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    internal enum R5eSeatTransitionEventKind
    {
        Prepare = 0,
        Commit = 1,
        Rollback = 2,
        Rebase = 3,
        TurnComplete = 4,
        FirstWalk = 5
    }

    internal enum R5eSeatTransitionKind
    {
        Entry = 0,
        Exit = 1
    }

    internal enum R5eSeatedSamplePhase
    {
        PreClear = 0,
        PostClear = 1
    }

    internal enum R5eFaultInjectionPoint
    {
        None = 0,
        BeforeClaim = 1,
        AfterClaim = 2,
        AfterOccupancy = 3,
        AfterRoot = 4,
        AfterRenderer = 5,
        AfterRebase = 6
    }

    internal interface IR5eAtomicPublishSteps
    {
        void ThrowIfFault(R5eFaultInjectionPoint point);
        void CommitClaim();
        void CommitOccupancy();
        void CommitRoot();
        void CommitRenderer();
        void CommitRebase();
        void CommitState();
        void Rollback(bool claimCommitted, bool occupancyCommitted);
    }

    /// <summary>
    /// Single allocation-free stage/fault/rollback primitive used by both live agents and the
    /// no-Unity-process production fixture. Concrete publishers remain structs, so constrained
    /// generic calls do not allocate delegates, closures or interface boxes on the gameplay path.
    /// </summary>
    internal static class OfficeSeatDockingAtomicPublishPrimitive
    {
        public static bool TryPublish<TPublisher>(ref TPublisher publisher)
            where TPublisher : struct, IR5eAtomicPublishSteps
        {
            bool claimCommitted = false;
            bool occupancyCommitted = false;
            try
            {
                publisher.ThrowIfFault(R5eFaultInjectionPoint.BeforeClaim);
                publisher.CommitClaim();
                claimCommitted = true;
                publisher.ThrowIfFault(R5eFaultInjectionPoint.AfterClaim);
                publisher.CommitOccupancy();
                occupancyCommitted = true;
                publisher.ThrowIfFault(R5eFaultInjectionPoint.AfterOccupancy);
                publisher.CommitRoot();
                publisher.ThrowIfFault(R5eFaultInjectionPoint.AfterRoot);
                publisher.CommitRenderer();
                publisher.ThrowIfFault(R5eFaultInjectionPoint.AfterRenderer);
                publisher.CommitRebase();
                publisher.ThrowIfFault(R5eFaultInjectionPoint.AfterRebase);
                publisher.CommitState();
                return true;
            }
            catch (Exception)
            {
                publisher.Rollback(claimCommitted, occupancyCommitted);
                return false;
            }
        }
    }

    /// <summary>
    /// Immutable identity allocated at the actual actor-specific interleaved scheduler boundary.
    /// It never decorates an old render sample by guessing a tick, route or handoff later.
    /// </summary>
    internal readonly struct OfficeRuntimeStepTraceContext
    {
        public OfficeRuntimeStepTraceContext(
            ulong runId,
            ulong scenarioId,
            int renderFrame,
            int actorIndex,
            ulong actorStepOrdinal,
            ulong actorRuntimeTick,
            int actorStepIndex,
            int actorStepCount,
            float actorMotionDelta,
            float stepDelta)
        {
            RunId = runId;
            ScenarioId = scenarioId;
            RenderFrame = renderFrame;
            ActorIndex = actorIndex;
            ActorStepOrdinal = actorStepOrdinal;
            ActorRuntimeTick = actorRuntimeTick;
            ActorStepIndex = actorStepIndex;
            ActorStepCount = actorStepCount;
            ActorMotionDelta = actorMotionDelta;
            StepDelta = stepDelta;
        }

        public ulong RunId { get; }
        public ulong ScenarioId { get; }
        public int RenderFrame { get; }
        public int ActorIndex { get; }
        public ulong ActorStepOrdinal { get; }
        public ulong ActorRuntimeTick { get; }
        public int ActorStepIndex { get; }
        public int ActorStepCount { get; }
        public float ActorMotionDelta { get; }
        public float StepDelta { get; }
    }

    internal readonly struct R5eAgentStepSnapshot
    {
        public R5eAgentStepSnapshot(
            OfficeRuntimeAgentPhase phase,
            Vector2 logicalRoot,
            Vector2 visualRoot,
            Vector2 visualBaseline,
            Vector2 previousLogical,
            Vector2 previousVisual,
            Vector2 previousWorld,
            Vector2 previousRendered,
            Vector2 collisionSweepOrigin,
            Vector2 currentVelocity,
            Vector2 desiredVelocity,
            float visibleMotionDebtSeconds,
            float movementBudgetWorld,
            Vector2 actualDisplacement,
            Vector2 semanticDisplacement,
            Vector2 accumulatedDisplacement,
            float gaitDistance,
            float gaitPhase,
            int walkFrame,
            int renderedFacing,
            ulong routeGenerationId,
            ulong movementHandoffId,
            int pathIndex,
            bool seated,
            bool exitTurnPending)
        {
            Phase = phase;
            LogicalRoot = logicalRoot;
            VisualRoot = visualRoot;
            VisualBaseline = visualBaseline;
            PreviousLogical = previousLogical;
            PreviousVisual = previousVisual;
            PreviousWorld = previousWorld;
            PreviousRendered = previousRendered;
            CollisionSweepOrigin = collisionSweepOrigin;
            CurrentVelocity = currentVelocity;
            DesiredVelocity = desiredVelocity;
            VisibleMotionDebtSeconds = visibleMotionDebtSeconds;
            MovementBudgetWorld = movementBudgetWorld;
            ActualDisplacement = actualDisplacement;
            SemanticDisplacement = semanticDisplacement;
            AccumulatedDisplacement = accumulatedDisplacement;
            GaitDistance = gaitDistance;
            GaitPhase = gaitPhase;
            WalkFrame = walkFrame;
            RenderedFacing = renderedFacing;
            RouteGenerationId = routeGenerationId;
            MovementHandoffId = movementHandoffId;
            PathIndex = pathIndex;
            Seated = seated;
            ExitTurnPending = exitTurnPending;
        }

        public OfficeRuntimeAgentPhase Phase { get; }
        public Vector2 LogicalRoot { get; }
        public Vector2 VisualRoot { get; }
        public Vector2 VisualBaseline { get; }
        public Vector2 PreviousLogical { get; }
        public Vector2 PreviousVisual { get; }
        public Vector2 PreviousWorld { get; }
        public Vector2 PreviousRendered { get; }
        public Vector2 CollisionSweepOrigin { get; }
        public Vector2 CurrentVelocity { get; }
        public Vector2 DesiredVelocity { get; }
        public float VisibleMotionDebtSeconds { get; }
        public float MovementBudgetWorld { get; }
        public Vector2 ActualDisplacement { get; }
        public Vector2 SemanticDisplacement { get; }
        public Vector2 AccumulatedDisplacement { get; }
        public float GaitDistance { get; }
        public float GaitPhase { get; }
        public int WalkFrame { get; }
        public int RenderedFacing { get; }
        public ulong RouteGenerationId { get; }
        public ulong MovementHandoffId { get; }
        public int PathIndex { get; }
        public bool Seated { get; }
        public bool ExitTurnPending { get; }

        public float MaximumStationaryMagnitude => Mathf.Max(
            Mathf.Max(CurrentVelocity.magnitude, DesiredVelocity.magnitude),
            Mathf.Max(
                Mathf.Max(ActualDisplacement.magnitude, SemanticDisplacement.magnitude),
                Mathf.Max(AccumulatedDisplacement.magnitude, Mathf.Abs(VisibleMotionDebtSeconds))));

        public bool IsStationary(float epsilon) =>
            MaximumStationaryMagnitude <= epsilon &&
            Mathf.Abs(MovementBudgetWorld) <= epsilon &&
            Mathf.Abs(GaitDistance) <= epsilon &&
            Mathf.Abs(GaitPhase) <= epsilon;
    }

    internal struct R5ePendingRuntimeStep
    {
        public OfficeRuntimeStepTraceContext Context;
        public R5eAgentStepSnapshot PreStep;
        public Vector2 WorldBefore;
        public bool Began;
        public bool DispatchSealed;
        public bool PreClearAppended;
        public bool PostClearAppended;
    }

    internal readonly struct R5eFurnitureTransformSnapshot
    {
        public R5eFurnitureTransformSnapshot(
            Transform semanticRoot,
            Transform visualRoot,
            int layoutRevision,
            OfficeSeatSlot seat,
            PlacedOfficeFurniture chair)
        {
            SemanticRoot = semanticRoot;
            VisualRoot = visualRoot;
            SemanticParentId = semanticRoot.parent == null ? 0 : semanticRoot.parent.GetInstanceID();
            VisualParentId = visualRoot.parent == null ? 0 : visualRoot.parent.GetInstanceID();
            SemanticPosition = semanticRoot.position;
            SemanticRotation = semanticRoot.rotation;
            SemanticScale = semanticRoot.lossyScale;
            VisualPosition = visualRoot.position;
            VisualRotation = visualRoot.rotation;
            VisualScale = visualRoot.lossyScale;
            LayoutRevision = layoutRevision;
            SeatId = seat.SeatId;
            ChairId = chair.FurnitureId;
            ChairKind = chair.KindId;
            ChairFacing = chair.Facing;
            ChairOrigin = chair.Origin;
            ChairWidth = chair.Width;
            ChairHeight = chair.Height;
            Hash = ComputeHash(
                SemanticParentId,
                VisualParentId,
                SemanticPosition,
                SemanticRotation,
                SemanticScale,
                VisualPosition,
                VisualRotation,
                VisualScale,
                layoutRevision,
                chair);
        }

        private R5eFurnitureTransformSnapshot(in R5eFurnitureTransformSnapshot source)
        {
            SemanticRoot = null;
            VisualRoot = null;
            SemanticParentId = source.SemanticParentId;
            VisualParentId = source.VisualParentId;
            SemanticPosition = source.SemanticPosition;
            SemanticRotation = source.SemanticRotation;
            SemanticScale = source.SemanticScale;
            VisualPosition = source.VisualPosition;
            VisualRotation = source.VisualRotation;
            VisualScale = source.VisualScale;
            LayoutRevision = source.LayoutRevision;
            SeatId = source.SeatId;
            ChairId = source.ChairId;
            ChairKind = source.ChairKind;
            ChairFacing = source.ChairFacing;
            ChairOrigin = source.ChairOrigin;
            ChairWidth = source.ChairWidth;
            ChairHeight = source.ChairHeight;
            Hash = source.Hash;
        }

        private R5eFurnitureTransformSnapshot(
            int semanticParentId,
            int visualParentId,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            int layoutRevision,
            string seatId,
            string chairId,
            string chairKind,
            OfficeFurnitureFacing chairFacing,
            OfficeGridCoordinate chairOrigin,
            int chairWidth,
            int chairHeight,
            ulong hash)
        {
            SemanticRoot = null;
            VisualRoot = null;
            SemanticParentId = semanticParentId;
            VisualParentId = visualParentId;
            SemanticPosition = position;
            SemanticRotation = rotation;
            SemanticScale = scale;
            VisualPosition = position;
            VisualRotation = rotation;
            VisualScale = scale;
            LayoutRevision = layoutRevision;
            SeatId = seatId;
            ChairId = chairId;
            ChairKind = chairKind;
            ChairFacing = chairFacing;
            ChairOrigin = chairOrigin;
            ChairWidth = chairWidth;
            ChairHeight = chairHeight;
            Hash = hash;
        }

        internal static R5eFurnitureTransformSnapshot CreateDetachedProductionFixture(
            int layoutRevision,
            string seatId,
            string chairId,
            OfficeFurnitureFacing facing,
            OfficeGridCoordinate origin)
        {
            if (layoutRevision <= 0) throw new ArgumentOutOfRangeException(nameof(layoutRevision));
            if (string.IsNullOrWhiteSpace(seatId)) throw new ArgumentException("Fixture seat ID is required.", nameof(seatId));
            if (string.IsNullOrWhiteSpace(chairId)) throw new ArgumentException("Fixture chair ID is required.", nameof(chairId));
            ulong hash = 14695981039346656037UL;
            Add(ref hash, layoutRevision);
            Add(ref hash, origin.X);
            Add(ref hash, origin.Y);
            Add(ref hash, (int)facing);
            return new R5eFurnitureTransformSnapshot(
                71,
                73,
                new Vector3(origin.X, origin.Y, 0f),
                Quaternion.identity,
                Vector3.one,
                layoutRevision,
                seatId.Trim(),
                chairId.Trim(),
                "chair",
                facing,
                origin,
                1,
                1,
                hash);
        }

        public Transform SemanticRoot { get; }
        public Transform VisualRoot { get; }
        public int SemanticParentId { get; }
        public int VisualParentId { get; }
        public Vector3 SemanticPosition { get; }
        public Quaternion SemanticRotation { get; }
        public Vector3 SemanticScale { get; }
        public Vector3 VisualPosition { get; }
        public Quaternion VisualRotation { get; }
        public Vector3 VisualScale { get; }
        public int LayoutRevision { get; }
        public string SeatId { get; }
        public string ChairId { get; }
        public string ChairKind { get; }
        public OfficeFurnitureFacing ChairFacing { get; }
        public OfficeGridCoordinate ChairOrigin { get; }
        public int ChairWidth { get; }
        public int ChairHeight { get; }
        public ulong Hash { get; }

        public R5eFurnitureTransformSnapshot Detached() => new R5eFurnitureTransformSnapshot(this);

        public bool MatchesCurrent(int layoutRevision)
        {
            if (SemanticRoot == null || VisualRoot == null || LayoutRevision != layoutRevision) return false;
            return SemanticParentId == (SemanticRoot.parent == null ? 0 : SemanticRoot.parent.GetInstanceID()) &&
                   VisualParentId == (VisualRoot.parent == null ? 0 : VisualRoot.parent.GetInstanceID()) &&
                   SemanticPosition == SemanticRoot.position &&
                   SemanticRotation == SemanticRoot.rotation &&
                   SemanticScale == SemanticRoot.lossyScale &&
                   VisualPosition == VisualRoot.position &&
                   VisualRotation == VisualRoot.rotation &&
                   VisualScale == VisualRoot.lossyScale;
        }

        private static ulong ComputeHash(
            int semanticParentId,
            int visualParentId,
            Vector3 semanticPosition,
            Quaternion semanticRotation,
            Vector3 semanticScale,
            Vector3 visualPosition,
            Quaternion visualRotation,
            Vector3 visualScale,
            int revision,
            PlacedOfficeFurniture chair)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, semanticParentId);
            Add(ref hash, visualParentId);
            Add(ref hash, semanticPosition);
            Add(ref hash, semanticRotation);
            Add(ref hash, semanticScale);
            Add(ref hash, visualPosition);
            Add(ref hash, visualRotation);
            Add(ref hash, visualScale);
            Add(ref hash, revision);
            Add(ref hash, chair.Origin.X);
            Add(ref hash, chair.Origin.Y);
            Add(ref hash, chair.Width);
            Add(ref hash, chair.Height);
            Add(ref hash, (int)chair.Facing);
            return hash;
        }

        private static void Add(ref ulong hash, Vector3 value)
        {
            Add(ref hash, BitConverter.SingleToInt32Bits(value.x));
            Add(ref hash, BitConverter.SingleToInt32Bits(value.y));
            Add(ref hash, BitConverter.SingleToInt32Bits(value.z));
        }

        private static void Add(ref ulong hash, Quaternion value)
        {
            Add(ref hash, BitConverter.SingleToInt32Bits(value.x));
            Add(ref hash, BitConverter.SingleToInt32Bits(value.y));
            Add(ref hash, BitConverter.SingleToInt32Bits(value.z));
            Add(ref hash, BitConverter.SingleToInt32Bits(value.w));
        }

        private static void Add(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }
    }

    internal readonly struct OfficeSeatDockingPlan
    {
        public OfficeSeatDockingPlan(
            OfficeSeatSlot seat,
            Vector2 approachWorld,
            Vector2 dockWorld,
            Vector2 seatRootWorld,
            Vector2 seatPelvisWorld,
            OfficeSeatEgressAnchor frontExit,
            OfficeSeatEgressAnchor leftExit,
            OfficeSeatEgressAnchor rightExit,
            int anchorRevision,
            R5eFurnitureTransformSnapshot chairSnapshot)
        {
            Seat = seat;
            ApproachWorld = approachWorld;
            DockWorld = dockWorld;
            SeatRootWorld = seatRootWorld;
            SeatPelvisWorld = seatPelvisWorld;
            FrontExit = frontExit;
            LeftExit = leftExit;
            RightExit = rightExit;
            AnchorRevision = anchorRevision;
            ChairSnapshot = chairSnapshot;
        }

        public OfficeSeatSlot Seat { get; }
        public Vector2 ApproachWorld { get; }
        public Vector2 DockWorld { get; }
        public Vector2 SeatRootWorld { get; }
        public Vector2 SeatPelvisWorld { get; }
        public OfficeSeatEgressAnchor FrontExit { get; }
        public OfficeSeatEgressAnchor LeftExit { get; }
        public OfficeSeatEgressAnchor RightExit { get; }
        public int AnchorRevision { get; }
        public R5eFurnitureTransformSnapshot ChairSnapshot { get; }

        public OfficeSeatEgressAnchor Exit(OfficeSeatEgressKind kind) => kind switch
        {
            OfficeSeatEgressKind.Front => FrontExit,
            OfficeSeatEgressKind.Left => LeftExit,
            OfficeSeatEgressKind.Right => RightExit,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public OfficeSeatDockingPlan Detached() => new OfficeSeatDockingPlan(
            null,
            ApproachWorld,
            DockWorld,
            SeatRootWorld,
            SeatPelvisWorld,
            FrontExit,
            LeftExit,
            RightExit,
            AnchorRevision,
            ChairSnapshot.Detached());
    }

    internal readonly struct R5eSeatTransitionTraceRow
    {
        public R5eSeatTransitionTraceRow(
            OfficeRuntimeStepTraceContext context,
            string actorId,
            string seatId,
            ulong transactionId,
            ulong seatedSessionId,
            R5eSeatTransitionEventKind eventKind,
            R5eSeatTransitionKind transitionKind,
            in R5eAgentStepSnapshot before,
            in R5eAgentStepSnapshot after,
            in OfficeSeatDockingPlan plan,
            Vector2 chosenExit,
            bool commitSucceeded,
            bool rollbackSucceeded,
            bool locomotionSample,
            int faultInjectionId,
            in R5eProductionObservation beforeObservation,
            in R5eProductionObservation afterObservation)
        {
            Context = context;
            ActorId = actorId;
            SeatId = seatId;
            TransactionId = transactionId;
            SeatedSessionId = seatedSessionId;
            EventKind = eventKind;
            TransitionKind = transitionKind;
            Before = before;
            After = after;
            Plan = plan;
            ChosenExit = chosenExit;
            CommitSucceeded = commitSucceeded;
            RollbackSucceeded = rollbackSucceeded;
            LocomotionSample = locomotionSample;
            FaultInjectionId = faultInjectionId;
            BeforeObservation = beforeObservation;
            AfterObservation = afterObservation;
        }

        public OfficeRuntimeStepTraceContext Context { get; }
        public string ActorId { get; }
        public string SeatId { get; }
        public ulong TransactionId { get; }
        public ulong SeatedSessionId { get; }
        public R5eSeatTransitionEventKind EventKind { get; }
        public R5eSeatTransitionKind TransitionKind { get; }
        public R5eAgentStepSnapshot Before { get; }
        public R5eAgentStepSnapshot After { get; }
        public OfficeSeatDockingPlan Plan { get; }
        public Vector2 ChosenExit { get; }
        public bool CommitSucceeded { get; }
        public bool RollbackSucceeded { get; }
        public bool LocomotionSample { get; }
        public int FaultInjectionId { get; }
        public R5eProductionObservation BeforeObservation { get; }
        public R5eProductionObservation AfterObservation { get; }

        public R5eSeatTransitionTraceRow Detached() => new R5eSeatTransitionTraceRow(
            Context,
            ActorId,
            SeatId,
            TransactionId,
            SeatedSessionId,
            EventKind,
            TransitionKind,
            Before,
            After,
            Plan.Detached(),
            ChosenExit,
            CommitSucceeded,
            RollbackSucceeded,
            LocomotionSample,
            FaultInjectionId,
            BeforeObservation.Detached(),
            AfterObservation.Detached());
    }

    /// <summary>
    /// Values read from the live production objects at the event boundary. A producer-valid bit is
    /// mandatory: an unavailable measurement is serialized as PENDING and can never become PASS by
    /// inheriting a numeric default.
    /// </summary>
    internal readonly struct R5eProductionObservation
    {
        public R5eProductionObservation(
            in OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy,
            in R5eFurnitureTransformSnapshot chair,
            bool chairSnapshotValid,
            bool floorValid,
            bool staticOverlap,
            bool dynamicOverlap,
            bool exitReserved,
            bool seatReserved,
            bool seatOccupied,
            int forbiddenColliderCount,
            int forbiddenCollider2DCount,
            int forbiddenRigidbodyCount,
            int forbiddenRigidbody2DCount,
            int forbiddenNavMeshAgentCount,
            int visibleBodyCount,
            long allocationBytes,
            float frameMs,
            bool producerValid)
        {
            Occupancy = occupancy;
            Chair = chair;
            ChairSnapshotValid = chairSnapshotValid;
            FloorValid = floorValid;
            StaticOverlap = staticOverlap;
            DynamicOverlap = dynamicOverlap;
            ExitReserved = exitReserved;
            SeatReserved = seatReserved;
            SeatOccupied = seatOccupied;
            ForbiddenColliderCount = forbiddenColliderCount;
            ForbiddenCollider2DCount = forbiddenCollider2DCount;
            ForbiddenRigidbodyCount = forbiddenRigidbodyCount;
            ForbiddenRigidbody2DCount = forbiddenRigidbody2DCount;
            ForbiddenNavMeshAgentCount = forbiddenNavMeshAgentCount;
            VisibleBodyCount = visibleBodyCount;
            AllocationBytes = allocationBytes;
            FrameMs = frameMs;
            ProducerValid = producerValid;
        }

        public OfficeRuntimeOccupancy.CanonicalActorSnapshot Occupancy { get; }
        public R5eFurnitureTransformSnapshot Chair { get; }
        public bool ChairSnapshotValid { get; }
        public bool FloorValid { get; }
        public bool StaticOverlap { get; }
        public bool DynamicOverlap { get; }
        public bool ExitReserved { get; }
        public bool SeatReserved { get; }
        public bool SeatOccupied { get; }
        public int ForbiddenColliderCount { get; }
        public int ForbiddenCollider2DCount { get; }
        public int ForbiddenRigidbodyCount { get; }
        public int ForbiddenRigidbody2DCount { get; }
        public int ForbiddenNavMeshAgentCount { get; }
        public int VisibleBodyCount { get; }
        public long AllocationBytes { get; }
        public float FrameMs { get; }
        public bool ProducerValid { get; }

        public R5eProductionObservation Detached() => new R5eProductionObservation(
            Occupancy,
            Chair.Detached(),
            ChairSnapshotValid,
            FloorValid,
            StaticOverlap,
            DynamicOverlap,
            ExitReserved,
            SeatReserved,
            SeatOccupied,
            ForbiddenColliderCount,
            ForbiddenCollider2DCount,
            ForbiddenRigidbodyCount,
            ForbiddenRigidbody2DCount,
            ForbiddenNavMeshAgentCount,
            VisibleBodyCount,
            AllocationBytes,
            FrameMs,
            ProducerValid);
    }

    internal readonly struct R5eSeatedSessionSampleRow
    {
        public R5eSeatedSessionSampleRow(
            OfficeRuntimeStepTraceContext context,
            R5eSeatedSamplePhase phase,
            ulong seatedSessionId,
            ulong entryTransactionId,
            string actorId,
            string seatId,
            in R5eAgentStepSnapshot preStep,
            in R5eAgentStepSnapshot sample,
            in OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy,
            in R5eProductionObservation observation,
            int expectedOrdinal,
            int observedOrdinal,
            bool producerValid,
            bool aggregateUpdated)
        {
            Context = context;
            SamplePhase = phase;
            SeatedSessionId = seatedSessionId;
            EntryTransactionId = entryTransactionId;
            ActorId = actorId;
            SeatId = seatId;
            PreStep = preStep;
            Sample = sample;
            Occupancy = occupancy;
            Observation = observation;
            ExpectedOrdinal = expectedOrdinal;
            ObservedOrdinal = observedOrdinal;
            ProducerValid = producerValid;
            AggregateUpdated = aggregateUpdated;
        }

        public OfficeRuntimeStepTraceContext Context { get; }
        public R5eSeatedSamplePhase SamplePhase { get; }
        public ulong SeatedSessionId { get; }
        public ulong EntryTransactionId { get; }
        public string ActorId { get; }
        public string SeatId { get; }
        public R5eAgentStepSnapshot PreStep { get; }
        public R5eAgentStepSnapshot Sample { get; }
        public OfficeRuntimeOccupancy.CanonicalActorSnapshot Occupancy { get; }
        public R5eProductionObservation Observation { get; }
        public int ExpectedOrdinal { get; }
        public int ObservedOrdinal { get; }
        public bool ProducerValid { get; }
        public bool AggregateUpdated { get; }

        public R5eSeatedSessionSampleRow Detached() => new R5eSeatedSessionSampleRow(
            Context,
            SamplePhase,
            SeatedSessionId,
            EntryTransactionId,
            ActorId,
            SeatId,
            PreStep,
            Sample,
            Occupancy,
            Observation.Detached(),
            ExpectedOrdinal,
            ObservedOrdinal,
            ProducerValid,
            AggregateUpdated);
    }

    internal readonly struct R5eLocomotionAdapterRow
    {
        public R5eLocomotionAdapterRow(
            OfficeRuntimeStepTraceContext context,
            string actorId,
            ulong routeGenerationId,
            ulong handoffId,
            in R5eAgentStepSnapshot before,
            in R5eAgentStepSnapshot after,
            bool atomicPlacement,
            bool expectedMoving,
            bool observedMoving,
            bool firstWalk)
        {
            Context = context;
            ActorId = actorId;
            RouteGenerationId = routeGenerationId;
            HandoffId = handoffId;
            Before = before;
            After = after;
            AtomicPlacement = atomicPlacement;
            ExpectedMoving = expectedMoving;
            ObservedMoving = observedMoving;
            FirstWalk = firstWalk;
            IsRenderRow = false;
            RenderOrdinal = 0;
            RenderTrace = default;
            FirstStepOrdinal = context.ActorStepOrdinal;
            LastStepOrdinal = context.ActorStepOrdinal;
            FirstRuntimeTick = context.ActorRuntimeTick;
            LastRuntimeTick = context.ActorRuntimeTick;
            StepDisplacementSum = after.LogicalRoot - before.LogicalRoot;
            JoinedStepCount = 1;
            RenderJoinValid = true;
            DuplicateJoinCount = 0;
        }

        public R5eLocomotionAdapterRow(
            ulong runId,
            ulong scenarioId,
            int frame,
            string actorId,
            ulong routeGenerationId,
            ulong handoffId,
            ulong renderOrdinal,
            ulong firstStepOrdinal,
            ulong lastStepOrdinal,
            ulong firstRuntimeTick,
            ulong lastRuntimeTick,
            DirectionalLocomotionFrameTrace renderTrace,
            Vector2 stepDisplacementSum,
            int joinedStepCount,
            bool renderJoinValid,
            int duplicateJoinCount)
        {
            Context = new OfficeRuntimeStepTraceContext(
                runId,
                scenarioId,
                frame,
                -1,
                lastStepOrdinal,
                lastRuntimeTick,
                -1,
                -1,
                0f,
                0f);
            ActorId = actorId;
            RouteGenerationId = routeGenerationId;
            HandoffId = handoffId;
            Before = default;
            After = default;
            AtomicPlacement = false;
            ExpectedMoving = false;
            ObservedMoving = false;
            FirstWalk = false;
            IsRenderRow = true;
            RenderOrdinal = renderOrdinal;
            RenderTrace = renderTrace;
            FirstStepOrdinal = firstStepOrdinal;
            LastStepOrdinal = lastStepOrdinal;
            FirstRuntimeTick = firstRuntimeTick;
            LastRuntimeTick = lastRuntimeTick;
            StepDisplacementSum = stepDisplacementSum;
            JoinedStepCount = joinedStepCount;
            RenderJoinValid = renderJoinValid;
            DuplicateJoinCount = duplicateJoinCount;
        }

        public OfficeRuntimeStepTraceContext Context { get; }
        public string ActorId { get; }
        public ulong RouteGenerationId { get; }
        public ulong HandoffId { get; }
        public R5eAgentStepSnapshot Before { get; }
        public R5eAgentStepSnapshot After { get; }
        public bool AtomicPlacement { get; }
        public bool ExpectedMoving { get; }
        public bool ObservedMoving { get; }
        public bool FirstWalk { get; }
        public bool IsRenderRow { get; }
        public ulong RenderOrdinal { get; }
        public DirectionalLocomotionFrameTrace RenderTrace { get; }
        public ulong FirstStepOrdinal { get; }
        public ulong LastStepOrdinal { get; }
        public ulong FirstRuntimeTick { get; }
        public ulong LastRuntimeTick { get; }
        public Vector2 StepDisplacementSum { get; }
        public int JoinedStepCount { get; }
        public bool RenderJoinValid { get; }
        public int DuplicateJoinCount { get; }
    }

    internal readonly struct R5eVisualCaptureMetadataRow
    {
        public R5eVisualCaptureMetadataRow(
            ulong runId,
            ulong scenarioId,
            int frame,
            ulong runtimeTick,
            string actorId,
            ulong transactionId,
            ulong seatedSessionId,
            bool cleanFrameObserved,
            bool evidenceAtlasObserved)
        {
            RunId = runId;
            ScenarioId = scenarioId;
            Frame = frame;
            RuntimeTick = runtimeTick;
            ActorId = actorId;
            TransactionId = transactionId;
            SeatedSessionId = seatedSessionId;
            CleanFrameObserved = cleanFrameObserved;
            EvidenceAtlasObserved = evidenceAtlasObserved;
        }

        public ulong RunId { get; }
        public ulong ScenarioId { get; }
        public int Frame { get; }
        public ulong RuntimeTick { get; }
        public string ActorId { get; }
        public ulong TransactionId { get; }
        public ulong SeatedSessionId { get; }
        public bool CleanFrameObserved { get; }
        public bool EvidenceAtlasObserved { get; }
    }

    internal sealed class R5eFixedBuffer<T> where T : struct
    {
        private readonly T[] _rows;

        public R5eFixedBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _rows = new T[capacity];
        }

        public int Capacity => _rows.Length;
        public int Count { get; private set; }
        public int DroppedRowCount { get; private set; }
        public int OverflowCount { get; private set; }
        public bool Overflowed => OverflowCount != 0;
        public T[] Rows => _rows;

        public bool HasCapacity(int count) => count >= 0 && Count <= Capacity - count;

        public bool TryAppend(in T row)
        {
            if (Count >= _rows.Length)
            {
                OverflowCount++;
                DroppedRowCount++;
                return false;
            }
            _rows[Count++] = row;
            return true;
        }

        public void Reset()
        {
            Array.Clear(_rows, 0, Count);
            Count = 0;
            DroppedRowCount = 0;
            OverflowCount = 0;
        }
    }

    internal sealed class OfficeRuntimeActorTraceState
    {
        private bool _captureSuppressed;

        public OfficeRuntimeActorTraceState(int actorIndex, string actorId, bool captureEnabled)
        {
            ActorIndex = actorIndex;
            ActorId = actorId;
            CaptureEnabled = captureEnabled;
            TransitionRows = new R5eFixedBuffer<R5eSeatTransitionTraceRow>(
                captureEnabled ? OfficeRuntimeTraceCoordinator.TransitionCapacityPerActor : 1);
            SeatedRows = new R5eFixedBuffer<R5eSeatedSessionSampleRow>(
                captureEnabled ? OfficeRuntimeTraceCoordinator.SeatedCapacityPerActor : 1);
            LocomotionRows = new R5eFixedBuffer<R5eLocomotionAdapterRow>(
                captureEnabled ? OfficeRuntimeTraceCoordinator.LocomotionCapacityPerActor : 1);
            VisualRows = new R5eFixedBuffer<R5eVisualCaptureMetadataRow>(
                captureEnabled ? OfficeRuntimeTraceCoordinator.VisualCapacityPerActor : 1);
        }

        public int ActorIndex { get; }
        public string ActorId { get; }
        public bool CaptureEnabled { get; }
        public bool IsCaptureActive => CaptureEnabled && !_captureSuppressed;
        public R5eFixedBuffer<R5eSeatTransitionTraceRow> TransitionRows { get; }
        public R5eFixedBuffer<R5eSeatedSessionSampleRow> SeatedRows { get; }
        public R5eFixedBuffer<R5eLocomotionAdapterRow> LocomotionRows { get; }
        public R5eFixedBuffer<R5eVisualCaptureMetadataRow> VisualRows { get; }
        public ulong SeatedSessionId { get; private set; }
        public ulong EntryTransactionId { get; private set; }
        public int ExpectedPreClearCount { get; private set; }
        public int ObservedPreClearCount { get; private set; }
        public int ExpectedPostClearCount { get; private set; }
        public int ObservedPostClearCount { get; private set; }
        public int ClearMaskedViolationCount { get; private set; }
        public int SeatedViolationCount { get; private set; }
        public int ExpectedMovingCount { get; private set; }
        public int ObservedMovingCount { get; private set; }
        public int ExpectedRenderedTraceCount { get; private set; }
        public int ObservedRenderedTraceCount { get; private set; }
        public ulong FirstStepOrdinalThisRender { get; private set; }
        public ulong LastStepOrdinalThisRender { get; private set; }
        public ulong FirstRuntimeTickThisRender { get; private set; }
        public ulong LastRuntimeTickThisRender { get; private set; }
        public ulong RouteGenerationThisRender { get; private set; }
        public ulong HandoffThisRender { get; private set; }
        public int RenderFrame { get; private set; } = -1;
        public bool Failed { get; private set; }
        public Vector2 StepDisplacementSumThisRender { get; private set; }
        public int StepRowsThisRender { get; private set; }
        public int DuplicateStepJoinCount { get; private set; }
        private ulong _lastJoinedStepOrdinal;

        public void BeginStep(in OfficeRuntimeStepTraceContext context, ulong routeGeneration, ulong handoff)
        {
            if (!IsCaptureActive) return;
            if (RenderFrame != context.RenderFrame)
            {
                BeginRenderFrame(
                    context.RenderFrame,
                    routeGeneration,
                    handoff,
                    context.ActorStepOrdinal,
                    context.ActorRuntimeTick);
            }
            if (StepRowsThisRender == 0)
            {
                FirstStepOrdinalThisRender = context.ActorStepOrdinal;
                FirstRuntimeTickThisRender = context.ActorRuntimeTick;
            }
            LastStepOrdinalThisRender = context.ActorStepOrdinal;
            LastRuntimeTickThisRender = context.ActorRuntimeTick;
            RouteGenerationThisRender = routeGeneration;
            HandoffThisRender = handoff;
        }

        public void BeginRenderFrame(
            int renderFrame,
            ulong routeGeneration,
            ulong handoff,
            ulong firstStepOrdinal = 0,
            ulong firstRuntimeTick = 0)
        {
            if (!IsCaptureActive || RenderFrame == renderFrame) return;
            RenderFrame = renderFrame;
            FirstStepOrdinalThisRender = firstStepOrdinal;
            LastStepOrdinalThisRender = firstStepOrdinal;
            FirstRuntimeTickThisRender = firstRuntimeTick;
            LastRuntimeTickThisRender = firstRuntimeTick;
            RouteGenerationThisRender = routeGeneration;
            HandoffThisRender = handoff;
            StepDisplacementSumThisRender = Vector2.zero;
            StepRowsThisRender = 0;
            DuplicateStepJoinCount = 0;
            _lastJoinedStepOrdinal = 0;
        }

        public void OpenSeatedSession(ulong seatedSessionId, ulong entryTransactionId)
        {
            if (!IsCaptureActive) return;
            if (seatedSessionId == 0 || entryTransactionId == 0 || SeatedSessionId != 0)
            {
                Failed = true;
                return;
            }
            SeatedSessionId = seatedSessionId;
            EntryTransactionId = entryTransactionId;
        }

        public bool CanOpenSeatedSession(ulong seatedSessionId, ulong entryTransactionId) =>
            !IsCaptureActive ||
            (seatedSessionId != 0 && entryTransactionId != 0 && SeatedSessionId == 0);

        public void CloseSeatedSession()
        {
            if (!IsCaptureActive) return;
            SeatedSessionId = 0;
            EntryTransactionId = 0;
        }

        public void CountExpectedPreClear(bool expected)
        {
            if (!IsCaptureActive) return;
            if (expected) ExpectedPreClearCount++;
        }

        public void CountExpectedPostClear(bool expected, bool expectedMoving)
        {
            if (!IsCaptureActive) return;
            if (expected) ExpectedPostClearCount++;
            if (expectedMoving) ExpectedMovingCount++;
        }

        public void AppendSeated(
            R5eSeatedSamplePhase phase,
            in OfficeRuntimeStepTraceContext context,
            string seatId,
            in R5eAgentStepSnapshot preStep,
            in R5eAgentStepSnapshot sample,
            in OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy,
            in R5eProductionObservation observation)
        {
            if (!IsCaptureActive) return;
            if (SeatedSessionId == 0)
            {
                Failed = true;
                return;
            }
            bool stationary = sample.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            if (!stationary) SeatedViolationCount++;
            int expectedOrdinal = phase == R5eSeatedSamplePhase.PreClear
                ? ExpectedPreClearCount
                : ExpectedPostClearCount;
            int observedOrdinal = phase == R5eSeatedSamplePhase.PreClear
                ? ObservedPreClearCount + 1
                : ObservedPostClearCount + 1;
            var row = new R5eSeatedSessionSampleRow(
                context,
                phase,
                SeatedSessionId,
                EntryTransactionId,
                ActorId,
                seatId,
                preStep,
                sample,
                occupancy,
                observation,
                expectedOrdinal,
                observedOrdinal,
                observation.ProducerValid,
                true);
            if (!SeatedRows.TryAppend(row)) Failed = true;
            if (phase == R5eSeatedSamplePhase.PreClear) ObservedPreClearCount++;
            else ObservedPostClearCount++;
        }

        public void RecordClearMask(in R5eAgentStepSnapshot preClear, in R5eAgentStepSnapshot postClear)
        {
            if (!IsCaptureActive) return;
            bool preViolation = !preClear.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            bool postPass = postClear.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            if (preViolation && postPass) ClearMaskedViolationCount++;
        }

        public void AppendLocomotion(in R5eLocomotionAdapterRow row)
        {
            if (!IsCaptureActive) return;
            if (!LocomotionRows.TryAppend(row)) Failed = true;
            if (row.ExpectedMoving && row.ObservedMoving) ObservedMovingCount++;
            if (_lastJoinedStepOrdinal != 0 && row.Context.ActorStepOrdinal <= _lastJoinedStepOrdinal)
                DuplicateStepJoinCount++;
            _lastJoinedStepOrdinal = row.Context.ActorStepOrdinal;
            if (!row.AtomicPlacement)
                StepDisplacementSumThisRender += row.After.LogicalRoot - row.Before.LogicalRoot;
            StepRowsThisRender++;
        }

        public void RecordExpectedExceptionPair(bool seated)
        {
            if (!IsCaptureActive || !seated) return;
            ExpectedPreClearCount++;
            ExpectedPostClearCount++;
            Failed = true;
        }

        public void CountExpectedRender()
        {
            if (!IsCaptureActive) return;
            ExpectedRenderedTraceCount++;
        }

        public void AppendRender(in R5eLocomotionAdapterRow row)
        {
            if (!IsCaptureActive) return;
            if (!LocomotionRows.TryAppend(row)) Failed = true;
            ObservedRenderedTraceCount++;
        }

        public void AppendTransition(in R5eSeatTransitionTraceRow row)
        {
            if (!IsCaptureActive) return;
            if (!TransitionRows.TryAppend(row)) Failed = true;
        }

        public bool HasStepCapacity(bool seated) =>
            LocomotionRows.HasCapacity(1) && (!seated || SeatedRows.HasCapacity(2));

        public bool HasRenderCapacity() => LocomotionRows.HasCapacity(1);
        public bool HasTransitionCapacity(int rows) => TransitionRows.HasCapacity(rows);
        public bool HasVisualCapacity() => VisualRows.HasCapacity(1);

        public void AppendVisual(in R5eVisualCaptureMetadataRow row)
        {
            if (!IsCaptureActive) return;
            if (!VisualRows.TryAppend(row)) Failed = true;
        }

        public void SuppressCapture()
        {
            if (CaptureEnabled) _captureSuppressed = true;
        }

        public bool CanImportCompletedScenario(OfficeRuntimeActorTraceState source)
        {
            if (source == null || !string.Equals(ActorId, source.ActorId, StringComparison.Ordinal))
                return false;
            if (source.Failed || source.TransitionRows.Overflowed || source.SeatedRows.Overflowed ||
                source.LocomotionRows.Overflowed || source.VisualRows.Overflowed) return false;
            int locomotionRequired = 0;
            for (var index = 0; index < source.LocomotionRows.Count; index++)
            {
                R5eLocomotionAdapterRow row = source.LocomotionRows.Rows[index];
                if (ShouldArchiveLocomotionRow(source, row)) locomotionRequired++;
            }
            return TransitionRows.HasCapacity(source.TransitionRows.Count) &&
                   SeatedRows.HasCapacity(source.SeatedRows.Count) &&
                   LocomotionRows.HasCapacity(locomotionRequired) &&
                   VisualRows.HasCapacity(source.VisualRows.Count);
        }

        public bool TryImportCompletedScenario(OfficeRuntimeActorTraceState source)
        {
            if (!CanImportCompletedScenario(source)) return false;

            for (var index = 0; index < source.TransitionRows.Count; index++)
                if (!TransitionRows.TryAppend(source.TransitionRows.Rows[index].Detached())) return false;
            for (var index = 0; index < source.SeatedRows.Count; index++)
                if (!SeatedRows.TryAppend(source.SeatedRows.Rows[index].Detached())) return false;
            for (var index = 0; index < source.LocomotionRows.Count; index++)
            {
                R5eLocomotionAdapterRow row = source.LocomotionRows.Rows[index];
                if (!ShouldArchiveLocomotionRow(source, row)) continue;
                if (!LocomotionRows.TryAppend(row)) return false;
                if (row.IsRenderRow)
                {
                    ExpectedRenderedTraceCount++;
                    ObservedRenderedTraceCount++;
                    DuplicateStepJoinCount += row.DuplicateJoinCount;
                }
                else
                {
                    if (row.ExpectedMoving) ExpectedMovingCount++;
                    if (row.ExpectedMoving && row.ObservedMoving) ObservedMovingCount++;
                }
            }
            for (var index = 0; index < source.VisualRows.Count; index++)
                if (!VisualRows.TryAppend(source.VisualRows.Rows[index])) return false;

            ExpectedPreClearCount += source.ExpectedPreClearCount;
            ObservedPreClearCount += source.ObservedPreClearCount;
            ExpectedPostClearCount += source.ExpectedPostClearCount;
            ObservedPostClearCount += source.ObservedPostClearCount;
            ClearMaskedViolationCount += source.ClearMaskedViolationCount;
            SeatedViolationCount += source.SeatedViolationCount;
            return !Failed;
        }

        private static bool ShouldArchiveLocomotionRow(
            OfficeRuntimeActorTraceState source,
            in R5eLocomotionAdapterRow row)
        {
            if (!row.IsRenderRow)
                return row.ExpectedMoving || row.ObservedMoving || row.AtomicPlacement || row.FirstWalk;
            if (row.RenderTrace.IsMoving ||
                row.RenderTrace.ActualDisplacement.sqrMagnitude >
                OfficeRuntimeTraceCoordinator.StationaryEpsilon *
                OfficeRuntimeTraceCoordinator.StationaryEpsilon) return true;
            for (var index = 0; index < source.LocomotionRows.Count; index++)
            {
                R5eLocomotionAdapterRow step = source.LocomotionRows.Rows[index];
                if (step.IsRenderRow || step.Context.RenderFrame != row.Context.RenderFrame) continue;
                if (step.ExpectedMoving || step.ObservedMoving || step.AtomicPlacement || step.FirstWalk)
                    return true;
            }
            return false;
        }

        public void ResetForQaCapture()
        {
            TransitionRows.Reset();
            SeatedRows.Reset();
            LocomotionRows.Reset();
            VisualRows.Reset();
            SeatedSessionId = 0;
            EntryTransactionId = 0;
            ExpectedPreClearCount = 0;
            ObservedPreClearCount = 0;
            ExpectedPostClearCount = 0;
            ObservedPostClearCount = 0;
            ClearMaskedViolationCount = 0;
            SeatedViolationCount = 0;
            ExpectedMovingCount = 0;
            ObservedMovingCount = 0;
            ExpectedRenderedTraceCount = 0;
            ObservedRenderedTraceCount = 0;
            FirstStepOrdinalThisRender = 0;
            LastStepOrdinalThisRender = 0;
            FirstRuntimeTickThisRender = 0;
            LastRuntimeTickThisRender = 0;
            RouteGenerationThisRender = 0;
            HandoffThisRender = 0;
            RenderFrame = -1;
            Failed = false;
            _captureSuppressed = false;
            StepDisplacementSumThisRender = Vector2.zero;
            StepRowsThisRender = 0;
            DuplicateStepJoinCount = 0;
            _lastJoinedStepOrdinal = 0;
        }
    }

    internal sealed class OfficeRuntimeTraceArchive
    {
        private readonly OfficeRuntimeActorTraceState[] _states;

        public OfficeRuntimeTraceArchive(IReadOnlyList<OfficeRuntimeAgent> actors)
        {
            if (actors == null || actors.Count != OfficeRuntimeTraceCoordinator.MaximumActors)
                throw new ArgumentException("R5e archive requires four canonical actors.", nameof(actors));
            _states = new OfficeRuntimeActorTraceState[actors.Count];
            for (var index = 0; index < actors.Count; index++)
                _states[index] = new OfficeRuntimeActorTraceState(
                    index,
                    actors[index].AgentId,
                    true);
        }

        public IReadOnlyList<OfficeRuntimeActorTraceState> States => _states;
        public int ImportedScenarioCount { get; private set; }
        public int FailureCount { get; private set; }

        public bool TryImportCompletedScenario(OfficeRuntimeTraceCoordinator coordinator)
        {
            if (coordinator == null || coordinator.FatalAbort ||
                coordinator.RegisteredActorCount != _states.Length)
            {
                FailureCount++;
                return false;
            }
            for (var index = 0; index < _states.Length; index++)
            {
                if (!coordinator.TryGetActorState(_states[index].ActorId, out var source) ||
                    !_states[index].CanImportCompletedScenario(source))
                {
                    FailureCount++;
                    return false;
                }
            }
            for (var index = 0; index < _states.Length; index++)
            {
                if (!coordinator.TryGetActorState(_states[index].ActorId, out var source) ||
                    !_states[index].TryImportCompletedScenario(source))
                {
                    FailureCount++;
                    return false;
                }
            }
            FailureCount += coordinator.FailureCount;
            ImportedScenarioCount++;
            return coordinator.FailureCount == 0;
        }
    }

    /// <summary>
    /// All allocations happen when the world and actors are configured. Gameplay append paths are
    /// bounded array writes; buffers never wrap or overwrite evidence. Actor IDs are the live
    /// lookup authority; the array index is only the immutable registration ordinal written to
    /// trace rows. A fatal observer result suppresses later evidence writes, never gameplay.
    /// </summary>
    internal sealed class OfficeRuntimeTraceCoordinator
    {
        public const int MaximumActors = 4;
        public const int TransitionCapacityPerActor = 512;
        public const int SeatedCapacityPerActor = 49152;
        public const int LocomotionCapacityPerActor = 24576;
        public const int VisualCapacityPerActor = 2048;
        public const int MaximumLifecycleEventRowsPerTransaction = 5;
        public const float StationaryEpsilon = 0.000001f;

        private readonly OfficeRuntimeActorTraceState[] _actorStates;
        private readonly Dictionary<string, OfficeRuntimeActorTraceState> _actorStatesById;
        private ulong _nextActorStepOrdinal;
        private ulong _nextTransactionId;
        private ulong _nextSessionId;
        private ulong _nextHandoffId;
        private ulong _nextRenderOrdinal;

        public OfficeRuntimeTraceCoordinator(
            ulong runId,
            ulong scenarioId,
            int maximumActors,
            bool captureEnabled)
        {
            if (runId == 0) throw new ArgumentOutOfRangeException(nameof(runId));
            if (maximumActors <= 0 || maximumActors > MaximumActors)
                throw new ArgumentOutOfRangeException(nameof(maximumActors));
            RunId = runId;
            ScenarioId = scenarioId;
            CaptureEnabled = captureEnabled;
            _actorStates = new OfficeRuntimeActorTraceState[maximumActors];
            _actorStatesById = new Dictionary<string, OfficeRuntimeActorTraceState>(
                maximumActors,
                StringComparer.Ordinal);
        }

        public ulong RunId { get; private set; }
        public ulong ScenarioId { get; private set; }
        public bool CaptureEnabled { get; }
        public bool PublishActive { get; private set; }
        public int RegisteredActorCount { get; private set; }
        public int FailureCount { get; private set; }
        public bool FatalAbort { get; private set; }
        public string FatalReason { get; private set; } = string.Empty;
        private string _faultActorId = string.Empty;
        private R5eFaultInjectionPoint _faultPoint;

        internal OfficeRuntimeActorTraceState ActorStateAt(int index)
        {
            if (index < 0 || index >= RegisteredActorCount || _actorStates[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _actorStates[index];
        }

        internal bool TryGetActorState(
            string actorId,
            out OfficeRuntimeActorTraceState state) =>
            _actorStatesById.TryGetValue(actorId ?? string.Empty, out state);

        public OfficeRuntimeActorTraceState RegisterActor(OfficeRuntimeAgent actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            return RegisterActorIdentity(actor.AgentId);
        }

        internal OfficeRuntimeActorTraceState RegisterActorIdentity(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                throw new ArgumentException("R5e trace actor ID is required.", nameof(actorId));
            if (RegisteredActorCount >= _actorStates.Length)
                throw new InvalidOperationException("R5e trace actor capacity is exhausted.");
            if (_actorStatesById.ContainsKey(actorId))
                throw new InvalidOperationException("R5e trace actor ID is already registered.");
            int actorIndex = RegisteredActorCount;
            var state = new OfficeRuntimeActorTraceState(actorIndex, actorId, CaptureEnabled);
            _actorStates[actorIndex] = state;
            _actorStatesById.Add(actorId, state);
            RegisteredActorCount++;
            return state;
        }

        public OfficeRuntimeStepTraceContext BeginActorStep(
            OfficeRuntimeAgent actor,
            int renderFrame,
            int actorStepIndex,
            int actorStepCount,
            float actorMotionDelta,
            float stepDelta)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state))
                throw new InvalidOperationException("R5e trace actor is not bound.");
            return BeginActorStep(
                actor,
                state,
                renderFrame,
                actorStepIndex,
                actorStepCount,
                actorMotionDelta,
                stepDelta);
        }

        private OfficeRuntimeStepTraceContext BeginActorStep(
            OfficeRuntimeAgent actor,
            OfficeRuntimeActorTraceState state,
            int renderFrame,
            int actorStepIndex,
            int actorStepCount,
            float actorMotionDelta,
            float stepDelta)
        {
            ulong stepOrdinal = NextNonZero(ref _nextActorStepOrdinal);
            ulong runtimeTick = actor.NextR5eRuntimeTick;
            var context = new OfficeRuntimeStepTraceContext(
                RunId,
                ScenarioId,
                renderFrame,
                state.ActorIndex,
                stepOrdinal,
                runtimeTick,
                actorStepIndex,
                actorStepCount,
                actorMotionDelta,
                stepDelta);
            state.BeginStep(context, actor.R5eRouteGenerationId, actor.R5eMovementHandoffId);
            if (state.Failed) AbortFatal("actor-step-append-failed:" + state.ActorId);
            return context;
        }

        public bool TryBeginActorStep(
            OfficeRuntimeAgent actor,
            int renderFrame,
            int actorStepIndex,
            int actorStepCount,
            float actorMotionDelta,
            float stepDelta,
            out OfficeRuntimeStepTraceContext context)
        {
            context = default;
            if (actor == null) return false;
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state)) return false;
            if (state.IsCaptureActive && !state.HasStepCapacity(actor.IsR5eSeatedPostState))
            {
                AbortFatal("actor-step-capacity-preflight:" + state.ActorId);
            }
            context = BeginActorStep(
                actor, state, renderFrame, actorStepIndex, actorStepCount,
                actorMotionDelta, stepDelta);
            return true;
        }

        public void CountExpectedPreClear(
            in OfficeRuntimeStepTraceContext context,
            bool expectedSeated)
        {
            if (TryGetActorStateAt(context.ActorIndex, out var state))
                state.CountExpectedPreClear(expectedSeated);
            else
                AbortFatal("actor-step-context-mismatch:" + context.ActorIndex);
        }

        public void CountExpectedPostClearAndMoving(
            in OfficeRuntimeStepTraceContext context,
            bool expectedSeated,
            bool expectedMoving)
        {
            if (TryGetActorStateAt(context.ActorIndex, out var state))
                state.CountExpectedPostClear(expectedSeated, expectedMoving);
            else
                AbortFatal("actor-step-context-mismatch:" + context.ActorIndex);
        }

        public void CountExpectedRender(OfficeRuntimeAgent actor)
        {
            if (TryResolveActorState(actor, out var state)) state.CountExpectedRender();
        }

        public ulong AllocateTransactionId() => NextNonZero(ref _nextTransactionId);
        public ulong AllocateSeatedSessionId() => NextNonZero(ref _nextSessionId);
        public ulong AllocateMovementHandoffId() => NextNonZero(ref _nextHandoffId);

        public bool TryReserveTransitionRows(OfficeRuntimeAgent actor, int rows)
        {
            return !TryResolveActorState(actor, out var state) ||
                   TryReserveTransitionRows(state, rows);
        }

        internal bool TryReserveTransitionRows(string actorId, int rows)
        {
            if (!TryGetActorState(actorId, out OfficeRuntimeActorTraceState state))
            {
                AbortFatal("transition-actor-id-mismatch:" + (actorId ?? string.Empty));
                return true;
            }
            return TryReserveTransitionRows(state, rows);
        }

        private bool TryReserveTransitionRows(OfficeRuntimeActorTraceState state, int rows)
        {
            if (!state.IsCaptureActive || state.HasTransitionCapacity(rows)) return true;
            AbortFatal("transition-capacity-preflight:" + state.ActorId);
            return true;
        }

        public bool TryPreflightRender(OfficeRuntimeAgent actor)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state)) return true;
            if (!state.IsCaptureActive || state.HasRenderCapacity()) return true;
            AbortFatal("render-capacity-preflight:" + state.ActorId);
            return true;
        }

        public void BeginRenderFrame(OfficeRuntimeAgent actor, int renderFrame)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state) ||
                !state.IsCaptureActive) return;
            state.BeginRenderFrame(
                renderFrame,
                actor.R5eRouteGenerationId,
                actor.R5eMovementHandoffId);
        }

        public bool TryPreflightVisual(OfficeRuntimeAgent actor)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state)) return true;
            if (!state.IsCaptureActive || state.HasVisualCapacity()) return true;
            AbortFatal("visual-capacity-preflight:" + state.ActorId);
            return true;
        }

        public void BeginQaCapture(ulong runId)
        {
            if (runId == 0) throw new ArgumentOutOfRangeException(nameof(runId));
            RunId = runId;
            ScenarioId = 0;
            _nextActorStepOrdinal = 0;
            _nextTransactionId = 0;
            _nextSessionId = 0;
            _nextHandoffId = 0;
            _nextRenderOrdinal = 0;
            FailureCount = 0;
            FatalAbort = false;
            FatalReason = string.Empty;
            _faultActorId = string.Empty;
            _faultPoint = R5eFaultInjectionPoint.None;
            for (var index = 0; index < RegisteredActorCount; index++)
                _actorStates[index].ResetForQaCapture();
        }

        public void SetScenarioId(ulong scenarioId)
        {
            if (scenarioId == 0) throw new ArgumentOutOfRangeException(nameof(scenarioId));
            ScenarioId = scenarioId;
        }

        public void ArmFault(string actorId, R5eFaultInjectionPoint point)
        {
            _faultActorId = actorId ?? string.Empty;
            _faultPoint = point;
        }

        public bool ConsumeFault(string actorId, R5eFaultInjectionPoint point)
        {
            if (_faultPoint != point ||
                !string.Equals(_faultActorId, actorId, StringComparison.Ordinal)) return false;
            _faultActorId = string.Empty;
            _faultPoint = R5eFaultInjectionPoint.None;
            return true;
        }

        public void AbortFatal(string reason)
        {
            if (!CaptureEnabled || FatalAbort) return;
            FatalAbort = true;
            FatalReason = reason ?? "r5e-fatal";
            FailureCount++;
            for (var index = 0; index < RegisteredActorCount; index++)
                _actorStates[index].SuppressCapture();
        }

        public void RecordActorStepException(
            in OfficeRuntimeStepTraceContext context,
            bool seatedBeforeException)
        {
            if (TryGetActorStateAt(context.ActorIndex, out var state))
                state.RecordExpectedExceptionPair(seatedBeforeException);
            AbortFatal("actor-step-exception:" + context.ActorIndex);
        }

        public void EnterPublish()
        {
            if (PublishActive) throw new InvalidOperationException("Nested R5e atomic publish is forbidden.");
            PublishActive = true;
        }

        public void ExitPublish()
        {
            if (!PublishActive) throw new InvalidOperationException("R5e atomic publish guard is not active.");
            PublishActive = false;
        }

        public void AppendRenderAdapter(OfficeRuntimeAgent actor, int renderFrame)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state)) return;
            if (!state.IsCaptureActive) return;
            try
            {
                DirectionalLocomotionFrameTrace accepted = actor.CaptureR5eAcceptedLocomotionFrameTrace();
                ulong ordinal = NextNonZero(ref _nextRenderOrdinal);
                var row = new R5eLocomotionAdapterRow(
                    RunId,
                    ScenarioId,
                    renderFrame,
                    actor.AgentId,
                    state.RouteGenerationThisRender,
                    state.HandoffThisRender,
                    ordinal,
                    state.FirstStepOrdinalThisRender,
                    state.LastStepOrdinalThisRender,
                    state.FirstRuntimeTickThisRender,
                    state.LastRuntimeTickThisRender,
                    accepted,
                    state.StepDisplacementSumThisRender,
                    state.StepRowsThisRender,
                    state.DuplicateStepJoinCount == 0 &&
                    Vector2.Distance(
                        accepted.ActualDisplacement,
                        state.StepDisplacementSumThisRender) <= StationaryEpsilon,
                    state.DuplicateStepJoinCount);
                state.AppendRender(row);
                if (state.Failed) AbortFatal("render-append-failed");
            }
            catch (Exception exception)
            {
                AbortFatal("render-observer-exception:" + exception.GetType().Name);
            }
        }

        public bool TryAppendVisualMetadata(OfficeRuntimeAgent actor, int renderFrame)
        {
            if (!TryResolveActorState(actor, out OfficeRuntimeActorTraceState state)) return true;
            if (!state.IsCaptureActive) return true;
            TryPreflightVisual(actor);
            if (!state.IsCaptureActive) return true;
            var row = new R5eVisualCaptureMetadataRow(
                RunId,
                ScenarioId,
                renderFrame,
                actor.R5eRuntimeTick,
                actor.AgentId,
                actor.R5eCurrentTransitionTransactionId,
                state.SeatedSessionId,
                false,
                false);
            state.AppendVisual(row);
            if (state.Failed)
            {
                AbortFatal("visual-append-failed");
            }
            return true;
        }

        private bool TryResolveActorState(
            OfficeRuntimeAgent actor,
            out OfficeRuntimeActorTraceState state)
        {
            state = null;
            if (actor == null) return false;
            OfficeRuntimeActorTraceState bound = actor.R5eBoundTraceState;
            if (bound == null ||
                !string.Equals(bound.ActorId, actor.AgentId, StringComparison.Ordinal))
            {
                AbortFatal("actor-trace-binding-mismatch:" + actor.AgentId);
                return false;
            }
            if (_actorStatesById.TryGetValue(actor.AgentId, out state) &&
                ReferenceEquals(state, bound)) return true;
            AbortFatal("actor-id-registry-mismatch:" + actor.AgentId);
            state = bound;
            return true;
        }

        private bool TryGetActorStateAt(
            int actorIndex,
            out OfficeRuntimeActorTraceState state)
        {
            state = null;
            if (actorIndex < 0 || actorIndex >= RegisteredActorCount ||
                _actorStates[actorIndex] == null) return false;
            state = _actorStates[actorIndex];
            return true;
        }

        private static ulong NextNonZero(ref ulong value)
        {
            if (value == ulong.MaxValue) throw new OverflowException("R5e stable ID allocator cannot wrap.");
            value++;
            return value;
        }
    }

    /// <summary>Literal post-window CSV contracts. Gameplay never formats or writes them.</summary>
    public static class OfficeSeatDockingTraceSchemas
    {
        public const string SchemaVersion = "classic-seat-docking-r5e-v1";

        public const string TransitionHeader =
            "schemaVersion,runId,tick,frame,actorId,seatId,transactionId,event,transitionKind,locomotionSample," +
            "stateBefore,stateAfter,claimBefore,claimAfter,occupancyBefore,occupancyAfter," +
            "chairSnapshotVersion,chairCommitVersion,chairPosBeforeX,chairPosBeforeY,chairPosBeforeZ,chairRotBeforeX,chairRotBeforeY,chairRotBeforeZ,chairRotBeforeW,chairScaleBeforeX,chairScaleBeforeY,chairScaleBeforeZ,chairPosAfterX,chairPosAfterY,chairPosAfterZ,chairRotAfterX,chairRotAfterY,chairRotAfterZ,chairRotAfterW,chairScaleAfterX,chairScaleAfterY,chairScaleAfterZ," +
            "dockX,dockY,seatX,seatY,exitX,exitY,logicalRootBeforeX,logicalRootBeforeY,logicalRootAfterX,logicalRootAfterY," +
            "visualBaselineBeforeX,visualBaselineBeforeY,visualBaselineAfterX,visualBaselineAfterY,previousWorldBeforeX,previousWorldBeforeY,previousWorldAfterX,previousWorldAfterY,previousRenderedBeforeX,previousRenderedBeforeY,previousRenderedAfterX,previousRenderedAfterY," +
            "previousLogicalBeforeX,previousLogicalBeforeY,previousLogicalAfterX,previousLogicalAfterY,previousVisualBeforeX,previousVisualBeforeY,previousVisualAfterX,previousVisualAfterY,visualRootBeforeX,visualRootBeforeY,visualRootAfterX,visualRootAfterY," +
            "velocityBeforeX,velocityBeforeY,velocityAfterX,velocityAfterY,motionDebtBeforeX,motionDebtBeforeY,motionDebtAfterX,motionDebtAfterY,renderedFacing,quantizedVelocityFacing,forwardDot," +
            "floorValid,staticOverlap,chairOverlap,exitReservationOwner,preconditionMask,faultInjectionId,commitSucceeded,rollbackSucceeded,gcAllocBytes,frameMs," +
            "wall_seconds,render_frame,sim_step,member,transition_id,event_kind,state,commit_result,fault_point,transition_dx,transition_dy," +
            "routeIdBefore,routeIdAfter,pathIndexBefore,pathIndexAfter,sweepOriginBeforeX,sweepOriginBeforeY,sweepOriginAfterX,sweepOriginAfterY,visibleMotionDebtSecondsBefore,visibleMotionDebtSecondsAfter,movementBudgetBefore,movementBudgetAfter," +
            "actualDisplacementBeforeX,actualDisplacementBeforeY,actualDisplacementAfterX,actualDisplacementAfterY,semanticDisplacementBeforeX,semanticDisplacementBeforeY,semanticDisplacementAfterX,semanticDisplacementAfterY,accumulatedDisplacementBeforeX,accumulatedDisplacementBeforeY,accumulatedDisplacementAfterX,accumulatedDisplacementAfterY," +
            "gaitDistanceBefore,gaitDistanceAfter,gaitPhaseBefore,gaitPhaseAfter,walkFrameBefore,walkFrameAfter,chairId,chairKind,chairFacing,chairRotation,footprintRevision,layoutRevisionBefore,layoutRevisionPrecommit,occupancyRevisionBefore,occupancyRevisionPrecommit,anchorRevisionBefore,anchorRevisionPrecommit,chairParentIdBefore,chairParentIdPrecommit,chairVisualParentIdBefore,chairVisualParentIdPrecommit,chairPosPrecommitX,chairPosPrecommitY,chairPosPrecommitZ,chairRotPrecommitX,chairRotPrecommitY,chairRotPrecommitZ,chairRotPrecommitW,chairScalePrecommitX,chairScalePrecommitY,chairScalePrecommitZ,chairSnapshotHashBefore,chairSnapshotHashPrecommit," +
            "approachX,approachY,seatRootX,seatRootY,staticClearance,dynamicClearance,seatReservedBefore,seatReservedAfter,seatOccupiedBefore,seatOccupiedAfter,exitReservedBefore,exitReservedAfter,forbiddenColliderCount,forbiddenCollider2DCount,forbiddenRigidbodyCount,forbiddenRigidbody2DCount,forbiddenNavMeshAgentCount,forbiddenAvoidanceCount,spriteAsset,flipX,visibleBodyCount,actualFurnitureOcclusionExternalPixels,preSnapshotHash,postSnapshotHash," +
            "eventSequence,seatedSessionId,scenarioId,traceCapacity,traceWriteCount,traceExpectedCount,traceObservedCount,droppedRowCount,overflowCount,overflowed,scenarioMaximumRenderFrames,scenarioMaximumStepsPerRender,scenarioMaximumTransactionsPerActor,scenarioMaximumRowsPerTransaction,expectedSeatedTickCount,observedSeatedTickCount,seatedViolationCount,firstTick,lastTick,sequenceGapCount,seatedMaxAbsVelocity,seatedMaxAbsMotionDebt,expectedTransactionCount,observedTransactionCount,duplicateTransactionCount,missingTerminalCount,transactionExpectedEventMask,transactionObservedEventMask,transactionDuplicateEventCount,transactionComplete,producerCoverageValid,traceWindowExceeded,runtimeStepIndex,runtimeStepCount,gcProfilerValid,mainThreadProfilerValid,profilerFrame,traceProducerAllocBytes,floorCellX,floorCellY,chairClearance,actualFurnitureOcclusionEvidenceValid,actualFurnitureOcclusionMaskPixels,actorTransactionSnapshotHashBefore,actorTransactionSnapshotHashAfter,observedChairSnapshotHashBefore,observedChairSnapshotHashPrecommit,observedChairMutation,candidateKind,turnCompleted,turnTargetFacing,turnDisplacement,movementHandoffId,locomotionTraceRowId,locomotionJoinFound,movingTickExpectedCount,movingTickObservedCount,movingTickMissingCount,wrongFacingCount,strafeCount,frontFacingLateralCount,backwardLookingCount,standWhileMovingCount,chairFootOnSeatCount,bodyDescendRiseCount,bodyPopCount,chairDeskPenetrationCount,defaultOnlyFieldMask";

        public const string SeatedSessionHeader =
            "schemaVersion,runId,scenarioId,rowKind,samplePhase,frame,actorIndex,actorStepOrdinal,runtimeStepIndex,runtimeStepCount,runtimeTick,sampleSequence,actorId,seatedSessionId,entryTransactionId,seatId,phase,seatEgressWaiting," +
            "preStepLogicalRootX,preStepLogicalRootY,preStepVisualRootX,preStepVisualRootY,preStepVisualBaselineX,preStepVisualBaselineY,preStepPreviousLogicalX,preStepPreviousLogicalY,preStepPreviousVisualX,preStepPreviousVisualY,preStepPreviousWorldX,preStepPreviousWorldY,preStepPreviousRenderedX,preStepPreviousRenderedY,preStepCurrentVelocityX,preStepCurrentVelocityY,preStepDesiredVelocityX,preStepDesiredVelocityY,preStepVisibleMotionDebtSeconds,preStepMovementBudgetWorld,preStepActualDisplacementX,preStepActualDisplacementY,preStepSemanticDisplacementX,preStepSemanticDisplacementY,preStepAccumulatedDisplacementX,preStepAccumulatedDisplacementY,preStepGaitDistance,preStepGaitPhase,preStepWalkFrame," +
            "logicalRootX,logicalRootY,visualRootX,visualRootY,visualBaselineX,visualBaselineY,previousLogicalX,previousLogicalY,previousVisualX,previousVisualY,previousWorldX,previousWorldY,previousRenderedX,previousRenderedY,occupancyPositionX,occupancyPositionY,occupancyCellX,occupancyCellY,currentVelocityX,currentVelocityY,desiredVelocityX,desiredVelocityY,visibleMotionDebtSeconds,movementBudgetWorld,actualDisplacementX,actualDisplacementY,semanticDisplacementX,semanticDisplacementY,accumulatedDisplacementX,accumulatedDisplacementY,gaitDistance,gaitPhase,walkFrame,chairSnapshotVersion,chairSnapshotHash,claimState,occupancyState,seatReserved,seatOccupied,locomotionSample,expectedOrdinal,observedOrdinal,phasePairValid,producerValid,aggregateUpdated,traceProducerAllocBytes,expectedPreClearSampleCount,observedPreClearSampleCount,expectedPostClearSampleCount,observedPostClearSampleCount,expectedStepPairCount,observedStepPairCount,missingPhasePairCount,duplicatePhasePairCount,firstTick,lastTick,sequenceGapCount,droppedRowCount,overflowCount,violationCount,clearMaskedViolationCount,maxAbsVelocity,maxAbsDebt";

        public const string LocomotionAdapterHeader =
            "schemaVersion,runId,scenarioId,rowKind,frame,actorIndex,actorStepOrdinal,runtimeStepIndex,runtimeStepCount,runtimeTick,actorId,routeGenerationId,movementHandoffId,phaseBefore,phaseAfter,stepDelta,positionBeforeX,positionBeforeY,positionAfterX,positionAfterY,rootDeltaX,rootDeltaY,atomicPlacement,agentLastActualDisplacementX,agentLastActualDisplacementY,rootDeltaMatchesAgentActual,currentVelocityX,currentVelocityY,desiredVelocityX,desiredVelocityY,expectedMoving,observedMoving,firstWalk,turnCompleteTick,firstWalkTick,quantizedVelocityFacing,renderedFacing,forwardDot,renderAdapterOrdinal,renderFirstActorStepOrdinal,renderLastActorStepOrdinal,renderFirstRuntimeTick,renderLastRuntimeTick,renderActualDisplacementX,renderActualDisplacementY,renderActualSpeed,renderMotionDirection,renderDisplayDirection,renderLocomotionPhase,renderClip,renderSprite,renderFlipX,renderIsMoving,stepDisplacementSumX,stepDisplacementSumY,renderDisplacementMatchesStepSum,renderJoinValid,expectedMovingCount,observedMovingCount,joinedMovingCount,missingMovingCount,duplicateMovingCount,expectedRenderedTraceCount,observedRenderedTraceCount,missingRenderedTraceCount,duplicateRenderedTraceCount,acceptedTraceOneToOneValid,wrongFacingCount,strafeCount,frontFacingLateralCount,backwardLookingCount,producerValid,droppedRowCount,overflowCount";

        public const string DecodedFrameHeader =
            "schemaVersion,runId,scenarioId,rowKind,videoId,videoKind,frameIndex,ptsSeconds,renderFrame,runtimeTick,actorId,memberId,arrivalDirection,chairRotation,state,event,transactionId,seatedSessionId,routeGenerationId,movementHandoffId,sourceFrameSha256,cleanFrameSha256,maskAtlasSha256,cameraMatrixHash,actorTransformHash,chairTransformHash,deskTransformHash,width,height,gameplayScale,sourceFrameIdentityValid,frameJoinValid,actorMaskValid,expectedPoseMaskValid,chairAlphaMaskValid,chairSeatMaskValid,deskAlphaMaskValid,solidFurnitureMaskValid,floorMaskValid,headMaskValid,pelvisMaskValid,leftFootMaskValid,rightFootMaskValid,actualFurnitureMaskValid,actorPixelCount,expectedPosePixelCount,visibleBodyRendererCount,visibleBodyLargeComponentCount,extraBodyPixelCount,ghostPixelCount,headPixelCount,headVisiblePixelCount,headCentroidX,headCentroidY,headDeltaPx,pelvisAnchorX,pelvisAnchorY,pelvisDeltaYPx,leftFootPixelCount,rightFootPixelCount,leftFootChairIntersectionPixels,rightFootChairIntersectionPixels,actorSolidFurnitureIntersectionPixels,upperBodyInvalidFurniturePixels,actualFurnitureOcclusionExternalPixels,rootDisplacementWorld,locomotionSpeedWorld,renderedFacing,quantizedVelocityFacing,forwardDot,silhouetteXorPixels,silhouetteUnionPixels,silhouetteChangeRatio,bboxEdgeDeltaPx,standWhileMovingViolation,standWhileMovingSampleValid,footOnChairViolation,footOnChairSampleValid,descendRiseViolation,descendRiseSampleValid,bodyPopViolation,bodyPopSampleValid,chairDeskPenetrationViolation,chairDeskPenetrationSampleValid,ghostViolation,ghostSampleValid,doubleBodyViolation,doubleBodySampleValid,headTeleportViolation,headTeleportSampleValid,expectedFrameSampleCount,observedFrameSampleCount,missingFrameSampleCount,standWhileMovingCount,standWhileMovingSampleCount,standWhileMovingProducerValid,footOnChairCount,footOnChairSampleCount,footOnChairProducerValid,descendRiseCount,descendRiseSampleCount,descendRiseProducerValid,bodyPopCount,bodyPopSampleCount,bodyPopProducerValid,chairDeskPenetrationCount,chairDeskPenetrationSampleCount,chairDeskPenetrationProducerValid,ghostCount,ghostSampleCount,ghostProducerValid,doubleBodyCount,doubleBodySampleCount,doubleBodyProducerValid,headTeleportCount,headTeleportSampleCount,headTeleportProducerValid,defaultOnlyMask";

        public const string HumanReviewHeader =
            "schemaVersion,runId,reviewerId,reviewedAtUtc,cleanVideoSha256,annotatedVideoSha256,decodedOracleSha256,normalScale,entryReadable,exitReadable,noStandWhileMoving,noFootOnChair,noDescendRise,noBodyPop,noPenetration,noGhostOrDouble,noHeadTeleport,noStrafeOrBackward,pass,notes";
    }
}

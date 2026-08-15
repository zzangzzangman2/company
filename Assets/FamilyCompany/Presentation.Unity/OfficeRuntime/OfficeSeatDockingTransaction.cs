using System;
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
            int faultInjectionId)
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
        public bool ProducerValid { get; }
        public bool AggregateUpdated { get; }
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
            DirectionalLocomotionFrameTrace renderTrace)
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
    }

    internal sealed class OfficeRuntimeActorTraceState
    {
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

        public void BeginStep(in OfficeRuntimeStepTraceContext context, ulong routeGeneration, ulong handoff)
        {
            if (!CaptureEnabled) return;
            if (RenderFrame != context.RenderFrame)
            {
                RenderFrame = context.RenderFrame;
                FirstStepOrdinalThisRender = context.ActorStepOrdinal;
                FirstRuntimeTickThisRender = context.ActorRuntimeTick;
            }
            LastStepOrdinalThisRender = context.ActorStepOrdinal;
            LastRuntimeTickThisRender = context.ActorRuntimeTick;
            RouteGenerationThisRender = routeGeneration;
            HandoffThisRender = handoff;
            if (!SeatedRows.HasCapacity(2)) Failed = true;
        }

        public void OpenSeatedSession(ulong seatedSessionId, ulong entryTransactionId)
        {
            if (!CaptureEnabled) return;
            if (seatedSessionId == 0 || entryTransactionId == 0 || SeatedSessionId != 0)
            {
                Failed = true;
                return;
            }
            SeatedSessionId = seatedSessionId;
            EntryTransactionId = entryTransactionId;
        }

        public void CloseSeatedSession()
        {
            if (!CaptureEnabled) return;
            SeatedSessionId = 0;
            EntryTransactionId = 0;
        }

        public void CountExpectedPreClear(bool expected)
        {
            if (!CaptureEnabled) return;
            if (expected) ExpectedPreClearCount++;
        }

        public void CountExpectedPostClear(bool expected, bool expectedMoving)
        {
            if (!CaptureEnabled) return;
            if (expected) ExpectedPostClearCount++;
            if (expectedMoving) ExpectedMovingCount++;
        }

        public void AppendSeated(
            R5eSeatedSamplePhase phase,
            in OfficeRuntimeStepTraceContext context,
            string seatId,
            in R5eAgentStepSnapshot preStep,
            in R5eAgentStepSnapshot sample,
            in OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy)
        {
            if (!CaptureEnabled) return;
            if (SeatedSessionId == 0)
            {
                Failed = true;
                return;
            }
            bool stationary = sample.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            if (!stationary) SeatedViolationCount++;
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
                true,
                true);
            if (!SeatedRows.TryAppend(row)) Failed = true;
            if (phase == R5eSeatedSamplePhase.PreClear) ObservedPreClearCount++;
            else ObservedPostClearCount++;
        }

        public void RecordClearMask(in R5eAgentStepSnapshot preClear, in R5eAgentStepSnapshot postClear)
        {
            if (!CaptureEnabled) return;
            bool preViolation = !preClear.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            bool postPass = postClear.IsStationary(OfficeRuntimeTraceCoordinator.StationaryEpsilon);
            if (preViolation && postPass) ClearMaskedViolationCount++;
        }

        public void AppendLocomotion(in R5eLocomotionAdapterRow row)
        {
            if (!CaptureEnabled) return;
            if (!LocomotionRows.TryAppend(row)) Failed = true;
            if (row.ExpectedMoving && row.ObservedMoving) ObservedMovingCount++;
        }

        public void CountExpectedRender()
        {
            if (!CaptureEnabled) return;
            ExpectedRenderedTraceCount++;
        }

        public void AppendRender(in R5eLocomotionAdapterRow row)
        {
            if (!CaptureEnabled) return;
            if (!LocomotionRows.TryAppend(row)) Failed = true;
            ObservedRenderedTraceCount++;
        }

        public void AppendTransition(in R5eSeatTransitionTraceRow row)
        {
            if (!CaptureEnabled) return;
            if (!TransitionRows.TryAppend(row)) Failed = true;
        }
    }

    /// <summary>
    /// All allocations happen when the world and actors are configured. Gameplay append paths are
    /// bounded array writes; buffers never wrap or overwrite evidence.
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
        }

        public ulong RunId { get; }
        public ulong ScenarioId { get; }
        public bool CaptureEnabled { get; }
        public bool PublishActive { get; private set; }
        public int RegisteredActorCount { get; private set; }
        public int FailureCount { get; private set; }

        internal OfficeRuntimeActorTraceState ActorStateAt(int index)
        {
            if (index < 0 || index >= RegisteredActorCount || _actorStates[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _actorStates[index];
        }

        public OfficeRuntimeActorTraceState RegisterActor(OfficeRuntimeAgent actor, int actorIndex)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (actorIndex < 0 || actorIndex >= _actorStates.Length)
                throw new ArgumentOutOfRangeException(nameof(actorIndex));
            if (_actorStates[actorIndex] != null)
                throw new InvalidOperationException("R5e trace actor index is already registered.");
            var state = new OfficeRuntimeActorTraceState(actorIndex, actor.AgentId, CaptureEnabled);
            _actorStates[actorIndex] = state;
            RegisteredActorCount++;
            return state;
        }

        public OfficeRuntimeStepTraceContext BeginActorStep(
            OfficeRuntimeAgent actor,
            int renderFrame,
            int actorIndex,
            int actorStepIndex,
            int actorStepCount,
            float actorMotionDelta,
            float stepDelta)
        {
            OfficeRuntimeActorTraceState state = RequiredState(actorIndex, actor);
            ulong stepOrdinal = NextNonZero(ref _nextActorStepOrdinal);
            ulong runtimeTick = actor.NextR5eRuntimeTick;
            var context = new OfficeRuntimeStepTraceContext(
                RunId,
                ScenarioId,
                renderFrame,
                actorIndex,
                stepOrdinal,
                runtimeTick,
                actorStepIndex,
                actorStepCount,
                actorMotionDelta,
                stepDelta);
            state.BeginStep(context, actor.R5eRouteGenerationId, actor.R5eMovementHandoffId);
            if (state.Failed) FailureCount++;
            return context;
        }

        public void CountExpectedPreClear(
            in OfficeRuntimeStepTraceContext context,
            bool expectedSeated)
        {
            RequiredState(context.ActorIndex, null).CountExpectedPreClear(expectedSeated);
        }

        public void CountExpectedPostClearAndMoving(
            in OfficeRuntimeStepTraceContext context,
            bool expectedSeated,
            bool expectedMoving)
        {
            RequiredState(context.ActorIndex, null).CountExpectedPostClear(
                expectedSeated,
                expectedMoving);
        }

        public void CountExpectedRender(OfficeRuntimeAgent actor)
        {
            actor.R5eTraceState.CountExpectedRender();
        }

        public ulong AllocateTransactionId() => NextNonZero(ref _nextTransactionId);
        public ulong AllocateSeatedSessionId() => NextNonZero(ref _nextSessionId);
        public ulong AllocateMovementHandoffId() => NextNonZero(ref _nextHandoffId);

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
            OfficeRuntimeActorTraceState state = actor.R5eTraceState;
            if (!CaptureEnabled) return;
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
                accepted);
            state.AppendRender(row);
            if (state.Failed) FailureCount++;
        }

        private OfficeRuntimeActorTraceState RequiredState(int actorIndex, OfficeRuntimeAgent actor)
        {
            if (actorIndex < 0 || actorIndex >= _actorStates.Length ||
                _actorStates[actorIndex] == null)
                throw new InvalidOperationException("R5e trace actor is not registered at the scheduler boundary.");
            OfficeRuntimeActorTraceState state = _actorStates[actorIndex];
            if (actor != null && !string.Equals(state.ActorId, actor.AgentId, StringComparison.Ordinal))
                throw new InvalidOperationException("R5e trace actor/index identity mismatch.");
            return state;
        }

        private static ulong NextNonZero(ref ulong value)
        {
            if (value == ulong.MaxValue) throw new OverflowException("R5e stable ID allocator cannot wrap.");
            value++;
            return value;
        }
    }

    /// <summary>Literal post-window CSV contracts. Gameplay never formats or writes them.</summary>
    internal static class OfficeSeatDockingTraceSchemas
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

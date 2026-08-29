using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeRuntimeAgentPhase
    {
        Idle = 0,
        Navigating = 1,
        ApproachingSeat = 2,
        AligningSeat = 3,
        RotatingToSeat = 4,
        SittingDown = 5,
        Working = 6,
        FinishingWork = 7,
        StandingUp = 8,
        LeavingSeat = 9,
        Outside = 10
    }

    [DisallowMultipleComponent]
    public sealed class OfficeRuntimeAgent : MonoBehaviour, IOfficeRuntimeAgent
    {
        public const float DefaultRadius = 0.22f;
        public const float DefaultMoveSpeed = 1.00f;
        internal const int RequiredR5eSeatPreloadDirection = (int)OfficeSeatFacing8.Northwest;
        private const float ArrivalDistance = 0.035f;
        // Keep the generated displacement strictly below the public 0.9 px contract so camera
        // projection roundoff can never turn an exactly-on-boundary step into a false violation.
        private const float MaximumSeatEgressStepPx = 0.899f;
        private const float SeatEgressCompletionTolerancePx = 0.25f;
        // Start far enough behind the authored entrance that a newly released attendee is outside
        // the office/camera bounds. The vector is derived from the first live path segment, so the
        // rule remains correct if the isometric tile basis or door approach changes.
        private const float AttendanceExteriorPathSegmentMultiplier = 2.5f;
        private const string EmptyOfficeWanderIntentPrefix = "empty-office-wander:";

        private PrototypeBootstrap _bootstrap;
        private OfficeRuntimeWorld _world;
        private string _agentId;
        private bool _playerControlled;
        private SpriteRenderer _renderer;
        private SpriteRenderer _seatedUpperBodyRenderer;
        private Transform _visualRoot;
        private DirectionalSpriteAnimator _animator;
        private PlayerNaturalWalkPresenter _playerNaturalWalk;
        private PlayerBakedWalkPresenterV2 _playerBakedWalk;
        private PlayerWalkPresentationMode _playerWalkMode = PlayerWalkPresentationMode.Legacy48;
        private const float PlayerNaturalTurnSeconds = 0.18f;
        private int _playerNaturalTurnFromDirection = -1;
        private int _playerNaturalTurnTargetDirection = -1;
        private float _playerNaturalTurnElapsedSeconds;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private readonly List<OfficeGridCoordinate> _path = new List<OfficeGridCoordinate>();
        private readonly List<OfficeGridCoordinate> _upcomingPathCells =
            new List<OfficeGridCoordinate>(2);
        private int _pathIndex;
        private int _pathRevision;
        private OfficeRuntimeDestination? _destination;
        private OfficeRuntimeDestination? _pendingDestination;
        private OfficeRuntimeDestination? _autonomyDestination;
        private string _autonomyIntentId = string.Empty;
        private OfficeSemanticLocation _autonomyRequestedLocation;
        private string _autonomyRequestedInteractionId = string.Empty;
        private int _autonomyLayoutRevision = -1;
        private string _autonomyStatus = string.Empty;
        private bool _emptyOfficeWanderActive;
        private int _navigationSegmentDirection = -1;
        private OfficeRuntimeInteractionHandle _interactionHandle;
        private OfficeRuntimeInteractionPhase _interactionPhase;
        private string _activeInteractionId = string.Empty;
        private string _activeInteractionOfferId = string.Empty;
        private string _activeInteractionFurnitureId = string.Empty;
        private int _interactionCompletedCount;
        private int _interactionAbortedCount;
        private OfficeRuntimeInteractionEndReason _lastInteractionEndReason;
        private int _standingFacingDirection = -1;
        private bool _attendanceDepartureActive;
        private bool _attendanceArrivalActive;
        private bool _attendanceWorkHandoffActive;
        private bool _attendanceIngressActive;
        private Vector2 _attendanceIngressExteriorWorld;
        private Vector2 _attendanceIngressInteriorWorld;
        private int _attendanceSeatArrivalCount;
        private string _lastSeatReleaseRequestReason = string.Empty;
        private ulong _lastSeatReleaseRequestTick;
        private Vector2 _locomotionVisualFootPlantOffsetWorld;
        private OfficeRuntimeDestination? _preparedAttendanceDestination;
        private readonly List<OfficeGridCoordinate> _preparedAttendancePath =
            new List<OfficeGridCoordinate>();
        private string _assignedTaskId = string.Empty;
        private float _assignedWorkRemaining;
        private long _assignedLastObservedMinute;
        private OfficeActivity _assignedActivity;
        private Vector2 _currentVelocity;
        private Vector2 _desiredVelocity;
        private Vector2 _playerInput;
        private float _stuckSeconds;
        private bool _arrived;
        private bool _releaseSeatRequested;
        private OfficeSeatRuntimeClaim _seatClaim;
        private OfficeSeatSlot _seat;
        private int _seatDirection;
        private OfficeSeatingAnimationClip? _alignedClip;
        private int _alignedFrame = -1;
        private float _seatedUpperBodyCutoffPx = float.NaN;
        private readonly Dictionary<long, Sprite> _seatedUpperBodySprites =
            new Dictionary<long, Sprite>();
        private bool _presentationAway;
        private float _chairDeskErrorPx;
        private Vector2 _chairDeskDeltaPx;
        private float _seatContactErrorPx;
        private float _handWorkErrorPx;
        private float _maxTypingSeatContactErrorPx;
        private float _maxTypingHandWorkErrorPx;
        private int _typingContactSampleCount;
        private bool _qaControl;
        private bool _qaDirectMovementControl;
        private bool _externalDirectionalSeatingPresentation;
        private Vector2 _lastActualDisplacement;
        private float _visibleMotionDebtSeconds;
        private float _visibleFrameMovementBudgetWorld;
        private float _visibleFrameMovementWorld;
        private OfficeGridCoordinate? _yieldCell;
        private int _presentationPathIndex = -1;
        private int _presentationWaypointChanges;
        private Vector3 _sitTransitionStartPelvisWorld;
        private Vector3 _standTransitionTargetPelvisWorld;
        private bool _sitTransitionInitialized;
        private bool _standTransitionInitialized;
        private float _sitPlacementProgress01;
        private float _standPlacementProgress01;
        private bool _seatPresentationPrepared;
        private bool _seatAlignmentComplete;
        private Vector3 _authoredSeatPelvisWorld;
        private Vector3 _workPresentationTargetPelvisWorld;
        private bool _finishingWorkPresentationObserved;
        private int _observedSitDownFrameMask;
        private int _observedWorkFrameMask;
        private int _observedStandUpFrameMask;
        private readonly HashSet<string> _observedOfficeWorkHookSprites =
            new HashSet<string>(StringComparer.Ordinal);
        private float _maxAnimatedAnchorErrorPx;
        private bool _hasTransitionPelvisSample;
        private OfficeSeatingAnimationClip _transitionPelvisClip;
        private Vector2 _previousTransitionPelvisOffsetScreen;
        private float _previousTransitionCushionDistancePx;
        private float _maxTransitionPelvisStepPx;
        private int _transitionMonotonicViolationCount;
        private bool _hasVisibleSeatingPelvisSample;
        private Vector2 _previousVisibleSeatingPelvisScreen;
        private bool _hasChairPresentationSample;
        private Vector2 _previousChairPresentationScreen;
        private float _maxChairPresentationStepPx;
        private int _seatingFacingViolationCount;
        private bool _seatFacingAlignedBeforeSitDown;
        private OfficeSeatingDepthSnapshot _lastSeatingDepthSample;
        private int _seatingDepthViolationCount;
        private bool _seatEgressReservationActive;
        private bool _seatEgressWaiting;
        private bool _seatEgressReachedSafeAnchor;
        private bool _r5eLastAtomicExitReservationBacked;
        private ulong _r5eLastAtomicExitTick;
        private int _r5eLastAtomicExitDirection = -1;
        private OfficeSeatEgressCandidate _seatEgressCandidate;
        private Vector2 _seatEgressTargetWorld;
        private float _seatEgressFrameMovementBudgetWorld;
        private float _seatEgressFrameMovementWorld;
        private float _maximumSeatEgressStepPx;
        private int _seatEgressReservationAttemptCount;
        private int _seatEgressBlockedAttemptCount;
        private int _seatEgressCollisionViolationCount;
        private int _seatEgressUnsafePhaseTransitionCount;
        private bool _hasCompletedSeatEgress;
        private OfficeSeatEgressKind _lastCompletedSeatEgressKind;
        private OfficeGridCoordinate _lastCompletedSeatEgressCell;
        private Vector2 _lastCompletedSeatEgressWorld;
        private bool _lastCompletedSeatEgressClearanceValid;
        private string _lastSeatEgressBlocker = string.Empty;
        private OfficeRuntimeTraceCoordinator _r5eTraceCoordinator;
        private OfficeRuntimeActorTraceState _r5eTraceState;
        private ulong _r5eRuntimeTick;
        private ulong _r5eRouteGenerationId;
        private ulong _r5eSeatedSessionId;
        private ulong _r5eLastClosedSeatedSessionId;
        private ulong _r5ePendingMovementHandoffId;
        private ulong _r5eActiveMovementHandoffId;
        private ulong _r5eTurnCompleteTick;
        private ulong _r5eAtomicPlacementTick;
        private ulong _r5eEntryTransactionId;
        private ulong _r5eExitTransactionId;
        private ulong _r5eSuppressedTransitionObservationId;
        private ulong _r5eAwaitingFirstWalkTransactionId;
        private bool _r5eAwaitingFirstWalk;
        private bool _r5eExitTurnPending;
        private bool _r5eAtomicPlacementThisStep;
        private int _r5eExitTurnDirection = -1;
        private float _r5eExitRetrySeconds;
        private Vector2 _r5eVisualBaselineWorld;
        private Vector2 _r5ePreviousLogicalWorld;
        private Vector2 _r5ePreviousVisualWorld;
        private Vector2 _r5ePreviousWorld;
        private Vector2 _r5ePreviousRenderedWorld;
        private Vector2 _r5eCollisionSweepOrigin;
        private OfficeSeatDockingPlan _r5eActiveDockingPlan;
        private R5ePendingRuntimeStep _r5ePendingStep;
        private bool _r5ePublishActive;
        private R5eProductionObservation _r5eTransitionBeforeObservation;
        private long _r5eTransitionAllocationStart;
        private int _r5eActiveFaultInjectionId;
        private int _r5eForbiddenColliderCount;
        private int _r5eForbiddenCollider2DCount;
        private int _r5eForbiddenRigidbodyCount;
        private int _r5eForbiddenRigidbody2DCount;
        private int _r5eForbiddenNavMeshAgentCount;
        private bool _r5eSeatPresentationPreloaded;
        private int _r5eFirstWalkCount;
        private ulong _r5eLastFirstWalkTick;
        private int _r5eLastFirstWalkDirection = -1;
        private bool _r5eQaOutwardRouteRequested;
        private bool _r5eQaPreparedOutwardRoute;
        private bool _r5eQaInvalidateAtomicVersion;
        private string _seatReservationToken = string.Empty;
        private const int R5eAtomicPathBackupCapacity = 256;
        private readonly OfficeGridCoordinate[] _r5eAtomicPathBackup =
            new OfficeGridCoordinate[R5eAtomicPathBackupCapacity];
        private static readonly InvalidOperationException R5eInjectedFaultException =
            new InvalidOperationException("R5e prepared atomic publish fault injection.");

        public event Action<IOfficeRuntimeAgent, string> AssignedTaskCompleted;

        public string AgentId => _agentId;
        public string MemberId => _agentId;
        public bool IsPlayerControlled => _playerControlled;
        public bool HasAssignedTask => _assignedTaskId.Length > 0;
        public string AssignedTaskId => _assignedTaskId;
        public bool IsSeated => Phase == OfficeRuntimeAgentPhase.Working;
        public bool IsEnteringSeat =>
            Phase == OfficeRuntimeAgentPhase.ApproachingSeat ||
            Phase == OfficeRuntimeAgentPhase.AligningSeat ||
            Phase == OfficeRuntimeAgentPhase.RotatingToSeat;
        internal float R5eVisibleMotionDebtSeconds => _visibleMotionDebtSeconds;
        internal float R5eCurrentVelocityMagnitude => _currentVelocity.magnitude;
        internal float R5eLastActualDisplacementMagnitude => _lastActualDisplacement.magnitude;
        internal float R5eStuckSeconds => _stuckSeconds;
        internal int R5eCollisionViolationCount =>
            _seatEgressCollisionViolationCount + _seatingDepthViolationCount;
        internal int R5eDeprecatedSitFrameMask => _observedSitDownFrameMask;
        internal int R5eDeprecatedStandFrameMask => _observedStandUpFrameMask;

        /// <summary>
        /// True from the moment the seat is claimed until the actor has stepped back out of it.
        /// Depth uses this rather than <see cref="IsSeated"/> so the body does not flip from behind
        /// the chair to in front of it partway through sitting down.
        /// </summary>
        public bool IsOccupyingSeat =>
            _seat != null &&
            (Phase == OfficeRuntimeAgentPhase.SittingDown ||
             Phase == OfficeRuntimeAgentPhase.Working ||
             Phase == OfficeRuntimeAgentPhase.FinishingWork ||
             Phase == OfficeRuntimeAgentPhase.StandingUp ||
             Phase == OfficeRuntimeAgentPhase.LeavingSeat);
        public bool IsBusy => HasAssignedTask ||
                              Phase != OfficeRuntimeAgentPhase.Idle ||
                              _interactionPhase != OfficeRuntimeInteractionPhase.None;
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Break;
        public Vector2 Position => new Vector2(transform.position.x, transform.position.y);
        public OfficeGridCoordinate CurrentCell => _world == null
            ? default
            : _world.Presenter.NearestCell(transform.position);
        public OfficeGridCoordinate? ActiveDestinationCell =>
            _destination.HasValue && !_arrived ? _destination.Value.Cell : null;
        public float AgentRadius { get; private set; } = DefaultRadius;
        public OfficeRuntimeAgentPhase Phase { get; private set; }
        public OfficeRuntimeInteractionPhase InteractionPhase => _interactionPhase;
        public string ActiveInteractionId => _activeInteractionId;
        public string ActiveInteractionOfferId => _activeInteractionOfferId;
        public string ActiveInteractionFurnitureId => _activeInteractionFurnitureId;
        public int InteractionCompletedCount => _interactionCompletedCount;
        public int InteractionAbortedCount => _interactionAbortedCount;
        public OfficeRuntimeInteractionEndReason LastInteractionEndReason => _lastInteractionEndReason;
        public bool HasActiveInteractionClaim => _interactionHandle != null && _interactionHandle.IsActive;
        public Vector2 DesiredVelocity => _desiredVelocity;
        public float StuckSeconds => _stuckSeconds;
        public string ActiveSeatId => _seatClaim == null || _seatClaim.IsReleased ? string.Empty : _seatClaim.SeatId;
        public float ChairDeskErrorPx => _chairDeskErrorPx;
        public Vector2 ChairDeskDeltaPx => _chairDeskDeltaPx;

        /// <summary>
        /// Screen distance between the seat contact of the drawn sprite and the chair cushion
        /// anchor. The only seated placement number that can fail, computed from the live
        /// SpriteRenderer - never hardcoded.
        /// </summary>
        public float SeatContactErrorPx => _seatContactErrorPx;
        public float HandWorkErrorPx => _handWorkErrorPx;
        public float MaxTypingSeatContactErrorPx => _maxTypingSeatContactErrorPx;
        public float MaxTypingHandWorkErrorPx => _maxTypingHandWorkErrorPx;
        public int TypingContactSampleCount => _typingContactSampleCount;
        public OfficeWorkMicroAction CurrentOfficeWorkMicroAction => _animator == null
            ? OfficeWorkMicroAction.None
            : _animator.CurrentOfficeWorkMicroAction;
        public Vector2 LastActualDisplacement => _lastActualDisplacement;
        public float VisibleMotionDebtSeconds => _visibleMotionDebtSeconds;
        public float VisibleFrameMovementWorld => _visibleFrameMovementWorld;
        public bool HasActiveVisibleMotionIntent =>
            _attendanceIngressActive ||
            (HasDirectMovementControl && _playerInput.sqrMagnitude > 0.0001f) ||
            (_destination.HasValue && !_arrived) ||
            IsEnteringSeat ||
            Phase == OfficeRuntimeAgentPhase.SittingDown ||
            Phase == OfficeRuntimeAgentPhase.StandingUp ||
            Phase == OfficeRuntimeAgentPhase.LeavingSeat;
        public Vector2 AccumulatedFrameDisplacement => _animator == null
            ? Vector2.zero
            : _animator.AccumulatedTileDisplacement;
        public Vector2 SemanticFrameDisplacement => _animator == null
            ? Vector2.zero
            : _animator.SemanticTileDisplacement;
        public float ActualPresentationSpeed => _animator == null ? 0f : _animator.ActualTileSpeed;
        public bool WasCollisionProjected => _animator != null && _animator.WasCollisionProjected;
        public int SemanticDirection => _animator == null ? 0 : _animator.SemanticDirection;
        public int RequestedDirection => _animator == null ? 0 : _animator.RequestedDirection;
        public int MotionDirection => _animator == null ? 0 : _animator.MotionDirection;
        public float FacingAlignmentDot => _animator == null ? 1f : _animator.FacingAlignmentDot;
        public float FacingAngularErrorDegrees => _animator == null
            ? 0f
            : _animator.FacingAngularErrorDegrees;
        public bool UsedSemanticHeading => _animator != null && _animator.UsedSemanticHeading;
        public OfficeLocomotionPhase LocomotionPhase => _animator == null
            ? OfficeLocomotionPhase.Idle
            : _animator.LocomotionPhase;
        public float GaitDistance => _animator == null ? 0f : _animator.GaitDistance;
        public float GaitPhase01 => _animator == null ? 0f : _animator.GaitPhase01;
        public float StrideLength => _animator == null
            ? OfficeLocomotionGaitRules.DefaultStrideLength
            : _animator.StrideLength;
        public int CurrentWalkFrame => _animator == null ? 0 : _animator.CurrentWalkFrame;
        public int CurrentDirection => _animator == null ? 0 : _animator.CurrentDirection;
        public PlayerWalkPresentationMode PlayerWalkMode => _playerWalkMode;
        public int VisibleWalkPose => _playerBakedWalk == null
            ? -1
            : _playerBakedWalk.VisibleWalkPose;
        public int VisibleWalkDirection => _playerBakedWalk == null
            ? -1
            : _playerBakedWalk.VisibleWalkDirection;
        public string VisibleWalkSpriteName => _playerBakedWalk == null
            ? string.Empty
            : _playerBakedWalk.VisibleWalkSpriteName;
        public PlayerWalkSupportLegV2 VisibleSupportLeg => _playerBakedWalk == null
            ? PlayerWalkSupportLegV2.None
            : _playerBakedWalk.VisibleSupportLeg;
        public Vector2 VisibleSupportFootWorld => _playerBakedWalk == null
            ? Vector2.zero
            : _playerBakedWalk.VisibleSupportFootWorld;
        public int ConfiguredLocomotionTransitionFrameCount => _animator == null
            ? 0
            : _animator.ConfiguredLocomotionTransitionFrameCount;
        public bool IsLocomotionTransitionSpriteActive =>
            _animator != null && _animator.IsLocomotionTransitionSpriteActive;
        public int ExpectedSeatDirection => _seatDirection;
        public bool IsOfficeSeatingFacingLocked =>
            _animator != null && _animator.IsOfficeSeatingFacingLocked;
        public int LockedOfficeSeatingDirection => _animator == null
            ? -1
            : _animator.LockedOfficeSeatingDirection;
        public int CurrentSpriteDirection => _animator == null
            ? -1
            : _animator.CurrentAppliedSpriteDirection;
        public int SeatingFacingViolationCount => _seatingFacingViolationCount;
        public bool WasSeatFacingAlignedBeforeSitDown => _seatFacingAlignedBeforeSitDown;
        public bool IsSeatEntryPresentationPlanted =>
            _animator != null && _animator.IsOfficeSeatingEntryPlanted;
        public int SeatingSpriteDirectionMismatchCount => _animator == null
            ? 0
            : _animator.OfficeSeatingDirectionMismatchCount;
        public int MaximumSeatingSpriteDirectionOctantDelta => _animator == null
            ? 0
            : _animator.MaximumOfficeSeatingDirectionOctantDelta;
        public SpriteRenderer PresentationRenderer => _renderer;
        public SpriteRenderer SeatedUpperBodyProtectionRenderer => _seatedUpperBodyRenderer;
        public bool IsSeatedUpperBodyProtectionVisible =>
            _seatedUpperBodyRenderer != null && _seatedUpperBodyRenderer.enabled;
        public int SemanticPathLength => _path.Count;
        public int PresentationPathIndex => _presentationPathIndex;
        public int PresentationWaypointChanges => _presentationWaypointChanges;
        public OfficeSeatingPresentationMode SeatingPresentationMode => _animator == null
            ? OfficeSeatingPresentationMode.SafeStaticWork
            : _animator.SeatingPresentationMode;
        public OfficeSeatingAnimationClip? CurrentSeatingClip =>
            _animator?.CurrentOfficeSeatingClip;
        public int CurrentSeatingFrame => _animator == null ? -1 : _animator.CurrentOfficeSeatingFrame;
        public int ObservedSitDownFrameCount => CountBits(_observedSitDownFrameMask);
        public int ObservedWorkFrameCount => CountBits(_observedWorkFrameMask);
        public int ObservedStandUpFrameCount => CountBits(_observedStandUpFrameMask);
        public bool IsOfficeWorkAnimationHookActive =>
            _animator != null && _animator.IsOfficeWorkAnimationHookActive;
        public int ObservedOfficeWorkHookSpriteCount => _observedOfficeWorkHookSprites.Count;
        public float MaxAnimatedAnchorErrorPx => _maxAnimatedAnchorErrorPx;
        public float MaxTransitionPelvisStepPx => _maxTransitionPelvisStepPx;
        public int TransitionMonotonicViolationCount => _transitionMonotonicViolationCount;
        public float MaxChairPresentationStepPx => _maxChairPresentationStepPx;
        public OfficeSeatingDepthSnapshot LastSeatingDepthSample => _lastSeatingDepthSample;
        public int SeatingDepthViolationCount => _seatingDepthViolationCount;
        public bool IsWaitingForSeatEgress =>
            Phase == OfficeRuntimeAgentPhase.FinishingWork && _seatEgressWaiting;
        public bool HasSeatEgressReservation => _seatEgressReservationActive;
        public bool HasReachedSeatEgressSafeAnchor => _seatEgressReachedSafeAnchor;
        public OfficeSeatEgressKind ActiveSeatEgressKind => _seatEgressReservationActive
            ? _seatEgressCandidate.Kind
            : OfficeSeatEgressKind.None;
        public OfficeGridCoordinate ActiveSeatEgressTargetCell => _seatEgressCandidate.TargetCell;
        public Vector2 ActiveSeatEgressTargetWorld => _seatEgressTargetWorld;
        public float MaximumSeatEgressRootStepPx => _maximumSeatEgressStepPx;
        public int SeatEgressReservationAttemptCount => _seatEgressReservationAttemptCount;
        public int SeatEgressBlockedAttemptCount => _seatEgressBlockedAttemptCount;
        public int SeatEgressCollisionViolationCount => _seatEgressCollisionViolationCount;
        public int SeatEgressUnsafePhaseTransitionCount => _seatEgressUnsafePhaseTransitionCount;
        public bool HasCompletedSeatEgress => _hasCompletedSeatEgress;
        public OfficeSeatEgressKind LastCompletedSeatEgressKind => _lastCompletedSeatEgressKind;
        public OfficeGridCoordinate LastCompletedSeatEgressCell => _lastCompletedSeatEgressCell;
        public Vector2 LastCompletedSeatEgressWorld => _lastCompletedSeatEgressWorld;
        public bool LastCompletedSeatEgressClearanceValid => _lastCompletedSeatEgressClearanceValid;
        public string LastSeatEgressBlocker => _lastSeatEgressBlocker;
        internal ulong NextR5eRuntimeTick => _r5eRuntimeTick == ulong.MaxValue
            ? throw new OverflowException("R5e actor runtime tick cannot wrap.")
            : _r5eRuntimeTick + 1UL;
        internal ulong R5eRouteGenerationId => _r5eRouteGenerationId;
        internal ulong R5eMovementHandoffId => _r5eActiveMovementHandoffId;
        internal OfficeRuntimeActorTraceState R5eTraceState =>
            _r5eTraceState ?? throw new InvalidOperationException("R5e trace state is not bound.");
        internal OfficeRuntimeActorTraceState R5eBoundTraceState => _r5eTraceState;
        private bool HasActiveR5eStepObservation =>
            _r5ePendingStep.Began && _r5eTraceCoordinator != null && _r5eTraceState != null;
        internal bool IsR5eSeatedPostState =>
            _seat != null &&
            _seatClaim != null &&
            !_seatClaim.IsReleased &&
            (Phase == OfficeRuntimeAgentPhase.Working ||
             Phase == OfficeRuntimeAgentPhase.FinishingWork);
        internal int R5eFirstWalkCount => _r5eFirstWalkCount;
        internal ulong R5eLastFirstWalkTick => _r5eLastFirstWalkTick;
        internal int R5eLastFirstWalkDirection => _r5eLastFirstWalkDirection;
        internal ulong R5eTurnCompleteTick => _r5eTurnCompleteTick;
        internal ulong R5eRuntimeTick => _r5eRuntimeTick;
        internal ulong R5eAtomicPlacementTick => _r5eAtomicPlacementTick;
        internal bool R5eLastAtomicExitReservationBacked =>
            _r5eLastAtomicExitReservationBacked;
        internal ulong R5eLastAtomicExitTick => _r5eLastAtomicExitTick;
        internal int R5eLastAtomicExitDirection => _r5eLastAtomicExitDirection;
        internal ulong R5eCurrentTransitionTransactionId =>
            _r5eExitTransactionId != 0 ? _r5eExitTransactionId : _r5eEntryTransactionId;
        public bool IsSeatForegroundOcclusionEngaged
        {
            get
            {
                if (_seat == null || _world == null) return false;
                return OfficeSeatOcclusionRules.Evaluate(
                    Phase,
                    Position,
                    _world.Workstations.SeatOperatorWorld(_seat),
                    _world.Workstations.SeatApproachWorld(_seat),
                    _animator?.CurrentOfficeSeatingClip,
                    _animator == null ? -1 : _animator.CurrentOfficeSeatingFrame).ForegroundEngaged;
            }
        }
        public float VisualRotationErrorDegrees => _visualRoot == null
            ? float.PositiveInfinity
            : Quaternion.Angle(Quaternion.identity, _visualRoot.localRotation);
        public float VisualScaleDeviation => _visualRoot == null
            ? float.PositiveInfinity
            : Mathf.Max(
                Mathf.Abs((_visualRoot.localScale.x / OfficeGridCharacterMover.UniformVisualScale) - 1f),
                Mathf.Max(
                    Mathf.Abs((_visualRoot.localScale.y / OfficeGridCharacterMover.UniformVisualScale) - 1f),
                    Mathf.Abs((_visualRoot.localScale.z / OfficeGridCharacterMover.UniformVisualScale) - 1f)));
        public string CurrentSpriteName => _renderer == null || _renderer.sprite == null
            ? string.Empty
            : _renderer.sprite.name;
        public DirectionalLocomotionFrameTrace CaptureLocomotionFrameTrace() =>
            _animator == null ? default : _animator.CaptureLocomotionFrameTrace();
        public bool IsPresentationAway => _presentationAway;
        public int AttendanceSeatArrivalCount => _attendanceSeatArrivalCount;
        public Vector2 LocomotionVisualFootPlantOffsetWorld =>
            _locomotionVisualFootPlantOffsetWorld;
        internal string DiagnosticDestinationId =>
            _destination.HasValue ? _destination.Value.DestinationId : string.Empty;
        internal string DiagnosticPendingDestinationId =>
            _pendingDestination.HasValue ? _pendingDestination.Value.DestinationId : string.Empty;
        internal string DiagnosticAutonomyDestinationId =>
            _autonomyDestination.HasValue ? _autonomyDestination.Value.DestinationId : string.Empty;
        internal string DiagnosticAutonomyIntentId => _autonomyIntentId;
        internal OfficeGridCoordinate? DiagnosticDestinationCell =>
            _destination.HasValue ? _destination.Value.Cell : null;
        internal bool DiagnosticEmptyOfficeWanderActive => _emptyOfficeWanderActive;
        internal bool DiagnosticAttendanceWorkHandoffActive => _attendanceWorkHandoffActive;
        internal int DiagnosticPathIndex => _pathIndex;
        internal bool DiagnosticSeatClaimOccupied => _seatClaim != null && _seatClaim.IsOccupied;
        internal bool DiagnosticSeatClaimReleased => _seatClaim == null || _seatClaim.IsReleased;
        internal string DiagnosticLastSeatReleaseRequestReason => _lastSeatReleaseRequestReason;
        internal ulong DiagnosticLastSeatReleaseRequestTick => _lastSeatReleaseRequestTick;
        internal string DiagnosticDockingPlan
        {
            get
            {
                if (_seat == null || _world == null ||
                    !_world.Workstations.TryResolveDockingPlan(_seat, out OfficeSeatDockingPlan plan))
                    return "none";
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "seat={0};revision={1};current={2};root=({3:F3},{4:F3});pelvis=({5:F3},{6:F3})",
                    _seat.SeatId,
                    plan.AnchorRevision,
                    _world.Workstations.IsDockingPlanCurrent(plan),
                    plan.SeatRootWorld.x,
                    plan.SeatRootWorld.y,
                    plan.SeatPelvisWorld.x,
                    plan.SeatPelvisWorld.y);
            }
        }
        public bool IsAttendanceIngressActive => _attendanceIngressActive;
        public Vector2 AttendanceIngressExteriorWorld => _attendanceIngressExteriorWorld;
        public Vector2 AttendanceIngressInteriorWorld => _attendanceIngressInteriorWorld;
        public string LastReservationBlocker { get; private set; } = string.Empty;
        public string LastMovementBlocker { get; private set; } = string.Empty;

        public OfficeObservationStatusKind StatusKind
        {
            get
            {
                if (_presentationAway) return OfficeObservationStatusKind.Outside;
                if (Phase == OfficeRuntimeAgentPhase.Navigating ||
                    IsEnteringSeat ||
                    Phase == OfficeRuntimeAgentPhase.LeavingSeat)
                    return OfficeObservationStatusKind.Moving;
                if (Phase == OfficeRuntimeAgentPhase.SittingDown ||
                    Phase == OfficeRuntimeAgentPhase.StandingUp ||
                    Phase == OfficeRuntimeAgentPhase.FinishingWork)
                    return OfficeObservationStatusKind.Seated;
                return CurrentActivity switch
                {
                    OfficeActivity.Work => OfficeObservationStatusKind.Typing,
                    OfficeActivity.Meeting => OfficeObservationStatusKind.Meeting,
                    OfficeActivity.Printing => OfficeObservationStatusKind.Printing,
                    OfficeActivity.Break => OfficeObservationStatusKind.Break,
                    OfficeActivity.Outside => OfficeObservationStatusKind.Outside,
                    _ => OfficeObservationStatusKind.Idle
                };
            }
        }

        public string StatusDetail => HasAssignedTask
            ? "계약 업무"
            : _autonomyStatus;

        public void Configure(
            PrototypeBootstrap bootstrap,
            OfficeRuntimeWorld world,
            string agentId,
            bool playerControlled,
            SpriteRenderer renderer,
            Transform visualRoot,
            DirectionalSpriteAnimator animator,
            OfficeCharacterSeatPoseCatalog poseCatalog,
            OfficeGridCoordinate spawnCell,
            float radius = DefaultRadius)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _agentId = string.IsNullOrWhiteSpace(agentId)
                ? throw new ArgumentException("Agent ID is required.", nameof(agentId))
                : agentId.Trim();
            _seatReservationToken = "starter-office-seat:" + _agentId;
            if (_path.Capacity < 8) _path.Capacity = 8;
            _playerControlled = playerControlled;
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
            if (_animator != null) _animator.OfficeFrameApplied -= HandleOfficeFrameApplied;
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _animator.SetExternallyTicked(true);
            SyncEmptyOfficeLocomotionPresentation();
            _animator.OfficeFrameApplied += HandleOfficeFrameApplied;
            _poseCatalog = poseCatalog ?? throw new ArgumentNullException(nameof(poseCatalog));
            AgentRadius = Mathf.Max(0.12f, radius);
            transform.position = _world.Presenter.CellCenterWorld(spawnCell);
            transform.localScale = Vector3.one;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
            ClearVisibleMotionDebt();
            Phase = OfficeRuntimeAgentPhase.Idle;
            _pathRevision = _world.Occupancy.Revision;
            Vector2 initial = Position;
            _r5eVisualBaselineWorld = initial;
            _r5ePreviousLogicalWorld = initial;
            _r5ePreviousVisualWorld = initial;
            _r5ePreviousWorld = initial;
            _r5ePreviousRenderedWorld = initial;
            _r5eCollisionSweepOrigin = initial;
            _r5eForbiddenColliderCount = GetComponentsInChildren<Collider>(true).Length;
            _r5eForbiddenCollider2DCount = GetComponentsInChildren<Collider2D>(true).Length;
            _r5eForbiddenRigidbodyCount = GetComponentsInChildren<Rigidbody>(true).Length;
            _r5eForbiddenRigidbody2DCount = GetComponentsInChildren<Rigidbody2D>(true).Length;
            _r5eForbiddenNavMeshAgentCount =
                GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length;
        }

        public void ConfigurePlayerNaturalWalk(PlayerNaturalWalkPresenter presenter)
        {
            _playerNaturalWalk = presenter;
        }

        public void ConfigurePlayerBakedWalk(PlayerBakedWalkPresenterV2 presenter)
        {
            _playerBakedWalk = presenter;
        }

        public void ConfigurePlayerWalkMode(PlayerWalkPresentationMode mode)
        {
            _playerWalkMode = mode;
        }

        internal void BindR5eTrace(
            OfficeRuntimeTraceCoordinator coordinator,
            OfficeRuntimeActorTraceState traceState)
        {
            _r5eTraceCoordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _r5eTraceState = traceState ?? throw new ArgumentNullException(nameof(traceState));
            if (!string.Equals(traceState.ActorId, _agentId, StringComparison.Ordinal))
                throw new InvalidOperationException("R5e trace binding belongs to another actor.");
        }

        public bool AssignOfficeTask(string taskId, OfficeActivity activity, float workMinutes)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task ID is required.", nameof(taskId));
            if (workMinutes <= 0f) throw new ArgumentOutOfRangeException(nameof(workMinutes));
            if (HasAssignedTask || _playerControlled || _qaControl) return false;
            if (!_world.Workstations.TryResolveActivityDestination(
                    activity,
                    _agentId,
                    taskId,
                    out OfficeRuntimeDestination destination)) return false;
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.ContractOverride);
            _autonomyDestination = null;
            _autonomyLayoutRevision = -1;
            _assignedTaskId = taskId.Trim();
            _assignedActivity = activity;
            _assignedWorkRemaining = workMinutes;
            _assignedLastObservedMinute = _bootstrap.State.Time.ElapsedMinutes;
            if (BeginDestination(destination)) return true;
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _assignedLastObservedMinute = 0L;
            _assignedActivity = OfficeActivity.Break;
            ReleaseSeatImmediately();
            Phase = OfficeRuntimeAgentPhase.Idle;
            ResumeAutonomy();
            return false;
        }

        public OfficeRuntimeAgentLayoutSnapshot CaptureLayoutSnapshot()
        {
            if (_world == null) throw new InvalidOperationException("Runtime world is unavailable.");
            return new OfficeRuntimeAgentLayoutSnapshot(
                _agentId,
                _world.Presenter.NearestCell(transform.position),
                Phase == OfficeRuntimeAgentPhase.Outside || _presentationAway,
                CurrentDirection,
                _assignedTaskId,
                _assignedActivity,
                _assignedWorkRemaining,
                _autonomyIntentId,
                _autonomyRequestedLocation,
                _autonomyRequestedInteractionId,
                _autonomyStatus);
        }

        public bool RestoreLayoutSnapshot(OfficeRuntimeAgentLayoutSnapshot snapshot)
        {
            if (!string.Equals(snapshot.MemberId, _agentId, StringComparison.Ordinal))
                throw new ArgumentException("Layout snapshot belongs to another actor.", nameof(snapshot));
            if (_world == null || _bootstrap == null || _bootstrap.State == null) return false;

            _autonomyIntentId = snapshot.AutonomyIntentId;
            _autonomyRequestedLocation = snapshot.AutonomyLocation;
            _autonomyRequestedInteractionId = snapshot.AutonomyInteractionId;
            _autonomyStatus = snapshot.AutonomyStatus;
            _autonomyLayoutRevision = -1;
            _autonomyDestination = null;
            _animator.RestoreStandingFacing(snapshot.Direction);

            if (snapshot.HasAssignedTask)
            {
                if (!_world.Workstations.TryResolveActivityDestination(
                        snapshot.AssignedActivity,
                        _agentId,
                        snapshot.AssignedTaskId,
                        out OfficeRuntimeDestination destination)) return false;
                _assignedTaskId = snapshot.AssignedTaskId;
                _assignedActivity = snapshot.AssignedActivity;
                _assignedWorkRemaining = snapshot.AssignedWorkRemainingMinutes;
                _assignedLastObservedMinute = _bootstrap.State.Time.ElapsedMinutes;
                if (!BeginDestination(destination))
                {
                    _assignedTaskId = string.Empty;
                    _assignedActivity = OfficeActivity.Break;
                    _assignedWorkRemaining = 0f;
                    _assignedLastObservedMinute = 0L;
                    return false;
                }
                return true;
            }

            if (snapshot.WasOutside)
            {
                Phase = OfficeRuntimeAgentPhase.Outside;
                CurrentActivity = OfficeActivity.Outside;
                SetPresentationAway(true);
                return true;
            }

            if (snapshot.HasAutonomyRequest && !_playerControlled)
                TryStartAutonomyRequest();
            return true;
        }

        public void CancelAssignedTask()
        {
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _assignedLastObservedMinute = 0L;
            _assignedActivity = OfficeActivity.Break;
            ResumeAutonomy();
        }

        public void SetAutonomousDestination(
            string intentId,
            OfficeSemanticLocation location,
            string statusLabel)
        {
            SetAutonomousDestination(intentId, location, string.Empty, statusLabel);
        }

        public void SetAutonomousDestination(
            string intentId,
            OfficeSemanticLocation location,
            string interactionId,
            string statusLabel)
        {
            if (_playerControlled || _qaControl) return;
            _emptyOfficeWanderActive = false;
            if (string.IsNullOrWhiteSpace(intentId))
            {
                ClearAutonomousDestination();
                return;
            }
            string normalizedIntentId = intentId.Trim();
            string normalizedInteractionId = (interactionId ?? string.Empty).Trim();
            bool sameRequest = string.Equals(
                                   _autonomyIntentId,
                                   normalizedIntentId,
                                   StringComparison.Ordinal) &&
                               _autonomyRequestedLocation == location &&
                               string.Equals(
                                   _autonomyRequestedInteractionId,
                                   normalizedInteractionId,
                                   StringComparison.Ordinal);
            _autonomyStatus = string.IsNullOrWhiteSpace(statusLabel) ? "자율 행동" : statusLabel.Trim();
            // Contract execution may last for many autonomy refreshes. Retain only the latest
            // request here; acquiring furniture while the contract owns the actor would create an
            // invisible, long-lived reservation.
            if (sameRequest && HasAssignedTask)
            {
                _autonomyLayoutRevision = -1;
                return;
            }
            if (!sameRequest && HasAssignedTask)
            {
                _autonomyIntentId = normalizedIntentId;
                _autonomyRequestedLocation = location;
                _autonomyRequestedInteractionId = normalizedInteractionId;
                _autonomyLayoutRevision = -1;
                _autonomyDestination = null;
                return;
            }
            if (sameRequest && _autonomyLayoutRevision == _world.Occupancy.Revision) return;

            if (_interactionPhase != OfficeRuntimeInteractionPhase.None)
            {
                bool completed = !sameRequest &&
                                 _interactionPhase == OfficeRuntimeInteractionPhase.Performing;
                EndInteraction(
                    completed
                        ? OfficeRuntimeInteractionTermination.Completed
                        : OfficeRuntimeInteractionTermination.Aborted,
                    completed
                        ? OfficeRuntimeInteractionEndReason.IntentAdvanced
                        : sameRequest
                            ? OfficeRuntimeInteractionEndReason.LayoutChanged
                            : OfficeRuntimeInteractionEndReason.SupersededBeforeArrival);
            }

            _autonomyIntentId = normalizedIntentId;
            _autonomyRequestedLocation = location;
            _autonomyRequestedInteractionId = normalizedInteractionId;
            _autonomyLayoutRevision = -1;
            _autonomyDestination = null;
            // Attendance owns the prewarmed door-to-desk route until the actor is seated. Do not
            // resolve a competing autonomy path in the same frame.
            if (!HasAssignedTask && !_attendanceArrivalActive && !_attendanceWorkHandoffActive)
                TryStartAutonomyRequest();
        }

        public bool TrySetAutonomousWanderDestination(
            string intentId,
            OfficeGridCoordinate destinationCell,
            string statusLabel)
        {
            if (_qaControl || _world == null || string.IsNullOrWhiteSpace(intentId) ||
                IsBusy || HasAssignedTask || _attendanceArrivalActive || _attendanceWorkHandoffActive ||
                IsSeated || IsEnteringSeat ||
                !_world.Grid.Contains(destinationCell) || !_world.Grid.IsWalkable(destinationCell))
                return false;

            OfficeGridCoordinate start = CurrentCell;
            if (start.Equals(destinationCell)) return false;
            IReadOnlyList<OfficeGridCoordinate> route = _world.Paths.FindPath(
                _agentId,
                start,
                destinationCell,
                string.Empty,
                true,
                AgentRadius);
            if (route.Count < 2) return false;

            string normalizedIntentId = intentId.Trim();
            var destination = new OfficeRuntimeDestination(
                normalizedIntentId + ":" + destinationCell.X + ":" + destinationCell.Y,
                OfficeSemanticLocation.OpenArea,
                OfficeActivity.Break,
                destinationCell);
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.IntentAdvanced);
            _autonomyIntentId = normalizedIntentId;
            _autonomyRequestedLocation = OfficeSemanticLocation.OpenArea;
            _autonomyRequestedInteractionId = string.Empty;
            _autonomyStatus = string.IsNullOrWhiteSpace(statusLabel)
                ? "빈 사무실 산책"
                : statusLabel.Trim();
            _autonomyDestination = destination;
            _autonomyLayoutRevision = _world.Occupancy.Revision;
            _emptyOfficeWanderActive = true;
            if (BeginDestination(destination)) return true;

            _emptyOfficeWanderActive = false;
            _autonomyDestination = null;
            _autonomyLayoutRevision = -1;
            return false;
        }

        public void ClearAutonomousDestination()
        {
            bool wasEmptyOfficeWander = _emptyOfficeWanderActive ||
                                        _autonomyIntentId.StartsWith(
                                            EmptyOfficeWanderIntentPrefix,
                                            StringComparison.Ordinal);
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.Cleared);
            _autonomyIntentId = string.Empty;
            _autonomyRequestedLocation = OfficeSemanticLocation.None;
            _autonomyRequestedInteractionId = string.Empty;
            _autonomyLayoutRevision = -1;
            _autonomyStatus = string.Empty;
            _autonomyDestination = null;
            _emptyOfficeWanderActive = false;
            if (!HasAssignedTask && (!_playerControlled || wasEmptyOfficeWander) &&
                !_attendanceWorkHandoffActive)
                RequestStopAndStand();
        }

        public void ResetRuntimeState()
        {
            ClearVisibleMotionDebt();
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.RuntimeReset);
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _autonomyIntentId = string.Empty;
            _autonomyRequestedLocation = OfficeSemanticLocation.None;
            _autonomyRequestedInteractionId = string.Empty;
            _autonomyLayoutRevision = -1;
            _autonomyStatus = string.Empty;
            _autonomyDestination = null;
            _emptyOfficeWanderActive = false;
            _navigationSegmentDirection = -1;
            _attendanceDepartureActive = false;
            _attendanceArrivalActive = false;
            _attendanceWorkHandoffActive = false;
            ReleaseAttendanceIngress();
            _attendanceSeatArrivalCount = 0;
            _lastSeatReleaseRequestReason = string.Empty;
            _lastSeatReleaseRequestTick = 0;
            _preparedAttendanceDestination = null;
            _preparedAttendancePath.Clear();
            _standingFacingDirection = -1;
            _destination = null;
            _pendingDestination = null;
            _r5eQaOutwardRouteRequested = false;
            _r5eQaPreparedOutwardRoute = false;
            _r5eAwaitingFirstWalk = false;
            _r5eLastFirstWalkDirection = -1;
            _path.Clear();
            _pathIndex = 0;
            _presentationPathIndex = -1;
            _presentationWaypointChanges = 0;
            _sitTransitionInitialized = false;
            _standTransitionInitialized = false;
            _observedSitDownFrameMask = 0;
            _observedWorkFrameMask = 0;
            _observedStandUpFrameMask = 0;
            _observedOfficeWorkHookSprites.Clear();
            _maxAnimatedAnchorErrorPx = 0f;
            _maxTypingSeatContactErrorPx = 0f;
            _maxTypingHandWorkErrorPx = 0f;
            _typingContactSampleCount = 0;
            ResetSeatEgressMetrics();
            ResetTransitionMotionMetrics();
            _maxTransitionPelvisStepPx = 0f;
            _transitionMonotonicViolationCount = 0;
            _arrived = false;
            _yieldCell = null;
            ReleaseSeatImmediately();
            ResetVisualPose();
            _animator?.ResumeWalkingAfterSeating();
            StopMotion();
            Phase = OfficeRuntimeAgentPhase.Idle;
            CurrentActivity = OfficeActivity.Break;
            SetPresentationAway(false);
        }

        public void SetAttendanceOutside(bool outside, bool walkToExit)
        {
            if (_qaControl) return;
            if (outside)
            {
                if (_presentationAway && Phase == OfficeRuntimeAgentPhase.Outside) return;
                if (walkToExit)
                {
                    if (_attendanceDepartureActive) return;
                    if (!_world.Workstations.TryResolveDestination(
                            OfficeSemanticLocation.Exit,
                            _agentId,
                            "attendance-exit:" + _agentId,
                            out OfficeRuntimeDestination departure)) return;
                    _attendanceDepartureActive = BeginDestination(departure);
                    return;
                }
                EndInteraction(
                    OfficeRuntimeInteractionTermination.Aborted,
                    OfficeRuntimeInteractionEndReason.Cleared);
                _destination = null;
                _pendingDestination = null;
                _attendanceArrivalActive = false;
                ReleaseAttendanceIngress();
                _path.Clear();
                _pathIndex = 0;
                _arrived = true;
                ReleaseSeatImmediately();
                ResetVisualPose();
                _animator?.ResumeWalkingAfterSeating();
                StopMotion();
                Phase = OfficeRuntimeAgentPhase.Outside;
                CurrentActivity = OfficeActivity.Outside;
                SetPresentationAway(true);
                return;
            }

            if (!_presentationAway && Phase != OfficeRuntimeAgentPhase.Outside) return;
            _attendanceDepartureActive = false;
            if (!_world.Workstations.TryResolveAttendanceEntrance(
                    _agentId,
                    "attendance-entry:" + _agentId,
                    out OfficeRuntimeDestination entry)) return;
            if ((!_preparedAttendanceDestination.HasValue || _preparedAttendancePath.Count == 0) &&
                !PrepareAttendanceArrival()) return;
            if (BeginPreparedAttendanceIngress(
                    _preparedAttendanceDestination.Value,
                    _preparedAttendancePath))
            {
                _attendanceArrivalActive = true;
                Debug.Log(
                    "STARTER_OFFICE_ATTENDANCE_ENTRY | member=" + _agentId +
                    " | exterior=" + _attendanceIngressExteriorWorld.ToString("F3") +
                    " | entrance=" + _attendanceIngressInteriorWorld.ToString("F3") +
                    " | routeCells=" + _preparedAttendancePath.Count +
                    " | route=" + string.Join(">", _preparedAttendancePath) +
                    " | destination=" + _preparedAttendanceDestination.Value.DestinationId);
            }
        }

        /// <summary>
        /// Resolves the family's canonical arrival route while the loading presentation is visible.
        /// A furnished office targets the assigned desk; a legitimate empty new office targets a
        /// deterministic open tile until the player buys furniture. The 09:00 arrival adopts this
        /// prewarmed route, avoiding a synchronous path search or a temporary corridor stop.
        /// </summary>
        public bool PrepareAttendanceArrival()
        {
            _preparedAttendanceDestination = null;
            _preparedAttendancePath.Clear();
            OfficeGridCoordinate entrance = OfficeRuntimeWorkstationService.StarterEntranceCell;
            if (!_world.Grid.Contains(entrance) || !_world.Grid.IsWalkable(entrance)) return false;
            if (!_world.Workstations.TryResolveDestination(
                    OfficeSemanticLocation.Desk,
                    _agentId,
                    "attendance-desk:" + _agentId,
                    out OfficeRuntimeDestination destination) &&
                !_world.Workstations.TryResolveDestination(
                    OfficeSemanticLocation.OpenArea,
                    _agentId,
                    "attendance-empty-office:" + _agentId,
                    out destination)) return false;
            IReadOnlyList<OfficeGridCoordinate> route = _world.Paths.FindPath(
                _agentId,
                entrance,
                destination.Cell,
                destination.SeatId,
                false,
                AgentRadius);
            if (route.Count < 2) return false;
            _preparedAttendanceDestination = destination;
            _preparedAttendancePath.AddRange(route);
            return true;
        }

        public void SetPlayerInput(Vector2 input)
        {
            if (!_playerControlled || _qaControl) return;
            _playerInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void QaSetPlayerInput(Vector2 input)
        {
            if (!_playerControlled || !_qaControl) return;
            _playerInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// Opt-in Player-QA hook that exercises the normal direct movement, collision and gait
        /// pipeline on a non-player family member. It is inert unless BeginQaControl has already
        /// isolated the actor and never changes normal NPC control.
        /// </summary>
        public void QaSetDirectMovementInput(Vector2 input)
        {
            if (!_qaControl) return;
            _qaDirectMovementControl = true;
            _playerInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// Declares that another full-body presenter owns directional seated pixels while the
        /// production agent retains route, facing, chair claim and atomic docking authority.
        /// Ordinary 2D actors never enable this and keep the SafeStaticWork northwest-only gate.
        /// </summary>
        public void SetExternalDirectionalSeatingPresentation(bool enabled)
        {
            _externalDirectionalSeatingPresentation = enabled;
            _animator?.SetExternalDirectionalSeatingPresentation(enabled);
        }

        /// <summary>
        /// QA-only catalog sweep hook.  It keeps the real actor, navigation displacement, shared
        /// gait state and final SpriteRenderer, and replaces only the 48-frame walk consumer input.
        /// Normal gameplay never calls this method.
        /// </summary>
        public void QaReplaceWalkFrames(Sprite[] frames)
        {
            if (!_qaControl) throw new InvalidOperationException("QA control must be active.");
            if (frames == null || frames.Length != DirectionalSpriteAnimator.RequiredFrameCount)
                throw new ArgumentException("QA walk catalog requires exactly 48 non-null sprites.", nameof(frames));
            for (var index = 0; index < frames.Length; index++)
                if (frames[index] == null)
                    throw new ArgumentException("QA walk catalog contains a null sprite.", nameof(frames));
            _animator.Configure(_renderer, frames);
        }

        public void BeginQaControl()
        {
            _qaControl = true;
            _qaDirectMovementControl = false;
            ResetRuntimeState();
        }

        public void EndQaControl()
        {
            _qaControl = false;
            _qaDirectMovementControl = false;
            _playerInput = Vector2.zero;
            ResetRuntimeState();
        }

        private bool HasDirectMovementControl =>
            _playerControlled || (_qaControl && _qaDirectMovementControl);

        public void QaTeleportToCell(OfficeGridCoordinate cell)
        {
            if (!_world.Grid.Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            BeginQaControl();
            Vector3 target = _world.Presenter.CellCenterWorld(cell);
            transform.position = new Vector3(target.x, target.y, transform.position.z);
            _world.Occupancy.UpdateActor(_agentId, Position, Vector2.zero, 0f);
            var anchor = new Vector2(target.x, target.y);
            _r5eVisualBaselineWorld = anchor;
            _r5ePreviousLogicalWorld = anchor;
            _r5ePreviousVisualWorld = anchor;
            _r5ePreviousWorld = anchor;
            _r5ePreviousRenderedWorld = anchor;
            _r5eCollisionSweepOrigin = anchor;
            _animator.RebaseTileMotionAfterAtomicPlacement(_animator.CurrentDirection);
        }

        public bool QaMoveToCell(OfficeGridCoordinate cell, string scenarioId)
        {
            if (!_qaControl) BeginQaControl();
            return BeginDestination(new OfficeRuntimeDestination(
                "qa:" + (scenarioId ?? string.Empty) + ":" + _agentId,
                OfficeSemanticLocation.None,
                OfficeActivity.Walking,
                cell));
        }

        public bool QaBeginSeatedWork(string scenarioId)
        {
            if (!_qaControl) BeginQaControl();
            ResetSeatingObservationMetrics();
            if (!_world.Workstations.TryResolveActivityDestination(
                    OfficeActivity.Work,
                    _agentId,
                    scenarioId ?? "qa-seated-work",
                    out OfficeRuntimeDestination destination)) return false;
            return BeginDestination(destination);
        }

        public bool QaBeginSeatedWorkAtSeat(string seatId, string scenarioId)
        {
            if (!_qaControl) BeginQaControl();
            ResetSeatingObservationMetrics();
            OfficeSeatSlot requested = _world.Workstations.RequiredSeat(seatId);
            var requestedDestination = new OfficeRuntimeDestination(
                "qa-r5e-seat",
                OfficeSemanticLocation.Desk,
                OfficeActivity.Work,
                requested.ApproachCell);
            return BeginDestination(_world.Workstations.DestinationForSeat(
                requested,
                requestedDestination));
        }

        public void QaArmR5eFault(int faultInjectionId)
        {
            if (faultInjectionId < 1 || faultInjectionId > 6)
                throw new ArgumentOutOfRangeException(nameof(faultInjectionId));
            _r5eTraceCoordinator.ArmFault(
                _agentId,
                (R5eFaultInjectionPoint)faultInjectionId);
        }

        public void QaInvalidateNextAtomicVersion() => _r5eQaInvalidateAtomicVersion = true;

        public bool QaBeginSemanticLocation(
            OfficeSemanticLocation location,
            string scenarioId,
            out OfficeGridCoordinate destinationCell)
        {
            if (!_qaControl) BeginQaControl();
            if (!_world.Workstations.TryResolveDestination(
                    location,
                    _agentId,
                    scenarioId ?? "qa-semantic-location",
                    out OfficeRuntimeDestination destination))
            {
                destinationCell = default;
                return false;
            }
            destinationCell = destination.Cell;
            return BeginDestination(destination);
        }

        public bool QaRequestStand()
        {
            if (!_qaControl || Phase != OfficeRuntimeAgentPhase.Working) return false;
            RequestStopAndStand();
            return true;
        }

        public bool QaRequestStandAndWalkToCell(
            OfficeGridCoordinate destinationCell,
            string scenarioId)
        {
            if (!_qaControl || Phase != OfficeRuntimeAgentPhase.Working ||
                !_world.Grid.Contains(destinationCell) ||
                !_world.Grid.IsWalkable(destinationCell)) return false;
            _pendingDestination = new OfficeRuntimeDestination(
                "qa-exit:" + (scenarioId ?? string.Empty) + ":" + _agentId,
                OfficeSemanticLocation.None,
                OfficeActivity.Walking,
                destinationCell);
            RequestStopAndStand();
            return true;
        }

        public bool QaRequestStandWithOutwardRoute()
        {
            if (!_qaControl || Phase != OfficeRuntimeAgentPhase.Working) return false;
            _r5eQaOutwardRouteRequested = true;
            RequestStopAndStand();
            return true;
        }

        internal bool QaTryGetActiveExitCells(
            out OfficeGridCoordinate front,
            out OfficeGridCoordinate left,
            out OfficeGridCoordinate right)
        {
            front = default;
            left = default;
            right = default;
            if (_seat == null ||
                !_world.Workstations.TryResolveDockingPlan(
                    _seat,
                    out OfficeSeatDockingPlan plan)) return false;
            front = plan.FrontExit.Cell;
            left = plan.LeftExit.Cell;
            right = plan.RightExit.Cell;
            return true;
        }

        internal ulong CaptureR5eCoreStateHash()
        {
            R5eAgentStepSnapshot snapshot = CaptureR5eStepSnapshot();
            OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy =
                _world.Occupancy.CaptureCanonicalActorSnapshot(_agentId);
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                AddR5eHash(ref hash, snapshot.LogicalRoot.x);
                AddR5eHash(ref hash, snapshot.LogicalRoot.y);
                AddR5eHash(ref hash, snapshot.VisualRoot.x);
                AddR5eHash(ref hash, snapshot.VisualRoot.y);
                AddR5eHash(ref hash, snapshot.CurrentVelocity.x);
                AddR5eHash(ref hash, snapshot.CurrentVelocity.y);
                AddR5eHash(ref hash, snapshot.VisibleMotionDebtSeconds);
                AddR5eHash(ref hash, (int)snapshot.Phase);
                AddR5eHash(ref hash, occupancy.Position.x);
                AddR5eHash(ref hash, occupancy.Position.y);
                AddR5eHash(ref hash, occupancy.CurrentCell.X);
                AddR5eHash(ref hash, occupancy.CurrentCell.Y);
                AddR5eHash(ref hash, unchecked((int)occupancy.Epoch));
                AddR5eHash(ref hash, _seatClaim != null && !_seatClaim.IsReleased ? 1 : 0);
                AddR5eHash(ref hash, _seatClaim != null && _seatClaim.IsOccupied ? 1 : 0);
                return hash;
            }
        }

        internal bool TryObserveR5eRuntimeClearance(
            out bool floorValid,
            out bool staticOverlap,
            out bool dynamicOverlap)
        {
            floorValid = false;
            staticOverlap = true;
            dynamicOverlap = true;
            if (_world == null) return false;
            OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy =
                _world.Occupancy.CaptureCanonicalActorSnapshot(_agentId);
            if (!occupancy.IsPresent) return false;
            _world.Occupancy.ObserveAtomicPlacementClearance(
                _agentId,
                occupancy.Position,
                occupancy.CurrentCell,
                AgentRadius,
                _seat?.SeatId ?? string.Empty,
                out floorValid,
                out staticOverlap,
                out dynamicOverlap);
            return true;
        }

        private static void AddR5eHash(ref ulong hash, float value) =>
            AddR5eHash(ref hash, BitConverter.SingleToInt32Bits(value));

        private static void AddR5eHash(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        public bool QaReachedCell(OfficeGridCoordinate cell)
        {
            return _arrived &&
                   _world.Presenter.NearestCell(transform.position).Equals(cell) &&
                   Vector2.Distance(
                       Position,
                       (Vector2)_world.Presenter.CellCenterWorld(cell)) <= ArrivalDistance;
        }

        public bool TryBeginPlayerWork(OfficeActivity activity)
        {
            if (!_playerControlled) return false;
            if (activity == OfficeActivity.Work)
            {
                if (IsSeated && CurrentActivity == OfficeActivity.Work) return true;
                if (!_world.Workstations.TryResolveActivityDestination(
                        activity,
                        _agentId,
                        "player-work",
                        out OfficeRuntimeDestination destination)) return false;
                Vector3 approach = _world.Presenter.CellCenterWorld(destination.Cell);
                if (Vector2.Distance(Position, new Vector2(approach.x, approach.y)) > 1.20f) return false;
                if (!_destination.HasValue || _destination.Value.DestinationId != destination.DestinationId)
                    BeginDestination(destination);
                return IsSeated;
            }

            if (!_world.Workstations.TryResolveActivityDestination(
                    activity,
                    _agentId,
                    "player-work",
                    out OfficeRuntimeDestination standingDestination)) return false;
            Vector3 target = _world.Presenter.CellCenterWorld(standingDestination.Cell);
            return Vector2.Distance(Position, new Vector2(target.x, target.y)) <= 0.72f;
        }

        public void EndPlayerWork()
        {
            if (!_playerControlled || _qaControl) return;
            if (IsSeated || Phase == OfficeRuntimeAgentPhase.SittingDown || IsEnteringSeat)
                RequestStopAndStand("player-work-controller-reset");
        }

        public void InvalidatePath()
        {
            if (_interactionPhase != OfficeRuntimeInteractionPhase.None)
            {
                AbortInteractionAttempt(OfficeRuntimeInteractionEndReason.LayoutChanged);
                return;
            }
            _pathRevision = -1;
        }

        public void TickRuntime(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            try
            {
                TickRuntimeDispatch(deltaTime);
            }
            finally
            {
                PublishDurableR5eFirstWalk();
                if (HasActiveR5eStepObservation) SealR5eRuntimeStepDispatch();
            }
        }

        private void PublishDurableR5eFirstWalk()
        {
            if (!_r5eAwaitingFirstWalk ||
                !(_r5eRuntimeTick > _r5eTurnCompleteTick) ||
                _r5eAtomicPlacementThisStep ||
                _lastActualDisplacement.magnitude <=
                OfficeRuntimeTraceCoordinator.StationaryEpsilon) return;

            _r5eAwaitingFirstWalk = false;
            _r5eFirstWalkCount++;
            _r5eLastFirstWalkTick = _r5eRuntimeTick;
            _r5eLastFirstWalkDirection = _animator.ResolveConfiguredTileDirection(
                _lastActualDisplacement,
                _r5eLastAtomicExitDirection);
        }

        private void TickRuntimeDispatch(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);

            if (_attendanceIngressActive)
            {
                TickAttendanceIngress(deltaTime);
                return;
            }

            if (HasDirectMovementControl && !_attendanceDepartureActive && !_attendanceArrivalActive &&
                _playerInput.sqrMagnitude > 0.0001f)
            {
                if (Phase == OfficeRuntimeAgentPhase.Working ||
                    Phase == OfficeRuntimeAgentPhase.SittingDown ||
                    IsEnteringSeat)
                {
                    RequestStopAndStand("player-direct-movement");
                }
                else if (Phase == OfficeRuntimeAgentPhase.Idle || Phase == OfficeRuntimeAgentPhase.Navigating)
                {
                    if (_emptyOfficeWanderActive)
                    {
                        _emptyOfficeWanderActive = false;
                        _autonomyIntentId = string.Empty;
                        _autonomyRequestedLocation = OfficeSemanticLocation.None;
                        _autonomyRequestedInteractionId = string.Empty;
                        _autonomyDestination = null;
                        _autonomyLayoutRevision = -1;
                    }
                    _destination = null;
                    _path.Clear();
                    _navigationSegmentDirection = -1;
                    TickDirectPlayerMovement(deltaTime);
                    return;
                }
            }

            if (HasDirectMovementControl && _playerInput.sqrMagnitude <= 0.0001f &&
                !_destination.HasValue && Phase == OfficeRuntimeAgentPhase.Navigating)
            {
                Phase = OfficeRuntimeAgentPhase.Idle;
                CurrentActivity = OfficeActivity.Break;
            }

            switch (Phase)
            {
                case OfficeRuntimeAgentPhase.ApproachingSeat:
                case OfficeRuntimeAgentPhase.AligningSeat:
                case OfficeRuntimeAgentPhase.RotatingToSeat:
                case OfficeRuntimeAgentPhase.SittingDown:
                case OfficeRuntimeAgentPhase.Working:
                case OfficeRuntimeAgentPhase.FinishingWork:
                case OfficeRuntimeAgentPhase.StandingUp:
                case OfficeRuntimeAgentPhase.LeavingSeat:
                    TickSeating(deltaTime);
                    return;
                case OfficeRuntimeAgentPhase.Outside:
                    StopMotion();
                    return;
            }

            if (_destination.HasValue && !_arrived)
            {
                TickNavigation(deltaTime);
                return;
            }

            StopMotion();
            TickArrivedWork(deltaTime);
        }

        private void SyncEmptyOfficeLocomotionPresentation()
        {
            if (_animator == null) return;
            bool emptyOffice = _world?.Grid != null && _world.Grid.SeatSlots.Count == 0;
            if (emptyOffice)
            {
                foreach (PlacedOfficeFurniture item in _world.Grid.Furniture)
                {
                    if (OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable != true) continue;
                    emptyOffice = false;
                    break;
                }
            }

            // The empty room is a continuous tile-centre walking space. Its actors use the stable
            // high-motion atlas for the planted pivot and every translated frame; as soon as the
            // player places editable furniture, the normal interaction transition presentation is
            // restored. Seating clips are independently owned and are never suppressed here.
            _animator.SetContinuousRouteLocomotionPresentation(emptyOffice);
        }

        internal R5eAgentStepSnapshot CaptureR5eStepSnapshot()
        {
            Vector2 visual = _visualRoot == null
                ? Position
                : new Vector2(_visualRoot.position.x, _visualRoot.position.y);
            return new R5eAgentStepSnapshot(
                Phase,
                Position,
                visual,
                _r5eVisualBaselineWorld,
                _r5ePreviousLogicalWorld,
                _r5ePreviousVisualWorld,
                _r5ePreviousWorld,
                _r5ePreviousRenderedWorld,
                _r5eCollisionSweepOrigin,
                _currentVelocity,
                _desiredVelocity,
                _visibleMotionDebtSeconds,
                _visibleFrameMovementBudgetWorld,
                _lastActualDisplacement,
                SemanticFrameDisplacement,
                AccumulatedFrameDisplacement,
                GaitDistance,
                GaitPhase01,
                CurrentWalkFrame,
                CurrentDirection,
                _r5eRouteGenerationId,
                _r5eActiveMovementHandoffId,
                _pathIndex,
                IsR5eSeatedPostState,
                _r5eExitTurnPending);
        }

        internal void BeginR5eRuntimeStep(
            in OfficeRuntimeStepTraceContext context,
            Vector2 beforePosition,
            in R5eAgentStepSnapshot preStep)
        {
            if (_r5eTraceCoordinator == null || _r5eTraceState == null)
                throw new InvalidOperationException("R5e runtime trace was not preloaded.");
            if (_r5ePendingStep.Began && !_r5ePendingStep.PostClearAppended)
                throw new InvalidOperationException("Previous R5e actor step did not reach its epilogue.");
            if (context.ActorRuntimeTick != NextR5eRuntimeTick)
                throw new InvalidOperationException("R5e actor runtime tick is not one-to-one with scheduler steps.");
            _r5eRuntimeTick = context.ActorRuntimeTick;
            _r5eAtomicPlacementThisStep = false;
            _r5ePendingStep = new R5ePendingRuntimeStep
            {
                Context = context,
                PreStep = preStep,
                WorldBefore = beforePosition,
                Began = true
            };
        }

        internal void AbortR5eRuntimeStep(in OfficeRuntimeStepTraceContext context)
        {
            if (!_r5ePendingStep.Began ||
                _r5ePendingStep.Context.ActorStepOrdinal != context.ActorStepOrdinal) return;
            _r5ePendingStep = default;
        }

        internal void BeginUnobservedR5eRuntimeStep()
        {
            _r5ePendingStep = default;
            _r5eRuntimeTick = NextR5eRuntimeTick;
            _r5eAtomicPlacementThisStep = false;
        }

        private void SealR5eRuntimeStepDispatch()
        {
            if (!_r5ePendingStep.Began ||
                _r5ePendingStep.Context.ActorRuntimeTick != _r5eRuntimeTick)
                throw new InvalidOperationException("R5e TickRuntime dispatch has no scheduler context.");
            // Observation only: a seated TickRuntime mutation must remain visible to the
            // immediate PreClear sample. Clearing it here would create a masked pass before
            // ClearInactiveVisibleMotionDebt and defeat clearMaskedViolationCount.
            _r5ePendingStep.DispatchSealed = true;
        }

        internal void AppendObservedPreClear(
            in OfficeRuntimeStepTraceContext context,
            in R5eAgentStepSnapshot preStep,
            in R5eAgentStepSnapshot preClear)
        {
            RequirePendingR5eStep(context, requirePreClear: false);
            if (!_r5ePendingStep.DispatchSealed)
                throw new InvalidOperationException("R5e pre-clear sample preceded TickRuntime epilogue.");
            if (IsR5eSeatedPostState)
            {
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                R5eProductionObservation observation = CaptureR5eProductionObservation(
                    _r5eActiveDockingPlan,
                    allocationStart);
                _r5eTraceState.AppendSeated(
                    R5eSeatedSamplePhase.PreClear,
                    context,
                    ActiveSeatId,
                    preStep,
                    preClear,
                    observation.Occupancy,
                    observation);
            }
            _r5ePendingStep.PreClearAppended = true;
        }

        internal void FinalizeR5eRuntimeStepPostClear(
            in OfficeRuntimeStepTraceContext context,
            in R5eAgentStepSnapshot preStep,
            in R5eAgentStepSnapshot preClear,
            in R5eAgentStepSnapshot postClear,
            bool expectedMoving)
        {
            RequirePendingR5eStep(context, requirePreClear: true);
            if (IsR5eSeatedPostState)
            {
                long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                R5eProductionObservation observation = CaptureR5eProductionObservation(
                    _r5eActiveDockingPlan,
                    allocationStart);
                _r5eTraceState.AppendSeated(
                    R5eSeatedSamplePhase.PostClear,
                    context,
                    ActiveSeatId,
                    preStep,
                    postClear,
                    observation.Occupancy,
                    observation);
                _r5eTraceState.RecordClearMask(preClear, postClear);
            }

            bool observedMoving =
                !_r5eAtomicPlacementThisStep &&
                Vector2.Distance(Position, _r5ePendingStep.WorldBefore) >
                OfficeRuntimeTraceCoordinator.StationaryEpsilon;
            bool firstWalk =
                observedMoving &&
                _r5eLastFirstWalkTick == _r5eRuntimeTick;
            var locomotion = new R5eLocomotionAdapterRow(
                context,
                _agentId,
                _r5eRouteGenerationId,
                _r5eActiveMovementHandoffId,
                preStep,
                postClear,
                _r5eAtomicPlacementThisStep,
                expectedMoving,
                observedMoving,
                firstWalk);
            _r5eTraceState.AppendLocomotion(locomotion);

            if (firstWalk)
            {
                if (_r5eAwaitingFirstWalkTransactionId != 0)
                    AppendR5eTransition(
                        _r5eAwaitingFirstWalkTransactionId,
                        R5eSeatTransitionEventKind.FirstWalk,
                        R5eSeatTransitionKind.Exit,
                        preStep,
                        postClear,
                        _r5eActiveDockingPlan,
                        _lastCompletedSeatEgressWorld,
                        true,
                        false,
                        false);
                _r5eAwaitingFirstWalkTransactionId = 0;
                _r5ePendingMovementHandoffId = 0;
                _r5eLastClosedSeatedSessionId = 0;
            }

            _r5ePreviousLogicalWorld = Position;
            _r5ePreviousVisualWorld = _r5eVisualBaselineWorld;
            _r5ePreviousWorld = Position;
            _r5ePreviousRenderedWorld = Position;
            _r5eCollisionSweepOrigin = Position;
            _r5ePendingStep.PostClearAppended = true;
        }

        internal bool AtomicPlacementOccurred(ulong runtimeTick) =>
            _r5eAtomicPlacementThisStep && _r5eAtomicPlacementTick == runtimeTick;

        internal DirectionalLocomotionFrameTrace CaptureR5eAcceptedLocomotionFrameTrace() =>
            _animator.CaptureLocomotionFrameTrace();

        private void RequirePendingR5eStep(
            in OfficeRuntimeStepTraceContext context,
            bool requirePreClear)
        {
            if (!_r5ePendingStep.Began ||
                _r5ePendingStep.Context.ActorStepOrdinal != context.ActorStepOrdinal ||
                _r5ePendingStep.Context.ActorRuntimeTick != context.ActorRuntimeTick ||
                (requirePreClear && !_r5ePendingStep.PreClearAppended))
                throw new InvalidOperationException("R5e actor step phase pair is missing or mismatched.");
        }

        private void AdvanceR5eRouteGeneration()
        {
            if (_r5eRouteGenerationId == ulong.MaxValue)
                throw new OverflowException("R5e route generation cannot wrap.");
            _r5eRouteGenerationId++;
            if (_r5ePendingMovementHandoffId != 0)
            {
                _r5eActiveMovementHandoffId = _r5ePendingMovementHandoffId;
            }
        }

        public void BeginPresentationFrame()
        {
            _visibleFrameMovementBudgetWorld =
                DefaultMoveSpeed * OfficeRuntimeWorld.MaximumVisibleMotionDeltaSeconds;
            _visibleFrameMovementWorld = 0f;
            _seatEgressFrameMovementBudgetWorld =
                MaximumSeatEgressStepPx / OfficeGridTilemapPresenter.PixelsPerUnit;
            _seatEgressFrameMovementWorld = 0f;
            _animator?.BeginTilePresentationFrame();
        }

        public void TickPresentation(float deltaTime)
        {
            if (_animator == null || deltaTime < 0f) return;
            // Office time can run at 2x/4x, but a human sit/stand gesture should keep its real
            // 0.62s/0.56s presentation duration instead of dropping into the chair in a few ticks.
            // Unity's capture clock is deterministic even when synchronous ReadPixels/PNG work
            // takes much longer than one rendered frame. Prefer it while a capture is active so
            // seating and typing cannot skip approved frames because of capture wall-clock time.
            float seatingPresentationDeltaTime = Time.captureDeltaTime > 0f
                ? Time.captureDeltaTime
                : Time.unscaledDeltaTime;
            float presentationDeltaTime = _animator.IsOfficeSeatingPoseActive
                ? Mathf.Max(0f, seatingPresentationDeltaTime)
                : deltaTime;
            RecordChairPresentationMotion();
            using (OfficePerformanceTelemetry.Measure(OfficePerformancePath.AnimatorTick))
                _animator.Tick(presentationDeltaTime);
            _animator.EndTilePresentationFrame();
            ApplyLocomotionFootPlantPresentation();
            _playerNaturalWalk?.Present(
                _animator.GaitPhase01,
                _animator.CurrentDirection,
                _animator.IsMoving,
                _animator.IsOfficeSeatingPoseActive || IsOccupyingSeat || IsEnteringSeat,
                _presentationAway,
                _playerNaturalTurnTargetDirection >= 0,
                PlayerNaturalTurnSeconds <= 0f
                    ? 1f
                    : _playerNaturalTurnElapsedSeconds / PlayerNaturalTurnSeconds,
                _playerNaturalTurnFromDirection,
                _playerNaturalTurnTargetDirection);
            _playerBakedWalk?.Present(
                _animator.GaitPhase01,
                _animator.CurrentDirection,
                _animator.IsMoving,
                _animator.IsOfficeSeatingPoseActive || IsOccupyingSeat || IsEnteringSeat,
                _presentationAway,
                _playerNaturalTurnTargetDirection >= 0,
                PlayerNaturalTurnSeconds <= 0f
                    ? 1f
                    : _playerNaturalTurnElapsedSeconds / PlayerNaturalTurnSeconds,
                _playerNaturalTurnFromDirection,
                _playerNaturalTurnTargetDirection);
            if (Phase == OfficeRuntimeAgentPhase.FinishingWork)
                _finishingWorkPresentationObserved = true;
            RecordSeatingFacingInvariant();
            if (_seat != null && _renderer != null)
            {
                _world.Workstations.ApplyPresentationStack(_seat, _renderer, transform.position);
            }
        }

        private bool TryStartAutonomyRequest()
        {
            if (_playerControlled || _qaControl || HasAssignedTask || _attendanceArrivalActive ||
                _attendanceWorkHandoffActive ||
                _autonomyIntentId.Length == 0)
                return false;

            OfficeRuntimeDestination destination;
            OfficeRuntimeInteractionHandle handle = null;
            if (_autonomyRequestedInteractionId.Length == 0)
            {
                if (!_world.Workstations.TryResolveDestination(
                        _autonomyRequestedLocation,
                        _agentId,
                        _autonomyIntentId,
                        out destination))
                {
                    _autonomyDestination = null;
                    _autonomyLayoutRevision = -1;
                    RequestStopAndStand();
                    return false;
                }
            }
            else
            {
                _interactionPhase = OfficeRuntimeInteractionPhase.Reserving;
                OfficeGridCoordinate start = _world.Presenter.NearestCell(transform.position);
                if (!_world.Workstations.TryBeginInteraction(
                        _autonomyRequestedInteractionId,
                        _agentId,
                        _autonomyIntentId,
                        start,
                        ActiveSeatId,
                        AgentRadius,
                        out destination,
                        out handle,
                        out OfficeRuntimeInteractionFailure failure))
                {
                    ClearInteractionExecutionState();
                    _autonomyDestination = null;
                    _autonomyLayoutRevision =
                        failure.Code == OfficeRuntimeInteractionFailureCode.UnsupportedReservationPolicy ||
                        failure.Code == OfficeRuntimeInteractionFailureCode.UnknownInteraction
                            ? _world.Occupancy.Revision
                            : -1;
                    RequestStopAndStand();
                    return false;
                }

                _interactionHandle = handle;
                _interactionPhase = OfficeRuntimeInteractionPhase.Navigating;
                _activeInteractionId = _autonomyRequestedInteractionId;
                _activeInteractionOfferId = destination.InteractionOfferId;
                _activeInteractionFurnitureId = destination.FurnitureId;
            }

            _autonomyDestination = destination;
            _autonomyLayoutRevision = _world.Occupancy.Revision;
            if (BeginDestination(destination)) return true;

            if (_interactionPhase != OfficeRuntimeInteractionPhase.None)
            {
                AbortInteractionAttempt(OfficeRuntimeInteractionEndReason.PathUnavailable);
            }
            else
            {
                _world.Occupancy.ClearReservations(_agentId);
                _destination = null;
                _pendingDestination = null;
                _autonomyDestination = null;
                _autonomyLayoutRevision = -1;
                _path.Clear();
                _pathIndex = 0;
                _arrived = false;
                _yieldCell = null;
            }
            ReleaseSeatImmediately();
            Phase = OfficeRuntimeAgentPhase.Idle;
            CurrentActivity = OfficeActivity.Break;
            StopMotion();
            return false;
        }

        private bool BeginDestination(OfficeRuntimeDestination destination)
        {
            // A destination is a new distance contract. Never transfer catch-up time from an
            // earlier completed/cancelled/idle job into its first rendered frame.
            ClearVisibleMotionDebt();
            _standingFacingDirection = -1;
            _navigationSegmentDirection = -1;
            if (_presentationAway)
            {
                OfficeGridCoordinate returnCell = _world.Presenter.NearestCell(transform.position);
                if (!_world.Occupancy.IsCellPassable(
                        returnCell,
                        _agentId,
                        destination.SeatId,
                        true)) return false;
            }

            bool canReuseCurrentSeat = destination.RequiresSeat &&
                                       _seat != null &&
                                       _seatClaim != null &&
                                       !_seatClaim.IsReleased &&
                                       string.Equals(
                                           _seatClaim.SeatId,
                                           destination.SeatId,
                                           StringComparison.Ordinal) &&
                                       (IsEnteringSeat ||
                                        Phase == OfficeRuntimeAgentPhase.SittingDown ||
                                        Phase == OfficeRuntimeAgentPhase.Working);
            if (canReuseCurrentSeat)
            {
                _destination = _world.Workstations.DestinationForSeat(_seat, destination);
                _pendingDestination = null;
                CurrentActivity = destination.Activity;
                if (Phase == OfficeRuntimeAgentPhase.Working)
                {
                    _arrived = true;
                    if (_interactionPhase == OfficeRuntimeInteractionPhase.Navigating ||
                        _interactionPhase == OfficeRuntimeInteractionPhase.Aligning)
                        _interactionPhase = OfficeRuntimeInteractionPhase.Performing;
                }
                else if (_interactionPhase == OfficeRuntimeInteractionPhase.Navigating)
                {
                    _interactionPhase = OfficeRuntimeInteractionPhase.Aligning;
                }
                return true;
            }

            if (Phase == OfficeRuntimeAgentPhase.Working ||
                Phase == OfficeRuntimeAgentPhase.SittingDown ||
                IsEnteringSeat ||
                Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp ||
                Phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                if (_destination.HasValue &&
                    _destination.Value.DestinationId == destination.DestinationId &&
                    destination.RequiresSeat) return true;
                _pendingDestination = destination;
                RequestStopAndStand("destination:" + destination.DestinationId);
                return true;
            }

            if (destination.RequiresSeat)
            {
                if (_seatClaim == null || _seatClaim.IsReleased ||
                    !string.Equals(_seatClaim.SeatId, destination.SeatId, StringComparison.Ordinal))
                {
                    ReleaseSeatImmediately();
                    if (!_world.Workstations.TryReserveSeat(
                            _agentId,
                            destination.SeatId,
                            _seatReservationToken,
                            out _seat,
                            out _seatClaim)) return false;
                    destination = _world.Workstations.DestinationForSeat(_seat, destination);
                }
            }
            else if (_seatClaim != null)
            {
                ReleaseSeatImmediately();
            }

            SetPresentationAway(false);
            _destination = destination;
            _pendingDestination = null;
            _arrived = false;
            CurrentActivity = OfficeActivity.Walking;
            Phase = OfficeRuntimeAgentPhase.Navigating;
            return RebuildPath();
        }

        private bool BeginPreparedAttendanceIngress(
            OfficeRuntimeDestination destination,
            IReadOnlyList<OfficeGridCoordinate> route)
        {
            if (route == null || route.Count < 2) return false;
            if (!route[0].Equals(OfficeRuntimeWorkstationService.StarterEntranceCell) ||
                !route[route.Count - 1].Equals(destination.Cell)) return false;

            Vector3 entranceWorld3 = _world.Presenter.CellCenterWorld(route[0]);
            Vector3 nextWorld3 = _world.Presenter.CellCenterWorld(route[1]);
            var entranceWorld = new Vector2(entranceWorld3.x, entranceWorld3.y);
            var inwardSegment = new Vector2(
                nextWorld3.x - entranceWorld3.x,
                nextWorld3.y - entranceWorld3.y);
            if (inwardSegment.sqrMagnitude <= 0.0001f) return false;
            Vector2 exteriorWorld =
                entranceWorld - inwardSegment * AttendanceExteriorPathSegmentMultiplier;
            if (!_world.Occupancy.TryClaimAttendanceIngress(
                    _agentId,
                    exteriorWorld,
                    entranceWorld,
                    AgentRadius)) return false;

            _standingFacingDirection = -1;
            _navigationSegmentDirection = -1;
            ReleaseSeatImmediately();
            if (destination.RequiresSeat)
            {
                if (!_world.Workstations.TryReserveSeat(
                        _agentId,
                        destination.SeatId,
                        _seatReservationToken,
                        out _seat,
                        out _seatClaim))
                {
                    _world.Occupancy.ReleaseAttendanceIngress(_agentId);
                    return false;
                }
                destination = _world.Workstations.DestinationForSeat(_seat, destination);
            }

            transform.position = new Vector3(exteriorWorld.x, exteriorWorld.y, transform.position.z);
            _attendanceIngressExteriorWorld = exteriorWorld;
            _attendanceIngressInteriorWorld = entranceWorld;
            _attendanceIngressActive = true;
            SetPresentationAway(false);
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                Vector2.zero,
                0f,
                destination.SeatId);
            _destination = destination;
            _pendingDestination = null;
            _arrived = false;
            CurrentActivity = OfficeActivity.Walking;
            Phase = OfficeRuntimeAgentPhase.Navigating;
            _path.Clear();
            _path.AddRange(route);
            _pathIndex = 1;
            _presentationPathIndex = _pathIndex;
            _pathRevision = _world.Occupancy.Revision;
            AdvanceR5eRouteGeneration();
            return true;
        }

        internal float ConsumeVisibleMotionDelta(float scaledFrameDeltaTime)
        {
            OfficeVisibleMotionBudget budget = OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                HasActiveVisibleMotionIntent,
                _visibleMotionDebtSeconds,
                scaledFrameDeltaTime);
            _visibleMotionDebtSeconds = budget.RemainingDebtSeconds;
            return budget.ConsumedSeconds;
        }

        internal void ClearInactiveVisibleMotionDebt()
        {
            if (!HasActiveVisibleMotionIntent) ClearVisibleMotionDebt();
        }

        private void ClearVisibleMotionDebt()
        {
            _visibleMotionDebtSeconds = 0f;
        }

        // R5e adapter boundary. Kept outside the accepted e36875 consume/clear hunk so the
        // original movement scheduler and debt bytes remain contiguous and independently locked.
        internal void PrepareR5eStationaryFrameAfterAcceptedMotionBudget()
        {
            if (!IsR5eSeatedPostState && !_r5eExitTurnPending) return;
            _visibleMotionDebtSeconds = 0f;
            _visibleFrameMovementBudgetWorld = 0f;
            _visibleFrameMovementWorld = 0f;
        }

        private void TickAttendanceIngress(float deltaTime)
        {
            Vector2 delta = _attendanceIngressInteriorWorld - Position;
            if (delta.magnitude <= ArrivalDistance)
            {
                if (!TryConsumeAttendanceIngressEndpoint(
                        _attendanceIngressInteriorWorld,
                        deltaTime)) return;
                ReleaseAttendanceIngress();
                StopMotion();
                _world.Occupancy.UpdateActor(
                    _agentId,
                    Position,
                    Vector2.zero,
                    0f,
                    _seat?.SeatId ?? string.Empty);
                return;
            }

            float arrivalScale = Mathf.Clamp01(delta.magnitude / 0.32f);
            float speed = Mathf.Lerp(0.34f, DefaultMoveSpeed, arrivalScale);
            MoveAttendanceIngress(delta.normalized * speed, deltaTime, delta.magnitude);
        }

        private void MoveAttendanceIngress(
            Vector2 targetVelocity,
            float deltaTime,
            float maximumDistance)
        {
            if (_visibleFrameMovementBudgetWorld <= 0.0000001f)
            {
                _lastActualDisplacement = Vector2.zero;
                _desiredVelocity = targetVelocity;
                return;
            }
            float changePerSecond = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                OfficeNavigationMotionIntegrator.DefaultAcceleration,
                false);
            OfficeMotionIntegrationResult motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                changePerSecond,
                deltaTime);
            _currentVelocity = new Vector2(motion.Velocity.X, motion.Velocity.Z);
            OfficeNavPoint clamped = OfficeNavigationMotionIntegrator.ClampDisplacement(
                motion.Displacement,
                Mathf.Min(
                    Mathf.Max(0f, maximumDistance),
                    _visibleFrameMovementBudgetWorld));
            var intended = new Vector2(clamped.X, clamped.Z);
            Vector2 before = Position;
            Vector2 after = before + intended;
            Vector2 actual = _world.Occupancy.CanMoveAttendanceIngress(
                _agentId,
                before,
                after,
                AgentRadius)
                ? intended
                : Vector2.zero;
            if (actual.sqrMagnitude > OfficeRuntimeCollisionMotion.MinimumDisplacementSquared)
            {
                transform.position = new Vector3(after.x, after.y, transform.position.z);
                ConsumeVisibleFrameMovement(actual.magnitude);
                _stuckSeconds = Mathf.Max(0f, _stuckSeconds - deltaTime * 2f);
                LastMovementBlocker = string.Empty;
            }
            else
            {
                _currentVelocity = Vector2.zero;
                _stuckSeconds += deltaTime;
                LastMovementBlocker = "attendance-ingress-reserved";
            }
            _animator.AccumulateTileMotion(targetVelocity, actual, deltaTime, false);
            _lastActualDisplacement = actual;
            _desiredVelocity = targetVelocity;
            _world.Workstations.ApplyDynamicCharacterOrder(_renderer, transform.position);
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);
        }

        private bool TryConsumeAttendanceIngressEndpoint(Vector2 target, float deltaTime)
        {
            Vector2 before = Position;
            Vector2 intended = target - before;
            float distance = intended.magnitude;
            if (distance > _visibleFrameMovementBudgetWorld + 0.0000001f) return false;
            if (distance <= 0.0000001f) return true;
            if (!_world.Occupancy.CanMoveAttendanceIngress(
                    _agentId,
                    before,
                    target,
                    AgentRadius))
            {
                LastMovementBlocker = "attendance-ingress-endpoint";
                return false;
            }
            transform.position = new Vector3(target.x, target.y, transform.position.z);
            ConsumeVisibleFrameMovement(distance);
            Vector2 semanticVelocity = intended.normalized * DefaultMoveSpeed;
            _animator.AccumulateTileMotion(semanticVelocity, intended, deltaTime, false);
            _lastActualDisplacement = intended;
            _desiredVelocity = semanticVelocity;
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);
            LastMovementBlocker = string.Empty;
            return true;
        }

        private void ReleaseAttendanceIngress()
        {
            if (_world != null && !string.IsNullOrEmpty(_agentId))
                _world.Occupancy.ReleaseAttendanceIngress(_agentId);
            _attendanceIngressActive = false;
            _attendanceIngressExteriorWorld = Vector2.zero;
            _attendanceIngressInteriorWorld = Vector2.zero;
        }

        private bool RebuildPath()
        {
            if (!_destination.HasValue) return false;
            OfficeGridCoordinate start = _world.Presenter.NearestCell(transform.position);
            IReadOnlyList<OfficeGridCoordinate> result = _world.FindPath(
                _agentId,
                start,
                _destination.Value.Cell,
                _destination.Value.SeatId,
                _emptyOfficeWanderActive ||
                _stuckSeconds >= OfficeNavigationTrafficRules.ReplanThresholdSeconds,
                AgentRadius);
            _path.Clear();
            _path.AddRange(result);
            _pathIndex = _path.Count > 1 ? 1 : 0;
            _presentationPathIndex = _pathIndex;
            _pathRevision = _world.Occupancy.Revision;
            if (_path.Count == 0) return false;
            AdvanceR5eRouteGeneration();
            if (_path.Count == 1) CompleteNavigation();
            return true;
        }

        private void TickNavigation(float deltaTime)
        {
            if (!_destination.HasValue) return;
            if (_pathRevision != _world.Occupancy.Revision || _path.Count == 0)
            {
                if (!RebuildPath())
                {
                    if (_interactionPhase != OfficeRuntimeInteractionPhase.None)
                        AbortInteractionAttempt(OfficeRuntimeInteractionEndReason.PathUnavailable);
                    StopMotion();
                    return;
                }
            }
            if (_arrived) return;
            OfficeGridCoordinate currentCell = _world.Presenter.NearestCell(transform.position);
            int presentationTargetIndex = _world.Paths.ResolvePresentationTargetIndex(
                _path,
                _pathIndex,
                _agentId,
                Position,
                AgentRadius,
                _destination.Value.SeatId);
            _pathIndex = OfficeSemanticPathProgressRules.AdvanceThroughOccupiedCell(
                _path,
                _pathIndex,
                presentationTargetIndex,
                currentCell);
            _upcomingPathCells.Clear();
            for (var index = _pathIndex; index < _path.Count && _upcomingPathCells.Count < 2; index++)
                _upcomingPathCells.Add(_path[index]);
            if (presentationTargetIndex != _presentationPathIndex)
            {
                _presentationPathIndex = presentationTargetIndex;
                _presentationWaypointChanges++;
            }
            Vector3 target3 = _world.Presenter.CellCenterWorld(_path[presentationTargetIndex]);
            Vector2 target = new Vector2(target3.x, target3.y);
            Vector2 delta = target - Position;
            Vector2 desiredDirection = delta.sqrMagnitude > 0.000001f
                ? delta.normalized
                : Vector2.zero;
            TrackNavigationSegmentDirection(desiredDirection);
            Vector2 presentationSemanticDirection = desiredDirection;
            // Stay on the semantic segment until its exact cell-center arrival. Blending the root
            // toward the next leg cuts the inside corner: a route that is valid center-to-center
            // can then drift into a neighbouring furniture clearance mask and never recover.
            // The gait state performs the visible stop/pivot before the next segment accelerates.
            float arrivalSpeedScale = presentationTargetIndex == _path.Count - 1
                ? OfficeNavigationMotionIntegrator.ResolveArrivalSpeedScale(delta.magnitude)
                : 1f;
            _desiredVelocity = desiredDirection * (DefaultMoveSpeed * arrivalSpeedScale);
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);
            if (!_world.Occupancy.TryReservePath(_agentId, currentCell, _upcomingPathCells))
            {
                LastReservationBlocker = _world.Occupancy.DescribePathReservationBlocker(
                    _agentId,
                    currentCell,
                    _upcomingPathCells);
                _stuckSeconds += deltaTime;
                if (_emptyOfficeWanderActive)
                {
                    StopMotion(keepStuck: true);
                    if (_stuckSeconds >= OfficeNavigationTrafficRules.ReplanThresholdSeconds)
                        _pathRevision = -1;
                    return;
                }
                OfficeTrafficDecision blockedTraffic = _world.ResolveTraffic(
                    _agentId,
                    Position,
                    _desiredVelocity,
                    AgentRadius,
                    _stuckSeconds);
                if (_stuckSeconds >= OfficeNavigationTrafficRules.RecoveryThresholdSeconds &&
                    TryTickGridYield(
                        currentCell,
                        _upcomingPathCells,
                        deltaTime,
                        _destination.Value.SeatId))
                {
                    if (_stuckSeconds >= 2.0f) _world.Occupancy.ClearReservations(_agentId);
                    return;
                }
                if (blockedTraffic.RecoveryWeight > 0f)
                {
                    var recovery = new Vector2(
                        blockedTraffic.RecoveryDirection.X,
                        blockedTraffic.RecoveryDirection.Z) * (DefaultMoveSpeed * 0.72f);
                    MoveWithCollision(recovery, deltaTime, _destination.Value.SeatId);
                }
                else StopMotion(keepStuck: true);
                if (blockedTraffic.ShouldReplan || _stuckSeconds >= 1.10f) _pathRevision = -1;
                if (_stuckSeconds >= 2.0f) _world.Occupancy.ClearReservations(_agentId);
                return;
            }

            LastReservationBlocker = string.Empty;
            _yieldCell = null;

            if (delta.magnitude <= ArrivalDistance)
            {
                if (!TryConsumeExactEndpoint(
                        target,
                        deltaTime,
                        _destination.Value.SeatId,
                        desiredDirection * DefaultMoveSpeed)) return;
                _pathIndex = presentationTargetIndex + 1;
                if (_pathIndex >= _path.Count) CompleteNavigation();
                return;
            }

            OfficeTrafficDecision traffic = _world.ResolveTraffic(
                _agentId,
                Position,
                _desiredVelocity,
                AgentRadius,
                _stuckSeconds);
            Vector2 targetVelocity = _desiredVelocity * traffic.ForwardScale;
            if (!_emptyOfficeWanderActive && traffic.RecoveryWeight > 0f)
            {
                var recovery = new Vector2(
                    traffic.RecoveryDirection.X,
                    traffic.RecoveryDirection.Z) * DefaultMoveSpeed;
                targetVelocity = Vector2.Lerp(targetVelocity, recovery, traffic.RecoveryWeight);
            }
            if (traffic.ShouldReplan) _pathRevision = -1;
            MoveWithCollision(
                targetVelocity,
                deltaTime,
                _destination.Value.SeatId,
                delta.magnitude,
                presentationSemanticDirection * targetVelocity.magnitude);
        }

        private bool TryTickGridYield(
            OfficeGridCoordinate currentCell,
            IReadOnlyList<OfficeGridCoordinate> upcoming,
            float deltaTime,
            string permittedSeatId)
        {
            if (!_yieldCell.HasValue)
            {
                OfficeGridCoordinate forward = upcoming != null && upcoming.Count > 0
                    ? new OfficeGridCoordinate(
                        Math.Sign(upcoming[0].X - currentCell.X),
                        Math.Sign(upcoming[0].Y - currentCell.Y))
                    : new OfficeGridCoordinate(0, 0);
                var left = new OfficeGridCoordinate(-forward.Y, forward.X);
                var right = new OfficeGridCoordinate(forward.Y, -forward.X);
                bool preferLeft = StableYieldSide(_agentId);
                OfficeGridCoordinate reverse = new OfficeGridCoordinate(-forward.X, -forward.Y);
                for (var offsetIndex = 0; offsetIndex < 3; offsetIndex++)
                {
                    OfficeGridCoordinate offset = offsetIndex switch
                    {
                        0 => preferLeft ? left : right,
                        1 => preferLeft ? right : left,
                        _ => reverse
                    };
                    if (offset.X == 0 && offset.Y == 0) continue;
                    var candidate = new OfficeGridCoordinate(
                        currentCell.X + offset.X,
                        currentCell.Y + offset.Y);
                    if (!_world.Grid.Contains(candidate) ||
                        !_world.Occupancy.IsCellPassable(
                            candidate,
                            _agentId,
                            permittedSeatId,
                            true)) continue;
                    _yieldCell = candidate;
                    break;
                }
            }
            if (!_yieldCell.HasValue) return false;

            Vector3 target3 = _world.Presenter.CellCenterWorld(_yieldCell.Value);
            Vector2 target = new Vector2(target3.x, target3.y);
            Vector2 delta = target - Position;
            if (delta.magnitude <= ArrivalDistance)
            {
                if (!TryConsumeExactEndpoint(
                        target,
                        deltaTime,
                        permittedSeatId,
                        delta.sqrMagnitude > 0.000001f
                            ? delta.normalized * (DefaultMoveSpeed * 0.72f)
                            : Vector2.zero)) return true;
                _world.Occupancy.UpdateActor(
                    _agentId,
                    Position,
                    Vector2.zero,
                    _stuckSeconds,
                    permittedSeatId);
                _yieldCell = null;
                _pathRevision = -1;
                _stuckSeconds = Mathf.Max(
                    _stuckSeconds,
                    OfficeNavigationTrafficRules.ReplanThresholdSeconds);
                StopMotion(keepStuck: true);
                return true;
            }

            float preservedStuck = _stuckSeconds;
            MoveWithCollision(
                delta.normalized * (DefaultMoveSpeed * 0.72f),
                deltaTime,
                permittedSeatId,
                delta.magnitude);
            _stuckSeconds = Mathf.Max(preservedStuck, _stuckSeconds);
            return true;
        }

        private static bool StableYieldSide(string agentId)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = agentId ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }
                return (hash & 1) == 0;
            }
        }

        private void CompleteNavigation()
        {
            _arrived = true;
            _world.Occupancy.ClearReservations(_agentId);
            StopMotion();
            if (!_destination.HasValue) return;

            if (_interactionPhase == OfficeRuntimeInteractionPhase.Navigating)
            {
                OfficeGridCoordinate actualCell = _world.Presenter.NearestCell(transform.position);
                if (_interactionHandle != null &&
                    !_interactionHandle.TryValidateArrival(actualCell, out _))
                {
                    AbortInteractionAttempt(
                        OfficeRuntimeInteractionEndReason.ArrivalRevalidationFailed);
                    return;
                }
            }

            bool hasStandingFacing = !_destination.Value.RequiresSeat &&
                                     _world.Workstations.TryResolveStandingInteractionFacing(
                                         _destination.Value,
                                         out _standingFacingDirection);
            if (_interactionPhase == OfficeRuntimeInteractionPhase.Navigating)
                _interactionPhase = _destination.Value.RequiresSeat || hasStandingFacing
                    ? OfficeRuntimeInteractionPhase.Aligning
                    : OfficeRuntimeInteractionPhase.Performing;

            _world.NotifyArrival();
            CurrentActivity = _destination.Value.Activity;
            if (_destination.Value.RequiresSeat)
            {
                Phase = OfficeRuntimeAgentPhase.ApproachingSeat;
                return;
            }
            if (CurrentActivity == OfficeActivity.Outside)
            {
                Phase = OfficeRuntimeAgentPhase.Outside;
                SetPresentationAway(true);
            }
            else
            {
                Phase = OfficeRuntimeAgentPhase.Idle;
                _attendanceArrivalActive = false;
            }
        }

        private void TickSeating(float deltaTime)
        {
            if (Phase != OfficeRuntimeAgentPhase.LeavingSeat &&
                (_seat == null || _seatClaim == null || _seatClaim.IsReleased))
            {
                EndInteraction(
                    OfficeRuntimeInteractionTermination.Aborted,
                    OfficeRuntimeInteractionEndReason.SeatUnavailable);
                Phase = OfficeRuntimeAgentPhase.Idle;
                ReleaseSeatImmediately();
                ResumeAutonomy();
                return;
            }
            switch (Phase)
            {
                case OfficeRuntimeAgentPhase.ApproachingSeat:
                {
                    OfficeSeatInteractionAnchors anchors =
                        _world.Workstations.ResolveInteractionAnchors(_seat);
                    Vector2 target = anchors.ApproachWorld;
                    Vector2 delta = target - Position;
                    if (delta.magnitude > ArrivalDistance)
                    {
                        Vector2 velocity = delta.normalized * 1.15f;
                        MoveWithCollision(
                            velocity,
                            deltaTime,
                            _seat.SeatId,
                            delta.magnitude);
                        return;
                    }
                    if (!TryConsumeExactEndpoint(
                            target,
                            deltaTime,
                            _seat.SeatId,
                            delta.sqrMagnitude > 0.000001f
                                ? delta.normalized * 1.15f
                                : Vector2.zero)) return;
                    StopMotion();
                    Phase = OfficeRuntimeAgentPhase.AligningSeat;
                    break;
                }
                case OfficeRuntimeAgentPhase.AligningSeat:
                {
                    _seatDirection = FacingDirection(_seat.Facing);
                    if (!PrepareSeatAlignmentForWork())
                    {
                        EndInteraction(
                            OfficeRuntimeInteractionTermination.Aborted,
                            OfficeRuntimeInteractionEndReason.SeatUnavailable);
                        Phase = OfficeRuntimeAgentPhase.Idle;
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    if (!_world.Workstations.TryResolveDockingPlan(
                            _seat,
                            out _r5eActiveDockingPlan))
                    {
                        StopMotion();
                        return;
                    }
                    Vector2 target = _r5eActiveDockingPlan.DockWorld;
                    Vector2 delta = target - Position;
                    if (delta.magnitude > ArrivalDistance)
                    {
                        Vector2 velocity = delta.normalized * 1.15f;
                        MoveWithCollision(velocity, deltaTime, _seat.SeatId, delta.magnitude);
                        return;
                    }
                    if (!TryConsumeExactEndpoint(
                            target,
                            deltaTime,
                            _seat.SeatId,
                            delta.sqrMagnitude > 0.000001f
                                ? delta.normalized * 1.15f
                                : Vector2.zero)) return;
                    StopMotion();
                    Phase = OfficeRuntimeAgentPhase.RotatingToSeat;
                    break;
                }
                case OfficeRuntimeAgentPhase.RotatingToSeat:
                {
                    StopMotion();
                    if (_animator.CurrentDirection != _seatDirection)
                    {
                        // A 3D/full-body presenter applies its own visible yaw from the resolved
                        // workstation socket. Keep the simulation-facing octant in lockstep without
                        // waiting on the hidden 2D planted-turn frames; ordinary 2D actors retain
                        // the authored accumulated pivot below.
                        if (_externalDirectionalSeatingPresentation)
                            _animator.RestoreStandingFacing(_seatDirection);
                        else
                        {
                            _animator.AccumulateStandingFacingRequest(_seatDirection, deltaTime);
                            return;
                        }
                    }
                    if (!_animator.IsOfficeSeatingEntryPlanted ||
                        !_seatAlignmentComplete) return;
                    TryPublishR5eAtomicSeat();
                    break;
                }
                case OfficeRuntimeAgentPhase.SittingDown:
                    // Classic R5e docking never selects the crouching SitDown clip path.
                    StopMotion();
                    _seatEgressUnsafePhaseTransitionCount++;
                    break;
                case OfficeRuntimeAgentPhase.Working:
                    PrepareR5eStationaryFrameAfterAcceptedMotionBudget();
                    StopMotion();
                    TrackWorkstationMetrics();
                    if (HasAssignedTask && _assignedActivity == CurrentActivity)
                        AdvanceAssignedWork();
                    if (_releaseSeatRequested) BeginSafeStand();
                    break;
                case OfficeRuntimeAgentPhase.FinishingWork:
                    PrepareR5eStationaryFrameAfterAcceptedMotionBudget();
                    StopMotion();
                    if (!_finishingWorkPresentationObserved) return;
                    if (!_animator.IsOfficeWorkSafeToStand) return;
                    _r5eExitRetrySeconds += deltaTime;
                    if (_seatEgressWaiting && _r5eExitRetrySeconds < 0.5f) return;
                    _r5eExitRetrySeconds = 0f;
                    TryPublishR5eAtomicExit();
                    break;
                case OfficeRuntimeAgentPhase.StandingUp:
                    // Classic R5e egress never selects the crouching StandUp clip path.
                    StopMotion();
                    _seatEgressUnsafePhaseTransitionCount++;
                    break;
                case OfficeRuntimeAgentPhase.LeavingSeat:
                {
                    PrepareR5eStationaryFrameAfterAcceptedMotionBudget();
                    StopMotion();
                    if (!_r5eExitTurnPending) return;
                    if (_animator.CurrentDirection != _r5eExitTurnDirection ||
                        !_animator.IsReadyForInteractionFacing(_r5eExitTurnDirection))
                    {
                        _animator.AccumulateStandingFacingRequest(
                            _r5eExitTurnDirection,
                            deltaTime);
                        return;
                    }
                    CompleteR5eExitTurnAndPublishRoute();
                    break;
                }
            }
        }

        private bool TryPublishR5eAtomicSeat()
        {
            if (_seat == null || _seatClaim == null || _seatClaim.IsReleased ||
                !_world.Workstations.TryResolveDockingPlan(
                    _seat,
                    out OfficeSeatDockingPlan plan)) return false;

            bool observeTransition = TryBeginR5eTransitionObservation(
                plan,
                3,
                allocateSeatedSession: true,
                allocateMovementHandoff: false,
                out ulong transactionId,
                out ulong sessionId,
                out _);
            R5eAgentStepSnapshot before = CaptureR5eStepSnapshot();
            if (observeTransition)
                observeTransition = AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Prepare,
                    R5eSeatTransitionKind.Entry,
                    before,
                    before,
                    plan,
                    Vector2.zero,
                    false,
                    false,
                    false);

            if (observeTransition && !CanOpenR5eSeatedSessionNoThrow(sessionId, transactionId))
                observeTransition = false;
            if (!observeTransition)
            {
                transactionId = 0;
                sessionId = 0;
            }

            bool posePrepared = false;
            OfficeCharacterSeatPoseProfile workProfile = null;
            try
            {
                workProfile = ResolveSeatPresentationProfile(
                    OfficeSeatingAnimationClip.Work,
                    0);
                posePrepared = workProfile != null;
            }
            catch (Exception)
            {
                posePrepared = false;
            }

            OfficeSeatingState.PreparedRuntimeMutation preparedClaim = default;
            OfficeRuntimeOccupancy.PreparedAtomicActorPlacement preparedPlacement = default;
            bool prepared =
                posePrepared &&
                _animator.CanEnterCompletedSeatedWorkAfterAtomicPlacement(_seatDirection) &&
                _seatClaim.TryPrepareOccupy(out preparedClaim) &&
                _world.Occupancy.TryPrepareAtomicActorPlacement(
                    _agentId,
                    plan.SeatRootWorld,
                    _seat.Cell,
                    AgentRadius,
                    _seat.SeatId,
                    string.Empty,
                    plan.AnchorRevision,
                    out preparedPlacement);
            if (prepared && _r5eQaInvalidateAtomicVersion)
            {
                _r5eQaInvalidateAtomicVersion = false;
                _world.Occupancy.InvalidateAtomicTokenForQa(_agentId);
            }
            if (!prepared ||
                !_world.Workstations.IsDockingPlanCurrent(plan) ||
                !_seatClaim.IsPreparedMutationCurrent(preparedClaim) ||
                !_world.Occupancy.IsPreparedAtomicActorPlacementCurrent(preparedPlacement))
            {
                _world.Occupancy.CancelPreparedAtomicActorPlacement(preparedPlacement);
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Entry,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    Vector2.zero,
                    false,
                    true,
                    false);
                return false;
            }

            if (!TryCaptureR5eAtomicAgentSnapshot(out R5eAtomicAgentSnapshot agentSnapshot))
            {
                _world.Occupancy.CancelPreparedAtomicActorPlacement(preparedPlacement);
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Entry,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    Vector2.zero,
                    false,
                    true,
                    false);
                return false;
            }

            bool publishSucceeded;
            _r5ePublishActive = true;
            bool publishObservationEntered =
                TryEnterR5ePublishObservationNoThrow(observeTransition, transactionId);
            if (observeTransition && !publishObservationEntered)
            {
                observeTransition = false;
                transactionId = 0;
                sessionId = 0;
            }
            var publisher = new R5eEntryAtomicPublisher(
                this,
                preparedClaim,
                preparedPlacement,
                agentSnapshot,
                plan,
                workProfile,
                sessionId,
                transactionId);
            try
            {
                publishSucceeded = OfficeSeatDockingAtomicPublishPrimitive.TryPublish(ref publisher);
            }
            finally
            {
                ExitR5ePublishObservationNoThrow(publishObservationEntered, transactionId);
                _r5ePublishActive = false;
            }

            if (!publishSucceeded)
            {
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Entry,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    Vector2.zero,
                    false,
                    true,
                    false);
                return false;
            }

            _world.Occupancy.CompletePreparedAtomicActorPlacement(preparedPlacement);
            OpenR5eSeatedSessionNoThrow(observeTransition, sessionId, transactionId);

            R5eAgentStepSnapshot after = CaptureR5eStepSnapshot();
            AppendR5eTransition(
                transactionId,
                R5eSeatTransitionEventKind.Commit,
                R5eSeatTransitionKind.Entry,
                before,
                after,
                plan,
                Vector2.zero,
                true,
                false,
                false);
            AppendR5eTransition(
                transactionId,
                R5eSeatTransitionEventKind.Rebase,
                R5eSeatTransitionKind.Entry,
                before,
                after,
                plan,
                Vector2.zero,
                true,
                false,
                false);
            if (_attendanceArrivalActive)
            {
                _attendanceArrivalActive = false;
                _attendanceWorkHandoffActive = true;
                _attendanceSeatArrivalCount++;
                Debug.Log(
                    "STARTER_OFFICE_ATTENDANCE_SEATED | member=" + _agentId +
                    " | count=" + _attendanceSeatArrivalCount);
            }
            if (_interactionPhase == OfficeRuntimeInteractionPhase.Aligning)
                _interactionPhase = OfficeRuntimeInteractionPhase.Performing;
            return true;
        }

        private bool TryPublishR5eAtomicExit()
        {
            if (_seat == null || _seatClaim == null || _seatClaim.IsReleased ||
                !_world.Workstations.TryResolveDockingPlan(
                    _seat,
                    out OfficeSeatDockingPlan plan)) return false;

            bool observeTransition = TryBeginR5eTransitionObservation(
                plan,
                5,
                allocateSeatedSession: false,
                allocateMovementHandoff: true,
                out ulong transactionId,
                out _,
                out ulong handoffId);
            R5eAgentStepSnapshot before = CaptureR5eStepSnapshot();
            if (observeTransition)
                observeTransition = AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Prepare,
                    R5eSeatTransitionKind.Exit,
                    before,
                    before,
                    plan,
                    Vector2.zero,
                    false,
                    false,
                    false);
            if (!observeTransition)
            {
                transactionId = 0;
                handoffId = 0;
            }

            if (!TryCaptureR5eAtomicAgentSnapshot(out R5eAtomicAgentSnapshot agentSnapshot))
            {
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Exit,
                    before,
                    before,
                    plan,
                    Vector2.zero,
                    false,
                    true,
                    false);
                return false;
            }
            if (!TryPrepareSeatEgressReservation(
                    plan,
                    out OfficeRuntimeOccupancy.PreparedAtomicReservationScope reservationScope))
            {
                RestoreR5eAtomicAgentSnapshot(agentSnapshot);
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Exit,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    Vector2.zero,
                    false,
                    true,
                    false);
                return false;
            }

            OfficeSeatEgressAnchor exitAnchor = plan.Exit(_seatEgressCandidate.Kind);
            Vector2 exitWorld = new Vector2(exitAnchor.World.x, exitAnchor.World.y);
            int exitDirection = _animator.ResolveConfiguredTileDirection(
                exitWorld - plan.SeatRootWorld,
                _seatDirection);
            OfficeRuntimeDestination? preparedQaOutward = null;
            if (_r5eQaOutwardRouteRequested &&
                TryResolveQaOutwardDestination(exitAnchor, out OfficeGridCoordinate outwardCell))
                preparedQaOutward = new OfficeRuntimeDestination(
                    "qa-r5e-outward",
                    OfficeSemanticLocation.None,
                    OfficeActivity.Walking,
                    outwardCell);
            OfficeSeatingState.PreparedRuntimeMutation preparedClaim = default;
            OfficeRuntimeOccupancy.PreparedAtomicActorPlacement preparedPlacement = default;
            bool prepared =
                _animator.CanLeaveCompletedSeatedWorkAfterAtomicPlacement &&
                _seatClaim.TryPrepareRelease(out preparedClaim) &&
                _world.Occupancy.TryPrepareAtomicActorPlacement(
                    _agentId,
                    exitWorld,
                    exitAnchor.Cell,
                    AgentRadius,
                    string.Empty,
                    _agentId,
                    plan.AnchorRevision,
                    out preparedPlacement);
            if (prepared && _r5eQaInvalidateAtomicVersion)
            {
                _r5eQaInvalidateAtomicVersion = false;
                _world.Occupancy.InvalidateAtomicTokenForQa(_agentId);
            }
            if (!prepared ||
                !_world.Workstations.IsDockingPlanCurrent(plan) ||
                !_seatClaim.IsPreparedMutationCurrent(preparedClaim) ||
                !_world.Occupancy.IsPreparedAtomicActorPlacementCurrent(preparedPlacement))
            {
                _world.Occupancy.CancelPreparedAtomicActorPlacement(preparedPlacement);
                _world.Occupancy.RestoreAtomicReservationScope(reservationScope);
                RestoreR5eAtomicAgentSnapshot(agentSnapshot);
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Exit,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    exitWorld,
                    false,
                    true,
                    false);
                return false;
            }

            bool reservationPreparedBeforePublish = _seatEgressReservationActive;
            OfficeSeatSlot releasedSeat = _seat;
            OfficeSeatRuntimeClaim releasedClaim = _seatClaim;
            bool publishSucceeded;
            _r5ePublishActive = true;
            bool publishObservationEntered =
                TryEnterR5ePublishObservationNoThrow(observeTransition, transactionId);
            if (observeTransition && !publishObservationEntered)
            {
                observeTransition = false;
                transactionId = 0;
                handoffId = 0;
            }
            var publisher = new R5eExitAtomicPublisher(
                this,
                releasedClaim,
                preparedClaim,
                preparedPlacement,
                reservationScope,
                agentSnapshot,
                exitAnchor,
                exitWorld,
                exitDirection,
                handoffId,
                transactionId,
                preparedQaOutward);
            try
            {
                publishSucceeded = OfficeSeatDockingAtomicPublishPrimitive.TryPublish(ref publisher);
            }
            finally
            {
                ExitR5ePublishObservationNoThrow(publishObservationEntered, transactionId);
                _r5ePublishActive = false;
            }

            if (!publishSucceeded)
            {
                AppendR5eTransition(
                    transactionId,
                    R5eSeatTransitionEventKind.Rollback,
                    R5eSeatTransitionKind.Exit,
                    before,
                    CaptureR5eStepSnapshot(),
                    plan,
                    exitWorld,
                    false,
                    true,
                    false);
                return false;
            }

            _r5eLastAtomicExitReservationBacked = reservationPreparedBeforePublish;
            _r5eLastAtomicExitTick = _r5eAtomicPlacementTick;
            _r5eLastAtomicExitDirection = exitDirection;
            _world.Workstations.ClearOcclusionAfterCommittedExitNoThrow(releasedSeat);
            _world.Occupancy.CompletePreparedAtomicActorPlacement(preparedPlacement);
            _world.Occupancy.CompleteAtomicReservationScope(reservationScope);
            _animator.CompleteAtomicPresentationSessionNoThrow();
            CloseR5eSeatedSessionNoThrow(observeTransition, transactionId);
            R5eAgentStepSnapshot after = CaptureR5eStepSnapshot();
            AppendR5eTransition(
                transactionId,
                R5eSeatTransitionEventKind.Commit,
                R5eSeatTransitionKind.Exit,
                before,
                after,
                plan,
                exitWorld,
                true,
                false,
                false);
            AppendR5eTransition(
                transactionId,
                R5eSeatTransitionEventKind.Rebase,
                R5eSeatTransitionKind.Exit,
                before,
                after,
                plan,
                exitWorld,
                true,
                false,
                false);
            return true;
        }

        private void RebaseAfterAtomicPlacement(Vector2 anchor, int direction)
        {
            _currentVelocity = Vector2.zero;
            _desiredVelocity = Vector2.zero;
            _lastActualDisplacement = Vector2.zero;
            _stuckSeconds = 0f;
            _visibleMotionDebtSeconds = 0f;
            _visibleFrameMovementBudgetWorld = 0f;
            _visibleFrameMovementWorld = 0f;
            _seatEgressFrameMovementBudgetWorld = 0f;
            _seatEgressFrameMovementWorld = 0f;
            _path.Clear();
            _pathIndex = 0;
            _presentationPathIndex = -1;
            _pathRevision = _world.Occupancy.Revision;
            _yieldCell = null;
            _r5eVisualBaselineWorld = anchor;
            _r5ePreviousLogicalWorld = anchor;
            _r5ePreviousVisualWorld = anchor;
            _r5ePreviousWorld = anchor;
            _r5ePreviousRenderedWorld = anchor;
            _r5eCollisionSweepOrigin = anchor;
            _animator.RebaseTileMotionAfterAtomicPlacement(direction);
            _r5eAtomicPlacementThisStep = true;
            _r5eAtomicPlacementTick = _r5eRuntimeTick;
        }

        private bool TryCaptureR5eAtomicAgentSnapshot(out R5eAtomicAgentSnapshot snapshot)
        {
            snapshot = default;
            if (_path.Count > _r5eAtomicPathBackup.Length) return false;
            for (var index = 0; index < _path.Count; index++)
                _r5eAtomicPathBackup[index] = _path[index];
            snapshot = new R5eAtomicAgentSnapshot(
                transform.position,
                _visualRoot.localPosition,
                _visualRoot.localRotation,
                _visualRoot.localScale,
                Phase,
                CurrentActivity,
                _currentVelocity,
                _desiredVelocity,
                _lastActualDisplacement,
                _stuckSeconds,
                _visibleMotionDebtSeconds,
                _visibleFrameMovementBudgetWorld,
                _visibleFrameMovementWorld,
                _path.Count,
                _pathIndex,
                _pathRevision,
                _presentationPathIndex,
                _yieldCell,
                _arrived,
                _releaseSeatRequested,
                _seat,
                _seatClaim,
                _alignedClip,
                _alignedFrame,
                _seatedUpperBodyCutoffPx,
                _seatPresentationPrepared,
                _seatAlignmentComplete,
                _finishingWorkPresentationObserved,
                _seatEgressReservationActive,
                _seatEgressWaiting,
                _seatEgressReachedSafeAnchor,
                _seatEgressCandidate,
                _seatEgressTargetWorld,
                _hasCompletedSeatEgress,
                _lastCompletedSeatEgressKind,
                _lastCompletedSeatEgressCell,
                _lastCompletedSeatEgressWorld,
                _lastCompletedSeatEgressClearanceValid,
                _r5ePendingMovementHandoffId,
                _r5eActiveMovementHandoffId,
                _r5eExitTransactionId,
                _r5eExitTurnDirection,
                _r5eExitTurnPending,
                _r5eTurnCompleteTick,
                _r5eSeatedSessionId,
                _r5eLastClosedSeatedSessionId,
                _r5eVisualBaselineWorld,
                _r5ePreviousLogicalWorld,
                _r5ePreviousVisualWorld,
                _r5ePreviousWorld,
                _r5ePreviousRenderedWorld,
                _r5eCollisionSweepOrigin,
                _r5eAtomicPlacementThisStep,
                _r5eAtomicPlacementTick,
                _r5eQaOutwardRouteRequested,
                _r5eQaPreparedOutwardRoute,
                _pendingDestination,
                _r5eActiveDockingPlan,
                _r5eEntryTransactionId,
                _seatFacingAlignedBeforeSitDown,
                CaptureR5eRendererSnapshot(_renderer),
                CaptureR5eRendererSnapshot(_seatedUpperBodyRenderer),
                _animator.CaptureAtomicPresentationSnapshot());
            return true;
        }

        private void RestoreR5eAtomicAgentSnapshot(in R5eAtomicAgentSnapshot snapshot)
        {
            transform.position = snapshot.RootPosition;
            _visualRoot.localPosition = snapshot.VisualLocalPosition;
            _visualRoot.localRotation = snapshot.VisualLocalRotation;
            _visualRoot.localScale = snapshot.VisualLocalScale;
            Phase = snapshot.Phase;
            CurrentActivity = snapshot.Activity;
            _currentVelocity = snapshot.CurrentVelocity;
            _desiredVelocity = snapshot.DesiredVelocity;
            _lastActualDisplacement = snapshot.LastActualDisplacement;
            _stuckSeconds = snapshot.StuckSeconds;
            _visibleMotionDebtSeconds = snapshot.VisibleMotionDebtSeconds;
            _visibleFrameMovementBudgetWorld = snapshot.VisibleFrameMovementBudgetWorld;
            _visibleFrameMovementWorld = snapshot.VisibleFrameMovementWorld;
            _path.Clear();
            for (var index = 0; index < snapshot.PathCount; index++)
                _path.Add(_r5eAtomicPathBackup[index]);
            _pathIndex = snapshot.PathIndex;
            _pathRevision = snapshot.PathRevision;
            _presentationPathIndex = snapshot.PresentationPathIndex;
            _yieldCell = snapshot.YieldCell;
            _arrived = snapshot.Arrived;
            _releaseSeatRequested = snapshot.ReleaseSeatRequested;
            _seat = snapshot.Seat;
            _seatClaim = snapshot.SeatClaim;
            _alignedClip = snapshot.AlignedClip;
            _alignedFrame = snapshot.AlignedFrame;
            _seatedUpperBodyCutoffPx = snapshot.SeatedUpperBodyCutoffPx;
            _seatPresentationPrepared = snapshot.SeatPresentationPrepared;
            _seatAlignmentComplete = snapshot.SeatAlignmentComplete;
            _finishingWorkPresentationObserved = snapshot.FinishingWorkPresentationObserved;
            _seatEgressReservationActive = snapshot.SeatEgressReservationActive;
            _seatEgressWaiting = snapshot.SeatEgressWaiting;
            _seatEgressReachedSafeAnchor = snapshot.SeatEgressReachedSafeAnchor;
            _seatEgressCandidate = snapshot.SeatEgressCandidate;
            _seatEgressTargetWorld = snapshot.SeatEgressTargetWorld;
            _hasCompletedSeatEgress = snapshot.HasCompletedSeatEgress;
            _lastCompletedSeatEgressKind = snapshot.LastCompletedSeatEgressKind;
            _lastCompletedSeatEgressCell = snapshot.LastCompletedSeatEgressCell;
            _lastCompletedSeatEgressWorld = snapshot.LastCompletedSeatEgressWorld;
            _lastCompletedSeatEgressClearanceValid = snapshot.LastCompletedSeatEgressClearanceValid;
            _r5ePendingMovementHandoffId = snapshot.PendingMovementHandoffId;
            _r5eActiveMovementHandoffId = snapshot.ActiveMovementHandoffId;
            _r5eExitTransactionId = snapshot.ExitTransactionId;
            _r5eExitTurnDirection = snapshot.ExitTurnDirection;
            _r5eExitTurnPending = snapshot.ExitTurnPending;
            _r5eTurnCompleteTick = snapshot.TurnCompleteTick;
            _r5eSeatedSessionId = snapshot.SeatedSessionId;
            _r5eLastClosedSeatedSessionId = snapshot.LastClosedSeatedSessionId;
            _r5eVisualBaselineWorld = snapshot.VisualBaselineWorld;
            _r5ePreviousLogicalWorld = snapshot.PreviousLogicalWorld;
            _r5ePreviousVisualWorld = snapshot.PreviousVisualWorld;
            _r5ePreviousWorld = snapshot.PreviousWorld;
            _r5ePreviousRenderedWorld = snapshot.PreviousRenderedWorld;
            _r5eCollisionSweepOrigin = snapshot.CollisionSweepOrigin;
            _r5eAtomicPlacementThisStep = snapshot.AtomicPlacementThisStep;
            _r5eAtomicPlacementTick = snapshot.AtomicPlacementTick;
            _r5eQaOutwardRouteRequested = snapshot.QaOutwardRouteRequested;
            _r5eQaPreparedOutwardRoute = snapshot.QaPreparedOutwardRoute;
            _pendingDestination = snapshot.PendingDestination;
            _r5eActiveDockingPlan = snapshot.ActiveDockingPlan;
            _r5eEntryTransactionId = snapshot.EntryTransactionId;
            _seatFacingAlignedBeforeSitDown = snapshot.SeatFacingAlignedBeforeSitDown;
            RestoreR5eRendererSnapshot(_renderer, snapshot.MainRenderer);
            RestoreR5eRendererSnapshot(_seatedUpperBodyRenderer, snapshot.UpperBodyRenderer);
            _animator.RestoreAtomicPresentationSnapshot(snapshot.Animator);
        }

        private static R5eSpriteRendererSnapshot CaptureR5eRendererSnapshot(SpriteRenderer renderer)
        {
            return renderer == null
                ? default
                : new R5eSpriteRendererSnapshot(
                    renderer.sprite,
                    renderer.enabled,
                    renderer.flipX,
                    renderer.flipY,
                    renderer.color,
                    renderer.sharedMaterial,
                    renderer.sortingLayerID,
                    renderer.sortingOrder,
                    renderer.maskInteraction,
                    renderer.spriteSortPoint,
                    renderer.transform.localPosition,
                    renderer.transform.localRotation,
                    renderer.transform.localScale);
        }

        private static void RestoreR5eRendererSnapshot(
            SpriteRenderer renderer,
            in R5eSpriteRendererSnapshot snapshot)
        {
            if (!snapshot.Exists || renderer == null) return;
            renderer.sprite = snapshot.Sprite;
            renderer.enabled = snapshot.Enabled;
            renderer.flipX = snapshot.FlipX;
            renderer.flipY = snapshot.FlipY;
            renderer.color = snapshot.Color;
            renderer.sharedMaterial = snapshot.Material;
            renderer.sortingLayerID = snapshot.SortingLayerId;
            renderer.sortingOrder = snapshot.SortingOrder;
            renderer.maskInteraction = snapshot.MaskInteraction;
            renderer.spriteSortPoint = snapshot.SpriteSortPoint;
            renderer.transform.localPosition = snapshot.LocalPosition;
            renderer.transform.localRotation = snapshot.LocalRotation;
            renderer.transform.localScale = snapshot.LocalScale;
        }

        private readonly struct R5eSpriteRendererSnapshot
        {
            public R5eSpriteRendererSnapshot(
                Sprite sprite,
                bool enabled,
                bool flipX,
                bool flipY,
                Color color,
                Material material,
                int sortingLayerId,
                int sortingOrder,
                SpriteMaskInteraction maskInteraction,
                SpriteSortPoint spriteSortPoint,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Exists = true;
                Sprite = sprite;
                Enabled = enabled;
                FlipX = flipX;
                FlipY = flipY;
                Color = color;
                Material = material;
                SortingLayerId = sortingLayerId;
                SortingOrder = sortingOrder;
                MaskInteraction = maskInteraction;
                SpriteSortPoint = spriteSortPoint;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            public bool Exists { get; }
            public Sprite Sprite { get; }
            public bool Enabled { get; }
            public bool FlipX { get; }
            public bool FlipY { get; }
            public Color Color { get; }
            public Material Material { get; }
            public int SortingLayerId { get; }
            public int SortingOrder { get; }
            public SpriteMaskInteraction MaskInteraction { get; }
            public SpriteSortPoint SpriteSortPoint { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }

        private struct R5eEntryAtomicPublisher : IR5eAtomicPublishSteps
        {
            private readonly OfficeRuntimeAgent _owner;
            private readonly OfficeSeatingState.PreparedRuntimeMutation _claim;
            private readonly OfficeRuntimeOccupancy.PreparedAtomicActorPlacement _placement;
            private readonly R5eAtomicAgentSnapshot _snapshot;
            private readonly OfficeSeatDockingPlan _plan;
            private readonly OfficeCharacterSeatPoseProfile _workProfile;
            private readonly ulong _sessionId;
            private readonly ulong _transactionId;

            public R5eEntryAtomicPublisher(
                OfficeRuntimeAgent owner,
                in OfficeSeatingState.PreparedRuntimeMutation claim,
                in OfficeRuntimeOccupancy.PreparedAtomicActorPlacement placement,
                in R5eAtomicAgentSnapshot snapshot,
                in OfficeSeatDockingPlan plan,
                OfficeCharacterSeatPoseProfile workProfile,
                ulong sessionId,
                ulong transactionId)
            {
                _owner = owner;
                _claim = claim;
                _placement = placement;
                _snapshot = snapshot;
                _plan = plan;
                _workProfile = workProfile;
                _sessionId = sessionId;
                _transactionId = transactionId;
            }

            public void ThrowIfFault(R5eFaultInjectionPoint point) => _owner.ThrowIfR5eFault(point);
            public void CommitClaim() => _owner._seatClaim.CommitPreparedOccupy(_claim);
            public void CommitOccupancy() =>
                _owner._world.Occupancy.CommitPreparedAtomicActorPlacement(_placement);
            public void CommitRoot() => _owner.transform.position = new Vector3(
                _plan.SeatRootWorld.x,
                _plan.SeatRootWorld.y,
                _owner.transform.position.z);
            public void CommitRenderer()
            {
                _owner.ResetVisualPose();
                _owner._animator.EnterCompletedSeatedWorkAfterAtomicPlacement(_owner._seatDirection);
                _owner.ApplySeatAnchorPlacement(
                    _workProfile,
                    new Vector3(_plan.SeatPelvisWorld.x, _plan.SeatPelvisWorld.y, 0f));
            }
            public void CommitRebase() =>
                _owner.RebaseAfterAtomicPlacement(_plan.SeatRootWorld, _owner._seatDirection);
            public void CommitState()
            {
                _owner.Phase = OfficeRuntimeAgentPhase.Working;
                _owner.CurrentActivity = _owner._destination.HasValue
                    ? _owner._destination.Value.Activity
                    : OfficeActivity.Work;
                _owner._arrived = true;
                _owner._seatFacingAlignedBeforeSitDown = true;
                _owner._r5eSeatedSessionId = _sessionId;
                _owner._r5eLastClosedSeatedSessionId = 0;
                _owner._r5eEntryTransactionId = _transactionId;
                _owner._r5eActiveDockingPlan = _plan;
            }
            public void Rollback(bool claimCommitted, bool occupancyCommitted)
            {
                if (occupancyCommitted)
                    _owner._world.Occupancy.RollbackPreparedAtomicActorPlacement(_placement);
                else
                    _owner._world.Occupancy.CancelPreparedAtomicActorPlacement(_placement);
                if (claimCommitted) _owner._seatClaim.RollbackPreparedOccupy(_claim);
                _owner.RestoreR5eAtomicAgentSnapshot(_snapshot);
            }
        }

        private struct R5eExitAtomicPublisher : IR5eAtomicPublishSteps
        {
            private readonly OfficeRuntimeAgent _owner;
            private readonly OfficeSeatRuntimeClaim _releasedClaim;
            private readonly OfficeSeatingState.PreparedRuntimeMutation _claim;
            private readonly OfficeRuntimeOccupancy.PreparedAtomicActorPlacement _placement;
            private readonly OfficeRuntimeOccupancy.PreparedAtomicReservationScope _reservation;
            private readonly R5eAtomicAgentSnapshot _snapshot;
            private readonly OfficeSeatEgressAnchor _exitAnchor;
            private readonly Vector2 _exitWorld;
            private readonly int _exitDirection;
            private readonly ulong _handoffId;
            private readonly ulong _transactionId;
            private readonly OfficeRuntimeDestination? _preparedOutward;

            public R5eExitAtomicPublisher(
                OfficeRuntimeAgent owner,
                OfficeSeatRuntimeClaim releasedClaim,
                in OfficeSeatingState.PreparedRuntimeMutation claim,
                in OfficeRuntimeOccupancy.PreparedAtomicActorPlacement placement,
                in OfficeRuntimeOccupancy.PreparedAtomicReservationScope reservation,
                in R5eAtomicAgentSnapshot snapshot,
                in OfficeSeatEgressAnchor exitAnchor,
                Vector2 exitWorld,
                int exitDirection,
                ulong handoffId,
                ulong transactionId,
                OfficeRuntimeDestination? preparedOutward)
            {
                _owner = owner;
                _releasedClaim = releasedClaim;
                _claim = claim;
                _placement = placement;
                _reservation = reservation;
                _snapshot = snapshot;
                _exitAnchor = exitAnchor;
                _exitWorld = exitWorld;
                _exitDirection = exitDirection;
                _handoffId = handoffId;
                _transactionId = transactionId;
                _preparedOutward = preparedOutward;
            }

            public void ThrowIfFault(R5eFaultInjectionPoint point) => _owner.ThrowIfR5eFault(point);
            public void CommitClaim() => _releasedClaim.CommitPreparedRelease(_claim);
            public void CommitOccupancy() =>
                _owner._world.Occupancy.CommitPreparedAtomicActorPlacement(_placement);
            public void CommitRoot() => _owner.transform.position = new Vector3(
                _exitWorld.x,
                _exitWorld.y,
                _owner.transform.position.z);
            public void CommitRenderer()
            {
                _owner.ResetVisualPose();
                _owner._animator.LeaveCompletedSeatedWorkAfterAtomicPlacement(_exitDirection);
            }
            public void CommitRebase() =>
                _owner.RebaseAfterAtomicPlacement(_exitWorld, _exitDirection);
            public void CommitState()
            {
                _owner._seat = null;
                _owner._seatClaim = null;
                _owner._releaseSeatRequested = false;
                _owner._alignedClip = null;
                _owner._alignedFrame = -1;
                _owner._seatedUpperBodyCutoffPx = float.NaN;
                _owner.ClearSeatedUpperBodyProtection();
                _owner._seatPresentationPrepared = false;
                _owner._seatAlignmentComplete = false;
                _owner._finishingWorkPresentationObserved = false;
                _owner._seatEgressReservationActive = false;
                _owner._seatEgressWaiting = false;
                _owner._seatEgressReachedSafeAnchor = true;
                _owner._hasCompletedSeatEgress = true;
                _owner._lastCompletedSeatEgressKind = _exitAnchor.Kind;
                _owner._lastCompletedSeatEgressCell = _exitAnchor.Cell;
                _owner._lastCompletedSeatEgressWorld = _exitWorld;
                _owner._lastCompletedSeatEgressClearanceValid = true;
                _owner._r5ePendingMovementHandoffId = _handoffId;
                _owner._r5eActiveMovementHandoffId = 0;
                _owner._r5eExitTransactionId = _transactionId;
                _owner._r5eExitTurnDirection = _exitDirection;
                _owner._r5eExitTurnPending = true;
                _owner._r5eQaPreparedOutwardRoute = _preparedOutward.HasValue;
                if (_preparedOutward.HasValue) _owner._pendingDestination = _preparedOutward;
                _owner._r5eQaOutwardRouteRequested = false;
                _owner._r5eTurnCompleteTick = 0;
                _owner._r5eLastClosedSeatedSessionId = _owner._r5eSeatedSessionId;
                _owner._r5eSeatedSessionId = 0;
                _owner.Phase = OfficeRuntimeAgentPhase.LeavingSeat;
                _owner.CurrentActivity = OfficeActivity.Break;
            }
            public void Rollback(bool claimCommitted, bool occupancyCommitted)
            {
                if (occupancyCommitted)
                    _owner._world.Occupancy.RollbackPreparedAtomicActorPlacement(_placement);
                else
                    _owner._world.Occupancy.CancelPreparedAtomicActorPlacement(_placement);
                if (claimCommitted) _releasedClaim.RollbackPreparedRelease(_claim);
                _owner._world.Occupancy.RestoreAtomicReservationScope(_reservation);
                _owner.RestoreR5eAtomicAgentSnapshot(_snapshot);
            }
        }

        private readonly struct R5eAtomicAgentSnapshot
        {
            public R5eAtomicAgentSnapshot(
                Vector3 rootPosition, Vector3 visualLocalPosition,
                Quaternion visualLocalRotation, Vector3 visualLocalScale,
                OfficeRuntimeAgentPhase phase, OfficeActivity activity,
                Vector2 currentVelocity, Vector2 desiredVelocity,
                Vector2 lastActualDisplacement, float stuckSeconds,
                float visibleMotionDebtSeconds, float visibleFrameMovementBudgetWorld,
                float visibleFrameMovementWorld, int pathCount, int pathIndex,
                int pathRevision, int presentationPathIndex,
                OfficeGridCoordinate? yieldCell, bool arrived, bool releaseSeatRequested,
                OfficeSeatSlot seat, OfficeSeatRuntimeClaim seatClaim,
                OfficeSeatingAnimationClip? alignedClip, int alignedFrame,
                float seatedUpperBodyCutoffPx, bool seatPresentationPrepared,
                bool seatAlignmentComplete, bool finishingWorkPresentationObserved,
                bool seatEgressReservationActive, bool seatEgressWaiting,
                bool seatEgressReachedSafeAnchor, OfficeSeatEgressCandidate seatEgressCandidate,
                Vector2 seatEgressTargetWorld, bool hasCompletedSeatEgress,
                OfficeSeatEgressKind lastCompletedSeatEgressKind,
                OfficeGridCoordinate lastCompletedSeatEgressCell,
                Vector2 lastCompletedSeatEgressWorld,
                bool lastCompletedSeatEgressClearanceValid,
                ulong pendingMovementHandoffId, ulong activeMovementHandoffId,
                ulong exitTransactionId, int exitTurnDirection, bool exitTurnPending,
                ulong turnCompleteTick, ulong seatedSessionId,
                ulong lastClosedSeatedSessionId, Vector2 visualBaselineWorld,
                Vector2 previousLogicalWorld, Vector2 previousVisualWorld,
                Vector2 previousWorld, Vector2 previousRenderedWorld,
                Vector2 collisionSweepOrigin, bool atomicPlacementThisStep,
                ulong atomicPlacementTick, bool qaOutwardRouteRequested,
                bool qaPreparedOutwardRoute,
                OfficeRuntimeDestination? pendingDestination,
                OfficeSeatDockingPlan activeDockingPlan,
                ulong entryTransactionId,
                bool seatFacingAlignedBeforeSitDown,
                R5eSpriteRendererSnapshot mainRenderer,
                R5eSpriteRendererSnapshot upperBodyRenderer,
                DirectionalSpriteAnimator.AtomicPresentationSnapshot animator)
            {
                RootPosition=rootPosition; VisualLocalPosition=visualLocalPosition;
                VisualLocalRotation=visualLocalRotation; VisualLocalScale=visualLocalScale;
                Phase=phase; Activity=activity; CurrentVelocity=currentVelocity;
                DesiredVelocity=desiredVelocity; LastActualDisplacement=lastActualDisplacement;
                StuckSeconds=stuckSeconds; VisibleMotionDebtSeconds=visibleMotionDebtSeconds;
                VisibleFrameMovementBudgetWorld=visibleFrameMovementBudgetWorld;
                VisibleFrameMovementWorld=visibleFrameMovementWorld; PathCount=pathCount;
                PathIndex=pathIndex; PathRevision=pathRevision;
                PresentationPathIndex=presentationPathIndex; YieldCell=yieldCell;
                Arrived=arrived; ReleaseSeatRequested=releaseSeatRequested; Seat=seat;
                SeatClaim=seatClaim; AlignedClip=alignedClip; AlignedFrame=alignedFrame;
                SeatedUpperBodyCutoffPx=seatedUpperBodyCutoffPx;
                SeatPresentationPrepared=seatPresentationPrepared;
                SeatAlignmentComplete=seatAlignmentComplete;
                FinishingWorkPresentationObserved=finishingWorkPresentationObserved;
                SeatEgressReservationActive=seatEgressReservationActive;
                SeatEgressWaiting=seatEgressWaiting;
                SeatEgressReachedSafeAnchor=seatEgressReachedSafeAnchor;
                SeatEgressCandidate=seatEgressCandidate;
                SeatEgressTargetWorld=seatEgressTargetWorld;
                HasCompletedSeatEgress=hasCompletedSeatEgress;
                LastCompletedSeatEgressKind=lastCompletedSeatEgressKind;
                LastCompletedSeatEgressCell=lastCompletedSeatEgressCell;
                LastCompletedSeatEgressWorld=lastCompletedSeatEgressWorld;
                LastCompletedSeatEgressClearanceValid=lastCompletedSeatEgressClearanceValid;
                PendingMovementHandoffId=pendingMovementHandoffId;
                ActiveMovementHandoffId=activeMovementHandoffId;
                ExitTransactionId=exitTransactionId; ExitTurnDirection=exitTurnDirection;
                ExitTurnPending=exitTurnPending; TurnCompleteTick=turnCompleteTick;
                SeatedSessionId=seatedSessionId;
                LastClosedSeatedSessionId=lastClosedSeatedSessionId;
                VisualBaselineWorld=visualBaselineWorld;
                PreviousLogicalWorld=previousLogicalWorld;
                PreviousVisualWorld=previousVisualWorld; PreviousWorld=previousWorld;
                PreviousRenderedWorld=previousRenderedWorld;
                CollisionSweepOrigin=collisionSweepOrigin;
                AtomicPlacementThisStep=atomicPlacementThisStep;
                AtomicPlacementTick=atomicPlacementTick; Animator=animator;
                QaOutwardRouteRequested=qaOutwardRouteRequested;
                QaPreparedOutwardRoute=qaPreparedOutwardRoute;
                PendingDestination=pendingDestination; ActiveDockingPlan=activeDockingPlan;
                EntryTransactionId=entryTransactionId;
                SeatFacingAlignedBeforeSitDown=seatFacingAlignedBeforeSitDown;
                MainRenderer=mainRenderer; UpperBodyRenderer=upperBodyRenderer;
            }
            public Vector3 RootPosition { get; } public Vector3 VisualLocalPosition { get; }
            public Quaternion VisualLocalRotation { get; } public Vector3 VisualLocalScale { get; }
            public OfficeRuntimeAgentPhase Phase { get; } public OfficeActivity Activity { get; }
            public Vector2 CurrentVelocity { get; } public Vector2 DesiredVelocity { get; }
            public Vector2 LastActualDisplacement { get; } public float StuckSeconds { get; }
            public float VisibleMotionDebtSeconds { get; } public float VisibleFrameMovementBudgetWorld { get; }
            public float VisibleFrameMovementWorld { get; } public int PathCount { get; }
            public int PathIndex { get; } public int PathRevision { get; }
            public int PresentationPathIndex { get; } public OfficeGridCoordinate? YieldCell { get; }
            public bool Arrived { get; } public bool ReleaseSeatRequested { get; }
            public OfficeSeatSlot Seat { get; } public OfficeSeatRuntimeClaim SeatClaim { get; }
            public OfficeSeatingAnimationClip? AlignedClip { get; } public int AlignedFrame { get; }
            public float SeatedUpperBodyCutoffPx { get; } public bool SeatPresentationPrepared { get; }
            public bool SeatAlignmentComplete { get; } public bool FinishingWorkPresentationObserved { get; }
            public bool SeatEgressReservationActive { get; } public bool SeatEgressWaiting { get; }
            public bool SeatEgressReachedSafeAnchor { get; }
            public OfficeSeatEgressCandidate SeatEgressCandidate { get; }
            public Vector2 SeatEgressTargetWorld { get; } public bool HasCompletedSeatEgress { get; }
            public OfficeSeatEgressKind LastCompletedSeatEgressKind { get; }
            public OfficeGridCoordinate LastCompletedSeatEgressCell { get; }
            public Vector2 LastCompletedSeatEgressWorld { get; }
            public bool LastCompletedSeatEgressClearanceValid { get; }
            public ulong PendingMovementHandoffId { get; } public ulong ActiveMovementHandoffId { get; }
            public ulong ExitTransactionId { get; } public int ExitTurnDirection { get; }
            public bool ExitTurnPending { get; } public ulong TurnCompleteTick { get; }
            public ulong SeatedSessionId { get; } public ulong LastClosedSeatedSessionId { get; }
            public Vector2 VisualBaselineWorld { get; } public Vector2 PreviousLogicalWorld { get; }
            public Vector2 PreviousVisualWorld { get; } public Vector2 PreviousWorld { get; }
            public Vector2 PreviousRenderedWorld { get; } public Vector2 CollisionSweepOrigin { get; }
            public bool AtomicPlacementThisStep { get; } public ulong AtomicPlacementTick { get; }
            public bool QaOutwardRouteRequested { get; }
            public bool QaPreparedOutwardRoute { get; }
            public OfficeRuntimeDestination? PendingDestination { get; }
            public OfficeSeatDockingPlan ActiveDockingPlan { get; }
            public ulong EntryTransactionId { get; }
            public bool SeatFacingAlignedBeforeSitDown { get; }
            public R5eSpriteRendererSnapshot MainRenderer { get; }
            public R5eSpriteRendererSnapshot UpperBodyRenderer { get; }
            public DirectionalSpriteAnimator.AtomicPresentationSnapshot Animator { get; }
        }

        private void CompleteR5eExitTurnAndPublishRoute()
        {
            R5eAgentStepSnapshot before = CaptureR5eStepSnapshot();
            _r5eExitTurnPending = false;
            _r5eTurnCompleteTick = _r5eRuntimeTick;
            StopMotion();
            R5eAgentStepSnapshot after = CaptureR5eStepSnapshot();
            AppendR5eTransition(
                _r5eExitTransactionId,
                R5eSeatTransitionEventKind.TurnComplete,
                R5eSeatTransitionKind.Exit,
                before,
                after,
                _r5eActiveDockingPlan,
                _lastCompletedSeatEgressWorld,
                true,
                false,
                false);
            _r5eAwaitingFirstWalkTransactionId = _r5eExitTransactionId;
            _r5eAwaitingFirstWalk = true;
            _r5eLastFirstWalkDirection = -1;

            Phase = OfficeRuntimeAgentPhase.Idle;
            if (_pendingDestination.HasValue)
            {
                OfficeRuntimeDestination pending = _pendingDestination.Value;
                _pendingDestination = null;
                bool began = _r5eQaPreparedOutwardRoute
                    ? BeginPreparedQaOutwardDestination(pending)
                    : BeginDestination(pending);
                _r5eQaPreparedOutwardRoute = false;
                if (!began &&
                    _interactionPhase != OfficeRuntimeInteractionPhase.None)
                    AbortInteractionAttempt(OfficeRuntimeInteractionEndReason.PathUnavailable);
            }
            else
            {
                ResumeAutonomy();
            }
        }

        private bool BeginPreparedQaOutwardDestination(OfficeRuntimeDestination destination)
        {
            OfficeGridCoordinate current = _world.Presenter.NearestCell(transform.position);
            if (!_world.Grid.Contains(current) || !_world.Grid.Contains(destination.Cell) ||
                !_world.Grid.IsWalkable(destination.Cell) ||
                Math.Abs(destination.Cell.X - current.X) > 1 ||
                Math.Abs(destination.Cell.Y - current.Y) > 1 ||
                destination.Cell.Equals(current) ||
                !_world.Occupancy.IsCellPassable(
                    destination.Cell,
                    _agentId,
                    string.Empty,
                    true)) return false;

            ClearVisibleMotionDebt();
            _standingFacingDirection = -1;
            _destination = destination;
            _pendingDestination = null;
            _arrived = false;
            CurrentActivity = OfficeActivity.Walking;
            Phase = OfficeRuntimeAgentPhase.Navigating;
            _path.Clear();
            _path.Add(current);
            _path.Add(destination.Cell);
            _pathIndex = 1;
            _presentationPathIndex = _pathIndex;
            _pathRevision = _world.Occupancy.Revision;
            AdvanceR5eRouteGeneration();
            return true;
        }

        private bool TryResolveQaOutwardDestination(
            in OfficeSeatEgressAnchor exitAnchor,
            out OfficeGridCoordinate destination)
        {
            destination = default;
            if (_seat == null) return false;
            int dx = Math.Sign(exitAnchor.Cell.X - _seat.Cell.X);
            int dy = Math.Sign(exitAnchor.Cell.Y - _seat.Cell.Y);
            if (dx == 0 && dy == 0) return false;
            var candidate = new OfficeGridCoordinate(
                exitAnchor.Cell.X + dx,
                exitAnchor.Cell.Y + dy);
            if (!_world.Grid.Contains(candidate) || !_world.Grid.IsWalkable(candidate) ||
                !_world.Occupancy.IsCellPassable(
                    candidate,
                    _agentId,
                    string.Empty,
                    true)) return false;
            destination = candidate;
            return true;
        }

        private bool AppendR5eTransition(
            ulong transactionId,
            R5eSeatTransitionEventKind eventKind,
            R5eSeatTransitionKind transitionKind,
            in R5eAgentStepSnapshot before,
            in R5eAgentStepSnapshot after,
            in OfficeSeatDockingPlan plan,
            Vector2 chosenExit,
            bool commitSucceeded,
            bool rollbackSucceeded,
            bool locomotionSample)
        {
            if (!IsR5eTransitionObservationActive(transactionId)) return false;
            try
            {
                if (_r5ePublishActive || _r5eTraceCoordinator.PublishActive)
                    throw new InvalidOperationException(
                        "R5e trace cannot observe a half-published transaction.");
                ulong seatedSessionId = transitionKind == R5eSeatTransitionKind.Exit
                    ? (_r5eSeatedSessionId != 0
                        ? _r5eSeatedSessionId
                        : _r5eLastClosedSeatedSessionId)
                    : _r5eSeatedSessionId;
                R5eProductionObservation afterObservation = CaptureR5eProductionObservation(plan);
                var row = new R5eSeatTransitionTraceRow(
                    _r5ePendingStep.Context,
                    _agentId,
                    plan.Seat == null ? string.Empty : plan.Seat.SeatId,
                    transactionId,
                    seatedSessionId,
                    eventKind,
                    transitionKind,
                    before,
                    after,
                    plan,
                    chosenExit,
                    commitSucceeded,
                    rollbackSucceeded,
                    locomotionSample,
                    _r5eActiveFaultInjectionId,
                    _r5eTransitionBeforeObservation,
                    afterObservation);
                _r5eTraceState.AppendTransition(row);
                if (!_r5eTraceState.Failed) return true;
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "append-failed",
                    null);
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "append-exception",
                    exception);
            }
            return false;
        }

        private bool TryBeginR5eTransitionObservation(
            in OfficeSeatDockingPlan plan,
            int transitionRowCount,
            bool allocateSeatedSession,
            bool allocateMovementHandoff,
            out ulong transactionId,
            out ulong seatedSessionId,
            out ulong movementHandoffId)
        {
            transactionId = 0;
            seatedSessionId = 0;
            movementHandoffId = 0;
            if (!HasActiveR5eStepObservation || !_r5eTraceState.IsCaptureActive) return false;
            _r5eActiveFaultInjectionId = 0;
            _r5eSuppressedTransitionObservationId = 0;
            try
            {
                if (!_r5eTraceCoordinator.TryReserveTransitionRows(this, transitionRowCount) ||
                    !_r5eTraceState.IsCaptureActive)
                {
                    SuppressR5eTransitionObservationNoThrow(0, "reserve-failed", null);
                    return false;
                }
                _r5eTransitionAllocationStart = GC.GetAllocatedBytesForCurrentThread();
                _r5eTransitionBeforeObservation = CaptureR5eProductionObservation(plan);
                transactionId = _r5eTraceCoordinator.AllocateTransactionId();
                if (allocateSeatedSession)
                    seatedSessionId = _r5eTraceCoordinator.AllocateSeatedSessionId();
                if (allocateMovementHandoff)
                    movementHandoffId = _r5eTraceCoordinator.AllocateMovementHandoffId();
                return true;
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "begin-exception",
                    exception);
                transactionId = 0;
                seatedSessionId = 0;
                movementHandoffId = 0;
                return false;
            }
        }

        private bool CanOpenR5eSeatedSessionNoThrow(ulong sessionId, ulong transactionId)
        {
            if (!IsR5eTransitionObservationActive(transactionId)) return false;
            try
            {
                if (_r5eTraceState.CanOpenSeatedSession(sessionId, transactionId)) return true;
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "session-preflight-failed",
                    null);
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "session-preflight-exception",
                    exception);
            }
            return false;
        }

        private void OpenR5eSeatedSessionNoThrow(
            bool observeTransition,
            ulong sessionId,
            ulong transactionId)
        {
            if (!observeTransition || !IsR5eTransitionObservationActive(transactionId)) return;
            try
            {
                _r5eTraceState.OpenSeatedSession(sessionId, transactionId);
                if (!_r5eTraceState.Failed) return;
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "session-open-failed",
                    null);
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "session-open-exception",
                    exception);
            }
        }

        private void CloseR5eSeatedSessionNoThrow(bool observeTransition, ulong transactionId)
        {
            if (!observeTransition || !IsR5eTransitionObservationActive(transactionId)) return;
            try
            {
                _r5eTraceState.CloseSeatedSession();
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "session-close-exception",
                    exception);
            }
        }

        private bool TryEnterR5ePublishObservationNoThrow(
            bool observeTransition,
            ulong transactionId)
        {
            if (!observeTransition || !IsR5eTransitionObservationActive(transactionId)) return false;
            try
            {
                _r5eTraceCoordinator.EnterPublish();
                return true;
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "publish-enter-exception",
                    exception);
                return false;
            }
        }

        private void ExitR5ePublishObservationNoThrow(bool entered, ulong transactionId)
        {
            if (!entered) return;
            try
            {
                _r5eTraceCoordinator.ExitPublish();
            }
            catch (Exception exception)
            {
                SuppressR5eTransitionObservationNoThrow(
                    transactionId,
                    "publish-exit-exception",
                    exception);
            }
        }

        private bool IsR5eTransitionObservationActive(ulong transactionId) =>
            transactionId != 0 &&
            HasActiveR5eStepObservation &&
            _r5eTraceState.IsCaptureActive &&
            _r5eSuppressedTransitionObservationId != transactionId;

        private void SuppressR5eTransitionObservationNoThrow(
            ulong transactionId,
            string stage,
            Exception exception)
        {
            if (transactionId != 0)
                _r5eSuppressedTransitionObservationId = transactionId;
            try
            {
                _r5eTraceCoordinator?.AbortFatal(
                    "transition-observer-" + (stage ?? "failure") +
                    (exception == null ? string.Empty : ":" + exception.GetType().Name));
            }
            catch
            {
                // Observation failure evidence cannot replace the gameplay transition.
            }
        }

        private void ThrowIfR5eFault(R5eFaultInjectionPoint point)
        {
            if (!_r5eTraceCoordinator.ConsumeFault(_agentId, point)) return;
            _r5eActiveFaultInjectionId = (int)point;
            throw R5eInjectedFaultException;
        }

        private R5eProductionObservation CaptureR5eProductionObservation(
            in OfficeSeatDockingPlan plan,
            long allocationStart = -1L)
        {
            OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy =
                _world.Occupancy.CaptureCanonicalActorSnapshot(_agentId);
            _world.Occupancy.ObserveAtomicPlacementClearance(
                _agentId,
                occupancy.Position,
                occupancy.CurrentCell,
                AgentRadius,
                _seat?.SeatId ?? string.Empty,
                out bool floorValid,
                out bool staticOverlap,
                out bool dynamicOverlap);
            bool chairValid = _world.Workstations.TryCaptureLiveChairSnapshot(
                plan,
                out R5eFurnitureTransformSnapshot chair);
            bool exitReserved = _seatEgressReservationActive &&
                                _world.Occupancy.HasReservation(
                                    _agentId,
                                    _seatEgressCandidate.TargetCell);
            int visibleBodyCount = 0;
            if (_renderer != null && _renderer.enabled && _renderer.sprite != null &&
                _renderer.gameObject.activeInHierarchy) visibleBodyCount++;
            if (_seatedUpperBodyRenderer != null && _seatedUpperBodyRenderer.enabled &&
                _seatedUpperBodyRenderer.sprite != null &&
                _seatedUpperBodyRenderer.gameObject.activeInHierarchy) visibleBodyCount++;
            long allocationBytes = Math.Max(
                0L,
                GC.GetAllocatedBytesForCurrentThread() -
                (allocationStart >= 0L ? allocationStart : _r5eTransitionAllocationStart));
            return new R5eProductionObservation(
                occupancy,
                chair,
                chairValid,
                floorValid,
                staticOverlap,
                dynamicOverlap,
                exitReserved,
                _seatClaim != null && !_seatClaim.IsReleased,
                _seatClaim != null && _seatClaim.IsOccupied,
                _r5eForbiddenColliderCount,
                _r5eForbiddenCollider2DCount,
                _r5eForbiddenRigidbodyCount,
                _r5eForbiddenRigidbody2DCount,
                _r5eForbiddenNavMeshAgentCount,
                visibleBodyCount,
                allocationBytes,
                Time.unscaledDeltaTime * 1000f,
                chairValid && occupancy.IsPresent);
        }

        private void TickArrivedWork(float deltaTime)
        {
            if (!_arrived) return;
            if (TickStandingAlignment(deltaTime)) return;
            if (!HasAssignedTask || _assignedActivity == OfficeActivity.Work) return;
            AdvanceAssignedWork();
        }

        private bool TickStandingAlignment(float deltaTime)
        {
            if (_standingFacingDirection < 0) return false;
            if (_animator.IsReadyForInteractionFacing(_standingFacingDirection))
            {
                _standingFacingDirection = -1;
                if (_interactionPhase == OfficeRuntimeInteractionPhase.Aligning)
                    _interactionPhase = OfficeRuntimeInteractionPhase.Performing;
                return false;
            }

            _animator.AccumulateStandingFacingRequest(_standingFacingDirection, deltaTime);
            return true;
        }

        private void AdvanceAssignedWork()
        {
            long currentMinute = _bootstrap.State.Time.ElapsedMinutes;
            long elapsedMinutes = Math.Max(0L, currentMinute - _assignedLastObservedMinute);
            _assignedLastObservedMinute = currentMinute;
            if (elapsedMinutes <= 0L) return;
            _assignedWorkRemaining = Mathf.Max(0f, _assignedWorkRemaining - elapsedMinutes);
            if (_assignedWorkRemaining > 0f) return;
            string completed = _assignedTaskId;
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _assignedLastObservedMinute = 0L;
            AssignedTaskCompleted?.Invoke(this, completed);
            ResumeAutonomy();
        }

        private void RequestStopAndStand(string reason = "unspecified")
        {
            if (Phase == OfficeRuntimeAgentPhase.Working ||
                Phase == OfficeRuntimeAgentPhase.SittingDown ||
                IsEnteringSeat ||
                Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp ||
                Phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                _lastSeatReleaseRequestReason = string.IsNullOrWhiteSpace(reason)
                    ? "unspecified"
                    : reason.Trim();
                _lastSeatReleaseRequestTick = _r5eRuntimeTick;
            }
            _standingFacingDirection = -1;
            _navigationSegmentDirection = -1;
            _destination = null;
            _path.Clear();
            _arrived = false;
            if (Phase == OfficeRuntimeAgentPhase.Working ||
                Phase == OfficeRuntimeAgentPhase.SittingDown ||
                IsEnteringSeat)
            {
                _releaseSeatRequested = true;
                if (Phase == OfficeRuntimeAgentPhase.Working) BeginSafeStand();
                return;
            }
            if (Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp ||
                Phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                _releaseSeatRequested = true;
                return;
            }
            ReleaseSeatImmediately();
            Phase = OfficeRuntimeAgentPhase.Idle;
            StopMotion();
        }

        private void BeginSafeStand()
        {
            if (Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp) return;
            _r5eAwaitingFirstWalk = false;
            _r5eLastFirstWalkDirection = -1;
            ClearSeatEgressReservation();
            _seatEgressWaiting = false;
            _seatEgressReachedSafeAnchor = false;
            _lastSeatEgressBlocker = string.Empty;
            _r5eLastAtomicExitReservationBacked = false;
            _r5eLastAtomicExitTick = 0;
            _r5eLastAtomicExitDirection = -1;
            _releaseSeatRequested = true;
            _finishingWorkPresentationObserved = false;
            _animator.RequestOfficeWorkSafeStop();
            Phase = OfficeRuntimeAgentPhase.FinishingWork;
        }

        private bool TryPrepareSeatEgressReservation(
            in OfficeSeatDockingPlan plan,
            out OfficeRuntimeOccupancy.PreparedAtomicReservationScope reservationScope)
        {
            reservationScope = default;
            if (_seat == null || _world == null ||
                !_world.Occupancy.TryBeginAtomicReservationScope(
                    _agentId,
                    out reservationScope)) return false;
            if (_seatEgressReservationActive)
            {
                bool retained = _world != null &&
                                _world.Occupancy.HasReservation(
                                    _agentId,
                                    _seatEgressCandidate.TargetCell);
                if (retained) return true;
                ClearSeatEgressReservation();
            }

            _seatEgressReservationAttemptCount++;
            _world.Occupancy.ClearReservations(_agentId);
            if (!_world.Occupancy.IsActorPresent(_agentId))
            {
                _seatEgressBlockedAttemptCount++;
                _lastSeatEgressBlocker = "actor-not-present";
                _world.Occupancy.RestoreAtomicReservationScope(reservationScope);
                return false;
            }

            Vector2 start = Position;
            for (var index = 0; index < OfficeSeatEgressRules.CandidateCount; index++)
            {
                OfficeSeatEgressAnchor anchor = index switch
                {
                    0 => plan.FrontExit,
                    1 => plan.LeftExit,
                    _ => plan.RightExit
                };
                var candidate = new OfficeSeatEgressCandidate(anchor.Kind, anchor.Cell);
                OfficeGridCoordinate cell = anchor.Cell;
                if (!_world.Grid.Contains(cell) || !_world.Grid.IsWalkable(cell))
                {
                    _lastSeatEgressBlocker = "floor-not-walkable";
                    continue;
                }

                Vector3 target3 = _world.Presenter.CellCenterWorld(cell);
                var target = new Vector2(target3.x, target3.y);
                if (!_world.Occupancy.CanTraverseStatic(
                        target,
                        target,
                        AgentRadius,
                        string.Empty))
                {
                    _lastSeatEgressBlocker = "target-static-clearance";
                    continue;
                }
                if (!_world.Occupancy.CanTraverseStatic(
                        start,
                        target,
                        AgentRadius,
                        _seat.SeatId))
                {
                    _lastSeatEgressBlocker = "segment-static-clearance";
                    continue;
                }
                if (!_world.Occupancy.IsCellPassable(
                        cell,
                        _agentId,
                        string.Empty,
                        true) ||
                    !_world.Occupancy.HasPresentationClearance(
                        _agentId,
                        start,
                        target,
                        AgentRadius,
                        0f))
                {
                    _lastSeatEgressBlocker = "dynamic-clearance";
                    continue;
                }
                if (!_world.Occupancy.TryReserveSingleCell(
                        _agentId,
                        _seat.Cell,
                        cell))
                {
                    _lastSeatEgressBlocker = "reservation-unavailable";
                    continue;
                }
                if (!_world.Occupancy.CanMove(
                        _agentId,
                        start,
                        target,
                        AgentRadius,
                        _seat.SeatId))
                {
                    _world.Occupancy.ClearReservations(_agentId);
                    _lastSeatEgressBlocker = "segment-dynamic-clearance";
                    continue;
                }

                if (_r5eQaOutwardRouteRequested)
                {
                    int outwardX = Math.Sign(cell.X - _seat.Cell.X);
                    int outwardY = Math.Sign(cell.Y - _seat.Cell.Y);
                    var outward = new OfficeGridCoordinate(
                        cell.X + outwardX,
                        cell.Y + outwardY);
                    if ((outwardX == 0 && outwardY == 0) ||
                        !_world.Grid.Contains(outward) ||
                        !_world.Grid.IsWalkable(outward) ||
                        !_world.Occupancy.IsCellPassable(
                            outward,
                            _agentId,
                            string.Empty,
                            true))
                    {
                        _world.Occupancy.ClearReservations(_agentId);
                        _lastSeatEgressBlocker = "qa-outward-route-unavailable";
                        continue;
                    }
                }

                _seatEgressReservationActive = true;
                _seatEgressCandidate = candidate;
                _seatEgressTargetWorld = target;
                _seatEgressReachedSafeAnchor = false;
                _lastSeatEgressBlocker = string.Empty;
                return true;
            }

            _world.Occupancy.ClearReservations(_agentId);
            _seatEgressBlockedAttemptCount++;
            _world.Occupancy.RestoreAtomicReservationScope(reservationScope);
            return false;
        }

        private void TickSeatEgressDismount(float deltaTime)
        {
            if (_seat == null || _world == null || !_seatEgressReservationActive ||
                !_world.Occupancy.HasReservation(_agentId, _seatEgressCandidate.TargetCell))
            {
                _seatEgressUnsafePhaseTransitionCount++;
                StopMotion();
                return;
            }

            Vector2 delta = _seatEgressTargetWorld - Position;
            float toleranceWorld =
                SeatEgressCompletionTolerancePx / OfficeGridTilemapPresenter.PixelsPerUnit;
            if (delta.magnitude > toleranceWorld)
            {
                float budget = Mathf.Max(0f, _seatEgressFrameMovementBudgetWorld);
                if (budget <= 0.0000001f)
                {
                    StopMotion();
                    return;
                }
                Vector2 before = Position;
                MoveWithCollision(
                    delta.normalized * 1.15f,
                    deltaTime,
                    _seat.SeatId,
                    Mathf.Min(delta.magnitude, budget));
                float moved = Vector2.Distance(before, Position);
                _seatEgressFrameMovementBudgetWorld = Mathf.Max(0f, budget - moved);
                _seatEgressFrameMovementWorld += moved;
                _maximumSeatEgressStepPx = Mathf.Max(
                    _maximumSeatEgressStepPx,
                    _seatEgressFrameMovementWorld * OfficeGridTilemapPresenter.PixelsPerUnit);
                if (moved <= 0.0000001f && LastMovementBlocker.Length > 0)
                {
                    _seatEgressCollisionViolationCount++;
                    _lastSeatEgressBlocker = LastMovementBlocker;
                }
                return;
            }

            if (delta.sqrMagnitude > 0.00000001f)
            {
                float moved = delta.magnitude;
                if (moved > _seatEgressFrameMovementBudgetWorld + 0.0000001f ||
                    moved > _visibleFrameMovementBudgetWorld + 0.0000001f) return;
                transform.position = new Vector3(
                    _seatEgressTargetWorld.x,
                    _seatEgressTargetWorld.y,
                    transform.position.z);
                ConsumeVisibleFrameMovement(moved);
                _seatEgressFrameMovementBudgetWorld =
                    Mathf.Max(0f, _seatEgressFrameMovementBudgetWorld - moved);
                _seatEgressFrameMovementWorld += moved;
                _maximumSeatEgressStepPx = Mathf.Max(
                    _maximumSeatEgressStepPx,
                    _seatEgressFrameMovementWorld * OfficeGridTilemapPresenter.PixelsPerUnit);
            }
            StopMotion();
            if (!TryValidateSeatEgressCompletion(out string blocker))
            {
                _seatEgressCollisionViolationCount++;
                _lastSeatEgressBlocker = blocker;
                return;
            }
            _seatEgressReachedSafeAnchor = true;
            _lastSeatEgressBlocker = string.Empty;
        }

        private bool TryValidateSeatEgressCompletion(out string blocker)
        {
            blocker = string.Empty;
            if (_seat == null || _world == null || !_seatEgressReservationActive)
            {
                blocker = "missing-seat-egress-state";
                return false;
            }
            if (!_world.Registry.TryGet(_agentId, out OfficeRuntimeAgent registered) ||
                !ReferenceEquals(registered, this) ||
                !_world.Occupancy.IsActorPresent(_agentId))
            {
                blocker = "actor-not-registered-present";
                return false;
            }
            if (!_world.Occupancy.HasReservation(_agentId, _seatEgressCandidate.TargetCell))
            {
                blocker = "egress-reservation-lost";
                return false;
            }
            if (!_world.Grid.Contains(_seatEgressCandidate.TargetCell) ||
                !_world.Grid.IsWalkable(_seatEgressCandidate.TargetCell) ||
                !_world.Presenter.NearestCell(transform.position).Equals(
                    _seatEgressCandidate.TargetCell) ||
                _seatEgressCandidate.TargetCell.Equals(_seat.Cell))
            {
                blocker = "root-not-on-walkable-egress-cell";
                return false;
            }
            if (!_world.Occupancy.CanTraverseStatic(
                    _seatEgressTargetWorld,
                    _seatEgressTargetWorld,
                    AgentRadius,
                    string.Empty))
            {
                blocker = "root-inside-chair-or-static-footprint";
                return false;
            }
            if (!_world.Occupancy.CanTraverseStatic(
                    (Vector2)_world.Workstations.SeatOperatorWorld(_seat),
                    _seatEgressTargetWorld,
                    AgentRadius,
                    _seat.SeatId) ||
                !_world.Occupancy.CanMove(
                    _agentId,
                    _seatEgressTargetWorld,
                    _seatEgressTargetWorld,
                    AgentRadius,
                    string.Empty))
            {
                blocker = "egress-segment-or-body-clearance-invalid";
                return false;
            }
            return true;
        }

        private void ClearSeatEgressReservation()
        {
            if (_seatEgressReservationActive && _world != null)
                _world.Occupancy.ClearReservations(_agentId);
            _seatEgressReservationActive = false;
            _seatEgressWaiting = false;
            _seatEgressReachedSafeAnchor = false;
            _seatEgressCandidate = default;
            _seatEgressTargetWorld = Vector2.zero;
            _seatEgressFrameMovementBudgetWorld = 0f;
            _seatEgressFrameMovementWorld = 0f;
        }

        private void ResumeAutonomy()
        {
            if (HasAssignedTask) return;
            if (_autonomyIntentId.Length > 0 && !_playerControlled)
            {
                // Resolve against the live layout and acquire capacity only now. A cached
                // destination may refer to furniture removed while a contract was running.
                TryStartAutonomyRequest();
                return;
            }
            if (Phase != OfficeRuntimeAgentPhase.LeavingSeat && _seatClaim == null)
            {
                Phase = OfficeRuntimeAgentPhase.Idle;
                CurrentActivity = OfficeActivity.Break;
                _destination = null;
                _arrived = false;
            }
        }

        private void TickDirectPlayerMovement(float deltaTime)
        {
            Vector2 velocity = _playerInput.normalized * DefaultMoveSpeed;
            MoveWithCollision(velocity, deltaTime, string.Empty);
            Phase = velocity.sqrMagnitude > 0.0001f
                ? OfficeRuntimeAgentPhase.Navigating
                : OfficeRuntimeAgentPhase.Idle;
            CurrentActivity = velocity.sqrMagnitude > 0.0001f
                ? OfficeActivity.Walking
                : OfficeActivity.Break;
        }

        private void MoveWithCollision(
            Vector2 targetVelocity,
            float deltaTime,
            string permittedSeatId,
            float maximumDistance = float.PositiveInfinity,
            Vector2? presentationSemanticVelocity = null)
        {
            if (_visibleFrameMovementBudgetWorld <= 0.0000001f)
            {
                // This render already consumed its complete visible budget. The route and its
                // time debt remain intact for the next frame; this is not a collision/stuck tick.
                _lastActualDisplacement = Vector2.zero;
                _desiredVelocity = targetVelocity;
                return;
            }
            // Movement direction changes are continuous. Seat-facing remains a presentation lock
            // during egress, but it no longer zeros the actor velocity just to turn a sprite row.
            float changePerSecond = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                OfficeNavigationMotionIntegrator.DefaultAcceleration,
                _playerControlled);
            OfficeMotionIntegrationResult motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                changePerSecond,
                deltaTime);
            _currentVelocity = new Vector2(motion.Velocity.X, motion.Velocity.Z);
            float visibleMaximumDistance = float.IsPositiveInfinity(maximumDistance)
                ? _visibleFrameMovementBudgetWorld
                : Mathf.Min(
                    Mathf.Max(0f, maximumDistance),
                    _visibleFrameMovementBudgetWorld);
            OfficeNavPoint clampedDisplacement = OfficeNavigationMotionIntegrator.ClampDisplacement(
                motion.Displacement,
                visibleMaximumDistance);
            Vector2 intended = new Vector2(clampedDisplacement.X, clampedDisplacement.Z);
            Vector2 before = Position;
            Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                _world.Occupancy,
                _agentId,
                before,
                intended,
                targetVelocity,
                _lastActualDisplacement,
                AgentRadius,
                permittedSeatId,
                out bool collisionProjected);
            if (actual.sqrMagnitude > OfficeRuntimeCollisionMotion.MinimumDisplacementSquared)
            {
                transform.position = new Vector3(
                    before.x + actual.x,
                    before.y + actual.y,
                    transform.position.z);
                ConsumeVisibleFrameMovement(actual.magnitude);
                _stuckSeconds = Mathf.Max(0f, _stuckSeconds - deltaTime * 2f);
            }
            else
            {
                _currentVelocity = Vector2.zero;
                if (targetVelocity.sqrMagnitude > 0.01f) _stuckSeconds += deltaTime;
                LastMovementBlocker = _world.Occupancy.DescribeMoveBlocker(
                    _agentId,
                    before,
                    before + intended,
                    AgentRadius,
                    permittedSeatId);
            }
            if (actual.sqrMagnitude > OfficeRuntimeCollisionMotion.MinimumDisplacementSquared)
                LastMovementBlocker = string.Empty;
            _animator.AccumulateTileMotion(
                presentationSemanticVelocity ?? targetVelocity,
                actual,
                deltaTime,
                collisionProjected);
            _lastActualDisplacement = actual;
            _desiredVelocity = targetVelocity;
            _world.Workstations.ApplyDynamicCharacterOrder(_renderer, transform.position);
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);
        }

        private void ConsumeVisibleFrameMovement(float distance)
        {
            float safeDistance = Mathf.Max(0f, distance);
            _visibleFrameMovementBudgetWorld = Mathf.Max(
                0f,
                _visibleFrameMovementBudgetWorld - safeDistance);
            _visibleFrameMovementWorld += safeDistance;
        }

        private bool TryConsumeExactEndpoint(
            Vector2 target,
            float deltaTime,
            string permittedSeatId,
            Vector2 semanticVelocity)
        {
            Vector2 before = Position;
            Vector2 intended = target - before;
            float distance = intended.magnitude;
            if (distance > _visibleFrameMovementBudgetWorld + 0.0000001f) return false;
            if (distance <= 0.0000001f) return true;
            Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                _world.Occupancy,
                _agentId,
                before,
                intended,
                semanticVelocity,
                _lastActualDisplacement,
                AgentRadius,
                permittedSeatId,
                out bool collisionProjected);
            if (actual.sqrMagnitude > OfficeRuntimeCollisionMotion.MinimumDisplacementSquared)
            {
                transform.position = new Vector3(
                    before.x + actual.x,
                    before.y + actual.y,
                    transform.position.z);
                ConsumeVisibleFrameMovement(actual.magnitude);
                _animator.AccumulateTileMotion(
                    semanticVelocity,
                    actual,
                    deltaTime,
                    collisionProjected);
                _lastActualDisplacement = actual;
                _desiredVelocity = semanticVelocity;
                _world.Occupancy.UpdateActor(
                    _agentId,
                    Position,
                    _desiredVelocity,
                    _stuckSeconds,
                    _seat?.SeatId ?? string.Empty);
            }
            bool reached = Vector2.Distance(actual, intended) <= 0.00001f;
            if (reached) LastMovementBlocker = string.Empty;
            else LastMovementBlocker = _world.Occupancy.DescribeMoveBlocker(
                _agentId,
                before,
                target,
                AgentRadius,
                permittedSeatId);
            return reached;
        }

        private void TrackNavigationSegmentDirection(Vector2 segmentDirection)
        {
            if (_animator == null || segmentDirection.sqrMagnitude <= 0.000001f) return;
            int requested = _animator.ResolveConfiguredTileDirection(
                segmentDirection,
                _animator.CurrentDirection);
            if (_navigationSegmentDirection == requested) return;
            _navigationSegmentDirection = requested;
            _playerNaturalTurnFromDirection = -1;
            _playerNaturalTurnTargetDirection = -1;
            _playerNaturalTurnElapsedSeconds = 0f;
        }

        private void StopMotion(bool keepStuck = false)
        {
            _currentVelocity = Vector2.zero;
            _desiredVelocity = Vector2.zero;
            _lastActualDisplacement = Vector2.zero;
            if (!keepStuck) _stuckSeconds = 0f;
            _animator?.StopTileMovementButKeepFacing();
            if (_world != null)
                _world.Occupancy.UpdateActor(
                    _agentId,
                    Position,
                    Vector2.zero,
                    _stuckSeconds,
                    _seat?.SeatId ?? string.Empty);
        }

        private void HandleOfficeFrameApplied(
            OfficeSeatingAnimationClip clip,
            int frame,
            Sprite appliedSprite)
        {
            if (_seat == null || appliedSprite == null) return;
            OfficeCharacterSeatPoseProfile profile = ResolveSeatPresentationProfile(clip, frame);
            _seatedUpperBodyCutoffPx = profile.PelvisAnchorPx.y;
            RecordObservedSeatingFrame(clip, frame);
            if (_attendanceWorkHandoffActive &&
                clip == OfficeSeatingAnimationClip.Work &&
                (_observedWorkFrameMask & 0x3f) == 0x3f)
            {
                _attendanceWorkHandoffActive = false;
                Debug.Log(
                    "STARTER_OFFICE_ATTENDANCE_WORK_HANDOFF | member=" + _agentId +
                    " | tick=" + _r5eRuntimeTick +
                    " | workFrames=6");
            }
            if (clip == OfficeSeatingAnimationClip.Work &&
                _animator.IsOfficeWorkAnimationHookActive &&
                !string.IsNullOrWhiteSpace(appliedSprite.name))
            {
                _observedOfficeWorkHookSprites.Add(appliedSprite.name);
            }
            bool alignTypingContact =
                clip == OfficeSeatingAnimationClip.Work &&
                _animator.SeatingPresentationMode == OfficeSeatingPresentationMode.Animated &&
                _animator.IsOfficeWorkAnimationHookActive &&
                _animator.CurrentOfficeWorkMicroAction == OfficeWorkMicroAction.Typing;
            if (alignTypingContact)
            {
                ApplyTypingWorkstationContactPlacement(profile);
            }
            else if (_animator.SeatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork)
            {
                ApplySeatedContactPlacement(profile);
            }
            else
            {
                ApplyAnimatedSeatingPlacement(clip, frame, profile);
            }
            RecordVisibleSeatingPelvisContinuity(profile);
            RecordChairPresentationMotion();
            _alignedClip = clip;
            _alignedFrame = frame;
        }

        /// <summary>
        /// Redraws the pose-defined upper body above the complete chair foreground. The main actor
        /// renderer remains below the chair foreground, so only pelvis/legs can be occluded while
        /// the chair Sprite itself stays continuous rather than being horizontally cropped.
        /// </summary>
        public void ApplySeatedUpperBodyProtection(int sortingOrder)
        {
            if (!IsOccupyingSeat || !IsSeatForegroundOcclusionEngaged ||
                _renderer == null || !_renderer.enabled || _renderer.sprite == null ||
                float.IsNaN(_seatedUpperBodyCutoffPx))
            {
                ClearSeatedUpperBodyProtection();
                return;
            }

            Sprite source = _renderer.sprite;
            int cutoff = OfficeSeatedUpperBodyProtectionRules.ResolveCutoffSourceY(
                source,
                new Vector2(0f, _seatedUpperBodyCutoffPx));
            long key = ((long)(uint)source.GetInstanceID() << 32) | (uint)cutoff;
            if (!_seatedUpperBodySprites.TryGetValue(key, out Sprite upperBody))
            {
                if (_r5eSeatPresentationPreloaded)
                {
                    _r5eTraceCoordinator?.AbortFatal("seated-upper-body-preload-miss:" + _agentId);
                }
                upperBody = OfficeSeatedUpperBodyProtectionRules.CreateUpperBodySprite(source, cutoff);
                _seatedUpperBodySprites.Add(key, upperBody);
            }

            EnsureSeatedUpperBodyRenderer();
            _seatedUpperBodyRenderer.sprite = upperBody;
            _seatedUpperBodyRenderer.transform.localPosition =
                OfficeSeatedUpperBodyProtectionRules.LocalPosition(source, cutoff);
            _seatedUpperBodyRenderer.sharedMaterial = _renderer.sharedMaterial;
            _seatedUpperBodyRenderer.color = _renderer.color;
            _seatedUpperBodyRenderer.flipX = _renderer.flipX;
            _seatedUpperBodyRenderer.flipY = _renderer.flipY;
            _seatedUpperBodyRenderer.sortingLayerID = _renderer.sortingLayerID;
            _seatedUpperBodyRenderer.sortingOrder = sortingOrder;
            _seatedUpperBodyRenderer.maskInteraction = _renderer.maskInteraction;
            _seatedUpperBodyRenderer.spriteSortPoint = _renderer.spriteSortPoint;
            _seatedUpperBodyRenderer.enabled = !_presentationAway;
        }

        internal void PreloadR5eSeatPresentation()
        {
            if (_r5eSeatPresentationPreloaded) return;
            EnsureSeatedUpperBodyRenderer();
            OfficeCharacterSeatPoseProfile[] preloadProfiles =
                BuildR5eSeatPresentationPreloadPlan(_poseCatalog.Profiles, _agentId);
            foreach (OfficeCharacterSeatPoseProfile profile in preloadProfiles)
            {
                Sprite source = _animator.GetOfficeSeatingFrame(
                    OfficeSeatingAnimationClip.Work,
                    profile.DirectionIndex,
                    profile.FrameIndex);
                if (source == null) throw new InvalidOperationException(
                    "R5e work sprite preload missing: " + _agentId + "/" +
                    profile.DirectionIndex + "/" + profile.FrameIndex);
                int cutoff = OfficeSeatedUpperBodyProtectionRules.ResolveCutoffSourceY(
                    source,
                    new Vector2(0f, profile.PelvisAnchorPx.y));
                long key = ((long)(uint)source.GetInstanceID() << 32) | (uint)cutoff;
                if (_seatedUpperBodySprites.ContainsKey(key)) continue;
                _seatedUpperBodySprites.Add(
                    key,
                    OfficeSeatedUpperBodyProtectionRules.CreateUpperBodySprite(source, cutoff));
            }
            _seatedUpperBodyRenderer.enabled = false;
            _r5eSeatPresentationPreloaded = true;
        }

        internal static OfficeCharacterSeatPoseProfile[] BuildR5eSeatPresentationPreloadPlan(
            IReadOnlyList<OfficeCharacterSeatPoseProfile> profiles,
            string memberId)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Preload member ID is required.", nameof(memberId));
            int frameCount = OfficeSeatingAnimationFrames.WorkFrameCount;
            var result = new List<OfficeCharacterSeatPoseProfile>();
            var framesByDirection = new Dictionary<int, bool[]>();
            foreach (OfficeCharacterSeatPoseProfile profile in profiles)
            {
                if (profile == null || !profile.HumanApproved ||
                    profile.Clip != OfficeSeatingAnimationClip.Work ||
                    profile.DirectionIndex != RequiredR5eSeatPreloadDirection ||
                    !string.Equals(profile.MemberId, memberId, StringComparison.Ordinal)) continue;
                if (profile.DirectionIndex < 0 ||
                    profile.DirectionIndex >= OfficeSeatingAnimationFrames.DirectionCount)
                    throw new InvalidOperationException(
                        "Invalid preload pose direction: " + memberId + "/" +
                        profile.DirectionIndex);
                if (!framesByDirection.TryGetValue(profile.DirectionIndex, out bool[] frames))
                {
                    frames = new bool[frameCount];
                    framesByDirection.Add(profile.DirectionIndex, frames);
                }
                if (profile.FrameIndex < 0 || profile.FrameIndex >= frameCount ||
                    frames[profile.FrameIndex])
                    throw new InvalidOperationException(
                        "Invalid or duplicate preload pose: " + memberId + "/" +
                        profile.DirectionIndex + "/Work/" + profile.FrameIndex);
                frames[profile.FrameIndex] = true;
                result.Add(profile);
            }
            if (result.Count == 0)
                throw new InvalidOperationException(
                    "No approved Northwest Work preload poses exist: " + memberId);
            foreach (KeyValuePair<int, bool[]> direction in framesByDirection)
            for (var frame = 0; frame < direction.Value.Length; frame++)
                if (!direction.Value[frame])
                    throw new InvalidOperationException(
                        "Incomplete preload pose direction: " + memberId + "/" +
                        direction.Key + "/Work/" + frame);
            result.Sort((left, right) =>
            {
                int direction = left.DirectionIndex.CompareTo(right.DirectionIndex);
                return direction != 0 ? direction : left.FrameIndex.CompareTo(right.FrameIndex);
            });
            return result.ToArray();
        }

        internal void ResetR5eSeatPresentationPreloadAfterFailure()
        {
            foreach (Sprite sprite in _seatedUpperBodySprites.Values)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
            _seatedUpperBodySprites.Clear();
            _r5eSeatPresentationPreloaded = false;
            if (_seatedUpperBodyRenderer != null) _seatedUpperBodyRenderer.enabled = false;
        }

        public void ClearSeatedUpperBodyProtection()
        {
            if (_seatedUpperBodyRenderer != null) _seatedUpperBodyRenderer.enabled = false;
        }

        private void EnsureSeatedUpperBodyRenderer()
        {
            if (_seatedUpperBodyRenderer != null) return;
            var upperBody = new GameObject("SeatedUpperBodyProtection");
            upperBody.transform.SetParent(_renderer.transform, false);
            upperBody.transform.localPosition = Vector3.zero;
            upperBody.transform.localRotation = Quaternion.identity;
            upperBody.transform.localScale = Vector3.one;
            _seatedUpperBodyRenderer = upperBody.AddComponent<SpriteRenderer>();
        }

        private void DestroySeatedUpperBodyProtection()
        {
            foreach (Sprite sprite in _seatedUpperBodySprites.Values)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
            _seatedUpperBodySprites.Clear();
            _seatedUpperBodyRenderer = null;
        }

        private bool PrepareSeatAlignmentForWork()
        {
            if (_seatPresentationPrepared) return true;
            if (_seat == null || _renderer == null || _renderer.sprite == null) return false;

            ResetVisualPose();
            _authoredSeatPelvisWorld =
                _world.Workstations.ResolveInteractionAnchors(_seat).PelvisWorld;
            _workPresentationTargetPelvisWorld = _authoredSeatPelvisWorld;
            _seatPresentationPrepared = true;
            _seatAlignmentComplete = true;
            SeedChairPresentationMotionSample();
            return true;
        }

        private void SeedChairPresentationMotionSample()
        {
            if (_seat == null || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(
                _world.Workstations.ChairSeatAnchorWorld(_seat));
            _hasChairPresentationSample = true;
            _previousChairPresentationScreen = new Vector2(screen.x, screen.y);
        }

        private void ApplyTypingWorkstationContactPlacement(
            OfficeCharacterSeatPoseProfile profile)
        {
            if (_seat == null || profile == null || !_seat.HasWorkstationBinding) return;
            if (Mathf.Abs(profile.RotationDegrees) > 0.01f)
                throw new InvalidOperationException($"Seated pose rotation must be zero for {_agentId}.");
            if (Mathf.Abs(profile.UniformScale - 1f) > 0.0001f)
                throw new InvalidOperationException($"Seated pose scale must be 1 for {_agentId}.");

            ApplySeatAnchorPlacement(
                profile,
                _workPresentationTargetPelvisWorld);
        }

        private void ApplyAnimatedSeatingPlacement(
            OfficeSeatingAnimationClip clip,
            int frame,
            OfficeCharacterSeatPoseProfile profile)
        {
            Vector3 cushion = _seatPresentationPrepared
                ? _workPresentationTargetPelvisWorld
                : _world.Workstations.ChairSeatAnchorWorld(_seat);
            Vector3 desiredPelvis;
            switch (clip)
            {
                case OfficeSeatingAnimationClip.SitDown:
                    if (!_sitTransitionInitialized)
                    {
                        ResetVisualPose();
                        _sitTransitionStartPelvisWorld =
                            OfficeSeatedOccupantContract.OccupantSeatContactWorld(
                                _renderer,
                                profile.PelvisAnchorPx);
                        _sitTransitionInitialized = true;
                        _standTransitionInitialized = false;
                        _sitPlacementProgress01 = 0f;
                    }
                    _sitPlacementProgress01 = ResolveStepLimitedTransitionProgress(
                        _sitTransitionStartPelvisWorld,
                        cushion,
                        _sitPlacementProgress01,
                        SmoothStep01(_animator.CurrentOfficeSeatingProgress01));
                    desiredPelvis = Vector3.Lerp(
                        _sitTransitionStartPelvisWorld,
                        cushion,
                        _sitPlacementProgress01);
                    break;
                case OfficeSeatingAnimationClip.Work:
                    _sitPlacementProgress01 = 1f;
                    desiredPelvis = cushion;
                    break;
                case OfficeSeatingAnimationClip.StandUp:
                    if (!_standTransitionInitialized)
                    {
                        OfficeCharacterSeatPoseProfile finalProfile =
                            ResolveSeatPresentationProfile(
                                OfficeSeatingAnimationClip.StandUp,
                                OfficeSeatingAnimationFrames.StandUpFrameCount - 1);
                        ResetVisualPose();
                        _standTransitionTargetPelvisWorld =
                            OfficeSeatedOccupantContract.OccupantSeatContactWorld(
                                _renderer,
                                finalProfile.PelvisAnchorPx);
                        _standTransitionInitialized = true;
                        _standPlacementProgress01 = 0f;
                    }
                    _standPlacementProgress01 = ResolveStepLimitedTransitionProgress(
                        cushion,
                        _standTransitionTargetPelvisWorld,
                        _standPlacementProgress01,
                        SmoothStep01(_animator.CurrentOfficeSeatingProgress01));
                    desiredPelvis = Vector3.Lerp(
                        cushion,
                        _standTransitionTargetPelvisWorld,
                        _standPlacementProgress01);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(clip));
            }
            ApplySeatAnchorPlacement(profile, desiredPelvis);
            RecordTransitionMotion(clip, desiredPelvis, cushion);
            if (Camera.main != null)
            {
                float error = OfficeGridAlignmentMetrics.ScreenDistance(
                    Camera.main,
                    OfficeSeatedOccupantContract.OccupantSeatContactWorld(
                        _renderer,
                        profile.PelvisAnchorPx),
                    desiredPelvis);
                _maxAnimatedAnchorErrorPx = Mathf.Max(_maxAnimatedAnchorErrorPx, error);
            }
        }

        private void RecordObservedSeatingFrame(OfficeSeatingAnimationClip clip, int frame)
        {
            int bit = 1 << frame;
            switch (clip)
            {
                case OfficeSeatingAnimationClip.SitDown:
                    _observedSitDownFrameMask |= bit;
                    break;
                case OfficeSeatingAnimationClip.Work:
                    _observedWorkFrameMask |= bit;
                    break;
                case OfficeSeatingAnimationClip.StandUp:
                    _observedStandUpFrameMask |= bit;
                    break;
            }
        }

        private void ResetSeatingObservationMetrics()
        {
            _observedSitDownFrameMask = 0;
            _observedWorkFrameMask = 0;
            _observedStandUpFrameMask = 0;
            _observedOfficeWorkHookSprites.Clear();
            _maxAnimatedAnchorErrorPx = 0f;
            _maxTypingSeatContactErrorPx = 0f;
            _maxTypingHandWorkErrorPx = 0f;
            _typingContactSampleCount = 0;
            _finishingWorkPresentationObserved = false;
            _hasChairPresentationSample = false;
            _previousChairPresentationScreen = Vector2.zero;
            _maxChairPresentationStepPx = 0f;
            _seatingFacingViolationCount = 0;
            _seatFacingAlignedBeforeSitDown = false;
            _lastSeatingDepthSample = default;
            _seatingDepthViolationCount = 0;
            _hasVisibleSeatingPelvisSample = false;
            _previousVisibleSeatingPelvisScreen = Vector2.zero;
            ResetTransitionMotionMetrics();
            _maxTransitionPelvisStepPx = 0f;
            _transitionMonotonicViolationCount = 0;
            ResetSeatEgressMetrics();
        }

        private void ResetSeatEgressMetrics()
        {
            ClearSeatEgressReservation();
            _maximumSeatEgressStepPx = 0f;
            _seatEgressReservationAttemptCount = 0;
            _seatEgressBlockedAttemptCount = 0;
            _seatEgressCollisionViolationCount = 0;
            _seatEgressUnsafePhaseTransitionCount = 0;
            _hasCompletedSeatEgress = false;
            _lastCompletedSeatEgressKind = OfficeSeatEgressKind.None;
            _lastCompletedSeatEgressCell = default;
            _lastCompletedSeatEgressWorld = Vector2.zero;
            _lastCompletedSeatEgressClearanceValid = false;
            _lastSeatEgressBlocker = string.Empty;
        }

        /// <summary>
        /// Put the sheet's seat contact point on the chair cushion, per
        /// <see cref="OfficeSeatedOccupantContract"/>. Translation only - the scale stays canonical
        /// and the rotation stays identity, so a wrong sheet can never be bent into place.
        /// </summary>
        private void ApplySeatedContactPlacement(OfficeCharacterSeatPoseProfile profile)
        {
            if (_seat == null || profile == null) return;
            ApplySeatAnchorPlacement(
                profile,
                _world.Workstations.ChairSeatAnchorWorld(_seat));
        }

        private void ApplySeatAnchorPlacement(
            OfficeCharacterSeatPoseProfile profile,
            Vector3 targetWorld)
        {
            if (_seat == null || profile == null) return;
            if (Mathf.Abs(profile.RotationDegrees) > 0.01f)
                throw new InvalidOperationException($"Seated pose rotation must be zero for {_agentId}.");
            if (Mathf.Abs(profile.UniformScale - 1f) > 0.0001f)
                throw new InvalidOperationException($"Seated pose scale must be 1 for {_agentId}.");
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
            Vector3 contact = OfficeSeatedOccupantContract.OccupantSeatContactWorld(
                _renderer,
                profile.PelvisAnchorPx);
            _visualRoot.localPosition = transform.InverseTransformVector(targetWorld - contact);
            _world.Workstations.ApplyPresentationStack(_seat, _renderer, transform.position);
        }

        private static int CountBits(int value)
        {
            var result = 0;
            while (value != 0)
            {
                result += value & 1;
                value >>= 1;
            }
            return result;
        }

        private static float SmoothStep01(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return progress * progress * (3f - (2f * progress));
        }

        private static float ResolveStepLimitedTransitionProgress(
            Vector3 startWorld,
            Vector3 endWorld,
            float currentProgress,
            float targetProgress)
        {
            targetProgress = Mathf.Clamp01(targetProgress);
            if (targetProgress <= currentProgress) return currentProgress;
            if (Camera.main == null) return targetProgress;
            float travelPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                startWorld,
                endWorld);
            if (travelPx <= 0.001f) return targetProgress;
            const float maximumPelvisStepPx = 1.9f;
            return Mathf.MoveTowards(
                currentProgress,
                targetProgress,
                maximumPelvisStepPx / travelPx);
        }

        private void RecordTransitionMotion(
            OfficeSeatingAnimationClip clip,
            Vector3 pelvisWorld,
            Vector3 cushionWorld)
        {
            if (clip == OfficeSeatingAnimationClip.Work || Camera.main == null) return;
            Vector2 pelvisScreen = Camera.main.WorldToScreenPoint(pelvisWorld);
            Vector2 cushionScreen = Camera.main.WorldToScreenPoint(cushionWorld);
            Vector2 pelvisOffsetScreen = pelvisScreen - cushionScreen;
            float cushionDistancePx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                pelvisWorld,
                cushionWorld);
            if (!_hasTransitionPelvisSample || _transitionPelvisClip != clip)
            {
                _hasTransitionPelvisSample = true;
                _transitionPelvisClip = clip;
                _previousTransitionPelvisOffsetScreen = pelvisOffsetScreen;
                _previousTransitionCushionDistancePx = cushionDistancePx;
                return;
            }

            _maxTransitionPelvisStepPx = Mathf.Max(
                _maxTransitionPelvisStepPx,
                Vector2.Distance(_previousTransitionPelvisOffsetScreen, pelvisOffsetScreen));
            const float monotonicTolerancePx = 0.01f;
            bool reversed = clip == OfficeSeatingAnimationClip.SitDown
                ? cushionDistancePx > _previousTransitionCushionDistancePx + monotonicTolerancePx
                : cushionDistancePx < _previousTransitionCushionDistancePx - monotonicTolerancePx;
            if (reversed) _transitionMonotonicViolationCount++;
            _previousTransitionPelvisOffsetScreen = pelvisOffsetScreen;
            _previousTransitionCushionDistancePx = cushionDistancePx;
        }

        private void ResetTransitionMotionMetrics()
        {
            _hasTransitionPelvisSample = false;
            _previousTransitionPelvisOffsetScreen = Vector2.zero;
            _previousTransitionCushionDistancePx = 0f;
        }

        private void RecordChairPresentationMotion()
        {
            if (_seat == null || Camera.main == null) return;
            Vector3 chairScreen3 = Camera.main.WorldToScreenPoint(
                _world.Workstations.ChairSeatAnchorWorld(_seat));
            var chairScreen = new Vector2(chairScreen3.x, chairScreen3.y);
            if (_hasChairPresentationSample)
            {
                _maxChairPresentationStepPx = Mathf.Max(
                    _maxChairPresentationStepPx,
                    Vector2.Distance(_previousChairPresentationScreen, chairScreen));
            }
            _hasChairPresentationSample = true;
            _previousChairPresentationScreen = chairScreen;
        }

        private void RecordVisibleSeatingPelvisContinuity(
            OfficeCharacterSeatPoseProfile profile)
        {
            if (profile == null || Camera.main == null) return;
            Vector3 pelvisScreen3 = Camera.main.WorldToScreenPoint(
                OfficeSeatedOccupantContract.OccupantSeatContactWorld(
                    _renderer,
                    profile.PelvisAnchorPx));
            var pelvisScreen = new Vector2(pelvisScreen3.x, pelvisScreen3.y);
            if (_hasVisibleSeatingPelvisSample)
            {
                _maxTransitionPelvisStepPx = Mathf.Max(
                    _maxTransitionPelvisStepPx,
                    Vector2.Distance(_previousVisibleSeatingPelvisScreen, pelvisScreen));
            }
            _hasVisibleSeatingPelvisSample = true;
            _previousVisibleSeatingPelvisScreen = pelvisScreen;
        }

        private void RecordSeatingFacingInvariant()
        {
            if (!IsOccupyingSeat || _animator == null) return;
            bool valid = _animator.IsOfficeSeatingFacingLocked &&
                         _animator.LockedOfficeSeatingDirection == _seatDirection &&
                         _animator.CurrentDirection == _seatDirection &&
                         _animator.IsCurrentAppliedSpriteDirectionLocked;
            if (!valid) _seatingFacingViolationCount++;
        }

        internal void RecordSeatingDepthSample(OfficeSeatingDepthSnapshot sample)
        {
            _lastSeatingDepthSample = sample;
            if (!sample.IsValidStack) _seatingDepthViolationCount++;
        }

        private void TrackWorkstationMetrics()
        {
            if (_seat == null || Camera.main == null) return;
            OfficeCharacterSeatPoseProfile profile = ResolveSeatPresentationProfile(
                OfficeSeatingAnimationClip.Work,
                _alignedFrame < 0 ? 0 : _alignedFrame);
            Vector3 chairScreen = Camera.main.WorldToScreenPoint(
                _world.Workstations.ChairSeatAnchorWorld(_seat));
            Vector3 deskScreen = Camera.main.WorldToScreenPoint(
                _world.Workstations.DeskSeatSocketWorld(_seat));
            _chairDeskDeltaPx = new Vector2(chairScreen.x - deskScreen.x, chairScreen.y - deskScreen.y);
            _chairDeskErrorPx = _chairDeskDeltaPx.magnitude;
            _seatContactErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                OfficeSeatedOccupantContract.OccupantSeatContactWorld(_renderer, profile.PelvisAnchorPx),
                _world.Workstations.ChairSeatAnchorWorld(_seat));
            _handWorkErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(_renderer, profile.HandAnchorPx),
                _world.Workstations.DeskWorkSocketWorld(_seat));
            if (_animator.CurrentOfficeWorkMicroAction == OfficeWorkMicroAction.Typing)
            {
                _typingContactSampleCount++;
                _maxTypingSeatContactErrorPx = Mathf.Max(
                    _maxTypingSeatContactErrorPx,
                    _seatContactErrorPx);
                _maxTypingHandWorkErrorPx = Mathf.Max(
                    _maxTypingHandWorkErrorPx,
                    _handWorkErrorPx);
            }
        }

        private OfficeCharacterSeatPoseProfile ResolveSeatPresentationProfile(
            OfficeSeatingAnimationClip clip,
            int frame)
        {
            try
            {
                return _poseCatalog.ResolveApproved(
                    _agentId,
                    _seatDirection,
                    clip,
                    frame);
            }
            catch (Exception) when (_externalDirectionalSeatingPresentation)
            {
                // The external presenter owns all visible body contacts and uses the actual
                // workstation sockets. The legacy SafeStaticWork profile is still needed by the
                // atomic docking transaction as a non-rendered pelvis baseline, so reuse its one
                // approved northwest profile rather than fabricating rotated 2D calibration.
                return _poseCatalog.ResolveApproved(
                    _agentId,
                    RequiredR5eSeatPreloadDirection,
                    clip,
                    frame);
            }
        }

        private void ReleaseSeatImmediately()
        {
            _attendanceWorkHandoffActive = false;
            ClearSeatEgressReservation();
            OfficeSeatSlot seat = _seat;
            OfficeSeatRuntimeClaim claim = _seatClaim;
            _seat = null;
            _seatClaim = null;
            try
            {
                claim?.TryRelease(out _);
            }
            finally
            {
                if (seat != null && _world != null) _world.Workstations.ClearOcclusion(seat);
                _releaseSeatRequested = false;
                _alignedClip = null;
                _alignedFrame = -1;
                _seatedUpperBodyCutoffPx = float.NaN;
                ClearSeatedUpperBodyProtection();
                _sitTransitionInitialized = false;
                _standTransitionInitialized = false;
                _sitPlacementProgress01 = 0f;
                _standPlacementProgress01 = 0f;
                _seatPresentationPrepared = false;
                _seatAlignmentComplete = false;
                _authoredSeatPelvisWorld = Vector3.zero;
                _workPresentationTargetPelvisWorld = Vector3.zero;
                _finishingWorkPresentationObserved = false;
                ResetTransitionMotionMetrics();
                if (_animator != null)
                {
                    if (_animator.IsOfficeSeatingPoseActive)
                        _animator.ResumeWalkingAfterSeating();
                    else if (_animator.IsOfficeSeatingFacingLocked)
                        _animator.ReleaseOfficeSeatingFacingLock();
                }
                ResetVisualPose();
            }
        }

        private void AbortInteractionAttempt(OfficeRuntimeInteractionEndReason reason)
        {
            EndInteraction(OfficeRuntimeInteractionTermination.Aborted, reason);
            _world?.Occupancy.ClearReservations(_agentId);
            _destination = null;
            _pendingDestination = null;
            _autonomyDestination = null;
            _autonomyLayoutRevision = -1;
            _path.Clear();
            _pathIndex = 0;
            _arrived = false;
            _yieldCell = null;
            _standingFacingDirection = -1;
            if (Phase != OfficeRuntimeAgentPhase.FinishingWork &&
                Phase != OfficeRuntimeAgentPhase.StandingUp &&
                Phase != OfficeRuntimeAgentPhase.LeavingSeat)
            {
                Phase = OfficeRuntimeAgentPhase.Idle;
                CurrentActivity = OfficeActivity.Break;
            }
        }

        private void EndInteraction(
            OfficeRuntimeInteractionTermination termination,
            OfficeRuntimeInteractionEndReason reason)
        {
            bool hadInteraction = _interactionPhase != OfficeRuntimeInteractionPhase.None ||
                                  _interactionHandle != null ||
                                  _activeInteractionId.Length > 0;
            if (!hadInteraction) return;

            _interactionPhase = OfficeRuntimeInteractionPhase.Finishing;
            bool completed = termination == OfficeRuntimeInteractionTermination.Completed;
            if (_interactionHandle != null)
            {
                if (completed)
                {
                    if (!_interactionHandle.TryComplete(out _))
                    {
                        completed = false;
                        _interactionHandle.TryAbort(out _);
                    }
                }
                else
                {
                    _interactionHandle.TryAbort(out _);
                }
                _interactionHandle.TryRelease(out _);
            }

            if (completed) _interactionCompletedCount++;
            else if (termination != OfficeRuntimeInteractionTermination.None) _interactionAbortedCount++;
            _lastInteractionEndReason = reason;
            ClearInteractionExecutionState();
        }

        private void ClearInteractionExecutionState()
        {
            _interactionHandle = null;
            _interactionPhase = OfficeRuntimeInteractionPhase.None;
            _activeInteractionId = string.Empty;
            _activeInteractionOfferId = string.Empty;
            _activeInteractionFurnitureId = string.Empty;
        }

        private void ResetVisualPose()
        {
            if (_visualRoot == null) return;
            _locomotionVisualFootPlantOffsetWorld = Vector2.zero;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
        }

        private void ApplyLocomotionFootPlantPresentation()
        {
            if (_visualRoot == null || _animator == null) return;

            bool seatingOwnsVisualRoot =
                _animator.IsOfficeSeatingPoseActive || IsOccupyingSeat || IsEnteringSeat;
            if (seatingOwnsVisualRoot)
            {
                // Seat contact placement is authoritative and must never inherit a walking offset.
                _locomotionVisualFootPlantOffsetWorld = Vector2.zero;
                return;
            }

            // The authored six-pose cycle already contains the complete foot, hip and shoulder
            // motion. Easing the whole visual root from contact to contact made its screen speed
            // oscillate from zero to 1.5x while the collision root moved linearly, which read as a
            // repeated hop. Keep the sprite root locked to the authoritative navigation root.
            _locomotionVisualFootPlantOffsetWorld = Vector2.zero;
            _visualRoot.localPosition = Vector3.zero;
        }

        private void SetPresentationAway(bool away)
        {
            _presentationAway = away;
            if (_renderer != null) _renderer.enabled = !away;
            if (_seatedUpperBodyRenderer != null)
                _seatedUpperBodyRenderer.enabled =
                    !away && IsOccupyingSeat && IsSeatForegroundOcclusionEngaged;
            if (_world != null &&
                _world.Registry.TryGet(_agentId, out OfficeRuntimeAgent registered) &&
                ReferenceEquals(registered, this))
                _world.Occupancy.SetActorPresent(_agentId, !away);
        }

        private static int FacingDirection(OfficeFurnitureFacing facing)
        {
            return facing switch
            {
                OfficeFurnitureFacing.SouthEast => 7,
                OfficeFurnitureFacing.SouthWest => 1,
                OfficeFurnitureFacing.NorthWest => 3,
                OfficeFurnitureFacing.NorthEast => 5,
                _ => 4
            };
        }

        private void OnDisable()
        {
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.Disabled);
            ReleaseAttendanceIngress();
            if (_world != null) _world.Occupancy.ClearReservations(_agentId);
            ReleaseSeatImmediately();
        }

        private void OnDestroy()
        {
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.Destroyed);
            if (_animator != null)
            {
                _animator.OfficeFrameApplied -= HandleOfficeFrameApplied;
                _animator.SetExternallyTicked(false);
            }
            ReleaseAttendanceIngress();
            if (_world != null) _world.Occupancy.UnregisterActor(_agentId);
            ReleaseSeatImmediately();
            DestroySeatedUpperBodyProtection();
        }
    }
}

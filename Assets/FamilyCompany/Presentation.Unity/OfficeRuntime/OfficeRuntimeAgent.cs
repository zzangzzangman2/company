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
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeRuntimeAgentPhase
    {
        Idle = 0,
        Navigating = 1,
        MovingToSit = 2,
        SittingDown = 3,
        Working = 4,
        FinishingWork = 5,
        StandingUp = 6,
        LeavingSeat = 7,
        Outside = 8
    }

    [DisallowMultipleComponent]
    public sealed class OfficeRuntimeAgent : MonoBehaviour, IOfficeRuntimeAgent
    {
        public const float DefaultRadius = 0.22f;
        public const float DefaultMoveSpeed = 1.65f;
        private const float ArrivalDistance = 0.035f;
        // One shared tolerance minimizes chair pull-out while keeping every approved typing hand
        // on the workstation. The remaining translation is derived only from pose anchors and
        // furniture sockets; member IDs never participate in the placement rule.
        private const float TypingHandContactBudgetPx = 3.499f;
        // The occupant may lead the pulled-out chair by less than one rendered pixel. This keeps
        // both the hand/contact calibration and chair-to-desk presentation inside their contracts
        // without making the rule depend on the active camera zoom or the member identity.
        private const float TypingSeatContactBudgetPx = 0.899f;
        // Keep the generated displacement strictly below the public 0.9 px contract so camera
        // projection roundoff can never turn an exactly-on-boundary step into a false violation.
        private const float MaximumChairPresentationStepPx = 0.899f;
        private const float MaximumSeatEgressStepPx = 0.899f;
        private const float SeatEgressCompletionTolerancePx = 0.25f;

        private PrototypeBootstrap _bootstrap;
        private OfficeRuntimeWorld _world;
        private string _agentId;
        private bool _playerControlled;
        private SpriteRenderer _renderer;
        private SpriteRenderer _seatedUpperBodyRenderer;
        private Transform _visualRoot;
        private DirectionalSpriteAnimator _animator;
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
        private int _attendanceSeatArrivalCount;
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
        private Vector2 _lastActualDisplacement;
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
        private bool _chairPresentationReturning;
        private bool _chairPresentationMoveComplete;
        private Vector3 _chairPresentationAuthoredPelvisWorld;
        private Vector3 _chairPresentationTargetPelvisWorld;
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

        public event Action<IOfficeRuntimeAgent, string> AssignedTaskCompleted;

        public string AgentId => _agentId;
        public string MemberId => _agentId;
        public bool IsPlayerControlled => _playerControlled;
        public bool HasAssignedTask => _assignedTaskId.Length > 0;
        public string AssignedTaskId => _assignedTaskId;
        public bool IsSeated => Phase == OfficeRuntimeAgentPhase.Working;

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
        public bool IsPresentationAway => _presentationAway;
        public int AttendanceSeatArrivalCount => _attendanceSeatArrivalCount;
        public string LastReservationBlocker { get; private set; } = string.Empty;
        public string LastMovementBlocker { get; private set; } = string.Empty;

        public OfficeObservationStatusKind StatusKind
        {
            get
            {
                if (_presentationAway) return OfficeObservationStatusKind.Outside;
                if (Phase == OfficeRuntimeAgentPhase.Navigating ||
                    Phase == OfficeRuntimeAgentPhase.MovingToSit ||
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
            _playerControlled = playerControlled;
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _visualRoot = visualRoot ?? throw new ArgumentNullException(nameof(visualRoot));
            if (_animator != null) _animator.OfficeFrameApplied -= HandleOfficeFrameApplied;
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _animator.SetExternallyTicked(true);
            _animator.OfficeFrameApplied += HandleOfficeFrameApplied;
            _poseCatalog = poseCatalog ?? throw new ArgumentNullException(nameof(poseCatalog));
            AgentRadius = Mathf.Max(0.12f, radius);
            transform.position = _world.Presenter.CellCenterWorld(spawnCell);
            transform.localScale = Vector3.one;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
            Phase = OfficeRuntimeAgentPhase.Idle;
            _pathRevision = _world.Occupancy.Revision;
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
            if (!HasAssignedTask && !_attendanceArrivalActive) TryStartAutonomyRequest();
        }

        public void ClearAutonomousDestination()
        {
            EndInteraction(
                OfficeRuntimeInteractionTermination.Aborted,
                OfficeRuntimeInteractionEndReason.Cleared);
            _autonomyIntentId = string.Empty;
            _autonomyRequestedLocation = OfficeSemanticLocation.None;
            _autonomyRequestedInteractionId = string.Empty;
            _autonomyLayoutRevision = -1;
            _autonomyStatus = string.Empty;
            _autonomyDestination = null;
            if (!HasAssignedTask && !_playerControlled) RequestStopAndStand();
        }

        public void ResetRuntimeState()
        {
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
            _attendanceDepartureActive = false;
            _attendanceArrivalActive = false;
            _attendanceSeatArrivalCount = 0;
            _preparedAttendanceDestination = null;
            _preparedAttendancePath.Clear();
            _standingFacingDirection = -1;
            _destination = null;
            _pendingDestination = null;
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
            Vector3 entrance = _world.Presenter.CellCenterWorld(entry.Cell);
            transform.position = new Vector3(entrance.x, entrance.y, transform.position.z);
            if ((!_preparedAttendanceDestination.HasValue || _preparedAttendancePath.Count == 0) &&
                !PrepareAttendanceArrival()) return;
            if (BeginPreparedAttendanceDestination(
                    _preparedAttendanceDestination.Value,
                    _preparedAttendancePath))
            {
                _attendanceArrivalActive = true;
                Debug.Log(
                    "STARTER_OFFICE_ATTENDANCE_ENTRY | member=" + _agentId +
                    " | routeCells=" + _preparedAttendancePath.Count +
                    " | route=" + string.Join(">", _preparedAttendancePath) +
                    " | destination=" + _preparedAttendanceDestination.Value.DestinationId);
            }
        }

        /// <summary>
        /// Resolves the family's canonical door-to-desk route while the loading presentation is
        /// visible. The 09:00 arrival then only reserves its already assigned seat and adopts this
        /// route, avoiding a synchronous path search or a temporary corridor stop on entry.
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
                    out OfficeRuntimeDestination destination)) return false;
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

        public void BeginQaControl()
        {
            _qaControl = true;
            ResetRuntimeState();
        }

        public void EndQaControl()
        {
            _qaControl = false;
            _playerInput = Vector2.zero;
            ResetRuntimeState();
        }

        public void QaTeleportToCell(OfficeGridCoordinate cell)
        {
            if (!_world.Grid.Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            BeginQaControl();
            Vector3 target = _world.Presenter.CellCenterWorld(cell);
            transform.position = new Vector3(target.x, target.y, transform.position.z);
            _world.Occupancy.UpdateActor(_agentId, Position, Vector2.zero, 0f);
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
            if (IsSeated || Phase == OfficeRuntimeAgentPhase.SittingDown ||
                Phase == OfficeRuntimeAgentPhase.MovingToSit)
                RequestStopAndStand();
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
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                _desiredVelocity,
                _stuckSeconds,
                _seat?.SeatId ?? string.Empty);

            if (_playerControlled && !_attendanceDepartureActive && !_attendanceArrivalActive &&
                _playerInput.sqrMagnitude > 0.0001f)
            {
                if (Phase == OfficeRuntimeAgentPhase.Working ||
                    Phase == OfficeRuntimeAgentPhase.SittingDown ||
                    Phase == OfficeRuntimeAgentPhase.MovingToSit)
                {
                    RequestStopAndStand();
                }
                else if (Phase == OfficeRuntimeAgentPhase.Idle || Phase == OfficeRuntimeAgentPhase.Navigating)
                {
                    _destination = null;
                    _path.Clear();
                    TickDirectPlayerMovement(deltaTime);
                    return;
                }
            }

            if (_playerControlled && _playerInput.sqrMagnitude <= 0.0001f &&
                !_destination.HasValue && Phase == OfficeRuntimeAgentPhase.Navigating)
            {
                Phase = OfficeRuntimeAgentPhase.Idle;
                CurrentActivity = OfficeActivity.Break;
            }

            switch (Phase)
            {
                case OfficeRuntimeAgentPhase.MovingToSit:
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

        public void BeginPresentationFrame()
        {
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
            BeginChairPresentationReturnAfterSafeAnchor();
            AdvanceChairPresentation();
            RecordChairPresentationMotion();
            _animator.Tick(presentationDeltaTime);
            _animator.EndTilePresentationFrame();
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
            _standingFacingDirection = -1;
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
                                       (Phase == OfficeRuntimeAgentPhase.MovingToSit ||
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
                Phase == OfficeRuntimeAgentPhase.MovingToSit ||
                Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp ||
                Phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                if (_destination.HasValue &&
                    _destination.Value.DestinationId == destination.DestinationId &&
                    destination.RequiresSeat) return true;
                _pendingDestination = destination;
                RequestStopAndStand();
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
                            "starter-office-seat:" + _agentId + ":" + destination.DestinationId,
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

        private bool BeginPreparedAttendanceDestination(
            OfficeRuntimeDestination destination,
            IReadOnlyList<OfficeGridCoordinate> route)
        {
            if (!destination.RequiresSeat || route == null || route.Count < 2) return false;
            OfficeGridCoordinate current = _world.Presenter.NearestCell(transform.position);
            if (!route[0].Equals(current) ||
                !route[route.Count - 1].Equals(destination.Cell)) return false;

            _standingFacingDirection = -1;
            ReleaseSeatImmediately();
            if (!_world.Workstations.TryReserveSeat(
                    _agentId,
                    destination.SeatId,
                    "starter-office-attendance-seat:" + _agentId,
                    out _seat,
                    out _seatClaim)) return false;
            destination = _world.Workstations.DestinationForSeat(_seat, destination);

            SetPresentationAway(false);
            _world.Occupancy.UpdateActor(
                _agentId,
                Position,
                Vector2.zero,
                0f,
                string.Empty);
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
            return true;
        }

        private bool RebuildPath()
        {
            if (!_destination.HasValue) return false;
            OfficeGridCoordinate start = _world.Presenter.NearestCell(transform.position);
            IReadOnlyList<OfficeGridCoordinate> result = _world.FindPath(
                _agentId,
                start,
                _destination.Value.Cell,
                string.Empty,
                _stuckSeconds >= OfficeNavigationTrafficRules.ReplanThresholdSeconds,
                AgentRadius);
            _path.Clear();
            _path.AddRange(result);
            _pathIndex = _path.Count > 1 ? 1 : 0;
            _presentationPathIndex = _pathIndex;
            _pathRevision = _world.Occupancy.Revision;
            if (_path.Count == 0) return false;
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
                string.Empty);
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
                string.Empty);
            if (!_world.Occupancy.TryReservePath(_agentId, currentCell, _upcomingPathCells))
            {
                LastReservationBlocker = _world.Occupancy.DescribePathReservationBlocker(
                    _agentId,
                    currentCell,
                    _upcomingPathCells);
                _stuckSeconds += deltaTime;
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
                        string.Empty))
                {
                    if (_stuckSeconds >= 2.0f) _world.Occupancy.ClearReservations(_agentId);
                    return;
                }
                if (blockedTraffic.RecoveryWeight > 0f)
                {
                    var recovery = new Vector2(
                        blockedTraffic.RecoveryDirection.X,
                        blockedTraffic.RecoveryDirection.Z) * (DefaultMoveSpeed * 0.72f);
                    MoveWithCollision(recovery, deltaTime, string.Empty);
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
                transform.position = new Vector3(target.x, target.y, transform.position.z);
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
            if (traffic.RecoveryWeight > 0f)
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
                string.Empty,
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
                transform.position = new Vector3(target.x, target.y, transform.position.z);
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
                Phase = OfficeRuntimeAgentPhase.MovingToSit;
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
            if (_seat == null || _seatClaim == null || _seatClaim.IsReleased)
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
                case OfficeRuntimeAgentPhase.MovingToSit:
                {
                    _seatDirection = FacingDirection(_seat.Facing);
                    if (!PrepareChairPresentationForWork())
                    {
                        EndInteraction(
                            OfficeRuntimeInteractionTermination.Aborted,
                            OfficeRuntimeInteractionEndReason.SeatUnavailable);
                        Phase = OfficeRuntimeAgentPhase.Idle;
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    Vector3 target3 = _world.Workstations.SeatOperatorWorld(_seat);
                    Vector2 target = new Vector2(target3.x, target3.y);
                    Vector2 delta = target - Position;
                    if (delta.magnitude > ArrivalDistance)
                    {
                        Vector2 velocity = delta.normalized * 1.15f;
                        MoveWithCollision(velocity, deltaTime, _seat.SeatId, delta.magnitude);
                        return;
                    }
                    transform.position = new Vector3(target.x, target.y, transform.position.z);
                    StopMotion();
                    if (_animator.CurrentDirection != _seatDirection)
                    {
                        _animator.AccumulateStandingFacingRequest(_seatDirection, deltaTime);
                        return;
                    }
                    if (!_animator.IsOfficeSeatingEntryPlanted ||
                        !_chairPresentationMoveComplete) return;
                    _sitTransitionInitialized = false;
                    _standTransitionInitialized = false;
                    _hasTransitionPelvisSample = false;
                    if (!_seatClaim.TryOccupy(out _) ||
                        !_animator.TryLockOfficeSeatingFacingAfterPlantedRotation(_seatDirection) ||
                        !_animator.BeginSitDown(_seatDirection))
                    {
                        EndInteraction(
                            OfficeRuntimeInteractionTermination.Aborted,
                            OfficeRuntimeInteractionEndReason.SeatUnavailable);
                        Phase = OfficeRuntimeAgentPhase.Idle;
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    _seatFacingAlignedBeforeSitDown = true;
                    Phase = OfficeRuntimeAgentPhase.SittingDown;
                    CurrentActivity = _destination.HasValue
                        ? _destination.Value.Activity
                        : OfficeActivity.Work;
                    break;
                }
                case OfficeRuntimeAgentPhase.SittingDown:
                    StopMotion();
                    if (!_animator.IsOfficeSeatingTransitionComplete || _sitPlacementProgress01 < 0.9999f)
                        return;
                    if (!_animator.BeginSeatedWork())
                    {
                        EndInteraction(
                            OfficeRuntimeInteractionTermination.Aborted,
                            OfficeRuntimeInteractionEndReason.SeatUnavailable);
                        Phase = OfficeRuntimeAgentPhase.Idle;
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    Phase = OfficeRuntimeAgentPhase.Working;
                    _arrived = true;
                    if (_attendanceArrivalActive)
                    {
                        _attendanceArrivalActive = false;
                        _attendanceSeatArrivalCount++;
                        Debug.Log(
                            "STARTER_OFFICE_ATTENDANCE_SEATED | member=" + _agentId +
                            " | count=" + _attendanceSeatArrivalCount);
                    }
                    if (_interactionPhase == OfficeRuntimeInteractionPhase.Aligning)
                        _interactionPhase = OfficeRuntimeInteractionPhase.Performing;
                    break;
                case OfficeRuntimeAgentPhase.Working:
                    StopMotion();
                    TrackWorkstationMetrics();
                    if (HasAssignedTask && _assignedActivity == CurrentActivity)
                        AdvanceAssignedWork();
                    if (_releaseSeatRequested) BeginSafeStand();
                    break;
                case OfficeRuntimeAgentPhase.FinishingWork:
                    StopMotion();
                    if (!_finishingWorkPresentationObserved) return;
                    if (!_animator.IsOfficeWorkSafeToStand) return;
                    if (!TryPrepareSeatEgressReservation())
                    {
                        _seatEgressWaiting = true;
                        return;
                    }
                    _seatEgressWaiting = false;
                    if (!_animator.BeginStandUp())
                    {
                        _seatEgressUnsafePhaseTransitionCount++;
                        ClearSeatEgressReservation();
                        _seatEgressWaiting = true;
                        return;
                    }
                    _standTransitionInitialized = false;
                    _hasTransitionPelvisSample = false;
                    Phase = OfficeRuntimeAgentPhase.StandingUp;
                    break;
                case OfficeRuntimeAgentPhase.StandingUp:
                    StopMotion();
                    if (!_seatEgressReservationActive ||
                        !_world.Occupancy.HasReservation(
                            _agentId,
                            _seatEgressCandidate.TargetCell))
                    {
                        _seatEgressUnsafePhaseTransitionCount++;
                        return;
                    }
                    if (!_animator.IsOfficeSeatingTransitionComplete || _standPlacementProgress01 < 0.9999f)
                        return;
                    ResetVisualPose();
                    if (!_animator.FinishOfficeSeatingPoseForLeavingSeat())
                    {
                        _seatEgressUnsafePhaseTransitionCount++;
                        return;
                    }
                    Phase = OfficeRuntimeAgentPhase.LeavingSeat;
                    break;
                case OfficeRuntimeAgentPhase.LeavingSeat:
                {
                    if (!_seatEgressReservationActive)
                    {
                        _seatEgressUnsafePhaseTransitionCount++;
                        StopMotion();
                        return;
                    }

                    if (!_seatEgressReachedSafeAnchor)
                    {
                        TickSeatEgressDismount(deltaTime);
                        return;
                    }

                    StopMotion();
                    BeginChairPresentationReturnAfterSafeAnchor();
                    if (!_chairPresentationMoveComplete) return;
                    _hasCompletedSeatEgress = true;
                    _lastCompletedSeatEgressKind = _seatEgressCandidate.Kind;
                    _lastCompletedSeatEgressCell = _seatEgressCandidate.TargetCell;
                    _lastCompletedSeatEgressWorld = _seatEgressTargetWorld;
                    _lastCompletedSeatEgressClearanceValid = true;
                    ReleaseSeatImmediately();
                    // Releasing the claim also releases the seat-facing lock, so LeavingSeat has
                    // ended at this exact point. Do not expose a one-frame unlocked LeavingSeat
                    // state to presentation/depth consumers before the next simulation tick.
                    Phase = OfficeRuntimeAgentPhase.Idle;
                    if (_pendingDestination.HasValue)
                    {
                        OfficeRuntimeDestination pending = _pendingDestination.Value;
                        _pendingDestination = null;
                        if (!BeginDestination(pending) &&
                            _interactionPhase != OfficeRuntimeInteractionPhase.None)
                            AbortInteractionAttempt(
                                OfficeRuntimeInteractionEndReason.PathUnavailable);
                    }
                    else ResumeAutonomy();
                    break;
                }
            }
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

        private void RequestStopAndStand()
        {
            _standingFacingDirection = -1;
            _destination = null;
            _path.Clear();
            _arrived = false;
            if (Phase == OfficeRuntimeAgentPhase.Working ||
                Phase == OfficeRuntimeAgentPhase.SittingDown ||
                Phase == OfficeRuntimeAgentPhase.MovingToSit)
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
            ClearSeatEgressReservation();
            _seatEgressWaiting = false;
            _seatEgressReachedSafeAnchor = false;
            _lastSeatEgressBlocker = string.Empty;
            _releaseSeatRequested = true;
            _finishingWorkPresentationObserved = false;
            _animator.RequestOfficeWorkSafeStop();
            Phase = OfficeRuntimeAgentPhase.FinishingWork;
        }

        private bool TryPrepareSeatEgressReservation()
        {
            if (_seatEgressReservationActive)
            {
                bool retained = _world != null &&
                                _world.Occupancy.HasReservation(
                                    _agentId,
                                    _seatEgressCandidate.TargetCell);
                if (retained) return true;
                ClearSeatEgressReservation();
            }
            if (_seat == null || _world == null) return false;

            _seatEgressReservationAttemptCount++;
            _world.Occupancy.ClearReservations(_agentId);
            if (!_world.Occupancy.IsActorPresent(_agentId))
            {
                _seatEgressBlockedAttemptCount++;
                _lastSeatEgressBlocker = "actor-not-present";
                return false;
            }

            Vector2 start = Position;
            IReadOnlyList<OfficeSeatEgressCandidate> candidates =
                _world.Workstations.ResolveEgressCandidates(_seat);
            for (var index = 0; index < candidates.Count; index++)
            {
                OfficeSeatEgressCandidate candidate = candidates[index];
                OfficeGridCoordinate cell = candidate.TargetCell;
                if (!_world.Grid.Contains(cell) || !_world.Grid.IsWalkable(cell))
                {
                    _lastSeatEgressBlocker = candidate.Kind + ":floor-not-walkable:" + cell;
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
                    _lastSeatEgressBlocker = candidate.Kind + ":target-static-clearance:" + cell;
                    continue;
                }
                if (!_world.Occupancy.CanTraverseStatic(
                        start,
                        target,
                        AgentRadius,
                        _seat.SeatId))
                {
                    _lastSeatEgressBlocker = candidate.Kind + ":segment-static-clearance:" + cell;
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
                    _lastSeatEgressBlocker = candidate.Kind + ":dynamic-clearance:" + cell;
                    continue;
                }
                if (!_world.Occupancy.TryReservePath(
                        _agentId,
                        _seat.Cell,
                        new[] { cell }))
                {
                    _lastSeatEgressBlocker = candidate.Kind + ":reservation:" + cell;
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
                    _lastSeatEgressBlocker = candidate.Kind + ":segment-dynamic-clearance:" + cell;
                    continue;
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
                if (moved > _seatEgressFrameMovementBudgetWorld + 0.0000001f) return;
                transform.position = new Vector3(
                    _seatEgressTargetWorld.x,
                    _seatEgressTargetWorld.y,
                    transform.position.z);
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
            Vector2 integrationTargetVelocity = targetVelocity;
            // LeavingSeat deliberately moves away from the chair while the presentation remains
            // locked to the seat facing. The normal pre-move pivot gate would otherwise wait for a
            // direction change that the lock is correctly refusing, deadlocking the exit step.
            bool preserveSeatFacingWhileLeaving =
                Phase == OfficeRuntimeAgentPhase.LeavingSeat &&
                _animator != null &&
                _animator.IsOfficeSeatingFacingLocked;
            bool waitingForPivot = !preserveSeatFacingWhileLeaving &&
                                   RequiresPivotBeforeMoving(targetVelocity);
            if (waitingForPivot) integrationTargetVelocity = Vector2.zero;
            float changePerSecond = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(integrationTargetVelocity.x, integrationTargetVelocity.y),
                7.5f,
                _playerControlled);
            OfficeMotionIntegrationResult motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(integrationTargetVelocity.x, integrationTargetVelocity.y),
                changePerSecond,
                deltaTime);
            _currentVelocity = new Vector2(motion.Velocity.X, motion.Velocity.Z);
            OfficeNavPoint clampedDisplacement = float.IsPositiveInfinity(maximumDistance)
                ? motion.Displacement
                : OfficeNavigationMotionIntegrator.ClampDisplacement(
                    motion.Displacement,
                    Mathf.Max(0f, maximumDistance));
            Vector2 intended = new Vector2(clampedDisplacement.X, clampedDisplacement.Z);
            Vector2 before = Position;
            Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                _world.Occupancy,
                _agentId,
                before,
                intended,
                integrationTargetVelocity,
                _lastActualDisplacement,
                AgentRadius,
                permittedSeatId,
                out bool collisionProjected);
            if (actual.sqrMagnitude > 0.0000001f)
            {
                transform.position = new Vector3(
                    before.x + actual.x,
                    before.y + actual.y,
                    transform.position.z);
                _stuckSeconds = Mathf.Max(0f, _stuckSeconds - deltaTime * 2f);
            }
            else
            {
                _currentVelocity = Vector2.zero;
                if (targetVelocity.sqrMagnitude > 0.01f) _stuckSeconds += deltaTime;
                LastMovementBlocker = waitingForPivot
                    ? $"pivot={_animator.CurrentDirection}->{DirectionalSpriteAnimator.ResolveTileDirection(targetVelocity, _animator.CurrentDirection)}:{_animator.LocomotionPhase}"
                    : _world.Occupancy.DescribeMoveBlocker(
                        _agentId,
                        before,
                        before + intended,
                        AgentRadius,
                        permittedSeatId);
            }
            if (actual.sqrMagnitude > 0.0000001f) LastMovementBlocker = string.Empty;
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

        private bool RequiresPivotBeforeMoving(Vector2 targetVelocity)
        {
            if (_animator == null || targetVelocity.sqrMagnitude <= 0.000001f) return false;
            int current = _animator.CurrentDirection;
            int target = DirectionalSpriteAnimator.ResolveTileDirection(targetVelocity, current);
            return OfficeSharedLocomotionRules.RequiresStationaryPivot(
                current,
                target,
                _animator.LocomotionPhase);
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
            OfficeCharacterSeatPoseProfile profile = _poseCatalog.ResolveApproved(
                _agentId,
                _seatDirection,
                clip,
                frame);
            _seatedUpperBodyCutoffPx = profile.PelvisAnchorPx.y;
            RecordObservedSeatingFrame(clip, frame);
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

        private bool PrepareChairPresentationForWork()
        {
            if (_seatPresentationPrepared) return true;
            if (_seat == null || _renderer == null || _renderer.sprite == null) return false;

            _world.Workstations.RestoreChairPresentation(_seat);
            ResetVisualPose();
            _chairPresentationAuthoredPelvisWorld =
                _world.Workstations.ChairSeatAnchorWorld(_seat);
            _chairPresentationTargetPelvisWorld = _chairPresentationAuthoredPelvisWorld;
            _workPresentationTargetPelvisWorld = _chairPresentationAuthoredPelvisWorld;
            _chairPresentationReturning = false;
            if (!_seat.HasWorkstationBinding)
            {
                _seatPresentationPrepared = true;
                _chairPresentationMoveComplete = true;
                SeedChairPresentationMotionSample();
                return true;
            }

            OfficeCharacterSeatPoseProfile profile = _poseCatalog.ResolveApproved(
                _agentId,
                _seatDirection,
                OfficeSeatingAnimationClip.Work,
                0);
            Vector2 pelvisToHandPx = profile.HandAnchorPx - profile.PelvisAnchorPx;
            if (_renderer.flipX) pelvisToHandPx.x = -pelvisToHandPx.x;
            if (_renderer.flipY) pelvisToHandPx.y = -pelvisToHandPx.y;
            Vector3 pelvisToHandWorld = _renderer.transform.TransformVector(new Vector3(
                pelvisToHandPx.x / _renderer.sprite.pixelsPerUnit,
                pelvisToHandPx.y / _renderer.sprite.pixelsPerUnit,
                0f));
            Vector3 exactHandContactPelvis =
                _world.Workstations.DeskWorkSocketWorld(_seat) - pelvisToHandWorld;
            Vector3 requiredShift =
                exactHandContactPelvis - _chairPresentationAuthoredPelvisWorld;
            float requiredShiftPx = Camera.main == null
                ? float.PositiveInfinity
                : OfficeGridAlignmentMetrics.WorldDisplacementScreenPx(
                    Camera.main,
                    _chairPresentationAuthoredPelvisWorld,
                    requiredShift);
            bool finiteShift = !float.IsNaN(requiredShiftPx) && !float.IsInfinity(requiredShiftPx);
            float shiftFraction = !finiteShift || requiredShiftPx <= 0.0001f
                ? 1f
                : Mathf.Clamp01(1f - (TypingHandContactBudgetPx / requiredShiftPx));
            _workPresentationTargetPelvisWorld =
                _chairPresentationAuthoredPelvisWorld + (requiredShift * shiftFraction);
            Vector3 requestedChairShift =
                _workPresentationTargetPelvisWorld - _chairPresentationAuthoredPelvisWorld;
            float requestedChairShiftPx = Camera.main == null
                ? 0f
                : OfficeGridAlignmentMetrics.WorldDisplacementScreenPx(
                    Camera.main,
                    _chairPresentationAuthoredPelvisWorld,
                    requestedChairShift);
            float chairShiftFraction = requestedChairShiftPx <= 0.0001f
                ? 1f
                : Mathf.Clamp01(1f - (TypingSeatContactBudgetPx / requestedChairShiftPx));
            _chairPresentationTargetPelvisWorld =
                _chairPresentationAuthoredPelvisWorld + requestedChairShift * chairShiftFraction;
            _seatPresentationPrepared = true;
            _chairPresentationMoveComplete =
                (_chairPresentationTargetPelvisWorld - _chairPresentationAuthoredPelvisWorld)
                .sqrMagnitude <= 0.0000001f;
            SeedChairPresentationMotionSample();
            return true;
        }

        private void BeginChairPresentationReturnAfterSafeAnchor()
        {
            if (!_seatPresentationPrepared || _chairPresentationReturning || _seat == null ||
                Phase != OfficeRuntimeAgentPhase.LeavingSeat ||
                !_seatEgressReachedSafeAnchor) return;
            _chairPresentationReturning = true;
            _chairPresentationTargetPelvisWorld = _chairPresentationAuthoredPelvisWorld;
            _chairPresentationMoveComplete = ChairPresentationDistancePxToTarget() <= 0.01f;
        }

        private void AdvanceChairPresentation()
        {
            if (!_seatPresentationPrepared || _chairPresentationMoveComplete || _seat == null) return;
            Vector3 current = _world.Workstations.ChairSeatAnchorWorld(_seat);
            if (Camera.main == null)
            {
                _world.Workstations.AlignChairPresentationToOccupant(
                    _seat,
                    _chairPresentationTargetPelvisWorld);
                _chairPresentationMoveComplete = true;
                return;
            }

            float remainingPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                current,
                _chairPresentationTargetPelvisWorld);
            if (remainingPx <= 0.01f)
            {
                _world.Workstations.AlignChairPresentationToOccupant(
                    _seat,
                    _chairPresentationTargetPelvisWorld);
                _chairPresentationMoveComplete = true;
                return;
            }

            float fraction = Mathf.Clamp01(MaximumChairPresentationStepPx / remainingPx);
            _world.Workstations.AlignChairPresentationToOccupant(
                _seat,
                Vector3.Lerp(current, _chairPresentationTargetPelvisWorld, fraction));
            _chairPresentationMoveComplete = fraction >= 0.9999f;
        }

        private float ChairPresentationDistancePxToTarget()
        {
            if (_seat == null) return 0f;
            if (Camera.main == null) return float.PositiveInfinity;
            return OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                _world.Workstations.ChairSeatAnchorWorld(_seat),
                _chairPresentationTargetPelvisWorld);
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
                        OfficeCharacterSeatPoseProfile finalProfile = _poseCatalog.ResolveApproved(
                            _agentId,
                            _seatDirection,
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
            OfficeCharacterSeatPoseProfile profile = _poseCatalog.ResolveApproved(
                _agentId,
                _seatDirection,
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

        private void ReleaseSeatImmediately()
        {
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
                _chairPresentationReturning = false;
                _chairPresentationMoveComplete = false;
                _chairPresentationAuthoredPelvisWorld = Vector3.zero;
                _chairPresentationTargetPelvisWorld = Vector3.zero;
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
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
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
            if (_world != null) _world.Occupancy.UnregisterActor(_agentId);
            ReleaseSeatImmediately();
            DestroySeatedUpperBodyProtection();
        }
    }
}

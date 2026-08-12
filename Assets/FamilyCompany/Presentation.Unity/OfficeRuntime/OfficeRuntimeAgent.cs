using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
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
        private const float CornerAnticipationDistance =
            OfficeLocomotionGaitRules.DefaultStrideLength * 0.20f;

        private PrototypeBootstrap _bootstrap;
        private OfficeRuntimeWorld _world;
        private string _agentId;
        private bool _playerControlled;
        private SpriteRenderer _renderer;
        private Transform _visualRoot;
        private DirectionalSpriteAnimator _animator;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private readonly List<OfficeGridCoordinate> _path = new List<OfficeGridCoordinate>();
        private int _pathIndex;
        private int _pathRevision;
        private OfficeRuntimeDestination? _destination;
        private OfficeRuntimeDestination? _pendingDestination;
        private OfficeRuntimeDestination? _autonomyDestination;
        private string _autonomyIntentId = string.Empty;
        private string _autonomyStatus = string.Empty;
        private string _assignedTaskId = string.Empty;
        private float _assignedWorkRemaining;
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
        private bool _presentationAway;
        private float _chairDeskErrorPx;
        private float _seatContactErrorPx;
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
        private int _observedSitDownFrameMask;
        private int _observedWorkFrameMask;
        private int _observedStandUpFrameMask;
        private float _maxAnimatedAnchorErrorPx;
        private bool _hasTransitionPelvisSample;
        private OfficeSeatingAnimationClip _transitionPelvisClip;
        private Vector2 _previousTransitionPelvisOffsetScreen;
        private float _previousTransitionCushionDistancePx;
        private float _maxTransitionPelvisStepPx;
        private int _transitionMonotonicViolationCount;

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
            Phase != OfficeRuntimeAgentPhase.LeavingSeat &&
            (Phase == OfficeRuntimeAgentPhase.SittingDown ||
             Phase == OfficeRuntimeAgentPhase.Working ||
             Phase == OfficeRuntimeAgentPhase.FinishingWork ||
             Phase == OfficeRuntimeAgentPhase.StandingUp);
        public bool IsBusy => HasAssignedTask || Phase != OfficeRuntimeAgentPhase.Idle;
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Break;
        public Vector2 Position => new Vector2(transform.position.x, transform.position.y);
        public float AgentRadius { get; private set; } = DefaultRadius;
        public OfficeRuntimeAgentPhase Phase { get; private set; }
        public Vector2 DesiredVelocity => _desiredVelocity;
        public float StuckSeconds => _stuckSeconds;
        public string ActiveSeatId => _seatClaim == null || _seatClaim.IsReleased ? string.Empty : _seatClaim.SeatId;
        public float ChairDeskErrorPx => _chairDeskErrorPx;

        /// <summary>
        /// Screen distance between the seat contact of the drawn sprite and the chair cushion
        /// anchor. The only seated placement number that can fail, computed from the live
        /// SpriteRenderer - never hardcoded.
        /// </summary>
        public float SeatContactErrorPx => _seatContactErrorPx;
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
        public int MotionDirection => _animator == null ? 0 : _animator.MotionDirection;
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
        public SpriteRenderer PresentationRenderer => _renderer;
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
        public float MaxAnimatedAnchorErrorPx => _maxAnimatedAnchorErrorPx;
        public float MaxTransitionPelvisStepPx => _maxTransitionPelvisStepPx;
        public int TransitionMonotonicViolationCount => _transitionMonotonicViolationCount;
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

        public bool AssignOfficeTask(string taskId, OfficeActivity activity, float workSeconds)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task ID is required.", nameof(taskId));
            if (workSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(workSeconds));
            if (HasAssignedTask || _playerControlled || _qaControl) return false;
            if (!_world.Workstations.TryResolveActivityDestination(
                    activity,
                    _agentId,
                    taskId,
                    out OfficeRuntimeDestination destination)) return false;
            _assignedTaskId = taskId.Trim();
            _assignedActivity = activity;
            _assignedWorkRemaining = workSeconds;
            if (BeginDestination(destination)) return true;
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _assignedActivity = OfficeActivity.Break;
            ReleaseSeatImmediately();
            Phase = OfficeRuntimeAgentPhase.Idle;
            return false;
        }

        public void CancelAssignedTask()
        {
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _assignedActivity = OfficeActivity.Break;
            ResumeAutonomy();
        }

        public void SetAutonomousDestination(
            string intentId,
            OfficeSemanticLocation location,
            string statusLabel)
        {
            if (_playerControlled || _qaControl) return;
            if (string.IsNullOrWhiteSpace(intentId))
            {
                ClearAutonomousDestination();
                return;
            }
            if (_autonomyIntentId == intentId) return;
            if (!_world.Workstations.TryResolveDestination(
                    location,
                    _agentId,
                    intentId,
                    out OfficeRuntimeDestination destination)) return;
            _autonomyIntentId = intentId.Trim();
            _autonomyStatus = string.IsNullOrWhiteSpace(statusLabel) ? "자율 행동" : statusLabel.Trim();
            _autonomyDestination = destination;
            if (!HasAssignedTask && !BeginDestination(destination))
            {
                _autonomyIntentId = string.Empty;
                _autonomyStatus = string.Empty;
                _autonomyDestination = null;
                ReleaseSeatImmediately();
                Phase = OfficeRuntimeAgentPhase.Idle;
            }
        }

        public void ClearAutonomousDestination()
        {
            _autonomyIntentId = string.Empty;
            _autonomyStatus = string.Empty;
            _autonomyDestination = null;
            if (!HasAssignedTask && !_playerControlled) RequestStopAndStand();
        }

        public void ResetRuntimeState()
        {
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            _autonomyIntentId = string.Empty;
            _autonomyStatus = string.Empty;
            _autonomyDestination = null;
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
            _maxAnimatedAnchorErrorPx = 0f;
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

            if (_playerControlled && _playerInput.sqrMagnitude > 0.0001f)
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
            _animator?.BeginTilePresentationFrame();
        }

        public void TickPresentation(float deltaTime)
        {
            if (_animator == null || deltaTime < 0f) return;
            // Office time can run at 2x/4x, but a human sit/stand gesture should keep its real
            // 0.62s/0.56s presentation duration instead of dropping into the chair in a few ticks.
            float presentationDeltaTime = _animator.IsOfficeSeatingPoseActive
                ? Mathf.Max(0f, Time.unscaledDeltaTime)
                : deltaTime;
            _animator.Tick(presentationDeltaTime);
            _animator.EndTilePresentationFrame();
            if (_seat != null && _renderer != null)
            {
                _world.Workstations.ApplyPresentationStack(_seat, _renderer, transform.position);
            }
        }

        private bool BeginDestination(OfficeRuntimeDestination destination)
        {
            if (Phase == OfficeRuntimeAgentPhase.Working ||
                Phase == OfficeRuntimeAgentPhase.SittingDown ||
                Phase == OfficeRuntimeAgentPhase.MovingToSit ||
                Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp)
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

        private bool RebuildPath()
        {
            if (!_destination.HasValue) return false;
            OfficeGridCoordinate start = _world.Presenter.NearestCell(transform.position);
            IReadOnlyList<OfficeGridCoordinate> result = _world.FindPath(
                _agentId,
                start,
                _destination.Value.Cell,
                _destination.Value.SeatId,
                _stuckSeconds >= OfficeNavigationTrafficRules.ReplanThresholdSeconds);
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
            var upcoming = new List<OfficeGridCoordinate>();
            for (var index = _pathIndex; index < _path.Count && upcoming.Count < 2; index++)
                upcoming.Add(_path[index]);
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
            if (presentationTargetIndex < _path.Count - 1 &&
                delta.magnitude <= CornerAnticipationDistance)
            {
                Vector3 next3 = _world.Presenter.CellCenterWorld(_path[presentationTargetIndex + 1]);
                Vector2 nextDelta = new Vector2(next3.x, next3.y) - target;
                if (nextDelta.sqrMagnitude > 0.000001f)
                {
                    float anticipation = 1f - Mathf.Clamp01(delta.magnitude / CornerAnticipationDistance);
                    Vector2 candidate = Vector2.Lerp(
                        desiredDirection,
                        nextDelta.normalized,
                        anticipation * 0.45f).normalized;
                    float probeDistance = Mathf.Min(0.12f, Mathf.Max(0.02f, delta.magnitude));
                    if (_world.Occupancy.CanTraverseStatic(
                            Position,
                            Position + candidate * probeDistance,
                            AgentRadius,
                            _destination.Value.SeatId) &&
                        _world.Occupancy.HasPresentationClearance(
                            _agentId,
                            Position,
                            Position + candidate * CornerAnticipationDistance,
                            AgentRadius)) presentationSemanticDirection = candidate;
                }
            }
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
            if (!_world.Occupancy.TryReservePath(_agentId, currentCell, upcoming))
            {
                _stuckSeconds += deltaTime;
                OfficeTrafficDecision blockedTraffic = _world.ResolveTraffic(
                    _agentId,
                    Position,
                    _desiredVelocity,
                    AgentRadius,
                    _stuckSeconds);
                if (_stuckSeconds >= OfficeNavigationTrafficRules.RecoveryThresholdSeconds &&
                    TryTickGridYield(currentCell, upcoming, deltaTime, _destination.Value.SeatId))
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
                var offsets = preferLeft
                    ? new[] { left, right, new OfficeGridCoordinate(-forward.X, -forward.Y) }
                    : new[] { right, left, new OfficeGridCoordinate(-forward.X, -forward.Y) };
                foreach (OfficeGridCoordinate offset in offsets)
                {
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
            _world.NotifyArrival();
            StopMotion();
            if (!_destination.HasValue) return;
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
            }
        }

        private void TickSeating(float deltaTime)
        {
            if (_seat == null || _seatClaim == null || _seatClaim.IsReleased)
            {
                ReleaseSeatImmediately();
                ResumeAutonomy();
                return;
            }
            switch (Phase)
            {
                case OfficeRuntimeAgentPhase.MovingToSit:
                {
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
                    _seatDirection = FacingDirection(_seat.Facing);
                    _sitTransitionInitialized = false;
                    _standTransitionInitialized = false;
                    _hasTransitionPelvisSample = false;
                    if (!_seatClaim.TryOccupy(out _) ||
                        !_animator.PrepareOfficeSeatingFacing(_seatDirection) ||
                        !_animator.BeginSitDown(_seatDirection))
                    {
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
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
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    Phase = OfficeRuntimeAgentPhase.Working;
                    _arrived = true;
                    break;
                case OfficeRuntimeAgentPhase.Working:
                    StopMotion();
                    TrackWorkstationMetrics();
                    if (HasAssignedTask && _assignedActivity == CurrentActivity)
                        AdvanceAssignedWork(deltaTime);
                    if (_releaseSeatRequested) BeginSafeStand();
                    break;
                case OfficeRuntimeAgentPhase.FinishingWork:
                    StopMotion();
                    if (!_animator.IsOfficeWorkSafeToStand) return;
                    if (!_animator.BeginStandUp())
                    {
                        ReleaseSeatImmediately();
                        ResumeAutonomy();
                        return;
                    }
                    _standTransitionInitialized = false;
                    _hasTransitionPelvisSample = false;
                    Phase = OfficeRuntimeAgentPhase.StandingUp;
                    break;
                case OfficeRuntimeAgentPhase.StandingUp:
                    StopMotion();
                    if (!_animator.IsOfficeSeatingTransitionComplete || _standPlacementProgress01 < 0.9999f)
                        return;
                    ResetVisualPose();
                    _world.Workstations.ClearOcclusion(_seat);
                    _animator.ResumeWalkingAfterSeating();
                    Phase = OfficeRuntimeAgentPhase.LeavingSeat;
                    break;
                case OfficeRuntimeAgentPhase.LeavingSeat:
                {
                    Vector3 target3 = _world.Workstations.SeatApproachWorld(_seat);
                    Vector2 target = new Vector2(target3.x, target3.y);
                    Vector2 delta = target - Position;
                    if (delta.magnitude > ArrivalDistance)
                    {
                        MoveWithCollision(
                            delta.normalized * 1.15f,
                            deltaTime,
                            _seat.SeatId,
                            delta.magnitude);
                        return;
                    }
                    transform.position = new Vector3(target.x, target.y, transform.position.z);
                    ReleaseSeatImmediately();
                    if (_pendingDestination.HasValue)
                    {
                        OfficeRuntimeDestination pending = _pendingDestination.Value;
                        _pendingDestination = null;
                        BeginDestination(pending);
                    }
                    else ResumeAutonomy();
                    break;
                }
            }
        }

        private void TickArrivedWork(float deltaTime)
        {
            if (!HasAssignedTask || !_arrived || _assignedActivity == OfficeActivity.Work) return;
            AdvanceAssignedWork(deltaTime);
        }

        private void AdvanceAssignedWork(float deltaTime)
        {
            _assignedWorkRemaining = Mathf.Max(0f, _assignedWorkRemaining - deltaTime);
            if (_assignedWorkRemaining > 0f) return;
            string completed = _assignedTaskId;
            _assignedTaskId = string.Empty;
            _assignedWorkRemaining = 0f;
            AssignedTaskCompleted?.Invoke(this, completed);
            ResumeAutonomy();
        }

        private void RequestStopAndStand()
        {
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
            ReleaseSeatImmediately();
            Phase = OfficeRuntimeAgentPhase.Idle;
            StopMotion();
        }

        private void BeginSafeStand()
        {
            if (Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                Phase == OfficeRuntimeAgentPhase.StandingUp) return;
            _releaseSeatRequested = true;
            _animator.RequestOfficeWorkSafeStop();
            Phase = OfficeRuntimeAgentPhase.FinishingWork;
        }

        private void ResumeAutonomy()
        {
            if (HasAssignedTask) return;
            if (_autonomyDestination.HasValue && !_playerControlled)
            {
                BeginDestination(_autonomyDestination.Value);
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
            float changePerSecond = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                7.5f,
                _playerControlled);
            OfficeMotionIntegrationResult motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                new OfficeNavPoint(_currentVelocity.x, _currentVelocity.y),
                new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
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
            Vector2 actual = intended;
            bool collisionProjected = false;
            if (!_world.Occupancy.CanMove(_agentId, before, before + actual, AgentRadius, permittedSeatId))
            {
                Vector2 xOnly = new Vector2(actual.x, 0f);
                Vector2 yOnly = new Vector2(0f, actual.y);
                bool canMoveX = Mathf.Abs(xOnly.x) > 0.00001f &&
                                _world.Occupancy.CanMove(
                                    _agentId,
                                    before,
                                    before + xOnly,
                                    AgentRadius,
                                    permittedSeatId);
                bool canMoveY = Mathf.Abs(yOnly.y) > 0.00001f &&
                                _world.Occupancy.CanMove(
                                    _agentId,
                                    before,
                                    before + yOnly,
                                    AgentRadius,
                                    permittedSeatId);
                OfficeNavPoint slide = OfficeCollisionSlideRules.SelectBestAxisSlide(
                    new OfficeNavPoint(actual.x, actual.y),
                    new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                    new OfficeNavPoint(_lastActualDisplacement.x, _lastActualDisplacement.y),
                    canMoveX,
                    canMoveY,
                    _agentId);
                actual = new Vector2(slide.X, slide.Z);
                collisionProjected = actual.sqrMagnitude > 0.0000001f;
            }
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
            }
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
            RecordObservedSeatingFrame(clip, frame);
            if (_animator.SeatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork)
            {
                ApplySeatedContactPlacement(profile);
            }
            else
            {
                ApplyAnimatedSeatingPlacement(clip, frame, profile);
            }
            _alignedClip = clip;
            _alignedFrame = frame;
        }

        private void ApplyAnimatedSeatingPlacement(
            OfficeSeatingAnimationClip clip,
            int frame,
            OfficeCharacterSeatPoseProfile profile)
        {
            Vector3 cushion = _world.Workstations.ChairSeatAnchorWorld(_seat);
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

        private void TrackWorkstationMetrics()
        {
            if (_seat == null || Camera.main == null) return;
            OfficeCharacterSeatPoseProfile profile = _poseCatalog.ResolveApproved(
                _agentId,
                _seatDirection,
                OfficeSeatingAnimationClip.Work,
                _alignedFrame < 0 ? 0 : _alignedFrame);
            _chairDeskErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                _world.Workstations.ChairSeatAnchorWorld(_seat),
                _world.Workstations.DeskSeatSocketWorld(_seat));
            _seatContactErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                Camera.main,
                OfficeSeatedOccupantContract.OccupantSeatContactWorld(_renderer, profile.PelvisAnchorPx),
                _world.Workstations.ChairSeatAnchorWorld(_seat));
        }

        private void ReleaseSeatImmediately()
        {
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
                _sitTransitionInitialized = false;
                _standTransitionInitialized = false;
                _sitPlacementProgress01 = 0f;
                _standPlacementProgress01 = 0f;
                ResetTransitionMotionMetrics();
                if (_animator != null && _animator.IsOfficeSeatingPoseActive)
                    _animator.ResumeWalkingAfterSeating();
                ResetVisualPose();
            }
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
            if (_world != null) _world.Occupancy.ClearReservations(_agentId);
            ReleaseSeatImmediately();
        }

        private void OnDestroy()
        {
            if (_animator != null)
            {
                _animator.OfficeFrameApplied -= HandleOfficeFrameApplied;
                _animator.SetExternallyTicked(false);
            }
            if (_world != null) _world.Occupancy.UnregisterActor(_agentId);
            ReleaseSeatImmediately();
        }
    }
}

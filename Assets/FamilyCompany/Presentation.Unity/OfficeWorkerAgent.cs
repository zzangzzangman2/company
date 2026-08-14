using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class OfficeWorkerAgent : MonoBehaviour
    {
        private const float SemanticArrivalDistance = 0.16f;
        private const float NavigationEscapeStepMeters = 0.06f;
        private static readonly float[] NavigationSlideProbeDegrees =
            { 0f, 30f, 55f, 80f, 105f, 130f, 155f, 180f };

        [SerializeField] private string agentId = "worker";
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float arrivalDistance = SemanticArrivalDistance;
        [SerializeField] private float acceleration = 6.5f;
        [SerializeField] private float deceleration = 8.5f;
        [SerializeField, Range(0.25f, 1f)] private float sharpCornerSpeedScale = 0.46f;
        [SerializeField] private int startingWaypointIndex;
        [SerializeField] private OfficeWaypoint[] route = Array.Empty<OfficeWaypoint>();
        [SerializeField] private DirectionalSpriteAnimator spriteAnimator;

        private CharacterController _controller;
        private OfficeNavigationWorld _navigationWorld;
        private int _nextWaypointIndex;
        private int _completedStops;
        private float _waitRemaining;
        private bool _initialized;
        private string _assignedTaskId = string.Empty;
        private OfficeWaypoint _assignedWaypoint;
        private float _assignedWorkRemaining;
        private bool _assignedWaypointReached;
        private int _completedAssignments;
        private string _autonomyIntentId = string.Empty;
        private string _autonomyStatusLabel = string.Empty;
        private OfficeWaypoint _autonomyWaypoint;
        private bool _autonomyWaypointReached;
        private Vector3[] _navigationPoints = Array.Empty<Vector3>();
        private int _navigationPointIndex;
        private int _navigationRevision = -1;
        private OfficeWaypoint _navigationDestination;
        private OfficeWaypoint _lastReachedWaypoint;
        private Vector3 _navigationTargetPosition;
        private bool _navigationTargetValid;
        private bool _pathUnavailable;
        private bool _navigationGoalProjected;
        private float _replanRemaining;
        private Vector3 _currentVelocity;
        private Vector3 _desiredVelocity;
        private float _stuckSeconds;
        private bool _isYielding;
        private Renderer[] _presentationRenderers = Array.Empty<Renderer>();
        private bool _presentationAway;
        private float _footstepRemaining;
        private int _footstepSequence;
        private bool _seatRuntimeEnabled;
        private OfficeSeatRuntimeClaim _seatClaim;
        private OfficeSeatAuthoring _seatAuthoring;
        private OfficeWaypoint _seatNavigationWaypoint;
        private string _seatIntentId = string.Empty;
        private string _seatStatusLabel = string.Empty;
        private bool _seatNavigationWaypointReached;
        private bool _seatReleaseRequested;
        private bool _seatedPhysics;
        private int _seatFacing;
        private OfficeSeatApproachRequest _seatApproachRequest;
        private Action<OfficeWorkerAgent, OfficeSeatApproachHandoff> _seatApproachReady;
        private Action<OfficeWorkerAgent, OfficeSeatApproachTermination> _seatApproachTerminated;
        private bool _seatHandoffReady;

        public event Action<OfficeWorkerAgent, string> AssignedTaskCompleted;
        public event Action<OfficeWorkerAgent, OfficeSeatApproachTermination> SeatApproachTerminated;

        public string AgentId => agentId;
        public int RouteCount => route?.Length ?? 0;
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Walking;
        public string CurrentActivityLabel => HasAssignedTask
            ? $"계약 · {ActivityLabel(CurrentActivity)}"
            : HasActiveSeatClaim
                ? CurrentActivity == OfficeActivity.Walking
                    ? $"{_seatStatusLabel} 좌석으로 이동 중"
                    : _seatStatusLabel
            : HasSeatApproach
                ? _seatHandoffReady ? "좌석 인계 대기" : "좌석으로 이동 중"
            : HasAutonomousDestination
                ? CurrentActivity == OfficeActivity.Walking
                    ? $"{_autonomyStatusLabel} 가는 중"
                    : _autonomyStatusLabel
                : ActivityLabel(CurrentActivity);
        public int CompletedStops => _completedStops;
        public int CompletedAssignments => _completedAssignments;
        public bool HasAssignedTask => _assignedWaypoint != null;
        public bool HasAutonomousDestination => _autonomyWaypoint != null;
        public bool HasSeatApproach => _seatApproachRequest != null;
        public bool IsSeatHandoffReady => _seatHandoffReady;
        public bool IsPresentationAway => _presentationAway;
        public string AssignedTaskId => _assignedTaskId;
        public DirectionalSpriteAnimator SpriteAnimator => spriteAnimator;
        public bool HasOfficeSeatingAnimation => spriteAnimator != null && spriteAnimator.HasOfficeSeatingFrames;
        public bool IsOfficeSeatingRuntimeEnabled => _seatRuntimeEnabled;
        public OfficeWorkerSeatingPhase SeatingPhase { get; private set; }
        public bool HasActiveSeatClaim => _seatClaim != null && !_seatClaim.IsReleased;
        public string ActiveSeatId => HasActiveSeatClaim ? _seatClaim.SeatId : string.Empty;
        public string ActiveSeatIntentId => _seatIntentId;
        public int NavigationRevision => _navigationRevision;
        public bool IsNavigationPathUnavailable => _pathUnavailable;
        public int NavigationPointCount => _navigationPoints?.Length ?? 0;
        public OfficeWaypoint TargetWaypoint =>
            HasAssignedTask
                ? _assignedWaypoint
                : HasActiveSeatClaim && !_seatReleaseRequested && _seatNavigationWaypoint != null
                    ? _seatNavigationWaypoint
                : HasAutonomousDestination
                    ? _autonomyWaypoint
                    : HasActiveSeatClaim && _seatNavigationWaypoint != null
                        ? _seatNavigationWaypoint
                    : route != null && route.Length > 0
                        ? route[Mathf.Clamp(_nextWaypointIndex, 0, route.Length - 1)]
                        : null;

        internal Vector3 NavigationDesiredVelocity => _desiredVelocity;
        internal float NavigationRadius => _controller == null ? 0.30f : Mathf.Max(0.05f, _controller.radius);
        internal float NavigationStuckSeconds => _stuckSeconds;
        internal bool NavigationCanMove => _controller != null && _controller.enabled && isActiveAndEnabled;

        public Vector2 ResolveVisualArtPixel()
        {
            var basePixel = OfficeVisualV2Calibration.WorldToArtPixel(transform.position);
            if (SeatingPhase != OfficeWorkerSeatingPhase.None) return basePixel;
            var anchor = _navigationDestination != null ? _navigationDestination : _lastReachedWaypoint;
            if (anchor == null || !anchor.HasArtAnchor) return basePixel;
            var delta = anchor.transform.position - transform.position;
            delta.y = 0f;
            var blend = 1f - Mathf.Clamp01(delta.magnitude / 0.75f);
            blend = blend * blend * (3f - 2f * blend);
            return Vector2.Lerp(basePixel, anchor.ArtAnchorPixel, blend);
        }

        public void Configure(
            string id,
            OfficeWaypoint[] newRoute,
            float speed,
            int startIndex,
            DirectionalSpriteAnimator animator = null)
        {
            agentId = string.IsNullOrWhiteSpace(id) ? "worker" : id;
            route = newRoute ?? Array.Empty<OfficeWaypoint>();
            moveSpeed = Mathf.Max(0.1f, speed);
            arrivalDistance = SemanticArrivalDistance;
            startingWaypointIndex = startIndex;
            spriteAnimator = animator;
            _initialized = false;
        }

        public void SetOfficeSeatingRuntimeEnabled(bool enabled)
        {
            if (_seatRuntimeEnabled == enabled) return;
            _seatRuntimeEnabled = enabled;
            if (!enabled) ClearSeatDestination();
        }

        public void SetAgentId(string id)
        {
            agentId = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Agent ID is required.", nameof(id))
                : id;
        }

        public bool AssignOfficeTask(string taskId, OfficeWaypoint waypoint, float workSeconds)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task ID is required.", nameof(taskId));
            if (waypoint == null) throw new ArgumentNullException(nameof(waypoint));
            if (workSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(workSeconds));
            if (!_initialized) InitializeNow();
            if (HasAssignedTask) return false;
            SetAwayPresentation(false);

            _assignedTaskId = taskId;
            _assignedWaypoint = waypoint;
            _assignedWorkRemaining = workSeconds;
            _assignedWaypointReached = false;
            if (_autonomyWaypoint != null) _autonomyWaypointReached = false;
            _waitRemaining = 0f;
            if (HasActiveSeatClaim)
            {
                if (waypoint.Activity != OfficeActivity.Work) ClearSeatDestination();
            }
            else if (!HasSeatApproach)
            {
                BeginNavigation(waypoint);
            }
            return true;
        }

        public void CancelAssignedTask()
        {
            _assignedTaskId = string.Empty;
            _assignedWaypoint = null;
            _assignedWorkRemaining = 0f;
            _assignedWaypointReached = false;
            if (!HasActiveSeatClaim && !HasSeatApproach && _autonomyWaypoint != null)
                BeginNavigation(_autonomyWaypoint);
        }

        public bool TryBeginSeatApproach(
            OfficeSeatApproachRequest request,
            Action<OfficeWorkerAgent, OfficeSeatApproachHandoff> onReady,
            Action<OfficeWorkerAgent, OfficeSeatApproachTermination> onTerminated)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (onReady == null) throw new ArgumentNullException(nameof(onReady));
            if (onTerminated == null) throw new ArgumentNullException(nameof(onTerminated));
            if (!_initialized) InitializeNow();
            if (HasActiveSeatClaim || HasSeatApproach) return false;
            SetAwayPresentation(false);
            _seatApproachRequest = request;
            _seatApproachReady = onReady;
            _seatApproachTerminated = onTerminated;
            _seatHandoffReady = false;
            spriteAnimator?.SetNavigationAnimationSuppressed(false);
            BeginNavigation(request.ApproachPosition, null);
            return true;
        }

        public void ReleaseSeatHandoff(bool resumeAutonomy = true)
        {
            TerminateSeatApproach(
                OfficeSeatApproachTerminationReason.ReleasedByOwner,
                resumeAutonomy);
        }

        public void TerminateSeatApproach(
            OfficeSeatApproachTerminationReason reason,
            bool resumeNavigation = true)
        {
            CancelSeatApproachInternal(resumeNavigation, reason, true);
        }

        public void SetAutonomousDestination(string intentId, OfficeWaypoint waypoint, string statusLabel)
        {
            if (string.IsNullOrWhiteSpace(intentId)) throw new ArgumentException("Intent ID is required.", nameof(intentId));
            if (waypoint == null) throw new ArgumentNullException(nameof(waypoint));
            if (!_initialized) InitializeNow();
            _autonomyStatusLabel = string.IsNullOrWhiteSpace(statusLabel) ? "자율 행동" : statusLabel;
            if (waypoint.Activity != OfficeActivity.Outside) SetAwayPresentation(false);
            if (_autonomyIntentId == intentId && _autonomyWaypoint == waypoint) return;
            _autonomyIntentId = intentId;
            _autonomyWaypoint = waypoint;
            _autonomyWaypointReached = false;
            if (!HasAssignedTask)
            {
                if (HasActiveSeatClaim && waypoint.Activity != OfficeActivity.Work)
                    ClearSeatDestination();
                else if (!HasActiveSeatClaim && !HasSeatApproach)
                    BeginNavigation(waypoint);
            }
        }

        public void ClearAutonomousDestination()
        {
            _autonomyIntentId = string.Empty;
            _autonomyStatusLabel = string.Empty;
            _autonomyWaypoint = null;
            _autonomyWaypointReached = false;
            ClearSeatDestination();
            if (!HasAssignedTask && !HasSeatApproach) ResetNavigation();
        }

        public bool SetSeatDestination(
            string intentId,
            OfficeSeatAuthoring seat,
            OfficeWaypoint navigationWaypoint,
            OfficeSeatRuntimeClaim claim,
            string statusLabel)
        {
            if (string.IsNullOrWhiteSpace(intentId))
                throw new ArgumentException("Seat intent ID is required.", nameof(intentId));
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (navigationWaypoint == null) throw new ArgumentNullException(nameof(navigationWaypoint));
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (!string.Equals(claim.MemberId, agentId, StringComparison.Ordinal))
                throw new ArgumentException("Seat claim member does not match this agent.", nameof(claim));
            if (!string.Equals(claim.SeatId, seat.SeatId, StringComparison.Ordinal))
                throw new ArgumentException("Seat claim does not match the authored seat.", nameof(claim));
            if (!_seatRuntimeEnabled || !HasOfficeSeatingAnimation || HasActiveSeatClaim ||
                !seat.IsRuntimeValid) return false;
            if (!_initialized) InitializeNow();

            SetAwayPresentation(false);
            _seatClaim = claim;
            _seatAuthoring = seat;
            _seatNavigationWaypoint = navigationWaypoint;
            _seatIntentId = intentId.Trim();
            _seatStatusLabel = string.IsNullOrWhiteSpace(statusLabel) ? "좌석 업무" : statusLabel.Trim();
            _seatNavigationWaypointReached = false;
            _seatReleaseRequested = false;
            SeatingPhase = OfficeWorkerSeatingPhase.MovingToApproach;
            BeginNavigation(navigationWaypoint);
            return true;
        }

        public bool UpdateActiveSeatIntent(string intentId, string statusLabel)
        {
            if (!HasActiveSeatClaim || string.IsNullOrWhiteSpace(intentId)) return false;
            _seatIntentId = intentId.Trim();
            _seatStatusLabel = string.IsNullOrWhiteSpace(statusLabel) ? "좌석 업무" : statusLabel.Trim();
            return true;
        }

        public void ClearSeatDestination()
        {
            if (!HasActiveSeatClaim) return;
            _seatReleaseRequested = true;
            switch (SeatingPhase)
            {
                case OfficeWorkerSeatingPhase.MovingToApproach:
                case OfficeWorkerSeatingPhase.MovingToSit:
                    ReleaseSeatImmediately();
                    ResumeMovementAfterSeatRelease();
                    break;
                case OfficeWorkerSeatingPhase.Working:
                    BeginStandingUp();
                    break;
            }
        }

        public void ResetOfficeSeatingRuntime()
        {
            ReleaseSeatImmediately();
        }

        public void InitializeNow()
        {
            if (_navigationWorld != null) _navigationWorld.Unregister(this);
            _controller = GetComponent<CharacterController>();
            _presentationRenderers = GetComponentsInChildren<Renderer>(true);
            ReleaseSeatImmediately();
            _navigationWorld = OfficeNavigationWorld.ResolveFor(this);
            _navigationWorld?.Register(this);
            SetAwayPresentation(false);
            _completedAssignments = 0;
            CancelSeatApproachInternal(
                false,
                OfficeSeatApproachTerminationReason.AgentReinitialized,
                true);
            ClearAutonomousDestination();
            CancelAssignedTask();
            _currentVelocity = Vector3.zero;
            _desiredVelocity = Vector3.zero;
            _stuckSeconds = 0f;
            _isYielding = false;
            if (route == null || route.Length == 0)
            {
                _initialized = true;
                CurrentActivity = OfficeActivity.Break;
                return;
            }

            var currentIndex = PositiveModulo(startingWaypointIndex, route.Length);
            var startsAtWaypoint = FlatDistance(transform.position, route[currentIndex].transform.position) <= 0.35f;
            _lastReachedWaypoint = startsAtWaypoint ? route[currentIndex] : null;
            ResetNavigation();
            _nextWaypointIndex = startsAtWaypoint ? (currentIndex + 1) % route.Length : currentIndex;
            CurrentActivity = startsAtWaypoint ? route[currentIndex].Activity : OfficeActivity.Walking;
            _waitRemaining = startsAtWaypoint ? ResolveStaySeconds(route[currentIndex]) : 0f;
            _completedStops = 0;
            _footstepRemaining = StableRandom.StableRandomInt(
                $"office-footstep-v1:{agentId}",
                260) / 1000f;
            _initialized = true;
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) InitializeNow();
            if (HasActiveSeatClaim && !HasValidSeatBinding())
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }
            if (deltaTime <= 0f) return;
            var stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(deltaTime);
            for (var stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                TickStep(OfficeNavigationMotionIntegrator.ResolveStepDelta(
                    deltaTime,
                    stepIndex,
                    stepCount));
            }
        }

        private void TickStep(float deltaTime)
        {
            _replanRemaining = Mathf.Max(0f, _replanRemaining - deltaTime);
            if (HasActiveSeatClaim)
            {
                TickSeat(deltaTime);
                return;
            }

            if (HasSeatApproach)
            {
                TickSeatApproach(deltaTime);
                return;
            }

            if (HasAssignedTask)
            {
                TickAssignedTask(deltaTime);
                return;
            }

            if (HasAutonomousDestination)
            {
                TickAutonomousDestination(deltaTime);
                return;
            }

            if (route == null || route.Length == 0)
            {
                StopMotion(deltaTime);
                return;
            }

            if (_waitRemaining > 0f)
            {
                _waitRemaining = Mathf.Max(0f, _waitRemaining - deltaTime);
                StopMotion(deltaTime);
                return;
            }

            var target = route[_nextWaypointIndex];
            if (MoveAlongNavigation(target, deltaTime))
            {
                CompleteNavigation(target);
                CurrentActivity = target.Activity;
                _completedStops++;
                _waitRemaining = ResolveStaySeconds(target);
                _nextWaypointIndex = (_nextWaypointIndex + 1) % route.Length;
                StopMotion(deltaTime);
            }
        }

        private void TickAssignedTask(float deltaTime)
        {
            if (!_assignedWaypointReached)
            {
                if (MoveAlongNavigation(_assignedWaypoint, deltaTime))
                {
                    CompleteNavigation(_assignedWaypoint);
                    _assignedWaypointReached = true;
                    CurrentActivity = _assignedWaypoint.Activity;
                    StopMotion(deltaTime);
                }

                return;
            }

            CurrentActivity = _assignedWaypoint.Activity;
            _assignedWorkRemaining = Mathf.Max(0f, _assignedWorkRemaining - deltaTime);
            StopMotion(deltaTime);
            if (_assignedWorkRemaining > 0f) return;

            var completedTaskId = _assignedTaskId;
            CancelAssignedTask();
            _completedAssignments++;
            AssignedTaskCompleted?.Invoke(this, completedTaskId);
        }

        private void TickSeat(float deltaTime)
        {
            if (!HasValidSeatBinding())
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }

            switch (SeatingPhase)
            {
                case OfficeWorkerSeatingPhase.MovingToApproach:
                    TickMovingToSeat(deltaTime);
                    break;
                case OfficeWorkerSeatingPhase.MovingToSit:
                    TickPrecisionMoveToSit(deltaTime);
                    break;
                case OfficeWorkerSeatingPhase.SittingDown:
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                    if (spriteAnimator != null && spriteAnimator.IsOfficeSeatingTransitionComplete)
                    {
                        if (_seatReleaseRequested)
                        {
                            BeginStandingUp();
                        }
                        else if (spriteAnimator.BeginSeatedWork())
                        {
                            SeatingPhase = OfficeWorkerSeatingPhase.Working;
                            CurrentActivity = OfficeActivity.Work;
                        }
                        else
                        {
                            ReleaseSeatImmediately();
                            ResumeMovementAfterSeatRelease();
                        }
                    }
                    break;
                case OfficeWorkerSeatingPhase.Working:
                    CurrentActivity = OfficeActivity.Work;
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                    TickSeatedAssignedWork(deltaTime);
                    if (_seatReleaseRequested && SeatingPhase == OfficeWorkerSeatingPhase.Working)
                        BeginStandingUp();
                    break;
                case OfficeWorkerSeatingPhase.FinishingWork:
                    TickFinishingSeatedWork();
                    break;
                case OfficeWorkerSeatingPhase.StandingUp:
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                    if (spriteAnimator == null || spriteAnimator.IsOfficeSeatingTransitionComplete)
                    {
                        ReleaseSeatImmediately();
                        ResumeMovementAfterSeatRelease();
                    }
                    break;
                default:
                    ReleaseSeatImmediately();
                    ResumeMovementAfterSeatRelease();
                    break;
            }
        }

        private void TickMovingToSeat(float deltaTime)
        {
            if (!_seatNavigationWaypointReached)
            {
                if (!MoveAlongNavigation(_seatNavigationWaypoint, deltaTime)) return;
                CompleteNavigation(_seatNavigationWaypoint);
                _seatNavigationWaypointReached = true;
            }

            var approach = _seatAuthoring.ApproachAnchor.position;
            approach.y = transform.position.y;
            if (!TickPrecisionMoveToApproach(approach, deltaTime)) return;
            if (!_seatAuthoring.TryResolveFacing(out var facing) ||
                spriteAnimator == null ||
                !spriteAnimator.PrepareOfficeSeatingFacing(
                    (int)facing,
                    _seatAuthoring.ForegroundOcclusionMode))
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }

            _seatFacing = (int)facing;
            SetSeatedPhysics(true);
            spriteAnimator.SetWorldVelocity(Vector3.zero);
            SeatingPhase = OfficeWorkerSeatingPhase.MovingToSit;
            CurrentActivity = OfficeActivity.Work;
        }

        private bool IsNavigationStepAllowed(Vector3 start, Vector3 end) =>
            _navigationWorld.IsMovementCollisionFree(start, end, NavigationRadius) &&
            _navigationWorld.IsAgentMovementCollisionFree(this, start, end, NavigationRadius);

        private bool TryResolveNavigationSlide(
            Vector3 before,
            Vector3 desiredDirection,
            Vector3 blocked,
            out Vector3 slide)
        {
            slide = Vector3.zero;
            var planar = new Vector3(blocked.x, 0f, blocked.z);
            var length = planar.magnitude;
            // The blocked frame zeroes the velocity, so the next frame's displacement
            // collapses to nothing.  Probe along the semantic heading instead, and never
            // below the escape step, or the agent can never test a way out at all.
            var forward = length > 0.0001f
                ? planar / length
                : new Vector3(desiredDirection.x, 0f, desiredDirection.z);
            if (forward.sqrMagnitude <= 0.000001f) return false;
            forward.Normalize();
            var step = Mathf.Max(length, NavigationEscapeStepMeters);
            for (var index = 0; index < NavigationSlideProbeDegrees.Length; index++)
            {
                var degrees = NavigationSlideProbeDegrees[index];
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var candidate =
                        Quaternion.AngleAxis(degrees * sign, Vector3.up) * forward * step;
                    if (!IsNavigationStepAllowed(before, before + candidate)) continue;
                    slide = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TickPrecisionMoveToApproach(Vector3 approach, float deltaTime)
        {
            var displacement = approach - transform.position;
            displacement.y = 0f;
            var step = OfficeSeatPrecisionMotion.Advance(
                transform.position.x,
                transform.position.z,
                approach.x,
                approach.z,
                OfficeSeatPrecisionMotion.ApproachSpeedMetersPerSecond,
                deltaTime);
            transform.position = new Vector3((float)step.X, transform.position.y, (float)step.Z);
            spriteAnimator?.SetWorldVelocity(step.Arrived ? Vector3.zero : displacement.normalized);
            return step.Arrived;
        }

        private void TickPrecisionMoveToSit(float deltaTime)
        {
            var sit = _seatAuthoring.SitAnchor.position;
            var step = OfficeSeatPrecisionMotion.Advance(
                transform.position.x,
                transform.position.z,
                sit.x,
                sit.z,
                OfficeSeatPrecisionMotion.SitSpeedMetersPerSecond,
                deltaTime);
            transform.position = new Vector3((float)step.X, transform.position.y, (float)step.Z);
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
            if (!step.Arrived) return;

            if (!_seatClaim.TryOccupy(out _) ||
                spriteAnimator == null ||
                !spriteAnimator.BeginSitDown(_seatFacing))
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }
            SeatingPhase = OfficeWorkerSeatingPhase.SittingDown;
            CurrentActivity = OfficeActivity.Work;
        }

        private void TickSeatedAssignedWork(float deltaTime)
        {
            if (!HasAssignedTask || _assignedWaypoint.Activity != OfficeActivity.Work) return;
            _assignedWaypointReached = true;
            _assignedWorkRemaining = Mathf.Max(0f, _assignedWorkRemaining - deltaTime);
            if (_assignedWorkRemaining > 0f) return;

            var completedTaskId = _assignedTaskId;
            CancelAssignedTask();
            _completedAssignments++;
            AssignedTaskCompleted?.Invoke(this, completedTaskId);
        }

        private void BeginStandingUp()
        {
            if (!HasActiveSeatClaim ||
                SeatingPhase == OfficeWorkerSeatingPhase.FinishingWork ||
                SeatingPhase == OfficeWorkerSeatingPhase.StandingUp)
            {
                return;
            }
            if (spriteAnimator == null)
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }
            spriteAnimator.RequestOfficeWorkSafeStop();
            SeatingPhase = OfficeWorkerSeatingPhase.FinishingWork;
        }

        private void TickFinishingSeatedWork()
        {
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
            if (spriteAnimator == null || !spriteAnimator.IsOfficeWorkSafeToStand) return;
            if (!spriteAnimator.BeginStandUp())
            {
                ReleaseSeatImmediately();
                ResumeMovementAfterSeatRelease();
                return;
            }
            SeatingPhase = OfficeWorkerSeatingPhase.StandingUp;
        }

        private bool HasValidSeatBinding()
        {
            return HasActiveSeatClaim &&
                   _seatAuthoring != null &&
                   _seatAuthoring.IsRuntimeValid &&
                   spriteAnimator != null &&
                   spriteAnimator.isActiveAndEnabled &&
                   spriteAnimator.HasOfficeSeatingFrames;
        }

        private void ReleaseSeatImmediately()
        {
            var claim = _seatClaim;
            _seatClaim = null;
            try
            {
                claim?.TryRelease(out _);
            }
            finally
            {
                _seatAuthoring = null;
                _seatNavigationWaypoint = null;
                _seatIntentId = string.Empty;
                _seatStatusLabel = string.Empty;
                _seatNavigationWaypointReached = false;
                _seatReleaseRequested = false;
                _seatFacing = 0;
                SeatingPhase = OfficeWorkerSeatingPhase.None;
                SetSeatedPhysics(false);
                if (spriteAnimator != null) spriteAnimator.ResumeWalkingAfterSeating();
                ResetNavigation();
            }
        }

        private void ResumeMovementAfterSeatRelease()
        {
            if (HasAssignedTask)
            {
                BeginNavigation(_assignedWaypoint);
                return;
            }
            if (HasAutonomousDestination)
            {
                BeginNavigation(_autonomyWaypoint);
            }
        }

        private void TickSeatApproach(float deltaTime)
        {
            if (_seatHandoffReady)
            {
                StopMotion(deltaTime);
                return;
            }

            if (!MoveAlongNavigation(_seatApproachRequest.ApproachPosition, null, deltaTime))
            {
                var settledUnavailable = _navigationWorld == null ||
                                         (!_navigationWorld.IsRebuildPending &&
                                          (!_navigationWorld.IsReady || _pathUnavailable));
                if (_navigationGoalProjected || settledUnavailable)
                {
                    TerminateSeatApproach(
                        OfficeSeatApproachTerminationReason.NavigationInvalidated,
                        true);
                }

                return;
            }
            ResetNavigation();
            _seatHandoffReady = true;
            StopMotion(deltaTime);
            spriteAnimator?.SetNavigationAnimationSuppressed(true);
            var request = _seatApproachRequest;
            _seatApproachReady?.Invoke(
                this,
                new OfficeSeatApproachHandoff(
                    request.RequestId,
                    request.SeatId,
                    transform.position,
                    request.SitPosition,
                    request.LookDirection,
                    request.Facing));
        }

        private void TickAutonomousDestination(float deltaTime)
        {
            if (!_autonomyWaypointReached)
            {
                if (MoveAlongNavigation(_autonomyWaypoint, deltaTime))
                {
                    CompleteNavigation(_autonomyWaypoint);
                    _autonomyWaypointReached = true;
                    CurrentActivity = _autonomyWaypoint.Activity;
                    if (CurrentActivity == OfficeActivity.Outside) SetAwayPresentation(true);
                    StopMotion(deltaTime);
                }

                return;
            }

            CurrentActivity = _autonomyWaypoint.Activity;
            if (CurrentActivity == OfficeActivity.Outside) SetAwayPresentation(true);
            StopMotion(deltaTime);
        }

        private void SetAwayPresentation(bool away)
        {
            if (_presentationAway == away && _presentationRenderers.Length > 0) return;
            _presentationAway = away;
            RefreshControllerEnabled();
            foreach (var item in _presentationRenderers)
            {
                if (item != null) item.enabled = !away;
            }
        }

        private void SetSeatedPhysics(bool seated)
        {
            _seatedPhysics = seated;
            RefreshControllerEnabled();
        }

        private void RefreshControllerEnabled()
        {
            if (_controller != null) _controller.enabled = !_presentationAway && !_seatedPhysics;
        }

        private void BeginNavigation(OfficeWaypoint destination)
        {
            if (destination == null)
            {
                ResetNavigation();
                return;
            }

            BeginNavigation(ResolveTargetPosition(destination), destination);
        }

        private void BeginNavigation(Vector3 targetPosition, OfficeWaypoint destination)
        {
            _navigationDestination = destination;
            _navigationTargetPosition = targetPosition;
            _navigationTargetValid = true;
            _navigationRevision = -1;
            _replanRemaining = 0f;
            RebuildNavigationPath();
        }

        private bool MoveAlongNavigation(OfficeWaypoint destination, float deltaTime)
        {
            return MoveAlongNavigation(ResolveTargetPosition(destination), destination, deltaTime);
        }

        private bool MoveAlongNavigation(Vector3 targetPosition, OfficeWaypoint destination, float deltaTime)
        {
            if (!_navigationTargetValid || _navigationDestination != destination ||
                FlatDistance(_navigationTargetPosition, targetPosition) > 0.08f)
            {
                BeginNavigation(targetPosition, destination);
            }

            if (_navigationWorld == null) _navigationWorld = OfficeNavigationWorld.ResolveFor(this);
            if (_navigationWorld == null || !_navigationWorld.IsReady || _navigationWorld.IsRebuildPending)
            {
                CurrentActivity = OfficeActivity.Walking;
                StopMotion(deltaTime);
                return false;
            }

            var worldRevision = _navigationWorld == null ? -1 : _navigationWorld.Revision;
            if ((_navigationRevision != worldRevision || _pathUnavailable) && _replanRemaining <= 0f)
            {
                RebuildNavigationPath();
            }

            if (_pathUnavailable || _navigationPoints == null || _navigationPoints.Length == 0)
            {
                CurrentActivity = OfficeActivity.Walking;
                StopMotion(deltaTime);
                return false;
            }

            return MoveAlongPath(deltaTime);
        }

        private void RebuildNavigationPath()
        {
            _navigationPoints = Array.Empty<Vector3>();
            _navigationPointIndex = 0;
            _pathUnavailable = true;
            _navigationGoalProjected = false;
            if (!_navigationTargetValid) return;
            if (_navigationWorld == null) _navigationWorld = OfficeNavigationWorld.ResolveFor(this);
            if (_navigationWorld == null ||
                !_navigationWorld.TryFindPath(
                    transform.position,
                    _navigationTargetPosition,
                    NavigationRadius,
                    out OfficeNavPath path))
            {
                _navigationRevision = _navigationWorld == null ? -1 : _navigationWorld.Revision;
                _replanRemaining = 0.24f;
                return;
            }

            if (!OfficeNavigationPathAcceptance.CanUseForSemanticDestination(path))
            {
                _navigationGoalProjected = path.GoalProjected;
                _navigationRevision = _navigationWorld.Revision;
                _replanRemaining = 0.24f;
                return;
            }

            var points = new Vector3[path.Waypoints.Count];
            for (var index = 0; index < points.Length; index++)
            {
                var point = path.Waypoints[index];
                points[index] = new Vector3(point.X, transform.position.y, point.Z);
            }

            _navigationPoints = points;
            _navigationPointIndex = 0;
            _navigationRevision = _navigationWorld.Revision;
            _pathUnavailable = false;
            _navigationGoalProjected = false;
            _replanRemaining = 0.12f;
            SkipReachedPoints();
        }

        private bool MoveAlongPath(float deltaTime)
        {
            SkipReachedPoints();
            if (_navigationPointIndex >= _navigationPoints.Length)
            {
                StopMotion(deltaTime);
                return true;
            }

            var target = _navigationPoints[_navigationPointIndex];
            var displacement = target - transform.position;
            displacement.y = 0f;
            var distance = displacement.magnitude;
            if (distance <= arrivalDistance)
            {
                _navigationPointIndex++;
                return MoveAlongPath(deltaTime);
            }

            CurrentActivity = OfficeActivity.Walking;
            var direction = displacement / distance;
            var remainingDistance = distance + RemainingPathDistance(_navigationPointIndex + 1);
            var brakingSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * Mathf.Max(0.1f, deceleration) * remainingDistance));
            var targetSpeed = Mathf.Min(moveSpeed, brakingSpeed);
            if (_navigationPointIndex + 1 < _navigationPoints.Length)
            {
                var nextDirection = _navigationPoints[_navigationPointIndex + 1] - target;
                nextDirection.y = 0f;
                if (nextDirection.sqrMagnitude > 0.0001f)
                {
                    nextDirection.Normalize();
                    var turnDot = Mathf.Clamp(Vector3.Dot(direction, nextDirection), -1f, 1f);
                    var turnScale = Mathf.Lerp(sharpCornerSpeedScale, 1f, (turnDot + 1f) * 0.5f);
                    targetSpeed *= turnScale;
                }
            }

            _desiredVelocity = direction * targetSpeed;
            var trafficVelocity = _desiredVelocity;
            var requestReplan = false;
            _isYielding = false;
            if (_navigationWorld != null)
            {
                trafficVelocity = _navigationWorld.ResolveTrafficVelocity(
                    this,
                    _desiredVelocity,
                    NavigationRadius,
                    _stuckSeconds,
                    out requestReplan,
                    out _isYielding);
            }

            var slowing = trafficVelocity.sqrMagnitude + 0.0001f < _currentVelocity.sqrMagnitude;
            var rate = slowing ? deceleration : acceleration;
            var motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                ToNavPoint(_currentVelocity),
                ToNavPoint(trafficVelocity),
                Mathf.Max(0.1f, rate),
                deltaTime);
            _currentVelocity = ToVector3(motion.Velocity);
            var before = transform.position;
            if (_controller == null || !_controller.enabled)
            {
                _currentVelocity = Vector3.zero;
                spriteAnimator?.SetWorldVelocity(Vector3.zero);
                return false;
            }

            var clampedDisplacement = OfficeNavigationMotionIntegrator.ClampDisplacement(
                motion.Displacement,
                distance);
            var movement = ToVector3(clampedDisplacement);
            if (!clampedDisplacement.Equals(motion.Displacement))
            {
                _currentVelocity = Vector3.zero;
            }

            var horizontalEnd = before + new Vector3(movement.x, 0f, movement.z);
            if (_navigationWorld != null && !IsNavigationStepAllowed(before, horizontalEnd))
            {
                // Freezing here strands the agent forever: a replan from a position the
                // pathfinder treats as unwalkable keeps producing the same blocked step.
                // Sliding along the blocking edge keeps it moving until the next replan
                // starts from somewhere routable again.
                if (!TryResolveNavigationSlide(before, direction, movement, out movement))
                {
                    _currentVelocity = Vector3.zero;
                    _desiredVelocity = Vector3.zero;
                    _navigationRevision = -1;
                    _replanRemaining = 0f;
                    _stuckSeconds += deltaTime;
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                    return false;
                }

                _currentVelocity = Vector3.zero;
                _navigationRevision = -1;
                _replanRemaining = 0f;
                _stuckSeconds += deltaTime;
            }

            movement.y = !_controller.isGrounded ? -2f * deltaTime : -0.15f * deltaTime;
            _controller.Move(movement);
            var actual = transform.position - before;
            actual.y = 0f;
            var intendedDistance = new Vector2(movement.x, movement.z).magnitude;
            if (_desiredVelocity.sqrMagnitude > 0.04f &&
                (_isYielding || actual.magnitude < intendedDistance * 0.20f))
            {
                _stuckSeconds += deltaTime;
            }
            else
            {
                _stuckSeconds = Mathf.Max(0f, _stuckSeconds - deltaTime * 2f);
            }

            if (requestReplan || _stuckSeconds >= 1.25f)
            {
                _navigationRevision = -1;
                _replanRemaining = 0f;
            }

            // Legacy worker presentation still shares DirectionalSpriteAnimator even though the
            // Starter Runtime disables this actor path. Feed the controller-observed displacement
            // rather than the pre-collision integrator velocity so it cannot regress to stale
            // facing if a legacy scene is loaded.
            spriteAnimator?.SetWorldVelocity(actual / Mathf.Max(0.000001f, deltaTime));
            TickFootsteps(deltaTime, actual.magnitude);
            if (spriteAnimator == null && _currentVelocity.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(
                    transform.forward,
                    _currentVelocity.normalized,
                    Mathf.Clamp01(deltaTime * 8f));
            }

            return false;
        }

        private void SkipReachedPoints()
        {
            while (_navigationPointIndex < _navigationPoints.Length &&
                   FlatDistance(transform.position, _navigationPoints[_navigationPointIndex]) <= arrivalDistance)
            {
                _navigationPointIndex++;
            }
        }

        private float RemainingPathDistance(int startIndex)
        {
            var distance = 0f;
            for (var index = startIndex; index < _navigationPoints.Length; index++)
                distance += FlatDistance(_navigationPoints[index - 1], _navigationPoints[index]);
            return distance;
        }

        private void CompleteNavigation(OfficeWaypoint destination)
        {
            _lastReachedWaypoint = destination;
            ResetNavigation();
        }

        private void ResetNavigation()
        {
            _navigationPoints = Array.Empty<Vector3>();
            _navigationPointIndex = 0;
            _navigationRevision = -1;
            _navigationDestination = null;
            _navigationTargetValid = false;
            _pathUnavailable = false;
            _navigationGoalProjected = false;
            _desiredVelocity = Vector3.zero;
            _currentVelocity = Vector3.zero;
            _stuckSeconds = 0f;
            _isYielding = false;
        }

        private void CancelSeatApproachInternal(
            bool resumeNavigation,
            OfficeSeatApproachTerminationReason reason,
            bool notifyOwner)
        {
            var request = _seatApproachRequest;
            var callback = _seatApproachTerminated;
            _seatApproachRequest = null;
            _seatApproachReady = null;
            _seatApproachTerminated = null;
            _seatHandoffReady = false;
            spriteAnimator?.SetNavigationAnimationSuppressed(false);
            if (request != null && notifyOwner)
            {
                var termination = new OfficeSeatApproachTermination(
                    request.RequestId,
                    request.SeatId,
                    reason);
                callback?.Invoke(this, termination);
                SeatApproachTerminated?.Invoke(this, termination);
            }

            if (!resumeNavigation) return;
            if (_assignedWaypoint != null) BeginNavigation(_assignedWaypoint);
            else if (_autonomyWaypoint != null) BeginNavigation(_autonomyWaypoint);
            else ResetNavigation();
        }

        private void StopMotion(float deltaTime)
        {
            _desiredVelocity = Vector3.zero;
            var motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                ToNavPoint(_currentVelocity),
                new OfficeNavPoint(0f, 0f),
                Mathf.Max(0.1f, deceleration),
                Mathf.Max(0f, deltaTime));
            _currentVelocity = ToVector3(motion.Velocity);
            if (_currentVelocity.sqrMagnitude < 0.0025f) _currentVelocity = Vector3.zero;
            spriteAnimator?.SetWorldVelocity(_currentVelocity);
            _isYielding = false;
            if (_currentVelocity == Vector3.zero) _stuckSeconds = 0f;
        }

        private void TickFootsteps(float deltaTime, float movedDistance)
        {
            _footstepRemaining -= deltaTime;
            if (_footstepRemaining > 0f || movedDistance <= 0.005f || !Application.isPlaying) return;
            var clipId = (_footstepSequence++ & 1) == 0 ? "footstep_1" : "footstep_2";
            GameAudioCoordinator.Instance.PlaySfx(clipId, 0.12f);
            _footstepRemaining = Mathf.Lerp(0.52f, 0.34f, Mathf.Clamp01(_currentVelocity.magnitude / moveSpeed));
        }

        private Vector3 ResolveTargetPosition(OfficeWaypoint target)
        {
            var position = target.transform.position;
            var familySlot = ResolveFamilySlot();
            if (target.Activity == OfficeActivity.Outside || target.IsMainCorridor)
            {
                position.z -= familySlot * 0.65f;
                return position;
            }

            if (target.Activity == OfficeActivity.Meeting)
            {
                position.x += familySlot * 0.65f;
            }
            else if (target.Activity == OfficeActivity.Break)
            {
                position.x -= familySlot * 0.65f;
            }

            return position;
        }

        private int ResolveFamilySlot()
        {
            switch (agentId)
            {
                case "older_sister": return 0;
                case "father": return 1;
                case "mother": return 2;
                default:
                    return StableRandom.StableRandomInt($"office-agent-slot-v1:{agentId}", 3);
            }
        }

        private void Awake()
        {
            InitializeNow();
        }

        private void OnEnable()
        {
            _navigationWorld?.Register(this);
        }

        private void OnDisable()
        {
            _navigationWorld?.Unregister(this);
            CancelSeatApproachInternal(
                false,
                OfficeSeatApproachTerminationReason.AgentDisabled,
                true);
            ResetNavigation();
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
            ReleaseSeatImmediately();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            ReleaseSeatImmediately();
        }

        private float ResolveStaySeconds(OfficeWaypoint waypoint)
        {
            var minimumMilliseconds = Mathf.RoundToInt(waypoint.MinimumStaySeconds * 1000f);
            var maximumMilliseconds = Mathf.RoundToInt(waypoint.MaximumStaySeconds * 1000f);
            var range = Mathf.Max(0, maximumMilliseconds - minimumMilliseconds);
            if (range == 0) return minimumMilliseconds / 1000f;
            var extra = StableRandom.StableRandomInt(
                $"office-v1:{agentId}:{_completedStops}:{waypoint.WaypointId}",
                range + 1);
            return (minimumMilliseconds + extra) / 1000f;
        }

        private static float FlatDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static OfficeNavPoint ToNavPoint(Vector3 value) => new OfficeNavPoint(value.x, value.z);

        private static Vector3 ToVector3(OfficeNavPoint value) => new Vector3(value.X, 0f, value.Z);

        private static int PositiveModulo(int value, int divisor)
        {
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static string ActivityLabel(OfficeActivity activity)
        {
            switch (activity)
            {
                case OfficeActivity.Reception: return "고객 응대";
                case OfficeActivity.Work: return "업무 중";
                case OfficeActivity.Printing: return "출력 중";
                case OfficeActivity.Meeting: return "회의 중";
                case OfficeActivity.Break: return "휴식 중";
                case OfficeActivity.Outside: return "외부 일정";
                default: return "이동 중";
            }
        }
    }
}

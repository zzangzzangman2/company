using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class OfficeWorkerAgent : MonoBehaviour
    {
        [SerializeField] private string agentId = "worker";
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float arrivalDistance = 0.08f;
        [SerializeField] private int startingWaypointIndex;
        [SerializeField] private OfficeWaypoint[] route = Array.Empty<OfficeWaypoint>();
        [SerializeField] private DirectionalSpriteAnimator spriteAnimator;
        private CharacterController _controller;
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
        private OfficeWaypoint[] _navigationPath = Array.Empty<OfficeWaypoint>();
        private int _navigationPathIndex;
        private OfficeWaypoint _navigationDestination;
        private OfficeWaypoint _lastReachedWaypoint;
        private Renderer[] _presentationRenderers = Array.Empty<Renderer>();
        private bool _presentationAway;
        private float _footstepRemaining;
        private int _footstepSequence;

        public event Action<OfficeWorkerAgent, string> AssignedTaskCompleted;

        public string AgentId => agentId;
        public int RouteCount => route?.Length ?? 0;
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Walking;
        public string CurrentActivityLabel => HasAssignedTask
            ? $"계약 · {ActivityLabel(CurrentActivity)}"
            : HasAutonomousDestination
                ? CurrentActivity == OfficeActivity.Walking
                    ? $"{_autonomyStatusLabel} 가는 중"
                    : _autonomyStatusLabel
                : ActivityLabel(CurrentActivity);
        public int CompletedStops => _completedStops;
        public int CompletedAssignments => _completedAssignments;
        public bool HasAssignedTask => _assignedWaypoint != null;
        public bool HasAutonomousDestination => _autonomyWaypoint != null;
        public bool IsPresentationAway => _presentationAway;
        public string AssignedTaskId => _assignedTaskId;
        public OfficeWaypoint TargetWaypoint =>
            HasAssignedTask
                ? _assignedWaypoint
                : HasAutonomousDestination
                    ? _autonomyWaypoint
                : route != null && route.Length > 0
                    ? route[Mathf.Clamp(_nextWaypointIndex, 0, route.Length - 1)]
                    : null;

        public Vector2 ResolveVisualArtPixel()
        {
            var basePixel = OfficeVisualV2Calibration.WorldToArtPixel(transform.position);
            var anchor = _navigationPath != null && _navigationPathIndex < _navigationPath.Length
                ? _navigationPath[_navigationPathIndex]
                : _lastReachedWaypoint;
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
            startingWaypointIndex = startIndex;
            spriteAnimator = animator;
            _initialized = false;
        }

        public void SetAgentId(string id)
        {
            agentId = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Agent ID is required.", nameof(id)) : id;
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
            BeginNavigation(waypoint);
            return true;
        }

        public void CancelAssignedTask()
        {
            _assignedTaskId = string.Empty;
            _assignedWaypoint = null;
            _assignedWorkRemaining = 0f;
            _assignedWaypointReached = false;
            if (_autonomyWaypoint != null) BeginNavigation(_autonomyWaypoint);
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
            if (!HasAssignedTask) BeginNavigation(waypoint);
        }

        public void ClearAutonomousDestination()
        {
            _autonomyIntentId = string.Empty;
            _autonomyStatusLabel = string.Empty;
            _autonomyWaypoint = null;
            _autonomyWaypointReached = false;
            _navigationPath = Array.Empty<OfficeWaypoint>();
            _navigationPathIndex = 0;
        }

        public void InitializeNow()
        {
            _controller = GetComponent<CharacterController>();
            _presentationRenderers = GetComponentsInChildren<Renderer>(true);
            SetAwayPresentation(false);
            _completedAssignments = 0;
            ClearAutonomousDestination();
            CancelAssignedTask();
            if (route == null || route.Length == 0)
            {
                _initialized = true;
                CurrentActivity = OfficeActivity.Break;
                return;
            }

            var currentIndex = PositiveModulo(startingWaypointIndex, route.Length);
            transform.position = route[currentIndex].transform.position;
            _lastReachedWaypoint = route[currentIndex];
            ResetNavigation();
            _nextWaypointIndex = (currentIndex + 1) % route.Length;
            CurrentActivity = route[currentIndex].Activity;
            _waitRemaining = ResolveStaySeconds(route[currentIndex]);
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
            if (deltaTime <= 0f) return;
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

            if (route == null || route.Length == 0) return;
            if (_waitRemaining > 0f)
            {
                _waitRemaining = Mathf.Max(0f, _waitRemaining - deltaTime);
                spriteAnimator?.SetWorldVelocity(Vector3.zero);
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
                spriteAnimator?.SetWorldVelocity(Vector3.zero);
                return;
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
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                }

                return;
            }

            CurrentActivity = _assignedWaypoint.Activity;
            _assignedWorkRemaining = Mathf.Max(0f, _assignedWorkRemaining - deltaTime);
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
            if (_assignedWorkRemaining > 0f) return;

            var completedTaskId = _assignedTaskId;
            CancelAssignedTask();
            _completedAssignments++;
            AssignedTaskCompleted?.Invoke(this, completedTaskId);
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
                    spriteAnimator?.SetWorldVelocity(Vector3.zero);
                }

                return;
            }

            CurrentActivity = _autonomyWaypoint.Activity;
            if (CurrentActivity == OfficeActivity.Outside) SetAwayPresentation(true);
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
        }

        private void SetAwayPresentation(bool away)
        {
            if (_controller != null) _controller.enabled = !away;
            if (_presentationAway == away && _presentationRenderers.Length > 0) return;
            _presentationAway = away;
            foreach (var item in _presentationRenderers)
            {
                if (item != null) item.enabled = !away;
            }
        }

        private void BeginNavigation(OfficeWaypoint destination)
        {
            _navigationPath = BuildNavigationPath(destination);
            _navigationPathIndex = 0;
            _navigationDestination = destination;
        }

        private bool MoveAlongNavigation(OfficeWaypoint destination, float deltaTime)
        {
            if (_navigationDestination != destination || _navigationPath == null || _navigationPath.Length == 0)
            {
                BeginNavigation(destination);
            }

            if (_navigationPathIndex >= _navigationPath.Length) return true;
            if (!MoveToward(_navigationPath[_navigationPathIndex], deltaTime)) return false;
            _navigationPathIndex++;
            return _navigationPathIndex >= _navigationPath.Length;
        }

        private void CompleteNavigation(OfficeWaypoint destination)
        {
            _lastReachedWaypoint = destination;
            ResetNavigation();
        }

        private void ResetNavigation()
        {
            _navigationPath = Array.Empty<OfficeWaypoint>();
            _navigationPathIndex = 0;
            _navigationDestination = null;
        }

        private OfficeWaypoint[] BuildNavigationPath(OfficeWaypoint destination)
        {
            if (destination == null) return Array.Empty<OfficeWaypoint>();
            var flatDistance = destination.transform.position - transform.position;
            flatDistance.y = 0f;
            if (flatDistance.magnitude <= arrivalDistance * 2f) return new[] { destination };

            var path = new List<OfficeWaypoint>();
            if (_lastReachedWaypoint != null &&
                _lastReachedWaypoint.ApproachPath.Length > 0 &&
                FlatDistance(transform.position, _lastReachedWaypoint.transform.position) <= 0.35f)
            {
                for (var index = _lastReachedWaypoint.ApproachPath.Length - 1; index >= 0; index--)
                    AppendDistinct(path, _lastReachedWaypoint.ApproachPath[index]);
            }

            var corridors = FindObjectsByType<OfficeWaypoint>(FindObjectsSortMode.None)
                .Where(item => item != null && item.IsMainCorridor)
                .Distinct()
                .OrderBy(item => item.transform.position.x)
                .ToArray();
            if (corridors.Length == 0 || destination.Activity == OfficeActivity.Walking)
            {
                AppendDistinct(path, destination);
                return path.ToArray();
            }

            var startPosition = path.Count > 0 ? path[path.Count - 1].transform.position : transform.position;
            var destinationEntry = destination.ApproachPath.Length > 0
                ? destination.ApproachPath[0].transform.position
                : destination.transform.position;
            var startIndex = ClosestWaypointIndex(corridors, startPosition);
            var endIndex = ClosestWaypointIndex(corridors, destinationEntry);
            var direction = startIndex <= endIndex ? 1 : -1;
            for (var index = startIndex;; index += direction)
            {
                AppendDistinct(path, corridors[index]);
                if (index == endIndex) break;
            }

            foreach (var approach in destination.ApproachPath) AppendDistinct(path, approach);
            AppendDistinct(path, destination);
            return path.ToArray();
        }

        private static float FlatDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static void AppendDistinct(List<OfficeWaypoint> path, OfficeWaypoint waypoint)
        {
            if (waypoint == null || (path.Count > 0 && path[path.Count - 1] == waypoint)) return;
            path.Add(waypoint);
        }

        private static int ClosestWaypointIndex(OfficeWaypoint[] candidates, Vector3 position)
        {
            var bestIndex = 0;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < candidates.Length; index++)
            {
                var distance = (candidates[index].transform.position - position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = index;
            }

            return bestIndex;
        }

        private bool MoveToward(OfficeWaypoint target, float deltaTime)
        {
            var targetPosition = ResolveTargetPosition(target);
            var displacement = targetPosition - transform.position;
            displacement.y = 0f;
            if (displacement.magnitude <= arrivalDistance)
            {
                transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
                return true;
            }

            CurrentActivity = OfficeActivity.Walking;
            var velocity = displacement.normalized * moveSpeed;
            var movement = velocity * deltaTime;
            movement.y = _controller != null && !_controller.isGrounded ? -2f * deltaTime : -0.15f * deltaTime;
            _controller.Move(movement);
            spriteAnimator?.SetWorldVelocity(velocity);
            _footstepRemaining -= deltaTime;
            if (_footstepRemaining <= 0f && Application.isPlaying)
            {
                var clipId = (_footstepSequence++ & 1) == 0 ? "footstep_1" : "footstep_2";
                GameAudioCoordinator.Instance.PlaySfx(clipId, 0.12f);
                _footstepRemaining = 0.42f;
            }
            if (spriteAnimator == null && velocity.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, velocity.normalized, Mathf.Clamp01(deltaTime * 10f));
            }

            return false;
        }

        private Vector3 ResolveTargetPosition(OfficeWaypoint target)
        {
            var position = target.transform.position;
            var familySlot = ResolveFamilySlot();
            if (target.Activity == OfficeActivity.Outside || target.IsMainCorridor)
            {
                // The office's safe east-west corridor is clear on its south side. Keeping the
                // three family NPCs in deterministic parallel lanes prevents head-on controller
                // deadlocks, including simultaneous departures through the single semantic exit.
                position.z -= familySlot * 0.65f;
                return position;
            }

            // Desks, printer and reception are exclusive stations and must preserve their exact
            // calibrated art foot point. The two genuinely shared destinations fan out only on
            // their measured safe side so concurrent family members cannot controller-deadlock.
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

        private void Update()
        {
            Tick(Time.deltaTime);
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

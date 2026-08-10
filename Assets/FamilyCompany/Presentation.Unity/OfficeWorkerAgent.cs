using System;
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

        public string AgentId => agentId;
        public int RouteCount => route?.Length ?? 0;
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Walking;
        public string CurrentActivityLabel => ActivityLabel(CurrentActivity);
        public int CompletedStops => _completedStops;
        public OfficeWaypoint TargetWaypoint =>
            route != null && route.Length > 0 ? route[Mathf.Clamp(_nextWaypointIndex, 0, route.Length - 1)] : null;

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

        public void InitializeNow()
        {
            _controller = GetComponent<CharacterController>();
            if (route == null || route.Length == 0)
            {
                _initialized = true;
                CurrentActivity = OfficeActivity.Break;
                return;
            }

            var currentIndex = PositiveModulo(startingWaypointIndex, route.Length);
            transform.position = route[currentIndex].transform.position;
            _nextWaypointIndex = (currentIndex + 1) % route.Length;
            CurrentActivity = route[currentIndex].Activity;
            _waitRemaining = ResolveStaySeconds(route[currentIndex]);
            _completedStops = 0;
            _initialized = true;
            spriteAnimator?.SetWorldVelocity(Vector3.zero);
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) InitializeNow();
            if (route == null || route.Length == 0 || deltaTime <= 0f) return;
            if (_waitRemaining > 0f)
            {
                _waitRemaining = Mathf.Max(0f, _waitRemaining - deltaTime);
                spriteAnimator?.SetWorldVelocity(Vector3.zero);
                return;
            }

            var target = route[_nextWaypointIndex];
            var displacement = target.transform.position - transform.position;
            displacement.y = 0f;
            if (displacement.magnitude <= arrivalDistance)
            {
                transform.position = new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z);
                CurrentActivity = target.Activity;
                _completedStops++;
                _waitRemaining = ResolveStaySeconds(target);
                _nextWaypointIndex = (_nextWaypointIndex + 1) % route.Length;
                spriteAnimator?.SetWorldVelocity(Vector3.zero);
                return;
            }

            CurrentActivity = OfficeActivity.Walking;
            var velocity = displacement.normalized * moveSpeed;
            var movement = velocity * deltaTime;
            movement.y = _controller != null && !_controller.isGrounded ? -2f * deltaTime : -0.15f * deltaTime;
            _controller.Move(movement);
            spriteAnimator?.SetWorldVelocity(velocity);
            if (spriteAnimator == null && velocity.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, velocity.normalized, Mathf.Clamp01(deltaTime * 10f));
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
                default: return "이동 중";
            }
        }
    }
}

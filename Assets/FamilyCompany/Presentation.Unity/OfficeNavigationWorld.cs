using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class OfficeNavigationWorld : MonoBehaviour
    {
        private const string RuntimeObjectName = "Office Dynamic Navigation";
        private const float PollIntervalSeconds = 0.10f;

        [SerializeField, Range(OfficeNavigationLimits.MinimumCellSize, OfficeNavigationLimits.MaximumCellSize)]
        private float cellSize = 0.25f;
        [SerializeField, Min(0f)] private float characterClearance = 0.04f;
        [SerializeField, Min(0.02f)] private float rebuildDebounceSeconds = 0.14f;
        [SerializeField, Min(0f)] private float maximumStepHeight = 0.18f;
        [SerializeField, Min(0.5f)] private float navigationBodyHeight = 1.75f;

        private readonly List<OfficeNavObstacle> _obstacles = new List<OfficeNavObstacle>();
        private readonly Dictionary<int, PathfinderCacheEntry> _pathfinders =
            new Dictionary<int, PathfinderCacheEntry>();
        private readonly HashSet<OfficeWorkerAgent> _agents = new HashSet<OfficeWorkerAgent>();
        private Transform _officeRoot;
        private Collider _surfaceCollider;
        private Renderer _surfaceRenderer;
        private OfficeNavBounds _navigationBounds;
        private ulong _fingerprint;
        private float _pollRemaining;
        private float _dirtyRemaining;
        private int _observedExternalMutation;
        private bool _dirty;
        private bool _ready;
        private static int s_externalMutation;

        public int Revision { get; private set; }
        public int ObstacleCount => _obstacles.Count;
        public bool IsReady => _ready;
        public OfficeNavBounds NavigationBounds => _navigationBounds;
        public ulong ObstacleFingerprint => _fingerprint;

        public static void NotifyObstacleMutation()
        {
            unchecked
            {
                s_externalMutation++;
            }
        }

        public static OfficeNavigationWorld ResolveFor(OfficeWorkerAgent agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            var existing = FindObjectsByType<OfficeNavigationWorld>(FindObjectsSortMode.None)
                .Where(item => item != null && item.enabled)
                .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal)
                .FirstOrDefault(item => item.Contains(agent.transform.position));
            if (existing != null)
            {
                existing.Register(agent);
                return existing;
            }

            var officeRoot = FindOfficeRoot();
            if (officeRoot == null) return null;
            var runtimeObject = new GameObject(RuntimeObjectName);
            runtimeObject.transform.SetParent(officeRoot, false);
            var created = runtimeObject.AddComponent<OfficeNavigationWorld>();
            created.ConfigureRuntime(officeRoot);
            created.Register(agent);
            return created;
        }

        public void ConfigureRuntime(Transform root)
        {
            _officeRoot = root != null ? root : throw new ArgumentNullException(nameof(root));
            ResolveSurface();
            RebuildImmediately();
        }

        public void Register(OfficeWorkerAgent agent)
        {
            if (agent != null) _agents.Add(agent);
        }

        public void Unregister(OfficeWorkerAgent agent)
        {
            if (agent != null) _agents.Remove(agent);
        }

        public bool TryFindPath(Vector3 start, Vector3 goal, float agentRadius, out OfficeNavPath path)
        {
            path = null;
            EnsureConfigured();
            if (!_ready) return false;
            return GetPathfinder(agentRadius).TryFindPath(
                new OfficeNavPoint(start.x, start.z),
                new OfficeNavPoint(goal.x, goal.z),
                out path);
        }

        public bool IsPathCollisionFree(OfficeNavPath path, float agentRadius)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            EnsureConfigured();
            if (!_ready || path.Waypoints.Count == 0) return false;
            var pathfinder = GetPathfinder(agentRadius);
            for (var index = 0; index < path.Waypoints.Count; index++)
            {
                if (!pathfinder.IsPointWalkable(path.Waypoints[index])) return false;
                if (index > 0 &&
                    !pathfinder.IsSegmentWalkable(path.Waypoints[index - 1], path.Waypoints[index]))
                    return false;
            }

            return true;
        }

        public Vector3 ResolveTrafficVelocity(
            OfficeWorkerAgent self,
            Vector3 desiredVelocity,
            float radius,
            float stuckSeconds,
            out bool shouldReplan,
            out bool isYielding)
        {
            var peers = _agents
                .Where(item => item != null && item != self && item.NavigationCanMove && !item.IsPresentationAway)
                .OrderBy(item => item.AgentId, StringComparer.Ordinal)
                .Select(item => new OfficeTrafficAgentState(
                    item.AgentId,
                    ToPoint(item.transform.position),
                    ToPoint(item.NavigationDesiredVelocity),
                    item.NavigationRadius,
                    item.NavigationStuckSeconds))
                .ToArray();
            var selfState = new OfficeTrafficAgentState(
                self.AgentId,
                ToPoint(self.transform.position),
                ToPoint(desiredVelocity),
                Mathf.Max(0.05f, radius),
                stuckSeconds);
            var decision = OfficeNavigationTrafficRules.Resolve(selfState, peers);
            shouldReplan = decision.ShouldReplan;
            isYielding = decision.IsYielding;
            var speed = new Vector2(desiredVelocity.x, desiredVelocity.z).magnitude;
            var resolved = desiredVelocity * decision.ForwardScale;
            if (decision.RecoveryWeight > 0f)
            {
                resolved += new Vector3(
                    decision.RecoveryDirection.X,
                    0f,
                    decision.RecoveryDirection.Z) * (speed * decision.RecoveryWeight);
            }

            return Vector3.ClampMagnitude(resolved, speed);
        }

        public void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            _dirtyRemaining = Mathf.Max(0.02f, rebuildDebounceSeconds);
        }

        public void RebuildImmediately()
        {
            EnsureConfigured(false);
            if (_officeRoot == null || (_surfaceCollider == null && _surfaceRenderer == null))
            {
                _ready = false;
                return;
            }

            var collected = CollectObstacles(out var fingerprint);
            _obstacles.Clear();
            _obstacles.AddRange(collected);
            _fingerprint = fingerprint;
            _pathfinders.Clear();
            Revision++;
            _dirty = false;
            _dirtyRemaining = 0f;
            _observedExternalMutation = s_externalMutation;
            _ready = true;
        }

        private void Awake()
        {
            EnsureConfigured();
        }

        private void Update()
        {
            if (!_ready) EnsureConfigured();
            if (!_ready) return;
            if (_observedExternalMutation != s_externalMutation)
            {
                _observedExternalMutation = s_externalMutation;
                MarkDirty();
            }

            _pollRemaining -= Time.unscaledDeltaTime;
            if (_pollRemaining <= 0f)
            {
                _pollRemaining = PollIntervalSeconds;
                var current = ComputeFingerprintOnly();
                if (current != _fingerprint) MarkDirty();
            }

            if (!_dirty) return;
            _dirtyRemaining -= Time.unscaledDeltaTime;
            if (_dirtyRemaining <= 0f) RebuildImmediately();
        }

        private void OnDestroy()
        {
            _agents.Clear();
            _pathfinders.Clear();
        }

        private void EnsureConfigured(bool rebuild = true)
        {
            if (_officeRoot == null) _officeRoot = FindOfficeRoot();
            if (_officeRoot == null) return;
            if (_surfaceCollider == null && _surfaceRenderer == null) ResolveSurface();
            if (rebuild && !_ready && (_surfaceCollider != null || _surfaceRenderer != null))
                RebuildImmediately();
        }

        private bool Contains(Vector3 position)
        {
            if (!_ready) EnsureConfigured();
            return _ready &&
                   position.x >= _navigationBounds.MinX && position.x <= _navigationBounds.MaxX &&
                   position.z >= _navigationBounds.MinZ && position.z <= _navigationBounds.MaxZ;
        }

        private void ResolveSurface()
        {
            _surfaceCollider = _officeRoot.GetComponentsInChildren<Collider>(true)
                .Where(item => item != null && item.gameObject.name.IndexOf("Office Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal)
                .FirstOrDefault();
            if (_surfaceCollider != null)
            {
                SetBounds(_surfaceCollider.bounds);
                return;
            }

            _surfaceRenderer = _officeRoot.GetComponentsInChildren<Renderer>(true)
                .Where(item => item != null && item.gameObject.name.IndexOf("Office Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal)
                .FirstOrDefault();
            if (_surfaceRenderer != null) SetBounds(_surfaceRenderer.bounds);
        }

        private void SetBounds(Bounds bounds)
        {
            _navigationBounds = new OfficeNavBounds(
                bounds.min.x,
                bounds.min.z,
                bounds.max.x,
                bounds.max.z);
        }

        private List<OfficeNavObstacle> CollectObstacles(out ulong fingerprint)
        {
            var result = new List<OfficeNavObstacle>();
            var contributed = new HashSet<int>();
            var surfaceTop = _surfaceCollider != null ? _surfaceCollider.bounds.max.y : _surfaceRenderer.bounds.max.y;
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None)
                         .Where(item => item != null)
                         .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal))
            {
                if (collider == _surfaceCollider || collider is CharacterController ||
                    !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                    continue;
                var marker = collider.GetComponentInParent<OfficeNavigationObstacle>();
                if (marker != null && marker.PassableDecoration) continue;
                if (!OverlapsNavigationHeight(collider.bounds, surfaceTop)) continue;
                AddObstacle(result, contributed, collider.GetInstanceID(),
                    HierarchyKey(collider.transform) + ":collider", collider.bounds,
                    marker == null ? 0f : marker.ExtraClearance);
            }

            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                         .Where(item => item != null)
                         .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal))
            {
                if (renderer == _surfaceRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (renderer.GetComponentInParent<OfficeWorkerAgent>() != null ||
                    renderer.GetComponentInParent<PrototypePlayerController>() != null)
                    continue;
                var marker = renderer.GetComponentInParent<OfficeNavigationObstacle>();
                if (marker != null && marker.PassableDecoration) continue;
                if (renderer.GetComponent<Collider>() != null) continue;
                if (marker != null && !marker.UseRendererFootprintWhenColliderMissing) continue;
                if (!OverlapsNavigationHeight(renderer.bounds, surfaceTop)) continue;
                AddObstacle(result, contributed, renderer.GetInstanceID(),
                    HierarchyKey(renderer.transform) + ":renderer", renderer.bounds,
                    marker == null ? 0f : marker.ExtraClearance);
            }

            result.Sort((left, right) => string.CompareOrdinal(left.ObstacleId, right.ObstacleId));
            if (result.Count > OfficeNavigationLimits.MaxObstacles)
                throw new InvalidOperationException(
                    $"Office navigation collected {result.Count} obstacles; cap is {OfficeNavigationLimits.MaxObstacles}.");
            fingerprint = Fingerprint(result);
            return result;
        }

        private ulong ComputeFingerprintOnly()
        {
            CollectObstacles(out var fingerprint);
            return fingerprint;
        }

        private void AddObstacle(
            ICollection<OfficeNavObstacle> target,
            ISet<int> contributed,
            int contributorId,
            string obstacleId,
            Bounds bounds,
            float extraClearance)
        {
            if (!contributed.Add(contributorId)) return;
            var minX = bounds.min.x - extraClearance;
            var minZ = bounds.min.z - extraClearance;
            var maxX = bounds.max.x + extraClearance;
            var maxZ = bounds.max.z + extraClearance;
            if (maxX < _navigationBounds.MinX || minX > _navigationBounds.MaxX ||
                maxZ < _navigationBounds.MinZ || minZ > _navigationBounds.MaxZ)
                return;
            target.Add(new OfficeNavObstacle(obstacleId, minX, minZ, maxX, maxZ));
        }

        private DeterministicOfficePathfinder GetPathfinder(float agentRadius)
        {
            var radiusKey = Mathf.RoundToInt(Mathf.Max(0.05f, agentRadius) * 1000f);
            if (_pathfinders.TryGetValue(radiusKey, out var cache) && cache.Revision == Revision)
                return cache.Pathfinder;
            cache = new PathfinderCacheEntry(
                Revision,
                new DeterministicOfficePathfinder(
                    _navigationBounds,
                    cellSize,
                    _obstacles,
                    radiusKey / 1000f,
                    characterClearance));
            _pathfinders[radiusKey] = cache;
            return cache.Pathfinder;
        }

        private bool OverlapsNavigationHeight(Bounds bounds, float surfaceTop) =>
            bounds.max.y > surfaceTop + maximumStepHeight &&
            bounds.min.y < surfaceTop + navigationBodyHeight;

        private static Transform FindOfficeRoot()
        {
            var exact = GameObject.Find("FAMILY OFFICE V0.2");
            if (exact != null) return exact.transform;
            var floor = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(item => item != null && item.gameObject.name.IndexOf("Office Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => HierarchyKey(item.transform), StringComparer.Ordinal)
                .FirstOrDefault();
            return floor == null ? null : floor.transform.parent;
        }

        private static string HierarchyKey(Transform value)
        {
            if (value == null) return string.Empty;
            var segments = new Stack<string>();
            var cursor = value;
            while (cursor != null)
            {
                segments.Push($"{cursor.GetSiblingIndex():D4}:{cursor.name}");
                cursor = cursor.parent;
            }

            return string.Join("/", segments.ToArray());
        }

        private static OfficeNavPoint ToPoint(Vector3 value) => new OfficeNavPoint(value.x, value.z);

        private static ulong Fingerprint(IReadOnlyList<OfficeNavObstacle> obstacles)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (var index = 0; index < obstacles.Count; index++)
                {
                    var item = obstacles[index];
                    HashString(ref hash, item.ObstacleId);
                    HashInt(ref hash, Mathf.RoundToInt(item.MinX * 1000f));
                    HashInt(ref hash, Mathf.RoundToInt(item.MinZ * 1000f));
                    HashInt(ref hash, Mathf.RoundToInt(item.MaxX * 1000f));
                    HashInt(ref hash, Mathf.RoundToInt(item.MaxZ * 1000f));
                }

                return hash;
            }
        }

        private static void HashString(ref ulong hash, string value)
        {
            unchecked
            {
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 1099511628211UL;
                }
            }
        }

        private static void HashInt(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        private sealed class PathfinderCacheEntry
        {
            public PathfinderCacheEntry(int revision, DeterministicOfficePathfinder pathfinder)
            {
                Revision = revision;
                Pathfinder = pathfinder;
            }

            public int Revision { get; }
            public DeterministicOfficePathfinder Pathfinder { get; }
        }
    }
}

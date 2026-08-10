using System;
using System.Collections.Generic;
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
        private const float TrafficSafetyProbeSeconds = 0.28f;
        private const int MaxObstacleCandidates = OfficeNavigationLimits.MaxObstacles * 8;

        [SerializeField, Range(OfficeNavigationLimits.MinimumCellSize, OfficeNavigationLimits.MaximumCellSize)]
        private float cellSize = 0.25f;
        [SerializeField, Min(0f)] private float characterClearance = 0.04f;
        [SerializeField, Min(0.02f)] private float rebuildDebounceSeconds = 0.14f;
        [SerializeField, Min(0f)] private float maximumStepHeight = 0.18f;
        [SerializeField, Min(0.5f)] private float navigationBodyHeight = 1.75f;

        private readonly List<OfficeNavObstacle> _obstacles = new List<OfficeNavObstacle>();
        private readonly List<OfficeNavObstacle> _scratchObstacles = new List<OfficeNavObstacle>();
        private readonly List<ObstacleCandidate> _colliderCandidates = new List<ObstacleCandidate>();
        private readonly List<ObstacleCandidate> _rendererCandidates = new List<ObstacleCandidate>();
        private readonly List<Collider> _colliderScan = new List<Collider>();
        private readonly List<MeshRenderer> _rendererScan = new List<MeshRenderer>();
        private readonly List<ObstacleCandidate> _liveObstacles = new List<ObstacleCandidate>();
        private readonly List<ObstacleCandidate> _scratchLiveObstacles = new List<ObstacleCandidate>();
        private readonly Dictionary<int, PathfinderCacheEntry> _pathfinders =
            new Dictionary<int, PathfinderCacheEntry>();
        private readonly HashSet<OfficeWorkerAgent> _agents = new HashSet<OfficeWorkerAgent>();
        private readonly List<OfficeWorkerAgent> _agentSnapshotScratch = new List<OfficeWorkerAgent>();
        private OfficeTrafficAgentState[] _trafficSnapshot = Array.Empty<OfficeTrafficAgentState>();
        private int _trafficSnapshotCount;
        private Transform _officeRoot;
        private Collider _surfaceCollider;
        private Renderer _surfaceRenderer;
        private OfficeNavBounds _navigationBounds;
        private ulong _fingerprint;
        private float _pollRemaining;
        private float _dirtyRemaining;
        private int _observedExternalMutation;
        private bool _candidatesDirty = true;
        private bool _dirty;
        private bool _ready;
        private string _failureReason = string.Empty;
        private static int s_externalMutation;

        public int Revision { get; private set; }
        public int ObstacleCount => _obstacles.Count;
        public bool IsReady => _ready;
        public bool IsRebuildPending => _dirty;
        public string FailureReason => _failureReason;
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
            var officeRoot = FindOfficeRoot();
            if (officeRoot == null) return null;
            var worlds = FindObjectsByType<OfficeNavigationWorld>(FindObjectsSortMode.None);
            Array.Sort(worlds, CompareWorlds);
            OfficeNavigationWorld existing = null;
            for (var index = 0; index < worlds.Length; index++)
            {
                var item = worlds[index];
                if (item == null || !item.enabled) continue;
                if (item._officeRoot == officeRoot || item.transform.IsChildOf(officeRoot))
                {
                    existing = item;
                    break;
                }
            }

            if (existing != null)
            {
                existing.Register(agent);
                return existing;
            }

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
            _candidatesDirty = true;
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
            if (!TryPrepareNavigationRead()) return false;
            if (!TryGetPathfinder(agentRadius, out var pathfinder)) return false;
            return pathfinder.TryFindPath(
                new OfficeNavPoint(start.x, start.z),
                new OfficeNavPoint(goal.x, goal.z),
                out path);
        }

        public bool IsPathCollisionFree(OfficeNavPath path, float agentRadius)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!TryPrepareNavigationRead() || path.Waypoints.Count == 0) return false;
            if (!TryGetPathfinder(agentRadius, out var pathfinder)) return false;
            for (var index = 0; index < path.Waypoints.Count; index++)
            {
                if (!pathfinder.IsPointWalkable(path.Waypoints[index])) return false;
                if (index > 0 &&
                    !pathfinder.IsSegmentWalkable(path.Waypoints[index - 1], path.Waypoints[index]))
                    return false;
            }

            return true;
        }

        public bool IsMovementCollisionFree(Vector3 start, Vector3 end, float agentRadius)
        {
            if (!TryPrepareNavigationRead()) return false;
            var startPoint = ToPoint(start);
            var endPoint = ToPoint(end);
            var inflation = Mathf.Max(0.05f, agentRadius) + Mathf.Max(0f, characterClearance);
            for (var index = 0; index < _liveObstacles.Count; index++)
            {
                var candidate = _liveObstacles[index];
                if (!candidate.TryGetBounds(out var bounds))
                {
                    _candidatesDirty = true;
                    MarkDirty();
                    return false;
                }

                var extra = candidate.Marker == null ? 0f : candidate.Marker.ExtraClearance;
                var minX = bounds.min.x - inflation - extra;
                var minZ = bounds.min.z - inflation - extra;
                var maxX = bounds.max.x + inflation + extra;
                var maxZ = bounds.max.z + inflation + extra;
                var startDepth = OfficeNavigationGeometryQueries.InteriorDepth(
                    startPoint, minX, minZ, maxX, maxZ);
                if (OfficeNavigationGeometryQueries.IsPointInClosedRectangle(
                        startPoint, minX, minZ, maxX, maxZ))
                {
                    var endDepth = OfficeNavigationGeometryQueries.InteriorDepth(
                        endPoint, minX, minZ, maxX, maxZ);
                    var movesOutward = OfficeNavigationGeometryQueries.MovesTowardNearestBoundary(
                        startPoint, endPoint, minX, minZ, maxX, maxZ);
                    if (movesOutward &&
                        (startDepth <= 0.0001f || endDepth + 0.0001f < startDepth))
                        continue;
                    return false;
                }

                if (OfficeNavigationGeometryQueries.SegmentIntersectsClosedRectangle(
                        startPoint, endPoint, minX, minZ, maxX, maxZ))
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
            var selfState = new OfficeTrafficAgentState(
                self.AgentId,
                ToPoint(self.transform.position),
                ToPoint(desiredVelocity),
                Mathf.Max(0.05f, radius),
                stuckSeconds);
            var decision = OfficeNavigationTrafficRules.Resolve(selfState, _trafficSnapshot);
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

            resolved = Vector3.ClampMagnitude(resolved, speed);
            if (IsVelocitySafe(self, resolved, radius)) return resolved;

            if (decision.RecoveryWeight > 0f && speed > 0.0001f)
            {
                var retreat = -desiredVelocity.normalized * (speed * decision.RecoveryWeight);
                if (IsVelocitySafe(self, retreat, radius))
                {
                    shouldReplan = true;
                    return retreat;
                }
            }

            shouldReplan = true;
            return Vector3.zero;
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
            if (_officeRoot == null)
            {
                FailClosed("Office navigation root is unavailable.");
                return;
            }

            if (_surfaceCollider == null && _surfaceRenderer == null) ResolveSurface();
            if (_surfaceCollider == null && _surfaceRenderer == null)
            {
                FailClosed("Office navigation surface is unavailable.");
                return;
            }

            if (!CanBuildGrid(out var gridFailure))
            {
                FailClosed(gridFailure);
                return;
            }

            if (_candidatesDirty && !TryRefreshObstacleCandidates(out var candidateFailure))
            {
                FailClosed(candidateFailure);
                return;
            }
            if (!TryCollectObstacles(
                    _scratchObstacles,
                    _scratchLiveObstacles,
                    out var fingerprint,
                    out var collectionFailure))
            {
                FailClosed(collectionFailure);
                return;
            }

            _obstacles.Clear();
            _obstacles.AddRange(_scratchObstacles);
            _liveObstacles.Clear();
            _liveObstacles.AddRange(_scratchLiveObstacles);
            _fingerprint = fingerprint;
            _pathfinders.Clear();
            Revision++;
            _dirty = false;
            _dirtyRemaining = 0f;
            _observedExternalMutation = s_externalMutation;
            _ready = true;
            _failureReason = string.Empty;
        }

        private void Awake()
        {
            EnsureConfigured();
        }

        private void Update()
        {
            RefreshTrafficSnapshot();
            if (!_ready) EnsureConfigured();
            if (!_ready) return;
            ObserveExternalMutation();

            _pollRemaining -= Time.unscaledDeltaTime;
            if (_pollRemaining <= 0f)
            {
                _pollRemaining = PollIntervalSeconds;
                if (!TryCollectObstacles(
                        _scratchObstacles,
                        null,
                        out var current,
                        out var collectionFailure))
                {
                    FailClosed(collectionFailure);
                    return;
                }

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
            _trafficSnapshot = Array.Empty<OfficeTrafficAgentState>();
            _trafficSnapshotCount = 0;
        }

        private void EnsureConfigured(bool rebuild = true)
        {
            if (_officeRoot == null) _officeRoot = FindOfficeRoot();
            if (_officeRoot == null) return;
            if (_surfaceCollider == null && _surfaceRenderer == null) ResolveSurface();
            var mutationChanged = _observedExternalMutation != s_externalMutation;
            if (rebuild && !_ready && (_surfaceCollider != null || _surfaceRenderer != null) &&
                (string.IsNullOrEmpty(_failureReason) || mutationChanged))
            {
                if (mutationChanged) _candidatesDirty = true;
                RebuildImmediately();
            }
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
            _surfaceCollider = null;
            _surfaceRenderer = null;
            if (_officeRoot == null) return;
            var colliders = _officeRoot.GetComponentsInChildren<Collider>(true);
            Array.Sort(colliders, CompareComponents);
            for (var index = 0; index < colliders.Length; index++)
            {
                var item = colliders[index];
                if (item == null || !IsOfficeFloor(item.gameObject.name)) continue;
                _surfaceCollider = item;
                break;
            }

            var renderers = _officeRoot.GetComponentsInChildren<Renderer>(true);
            Array.Sort(renderers, CompareComponents);
            for (var index = 0; index < renderers.Length; index++)
            {
                var item = renderers[index];
                if (item == null || !IsOfficeFloor(item.gameObject.name)) continue;
                _surfaceRenderer = item;
                break;
            }

            if (_surfaceCollider != null) SetBounds(_surfaceCollider.bounds);
            else if (_surfaceRenderer != null) SetBounds(_surfaceRenderer.bounds);
            _candidatesDirty = true;
        }

        private void SetBounds(Bounds bounds)
        {
            _navigationBounds = new OfficeNavBounds(
                bounds.min.x,
                bounds.min.z,
                bounds.max.x,
                bounds.max.z);
        }

        private bool TryRefreshObstacleCandidates(out string failure)
        {
            failure = string.Empty;
            _colliderCandidates.Clear();
            _rendererCandidates.Clear();
            _colliderScan.Clear();
            _rendererScan.Clear();
            if (_officeRoot == null)
            {
                failure = "Office navigation root is unavailable.";
                return false;
            }

            _officeRoot.GetComponentsInChildren(true, _colliderScan);
            if (_colliderScan.Count > MaxObstacleCandidates)
            {
                failure = $"Office navigation collider scan exceeded {MaxObstacleCandidates} candidates.";
                return false;
            }

            _colliderScan.Sort(CompareComponents);
            for (var index = 0; index < _colliderScan.Count; index++)
            {
                var collider = _colliderScan[index];
                if (collider == null) continue;
                _colliderCandidates.Add(new ObstacleCandidate(
                    collider,
                    null,
                    collider.GetComponent<OfficeNavigationObstacle>(),
                    HierarchyKey(collider.transform) + ":collider"));
            }

            _officeRoot.GetComponentsInChildren(true, _rendererScan);
            if (_rendererScan.Count > MaxObstacleCandidates)
            {
                failure = $"Office navigation renderer scan exceeded {MaxObstacleCandidates} candidates.";
                return false;
            }

            _rendererScan.Sort(CompareComponents);
            for (var index = 0; index < _rendererScan.Count; index++)
            {
                var renderer = _rendererScan[index];
                if (renderer == null) continue;
                _rendererCandidates.Add(new ObstacleCandidate(
                    null,
                    renderer,
                    renderer.GetComponent<OfficeNavigationObstacle>(),
                    HierarchyKey(renderer.transform) + ":renderer"));
            }

            _candidatesDirty = false;
            return true;
        }

        private bool TryCollectObstacles(
            List<OfficeNavObstacle> result,
            List<ObstacleCandidate> liveResult,
            out ulong fingerprint,
            out string failure)
        {
            result.Clear();
            liveResult?.Clear();
            fingerprint = 0UL;
            failure = string.Empty;
            if (_surfaceCollider == null && _surfaceRenderer == null)
            {
                failure = "Office navigation surface was destroyed.";
                return false;
            }

            if (_candidatesDirty && !TryRefreshObstacleCandidates(out failure)) return false;
            var surfaceTop = _surfaceCollider != null
                ? _surfaceCollider.bounds.max.y
                : _surfaceRenderer.bounds.max.y;
            for (var index = 0; index < _colliderCandidates.Count; index++)
            {
                var candidate = _colliderCandidates[index];
                var collider = candidate.Collider;
                if (collider == null)
                {
                    _candidatesDirty = true;
                    continue;
                }

                if (collider == _surfaceCollider || collider is CharacterController ||
                    !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                    continue;
                if (collider.GetComponentInParent<OfficeWorkerAgent>() != null ||
                    collider.GetComponentInParent<PrototypePlayerController>() != null)
                    continue;
                if (candidate.Marker != null && candidate.Marker.PassableDecoration) continue;
                if (!OverlapsNavigationHeight(collider.bounds, surfaceTop)) continue;
                if (!TryAddObstacle(result, liveResult, candidate, collider.bounds, out failure)) return false;
            }

            for (var index = 0; index < _rendererCandidates.Count; index++)
            {
                var candidate = _rendererCandidates[index];
                var renderer = candidate.Renderer;
                if (renderer == null)
                {
                    _candidatesDirty = true;
                    continue;
                }

                if (renderer == _surfaceRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (candidate.Marker == null || candidate.Marker.PassableDecoration ||
                    !candidate.Marker.UseRendererFootprintWhenColliderMissing)
                    continue;
                if (renderer.GetComponent<Collider>() != null || IsPresentationOnly(renderer)) continue;
                if (!OverlapsNavigationHeight(renderer.bounds, surfaceTop)) continue;
                if (!TryAddObstacle(result, liveResult, candidate, renderer.bounds, out failure)) return false;
            }

            result.Sort(CompareObstacles);
            fingerprint = Fingerprint(result);
            return true;
        }

        private bool TryAddObstacle(
            ICollection<OfficeNavObstacle> target,
            ICollection<ObstacleCandidate> liveTarget,
            ObstacleCandidate candidate,
            Bounds bounds,
            out string failure)
        {
            failure = string.Empty;
            var extraClearance = candidate.Marker == null ? 0f : candidate.Marker.ExtraClearance;
            var minX = bounds.min.x - extraClearance;
            var minZ = bounds.min.z - extraClearance;
            var maxX = bounds.max.x + extraClearance;
            var maxZ = bounds.max.z + extraClearance;
            if (maxX < _navigationBounds.MinX || minX > _navigationBounds.MaxX ||
                maxZ < _navigationBounds.MinZ || minZ > _navigationBounds.MaxZ)
                return true;
            if (target.Count >= OfficeNavigationLimits.MaxObstacles)
            {
                failure = $"Office navigation obstacle cap {OfficeNavigationLimits.MaxObstacles} was exceeded.";
                return false;
            }

            minX = Mathf.Max(minX, _navigationBounds.MinX);
            minZ = Mathf.Max(minZ, _navigationBounds.MinZ);
            maxX = Mathf.Min(maxX, _navigationBounds.MaxX);
            maxZ = Mathf.Min(maxZ, _navigationBounds.MaxZ);
            target.Add(new OfficeNavObstacle(candidate.ObstacleId, minX, minZ, maxX, maxZ));
            liveTarget?.Add(candidate);
            return true;
        }

        private bool TryGetPathfinder(float agentRadius, out DeterministicOfficePathfinder pathfinder)
        {
            pathfinder = null;
            if (!_ready) return false;
            var radiusKey = Mathf.RoundToInt(Mathf.Max(0.05f, agentRadius) * 1000f);
            if (_pathfinders.TryGetValue(radiusKey, out var cache) && cache.Revision == Revision)
            {
                pathfinder = cache.Pathfinder;
                return true;
            }

            try
            {
                pathfinder = new DeterministicOfficePathfinder(
                    _navigationBounds,
                    cellSize,
                    _obstacles,
                    radiusKey / 1000f,
                    characterClearance);
            }
            catch (Exception exception) when (
                exception is ArgumentOutOfRangeException || exception is OverflowException)
            {
                FailClosed("Office navigation pathfinder rejected its bounds: " + exception.Message);
                pathfinder = null;
                return false;
            }

            cache = new PathfinderCacheEntry(Revision, pathfinder);
            _pathfinders[radiusKey] = cache;
            return true;
        }

        private bool OverlapsNavigationHeight(Bounds bounds, float surfaceTop) =>
            bounds.max.y > surfaceTop + maximumStepHeight &&
            bounds.min.y < surfaceTop + navigationBodyHeight;

        private bool CanBuildGrid(out string failure)
        {
            failure = string.Empty;
            if (!OfficeNavigationLimits.TryResolveGridDimensions(
                    _navigationBounds,
                    cellSize,
                    out _,
                    out _,
                    out var count))
            {
                failure =
                    $"Office navigation grid is invalid or exceeds {OfficeNavigationLimits.MaxGridCells} cells.";
                return false;
            }

            return true;
        }

        private void FailClosed(string reason)
        {
            var changed = _ready || !string.Equals(_failureReason, reason, StringComparison.Ordinal);
            _ready = false;
            _dirty = false;
            _dirtyRemaining = 0f;
            _failureReason = reason ?? "Office navigation is unavailable.";
            _observedExternalMutation = s_externalMutation;
            _pathfinders.Clear();
            _obstacles.Clear();
            _liveObstacles.Clear();
            if (changed) Revision++;
        }

        private bool TryPrepareNavigationRead()
        {
            ObserveExternalMutation();
            EnsureConfigured();
            if (_officeRoot == null)
            {
                FailClosed("Office navigation root is unavailable.");
                return false;
            }

            if (_surfaceCollider == null && _surfaceRenderer == null)
            {
                FailClosed("Office navigation surface was destroyed.");
                return false;
            }

            return _ready && !_dirty;
        }

        private bool ObserveExternalMutation()
        {
            if (_observedExternalMutation == s_externalMutation) return false;
            _candidatesDirty = true;
            MarkDirty();
            return true;
        }

        private void RefreshTrafficSnapshot()
        {
            _agentSnapshotScratch.Clear();
            foreach (var agent in _agents)
            {
                if (agent == null || !agent.NavigationCanMove || agent.IsPresentationAway) continue;
                _agentSnapshotScratch.Add(agent);
            }

            _agentSnapshotScratch.Sort(CompareAgents);
            if (_trafficSnapshot.Length != _agentSnapshotScratch.Count)
                _trafficSnapshot = new OfficeTrafficAgentState[_agentSnapshotScratch.Count];
            _trafficSnapshotCount = _agentSnapshotScratch.Count;
            for (var index = 0; index < _trafficSnapshotCount; index++)
            {
                var agent = _agentSnapshotScratch[index];
                _trafficSnapshot[index] = new OfficeTrafficAgentState(
                    agent.AgentId,
                    ToPoint(agent.transform.position),
                    ToPoint(agent.NavigationDesiredVelocity),
                    agent.NavigationRadius,
                    agent.NavigationStuckSeconds);
            }
        }

        private bool IsVelocitySafe(OfficeWorkerAgent self, Vector3 velocity, float radius)
        {
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= 0.000001f) return true;
            var start = self.transform.position;
            var end = start + velocity * TrafficSafetyProbeSeconds;
            return IsMovementCollisionFree(start, end, radius);
        }

        private static bool IsPresentationOnly(Renderer renderer)
        {
            if (renderer.GetComponentInParent<Canvas>() != null) return true;
            var name = renderer.gameObject.name;
            return name.IndexOf("Foreground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Guide", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsOfficeFloor(string name) =>
            !string.IsNullOrEmpty(name) &&
            name.IndexOf("Office Floor", StringComparison.OrdinalIgnoreCase) >= 0;

        private static Transform FindOfficeRoot()
        {
            var exact = GameObject.Find("FAMILY OFFICE V0.2");
            if (exact != null) return exact.transform;
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            Array.Sort(colliders, CompareComponents);
            for (var index = 0; index < colliders.Length; index++)
            {
                var item = colliders[index];
                if (item != null && IsOfficeFloor(item.gameObject.name)) return item.transform.parent;
            }

            return null;
        }

        private static int CompareWorlds(OfficeNavigationWorld left, OfficeNavigationWorld right) =>
            string.CompareOrdinal(
                left == null ? string.Empty : HierarchyKey(left.transform),
                right == null ? string.Empty : HierarchyKey(right.transform));

        private static int CompareComponents<T>(T left, T right) where T : Component =>
            string.CompareOrdinal(
                left == null ? string.Empty : HierarchyKey(left.transform),
                right == null ? string.Empty : HierarchyKey(right.transform));

        private static int CompareAgents(OfficeWorkerAgent left, OfficeWorkerAgent right) =>
            string.CompareOrdinal(
                left == null ? string.Empty : left.AgentId,
                right == null ? string.Empty : right.AgentId);

        private static int CompareObstacles(OfficeNavObstacle left, OfficeNavObstacle right) =>
            string.CompareOrdinal(left.ObstacleId, right.ObstacleId);

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

        private sealed class ObstacleCandidate
        {
            public ObstacleCandidate(
                Collider collider,
                MeshRenderer renderer,
                OfficeNavigationObstacle marker,
                string obstacleId)
            {
                Collider = collider;
                Renderer = renderer;
                Marker = marker;
                ObstacleId = obstacleId;
            }

            public Collider Collider { get; }
            public MeshRenderer Renderer { get; }
            public OfficeNavigationObstacle Marker { get; }
            public string ObstacleId { get; }

            public bool TryGetBounds(out Bounds bounds)
            {
                if (Collider != null && Collider.enabled && Collider.gameObject.activeInHierarchy)
                {
                    bounds = Collider.bounds;
                    return true;
                }

                if (Renderer != null && Renderer.enabled && Renderer.gameObject.activeInHierarchy)
                {
                    bounds = Renderer.bounds;
                    return true;
                }

                bounds = default;
                return false;
            }
        }
    }
}

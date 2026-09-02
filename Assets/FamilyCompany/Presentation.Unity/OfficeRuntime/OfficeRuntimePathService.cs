using System;
using System.Collections;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public sealed class OfficeRuntimePathService
    {
        private static readonly OfficeGridCoordinate[] Neighbors =
        {
            new OfficeGridCoordinate(1, 0),
            new OfficeGridCoordinate(0, -1),
            new OfficeGridCoordinate(-1, 0),
            new OfficeGridCoordinate(0, 1)
        };

        private readonly OfficeGrid _grid;
        private readonly OfficeRuntimeOccupancy _occupancy;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly Dictionary<StaticTraversalKey, StaticTraversalGraph> _staticGraphs =
            new Dictionary<StaticTraversalKey, StaticTraversalGraph>();
        private readonly Queue<OfficeGridCoordinate> _pathQueue =
            new Queue<OfficeGridCoordinate>();
        private readonly HashSet<OfficeGridCoordinate> _pathVisited =
            new HashSet<OfficeGridCoordinate>();
        private readonly Dictionary<OfficeGridCoordinate, OfficeGridCoordinate> _pathParents =
            new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate>();
        private int _cachedOccupancyRevision = -1;

        public long PathSearchCount { get; private set; }
        public long PathVisitedNodeCount { get; private set; }
        public long ReachabilityCallCount { get; private set; }
        public long ReachabilityFloodCount { get; private set; }
        public long ReachabilityCacheMissCount => ReachabilityFloodCount;
        public long ReachabilityCacheHitCount { get; private set; }
        public long ReachabilityVisitedNodeCount { get; private set; }
        public long StaticGraphBuildCount { get; private set; }
        public long StaticGraphCacheMissCount => StaticGraphBuildCount;
        public long StaticGraphCacheHitCount { get; private set; }
        public long StaticGraphNodeCheckCount { get; private set; }
        public long StaticGraphEdgeCheckCount { get; private set; }

        public OfficeRuntimePathService(
            OfficeGrid grid,
            OfficeRuntimeOccupancy occupancy,
            OfficeGridTilemapPresenter presenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public IReadOnlyList<OfficeGridCoordinate> FindPath(
            string agentId,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal,
            string permittedSeatId = "",
            bool avoidDynamic = false,
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.NavigationFindPath);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!_grid.Contains(start) || !_grid.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            PathSearchCount++;
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation)
                return FindPathUncached(
                    agentId,
                    start,
                    goal,
                    permittedSeatId,
                    avoidDynamic,
                    radius);
            StaticTraversalGraph graph = StaticGraph(permittedSeatId, radius);
            if (_occupancy.FurnitureClearancePaddingOf(agentId) > 0f)
                return FindPathWithDeskProximityCost(
                    agentId, start, goal, permittedSeatId, avoidDynamic, radius, graph);
            _pathQueue.Clear();
            _pathVisited.Clear();
            _pathParents.Clear();
            _pathVisited.Add(start);
            _pathQueue.Enqueue(start);
            while (_pathQueue.Count > 0)
            {
                OfficeGridCoordinate current = _pathQueue.Dequeue();
                PathVisitedNodeCount++;
                if (current.Equals(goal)) break;
                OfficeGridCoordinate[] neighbors = ResolveStaticNeighbors(
                    graph,
                    current,
                    permittedSeatId,
                    radius);
                for (var index = 0; index < neighbors.Length; index++)
                {
                    OfficeGridCoordinate next = neighbors[index];
                    if (_pathVisited.Contains(next)) continue;
                    // A moving peer may still be leaving the final goal, so the goal remains
                    // statically testable. Replans avoid every dynamic cell before that goal.
                    bool includeDynamic = avoidDynamic && !next.Equals(goal);
                    if (includeDynamic &&
                        !_occupancy.IsCellPassable(next, agentId, permittedSeatId, true)) continue;
                    _pathVisited.Add(next);
                    _pathParents[next] = current;
                    _pathQueue.Enqueue(next);
                }
            }

            if (!_pathVisited.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            var path = new List<OfficeGridCoordinate> { goal };
            while (!path[path.Count - 1].Equals(start))
                path.Add(_pathParents[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }

        // Wide-bodied (furniture-padded) actors: a cell touching a blocking desk footprint costs
        // this much extra, so the search prefers a route one cell further away whenever the layout
        // leaves room, while still using desk-adjacent cells when they are the only way (or the
        // goal itself). The arm swing of the enlarged candidate bodies reaches past the half-cell,
        // so hugging a desk visibly puts the arm inside the desk top.
        private const float DeskProximityStepPenalty = 2.5f;

        private readonly Dictionary<OfficeGridCoordinate, float> _pathCost =
            new Dictionary<OfficeGridCoordinate, float>();
        private readonly List<OfficeGridCoordinate> _pathOpen = new List<OfficeGridCoordinate>();

        private IReadOnlyList<OfficeGridCoordinate> FindPathWithDeskProximityCost(
            string agentId,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal,
            string permittedSeatId,
            bool avoidDynamic,
            float radius,
            StaticTraversalGraph graph)
        {
            _pathCost.Clear();
            _pathParents.Clear();
            _pathVisited.Clear();
            _pathOpen.Clear();
            _pathCost[start] = 0f;
            _pathOpen.Add(start);
            while (_pathOpen.Count > 0)
            {
                // Uniform-cost search; office grids are small enough for a linear open-list scan.
                var bestIndex = 0;
                float bestCost = _pathCost[_pathOpen[0]];
                for (var index = 1; index < _pathOpen.Count; index++)
                {
                    float cost = _pathCost[_pathOpen[index]];
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestIndex = index;
                    }
                }
                OfficeGridCoordinate current = _pathOpen[bestIndex];
                _pathOpen.RemoveAt(bestIndex);
                if (_pathVisited.Contains(current)) continue;
                _pathVisited.Add(current);
                PathVisitedNodeCount++;
                if (current.Equals(goal)) break;
                OfficeGridCoordinate[] neighbors = ResolveStaticNeighbors(
                    graph,
                    current,
                    permittedSeatId,
                    radius);
                for (var index = 0; index < neighbors.Length; index++)
                {
                    OfficeGridCoordinate next = neighbors[index];
                    if (_pathVisited.Contains(next)) continue;
                    bool includeDynamic = avoidDynamic && !next.Equals(goal);
                    if (includeDynamic &&
                        !_occupancy.IsCellPassable(next, agentId, permittedSeatId, true)) continue;
                    float step = (next.X != current.X && next.Y != current.Y) ? 1.41421356f : 1f;
                    if (!next.Equals(goal) &&
                        _occupancy.HasBlockingFurnitureAdjacent(next, permittedSeatId))
                        step += DeskProximityStepPenalty;
                    float candidate = bestCost + step;
                    if (_pathCost.TryGetValue(next, out float known) && known <= candidate) continue;
                    _pathCost[next] = candidate;
                    _pathParents[next] = current;
                    _pathOpen.Add(next);
                }
            }

            if (!_pathVisited.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            var path = new List<OfficeGridCoordinate> { goal };
            while (!path[path.Count - 1].Equals(start))
                path.Add(_pathParents[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Computes the statically reachable component once for interaction-offer projection.
        /// Calling FindPath once per candidate approach cell repeats the same flood fill hundreds
        /// of times on an open QA layout and can stall runtime rebuilds for minutes.
        /// </summary>
        public HashSet<OfficeGridCoordinate> FindStaticallyReachableCells(
            string agentId,
            OfficeGridCoordinate start,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.NavigationReachability);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            ReachabilityCallCount++;
            if (!_grid.Contains(start)) return new HashSet<OfficeGridCoordinate>();
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation)
                return FindStaticallyReachableCellsUncached(
                    agentId,
                    start,
                    permittedSeatId,
                    radius);
            StaticTraversalGraph graph = StaticGraph(permittedSeatId, radius);
            if (graph.ComponentsByCell.TryGetValue(start, out HashSet<OfficeGridCoordinate> cached))
            {
                ReachabilityCacheHitCount++;
                return cached;
            }

            ReachabilityFloodCount++;
            var reachable = new HashSet<OfficeGridCoordinate> { start };
            var queue = new Queue<OfficeGridCoordinate>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                ReachabilityVisitedNodeCount++;
                OfficeGridCoordinate[] neighbors = ResolveStaticNeighbors(
                    graph,
                    current,
                    permittedSeatId,
                    radius);
                for (var index = 0; index < neighbors.Length; index++)
                {
                    OfficeGridCoordinate next = neighbors[index];
                    if (!reachable.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            foreach (OfficeGridCoordinate cell in reachable)
                graph.ComponentsByCell[cell] = reachable;
            return reachable;
        }

        public void PrewarmStaticTraversalGraph(
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation) return;
            for (var y = 0; y < _grid.Height; y++)
            for (var x = 0; x < _grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!_occupancy.IsCellPassable(
                        cell,
                        string.Empty,
                        permittedSeatId,
                        false)) continue;
                Vector3 center3 = _presenter.CellCenterWorld(cell);
                var center = new Vector2(center3.x, center3.y);
                if (!_occupancy.CanTraverseStatic(
                        center,
                        center,
                        radius,
                        permittedSeatId)) continue;
                FindStaticallyReachableCells(
                    string.Empty,
                    cell,
                    permittedSeatId,
                    radius);
                return;
            }
        }

        public void PrewarmStaticTraversalGraph(
            OfficeGridCoordinate start,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation || !_grid.Contains(start)) return;
            FindStaticallyReachableCells(
                string.Empty,
                start,
                permittedSeatId,
                radius);
        }

        public IEnumerator PrewarmAllStaticTraversalGraphs(
            IReadOnlyList<string> permittedSeatIds,
            int maximumNodesPerFrame,
            Action<float> reportProgress,
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (permittedSeatIds == null) throw new ArgumentNullException(nameof(permittedSeatIds));
            if (maximumNodesPerFrame < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumNodesPerFrame));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation)
            {
                reportProgress?.Invoke(1f);
                yield break;
            }

            var keys = new List<string>();
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < permittedSeatIds.Count; index++)
            {
                string key = (permittedSeatIds[index] ?? string.Empty).Trim();
                if (distinct.Add(key)) keys.Add(key);
            }
            int totalNodes = checked(Math.Max(1, keys.Count * _grid.CellCount));
            var completedNodes = 0;
            var frameNodes = 0;
            foreach (string permittedSeatId in keys)
            {
                StaticTraversalGraph graph = StaticGraph(permittedSeatId, radius);
                for (var y = 0; y < _grid.Height; y++)
                for (var x = 0; x < _grid.Width; x++)
                {
                    ResolveStaticNeighbors(
                        graph,
                        new OfficeGridCoordinate(x, y),
                        permittedSeatId,
                        radius);
                    completedNodes++;
                    frameNodes++;
                    if (frameNodes < maximumNodesPerFrame) continue;
                    frameNodes = 0;
                    reportProgress?.Invoke(completedNodes / (float)totalNodes);
                    yield return null;
                }
                PopulateReachabilityComponents(graph);
            }
            reportProgress?.Invoke(1f);
        }

        public void ResetPerformanceCounters()
        {
            PathSearchCount = 0L;
            PathVisitedNodeCount = 0L;
            ReachabilityCallCount = 0L;
            ReachabilityFloodCount = 0L;
            ReachabilityCacheHitCount = 0L;
            ReachabilityVisitedNodeCount = 0L;
            StaticGraphBuildCount = 0L;
            StaticGraphCacheHitCount = 0L;
            StaticGraphNodeCheckCount = 0L;
            StaticGraphEdgeCheckCount = 0L;
        }

        public bool HasStaticTraversalNeighbor(
            OfficeGridCoordinate cell,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!_grid.Contains(cell)) return false;
            if (OfficeRuntimePerformanceProbe.UseUncachedNavigation)
            {
                if (!_occupancy.IsCellPassable(
                        cell,
                        string.Empty,
                        permittedSeatId,
                        false)) return false;
                Vector3 center3 = _presenter.CellCenterWorld(cell);
                var center = new Vector2(center3.x, center3.y);
                if (!_occupancy.CanTraverseStatic(
                        center,
                        center,
                        radius,
                        permittedSeatId)) return false;
                for (var index = 0; index < Neighbors.Length; index++)
                {
                    OfficeGridCoordinate offset = Neighbors[index];
                    var neighbor = new OfficeGridCoordinate(
                        cell.X + offset.X,
                        cell.Y + offset.Y);
                    if (!_grid.Contains(neighbor) ||
                        !_occupancy.IsCellPassable(
                            neighbor,
                            string.Empty,
                            permittedSeatId,
                            false)) continue;
                    Vector3 neighbor3 = _presenter.CellCenterWorld(neighbor);
                    if (_occupancy.CanTraverseStatic(
                            new Vector2(neighbor3.x, neighbor3.y),
                            center,
                            radius,
                            permittedSeatId)) return true;
                }
                return false;
            }
            StaticTraversalGraph graph = StaticGraph(permittedSeatId, radius);
            return ResolveStaticNeighbors(
                graph,
                cell,
                permittedSeatId,
                radius).Length > 0;
        }

        private StaticTraversalGraph StaticGraph(string permittedSeatId, float radius)
        {
            if (_cachedOccupancyRevision != _occupancy.Revision)
            {
                _staticGraphs.Clear();
                _cachedOccupancyRevision = _occupancy.Revision;
            }
            var key = new StaticTraversalKey(permittedSeatId, radius);
            if (_staticGraphs.TryGetValue(key, out StaticTraversalGraph graph))
            {
                StaticGraphCacheHitCount++;
                return graph;
            }
            StaticGraphBuildCount++;
            graph = new StaticTraversalGraph();
            _staticGraphs.Add(key, graph);
            return graph;
        }

        private OfficeGridCoordinate[] ResolveStaticNeighbors(
            StaticTraversalGraph graph,
            OfficeGridCoordinate cell,
            string permittedSeatId,
            float radius)
        {
            if (graph.Neighbors.TryGetValue(
                    cell,
                    out OfficeGridCoordinate[] cached)) return cached;
            StaticGraphNodeCheckCount++;
            Vector3 center3 = _presenter.CellCenterWorld(cell);
            var center = new Vector2(center3.x, center3.y);
            var open = new List<OfficeGridCoordinate>(Neighbors.Length);
            for (var index = 0; index < Neighbors.Length; index++)
            {
                StaticGraphEdgeCheckCount++;
                OfficeGridCoordinate offset = Neighbors[index];
                var next = new OfficeGridCoordinate(cell.X + offset.X, cell.Y + offset.Y);
                if (!_grid.Contains(next) ||
                    !_occupancy.IsCellPassable(
                        next,
                        string.Empty,
                        permittedSeatId,
                        false)) continue;
                Vector3 next3 = _presenter.CellCenterWorld(next);
                if (_occupancy.CanTraverseStatic(
                        center,
                        new Vector2(next3.x, next3.y),
                        radius,
                        permittedSeatId)) open.Add(next);
            }
            cached = open.ToArray();
            graph.Neighbors.Add(cell, cached);
            return cached;
        }

        private static void PopulateReachabilityComponents(StaticTraversalGraph graph)
        {
            foreach (OfficeGridCoordinate seed in graph.Neighbors.Keys)
            {
                if (graph.ComponentsByCell.ContainsKey(seed)) continue;
                var component = new HashSet<OfficeGridCoordinate> { seed };
                var queue = new Queue<OfficeGridCoordinate>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    OfficeGridCoordinate current = queue.Dequeue();
                    OfficeGridCoordinate[] neighbors = graph.Neighbors[current];
                    for (var index = 0; index < neighbors.Length; index++)
                    {
                        OfficeGridCoordinate next = neighbors[index];
                        if (!component.Add(next)) continue;
                        queue.Enqueue(next);
                    }
                }
                foreach (OfficeGridCoordinate cell in component)
                    graph.ComponentsByCell[cell] = component;
            }
        }

        // Retained only as the command-line A/B baseline for the unattended performance QA.
        // Ordinary gameplay never enters these methods.
        private IReadOnlyList<OfficeGridCoordinate> FindPathUncached(
            string agentId,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal,
            string permittedSeatId,
            bool avoidDynamic,
            float radius)
        {
            var queue = new Queue<OfficeGridCoordinate>();
            var visited = new HashSet<OfficeGridCoordinate> { start };
            var parent = new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                PathVisitedNodeCount++;
                if (current.Equals(goal)) break;
                for (var index = 0; index < Neighbors.Length; index++)
                {
                    OfficeGridCoordinate offset = Neighbors[index];
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!_grid.Contains(next) || visited.Contains(next)) continue;
                    bool includeDynamic = avoidDynamic && !next.Equals(goal);
                    if (!_occupancy.IsCellPassable(
                            next,
                            agentId,
                            permittedSeatId,
                            includeDynamic)) continue;
                    Vector3 currentCenter3 = _presenter.CellCenterWorld(current);
                    Vector3 nextCenter3 = _presenter.CellCenterWorld(next);
                    if (!_occupancy.CanTraverseStatic(
                            new Vector2(currentCenter3.x, currentCenter3.y),
                            new Vector2(nextCenter3.x, nextCenter3.y),
                            radius,
                            permittedSeatId)) continue;
                    visited.Add(next);
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
            if (!visited.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            var path = new List<OfficeGridCoordinate> { goal };
            while (!path[path.Count - 1].Equals(start))
                path.Add(parent[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }

        private HashSet<OfficeGridCoordinate> FindStaticallyReachableCellsUncached(
            string agentId,
            OfficeGridCoordinate start,
            string permittedSeatId,
            float radius)
        {
            ReachabilityFloodCount++;
            var visited = new HashSet<OfficeGridCoordinate> { start };
            var queue = new Queue<OfficeGridCoordinate>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                ReachabilityVisitedNodeCount++;
                for (var index = 0; index < Neighbors.Length; index++)
                {
                    OfficeGridCoordinate offset = Neighbors[index];
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!_grid.Contains(next) || visited.Contains(next)) continue;
                    if (!_occupancy.IsCellPassable(
                            next,
                            agentId,
                            permittedSeatId,
                            false)) continue;
                    Vector3 currentCenter3 = _presenter.CellCenterWorld(current);
                    Vector3 nextCenter3 = _presenter.CellCenterWorld(next);
                    if (!_occupancy.CanTraverseStatic(
                            new Vector2(currentCenter3.x, currentCenter3.y),
                            new Vector2(nextCenter3.x, nextCenter3.y),
                            radius,
                            permittedSeatId)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return visited;
        }

        private readonly struct StaticTraversalKey : IEquatable<StaticTraversalKey>
        {
            public StaticTraversalKey(string permittedSeatId, float radius)
            {
                PermittedSeatId = (permittedSeatId ?? string.Empty).Trim();
                Radius = radius;
            }

            public string PermittedSeatId { get; }
            public float Radius { get; }

            public bool Equals(StaticTraversalKey other) =>
                Radius.Equals(other.Radius) &&
                string.Equals(PermittedSeatId, other.PermittedSeatId, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is StaticTraversalKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((PermittedSeatId != null
                                ? StringComparer.Ordinal.GetHashCode(PermittedSeatId)
                                : 0) * 397) ^ Radius.GetHashCode();
                }
            }
        }

        private sealed class StaticTraversalGraph
        {
            public readonly Dictionary<OfficeGridCoordinate, OfficeGridCoordinate[]> Neighbors =
                new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate[]>();
            public readonly Dictionary<OfficeGridCoordinate, HashSet<OfficeGridCoordinate>>
                ComponentsByCell =
                    new Dictionary<OfficeGridCoordinate, HashSet<OfficeGridCoordinate>>();
        }

        public int ResolvePresentationTargetIndex(
            IReadOnlyList<OfficeGridCoordinate> semanticPath,
            int semanticStartIndex,
            string agentId,
            Vector2 currentPosition,
            float radius,
            string permittedSeatId,
            int maximumLookAhead = 6)
        {
            if (semanticPath == null) throw new ArgumentNullException(nameof(semanticPath));
            if (semanticPath.Count == 0) return -1;
            if (semanticStartIndex < 0 || semanticStartIndex >= semanticPath.Count)
                throw new ArgumentOutOfRangeException(nameof(semanticStartIndex));
            if (maximumLookAhead < 1) throw new ArgumentOutOfRangeException(nameof(maximumLookAhead));
            int furthest = Math.Min(semanticPath.Count - 1, semanticStartIndex + maximumLookAhead - 1);
            for (var index = furthest; index >= semanticStartIndex; index--)
            {
                if (!OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(
                        semanticPath,
                        semanticStartIndex,
                        index)) continue;
                Vector3 target3 = _presenter.CellCenterWorld(semanticPath[index]);
                Vector2 target = new Vector2(target3.x, target3.y);
                if (_occupancy.CanTraverseStatic(
                        currentPosition,
                        target,
                        radius,
                        permittedSeatId) &&
                    _occupancy.HasPresentationClearance(
                        agentId,
                        currentPosition,
                        target,
                        radius)) return index;
            }
            return semanticStartIndex;
        }
    }
}

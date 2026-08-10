using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Navigation
{
    public sealed class DeterministicOfficePathfinder
    {
        private const int CardinalCost = 1000;
        private const int DiagonalCost = 1414;
        private const float SegmentEpsilon = 0.000001f;

        private readonly OfficeNavBounds _bounds;
        private readonly float _cellSize;
        private readonly float _maximumProjectionDistance;
        private readonly int _width;
        private readonly int _height;
        private readonly bool[] _blocked;
        private readonly int[] _g;
        private readonly int[] _parent;
        private readonly bool[] _closed;
        private readonly bool[] _reachable;
        private readonly int[] _queue;
        private readonly MinHeap _heap;

        public DeterministicOfficePathfinder(
            OfficeNavBounds bounds,
            float cellSize,
            IReadOnlyList<OfficeNavObstacle> obstacles,
            float agentRadius,
            float clearance = 0.04f,
            float maximumProjectionDistance = OfficeNavigationLimits.DefaultMaximumProjectionDistance)
        {
            if (cellSize < OfficeNavigationLimits.MinimumCellSize ||
                cellSize > OfficeNavigationLimits.MaximumCellSize ||
                float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (agentRadius < 0f || clearance < 0f ||
                float.IsNaN(agentRadius) || float.IsInfinity(agentRadius) ||
                float.IsNaN(clearance) || float.IsInfinity(clearance))
                throw new ArgumentOutOfRangeException(nameof(agentRadius));
            if (maximumProjectionDistance < 0f ||
                float.IsNaN(maximumProjectionDistance) || float.IsInfinity(maximumProjectionDistance))
                throw new ArgumentOutOfRangeException(nameof(maximumProjectionDistance));
            if (obstacles == null) throw new ArgumentNullException(nameof(obstacles));
            if (obstacles.Count > OfficeNavigationLimits.MaxObstacles)
                throw new ArgumentOutOfRangeException(nameof(obstacles),
                    $"Obstacle count exceeds {OfficeNavigationLimits.MaxObstacles}.");

            _bounds = bounds;
            _cellSize = cellSize;
            _maximumProjectionDistance = maximumProjectionDistance;
            if (!OfficeNavigationLimits.TryResolveGridDimensions(
                    bounds,
                    cellSize,
                    out _width,
                    out _height,
                    out var cellCount))
                throw new ArgumentOutOfRangeException(nameof(bounds),
                    $"Grid dimensions are invalid or exceed {OfficeNavigationLimits.MaxGridCells} cells.");

            _blocked = new bool[cellCount];
            _g = new int[cellCount];
            _parent = new int[cellCount];
            _closed = new bool[cellCount];
            _reachable = new bool[cellCount];
            _queue = new int[cellCount];
            _heap = new MinHeap(Math.Min(cellCount, 256));
            var inflation = agentRadius + clearance;
            for (var obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                var obstacle = obstacles[obstacleIndex].Expanded(inflation);
                var minCellX = ClampX((int)Math.Floor((obstacle.MinX - bounds.MinX) / cellSize));
                var maxCellX = ClampX((int)Math.Floor((obstacle.MaxX - bounds.MinX) / cellSize));
                var minCellZ = ClampZ((int)Math.Floor((obstacle.MinZ - bounds.MinZ) / cellSize));
                var maxCellZ = ClampZ((int)Math.Floor((obstacle.MaxZ - bounds.MinZ) / cellSize));
                for (var z = minCellZ; z <= maxCellZ; z++)
                {
                    for (var x = minCellX; x <= maxCellX; x++)
                    {
                        CellBounds(x, z, out var cellMinX, out var cellMinZ, out var cellMaxX, out var cellMaxZ);
                        if (obstacle.Intersects(cellMinX, cellMinZ, cellMaxX, cellMaxZ))
                            _blocked[ToIndex(x, z)] = true;
                    }
                }
            }
        }

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _blocked.Length;
        public float CellSize => _cellSize;
        public float MaximumProjectionDistance => _maximumProjectionDistance;
        public OfficeNavBounds Bounds => _bounds;

        public bool TryFindPath(OfficeNavPoint start, OfficeNavPoint goal, out OfficeNavPath path)
        {
            path = null;
            if (!TryProjectToWalkable(start, null, out var startIndex, out var startProjected))
                return false;
            BuildReachableSet(startIndex);
            if (!TryProjectToWalkable(goal, _reachable, out var goalIndex, out var goalProjected)) return false;

            for (var index = 0; index < _g.Length; index++)
            {
                _g[index] = int.MaxValue;
                _parent[index] = -1;
                _closed[index] = false;
            }

            _heap.Clear();
            _g[startIndex] = 0;
            var initialH = Heuristic(startIndex, goalIndex);
            _heap.Push(new HeapEntry(startIndex, initialH, initialH));
            var expanded = 0;
            var found = false;
            while (_heap.Count > 0 && expanded < OfficeNavigationLimits.MaxExpandedNodes)
            {
                var current = _heap.Pop();
                if (_closed[current.Index]) continue;
                if (_g[current.Index] == int.MaxValue || current.F != _g[current.Index] + current.H) continue;
                _closed[current.Index] = true;
                expanded++;
                if (current.Index == goalIndex)
                {
                    found = true;
                    break;
                }

                GetCoordinates(current.Index, out var currentX, out var currentZ);
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var nextX = currentX + dx;
                        var nextZ = currentZ + dz;
                        if (!IsInside(nextX, nextZ)) continue;
                        var nextIndex = ToIndex(nextX, nextZ);
                        if (_blocked[nextIndex] || _closed[nextIndex]) continue;
                        if (dx != 0 && dz != 0 &&
                            (_blocked[ToIndex(currentX + dx, currentZ)] ||
                             _blocked[ToIndex(currentX, currentZ + dz)]))
                            continue;

                        var stepCost = dx == 0 || dz == 0 ? CardinalCost : DiagonalCost;
                        var candidate = _g[current.Index] + stepCost;
                        if (candidate >= _g[nextIndex]) continue;
                        _g[nextIndex] = candidate;
                        _parent[nextIndex] = current.Index;
                        var h = Heuristic(nextIndex, goalIndex);
                        _heap.Push(new HeapEntry(nextIndex, candidate + h, h));
                    }
                }
            }

            if (!found) return false;
            var raw = Reconstruct(_parent, startIndex, goalIndex);
            var simplified = Simplify(raw);
            var points = new List<OfficeNavPoint>(simplified.Count + 4);
            for (var index = 0; index < simplified.Count; index++)
                points.Add(CellCenter(simplified[index]));
            AttachExactStart(points, start, startProjected);
            AttachExactGoal(points, goal, goalProjected);
            var uneased = points;
            points = EaseCorners(uneased, Math.Min(0.24f, _cellSize * 0.9f));
            if (!ArePointsWalkable(points)) points = uneased;
            if (!ArePointsWalkable(points)) return false;
            path = new OfficeNavPath(
                points.ToArray(),
                startProjected,
                goalProjected,
                expanded,
                raw.Count,
                _g[goalIndex] * _cellSize / CardinalCost);
            return true;
        }

        public bool IsPointWalkable(OfficeNavPoint point)
        {
            if (!_bounds.Contains(point)) return false;
            return !_blocked[PointToIndex(point)];
        }

        public bool IsSegmentWalkable(OfficeNavPoint start, OfficeNavPoint end)
        {
            if (!_bounds.Contains(start) || !_bounds.Contains(end)) return false;
            var minCellX = ClampX((int)Math.Floor((Math.Min(start.X, end.X) - _bounds.MinX) / _cellSize));
            var maxCellX = ClampX((int)Math.Floor((Math.Max(start.X, end.X) - _bounds.MinX) / _cellSize));
            var minCellZ = ClampZ((int)Math.Floor((Math.Min(start.Z, end.Z) - _bounds.MinZ) / _cellSize));
            var maxCellZ = ClampZ((int)Math.Floor((Math.Max(start.Z, end.Z) - _bounds.MinZ) / _cellSize));
            for (var z = minCellZ; z <= maxCellZ; z++)
            {
                for (var x = minCellX; x <= maxCellX; x++)
                {
                    if (!_blocked[ToIndex(x, z)]) continue;
                    CellBounds(x, z, out var minX, out var minZ, out var maxX, out var maxZ);
                    if (OfficeNavigationGeometryQueries.SegmentIntersectsClosedRectangle(
                            start, end, minX, minZ, maxX, maxZ))
                        return false;
                }
            }

            return true;
        }

        private void AttachExactStart(List<OfficeNavPoint> points, OfficeNavPoint start, bool projected)
        {
            if (projected || points.Count == 0 ||
                OfficeNavPoint.Distance(points[0], start) <= SegmentEpsilon)
                return;
            if (points.Count > 1 && IsSegmentWalkable(start, points[1])) points[0] = start;
            else points.Insert(0, start);
        }

        private void AttachExactGoal(List<OfficeNavPoint> points, OfficeNavPoint goal, bool projected)
        {
            if (projected || points.Count == 0 ||
                OfficeNavPoint.Distance(points[points.Count - 1], goal) <= SegmentEpsilon)
                return;
            if (points.Count > 1 && IsSegmentWalkable(points[points.Count - 2], goal))
                points[points.Count - 1] = goal;
            else
                points.Add(goal);
        }

        private bool ArePointsWalkable(IReadOnlyList<OfficeNavPoint> points)
        {
            if (points == null || points.Count == 0) return false;
            for (var index = 0; index < points.Count; index++)
            {
                if (!IsPointWalkable(points[index])) return false;
                if (index > 0 && !IsSegmentWalkable(points[index - 1], points[index])) return false;
            }

            return true;
        }

        private bool TryProjectToWalkable(
            OfficeNavPoint point,
            IReadOnlyList<bool> allowed,
            out int result,
            out bool projected)
        {
            var clampedX = Math.Max(_bounds.MinX, Math.Min(_bounds.MaxX - 0.0001f, point.X));
            var clampedZ = Math.Max(_bounds.MinZ, Math.Min(_bounds.MaxZ - 0.0001f, point.Z));
            var clamped = new OfficeNavPoint(clampedX, clampedZ);
            var direct = PointToIndex(clamped);
            var pointWasClamped = !_bounds.Contains(point);
            if (!_blocked[direct] && (allowed == null || allowed[direct]))
            {
                result = direct;
                projected = pointWasClamped;
                if (projected && OfficeNavPoint.Distance(point, CellCenter(direct)) >
                    _maximumProjectionDistance + SegmentEpsilon)
                    return false;
                return true;
            }

            result = -1;
            var bestDistance = float.MaxValue;
            var maximumDistanceSquared = _maximumProjectionDistance * _maximumProjectionDistance;
            for (var index = 0; index < _blocked.Length; index++)
            {
                if (_blocked[index] || (allowed != null && !allowed[index])) continue;
                var distance = (CellCenter(index) - point).SqrMagnitude;
                if (distance > maximumDistanceSquared + SegmentEpsilon) continue;
                if (distance > bestDistance + 0.000001f) continue;
                if (Math.Abs(distance - bestDistance) <= 0.000001f && index > result) continue;
                bestDistance = distance;
                result = index;
            }

            projected = true;
            return result >= 0;
        }

        private void BuildReachableSet(int startIndex)
        {
            Array.Clear(_reachable, 0, _reachable.Length);
            var read = 0;
            var write = 0;
            _reachable[startIndex] = true;
            _queue[write++] = startIndex;
            while (read < write)
            {
                var current = _queue[read++];
                GetCoordinates(current, out var currentX, out var currentZ);
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var nextX = currentX + dx;
                        var nextZ = currentZ + dz;
                        if (!IsOpen(nextX, nextZ)) continue;
                        if (dx != 0 && dz != 0 &&
                            (!IsOpen(currentX + dx, currentZ) || !IsOpen(currentX, currentZ + dz)))
                            continue;
                        var next = ToIndex(nextX, nextZ);
                        if (_reachable[next]) continue;
                        _reachable[next] = true;
                        _queue[write++] = next;
                    }
                }
            }
        }

        private List<int> Reconstruct(int[] parent, int start, int goal)
        {
            var result = new List<int>();
            var current = goal;
            while (current >= 0)
            {
                result.Add(current);
                if (current == start) break;
                current = parent[current];
            }

            if (result[result.Count - 1] != start) return new List<int>();
            result.Reverse();
            return result;
        }

        private List<int> Simplify(IReadOnlyList<int> raw)
        {
            if (raw.Count <= 2) return new List<int>(raw);
            var result = new List<int> { raw[0] };
            var anchor = 0;
            while (anchor < raw.Count - 1)
            {
                var furthest = anchor + 1;
                for (var candidate = raw.Count - 1; candidate > anchor + 1; candidate--)
                {
                    if (!HasLineOfSight(raw[anchor], raw[candidate])) continue;
                    furthest = candidate;
                    break;
                }

                result.Add(raw[furthest]);
                anchor = furthest;
            }

            return result;
        }

        private List<OfficeNavPoint> EaseCorners(IReadOnlyList<OfficeNavPoint> points, float radius)
        {
            if (points.Count <= 2 || radius <= 0f) return new List<OfficeNavPoint>(points);
            var result = new List<OfficeNavPoint>(points.Count * 2) { points[0] };
            for (var index = 1; index < points.Count - 1; index++)
            {
                var previous = points[index - 1];
                var current = points[index];
                var next = points[index + 1];
                var incoming = (current - previous).Normalized;
                var outgoing = (next - current).Normalized;
                var dot = OfficeNavPoint.Dot(incoming, outgoing);
                if (dot > 0.985f)
                {
                    result.Add(current);
                    continue;
                }

                var trim = Math.Min(radius,
                    Math.Min(OfficeNavPoint.Distance(previous, current), OfficeNavPoint.Distance(current, next)) * 0.32f);
                var before = current - incoming * trim;
                var after = current + outgoing * trim;
                if (IsSegmentWalkable(result[result.Count - 1], before) &&
                    IsSegmentWalkable(before, after) &&
                    IsSegmentWalkable(after, next))
                {
                    result.Add(before);
                    result.Add(after);
                }
                else
                {
                    result.Add(current);
                }
            }

            result.Add(points[points.Count - 1]);
            return result;
        }

        private bool HasLineOfSight(int startIndex, int endIndex)
        {
            return IsSegmentWalkable(CellCenter(startIndex), CellCenter(endIndex));
        }

        private bool IsOpen(int x, int z) => IsInside(x, z) && !_blocked[ToIndex(x, z)];
        private bool IsInside(int x, int z) => x >= 0 && x < _width && z >= 0 && z < _height;
        private int ClampX(int value) => Math.Max(0, Math.Min(_width - 1, value));
        private int ClampZ(int value) => Math.Max(0, Math.Min(_height - 1, value));
        private int ToIndex(int x, int z) => z * _width + x;

        private int PointToIndex(OfficeNavPoint point)
        {
            var x = ClampX((int)Math.Floor((point.X - _bounds.MinX) / _cellSize));
            var z = ClampZ((int)Math.Floor((point.Z - _bounds.MinZ) / _cellSize));
            return ToIndex(x, z);
        }

        private OfficeNavPoint CellCenter(int index)
        {
            GetCoordinates(index, out var x, out var z);
            CellBounds(x, z, out var minX, out var minZ, out var maxX, out var maxZ);
            return new OfficeNavPoint((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        private void CellBounds(int x, int z, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = _bounds.MinX + x * _cellSize;
            minZ = _bounds.MinZ + z * _cellSize;
            maxX = Math.Min(_bounds.MaxX, minX + _cellSize);
            maxZ = Math.Min(_bounds.MaxZ, minZ + _cellSize);
        }

        private void GetCoordinates(int index, out int x, out int z)
        {
            z = index / _width;
            x = index - z * _width;
        }

        private int Heuristic(int from, int to)
        {
            GetCoordinates(from, out var fromX, out var fromZ);
            GetCoordinates(to, out var toX, out var toZ);
            var dx = Math.Abs(toX - fromX);
            var dz = Math.Abs(toZ - fromZ);
            var diagonal = Math.Min(dx, dz);
            var straight = Math.Max(dx, dz) - diagonal;
            return diagonal * DiagonalCost + straight * CardinalCost;
        }

        private readonly struct HeapEntry
        {
            public HeapEntry(int index, int f, int h)
            {
                Index = index;
                F = f;
                H = h;
            }

            public int Index { get; }
            public int F { get; }
            public int H { get; }
        }

        private sealed class MinHeap
        {
            private readonly List<HeapEntry> _items;

            public MinHeap(int capacity)
            {
                _items = new List<HeapEntry>(capacity);
            }

            public int Count => _items.Count;

            public void Clear()
            {
                _items.Clear();
            }

            public void Push(HeapEntry value)
            {
                _items.Add(value);
                var index = _items.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (Compare(_items[parent], value) <= 0) break;
                    _items[index] = _items[parent];
                    index = parent;
                }

                _items[index] = value;
            }

            public HeapEntry Pop()
            {
                var result = _items[0];
                var lastIndex = _items.Count - 1;
                var last = _items[lastIndex];
                _items.RemoveAt(lastIndex);
                if (_items.Count == 0) return result;
                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= _items.Count) break;
                    var right = left + 1;
                    var child = right < _items.Count && Compare(_items[right], _items[left]) < 0 ? right : left;
                    if (Compare(last, _items[child]) <= 0) break;
                    _items[index] = _items[child];
                    index = child;
                }

                _items[index] = last;
                return result;
            }

            private static int Compare(HeapEntry left, HeapEntry right)
            {
                var byF = left.F.CompareTo(right.F);
                if (byF != 0) return byF;
                var byH = left.H.CompareTo(right.H);
                if (byH != 0) return byH;
                return left.Index.CompareTo(right.Index);
            }
        }
    }
}

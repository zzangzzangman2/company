using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeRuntimeOccupancyLayer
    {
        StaticHard = 0,
        Interaction = 1,
        Dynamic = 2
    }

    public sealed class OfficeRuntimeOccupancy
    {
        private sealed class FurnitureObstacle
        {
            public PlacedOfficeFurniture Furniture;
            public OfficeFurnitureCollisionProfile Profile;
            public OfficeRuntimeOccupancyLayer Layer;
            public string InteractionSeatId = string.Empty;
            public readonly HashSet<string> PermittedWorkSurfaceSeatIds =
                new HashSet<string>(StringComparer.Ordinal);

            public bool IsPermitted(string permittedSeatId)
            {
                if (permittedSeatId.Length == 0) return false;
                return string.Equals(InteractionSeatId, permittedSeatId, StringComparison.Ordinal) ||
                       PermittedWorkSurfaceSeatIds.Contains(permittedSeatId);
            }
        }

        private readonly struct ContinuousGridTransform
        {
            public ContinuousGridTransform(Vector2 origin, Vector2 basisX, Vector2 basisY)
            {
                Origin = origin;
                BasisX = basisX;
                BasisY = basisY;
                Determinant = basisX.x * basisY.y - basisX.y * basisY.x;
            }

            public Vector2 Origin { get; }
            public Vector2 BasisX { get; }
            public Vector2 BasisY { get; }
            public float Determinant { get; }

            public bool TryConvert(Vector2 point, out float gridX, out float gridY)
            {
                if (Mathf.Abs(Determinant) <= 0.000001f)
                {
                    gridX = 0f;
                    gridY = 0f;
                    return false;
                }
                Vector2 delta = point - Origin;
                gridX = (delta.x * BasisY.y - delta.y * BasisY.x) / Determinant;
                gridY = (BasisX.x * delta.y - BasisX.y * delta.x) / Determinant;
                return true;
            }
        }

        private sealed class ActorState
        {
            public string AgentId;
            public Vector2 Position;
            public Vector2 DesiredVelocity;
            public float Radius;
            public float StuckSeconds;
            public OfficeGridCoordinate CurrentCell;
            public bool IsPresent = true;
            public readonly HashSet<OfficeGridCoordinate> Reservations =
                new HashSet<OfficeGridCoordinate>();
        }

        private static readonly Vector2[] CollisionDirections =
        {
            Vector2.zero,
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f)
        };

        private readonly HashSet<OfficeGridCoordinate> _hardFloor =
            new HashSet<OfficeGridCoordinate>();
        private readonly List<FurnitureObstacle> _furnitureObstacles =
            new List<FurnitureObstacle>();
        private readonly Dictionary<OfficeGridCoordinate, string> _interactionSeats =
            new Dictionary<OfficeGridCoordinate, string>();
        private readonly HashSet<string> _profiledInteractionSeatIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActorState> _actors =
            new Dictionary<string, ActorState>(StringComparer.Ordinal);
        private readonly Dictionary<OfficeGridCoordinate, int> _narrowCorridorIds =
            new Dictionary<OfficeGridCoordinate, int>();
        private readonly Dictionary<int, string> _narrowCorridorOwners =
            new Dictionary<int, string>();
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _presenter;

        public int Revision { get; private set; }
        public int StaticViolationCount { get; private set; }
        public int InteractionViolationCount { get; private set; }
        public int AgentPenetrationCount { get; private set; }
        public int BlockedStaticMoveCount { get; private set; }
        public int BlockedInteractionMoveCount { get; private set; }
        public int BlockedAgentMoveCount { get; private set; }
        public float MinimumAgentSeparationMargin { get; private set; } = float.PositiveInfinity;

        public void ResetMetrics()
        {
            StaticViolationCount = 0;
            InteractionViolationCount = 0;
            AgentPenetrationCount = 0;
            BlockedStaticMoveCount = 0;
            BlockedInteractionMoveCount = 0;
            BlockedAgentMoveCount = 0;
            MinimumAgentSeparationMargin = float.PositiveInfinity;
        }

        public void Rebuild(OfficeGrid grid, OfficeGridTilemapPresenter presenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _hardFloor.Clear();
            _furnitureObstacles.Clear();
            _interactionSeats.Clear();
            _profiledInteractionSeatIds.Clear();
            _narrowCorridorIds.Clear();
            _narrowCorridorOwners.Clear();
            var blockingFurnitureCells = new HashSet<OfficeGridCoordinate>();
            foreach (PlacedOfficeFurniture furniture in grid.Furniture)
            {
                if (!furniture.BlocksMovement) continue;
                for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
                for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
                    blockingFurnitureCells.Add(new OfficeGridCoordinate(x, y));
            }
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                // Authored layouts also mark cells beneath blocking furniture as unwalkable.
                // Those cells must be removed from floor occupancy so the furniture's precise
                // subcell mask, rather than the old whole-cell rectangle, owns the collision.
                if (!grid.IsWalkable(cell) && !blockingFurnitureCells.Contains(cell))
                    _hardFloor.Add(cell);
            }

            OfficeFurnitureCollisionCatalog catalog =
                Resources.Load<OfficeFurnitureCollisionCatalog>(
                    OfficeFurnitureCollisionCatalog.DefaultResourcePath);
            if (catalog != null) catalog.Validate();
            var obstaclesByFurnitureId = new Dictionary<string, FurnitureObstacle>(StringComparer.Ordinal);
            foreach (PlacedOfficeFurniture furniture in grid.Furniture)
            {
                if (!furniture.BlocksMovement) continue;
                var obstacle = CreateObstacle(
                    furniture,
                    OfficeRuntimeOccupancyLayer.StaticHard,
                    string.Empty,
                    catalog);
                _furnitureObstacles.Add(obstacle);
                obstaclesByFurnitureId[furniture.FurnitureId] = obstacle;
            }

            foreach (OfficeSeatSlot seat in grid.SeatSlots)
            {
                _interactionSeats[seat.Cell] = seat.SeatId;
                PlacedOfficeFurniture seatFurniture = grid.Furniture.FirstOrDefault(item =>
                    string.Equals(item.FurnitureId, seat.FurnitureId, StringComparison.Ordinal));
                if (seatFurniture != null && !seatFurniture.BlocksMovement)
                {
                    _furnitureObstacles.Add(CreateObstacle(
                        seatFurniture,
                        OfficeRuntimeOccupancyLayer.Interaction,
                        seat.SeatId,
                        catalog));
                    _profiledInteractionSeatIds.Add(seat.SeatId);
                }
                if (!seat.HasWorkstationBinding) continue;
                if (obstaclesByFurnitureId.TryGetValue(
                        seat.WorkSurfaceFurnitureId,
                        out FurnitureObstacle workSurfaceObstacle))
                    workSurfaceObstacle.PermittedWorkSurfaceSeatIds.Add(seat.SeatId);
            }
            BuildNarrowCorridorComponents();
            foreach (var actor in _actors.Values) actor.Reservations.Clear();
            Revision++;
        }

        public void RegisterActor(string agentId, Vector2 position, float radius)
        {
            var canonical = RequiredId(agentId);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (_actors.ContainsKey(canonical))
                throw new InvalidOperationException("Duplicate runtime occupancy actor: " + canonical);
            if (!PointClearsStatic(position, radius, string.Empty, out OfficeRuntimeOccupancyLayer blockedLayer))
                throw new InvalidOperationException(
                    $"Runtime actor '{canonical}' spawn intersects {blockedLayer} occupancy.");
            _actors.Add(canonical, new ActorState
            {
                AgentId = canonical,
                Position = position,
                Radius = radius,
                CurrentCell = _presenter.NearestCell(new Vector3(position.x, position.y, 0f))
            });
        }

        public void UnregisterActor(string agentId)
        {
            ReleaseNarrowCorridors(agentId ?? string.Empty);
            _actors.Remove(agentId ?? string.Empty);
        }

        public bool IsActorPresent(string agentId) => RequiredActor(agentId).IsPresent;

        public void SetActorPresent(string agentId, bool isPresent)
        {
            ActorState state = RequiredActor(agentId);
            if (!isPresent)
            {
                state.IsPresent = false;
                state.DesiredVelocity = Vector2.zero;
                state.StuckSeconds = 0f;
                state.Reservations.Clear();
                ReleaseNarrowCorridors(state.AgentId);
                return;
            }

            if (state.IsPresent) return;
            // A returning actor keeps its registered identity and most recent position, but it
            // must re-enter dynamic occupancy without any stale route or corridor ownership.
            state.Reservations.Clear();
            ReleaseNarrowCorridors(state.AgentId);
            state.DesiredVelocity = Vector2.zero;
            state.StuckSeconds = 0f;
            state.IsPresent = true;
        }

        public void UpdateActor(
            string agentId,
            Vector2 position,
            Vector2 desiredVelocity,
            float stuckSeconds,
            string permittedSeatId = "")
        {
            ActorState state = RequiredActor(agentId);
            if (!state.IsPresent)
            {
                state.Position = position;
                state.DesiredVelocity = desiredVelocity;
                state.StuckSeconds = Mathf.Max(0f, stuckSeconds);
                state.CurrentCell = _presenter.NearestCell(new Vector3(position.x, position.y, 0f));
                return;
            }
            if (!PointClearsStatic(position, state.Radius, permittedSeatId, out OfficeRuntimeOccupancyLayer blockedLayer))
            {
                if (blockedLayer == OfficeRuntimeOccupancyLayer.Interaction) InteractionViolationCount++;
                else StaticViolationCount++;
            }
            state.Position = position;
            state.DesiredVelocity = desiredVelocity;
            state.StuckSeconds = Mathf.Max(0f, stuckSeconds);
            state.CurrentCell = _presenter.NearestCell(new Vector3(position.x, position.y, 0f));
            ReleaseExitedNarrowCorridors(state);
            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, state) || !peer.IsPresent) continue;
                float margin = Vector2.Distance(position, peer.Position) - (state.Radius + peer.Radius);
                MinimumAgentSeparationMargin = Mathf.Min(MinimumAgentSeparationMargin, margin);
                if (margin < -0.01f) AgentPenetrationCount++;
            }
        }

        public bool TryReservePath(
            string agentId,
            OfficeGridCoordinate current,
            IReadOnlyList<OfficeGridCoordinate> upcoming)
        {
            ActorState self = RequiredActor(agentId);
            if (!self.IsPresent)
            {
                self.Reservations.Clear();
                ReleaseNarrowCorridors(self.AgentId);
                return false;
            }
            self.Reservations.Clear();
            // Reservations from the previous frame must not keep a corridor locked after
            // the owner has stepped into the destination room.
            ReleaseExitedNarrowCorridors(self);
            var requested = new List<OfficeGridCoordinate> { current };
            if (upcoming != null)
            {
                for (var index = 0; index < upcoming.Count && index < 2; index++)
                    if (!requested.Contains(upcoming[index])) requested.Add(upcoming[index]);
            }

            var claimedNow = new List<int>();
            foreach (OfficeGridCoordinate cell in requested)
            {
                if (!_narrowCorridorIds.TryGetValue(cell, out int corridorId)) continue;
                if (_narrowCorridorOwners.TryGetValue(corridorId, out string ownerId))
                {
                    if (string.Equals(ownerId, self.AgentId, StringComparison.Ordinal)) continue;
                    self.Reservations.Clear();
                    return false;
                }
                _narrowCorridorOwners.Add(corridorId, self.AgentId);
                claimedNow.Add(corridorId);
            }

            for (var requestIndex = 0; requestIndex < requested.Count; requestIndex++)
            {
                OfficeGridCoordinate cell = requested[requestIndex];
                bool blocked = false;
                foreach (ActorState peer in _actors.Values)
                {
                    if (ReferenceEquals(peer, self) || !peer.IsPresent) continue;
                    if (!peer.CurrentCell.Equals(cell) && !peer.Reservations.Contains(cell)) continue;
                    blocked = true;
                    break;
                }
                if (!blocked)
                {
                    self.Reservations.Add(cell);
                    continue;
                }
                if (requestIndex <= 1)
                {
                    self.Reservations.Clear();
                    foreach (int corridorId in claimedNow)
                        if (_narrowCorridorOwners.TryGetValue(corridorId, out string ownerId) &&
                            string.Equals(ownerId, self.AgentId, StringComparison.Ordinal))
                            _narrowCorridorOwners.Remove(corridorId);
                    return false;
                }
                break;
            }
            return true;
        }

        public void ClearReservations(string agentId)
        {
            if (_actors.TryGetValue(agentId ?? string.Empty, out ActorState actor))
                actor.Reservations.Clear();
            ReleaseNarrowCorridors(agentId ?? string.Empty);
        }

        public string DescribePathReservationBlocker(
            string agentId,
            OfficeGridCoordinate current,
            IReadOnlyList<OfficeGridCoordinate> upcoming)
        {
            ActorState self = RequiredActor(agentId);
            var requested = new List<OfficeGridCoordinate> { current };
            if (upcoming != null)
            {
                for (var index = 0; index < upcoming.Count && index < 2; index++)
                    if (!requested.Contains(upcoming[index])) requested.Add(upcoming[index]);
            }
            foreach (OfficeGridCoordinate cell in requested)
            {
                if (_narrowCorridorIds.TryGetValue(cell, out int corridorId) &&
                    _narrowCorridorOwners.TryGetValue(corridorId, out string ownerId) &&
                    !string.Equals(ownerId, self.AgentId, StringComparison.Ordinal))
                    return $"corridor={corridorId}:owner={ownerId}:cell={cell}";
                foreach (ActorState peer in _actors.Values)
                {
                    if (ReferenceEquals(peer, self) || !peer.IsPresent) continue;
                    if (peer.CurrentCell.Equals(cell))
                        return $"peer={peer.AgentId}:current={cell}";
                    if (peer.Reservations.Contains(cell))
                        return $"peer={peer.AgentId}:reserved={cell}";
                }
            }
            return "unknown";
        }

        public bool IsCellPassable(
            OfficeGridCoordinate cell,
            string agentId,
            string permittedSeatId,
            bool includeDynamic)
        {
            if (_grid == null || !_grid.Contains(cell)) return false;
            Vector3 center = _presenter.CellCenterWorld(cell);
            if (!PointClearsStatic(
                    new Vector2(center.x, center.y),
                    OfficeRuntimeAgent.DefaultRadius,
                    permittedSeatId ?? string.Empty,
                    out _)) return false;
            if (!includeDynamic) return true;
            foreach (ActorState peer in _actors.Values)
            {
                if (!peer.IsPresent || string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
                if (peer.CurrentCell.Equals(cell) || peer.Reservations.Contains(cell)) return false;
            }
            return true;
        }

        public bool CanMove(
            string agentId,
            Vector2 start,
            Vector2 end,
            float radius,
            string permittedSeatId)
        {
            var delta = end - start;
            var samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                if (!PointClearsStatic(point, radius, permittedSeatId, out OfficeRuntimeOccupancyLayer blockedLayer))
                {
                    if (blockedLayer == OfficeRuntimeOccupancyLayer.Interaction) BlockedInteractionMoveCount++;
                    else BlockedStaticMoveCount++;
                    return false;
                }
                OfficeGridCoordinate pointCell = _presenter.NearestCell(
                    new Vector3(point.x, point.y, 0f));
                foreach (ActorState peer in _actors.Values)
                {
                    if (!peer.IsPresent || string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
                    if (peer.Reservations.Contains(pointCell) && !peer.CurrentCell.Equals(pointCell))
                    {
                        BlockedAgentMoveCount++;
                        return false;
                    }
                    float margin = Vector2.Distance(point, peer.Position) - (radius + peer.Radius);
                    if (margin >= -0.01f) continue;
                    BlockedAgentMoveCount++;
                    return false;
                }
            }
            return true;
        }

        public string DescribeMoveBlocker(
            string agentId,
            Vector2 start,
            Vector2 end,
            float radius,
            string permittedSeatId)
        {
            var delta = end - start;
            var samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                if (!PointClearsStatic(point, radius, permittedSeatId, out OfficeRuntimeOccupancyLayer layer))
                    return "static=" + DescribeStaticBlocker(point, radius, permittedSeatId, layer);
                OfficeGridCoordinate pointCell = _presenter.NearestCell(new Vector3(point.x, point.y, 0f));
                foreach (ActorState peer in _actors.Values)
                {
                    if (!peer.IsPresent || string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
                    if (peer.Reservations.Contains(pointCell) && !peer.CurrentCell.Equals(pointCell))
                        return $"peer={peer.AgentId}:reserved={pointCell}";
                    float margin = Vector2.Distance(point, peer.Position) - (radius + peer.Radius);
                    if (margin < -0.01f)
                        return $"peer={peer.AgentId}:overlap={margin:F3}:cell={peer.CurrentCell}";
                }
            }
            return "unknown";
        }

        private string DescribeStaticBlocker(
            Vector2 point,
            float radius,
            string permittedSeatId,
            OfficeRuntimeOccupancyLayer fallbackLayer)
        {
            string permitted = permittedSeatId ?? string.Empty;
            foreach (Vector2 direction in CollisionDirections)
            {
                OfficeGridCoordinate cell = _presenter.NearestCell(
                    new Vector3(point.x + direction.x * radius, point.y + direction.y * radius, 0f));
                if (_hardFloor.Contains(cell)) return $"hard-floor:cell={cell}";
            }
            ContinuousGridTransform gridTransform = CaptureContinuousGridTransform();
            foreach (FurnitureObstacle obstacle in _furnitureObstacles)
            {
                if (obstacle.IsPermitted(permitted)) continue;
                float expandedRadius = radius + (obstacle.Profile?.ClearancePadding ?? 0f);
                foreach (Vector2 direction in CollisionDirections)
                {
                    Vector2 samplePoint = point + direction * expandedRadius;
                    if (PointInsideObstacle(samplePoint, obstacle, gridTransform))
                        return $"{obstacle.Layer}:furniture={obstacle.Furniture.FurnitureId}:kind={obstacle.Furniture.KindId}";
                }
            }
            return fallbackLayer.ToString();
        }

        public bool CanTraverseStatic(
            Vector2 start,
            Vector2 end,
            float radius,
            string permittedSeatId)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            // With no authored blockers, the radius-cleared grid footprint is convex. Checking
            // both endpoints is therefore equivalent to sampling the whole segment and avoids
            // thousands of repeated Tilemap conversions on large open QA/editor layouts.
            if (_hardFloor.Count == 0 && _furnitureObstacles.Count == 0 && _interactionSeats.Count == 0)
            {
                return PointClearsStatic(start, radius, permittedSeatId, out _) &&
                       PointClearsStatic(end, radius, permittedSeatId, out _);
            }
            var delta = end - start;
            var samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                if (!PointClearsStatic(point, radius, permittedSeatId, out _)) return false;
            }
            return true;
        }

        public bool HasPresentationClearance(
            string agentId,
            Vector2 start,
            Vector2 end,
            float radius,
            float extraClearance = 0.75f)
        {
            ActorState self = RequiredActor(agentId);
            if (!self.IsPresent) return false;
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (extraClearance < 0f || float.IsNaN(extraClearance) || float.IsInfinity(extraClearance))
                throw new ArgumentOutOfRangeException(nameof(extraClearance));
            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, self) || !peer.IsPresent) continue;
                float required = radius + peer.Radius + extraClearance;
                if (DistanceToSegment(peer.Position, start, end) < required) return false;
            }
            return true;
        }

        public IReadOnlyList<OfficeTrafficAgentState> TrafficSnapshot()
        {
            return _actors.Values
                .Where(item => item.IsPresent)
                .OrderBy(item => item.AgentId, StringComparer.Ordinal)
                .Select(item => new OfficeTrafficAgentState(
                    item.AgentId,
                    new OfficeNavPoint(item.Position.x, item.Position.y),
                    new OfficeNavPoint(item.DesiredVelocity.x, item.DesiredVelocity.y),
                    item.Radius,
                    item.StuckSeconds))
                .ToArray();
        }

        public OfficeGridCoordinate CurrentCell(string agentId) => RequiredActor(agentId).CurrentCell;

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0000001f) return Vector2.Distance(point, start);
            float projection = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * projection);
        }

        private void BuildNarrowCorridorComponents()
        {
            var candidates = new HashSet<OfficeGridCoordinate>();
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0), new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1), new OfficeGridCoordinate(0, -1)
            };
            for (var y = 0; y < _grid.Height; y++)
            for (var x = 0; x < _grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!IsStaticOpen(cell)) continue;
                var open = new List<OfficeGridCoordinate>(4);
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var neighbor = new OfficeGridCoordinate(cell.X + offset.X, cell.Y + offset.Y);
                    if (IsStaticOpen(neighbor)) open.Add(neighbor);
                }
                if (open.Count != 2) continue;
                bool horizontal = open[0].Y == cell.Y && open[1].Y == cell.Y;
                bool vertical = open[0].X == cell.X && open[1].X == cell.X;
                if (horizontal || vertical) candidates.Add(cell);
            }

            int nextId = 1;
            while (candidates.Count > 0)
            {
                OfficeGridCoordinate seed = candidates.First();
                candidates.Remove(seed);
                var queue = new Queue<OfficeGridCoordinate>();
                var component = new List<OfficeGridCoordinate>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    OfficeGridCoordinate current = queue.Dequeue();
                    component.Add(current);
                    foreach (OfficeGridCoordinate offset in offsets)
                    {
                        var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                        if (!candidates.Remove(next)) continue;
                        queue.Enqueue(next);
                    }
                }
                // Single isolated cells are ordinary doorway turns; reserving only a run of
                // at least two cells avoids turning every office nook into a corridor lock.
                if (component.Count < 2) continue;
                foreach (OfficeGridCoordinate cell in component) _narrowCorridorIds[cell] = nextId;
                nextId++;
            }
        }

        private bool IsStaticOpen(OfficeGridCoordinate cell)
        {
            if (!_grid.Contains(cell)) return false;
            Vector3 center = _presenter.CellCenterWorld(cell);
            return PointClearsStatic(
                new Vector2(center.x, center.y),
                OfficeRuntimeAgent.DefaultRadius,
                string.Empty,
                out _);
        }

        private void ReleaseExitedNarrowCorridors(ActorState actor)
        {
            var release = new List<int>();
            foreach (KeyValuePair<int, string> item in _narrowCorridorOwners)
            {
                if (!string.Equals(item.Value, actor.AgentId, StringComparison.Ordinal)) continue;
                bool currentInside = _narrowCorridorIds.TryGetValue(actor.CurrentCell, out int currentId) &&
                                     currentId == item.Key;
                bool reservedInside = actor.Reservations.Any(cell =>
                    _narrowCorridorIds.TryGetValue(cell, out int reservedId) && reservedId == item.Key);
                if (!currentInside && !reservedInside) release.Add(item.Key);
            }
            foreach (int corridorId in release) _narrowCorridorOwners.Remove(corridorId);
        }

        private void ReleaseNarrowCorridors(string agentId)
        {
            int[] owned = _narrowCorridorOwners
                .Where(item => string.Equals(item.Value, agentId, StringComparison.Ordinal))
                .Select(item => item.Key)
                .ToArray();
            foreach (int corridorId in owned) _narrowCorridorOwners.Remove(corridorId);
        }

        private bool PointClearsStatic(
            Vector2 point,
            float radius,
            string permittedSeatId,
            out OfficeRuntimeOccupancyLayer blockedLayer)
        {
            string permitted = permittedSeatId ?? string.Empty;
            foreach (Vector2 direction in CollisionDirections)
            {
                Vector2 samplePoint = point + direction * radius;
                OfficeGridCoordinate cell = _presenter.NearestCell(
                    new Vector3(samplePoint.x, samplePoint.y, 0f));
                if (!_grid.Contains(cell))
                {
                    blockedLayer = OfficeRuntimeOccupancyLayer.StaticHard;
                    return false;
                }
                if (_hardFloor.Contains(cell))
                {
                    blockedLayer = OfficeRuntimeOccupancyLayer.StaticHard;
                    return false;
                }
                if (_interactionSeats.TryGetValue(cell, out string seatId) &&
                    !_profiledInteractionSeatIds.Contains(seatId) &&
                    !string.Equals(seatId, permitted, StringComparison.Ordinal))
                {
                    blockedLayer = OfficeRuntimeOccupancyLayer.Interaction;
                    return false;
                }
            }

            ContinuousGridTransform gridTransform = CaptureContinuousGridTransform();
            foreach (FurnitureObstacle obstacle in _furnitureObstacles)
            {
                if (obstacle.IsPermitted(permitted)) continue;
                float expandedRadius = radius + (obstacle.Profile?.ClearancePadding ?? 0f);
                foreach (Vector2 direction in CollisionDirections)
                {
                    Vector2 samplePoint = point + direction * expandedRadius;
                    if (!PointInsideObstacle(samplePoint, obstacle, gridTransform)) continue;
                    blockedLayer = obstacle.Layer;
                    return false;
                }
            }
            blockedLayer = default;
            return true;
        }

        private static FurnitureObstacle CreateObstacle(
            PlacedOfficeFurniture furniture,
            OfficeRuntimeOccupancyLayer layer,
            string interactionSeatId,
            OfficeFurnitureCollisionCatalog catalog)
        {
            OfficeFurnitureCollisionProfile profile = null;
            catalog?.TryResolve(
                furniture.KindId,
                furniture.Facing,
                furniture.Width,
                furniture.Height,
                out profile);
            return new FurnitureObstacle
            {
                Furniture = furniture,
                Profile = profile,
                Layer = layer,
                InteractionSeatId = interactionSeatId ?? string.Empty
            };
        }

        private bool PointInsideObstacle(
            Vector2 point,
            FurnitureObstacle obstacle,
            ContinuousGridTransform gridTransform)
        {
            if (!gridTransform.TryConvert(point, out float gridX, out float gridY))
                return false;
            PlacedOfficeFurniture furniture = obstacle.Furniture;
            float localX = gridX - furniture.Origin.X + 0.5f;
            float localY = gridY - furniture.Origin.Y + 0.5f;
            if (localX < 0f || localY < 0f || localX >= furniture.Width || localY >= furniture.Height)
                return false;
            if (obstacle.Profile == null) return true;
            int subcellX = Mathf.Min(
                furniture.Width * OfficeFurnitureCollisionCatalog.SubcellsPerCell - 1,
                Mathf.FloorToInt(localX * OfficeFurnitureCollisionCatalog.SubcellsPerCell));
            int subcellY = Mathf.Min(
                furniture.Height * OfficeFurnitureCollisionCatalog.SubcellsPerCell - 1,
                Mathf.FloorToInt(localY * OfficeFurnitureCollisionCatalog.SubcellsPerCell));
            return obstacle.Profile.IsOccupied(subcellX, subcellY);
        }

        private ContinuousGridTransform CaptureContinuousGridTransform()
        {
            Vector3 center00 = _presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector3 center10 = _grid.Width > 1
                ? _presenter.CellCenterWorld(new OfficeGridCoordinate(1, 0))
                : center00 + new Vector3(
                    OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                    OfficeGridTilemapPresenter.TileWorldHeight * 0.5f,
                    0f);
            Vector3 center01 = _grid.Height > 1
                ? _presenter.CellCenterWorld(new OfficeGridCoordinate(0, 1))
                : center00 + new Vector3(
                    -OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                    OfficeGridTilemapPresenter.TileWorldHeight * 0.5f,
                    0f);
            var origin = new Vector2(center00.x, center00.y);
            var basisX = new Vector2(center10.x - center00.x, center10.y - center00.y);
            var basisY = new Vector2(center01.x - center00.x, center01.y - center00.y);
            return new ContinuousGridTransform(origin, basisX, basisY);
        }

        private ActorState RequiredActor(string agentId)
        {
            if (!_actors.TryGetValue(agentId ?? string.Empty, out ActorState result))
                throw new InvalidOperationException("Runtime occupancy actor is not registered: " + agentId);
            return result;
        }

        private static string RequiredId(string value)
        {
            var canonical = (value ?? string.Empty).Trim();
            return canonical.Length == 0
                ? throw new ArgumentException("Agent ID is required.", nameof(value))
                : canonical;
        }
    }
}

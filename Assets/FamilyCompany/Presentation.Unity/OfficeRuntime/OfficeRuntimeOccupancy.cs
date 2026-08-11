using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
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
        private sealed class ActorState
        {
            public string AgentId;
            public Vector2 Position;
            public Vector2 DesiredVelocity;
            public float Radius;
            public float StuckSeconds;
            public OfficeGridCoordinate CurrentCell;
            public readonly HashSet<OfficeGridCoordinate> Reservations =
                new HashSet<OfficeGridCoordinate>();
        }

        private readonly HashSet<OfficeGridCoordinate> _hard = new HashSet<OfficeGridCoordinate>();
        private readonly Dictionary<OfficeGridCoordinate, string> _interactionSeats =
            new Dictionary<OfficeGridCoordinate, string>();
        private readonly Dictionary<OfficeGridCoordinate, string> _seatWorkSurfaceCells =
            new Dictionary<OfficeGridCoordinate, string>();
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
            _hard.Clear();
            _interactionSeats.Clear();
            _seatWorkSurfaceCells.Clear();
            _narrowCorridorIds.Clear();
            _narrowCorridorOwners.Clear();
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!grid.IsWalkable(cell)) _hard.Add(cell);
            }

            foreach (var furniture in grid.Furniture)
            {
                if (!furniture.BlocksMovement) continue;
                for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
                for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
                    _hard.Add(new OfficeGridCoordinate(x, y));
            }

            foreach (var seat in grid.SeatSlots) _interactionSeats[seat.Cell] = seat.SeatId;
            foreach (OfficeSeatSlot seat in grid.SeatSlots)
            {
                if (!seat.HasWorkstationBinding) continue;
                PlacedOfficeFurniture workSurface = grid.Furniture.FirstOrDefault(item =>
                    string.Equals(item.FurnitureId, seat.WorkSurfaceFurnitureId, StringComparison.Ordinal));
                if (workSurface == null) continue;
                for (var y = workSurface.Origin.Y; y < workSurface.Origin.Y + workSurface.Height; y++)
                for (var x = workSurface.Origin.X; x < workSurface.Origin.X + workSurface.Width; x++)
                    _seatWorkSurfaceCells[new OfficeGridCoordinate(x, y)] = seat.SeatId;
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

        public void UpdateActor(
            string agentId,
            Vector2 position,
            Vector2 desiredVelocity,
            float stuckSeconds,
            string permittedSeatId = "")
        {
            ActorState state = RequiredActor(agentId);
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
                if (ReferenceEquals(peer, state)) continue;
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
                    if (ReferenceEquals(peer, self)) continue;
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

        public bool IsCellPassable(
            OfficeGridCoordinate cell,
            string agentId,
            string permittedSeatId,
            bool includeDynamic)
        {
            if (_grid == null || !_grid.Contains(cell) || _hard.Contains(cell)) return false;
            if (_interactionSeats.TryGetValue(cell, out string seatId) &&
                !string.Equals(seatId, permittedSeatId, StringComparison.Ordinal)) return false;
            if (!includeDynamic) return true;
            foreach (ActorState peer in _actors.Values)
            {
                if (string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
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
                    if (string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
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

        public IReadOnlyList<OfficeTrafficAgentState> TrafficSnapshot()
        {
            return _actors.Values
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

        private bool IsStaticOpen(OfficeGridCoordinate cell) =>
            _grid.Contains(cell) && !_hard.Contains(cell) && !_interactionSeats.ContainsKey(cell);

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
            var offsets = new[]
            {
                Vector2.zero,
                new Vector2(radius, 0f), new Vector2(-radius, 0f),
                new Vector2(0f, radius), new Vector2(0f, -radius),
                new Vector2(radius * 0.7071f, radius * 0.7071f),
                new Vector2(-radius * 0.7071f, radius * 0.7071f),
                new Vector2(radius * 0.7071f, -radius * 0.7071f),
                new Vector2(-radius * 0.7071f, -radius * 0.7071f)
            };
            foreach (Vector2 offset in offsets)
            {
                OfficeGridCoordinate cell = _presenter.NearestCell(
                    new Vector3(point.x + offset.x, point.y + offset.y, 0f));
                if (!_grid.Contains(cell))
                {
                    blockedLayer = OfficeRuntimeOccupancyLayer.StaticHard;
                    return false;
                }
                if (_hard.Contains(cell))
                {
                    bool permittedWorkSurface = permittedSeatId.Length > 0 &&
                                                _seatWorkSurfaceCells.TryGetValue(cell, out string ownerSeatId) &&
                                                string.Equals(ownerSeatId, permittedSeatId, StringComparison.Ordinal);
                    if (!permittedWorkSurface)
                    {
                        blockedLayer = OfficeRuntimeOccupancyLayer.StaticHard;
                        return false;
                    }
                }
                if (_interactionSeats.TryGetValue(cell, out string seatId) &&
                    !string.Equals(seatId, permittedSeatId, StringComparison.Ordinal))
                {
                    blockedLayer = OfficeRuntimeOccupancyLayer.Interaction;
                    return false;
                }
            }
            blockedLayer = default;
            return true;
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

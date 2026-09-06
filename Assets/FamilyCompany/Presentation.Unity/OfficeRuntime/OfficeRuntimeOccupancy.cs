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
        private sealed class FurnitureObstacle
        {
            public PlacedOfficeFurniture Furniture;
            public OfficeFurnitureGeometryProfile CanonicalProfile;
            public OfficeRuntimeOccupancyLayer Layer;
            public string InteractionSeatId = string.Empty;
            public readonly HashSet<string> PermittedWorkSurfaceSeatIds =
                new HashSet<string>(StringComparer.Ordinal);

            public float ClearancePadding => 0f;

            public bool IsOccupied(int subcellX, int subcellY)
            {
                if (CanonicalProfile != null)
                    return CanonicalProfile.IsSolidGroundSubcell(subcellX, subcellY);
                return true;
            }

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

            public void GridRadiusExtents(float worldRadius, out float gridX, out float gridY)
            {
                float inverse = 1f / Mathf.Abs(Determinant);
                gridX = worldRadius * Mathf.Sqrt(
                    BasisY.y * BasisY.y + BasisY.x * BasisY.x) * inverse;
                gridY = worldRadius * Mathf.Sqrt(
                    BasisX.y * BasisX.y + BasisX.x * BasisX.x) * inverse;
            }

        }

        internal sealed class ActorState
        {
            public const int AtomicReservationCapacity = 16;
            public string AgentId;
            public Vector2 Position;
            public Vector2 DesiredVelocity;
            public float Radius;
            public float StaticRadius;
            // Extra radius applied to furniture obstacles only (never to hard floor or walls), so a
            // visible body whose arm swing reaches beyond StaticRadius stays out of desk geometry.
            public float FurnitureClearancePadding;
            public float StuckSeconds;
            public OfficeGridCoordinate CurrentCell;
            public bool IsPresent = true;
            public ulong Epoch;
            public readonly HashSet<OfficeGridCoordinate> Reservations =
                new HashSet<OfficeGridCoordinate>();
            public readonly OfficeGridCoordinate[] ReservationScopeBackup =
                new OfficeGridCoordinate[AtomicReservationCapacity];
            public readonly int[] ReservationScopeCorridorBackup = new int[3];
            public int ReservationScopeBackupCount;
            public int ReservationScopeCorridorCount;
            public ulong ReservationScopeEpoch;
            public bool ReservationScopeActive;
            public readonly OfficeGridCoordinate[] PlacementBackup =
                new OfficeGridCoordinate[AtomicReservationCapacity];
            public readonly int[] PlacementCorridorBackup = new int[3];
            public int PlacementBackupCount;
            public int PlacementCorridorCount;
            public Vector2 PlacementPosition;
            public Vector2 PlacementDesiredVelocity;
            public float PlacementStuckSeconds;
            public OfficeGridCoordinate PlacementCell;
            public ulong PlacementEpoch;
            public bool PlacementBackupActive;
        }

        internal readonly struct CanonicalActorSnapshot
        {
            public CanonicalActorSnapshot(
                string actorId,
                Vector2 position,
                Vector2 desiredVelocity,
                float stuckSeconds,
                float radius,
                OfficeGridCoordinate currentCell,
                bool isPresent,
                int reservationCount,
                ulong epoch,
                int revision)
            {
                ActorId = actorId;
                Position = position;
                DesiredVelocity = desiredVelocity;
                StuckSeconds = stuckSeconds;
                Radius = radius;
                CurrentCell = currentCell;
                IsPresent = isPresent;
                ReservationCount = reservationCount;
                Epoch = epoch;
                Revision = revision;
            }

            public string ActorId { get; }
            public Vector2 Position { get; }
            public Vector2 DesiredVelocity { get; }
            public float StuckSeconds { get; }
            public float Radius { get; }
            public OfficeGridCoordinate CurrentCell { get; }
            public bool IsPresent { get; }
            public int ReservationCount { get; }
            public ulong Epoch { get; }
            public int Revision { get; }
        }

        internal readonly struct PreparedAtomicActorPlacement
        {
            internal readonly ActorState _actor;

            internal PreparedAtomicActorPlacement(
                ActorState actor,
                Vector2 targetWorld,
                OfficeGridCoordinate targetCell,
                int capturedRevision,
                ulong capturedEpoch,
                bool reservationRequired,
                int corridor0,
                int corridor1,
                int corridor2,
                int corridorCount)
            {
                _actor = actor;
                TargetWorld = targetWorld;
                TargetCell = targetCell;
                CapturedRevision = capturedRevision;
                CapturedEpoch = capturedEpoch;
                ReservationRequired = reservationRequired;
                Corridor0 = corridor0;
                Corridor1 = corridor1;
                Corridor2 = corridor2;
                CorridorCount = corridorCount;
            }

            public Vector2 TargetWorld { get; }
            public OfficeGridCoordinate TargetCell { get; }
            public int CapturedRevision { get; }
            public ulong CapturedEpoch { get; }
            public bool ReservationRequired { get; }
            internal int Corridor0 { get; }
            internal int Corridor1 { get; }
            internal int Corridor2 { get; }
            internal int CorridorCount { get; }
        }

        internal readonly struct PreparedAtomicReservationScope
        {
            internal PreparedAtomicReservationScope(ActorState actor, ulong epoch)
            {
                _actor = actor;
                CapturedEpoch = epoch;
            }

            internal readonly ActorState _actor;
            public ulong CapturedEpoch { get; }
            public bool IsValid => _actor != null;
        }

        private static readonly Vector2[] CollisionDirections =
        {
            Vector2.zero,
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f)
        };
        private const float AgentContactTolerance = 0.01f;
        // CanMove intentionally permits its historical 0.01 contact tolerance. Recomputing the
        // same boundary after applying a refined displacement can differ by a few float ulps, so
        // the QA metric needs a smaller numerical epsilon before calling permitted contact a real
        // penetration. This does not enlarge the movement envelope.
        private const float AgentContactMetricEpsilon = 0.00001f;
        private static readonly Comparison<OfficeTrafficAgentState> TrafficAgentOrder =
            (left, right) => string.Compare(
                left.AgentId,
                right.AgentId,
                StringComparison.Ordinal);

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
        private readonly List<OfficeTrafficAgentState> _trafficSnapshot =
            new List<OfficeTrafficAgentState>(12);
        private readonly List<OfficeGridCoordinate> _reservationRequestBuffer =
            new List<OfficeGridCoordinate>(3);
        private readonly List<OfficeGridCoordinate> _singleUpcomingReservationBuffer =
            new List<OfficeGridCoordinate>(1);
        private readonly List<int> _corridorClaimBuffer = new List<int>(3);
        private readonly List<int> _corridorReleaseBuffer = new List<int>(4);
        private readonly Dictionary<OfficeGridCoordinate, int> _narrowCorridorIds =
            new Dictionary<OfficeGridCoordinate, int>();
        private readonly Dictionary<int, string> _narrowCorridorOwners =
            new Dictionary<int, string>();
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _presenter;
        private readonly Dictionary<string, AttendanceIngressClaim> _attendanceIngressClaims =
            new Dictionary<string, AttendanceIngressClaim>(StringComparer.Ordinal);
        private readonly struct AttendanceIngressClaim
        {
            public readonly Vector2 Exterior, Interior;
            public readonly float Radius;
            public AttendanceIngressClaim(Vector2 exterior, Vector2 interior, float radius)
            { Exterior = exterior; Interior = interior; Radius = radius; }
        }
        private string _qaInvalidatedAtomicAgentId = string.Empty;
        private ContinuousGridTransform _gridTransform;

        public int Revision { get; private set; }
        public int StaticViolationCount { get; private set; }
        public int InteractionViolationCount { get; private set; }
        public int AgentPenetrationCount { get; private set; }
        public int BlockedStaticMoveCount { get; private set; }
        public int BlockedInteractionMoveCount { get; private set; }
        public int BlockedAgentMoveCount { get; private set; }
        public int CanonicalGeometryObstacleCount { get; private set; }
        public int LegacyCollisionFallbackCount { get; private set; }
        public int FullCellFallbackCount { get; private set; }
        public string AttendanceIngressOwner => string.Join(",", _attendanceIngressClaims.Keys);
        public int AttendanceIngressClaimCount => _attendanceIngressClaims.Count;
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
            _gridTransform = CaptureContinuousGridTransform();
            _hardFloor.Clear();
            _furnitureObstacles.Clear();
            _interactionSeats.Clear();
            _profiledInteractionSeatIds.Clear();
            _narrowCorridorIds.Clear();
            _narrowCorridorOwners.Clear();
            _attendanceIngressClaims.Clear();
            _qaInvalidatedAtomicAgentId = string.Empty;
            CanonicalGeometryObstacleCount = 0;
            LegacyCollisionFallbackCount = 0;
            FullCellFallbackCount = 0;
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

            var obstaclesByFurnitureId = new Dictionary<string, FurnitureObstacle>(StringComparer.Ordinal);
            foreach (PlacedOfficeFurniture furniture in grid.Furniture)
            {
                if (!furniture.BlocksMovement) continue;
                var obstacle = CreateObstacle(
                    furniture,
                    OfficeRuntimeOccupancyLayer.StaticHard,
                    string.Empty);
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
                        seat.SeatId));
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

        public void RegisterActor(
            string agentId,
            Vector2 position,
            float radius,
            float staticRadius = -1f)
        {
            var canonical = RequiredId(agentId);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            float resolvedStaticRadius = staticRadius > 0f ? staticRadius : radius;
            if (float.IsNaN(resolvedStaticRadius) || float.IsInfinity(resolvedStaticRadius))
                throw new ArgumentOutOfRangeException(nameof(staticRadius));
            if (_actors.ContainsKey(canonical))
                throw new InvalidOperationException("Duplicate runtime occupancy actor: " + canonical);
            if (!PointClearsStatic(
                    position,
                    resolvedStaticRadius,
                    string.Empty,
                    out OfficeRuntimeOccupancyLayer blockedLayer))
                throw new InvalidOperationException(
                    $"Runtime actor '{canonical}' spawn intersects {blockedLayer} occupancy.");
            _actors.Add(canonical, new ActorState
            {
                AgentId = canonical,
                Position = position,
                Radius = radius,
                StaticRadius = resolvedStaticRadius,
                CurrentCell = _presenter.NearestCell(new Vector3(position.x, position.y, 0f))
            });
        }

        /// <summary>
        /// Furniture-only clearance added to the actor's static radius. Hard-floor and wall cells
        /// keep the plain radius so wall-adjacent rows stay walkable; the actor's own permitted seat
        /// desk stays exempt through <see cref="FurnitureObstacle.IsPermitted"/>.
        /// </summary>
        public void SetFurnitureClearancePadding(string agentId, float padding)
        {
            if (float.IsNaN(padding) || float.IsInfinity(padding) || padding < 0f)
                throw new ArgumentOutOfRangeException(nameof(padding));
            RequiredActor(agentId).FurnitureClearancePadding = padding;
        }

        /// <summary>
        /// True when a blocking (StaticHard) furniture footprint touches the 8-neighbourhood of
        /// the cell. The permitted seat's own work surface is ignored so seat approaches keep their
        /// natural final cell. Used by the path service to steer padded (wide-bodied) actors one
        /// cell away from desks whenever the layout leaves room.
        /// </summary>
        public bool HasBlockingFurnitureAdjacent(OfficeGridCoordinate cell, string permittedSeatId)
        {
            string permitted = permittedSeatId ?? string.Empty;
            foreach (FurnitureObstacle obstacle in _furnitureObstacles)
            {
                if (obstacle.Layer != OfficeRuntimeOccupancyLayer.StaticHard) continue;
                if (obstacle.IsPermitted(permitted)) continue;
                PlacedOfficeFurniture furniture = obstacle.Furniture;
                int minX = furniture.Origin.X - 1;
                int minY = furniture.Origin.Y - 1;
                int maxX = furniture.Origin.X + furniture.Width;
                int maxY = furniture.Origin.Y + furniture.Height;
                if (cell.X >= minX && cell.X <= maxX && cell.Y >= minY && cell.Y <= maxY)
                    return true;
            }
            return false;
        }

        public float FurnitureClearancePaddingOf(string agentId) =>
            _actors.TryGetValue(agentId ?? string.Empty, out ActorState state)
                ? state.FurnitureClearancePadding
                : 0f;

        public void UnregisterActor(string agentId)
        {
            ReleaseAttendanceIngress(agentId);
            ReleaseNarrowCorridors(agentId ?? string.Empty);
            _actors.Remove(agentId ?? string.Empty);
        }

        public bool IsActorPresent(string agentId) => RequiredActor(agentId).IsPresent;

        public void SetActorPresent(string agentId, bool isPresent)
        {
            ActorState state = RequiredActor(agentId);
            if (!isPresent)
            {
                ReleaseAttendanceIngress(state.AgentId);
                state.IsPresent = false;
                state.DesiredVelocity = Vector2.zero;
                state.StuckSeconds = 0f;
                state.Reservations.Clear();
                state.Epoch++;
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
            state.Epoch++;
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
                state.Epoch++;
                return;
            }
            bool insideClaimedIngress = PointInsideAttendanceIngress(state.AgentId, position, state.Radius);
            if (!insideClaimedIngress &&
                !PointClearsStatic(
                    position,
                    state.StaticRadius,
                    permittedSeatId,
                    out OfficeRuntimeOccupancyLayer blockedLayer,
                    state.FurnitureClearancePadding))
            {
                if (blockedLayer == OfficeRuntimeOccupancyLayer.Interaction) InteractionViolationCount++;
                else StaticViolationCount++;
            }
            state.Position = position;
            state.DesiredVelocity = desiredVelocity;
            state.StuckSeconds = Mathf.Max(0f, stuckSeconds);
            state.CurrentCell = _presenter.NearestCell(new Vector3(position.x, position.y, 0f));
            state.Epoch++;
            ReleaseExitedNarrowCorridors(state);
            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, state) || !peer.IsPresent) continue;
                float margin = Vector2.Distance(position, peer.Position) - (state.Radius + peer.Radius);
                MinimumAgentSeparationMargin = Mathf.Min(MinimumAgentSeparationMargin, margin);
                if (margin < -AgentContactTolerance - AgentContactMetricEpsilon)
                    AgentPenetrationCount++;
            }
        }

        public bool TryClaimAttendanceIngress(
            string agentId,
            Vector2 exterior,
            Vector2 interior,
            float radius)
        {
            ActorState actor = RequiredActor(agentId);
            if (actor.IsPresent || radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                return false;
            if (_attendanceIngressClaims.ContainsKey(actor.AgentId)) return false;
            if ((interior - exterior).sqrMagnitude <= 0.0001f) return false;
            foreach (var pending in _attendanceIngressClaims)
            {
                ActorState owner = RequiredActor(pending.Key);
                if (!owner.IsPresent && Vector2.Distance(pending.Value.Exterior, exterior) <
                    actor.Radius + owner.Radius + 0.06f) return false;
            }

            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, actor) || !peer.IsPresent) continue;
                float required = actor.Radius + peer.Radius + 0.06f;
                // Claim only this actor's corridor and safe spawn, not exclusive ownership of
                // the whole doorway. A following entrant still collision-checks every step.
                if (Vector2.Distance(peer.Position, exterior) < required) return false;
            }

            _attendanceIngressClaims.Add(actor.AgentId, new AttendanceIngressClaim(exterior, interior, radius));
            actor.Position = exterior;
            actor.CurrentCell = _presenter.NearestCell(new Vector3(exterior.x, exterior.y, 0f));
            return true;
        }

        public bool CanMoveAttendanceIngress(
            string agentId,
            Vector2 start,
            Vector2 end,
            float radius)
        {
            ActorState actor = RequiredActor(agentId);
            if (!actor.IsPresent ||
                !_attendanceIngressClaims.TryGetValue(actor.AgentId, out AttendanceIngressClaim claim) ||
                Mathf.Abs(radius - claim.Radius) > 0.0001f ||
                !PointInsideAttendanceIngress(actor.AgentId, start, radius) ||
                !PointInsideAttendanceIngress(actor.AgentId, end, radius))
                return false;

            Vector2 delta = end - start;
            int samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                OfficeGridCoordinate pointCell = _presenter.NearestCell(
                    new Vector3(point.x, point.y, 0f));
                foreach (ActorState peer in _actors.Values)
                {
                    if (ReferenceEquals(peer, actor) || !peer.IsPresent) continue;
                    if (peer.Reservations.Contains(pointCell) && !peer.CurrentCell.Equals(pointCell))
                        return false;
                    if (Vector2.Distance(point, peer.Position) <
                        actor.Radius + peer.Radius - AgentContactTolerance)
                        return false;
                }
            }
            return true;
        }

        public void ReleaseAttendanceIngress(string agentId)
        {
            _attendanceIngressClaims.Remove(agentId ?? string.Empty);
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
            List<OfficeGridCoordinate> requested = PrepareReservationRequest(current, upcoming);

            _corridorClaimBuffer.Clear();
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
                _corridorClaimBuffer.Add(corridorId);
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
                    foreach (int corridorId in _corridorClaimBuffer)
                        if (_narrowCorridorOwners.TryGetValue(corridorId, out string ownerId) &&
                            string.Equals(ownerId, self.AgentId, StringComparison.Ordinal))
                            _narrowCorridorOwners.Remove(corridorId);
                    return false;
                }
                break;
            }
            return true;
        }

        public bool TryReserveSingleCell(
            string agentId,
            OfficeGridCoordinate current,
            OfficeGridCoordinate upcoming)
        {
            _singleUpcomingReservationBuffer.Clear();
            _singleUpcomingReservationBuffer.Add(upcoming);
            bool reserved = TryReservePath(agentId, current, _singleUpcomingReservationBuffer);
            if (reserved) RequiredActor(agentId).Epoch++;
            return reserved;
        }

        internal bool TryBeginAtomicReservationScope(
            string agentId,
            out PreparedAtomicReservationScope scope)
        {
            scope = default;
            ActorState actor = RequiredActor(agentId);
            if (actor.ReservationScopeActive ||
                actor.Reservations.Count > ActorState.AtomicReservationCapacity)
                return false;
            actor.ReservationScopeBackupCount = 0;
            foreach (OfficeGridCoordinate cell in actor.Reservations)
                actor.ReservationScopeBackup[actor.ReservationScopeBackupCount++] = cell;
            actor.ReservationScopeCorridorCount = CaptureOwnedCorridors(
                actor.AgentId,
                actor.ReservationScopeCorridorBackup);
            if (actor.ReservationScopeCorridorCount > actor.ReservationScopeCorridorBackup.Length)
            {
                actor.ReservationScopeBackupCount = 0;
                actor.ReservationScopeCorridorCount = 0;
                return false;
            }
            actor.ReservationScopeEpoch = actor.Epoch;
            actor.ReservationScopeActive = true;
            scope = new PreparedAtomicReservationScope(actor, actor.Epoch);
            return true;
        }

        internal void RestoreAtomicReservationScope(in PreparedAtomicReservationScope scope)
        {
            ActorState actor = scope._actor;
            if (actor == null || !actor.ReservationScopeActive) return;
            RestoreReservationsAndCorridors(
                actor,
                actor.ReservationScopeBackup,
                actor.ReservationScopeBackupCount,
                actor.ReservationScopeCorridorBackup,
                actor.ReservationScopeCorridorCount,
                actor.ReservationScopeEpoch);
            actor.ReservationScopeActive = false;
        }

        internal void CompleteAtomicReservationScope(in PreparedAtomicReservationScope scope)
        {
            ActorState actor = scope._actor;
            if (actor != null) actor.ReservationScopeActive = false;
        }

        internal void InvalidateAtomicTokenForQa(string agentId)
        {
            RequiredActor(agentId);
            _qaInvalidatedAtomicAgentId = agentId;
        }

        /// <summary>
        /// Creates an engine-object-free occupancy owner for the executable production transaction
        /// fixture. The fixture then calls the same current/commit/rollback/complete methods as the
        /// game; it cannot enter navigation or collision code without a real presenter.
        /// </summary>
        internal static OfficeRuntimeOccupancy CreateProductionTransactionFixture(
            string agentId,
            Vector2 position,
            OfficeGridCoordinate cell,
            float radius,
            int revision)
        {
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            string canonical = RequiredId(agentId);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            var result = new OfficeRuntimeOccupancy { Revision = revision };
            result._actors.Add(canonical, new ActorState
            {
                AgentId = canonical,
                Position = position,
                DesiredVelocity = Vector2.zero,
                Radius = radius,
                StuckSeconds = 0f,
                CurrentCell = cell,
                IsPresent = true,
                Epoch = 1
            });
            return result;
        }

        internal bool TryPrepareProductionTransactionFixturePlacement(
            string agentId,
            Vector2 targetWorld,
            OfficeGridCoordinate targetCell,
            out PreparedAtomicActorPlacement prepared)
        {
            prepared = default;
            ActorState actor = RequiredActor(agentId);
            if (!actor.IsPresent || actor.PlacementBackupActive ||
                actor.Reservations.Count > ActorState.AtomicReservationCapacity) return false;
            actor.PlacementBackupCount = 0;
            foreach (OfficeGridCoordinate cell in actor.Reservations)
                actor.PlacementBackup[actor.PlacementBackupCount++] = cell;
            actor.PlacementCorridorCount = CaptureOwnedCorridors(
                actor.AgentId,
                actor.PlacementCorridorBackup);
            actor.PlacementPosition = actor.Position;
            actor.PlacementDesiredVelocity = actor.DesiredVelocity;
            actor.PlacementStuckSeconds = actor.StuckSeconds;
            actor.PlacementCell = actor.CurrentCell;
            actor.PlacementEpoch = actor.Epoch;
            actor.PlacementBackupActive = true;
            prepared = new PreparedAtomicActorPlacement(
                actor,
                targetWorld,
                targetCell,
                Revision,
                actor.Epoch,
                false,
                0,
                0,
                0,
                0);
            return true;
        }

        public void ClearReservations(string agentId)
        {
            if (_actors.TryGetValue(agentId ?? string.Empty, out ActorState actor))
            {
                actor.Reservations.Clear();
                actor.Epoch++;
            }
            ReleaseNarrowCorridors(agentId ?? string.Empty);
        }

        internal bool TryPrepareAtomicActorPlacement(
            string agentId,
            Vector2 targetWorld,
            OfficeGridCoordinate targetCell,
            float radius,
            string permittedSeatId,
            string requiredReservationOwner,
            int occupancyRevision,
            out PreparedAtomicActorPlacement prepared)
        {
            prepared = default;
            string permitted = permittedSeatId ?? string.Empty;
            bool targetIsPermittedSeat =
                permitted.Length > 0 &&
                _interactionSeats.TryGetValue(targetCell, out string interactionSeatId) &&
                string.Equals(interactionSeatId, permitted, StringComparison.Ordinal);
            if (occupancyRevision != Revision ||
                radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius) ||
                !_actors.TryGetValue(agentId ?? string.Empty, out ActorState actor) ||
                !actor.IsPresent ||
                !_grid.Contains(targetCell) ||
                (!_grid.IsWalkable(targetCell) && !targetIsPermittedSeat) ||
                !_presenter.NearestCell(new Vector3(targetWorld.x, targetWorld.y, 0f)).Equals(targetCell) ||
                !PointClearsStatic(
                    targetWorld,
                    radius,
                    permitted,
                    out _,
                    actor.FurnitureClearancePadding)) return false;

            bool reservationRequired = !string.IsNullOrEmpty(requiredReservationOwner);
            if (reservationRequired &&
                (!string.Equals(requiredReservationOwner, actor.AgentId, StringComparison.Ordinal) ||
                 !actor.Reservations.Contains(targetCell))) return false;

            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, actor) || !peer.IsPresent) continue;
                if (peer.Reservations.Contains(targetCell) && !peer.CurrentCell.Equals(targetCell))
                    return false;
                if (Vector2.Distance(targetWorld, peer.Position) <
                    actor.Radius + peer.Radius - AgentContactTolerance) return false;
            }

            int corridor0 = 0;
            int corridor1 = 0;
            int corridor2 = 0;
            int corridorCount = 0;
            foreach (KeyValuePair<int, string> item in _narrowCorridorOwners)
            {
                if (!string.Equals(item.Value, actor.AgentId, StringComparison.Ordinal)) continue;
                if (corridorCount == 0) corridor0 = item.Key;
                else if (corridorCount == 1) corridor1 = item.Key;
                else if (corridorCount == 2) corridor2 = item.Key;
                else return false;
                corridorCount++;
            }

            if (actor.PlacementBackupActive ||
                actor.Reservations.Count > ActorState.AtomicReservationCapacity) return false;
            actor.PlacementBackupCount = 0;
            foreach (OfficeGridCoordinate cell in actor.Reservations)
                actor.PlacementBackup[actor.PlacementBackupCount++] = cell;
            actor.PlacementCorridorCount = CaptureOwnedCorridors(
                actor.AgentId,
                actor.PlacementCorridorBackup);
            actor.PlacementPosition = actor.Position;
            actor.PlacementDesiredVelocity = actor.DesiredVelocity;
            actor.PlacementStuckSeconds = actor.StuckSeconds;
            actor.PlacementCell = actor.CurrentCell;
            actor.PlacementEpoch = actor.Epoch;
            actor.PlacementBackupActive = true;

            prepared = new PreparedAtomicActorPlacement(
                actor,
                targetWorld,
                targetCell,
                Revision,
                actor.Epoch,
                reservationRequired,
                corridor0,
                corridor1,
                corridor2,
                corridorCount);
            return true;
        }

        internal bool IsPreparedAtomicActorPlacementCurrent(
            in PreparedAtomicActorPlacement prepared)
        {
            ActorState actor = prepared._actor;
            if (actor != null && string.Equals(
                    _qaInvalidatedAtomicAgentId,
                    actor.AgentId,
                    StringComparison.Ordinal))
            {
                _qaInvalidatedAtomicAgentId = string.Empty;
                return false;
            }
            return actor != null &&
                   prepared.CapturedRevision == Revision &&
                   prepared.CapturedEpoch == actor.Epoch &&
                   actor.IsPresent &&
                   (!prepared.ReservationRequired || actor.Reservations.Contains(prepared.TargetCell));
        }

        internal void CommitPreparedAtomicActorPlacement(
            in PreparedAtomicActorPlacement prepared)
        {
            ActorState actor = prepared._actor;
            if (!IsPreparedAtomicActorPlacementCurrent(prepared) || !actor.PlacementBackupActive)
                throw new InvalidOperationException("Prepared atomic placement became stale before commit.");
            actor.Position = prepared.TargetWorld;
            actor.DesiredVelocity = Vector2.zero;
            actor.StuckSeconds = 0f;
            actor.CurrentCell = prepared.TargetCell;
            actor.Reservations.Clear();
            if (prepared.CorridorCount > 0) _narrowCorridorOwners.Remove(prepared.Corridor0);
            if (prepared.CorridorCount > 1) _narrowCorridorOwners.Remove(prepared.Corridor1);
            if (prepared.CorridorCount > 2) _narrowCorridorOwners.Remove(prepared.Corridor2);
            actor.Epoch++;
        }

        internal void CompletePreparedAtomicActorPlacement(
            in PreparedAtomicActorPlacement prepared)
        {
            if (prepared._actor != null) prepared._actor.PlacementBackupActive = false;
        }

        internal void CancelPreparedAtomicActorPlacement(
            in PreparedAtomicActorPlacement prepared)
        {
            if (prepared._actor != null) prepared._actor.PlacementBackupActive = false;
        }

        internal void RollbackPreparedAtomicActorPlacement(
            in PreparedAtomicActorPlacement prepared)
        {
            ActorState actor = prepared._actor;
            if (actor == null || !actor.PlacementBackupActive) return;
            actor.Position = actor.PlacementPosition;
            actor.DesiredVelocity = actor.PlacementDesiredVelocity;
            actor.StuckSeconds = actor.PlacementStuckSeconds;
            actor.CurrentCell = actor.PlacementCell;
            RestoreReservationsAndCorridors(
                actor,
                actor.PlacementBackup,
                actor.PlacementBackupCount,
                actor.PlacementCorridorBackup,
                actor.PlacementCorridorCount,
                actor.PlacementEpoch);
            actor.PlacementBackupActive = false;
        }

        internal CanonicalActorSnapshot CaptureCanonicalActorSnapshot(string agentId)
        {
            ActorState actor = RequiredActor(agentId);
            return new CanonicalActorSnapshot(
                actor.AgentId,
                actor.Position,
                actor.DesiredVelocity,
                actor.StuckSeconds,
                actor.Radius,
                actor.CurrentCell,
                actor.IsPresent,
                actor.Reservations.Count,
                actor.Epoch,
                Revision);
        }

        internal void ObserveAtomicPlacementClearance(
            string agentId,
            Vector2 point,
            OfficeGridCoordinate cell,
            float radius,
            string permittedSeatId,
            out bool floorValid,
            out bool staticOverlap,
            out bool dynamicOverlap)
        {
            ActorState self = RequiredActor(agentId);
            floorValid = _grid != null && _grid.Contains(cell) &&
                         (_grid.IsWalkable(cell) ||
                          (!string.IsNullOrEmpty(permittedSeatId) &&
                           _interactionSeats.TryGetValue(cell, out string seatId) &&
                           string.Equals(seatId, permittedSeatId, StringComparison.Ordinal)));
            staticOverlap = !PointClearsStatic(
                point,
                radius,
                permittedSeatId ?? string.Empty,
                out _,
                self.FurnitureClearancePadding);
            dynamicOverlap = false;
            foreach (ActorState peer in _actors.Values)
            {
                if (ReferenceEquals(peer, self) || !peer.IsPresent) continue;
                if (Vector2.Distance(point, peer.Position) <
                    self.Radius + peer.Radius - AgentContactTolerance)
                {
                    dynamicOverlap = true;
                    break;
                }
            }
        }

        public bool HasReservation(string agentId, OfficeGridCoordinate cell) =>
            RequiredActor(agentId).Reservations.Contains(cell);

        public string DescribePathReservationBlocker(
            string agentId,
            OfficeGridCoordinate current,
            IReadOnlyList<OfficeGridCoordinate> upcoming)
        {
            ActorState self = RequiredActor(agentId);
            List<OfficeGridCoordinate> requested = PrepareReservationRequest(current, upcoming);
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
            ActorState self = RequiredActor(agentId);
            var delta = end - start;
            var samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                if (!PointClearsStatic(
                        point, radius, permittedSeatId, out OfficeRuntimeOccupancyLayer blockedLayer,
                        self.FurnitureClearancePadding))
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
                    float margin = Vector2.Distance(point, peer.Position) -
                                   (self.Radius + peer.Radius);
                    if (margin >= -AgentContactTolerance) continue;
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
            ActorState self = RequiredActor(agentId);
            var delta = end - start;
            var samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.045f));
            for (var sample = 1; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
                if (!PointClearsStatic(
                        point, radius, permittedSeatId, out OfficeRuntimeOccupancyLayer layer,
                        self.FurnitureClearancePadding))
                    return "static=" + DescribeStaticBlocker(point, radius, permittedSeatId, layer);
                OfficeGridCoordinate pointCell = _presenter.NearestCell(new Vector3(point.x, point.y, 0f));
                foreach (ActorState peer in _actors.Values)
                {
                    if (!peer.IsPresent || string.Equals(peer.AgentId, agentId, StringComparison.Ordinal)) continue;
                    if (peer.Reservations.Contains(pointCell) && !peer.CurrentCell.Equals(pointCell))
                        return $"peer={peer.AgentId}:reserved={pointCell}";
                    float margin = Vector2.Distance(point, peer.Position) -
                                   (self.Radius + peer.Radius);
                    if (margin < -AgentContactTolerance)
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
            ContinuousGridTransform gridTransform = _gridTransform;
            foreach (FurnitureObstacle obstacle in _furnitureObstacles)
            {
                if (obstacle.IsPermitted(permitted)) continue;
                float expandedRadius = radius + obstacle.ClearancePadding;
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
                float required = self.Radius + peer.Radius + extraClearance;
                if (DistanceToSegment(peer.Position, start, end) < required) return false;
            }
            return true;
        }

        public IReadOnlyList<OfficeTrafficAgentState> TrafficSnapshot()
        {
            _trafficSnapshot.Clear();
            foreach (ActorState item in _actors.Values)
            {
                if (!item.IsPresent) continue;
                _trafficSnapshot.Add(new OfficeTrafficAgentState(
                    item.AgentId,
                    new OfficeNavPoint(item.Position.x, item.Position.y),
                    new OfficeNavPoint(item.DesiredVelocity.x, item.DesiredVelocity.y),
                    item.Radius,
                    item.StuckSeconds));
            }
            _trafficSnapshot.Sort(TrafficAgentOrder);
            return _trafficSnapshot;
        }

        private List<OfficeGridCoordinate> PrepareReservationRequest(
            OfficeGridCoordinate current,
            IReadOnlyList<OfficeGridCoordinate> upcoming)
        {
            _reservationRequestBuffer.Clear();
            _reservationRequestBuffer.Add(current);
            if (upcoming == null) return _reservationRequestBuffer;
            for (var index = 0; index < upcoming.Count && index < 2; index++)
            {
                OfficeGridCoordinate cell = upcoming[index];
                if (!_reservationRequestBuffer.Contains(cell))
                    _reservationRequestBuffer.Add(cell);
            }
            return _reservationRequestBuffer;
        }

        public OfficeGridCoordinate CurrentCell(string agentId) => RequiredActor(agentId).CurrentCell;

        private bool PointInsideAttendanceIngress(string agentId, Vector2 point, float radius)
        {
            if (!_attendanceIngressClaims.TryGetValue(agentId, out AttendanceIngressClaim claim)) return false;
            float tolerance = Mathf.Min(0.04f, Mathf.Max(0.01f, radius * 0.20f));
            return DistanceToSegment(
                       point,
                       claim.Exterior,
                       claim.Interior) <= tolerance;
        }

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
            _corridorReleaseBuffer.Clear();
            foreach (KeyValuePair<int, string> item in _narrowCorridorOwners)
            {
                if (!string.Equals(item.Value, actor.AgentId, StringComparison.Ordinal)) continue;
                bool currentInside = _narrowCorridorIds.TryGetValue(actor.CurrentCell, out int currentId) &&
                                     currentId == item.Key;
                bool reservedInside = false;
                foreach (OfficeGridCoordinate cell in actor.Reservations)
                {
                    if (!_narrowCorridorIds.TryGetValue(cell, out int reservedId) ||
                        reservedId != item.Key) continue;
                    reservedInside = true;
                    break;
                }
                if (!currentInside && !reservedInside) _corridorReleaseBuffer.Add(item.Key);
            }
            foreach (int corridorId in _corridorReleaseBuffer)
                _narrowCorridorOwners.Remove(corridorId);
        }

        private void ReleaseNarrowCorridors(string agentId)
        {
            _corridorReleaseBuffer.Clear();
            foreach (KeyValuePair<int, string> item in _narrowCorridorOwners)
                if (string.Equals(item.Value, agentId, StringComparison.Ordinal))
                    _corridorReleaseBuffer.Add(item.Key);
            foreach (int corridorId in _corridorReleaseBuffer)
                _narrowCorridorOwners.Remove(corridorId);
        }

        private bool PointClearsStatic(
            Vector2 point,
            float radius,
            string permittedSeatId,
            out OfficeRuntimeOccupancyLayer blockedLayer,
            float furniturePadding = 0f)
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

            ContinuousGridTransform gridTransform = _gridTransform;
            if (!gridTransform.TryConvert(point, out float pointGridX, out float pointGridY))
            {
                blockedLayer = OfficeRuntimeOccupancyLayer.StaticHard;
                return false;
            }
            foreach (FurnitureObstacle obstacle in _furnitureObstacles)
            {
                if (obstacle.IsPermitted(permitted)) continue;
                float expandedRadius = radius + obstacle.ClearancePadding + furniturePadding;
                gridTransform.GridRadiusExtents(
                    expandedRadius,
                    out float gridRadiusX,
                    out float gridRadiusY);
                if (!CouldIntersectObstacle(
                        pointGridX,
                        pointGridY,
                        gridRadiusX,
                        gridRadiusY,
                        obstacle)) continue;
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

        private static bool CouldIntersectObstacle(
            float pointGridX,
            float pointGridY,
            float gridRadiusX,
            float gridRadiusY,
            FurnitureObstacle obstacle)
        {
            PlacedOfficeFurniture furniture = obstacle.Furniture;
            float minimumX = furniture.Origin.X - 0.5f;
            float minimumY = furniture.Origin.Y - 0.5f;
            float maximumX = minimumX + furniture.Width;
            float maximumY = minimumY + furniture.Height;
            return pointGridX + gridRadiusX >= minimumX &&
                   pointGridX - gridRadiusX <= maximumX &&
                   pointGridY + gridRadiusY >= minimumY &&
                   pointGridY - gridRadiusY <= maximumY;
        }

        private FurnitureObstacle CreateObstacle(
            PlacedOfficeFurniture furniture,
            OfficeRuntimeOccupancyLayer layer,
            string interactionSeatId)
        {
            OfficeFurnitureGeometryProfile canonicalProfile = null;
            if (OfficeFurnitureGeometryQuery.Shared.TryResolve(
                    furniture.KindId,
                    furniture.Origin,
                    furniture.Facing,
                    out OfficeFurnitureGeometrySnapshot geometry))
            {
                if (geometry.Profile.FootprintWidth != furniture.Width ||
                    geometry.Profile.FootprintHeight != furniture.Height)
                    throw new InvalidOperationException(
                        $"Furniture '{furniture.FurnitureId}' footprint {furniture.Width}x{furniture.Height} " +
                        $"does not match canonical geometry " +
                        $"{geometry.Profile.FootprintWidth}x{geometry.Profile.FootprintHeight}.");
                canonicalProfile = geometry.Profile;
                CanonicalGeometryObstacleCount++;
            }

            // A kind/facing absent from the canonical query is legacy or unknown save content.
            // Keep it as a full semantic rectangle: a partial legacy profile could create a new
            // visible pass-through during migration, while the conservative rectangle preserves
            // the pre-geometry collision contract until that content receives canonical geometry.
            if (canonicalProfile == null) FullCellFallbackCount++;
            return new FurnitureObstacle
            {
                Furniture = furniture,
                CanonicalProfile = canonicalProfile,
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
            if (obstacle.CanonicalProfile == null) return true;
            int subcellX = Mathf.Min(
                furniture.Width * OfficeFurnitureGeometryProfile.SubcellsPerCell - 1,
                Mathf.FloorToInt(localX * OfficeFurnitureGeometryProfile.SubcellsPerCell));
            int subcellY = Mathf.Min(
                furniture.Height * OfficeFurnitureGeometryProfile.SubcellsPerCell - 1,
                Mathf.FloorToInt(localY * OfficeFurnitureGeometryProfile.SubcellsPerCell));
            return obstacle.IsOccupied(subcellX, subcellY);
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

        private int CaptureOwnedCorridors(string agentId, int[] target)
        {
            int count = 0;
            foreach (KeyValuePair<int, string> item in _narrowCorridorOwners)
            {
                if (!string.Equals(item.Value, agentId, StringComparison.Ordinal)) continue;
                if (count >= target.Length) return target.Length + 1;
                target[count++] = item.Key;
            }
            return count;
        }

        private void RestoreReservationsAndCorridors(
            ActorState actor,
            OfficeGridCoordinate[] reservations,
            int reservationCount,
            int[] corridors,
            int corridorCount,
            ulong epoch)
        {
            actor.Reservations.Clear();
            for (var index = 0; index < reservationCount; index++)
                actor.Reservations.Add(reservations[index]);
            ReleaseNarrowCorridors(actor.AgentId);
            for (var index = 0; index < corridorCount; index++)
                _narrowCorridorOwners[corridors[index]] = actor.AgentId;
            actor.Epoch = epoch;
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

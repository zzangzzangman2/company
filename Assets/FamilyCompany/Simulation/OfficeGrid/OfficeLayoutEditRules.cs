using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeLayoutEditFailure
    {
        None = 0,
        UnknownTarget = 1,
        OutOfBounds = 2,
        OverlapsFurniture = 3,
        NotOnFloor = 4,
        SeatBroken = 5,
        RotationUnsupported = 6,
        NothingToDo = 7,
        EntranceBlocked = 8,
        PathDisconnected = 9,
        AccessBlocked = 10,
        RequiredWorkstation = 11,
        InvalidDefinition = 12
    }

    public sealed class OfficeLayoutEditResult
    {
        private OfficeLayoutEditResult(OfficeGrid grid, OfficeLayoutEditFailure failure, string message)
        {
            Grid = grid;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public OfficeGrid Grid { get; }
        public OfficeLayoutEditFailure Failure { get; }
        public string Message { get; }
        public bool Success => Failure == OfficeLayoutEditFailure.None;

        public static OfficeLayoutEditResult Ok(OfficeGrid grid) =>
            new OfficeLayoutEditResult(
                grid ?? throw new ArgumentNullException(nameof(grid)),
                OfficeLayoutEditFailure.None,
                string.Empty);

        public static OfficeLayoutEditResult Fail(OfficeLayoutEditFailure failure, string message) =>
            new OfficeLayoutEditResult(null, failure, message);
    }

    /// <summary>
    /// Every edit the layout editor can perform, as pure grid to grid transforms.
    ///
    /// The point of putting these here rather than in the editor UI is that one move has to change
    /// the rendered position, the collision footprint, the seat cell, the approach cell and the
    /// operator anchor together or not at all. A move that only shifted the sprite would put the
    /// player's furniture somewhere the pathfinder still treats as empty floor.
    ///
    /// Validity is decided by rebuilding the grid: <see cref="OfficeGrid"/> already refuses a seat
    /// that is not walkable, an approach cell that is not cardinally adjacent, a chair that does not
    /// match its seat and blocking furniture standing on walkable floor. Overlap between two
    /// blocking pieces is the one rule it cannot see, so it is checked here.
    /// </summary>
    public static class OfficeLayoutEditRules
    {
        public static readonly OfficeGridCoordinate CanonicalInteriorEntrance =
            new OfficeGridCoordinate(8, 1);

        public static OfficeLayoutEditResult PlaceFurniture(
            OfficeGrid grid,
            string instanceId,
            string definitionId,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing facing)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (Find(grid, instanceId) != null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.OverlapsFurniture, "같은 인스턴스 ID의 가구가 이미 배치되어 있습니다.");
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(definitionId);
            if (definition == null || !definition.IsPlayerEditable)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.InvalidDefinition, "구매·배치할 수 없는 가구 종류입니다.");
            OfficeGridCoordinate footprint = definition.FootprintFor(facing);
            var placed = new PlacedOfficeFurniture(
                instanceId,
                definition.DefinitionId,
                origin,
                footprint.X,
                footprint.Y,
                facing,
                definition.BlocksNavigation);
            return Rebuild(
                grid,
                grid.Furniture.Concat(new[] { placed }).ToList(),
                grid.SeatSlots.ToList());
        }

        public static OfficeLayoutEditResult MoveFurniture(
            OfficeGrid grid,
            string furnitureId,
            int deltaX,
            int deltaY)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (deltaX == 0 && deltaY == 0)
                return OfficeLayoutEditResult.Fail(OfficeLayoutEditFailure.NothingToDo, "이동 거리가 0입니다.");
            PlacedOfficeFurniture target = Find(grid, furnitureId);
            if (target == null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, $"가구 '{furnitureId}'가 없습니다.");
            if (grid.SeatSlots.Any(seat =>
                    Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId)))
            {
                OfficeSeatSlot owner = grid.SeatSlots.First(seat =>
                    Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId));
                return MoveWorkstation(grid, owner.SeatId, deltaX, deltaY);
            }
            return Rebuild(
                grid,
                grid.Furniture.Select(item => Same(item.FurnitureId, furnitureId)
                    ? Translate(item, deltaX, deltaY)
                    : item).ToList(),
                grid.SeatSlots.ToList());
        }

        /// <summary>
        /// Moves a desk, its chair, the seat cell, the approach cell and the operator anchor as one
        /// object. Partial results are impossible: either every part lands on valid floor or the
        /// original grid is returned untouched.
        /// </summary>
        public static OfficeLayoutEditResult MoveWorkstation(
            OfficeGrid grid,
            string seatId,
            int deltaX,
            int deltaY)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (deltaX == 0 && deltaY == 0)
                return OfficeLayoutEditResult.Fail(OfficeLayoutEditFailure.NothingToDo, "이동 거리가 0입니다.");
            OfficeSeatSlot seat = grid.SeatSlots.FirstOrDefault(item => Same(item.SeatId, seatId));
            if (seat == null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, $"좌석 '{seatId}'가 없습니다.");

            var moved = new HashSet<string>(StringComparer.Ordinal) { seat.ChairFurnitureId };
            if (seat.HasWorkstationBinding) moved.Add(seat.WorkSurfaceFurnitureId);
            List<PlacedOfficeFurniture> furniture = grid.Furniture
                .Select(item => moved.Contains(item.FurnitureId) ? Translate(item, deltaX, deltaY) : item)
                .ToList();
            List<OfficeSeatSlot> seats = grid.SeatSlots
                .Select(item => Same(item.SeatId, seatId) ? Translate(item, deltaX, deltaY) : item)
                .ToList();
            return Rebuild(grid, furniture, seats);
        }

        public static OfficeLayoutEditResult RemoveFurniture(OfficeGrid grid, string furnitureId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            PlacedOfficeFurniture target = Find(grid, furnitureId);
            if (target == null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, $"가구 '{furnitureId}'가 없습니다.");
            OfficeSeatSlot owner = grid.SeatSlots.FirstOrDefault(seat =>
                Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId));
            if (owner != null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.RequiredWorkstation,
                    "가족 4명의 지정 워크스테이션은 보관하거나 판매할 수 없습니다.");
            var dropFurniture = new HashSet<string>(StringComparer.Ordinal) { furnitureId };
            return Rebuild(
                grid,
                grid.Furniture.Where(item => !dropFurniture.Contains(item.FurnitureId)).ToList(),
                grid.SeatSlots.ToList());
        }

        /// <summary>
        /// Returns the horizontal mirror facing. Presentation may use this as an authored-art
        /// fallback, while semantic rotation always advances by a full quarter turn below.
        /// </summary>
        public static OfficeFurnitureFacing Mirror(OfficeFurnitureFacing facing)
        {
            switch (facing)
            {
                case OfficeFurnitureFacing.SouthEast: return OfficeFurnitureFacing.SouthWest;
                case OfficeFurnitureFacing.SouthWest: return OfficeFurnitureFacing.SouthEast;
                case OfficeFurnitureFacing.NorthWest: return OfficeFurnitureFacing.NorthEast;
                default: return OfficeFurnitureFacing.NorthWest;
            }
        }

        public static OfficeFurnitureFacing QuarterTurnClockwise(OfficeFurnitureFacing facing) =>
            (OfficeFurnitureFacing)(((int)facing + 1) & 3);

        /// <summary>
        /// Turns a free-standing piece by 90 degrees, including its footprint. Workstation members
        /// are promoted to the atomic desk/chair/seat rotation below.
        /// </summary>
        public static OfficeLayoutEditResult RotateFurniture(OfficeGrid grid, string furnitureId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            PlacedOfficeFurniture target = Find(grid, furnitureId);
            if (target == null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, $"가구 '{furnitureId}'가 없습니다.");
            OfficeSeatSlot workstationSeat = grid.SeatSlots.FirstOrDefault(seat =>
                Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId));
            if (workstationSeat != null) return RotateWorkstation(grid, workstationSeat.SeatId);

            var rotated = new PlacedOfficeFurniture(
                target.FurnitureId,
                target.KindId,
                target.Origin,
                target.Height,
                target.Width,
                QuarterTurnClockwise(target.Facing),
                target.BlocksMovement);
            return Rebuild(
                grid,
                grid.Furniture.Select(item => Same(item.FurnitureId, furnitureId) ? rotated : item).ToList(),
                grid.SeatSlots.ToList());
        }

        public static OfficeLayoutEditResult RotateWorkstation(OfficeGrid grid, string seatId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            OfficeSeatSlot seat = grid.SeatSlots.FirstOrDefault(item => Same(item.SeatId, seatId));
            if (seat == null || !seat.HasWorkstationBinding)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, "회전할 워크스테이션이 없습니다.");

            var memberIds = new HashSet<string>(StringComparer.Ordinal)
            {
                seat.ChairFurnitureId,
                seat.WorkSurfaceFurnitureId
            };
            List<PlacedOfficeFurniture> furniture = grid.Furniture.Select(item =>
                memberIds.Contains(item.FurnitureId) ? RotateAroundCell(item, seat.Cell) : item).ToList();
            OfficeGridCoordinate approach = RotateCellClockwise(seat.ApproachCell, seat.Cell);
            OfficeGridSubcellAnchor operatorAnchor = RotateAnchorClockwise(seat.OperatorAnchor, seat.Cell);
            OfficeFurnitureFacing facing = QuarterTurnClockwise(seat.Facing);
            List<OfficeSeatSlot> seats = grid.SeatSlots.Select(item => Same(item.SeatId, seatId)
                ? new OfficeSeatSlot(
                    item.SeatId,
                    item.ChairFurnitureId,
                    item.WorkSurfaceFurnitureId,
                    item.Cell,
                    approach,
                    operatorAnchor,
                    facing)
                : item).ToList();
            return Rebuild(grid, furniture, seats);
        }

        /// <summary>True when the editor should offer the rotate button for this piece.</summary>
        public static bool CanRotate(OfficeGrid grid, string furnitureId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return Find(grid, furnitureId) != null;
        }

        /// <summary>Cells a piece of furniture would occupy after a move, for the editor overlay.</summary>
        public static IReadOnlyList<OfficeGridCoordinate> FootprintCells(PlacedOfficeFurniture furniture)
        {
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            var cells = new List<OfficeGridCoordinate>(furniture.Width * furniture.Height);
            for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
            for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
                cells.Add(new OfficeGridCoordinate(x, y));
            return cells;
        }

        /// <summary>The floor a cell would have if no furniture stood on it.</summary>
        public static bool[] BaseFloorMask(OfficeGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            bool[] mask = grid.CopyWalkable();
            foreach (PlacedOfficeFurniture item in grid.Furniture)
            {
                if (!item.BlocksMovement) continue;
                if (OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable != true) continue;
                foreach (OfficeGridCoordinate cell in FootprintCells(item))
                {
                    if (!grid.Contains(cell)) continue;
                    mask[cell.Y * grid.Width + cell.X] =
                        grid.FloorAt(cell) != OfficeFloorTileKind.Void;
                }
            }
            return mask;
        }

        private static OfficeLayoutEditResult Rebuild(
            OfficeGrid source,
            IReadOnlyList<PlacedOfficeFurniture> furniture,
            IReadOnlyList<OfficeSeatSlot> seats)
        {
            foreach (PlacedOfficeFurniture item in furniture)
            {
                var maxX = item.Origin.X + item.Width - 1;
                var maxY = item.Origin.Y + item.Height - 1;
                if (!source.Contains(item.Origin) || !source.Contains(new OfficeGridCoordinate(maxX, maxY)))
                    return OfficeLayoutEditResult.Fail(
                        OfficeLayoutEditFailure.OutOfBounds, $"'{item.FurnitureId}'가 사무실 밖으로 나갑니다.");
            }

            bool[] walkable = BaseFloorMask(source);
            bool[] buildable = (bool[])walkable.Clone();
            var claimed = new Dictionary<int, string>();
            foreach (PlacedOfficeFurniture item in furniture)
            {
                bool playerEditable = OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true;
                foreach (OfficeGridCoordinate cell in FootprintCells(item))
                {
                    int index = cell.Y * source.Width + cell.X;
                    if (playerEditable && !buildable[index])
                        return OfficeLayoutEditResult.Fail(
                            OfficeLayoutEditFailure.NotOnFloor,
                            $"'{item.FurnitureId}'의 footprint 전체가 실내 walkable floor cell 안에 있어야 합니다.");
                    // Structural wall bays can share a corner anchor. They are outside the
                    // buildable interior and are owned by the parallel wall task, so only
                    // player-editable furniture participates in placement occupancy here.
                    if (!playerEditable) continue;
                    if (claimed.TryGetValue(index, out string other))
                        return OfficeLayoutEditResult.Fail(
                            OfficeLayoutEditFailure.OverlapsFurniture,
                            $"'{item.FurnitureId}'가 '{other}'와 겹칩니다.");
                    claimed.Add(index, item.FurnitureId);
                    if (item.BlocksMovement) walkable[index] = false;
                }
            }

            try
            {
                var candidate = new OfficeGrid(
                    source.Width,
                    source.Height,
                    source.CopyFloorTiles(),
                    walkable,
                    furniture,
                    seats);
                OfficeLayoutEditResult topology = ValidateTopology(candidate);
                return topology.Success ? OfficeLayoutEditResult.Ok(candidate) : topology;
            }
            catch (ArgumentException exception)
            {
                return OfficeLayoutEditResult.Fail(OfficeLayoutEditFailure.SeatBroken, exception.Message);
            }
        }

        public static IReadOnlyList<OfficeGridCoordinate> AccessCells(
            OfficeGrid grid,
            PlacedOfficeFurniture furniture)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(furniture.KindId);
            if (definition == null || definition.AccessPolicy == OfficeFurnitureAccessPolicy.None)
                return Array.Empty<OfficeGridCoordinate>();

            if (OfficeFurnitureGeometryQuery.Shared.TryResolve(
                    definition.DefinitionId,
                    furniture.Origin,
                    furniture.Facing,
                    out OfficeFurnitureGeometrySnapshot geometry))
            {
                var authored = new HashSet<OfficeGridCoordinate>();
                foreach (OfficeFurnitureWorldSocket socket in geometry.InteractionAccessSockets)
                    AddWalkable(grid, authored, socket.WorldCell);
                return authored.OrderBy(item => item.Y).ThenBy(item => item.X).ToList();
            }

            // Legacy-only structural definitions have no build-editor geometry. Keep the old
            // cardinal fallback isolated here; player-editable catalog entries never take it.
            var cells = new HashSet<OfficeGridCoordinate>();
            foreach (OfficeGridCoordinate footprint in FootprintCells(furniture))
            {
                AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X + 1, footprint.Y));
                AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X - 1, footprint.Y));
                AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X, footprint.Y + 1));
                AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X, footprint.Y - 1));
            }
            if (definition.AccessPolicy == OfficeFurnitureAccessPolicy.AdjacentOrTwoCells)
            {
                foreach (OfficeGridCoordinate footprint in FootprintCells(furniture))
                {
                    AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X + 2, footprint.Y));
                    AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X - 2, footprint.Y));
                    AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X, footprint.Y + 2));
                    AddWalkable(grid, cells, new OfficeGridCoordinate(footprint.X, footprint.Y - 2));
                }
            }
            return cells.OrderBy(item => item.Y).ThenBy(item => item.X).ToList();
        }

        private static OfficeLayoutEditResult ValidateTopology(OfficeGrid grid)
        {
            if (!grid.Contains(CanonicalInteriorEntrance) || !grid.IsWalkable(CanonicalInteriorEntrance))
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.EntranceBlocked, "유일한 외부 출입구 안쪽 셀을 막을 수 없습니다.");
            HashSet<OfficeGridCoordinate> reachable = ReachableFrom(grid, CanonicalInteriorEntrance);
            foreach (OfficeSeatSlot seat in grid.SeatSlots)
            {
                if (!reachable.Contains(seat.ApproachCell))
                    return OfficeLayoutEditResult.Fail(
                        OfficeLayoutEditFailure.PathDisconnected,
                        $"지정 좌석 '{seat.SeatId}'로 가는 통로가 끊깁니다.");
                PlacedOfficeFurniture chair = grid.Furniture.FirstOrDefault(item =>
                    Same(item.FurnitureId, seat.ChairFurnitureId));
                OfficeLayoutEditResult seatEgress = ValidateSeatEgress(
                    grid,
                    reachable,
                    chair,
                    0,
                    seat.SeatId);
                if (!seatEgress.Success) return seatEgress;
            }
            foreach (PlacedOfficeFurniture furniture in grid.Furniture)
            {
                OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(furniture.KindId);
                if (definition == null || definition.Capabilities == OfficeFurnitureCapability.None) continue;
                OfficeFurnitureGeometrySnapshot geometry = OfficeFurnitureGeometryQuery.Shared.Resolve(furniture);
                bool isBoundChair = grid.SeatSlots.Any(seat =>
                    Same(seat.ChairFurnitureId, furniture.FurnitureId));
                if (!isBoundChair || definition.Capabilities != OfficeFurnitureCapability.Seat)
                {
                    foreach (IGrouping<int, OfficeFurnitureWorldSocket> slot in
                             geometry.InteractionAccessSockets.GroupBy(item => item.SlotIndex))
                    {
                        OfficeFurnitureWorldSocket[] safe = slot
                            .Where(item => IsSafeSocket(grid, item.WorldCell))
                            .ToArray();
                        if (safe.Length == 0)
                            return OfficeLayoutEditResult.Fail(
                                OfficeLayoutEditFailure.AccessBlocked,
                                $"'{definition.KoreanDisplayName}' {slot.Key + 1}번 사용 위치의 안전한 접근 칸이 없습니다.");
                        if (!safe.Any(item => reachable.Contains(item.WorldCell)))
                            return OfficeLayoutEditResult.Fail(
                                OfficeLayoutEditFailure.PathDisconnected,
                                $"'{definition.KoreanDisplayName}' {slot.Key + 1}번 접근 칸으로 가는 길이 없습니다.");
                    }
                }

                if (!definition.HasCapability(OfficeFurnitureCapability.Seat) || isBoundChair) continue;
                int seatCount = Math.Max(1, definition.Capacity);
                for (var seatIndex = 0; seatIndex < seatCount; seatIndex++)
                {
                    OfficeLayoutEditResult egress = ValidateSeatEgress(
                        grid,
                        reachable,
                        furniture,
                        seatIndex,
                        furniture.FurnitureId + ":" + seatIndex);
                    if (!egress.Success) return egress;
                }
            }
            return OfficeLayoutEditResult.Ok(grid);
        }

        private static OfficeLayoutEditResult ValidateSeatEgress(
            OfficeGrid grid,
            ISet<OfficeGridCoordinate> reachable,
            PlacedOfficeFurniture furniture,
            int slotIndex,
            string seatLabel)
        {
            if (furniture == null || !OfficeFurnitureGeometryQuery.Shared.TryResolve(
                    furniture.KindId,
                    furniture.Origin,
                    furniture.Facing,
                    out OfficeFurnitureGeometrySnapshot geometry))
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.AccessBlocked,
                    $"좌석 '{seatLabel}'의 egress geometry가 없습니다.");
            OfficeFurnitureWorldSocket[] safe = geometry.SeatEgressSockets
                .Where(item => item.SlotIndex == slotIndex && IsSafeSocket(grid, item.WorldCell))
                .ToArray();
            if (safe.Length == 0)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.AccessBlocked,
                    $"좌석 '{seatLabel}'의 앞/왼쪽/오른쪽 egress가 모두 막혔습니다.");
            if (!safe.Any(item => reachable.Contains(item.WorldCell)))
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.PathDisconnected,
                    $"좌석 '{seatLabel}'의 안전한 egress로 가는 길이 없습니다.");
            return OfficeLayoutEditResult.Ok(grid);
        }

        private static bool IsSafeSocket(OfficeGrid grid, OfficeGridCoordinate cell) =>
            grid.Contains(cell) && grid.IsWalkable(cell);

        private static HashSet<OfficeGridCoordinate> ReachableFrom(
            OfficeGrid grid,
            OfficeGridCoordinate start)
        {
            var result = new HashSet<OfficeGridCoordinate> { start };
            var queue = new Queue<OfficeGridCoordinate>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                OfficeGridCoordinate cell = queue.Dequeue();
                foreach (OfficeGridCoordinate next in new[]
                         {
                             new OfficeGridCoordinate(cell.X + 1, cell.Y),
                             new OfficeGridCoordinate(cell.X - 1, cell.Y),
                             new OfficeGridCoordinate(cell.X, cell.Y + 1),
                             new OfficeGridCoordinate(cell.X, cell.Y - 1)
                         })
                {
                    if (!grid.Contains(next) || !grid.IsWalkable(next) || !result.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            return result;
        }

        private static void AddWalkable(
            OfficeGrid grid,
            ISet<OfficeGridCoordinate> cells,
            OfficeGridCoordinate candidate)
        {
            if (grid.Contains(candidate) && grid.IsWalkable(candidate)) cells.Add(candidate);
        }

        private static PlacedOfficeFurniture RotateAroundCell(
            PlacedOfficeFurniture item,
            OfficeGridCoordinate pivot)
        {
            var rotatedCells = FootprintCells(item).Select(cell => RotateCellClockwise(cell, pivot)).ToList();
            int minX = rotatedCells.Min(cell => cell.X);
            int maxX = rotatedCells.Max(cell => cell.X);
            int minY = rotatedCells.Min(cell => cell.Y);
            int maxY = rotatedCells.Max(cell => cell.Y);
            return new PlacedOfficeFurniture(
                item.FurnitureId,
                item.KindId,
                new OfficeGridCoordinate(minX, minY),
                maxX - minX + 1,
                maxY - minY + 1,
                QuarterTurnClockwise(item.Facing),
                item.BlocksMovement);
        }

        private static OfficeGridCoordinate RotateCellClockwise(
            OfficeGridCoordinate cell,
            OfficeGridCoordinate pivot)
        {
            int dx = cell.X - pivot.X;
            int dy = cell.Y - pivot.Y;
            return new OfficeGridCoordinate(pivot.X + dy, pivot.Y - dx);
        }

        private static OfficeGridSubcellAnchor RotateAnchorClockwise(
            OfficeGridSubcellAnchor anchor,
            OfficeGridCoordinate pivot)
        {
            int pivotX2 = pivot.X * 2;
            int pivotY2 = pivot.Y * 2;
            int dx2 = anchor.X2 - pivotX2;
            int dy2 = anchor.Y2 - pivotY2;
            return new OfficeGridSubcellAnchor(pivotX2 + dy2, pivotY2 - dx2);
        }

        private static PlacedOfficeFurniture Find(OfficeGrid grid, string furnitureId) =>
            grid.Furniture.FirstOrDefault(item => Same(item.FurnitureId, furnitureId));

        private static bool Same(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);

        private static PlacedOfficeFurniture Translate(PlacedOfficeFurniture item, int deltaX, int deltaY) =>
            new PlacedOfficeFurniture(
                item.FurnitureId,
                item.KindId,
                new OfficeGridCoordinate(item.Origin.X + deltaX, item.Origin.Y + deltaY),
                item.Width,
                item.Height,
                new OfficeGridSubcellAnchor(
                    item.PlacementAnchor.X2 + deltaX * 2,
                    item.PlacementAnchor.Y2 + deltaY * 2),
                item.Facing,
                item.BlocksMovement);

        private static OfficeSeatSlot Translate(OfficeSeatSlot seat, int deltaX, int deltaY) =>
            new OfficeSeatSlot(
                seat.SeatId,
                seat.ChairFurnitureId,
                seat.WorkSurfaceFurnitureId,
                new OfficeGridCoordinate(seat.Cell.X + deltaX, seat.Cell.Y + deltaY),
                new OfficeGridCoordinate(seat.ApproachCell.X + deltaX, seat.ApproachCell.Y + deltaY),
                new OfficeGridSubcellAnchor(
                    seat.OperatorAnchor.X2 + deltaX * 2,
                    seat.OperatorAnchor.Y2 + deltaY * 2),
                seat.Facing);
    }
}

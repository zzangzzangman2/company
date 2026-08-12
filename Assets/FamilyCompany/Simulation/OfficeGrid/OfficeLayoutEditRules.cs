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
        NothingToDo = 7
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
            var dropFurniture = new HashSet<string>(StringComparer.Ordinal) { furnitureId };
            var dropSeats = new HashSet<string>(StringComparer.Ordinal);
            if (owner != null)
            {
                dropSeats.Add(owner.SeatId);
                dropFurniture.Add(owner.ChairFurnitureId);
                if (owner.HasWorkstationBinding) dropFurniture.Add(owner.WorkSurfaceFurnitureId);
            }
            return Rebuild(
                grid,
                grid.Furniture.Where(item => !dropFurniture.Contains(item.FurnitureId)).ToList(),
                grid.SeatSlots.Where(item => !dropSeats.Contains(item.SeatId)).ToList());
        }

        /// <summary>
        /// The facings an isometric sprite can honestly be drawn at when only one was authored:
        /// itself, and its horizontal mirror. SouthEast mirrors to SouthWest and NorthWest mirrors
        /// to NorthEast, so flipping the sprite on X turns the piece to face the other way without
        /// inventing pixels. The remaining two facings would need real art.
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

        /// <summary>
        /// Turns a free standing piece to face the other way. The footprint is unchanged - a mirror
        /// is not a quarter turn - so this can only fail if the piece does not exist or belongs to a
        /// workstation, where the desk, chair, seat and approach would all have to turn together and
        /// no art exists for the resulting facings.
        /// </summary>
        public static OfficeLayoutEditResult RotateFurniture(OfficeGrid grid, string furnitureId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            PlacedOfficeFurniture target = Find(grid, furnitureId);
            if (target == null)
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.UnknownTarget, $"가구 '{furnitureId}'가 없습니다.");
            if (grid.SeatSlots.Any(seat =>
                    Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId)))
                return OfficeLayoutEditResult.Fail(
                    OfficeLayoutEditFailure.RotationUnsupported,
                    "워크스테이션은 방향별 책상·의자·착석 아트가 준비되면 회전할 수 있습니다.");

            var rotated = new PlacedOfficeFurniture(
                target.FurnitureId,
                target.KindId,
                target.Origin,
                target.Width,
                target.Height,
                target.PlacementAnchor,
                Mirror(target.Facing),
                target.BlocksMovement);
            return Rebuild(
                grid,
                grid.Furniture.Select(item => Same(item.FurnitureId, furnitureId) ? rotated : item).ToList(),
                grid.SeatSlots.ToList());
        }

        /// <summary>True when the editor should offer the rotate button for this piece.</summary>
        public static bool CanRotate(OfficeGrid grid, string furnitureId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (Find(grid, furnitureId) == null) return false;
            return !grid.SeatSlots.Any(seat =>
                Same(seat.ChairFurnitureId, furnitureId) || Same(seat.WorkSurfaceFurnitureId, furnitureId));
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
            var claimed = new Dictionary<int, string>();
            foreach (PlacedOfficeFurniture item in furniture)
            {
                foreach (OfficeGridCoordinate cell in FootprintCells(item))
                {
                    int index = cell.Y * source.Width + cell.X;
                    if (!walkable[index] && source.FloorAt(cell) == OfficeFloorTileKind.Void)
                        return OfficeLayoutEditResult.Fail(
                            OfficeLayoutEditFailure.NotOnFloor, $"'{item.FurnitureId}'를 바닥 밖에 둘 수 없습니다.");
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
                return OfficeLayoutEditResult.Ok(new OfficeGrid(
                    source.Width,
                    source.Height,
                    source.CopyFloorTiles(),
                    walkable,
                    furniture,
                    seats));
            }
            catch (ArgumentException exception)
            {
                return OfficeLayoutEditResult.Fail(OfficeLayoutEditFailure.SeatBroken, exception.Message);
            }
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

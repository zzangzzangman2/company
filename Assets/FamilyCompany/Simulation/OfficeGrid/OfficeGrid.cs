using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeFloorTileKind
    {
        Void = 0,
        WarmWoodA = 1,
        WarmWoodB = 2,
        WarmWoodC = 3,
        DustyMintCarpet = 4
    }

    public enum OfficeFurnitureFacing
    {
        SouthEast = 0,
        SouthWest = 1,
        NorthWest = 2,
        NorthEast = 3
    }

    public readonly struct OfficeGridCoordinate : IEquatable<OfficeGridCoordinate>
    {
        public OfficeGridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(OfficeGridCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is OfficeGridCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>
    /// A semantic floor anchor with half-cell precision. Integer cell centers are even values;
    /// odd values represent the midpoint between two neighboring cell centers.
    /// </summary>
    public readonly struct OfficeGridSubcellAnchor : IEquatable<OfficeGridSubcellAnchor>
    {
        public OfficeGridSubcellAnchor(int x2, int y2)
        {
            X2 = x2;
            Y2 = y2;
        }

        public int X2 { get; }
        public int Y2 { get; }

        public static OfficeGridSubcellAnchor FromCellCenter(OfficeGridCoordinate cell) =>
            new OfficeGridSubcellAnchor(checked(cell.X * 2), checked(cell.Y * 2));

        public bool Equals(OfficeGridSubcellAnchor other) => X2 == other.X2 && Y2 == other.Y2;
        public override bool Equals(object obj) => obj is OfficeGridSubcellAnchor other && Equals(other);
        public override int GetHashCode() => unchecked((X2 * 397) ^ Y2);
        public override string ToString() => $"({X2}/2,{Y2}/2)";
    }

    public sealed class PlacedOfficeFurniture
    {
        public PlacedOfficeFurniture(
            string furnitureId,
            string kindId,
            OfficeGridCoordinate origin,
            int width,
            int height,
            OfficeFurnitureFacing facing,
            bool blocksMovement = true)
            : this(
                furnitureId,
                kindId,
                origin,
                width,
                height,
                DefaultPlacementAnchor(origin, width, height),
                facing,
                blocksMovement)
        {
        }

        public PlacedOfficeFurniture(
            string furnitureId,
            string kindId,
            OfficeGridCoordinate origin,
            int width,
            int height,
            OfficeGridSubcellAnchor placementAnchor,
            OfficeFurnitureFacing facing,
            bool blocksMovement = true)
        {
            FurnitureId = RequiredId(furnitureId, nameof(furnitureId));
            KindId = RequiredId(kindId, nameof(kindId));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Origin = origin;
            Width = width;
            Height = height;
            PlacementAnchor = placementAnchor;
            Facing = facing;
            BlocksMovement = blocksMovement;
        }

        public string FurnitureId { get; }
        public string KindId { get; }
        public OfficeGridCoordinate Origin { get; }
        public int Width { get; }
        public int Height { get; }
        public OfficeGridSubcellAnchor PlacementAnchor { get; }
        public OfficeFurnitureFacing Facing { get; }
        public bool BlocksMovement { get; }

        public static OfficeGridSubcellAnchor DefaultPlacementAnchor(
            OfficeGridCoordinate origin,
            int width,
            int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            return new OfficeGridSubcellAnchor(
                checked(origin.X * 2 + width - 1),
                checked(origin.Y * 2 + height - 1));
        }

        /// <summary>
        /// True when the rendered pivot is the exact half-cell center of the collision footprint.
        /// Keeping this invariant means a moved or mirrored prop cannot accumulate an independent
        /// presentation offset that no longer matches navigation and interaction cells.
        /// </summary>
        public bool HasCanonicalPlacementAnchor =>
            PlacementAnchor.Equals(DefaultPlacementAnchor(Origin, Width, Height));

        private static string RequiredId(string value, string parameterName)
        {
            var canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("ID cannot be empty.", parameterName);
            return canonical;
        }
    }

    public sealed class OfficeSeatSlot
    {
        public OfficeSeatSlot(
            string seatId,
            string furnitureId,
            OfficeGridCoordinate cell,
            OfficeFurnitureFacing facing)
            : this(
                seatId,
                furnitureId,
                string.Empty,
                cell,
                cell,
                OfficeGridSubcellAnchor.FromCellCenter(cell),
                facing)
        {
        }

        public OfficeSeatSlot(
            string seatId,
            string chairFurnitureId,
            string workSurfaceFurnitureId,
            OfficeGridCoordinate cell,
            OfficeGridCoordinate approachCell,
            OfficeFurnitureFacing facing)
            : this(
                seatId,
                chairFurnitureId,
                workSurfaceFurnitureId,
                cell,
                approachCell,
                OfficeGridSubcellAnchor.FromCellCenter(cell),
                facing)
        {
        }

        public OfficeSeatSlot(
            string seatId,
            string chairFurnitureId,
            string workSurfaceFurnitureId,
            OfficeGridCoordinate cell,
            OfficeGridCoordinate approachCell,
            OfficeGridSubcellAnchor operatorAnchor,
            OfficeFurnitureFacing facing)
        {
            SeatId = RequiredId(seatId, nameof(seatId));
            FurnitureId = RequiredId(chairFurnitureId, nameof(chairFurnitureId));
            WorkSurfaceFurnitureId = OptionalId(workSurfaceFurnitureId);
            Cell = cell;
            ApproachCell = approachCell;
            OperatorAnchor = operatorAnchor;
            Facing = facing;
        }

        public string SeatId { get; }
        public string ChairFurnitureId => FurnitureId;
        public string FurnitureId { get; }
        public string WorkSurfaceFurnitureId { get; }
        public OfficeGridCoordinate Cell { get; }
        public OfficeGridCoordinate ApproachCell { get; }
        public OfficeGridSubcellAnchor OperatorAnchor { get; }
        public OfficeFurnitureFacing Facing { get; }
        public bool HasWorkstationBinding => WorkSurfaceFurnitureId.Length > 0;

        private static string RequiredId(string value, string parameterName)
        {
            var canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("ID cannot be empty.", parameterName);
            return canonical;
        }

        private static string OptionalId(string value) => (value ?? string.Empty).Trim();
    }

    /// <summary>
    /// Explicit desk/chair/seat binding used by presentation and QA. It is derived from the
    /// persisted seat slot so there is one semantic source of truth.
    /// </summary>
    public sealed class OfficeWorkstationSlot
    {
        internal OfficeWorkstationSlot(OfficeSeatSlot seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (!seat.HasWorkstationBinding)
                throw new ArgumentException("A workstation requires an explicit desk binding.", nameof(seat));
            WorkstationId = "workstation_" + seat.SeatId;
            SeatId = seat.SeatId;
            DeskFurnitureId = seat.WorkSurfaceFurnitureId;
            ChairFurnitureId = seat.ChairFurnitureId;
            SeatCell = seat.Cell;
            ApproachCell = seat.ApproachCell;
            OperatorAnchor = seat.OperatorAnchor;
            Facing = seat.Facing;
        }

        public string WorkstationId { get; }
        public string SeatId { get; }
        public string DeskFurnitureId { get; }
        public string ChairFurnitureId { get; }
        public OfficeGridCoordinate SeatCell { get; }
        public OfficeGridCoordinate ApproachCell { get; }
        public OfficeGridSubcellAnchor OperatorAnchor { get; }
        public OfficeFurnitureFacing Facing { get; }
    }

    /// <summary>
    /// Immutable semantic office layout. Coordinates, not scene Transforms, are persisted.
    /// </summary>
    public sealed class OfficeGrid
    {
        public const int MaximumSideLength = 128;

        private readonly OfficeFloorTileKind[] _floorTiles;
        private readonly bool[] _walkable;
        private readonly ReadOnlyCollection<PlacedOfficeFurniture> _furniture;
        private readonly ReadOnlyCollection<OfficeSeatSlot> _seatSlots;
        private readonly ReadOnlyCollection<OfficeWorkstationSlot> _workstations;

        public OfficeGrid(
            int width,
            int height,
            IReadOnlyList<OfficeFloorTileKind> floorTiles,
            IReadOnlyList<bool> walkable,
            IEnumerable<PlacedOfficeFurniture> furniture = null,
            IEnumerable<OfficeSeatSlot> seatSlots = null)
        {
            if (width <= 0 || width > MaximumSideLength) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0 || height > MaximumSideLength) throw new ArgumentOutOfRangeException(nameof(height));
            if (floorTiles == null) throw new ArgumentNullException(nameof(floorTiles));
            if (walkable == null) throw new ArgumentNullException(nameof(walkable));

            var cellCount = checked(width * height);
            if (floorTiles.Count != cellCount)
                throw new ArgumentException("Floor tile count must match width * height.", nameof(floorTiles));
            if (walkable.Count != cellCount)
                throw new ArgumentException("Walkable count must match width * height.", nameof(walkable));

            Width = width;
            Height = height;
            _floorTiles = new OfficeFloorTileKind[cellCount];
            _walkable = new bool[cellCount];
            for (var index = 0; index < cellCount; index++)
            {
                var tile = floorTiles[index];
                if (!Enum.IsDefined(typeof(OfficeFloorTileKind), tile))
                    throw new ArgumentOutOfRangeException(nameof(floorTiles), $"Unknown floor tile value at {index}.");
                if (walkable[index] && tile == OfficeFloorTileKind.Void)
                    throw new ArgumentException("A void cell cannot be walkable.", nameof(walkable));
                _floorTiles[index] = tile;
                _walkable[index] = walkable[index];
            }

            var furnitureList = furniture == null
                ? new List<PlacedOfficeFurniture>()
                : new List<PlacedOfficeFurniture>(furniture);
            var seatList = seatSlots == null
                ? new List<OfficeSeatSlot>()
                : new List<OfficeSeatSlot>(seatSlots);
            ValidateFurniture(furnitureList);
            ValidateSeats(furnitureList, seatList);
            _furniture = furnitureList.AsReadOnly();
            _seatSlots = seatList.AsReadOnly();
            var workstationList = new List<OfficeWorkstationSlot>();
            foreach (var seat in seatList)
            {
                if (seat.HasWorkstationBinding) workstationList.Add(new OfficeWorkstationSlot(seat));
            }
            _workstations = workstationList.AsReadOnly();
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _floorTiles.Length;
        public IReadOnlyList<PlacedOfficeFurniture> Furniture => _furniture;
        public IReadOnlyList<OfficeSeatSlot> SeatSlots => _seatSlots;
        public IReadOnlyList<OfficeWorkstationSlot> Workstations => _workstations;

        public bool Contains(OfficeGridCoordinate cell) =>
            cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        public bool Contains(OfficeGridSubcellAnchor anchor) =>
            anchor.X2 >= 0 && anchor.X2 <= checked((Width - 1) * 2) &&
            anchor.Y2 >= 0 && anchor.Y2 <= checked((Height - 1) * 2);

        public OfficeFloorTileKind FloorAt(OfficeGridCoordinate cell) => _floorTiles[IndexOf(cell)];
        public bool IsWalkable(OfficeGridCoordinate cell) => _walkable[IndexOf(cell)];

        public OfficeFloorTileKind[] CopyFloorTiles() => (OfficeFloorTileKind[])_floorTiles.Clone();
        public bool[] CopyWalkable() => (bool[])_walkable.Clone();

        public string ComputeLayoutHash()
        {
            const ulong offset = 14695981039346656037UL;
            var hash = offset;
            AddInt(ref hash, Width);
            AddInt(ref hash, Height);
            for (var index = 0; index < _floorTiles.Length; index++)
            {
                AddInt(ref hash, (int)_floorTiles[index]);
                AddByte(ref hash, _walkable[index] ? (byte)1 : (byte)0);
            }

            foreach (var item in _furniture)
            {
                AddString(ref hash, item.FurnitureId);
                AddString(ref hash, item.KindId);
                AddInt(ref hash, item.Origin.X);
                AddInt(ref hash, item.Origin.Y);
                AddInt(ref hash, item.Width);
                AddInt(ref hash, item.Height);
                AddInt(ref hash, item.PlacementAnchor.X2);
                AddInt(ref hash, item.PlacementAnchor.Y2);
                AddInt(ref hash, (int)item.Facing);
                AddByte(ref hash, item.BlocksMovement ? (byte)1 : (byte)0);
            }

            foreach (var item in _seatSlots)
            {
                AddString(ref hash, item.SeatId);
                AddString(ref hash, item.FurnitureId);
                AddString(ref hash, item.WorkSurfaceFurnitureId);
                AddInt(ref hash, item.Cell.X);
                AddInt(ref hash, item.Cell.Y);
                AddInt(ref hash, item.ApproachCell.X);
                AddInt(ref hash, item.ApproachCell.Y);
                AddInt(ref hash, item.OperatorAnchor.X2);
                AddInt(ref hash, item.OperatorAnchor.Y2);
                AddInt(ref hash, (int)item.Facing);
            }

            return hash.ToString("X16");
        }

        private int IndexOf(OfficeGridCoordinate cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the office grid.");
            return cell.Y * Width + cell.X;
        }

        private void ValidateFurniture(IReadOnlyList<PlacedOfficeFurniture> furniture)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < furniture.Count; index++)
            {
                var item = furniture[index] ?? throw new ArgumentException("Furniture cannot contain null.", nameof(furniture));
                if (!ids.Add(item.FurnitureId))
                    throw new ArgumentException($"Duplicate furniture ID: {item.FurnitureId}.", nameof(furniture));
                var maximumX = checked(item.Origin.X + item.Width - 1);
                var maximumY = checked(item.Origin.Y + item.Height - 1);
                if (!Contains(item.Origin) || !Contains(new OfficeGridCoordinate(maximumX, maximumY)))
                    throw new ArgumentException($"Furniture is outside the grid: {item.FurnitureId}.", nameof(furniture));
                if (!Contains(item.PlacementAnchor))
                    throw new ArgumentException($"Furniture placement anchor is outside the grid: {item.FurnitureId}.", nameof(furniture));
                if (!item.BlocksMovement) continue;
                for (var y = item.Origin.Y; y <= maximumY; y++)
                for (var x = item.Origin.X; x <= maximumX; x++)
                {
                    if (IsWalkable(new OfficeGridCoordinate(x, y)))
                        throw new ArgumentException($"Blocking furniture occupies a walkable cell: {item.FurnitureId}.", nameof(furniture));
                }
            }
        }

        private void ValidateSeats(
            IReadOnlyList<PlacedOfficeFurniture> furniture,
            IReadOnlyList<OfficeSeatSlot> seats)
        {
            var furnitureById = new Dictionary<string, PlacedOfficeFurniture>(StringComparer.Ordinal);
            foreach (var item in furniture) furnitureById.Add(item.FurnitureId, item);
            var seatIds = new HashSet<string>(StringComparer.Ordinal);
            var seatedFurnitureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                if (seat == null) throw new ArgumentException("Seat slots cannot contain null.", nameof(seats));
                if (!seatIds.Add(seat.SeatId))
                    throw new ArgumentException($"Duplicate seat ID: {seat.SeatId}.", nameof(seats));
                if (!furnitureById.TryGetValue(seat.FurnitureId, out var seatFurniture))
                    throw new ArgumentException($"Seat references unknown furniture: {seat.FurnitureId}.", nameof(seats));
                if (!Contains(seat.Cell))
                    throw new ArgumentException($"Seat is outside the grid: {seat.SeatId}.", nameof(seats));
                if (!Contains(seat.OperatorAnchor))
                    throw new ArgumentException($"Seat operator anchor is outside the grid: {seat.SeatId}.", nameof(seats));
                if (!IsWalkable(seat.Cell))
                    throw new ArgumentException($"Seat cell is not walkable: {seat.SeatId}.", nameof(seats));
                if (seatFurniture.BlocksMovement)
                    throw new ArgumentException($"Seat furniture blocks movement: {seat.FurnitureId}.", nameof(seats));
                if (!seatFurniture.Origin.Equals(seat.Cell))
                    throw new ArgumentException($"Seat cell does not match its chair origin: {seat.SeatId}.", nameof(seats));
                if (seatFurniture.Facing != seat.Facing)
                    throw new ArgumentException($"Seat facing does not match its chair: {seat.SeatId}.", nameof(seats));
                if (!seatedFurnitureIds.Add(seat.FurnitureId))
                    throw new ArgumentException($"Chair has more than one seat slot: {seat.FurnitureId}.", nameof(seats));
                if (!seat.HasWorkstationBinding) continue;
                if (!furnitureById.TryGetValue(seat.WorkSurfaceFurnitureId, out var workSurface))
                    throw new ArgumentException($"Seat references unknown work surface: {seat.WorkSurfaceFurnitureId}.", nameof(seats));
                if (!workSurface.BlocksMovement)
                    throw new ArgumentException($"Seat work surface must block movement: {seat.WorkSurfaceFurnitureId}.", nameof(seats));
                if (!Contains(seat.ApproachCell) || !IsWalkable(seat.ApproachCell))
                    throw new ArgumentException($"Seat approach cell is not walkable: {seat.SeatId}.", nameof(seats));
                if (CardinalDistance(seat.Cell, seat.ApproachCell) != 1)
                    throw new ArgumentException($"Seat approach cell is not cardinally adjacent: {seat.SeatId}.", nameof(seats));
                var seatCenterAnchor = OfficeGridSubcellAnchor.FromCellCenter(seat.Cell);
                var operatorDeltaX2 = Math.Abs(seat.OperatorAnchor.X2 - seatCenterAnchor.X2);
                var operatorDeltaY2 = Math.Abs(seat.OperatorAnchor.Y2 - seatCenterAnchor.Y2);
                if (operatorDeltaX2 > 1 || operatorDeltaY2 > 1)
                    throw new ArgumentException($"Seat operator anchor must remain within the surrounding half-cell square: {seat.SeatId}.", nameof(seats));
                var nearestWorkCell = NearestFootprintCell(workSurface, seat.Cell, out var workDistance);
                if (workDistance != 1)
                    throw new ArgumentException($"Seat is not cardinally adjacent to its work surface: {seat.SeatId}.", nameof(seats));
                var expectedFacing = FacingFromDelta(
                    nearestWorkCell.X - seat.Cell.X,
                    nearestWorkCell.Y - seat.Cell.Y);
                if (expectedFacing != seat.Facing)
                    throw new ArgumentException($"Seat does not face its work surface: {seat.SeatId}.", nameof(seats));
            }
        }

        private static OfficeGridCoordinate NearestFootprintCell(
            PlacedOfficeFurniture furniture,
            OfficeGridCoordinate origin,
            out int distance)
        {
            var best = furniture.Origin;
            distance = int.MaxValue;
            for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
            for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
            {
                var candidate = new OfficeGridCoordinate(x, y);
                var candidateDistance = CardinalDistance(origin, candidate);
                if (candidateDistance >= distance) continue;
                best = candidate;
                distance = candidateDistance;
            }
            return best;
        }

        private static int CardinalDistance(OfficeGridCoordinate left, OfficeGridCoordinate right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

        private static OfficeFurnitureFacing FacingFromDelta(int deltaX, int deltaY)
        {
            if (deltaX == 1 && deltaY == 0) return OfficeFurnitureFacing.NorthEast;
            if (deltaX == -1 && deltaY == 0) return OfficeFurnitureFacing.SouthWest;
            if (deltaX == 0 && deltaY == 1) return OfficeFurnitureFacing.NorthWest;
            if (deltaX == 0 && deltaY == -1) return OfficeFurnitureFacing.SouthEast;
            throw new ArgumentException($"Seat-to-work-surface delta is not cardinal: ({deltaX},{deltaY}).");
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddString(ref ulong hash, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            AddInt(ref hash, bytes.Length);
            for (var index = 0; index < bytes.Length; index++) AddByte(ref hash, bytes[index]);
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
        }
    }
}

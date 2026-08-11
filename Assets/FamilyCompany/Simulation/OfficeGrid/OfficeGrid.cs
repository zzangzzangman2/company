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
        {
            FurnitureId = RequiredId(furnitureId, nameof(furnitureId));
            KindId = RequiredId(kindId, nameof(kindId));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Origin = origin;
            Width = width;
            Height = height;
            Facing = facing;
            BlocksMovement = blocksMovement;
        }

        public string FurnitureId { get; }
        public string KindId { get; }
        public OfficeGridCoordinate Origin { get; }
        public int Width { get; }
        public int Height { get; }
        public OfficeFurnitureFacing Facing { get; }
        public bool BlocksMovement { get; }

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
        {
            SeatId = RequiredId(seatId, nameof(seatId));
            FurnitureId = RequiredId(furnitureId, nameof(furnitureId));
            Cell = cell;
            Facing = facing;
        }

        public string SeatId { get; }
        public string FurnitureId { get; }
        public OfficeGridCoordinate Cell { get; }
        public OfficeFurnitureFacing Facing { get; }

        private static string RequiredId(string value, string parameterName)
        {
            var canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("ID cannot be empty.", parameterName);
            return canonical;
        }
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
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _floorTiles.Length;
        public IReadOnlyList<PlacedOfficeFurniture> Furniture => _furniture;
        public IReadOnlyList<OfficeSeatSlot> SeatSlots => _seatSlots;

        public bool Contains(OfficeGridCoordinate cell) =>
            cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

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
                AddInt(ref hash, (int)item.Facing);
                AddByte(ref hash, item.BlocksMovement ? (byte)1 : (byte)0);
            }

            foreach (var item in _seatSlots)
            {
                AddString(ref hash, item.SeatId);
                AddString(ref hash, item.FurnitureId);
                AddInt(ref hash, item.Cell.X);
                AddInt(ref hash, item.Cell.Y);
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
            var furnitureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in furniture) furnitureIds.Add(item.FurnitureId);
            var seatIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                if (seat == null) throw new ArgumentException("Seat slots cannot contain null.", nameof(seats));
                if (!seatIds.Add(seat.SeatId))
                    throw new ArgumentException($"Duplicate seat ID: {seat.SeatId}.", nameof(seats));
                if (!furnitureIds.Contains(seat.FurnitureId))
                    throw new ArgumentException($"Seat references unknown furniture: {seat.FurnitureId}.", nameof(seats));
                if (!Contains(seat.Cell))
                    throw new ArgumentException($"Seat is outside the grid: {seat.SeatId}.", nameof(seats));
            }
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

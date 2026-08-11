using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [CreateAssetMenu(
        fileName = "StarterOfficeV1",
        menuName = "Family Company/Office/Starter Office Layout")]
    public sealed class StarterOfficeLayoutAsset : ScriptableObject
    {
        public const string DefaultResourcePath = "OfficeLayouts/StarterOfficeV1";

        [Serializable]
        public sealed class FurnitureRecord
        {
            [SerializeField] private string furnitureId = string.Empty;
            [SerializeField] private string kindId = string.Empty;
            [SerializeField] private int originX;
            [SerializeField] private int originY;
            [SerializeField] private int width = 1;
            [SerializeField] private int height = 1;
            [SerializeField] private int placementX2;
            [SerializeField] private int placementY2;
            [SerializeField] private OfficeFurnitureFacing facing;
            [SerializeField] private bool blocksMovement = true;

            public string FurnitureId => furnitureId;
            public string KindId => kindId;
            public int OriginX => originX;
            public int OriginY => originY;
            public int Width => width;
            public int Height => height;
            public int PlacementX2 => placementX2;
            public int PlacementY2 => placementY2;
            public OfficeFurnitureFacing Facing => facing;
            public bool BlocksMovement => blocksMovement;

            public FurnitureRecord Clone(string replacementId = null) => new FurnitureRecord
            {
                furnitureId = replacementId ?? furnitureId,
                kindId = kindId,
                originX = originX,
                originY = originY,
                width = width,
                height = height,
                placementX2 = placementX2,
                placementY2 = placementY2,
                facing = facing,
                blocksMovement = blocksMovement
            };

            internal static FurnitureRecord From(PlacedOfficeFurniture value) => new FurnitureRecord
            {
                furnitureId = value.FurnitureId,
                kindId = value.KindId,
                originX = value.Origin.X,
                originY = value.Origin.Y,
                width = value.Width,
                height = value.Height,
                placementX2 = value.PlacementAnchor.X2,
                placementY2 = value.PlacementAnchor.Y2,
                facing = value.Facing,
                blocksMovement = value.BlocksMovement
            };

            internal PlacedOfficeFurniture Build() => new PlacedOfficeFurniture(
                furnitureId,
                kindId,
                new OfficeGridCoordinate(originX, originY),
                width,
                height,
                new OfficeGridSubcellAnchor(placementX2, placementY2),
                facing,
                blocksMovement);

            internal void TranslateAnchor(int deltaX2, int deltaY2)
            {
                placementX2 = checked(placementX2 + deltaX2);
                placementY2 = checked(placementY2 + deltaY2);
            }

            internal void TranslateFootprint(int deltaX, int deltaY)
            {
                originX = checked(originX + deltaX);
                originY = checked(originY + deltaY);
                placementX2 = checked(placementX2 + deltaX * 2);
                placementY2 = checked(placementY2 + deltaY * 2);
            }

            internal void RotateClockwise()
            {
                int previousWidth = width;
                width = height;
                height = previousWidth;
                facing = (OfficeFurnitureFacing)(((int)facing + 1) & 3);
            }

            internal static FurnitureRecord Create(
                string id,
                string kind,
                int x,
                int y,
                int recordWidth,
                int recordHeight,
                OfficeFurnitureFacing recordFacing,
                bool blocking,
                OfficeGridSubcellAnchor anchor) => new FurnitureRecord
            {
                furnitureId = id,
                kindId = kind,
                originX = x,
                originY = y,
                width = recordWidth,
                height = recordHeight,
                placementX2 = anchor.X2,
                placementY2 = anchor.Y2,
                facing = recordFacing,
                blocksMovement = blocking
            };
        }

        [Serializable]
        public sealed class SeatRecord
        {
            [SerializeField] private string seatId = string.Empty;
            [SerializeField] private string chairFurnitureId = string.Empty;
            [SerializeField] private string workSurfaceFurnitureId = string.Empty;
            [SerializeField] private int cellX;
            [SerializeField] private int cellY;
            [SerializeField] private int approachX;
            [SerializeField] private int approachY;
            [SerializeField] private int operatorX2;
            [SerializeField] private int operatorY2;
            [SerializeField] private OfficeFurnitureFacing facing;

            public string SeatId => seatId;
            public string ChairFurnitureId => chairFurnitureId;
            public string WorkSurfaceFurnitureId => workSurfaceFurnitureId;
            public int CellX => cellX;
            public int CellY => cellY;
            public int ApproachX => approachX;
            public int ApproachY => approachY;
            public int OperatorX2 => operatorX2;
            public int OperatorY2 => operatorY2;
            public OfficeFurnitureFacing Facing => facing;

            internal static SeatRecord From(OfficeSeatSlot value) => new SeatRecord
            {
                seatId = value.SeatId,
                chairFurnitureId = value.ChairFurnitureId,
                workSurfaceFurnitureId = value.WorkSurfaceFurnitureId,
                cellX = value.Cell.X,
                cellY = value.Cell.Y,
                approachX = value.ApproachCell.X,
                approachY = value.ApproachCell.Y,
                operatorX2 = value.OperatorAnchor.X2,
                operatorY2 = value.OperatorAnchor.Y2,
                facing = value.Facing
            };

            internal OfficeSeatSlot Build() => new OfficeSeatSlot(
                seatId,
                chairFurnitureId,
                workSurfaceFurnitureId,
                new OfficeGridCoordinate(cellX, cellY),
                new OfficeGridCoordinate(approachX, approachY),
                new OfficeGridSubcellAnchor(operatorX2, operatorY2),
                facing);

            internal void Translate(int deltaX, int deltaY, int deltaX2, int deltaY2)
            {
                cellX = checked(cellX + deltaX);
                cellY = checked(cellY + deltaY);
                approachX = checked(approachX + deltaX);
                approachY = checked(approachY + deltaY);
                operatorX2 = checked(operatorX2 + deltaX2);
                operatorY2 = checked(operatorY2 + deltaY2);
            }

            internal void TranslateOperator(int deltaX2, int deltaY2)
            {
                operatorX2 = checked(operatorX2 + deltaX2);
                operatorY2 = checked(operatorY2 + deltaY2);
            }

            internal void SetApproach(int x, int y)
            {
                approachX = x;
                approachY = y;
            }

            internal static SeatRecord Create(
                string memberId,
                string chairId,
                string deskId,
                int chairX,
                int chairY) => new SeatRecord
            {
                seatId = "seat_" + memberId,
                chairFurnitureId = chairId,
                workSurfaceFurnitureId = deskId,
                cellX = chairX,
                cellY = chairY,
                approachX = chairX,
                approachY = chairY - 1,
                operatorX2 = checked(chairX * 2 + 1),
                operatorY2 = checked(chairY * 2 + 1),
                facing = OfficeFurnitureFacing.NorthWest
            };
        }

        [SerializeField] private string layoutId = "starter_office_v1";
        [SerializeField] private int version = 1;
        [SerializeField] private int width = OfficeGridLayouts.StarterOfficeWidth;
        [SerializeField] private int height = OfficeGridLayouts.StarterOfficeHeight;
        [SerializeField] private OfficeFloorTileKind[] floorTiles = Array.Empty<OfficeFloorTileKind>();
        [SerializeField] private bool[] walkable = Array.Empty<bool>();
        [SerializeField] private List<FurnitureRecord> furniture = new List<FurnitureRecord>();
        [SerializeField] private List<SeatRecord> seats = new List<SeatRecord>();

        public string LayoutId => layoutId;
        public int Version => version;
        public int Width => width;
        public int Height => height;
        public IReadOnlyList<FurnitureRecord> Furniture => furniture;
        public IReadOnlyList<SeatRecord> Seats => seats;
        public string LayoutHash => BuildGrid().ComputeLayoutHash();

        public static StarterOfficeLayoutAsset LoadDefault() =>
            Resources.Load<StarterOfficeLayoutAsset>(DefaultResourcePath);

        public OfficeGrid BuildGrid()
        {
            if (floorTiles == null || walkable == null)
                throw new InvalidOperationException("Starter Office floor arrays are missing.");
            return new OfficeGrid(
                width,
                height,
                (OfficeFloorTileKind[])floorTiles.Clone(),
                (bool[])walkable.Clone(),
                furniture.Select(item => item.Build()),
                seats.Select(item => item.Build()));
        }

        public void Capture(OfficeGrid grid, string newLayoutId = null, int newVersion = 1)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            layoutId = string.IsNullOrWhiteSpace(newLayoutId) ? layoutId : newLayoutId.Trim();
            version = Math.Max(1, newVersion);
            width = grid.Width;
            height = grid.Height;
            floorTiles = grid.CopyFloorTiles();
            walkable = grid.CopyWalkable();
            furniture = grid.Furniture.Select(FurnitureRecord.From).ToList();
            seats = grid.SeatSlots.Select(SeatRecord.From).ToList();
        }

        public bool TranslateFurnitureAnchor(string furnitureId, int deltaX2, int deltaY2)
        {
            FurnitureRecord record = FindFurniture(furnitureId);
            if (record == null) return false;
            record.TranslateAnchor(deltaX2, deltaY2);
            foreach (SeatRecord seat in seats.Where(item => item.ChairFurnitureId == record.FurnitureId))
                seat.TranslateOperator(deltaX2, deltaY2);
            return true;
        }

        public bool TranslateFurnitureFootprint(string furnitureId, int deltaX, int deltaY)
        {
            FurnitureRecord record = FindFurniture(furnitureId);
            if (record == null) return false;
            record.TranslateFootprint(deltaX, deltaY);
            foreach (SeatRecord seat in seats.Where(item => item.ChairFurnitureId == record.FurnitureId))
                seat.Translate(deltaX, deltaY, deltaX * 2, deltaY * 2);
            RebuildStarterWalkability();
            return true;
        }

        public bool RotateFurnitureClockwise(string furnitureId)
        {
            FurnitureRecord record = FindFurniture(furnitureId);
            if (record == null) return false;
            record.RotateClockwise();
            RebuildStarterWalkability();
            return true;
        }

        public bool SetSeatApproachForChair(string chairFurnitureId, int x, int y)
        {
            SeatRecord seat = seats.FirstOrDefault(item =>
                string.Equals(item.ChairFurnitureId, chairFurnitureId, StringComparison.Ordinal));
            if (seat == null) return false;
            seat.SetApproach(x, y);
            return true;
        }

        public string DuplicateFurniture(string furnitureId)
        {
            FurnitureRecord record = FindFurniture(furnitureId);
            if (record == null) return string.Empty;
            string nextId = UniqueId(record.FurnitureId + "_copy");
            FurnitureRecord copy = record.Clone(nextId);
            copy.TranslateFootprint(1, 0);
            furniture.Add(copy);
            RebuildStarterWalkability();
            return nextId;
        }

        public bool DeleteFurniture(string furnitureId)
        {
            FurnitureRecord record = FindFurniture(furnitureId);
            if (record == null) return false;
            furniture.Remove(record);
            seats.RemoveAll(item =>
                item.ChairFurnitureId == furnitureId || item.WorkSurfaceFurnitureId == furnitureId);
            RebuildStarterWalkability();
            return true;
        }

        public string AddFurniture(
            string kindId,
            int x,
            int y,
            int itemWidth = 1,
            int itemHeight = 1,
            bool blocksMovement = true)
        {
            string id = UniqueId(kindId);
            furniture.Add(FurnitureRecord.Create(
                id,
                kindId,
                x,
                y,
                itemWidth,
                itemHeight,
                OfficeFurnitureFacing.SouthEast,
                blocksMovement,
                PlacedOfficeFurniture.DefaultPlacementAnchor(
                    new OfficeGridCoordinate(x, y), itemWidth, itemHeight)));
            RebuildStarterWalkability();
            return id;
        }

        public string AddWorkstationBlueprint(string memberId, int chairX, int chairY)
        {
            string canonical = string.IsNullOrWhiteSpace(memberId)
                ? "worker"
                : memberId.Trim();
            string deskId = UniqueId("desk_" + canonical);
            string chairId = UniqueId("chair_" + canonical);
            string seatMemberId = UniqueSeatMemberId(canonical);
            furniture.Add(FurnitureRecord.Create(
                deskId,
                OfficeGridLayouts.DeskWithPcKind,
                chairX,
                chairY + 1,
                2,
                1,
                OfficeFurnitureFacing.SouthEast,
                true,
                PlacedOfficeFurniture.DefaultPlacementAnchor(
                    new OfficeGridCoordinate(chairX, chairY + 1), 2, 1)));
            OfficeGridSubcellAnchor chairAnchor = OfficeGridSubcellAnchor.FromCellCenter(
                new OfficeGridCoordinate(chairX, chairY));
            furniture.Add(FurnitureRecord.Create(
                chairId,
                OfficeGridLayouts.SwivelChairKind,
                chairX,
                chairY,
                1,
                1,
                OfficeFurnitureFacing.NorthWest,
                false,
                chairAnchor));
            seats.Add(SeatRecord.Create(seatMemberId, chairId, deskId, chairX, chairY));
            RebuildStarterWalkability();
            return chairId;
        }

        private FurnitureRecord FindFurniture(string id) => furniture.FirstOrDefault(
            item => string.Equals(item.FurnitureId, id, StringComparison.Ordinal));

        private string UniqueId(string stem)
        {
            string canonical = string.IsNullOrWhiteSpace(stem) ? "furniture" : stem.Trim();
            var used = new HashSet<string>(furniture.Select(item => item.FurnitureId), StringComparer.Ordinal);
            if (!used.Contains(canonical)) return canonical;
            for (var suffix = 2; ; suffix++)
            {
                string candidate = canonical + "_" + suffix;
                if (!used.Contains(candidate)) return candidate;
            }
        }

        private string UniqueSeatMemberId(string stem)
        {
            var used = new HashSet<string>(seats.Select(item => item.SeatId), StringComparer.Ordinal);
            if (!used.Contains("seat_" + stem)) return stem;
            for (var suffix = 2; ; suffix++)
            {
                string candidate = stem + "_" + suffix;
                if (!used.Contains("seat_" + candidate)) return candidate;
            }
        }

        private void RebuildStarterWalkability()
        {
            if (floorTiles == null || floorTiles.Length != checked(width * height)) return;
            if (walkable == null || walkable.Length != floorTiles.Length)
                walkable = new bool[floorTiles.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int index = checked(y * width + x);
                walkable[index] = x > 0 && x < width - 1 && y > 0 && y < height - 1 &&
                                  floorTiles[index] != OfficeFloorTileKind.Void;
            }
            foreach (FurnitureRecord item in furniture.Where(value => value.BlocksMovement))
            {
                for (var y = item.OriginY; y < item.OriginY + item.Height; y++)
                for (var x = item.OriginX; x < item.OriginX + item.Width; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;
                    walkable[checked(y * width + x)] = false;
                }
            }
        }
    }

    public sealed class OfficeLayoutValidationReport
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public bool IsValid => _errors.Count == 0;
        public void Error(string message) => _errors.Add(message);
        public void Warning(string message) => _warnings.Add(message);
    }

    public static class OfficeLayoutSemanticValidator
    {
        private static readonly string[] RequiredMembers =
            { "player", "older_sister", "father", "mother" };

        public static OfficeLayoutValidationReport Validate(StarterOfficeLayoutAsset asset)
        {
            var report = new OfficeLayoutValidationReport();
            if (asset == null)
            {
                report.Error("Layout asset is missing.");
                return report;
            }

            OfficeGrid grid;
            try
            {
                grid = asset.BuildGrid();
            }
            catch (Exception exception)
            {
                report.Error(exception.Message);
                return report;
            }

            var occupied = new Dictionary<OfficeGridCoordinate, string>();
            foreach (PlacedOfficeFurniture item in grid.Furniture.Where(value => value.BlocksMovement))
            {
                for (var y = item.Origin.Y; y < item.Origin.Y + item.Height; y++)
                for (var x = item.Origin.X; x < item.Origin.X + item.Width; x++)
                {
                    var cell = new OfficeGridCoordinate(x, y);
                    if (occupied.TryGetValue(cell, out string peer))
                        report.Error($"Hard footprints overlap at {cell}: {peer}, {item.FurnitureId}.");
                    else occupied.Add(cell, item.FurnitureId);
                }
            }

            foreach (string member in RequiredMembers)
            {
                string seatId = "seat_" + member;
                if (grid.SeatSlots.Count(item => item.SeatId == seatId) != 1)
                    report.Error($"Required workstation seat must exist exactly once: {seatId}.");
            }

            OfficeGridCoordinate exit = FindExit(grid);
            HashSet<OfficeGridCoordinate> connected = FloodWalkable(grid, exit);
            foreach (OfficeSeatSlot seat in grid.SeatSlots)
            {
                if (!connected.Contains(seat.ApproachCell))
                    report.Error($"Seat approach is disconnected from the exit: {seat.SeatId}.");
            }
            int walkableCount = 0;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
                if (grid.IsWalkable(new OfficeGridCoordinate(x, y))) walkableCount++;
            if (connected.Count < walkableCount)
                report.Warning($"{walkableCount - connected.Count} walkable cells are isolated from the exit.");
            return report;
        }

        private static OfficeGridCoordinate FindExit(OfficeGrid grid)
        {
            for (var y = 1; y < grid.Height - 1; y++)
            for (var x = 1; x < grid.Width - 1; x++)
            {
                var candidate = new OfficeGridCoordinate(x, y);
                if (grid.IsWalkable(candidate)) return candidate;
            }
            return new OfficeGridCoordinate(0, 0);
        }

        private static HashSet<OfficeGridCoordinate> FloodWalkable(
            OfficeGrid grid,
            OfficeGridCoordinate origin)
        {
            var visited = new HashSet<OfficeGridCoordinate>();
            if (!grid.Contains(origin) || !grid.IsWalkable(origin)) return visited;
            var queue = new Queue<OfficeGridCoordinate>();
            queue.Enqueue(origin);
            visited.Add(origin);
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0), new OfficeGridCoordinate(0, 1),
                new OfficeGridCoordinate(-1, 0), new OfficeGridCoordinate(0, -1)
            };
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (grid.Contains(next) && grid.IsWalkable(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }
    }
}

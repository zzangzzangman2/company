using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    public static class OfficePerimeterExteriorValidation
    {
        private const string BaselineFloorNavigationSha256 =
            "EFA8B6F3760141AC0A336CB5ACADDB3480C8A82886A6C86FBBB56D8BDEA3C8F8";
        private const string BaselineFurnitureSha256 =
            "683F3F4820C88B01FEC136865D047EEBB94CBC23AEF6FBD1BD71594CDEAED366";
        private const string BaselineSeatSha256 =
            "0002729656BD3C02FED3B1248D51EA6A83650EA4F9CDAFC04744588E3E91E140";
        private static readonly HashSet<string> AddedExteriorTerminalBays =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "wall_front_y0_x12",
                "wall_back_y12_x12",
                "wall_front_x0_y12",
                "wall_back_x12_y12"
            };

        [MenuItem("Family Company/Validate/Office Perimeter Exterior Contract")]
        public static void RunBatch()
        {
            try
            {
                FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid =
                    OfficeGridLayouts.CreateStarterOfficeV1();
                ValidationMetrics metrics = ValidateCurrentLayout(grid);
                Debug.Log(
                    "OFFICE_PERIMETER_EXTERIOR_CONTRACT: PASS | " +
                    $"grid=13x13 floorCells={metrics.FloorCells} walkableCells={metrics.WalkableCells} " +
                    $"furniture={grid.Furniture.Count} preservedFurniture=65 seats={grid.SeatSlots.Count} " +
                    $"perimeter=52 full=26 cutaway=25 openThreshold=1 routes=4 " +
                    $"routeSteps={string.Join(",", metrics.RouteSteps)} " +
                    "floorNavUnchanged=true furniturePositionsUnchanged=true seatsUnchanged=true");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_PERIMETER_EXTERIOR_CONTRACT: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        public static ValidationMetrics ValidateCurrentLayout(
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            Require(grid.Width == 13 && grid.Height == 13, "Starter grid dimensions changed.");
            var floorCells = 0;
            var walkableCells = 0;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (grid.FloorAt(cell) != OfficeFloorTileKind.Void) floorCells++;
                if (grid.IsWalkable(cell)) walkableCells++;
            }
            Require(floorCells == 169 && walkableCells == 100,
                $"Floor/navigation counts changed: floor={floorCells}, walkable={walkableCells}.");
            Require(string.Equals(FloorNavigationSignature(grid), BaselineFloorNavigationSha256,
                    StringComparison.Ordinal),
                "Floor kinds or navigation bounds changed from the pre-rework baseline.");

            PlacedOfficeFurniture[] preservedFurniture = grid.Furniture
                .Where(item => !AddedExteriorTerminalBays.Contains(item.FurnitureId))
                .ToArray();
            Require(preservedFurniture.Length == 65,
                $"Expected 65 pre-existing furniture records, found {preservedFurniture.Length}.");
            Require(string.Equals(FurnitureSignature(preservedFurniture), BaselineFurnitureSha256,
                    StringComparison.Ordinal),
                "A pre-existing furniture coordinate, footprint, facing or blocking flag changed.");
            Require(grid.SeatSlots.Count == 4 &&
                    string.Equals(SeatSignature(grid.SeatSlots), BaselineSeatSha256,
                        StringComparison.Ordinal),
                "Seat sockets or assignments changed from the pre-rework baseline.");

            PlacedOfficeFurniture[] perimeter = grid.Furniture
                .Where(item => IsPerimeterKind(item.KindId))
                .ToArray();
            Require(perimeter.Length == 52, $"Expected 52 exterior bays, found {perimeter.Length}.");
            Require(perimeter.Count(item => item.KindId == OfficeGridLayouts.EntranceWallKind) == 26,
                "Full-height exterior wall count must be 26.");
            Require(perimeter.Count(item => item.KindId == OfficeGridLayouts.PerimeterCutawayWallKind) == 25,
                "Cutaway exterior wall count must be 25.");
            PlacedOfficeFurniture entrance = perimeter.Single(item =>
                string.Equals(item.FurnitureId, "entrance_door", StringComparison.Ordinal));
            Require(entrance.KindId == OfficeGridLayouts.EntranceDoorKind &&
                    entrance.Origin.Equals(new OfficeGridCoordinate(8, 0)) &&
                    entrance.Facing == OfficeFurnitureFacing.SouthEast &&
                    !entrance.BlocksMovement,
                "The open one-bay entrance threshold moved or became blocking.");

            for (var axis = 0; axis < 13; axis++)
            {
                Require(HasBay(perimeter, axis, 0, OfficeFurnitureFacing.SouthEast),
                    $"Front exterior edge is missing bay {axis}.");
                Require(HasBay(perimeter, axis, 12, OfficeFurnitureFacing.SouthEast),
                    $"Back exterior edge is missing bay {axis}.");
                Require(HasBay(perimeter, 0, axis, OfficeFurnitureFacing.SouthWest),
                    $"Left exterior edge is missing bay {axis}.");
                Require(HasBay(perimeter, 12, axis, OfficeFurnitureFacing.SouthWest),
                    $"Right exterior edge is missing bay {axis}.");
            }

            var entranceCell = new OfficeGridCoordinate(8, 1);
            Require(grid.IsWalkable(entranceCell), "The aligned interior entrance cell is not walkable.");
            int[] routeSteps = grid.SeatSlots
                .OrderBy(seat => seat.SeatId, StringComparer.Ordinal)
                .Select(seat => FindWalkableRouteLength(grid, entranceCell, seat.ApproachCell))
                .ToArray();
            Require(routeSteps.Length == 4 && routeSteps.All(length => length > 0),
                "All four entrance-to-seat routes must remain continuous.");
            return new ValidationMetrics(floorCells, walkableCells, routeSteps);
        }

        [MenuItem("Family Company/Validate/Capture Office Perimeter Exterior Baseline")]
        public static void CaptureBaselineBatch()
        {
            try
            {
                FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid =
                    OfficeGridLayouts.CreateStarterOfficeV1();
                int floorCells = 0;
                int walkableCells = 0;
                for (var y = 0; y < grid.Height; y++)
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new OfficeGridCoordinate(x, y);
                    if (grid.FloorAt(cell) != OfficeFloorTileKind.Void) floorCells++;
                    if (grid.IsWalkable(cell)) walkableCells++;
                }

                Debug.Log(
                    "OFFICE_PERIMETER_EXTERIOR_BASELINE: PASS | " +
                    $"grid={grid.Width}x{grid.Height} floorCells={floorCells} walkableCells={walkableCells} " +
                    $"furniture={grid.Furniture.Count} seats={grid.SeatSlots.Count} " +
                    $"floorNavSha256={FloorNavigationSignature(grid)} " +
                    $"furnitureSha256={FurnitureSignature(grid.Furniture)} " +
                    $"seatSha256={SeatSignature(grid.SeatSlots)}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_PERIMETER_EXTERIOR_BASELINE: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static bool HasBay(
            IEnumerable<PlacedOfficeFurniture> perimeter,
            int x,
            int y,
            OfficeFurnitureFacing facing) => perimeter.Any(item =>
            item.Origin.Equals(new OfficeGridCoordinate(x, y)) && item.Facing == facing);

        private static bool IsPerimeterKind(string kindId) =>
            string.Equals(kindId, OfficeGridLayouts.EntranceDoorKind, StringComparison.Ordinal) ||
            string.Equals(kindId, OfficeGridLayouts.EntranceWallKind, StringComparison.Ordinal) ||
            string.Equals(kindId, OfficeGridLayouts.PerimeterCutawayWallKind, StringComparison.Ordinal);

        private static int FindWalkableRouteLength(
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal)
        {
            var queue = new Queue<OfficeGridCoordinate>();
            var distances = new Dictionary<OfficeGridCoordinate, int> { [start] = 1 };
            queue.Enqueue(start);
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0),
                new OfficeGridCoordinate(0, -1),
                new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1)
            };
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                if (current.Equals(goal)) return distances[current];
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!grid.Contains(next) || !grid.IsWalkable(next) || distances.ContainsKey(next))
                        continue;
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }
            return 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public readonly struct ValidationMetrics
        {
            public ValidationMetrics(int floorCells, int walkableCells, int[] routeSteps)
            {
                FloorCells = floorCells;
                WalkableCells = walkableCells;
                RouteSteps = routeSteps ?? Array.Empty<int>();
            }

            public int FloorCells { get; }
            public int WalkableCells { get; }
            public int[] RouteSteps { get; }
        }

        private static string FloorNavigationSignature(
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid)
        {
            var builder = new StringBuilder();
            builder.Append(grid.Width).Append('x').Append(grid.Height).Append('|');
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                builder.Append(x).Append(',').Append(y).Append(':')
                    .Append((int)grid.FloorAt(cell)).Append(':')
                    .Append(grid.IsWalkable(cell) ? '1' : '0').Append(';');
            }
            return Sha256(builder.ToString());
        }

        private static string FurnitureSignature(IEnumerable<PlacedOfficeFurniture> furniture)
        {
            string canonical = string.Join(
                "\n",
                furniture.OrderBy(item => item.FurnitureId, StringComparer.Ordinal).Select(item =>
                    $"{item.FurnitureId}|{item.KindId}|{item.Origin.X},{item.Origin.Y}|" +
                    $"{item.Width}x{item.Height}|{item.Facing}|{item.BlocksMovement}"));
            return Sha256(canonical);
        }

        private static string SeatSignature(IEnumerable<OfficeSeatSlot> seats)
        {
            string canonical = string.Join(
                "\n",
                seats.OrderBy(item => item.SeatId, StringComparer.Ordinal).Select(item =>
                    $"{item.SeatId}|{item.FurnitureId}|{item.Cell.X},{item.Cell.Y}|" +
                    $"{item.ApproachCell.X},{item.ApproachCell.Y}|{item.Facing}|" +
                    $"{item.WorkSurfaceFurnitureId}"));
            return Sha256(canonical);
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}

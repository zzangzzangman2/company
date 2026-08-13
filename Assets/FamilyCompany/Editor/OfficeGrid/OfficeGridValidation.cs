using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Save.OfficeGrid;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;
using OfficeGridState = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class OfficeGridValidation
    {
        [MenuItem("Family Company/Validate Office Grid T1")]
        public static void Run()
        {
            ValidatePreviewIntegrity();
            ValidateStarterOfficeIntegrity();
            ValidateLayoutSaveRoundTrip();
            ValidateFurnitureAndSeatRoundTrip();
            ValidateSchemaOneSeatMigration();
            ValidateSchemaTwoOperatorAnchorMigration();
            ValidateSchemaThreeFurniturePlacementMigration();
            ValidateSchemaFourFurniturePlacementNormalization();
            ValidateInvalidPayloadsAreRejected();
            ValidateV5Migration();
            Debug.Log("FAMILY_COMPANY_OFFICE_GRID_T1_VALIDATION: PASS");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidatePreviewIntegrity()
        {
            var grid = OfficeGridLayouts.CreateMigrationPreview();
            AssertEqual(13, grid.Width, "preview width");
            AssertEqual(13, grid.Height, "preview height");
            AssertEqual(169, grid.CellCount, "preview cell count");
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                var boundary = x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1;
                if (grid.FloorAt(cell) == OfficeFloorTileKind.Void)
                    throw new InvalidOperationException($"Preview floor must render beneath the perimeter wall at {cell}.");
                if (boundary && grid.IsWalkable(cell))
                    throw new InvalidOperationException($"Boundary cell is unexpectedly walkable at {cell}.");
            }

            AssertEqual(false, grid.IsWalkable(new OfficeGridCoordinate(6, 6)), "blocked service cell");
            AssertEqual(false, grid.IsWalkable(new OfficeGridCoordinate(6, 7)), "blocked service cell 2");
            AssertEqual(true, grid.IsWalkable(new OfficeGridCoordinate(5, 6)), "walkable service neighbor");
            AssertEqual(66, grid.Furniture.Count, "preview furniture count");
            AssertEqual(4, grid.SeatSlots.Count, "preview seat count");
            AssertEqual(4, grid.Workstations.Count, "preview workstation count");
            var kindIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in grid.Furniture) kindIds.Add(item.KindId);
            AssertEqual(15, kindIds.Count, "preview furniture kind count");
            foreach (var seat in grid.SeatSlots)
            {
                AssertEqual(true, grid.IsWalkable(seat.Cell), seat.SeatId + " walkable");
                PlacedOfficeFurniture chair = null;
                foreach (var item in grid.Furniture)
                {
                    if (item.FurnitureId == seat.FurnitureId) chair = item;
                }
                if (chair == null) throw new InvalidOperationException("Seat chair is missing: " + seat.SeatId);
                AssertEqual(false, chair.BlocksMovement, seat.SeatId + " chair blocking");
                AssertEqual(seat.Cell, chair.Origin, seat.SeatId + " chair origin");
                AssertEqual(seat.Facing, chair.Facing, seat.SeatId + " chair facing");
                AssertEqual(true, seat.HasWorkstationBinding, seat.SeatId + " workstation binding");
                AssertEqual(true, grid.IsWalkable(seat.ApproachCell), seat.SeatId + " approach walkable");
                AssertEqual(
                    new OfficeGridSubcellAnchor(seat.Cell.X * 2 + 1, seat.Cell.Y * 2 + 1),
                    seat.OperatorAnchor,
                    seat.SeatId + " diagonal half-cell operator anchor");
            }
        }

        private static void ValidateStarterOfficeIntegrity()
        {
            var grid = OfficeGridLayouts.CreateStarterOfficeV1();
            AssertEqual(13, grid.Width, "starter width");
            AssertEqual(13, grid.Height, "starter height");
            AssertEqual(65, grid.Furniture.Count, "starter furniture count");
            AssertEqual(4, grid.SeatSlots.Count, "starter seat count");
            AssertEqual(4, grid.Workstations.Count, "starter workstation count");
            foreach (var item in grid.Furniture)
            {
                if (string.Equals(item.KindId, OfficeGridLayouts.PartitionKind, StringComparison.Ordinal))
                    throw new InvalidOperationException("Starter office must not contain the migration occlusion partition.");
            }
            foreach (var workstation in grid.Workstations)
            {
                AssertEqual(true, grid.IsWalkable(workstation.SeatCell), workstation.SeatId + " starter seat walkable");
                AssertEqual(true, grid.IsWalkable(workstation.ApproachCell), workstation.SeatId + " starter approach walkable");
            }
            foreach (var item in grid.Furniture)
                AssertEqual(true, item.HasCanonicalPlacementAnchor, item.FurnitureId + " canonical floor anchor");
        }

        private static void ValidateLayoutSaveRoundTrip()
        {
            var source = OfficeGridLayouts.CreateMigrationPreview();
            var json = JsonUtility.ToJson(OfficeGridSaveAdapter.ToDto(source));
            RequireContains(json, "\"schemaVersion\":4", "office grid schema");
            RequireContains(json, "\"workSurfaceFurnitureId\"", "workstation binding payload");
            RequireContains(json, "\"approachX\"", "seat approach payload");
            RequireContains(json, "\"operatorX2\"", "subcell operator payload");
            RequireContains(json, "\"floorTiles\"", "floor payload");
            RequireContains(json, "\"walkable\"", "walkable payload");
            RequireNotContains(json, "transform", "semantic save excludes Transform");
            RequireNotContains(json, "worldPosition", "semantic save excludes world position");
            var restored = OfficeGridSaveAdapter.Restore(JsonUtility.FromJson<OfficeGridSaveDto>(json));
            AssertEqual(source.ComputeLayoutHash(), restored.ComputeLayoutHash(), "layout hash roundtrip");
        }

        private static void ValidateSchemaOneSeatMigration()
        {
            var dto = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateMigrationPreview());
            dto.schemaVersion = 1;
            var restored = OfficeGridSaveAdapter.Restore(dto);
            AssertEqual(4, restored.SeatSlots.Count, "schema 1 migrated seat count");
            foreach (var seat in restored.SeatSlots)
            {
                AssertEqual(false, seat.HasWorkstationBinding, seat.SeatId + " schema 1 optional workstation");
                AssertEqual(seat.Cell, seat.ApproachCell, seat.SeatId + " schema 1 approach fallback");
            }
        }

        private static void ValidateSchemaTwoOperatorAnchorMigration()
        {
            var source = OfficeGridLayouts.CreateMigrationPreview();
            var dto = OfficeGridSaveAdapter.ToDto(source);
            dto.schemaVersion = 2;
            foreach (var seat in dto.seatSlots)
            {
                seat.operatorX2 = 0;
                seat.operatorY2 = 0;
            }
            var restored = OfficeGridSaveAdapter.Restore(dto);
            AssertEqual(4, restored.Workstations.Count, "schema 2 workstation count");
            foreach (var seat in restored.SeatSlots)
            {
                var original = source.SeatSlots.Single(item => item.SeatId == seat.SeatId);
                AssertEqual(
                    original.OperatorAnchor,
                    seat.OperatorAnchor,
                    seat.SeatId + " schema 2 inferred operator midpoint");
            }
        }

        private static void ValidateFurnitureAndSeatRoundTrip()
        {
            var floor = new OfficeFloorTileKind[16];
            var walkable = new bool[16];
            for (var index = 0; index < floor.Length; index++)
            {
                floor[index] = OfficeFloorTileKind.WarmWoodA;
                walkable[index] = true;
            }

            walkable[1 + 1 * 4] = false;
            var source = new OfficeGridState(
                4,
                4,
                floor,
                walkable,
                new[]
                {
                    new PlacedOfficeFurniture(
                        "desk_a",
                        OfficeGridLayouts.DeskWithPcKind,
                        new OfficeGridCoordinate(1, 1),
                        1,
                        1,
                        OfficeFurnitureFacing.SouthEast),
                    new PlacedOfficeFurniture(
                        "chair_a",
                        OfficeGridLayouts.SwivelChairKind,
                        new OfficeGridCoordinate(1, 2),
                        1,
                        1,
                        OfficeFurnitureFacing.NorthWest,
                        false)
                },
                new[]
                {
                    new OfficeSeatSlot(
                        "desk_a_seat",
                        "chair_a",
                        new OfficeGridCoordinate(1, 2),
                        OfficeFurnitureFacing.NorthWest)
                });
            var restored = OfficeGridSaveAdapter.Restore(OfficeGridSaveAdapter.ToDto(source));
            AssertEqual(source.ComputeLayoutHash(), restored.ComputeLayoutHash(), "furniture and seat hash roundtrip");
            AssertEqual(2, restored.Furniture.Count, "furniture count");
            AssertEqual(1, restored.SeatSlots.Count, "seat count");
        }

        private static void ValidateSchemaThreeFurniturePlacementMigration()
        {
            var dto = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateStarterOfficeV1());
            dto.schemaVersion = 3;
            foreach (var item in dto.furniture)
            {
                item.placementX2 = 0;
                item.placementY2 = 0;
            }
            var restored = OfficeGridSaveAdapter.Restore(dto);
            foreach (var item in restored.Furniture)
            {
                AssertEqual(
                    PlacedOfficeFurniture.DefaultPlacementAnchor(
                        item.Origin, item.Width, item.Height),
                    item.PlacementAnchor,
                    item.FurnitureId + " schema 3 inferred placement anchor");
            }
        }

        private static void ValidateSchemaFourFurniturePlacementNormalization()
        {
            var dto = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateStarterOfficeV1());
            foreach (var item in dto.furniture)
            {
                item.placementX2 += 1;
                item.placementY2 -= 1;
            }
            var restored = OfficeGridSaveAdapter.Restore(dto);
            foreach (var item in restored.Furniture)
                AssertEqual(true, item.HasCanonicalPlacementAnchor, item.FurnitureId + " schema 4 snapped anchor");
        }

        private static void ValidateInvalidPayloadsAreRejected()
        {
            var shortFloor = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateMigrationPreview());
            shortFloor.floorTiles.RemoveAt(shortFloor.floorTiles.Count - 1);
            ExpectFailure(() => OfficeGridSaveAdapter.Restore(shortFloor), "short floor payload");

            var voidWalkable = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateMigrationPreview());
            var index = 5 + 5 * voidWalkable.width;
            voidWalkable.floorTiles[index] = (int)OfficeFloorTileKind.Void;
            voidWalkable.walkable[index] = true;
            ExpectFailure(() => OfficeGridSaveAdapter.Restore(voidWalkable), "walkable void cell");

            var invalidOperator = OfficeGridSaveAdapter.ToDto(OfficeGridLayouts.CreateMigrationPreview());
            invalidOperator.seatSlots[0].operatorX2 += 4;
            ExpectFailure(() => OfficeGridSaveAdapter.Restore(invalidOperator), "distant operator anchor");
        }

        private static void ValidateV5Migration()
        {
            var dto = GameSaveMapper.ToDto(PrototypeStateFactory.Create(5505));
            dto.schemaVersion = 5;
            dto.officeGrid = null;
            var restored = GameSaveMapper.FromDto(dto);
            AssertEqual(
                OfficeGridLayouts.CreateStarterOfficeV1().ComputeLayoutHash(),
                restored.OfficeGrid.ComputeLayoutHash(),
                "v5 office grid migration");
        }

        private static void ExpectFailure(Action action, string scenario)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                return;
            }

            throw new InvalidOperationException(scenario + ": expected validation failure.");
        }

        private static void RequireContains(string value, string expected, string scenario)
        {
            if (value == null || value.IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(scenario + ": missing " + expected + ".");
        }

        private static void RequireNotContains(string value, string forbidden, string scenario)
        {
            if (value != null && value.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException(scenario + ": found " + forbidden + ".");
        }

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
        }
    }
}

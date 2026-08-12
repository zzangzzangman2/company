using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    /// <summary>
    /// Layout edits, checked without a scene. What matters is that an edit is atomic: the sprite,
    /// the collision footprint, the seat cell, the approach cell and the operator anchor move
    /// together or the whole edit is refused.
    /// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod
    ///   FamilyCompany.Editor.OfficeGrid.OfficeLayoutEditRulesValidation.RunBatch
    /// </summary>
    public static class OfficeLayoutEditRulesValidation
    {
        [MenuItem("Family Company/Validate/Office Layout Edit Rules")]
        public static void Run()
        {
            var failures = new List<string>();
            OfficeGrid start = OfficeGridLayouts.CreateStarterOfficeV1();

            OfficeLayoutEditResult moved = OfficeLayoutEditRules.MoveWorkstation(start, "seat_player", 2, 0);
            Require(failures, moved.Success, "workstation moves two cells east: " + moved.Message);
            if (moved.Success)
            {
                OfficeGrid grid = moved.Grid;
                PlacedOfficeFurniture desk = grid.Furniture.First(f => f.FurnitureId == "desk_player");
                PlacedOfficeFurniture chair = grid.Furniture.First(f => f.FurnitureId == "chair_player");
                OfficeSeatSlot seat = grid.SeatSlots.First(s => s.SeatId == "seat_player");
                Require(failures, desk.Origin.X == 4 && desk.Origin.Y == 4, "desk followed");
                Require(failures, chair.Origin.X == 4 && chair.Origin.Y == 3, "chair followed");
                Require(failures, seat.Cell.X == 4 && seat.Cell.Y == 3, "seat cell followed");
                Require(failures, seat.ApproachCell.X == 4 && seat.ApproachCell.Y == 2, "approach followed");
                Require(failures, seat.OperatorAnchor.X2 == 9 && seat.OperatorAnchor.Y2 == 7, "operator anchor followed");
                Require(failures, grid.IsWalkable(new OfficeGridCoordinate(2, 4)), "vacated cell reopened");
                Require(failures, !grid.IsWalkable(new OfficeGridCoordinate(4, 4)), "new cell blocks movement");
                Require(
                    failures,
                    !string.Equals(grid.ComputeLayoutHash(), start.ComputeLayoutHash(), StringComparison.Ordinal),
                    "layout hash changed");
            }

            OfficeLayoutEditResult promoted = OfficeLayoutEditRules.MoveFurniture(start, "desk_player", 2, 0);
            Require(
                failures,
                promoted.Success && promoted.Grid.SeatSlots.First(s => s.SeatId == "seat_player").Cell.X == 4,
                "moving the desk alone drags its chair and seat");

            Require(
                failures,
                !OfficeLayoutEditRules.MoveWorkstation(start, "seat_player", 5, 0).Success,
                "a move onto another workstation is refused");
            Require(
                failures,
                !OfficeLayoutEditRules.MoveWorkstation(start, "seat_player", -3, 0).Success,
                "a move through the wall is refused");
            Require(
                failures,
                OfficeLayoutEditRules.MoveFurniture(start, "plant", 0, -1).Success,
                "free standing furniture moves on its own");

            OfficeLayoutEditResult removed = OfficeLayoutEditRules.RemoveFurniture(start, "desk_father");
            Require(failures, removed.Success, "removing a desk succeeds");
            if (removed.Success)
            {
                Require(failures, removed.Grid.SeatSlots.All(s => s.SeatId != "seat_father"), "seat removed with it");
                Require(
                    failures,
                    removed.Grid.Furniture.All(f => f.FurnitureId != "chair_father"),
                    "chair removed with it");
                Require(
                    failures,
                    removed.Grid.IsWalkable(new OfficeGridCoordinate(2, 8)),
                    "floor reopened after removal");
            }

            Require(
                failures,
                OfficeLayoutEditRules.RotateWorkstation(start, "seat_player", Array.Empty<OfficeFurnitureFacing>())
                    .Failure == OfficeLayoutEditFailure.RotationUnsupported,
                "rotation is refused explicitly instead of falling back");

            var accepted = 0;
            var refused = 0;
            foreach (string seatId in new[] { "seat_player", "seat_older_sister", "seat_father", "seat_mother" })
            foreach (Vector2Int delta in new[]
                     {
                         new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1),
                         new Vector2Int(0, -1), new Vector2Int(2, 2), new Vector2Int(-2, -2)
                     })
            {
                OfficeLayoutEditResult result =
                    OfficeLayoutEditRules.MoveWorkstation(start, seatId, delta.x, delta.y);
                if (!result.Success) { refused++; continue; }
                accepted++;
                OfficeSeatSlot seat = result.Grid.SeatSlots.First(s => s.SeatId == seatId);
                PlacedOfficeFurniture chair =
                    result.Grid.Furniture.First(f => f.FurnitureId == seat.ChairFurnitureId);
                Require(failures, chair.Origin.Equals(seat.Cell), $"{seatId} {delta}: chair matches seat cell");
                Require(
                    failures,
                    result.Grid.IsWalkable(seat.Cell) && result.Grid.IsWalkable(seat.ApproachCell),
                    $"{seatId} {delta}: seat and approach stay walkable");
            }
            Require(failures, accepted > 0 && refused > 0, "candidate moves split into accepted and refused");

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "OFFICE_LAYOUT_EDIT_RULES_VALIDATION: FAIL | " + string.Join(" | ", failures.Take(10)));
            Debug.Log(
                $"OFFICE_LAYOUT_EDIT_RULES_VALIDATION: PASS | accepted={accepted} refused={refused}");
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
                Debug.LogError(exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(ICollection<string> failures, bool condition, string label)
        {
            if (!condition) failures.Add(label);
        }
    }
}

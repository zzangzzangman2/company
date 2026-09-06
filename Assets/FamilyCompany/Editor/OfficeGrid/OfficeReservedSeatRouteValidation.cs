using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor.OfficeGrid
{
    public static class OfficeReservedSeatRouteValidation
    {
        public static void RunBatch()
        {
            int routes = 0, crossings = 0;
            var examples = new List<string>();
            bool passed = false;
            try
            {
                foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
                {
                    var result = OfficeLayoutEditRules.PlaceWorkstation(OfficeGridLayouts.CreateNewGameEmptyOfficeV1(),
                        "route_desk", "route_chair", "seat_route", new OfficeGridCoordinate(6, 6), facing);
                    if (!result.Success) throw new InvalidOperationException("Workstation setup failed.");
                    var grid = result.Grid;
                    var seat = grid.SeatSlots.Single();
                    var host = new GameObject("ReservedSeatRouteValidation");
                    var tiles = new[] { ScriptableObject.CreateInstance<Tile>(), ScriptableObject.CreateInstance<Tile>(), ScriptableObject.CreateInstance<Tile>() };
                    try
                    {
                        var presenter = host.AddComponent<OfficeGridTilemapPresenter>();
                        presenter.Configure(grid, tiles);
                        var occupancy = new OfficeRuntimeOccupancy();
                        occupancy.Rebuild(grid, presenter);
                        var paths = new OfficeRuntimePathService(grid, occupancy, presenter);
                        for (int x = 1; x < 12; x++)
                        for (int y = 1; y < 12; y++)
                        {
                            var start = new OfficeGridCoordinate(x, y);
                            if (!occupancy.IsCellPassable(start, "route", string.Empty, false) ||
                                !occupancy.CanTraverseStatic(presenter.CellCenterWorld(start), presenter.CellCenterWorld(start), 0.22f, string.Empty)) continue;
                            var path = paths.FindPath("route", start, seat.ApproachCell, seat.SeatId, false, 0.22f);
                            if (path.Count == 0) throw new InvalidOperationException("No open-floor approach route.");
                            routes++;
                            bool crossed = path.Contains(seat.Cell);
                            for (int i = 1; i < path.Count; i++)
                                crossed |= !occupancy.CanTraverseStatic(presenter.CellCenterWorld(path[i - 1]),
                                    presenter.CellCenterWorld(path[i]), 0.22f, string.Empty);
                            if (!crossed) continue;
                            crossings++;
                            if (examples.Count < 8) examples.Add(facing + " " + start + " -> " + seat.ApproachCell + " via " + string.Join("/", path));
                        }
                    }
                    finally { Object.DestroyImmediate(host); foreach (var tile in tiles) Object.DestroyImmediate(tile); }
                }
                passed = routes > 400 && crossings == 0;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            string report = $"RESERVED_SEAT_APPROACH_ROUTES: {(passed ? "PASS" : "FAIL")} routes={routes} furnitureCrossings={crossings}\n" + string.Join("\n", examples);
            Directory.CreateDirectory("Artifacts/NormalAutonomy");
            File.WriteAllText("Artifacts/NormalAutonomy/reserved-seat-routes.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}

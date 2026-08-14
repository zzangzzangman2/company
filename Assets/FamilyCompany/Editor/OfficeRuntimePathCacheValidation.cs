using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using SemanticOfficeGrid = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor
{
    public static class OfficeRuntimePathCacheValidation
    {
        private const int Width = 5;
        private const int Height = 3;

        [MenuItem("Family Company/Validate Runtime Path Cache")]
        public static void Run()
        {
            GameObject root = null;
            Tile[] tiles = null;
            try
            {
                SemanticOfficeGrid open = CreateCorridor(false);
                root = new GameObject("OfficeRuntimePathCacheValidation");
                OfficeGridTilemapPresenter presenter =
                    root.AddComponent<OfficeGridTilemapPresenter>();
                tiles = new[]
                {
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>()
                };
                presenter.Configure(open, tiles);
                ValidateNearestCellParity(presenter, open);
                var occupancy = new OfficeRuntimeOccupancy();
                occupancy.Rebuild(open, presenter);
                var paths = new OfficeRuntimePathService(open, occupancy, presenter);
                var start = new OfficeGridCoordinate(0, 1);
                var goal = new OfficeGridCoordinate(4, 1);

                HashSet<OfficeGridCoordinate> initial =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(initial.Contains(goal), "open corridor did not reach goal");
                Require(paths.StaticGraphBuildCount == 1L, "first query was not one graph miss");
                Require(paths.ReachabilityFloodCount == 1L, "first query was not one flood miss");

                HashSet<OfficeGridCoordinate> repeat =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(ReferenceEquals(initial, repeat), "component cache did not reuse its result");
                Require(paths.StaticGraphBuildCount == 1L, "repeat query rebuilt static graph");
                Require(paths.ReachabilityCacheHitCount == 1L, "repeat query missed component cache");

                int revisionBeforeDynamic = occupancy.Revision;
                Vector3 actorCenter3 = presenter.CellCenterWorld(goal);
                var actorCenter = new Vector2(actorCenter3.x, actorCenter3.y);
                occupancy.RegisterActor("moving_peer", actorCenter, 0.10f);
                occupancy.UpdateActor("moving_peer", actorCenter, Vector2.right, 0f);
                HashSet<OfficeGridCoordinate> withDynamicActor =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(occupancy.Revision == revisionBeforeDynamic,
                    "dynamic actor changed static layout revision");
                Require(withDynamicActor.Contains(goal), "dynamic actor polluted static reachability");
                Require(paths.StaticGraphBuildCount == 1L,
                    "dynamic actor invalidated static graph cache");
                Require(paths.ReachabilityCacheHitCount == 2L,
                    "dynamic actor prevented component cache hit");
                occupancy.UnregisterActor("moving_peer");

                SemanticOfficeGrid blocked = CreateCorridor(true);
                occupancy.Rebuild(blocked, presenter);
                HashSet<OfficeGridCoordinate> afterPlacement =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(!afterPlacement.Contains(goal),
                    "layout invalidation retained stale reachable goal through new wall");
                Require(paths.StaticGraphBuildCount == 2L,
                    "layout placement did not miss and rebuild static graph");
                Require(paths.ReachabilityFloodCount == 2L,
                    "layout placement did not recompute reachable component");

                occupancy.Rebuild(open, presenter);
                HashSet<OfficeGridCoordinate> afterDeletion =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(afterDeletion.Contains(goal),
                    "layout invalidation retained stale blocked component after deletion");
                Require(paths.StaticGraphBuildCount == 3L,
                    "layout deletion did not miss and rebuild static graph");
                Require(paths.ReachabilityFloodCount == 3L,
                    "layout deletion did not recompute reachable component");

                SemanticOfficeGrid placedFurniture = CreateFurnitureCorridor(
                    FurnitureFixture.VerticalBlocking);
                occupancy.Rebuild(placedFurniture, presenter);
                HashSet<OfficeGridCoordinate> afterFurniturePlacement =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(!afterFurniturePlacement.Contains(goal),
                    "furniture placement retained a stale path through its collision footprint");
                Require(paths.StaticGraphBuildCount == 4L &&
                        paths.ReachabilityFloodCount == 4L,
                    "furniture placement did not invalidate and rebuild static reachability");

                SemanticOfficeGrid rotatedFurniture = CreateFurnitureCorridor(
                    FurnitureFixture.HorizontalOpen);
                occupancy.Rebuild(rotatedFurniture, presenter);
                HashSet<OfficeGridCoordinate> afterFurnitureRotation =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(afterFurnitureRotation.Contains(goal),
                    "furniture rotation retained a stale blocked corridor");
                Require(paths.StaticGraphBuildCount == 5L &&
                        paths.ReachabilityFloodCount == 5L,
                    "furniture rotation did not invalidate and rebuild static reachability");

                SemanticOfficeGrid deletedFurniture = CreateFurnitureCorridor(
                    FurnitureFixture.None);
                occupancy.Rebuild(deletedFurniture, presenter);
                HashSet<OfficeGridCoordinate> afterFurnitureDeletion =
                    paths.FindStaticallyReachableCells("qa", start, string.Empty, 0.10f);
                Require(afterFurnitureDeletion.Contains(goal),
                    "furniture deletion retained stale collision reachability");
                Require(paths.StaticGraphBuildCount == 6L &&
                        paths.ReachabilityFloodCount == 6L,
                    "furniture deletion did not invalidate and rebuild static reachability");

                var prewarmedPaths = new OfficeRuntimePathService(
                    deletedFurniture,
                    occupancy,
                    presenter);
                IEnumerator prewarm = prewarmedPaths.PrewarmAllStaticTraversalGraphs(
                    new[] { string.Empty },
                    4,
                    null,
                    0.10f);
                var yieldedFrames = 0;
                while (prewarm.MoveNext()) yieldedFrames++;
                Require(yieldedFrames > 0, "full graph prewarm did not yield between batches");
                prewarmedPaths.ResetPerformanceCounters();
                HashSet<OfficeGridCoordinate> firstPlayQuery =
                    prewarmedPaths.FindStaticallyReachableCells(
                        "qa",
                        start,
                        string.Empty,
                        0.10f);
                Require(firstPlayQuery.Contains(goal), "prewarmed graph lost reachable goal");
                Require(prewarmedPaths.ReachabilityFloodCount == 0L,
                    "first play query moved a flood-fill out of loading");
                Require(prewarmedPaths.ReachabilityCacheHitCount == 1L,
                    "first play query did not hit prewarmed component");
                Require(prewarmedPaths.StaticGraphBuildCount == 0L &&
                        prewarmedPaths.StaticGraphNodeCheckCount == 0L,
                    "first play query lazily built static graph state");
                Require(prewarmedPaths.HasStaticTraversalNeighbor(start, string.Empty, 0.10f),
                    "prewarmed open-cell candidate was rejected");

                Debug.Log(
                    "OFFICE_RUNTIME_PATH_CACHE_VALIDATION: PASS | " +
                    "sameLayoutCalls=3 sameLayoutFloods=1 sameLayoutHits=2 " +
                    "dynamicInvalidations=0 layoutInvalidations=5 " +
                    "furniturePlacement=blocked furnitureRotation=open furnitureDeletion=open " +
                    "prewarmYieldFrames=" + yieldedFrames + " firstPlayFloods=0 " +
                    "graphMisses=" + paths.StaticGraphCacheMissCount +
                    " reachabilityVisited=" + paths.ReachabilityVisitedNodeCount);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (tiles != null)
                    foreach (Tile tile in tiles.Where(item => item != null))
                        Object.DestroyImmediate(tile);
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static SemanticOfficeGrid CreateCorridor(bool blocked)
        {
            OfficeFloorTileKind[] floor = Enumerable.Repeat(
                OfficeFloorTileKind.WarmWoodA,
                Width * Height).ToArray();
            var walkable = new bool[Width * Height];
            for (var x = 0; x < Width; x++) walkable[Width + x] = true;
            if (blocked) walkable[Width + 2] = false;
            return new SemanticOfficeGrid(Width, Height, floor, walkable);
        }

        private static SemanticOfficeGrid CreateFurnitureCorridor(FurnitureFixture fixture)
        {
            OfficeFloorTileKind[] floor = Enumerable.Repeat(
                OfficeFloorTileKind.WarmWoodA,
                Width * Height).ToArray();
            var walkable = new bool[Width * Height];
            for (var x = 0; x < Width; x++) walkable[Width + x] = true;
            var furniture = new List<PlacedOfficeFurniture>();
            if (fixture == FurnitureFixture.VerticalBlocking)
            {
                walkable[Width + 2] = false;
                furniture.Add(new PlacedOfficeFurniture(
                    "qa_rotating_furniture",
                    OfficeGridLayouts.EntranceWallKind,
                    new OfficeGridCoordinate(2, 0),
                    1,
                    2,
                    OfficeFurnitureFacing.NorthEast));
            }
            else if (fixture == FurnitureFixture.HorizontalOpen)
            {
                furniture.Add(new PlacedOfficeFurniture(
                    "qa_rotating_furniture",
                    OfficeGridLayouts.EntranceWallKind,
                    new OfficeGridCoordinate(2, 0),
                    2,
                    1,
                    OfficeFurnitureFacing.SouthEast));
            }
            return new SemanticOfficeGrid(Width, Height, floor, walkable, furniture);
        }

        private enum FurnitureFixture
        {
            None = 0,
            VerticalBlocking = 1,
            HorizontalOpen = 2
        }

        private static void ValidateNearestCellParity(
            OfficeGridTilemapPresenter presenter,
            SemanticOfficeGrid grid)
        {
            Vector3 origin = presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector3 basisX = presenter.CellBasisXWorld();
            Vector3 basisY = presenter.CellBasisYWorld();
            for (var yStep = -8; yStep <= Height * 8 + 8; yStep++)
            for (var xStep = -8; xStep <= Width * 8 + 8; xStep++)
            {
                Vector3 point = origin + basisX * (xStep / 8f) + basisY * (yStep / 8f);
                OfficeGridCoordinate expected = ExhaustiveNearestCell(presenter, grid, point);
                OfficeGridCoordinate actual = presenter.NearestCell(point);
                Require(actual.Equals(expected),
                    $"nearest-cell mismatch point={point} expected={expected} actual={actual} " +
                    $"expectedDistance={(presenter.CellCenterWorld(expected) - point).sqrMagnitude:R} " +
                    $"actualDistance={(presenter.CellCenterWorld(actual) - point).sqrMagnitude:R} " +
                    $"basisX={basisX} basisY={basisY} origin={origin}");
            }
        }

        private static OfficeGridCoordinate ExhaustiveNearestCell(
            OfficeGridTilemapPresenter presenter,
            SemanticOfficeGrid grid,
            Vector3 point)
        {
            var best = new OfficeGridCoordinate(0, 0);
            float bestDistance = float.PositiveInfinity;
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var candidate = new OfficeGridCoordinate(x, y);
                float distance = (presenter.CellCenterWorld(candidate) - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

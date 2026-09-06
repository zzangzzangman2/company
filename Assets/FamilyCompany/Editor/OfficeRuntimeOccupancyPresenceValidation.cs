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
using OfficeGridState = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor
{
    public static class OfficeRuntimeOccupancyPresenceValidation
    {
        private const string ActorA = "presence-a";
        private const string ActorB = "presence-b";
        private const string RuntimeAgentPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeAgent.cs";

        [MenuItem("Family Company/Validate Office Runtime Occupancy Presence")]
        public static void Run()
        {
            ValidateDynamicQueriesAndTrafficSnapshot();
            ValidateAdjacentBodySweptPath();
            ValidateReservationsAndCorridorOwnershipAreReleased();
            ValidateAbsentActorsDoNotAffectSeparationMetrics();
            ValidateRegisteredActorCanMoveWhileAbsentAndReturn();
            ValidateRuntimeAgentPresentationAwayIntegration();
            Debug.Log("OFFICE_RUNTIME_OCCUPANCY_PRESENCE_VALIDATION: PASS");
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

        private static void ValidateDynamicQueriesAndTrafficSnapshot()
        {
            using (var harness = new OccupancyHarness(CreateOpenGrid(7, 7)))
            {
                var actorACell = new OfficeGridCoordinate(3, 3);
                var actorBCell = new OfficeGridCoordinate(1, 3);
                Vector2 actorAPosition = harness.Register(ActorA, actorACell);
                Vector2 actorBPosition = harness.Register(ActorB, actorBCell);

                Require(harness.Occupancy.IsActorPresent(ActorA), "A registered actor must be present.");
                AssertEqual(2, harness.Occupancy.TrafficSnapshot().Count, "initial traffic count");
                Require(!harness.Occupancy.IsCellPassable(actorACell, ActorB, string.Empty, true),
                    "A present actor must block dynamic cell passability.");
                Require(!harness.Occupancy.CanMove(
                        ActorB,
                        actorBPosition,
                        actorAPosition,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty),
                    "A present actor must block movement into its position.");
                Require(!harness.Occupancy.HasPresentationClearance(
                        ActorB,
                        actorBPosition,
                        actorAPosition,
                        OfficeRuntimeAgent.DefaultRadius),
                    "A present actor must block presentation clearance.");

                harness.Occupancy.SetActorPresent(ActorA, false);
                harness.Occupancy.SetActorPresent(ActorA, false);
                Require(!harness.Occupancy.IsActorPresent(ActorA),
                    "Repeated absence updates must remain idempotently absent.");
                AssertEqual(1, harness.Occupancy.TrafficSnapshot().Count, "absent traffic count");
                Require(harness.Occupancy.IsCellPassable(actorACell, ActorB, string.Empty, true),
                    "An absent actor must not block dynamic cell passability.");
                Require(harness.Occupancy.CanMove(
                        ActorB,
                        actorBPosition,
                        actorAPosition,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty),
                    "An absent actor must not block movement.");
                Require(harness.Occupancy.HasPresentationClearance(
                        ActorB,
                        actorBPosition,
                        actorAPosition,
                        OfficeRuntimeAgent.DefaultRadius),
                    "An absent actor must not block presentation clearance.");

                harness.Occupancy.SetActorPresent(ActorA, true);
                harness.Occupancy.SetActorPresent(ActorA, true);
                Require(harness.Occupancy.IsActorPresent(ActorA),
                    "Repeated return updates must remain idempotently present.");
                AssertEqual(2, harness.Occupancy.TrafficSnapshot().Count, "returned traffic count");
                Require(!harness.Occupancy.IsCellPassable(actorACell, ActorB, string.Empty, true),
                    "A returned actor must rejoin dynamic occupancy.");
            }
        }

        private static void ValidateAdjacentBodySweptPath()
        {
            var grid = CreateOpenGrid(9, 9);
            using (var harness = new OccupancyHarness(grid))
            {
                var start = new OfficeGridCoordinate(2, 4);
                var middle = new OfficeGridCoordinate(4, 4);
                var goal = new OfficeGridCoordinate(6, 4);
                Vector2 centre = harness.Position(middle);
                Vector2 edge = harness.Position(new OfficeGridCoordinate(5, 4)) - centre;
                Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
                harness.Occupancy.RegisterActor(ActorA, harness.Position(start), 0.35f, 0.22f);
                // Its nearest tile differs from the route, but the two bodies still collide.
                harness.Occupancy.RegisterActor(ActorB, centre + normal * 0.55f, 0.35f, 0.22f);
                Require(harness.Occupancy.IsCellPassable(middle, ActorA, "", true), "fixture: route cell must look free");
                Require(!harness.Occupancy.CanTraverseDynamic(ActorA,
                    harness.Position(new OfficeGridCoordinate(3, 4)), centre), "adjacent body blocks swept edge");
                var paths = new OfficeRuntimePathService(grid, harness.Occupancy, harness.Presenter);
                var route = paths.FindPath(ActorA, start, goal, "", true, 0.22f);
                Require(route.Count > 0, "dynamic swept-body detour must remain reachable");
                for (int i = 1; i < route.Count; i++)
                    Require(harness.Occupancy.CanMove(ActorA, harness.Position(route[i - 1]),
                        harness.Position(route[i]), 0.22f, ""), "planned detour must be executable without overlap");
                harness.Occupancy.SetActorPresent(ActorB, false);
                Require(harness.Occupancy.CanTraverseDynamic(ActorA,
                    harness.Position(new OfficeGridCoordinate(3, 4)), centre), "absent body no longer blocks edge");
            }
        }

        private static void ValidateReservationsAndCorridorOwnershipAreReleased()
        {
            using (var harness = new OccupancyHarness(CreateHorizontalCorridorGrid()))
            {
                var actorACell = new OfficeGridCoordinate(2, 2);
                var actorBCell = new OfficeGridCoordinate(5, 2);
                harness.Register(ActorA, actorACell);
                harness.Register(ActorB, actorBCell);
                var actorAUpcoming = new[] { new OfficeGridCoordinate(3, 2) };
                var actorBUpcoming = new[] { new OfficeGridCoordinate(4, 2) };

                Require(harness.Occupancy.TryReservePath(ActorA, actorACell, actorAUpcoming),
                    "Actor A must acquire the corridor initially.");
                Require(!harness.Occupancy.TryReservePath(ActorB, actorBCell, actorBUpcoming),
                    "Actor B must not acquire an owned narrow corridor.");

                harness.Occupancy.SetActorPresent(ActorA, false);
                Require(harness.Occupancy.TryReservePath(ActorB, actorBCell, actorBUpcoming),
                    "Marking A absent must release its reservations and narrow-corridor ownership.");
                Require(!harness.Occupancy.TryReservePath(ActorA, actorACell, actorAUpcoming),
                    "An absent actor must not create new path reservations.");
                Require(harness.Occupancy.IsCellPassable(actorACell, ActorB, string.Empty, true),
                    "An absent actor's old cell and reservations must not block peers.");

                harness.Occupancy.ClearReservations(ActorB);
                harness.Occupancy.SetActorPresent(ActorA, true);
                Require(harness.Occupancy.TryReservePath(ActorA, actorACell, actorAUpcoming),
                    "A returned actor must reserve again without being re-registered.");
            }
        }

        private static void ValidateAbsentActorsDoNotAffectSeparationMetrics()
        {
            using (var harness = new OccupancyHarness(CreateOpenGrid(7, 7)))
            {
                harness.Register(ActorA, new OfficeGridCoordinate(3, 3));
                Vector2 actorBPosition = harness.Register(ActorB, new OfficeGridCoordinate(1, 3));
                harness.Occupancy.SetActorPresent(ActorA, false);

                harness.Occupancy.ResetMetrics();
                harness.Occupancy.UpdateActor(ActorA, actorBPosition, Vector2.zero, 0f);
                harness.Occupancy.UpdateActor(ActorB, actorBPosition, Vector2.zero, 0f);
                AssertEqual(0, harness.Occupancy.AgentPenetrationCount,
                    "absent actor penetration count");
                Require(float.IsPositiveInfinity(harness.Occupancy.MinimumAgentSeparationMargin),
                    "An absent actor must not contribute a minimum-separation sample.");

                harness.Occupancy.SetActorPresent(ActorA, true);
                harness.Occupancy.ResetMetrics();
                harness.Occupancy.UpdateActor(ActorB, actorBPosition, Vector2.zero, 0f);
                Require(harness.Occupancy.AgentPenetrationCount > 0,
                    "A returned overlapping actor must contribute a penetration violation.");
                Require(harness.Occupancy.MinimumAgentSeparationMargin < 0f,
                    "A returned overlapping actor must contribute a negative separation margin.");

            }
        }

        private static void ValidateRegisteredActorCanMoveWhileAbsentAndReturn()
        {
            using (var harness = new OccupancyHarness(CreateOpenGrid(7, 7)))
            {
                var start = new OfficeGridCoordinate(2, 2);
                var returnCell = new OfficeGridCoordinate(4, 4);
                harness.Register(ActorA, start);
                harness.Occupancy.SetActorPresent(ActorA, false);
                harness.Occupancy.UpdateActor(
                    ActorA,
                    harness.Position(returnCell),
                    new Vector2(0.4f, 0.1f),
                    3f);

                AssertEqual(returnCell, harness.Occupancy.CurrentCell(ActorA),
                    "absent actor stored return cell");
                AssertEqual(0, harness.Occupancy.TrafficSnapshot().Count,
                    "moving absent actor traffic count");

                harness.Occupancy.SetActorPresent(ActorA, true);
                AssertEqual(1, harness.Occupancy.TrafficSnapshot().Count,
                    "re-enabled registered actor traffic count");
                AssertEqual(returnCell, harness.Occupancy.CurrentCell(ActorA),
                    "re-enabled actor current cell");
            }
        }

        private static void ValidateRuntimeAgentPresentationAwayIntegration()
        {
            string source = File.ReadAllText(Path.GetFullPath(RuntimeAgentPath));
            string awayMethod = SourceSection(
                source,
                "private void SetPresentationAway(bool away)",
                "private static int FacingDirection");
            Require(awayMethod.Contains(
                    "_world.Occupancy.SetActorPresent(_agentId, !away);"),
                "Presentation-away state must update registered runtime occupancy presence.");

            string resetMethod = SourceSection(
                source,
                "public void ResetRuntimeState()",
                "public void SetPlayerInput");
            Require(resetMethod.Contains("SetPresentationAway(false);"),
                "Resetting an away runtime agent must restore occupancy presence.");

            string completionMethod = SourceSection(
                source,
                "private void CompleteNavigation()",
                "private void TickSeating");
            Require(completionMethod.Contains("SetPresentationAway(true);"),
                "Completing an outside route must remove the agent from dynamic occupancy.");

            string beginMethod = SourceSection(
                source,
                "private bool BeginDestination(OfficeRuntimeDestination destination)",
                "private bool RebuildPath()");
            int returnCheck = beginMethod.IndexOf("if (_presentationAway)", StringComparison.Ordinal);
            int restorePresence = beginMethod.IndexOf("SetPresentationAway(false);", StringComparison.Ordinal);
            Require(returnCheck >= 0 && restorePresence > returnCheck,
                "An away actor must check its return cell before restoring dynamic presence.");
            Require(beginMethod.Contains("_world.Occupancy.IsCellPassable("),
                "An away actor must not reappear through a present actor or live reservation.");
        }

        private static string SourceSection(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = start < 0
                ? -1
                : source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException(
                    $"Unable to resolve source section '{startMarker}' -> '{endMarker}'.");
            return source.Substring(start, end - start);
        }

        private static OfficeGridState CreateOpenGrid(int width, int height)
        {
            int count = checked(width * height);
            return new OfficeGridState(
                width,
                height,
                Enumerable.Repeat(OfficeFloorTileKind.WarmWoodA, count).ToArray(),
                Enumerable.Repeat(true, count).ToArray());
        }

        private static OfficeGridState CreateHorizontalCorridorGrid()
        {
            const int width = 7;
            const int height = 5;
            int count = width * height;
            var walkable = new bool[count];
            for (var x = 1; x <= 5; x++) walkable[2 * width + x] = true;
            return new OfficeGridState(
                width,
                height,
                Enumerable.Repeat(OfficeFloorTileKind.WarmWoodA, count).ToArray(),
                walkable);
        }

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class OccupancyHarness : IDisposable
        {
            private readonly GameObject _root;
            private readonly Tile[] _tiles;

            public OccupancyHarness(OfficeGridState grid)
            {
                _root = new GameObject("Office Runtime Occupancy Presence QA");
                _tiles = new[]
                {
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>()
                };
                Presenter = _root.AddComponent<OfficeGridTilemapPresenter>();
                Presenter.Configure(grid, _tiles);
                Occupancy = new OfficeRuntimeOccupancy();
                Occupancy.Rebuild(grid, Presenter);
            }

            public OfficeGridTilemapPresenter Presenter { get; }
            public OfficeRuntimeOccupancy Occupancy { get; }

            public Vector2 Position(OfficeGridCoordinate cell)
            {
                Vector3 position = Presenter.CellCenterWorld(cell);
                return new Vector2(position.x, position.y);
            }

            public Vector2 Register(string actorId, OfficeGridCoordinate cell)
            {
                Vector2 position = Position(cell);
                Occupancy.RegisterActor(actorId, position, OfficeRuntimeAgent.DefaultRadius);
                return position;
            }

            public void Dispose()
            {
                if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
                foreach (Tile tile in _tiles)
                    if (tile != null) UnityEngine.Object.DestroyImmediate(tile);
            }
        }
    }
}

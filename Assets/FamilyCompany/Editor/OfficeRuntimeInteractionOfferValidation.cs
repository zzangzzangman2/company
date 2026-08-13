using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using OfficeGridState = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor
{
    public static class OfficeRuntimeInteractionOfferValidation
    {
        private static readonly OfficeGridCoordinate Start = new OfficeGridCoordinate(4, 6);

        [MenuItem("Family Company/Validate Office Interaction Offers")]
        public static void Run()
        {
            ValidateCanonicalOffers();
            ValidateRemovedFurnitureAdvertisesNothing();
            ValidateMovedFurnitureHasNoStaleApproach();
            ValidateUnreachableFurnitureAdvertisesNothing();
            ValidateBlockedApproachAdvertisesNothing();
            ValidateMovedSofaTracksInstance();
            ValidateMultipleFurnitureCreatesMultipleOffers();
            Debug.Log("OFFICE_RUNTIME_INTERACTION_OFFER_VALIDATION: PASS");
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

        private static void ValidateCanonicalOffers()
        {
            OfficeGridState grid = OfficeGridLayouts.CreateStarterOfficeV1();
            using (var harness = new ResolverHarness(grid))
            {
                AssertFurnitureOffer(harness, "water-drink", "father", "water", 1);
                AssertFurnitureOffer(harness, "copier-use", "father", "copier", 1);
                AssertFurnitureOffer(harness, "filing-read", "father", "bookcase", 1);
                AssertFurnitureOffer(harness, "lounge-chat", "father", "sofa", 2);
                AssertFurnitureOffer(harness, "coffee-drink", "mother", "coffee", 2);
                AssertFurnitureOffer(harness, "desk-typing", "father", "desk_father", 1);
            }
        }

        private static void ValidateRemovedFurnitureAdvertisesNothing()
        {
            OfficeGridState withoutWater = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Where(item => item.FurnitureId != "water"),
                walkable => Set(walkable, 11, 5, true));
            using (var harness = new ResolverHarness(withoutWater))
                AssertEqual(0, Offers(harness, "water-drink", "father").Count, "removed water offer count");

            OfficeGridState withoutCopier = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Where(item => item.FurnitureId != "copier"),
                walkable => Set(walkable, 10, 2, true));
            using (var harness = new ResolverHarness(withoutCopier))
                AssertEqual(0, Offers(harness, "copier-use", "father").Count, "removed copier offer count");
        }

        private static void ValidateMovedFurnitureHasNoStaleApproach()
        {
            var oldApproach = new HashSet<OfficeGridCoordinate>
            {
                new OfficeGridCoordinate(10, 5),
                new OfficeGridCoordinate(11, 4),
                new OfficeGridCoordinate(11, 6)
            };
            var moved = new PlacedOfficeFurniture(
                "water-moved",
                OfficeGridLayouts.WaterDispenserKind,
                new OfficeGridCoordinate(5, 6),
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                true);
            OfficeGridState grid = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Where(item => item.FurnitureId != "water").Concat(new[] { moved }),
                walkable =>
                {
                    Set(walkable, 11, 5, true);
                    Set(walkable, 5, 6, false);
                });
            using (var harness = new ResolverHarness(grid))
            {
                OfficeInteractionOffer offer = SingleOffer(harness, "water-drink", "father");
                AssertEqual("water-moved", offer.FurnitureId, "moved water furniture id");
                Require(offer.ApproachCells.All(cell => !oldApproach.Contains(cell)),
                    "Moved water reused an approach cell from the previous placement.");
            }
        }

        private static void ValidateUnreachableFurnitureAdvertisesNothing()
        {
            OfficeGridState grid = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture,
                walkable =>
                {
                    for (var y = 1; y < OfficeGridLayouts.StarterOfficeHeight - 1; y++)
                        Set(walkable, 6, y, false);
                });
            using (var harness = new ResolverHarness(grid))
                AssertEqual(0, Offers(harness, "water-drink", "father").Count, "unreachable water offer count");
        }

        private static void ValidateBlockedApproachAdvertisesNothing()
        {
            PlacedOfficeFurniture[] blockers =
            {
                Blocker("bookcase-block-east", 2, 10),
                Blocker("bookcase-block-north", 1, 9),
                Blocker("bookcase-block-south", 1, 11)
            };
            OfficeGridState grid = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Concat(blockers),
                walkable =>
                {
                    Set(walkable, 2, 10, false);
                    Set(walkable, 1, 9, false);
                    Set(walkable, 1, 11, false);
                });
            using (var harness = new ResolverHarness(grid))
                AssertEqual(0, Offers(harness, "filing-read", "father").Count, "blocked bookcase offer count");
        }

        private static void ValidateMovedSofaTracksInstance()
        {
            var moved = new PlacedOfficeFurniture(
                "sofa-moved",
                OfficeGridLayouts.SofaKind,
                new OfficeGridCoordinate(7, 10),
                2,
                1,
                OfficeFurnitureFacing.SouthEast,
                true);
            OfficeGridState grid = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Where(item => item.FurnitureId != "sofa").Concat(new[] { moved }),
                walkable =>
                {
                    Set(walkable, 9, 10, true);
                    Set(walkable, 10, 10, true);
                    Set(walkable, 7, 10, false);
                    Set(walkable, 8, 10, false);
                });
            using (var harness = new ResolverHarness(grid))
                AssertEqual("sofa-moved", SingleOffer(harness, "lounge-chat", "father").FurnitureId,
                    "moved sofa furniture id");
        }

        private static void ValidateMultipleFurnitureCreatesMultipleOffers()
        {
            var second = new PlacedOfficeFurniture(
                "coffee-2",
                OfficeGridLayouts.CoffeeTableKind,
                new OfficeGridCoordinate(4, 5),
                2,
                1,
                OfficeFurnitureFacing.SouthEast,
                true);
            OfficeGridState grid = Mutate(
                OfficeGridLayouts.CreateStarterOfficeV1(),
                furniture => furniture.Concat(new[] { second }),
                walkable =>
                {
                    Set(walkable, 4, 5, false);
                    Set(walkable, 5, 5, false);
                });
            using (var harness = new ResolverHarness(grid))
            {
                IReadOnlyList<OfficeInteractionOffer> offers = Offers(harness, "coffee-drink", "mother");
                AssertEqual(2, offers.Count, "two coffee table offer count");
                AssertEqual(2, offers.Select(offer => offer.OfferId).Distinct(StringComparer.Ordinal).Count(),
                    "two coffee table distinct offer IDs");
                Require(offers.All(offer => offer.Capacity == 2),
                    "Coffee capacity must be copied to every furniture instance offer.");
            }
        }

        private static void AssertFurnitureOffer(
            ResolverHarness harness,
            string interactionId,
            string memberId,
            string furnitureId,
            int capacity)
        {
            OfficeInteractionOffer offer = SingleOffer(harness, interactionId, memberId);
            AssertEqual(furnitureId, offer.FurnitureId, interactionId + " furniture");
            AssertEqual(interactionId + "@" + furnitureId, offer.OfferId, interactionId + " offer ID");
            AssertEqual(capacity, offer.Capacity, interactionId + " capacity");
            Require(offer.ApproachCells.Count > 0, interactionId + " has no reachable approach cells.");
        }

        private static OfficeInteractionOffer SingleOffer(
            ResolverHarness harness,
            string interactionId,
            string memberId)
        {
            IReadOnlyList<OfficeInteractionOffer> offers = Offers(harness, interactionId, memberId);
            AssertEqual(1, offers.Count, interactionId + " offer count");
            return offers[0];
        }

        private static IReadOnlyList<OfficeInteractionOffer> Offers(
            ResolverHarness harness,
            string interactionId,
            string memberId)
        {
            Require(OfficeInteractionCatalog.TryGetDefinition(interactionId, out OfficeInteractionDefinition definition),
                "Missing interaction definition: " + interactionId);
            return harness.Resolver.ResolveReachableOffers(definition, memberId, Start);
        }

        private static OfficeGridState Mutate(
            OfficeGridState source,
            Func<IEnumerable<PlacedOfficeFurniture>, IEnumerable<PlacedOfficeFurniture>> mutateFurniture,
            Action<bool[]> mutateWalkable)
        {
            bool[] walkable = source.CopyWalkable();
            mutateWalkable(walkable);
            return new OfficeGridState(
                source.Width,
                source.Height,
                source.CopyFloorTiles(),
                walkable,
                mutateFurniture(source.Furniture).ToArray(),
                source.SeatSlots.ToArray());
        }

        private static void Set(bool[] walkable, int x, int y, bool value)
        {
            walkable[y * OfficeGridLayouts.StarterOfficeWidth + x] = value;
        }

        private static PlacedOfficeFurniture Blocker(string id, int x, int y)
        {
            return new PlacedOfficeFurniture(
                id,
                OfficeGridLayouts.PottedPlantKind,
                new OfficeGridCoordinate(x, y),
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                true);
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

        private sealed class ResolverHarness : IDisposable
        {
            private readonly GameObject _root;
            private readonly Tile[] _tiles;
            private readonly Dictionary<string, OfficeSeatSlot> _assignedSeats;

            public ResolverHarness(OfficeGridState grid)
            {
                _root = new GameObject("Office Interaction Offer QA");
                _tiles = new[]
                {
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>()
                };
                var presenter = _root.AddComponent<OfficeGridTilemapPresenter>();
                presenter.Configure(grid, _tiles);
                var occupancy = new OfficeRuntimeOccupancy();
                occupancy.Rebuild(grid, presenter);
                var paths = new OfficeRuntimePathService(grid, occupancy, presenter);
                _assignedSeats = grid.SeatSlots
                    .Where(seat => seat.SeatId.StartsWith("seat_", StringComparison.Ordinal))
                    .ToDictionary(
                        seat => seat.SeatId.Substring("seat_".Length),
                        seat => seat,
                        StringComparer.Ordinal);
                Resolver = new OfficeRuntimeInteractionOfferResolver(
                    grid,
                    presenter,
                    occupancy,
                    paths,
                    memberId => _assignedSeats.TryGetValue(memberId ?? string.Empty, out OfficeSeatSlot seat)
                        ? seat
                        : null);
            }

            public OfficeRuntimeInteractionOfferResolver Resolver { get; }

            public void Dispose()
            {
                if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
                foreach (Tile tile in _tiles)
                    if (tile != null) UnityEngine.Object.DestroyImmediate(tile);
            }
        }
    }
}

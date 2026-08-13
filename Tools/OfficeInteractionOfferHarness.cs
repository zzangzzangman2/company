using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;

internal static class OfficeInteractionOfferHarness
{
    private static readonly OfficeGridCoordinate Start = new OfficeGridCoordinate(4, 6);
    private static readonly OfficeGridCoordinate[] Neighbors =
    {
        new OfficeGridCoordinate(1, 0),
        new OfficeGridCoordinate(0, -1),
        new OfficeGridCoordinate(-1, 0),
        new OfficeGridCoordinate(0, 1)
    };

    public static int Main()
    {
        try
        {
            AssertEqual(20, OfficeInteractionCatalog.All.Count, "catalog definition count");
            AssertEqual(13, OfficeInteractionCatalog.All.Select(item => item.MicroAction).Distinct().Count(),
                "catalog action count");
            ValidateCanonicalAndAssignedOffers();
            ValidateRemovalAndBlockedApproach();
            ValidateMoveAndMultipleInstances();
            ValidateUnreachableFurniture();
            Console.WriteLine("OFFICE_INTERACTION_OFFER_EXTERNAL: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ValidateCanonicalAndAssignedOffers()
    {
        OfficeGrid grid = OfficeGridLayouts.CreateStarterOfficeV1();
        AssertOffer(grid, "water-drink", "father", "water", 1);
        AssertOffer(grid, "copier-use", "father", "copier", 1);
        AssertOffer(grid, "filing-read", "father", "bookcase", 1);
        AssertOffer(grid, "lounge-chat", "father", "sofa", 2);
        AssertOffer(grid, "coffee-drink", "mother", "coffee", 2);
        AssertOffer(grid, "desk-typing", "father", "desk_father", 1);
    }

    private static void ValidateRemovalAndBlockedApproach()
    {
        OfficeGrid withoutWater = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Where(item => item.FurnitureId != "water"),
            walkable => Set(walkable, 11, 5, true));
        AssertEqual(0, Offers(withoutWater, "water-drink", "father").Count, "removed water");

        OfficeGrid withoutCopier = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Where(item => item.FurnitureId != "copier"),
            walkable => Set(walkable, 10, 2, true));
        AssertEqual(0, Offers(withoutCopier, "copier-use", "father").Count, "removed copier");

        PlacedOfficeFurniture[] blockers =
        {
            Blocker("bookcase-block-east", 2, 10),
            Blocker("bookcase-block-north", 1, 9),
            Blocker("bookcase-block-south", 1, 11)
        };
        OfficeGrid blocked = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Concat(blockers),
            walkable =>
            {
                Set(walkable, 2, 10, false);
                Set(walkable, 1, 9, false);
                Set(walkable, 1, 11, false);
            });
        AssertEqual(0, Offers(blocked, "filing-read", "father").Count, "blocked bookcase");
    }

    private static void ValidateMoveAndMultipleInstances()
    {
        var movedWater = new PlacedOfficeFurniture(
            "water-moved",
            OfficeGridLayouts.WaterDispenserKind,
            new OfficeGridCoordinate(5, 6),
            1,
            1,
            OfficeFurnitureFacing.SouthEast,
            true);
        OfficeGrid movedWaterGrid = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Where(item => item.FurnitureId != "water").Concat(new[] { movedWater }),
            walkable =>
            {
                Set(walkable, 11, 5, true);
                Set(walkable, 5, 6, false);
            });
        OfficeInteractionOffer movedOffer = Single(Offers(movedWaterGrid, "water-drink", "father"));
        AssertEqual("water-moved", movedOffer.FurnitureId, "moved water furniture");
        var stale = new HashSet<OfficeGridCoordinate>
        {
            new OfficeGridCoordinate(10, 5),
            new OfficeGridCoordinate(11, 4),
            new OfficeGridCoordinate(11, 6)
        };
        Require(movedOffer.ApproachCells.All(cell => !stale.Contains(cell)), "moved water reused stale approach");

        var movedSofa = new PlacedOfficeFurniture(
            "sofa-moved",
            OfficeGridLayouts.SofaKind,
            new OfficeGridCoordinate(7, 10),
            2,
            1,
            OfficeFurnitureFacing.SouthEast,
            true);
        OfficeGrid movedSofaGrid = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Where(item => item.FurnitureId != "sofa").Concat(new[] { movedSofa }),
            walkable =>
            {
                Set(walkable, 9, 10, true);
                Set(walkable, 10, 10, true);
                Set(walkable, 7, 10, false);
                Set(walkable, 8, 10, false);
            });
        AssertEqual("sofa-moved", Single(Offers(movedSofaGrid, "lounge-chat", "father")).FurnitureId,
            "moved sofa furniture");

        var secondCoffee = new PlacedOfficeFurniture(
            "coffee-2",
            OfficeGridLayouts.CoffeeTableKind,
            new OfficeGridCoordinate(4, 5),
            2,
            1,
            OfficeFurnitureFacing.SouthEast,
            true);
        OfficeGrid twoCoffee = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture.Concat(new[] { secondCoffee }),
            walkable =>
            {
                Set(walkable, 4, 5, false);
                Set(walkable, 5, 5, false);
            });
        IReadOnlyList<OfficeInteractionOffer> offers = Offers(twoCoffee, "coffee-drink", "mother");
        AssertEqual(2, offers.Count, "two coffee offers");
        AssertEqual(2, offers.Select(offer => offer.OfferId).Distinct(StringComparer.Ordinal).Count(),
            "two coffee IDs");
        Require(offers.All(offer => offer.Capacity == 2), "coffee capacity was not copied per instance");
    }

    private static void ValidateUnreachableFurniture()
    {
        OfficeGrid grid = Mutate(
            OfficeGridLayouts.CreateStarterOfficeV1(),
            furniture => furniture,
            walkable =>
            {
                for (var y = 1; y < OfficeGridLayouts.StarterOfficeHeight - 1; y++)
                    Set(walkable, 6, y, false);
            });
        AssertEqual(0, Offers(grid, "water-drink", "father").Count, "unreachable water");
    }

    private static void AssertOffer(
        OfficeGrid grid,
        string interactionId,
        string memberId,
        string furnitureId,
        int capacity)
    {
        OfficeInteractionOffer offer = Single(Offers(grid, interactionId, memberId));
        AssertEqual(furnitureId, offer.FurnitureId, interactionId + " furniture");
        AssertEqual(interactionId + "@" + furnitureId, offer.OfferId, interactionId + " ID");
        AssertEqual(capacity, offer.Capacity, interactionId + " capacity");
    }

    private static IReadOnlyList<OfficeInteractionOffer> Offers(
        OfficeGrid grid,
        string interactionId,
        string memberId)
    {
        Require(OfficeInteractionCatalog.TryGetDefinition(interactionId, out OfficeInteractionDefinition definition),
            "missing interaction " + interactionId);
        OfficeSeatSlot seat = grid.SeatSlots.FirstOrDefault(item =>
            string.Equals(item.SeatId, "seat_" + memberId, StringComparison.Ordinal));
        return OfficeInteractionOfferFactory.Resolve(
            definition,
            grid,
            memberId,
            Start,
            seat,
            cell => grid.Contains(cell) && grid.IsWalkable(cell),
            cell => HasPath(grid, Start, cell));
    }

    private static bool HasPath(OfficeGrid grid, OfficeGridCoordinate start, OfficeGridCoordinate goal)
    {
        if (!grid.Contains(start) || !grid.Contains(goal) || !grid.IsWalkable(goal)) return false;
        var queue = new Queue<OfficeGridCoordinate>();
        var visited = new HashSet<OfficeGridCoordinate> { start };
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            OfficeGridCoordinate current = queue.Dequeue();
            if (current.Equals(goal)) return true;
            foreach (OfficeGridCoordinate offset in Neighbors)
            {
                var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                if (!grid.Contains(next) || !grid.IsWalkable(next) || !visited.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }

    private static OfficeGrid Mutate(
        OfficeGrid source,
        Func<IEnumerable<PlacedOfficeFurniture>, IEnumerable<PlacedOfficeFurniture>> mutateFurniture,
        Action<bool[]> mutateWalkable)
    {
        bool[] walkable = source.CopyWalkable();
        mutateWalkable(walkable);
        return new OfficeGrid(
            source.Width,
            source.Height,
            source.CopyFloorTiles(),
            walkable,
            mutateFurniture(source.Furniture).ToArray(),
            source.SeatSlots.ToArray());
    }

    private static OfficeInteractionOffer Single(IReadOnlyList<OfficeInteractionOffer> offers)
    {
        AssertEqual(1, offers.Count, "single offer count");
        return offers[0];
    }

    private static void Set(bool[] walkable, int x, int y, bool value) =>
        walkable[y * OfficeGridLayouts.StarterOfficeWidth + x] = value;

    private static PlacedOfficeFurniture Blocker(string id, int x, int y) =>
        new PlacedOfficeFurniture(
            id,
            OfficeGridLayouts.PottedPlantKind,
            new OfficeGridCoordinate(x, y),
            1,
            1,
            OfficeFurnitureFacing.SouthEast,
            true);

    private static void AssertEqual<T>(T expected, T actual, string scenario)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

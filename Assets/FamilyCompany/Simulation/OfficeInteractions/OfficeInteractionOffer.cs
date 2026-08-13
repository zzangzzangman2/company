using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    /// <summary>
    /// One interaction advertised by one concrete furniture instance (or one explicit virtual
    /// location). Runtime pathfinding supplies the reachable approach cells; the definition remains
    /// the single source of truth for kind, location, capacity, and approach policy.
    /// </summary>
    public sealed class OfficeInteractionOffer
    {
        private readonly OfficeGridCoordinate[] _approachCells;

        public OfficeInteractionOffer(
            string offerId,
            string interactionId,
            string furnitureId,
            string furnitureKindId,
            OfficeSemanticLocation location,
            IEnumerable<OfficeGridCoordinate> approachCells,
            int capacity)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                throw new ArgumentException("Offer ID is required.", nameof(offerId));
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

            _approachCells = (approachCells ?? throw new ArgumentNullException(nameof(approachCells)))
                .Distinct()
                .OrderBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToArray();
            if (_approachCells.Length == 0)
                throw new ArgumentException("At least one reachable approach cell is required.", nameof(approachCells));

            OfferId = offerId.Trim();
            InteractionId = interactionId.Trim();
            FurnitureId = furnitureId?.Trim() ?? string.Empty;
            FurnitureKindId = furnitureKindId?.Trim() ?? string.Empty;
            Location = location;
            Capacity = capacity;
        }

        public string OfferId { get; }
        public string InteractionId { get; }
        public string FurnitureId { get; }
        public string FurnitureKindId { get; }
        public OfficeSemanticLocation Location { get; }
        public IReadOnlyList<OfficeGridCoordinate> ApproachCells => _approachCells;
        public int Capacity { get; }
        public bool IsFurnitureOffer => FurnitureId.Length > 0;
    }

    /// <summary>
    /// Projects one definition over an immutable semantic layout. Callers provide passability and
    /// reachability so the same deterministic projection can be used by Unity runtime pathfinding
    /// and by no-engine regression tests.
    /// </summary>
    public static class OfficeInteractionOfferFactory
    {
        private static readonly OfficeGridCoordinate[] CardinalOffsets =
        {
            new OfficeGridCoordinate(1, 0),
            new OfficeGridCoordinate(0, -1),
            new OfficeGridCoordinate(-1, 0),
            new OfficeGridCoordinate(0, 1)
        };

        public static IReadOnlyList<OfficeInteractionOffer> Resolve(
            OfficeInteractionDefinition definition,
            OfficeGrid grid,
            string memberId,
            OfficeGridCoordinate start,
            OfficeSeatSlot assignedSeat,
            Func<OfficeGridCoordinate, bool> isOpen,
            Func<OfficeGridCoordinate, bool> isReachable)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (isOpen == null) throw new ArgumentNullException(nameof(isOpen));
            if (isReachable == null) throw new ArgumentNullException(nameof(isReachable));
            if (!grid.Contains(start)) return Array.Empty<OfficeInteractionOffer>();

            if (definition.ApproachPolicy == OfficeInteractionApproachPolicy.CurrentPosition)
                return new[] { CreateVirtualOffer(definition, memberId, new[] { start }) };

            if (definition.ApproachPolicy == OfficeInteractionApproachPolicy.OpenArea)
            {
                OfficeGridCoordinate[] cells = OpenAreaCandidates(grid, isOpen)
                    .Where(isReachable)
                    .ToArray();
                return cells.Length == 0
                    ? Array.Empty<OfficeInteractionOffer>()
                    : new[] { CreateVirtualOffer(definition, memberId, cells) };
            }

            if (definition.ApproachPolicy == OfficeInteractionApproachPolicy.AssignedSeatApproach)
            {
                PlacedOfficeFurniture workSurface = assignedSeat == null
                    ? null
                    : grid.Furniture.FirstOrDefault(item => string.Equals(
                        item.FurnitureId,
                        assignedSeat.WorkSurfaceFurnitureId,
                        StringComparison.Ordinal));
                if (assignedSeat == null ||
                    workSurface == null ||
                    !string.Equals(workSurface.KindId, definition.FurnitureKindId, StringComparison.Ordinal) ||
                    !isOpen(assignedSeat.ApproachCell) ||
                    !isReachable(assignedSeat.ApproachCell))
                    return Array.Empty<OfficeInteractionOffer>();
                return new[]
                {
                    CreateFurnitureOffer(definition, workSurface, new[] { assignedSeat.ApproachCell })
                };
            }

            var result = new List<OfficeInteractionOffer>();
            foreach (PlacedOfficeFurniture furniture in grid.Furniture
                         .Where(item => string.Equals(
                             item.KindId,
                             definition.FurnitureKindId,
                             StringComparison.Ordinal))
                         .OrderBy(item => item.FurnitureId, StringComparer.Ordinal))
            {
                OfficeGridCoordinate[] cells = ApproachCandidates(
                        furniture,
                        definition.ApproachPolicy,
                        isOpen)
                    .Where(isReachable)
                    .ToArray();
                if (cells.Length > 0) result.Add(CreateFurnitureOffer(definition, furniture, cells));
            }
            return result;
        }

        private static OfficeInteractionOffer CreateFurnitureOffer(
            OfficeInteractionDefinition definition,
            PlacedOfficeFurniture furniture,
            IEnumerable<OfficeGridCoordinate> cells)
        {
            return new OfficeInteractionOffer(
                definition.InteractionId + "@" + furniture.FurnitureId,
                definition.InteractionId,
                furniture.FurnitureId,
                furniture.KindId,
                definition.SemanticLocation,
                cells,
                definition.Capacity);
        }

        private static OfficeInteractionOffer CreateVirtualOffer(
            OfficeInteractionDefinition definition,
            string memberId,
            IEnumerable<OfficeGridCoordinate> cells)
        {
            return new OfficeInteractionOffer(
                definition.InteractionId + "@virtual:" + (memberId ?? string.Empty),
                definition.InteractionId,
                string.Empty,
                string.Empty,
                definition.SemanticLocation,
                cells,
                definition.Capacity);
        }

        private static IEnumerable<OfficeGridCoordinate> ApproachCandidates(
            PlacedOfficeFurniture furniture,
            OfficeInteractionApproachPolicy policy,
            Func<OfficeGridCoordinate, bool> isOpen)
        {
            var result = new HashSet<OfficeGridCoordinate>();
            int maximumDistance = policy == OfficeInteractionApproachPolicy.AdjacentOrTwoCells ||
                                  policy == OfficeInteractionApproachPolicy.SharedLoungeArea
                ? 2
                : 1;
            for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
            for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
            for (var distance = 1; distance <= maximumDistance; distance++)
            for (var index = 0; index < CardinalOffsets.Length; index++)
            {
                OfficeGridCoordinate offset = CardinalOffsets[index];
                var candidate = new OfficeGridCoordinate(
                    x + offset.X * distance,
                    y + offset.Y * distance);
                if (isOpen(candidate)) result.Add(candidate);
            }
            return result.OrderBy(cell => cell.Y).ThenBy(cell => cell.X);
        }

        private static IEnumerable<OfficeGridCoordinate> OpenAreaCandidates(
            OfficeGrid grid,
            Func<OfficeGridCoordinate, bool> isOpen)
        {
            for (var y = 2; y < grid.Height - 1; y++)
            for (var x = 1; x < grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (isOpen(cell)) yield return cell;
            }
        }
    }
}

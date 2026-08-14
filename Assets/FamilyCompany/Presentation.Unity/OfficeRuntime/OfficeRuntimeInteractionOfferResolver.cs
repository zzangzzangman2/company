using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Converts pure interaction definitions into offers from the live semantic office layout.
    /// Offers are rebuilt for every request, so layout edits cannot leave stale furniture IDs or
    /// approach cells behind.
    /// </summary>
    public sealed class OfficeRuntimeInteractionOfferResolver
    {
        private readonly OfficeGrid _grid;
        private readonly OfficeRuntimePathService _paths;
        private readonly Func<string, OfficeSeatSlot> _assignedSeat;

        public OfficeRuntimeInteractionOfferResolver(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeRuntimeOccupancy occupancy,
            OfficeRuntimePathService paths,
            Func<string, OfficeSeatSlot> assignedSeat)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _assignedSeat = assignedSeat ?? throw new ArgumentNullException(nameof(assignedSeat));
        }

        public IReadOnlyList<OfficeInteractionOffer> ResolveReachableOffers(
            OfficeInteractionDefinition definition,
            string memberId,
            OfficeGridCoordinate start,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.InteractionOfferResolve);
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            OfficeSeatSlot assignedSeat = _assignedSeat(memberId);
            if (definition.ApproachPolicy == OfficeInteractionApproachPolicy.CurrentPosition)
            {
                return OfficeInteractionOfferFactory.Resolve(
                    definition,
                    _grid,
                    memberId,
                    start,
                    assignedSeat,
                    cell => IsOpen(cell, radius),
                    cell => cell.Equals(start));
            }
            if (definition.ApproachPolicy == OfficeInteractionApproachPolicy.AssignedSeatApproach &&
                assignedSeat == null)
                return Array.Empty<OfficeInteractionOffer>();

            HashSet<OfficeGridCoordinate> reachable = _paths.FindStaticallyReachableCells(
                memberId ?? string.Empty,
                start,
                permittedSeatId ?? string.Empty,
                radius);
            return OfficeInteractionOfferFactory.Resolve(
                definition,
                _grid,
                memberId,
                start,
                assignedSeat,
                cell => IsOpen(cell, radius),
                cell => reachable.Contains(cell));
        }

        private bool IsOpen(OfficeGridCoordinate cell, float radius)
        {
            return _paths.HasStaticTraversalNeighbor(cell, string.Empty, radius);
        }

    }
}

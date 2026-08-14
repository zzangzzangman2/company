using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Converts pure interaction definitions into offers from the live semantic office layout.
    /// Offers are rebuilt for every request, so layout edits cannot leave stale furniture IDs or
    /// approach cells behind.
    /// </summary>
    public sealed class OfficeRuntimeInteractionOfferResolver
    {
        private static readonly OfficeGridCoordinate[] CardinalOffsets =
        {
            new OfficeGridCoordinate(1, 0),
            new OfficeGridCoordinate(0, -1),
            new OfficeGridCoordinate(-1, 0),
            new OfficeGridCoordinate(0, 1)
        };

        private readonly OfficeGrid _grid;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly OfficeRuntimeOccupancy _occupancy;
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
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _assignedSeat = assignedSeat ?? throw new ArgumentNullException(nameof(assignedSeat));
            LayoutRevision = grid.ComputeLayoutHash();
            if (!string.Equals(LayoutRevision, occupancy.LayoutRevision, StringComparison.Ordinal) ||
                !string.Equals(LayoutRevision, paths.LayoutRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("Interaction/path/occupancy layout revisions differ.");
        }

        public string LayoutRevision { get; }

        public IReadOnlyList<OfficeInteractionOffer> ResolveReachableOffers(
            OfficeInteractionDefinition definition,
            string memberId,
            OfficeGridCoordinate start,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
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
            if (!_grid.Contains(cell) ||
                !_occupancy.IsCellPassable(cell, string.Empty, string.Empty, false)) return false;
            Vector3 center3 = _presenter.CellCenterWorld(cell);
            var center = new Vector2(center3.x, center3.y);
            if (!_occupancy.CanTraverseStatic(center, center, radius, string.Empty)) return false;
            foreach (OfficeGridCoordinate offset in CardinalOffsets)
            {
                var neighbor = new OfficeGridCoordinate(cell.X + offset.X, cell.Y + offset.Y);
                if (!_grid.Contains(neighbor) ||
                    !_occupancy.IsCellPassable(neighbor, string.Empty, string.Empty, false)) continue;
                Vector3 neighbor3 = _presenter.CellCenterWorld(neighbor);
                if (_occupancy.CanTraverseStatic(
                        new Vector2(neighbor3.x, neighbor3.y),
                        center,
                        radius,
                        string.Empty)) return true;
            }
            return false;
        }

    }
}

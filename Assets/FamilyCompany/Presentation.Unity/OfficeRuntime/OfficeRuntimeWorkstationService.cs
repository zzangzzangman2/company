using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public sealed class OfficeRuntimeWorkstationService
    {
        private readonly OfficeGrid _grid;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly OfficeGridFurniturePresenter _furniturePresenter;
        private readonly OfficeRuntimeOccupancy _occupancy;
        private readonly OfficeSeatingState _seatingState;
        private readonly Dictionary<string, OfficeSeatSlot> _seats =
            new Dictionary<string, OfficeSeatSlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _assignedSeats =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public OfficeRuntimeWorkstationService(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter,
            OfficeRuntimeOccupancy occupancy)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _seatingState = new OfficeSeatingState(grid.SeatSlots.Select(item =>
                new FamilyCompany.Simulation.OfficeSeating.OfficeSeatDefinition(
                    item.SeatId,
                    new OfficeSeatPosition(item.Cell.X, item.Cell.Y))));
            foreach (OfficeSeatSlot seat in grid.SeatSlots)
            {
                _seats.Add(seat.SeatId, seat);
                string memberId = MemberIdFromSeat(seat.SeatId);
                if (memberId.Length == 0 || _assignedSeats.ContainsKey(memberId)) continue;
                if (_seatingState.TryAssign(seat.SeatId, memberId, out _))
                    _assignedSeats.Add(memberId, seat.SeatId);
            }
        }

        public OfficeSeatingState SeatingState => _seatingState;

        public bool TryReserveSeat(
            string memberId,
            string token,
            out OfficeSeatSlot seat,
            out OfficeSeatRuntimeClaim claim)
        {
            seat = null;
            claim = null;
            _assignedSeats.TryGetValue(memberId, out string assignedSeat);
            IEnumerable<OfficeSeatSlot> candidates = _grid.SeatSlots
                .OrderBy(item => item.SeatId == assignedSeat ? 0 : 1)
                .ThenBy(item => item.SeatId, StringComparer.Ordinal);
            foreach (OfficeSeatSlot candidate in candidates)
            {
                if (!OfficeSeatRuntimeClaim.TryReserve(
                        _seatingState,
                        candidate.SeatId,
                        memberId,
                        token + ":" + candidate.SeatId,
                        out OfficeSeatRuntimeClaim created,
                        out _)) continue;
                seat = candidate;
                claim = created;
                return true;
            }
            return false;
        }

        public bool TryResolveDestination(
            OfficeSemanticLocation location,
            string memberId,
            string stableKey,
            out OfficeRuntimeDestination destination)
        {
            // NPC meetings use the member's assigned PC as a seated video-call station. The
            // meeting table currently has no authored chairs, so routing a 60-120 minute meeting
            // there leaves the family standing motionless for the whole block. Player interaction
            // still resolves to the physical meeting table.
            bool seatedNpcMeeting = location == OfficeSemanticLocation.MeetingRoom &&
                                    !string.Equals(memberId, "player", StringComparison.Ordinal);
            if (location == OfficeSemanticLocation.Desk || seatedNpcMeeting)
            {
                OfficeSeatSlot seat = AssignedSeat(memberId) ?? _grid.SeatSlots.FirstOrDefault();
                if (seat != null)
                {
                    OfficeActivity seatedActivity = seatedNpcMeeting
                        ? OfficeActivity.Meeting
                        : OfficeActivity.Work;
                    string destinationPrefix = seatedNpcMeeting ? "video-meeting:" : "desk:";
                    destination = new OfficeRuntimeDestination(
                        destinationPrefix + seat.SeatId,
                        location,
                        seatedActivity,
                        seat.ApproachCell,
                        seat.SeatId);
                    return true;
                }
            }

            string kind = location switch
            {
                OfficeSemanticLocation.Reception => OfficeGridLayouts.ReceptionCounterKind,
                OfficeSemanticLocation.Printer => OfficeGridLayouts.FaxCopierKind,
                OfficeSemanticLocation.MeetingRoom => OfficeGridLayouts.MeetingTableKind,
                OfficeSemanticLocation.Lounge => OfficeGridLayouts.SofaKind,
                _ => string.Empty
            };
            OfficeActivity activity = location switch
            {
                OfficeSemanticLocation.Reception => OfficeActivity.Reception,
                OfficeSemanticLocation.Printer => OfficeActivity.Printing,
                OfficeSemanticLocation.MeetingRoom => OfficeActivity.Meeting,
                OfficeSemanticLocation.Lounge => OfficeActivity.Break,
                OfficeSemanticLocation.Exit => OfficeActivity.Outside,
                _ => OfficeActivity.Walking
            };
            var candidates = location == OfficeSemanticLocation.Exit
                ? ExitCandidates()
                : InteractionCandidates(kind);
            if (candidates.Count == 0)
            {
                destination = default;
                return false;
            }
            int index = StableRandom.StableRandomInt(
                "starter-office-destination:" + stableKey + ":" + memberId + ":" + location,
                candidates.Count);
            OfficeGridCoordinate cell = candidates[index];
            destination = new OfficeRuntimeDestination(
                location.ToString().ToLowerInvariant() + ":" + cell.X + ":" + cell.Y,
                location,
                activity,
                cell);
            return true;
        }

        public bool TryResolveActivityDestination(
            OfficeActivity activity,
            string memberId,
            string stableKey,
            out OfficeRuntimeDestination destination)
        {
            OfficeSemanticLocation location = activity switch
            {
                OfficeActivity.Work => OfficeSemanticLocation.Desk,
                OfficeActivity.Printing => OfficeSemanticLocation.Printer,
                OfficeActivity.Meeting => OfficeSemanticLocation.MeetingRoom,
                OfficeActivity.Reception => OfficeSemanticLocation.Reception,
                OfficeActivity.Outside => OfficeSemanticLocation.Exit,
                _ => OfficeSemanticLocation.Lounge
            };
            return TryResolveDestination(location, memberId, stableKey, out destination);
        }

        public OfficeSeatSlot RequiredSeat(string seatId)
        {
            if (!_seats.TryGetValue(seatId ?? string.Empty, out OfficeSeatSlot result))
                throw new ArgumentException("Unknown Starter Office seat: " + seatId, nameof(seatId));
            return result;
        }

        public OfficeRuntimeDestination DestinationForSeat(
            OfficeSeatSlot seat,
            OfficeRuntimeDestination requestedDestination)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            return new OfficeRuntimeDestination(
                requestedDestination.DestinationId,
                requestedDestination.SemanticLocation,
                requestedDestination.Activity,
                seat.ApproachCell,
                seat.SeatId);
        }

        /// <summary>
        /// Sorting order the chair sprite itself renders at. The chair's sort anchor sits below its
        /// ground anchor, so an occupant ordered from the floor point alone ends up behind the seat
        /// and the cushion covers their hips.
        /// </summary>
        public int ChairBaseSortingOrder(OfficeSeatSlot seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            return OfficeGridCharacterMover.ResolveDynamicSortingOrder(
                _furniturePresenter.SortAnchorWorld(seat.ChairFurnitureId));
        }

        public Vector3 SeatOperatorWorld(OfficeSeatSlot seat) =>
            _presenter.SubcellAnchorWorld(seat.OperatorAnchor);

        public Vector3 SeatApproachWorld(OfficeSeatSlot seat) =>
            _presenter.CellCenterWorld(seat.ApproachCell);

        public Vector3 ChairSeatAnchorWorld(OfficeSeatSlot seat) =>
            _furniturePresenter.SeatAnchorWorld(seat.ChairFurnitureId);

        /// <summary>
        /// Floor point of the chair - the semantic anchor its sprite pivot already stands on. This
        /// is where a seated occupant is placed, per <see cref="OfficeSeatedOccupantContract"/>.
        /// </summary>
        public Vector3 ChairFloorAnchorWorld(OfficeSeatSlot seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            PlacedOfficeFurniture chair = _grid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, seat.ChairFurnitureId, StringComparison.Ordinal));
            if (chair == null)
                throw new InvalidOperationException("Seat has no chair furniture: " + seat.SeatId);
            return _presenter.SubcellAnchorWorld(chair.PlacementAnchor);
        }

        public Vector3 DeskSeatSocketWorld(OfficeSeatSlot seat) =>
            _furniturePresenter.OperatorSeatSocketWorld(seat.WorkSurfaceFurnitureId);

        public Vector3 DeskWorkSocketWorld(OfficeSeatSlot seat) =>
            _furniturePresenter.OperatorWorkSocketWorld(seat.WorkSurfaceFurnitureId);

        /// <summary>
        /// Kept as the seating hook, but sorting is no longer decided here.
        /// <see cref="OfficeRuntimeDepthSorter"/> orders the whole office from its footprints once
        /// per frame, so there is exactly one owner of every sorting order.
        /// </summary>
        public void ApplyPresentationStack(
            OfficeSeatSlot seat,
            SpriteRenderer characterRenderer,
            Vector3 semanticActorWorld)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (characterRenderer == null) throw new ArgumentNullException(nameof(characterRenderer));
        }

        /// <summary>Superseded by <see cref="OfficeRuntimeDepthSorter"/>; kept for the walk path's call site.</summary>
        public void ApplyDynamicCharacterOrder(
            SpriteRenderer characterRenderer,
            Vector3 semanticActorWorld)
        {
            if (characterRenderer == null) throw new ArgumentNullException(nameof(characterRenderer));
        }

        public void ClearOcclusion(OfficeSeatSlot seat) =>
            _furniturePresenter.ClearSeatOcclusion(seat);

        private OfficeSeatSlot AssignedSeat(string memberId)
        {
            return _assignedSeats.TryGetValue(memberId ?? string.Empty, out string seatId)
                ? RequiredSeat(seatId)
                : null;
        }

        private List<OfficeGridCoordinate> InteractionCandidates(string kindId)
        {
            var result = new HashSet<OfficeGridCoordinate>();
            foreach (PlacedOfficeFurniture item in _grid.Furniture)
            {
                if (!string.Equals(item.KindId, kindId, StringComparison.Ordinal)) continue;
                for (var y = item.Origin.Y; y < item.Origin.Y + item.Height; y++)
                for (var x = item.Origin.X; x < item.Origin.X + item.Width; x++)
                {
                    AddIfOpen(result, new OfficeGridCoordinate(x + 1, y));
                    AddIfOpen(result, new OfficeGridCoordinate(x - 1, y));
                    AddIfOpen(result, new OfficeGridCoordinate(x, y + 1));
                    AddIfOpen(result, new OfficeGridCoordinate(x, y - 1));
                }
            }
            return result.OrderBy(item => item.Y).ThenBy(item => item.X).ToList();
        }

        private List<OfficeGridCoordinate> ExitCandidates()
        {
            var result = new List<OfficeGridCoordinate>();
            for (var x = 1; x < _grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, 1);
                if (_occupancy.IsCellPassable(cell, string.Empty, string.Empty, false)) result.Add(cell);
            }
            return result;
        }

        private void AddIfOpen(ISet<OfficeGridCoordinate> result, OfficeGridCoordinate cell)
        {
            if (_grid.Contains(cell) && _occupancy.IsCellPassable(cell, string.Empty, string.Empty, false))
                result.Add(cell);
        }

        private static string MemberIdFromSeat(string seatId)
        {
            const string prefix = "seat_";
            return seatId != null && seatId.StartsWith(prefix, StringComparison.Ordinal)
                ? seatId.Substring(prefix.Length)
                : string.Empty;
        }
    }
}

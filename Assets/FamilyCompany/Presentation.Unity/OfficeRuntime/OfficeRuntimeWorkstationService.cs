using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeInteractions;
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
        private readonly OfficeRuntimeInteractionOfferResolver _offerResolver;
        private readonly OfficeRuntimeInteractionLifecycleService _interactionLifecycle;
        private readonly OfficeSeatingState _seatingState;
        private readonly Dictionary<string, OfficeSeatSlot> _seats =
            new Dictionary<string, OfficeSeatSlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _assignedSeats =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // StarterOfficeV1 has one entrance. Keep the authority here instead of treating every
        // open cell along the south edge as an interchangeable/random door.
        public static readonly OfficeGridCoordinate StarterEntranceCell =
            new OfficeGridCoordinate(8, 1);

        public OfficeRuntimeWorkstationService(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter,
            OfficeRuntimeOccupancy occupancy,
            OfficeRuntimePathService paths)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
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
            _offerResolver = new OfficeRuntimeInteractionOfferResolver(
                grid,
                presenter,
                occupancy,
                paths,
                AssignedSeat);
            _interactionLifecycle = new OfficeRuntimeInteractionLifecycleService(_offerResolver);
        }

        public OfficeSeatingState SeatingState => _seatingState;
        public OfficeRuntimeInteractionOfferResolver InteractionOffers => _offerResolver;
        public OfficeRuntimeInteractionLifecycleService InteractionLifecycle => _interactionLifecycle;

        public bool TryResolveStandingInteractionFacing(
            OfficeRuntimeDestination destination,
            out int direction)
        {
            direction = -1;
            if (destination.RequiresSeat || destination.FurnitureId.Length == 0) return false;
            PlacedOfficeFurniture furniture = _grid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, destination.FurnitureId, StringComparison.Ordinal));
            if (furniture == null) return false;

            Vector3 actorWorld = _presenter.CellCenterWorld(destination.Cell);
            Vector3 furnitureWorld = _presenter.SubcellAnchorWorld(furniture.PlacementAnchor);
            Vector2 heading = new Vector2(
                furnitureWorld.x - actorWorld.x,
                furnitureWorld.y - actorWorld.y);
            if (heading.sqrMagnitude <= 0.000001f) return false;
            direction = DirectionalSpriteAnimator.ResolveTileDirection(heading);
            return true;
        }

        public bool TryResolveInteractionDestination(
            string interactionId,
            string memberId,
            string stableKey,
            OfficeGridCoordinate start,
            string permittedSeatId,
            float radius,
            out OfficeRuntimeDestination destination)
        {
            if (!OfficeInteractionCatalog.TryGetDefinition(interactionId, out OfficeInteractionDefinition definition))
            {
                destination = default;
                return false;
            }

            IReadOnlyList<OfficeInteractionOffer> offers = _offerResolver.ResolveReachableOffers(
                definition,
                memberId,
                start,
                permittedSeatId,
                radius);
            if (offers.Count == 0)
            {
                destination = default;
                return false;
            }

            int offerIndex = StableRandom.StableRandomInt(
                "starter-office-offer:" + stableKey + ":" + memberId + ":" + interactionId,
                offers.Count);
            OfficeInteractionOffer offer = offers[offerIndex];
            int cellIndex = StableRandom.StableRandomInt(
                "starter-office-offer-cell:" + stableKey + ":" + memberId + ":" + offer.OfferId,
                offer.ApproachCells.Count);
            OfficeGridCoordinate cell = offer.ApproachCells[cellIndex];
            OfficeSeatSlot seat = definition.RequiresAssignedSeat ? AssignedSeat(memberId) : null;
            destination = new OfficeRuntimeDestination(
                offer.OfferId + ":" + cell.X + ":" + cell.Y,
                definition.SemanticLocation,
                ActivityFor(definition.SemanticLocation),
                cell,
                seat?.SeatId ?? string.Empty,
                offer.OfferId,
                offer.FurnitureId);
            return true;
        }

        /// <summary>
        /// Unified entry point for a future Agent integration. None and AssignedSeat definitions
        /// keep their existing destination/seat owners. Concrete non-seat furniture definitions
        /// receive a transient lifecycle handle, while pair/group policies fail closed.
        /// </summary>
        public bool TryBeginInteraction(
            string interactionId,
            string memberId,
            string stableKey,
            OfficeGridCoordinate start,
            string permittedSeatId,
            float radius,
            out OfficeRuntimeDestination destination,
            out OfficeRuntimeInteractionHandle interactionHandle,
            out OfficeRuntimeInteractionFailure failure)
        {
            destination = default;
            interactionHandle = null;
            if (!OfficeInteractionCatalog.TryGetDefinition(
                    interactionId,
                    out OfficeInteractionDefinition definition))
            {
                failure = new OfficeRuntimeInteractionFailure(
                    OfficeRuntimeInteractionFailureCode.UnknownInteraction);
                return false;
            }

            if (definition.ReservationPolicy == OfficeInteractionReservationPolicy.None ||
                definition.ReservationPolicy == OfficeInteractionReservationPolicy.AssignedSeat)
            {
                bool resolved = TryResolveInteractionDestination(
                    interactionId,
                    memberId,
                    stableKey,
                    start,
                    permittedSeatId,
                    radius,
                    out destination);
                failure = resolved
                    ? OfficeRuntimeInteractionFailure.None
                    : new OfficeRuntimeInteractionFailure(
                        OfficeRuntimeInteractionFailureCode.NoReachableOffer);
                return resolved;
            }

            if (definition.ReservationPolicy == OfficeInteractionReservationPolicy.PairedConversation ||
                definition.ReservationPolicy == OfficeInteractionReservationPolicy.GroupMeeting)
            {
                failure = new OfficeRuntimeInteractionFailure(
                    OfficeRuntimeInteractionFailureCode.UnsupportedReservationPolicy);
                return false;
            }

            var request = new OfficeRuntimeInteractionRequest(
                interactionId,
                memberId,
                stableKey,
                start,
                permittedSeatId,
                radius);
            if (!_interactionLifecycle.TryBegin(request, out interactionHandle, out failure)) return false;

            OfficeInteractionOffer offer = interactionHandle.Offer;
            OfficeGridCoordinate cell = interactionHandle.ApproachCell;
            destination = new OfficeRuntimeDestination(
                offer.OfferId + ":" + cell.X + ":" + cell.Y,
                definition.SemanticLocation,
                ActivityFor(definition.SemanticLocation),
                cell,
                string.Empty,
                offer.OfferId,
                offer.FurnitureId);
            return true;
        }

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

        /// <summary>
        /// Reserves exactly the seat advertised by an interaction offer. There is deliberately no
        /// fallback: changing seats here would make the destination/offer and physical claim differ.
        /// </summary>
        public bool TryReserveSeat(
            string memberId,
            string requestedSeatId,
            string token,
            out OfficeSeatSlot seat,
            out OfficeSeatRuntimeClaim claim)
        {
            seat = null;
            claim = null;
            if (string.IsNullOrWhiteSpace(requestedSeatId) ||
                !_seats.TryGetValue(requestedSeatId.Trim(), out OfficeSeatSlot requested)) return false;
            if (!OfficeSeatRuntimeClaim.TryReserve(
                    _seatingState,
                    requested.SeatId,
                    memberId,
                    token + ":" + requested.SeatId,
                    out OfficeSeatRuntimeClaim created,
                    out _)) return false;
            seat = requested;
            claim = created;
            return true;
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

            string kind = LegacyFurnitureKindFor(location);
            OfficeActivity activity = location switch
            {
                OfficeSemanticLocation.Reception => OfficeActivity.Reception,
                OfficeSemanticLocation.Printer => OfficeActivity.Printing,
                OfficeSemanticLocation.MeetingRoom => OfficeActivity.Meeting,
                OfficeSemanticLocation.Lounge => OfficeActivity.Break,
                OfficeSemanticLocation.Filing => OfficeActivity.Printing,
                OfficeSemanticLocation.Water => OfficeActivity.Break,
                OfficeSemanticLocation.Coffee => OfficeActivity.Break,
                OfficeSemanticLocation.OpenArea => OfficeActivity.Break,
                OfficeSemanticLocation.Exit => OfficeActivity.Outside,
                _ => OfficeActivity.Walking
            };
            var candidates = location == OfficeSemanticLocation.Exit
                ? ExitCandidates()
                : location == OfficeSemanticLocation.OpenArea
                    ? OpenAreaCandidates()
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
            PlacedOfficeFurniture targetFurniture = NearestFurniture(kind, cell);
            destination = new OfficeRuntimeDestination(
                location.ToString().ToLowerInvariant() + ":" + cell.X + ":" + cell.Y,
                location,
                activity,
                cell,
                string.Empty,
                string.Empty,
                targetFurniture?.FurnitureId ?? string.Empty);
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

        public bool TryResolveAttendanceEntryDestination(
            string memberId,
            string stableKey,
            out OfficeRuntimeDestination destination)
        {
            OfficeGridCoordinate selected = ResolveAttendanceEntryCell(memberId);
            if (!_grid.Contains(selected))
            {
                destination = default;
                return false;
            }
            destination = new OfficeRuntimeDestination(
                "attendance-open:" + selected.X + ":" + selected.Y,
                OfficeSemanticLocation.OpenArea,
                OfficeActivity.Break,
                selected);
            return true;
        }

        public bool TryResolveAttendanceEntrance(
            string memberId,
            string stableKey,
            out OfficeRuntimeDestination destination)
        {
            OfficeGridCoordinate selected = StarterEntranceCell;
            if (!_grid.Contains(selected) ||
                !_occupancy.IsCellPassable(selected, memberId, string.Empty, true))
            {
                destination = default;
                return false;
            }
            destination = new OfficeRuntimeDestination(
                "attendance-door:" + selected.X + ":" + selected.Y,
                OfficeSemanticLocation.Exit,
                OfficeActivity.Outside,
                selected);
            return true;
        }

        private OfficeGridCoordinate ResolveAttendanceEntryCell(string memberId)
        {
            // All four actors emerge from the same door, then immediately clear it along the
            // reception corridor. The deterministic fallback is only for a live layout edit that
            // temporarily blocks the preferred corridor cell.
            OfficeGridCoordinate[] corridor =
            {
                new OfficeGridCoordinate(8, 2),
                new OfficeGridCoordinate(8, 3),
                new OfficeGridCoordinate(9, 2),
                new OfficeGridCoordinate(9, 3)
            };
            foreach (OfficeGridCoordinate cell in corridor)
            {
                if (_grid.Contains(cell) &&
                    _occupancy.IsCellPassable(cell, memberId, string.Empty, true)) return cell;
            }
            return new OfficeGridCoordinate(-1, -1);
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
                seat.SeatId,
                requestedDestination.InteractionOfferId,
                requestedDestination.FurnitureId);
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

        public void AlignChairPresentationToOccupant(OfficeSeatSlot seat, Vector3 occupantPelvisWorld) =>
            _furniturePresenter.AlignSeatPresentationToWorld(seat, occupantPelvisWorld);

        public void RestoreChairPresentation(OfficeSeatSlot seat) =>
            _furniturePresenter.RestoreSeatPresentation(seat);

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

        private static OfficeActivity ActivityFor(OfficeSemanticLocation location)
        {
            return location switch
            {
                OfficeSemanticLocation.Desk => OfficeActivity.Work,
                OfficeSemanticLocation.Reception => OfficeActivity.Reception,
                OfficeSemanticLocation.Printer => OfficeActivity.Printing,
                OfficeSemanticLocation.MeetingRoom => OfficeActivity.Meeting,
                OfficeSemanticLocation.Filing => OfficeActivity.Printing,
                OfficeSemanticLocation.Exit => OfficeActivity.Outside,
                OfficeSemanticLocation.Lounge => OfficeActivity.Break,
                OfficeSemanticLocation.Water => OfficeActivity.Break,
                OfficeSemanticLocation.Coffee => OfficeActivity.Break,
                OfficeSemanticLocation.OpenArea => OfficeActivity.Break,
                _ => OfficeActivity.Walking
            };
        }

        private static string LegacyFurnitureKindFor(OfficeSemanticLocation location)
        {
            // The physical meeting table remains a direct player/contract destination until it has
            // authored seats. Every Micro Action furniture mapping comes from the catalog.
            if (location == OfficeSemanticLocation.MeetingRoom)
                return OfficeGridLayouts.MeetingTableKind;
            string[] kinds = OfficeInteractionCatalog.All
                .Where(definition =>
                    definition.SemanticLocation == location &&
                    definition.RequiresFurniture &&
                    definition.FurnitureKindId.Length > 0)
                .Select(definition => definition.FurnitureKindId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return kinds.Length == 1 ? kinds[0] : string.Empty;
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
                    AddIfOpen(result, new OfficeGridCoordinate(x + 2, y));
                    AddIfOpen(result, new OfficeGridCoordinate(x - 2, y));
                    AddIfOpen(result, new OfficeGridCoordinate(x, y + 2));
                    AddIfOpen(result, new OfficeGridCoordinate(x, y - 2));
                }
            }
            return result.OrderBy(item => item.Y).ThenBy(item => item.X).ToList();
        }

        private PlacedOfficeFurniture NearestFurniture(
            string kindId,
            OfficeGridCoordinate cell)
        {
            if (string.IsNullOrWhiteSpace(kindId)) return null;
            Vector3 cellWorld = _presenter.CellCenterWorld(cell);
            return _grid.Furniture
                .Where(item => string.Equals(item.KindId, kindId, StringComparison.Ordinal))
                .OrderBy(item =>
                    (_presenter.SubcellAnchorWorld(item.PlacementAnchor) - cellWorld).sqrMagnitude)
                .ThenBy(item => item.FurnitureId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private List<OfficeGridCoordinate> ExitCandidates()
        {
            return _grid.Contains(StarterEntranceCell) &&
                   _occupancy.IsCellPassable(StarterEntranceCell, string.Empty, string.Empty, false)
                ? new List<OfficeGridCoordinate> { StarterEntranceCell }
                : new List<OfficeGridCoordinate>();
        }

        private List<OfficeGridCoordinate> OpenAreaCandidates()
        {
            var result = new List<OfficeGridCoordinate>();
            for (var y = 2; y < _grid.Height - 1; y++)
            for (var x = 1; x < _grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!_occupancy.IsCellPassable(cell, string.Empty, string.Empty, false)) continue;
                result.Add(cell);
            }
            return result;
        }

        private void AddIfOpen(ISet<OfficeGridCoordinate> result, OfficeGridCoordinate cell)
        {
            if (!_grid.Contains(cell) ||
                !_occupancy.IsCellPassable(cell, string.Empty, string.Empty, false)) return;
            Vector3 center3 = _presenter.CellCenterWorld(cell);
            var center = new Vector2(center3.x, center3.y);
            if (_occupancy.CanTraverseStatic(
                    center,
                    center,
                    OfficeRuntimeAgent.DefaultRadius,
                    string.Empty) &&
                HasRadiusClearEntrance(cell, center)) result.Add(cell);
        }

        private bool HasRadiusClearEntrance(OfficeGridCoordinate cell, Vector2 center)
        {
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0),
                new OfficeGridCoordinate(0, -1),
                new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1)
            };
            foreach (OfficeGridCoordinate offset in offsets)
            {
                var neighbor = new OfficeGridCoordinate(cell.X + offset.X, cell.Y + offset.Y);
                if (!_grid.Contains(neighbor) ||
                    !_occupancy.IsCellPassable(neighbor, string.Empty, string.Empty, false)) continue;
                Vector3 neighbor3 = _presenter.CellCenterWorld(neighbor);
                if (_occupancy.CanTraverseStatic(
                        new Vector2(neighbor3.x, neighbor3.y),
                        center,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty)) return true;
            }
            return false;
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

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    public enum OfficeInteractionReservationFailure
    {
        None = 0,
        NoReservationRequired = 1,
        UnsupportedReservationPolicy = 2,
        DefinitionOfferMismatch = 3,
        FurnitureRequired = 4,
        ApproachCellNotOffered = 5,
        TokenConflict = 6,
        TokenAlreadyReleased = 7,
        MemberAlreadyReserved = 8,
        CapacityReached = 9,
        ApproachCellOccupied = 10
    }

    /// <summary>
    /// One transient claim against a concrete office-furniture resource and one approach cell.
    /// Claims are presentation/runtime state only and must never be written to a game save.
    /// </summary>
    public sealed class OfficeInteractionReservation
    {
        internal OfficeInteractionReservation(
            string token,
            string memberId,
            string resourceId,
            string interactionId,
            string offerId,
            string furnitureId,
            OfficeGridCoordinate approachCell,
            int capacity,
            OfficeInteractionReservationPolicy policy)
        {
            Token = token;
            MemberId = memberId;
            ResourceId = resourceId;
            InteractionId = interactionId;
            OfferId = offerId;
            FurnitureId = furnitureId;
            ApproachCell = approachCell;
            Capacity = capacity;
            Policy = policy;
        }

        public string Token { get; }
        public string MemberId { get; }
        public string ResourceId { get; }
        public string InteractionId { get; }
        public string OfferId { get; }
        public string FurnitureId { get; }
        public OfficeGridCoordinate ApproachCell { get; }
        public int Capacity { get; }
        public OfficeInteractionReservationPolicy Policy { get; }
        public bool IsReleased { get; private set; }

        internal bool Matches(
            string memberId,
            string resourceId,
            OfficeInteractionDefinition definition,
            OfficeInteractionOffer offer,
            OfficeGridCoordinate approachCell,
            int capacity)
        {
            return !IsReleased &&
                   string.Equals(MemberId, memberId, StringComparison.Ordinal) &&
                   string.Equals(ResourceId, resourceId, StringComparison.Ordinal) &&
                   string.Equals(InteractionId, definition.InteractionId, StringComparison.Ordinal) &&
                   string.Equals(OfferId, offer.OfferId, StringComparison.Ordinal) &&
                   string.Equals(FurnitureId, offer.FurnitureId, StringComparison.Ordinal) &&
                   ApproachCell.Equals(approachCell) &&
                   Capacity == capacity &&
                   Policy == definition.ReservationPolicy;
        }

        internal void MarkReleased()
        {
            IsReleased = true;
        }
    }

    /// <summary>
    /// Character-agnostic transient reservation owner for non-seat office interactions.
    /// A furniture ID is the resource key, so different interaction definitions targeting the
    /// same physical object share one capacity. Approach cells are exclusive across all resources.
    /// This type is deliberately Unity-free and is expected to be owned by one runtime office.
    /// </summary>
    public sealed class OfficeInteractionReservationBook
    {
        private const string FurnitureResourcePrefix = "furniture:";

        private readonly Dictionary<string, OfficeInteractionReservation> _reservationsByToken =
            new Dictionary<string, OfficeInteractionReservation>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<OfficeInteractionReservation>> _activeByResource =
            new Dictionary<string, List<OfficeInteractionReservation>>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeInteractionReservation> _activeByMember =
            new Dictionary<string, OfficeInteractionReservation>(StringComparer.Ordinal);
        private readonly Dictionary<OfficeGridCoordinate, OfficeInteractionReservation> _activeByApproachCell =
            new Dictionary<OfficeGridCoordinate, OfficeInteractionReservation>();

        public int ActiveReservationCount { get; private set; }

        public IReadOnlyList<OfficeInteractionReservation> ActiveReservations =>
            _activeByMember.Values
                .OrderBy(item => item.ResourceId, StringComparer.Ordinal)
                .ThenBy(item => item.Token, StringComparer.Ordinal)
                .ToArray();

        public bool TryReserve(
            OfficeInteractionDefinition definition,
            OfficeInteractionOffer offer,
            string memberId,
            string token,
            OfficeGridCoordinate approachCell,
            out OfficeInteractionReservation reservation,
            out OfficeInteractionReservationFailure failure)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Reservation token is required.", nameof(token));

            string normalizedMemberId = memberId.Trim();
            string normalizedToken = token.Trim();
            reservation = null;

            if (definition.ReservationPolicy == OfficeInteractionReservationPolicy.None)
                return Fail(OfficeInteractionReservationFailure.NoReservationRequired, out failure);
            if (definition.ReservationPolicy == OfficeInteractionReservationPolicy.AssignedSeat ||
                definition.ReservationPolicy == OfficeInteractionReservationPolicy.PairedConversation ||
                definition.ReservationPolicy == OfficeInteractionReservationPolicy.GroupMeeting)
                return Fail(OfficeInteractionReservationFailure.UnsupportedReservationPolicy, out failure);
            if (definition.ReservationPolicy != OfficeInteractionReservationPolicy.ExclusiveFurniture &&
                definition.ReservationPolicy != OfficeInteractionReservationPolicy.SharedFurnitureCapacity)
                return Fail(OfficeInteractionReservationFailure.UnsupportedReservationPolicy, out failure);

            if (!DefinitionMatchesOffer(definition, offer))
                return Fail(OfficeInteractionReservationFailure.DefinitionOfferMismatch, out failure);
            if (!definition.RequiresFurniture || !offer.IsFurnitureOffer)
                return Fail(OfficeInteractionReservationFailure.FurnitureRequired, out failure);
            if (!offer.ApproachCells.Contains(approachCell))
                return Fail(OfficeInteractionReservationFailure.ApproachCellNotOffered, out failure);

            int capacity = definition.ReservationPolicy == OfficeInteractionReservationPolicy.ExclusiveFurniture
                ? 1
                : definition.Capacity;
            string resourceId = FurnitureResourcePrefix + offer.FurnitureId;

            if (_reservationsByToken.TryGetValue(normalizedToken, out OfficeInteractionReservation existing))
            {
                if (existing.IsReleased)
                    return Fail(OfficeInteractionReservationFailure.TokenAlreadyReleased, out failure);
                if (!existing.Matches(
                        normalizedMemberId,
                        resourceId,
                        definition,
                        offer,
                        approachCell,
                        capacity))
                    return Fail(OfficeInteractionReservationFailure.TokenConflict, out failure);
                reservation = existing;
                failure = OfficeInteractionReservationFailure.None;
                return true;
            }

            if (_activeByMember.ContainsKey(normalizedMemberId))
                return Fail(OfficeInteractionReservationFailure.MemberAlreadyReserved, out failure);
            if (_activeByApproachCell.ContainsKey(approachCell))
                return Fail(OfficeInteractionReservationFailure.ApproachCellOccupied, out failure);

            if (!_activeByResource.TryGetValue(resourceId, out List<OfficeInteractionReservation> resourceClaims))
            {
                resourceClaims = new List<OfficeInteractionReservation>();
                _activeByResource.Add(resourceId, resourceClaims);
            }
            if (resourceClaims.Count >= capacity)
                return Fail(OfficeInteractionReservationFailure.CapacityReached, out failure);

            reservation = new OfficeInteractionReservation(
                normalizedToken,
                normalizedMemberId,
                resourceId,
                definition.InteractionId,
                offer.OfferId,
                offer.FurnitureId,
                approachCell,
                capacity,
                definition.ReservationPolicy);
            _reservationsByToken.Add(normalizedToken, reservation);
            resourceClaims.Add(reservation);
            _activeByMember.Add(normalizedMemberId, reservation);
            _activeByApproachCell.Add(approachCell, reservation);
            ActiveReservationCount++;
            failure = OfficeInteractionReservationFailure.None;
            return true;
        }

        public bool TryRelease(string token, out OfficeInteractionReservation reservation)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Reservation token is required.", nameof(token));
            if (!_reservationsByToken.TryGetValue(token.Trim(), out reservation)) return false;
            if (reservation.IsReleased) return true;

            if (_activeByResource.TryGetValue(
                    reservation.ResourceId,
                    out List<OfficeInteractionReservation> resourceClaims))
            {
                resourceClaims.Remove(reservation);
                if (resourceClaims.Count == 0) _activeByResource.Remove(reservation.ResourceId);
            }
            _activeByMember.Remove(reservation.MemberId);
            _activeByApproachCell.Remove(reservation.ApproachCell);
            reservation.MarkReleased();
            ActiveReservationCount--;
            return true;
        }

        public int ReleaseAllForMember(string memberId)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (!_activeByMember.TryGetValue(
                    memberId.Trim(),
                    out OfficeInteractionReservation reservation)) return 0;
            return TryRelease(reservation.Token, out _) ? 1 : 0;
        }

        public bool ContainsActiveToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return _reservationsByToken.TryGetValue(token.Trim(), out OfficeInteractionReservation reservation) &&
                   !reservation.IsReleased;
        }

        public int ActiveCountForFurniture(string furnitureId)
        {
            if (string.IsNullOrWhiteSpace(furnitureId)) return 0;
            string resourceId = FurnitureResourcePrefix + furnitureId.Trim();
            return _activeByResource.TryGetValue(resourceId, out List<OfficeInteractionReservation> claims)
                ? claims.Count
                : 0;
        }

        public bool IsApproachCellReserved(OfficeGridCoordinate cell) =>
            _activeByApproachCell.ContainsKey(cell);

        private static bool DefinitionMatchesOffer(
            OfficeInteractionDefinition definition,
            OfficeInteractionOffer offer)
        {
            return string.Equals(definition.InteractionId, offer.InteractionId, StringComparison.Ordinal) &&
                   string.Equals(definition.FurnitureKindId, offer.FurnitureKindId, StringComparison.Ordinal) &&
                   definition.SemanticLocation == offer.Location &&
                   definition.Capacity == offer.Capacity;
        }

        private static bool Fail(
            OfficeInteractionReservationFailure value,
            out OfficeInteractionReservationFailure failure)
        {
            failure = value;
            return false;
        }
    }
}

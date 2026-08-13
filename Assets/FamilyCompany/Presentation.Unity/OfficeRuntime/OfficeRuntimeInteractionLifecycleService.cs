using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeRuntimeInteractionFailureCode
    {
        None = 0,
        UnknownInteraction = 1,
        PolicyManagedByExistingPath = 2,
        UnsupportedReservationPolicy = 3,
        ExistingMemberReservation = 4,
        NoReachableOffer = 5,
        ReservationRejected = 6,
        ArrivalCellMismatch = 7,
        LiveOfferUnavailable = 8,
        ReservationLost = 9,
        NotArrived = 10,
        AlreadyCompleted = 11,
        AlreadyAborted = 12,
        AlreadyReleased = 13,
        ForeignHandle = 14
    }

    public readonly struct OfficeRuntimeInteractionFailure
    {
        public OfficeRuntimeInteractionFailure(
            OfficeRuntimeInteractionFailureCode code,
            OfficeInteractionReservationFailure reservationFailure = OfficeInteractionReservationFailure.None)
        {
            Code = code;
            ReservationFailure = reservationFailure;
        }

        public static OfficeRuntimeInteractionFailure None =>
            new OfficeRuntimeInteractionFailure(OfficeRuntimeInteractionFailureCode.None);

        public OfficeRuntimeInteractionFailureCode Code { get; }
        public OfficeInteractionReservationFailure ReservationFailure { get; }
        public bool IsNone => Code == OfficeRuntimeInteractionFailureCode.None;

        public override string ToString()
        {
            return ReservationFailure == OfficeInteractionReservationFailure.None
                ? Code.ToString()
                : Code + ":" + ReservationFailure;
        }
    }

    /// <summary>
    /// One transient request to acquire a concrete non-seat furniture interaction. StableKey is an
    /// autonomy intent/attempt key; it affects deterministic traversal but is never persisted.
    /// </summary>
    public sealed class OfficeRuntimeInteractionRequest
    {
        public OfficeRuntimeInteractionRequest(
            string interactionId,
            string memberId,
            string stableKey,
            OfficeGridCoordinate start,
            string permittedSeatId = "",
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(stableKey))
                throw new ArgumentException("Stable interaction key is required.", nameof(stableKey));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));

            InteractionId = interactionId.Trim();
            MemberId = memberId.Trim();
            StableKey = stableKey.Trim();
            Start = start;
            PermittedSeatId = (permittedSeatId ?? string.Empty).Trim();
            Radius = radius;
        }

        public string InteractionId { get; }
        public string MemberId { get; }
        public string StableKey { get; }
        public OfficeGridCoordinate Start { get; }
        public string PermittedSeatId { get; }
        public float Radius { get; }

        internal string RequestKey =>
            MemberId.Length.ToString(CultureInfo.InvariantCulture) + ":" + MemberId + ":" +
            InteractionId.Length.ToString(CultureInfo.InvariantCulture) + ":" + InteractionId + ":" +
            StableKey.Length.ToString(CultureInfo.InvariantCulture) + ":" + StableKey;
    }

    public enum OfficeRuntimeInteractionHandleState
    {
        Reserved = 0,
        Arrived = 1,
        Completed = 2,
        Aborted = 3,
        Released = 4
    }

    /// <summary>
    /// The sole owner-facing handle for one transient furniture reservation. Repeated completion,
    /// abort, and release calls are idempotent for the same terminal outcome.
    /// </summary>
    public sealed class OfficeRuntimeInteractionHandle : IDisposable
    {
        private readonly OfficeRuntimeInteractionLifecycleService _owner;

        internal OfficeRuntimeInteractionHandle(
            OfficeRuntimeInteractionLifecycleService owner,
            OfficeRuntimeInteractionRequest request,
            OfficeInteractionDefinition definition,
            OfficeInteractionOffer offer,
            OfficeInteractionReservation reservation)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            Reservation = reservation ?? throw new ArgumentNullException(nameof(reservation));
            State = OfficeRuntimeInteractionHandleState.Reserved;
        }

        public OfficeRuntimeInteractionRequest Request { get; }
        public OfficeInteractionDefinition Definition { get; }
        public OfficeInteractionOffer Offer { get; }
        public OfficeInteractionReservation Reservation { get; }
        public string Token => Reservation.Token;
        public string MemberId => Reservation.MemberId;
        public string InteractionId => Reservation.InteractionId;
        public string OfferId => Reservation.OfferId;
        public string FurnitureId => Reservation.FurnitureId;
        public OfficeGridCoordinate ApproachCell => Reservation.ApproachCell;
        public OfficeRuntimeInteractionHandleState State { get; internal set; }
        public bool IsActive => State == OfficeRuntimeInteractionHandleState.Reserved ||
                                State == OfficeRuntimeInteractionHandleState.Arrived;
        public bool IsArrived => State == OfficeRuntimeInteractionHandleState.Arrived;
        public bool IsReleased => Reservation.IsReleased;

        internal bool IsOwnedBy(OfficeRuntimeInteractionLifecycleService service) =>
            ReferenceEquals(_owner, service);

        public bool TryValidateArrival(
            OfficeGridCoordinate actualCell,
            out OfficeRuntimeInteractionFailure failure) =>
            _owner.TryValidateArrival(this, actualCell, out failure);

        public bool TryComplete(out OfficeRuntimeInteractionFailure failure) =>
            _owner.TryComplete(this, out failure);

        public bool TryAbort(out OfficeRuntimeInteractionFailure failure) =>
            _owner.TryAbort(this, out failure);

        public bool TryRelease(out OfficeRuntimeInteractionFailure failure) =>
            _owner.TryRelease(this, out failure);

        public void Dispose()
        {
            TryRelease(out _);
        }
    }

    public delegate IReadOnlyList<OfficeInteractionOffer> OfficeRuntimeInteractionOfferProvider(
        OfficeInteractionDefinition definition,
        string memberId,
        OfficeGridCoordinate start,
        string permittedSeatId,
        float radius);

    /// <summary>
    /// Runtime-office owner for non-seat interaction reservations. It chooses live concrete offers,
    /// acquires capacity plus an approach cell, validates the same offer again at arrival, and owns
    /// all terminal cleanup. No handle or reservation belongs in GameState or a save file.
    /// </summary>
    public sealed class OfficeRuntimeInteractionLifecycleService
    {
        private readonly OfficeRuntimeInteractionOfferProvider _offerProvider;
        private readonly OfficeInteractionReservationBook _reservations =
            new OfficeInteractionReservationBook();
        private readonly Dictionary<string, OfficeRuntimeInteractionHandle> _activeByMember =
            new Dictionary<string, OfficeRuntimeInteractionHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _nextAttemptByRequest =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public OfficeRuntimeInteractionLifecycleService(OfficeRuntimeInteractionOfferResolver resolver)
            : this(RequiredProvider(resolver))
        {
        }

        public OfficeRuntimeInteractionLifecycleService(OfficeRuntimeInteractionOfferProvider offerProvider)
        {
            _offerProvider = offerProvider ?? throw new ArgumentNullException(nameof(offerProvider));
        }

        public int ActiveReservationCount => _reservations.ActiveReservationCount;

        public IReadOnlyList<OfficeRuntimeInteractionHandle> ActiveHandles =>
            _activeByMember.Values
                .OrderBy(item => item.MemberId, StringComparer.Ordinal)
                .ThenBy(item => item.Token, StringComparer.Ordinal)
                .ToArray();

        public bool TryBegin(
            OfficeRuntimeInteractionRequest request,
            out OfficeRuntimeInteractionHandle handle,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            handle = null;

            if (!OfficeInteractionCatalog.TryGetDefinition(
                    request.InteractionId,
                    out OfficeInteractionDefinition definition))
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.UnknownInteraction);
                return false;
            }

            switch (definition.ReservationPolicy)
            {
                case OfficeInteractionReservationPolicy.None:
                case OfficeInteractionReservationPolicy.AssignedSeat:
                    failure = Failure(OfficeRuntimeInteractionFailureCode.PolicyManagedByExistingPath);
                    return false;
                case OfficeInteractionReservationPolicy.PairedConversation:
                case OfficeInteractionReservationPolicy.GroupMeeting:
                    failure = Failure(OfficeRuntimeInteractionFailureCode.UnsupportedReservationPolicy);
                    return false;
                case OfficeInteractionReservationPolicy.ExclusiveFurniture:
                case OfficeInteractionReservationPolicy.SharedFurnitureCapacity:
                    break;
                default:
                    failure = Failure(OfficeRuntimeInteractionFailureCode.UnsupportedReservationPolicy);
                    return false;
            }

            if (_activeByMember.TryGetValue(request.MemberId, out OfficeRuntimeInteractionHandle active))
            {
                if (active.IsActive &&
                    string.Equals(active.Request.RequestKey, request.RequestKey, StringComparison.Ordinal))
                {
                    handle = active;
                    failure = OfficeRuntimeInteractionFailure.None;
                    return true;
                }

                if (active.IsActive)
                {
                    failure = Failure(OfficeRuntimeInteractionFailureCode.ExistingMemberReservation);
                    return false;
                }
                _activeByMember.Remove(request.MemberId);
            }

            OfficeInteractionOffer[] offers = OrderedOffers(_offerProvider(
                definition,
                request.MemberId,
                request.Start,
                request.PermittedSeatId,
                request.Radius));
            if (offers.Length == 0)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.NoReachableOffer);
                return false;
            }

            int attempt = _nextAttemptByRequest.TryGetValue(request.RequestKey, out int nextAttempt)
                ? nextAttempt
                : 0;
            string token = BuildToken(request, attempt);
            int offerStart = StableRandom.StableRandomInt(
                "starter-office-interaction-offer:" + request.RequestKey,
                offers.Length);
            OfficeInteractionReservationFailure lastReservationFailure =
                OfficeInteractionReservationFailure.None;
            for (var offerOffset = 0; offerOffset < offers.Length; offerOffset++)
            {
                OfficeInteractionOffer offer = offers[(offerStart + offerOffset) % offers.Length];
                OfficeGridCoordinate[] cells = offer.ApproachCells
                    .OrderBy(cell => cell.Y)
                    .ThenBy(cell => cell.X)
                    .ToArray();
                int cellStart = StableRandom.StableRandomInt(
                    "starter-office-interaction-cell:" + request.RequestKey + ":" + offer.OfferId,
                    cells.Length);
                for (var cellOffset = 0; cellOffset < cells.Length; cellOffset++)
                {
                    OfficeGridCoordinate cell = cells[(cellStart + cellOffset) % cells.Length];
                    if (_reservations.TryReserve(
                            definition,
                            offer,
                            request.MemberId,
                            token,
                            cell,
                            out OfficeInteractionReservation reservation,
                            out OfficeInteractionReservationFailure reservationFailure))
                    {
                        handle = new OfficeRuntimeInteractionHandle(
                            this,
                            request,
                            definition,
                            offer,
                            reservation);
                        _activeByMember.Add(request.MemberId, handle);
                        _nextAttemptByRequest[request.RequestKey] = attempt + 1;
                        failure = OfficeRuntimeInteractionFailure.None;
                        return true;
                    }

                    lastReservationFailure = reservationFailure;
                    if (reservationFailure == OfficeInteractionReservationFailure.MemberAlreadyReserved ||
                        reservationFailure == OfficeInteractionReservationFailure.TokenConflict ||
                        reservationFailure == OfficeInteractionReservationFailure.TokenAlreadyReleased ||
                        reservationFailure == OfficeInteractionReservationFailure.UnsupportedReservationPolicy ||
                        reservationFailure == OfficeInteractionReservationFailure.NoReservationRequired)
                    {
                        failure = Failure(
                            OfficeRuntimeInteractionFailureCode.ReservationRejected,
                            reservationFailure);
                        return false;
                    }
                }
            }

            failure = Failure(
                OfficeRuntimeInteractionFailureCode.ReservationRejected,
                lastReservationFailure);
            return false;
        }

        public bool TryValidateArrival(
            OfficeRuntimeInteractionHandle handle,
            OfficeGridCoordinate actualCell,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (!ValidateOwnedHandle(handle, out failure)) return false;
            if (handle.State == OfficeRuntimeInteractionHandleState.Completed)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyCompleted);
                return false;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Aborted)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyAborted);
                return false;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Released)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyReleased);
                return false;
            }
            if (!_reservations.ContainsActiveToken(handle.Token))
            {
                MarkTerminal(handle, OfficeRuntimeInteractionHandleState.Released);
                failure = Failure(OfficeRuntimeInteractionFailureCode.ReservationLost);
                return false;
            }
            if (!actualCell.Equals(handle.ApproachCell))
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.ArrivalCellMismatch);
                return false;
            }

            OfficeInteractionOffer[] liveOffers = OrderedOffers(_offerProvider(
                handle.Definition,
                handle.MemberId,
                actualCell,
                handle.Request.PermittedSeatId,
                handle.Request.Radius));
            bool isLive = liveOffers.Any(offer =>
                DefinitionMatchesOffer(handle.Definition, offer) &&
                string.Equals(offer.OfferId, handle.OfferId, StringComparison.Ordinal) &&
                string.Equals(offer.FurnitureId, handle.FurnitureId, StringComparison.Ordinal) &&
                offer.ApproachCells.Contains(handle.ApproachCell));
            if (!isLive)
            {
                ReleaseAs(handle, OfficeRuntimeInteractionHandleState.Aborted, out _);
                failure = Failure(OfficeRuntimeInteractionFailureCode.LiveOfferUnavailable);
                return false;
            }

            handle.State = OfficeRuntimeInteractionHandleState.Arrived;
            failure = OfficeRuntimeInteractionFailure.None;
            return true;
        }

        public bool TryComplete(
            OfficeRuntimeInteractionHandle handle,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (!ValidateOwnedHandle(handle, out failure)) return false;
            if (handle.State == OfficeRuntimeInteractionHandleState.Completed)
            {
                failure = OfficeRuntimeInteractionFailure.None;
                return true;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Aborted)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyAborted);
                return false;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Released)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyReleased);
                return false;
            }
            if (handle.State != OfficeRuntimeInteractionHandleState.Arrived)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.NotArrived);
                return false;
            }
            return ReleaseAs(handle, OfficeRuntimeInteractionHandleState.Completed, out failure);
        }

        public bool TryAbort(
            OfficeRuntimeInteractionHandle handle,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (!ValidateOwnedHandle(handle, out failure)) return false;
            if (handle.State == OfficeRuntimeInteractionHandleState.Aborted)
            {
                failure = OfficeRuntimeInteractionFailure.None;
                return true;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Completed)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyCompleted);
                return false;
            }
            if (handle.State == OfficeRuntimeInteractionHandleState.Released)
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.AlreadyReleased);
                return false;
            }
            return ReleaseAs(handle, OfficeRuntimeInteractionHandleState.Aborted, out failure);
        }

        public bool TryRelease(
            OfficeRuntimeInteractionHandle handle,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (!ValidateOwnedHandle(handle, out failure)) return false;
            if (!handle.IsActive)
            {
                failure = OfficeRuntimeInteractionFailure.None;
                return true;
            }
            return ReleaseAs(handle, OfficeRuntimeInteractionHandleState.Released, out failure);
        }

        public int AbortAll()
        {
            OfficeRuntimeInteractionHandle[] active = ActiveHandles.ToArray();
            var aborted = 0;
            foreach (OfficeRuntimeInteractionHandle handle in active)
            {
                if (TryAbort(handle, out _)) aborted++;
            }
            return aborted;
        }

        public bool TryAbortForMember(
            string memberId,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (!_activeByMember.TryGetValue(memberId.Trim(), out OfficeRuntimeInteractionHandle handle))
            {
                failure = OfficeRuntimeInteractionFailure.None;
                return true;
            }
            return TryAbort(handle, out failure);
        }

        private bool ReleaseAs(
            OfficeRuntimeInteractionHandle handle,
            OfficeRuntimeInteractionHandleState terminalState,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (!_reservations.TryRelease(handle.Token, out _))
            {
                MarkTerminal(handle, OfficeRuntimeInteractionHandleState.Released);
                failure = Failure(OfficeRuntimeInteractionFailureCode.ReservationLost);
                return false;
            }
            MarkTerminal(handle, terminalState);
            failure = OfficeRuntimeInteractionFailure.None;
            return true;
        }

        private void MarkTerminal(
            OfficeRuntimeInteractionHandle handle,
            OfficeRuntimeInteractionHandleState terminalState)
        {
            handle.State = terminalState;
            if (_activeByMember.TryGetValue(handle.MemberId, out OfficeRuntimeInteractionHandle active) &&
                ReferenceEquals(active, handle))
            {
                _activeByMember.Remove(handle.MemberId);
            }
        }

        private bool ValidateOwnedHandle(
            OfficeRuntimeInteractionHandle handle,
            out OfficeRuntimeInteractionFailure failure)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            if (!handle.IsOwnedBy(this))
            {
                failure = Failure(OfficeRuntimeInteractionFailureCode.ForeignHandle);
                return false;
            }
            failure = OfficeRuntimeInteractionFailure.None;
            return true;
        }

        private static OfficeRuntimeInteractionOfferProvider RequiredProvider(
            OfficeRuntimeInteractionOfferResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            return resolver.ResolveReachableOffers;
        }

        private static OfficeInteractionOffer[] OrderedOffers(IReadOnlyList<OfficeInteractionOffer> offers)
        {
            return offers == null
                ? Array.Empty<OfficeInteractionOffer>()
                : offers
                    .Where(offer => offer != null)
                    .OrderBy(offer => offer.OfferId, StringComparer.Ordinal)
                    .ThenBy(offer => offer.FurnitureId, StringComparer.Ordinal)
                    .ToArray();
        }

        private static bool DefinitionMatchesOffer(
            OfficeInteractionDefinition definition,
            OfficeInteractionOffer offer)
        {
            return string.Equals(definition.InteractionId, offer.InteractionId, StringComparison.Ordinal) &&
                   string.Equals(definition.FurnitureKindId, offer.FurnitureKindId, StringComparison.Ordinal) &&
                   definition.SemanticLocation == offer.Location &&
                   definition.Capacity == offer.Capacity &&
                   offer.IsFurnitureOffer;
        }

        private static string BuildToken(OfficeRuntimeInteractionRequest request, int attempt)
        {
            return "starter-office-interaction:" + request.RequestKey + ":" +
                   attempt.ToString(CultureInfo.InvariantCulture);
        }

        private static OfficeRuntimeInteractionFailure Failure(
            OfficeRuntimeInteractionFailureCode code,
            OfficeInteractionReservationFailure reservationFailure = OfficeInteractionReservationFailure.None)
        {
            return new OfficeRuntimeInteractionFailure(code, reservationFailure);
        }
    }
}

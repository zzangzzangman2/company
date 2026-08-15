using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Simulation.OfficeSeating
{
    public enum OfficeSeatMeaningState
    {
        Unassigned = 0,
        Assigned = 1,
        Reserved = 2,
        Occupied = 3
    }

    public enum OfficeSeatOperationFailure
    {
        None = 0,
        InvalidSeatId = 1,
        InvalidMemberId = 2,
        InvalidToken = 3,
        UnknownSeat = 4,
        SeatAssignedToOtherMember = 5,
        SeatHasActiveClaim = 6,
        SeatClaimedByOtherMember = 7,
        MemberHasActiveClaim = 8,
        ReservationRequired = 9,
        SeatAlreadyOccupied = 10,
        TokenMismatch = 11,
        TokenAlreadyActive = 12
    }

    public enum OfficeSeatingImportFailure
    {
        None = 0,
        InvalidSnapshot = 1,
        InvalidSeatId = 2,
        InvalidMemberId = 3,
        UnknownSeat = 4,
        DuplicateSeat = 5,
        DuplicateMember = 6,
        ActiveClaimsPresent = 7
    }

    public struct OfficeSeatPosition : IEquatable<OfficeSeatPosition>
    {
        public OfficeSeatPosition(double x, double z)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                throw new ArgumentOutOfRangeException(nameof(x));
            if (double.IsNaN(z) || double.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(z));
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }

        public double DistanceSquaredTo(OfficeSeatPosition other)
        {
            var deltaX = X - other.X;
            var deltaZ = Z - other.Z;
            return (deltaX * deltaX) + (deltaZ * deltaZ);
        }

        public bool Equals(OfficeSeatPosition other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is OfficeSeatPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Z.GetHashCode();
            }
        }
    }

    public sealed class OfficeSeatDefinition
    {
        public OfficeSeatDefinition(string seatId, OfficeSeatPosition position)
        {
            SeatId = OfficeSeatingState.NormalizeRequiredId(seatId, nameof(seatId));
            Position = position;
        }

        public string SeatId { get; }
        public OfficeSeatPosition Position { get; }
    }

    public sealed class OfficeSeatView
    {
        internal OfficeSeatView(
            string seatId,
            OfficeSeatPosition position,
            string assignedMemberId,
            string runtimeMemberId,
            OfficeSeatMeaningState state)
        {
            SeatId = seatId;
            Position = position;
            AssignedMemberId = assignedMemberId ?? string.Empty;
            RuntimeMemberId = runtimeMemberId ?? string.Empty;
            State = state;
        }

        public string SeatId { get; }
        public OfficeSeatPosition Position { get; }
        public string AssignedMemberId { get; }
        public string RuntimeMemberId { get; }
        public OfficeSeatMeaningState State { get; }
    }

    public sealed class OfficeSeatOperationResult
    {
        internal OfficeSeatOperationResult(
            bool succeeded,
            bool changed,
            OfficeSeatOperationFailure failure,
            string seatId,
            string memberId,
            string previousAssignedSeatId,
            OfficeSeatMeaningState state)
        {
            Succeeded = succeeded;
            Changed = changed;
            Failure = failure;
            SeatId = seatId ?? string.Empty;
            MemberId = memberId ?? string.Empty;
            PreviousAssignedSeatId = previousAssignedSeatId ?? string.Empty;
            State = state;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public OfficeSeatOperationFailure Failure { get; }
        public string SeatId { get; }
        public string MemberId { get; }
        public string PreviousAssignedSeatId { get; }
        public OfficeSeatMeaningState State { get; }
    }

    public sealed class OfficeSeatAssignment
    {
        public OfficeSeatAssignment(string seatId, string memberId)
        {
            SeatId = seatId;
            MemberId = memberId;
        }

        public string SeatId { get; }
        public string MemberId { get; }
    }

    public sealed class OfficeSeatingAssignmentSnapshot
    {
        private readonly ReadOnlyCollection<OfficeSeatAssignment> _assignments;

        public OfficeSeatingAssignmentSnapshot(IEnumerable<OfficeSeatAssignment> assignments)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            _assignments = new List<OfficeSeatAssignment>(assignments).AsReadOnly();
        }

        public IReadOnlyList<OfficeSeatAssignment> Assignments => _assignments;
    }

    public sealed class OfficeSeatingImportResult
    {
        internal OfficeSeatingImportResult(
            bool succeeded,
            OfficeSeatingImportFailure failure,
            string offendingId,
            int importedAssignmentCount)
        {
            Succeeded = succeeded;
            Failure = failure;
            OffendingId = offendingId ?? string.Empty;
            ImportedAssignmentCount = importedAssignmentCount;
        }

        public bool Succeeded { get; }
        public OfficeSeatingImportFailure Failure { get; }
        public string OffendingId { get; }
        public int ImportedAssignmentCount { get; }
    }

    public sealed class OfficeSeatingState
    {
        public enum PreparedRuntimeMutationKind
        {
            Occupy = 0,
            Release = 1
        }

        public readonly struct PreparedRuntimeMutation
        {
            internal readonly OfficeSeatingState _owner;
            internal readonly SeatState _seat;

            internal PreparedRuntimeMutation(
                OfficeSeatingState owner,
                SeatState seat,
                string memberId,
                string token,
                OfficeSeatMeaningState expectedState,
                PreparedRuntimeMutationKind kind,
                ulong version)
            {
                _owner = owner;
                _seat = seat;
                MemberId = memberId;
                Token = token;
                ExpectedState = expectedState;
                Kind = kind;
                Version = version;
            }

            public string MemberId { get; }
            public string Token { get; }
            public OfficeSeatMeaningState ExpectedState { get; }
            public PreparedRuntimeMutationKind Kind { get; }
            public ulong Version { get; }
        }

        private readonly Dictionary<string, SeatState> _seats;
        private readonly List<string> _orderedSeatIds;
        private readonly Dictionary<string, string> _assignedSeatByMember;
        private readonly Dictionary<string, string> _activeSeatByMember;
        private readonly Dictionary<string, string> _activeSeatByToken;
        private ulong _runtimeMutationVersion;

        public OfficeSeatingState(IEnumerable<OfficeSeatDefinition> seatDefinitions)
        {
            if (seatDefinitions == null) throw new ArgumentNullException(nameof(seatDefinitions));

            _seats = new Dictionary<string, SeatState>(StringComparer.Ordinal);
            _orderedSeatIds = new List<string>();
            _assignedSeatByMember = new Dictionary<string, string>(StringComparer.Ordinal);
            _activeSeatByMember = new Dictionary<string, string>(StringComparer.Ordinal);
            _activeSeatByToken = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var definition in seatDefinitions)
            {
                if (definition == null)
                    throw new ArgumentException("Seat definitions cannot contain null.", nameof(seatDefinitions));
                if (_seats.ContainsKey(definition.SeatId))
                    throw new ArgumentException("Duplicate seat ID: " + definition.SeatId, nameof(seatDefinitions));
                _seats.Add(definition.SeatId, new SeatState(definition.SeatId, definition.Position));
                _orderedSeatIds.Add(definition.SeatId);
            }

            _orderedSeatIds.Sort(StringComparer.Ordinal);
        }

        public int SeatCount => _orderedSeatIds.Count;

        public IReadOnlyList<OfficeSeatView> GetSeats()
        {
            var views = new OfficeSeatView[_orderedSeatIds.Count];
            for (var index = 0; index < _orderedSeatIds.Count; index++)
                views[index] = ToView(_seats[_orderedSeatIds[index]]);
            return Array.AsReadOnly(views);
        }

        public bool TryGetSeat(string seatId, out OfficeSeatView seat)
        {
            seat = null;
            if (!TryNormalizeId(seatId, out var normalizedSeatId)) return false;
            if (!_seats.TryGetValue(normalizedSeatId, out var state)) return false;
            seat = ToView(state);
            return true;
        }

        public bool TryAssign(string seatId, string memberId, out OfficeSeatOperationResult result)
        {
            if (!TryNormalizeId(seatId, out var normalizedSeatId))
                return Failed(OfficeSeatOperationFailure.InvalidSeatId, seatId, memberId, out result);
            if (!TryNormalizeId(memberId, out var normalizedMemberId))
                return Failed(OfficeSeatOperationFailure.InvalidMemberId, normalizedSeatId, memberId, out result);
            if (!_seats.TryGetValue(normalizedSeatId, out var target))
                return Failed(OfficeSeatOperationFailure.UnknownSeat, normalizedSeatId, normalizedMemberId, out result);

            if (string.Equals(target.AssignedMemberId, normalizedMemberId, StringComparison.Ordinal))
            {
                result = Success(false, target, normalizedMemberId, string.Empty);
                return true;
            }

            if (target.HasActiveClaim)
                return Failed(OfficeSeatOperationFailure.SeatHasActiveClaim, target, normalizedMemberId, out result);
            if (!string.IsNullOrEmpty(target.AssignedMemberId))
                return Failed(OfficeSeatOperationFailure.SeatAssignedToOtherMember, target, normalizedMemberId, out result);
            if (_activeSeatByMember.ContainsKey(normalizedMemberId))
                return Failed(OfficeSeatOperationFailure.MemberHasActiveClaim, target, normalizedMemberId, out result);

            var previousSeatId = string.Empty;
            if (_assignedSeatByMember.TryGetValue(normalizedMemberId, out var existingSeatId))
            {
                var existing = _seats[existingSeatId];
                if (existing.HasActiveClaim)
                    return Failed(OfficeSeatOperationFailure.MemberHasActiveClaim, target, normalizedMemberId, out result);
                previousSeatId = existing.SeatId;
            }

            if (previousSeatId.Length > 0)
                _seats[previousSeatId].AssignedMemberId = null;
            target.AssignedMemberId = normalizedMemberId;
            _assignedSeatByMember[normalizedMemberId] = target.SeatId;
            _runtimeMutationVersion++;
            result = Success(true, target, normalizedMemberId, previousSeatId);
            return true;
        }

        public bool TryUnassign(string seatId, string memberId, out OfficeSeatOperationResult result)
        {
            if (!TryNormalizeId(seatId, out var normalizedSeatId))
                return Failed(OfficeSeatOperationFailure.InvalidSeatId, seatId, memberId, out result);
            if (!TryNormalizeId(memberId, out var normalizedMemberId))
                return Failed(OfficeSeatOperationFailure.InvalidMemberId, normalizedSeatId, memberId, out result);
            if (!_seats.TryGetValue(normalizedSeatId, out var target))
                return Failed(OfficeSeatOperationFailure.UnknownSeat, normalizedSeatId, normalizedMemberId, out result);

            if (string.IsNullOrEmpty(target.AssignedMemberId))
            {
                result = Success(false, target, normalizedMemberId, string.Empty);
                return true;
            }
            if (!string.Equals(target.AssignedMemberId, normalizedMemberId, StringComparison.Ordinal))
                return Failed(OfficeSeatOperationFailure.SeatAssignedToOtherMember, target, normalizedMemberId, out result);
            if (target.HasActiveClaim)
                return Failed(OfficeSeatOperationFailure.SeatHasActiveClaim, target, normalizedMemberId, out result);

            target.AssignedMemberId = null;
            _assignedSeatByMember.Remove(normalizedMemberId);
            _runtimeMutationVersion++;
            result = Success(true, target, normalizedMemberId, string.Empty);
            return true;
        }

        public bool TryReserve(
            string seatId,
            string memberId,
            string token,
            out OfficeSeatOperationResult result)
        {
            if (!TryValidateClaimArguments(seatId, memberId, token, out var target, out var normalizedMemberId, out var normalizedToken, out result))
                return false;

            if (!string.IsNullOrEmpty(target.AssignedMemberId) &&
                !string.Equals(target.AssignedMemberId, normalizedMemberId, StringComparison.Ordinal))
            {
                return Failed(OfficeSeatOperationFailure.SeatAssignedToOtherMember, target, normalizedMemberId, out result);
            }

            if (target.RuntimeState == OfficeSeatMeaningState.Occupied)
            {
                if (string.Equals(target.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal) &&
                    string.Equals(target.Token, normalizedToken, StringComparison.Ordinal))
                {
                    result = Success(false, target, normalizedMemberId, string.Empty);
                    return true;
                }
                return Failed(OfficeSeatOperationFailure.SeatAlreadyOccupied, target, normalizedMemberId, out result);
            }
            if (target.RuntimeState == OfficeSeatMeaningState.Reserved)
            {
                if (!string.Equals(target.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal))
                    return Failed(OfficeSeatOperationFailure.SeatClaimedByOtherMember, target, normalizedMemberId, out result);
                if (!string.Equals(target.Token, normalizedToken, StringComparison.Ordinal))
                    return Failed(OfficeSeatOperationFailure.TokenMismatch, target, normalizedMemberId, out result);
                result = Success(false, target, normalizedMemberId, string.Empty);
                return true;
            }

            if (_activeSeatByMember.TryGetValue(normalizedMemberId, out var activeSeatId) &&
                !string.Equals(activeSeatId, target.SeatId, StringComparison.Ordinal))
            {
                return Failed(OfficeSeatOperationFailure.MemberHasActiveClaim, target, normalizedMemberId, out result);
            }
            if (_activeSeatByToken.TryGetValue(normalizedToken, out var tokenSeatId) &&
                !string.Equals(tokenSeatId, target.SeatId, StringComparison.Ordinal))
            {
                return Failed(OfficeSeatOperationFailure.TokenAlreadyActive, target, normalizedMemberId, out result);
            }

            target.RuntimeMemberId = normalizedMemberId;
            target.Token = normalizedToken;
            target.RuntimeState = OfficeSeatMeaningState.Reserved;
            _activeSeatByMember[normalizedMemberId] = target.SeatId;
            _activeSeatByToken[normalizedToken] = target.SeatId;
            _runtimeMutationVersion++;
            result = Success(true, target, normalizedMemberId, string.Empty);
            return true;
        }

        public bool TryOccupy(string token, out OfficeSeatOperationResult result)
        {
            if (!TryNormalizeId(token, out var normalizedToken))
                return Failed(OfficeSeatOperationFailure.InvalidToken, string.Empty, string.Empty, out result);
            if (!_activeSeatByToken.TryGetValue(normalizedToken, out var seatId))
                return Failed(OfficeSeatOperationFailure.ReservationRequired, string.Empty, string.Empty, out result);
            var seat = _seats[seatId];
            return TryOccupy(seat.SeatId, seat.RuntimeMemberId, normalizedToken, out result);
        }

        public bool TryOccupy(
            string seatId,
            string memberId,
            string token,
            out OfficeSeatOperationResult result)
        {
            if (!TryValidateClaimArguments(seatId, memberId, token, out var target, out var normalizedMemberId, out var normalizedToken, out result))
                return false;
            if (!target.HasActiveClaim)
                return Failed(OfficeSeatOperationFailure.ReservationRequired, target, normalizedMemberId, out result);
            if (!string.Equals(target.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal))
                return Failed(OfficeSeatOperationFailure.SeatClaimedByOtherMember, target, normalizedMemberId, out result);
            if (!string.Equals(target.Token, normalizedToken, StringComparison.Ordinal))
                return Failed(OfficeSeatOperationFailure.TokenMismatch, target, normalizedMemberId, out result);
            if (target.RuntimeState == OfficeSeatMeaningState.Occupied)
            {
                result = Success(false, target, normalizedMemberId, string.Empty);
                return true;
            }
            if (target.RuntimeState != OfficeSeatMeaningState.Reserved)
                return Failed(OfficeSeatOperationFailure.ReservationRequired, target, normalizedMemberId, out result);

            target.RuntimeState = OfficeSeatMeaningState.Occupied;
            _runtimeMutationVersion++;
            result = Success(true, target, normalizedMemberId, string.Empty);
            return true;
        }

        public bool TryRelease(
            string seatId,
            string memberId,
            string token,
            out OfficeSeatOperationResult result)
        {
            if (!TryValidateClaimArguments(seatId, memberId, token, out var target, out var normalizedMemberId, out var normalizedToken, out result))
                return false;
            if (!target.HasActiveClaim)
            {
                result = Success(false, target, normalizedMemberId, string.Empty);
                return true;
            }
            if (!string.Equals(target.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal))
                return Failed(OfficeSeatOperationFailure.SeatClaimedByOtherMember, target, normalizedMemberId, out result);
            if (!string.Equals(target.Token, normalizedToken, StringComparison.Ordinal))
                return Failed(OfficeSeatOperationFailure.TokenMismatch, target, normalizedMemberId, out result);

            var releasedToken = target.Token;
            target.RuntimeMemberId = null;
            target.Token = null;
            target.RuntimeState = OfficeSeatMeaningState.Unassigned;
            _activeSeatByMember.Remove(normalizedMemberId);
            _activeSeatByToken.Remove(releasedToken);
            _runtimeMutationVersion++;
            result = Success(true, target, normalizedMemberId, string.Empty);
            return true;
        }

        public bool TryPrepareRuntimeOccupy(
            string seatId,
            string memberId,
            string token,
            out PreparedRuntimeMutation prepared)
        {
            prepared = default;
            if (!TryValidateClaimArguments(
                    seatId,
                    memberId,
                    token,
                    out SeatState seat,
                    out string normalizedMemberId,
                    out string normalizedToken,
                    out _)) return false;
            if (seat.RuntimeState != OfficeSeatMeaningState.Reserved ||
                !string.Equals(seat.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal) ||
                !string.Equals(seat.Token, normalizedToken, StringComparison.Ordinal)) return false;
            prepared = new PreparedRuntimeMutation(
                this,
                seat,
                normalizedMemberId,
                normalizedToken,
                OfficeSeatMeaningState.Reserved,
                PreparedRuntimeMutationKind.Occupy,
                _runtimeMutationVersion);
            return true;
        }

        public bool TryPrepareRuntimeRelease(
            string seatId,
            string memberId,
            string token,
            out PreparedRuntimeMutation prepared)
        {
            prepared = default;
            if (!TryValidateClaimArguments(
                    seatId,
                    memberId,
                    token,
                    out SeatState seat,
                    out string normalizedMemberId,
                    out string normalizedToken,
                    out _)) return false;
            if (seat.RuntimeState != OfficeSeatMeaningState.Occupied ||
                !string.Equals(seat.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal) ||
                !string.Equals(seat.Token, normalizedToken, StringComparison.Ordinal)) return false;
            prepared = new PreparedRuntimeMutation(
                this,
                seat,
                normalizedMemberId,
                normalizedToken,
                OfficeSeatMeaningState.Occupied,
                PreparedRuntimeMutationKind.Release,
                _runtimeMutationVersion);
            return true;
        }

        public bool IsPreparedRuntimeMutationCurrent(in PreparedRuntimeMutation prepared)
        {
            return ReferenceEquals(prepared._owner, this) &&
                   prepared._seat != null &&
                   prepared.Version == _runtimeMutationVersion &&
                   prepared._seat.RuntimeState == prepared.ExpectedState &&
                   string.Equals(prepared._seat.RuntimeMemberId, prepared.MemberId, StringComparison.Ordinal) &&
                   string.Equals(prepared._seat.Token, prepared.Token, StringComparison.Ordinal);
        }

        public void CommitPreparedRuntimeOccupy(in PreparedRuntimeMutation prepared)
        {
            prepared._seat.RuntimeState = OfficeSeatMeaningState.Occupied;
            _runtimeMutationVersion++;
        }

        public void CommitPreparedRuntimeRelease(in PreparedRuntimeMutation prepared)
        {
            string releasedToken = prepared._seat.Token;
            prepared._seat.RuntimeMemberId = null;
            prepared._seat.Token = null;
            prepared._seat.RuntimeState = OfficeSeatMeaningState.Unassigned;
            _activeSeatByMember.Remove(prepared.MemberId);
            _activeSeatByToken.Remove(releasedToken);
            _runtimeMutationVersion++;
        }

        public bool TryRelease(string token, out OfficeSeatOperationResult result)
        {
            if (!TryNormalizeId(token, out var normalizedToken))
                return Failed(OfficeSeatOperationFailure.InvalidToken, string.Empty, string.Empty, out result);
            if (!_activeSeatByToken.TryGetValue(normalizedToken, out var seatId))
            {
                result = new OfficeSeatOperationResult(
                    true,
                    false,
                    OfficeSeatOperationFailure.None,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    OfficeSeatMeaningState.Unassigned);
                return true;
            }
            var seat = _seats[seatId];
            return TryRelease(seat.SeatId, seat.RuntimeMemberId, normalizedToken, out result);
        }

        public bool TryReleaseForMember(
            string memberId,
            string token,
            out OfficeSeatOperationResult result)
        {
            if (!TryNormalizeId(memberId, out var normalizedMemberId))
                return Failed(OfficeSeatOperationFailure.InvalidMemberId, string.Empty, memberId, out result);
            if (!TryNormalizeId(token, out var normalizedToken))
                return Failed(OfficeSeatOperationFailure.InvalidToken, string.Empty, normalizedMemberId, out result);
            if (!_activeSeatByMember.TryGetValue(normalizedMemberId, out var seatId))
            {
                result = new OfficeSeatOperationResult(
                    true,
                    false,
                    OfficeSeatOperationFailure.None,
                    string.Empty,
                    normalizedMemberId,
                    string.Empty,
                    OfficeSeatMeaningState.Unassigned);
                return true;
            }
            return TryRelease(seatId, normalizedMemberId, normalizedToken, out result);
        }

        public bool TryFindNearestAvailableSeat(
            OfficeSeatPosition origin,
            string memberId,
            out OfficeSeatView seat)
        {
            seat = null;
            if (!TryNormalizeId(memberId, out var normalizedMemberId)) return false;
            if (_activeSeatByMember.ContainsKey(normalizedMemberId)) return false;

            SeatState best = null;
            var bestDistance = double.PositiveInfinity;
            for (var index = 0; index < _orderedSeatIds.Count; index++)
            {
                var candidate = _seats[_orderedSeatIds[index]];
                if (candidate.HasActiveClaim) continue;
                if (!string.IsNullOrEmpty(candidate.AssignedMemberId) &&
                    !string.Equals(candidate.AssignedMemberId, normalizedMemberId, StringComparison.Ordinal))
                {
                    continue;
                }

                var distance = origin.DistanceSquaredTo(candidate.Position);
                var distanceOrder = distance.CompareTo(bestDistance);
                if (best == null || distanceOrder < 0 ||
                    (distanceOrder == 0 && string.CompareOrdinal(candidate.SeatId, best.SeatId) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (best == null) return false;
            seat = ToView(best);
            return true;
        }

        public OfficeSeatingAssignmentSnapshot ExportPersistentAssignments()
        {
            var assignments = new List<OfficeSeatAssignment>();
            for (var index = 0; index < _orderedSeatIds.Count; index++)
            {
                var seat = _seats[_orderedSeatIds[index]];
                if (!string.IsNullOrEmpty(seat.AssignedMemberId))
                    assignments.Add(new OfficeSeatAssignment(seat.SeatId, seat.AssignedMemberId));
            }
            return new OfficeSeatingAssignmentSnapshot(assignments);
        }

        public bool TryImportPersistentAssignments(
            OfficeSeatingAssignmentSnapshot snapshot,
            out OfficeSeatingImportResult result)
        {
            if (snapshot == null)
                return ImportFailed(OfficeSeatingImportFailure.InvalidSnapshot, string.Empty, out result);
            if (_activeSeatByMember.Count > 0)
                return ImportFailed(OfficeSeatingImportFailure.ActiveClaimsPresent, string.Empty, out result);

            var normalized = new List<OfficeSeatAssignment>();
            var seenSeats = new HashSet<string>(StringComparer.Ordinal);
            var seenMembers = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Assignments.Count; index++)
            {
                var assignment = snapshot.Assignments[index];
                if (assignment == null)
                    return ImportFailed(OfficeSeatingImportFailure.InvalidSnapshot, string.Empty, out result);
                if (!TryNormalizeId(assignment.SeatId, out var seatId))
                    return ImportFailed(OfficeSeatingImportFailure.InvalidSeatId, assignment.SeatId, out result);
                if (!TryNormalizeId(assignment.MemberId, out var memberId))
                    return ImportFailed(OfficeSeatingImportFailure.InvalidMemberId, assignment.MemberId, out result);
                if (!_seats.ContainsKey(seatId))
                    return ImportFailed(OfficeSeatingImportFailure.UnknownSeat, seatId, out result);
                if (!seenSeats.Add(seatId))
                    return ImportFailed(OfficeSeatingImportFailure.DuplicateSeat, seatId, out result);
                if (!seenMembers.Add(memberId))
                    return ImportFailed(OfficeSeatingImportFailure.DuplicateMember, memberId, out result);
                normalized.Add(new OfficeSeatAssignment(seatId, memberId));
            }

            normalized.Sort((left, right) => string.CompareOrdinal(left.SeatId, right.SeatId));
            for (var index = 0; index < _orderedSeatIds.Count; index++)
                _seats[_orderedSeatIds[index]].AssignedMemberId = null;
            _assignedSeatByMember.Clear();
            for (var index = 0; index < normalized.Count; index++)
            {
                var assignment = normalized[index];
                _seats[assignment.SeatId].AssignedMemberId = assignment.MemberId;
                _assignedSeatByMember.Add(assignment.MemberId, assignment.SeatId);
            }
            _runtimeMutationVersion++;

            result = new OfficeSeatingImportResult(true, OfficeSeatingImportFailure.None, string.Empty, normalized.Count);
            return true;
        }

        internal static string NormalizeRequiredId(string value, string parameterName)
        {
            if (!TryNormalizeId(value, out var normalized))
                throw new ArgumentException("A non-empty stable ordinal ID is required.", parameterName);
            return normalized;
        }

        private bool TryValidateClaimArguments(
            string seatId,
            string memberId,
            string token,
            out SeatState seat,
            out string normalizedMemberId,
            out string normalizedToken,
            out OfficeSeatOperationResult result)
        {
            seat = null;
            normalizedMemberId = string.Empty;
            normalizedToken = string.Empty;
            if (!TryNormalizeId(seatId, out var normalizedSeatId))
                return Failed(OfficeSeatOperationFailure.InvalidSeatId, seatId, memberId, out result);
            if (!TryNormalizeId(memberId, out normalizedMemberId))
                return Failed(OfficeSeatOperationFailure.InvalidMemberId, normalizedSeatId, memberId, out result);
            if (!TryNormalizeId(token, out normalizedToken))
                return Failed(OfficeSeatOperationFailure.InvalidToken, normalizedSeatId, normalizedMemberId, out result);
            if (!_seats.TryGetValue(normalizedSeatId, out seat))
                return Failed(OfficeSeatOperationFailure.UnknownSeat, normalizedSeatId, normalizedMemberId, out result);
            result = null;
            return true;
        }

        private static bool TryNormalizeId(string value, out string normalized)
        {
            normalized = value == null ? string.Empty : value.Trim();
            return normalized.Length > 0;
        }

        private static OfficeSeatView ToView(SeatState seat)
        {
            return new OfficeSeatView(
                seat.SeatId,
                seat.Position,
                seat.AssignedMemberId,
                seat.RuntimeMemberId,
                seat.MeaningState);
        }

        private static OfficeSeatOperationResult Success(
            bool changed,
            SeatState seat,
            string memberId,
            string previousAssignedSeatId)
        {
            return new OfficeSeatOperationResult(
                true,
                changed,
                OfficeSeatOperationFailure.None,
                seat.SeatId,
                memberId,
                previousAssignedSeatId,
                seat.MeaningState);
        }

        private static bool Failed(
            OfficeSeatOperationFailure failure,
            SeatState seat,
            string memberId,
            out OfficeSeatOperationResult result)
        {
            result = new OfficeSeatOperationResult(
                false,
                false,
                failure,
                seat.SeatId,
                memberId,
                string.Empty,
                seat.MeaningState);
            return false;
        }

        private static bool Failed(
            OfficeSeatOperationFailure failure,
            string seatId,
            string memberId,
            out OfficeSeatOperationResult result)
        {
            result = new OfficeSeatOperationResult(
                false,
                false,
                failure,
                seatId == null ? string.Empty : seatId.Trim(),
                memberId == null ? string.Empty : memberId.Trim(),
                string.Empty,
                OfficeSeatMeaningState.Unassigned);
            return false;
        }

        private static bool ImportFailed(
            OfficeSeatingImportFailure failure,
            string offendingId,
            out OfficeSeatingImportResult result)
        {
            result = new OfficeSeatingImportResult(false, failure, offendingId, 0);
            return false;
        }

        internal sealed class SeatState
        {
            public SeatState(string seatId, OfficeSeatPosition position)
            {
                SeatId = seatId;
                Position = position;
                RuntimeState = OfficeSeatMeaningState.Unassigned;
            }

            public string SeatId { get; }
            public OfficeSeatPosition Position { get; }
            public string AssignedMemberId { get; set; }
            public string RuntimeMemberId { get; set; }
            public string Token { get; set; }
            public OfficeSeatMeaningState RuntimeState { get; set; }
            public bool HasActiveClaim => RuntimeState == OfficeSeatMeaningState.Reserved ||
                                          RuntimeState == OfficeSeatMeaningState.Occupied;
            public OfficeSeatMeaningState MeaningState => HasActiveClaim
                ? RuntimeState
                : string.IsNullOrEmpty(AssignedMemberId)
                    ? OfficeSeatMeaningState.Unassigned
                    : OfficeSeatMeaningState.Assigned;
        }
    }
}

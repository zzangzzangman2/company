using System;
using System.Collections.Generic;

namespace FamilyCompany.Save.OfficeSeating
{
    public enum UnknownOfficeSeatingIdPolicy
    {
        Reject = 0,
        Skip = 1
    }

    public enum OfficeSeatingSaveError
    {
        None = 0,
        UnsupportedSchemaVersion = 1,
        NullAssignment = 2,
        EmptyMemberId = 3,
        EmptySeatId = 4,
        DuplicateMember = 5,
        DuplicateSeat = 6,
        UnknownMember = 7,
        UnknownSeat = 8
    }

    public sealed class OfficeSeatingSaveValidationException : InvalidOperationException
    {
        public OfficeSeatingSaveValidationException(
            OfficeSeatingSaveError error,
            string identifier,
            string message)
            : base(message)
        {
            Error = error;
            Identifier = identifier ?? string.Empty;
        }

        public OfficeSeatingSaveError Error { get; }
        public string Identifier { get; }
    }

    /// <summary>
    /// The integration boundary between the mutable JsonUtility DTO and an immutable,
    /// deterministically ordered semantic snapshot. It deliberately has no dependency on
    /// the future simulation seating implementation.
    /// </summary>
    public static class OfficeSeatingSaveAdapter
    {
        public static OfficeSeatingSnapshot Capture(
            IEnumerable<OfficeSeatingAssignment> assignments)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            return Canonicalize(assignments, null, null, UnknownOfficeSeatingIdPolicy.Reject);
        }

        public static OfficeSeatingSaveDto ToDto(OfficeSeatingSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var dto = new OfficeSeatingSaveDto();
            foreach (var assignment in snapshot.Assignments)
            {
                dto.seatAssignments.Add(new OfficeSeatAssignmentSaveDto
                {
                    memberId = assignment.MemberId,
                    seatId = assignment.SeatId
                });
            }

            return dto;
        }

        /// <summary>
        /// Restores a payload using an explicit catalog supplied by the future central
        /// integration. A null payload is the v5 migration path and produces an empty snapshot.
        /// </summary>
        public static OfficeSeatingSnapshot Restore(
            OfficeSeatingSaveDto dto,
            IEnumerable<string> knownMemberIds,
            IEnumerable<string> knownSeatIds,
            UnknownOfficeSeatingIdPolicy unknownIdPolicy = UnknownOfficeSeatingIdPolicy.Reject)
        {
            if (dto == null) return OfficeSeatingSnapshot.Empty;
            if (dto.schemaVersion != OfficeSeatingSaveDto.CurrentSchemaVersion)
            {
                throw Error(
                    OfficeSeatingSaveError.UnsupportedSchemaVersion,
                    dto.schemaVersion.ToString(),
                    $"Unsupported office seating schema: {dto.schemaVersion}.");
            }

            var members = BuildKnownIds(knownMemberIds, nameof(knownMemberIds));
            var seats = BuildKnownIds(knownSeatIds, nameof(knownSeatIds));
            var assignments = new List<OfficeSeatingAssignment>();
            var source = dto.seatAssignments ?? new List<OfficeSeatAssignmentSaveDto>();
            for (var index = 0; index < source.Count; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    throw Error(
                        OfficeSeatingSaveError.NullAssignment,
                        index.ToString(),
                        $"Office seat assignment at index {index} is null.");
                }

                var memberId = CanonicalDtoId(
                    item.memberId,
                    OfficeSeatingSaveError.EmptyMemberId,
                    "memberId");
                var seatId = CanonicalDtoId(
                    item.seatId,
                    OfficeSeatingSaveError.EmptySeatId,
                    "seatId");
                var memberKnown = members.Contains(memberId);
                var seatKnown = seats.Contains(seatId);
                if (!memberKnown || !seatKnown)
                {
                    if (unknownIdPolicy == UnknownOfficeSeatingIdPolicy.Skip) continue;
                    if (!memberKnown)
                    {
                        throw Error(
                            OfficeSeatingSaveError.UnknownMember,
                            memberId,
                            $"Unknown office seating member ID: {memberId}.");
                    }

                    throw Error(
                        OfficeSeatingSaveError.UnknownSeat,
                        seatId,
                        $"Unknown office seat ID: {seatId}.");
                }

                assignments.Add(new OfficeSeatingAssignment(memberId, seatId));
            }

            return Canonicalize(assignments, members, seats, unknownIdPolicy);
        }

        private static OfficeSeatingSnapshot Canonicalize(
            IEnumerable<OfficeSeatingAssignment> source,
            HashSet<string> knownMembers,
            HashSet<string> knownSeats,
            UnknownOfficeSeatingIdPolicy unknownIdPolicy)
        {
            var result = new List<OfficeSeatingAssignment>();
            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            var seatIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw Error(
                        OfficeSeatingSaveError.NullAssignment,
                        string.Empty,
                        "Office seating snapshot contains a null assignment.");
                }

                var memberId = item.MemberId.Trim();
                var seatId = item.SeatId.Trim();
                var memberKnown = knownMembers == null || knownMembers.Contains(memberId);
                var seatKnown = knownSeats == null || knownSeats.Contains(seatId);
                if (!memberKnown || !seatKnown)
                {
                    if (unknownIdPolicy == UnknownOfficeSeatingIdPolicy.Skip) continue;
                    throw Error(
                        !memberKnown
                            ? OfficeSeatingSaveError.UnknownMember
                            : OfficeSeatingSaveError.UnknownSeat,
                        !memberKnown ? memberId : seatId,
                        !memberKnown
                            ? $"Unknown office seating member ID: {memberId}."
                            : $"Unknown office seat ID: {seatId}.");
                }

                if (!memberIds.Add(memberId))
                {
                    throw Error(
                        OfficeSeatingSaveError.DuplicateMember,
                        memberId,
                        $"Office seating member is assigned more than once: {memberId}.");
                }

                if (!seatIds.Add(seatId))
                {
                    throw Error(
                        OfficeSeatingSaveError.DuplicateSeat,
                        seatId,
                        $"Office seat is assigned more than once: {seatId}.");
                }

                result.Add(new OfficeSeatingAssignment(memberId, seatId));
            }

            result.Sort(CompareAssignments);
            return result.Count == 0
                ? OfficeSeatingSnapshot.Empty
                : new OfficeSeatingSnapshot(result);
        }

        private static HashSet<string> BuildKnownIds(
            IEnumerable<string> source,
            string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                var canonical = (item ?? string.Empty).Trim();
                if (canonical.Length == 0)
                    throw new ArgumentException("Known office seating IDs cannot be empty.", parameterName);
                if (!result.Add(canonical))
                    throw new ArgumentException($"Known office seating ID is duplicated: {canonical}.", parameterName);
            }

            return result;
        }

        private static string CanonicalDtoId(
            string value,
            OfficeSeatingSaveError emptyError,
            string fieldName)
        {
            var canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0)
            {
                throw Error(
                    emptyError,
                    string.Empty,
                    $"Office seating {fieldName} cannot be empty.");
            }

            return canonical;
        }

        private static int CompareAssignments(
            OfficeSeatingAssignment left,
            OfficeSeatingAssignment right)
        {
            var memberComparison = string.CompareOrdinal(left.MemberId, right.MemberId);
            return memberComparison != 0
                ? memberComparison
                : string.CompareOrdinal(left.SeatId, right.SeatId);
        }

        private static OfficeSeatingSaveValidationException Error(
            OfficeSeatingSaveError error,
            string identifier,
            string message)
        {
            return new OfficeSeatingSaveValidationException(error, identifier, message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Save.OfficeSeating
{
    public sealed class OfficeSeatingAssignment
    {
        public OfficeSeatingAssignment(string memberId, string seatId)
        {
            MemberId = CanonicalId(memberId, nameof(memberId));
            SeatId = CanonicalId(seatId, nameof(seatId));
        }

        public string MemberId { get; }
        public string SeatId { get; }

        private static string CanonicalId(string value, string parameterName)
        {
            var canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0)
                throw new ArgumentException("Office seating IDs cannot be empty.", parameterName);
            return canonical;
        }
    }

    /// <summary>
    /// Adapter-neutral snapshot that the future pure seating rules can translate without
    /// taking a dependency on Unity or on this DTO's mutable List representation.
    /// </summary>
    public sealed class OfficeSeatingSnapshot
    {
        private static readonly OfficeSeatingSnapshot EmptySnapshot =
            new OfficeSeatingSnapshot(Array.Empty<OfficeSeatingAssignment>());

        private readonly ReadOnlyCollection<OfficeSeatingAssignment> _assignments;

        internal OfficeSeatingSnapshot(IList<OfficeSeatingAssignment> assignments)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            _assignments = new List<OfficeSeatingAssignment>(assignments).AsReadOnly();
        }

        public static OfficeSeatingSnapshot Empty => EmptySnapshot;
        public IReadOnlyList<OfficeSeatingAssignment> Assignments => _assignments;
        public int Count => _assignments.Count;
    }
}

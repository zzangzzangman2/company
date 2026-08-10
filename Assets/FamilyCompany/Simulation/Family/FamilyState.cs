using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Family
{
    public sealed class FamilyState
    {
        private readonly List<FamilyMemberState> _members;

        public FamilyState(IEnumerable<FamilyMemberState> members)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            _members = new List<FamilyMemberState>(members);
            if (_members.Count == 0 || _members.Select(item => item.MemberId).Distinct(StringComparer.Ordinal).Count() != _members.Count)
            {
                throw new InvalidOperationException("Family members must have unique IDs.");
            }
        }

        public IReadOnlyList<FamilyMemberState> Members => _members;

        public FamilyMemberState Get(string memberId)
        {
            var member = _members.FirstOrDefault(item => item.MemberId == memberId);
            if (member == null)
            {
                throw new KeyNotFoundException($"Unknown family member: {memberId}");
            }

            return member;
        }
    }
}


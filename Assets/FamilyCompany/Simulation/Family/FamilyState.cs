using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;

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

        public void RecordSharedCareerMemory(
            string memoryId,
            BusinessIndustry industry,
            CareerMemoryKind kind,
            string summary,
            long occurredMinute,
            int bondDelta)
        {
            var memberIds = _members.Select(item => item.MemberId).ToArray();
            foreach (var member in _members)
            {
                member.RecordCareerMemory(new CareerMemoryState(
                    $"{memoryId}:{member.MemberId}",
                    industry,
                    kind,
                    summary,
                    occurredMinute,
                    bondDelta,
                    memberIds.Where(id => id != member.MemberId)));
            }
        }

        public void RecordPairCareerMemory(
            string memoryId,
            string firstMemberId,
            string secondMemberId,
            BusinessIndustry industry,
            CareerMemoryKind kind,
            string summary,
            long occurredMinute,
            int bondDelta)
        {
            if (string.IsNullOrWhiteSpace(memoryId)) throw new ArgumentException("Memory ID is required.", nameof(memoryId));
            if (firstMemberId == secondMemberId) throw new ArgumentException("Relationship members must be different.");
            var first = Get(firstMemberId);
            var second = Get(secondMemberId);
            first.RecordCareerMemory(new CareerMemoryState(
                $"{memoryId}:{first.MemberId}",
                industry,
                kind,
                summary,
                occurredMinute,
                bondDelta,
                new[] { second.MemberId }));
            second.RecordCareerMemory(new CareerMemoryState(
                $"{memoryId}:{second.MemberId}",
                industry,
                kind,
                summary,
                occurredMinute,
                bondDelta,
                new[] { first.MemberId }));
        }

        public int RelationshipScore(string memberId, string otherMemberId)
        {
            if (memberId == otherMemberId) throw new ArgumentException("Relationship members must be different.");
            var member = Get(memberId);
            Get(otherMemberId);
            return member.CareerMemories
                .Where(memory => memory.ColleagueMemberIds.Contains(otherMemberId))
                .Sum(memory => memory.BondDelta);
        }

        public string RelationshipLabel(string memberId, string otherMemberId)
        {
            var score = RelationshipScore(memberId, otherMemberId);
            if (score >= 5) return "단짝";
            if (score <= -3) return "앙숙";
            if (score >= 2) return "좋은 동료";
            if (score <= -1) return "서먹한 동료";
            return "평범한 동료";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Family
{
    public sealed class FamilyMemberWorkCapacity
    {
        public FamilyMemberWorkCapacity(
            string memberId,
            FamilyRole role,
            long fromMinute,
            long toMinute,
            long availableBlockCount)
        {
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (!Enum.IsDefined(typeof(FamilyRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (fromMinute < 0) throw new ArgumentOutOfRangeException(nameof(fromMinute));
            if (toMinute < fromMinute) throw new ArgumentOutOfRangeException(nameof(toMinute));
            if (availableBlockCount < 0) throw new ArgumentOutOfRangeException(nameof(availableBlockCount));

            MemberId = memberId;
            Role = role;
            FromMinute = fromMinute;
            ToMinute = toMinute;
            AvailableBlockCount = availableBlockCount;
        }

        public string MemberId { get; }
        public FamilyRole Role { get; }
        public long FromMinute { get; }
        public long ToMinute { get; }
        public long AvailableBlockCount { get; }
        public long AvailableMinutes => checked(AvailableBlockCount * FamilyWorkCapacityPlanner.WorkBlockMinutes);
        public decimal AvailablePersonHours => AvailableMinutes / 60m;
    }

    public sealed class FamilyWorkCapacityPlan
    {
        private readonly FamilyMemberWorkCapacity[] _members;

        public FamilyWorkCapacityPlan(
            long fromMinute,
            long toMinute,
            IEnumerable<FamilyMemberWorkCapacity> members,
            long companyAvailableBlockCount,
            int peakConcurrentMemberCount)
        {
            if (fromMinute < 0) throw new ArgumentOutOfRangeException(nameof(fromMinute));
            if (toMinute < fromMinute) throw new ArgumentOutOfRangeException(nameof(toMinute));
            if (members == null) throw new ArgumentNullException(nameof(members));
            if (companyAvailableBlockCount < 0) throw new ArgumentOutOfRangeException(nameof(companyAvailableBlockCount));
            if (peakConcurrentMemberCount < 0) throw new ArgumentOutOfRangeException(nameof(peakConcurrentMemberCount));

            FromMinute = fromMinute;
            ToMinute = toMinute;
            _members = members.OrderBy(item => item.MemberId, StringComparer.Ordinal).ToArray();
            if (_members.Select(item => item.MemberId).Distinct(StringComparer.Ordinal).Count() != _members.Length)
            {
                throw new InvalidOperationException("Capacity member IDs must be unique.");
            }

            CompanyAvailableBlockCount = companyAvailableBlockCount;
            PeakConcurrentMemberCount = peakConcurrentMemberCount;
            TotalAvailableMemberBlockCount = _members.Aggregate(
                0L,
                (total, member) => checked(total + member.AvailableBlockCount));
        }

        public long FromMinute { get; }
        public long ToMinute { get; }
        public IReadOnlyList<FamilyMemberWorkCapacity> Members => _members;

        // Number of clock blocks in which at least one family member can work.
        public long CompanyAvailableBlockCount { get; }

        // Sum of member blocks. This is the capacity used to calculate person-hours.
        public long TotalAvailableMemberBlockCount { get; }
        public long TotalAvailableMinutes => checked(TotalAvailableMemberBlockCount * FamilyWorkCapacityPlanner.WorkBlockMinutes);
        public decimal TotalAvailablePersonHours => TotalAvailableMinutes / 60m;
        public int PeakConcurrentMemberCount { get; }

        public FamilyMemberWorkCapacity GetMember(string memberId)
        {
            var member = _members.FirstOrDefault(item => item.MemberId == memberId);
            if (member == null) throw new KeyNotFoundException($"Unknown family member capacity: {memberId}");
            return member;
        }
    }

    public sealed class FamilyWorkCompletionEstimate
    {
        public FamilyWorkCompletionEstimate(
            long fromMinute,
            long toMinute,
            long requiredPersonHours,
            long requiredMemberBlockCount,
            long accumulatedMemberBlockCount,
            long earliestCompletionMinute)
        {
            FromMinute = fromMinute;
            ToMinute = toMinute;
            RequiredPersonHours = requiredPersonHours;
            RequiredMemberBlockCount = requiredMemberBlockCount;
            AccumulatedMemberBlockCount = accumulatedMemberBlockCount;
            EarliestCompletionMinute = earliestCompletionMinute;
        }

        public long FromMinute { get; }
        public long ToMinute { get; }
        public long RequiredPersonHours { get; }
        public long RequiredMemberBlockCount { get; }
        public long AccumulatedMemberBlockCount { get; }
        public bool CanComplete => EarliestCompletionMinute >= 0;
        public long EarliestCompletionMinute { get; }
        public DateTime? EarliestCompletionTime => CanComplete
            ? GameTime.CampaignStart.AddMinutes(EarliestCompletionMinute)
            : (DateTime?)null;
    }

    public static class FamilyWorkCapacityPlanner
    {
        public const int WorkBlockMinutes = AutonomousOfficeSimulation.PulseMinutes;
        public const int WorkBlocksPerPersonHour = 60 / WorkBlockMinutes;

        public static FamilyWorkCapacityPlan Calculate(FamilyState family, long fromMinute, long toMinute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            ValidateRange(fromMinute, toMinute);

            var members = family.Members
                .OrderBy(item => item.MemberId, StringComparer.Ordinal)
                .ToArray();
            var blockCounts = members.ToDictionary(item => item.MemberId, item => 0L, StringComparer.Ordinal);
            var companyAvailableBlocks = 0L;
            var peakConcurrentMembers = 0;

            foreach (var blockStart in CompleteBlockStarts(fromMinute, toMinute))
            {
                var now = GameTime.CampaignStart.AddMinutes(blockStart);
                var concurrentMembers = 0;
                foreach (var member in members)
                {
                    if (!FamilyScheduleRules.Resolve(member.Role, now).CanPerformCompanyWork) continue;
                    blockCounts[member.MemberId] = checked(blockCounts[member.MemberId] + 1L);
                    concurrentMembers++;
                }

                if (concurrentMembers <= 0) continue;
                companyAvailableBlocks = checked(companyAvailableBlocks + 1L);
                peakConcurrentMembers = Math.Max(peakConcurrentMembers, concurrentMembers);
            }

            var memberResults = members.Select(member => new FamilyMemberWorkCapacity(
                member.MemberId,
                member.Role,
                fromMinute,
                toMinute,
                blockCounts[member.MemberId]));
            return new FamilyWorkCapacityPlan(
                fromMinute,
                toMinute,
                memberResults,
                companyAvailableBlocks,
                peakConcurrentMembers);
        }

        public static FamilyMemberWorkCapacity CalculateMember(
            FamilyMemberState member,
            long fromMinute,
            long toMinute)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            ValidateRange(fromMinute, toMinute);

            var availableBlocks = 0L;
            foreach (var blockStart in CompleteBlockStarts(fromMinute, toMinute))
            {
                var now = GameTime.CampaignStart.AddMinutes(blockStart);
                if (FamilyScheduleRules.Resolve(member.Role, now).CanPerformCompanyWork)
                {
                    availableBlocks = checked(availableBlocks + 1L);
                }
            }

            return new FamilyMemberWorkCapacity(
                member.MemberId,
                member.Role,
                fromMinute,
                toMinute,
                availableBlocks);
        }

        public static FamilyWorkCompletionEstimate EstimateEarliestCompletion(
            FamilyState family,
            long fromMinute,
            long toMinute,
            long requiredPersonHours)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            return EstimateEarliestCompletion(
                family.Members.OrderBy(item => item.MemberId, StringComparer.Ordinal),
                fromMinute,
                toMinute,
                requiredPersonHours);
        }

        public static FamilyWorkCompletionEstimate EstimateEarliestCompletion(
            FamilyMemberState member,
            long fromMinute,
            long toMinute,
            long requiredPersonHours)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            return EstimateEarliestCompletion(
                new[] { member },
                fromMinute,
                toMinute,
                requiredPersonHours);
        }

        private static FamilyWorkCompletionEstimate EstimateEarliestCompletion(
            IEnumerable<FamilyMemberState> sourceMembers,
            long fromMinute,
            long toMinute,
            long requiredPersonHours)
        {
            ValidateRange(fromMinute, toMinute);
            if (requiredPersonHours <= 0) throw new ArgumentOutOfRangeException(nameof(requiredPersonHours));
            if (60 % WorkBlockMinutes != 0)
            {
                throw new InvalidOperationException("The work pulse must divide one person-hour exactly.");
            }

            var members = sourceMembers
                .OrderBy(item => item.MemberId, StringComparer.Ordinal)
                .ToArray();
            var requiredBlocks = checked(requiredPersonHours * WorkBlocksPerPersonHour);
            var accumulatedBlocks = 0L;

            foreach (var blockStart in CompleteBlockStarts(fromMinute, toMinute))
            {
                var now = GameTime.CampaignStart.AddMinutes(blockStart);
                foreach (var member in members)
                {
                    if (FamilyScheduleRules.Resolve(member.Role, now).CanPerformCompanyWork)
                    {
                        accumulatedBlocks = checked(accumulatedBlocks + 1L);
                    }
                }

                if (accumulatedBlocks < requiredBlocks) continue;
                return new FamilyWorkCompletionEstimate(
                    fromMinute,
                    toMinute,
                    requiredPersonHours,
                    requiredBlocks,
                    accumulatedBlocks,
                    checked(blockStart + WorkBlockMinutes));
            }

            return new FamilyWorkCompletionEstimate(
                fromMinute,
                toMinute,
                requiredPersonHours,
                requiredBlocks,
                accumulatedBlocks,
                -1);
        }

        private static IEnumerable<long> CompleteBlockStarts(long fromMinute, long toMinute)
        {
            var blockStart = AlignUpToBlock(fromMinute);
            while (blockStart < toMinute)
            {
                var blockEnd = checked(blockStart + WorkBlockMinutes);
                if (blockEnd > toMinute) yield break;
                yield return blockStart;
                blockStart = blockEnd;
            }
        }

        private static long AlignUpToBlock(long minute)
        {
            // Work pulses are wall-clock half-hours. CampaignStart begins at 08:50 so the
            // opening can show the 09:00 arrival; elapsed-zero anchoring would shift every
            // work pulse to :20/:50.
            long campaignMinuteOfDay = checked(
                GameTime.CampaignStart.Hour * 60L + GameTime.CampaignStart.Minute);
            var remainder = (campaignMinuteOfDay + minute) % WorkBlockMinutes;
            return remainder == 0
                ? minute
                : checked(minute + WorkBlockMinutes - remainder);
        }

        private static void ValidateRange(long fromMinute, long toMinute)
        {
            if (fromMinute < 0) throw new ArgumentOutOfRangeException(nameof(fromMinute));
            if (toMinute < fromMinute) throw new ArgumentOutOfRangeException(nameof(toMinute));
        }
    }
}

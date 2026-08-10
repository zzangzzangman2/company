using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Leisure
{
    public enum LeisureExecutionRejectionReason
    {
        None = 0,
        UnknownActivity = 1,
        InvalidStartMinute = 2,
        InvalidParticipants = 3,
        DuplicateParticipant = 4,
        ParticipantCountUnavailable = 5,
        InvalidParticipantState = 6,
        InvalidFamilyBond = 7,
        InvalidFunds = 8,
        ActivityNotYetAvailable = 9,
        ActivityExpired = 10,
        DayUnavailable = 11,
        InsufficientFunds = 12
    }

    public enum LeisureFundingSource
    {
        None = 0,
        Household = 1,
        Company = 2,
        HouseholdAndCompany = 3
    }

    public sealed class LeisureParticipantInput
    {
        public LeisureParticipantInput(string familyMemberId, int energy, int stress)
        {
            FamilyMemberId = familyMemberId;
            Energy = energy;
            Stress = stress;
        }

        public string FamilyMemberId { get; }
        public int Energy { get; }
        public int Stress { get; }
    }

    public sealed class LeisureParticipantEffect
    {
        internal LeisureParticipantEffect(
            string familyMemberId,
            int energyBefore,
            int energyAfter,
            int stressBefore,
            int stressAfter)
        {
            FamilyMemberId = familyMemberId;
            EnergyBefore = energyBefore;
            EnergyAfter = energyAfter;
            AppliedEnergyDelta = energyAfter - energyBefore;
            StressBefore = stressBefore;
            StressAfter = stressAfter;
            AppliedStressDelta = stressAfter - stressBefore;
        }

        public string FamilyMemberId { get; }
        public int EnergyBefore { get; }
        public int EnergyAfter { get; }
        public int AppliedEnergyDelta { get; }
        public int StressBefore { get; }
        public int StressAfter { get; }
        public int AppliedStressDelta { get; }
    }

    public sealed class LeisureExecutionResult
    {
        private readonly ReadOnlyCollection<LeisureParticipantEffect> _participantEffects;

        internal LeisureExecutionResult(
            bool succeeded,
            LeisureExecutionRejectionReason rejectionReason,
            string activityId,
            long startMinute,
            long endMinute,
            int durationMinutes,
            long requiredCostWon,
            long householdCostWon,
            long companyCostWon,
            long householdCashBeforeWon,
            long householdCashAfterWon,
            long companyCashBeforeWon,
            long companyCashAfterWon,
            int familyBondBefore,
            int familyBondAfter,
            int appliedFamilyBondDelta,
            LeisureFundingSource fundingSource,
            LeisureParticipantEffect[] participantEffects)
        {
            Succeeded = succeeded;
            RejectionReason = rejectionReason;
            ActivityId = activityId ?? string.Empty;
            StartMinute = startMinute;
            EndMinute = endMinute;
            DurationMinutes = durationMinutes;
            RequiredCostWon = requiredCostWon;
            HouseholdCostWon = householdCostWon;
            CompanyCostWon = companyCostWon;
            TotalCostWon = checked(householdCostWon + companyCostWon);
            HouseholdCashBeforeWon = householdCashBeforeWon;
            HouseholdCashAfterWon = householdCashAfterWon;
            CompanyCashBeforeWon = companyCashBeforeWon;
            CompanyCashAfterWon = companyCashAfterWon;
            FamilyBondBefore = familyBondBefore;
            FamilyBondAfter = familyBondAfter;
            AppliedFamilyBondDelta = appliedFamilyBondDelta;
            FundingSource = fundingSource;
            _participantEffects = Array.AsReadOnly(participantEffects ?? Array.Empty<LeisureParticipantEffect>());
        }

        public bool Succeeded { get; }
        public LeisureExecutionRejectionReason RejectionReason { get; }
        public string ActivityId { get; }
        public long StartMinute { get; }
        public long EndMinute { get; }
        public int DurationMinutes { get; }
        public long RequiredCostWon { get; }
        public long HouseholdCostWon { get; }
        public long CompanyCostWon { get; }
        public long TotalCostWon { get; }
        public long HouseholdCashBeforeWon { get; }
        public long HouseholdCashAfterWon { get; }
        public long CompanyCashBeforeWon { get; }
        public long CompanyCashAfterWon { get; }
        public int FamilyBondBefore { get; }
        public int FamilyBondAfter { get; }
        public int AppliedFamilyBondDelta { get; }
        public LeisureFundingSource FundingSource { get; }
        public IReadOnlyList<LeisureParticipantEffect> ParticipantEffects => _participantEffects;

        public LeisureParticipantEffect GetParticipant(string familyMemberId)
        {
            if (string.IsNullOrWhiteSpace(familyMemberId))
                throw new ArgumentException("A family member ID is required.", nameof(familyMemberId));
            var normalizedId = familyMemberId.Trim();
            for (var index = 0; index < _participantEffects.Count; index++)
            {
                if (string.Equals(_participantEffects[index].FamilyMemberId, normalizedId, StringComparison.Ordinal))
                    return _participantEffects[index];
            }

            throw new KeyNotFoundException("Unknown leisure participant: " + normalizedId);
        }
    }

    public static class LeisureExecutionRules
    {
        public static LeisureExecutionResult Evaluate(
            string activityId,
            long startMinute,
            IEnumerable<LeisureParticipantInput> participants,
            int sharedFamilyBond,
            long companyAvailableCashWon,
            long householdAvailableCashWon)
        {
            var normalizedActivityId = activityId == null ? string.Empty : activityId.Trim();
            var activity = LeisureActivityCatalog.FindById(normalizedActivityId);
            if (activity == null)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.UnknownActivity,
                    normalizedActivityId,
                    startMinute,
                    0,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (!TryGetStartTime(startMinute, out var startAt))
            {
                return Rejected(
                    LeisureExecutionRejectionReason.InvalidStartMinute,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (sharedFamilyBond < 0 || sharedFamilyBond > 100)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.InvalidFamilyBond,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (companyAvailableCashWon < 0 || householdAvailableCashWon < 0)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.InvalidFunds,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            var participantReason = NormalizeParticipants(participants, out var normalizedParticipants);
            if (participantReason != LeisureExecutionRejectionReason.None)
            {
                return Rejected(
                    participantReason,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (normalizedParticipants.Count < activity.MinimumParticipants ||
                normalizedParticipants.Count > activity.MaximumParticipants)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.ParticipantCountUnavailable,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (startAt.Year < activity.MinimumYear)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.ActivityNotYetAvailable,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if (startAt.Year > activity.MaximumYearInclusive)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.ActivityExpired,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            if ((activity.AllowedDays & LeisureActivityDefinition.MaskFor(startAt.DayOfWeek)) == 0)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.DayUnavailable,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            var householdCostWon = Math.Min(householdAvailableCashWon, activity.CostWon);
            var companyCostWon = checked(activity.CostWon - householdCostWon);
            if (companyAvailableCashWon < companyCostWon)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.InsufficientFunds,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            long endMinute;
            try
            {
                endMinute = checked(startMinute + activity.DurationMinutes);
            }
            catch (OverflowException)
            {
                return Rejected(
                    LeisureExecutionRejectionReason.InvalidStartMinute,
                    activity.Id,
                    startMinute,
                    activity.CostWon,
                    sharedFamilyBond,
                    companyAvailableCashWon,
                    householdAvailableCashWon);
            }

            var effects = new LeisureParticipantEffect[normalizedParticipants.Count];
            for (var index = 0; index < normalizedParticipants.Count; index++)
            {
                var participant = normalizedParticipants[index];
                effects[index] = new LeisureParticipantEffect(
                    participant.FamilyMemberId,
                    participant.Energy,
                    ClampPercent(participant.Energy + activity.EnergyDelta),
                    participant.Stress,
                    ClampPercent(participant.Stress + activity.StressDelta));
            }

            var requestedBondDelta = normalizedParticipants.Count >= 2
                ? activity.SharedFamilyBondDelta
                : 0;
            var familyBondAfter = ClampPercent(sharedFamilyBond + requestedBondDelta);
            return new LeisureExecutionResult(
                true,
                LeisureExecutionRejectionReason.None,
                activity.Id,
                startMinute,
                endMinute,
                activity.DurationMinutes,
                activity.CostWon,
                householdCostWon,
                companyCostWon,
                householdAvailableCashWon,
                checked(householdAvailableCashWon - householdCostWon),
                companyAvailableCashWon,
                checked(companyAvailableCashWon - companyCostWon),
                sharedFamilyBond,
                familyBondAfter,
                familyBondAfter - sharedFamilyBond,
                FundingSource(householdCostWon, companyCostWon),
                effects);
        }

        private static LeisureExecutionRejectionReason NormalizeParticipants(
            IEnumerable<LeisureParticipantInput> participants,
            out List<NormalizedParticipant> normalizedParticipants)
        {
            normalizedParticipants = new List<NormalizedParticipant>();
            if (participants == null) return LeisureExecutionRejectionReason.InvalidParticipants;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var participant in participants)
            {
                if (participant == null || string.IsNullOrWhiteSpace(participant.FamilyMemberId))
                    return LeisureExecutionRejectionReason.InvalidParticipants;
                var normalizedId = participant.FamilyMemberId.Trim();
                if (!seenIds.Add(normalizedId)) return LeisureExecutionRejectionReason.DuplicateParticipant;
                if (!IsPercent(participant.Energy) || !IsPercent(participant.Stress))
                    return LeisureExecutionRejectionReason.InvalidParticipantState;
                normalizedParticipants.Add(
                    new NormalizedParticipant(normalizedId, participant.Energy, participant.Stress));
            }

            if (normalizedParticipants.Count < 1 || normalizedParticipants.Count > 4)
                return LeisureExecutionRejectionReason.ParticipantCountUnavailable;
            normalizedParticipants.Sort(
                (left, right) => string.CompareOrdinal(left.FamilyMemberId, right.FamilyMemberId));
            return LeisureExecutionRejectionReason.None;
        }

        private static bool TryGetStartTime(long startMinute, out DateTime startAt)
        {
            startAt = default(DateTime);
            if (startMinute < 0) return false;
            var maximumMinute = (DateTime.MaxValue.Ticks - GameTime.CampaignStart.Ticks) / TimeSpan.TicksPerMinute;
            if (startMinute > maximumMinute) return false;
            startAt = GameTime.CampaignStart.AddTicks(checked(startMinute * TimeSpan.TicksPerMinute));
            return true;
        }

        private static LeisureExecutionResult Rejected(
            LeisureExecutionRejectionReason reason,
            string activityId,
            long startMinute,
            long requiredCostWon,
            int sharedFamilyBond,
            long companyAvailableCashWon,
            long householdAvailableCashWon)
        {
            return new LeisureExecutionResult(
                false,
                reason,
                activityId,
                startMinute,
                startMinute,
                0,
                requiredCostWon,
                0,
                0,
                householdAvailableCashWon,
                householdAvailableCashWon,
                companyAvailableCashWon,
                companyAvailableCashWon,
                sharedFamilyBond,
                sharedFamilyBond,
                0,
                LeisureFundingSource.None,
                Array.Empty<LeisureParticipantEffect>());
        }

        private static LeisureFundingSource FundingSource(long householdCostWon, long companyCostWon)
        {
            if (householdCostWon > 0 && companyCostWon > 0) return LeisureFundingSource.HouseholdAndCompany;
            if (householdCostWon > 0) return LeisureFundingSource.Household;
            if (companyCostWon > 0) return LeisureFundingSource.Company;
            return LeisureFundingSource.None;
        }

        private static bool IsPercent(int value)
        {
            return value >= 0 && value <= 100;
        }

        private static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private sealed class NormalizedParticipant
        {
            public NormalizedParticipant(string familyMemberId, int energy, int stress)
            {
                FamilyMemberId = familyMemberId;
                Energy = energy;
                Stress = stress;
            }

            public string FamilyMemberId { get; }
            public int Energy { get; }
            public int Stress { get; }
        }
    }
}

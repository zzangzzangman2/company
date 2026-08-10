using System;

namespace FamilyCompany.Simulation.Contracts
{
    public enum ContractRejectionReason
    {
        None = 0,
        TeamTooSmall = 1,
        ContractTooLarge = 2,
        TooManyConcurrentContracts = 3,
        ScheduleCapacityExceeded = 4,
        UpfrontCashInsufficient = 5,
        ReputationInsufficient = 6,
        RewardOutsideBootstrapScope = 7
    }

    public sealed class ContractCapacityDecision
    {
        public ContractCapacityDecision(bool canAccept, ContractRejectionReason rejectionReason)
        {
            CanAccept = canAccept;
            RejectionReason = rejectionReason;
        }

        public bool CanAccept { get; }
        public ContractRejectionReason RejectionReason { get; }
    }

    public sealed class SmallTeamContractPolicy
    {
        public const int DefaultMaximumConcurrentContracts = 2;
        public const int DefaultMaximumPersonHoursPerContract = 80;
        public const long DefaultMaximumRewardWon = 2_500_000;
        public const int DefaultBillableHoursPerMemberPerWeek = 16;

        private readonly int _teamMemberCount;
        private readonly int _maximumConcurrentContracts;
        private readonly int _maximumPersonHoursPerContract;
        private readonly long _maximumRewardWon;
        private readonly int _billableHoursPerMemberPerWeek;

        public SmallTeamContractPolicy(
            int teamMemberCount,
            int maximumConcurrentContracts = DefaultMaximumConcurrentContracts,
            int maximumPersonHoursPerContract = DefaultMaximumPersonHoursPerContract,
            long maximumRewardWon = DefaultMaximumRewardWon,
            int billableHoursPerMemberPerWeek = DefaultBillableHoursPerMemberPerWeek)
        {
            if (teamMemberCount <= 0) throw new ArgumentOutOfRangeException(nameof(teamMemberCount));
            if (maximumConcurrentContracts <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrentContracts));
            if (maximumPersonHoursPerContract <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPersonHoursPerContract));
            if (maximumRewardWon <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRewardWon));
            if (billableHoursPerMemberPerWeek <= 0) throw new ArgumentOutOfRangeException(nameof(billableHoursPerMemberPerWeek));

            _teamMemberCount = teamMemberCount;
            _maximumConcurrentContracts = maximumConcurrentContracts;
            _maximumPersonHoursPerContract = maximumPersonHoursPerContract;
            _maximumRewardWon = maximumRewardWon;
            _billableHoursPerMemberPerWeek = billableHoursPerMemberPerWeek;
        }

        public ContractCapacityDecision Evaluate(
            SubcontractOffer offer,
            long companyCashWon,
            int companyReputation,
            int activeContractCount,
            int committedPersonHours)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (companyCashWon < 0) throw new ArgumentOutOfRangeException(nameof(companyCashWon));
            if (companyReputation < 0 || companyReputation > 100) throw new ArgumentOutOfRangeException(nameof(companyReputation));
            if (activeContractCount < 0) throw new ArgumentOutOfRangeException(nameof(activeContractCount));
            if (committedPersonHours < 0) throw new ArgumentOutOfRangeException(nameof(committedPersonHours));

            if (offer.RequiredWorkers > _teamMemberCount)
            {
                return Rejected(ContractRejectionReason.TeamTooSmall);
            }

            if (offer.EstimatedPersonHours > _maximumPersonHoursPerContract)
            {
                return Rejected(ContractRejectionReason.ContractTooLarge);
            }

            if (activeContractCount >= _maximumConcurrentContracts)
            {
                return Rejected(ContractRejectionReason.TooManyConcurrentContracts);
            }

            var deadlineWeeks = Math.Max(1, (offer.DeadlineDays + 6) / 7);
            var availablePersonHours = checked(_teamMemberCount * _billableHoursPerMemberPerWeek * deadlineWeeks);
            if (checked(committedPersonHours + offer.EstimatedPersonHours) > availablePersonHours)
            {
                return Rejected(ContractRejectionReason.ScheduleCapacityExceeded);
            }

            if (offer.UpfrontCostWon > companyCashWon)
            {
                return Rejected(ContractRejectionReason.UpfrontCashInsufficient);
            }

            if (offer.ReputationRequired > companyReputation)
            {
                return Rejected(ContractRejectionReason.ReputationInsufficient);
            }

            if (offer.RewardWon > _maximumRewardWon)
            {
                return Rejected(ContractRejectionReason.RewardOutsideBootstrapScope);
            }

            return new ContractCapacityDecision(true, ContractRejectionReason.None);
        }

        private static ContractCapacityDecision Rejected(ContractRejectionReason reason)
        {
            return new ContractCapacityDecision(false, reason);
        }
    }
}

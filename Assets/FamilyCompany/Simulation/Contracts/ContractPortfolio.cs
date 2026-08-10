using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.Contracts
{
    public enum ContractWorkRejectionReason
    {
        None = 0,
        ContractNotActive = 1,
        DeadlinePassed = 2,
        MemberEnergyInsufficient = 3,
        MemberUnavailable = 4
    }

    public sealed class ContractAcceptanceResult
    {
        public ContractAcceptanceResult(ContractCapacityDecision decision, SubcontractState contract)
        {
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            Contract = contract;
        }

        public ContractCapacityDecision Decision { get; }
        public SubcontractState Contract { get; }
        public bool Accepted => Decision.CanAccept;
    }

    public sealed class ContractWorkResult
    {
        public ContractWorkResult(
            ContractWorkRejectionReason rejectionReason,
            int appliedPersonHours,
            bool completed,
            long rewardWon)
        {
            RejectionReason = rejectionReason;
            AppliedPersonHours = appliedPersonHours;
            Completed = completed;
            RewardWon = rewardWon;
        }

        public ContractWorkRejectionReason RejectionReason { get; }
        public int AppliedPersonHours { get; }
        public bool Completed { get; }
        public long RewardWon { get; }
        public bool Applied => RejectionReason == ContractWorkRejectionReason.None;
    }

    public sealed class ContractPortfolio
    {
        private const int EnergyCostPerPersonHour = 2;
        private readonly List<SubcontractState> _contracts;
        private readonly SmallTeamContractPolicy _policy;

        public ContractPortfolio(int teamMemberCount, IEnumerable<SubcontractState> contracts = null)
        {
            _policy = new SmallTeamContractPolicy(teamMemberCount);
            _contracts = contracts == null ? new List<SubcontractState>() : new List<SubcontractState>(contracts);
            if (_contracts.Select(item => item.Offer.OfferId).Distinct(StringComparer.Ordinal).Count() != _contracts.Count)
            {
                throw new InvalidOperationException("Contract offer IDs must be unique.");
            }
        }

        public IReadOnlyList<SubcontractState> Contracts => _contracts;
        public int ActiveCount => _contracts.Count(item => item.Status == SubcontractStatus.Active);

        public ContractAcceptanceResult Accept(SubcontractOffer offer, CompanyState company, long elapsedMinute)
        {
            return AcceptInternal(offer, company, null, null, elapsedMinute);
        }

        public ContractAcceptanceResult Accept(
            SubcontractOffer offer,
            CompanyState company,
            FamilyState family,
            CompanyGrowthState growth,
            long elapsedMinute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (growth == null) throw new ArgumentNullException(nameof(growth));
            return AcceptInternal(offer, company, family, growth, elapsedMinute);
        }

        private ContractAcceptanceResult AcceptInternal(
            SubcontractOffer offer,
            CompanyState company,
            FamilyState family,
            CompanyGrowthState growth,
            long elapsedMinute)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));

            if (_contracts.Any(item => item.Offer.OfferId == offer.OfferId))
            {
                return new ContractAcceptanceResult(
                    new ContractCapacityDecision(false, ContractRejectionReason.DuplicateOffer),
                    null);
            }

            var committedPersonHours = _contracts
                .Where(item => item.Status == SubcontractStatus.Active)
                .Sum(item => item.RemainingPersonHours);
            var decision = _policy.Evaluate(
                offer,
                company.CashWon,
                company.Reputation,
                ActiveCount,
                committedPersonHours);
            if (!decision.CanAccept)
            {
                return new ContractAcceptanceResult(decision, null);
            }

            if (family != null && family.Members.Max(member => member.Stats.Development) < offer.RequiredDevelopment)
            {
                return RejectedAcceptance(ContractRejectionReason.DevelopmentInsufficient);
            }

            if (family != null && family.Members.Max(member => member.Stats.Speed) < offer.RequiredSpeed)
            {
                return RejectedAcceptance(ContractRejectionReason.SpeedInsufficient);
            }

            if (growth != null && !growth.HasTechnology(offer.RequiredTechnologyId))
            {
                return RejectedAcceptance(ContractRejectionReason.RequiredTechnologyMissing);
            }

            if (offer.UpfrontCostWon > 0)
            {
                company.PayOperatingExpense(
                    $"contract:{offer.OfferId}:upfront",
                    elapsedMinute,
                    offer.UpfrontCostWon,
                    $"{offer.ExactClientDisplayName} 계약 착수 비용");
            }

            var contract = new SubcontractState(offer, elapsedMinute);
            _contracts.Add(contract);
            return new ContractAcceptanceResult(decision, contract);
        }

        public ContractWorkResult RecordWork(
            string offerId,
            string memberId,
            int requestedPersonHours,
            long elapsedMinute,
            FamilyState family,
            CompanyState company)
        {
            if (requestedPersonHours <= 0) throw new ArgumentOutOfRangeException(nameof(requestedPersonHours));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (company == null) throw new ArgumentNullException(nameof(company));
            var contract = Get(offerId);
            if (elapsedMinute < contract.AcceptedMinute)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            }
            if (contract.Status != SubcontractStatus.Active)
            {
                return RejectedWork(ContractWorkRejectionReason.ContractNotActive);
            }

            if (elapsedMinute > contract.DueMinute)
            {
                Fail(contract, elapsedMinute, company, family);
                return RejectedWork(ContractWorkRejectionReason.DeadlinePassed);
            }

            var member = family.Get(memberId);
            var schedule = FamilyScheduleRules.Resolve(
                member.Role,
                FamilyCompany.Simulation.Core.GameTime.CampaignStart.AddMinutes(elapsedMinute));
            if (!schedule.CanPerformCompanyWork)
            {
                return RejectedWork(ContractWorkRejectionReason.MemberUnavailable);
            }
            var applicableHours = Math.Min(requestedPersonHours, contract.RemainingPersonHours);
            var energyCost = checked(applicableHours * EnergyCostPerPersonHour);
            if (member.Energy < energyCost)
            {
                return RejectedWork(ContractWorkRejectionReason.MemberEnergyInsufficient);
            }

            var appliedHours = contract.AddWork(memberId, applicableHours);
            member.ChangeEnergy(-checked(appliedHours * EnergyCostPerPersonHour));
            member.ChangeStress(Math.Max(1, (appliedHours + 3) / 4));

            if (contract.RemainingPersonHours != 0)
            {
                return new ContractWorkResult(ContractWorkRejectionReason.None, appliedHours, false, 0);
            }

            contract.MarkCompleted(elapsedMinute);
            company.RecordSale(
                $"contract:{contract.Offer.OfferId}:settlement",
                elapsedMinute,
                contract.Offer.RewardWon);
            company.ChangeReputation(2);
            var participantIds = contract.Contributions.Select(item => item.MemberId).ToArray();
            foreach (var participantId in participantIds)
            {
                family.Get(participantId).RecordCareerMemory(new CareerMemoryState(
                    $"contract:{contract.Offer.OfferId}:{participantId}",
                    contract.Offer.Industry,
                    CareerMemoryKind.ContractCompleted,
                    $"{contract.Offer.Title} 하청을 함께 끝냈다.",
                    elapsedMinute,
                    participantIds.Length > 1 ? 1 : 0,
                    participantIds.Where(id => id != participantId)));
            }
            return new ContractWorkResult(
                ContractWorkRejectionReason.None,
                appliedHours,
                true,
                contract.Offer.RewardWon);
        }

        public int FailOverdue(long elapsedMinute, CompanyState company, FamilyState family = null)
        {
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            if (company == null) throw new ArgumentNullException(nameof(company));
            var overdue = _contracts
                .Where(item => item.Status == SubcontractStatus.Active && elapsedMinute > item.DueMinute)
                .ToList();
            foreach (var contract in overdue)
            {
                Fail(contract, elapsedMinute, company, family);
            }

            return overdue.Count;
        }

        public SubcontractState Get(string offerId)
        {
            var contract = _contracts.FirstOrDefault(item => item.Offer.OfferId == offerId);
            if (contract == null) throw new KeyNotFoundException($"Unknown contract offer: {offerId}");
            return contract;
        }

        private static void Fail(SubcontractState contract, long elapsedMinute, CompanyState company, FamilyState family)
        {
            contract.MarkFailed(elapsedMinute);
            if (contract.Offer.PenaltyWon > 0)
            {
                company.RecordContractPenalty(
                    $"contract:{contract.Offer.OfferId}:penalty",
                    elapsedMinute,
                    contract.Offer.PenaltyWon,
                    $"{contract.Offer.ExactClientDisplayName} 계약 위약금");
            }
            if (family != null)
            {
                var participantIds = contract.Contributions.Select(item => item.MemberId).ToArray();
                foreach (var participantId in participantIds)
                {
                    family.Get(participantId).RecordCareerMemory(new CareerMemoryState(
                        $"contract:{contract.Offer.OfferId}:failed:{participantId}",
                        contract.Offer.Industry,
                        CareerMemoryKind.ContractFailed,
                        $"{contract.Offer.Title} 하청 마감을 함께 놓쳤다.",
                        elapsedMinute,
                        participantIds.Length > 1 ? -2 : -1,
                        participantIds.Where(id => id != participantId)));
                }
            }
            company.ChangeReputation(-2);
        }

        private static ContractAcceptanceResult RejectedAcceptance(ContractRejectionReason reason)
        {
            return new ContractAcceptanceResult(new ContractCapacityDecision(false, reason), null);
        }

        private static ContractWorkResult RejectedWork(ContractWorkRejectionReason reason)
        {
            return new ContractWorkResult(reason, 0, false, 0);
        }
    }
}

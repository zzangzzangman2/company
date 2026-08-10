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
        MemberEnergyInsufficient = 3
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
                Fail(contract, elapsedMinute, company);
                return RejectedWork(ContractWorkRejectionReason.DeadlinePassed);
            }

            var member = family.Get(memberId);
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
            return new ContractWorkResult(
                ContractWorkRejectionReason.None,
                appliedHours,
                true,
                contract.Offer.RewardWon);
        }

        public int FailOverdue(long elapsedMinute, CompanyState company)
        {
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            if (company == null) throw new ArgumentNullException(nameof(company));
            var overdue = _contracts
                .Where(item => item.Status == SubcontractStatus.Active && elapsedMinute > item.DueMinute)
                .ToList();
            foreach (var contract in overdue)
            {
                Fail(contract, elapsedMinute, company);
            }

            return overdue.Count;
        }

        public SubcontractState Get(string offerId)
        {
            var contract = _contracts.FirstOrDefault(item => item.Offer.OfferId == offerId);
            if (contract == null) throw new KeyNotFoundException($"Unknown contract offer: {offerId}");
            return contract;
        }

        private static void Fail(SubcontractState contract, long elapsedMinute, CompanyState company)
        {
            contract.MarkFailed(elapsedMinute);
            company.ChangeReputation(-2);
        }

        private static ContractWorkResult RejectedWork(ContractWorkRejectionReason reason)
        {
            return new ContractWorkResult(reason, 0, false, 0);
        }
    }
}

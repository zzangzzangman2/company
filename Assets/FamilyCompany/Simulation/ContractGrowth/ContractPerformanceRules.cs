using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public static class ContractPerformanceRules
    {
        public static ContractPerformanceSummary Rebuild(
            ContractPortfolio portfolio,
            FamilyState family,
            ContractClientTierCatalog clients)
        {
            if (portfolio == null) throw new ArgumentNullException(nameof(portfolio));
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (clients == null) throw new ArgumentNullException(nameof(clients));

            var records = new List<ContractPerformanceRecord>();
            foreach (var contract in portfolio.Contracts
                         .Where(item => item.Status == SubcontractStatus.Completed || item.Status == SubcontractStatus.Failed)
                         .OrderBy(item => item.ResolvedMinute)
                         .ThenBy(item => item.Offer.OfferId, StringComparer.Ordinal))
            {
                var client = clients.ResolveSavedClient(
                    contract.Offer.ClientCompanyId,
                    contract.Offer.ExactClientDisplayName,
                    contract.Offer.Industry);
                var completed = contract.Status == SubcontractStatus.Completed;
                var quality = completed ? CalculateQuality(contract, family) : 0;
                var onTime = completed && contract.ResolvedMinute <= contract.DueMinute;
                var satisfaction = completed
                    ? Clamp100(quality + (onTime ? 8 : -20) - SchedulePressurePenalty(contract))
                    : 0;
                records.Add(new ContractPerformanceRecord(
                    contract.Offer.OfferId,
                    client.ClientId,
                    client.Tier,
                    contract.Offer.Industry,
                    completed,
                    onTime,
                    quality,
                    satisfaction,
                    completed ? contract.CompletedPersonHours : contract.CompletedPersonHours / 2,
                    contract.AcceptedMinute,
                    contract.ResolvedMinute,
                    completed ? contract.Offer.RewardWon : 0));
            }
            return Summarize(records);
        }

        public static ContractPerformanceSummary Summarize(IEnumerable<ContractPerformanceRecord> source)
        {
            var records = (source ?? throw new ArgumentNullException(nameof(source)))
                .GroupBy(item => item.ContractInstanceId, StringComparer.Ordinal)
                .Select(group => group.Single())
                .OrderBy(item => item.ResolvedMinute)
                .ThenBy(item => item.ContractInstanceId, StringComparer.Ordinal)
                .ToArray();
            var completed = records.Where(item => item.Completed).ToArray();
            var domain = Enum.GetValues(typeof(BusinessIndustry))
                .Cast<BusinessIndustry>()
                .ToDictionary(
                    industry => industry,
                    industry => records.Where(item => item.Industry == industry).Sum(item => item.CreditedExperienceHours));
            var resolvedCount = records.Length;
            return new ContractPerformanceSummary(
                records,
                new ReadOnlyDictionary<BusinessIndustry, int>(domain),
                completed.Length,
                records.Count(item => !item.Completed),
                resolvedCount == 0 ? 10_000 : completed.Count(item => item.OnTime) * 10_000 / resolvedCount,
                completed.Length == 0 ? 0 : completed.Sum(item => item.Quality) / completed.Length,
                resolvedCount == 0 ? 0 : records.Sum(item => item.ClientSatisfaction) / resolvedCount,
                completed.Sum(item => item.EarnedRevenueWon));
        }

        public static ContractCompanyProfile BuildCompanyProfile(
            ContractPerformanceSummary summary,
            CompanyState company,
            FamilyState family,
            CompanyGrowthState growth)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (growth == null) throw new ArgumentNullException(nameof(growth));

            var capability = family.Members.Sum(member =>
                member.Capability.Skills.Engineering + member.Capability.Skills.Planning +
                member.Capability.Skills.Creative + member.Capability.Skills.Business +
                member.Capability.Skills.Operations + member.Capability.Skills.Collaboration) /
                Math.Max(1, family.Members.Count * 6);
            var capacity = Clamp100(
                capability +
                Math.Min(10, summary.CompletedContracts / 3) +
                Math.Min(8, growth.ResearchedTechnologyIds.Count * 2) +
                Math.Min(8, growth.OwnedBusinesses.Count * 2));
            var grade = ContractCompanyGrade.FamilyWorkshop;
            if (summary.CompletedContracts >= 3 && capacity >= 45) grade = ContractCompanyGrade.LocalProfessional;
            if (summary.CompletedContracts >= 8 && capacity >= 55) grade = ContractCompanyGrade.GrowthCompany;
            if (summary.CompletedContracts >= 16 && capacity >= 65) grade = ContractCompanyGrade.EstablishedVendor;
            if (summary.CompletedContracts >= 28 && capacity >= 75) grade = ContractCompanyGrade.PrimeReady;
            return new ContractCompanyProfile(
                grade,
                capacity,
                company.Reputation,
                company.CashWon,
                family.Members.Count,
                growth.ResearchedTechnologyIds.Count,
                growth.OwnedBusinesses.Count);
        }

        private static int CalculateQuality(SubcontractState contract, FamilyState family)
        {
            if (contract.Contributions.Count == 0) return 45;
            var specialty = LegacyContractTemplateCatalog.ResolveSpecialty(contract.Offer);
            var task = ContractWorkTaskProfiles.Resolve(specialty);
            var contributions = contract.Contributions.Select(item =>
                new KeyValuePair<WorkforceCapabilityState, int>(
                    family.Get(item.MemberId).Capability,
                    item.PersonHours));
            var average = WorkforcePerformanceRules.CalculateWeightedTeamScore(contributions, task, true);
            var requirementPenalty = Math.Max(0, contract.Offer.RequiredCapability - average) / 2;
            var collaborationBonus = Math.Min(6, Math.Max(0, contract.Contributions.Count - 1) * 2);
            return Clamp100(average + collaborationBonus - requirementPenalty);
        }

        private static int SchedulePressurePenalty(SubcontractState contract)
        {
            var duration = Math.Max(1L, contract.DueMinute - contract.AcceptedMinute);
            var remaining = Math.Max(0L, contract.DueMinute - contract.ResolvedMinute);
            return remaining * 10 / duration == 0 ? 3 : 0;
        }

        private static int Clamp100(int value) => Math.Max(0, Math.Min(100, value));
    }

    public static class ContractProgressionRules
    {
        private static readonly ContractTierRequirement[] Requirements =
        {
            new ContractTierRequirement(ContractClientTier.T0LocalBusiness, 0, 0, 0, 0, 0, 0, ContractCompanyGrade.FamilyWorkshop, 0),
            new ContractTierRequirement(ContractClientTier.T1RegionalSmallBusiness, 3, 7_000, 55, 55, 4, 35, ContractCompanyGrade.LocalProfessional, 45),
            new ContractTierRequirement(ContractClientTier.T2GrowthCompany, 8, 8_000, 65, 62, 12, 120, ContractCompanyGrade.GrowthCompany, 55),
            new ContractTierRequirement(ContractClientTier.T3PrimeVendor, 16, 8_500, 72, 70, 28, 300, ContractCompanyGrade.EstablishedVendor, 65),
            new ContractTierRequirement(ContractClientTier.T4NationalEnterprise, 28, 9_000, 80, 78, 45, 600, ContractCompanyGrade.PrimeReady, 75)
        };

        public static IReadOnlyList<ContractTierRequirement> TierRequirements => Requirements;

        public static IReadOnlyList<ContractTierProgress> EvaluateAll(
            ContractPerformanceSummary summary,
            ContractCompanyProfile profile,
            BusinessIndustry industry)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var result = new List<ContractTierProgress>();
            var previousUnlocked = true;
            foreach (var requirement in Requirements)
            {
                var progress = Evaluate(requirement, summary, profile, industry, previousUnlocked);
                result.Add(progress);
                previousUnlocked = progress.Unlocked;
            }
            return result;
        }

        public static ContractClientTier HighestUnlocked(IReadOnlyList<ContractTierProgress> progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            return progress.Where(item => item.Unlocked).Select(item => item.Tier).DefaultIfEmpty().Max();
        }

        private static ContractTierProgress Evaluate(
            ContractTierRequirement requirement,
            ContractPerformanceSummary summary,
            ContractCompanyProfile profile,
            BusinessIndustry industry,
            bool previousUnlocked)
        {
            if (requirement.Tier == ContractClientTier.T0LocalBusiness)
                return new ContractTierProgress(requirement, true, 10_000, new[] { "회복용 동네 계약은 항상 열림" });
            var experience = summary.DomainExperienceHours(industry);
            var ratios = new[]
            {
                Ratio(summary.CompletedContracts, requirement.CompletedContracts),
                Ratio(summary.OnTimeRateBasisPoints, requirement.OnTimeRateBasisPoints),
                Ratio(summary.AverageQuality, requirement.AverageQuality),
                Ratio(summary.AverageClientSatisfaction, requirement.AverageClientSatisfaction),
                Ratio(profile.Reputation, requirement.Reputation),
                Ratio(experience, requirement.RelevantDomainExperienceHours),
                Ratio((int)profile.Grade, (int)requirement.CompanyGrade),
                Ratio(profile.CapacityScore, requirement.CapacityScore)
            };
            var unlocked = previousUnlocked && ratios.All(value => value >= 10_000);
            var labels = new[]
            {
                $"완료 {summary.CompletedContracts}/{requirement.CompletedContracts}건",
                $"정시율 {summary.OnTimeRateBasisPoints / 100}%/{requirement.OnTimeRateBasisPoints / 100}%",
                $"평균 품질 {summary.AverageQuality}/{requirement.AverageQuality}",
                $"고객 만족 {summary.AverageClientSatisfaction}/{requirement.AverageClientSatisfaction}",
                $"평판 {profile.Reputation}/{requirement.Reputation}",
                $"{BusinessIndustryCatalog.Get(industry).DisplayName} 경험 {experience}/{requirement.RelevantDomainExperienceHours}인시",
                $"회사 등급 {profile.Grade}/{requirement.CompanyGrade}",
                $"업무 역량 {profile.CapacityScore}/{requirement.CapacityScore}"
            };
            return new ContractTierProgress(requirement, unlocked, previousUnlocked ? ratios.Min() : 0, labels);
        }

        private static int Ratio(int value, int requirement)
        {
            if (requirement <= 0) return 10_000;
            return Math.Min(10_000, Math.Max(0, value) * 10_000 / requirement);
        }
    }
}

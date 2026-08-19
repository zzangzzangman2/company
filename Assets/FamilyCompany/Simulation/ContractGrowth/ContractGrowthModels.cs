using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Technology;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public enum ContractClientTier
    {
        T0LocalBusiness = 0,
        T1RegionalSmallBusiness = 1,
        T2GrowthCompany = 2,
        T3PrimeVendor = 3,
        T4NationalEnterprise = 4
    }

    public enum ContractSpecialty
    {
        WebContent = 0,
        DataQualityAssurance = 1,
        OfficeNetwork = 2,
        BusinessSoftware = 3,
        Localization = 4,
        MobileContent = 5,
        HardwareOperations = 6,
        RetailOperations = 7
    }

    public enum ContractDifficulty
    {
        Starter = 0,
        Routine = 1,
        Skilled = 2,
        Professional = 3,
        Enterprise = 4
    }

    public enum ContractRiskLevel
    {
        Low = 0,
        Moderate = 1,
        High = 2,
        Critical = 3
    }

    public enum ContractCompanyGrade
    {
        FamilyWorkshop = 0,
        LocalProfessional = 1,
        GrowthCompany = 2,
        EstablishedVendor = 3,
        PrimeReady = 4
    }

    public enum ContractBusinessRoute
    {
        OfficeWorld = 0,
        BusinessHub = 1,
        ContractBoard = 2,
        ProductOpportunities = 3
    }

    public sealed class ContractClientDefinition
    {
        private readonly BusinessIndustry[] _industries;
        private readonly ContractSpecialty[] _specialties;

        public ContractClientDefinition(
            string clientId,
            string displayNameKo,
            ContractClientTier tier,
            IEnumerable<BusinessIndustry> industries,
            IEnumerable<ContractSpecialty> specialties,
            bool isHistoricalCompany,
            string sourceCompanySizeTier,
            string sourcePlayerReachIn2000,
            string neutralIndustryIconId,
            string logoResourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client ID is required.", nameof(clientId));
            if (string.IsNullOrWhiteSpace(displayNameKo)) throw new ArgumentException("Client display name is required.", nameof(displayNameKo));
            if (!Enum.IsDefined(typeof(ContractClientTier), tier)) throw new ArgumentOutOfRangeException(nameof(tier));
            ClientId = clientId;
            DisplayNameKo = displayNameKo;
            Tier = tier;
            _industries = (industries ?? throw new ArgumentNullException(nameof(industries))).Distinct().OrderBy(item => item).ToArray();
            _specialties = (specialties ?? throw new ArgumentNullException(nameof(specialties))).Distinct().OrderBy(item => item).ToArray();
            if (_industries.Length == 0) throw new ArgumentException("At least one client industry is required.", nameof(industries));
            if (_specialties.Length == 0) throw new ArgumentException("At least one client specialty is required.", nameof(specialties));
            IsHistoricalCompany = isHistoricalCompany;
            SourceCompanySizeTier = sourceCompanySizeTier ?? string.Empty;
            SourcePlayerReachIn2000 = sourcePlayerReachIn2000 ?? string.Empty;
            NeutralIndustryIconId = neutralIndustryIconId ?? string.Empty;
            LogoResourcePath = logoResourcePath ?? string.Empty;
        }

        public string ClientId { get; }
        public string DisplayNameKo { get; }
        public ContractClientTier Tier { get; }
        public IReadOnlyList<BusinessIndustry> Industries => _industries;
        public IReadOnlyList<ContractSpecialty> Specialties => _specialties;
        public bool IsHistoricalCompany { get; }
        public string SourceCompanySizeTier { get; }
        public string SourcePlayerReachIn2000 { get; }
        public string NeutralIndustryIconId { get; }
        public string LogoResourcePath { get; }
    }

    public sealed class LegacyContractTemplateDefinition
    {
        public LegacyContractTemplateDefinition(
            string templateId,
            int legacyGlobalIndex,
            SubcontractOffer baseline,
            ContractSpecialty specialty,
            ContractDifficulty baselineDifficulty)
        {
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Template ID is required.", nameof(templateId));
            if (legacyGlobalIndex < 0) throw new ArgumentOutOfRangeException(nameof(legacyGlobalIndex));
            TemplateId = templateId;
            LegacyGlobalIndex = legacyGlobalIndex;
            Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            Specialty = specialty;
            BaselineDifficulty = baselineDifficulty;
        }

        public string TemplateId { get; }
        public int LegacyGlobalIndex { get; }
        public SubcontractOffer Baseline { get; }
        public ContractSpecialty Specialty { get; }
        public ContractDifficulty BaselineDifficulty { get; }
    }

    public sealed class ContractOfferDefinition
    {
        private readonly string[] _prerequisiteLabels;

        public ContractOfferDefinition(
            SubcontractOffer offer,
            LegacyContractTemplateDefinition template,
            ContractClientDefinition client,
            ContractDifficulty difficulty,
            ContractSpecialty specialty,
            int qualityStandard,
            int reputationReward,
            int reputationRisk,
            ContractRiskLevel riskLevel,
            IEnumerable<string> prerequisiteLabels,
            bool onboardingRecommendation)
        {
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            if (qualityStandard < 0 || qualityStandard > 100) throw new ArgumentOutOfRangeException(nameof(qualityStandard));
            if (reputationReward < 0) throw new ArgumentOutOfRangeException(nameof(reputationReward));
            if (reputationRisk < 0) throw new ArgumentOutOfRangeException(nameof(reputationRisk));
            Difficulty = difficulty;
            Specialty = specialty;
            QualityStandard = qualityStandard;
            ReputationReward = reputationReward;
            ReputationRisk = reputationRisk;
            RiskLevel = riskLevel;
            _prerequisiteLabels = (prerequisiteLabels ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            OnboardingRecommendation = onboardingRecommendation;
        }

        public SubcontractOffer Offer { get; }
        public LegacyContractTemplateDefinition Template { get; }
        public ContractClientDefinition Client { get; }
        public ContractClientTier ClientTier => Client.Tier;
        public ContractDifficulty Difficulty { get; }
        public ContractSpecialty Specialty { get; }
        public int QualityStandard { get; }
        public int ReputationReward { get; }
        public int ReputationRisk { get; }
        public ContractRiskLevel RiskLevel { get; }
        public IReadOnlyList<string> PrerequisiteLabels => _prerequisiteLabels;
        public bool OnboardingRecommendation { get; }
    }

    public sealed class ContractPerformanceRecord
    {
        public ContractPerformanceRecord(
            string contractInstanceId,
            string clientId,
            ContractClientTier clientTier,
            BusinessIndustry industry,
            bool completed,
            bool onTime,
            int quality,
            int clientSatisfaction,
            int creditedExperienceHours,
            long acceptedMinute,
            long resolvedMinute,
            long earnedRevenueWon)
        {
            if (string.IsNullOrWhiteSpace(contractInstanceId)) throw new ArgumentException("Contract instance ID is required.", nameof(contractInstanceId));
            ContractInstanceId = contractInstanceId;
            ClientId = clientId ?? string.Empty;
            ClientTier = clientTier;
            Industry = industry;
            Completed = completed;
            OnTime = onTime;
            Quality = ClampPercent(quality);
            ClientSatisfaction = ClampPercent(clientSatisfaction);
            CreditedExperienceHours = Math.Max(0, creditedExperienceHours);
            AcceptedMinute = acceptedMinute;
            ResolvedMinute = resolvedMinute;
            EarnedRevenueWon = Math.Max(0, earnedRevenueWon);
        }

        public string ContractInstanceId { get; }
        public string ClientId { get; }
        public ContractClientTier ClientTier { get; }
        public BusinessIndustry Industry { get; }
        public bool Completed { get; }
        public bool OnTime { get; }
        public int Quality { get; }
        public int ClientSatisfaction { get; }
        public int CreditedExperienceHours { get; }
        public long AcceptedMinute { get; }
        public long ResolvedMinute { get; }
        public long EarnedRevenueWon { get; }

        private static int ClampPercent(int value) => Math.Max(0, Math.Min(100, value));
    }

    public sealed class ContractPerformanceSummary
    {
        private readonly ContractPerformanceRecord[] _records;
        private readonly IReadOnlyDictionary<BusinessIndustry, int> _domainExperienceHours;

        internal ContractPerformanceSummary(
            IEnumerable<ContractPerformanceRecord> records,
            IReadOnlyDictionary<BusinessIndustry, int> domainExperienceHours,
            int completedContracts,
            int failedContracts,
            int onTimeRateBasisPoints,
            int averageQuality,
            int averageClientSatisfaction,
            long earnedContractRevenueWon)
        {
            _records = (records ?? throw new ArgumentNullException(nameof(records)))
                .OrderBy(item => item.ResolvedMinute)
                .ThenBy(item => item.ContractInstanceId, StringComparer.Ordinal)
                .ToArray();
            _domainExperienceHours = domainExperienceHours ?? throw new ArgumentNullException(nameof(domainExperienceHours));
            CompletedContracts = completedContracts;
            FailedContracts = failedContracts;
            OnTimeRateBasisPoints = onTimeRateBasisPoints;
            AverageQuality = averageQuality;
            AverageClientSatisfaction = averageClientSatisfaction;
            EarnedContractRevenueWon = earnedContractRevenueWon;
        }

        public IReadOnlyList<ContractPerformanceRecord> Records => _records;
        public int CompletedContracts { get; }
        public int FailedContracts { get; }
        public int ResolvedContracts => CompletedContracts + FailedContracts;
        public int OnTimeRateBasisPoints { get; }
        public int AverageQuality { get; }
        public int AverageClientSatisfaction { get; }
        public long EarnedContractRevenueWon { get; }

        public int DomainExperienceHours(BusinessIndustry industry)
        {
            return _domainExperienceHours.TryGetValue(industry, out var hours) ? hours : 0;
        }
    }

    public sealed class ContractCompanyProfile
    {
        public ContractCompanyProfile(
            ContractCompanyGrade grade,
            int capacityScore,
            int reputation,
            long cashWon,
            int teamMemberCount,
            int researchedTechnologyCount,
            int ownedBusinessCount)
        {
            Grade = grade;
            CapacityScore = Math.Max(0, Math.Min(100, capacityScore));
            Reputation = Math.Max(0, Math.Min(100, reputation));
            CashWon = Math.Max(0, cashWon);
            TeamMemberCount = Math.Max(0, teamMemberCount);
            ResearchedTechnologyCount = Math.Max(0, researchedTechnologyCount);
            OwnedBusinessCount = Math.Max(0, ownedBusinessCount);
        }

        public ContractCompanyGrade Grade { get; }
        public int CapacityScore { get; }
        public int Reputation { get; }
        public long CashWon { get; }
        public int TeamMemberCount { get; }
        public int ResearchedTechnologyCount { get; }
        public int OwnedBusinessCount { get; }
    }

    public sealed class ContractTierRequirement
    {
        public ContractTierRequirement(
            ContractClientTier tier,
            int completedContracts,
            int onTimeRateBasisPoints,
            int averageQuality,
            int averageClientSatisfaction,
            int reputation,
            int relevantDomainExperienceHours,
            ContractCompanyGrade companyGrade,
            int capacityScore)
        {
            Tier = tier;
            CompletedContracts = completedContracts;
            OnTimeRateBasisPoints = onTimeRateBasisPoints;
            AverageQuality = averageQuality;
            AverageClientSatisfaction = averageClientSatisfaction;
            Reputation = reputation;
            RelevantDomainExperienceHours = relevantDomainExperienceHours;
            CompanyGrade = companyGrade;
            CapacityScore = capacityScore;
        }

        public ContractClientTier Tier { get; }
        public int CompletedContracts { get; }
        public int OnTimeRateBasisPoints { get; }
        public int AverageQuality { get; }
        public int AverageClientSatisfaction { get; }
        public int Reputation { get; }
        public int RelevantDomainExperienceHours { get; }
        public ContractCompanyGrade CompanyGrade { get; }
        public int CapacityScore { get; }
    }

    public sealed class ContractTierProgress
    {
        private readonly string[] _conditionLabels;

        public ContractTierProgress(
            ContractTierRequirement requirement,
            bool unlocked,
            int progressBasisPoints,
            IEnumerable<string> conditionLabels)
        {
            Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
            Unlocked = unlocked;
            ProgressBasisPoints = Math.Max(0, Math.Min(10_000, progressBasisPoints));
            _conditionLabels = (conditionLabels ?? Array.Empty<string>()).ToArray();
        }

        public ContractTierRequirement Requirement { get; }
        public ContractClientTier Tier => Requirement.Tier;
        public bool Unlocked { get; }
        public int ProgressBasisPoints { get; }
        public IReadOnlyList<string> ConditionLabels => _conditionLabels;
    }

    public sealed class ContractOfferBoardSnapshot
    {
        private readonly ContractOfferDefinition[] _offers;
        private readonly ContractTierProgress[] _tierProgress;

        public ContractOfferBoardSnapshot(
            long dayIndex,
            BusinessIndustry industry,
            bool firstContractRecommendation,
            IEnumerable<ContractOfferDefinition> offers,
            IEnumerable<ContractTierProgress> tierProgress)
        {
            DayIndex = dayIndex;
            Industry = industry;
            FirstContractRecommendation = firstContractRecommendation;
            _offers = (offers ?? throw new ArgumentNullException(nameof(offers))).ToArray();
            _tierProgress = (tierProgress ?? throw new ArgumentNullException(nameof(tierProgress))).OrderBy(item => item.Tier).ToArray();
            if (_offers.Select(item => item.Offer.OfferId).Distinct(StringComparer.Ordinal).Count() != _offers.Length)
                throw new InvalidOperationException("Offer board IDs must be unique.");
        }

        public long DayIndex { get; }
        public BusinessIndustry Industry { get; }
        public bool FirstContractRecommendation { get; }
        public IReadOnlyList<ContractOfferDefinition> Offers => _offers;
        public IReadOnlyList<ContractTierProgress> TierProgress => _tierProgress;
    }

    /// <summary>A technology level an own-product path needs before it can be started.</summary>
    public readonly struct ProductTechnologyRequirement
    {
        public ProductTechnologyRequirement(string technologyId, int requiredLevel)
        {
            if (!CompanyTechnologyCatalog.Exists(technologyId))
                throw new ArgumentException($"Unknown company technology: {technologyId}", nameof(technologyId));
            if (requiredLevel < 1 || requiredLevel > CompanyTechnologyCatalog.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(requiredLevel));
            TechnologyId = technologyId;
            RequiredLevel = requiredLevel;
        }

        public string TechnologyId { get; }
        public int RequiredLevel { get; }

        public string DisplayKo =>
            $"{CompanyTechnologyCatalog.Get(TechnologyId).DisplayNameKo} Lv{RequiredLevel}";
    }

    public sealed class ProductOpportunityDefinition
    {
        private readonly string[] _requiredTechnologyIds;
        private readonly ProductTechnologyRequirement[] _requiredTechnologyLevels;

        public ProductOpportunityDefinition(
            string productPathId,
            BusinessIndustry industry,
            string displayNameKo,
            long requiredCashWon,
            int requiredCompletedContracts,
            int requiredDomainExperienceHours,
            int requiredReputation,
            IEnumerable<string> requiredTechnologyIds,
            ContractRiskLevel riskLevel,
            string revenueModelKo,
            IEnumerable<ProductTechnologyRequirement> requiredTechnologyLevels = null)
        {
            _requiredTechnologyLevels = (requiredTechnologyLevels ?? Array.Empty<ProductTechnologyRequirement>())
                .ToArray();
            ProductPathId = productPathId ?? string.Empty;
            Industry = industry;
            DisplayNameKo = displayNameKo ?? string.Empty;
            RequiredCashWon = requiredCashWon;
            RequiredCompletedContracts = requiredCompletedContracts;
            RequiredDomainExperienceHours = requiredDomainExperienceHours;
            RequiredReputation = requiredReputation;
            _requiredTechnologyIds = (requiredTechnologyIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            RiskLevel = riskLevel;
            RevenueModelKo = revenueModelKo ?? string.Empty;
        }

        public string ProductPathId { get; }
        public BusinessIndustry Industry { get; }
        public string DisplayNameKo { get; }
        public long RequiredCashWon { get; }
        public int RequiredCompletedContracts { get; }
        public int RequiredDomainExperienceHours { get; }
        public int RequiredReputation { get; }
        public IReadOnlyList<string> RequiredTechnologyIds => _requiredTechnologyIds;

        /// <summary>Levels the company has to reach by doing subcontract work, not by paying.</summary>
        public IReadOnlyList<ProductTechnologyRequirement> RequiredTechnologyLevels => _requiredTechnologyLevels;

        public ContractRiskLevel RiskLevel { get; }
        public string RevenueModelKo { get; }
    }

    public sealed class ProductOpportunityProgress
    {
        private readonly string[] _conditionLabels;

        public ProductOpportunityProgress(
            ProductOpportunityDefinition definition,
            bool unlocked,
            int progressBasisPoints,
            IEnumerable<string> conditionLabels,
            bool ownedBusinessExists,
            bool productSystemReady)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Unlocked = unlocked;
            ProgressBasisPoints = Math.Max(0, Math.Min(10_000, progressBasisPoints));
            _conditionLabels = (conditionLabels ?? Array.Empty<string>()).ToArray();
            OwnedBusinessExists = ownedBusinessExists;
            ProductSystemReady = productSystemReady;
        }

        public ProductOpportunityDefinition Definition { get; }
        public bool Unlocked { get; }
        public int ProgressBasisPoints { get; }
        public IReadOnlyList<string> ConditionLabels => _conditionLabels;
        public bool OwnedBusinessExists { get; }
        public bool ProductSystemReady { get; }
    }
}

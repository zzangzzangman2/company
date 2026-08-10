using System;
using System.Collections.Generic;

namespace FamilyCompany.Save
{
    [Serializable]
    public sealed class GameSaveDto
    {
        public int schemaVersion = 4;
        public int worldSeed;
        public long elapsedMinutes;
        public CompanySaveDto company = new CompanySaveDto();
        public List<FamilyMemberSaveDto> family = new List<FamilyMemberSaveDto>();
        public List<ScheduledEventSaveDto> events = new List<ScheduledEventSaveDto>();
        public List<LedgerTransactionSaveDto> ledger = new List<LedgerTransactionSaveDto>();
        public List<SubcontractSaveDto> contracts = new List<SubcontractSaveDto>();
        public CompanyGrowthSaveDto growth = new CompanyGrowthSaveDto();
    }

    [Serializable]
    public sealed class CompanySaveDto
    {
        public string companyName = string.Empty;
        public long cashWon;
        public int reputation;
    }

    [Serializable]
    public sealed class FamilyMemberSaveDto
    {
        public string memberId = string.Empty;
        public string displayName = string.Empty;
        public int role;
        public int birthYear;
        public int birthMonth;
        public int birthDay;
        public string companyDuty = string.Empty;
        public int energy;
        public int trust;
        public int stress;
        public int development;
        public int speed;
        public int stamina;
        public int planning;
        public int art;
        public int sales;
        public int mental;
        public int teamwork;
        public int loyalty;
        public int potential;
        public List<CareerMemorySaveDto> careerMemories = new List<CareerMemorySaveDto>();
    }

    [Serializable]
    public sealed class CareerMemorySaveDto
    {
        public string memoryId = string.Empty;
        public int industry;
        public int kind;
        public string summary = string.Empty;
        public long occurredMinute;
        public int bondDelta;
        public List<string> colleagueMemberIds = new List<string>();
    }

    [Serializable]
    public sealed class ScheduledEventSaveDto
    {
        public string eventId = string.Empty;
        public long dueMinute;
        public int priority;
        public string kind = string.Empty;
        public string payload = string.Empty;
    }

    [Serializable]
    public sealed class LedgerTransactionSaveDto
    {
        public string transactionId = string.Empty;
        public long elapsedMinute;
        public string memo = string.Empty;
        public List<LedgerLineSaveDto> lines = new List<LedgerLineSaveDto>();
    }

    [Serializable]
    public sealed class LedgerLineSaveDto
    {
        public string accountCode = string.Empty;
        public long debitWon;
        public long creditWon;
    }

    [Serializable]
    public sealed class SubcontractSaveDto
    {
        public string offerId = string.Empty;
        public string clientCompanyId = string.Empty;
        public string exactClientDisplayName = string.Empty;
        public int serviceType;
        public string title = string.Empty;
        public int requiredWorkers;
        public int estimatedPersonHours;
        public int deadlineDays;
        public long upfrontCostWon;
        public long rewardWon;
        public int reputationRequired;
        public long penaltyWon;
        public int requiredDevelopment;
        public int requiredSpeed;
        public string requiredTechnologyId = string.Empty;
        public int industry;
        public long acceptedMinute;
        public int status;
        public int completedPersonHours;
        public long resolvedMinute = -1;
        public List<ContractWorkerContributionSaveDto> contributions = new List<ContractWorkerContributionSaveDto>();
    }

    [Serializable]
    public sealed class ContractWorkerContributionSaveDto
    {
        public string memberId = string.Empty;
        public int personHours;
    }

    [Serializable]
    public sealed class CompanyGrowthSaveDto
    {
        public bool researchCenterUnlocked;
        public List<string> researchedTechnologyIds = new List<string>();
        public int marketReportSequence;
        public int productSequence;
        public bool hasMarketReport;
        public bool hasProductProject;
        public MarketReportSaveDto marketReport;
        public ProductProjectSaveDto productProject;
        public List<OwnedBusinessSaveDto> ownedBusinesses = new List<OwnedBusinessSaveDto>();
    }

    [Serializable]
    public sealed class MarketReportSaveDto
    {
        public string genre = string.Empty;
        public string desiredFeature = string.Empty;
        public int demand;
        public long purchasedMinute;
        public int industry;
    }

    [Serializable]
    public sealed class ProductProjectSaveDto
    {
        public int sequence;
        public string title = string.Empty;
        public string targetGenre = string.Empty;
        public string targetFeature = string.Empty;
        public long budgetWon;
        public long startedMinute;
        public long dueMinute;
        public bool resolved;
        public int quality;
        public long revenueWon;
        public int industry;
    }

    [Serializable]
    public sealed class OwnedBusinessSaveDto
    {
        public int industry;
        public string businessName = string.Empty;
        public long foundedMinute;
        public long foundingInvestmentWon;
        public long totalRevenueWon;
        public int launchedProductCount;
    }
}

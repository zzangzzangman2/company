using System;
using System.Collections.Generic;
using FamilyCompany.Save.OfficeGrid;
using FamilyCompany.Save.OfficeFurniture;
using FamilyCompany.Simulation.Stamina;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Save
{
    [Serializable]
    public sealed class GameSaveDto
    {
        public int schemaVersion = 12;
        public int worldSeed;
        public long elapsedMinutes;
        public CompanySaveDto company = new CompanySaveDto();
        public List<FamilyMemberSaveDto> family = new List<FamilyMemberSaveDto>();
        public List<ScheduledEventSaveDto> events = new List<ScheduledEventSaveDto>();
        public List<LedgerTransactionSaveDto> ledger = new List<LedgerTransactionSaveDto>();
        public List<SubcontractSaveDto> contracts = new List<SubcontractSaveDto>();
        public CompanyGrowthSaveDto growth = new CompanyGrowthSaveDto();
        public StockMarketSessionSaveDto stockMarket = new StockMarketSessionSaveDto();
        public OfficeGridSaveDto officeGrid;
        public OfficeFurnitureInventorySaveDto officeFurnitureInventory;
        public CharacterStaminaRosterSnapshotDto staminaState;
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
        public WorkforceCapabilitySnapshotDto capability;
        public List<CareerMemorySaveDto> careerMemories = new List<CareerMemorySaveDto>();
        public OfficeAutonomySaveDto autonomy = new OfficeAutonomySaveDto();
    }

    [Serializable]
    public sealed class OfficeAutonomySaveDto
    {
        public int currentAction;
        public int targetLocation;
        public long actionStartedMinute;
        public long actionEndsMinute;
        public long lastProcessedMinute;
        public int completedWorkBlocks;
        public int completedBreaks;
        public int burnoutCount;
        public string lastIncidentSummary = string.Empty;
        public long lastIncidentMinute = -1;
        public long lastSocialEventDay = -1;
        public OfficeMicroActionSaveDto microAction = new OfficeMicroActionSaveDto();
    }

    [Serializable]
    public sealed class OfficeMicroActionSaveDto
    {
        public int action;
        public string targetId = string.Empty;
        public int targetLocation;
        public long startedMinute;
        public long endsMinute;
        public int sequenceIndex;
        public string partnerMemberId = string.Empty;
        public long macroActionStartedMinute = -1;
        public int lastAction;
        public string lastTargetId = string.Empty;
        public long lastTargetEndedMinute = -100000;
        public long lastWaterStartedMinute = -100000;
        public long lastCoffeeStartedMinute = -100000;
        public long lastConversationStartedMinute = -100000;
        public string lastConversationPartnerId = string.Empty;
        public long deskResidenceStartedMinute = -1;
        public int visitedLocationMask;
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
        public int workPurpose;
        public long workDueMinute = -1;
        public int workRateBasisPoints = 10000;
        public int qualityBonus;
        public int resolvedQuality = -1;
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
        public int requiredCapability;
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

        /// <summary>Schema v11: technology points earned by finishing subcontracts.</summary>
        public List<TechnologyPointsSaveDto> technologyPoints = new List<TechnologyPointsSaveDto>();
        public StarterProductSaveDto starterProduct = new StarterProductSaveDto();
    }

    [Serializable]
    public sealed class StarterProductSaveDto
    {
        public int phase;
        public int developmentAttempt;
        public string developmentOrderId = string.Empty;
        public int quality;
        public int customers;
        public int satisfaction = 50;
        public long nextBillingMinute = -1;
        public int billingPeriod;
        public string maintenanceOrderId = string.Empty;
        public long totalRevenueWon;
        public long lastPeriodRevenueWon;
        public int missedPeriods;
    }

    [Serializable]
    public sealed class TechnologyPointsSaveDto
    {
        public string technologyId = string.Empty;
        public int points;
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

    [Serializable]
    public sealed class StockMarketSessionSaveDto
    {
        public int schemaVersion = 1;
        public bool initialized;
        public long dateTicks;
        public int marketMinute;
        public double realtimeResidualSeconds;
        public int playbackIndex = 1;
        public bool openingAuctionProcessed;
        public int openingAuctionProcessCount;
        public int canonicalMinuteUpdateCount;
        public int liquidityPulse;
        public long brokerageCashWon;
        public int orderSequence;
        public int journalSequence;
        public List<BrokeragePositionSaveDto> positions = new List<BrokeragePositionSaveDto>();
        public List<BrokeragePendingOrderSaveDto> pendingOrders = new List<BrokeragePendingOrderSaveDto>();
        public List<BrokerageTradeSaveDto> playerTrades = new List<BrokerageTradeSaveDto>();
        public List<BrokerageOrderJournalSaveDto> orderJournal = new List<BrokerageOrderJournalSaveDto>();
        public List<string> favoriteAssetIds = new List<string>();
    }

    [Serializable]
    public sealed class BrokeragePositionSaveDto
    {
        public string assetId = string.Empty;
        public int units;
        public double averageCostWon;
    }

    [Serializable]
    public sealed class BrokeragePendingOrderSaveDto
    {
        public string id = string.Empty;
        public int side;
        public string assetId = string.Empty;
        public long limitPrice;
        public double originalQuantity;
        public double remainingQuantity;
        public long placedDateTicks;
        public int placedMinute;
        public int placedSequence;
        public double queueAheadQuantity;
        public bool hasMaximumPositionUnits;
        public int maximumPositionUnits;
        public bool isIpoFirstTradingDay;
    }

    [Serializable]
    public sealed class BrokerageTradeSaveDto
    {
        public string assetId = string.Empty;
        public int marketMinute;
        public int liquidityPulse;
        public long price;
        public int quantity;
        public bool isBuy;
    }

    [Serializable]
    public sealed class BrokerageOrderJournalSaveDto
    {
        public int sequence;
        public string assetId = string.Empty;
        public int marketMinute;
        public bool isBuy;
        public bool isMarket;
        public long limitPrice;
        public int requestedQuantity;
        public int filledQuantity;
        public int remainingQuantity;
        public double averagePrice;
    }
}

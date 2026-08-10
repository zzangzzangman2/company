using System;
using System.Collections.Generic;

namespace FamilyCompany.Save
{
    [Serializable]
    public sealed class GameSaveDto
    {
        public int schemaVersion = 2;
        public int worldSeed;
        public long elapsedMinutes;
        public CompanySaveDto company = new CompanySaveDto();
        public List<FamilyMemberSaveDto> family = new List<FamilyMemberSaveDto>();
        public List<ScheduledEventSaveDto> events = new List<ScheduledEventSaveDto>();
        public List<LedgerTransactionSaveDto> ledger = new List<LedgerTransactionSaveDto>();
        public List<SubcontractSaveDto> contracts = new List<SubcontractSaveDto>();
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
}

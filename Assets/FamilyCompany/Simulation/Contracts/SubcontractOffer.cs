using System;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Contracts
{
    public enum CompanyWorkPurpose { Subcontract = 0, StarterDevelopment = 1, StarterMaintenance = 2 }

    public sealed class SubcontractOffer
    {
        public SubcontractOffer(
            string offerId,
            string clientCompanyId,
            string exactClientDisplayName,
            ContractServiceType serviceType,
            string title,
            int requiredWorkers,
            int estimatedPersonHours,
            int deadlineDays,
            long upfrontCostWon,
            long rewardWon,
            int reputationRequired,
            long penaltyWon = 0,
            int requiredDevelopment = 0,
            int requiredSpeed = 0,
            string requiredTechnologyId = "",
            BusinessIndustry industry = BusinessIndustry.WebAndSoftware,
            int requiredCapability = -1,
            CompanyWorkPurpose purpose = CompanyWorkPurpose.Subcontract)
        {
            OfferId = RequireText(offerId, nameof(offerId));
            ClientCompanyId = RequireText(clientCompanyId, nameof(clientCompanyId));
            ExactClientDisplayName = RequireText(exactClientDisplayName, nameof(exactClientDisplayName));
            Title = RequireText(title, nameof(title));

            if (!Enum.IsDefined(typeof(ContractServiceType), serviceType))
            {
                throw new ArgumentOutOfRangeException(nameof(serviceType));
            }

            if (requiredWorkers <= 0) throw new ArgumentOutOfRangeException(nameof(requiredWorkers));
            if (estimatedPersonHours <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedPersonHours));
            if (deadlineDays <= 0) throw new ArgumentOutOfRangeException(nameof(deadlineDays));
            if (upfrontCostWon < 0) throw new ArgumentOutOfRangeException(nameof(upfrontCostWon));
            if (!Enum.IsDefined(typeof(CompanyWorkPurpose), purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
            if (purpose == CompanyWorkPurpose.Subcontract ? rewardWon <= upfrontCostWon : rewardWon != 0 || upfrontCostWon != 0)
                throw new ArgumentOutOfRangeException(nameof(rewardWon));
            if (penaltyWon < 0) throw new ArgumentOutOfRangeException(nameof(penaltyWon));
            if (requiredDevelopment < 0 || requiredDevelopment > 100) throw new ArgumentOutOfRangeException(nameof(requiredDevelopment));
            if (requiredSpeed < 0 || requiredSpeed > 100) throw new ArgumentOutOfRangeException(nameof(requiredSpeed));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            if (reputationRequired < 0 || reputationRequired > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(reputationRequired));
            }

            ServiceType = serviceType;
            RequiredWorkers = requiredWorkers;
            EstimatedPersonHours = estimatedPersonHours;
            DeadlineDays = deadlineDays;
            UpfrontCostWon = upfrontCostWon;
            RewardWon = rewardWon;
            ReputationRequired = reputationRequired;
            PenaltyWon = penaltyWon;
            RequiredDevelopment = requiredDevelopment;
            RequiredSpeed = requiredSpeed;
            RequiredCapability = requiredCapability < 0 ? requiredDevelopment : requiredCapability;
            if (RequiredCapability < 0 || RequiredCapability > 100) throw new ArgumentOutOfRangeException(nameof(requiredCapability));
            RequiredTechnologyId = requiredTechnologyId ?? string.Empty;
            Industry = industry;
            Purpose = purpose;
        }

        public string OfferId { get; }
        public string ClientCompanyId { get; }

        // 개발판 UI는 가명을 만들지 않고 이 날짜의 실제 회사명을 그대로 표시한다.
        public string ExactClientDisplayName { get; }

        public ContractServiceType ServiceType { get; }
        public string Title { get; }
        public int RequiredWorkers { get; }
        public int EstimatedPersonHours { get; }
        public int DeadlineDays { get; }
        public long UpfrontCostWon { get; }
        public long RewardWon { get; }
        public int ReputationRequired { get; }
        public long PenaltyWon { get; }
        public int RequiredDevelopment { get; }
        public int RequiredSpeed { get; }
        public int RequiredCapability { get; }
        public string RequiredTechnologyId { get; }
        public BusinessIndustry Industry { get; }
        public CompanyWorkPurpose Purpose { get; }
        public bool IsExternal => Purpose == CompanyWorkPurpose.Subcontract;

        // Retrying creates a new identity, never resets a settled contract. Keep legacy
        // compatibility fields inside the offer boundary rather than business logic.
        internal SubcontractOffer WithOfferId(string id) => new SubcontractOffer(id,
            ClientCompanyId, ExactClientDisplayName, ServiceType, Title, RequiredWorkers,
            EstimatedPersonHours, DeadlineDays, UpfrontCostWon, RewardWon, ReputationRequired,
            PenaltyWon, RequiredDevelopment, RequiredSpeed, RequiredTechnologyId, Industry,
            RequiredCapability, Purpose);

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }
}

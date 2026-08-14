using System;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Contracts
{
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
            int requiredCapability = -1)
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
            if (rewardWon <= upfrontCostWon) throw new ArgumentOutOfRangeException(nameof(rewardWon));
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

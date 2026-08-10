using System;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Contracts
{
    public static class BootstrapContractCatalog
    {
        private sealed class Template
        {
            public Template(
                ContractServiceType serviceType,
                string title,
                int requiredWorkers,
                int estimatedPersonHours,
                int deadlineDays,
                long upfrontCostWon,
                long rewardWon,
                int reputationRequired)
            {
                ServiceType = serviceType;
                Title = title;
                RequiredWorkers = requiredWorkers;
                EstimatedPersonHours = estimatedPersonHours;
                DeadlineDays = deadlineDays;
                UpfrontCostWon = upfrontCostWon;
                RewardWon = rewardWon;
                ReputationRequired = reputationRequired;
            }

            public ContractServiceType ServiceType { get; }
            public string Title { get; }
            public int RequiredWorkers { get; }
            public int EstimatedPersonHours { get; }
            public int DeadlineDays { get; }
            public long UpfrontCostWon { get; }
            public long RewardWon { get; }
            public int ReputationRequired { get; }
        }

        private static readonly Template[] Templates =
        {
            new Template(ContractServiceType.WebsiteMaintenance, "홈페이지 문구·상품 사진 갱신", 1, 12, 5, 50_000, 450_000, 0),
            new Template(ContractServiceType.DataEntryAndQualityAssurance, "상품 목록 전산 입력과 오류 검사", 2, 28, 7, 80_000, 850_000, 0),
            new Template(ContractServiceType.OfficePcAndNetworkSupport, "소규모 사무실 PC·네트워크 점검", 2, 24, 5, 200_000, 900_000, 0),
            new Template(ContractServiceType.SmallBusinessTool, "소형 매출 집계 프로그램 제작", 3, 56, 14, 300_000, 1_800_000, 0),
            new Template(ContractServiceType.LocalizationAndWebQualityAssurance, "웹페이지 한글화와 동작 검사", 2, 36, 10, 100_000, 1_100_000, 0)
        };

        public static SubcontractOffer CreateOffer(
            int worldSeed,
            string clientCompanyId,
            string exactClientDisplayName,
            long offerSequence)
        {
            if (offerSequence < 0) throw new ArgumentOutOfRangeException(nameof(offerSequence));
            if (string.IsNullOrWhiteSpace(clientCompanyId)) throw new ArgumentException("Client company ID is required.", nameof(clientCompanyId));

            var templateIndex = StableRandom.StableRandomInt(
                $"bootstrap-contract:{worldSeed}:{clientCompanyId}:{offerSequence}",
                Templates.Length);
            var template = Templates[templateIndex];
            return new SubcontractOffer(
                $"subcontract:{clientCompanyId}:{offerSequence:D6}",
                clientCompanyId,
                exactClientDisplayName,
                template.ServiceType,
                template.Title,
                template.RequiredWorkers,
                template.EstimatedPersonHours,
                template.DeadlineDays,
                template.UpfrontCostWon,
                template.RewardWon,
                template.ReputationRequired);
        }
    }
}

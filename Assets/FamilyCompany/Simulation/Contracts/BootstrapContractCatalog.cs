using System;
using System.Linq;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Contracts
{
    public static class BootstrapContractCatalog
    {
        private sealed class Template
        {
            public Template(
                BusinessIndustry industry,
                ContractServiceType serviceType,
                string title,
                int requiredWorkers,
                int estimatedPersonHours,
                int deadlineDays,
                long upfrontCostWon,
                long rewardWon,
                int reputationRequired,
                long penaltyWon,
                int requiredDevelopment,
                int requiredSpeed,
                string requiredTechnologyId = "")
            {
                Industry = industry;
                ServiceType = serviceType;
                Title = title;
                RequiredWorkers = requiredWorkers;
                EstimatedPersonHours = estimatedPersonHours;
                DeadlineDays = deadlineDays;
                UpfrontCostWon = upfrontCostWon;
                RewardWon = rewardWon;
                ReputationRequired = reputationRequired;
                PenaltyWon = penaltyWon;
                RequiredDevelopment = requiredDevelopment;
                RequiredSpeed = requiredSpeed;
                RequiredTechnologyId = requiredTechnologyId ?? string.Empty;
            }

            public BusinessIndustry Industry { get; }
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
            public string RequiredTechnologyId { get; }
        }

        private static readonly Template[] Templates =
        {
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.WebsiteMaintenance, "미니홈피용 64×64 아바타 도트", 1, 14, 5, 0, 360_000, 0, 0, 20, 25),
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.DataEntryAndQualityAssurance, "이모티콘 인터넷 소설 타이핑", 1, 18, 6, 0, 420_000, 0, 0, 18, 35),
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.DataEntryAndQualityAssurance, "타자 연습 프로그램 단어 DB 입력", 2, 26, 7, 40_000, 720_000, 0, 0, 30, 38),
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.WebsiteMaintenance, "엽기 플래시 애니메이션 프레임 채색", 2, 36, 7, 80_000, 1_100_000, 2, 200_000, 38, 42),
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.OfficePcAndNetworkSupport, "초창기 P2P 서버 야간 모니터링", 3, 60, 10, 250_000, 2_100_000, 8, 600_000, 52, 48, ResearchTechnologyIds.AutomationLine),
            new Template(BusinessIndustry.WebAndSoftware, ContractServiceType.WebsiteMaintenance, "쇼핑몰용 3D 회전 상품 이미지", 2, 48, 9, 220_000, 1_700_000, 6, 450_000, 50, 43, ResearchTechnologyIds.ThreeDModeling),

            new Template(BusinessIndustry.FeaturePhoneAndMobile, ContractServiceType.DataEntryAndQualityAssurance, "64화음 벨소리 MIDI 입력", 1, 12, 4, 0, 350_000, 0, 0, 22, 30),
            new Template(BusinessIndustry.FeaturePhoneAndMobile, ContractServiceType.WebsiteMaintenance, "문자 메시지 도트 배경화면", 1, 16, 5, 0, 430_000, 0, 0, 28, 32),
            new Template(BusinessIndustry.FeaturePhoneAndMobile, ContractServiceType.DataEntryAndQualityAssurance, "피처폰 천지인 키패드 고무판 조립", 2, 28, 6, 50_000, 760_000, 0, 0, 30, 40),
            new Template(BusinessIndustry.FeaturePhoneAndMobile, ContractServiceType.LocalizationAndWebQualityAssurance, "컬러폰 메뉴 아이콘 도트 변환", 2, 32, 7, 80_000, 980_000, 2, 120_000, 38, 43),
            new Template(BusinessIndustry.FeaturePhoneAndMobile, ContractServiceType.SmallBusinessTool, "모바일 맞고·타이쿤 다기종 포팅", 3, 64, 10, 300_000, 2_300_000, 8, 700_000, 55, 52, ResearchTechnologyIds.AutomationLine),

            new Template(BusinessIndustry.HardwareAndPc, ContractServiceType.DataEntryAndQualityAssurance, "교육용 CD·디스켓 복제와 포장", 1, 16, 5, 0, 330_000, 0, 0, 20, 30),
            new Template(BusinessIndustry.HardwareAndPc, ContractServiceType.OfficePcAndNetworkSupport, "PC방 재떨이·컵라면 정기 납품", 2, 22, 6, 0, 560_000, 0, 0, 24, 36),
            new Template(BusinessIndustry.HardwareAndPc, ContractServiceType.SmallBusinessTool, "게임 방송 이벤트용 RTS 유즈맵 제작", 2, 34, 8, 100_000, 1_050_000, 2, 0, 40, 38),
            new Template(BusinessIndustry.HardwareAndPc, ContractServiceType.OfficePcAndNetworkSupport, "조립 PC 견적·24시간 번인 테스트", 2, 42, 9, 160_000, 1_350_000, 4, 250_000, 44, 42),
            new Template(BusinessIndustry.HardwareAndPc, ContractServiceType.DataEntryAndQualityAssurance, "MP3 플레이어 메인보드 조립·용량 검수", 3, 70, 12, 450_000, 2_450_000, 10, 900_000, 50, 48, ResearchTechnologyIds.AutomationLine),

            new Template(BusinessIndustry.FashionRetailAndOffline, ContractServiceType.DataEntryAndQualityAssurance, "오버핏 의류 브랜드 로고 자수", 2, 24, 6, 0, 580_000, 0, 0, 24, 35),
            new Template(BusinessIndustry.FashionRetailAndOffline, ContractServiceType.WebsiteMaintenance, "야식·휴대폰 전단지와 스티커 제작", 1, 18, 5, 0, 420_000, 0, 0, 25, 34),
            new Template(BusinessIndustry.FashionRetailAndOffline, ContractServiceType.SmallBusinessTool, "비디오·만화책 대여점 연체 관리", 2, 36, 9, 100_000, 1_100_000, 2, 0, 38, 36),
            new Template(BusinessIndustry.FashionRetailAndOffline, ContractServiceType.DataEntryAndQualityAssurance, "동대문 도매 의류 재고 전산화", 2, 44, 9, 180_000, 1_400_000, 4, 300_000, 42, 45),
            new Template(BusinessIndustry.FashionRetailAndOffline, ContractServiceType.WebsiteMaintenance, "초기 인터넷 쇼핑몰 상품 촬영·등록", 3, 60, 12, 300_000, 2_200_000, 8, 600_000, 48, 45, ResearchTechnologyIds.MarketAnalysis)
        };

        public static int TotalOfferTemplateCount => Templates.Length;

        public static int OfferCountForIndustry(BusinessIndustry industry)
        {
            return Templates.Count(item => item.Industry == industry);
        }

        public static SubcontractOffer CreateOffer(
            int worldSeed,
            string clientCompanyId,
            string exactClientDisplayName,
            long offerSequence)
        {
            if (offerSequence < 0) throw new ArgumentOutOfRangeException(nameof(offerSequence));
            if (string.IsNullOrWhiteSpace(clientCompanyId)) throw new ArgumentException("Client company ID is required.", nameof(clientCompanyId));

            var template = Templates[(int)(offerSequence % Templates.Length)];
            return BuildOffer(clientCompanyId, exactClientDisplayName, offerSequence, template, string.Empty);
        }

        public static SubcontractOffer CreateIndustryOffer(
            int worldSeed,
            string clientCompanyId,
            string exactClientDisplayName,
            BusinessIndustry industry,
            long offerSequence)
        {
            if (offerSequence < 0) throw new ArgumentOutOfRangeException(nameof(offerSequence));
            if (string.IsNullOrWhiteSpace(clientCompanyId)) throw new ArgumentException("Client company ID is required.", nameof(clientCompanyId));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            var industryTemplates = Templates.Where(item => item.Industry == industry).ToArray();
            var template = industryTemplates[(int)(offerSequence % industryTemplates.Length)];
            return BuildOffer(clientCompanyId, exactClientDisplayName, offerSequence, template, $":{(int)industry}");
        }

        private static SubcontractOffer BuildOffer(
            string clientCompanyId,
            string exactClientDisplayName,
            long offerSequence,
            Template template,
            string industryOfferIdSuffix)
        {
            return new SubcontractOffer(
                $"subcontract:{clientCompanyId}{industryOfferIdSuffix}:{offerSequence:D6}",
                clientCompanyId,
                exactClientDisplayName,
                template.ServiceType,
                template.Title,
                template.RequiredWorkers,
                template.EstimatedPersonHours,
                template.DeadlineDays,
                template.UpfrontCostWon,
                template.RewardWon,
                template.ReputationRequired,
                template.PenaltyWon,
                template.RequiredDevelopment,
                template.RequiredSpeed,
                template.RequiredTechnologyId,
                template.Industry);
        }
    }
}

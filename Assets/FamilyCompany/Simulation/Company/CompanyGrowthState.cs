using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Technology;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.Company
{
    public static class ResearchTechnologyIds
    {
        public const string ThreeDModeling = "three_d_modeling";
        public const string AutomationLine = "automation_line";
        public const string MarketAnalysis = "market_analysis";
    }

    public sealed class ResearchTechnologyDefinition
    {
        public ResearchTechnologyDefinition(string technologyId, string displayName, string description, long costWon, string prerequisiteId = "")
        {
            TechnologyId = technologyId ?? throw new ArgumentNullException(nameof(technologyId));
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            if (costWon <= 0) throw new ArgumentOutOfRangeException(nameof(costWon));
            CostWon = costWon;
            PrerequisiteId = prerequisiteId ?? string.Empty;
        }

        public string TechnologyId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public long CostWon { get; }
        public string PrerequisiteId { get; }
    }

    public static class ResearchTechnologyCatalog
    {
        private static readonly ResearchTechnologyDefinition[] Definitions =
        {
            new ResearchTechnologyDefinition(
                ResearchTechnologyIds.ThreeDModeling,
                "3D 모델링 숙련",
                "3D 에셋·시각화 하청과 제품 외형 설계를 해금합니다.",
                700_000),
            new ResearchTechnologyDefinition(
                ResearchTechnologyIds.AutomationLine,
                "자동화 라인 구축",
                "반복 작업을 자동화해 고단가 시스템 하청을 받을 수 있습니다.",
                1_200_000),
            new ResearchTechnologyDefinition(
                ResearchTechnologyIds.MarketAnalysis,
                "소비자 시장 분석",
                "시장 보고서를 구매하고 자체 제품 프로젝트를 기획할 수 있습니다.",
                900_000,
                ResearchTechnologyIds.AutomationLine)
        };

        public static IReadOnlyList<ResearchTechnologyDefinition> All => Definitions;

        public static ResearchTechnologyDefinition Get(string technologyId)
        {
            var definition = Definitions.FirstOrDefault(item => item.TechnologyId == technologyId);
            if (definition == null) throw new KeyNotFoundException($"Unknown technology: {technologyId}");
            return definition;
        }
    }

    public sealed class MarketReportState
    {
        public MarketReportState(
            string genre,
            string desiredFeature,
            int demand,
            long purchasedMinute,
            BusinessIndustry industry = BusinessIndustry.WebAndSoftware)
        {
            Genre = genre ?? string.Empty;
            DesiredFeature = desiredFeature ?? string.Empty;
            Demand = Math.Max(0, Math.Min(100, demand));
            if (purchasedMinute < 0) throw new ArgumentOutOfRangeException(nameof(purchasedMinute));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            PurchasedMinute = purchasedMinute;
            Industry = industry;
        }

        public string Genre { get; }
        public string DesiredFeature { get; }
        public int Demand { get; }
        public long PurchasedMinute { get; }
        public BusinessIndustry Industry { get; }
    }

    public sealed class ProductProjectState
    {
        public ProductProjectState(
            int sequence,
            string title,
            string targetGenre,
            string targetFeature,
            long budgetWon,
            long startedMinute,
            long dueMinute,
            bool resolved = false,
            int quality = 0,
            long revenueWon = 0,
            BusinessIndustry industry = BusinessIndustry.WebAndSoftware)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Product title is required.", nameof(title));
            if (budgetWon <= 0) throw new ArgumentOutOfRangeException(nameof(budgetWon));
            if (startedMinute < 0 || dueMinute <= startedMinute) throw new ArgumentOutOfRangeException(nameof(dueMinute));
            if (quality < 0 || quality > 100) throw new ArgumentOutOfRangeException(nameof(quality));
            if (revenueWon < 0) throw new ArgumentOutOfRangeException(nameof(revenueWon));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            Sequence = sequence;
            Title = title;
            TargetGenre = targetGenre ?? string.Empty;
            TargetFeature = targetFeature ?? string.Empty;
            BudgetWon = budgetWon;
            StartedMinute = startedMinute;
            DueMinute = dueMinute;
            Resolved = resolved;
            Quality = quality;
            RevenueWon = revenueWon;
            Industry = industry;
        }

        public int Sequence { get; }
        public string Title { get; }
        public string TargetGenre { get; }
        public string TargetFeature { get; }
        public long BudgetWon { get; }
        public long StartedMinute { get; }
        public long DueMinute { get; }
        public bool Resolved { get; private set; }
        public int Quality { get; private set; }
        public long RevenueWon { get; private set; }
        public BusinessIndustry Industry { get; }

        internal void Resolve(int quality, long revenueWon)
        {
            if (Resolved) throw new InvalidOperationException("Product project is already resolved.");
            Quality = Math.Max(0, Math.Min(100, quality));
            RevenueWon = Math.Max(0, revenueWon);
            Resolved = true;
        }
    }

    public sealed class OwnedBusinessState
    {
        public OwnedBusinessState(
            BusinessIndustry industry,
            string businessName,
            long foundedMinute,
            long foundingInvestmentWon,
            long totalRevenueWon = 0,
            int launchedProductCount = 0)
        {
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            if (string.IsNullOrWhiteSpace(businessName)) throw new ArgumentException("Business name is required.", nameof(businessName));
            if (foundedMinute < 0) throw new ArgumentOutOfRangeException(nameof(foundedMinute));
            if (foundingInvestmentWon <= 0) throw new ArgumentOutOfRangeException(nameof(foundingInvestmentWon));
            if (totalRevenueWon < 0 || launchedProductCount < 0) throw new ArgumentOutOfRangeException();
            Industry = industry;
            BusinessName = businessName;
            FoundedMinute = foundedMinute;
            FoundingInvestmentWon = foundingInvestmentWon;
            TotalRevenueWon = totalRevenueWon;
            LaunchedProductCount = launchedProductCount;
        }

        public BusinessIndustry Industry { get; }
        public string BusinessName { get; }
        public long FoundedMinute { get; }
        public long FoundingInvestmentWon { get; }
        public long TotalRevenueWon { get; private set; }
        public int LaunchedProductCount { get; private set; }
        public int Level => 1 + Math.Min(4, (int)(TotalRevenueWon / 10_000_000L));

        internal void RecordProductRevenue(long revenueWon)
        {
            if (revenueWon < 0) throw new ArgumentOutOfRangeException(nameof(revenueWon));
            TotalRevenueWon = checked(TotalRevenueWon + revenueWon);
            LaunchedProductCount = checked(LaunchedProductCount + 1);
        }
    }

    public sealed class CompanyGrowthState
    {
        public const long ResearchCenterOpeningCostWon = 1_000_000;
        public const long MarketReportCostWon = 100_000;
        public const long FirstOwnedBusinessCostWon = 5_000_000;
        public const long AdditionalIndustryCostStepWon = 3_000_000;
        public const long GlobalExpansionRevenueRequirementWon = 100_000_000;
        public const int GlobalExpansionReputationRequirement = 60;

        private static readonly string[][] TrendGenres =
        {
            new[] { "개인 홈페이지 제작 도구", "PC 메신저 부가 기능", "교육용 PC 소프트웨어", "파일 공유 관리 도구" },
            new[] { "캐릭터 벨소리·배경화면", "피처폰 타이쿤 게임", "모바일 교통·날씨 정보", "휴대폰 꾸미기 콘텐츠" },
            new[] { "휴대용 MP3 플레이어", "PC방 주변기기", "게임잡지 부록 CD", "조립 PC 품질보증" },
            new[] { "밀레니엄 스트릿 의류", "동네 대여점 전산화", "배달 상권 인쇄물", "초기 인터넷 쇼핑몰" }
        };

        private static readonly string[][] TrendFeatures =
        {
            new[] { "완전한 한글 지원", "초보자용 간편 설치", "낮은 PC 사양", "빠른 패치와 게시판 지원" },
            new[] { "여러 기종 호환", "작은 화면 가독성", "적은 데이터 사용량", "중독성 있는 짧은 플레이" },
            new[] { "튼튼한 내구성", "128MB 이상 저장공간", "간단한 드라이버 설치", "빠른 교환·수리" },
            new[] { "넉넉한 오버핏", "눈에 띄는 인쇄 디자인", "정확한 재고 관리", "빠른 동대문 배송" }
        };

        private readonly HashSet<string> _researchedTechnologyIds;
        private readonly List<OwnedBusinessState> _ownedBusinesses;
        private int _marketReportSequence;
        private int _productSequence;

        public CompanyGrowthState(
            bool researchCenterUnlocked = false,
            IEnumerable<string> researchedTechnologyIds = null,
            MarketReportState marketReport = null,
            ProductProjectState productProject = null,
            int marketReportSequence = 0,
            int productSequence = 0,
            IEnumerable<OwnedBusinessState> ownedBusinesses = null,
            IEnumerable<KeyValuePair<string, int>> technologyPoints = null,
            StarterProductState starterProduct = null)
        {
            Technology = new CompanyTechnologyState(technologyPoints);
            StarterProduct = starterProduct ?? new StarterProductState();
            ResearchCenterUnlocked = researchCenterUnlocked;
            _researchedTechnologyIds = researchedTechnologyIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(researchedTechnologyIds, StringComparer.Ordinal);
            if (_researchedTechnologyIds.Any(id => ResearchTechnologyCatalog.All.All(item => item.TechnologyId != id)))
            {
                throw new InvalidOperationException("Unknown researched technology ID.");
            }
            if (marketReportSequence < 0 || productSequence < 0) throw new ArgumentOutOfRangeException();
            MarketReport = marketReport;
            ProductProject = productProject;
            _ownedBusinesses = ownedBusinesses == null
                ? new List<OwnedBusinessState>()
                : ownedBusinesses.Select(item => new OwnedBusinessState(
                    item.Industry,
                    item.BusinessName,
                    item.FoundedMinute,
                    item.FoundingInvestmentWon,
                    item.TotalRevenueWon,
                    item.LaunchedProductCount)).ToList();
            if (_ownedBusinesses.Select(item => item.Industry).Distinct().Count() != _ownedBusinesses.Count)
            {
                throw new InvalidOperationException("Owned business industries must be unique.");
            }
            _marketReportSequence = marketReportSequence;
            _productSequence = productSequence;
        }

        public bool ResearchCenterUnlocked { get; private set; }
        public IReadOnlyCollection<string> ResearchedTechnologyIds => _researchedTechnologyIds;

        /// <summary>
        /// Know-how earned by finishing subcontracts, separate from <see cref="ResearchedTechnologyIds"/>,
        /// which is the older cash-purchased unlock. Money buys a licence; work buys a level.
        /// </summary>
        public CompanyTechnologyState Technology { get; }
        public StarterProductState StarterProduct { get; }
        public MarketReportState MarketReport { get; private set; }
        public ProductProjectState ProductProject { get; private set; }
        public int MarketReportSequence => _marketReportSequence;
        public int ProductSequence => _productSequence;
        public IReadOnlyList<OwnedBusinessState> OwnedBusinesses => _ownedBusinesses;
        public long TotalOwnedBusinessRevenueWon => _ownedBusinesses.Sum(item => item.TotalRevenueWon);

        public long FoundingCostFor(BusinessIndustry industry)
        {
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            return checked(FirstOwnedBusinessCostWon + _ownedBusinesses.Count * AdditionalIndustryCostStepWon);
        }

        public bool HasOwnedBusiness(BusinessIndustry industry)
        {
            return _ownedBusinesses.Any(item => item.Industry == industry);
        }

        public OwnedBusinessState GetOwnedBusiness(BusinessIndustry industry)
        {
            var business = _ownedBusinesses.FirstOrDefault(item => item.Industry == industry);
            if (business == null) throw new KeyNotFoundException($"Owned business not found: {industry}");
            return business;
        }

        public string CorporateStage
        {
            get
            {
                if (_ownedBusinesses.Count == 0) return "가족 하청업체";
                if (_ownedBusinesses.Count == 1 && TotalOwnedBusinessRevenueWon < 30_000_000) return "자체 사업 스타트업";
                if (_ownedBusinesses.Count < 4 || TotalOwnedBusinessRevenueWon < 100_000_000) return "다각화 성장기업";
                return "글로벌 진출 준비기업";
            }
        }

        public int GlobalExpansionReadiness(CompanyState company)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            var industryScore = _ownedBusinesses.Count * 10;
            var revenueScore = (int)Math.Min(40, TotalOwnedBusinessRevenueWon * 40 / GlobalExpansionRevenueRequirementWon);
            var reputationScore = Math.Min(20, company.Reputation * 20 / GlobalExpansionReputationRequirement);
            return Math.Min(100, industryScore + revenueScore + reputationScore);
        }

        public bool CanBeginGlobalExpansion(CompanyState company)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            return _ownedBusinesses.Count == BusinessIndustryCatalog.All.Count
                   && TotalOwnedBusinessRevenueWon >= GlobalExpansionRevenueRequirementWon
                   && company.Reputation >= GlobalExpansionReputationRequirement;
        }

        public bool HasTechnology(string technologyId)
        {
            return string.IsNullOrEmpty(technologyId) || _researchedTechnologyIds.Contains(technologyId);
        }

        public bool TryOpenResearchCenter(CompanyState company, long elapsedMinute, out string message)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (ResearchCenterUnlocked)
            {
                message = "R&D 센터는 이미 운영 중입니다.";
                return false;
            }
            if (company.CashWon < ResearchCenterOpeningCostWon)
            {
                message = "R&D 센터를 열 자금이 부족합니다.";
                return false;
            }

            company.PayOperatingExpense(
                "growth:research-center",
                elapsedMinute,
                ResearchCenterOpeningCostWon,
                "R&D 센터 설립 투자");
            ResearchCenterUnlocked = true;
            message = "R&D 센터를 열었습니다. 고급 능력치와 연구 항목이 공개됩니다.";
            return true;
        }

        public bool TryResearch(string technologyId, CompanyState company, long elapsedMinute, out string message)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            var definition = ResearchTechnologyCatalog.Get(technologyId);
            if (!ResearchCenterUnlocked)
            {
                message = "먼저 R&D 센터를 설립해야 합니다.";
                return false;
            }
            if (HasTechnology(technologyId))
            {
                message = $"{definition.DisplayName} 연구는 이미 완료했습니다.";
                return false;
            }
            if (!string.IsNullOrEmpty(definition.PrerequisiteId) && !HasTechnology(definition.PrerequisiteId))
            {
                message = $"선행 연구: {ResearchTechnologyCatalog.Get(definition.PrerequisiteId).DisplayName}";
                return false;
            }
            if (company.CashWon < definition.CostWon)
            {
                message = "연구 자금이 부족합니다.";
                return false;
            }

            company.PayOperatingExpense(
                $"growth:research:{definition.TechnologyId}",
                elapsedMinute,
                definition.CostWon,
                $"R&D · {definition.DisplayName}");
            _researchedTechnologyIds.Add(definition.TechnologyId);
            message = $"연구 완료 · {definition.DisplayName}";
            return true;
        }

        public bool TryPurchaseMarketReport(int worldSeed, CompanyState company, long elapsedMinute, out string message)
        {
            return TryPurchaseMarketReport(
                worldSeed,
                BusinessIndustry.WebAndSoftware,
                company,
                elapsedMinute,
                out message);
        }

        public bool TryPurchaseMarketReport(
            int worldSeed,
            BusinessIndustry industry,
            CompanyState company,
            long elapsedMinute,
            out string message)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            if (!HasTechnology(ResearchTechnologyIds.MarketAnalysis))
            {
                message = "소비자 시장 분석 연구가 필요합니다.";
                return false;
            }
            if (company.CashWon < MarketReportCostWon)
            {
                message = "시장 조사비가 부족합니다.";
                return false;
            }

            var sequence = _marketReportSequence++;
            company.PayOperatingExpense(
                $"growth:market-report:{(int)industry}:{sequence:D6}",
                elapsedMinute,
                MarketReportCostWon,
                "소비자 시장 조사 보고서");
            var industryGenres = TrendGenres[(int)industry];
            var industryFeatures = TrendFeatures[(int)industry];
            var genreIndex = StableRandom.StableRandomInt($"market-genre:{worldSeed}:{(int)industry}:{elapsedMinute}:{sequence}", industryGenres.Length);
            var featureIndex = StableRandom.StableRandomInt($"market-feature:{worldSeed}:{(int)industry}:{elapsedMinute}:{sequence}", industryFeatures.Length);
            var demand = 55 + StableRandom.StableRandomInt($"market-demand:{worldSeed}:{elapsedMinute}:{sequence}", 41);
            MarketReport = new MarketReportState(
                industryGenres[genreIndex],
                industryFeatures[featureIndex],
                demand,
                elapsedMinute,
                industry);
            message = $"{BusinessIndustryCatalog.Get(industry).DisplayName} 조사 완료 · {MarketReport.Genre} 수요 {MarketReport.Demand}";
            return true;
        }

        public bool TryFoundBusiness(
            BusinessIndustry industry,
            CompanyState company,
            long elapsedMinute,
            out string message)
        {
            return TryFoundBusinessInternal(industry, company, null, elapsedMinute, out message);
        }

        public bool TryFoundBusiness(
            BusinessIndustry industry,
            CompanyState company,
            FamilyState family,
            long elapsedMinute,
            out string message)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            return TryFoundBusinessInternal(industry, company, family, elapsedMinute, out message);
        }

        private bool TryFoundBusinessInternal(
            BusinessIndustry industry,
            CompanyState company,
            FamilyState family,
            long elapsedMinute,
            out string message)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            var definition = BusinessIndustryCatalog.Get(industry);
            if (!HasTechnology(ResearchTechnologyIds.MarketAnalysis) || MarketReport == null || MarketReport.Industry != industry)
            {
                message = "소비자 시장 분석 연구와 시장 보고서가 필요합니다.";
                return false;
            }
            if (HasOwnedBusiness(industry))
            {
                message = $"{definition.DisplayName} 사업은 이미 운영 중입니다.";
                return false;
            }

            var costWon = FoundingCostFor(industry);
            if (company.CashWon < costWon)
            {
                message = $"사업 창업 자금이 부족합니다. 필요 자금 {costWon:N0}원";
                return false;
            }

            company.PayOperatingExpense(
                $"growth:business:{(int)industry}:founding",
                elapsedMinute,
                costWon,
                $"자체 사업 창업 · {definition.OwnBusinessName}");
            _ownedBusinesses.Add(new OwnedBusinessState(
                industry,
                definition.OwnBusinessName,
                elapsedMinute,
                costWon));
            company.ChangeReputation(3);
            family?.RecordSharedCareerMemory(
                $"business:{(int)industry}:founded",
                industry,
                CareerMemoryKind.BusinessFounded,
                $"{definition.OwnBusinessName}를 함께 창업했다.",
                elapsedMinute,
                2);
            message = _ownedBusinesses.Count == 1
                ? $"첫 자체 사업 시작 · {definition.OwnBusinessName}"
                : $"문어발 확장 완료 · {definition.OwnBusinessName}";
            return true;
        }

        public bool TryStartProduct(string title, long budgetWon, CompanyState company, long elapsedMinute, out string message)
        {
            if (_ownedBusinesses.Count == 0)
            {
                message = "먼저 자체 사업 분야를 창업해야 합니다.";
                return false;
            }

            return TryStartProduct(_ownedBusinesses[0].Industry, title, budgetWon, company, elapsedMinute, out message);
        }

        public bool TryStartProduct(
            BusinessIndustry industry,
            string title,
            long budgetWon,
            CompanyState company,
            long elapsedMinute,
            out string message)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (!HasTechnology(ResearchTechnologyIds.MarketAnalysis) || MarketReport == null || MarketReport.Industry != industry)
            {
                message = "시장 분석 연구와 최신 시장 보고서가 필요합니다.";
                return false;
            }
            if (!HasOwnedBusiness(industry))
            {
                message = "선택한 분야의 자체 사업을 먼저 창업해야 합니다.";
                return false;
            }
            if (ProductProject != null && !ProductProject.Resolved)
            {
                message = "이미 진행 중인 자체 제품이 있습니다.";
                return false;
            }
            if (budgetWon != 1_000_000 && budgetWon != 2_000_000 && budgetWon != 4_000_000)
            {
                throw new ArgumentOutOfRangeException(nameof(budgetWon));
            }
            if (company.CashWon < budgetWon)
            {
                message = "제품 개발 예산이 부족합니다.";
                return false;
            }

            var sequence = _productSequence++;
            var developmentDays = budgetWon == 1_000_000 ? 14 : budgetWon == 2_000_000 ? 21 : 30;
            company.PayOperatingExpense(
                $"growth:product:{sequence:D6}:budget",
                elapsedMinute,
                budgetWon,
                $"자체 제품 개발 · {title}");
            ProductProject = new ProductProjectState(
                sequence,
                string.IsNullOrWhiteSpace(title) ? "우리 가족의 첫 제품" : title.Trim(),
                MarketReport.Genre,
                MarketReport.DesiredFeature,
                budgetWon,
                elapsedMinute,
                checked(elapsedMinute + developmentDays * 1440L),
                industry: industry);
            message = $"자체 제품 개발 시작 · {developmentDays}일 뒤 출시";
            return true;
        }

        public long ResolveProductIfDue(int worldSeed, long elapsedMinute, FamilyState family, CompanyState company)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (company == null) throw new ArgumentNullException(nameof(company));
            var project = ProductProject;
            if (project == null || project.Resolved || elapsedMinute < project.DueMinute) return 0;

            var workforceQuality = ProductWorkforcePerformanceRules.CalculateCompatibilityQuality(family);
            var technologyBonus = (HasTechnology(ResearchTechnologyIds.ThreeDModeling) ? 5 : 0)
                                  + (HasTechnology(ResearchTechnologyIds.AutomationLine) ? 7 : 0);
            var variance = StableRandom.StableRandomInt(
                $"product-result:{worldSeed}:{project.Sequence}:{project.StartedMinute}",
                31) - 15;
            var quality = Math.Max(0, Math.Min(100,
                workforceQuality + technologyBonus + variance));
            var revenueRatePercent = Math.Max(20, 35 + quality * 2 + variance);
            var revenue = checked(project.BudgetWon * revenueRatePercent / 100L);
            project.Resolve(quality, revenue);
            var business = _ownedBusinesses.FirstOrDefault(item => item.Industry == project.Industry);
            business?.RecordProductRevenue(revenue);
            family.RecordSharedCareerMemory(
                $"product:{project.Sequence:D6}:launched",
                project.Industry,
                CareerMemoryKind.ProductLaunched,
                $"{project.Title} 제품을 함께 출시했다.",
                elapsedMinute,
                quality >= 70 ? 2 : 1);
            if (revenue > 0)
            {
                company.RecordSale(
                    $"growth:product:{project.Sequence:D6}:launch",
                    elapsedMinute,
                    revenue);
                company.ChangeReputation(quality >= 70 ? 5 : quality >= 45 ? 2 : -1);
            }
            return revenue;
        }
    }
}

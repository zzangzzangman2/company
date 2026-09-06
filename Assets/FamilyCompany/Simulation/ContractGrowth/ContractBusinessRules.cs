using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Technology;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public sealed class ContractMemberChoiceViewModel
    {
        public ContractMemberChoiceViewModel(string memberId, string displayName, bool available, string availabilityKo)
        {
            MemberId = memberId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Available = available;
            AvailabilityKo = availabilityKo ?? string.Empty;
        }
        public string MemberId { get; }
        public string DisplayName { get; }
        public bool Available { get; }
        public string AvailabilityKo { get; }
    }

    public sealed class ContractCardViewModel
    {
        public ContractCardViewModel(
            ContractOfferDefinition definition,
            string tierKo,
            string rewardKo,
            string deadlineKo,
            string workKo,
            string capabilityKo,
            string riskKo,
            string reputationKo,
            IEnumerable<ContractMemberChoiceViewModel> memberChoices,
            string technologyKo = "",
            string requirementKo = "",
            bool technologyRequirementMet = true,
            string proficiencyKo = "")
        {
            TechnologyKo = technologyKo ?? string.Empty;
            RequirementKo = requirementKo ?? string.Empty;
            TechnologyRequirementMet = technologyRequirementMet;
            ProficiencyKo = proficiencyKo ?? string.Empty;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            TierKo = tierKo ?? string.Empty;
            RewardKo = rewardKo ?? string.Empty;
            DeadlineKo = deadlineKo ?? string.Empty;
            WorkKo = workKo ?? string.Empty;
            CapabilityKo = capabilityKo ?? string.Empty;
            RiskKo = riskKo ?? string.Empty;
            ReputationKo = reputationKo ?? string.Empty;
            MemberChoices = (memberChoices ?? Array.Empty<ContractMemberChoiceViewModel>()).ToArray();
        }
        public ContractOfferDefinition Definition { get; }
        public string OfferId => Definition.Offer.OfferId;
        public string ClientNameKo => Definition.Client.DisplayNameKo;
        public string TitleKo => Definition.Offer.Title;
        public string TierKo { get; }
        public string RewardKo { get; }
        public string DeadlineKo { get; }
        public string WorkKo { get; }
        public string CapabilityKo { get; }
        public string RiskKo { get; }
        public string ReputationKo { get; }

        /// <summary>
        /// Exactly which know-how finishing this job builds, e.g. <c>기술 DB 설계 +40pt · 자료 입력 +15pt</c>.
        /// Kept as its own line so the player reads money and technology as two separate rewards.
        /// </summary>
        public string TechnologyKo { get; }

        /// <summary>
        /// Proven experience the client asks for, written as what the company has against what is
        /// needed: <c>필요 기술 DB 설계 Lv1/2</c>. Empty when the job is open to everyone.
        /// </summary>
        public string RequirementKo { get; }

        /// <summary>False when the company cannot take this job yet for lack of technology.</summary>
        public bool TechnologyRequirementMet { get; }

        /// <summary>"기술 숙련 Lv3 · 작업 속도 +10%" — what past experience is worth on this job.</summary>
        public string ProficiencyKo { get; }

        public IReadOnlyList<ContractMemberChoiceViewModel> MemberChoices { get; }
    }

    public sealed class ContractBoardViewModel
    {
        public ContractBoardViewModel(
            ContractOfferBoardSnapshot snapshot,
            IEnumerable<ContractCardViewModel> cards,
            string headingKo,
            string guidanceKo)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Cards = (cards ?? throw new ArgumentNullException(nameof(cards))).ToArray();
            HeadingKo = headingKo ?? string.Empty;
            GuidanceKo = guidanceKo ?? string.Empty;
        }
        public ContractOfferBoardSnapshot Snapshot { get; }
        public IReadOnlyList<ContractCardViewModel> Cards { get; }
        public string HeadingKo { get; }
        public string GuidanceKo { get; }
    }

    public sealed class BusinessHubViewModel
    {
        public BusinessHubViewModel(
            string stageKo,
            string cashKo,
            string reputationKo,
            bool showFirstContractBadge,
            string notificationKo,
            ContractClientTier highestUnlockedTier,
            IReadOnlyList<ContractTierProgress> tierProgress,
            IReadOnlyList<ProductOpportunityProgress> productProgress)
        {
            StageKo = stageKo ?? string.Empty;
            CashKo = cashKo ?? string.Empty;
            ReputationKo = reputationKo ?? string.Empty;
            ShowFirstContractBadge = showFirstContractBadge;
            NotificationKo = notificationKo ?? string.Empty;
            HighestUnlockedTier = highestUnlockedTier;
            TierProgress = tierProgress ?? throw new ArgumentNullException(nameof(tierProgress));
            ProductProgress = productProgress ?? throw new ArgumentNullException(nameof(productProgress));
        }
        public string StageKo { get; }
        public string CashKo { get; }
        public string ReputationKo { get; }
        public bool ShowFirstContractBadge { get; }
        public string NotificationKo { get; }
        public ContractClientTier HighestUnlockedTier { get; }
        public IReadOnlyList<ContractTierProgress> TierProgress { get; }
        public IReadOnlyList<ProductOpportunityProgress> ProductProgress { get; }
    }

    public static class ContractBusinessViewModelRules
    {
        public static ContractBoardViewModel CreateBoard(
            GameState state,
            ContractClientTierCatalog clients,
            BusinessIndustry industry)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var summary = ContractPerformanceRules.Rebuild(state.Contracts, state.Family, clients);
            var profile = ContractPerformanceRules.BuildCompanyProfile(summary, state.Company, state.Family, state.Growth);
            var snapshot = ContractOfferBoardRules.Generate(
                state.WorldSeed,
                state.Time.ElapsedMinutes,
                industry,
                state.Contracts.Contracts.Count > 0,
                summary,
                profile,
                clients);
            var cards = snapshot.Offers
                .Select(item => CreateCard(item, state.Family, state.Time.Now, state.Growth.Technology))
                .ToArray();
            return new ContractBoardViewModel(
                snapshot,
                cards,
                snapshot.FirstContractRecommendation ? "첫 계약 선택" : "하청 계약 게시판",
                snapshot.FirstContractRecommendation
                    ? "자동 수락되지 않습니다. 세 제안의 보상·기간·작업량을 비교해 직접 고르세요."
                    : "게임 날짜가 바뀔 때만 제안이 갱신됩니다. 창을 다시 열어도 바뀌지 않습니다.");
        }

        public static BusinessHubViewModel CreateHub(
            GameState state,
            ContractClientTierCatalog clients,
            BusinessIndustry industry)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var summary = ContractPerformanceRules.Rebuild(state.Contracts, state.Family, clients);
            var profile = ContractPerformanceRules.BuildCompanyProfile(summary, state.Company, state.Family, state.Growth);
            var tierProgress = ContractProgressionRules.EvaluateAll(summary, profile, industry);
            var firstContractPending = state.Contracts.Contracts.Count == 0;
            return new BusinessHubViewModel(
                state.Growth.CorporateStage,
                Won(state.Company.CashWon),
                $"평판 {state.Company.Reputation}",
                firstContractPending,
                firstContractPending ? "새 하청 제안 3건 · 첫 계약을 직접 골라보세요" : "실적에 따라 더 큰 고객 제안이 섞입니다",
                ContractProgressionRules.HighestUnlocked(tierProgress),
                tierProgress,
                ProductOpportunityRules.EvaluateAll(summary, state.Company, state.Growth));
        }

        /// <summary>
        /// "필요 기술 DB 설계 Lv1/2" — always written as have/need so the player can see how close the
        /// company is, instead of a bare locked flag.
        /// </summary>
        private static string RequirementLabel(
            ContractOfferDefinition definition,
            CompanyTechnologyState technology)
        {
            var requirements = definition.RequiredTechnologyLevels;
            if (requirements.Count == 0) return string.Empty;
            var parts = requirements.Select(item => item.ProgressKo(technology));
            return "필요 기술 " + string.Join(" · ", parts);
        }

        public static ContractCardViewModel CreateCard(
            ContractOfferDefinition definition,
            FamilyState family,
            DateTime now,
            CompanyTechnologyState technology = null)
        {
            var offer = definition.Offer;
            var task = ContractWorkTaskProfiles.Resolve(definition.Specialty);
            var choices = family.Members.Select(member =>
            {
                var schedule = FamilyScheduleRules.Resolve(member.Role, now);
                var taskScore = WorkforcePerformanceRules.CalculateScore(member.Capability.Skills, task.ProgressWeights);
                var capable = taskScore >= offer.RequiredCapability;
                var technologyReady = definition.RequiredTechnologyLevels.AllMetBy(technology);
                var available = schedule.CanPerformCompanyWork && member.Energy >= 2 && capable && technologyReady;
                var label = !technologyReady
                    ? "회사 기술 부족"
                    : !schedule.CanPerformCompanyWork ? schedule.Label
                    : !capable ? "요구 역량 부족"
                    : member.Energy < 2 ? "체력 부족" : "배정 가능";
                return new ContractMemberChoiceViewModel(member.MemberId, member.DisplayName, available, label);
            }).ToArray();
            return new ContractCardViewModel(
                definition,
                ContractRewardBalanceRules.TierLabel(definition.ClientTier),
                $"보상 {Won(offer.RewardWon)}",
                $"마감 {offer.DeadlineDays}일",
                $"작업 {offer.EstimatedPersonHours}인시 · {offer.RequiredWorkers}명 권장",
                $"업무 적합도 {offer.RequiredCapability} · 품질 기준 {definition.QualityStandard}",
                $"예상 위험 {RiskLabel(definition.RiskLevel)}",
                $"완료 평판 +{definition.ReputationReward} · 실패 위험 -{definition.ReputationRisk}",
                choices,
                "기술 " + ContractTechnologyGrantCatalog.DisplayKo(definition.Template.LegacyGlobalIndex),
                RequirementLabel(definition, technology),
                definition.RequiredTechnologyLevels.AllMetBy(technology),
                CompanyTechnologyBonusRules.DisplayKo(
                    technology,
                    ContractTechnologyGrantCatalog.ForTemplateIndex(definition.Template.LegacyGlobalIndex)));
        }

        public static string Won(long amountWon) => "₩" + amountWon.ToString("N0", CultureInfo.InvariantCulture);

        private static string RiskLabel(ContractRiskLevel risk)
        {
            switch (risk)
            {
                case ContractRiskLevel.Low: return "낮음";
                case ContractRiskLevel.Moderate: return "보통";
                case ContractRiskLevel.High: return "높음";
                default: return "매우 높음";
            }
        }
    }

    public static class ProductOpportunityRules
    {
        private static readonly ProductOpportunityDefinition[] Definitions =
        {
            // Each path asks for the technologies its own industry's subcontracts actually teach, so
            // the way to unlock a product is to keep taking that kind of work. Levels come only from
            // finished contracts; the ResearchTechnologyIds entry stays as the separate cash unlock.
            new ProductOpportunityDefinition("own-product:web", BusinessIndustry.WebAndSoftware, "웹 서비스·PC 소프트웨어", 6_000_000, 4, 80, 8, new[] { ResearchTechnologyIds.MarketAnalysis }, ContractRiskLevel.Moderate, "패키지 판매·서비스 이용료",
                new[]
                {
                    new TechnologyLevelRequirement(CompanyTechnologyIds.DatabaseDesign, 2),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.WebPublishing, 1),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.ServerOperations, 1)
                }),
            new ProductOpportunityDefinition("own-product:mobile", BusinessIndustry.FeaturePhoneAndMobile, "피처폰 콘텐츠·모바일 게임", 7_000_000, 6, 120, 12, new[] { ResearchTechnologyIds.AutomationLine, ResearchTechnologyIds.MarketAnalysis }, ContractRiskLevel.High, "다운로드 판매·퍼블리싱 정산",
                new[]
                {
                    new TechnologyLevelRequirement(CompanyTechnologyIds.FeaturePhoneUi, 2),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.DevicePorting, 1),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.MidiSound, 1)
                }),
            new ProductOpportunityDefinition("own-product:hardware", BusinessIndustry.HardwareAndPc, "PC 주변기기·디지털 기기", 8_000_000, 8, 160, 16, new[] { ResearchTechnologyIds.AutomationLine, ResearchTechnologyIds.MarketAnalysis }, ContractRiskLevel.High, "제조 원가 후 기기 판매",
                new[]
                {
                    new TechnologyLevelRequirement(CompanyTechnologyIds.BoardAssembly, 2),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.PcAssembly, 1),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.QualityInspection, 1)
                }),
            new ProductOpportunityDefinition("own-product:retail", BusinessIndustry.FashionRetailAndOffline, "상점 전산화·유통 브랜드", 6_000_000, 5, 100, 10, new[] { ResearchTechnologyIds.MarketAnalysis }, ContractRiskLevel.Moderate, "상품 마진·납품 매출",
                new[]
                {
                    new TechnologyLevelRequirement(CompanyTechnologyIds.InventorySystem, 2),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.AdminTool, 1),
                    new TechnologyLevelRequirement(CompanyTechnologyIds.ProductPhotography, 1)
                })
        };

        public static IReadOnlyList<ProductOpportunityDefinition> All => Definitions;

        public static IReadOnlyList<ProductOpportunityProgress> EvaluateAll(
            ContractPerformanceSummary summary,
            CompanyState company,
            CompanyGrowthState growth)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (company == null) throw new ArgumentNullException(nameof(company));
            if (growth == null) throw new ArgumentNullException(nameof(growth));
            return Definitions.Select(definition => Evaluate(definition, summary, company, growth)).ToArray();
        }

        /// <summary>
        /// Reads the level requirements as "쌓은 기술 DB 설계 Lv1/2 · 웹 퍼블리싱 Lv1/1" so the player can
        /// see exactly which subcontract work is still missing rather than a single locked flag.
        /// </summary>
        private static string SkillLabel(
            IReadOnlyList<TechnologyLevelRequirement> requirements,
            CompanyGrowthState growth)
        {
            if (requirements.Count == 0) return "필요 기술 없음";
            var parts = requirements.Select(item =>
            {
                var name = CompanyTechnologyCatalog.Get(item.TechnologyId).DisplayNameKo;
                return $"{name} Lv{growth.Technology.LevelFor(item.TechnologyId)}/{item.RequiredLevel}";
            });
            return "쌓은 기술 " + string.Join(" · ", parts);
        }

        private static ProductOpportunityProgress Evaluate(
            ProductOpportunityDefinition definition,
            ContractPerformanceSummary summary,
            CompanyState company,
            CompanyGrowthState growth)
        {
            var experience = summary.DomainExperienceHours(definition.Industry);
            var correctReport = growth.MarketReport != null && growth.MarketReport.Industry == definition.Industry;
            var technologyReady = definition.RequiredTechnologyIds.All(growth.HasTechnology);
            // Averaged per requirement so a path that is one level short reads as nearly ready
            // instead of collapsing to zero.
            var levelRequirements = definition.RequiredTechnologyLevels;
            var skillReady = levelRequirements.Count == 0
                ? 10_000
                : levelRequirements.Sum(item => Ratio(
                      growth.Technology.LevelFor(item.TechnologyId),
                      item.RequiredLevel)) / levelRequirements.Count;
            var values = new[]
            {
                Ratio(company.CashWon, definition.RequiredCashWon),
                Ratio(summary.CompletedContracts, definition.RequiredCompletedContracts),
                Ratio(experience, definition.RequiredDomainExperienceHours),
                Ratio(company.Reputation, definition.RequiredReputation),
                technologyReady ? 10_000 : 0,
                skillReady,
                correctReport ? 10_000 : 0
            };
            var labels = new[]
            {
                $"가용 현금 {ContractBusinessViewModelRules.Won(company.CashWon)}/{ContractBusinessViewModelRules.Won(definition.RequiredCashWon)}",
                $"하청 완료 {summary.CompletedContracts}/{definition.RequiredCompletedContracts}건",
                $"관련 하청 경험 {experience}/{definition.RequiredDomainExperienceHours}인시",
                $"평판 {company.Reputation}/{definition.RequiredReputation}",
                technologyReady ? "필요 연구 완료" : "소비자 시장 분석 등 필요 연구 미완료",
                SkillLabel(levelRequirements, growth),
                correctReport ? "해당 분야 시장 보고서 보유" : "해당 분야 시장 보고서 필요"
            };
            return new ProductOpportunityProgress(
                definition,
                values.All(value => value >= 10_000),
                values.Sum() / values.Length,
                labels,
                growth.HasOwnedBusiness(definition.Industry),
                true);
        }

        private static int Ratio(long value, long requirement)
        {
            if (requirement <= 0) return 10_000;
            return (int)Math.Min(10_000L, Math.Max(0L, value) * 10_000L / requirement);
        }
    }

    public sealed class ContractBusinessRouteStack
    {
        private readonly List<ContractBusinessRoute> _routes = new List<ContractBusinessRoute> { ContractBusinessRoute.OfficeWorld };
        public ContractBusinessRoute Current => _routes[_routes.Count - 1];
        public IReadOnlyList<ContractBusinessRoute> Routes => _routes;

        public void Open(ContractBusinessRoute route)
        {
            if (route == Current) return;
            _routes.Add(route);
        }

        public bool TryBack()
        {
            if (_routes.Count <= 1) return false;
            _routes.RemoveAt(_routes.Count - 1);
            return true;
        }

        public void ResetToOffice()
        {
            _routes.Clear();
            _routes.Add(ContractBusinessRoute.OfficeWorld);
        }
    }

    public sealed class AuthoritativeContractWorkAdvanceResult
    {
        private readonly ContractTechnologyGrant[] _technologyGrants;

        public AuthoritativeContractWorkAdvanceResult(
            int attemptedHours,
            int appliedHours,
            bool completed,
            long rewardWon,
            ContractWorkRejectionReason lastRejection,
            IEnumerable<ContractTechnologyGrant> technologyGrants = null,
            int workRateBasisPoints = CompanyTechnologyBonusRules.NeutralBasisPoints,
            IReadOnlyList<CompanyTechnologyGainRecord> technologyGains = null)
        {
            AttemptedHours = attemptedHours;
            AppliedHours = appliedHours;
            Completed = completed;
            RewardWon = rewardWon;
            LastRejection = lastRejection;
            _technologyGrants = (technologyGrants ?? Array.Empty<ContractTechnologyGrant>()).ToArray();
            WorkRateBasisPoints = workRateBasisPoints;
            TechnologyGains = technologyGains ?? Array.Empty<CompanyTechnologyGainRecord>();
        }

        public int AttemptedHours { get; }
        public int AppliedHours { get; }
        public bool Completed { get; }

        /// <summary>Cash settled for the finished contract. Never mixed with the technology reward.</summary>
        public long RewardWon { get; }

        public ContractWorkRejectionReason LastRejection { get; }

        /// <summary>
        /// Informational grants only. ContractPortfolio already settled TechnologyGains exactly
        /// once; callers must never apply TechnologyGrants to company state again.
        /// </summary>
        public IReadOnlyList<ContractTechnologyGrant> TechnologyGrants => _technologyGrants;
        public IReadOnlyList<CompanyTechnologyGainRecord> TechnologyGains { get; }

        /// <summary>
        /// Speed the company's existing experience gave this job, in basis points against a neutral
        /// 10,000. Reported so the work panel can say why the same contract went faster this time.
        /// </summary>
        public int WorkRateBasisPoints { get; }
    }

    /// <summary>
    /// Transient work command. It creates person-hours only from monotonically increasing authoritative GameTime minutes.
    /// Real seconds, frame count, UI reopen and timeScale never enter this API.
    /// </summary>
    public sealed class AuthoritativeContractWorkSession
    {
        private readonly string _offerId;
        private readonly string _memberId;
        private long _consumedThroughMinute;

        public AuthoritativeContractWorkSession(string offerId, string memberId, long startedMinute)
        {
            if (string.IsNullOrWhiteSpace(offerId)) throw new ArgumentException("Offer ID is required.", nameof(offerId));
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (startedMinute < 0) throw new ArgumentOutOfRangeException(nameof(startedMinute));
            _offerId = offerId;
            _memberId = memberId;
            _consumedThroughMinute = startedMinute;
        }

        public long ConsumedThroughMinute => _consumedThroughMinute;

        /// <summary>
        /// The technology a specific job teaches, looked up by the stable bootstrap template the
        /// offer came from. An offer with no matching template teaches nothing rather than guessing.
        /// </summary>
        private static IReadOnlyList<ContractTechnologyGrant> ResolveTechnologyGrants(SubcontractOffer offer)
        {
            return ContractPortfolio.TechnologyGrantsFor(offer);
        }

        public AuthoritativeContractWorkAdvanceResult AdvanceTo(
            long authoritativeElapsedMinute,
            ContractPortfolio portfolio,
            FamilyState family,
            CompanyState company,
            CompanyTechnologyState technology = null)
        {
            if (authoritativeElapsedMinute < _consumedThroughMinute) throw new ArgumentOutOfRangeException(nameof(authoritativeElapsedMinute));
            if (portfolio == null) throw new ArgumentNullException(nameof(portfolio));
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (company == null) throw new ArgumentNullException(nameof(company));
            var contract = portfolio.Get(_offerId);
            var member = family.Get(_memberId);
            var task = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(contract.Offer));
            var attempted = 0;
            var applied = 0;
            var completed = false;
            long reward = 0;
            var lastRejection = ContractWorkRejectionReason.None;
            // Experience the company already has with this kind of job. Constant for the whole
            // advance: a level earned by finishing this contract only helps the next one.
            var jobGrants = ResolveTechnologyGrants(contract.Offer);
            var workRate = contract.WorkRateBasisPoints;
            IReadOnlyList<CompanyTechnologyGainRecord> gains = Array.Empty<CompanyTechnologyGainRecord>();
            while (!completed)
            {
                var minutesPerPersonHour = ContractPortfolio.MinutesPerPersonHour(contract, member);
                if (authoritativeElapsedMinute - _consumedThroughMinute < minutesPerPersonHour) break;
                var creditMinute = checked(_consumedThroughMinute + minutesPerPersonHour);
                var result = portfolio.RecordWork(_offerId, _memberId, 1, creditMinute, family, company);
                _consumedThroughMinute = creditMinute;
                attempted++;
                applied += result.AppliedPersonHours;
                completed |= result.Completed;
                if (result.Completed) gains = result.TechnologyGains;
                reward = checked(reward + result.RewardWon);
                lastRejection = result.RejectionReason;
                if (completed || result.RejectionReason == ContractWorkRejectionReason.ContractNotActive) break;
            }

            var grants = completed ? jobGrants : Array.Empty<ContractTechnologyGrant>();
            return new AuthoritativeContractWorkAdvanceResult(
                attempted, applied, completed, reward, lastRejection, grants, workRate, gains);
        }
    }
}

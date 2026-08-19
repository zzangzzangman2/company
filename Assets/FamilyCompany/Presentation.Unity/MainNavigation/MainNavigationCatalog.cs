using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public enum MainNavigationTabId
    {
        Company,
        People,
        Projects,
        Research,
        Investment
    }

    public enum MainNavigationFeatureAction
    {
        None,
        OpenStatus,
        OpenBuildingEditor,
        OpenContractBoard,
        OpenProductOpportunities,
        OpenStockMarket,
        OpenTechnologyLedger
    }

    public static class MainNavigationRouteIds
    {
        public const string BuildingEditor = "company.hub.build_editor";
        public const string BusinessContracts = "business.contracts";
        public const string BusinessProducts = "business.products";
        public const string StockMarket = "investment.stock-market";
        public const string ResearchTechnology = "research.technology";
    }

    public sealed class MainNavigationFeatureDefinition
    {
        public MainNavigationFeatureDefinition(
            string id,
            string displayNameKo,
            string descriptionKo,
            string statusKo = "준비 중",
            MainNavigationFeatureAction action = MainNavigationFeatureAction.None,
            string routeId = "",
            string iconResourcePath = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Feature ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayNameKo)) throw new ArgumentException("Feature display name is required.", nameof(displayNameKo));
            if (string.IsNullOrWhiteSpace(descriptionKo)) throw new ArgumentException("Feature description is required.", nameof(descriptionKo));
            Id = id;
            DisplayNameKo = displayNameKo;
            DescriptionKo = descriptionKo;
            StatusKo = statusKo ?? string.Empty;
            Action = action;
            RouteId = routeId ?? string.Empty;
            IconResourcePath = iconResourcePath ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string DescriptionKo { get; }
        public string StatusKo { get; }
        public MainNavigationFeatureAction Action { get; }
        public string RouteId { get; }
        public string IconResourcePath { get; }
    }

    public sealed class MainNavigationTabDefinition
    {
        public MainNavigationTabDefinition(
            MainNavigationTabId tabId,
            string id,
            string displayNameKo,
            string descriptionKo,
            string iconResourcePath,
            params MainNavigationFeatureDefinition[] features)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tab ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayNameKo)) throw new ArgumentException("Tab display name is required.", nameof(displayNameKo));
            if (string.IsNullOrWhiteSpace(descriptionKo)) throw new ArgumentException("Tab description is required.", nameof(descriptionKo));
            if (string.IsNullOrWhiteSpace(iconResourcePath)) throw new ArgumentException("Tab icon path is required.", nameof(iconResourcePath));
            if (features == null || features.Length == 0) throw new ArgumentException("At least one feature is required.", nameof(features));
            TabId = tabId;
            Id = id;
            DisplayNameKo = displayNameKo;
            DescriptionKo = descriptionKo;
            IconResourcePath = iconResourcePath;
            Features = Array.AsReadOnly((MainNavigationFeatureDefinition[])features.Clone());
        }

        public MainNavigationTabId TabId { get; }
        public string Id { get; }
        public string DisplayNameKo { get; }
        public string DescriptionKo { get; }
        public string IconResourcePath { get; }
        public IReadOnlyList<MainNavigationFeatureDefinition> Features { get; }
    }

    public static class MainNavigationCatalog
    {
        public const string ResourceRoot = "MainNavigationV2/";
        public const string ModalFrameResourcePath = ResourceRoot + "Frames/modal_frame_v2";

        private static readonly MainNavigationTabDefinition[] Definitions =
        {
            new MainNavigationTabDefinition(
                MainNavigationTabId.Company,
                "company",
                "회사",
                "우리 가족회사의 공간과 성장 기반을 관리합니다.",
                ResourceRoot + "Icons/Bottom/company_v2",
                Feature("company-grade", "회사 등급", "평판과 성과에 따라 다음 성장 단계를 확인합니다."),
                Feature("company-assets", "보유 자산", "사무실·장비·사업 자산을 한곳에서 살펴봅니다."),
                Feature("company-daily-profit", "일일 수익", "오늘의 매출·비용·순수익 흐름을 확인합니다."),
                Feature(
                    "company-building-editor",
                    "사무실 관리",
                    "업무·좌석·기기·수납·음료 등 분류별 가구를 사고 타일에 배치합니다.",
                    "이용 가능 · 열기",
                    MainNavigationFeatureAction.OpenBuildingEditor,
                    MainNavigationRouteIds.BuildingEditor)),
            new MainNavigationTabDefinition(
                MainNavigationTabId.People,
                "people",
                "인사",
                "가족과 직원이 오래 함께 성장할 수 있는 조직을 만듭니다.",
                ResourceRoot + "Icons/Bottom/people_v2",
                Feature("people-hiring", "채용·해고", "지원자를 검토하고 회사에 맞는 인재를 배치합니다."),
                Feature("people-salary", "연봉 협상", "급여와 계약 조건을 회사 사정에 맞게 조율합니다."),
                Feature("people-training", "능력치 교육", "업무 역량과 잠재력을 교육으로 키웁니다."),
                Feature("people-department", "부서 배치", "역할과 강점에 맞춰 팀과 담당 업무를 정합니다.")),
            new MainNavigationTabDefinition(
                MainNavigationTabId.Projects,
                "projects",
                "사업",
                "하청에서 자체 제품까지 회사의 실제 일을 선택합니다.",
                ResourceRoot + "Icons/Bottom/projects_v2",
                Feature(
                    "projects-contracts",
                    "하청 계약",
                    "고객사의 요구 역량·마감·보상을 비교해 일을 수주합니다.",
                    "이용 가능 · 열기",
                    MainNavigationFeatureAction.OpenContractBoard,
                    MainNavigationRouteIds.BusinessContracts),
                Feature(
                    "projects-products",
                    "자체 제품",
                    "현금·평판·분야 경험을 쌓아 자체 제품을 해금합니다.",
                    "이용 가능 · 진행 보기",
                    MainNavigationFeatureAction.OpenProductOpportunities,
                    MainNavigationRouteIds.BusinessProducts),
                Feature("projects-outsourcing", "외주", "부족한 역량과 시간을 외부 파트너로 보완합니다."),
                Feature("projects-operations", "운영·유지보수", "완료한 제품과 고객 시스템을 안정적으로 운영합니다.")),
            new MainNavigationTabDefinition(
                MainNavigationTabId.Research,
                "research",
                "연구",
                "새 기술과 운영 노하우로 가족회사의 다음 기회를 엽니다.",
                ResourceRoot + "Icons/Bottom/research_v2",
                Feature(
                    "research-tech-tree",
                    "보유 기술",
                    "하청을 하며 쌓은 기술과 숙련도를 확인합니다.",
                    "이용 가능 · 열기",
                    MainNavigationFeatureAction.OpenTechnologyLedger,
                    MainNavigationRouteIds.ResearchTechnology),
                Feature("research-efficiency", "효율 강화", "같은 시간과 자원으로 더 좋은 결과를 만듭니다."),
                Feature("research-marketing", "마케팅 기법", "새로운 고객 획득과 홍보 방식을 해금합니다."),
                Feature("research-lineup", "제품 라인업", "연구 성과로 새로운 제품군을 확장합니다.")),
            new MainNavigationTabDefinition(
                MainNavigationTabId.Investment,
                "investment",
                "투자",
                "회사 자금과 자산을 장기 성장 기회에 배분합니다.",
                ResourceRoot + "Icons/Bottom/investment_v2",
                Feature(
                    "investment-stocks",
                    "주식시장",
                    "기존 회사 증권계좌와 공개 시장 화면으로 들어갑니다.",
                    "이용 가능 · 열기",
                    MainNavigationFeatureAction.OpenStockMarket,
                    MainNavigationRouteIds.StockMarket,
                    ResourceRoot + "Icons/Investment/stock_market_v2"),
                Feature(
                    "investment-loans",
                    "은행·대출",
                    "금리와 상환 능력을 비교해 필요한 자금을 조달합니다.",
                    iconResourcePath: ResourceRoot + "Icons/Investment/bank_loan_v2"),
                Feature(
                    "investment-property",
                    "부동산",
                    "사무실과 수익 자산의 장기 가치를 살펴봅니다.",
                    iconResourcePath: ResourceRoot + "Icons/Investment/real_estate_v2"),
                Feature(
                    "investment-angel",
                    "엔젤 투자",
                    "초기 기업과 기술에 전략적으로 투자합니다.",
                    iconResourcePath: ResourceRoot + "Icons/Investment/angel_v2"),
                Feature(
                    "investment-ma",
                    "M&A",
                    "기업·사업부·기술 자산의 인수 기회를 검토합니다.",
                    iconResourcePath: ResourceRoot + "Icons/Investment/mergers_v2"))
        };

        public static IReadOnlyList<MainNavigationTabDefinition> All { get; } =
            Array.AsReadOnly(Definitions);

        public static MainNavigationTabDefinition Get(MainNavigationTabId tabId)
        {
            for (var index = 0; index < Definitions.Length; index++)
                if (Definitions[index].TabId == tabId) return Definitions[index];
            throw new ArgumentOutOfRangeException(nameof(tabId), tabId, "Unknown main navigation tab.");
        }

        public static void ValidateOrThrow()
        {
            if (Definitions.Length != 5)
                throw new InvalidOperationException($"Exactly five main navigation tabs are required; found {Definitions.Length}.");
            if (Definitions.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != Definitions.Length)
                throw new InvalidOperationException("Main navigation tab IDs must be unique.");
            if (Definitions.Select(item => item.TabId).Distinct().Count() != Definitions.Length)
                throw new InvalidOperationException("Main navigation enum mappings must be unique.");
            foreach (var definition in Definitions)
            {
                if (definition.Features.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != definition.Features.Count)
                    throw new InvalidOperationException($"Feature IDs must be unique within tab '{definition.Id}'.");
                if (definition.Features.Any(item => string.IsNullOrWhiteSpace(item.StatusKo)))
                    throw new InvalidOperationException($"Every feature in tab '{definition.Id}' must expose a Korean status.");
            }
            var allFeatureIds = Definitions.SelectMany(item => item.Features).Select(item => item.Id).ToArray();
            if (allFeatureIds.Distinct(StringComparer.Ordinal).Count() != allFeatureIds.Length)
                throw new InvalidOperationException("Feature IDs must be unique across the entire main navigation catalog.");
            var stockRoutes = Definitions
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.Action == MainNavigationFeatureAction.OpenStockMarket)
                .ToArray();
            if (stockRoutes.Length != 1 || stockRoutes[0].TabId != MainNavigationTabId.Investment ||
                stockRoutes[0].Feature.RouteId != MainNavigationRouteIds.StockMarket)
                throw new InvalidOperationException("Stock market must have one route inside Investment.");
            if (Get(MainNavigationTabId.Investment).Features.Any(feature =>
                    string.IsNullOrWhiteSpace(feature.IconResourcePath)))
                throw new InvalidOperationException("Every Investment feature must reference one canonical V2 icon.");
            var buildingRoutes = Definitions
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == MainNavigationRouteIds.BuildingEditor)
                .ToArray();
            if (buildingRoutes.Length != 1 || buildingRoutes[0].TabId != MainNavigationTabId.Company ||
                buildingRoutes[0].Feature.Action != MainNavigationFeatureAction.OpenBuildingEditor)
                throw new InvalidOperationException("Building editor must consume one Company-hub adapter route.");
            ValidateIntegratedRoute(
                MainNavigationRouteIds.BusinessContracts,
                MainNavigationTabId.Projects,
                MainNavigationFeatureAction.OpenContractBoard,
                "Business contracts");
            ValidateIntegratedRoute(
                MainNavigationRouteIds.BusinessProducts,
                MainNavigationTabId.Projects,
                MainNavigationFeatureAction.OpenProductOpportunities,
                "Business products");
            if (Definitions.SelectMany(item => item.Features).Any(item => item.Action == MainNavigationFeatureAction.None))
                throw new InvalidOperationException("Every visible feature card must expose a mouse/keyboard route.");
        }

        public static IEnumerable<string> EnumerateKoreanText()
        {
            yield return "우리 가족회사";
            yield return "사무실로";
            yield return "준비 중";
            yield return "기술개발 기획 창작 사업 운영 협업 잠재력 현재 상태 체력 스트레스 신뢰 저항 창업 가족 교육";
            foreach (var definition in Definitions)
            {
                yield return definition.DisplayNameKo;
                yield return definition.DescriptionKo;
                foreach (var feature in definition.Features)
                {
                    yield return feature.DisplayNameKo;
                    yield return feature.DescriptionKo;
                    yield return feature.StatusKo;
                }
            }
        }

        private static MainNavigationFeatureDefinition Feature(
            string id,
            string displayNameKo,
            string descriptionKo,
            string statusKo = "준비 중",
            MainNavigationFeatureAction action = MainNavigationFeatureAction.OpenStatus,
            string routeId = "",
            string iconResourcePath = "")
        {
            return new MainNavigationFeatureDefinition(
                id,
                displayNameKo,
                descriptionKo,
                statusKo,
                action,
                routeId,
                iconResourcePath);
        }

        private static void ValidateIntegratedRoute(
            string routeId,
            MainNavigationTabId expectedTab,
            MainNavigationFeatureAction expectedAction,
            string label)
        {
            var matches = Definitions
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == routeId)
                .ToArray();
            if (matches.Length != 1 || matches[0].TabId != expectedTab ||
                matches[0].Feature.Action != expectedAction)
                throw new InvalidOperationException($"{label} must expose one integrated {expectedTab}-hub route.");
        }
    }
}

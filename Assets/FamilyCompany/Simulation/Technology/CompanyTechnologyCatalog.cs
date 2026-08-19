using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>
    /// Which part of the company a technology belongs to. Only used to group the research screen;
    /// a technology is owned by the company as a whole, not by an industry.
    /// </summary>
    public enum CompanyTechnologyTrack
    {
        WebAndSoftware = 0,
        Mobile = 1,
        Hardware = 2,
        RetailAndOffline = 3,
        Shared = 4
    }

    public static class CompanyTechnologyIds
    {
        public const string DotPixelArt = "dot_pixel_art";
        public const string WebPublishing = "web_publishing";
        public const string DataEntry = "data_entry";
        public const string DatabaseDesign = "database_design";
        public const string FlashAnimation = "flash_animation";
        public const string ServerOperations = "server_operations";
        public const string AdminTool = "admin_tool";
        public const string InventorySystem = "inventory_system";

        public const string MidiSound = "midi_sound";
        public const string FeaturePhoneUi = "feature_phone_ui";
        public const string DevicePorting = "device_porting";
        public const string MobileGameLogic = "mobile_game_logic";

        public const string PcAssembly = "pc_assembly";
        public const string BoardAssembly = "board_assembly";
        public const string MediaDuplication = "media_duplication";
        public const string NetworkSetup = "network_setup";
        public const string MapScripting = "map_scripting";

        public const string LogisticsSupply = "logistics_supply";
        public const string EmbroideryPrint = "embroidery_print";
        public const string FlyerDesign = "flyer_design";
        public const string ProductPhotography = "product_photography";

        public const string QualityInspection = "quality_inspection";

        // The three ids that shipped as purchasable research keep their original strings so saved
        // games and the existing product requirements stay valid.
        public const string ThreeDModeling = ResearchTechnologyIds.ThreeDModeling;
        public const string AutomationLine = ResearchTechnologyIds.AutomationLine;
        public const string MarketAnalysis = ResearchTechnologyIds.MarketAnalysis;
    }

    public sealed class CompanyTechnologyDefinition
    {
        public CompanyTechnologyDefinition(
            string technologyId,
            string displayNameKo,
            CompanyTechnologyTrack track,
            string descriptionKo)
        {
            if (string.IsNullOrWhiteSpace(technologyId))
                throw new ArgumentException("Technology ID is required.", nameof(technologyId));
            if (string.IsNullOrWhiteSpace(displayNameKo))
                throw new ArgumentException("Technology display name is required.", nameof(displayNameKo));
            if (!Enum.IsDefined(typeof(CompanyTechnologyTrack), track))
                throw new ArgumentOutOfRangeException(nameof(track));
            TechnologyId = technologyId;
            DisplayNameKo = displayNameKo;
            Track = track;
            DescriptionKo = descriptionKo ?? string.Empty;
        }

        public string TechnologyId { get; }
        public string DisplayNameKo { get; }
        public CompanyTechnologyTrack Track { get; }
        public string DescriptionKo { get; }
    }

    /// <summary>
    /// The technologies the company can build up. Every one of them is taught by at least one
    /// subcontract in <see cref="ContractTechnologyGrantCatalog"/>: doing the work is the way the
    /// company learns, which is the whole point of the subcontract-to-own-product loop.
    /// </summary>
    public static class CompanyTechnologyCatalog
    {
        /// <summary>Points that buy one level. Level 1 starts as soon as any point is earned.</summary>
        public const int PointsPerLevel = 100;

        public const int MaximumLevel = 5;

        /// <summary>Points at which a technology is fully mastered.</summary>
        public const int MasteryPoints = (MaximumLevel - 1) * PointsPerLevel;

        private static readonly CompanyTechnologyDefinition[] Definitions =
        {
            Web(CompanyTechnologyIds.DotPixelArt, "도트 그래픽", "작은 화면용 픽셀 아바타·아이콘·배경을 그립니다."),
            Web(CompanyTechnologyIds.WebPublishing, "웹 퍼블리싱", "홈페이지와 쇼핑몰 화면을 만들고 상품을 등록합니다."),
            Web(CompanyTechnologyIds.DataEntry, "자료 입력·타이핑", "원고와 목록을 빠르고 정확하게 전산으로 옮깁니다."),
            Web(CompanyTechnologyIds.DatabaseDesign, "DB 설계", "자료를 구조로 만들어 검색과 집계가 되게 합니다."),
            Web(CompanyTechnologyIds.FlashAnimation, "플래시 애니메이션", "프레임 단위 채색과 연출로 움직이는 콘텐츠를 만듭니다."),
            Web(CompanyTechnologyIds.ServerOperations, "서버 운영", "24시간 도는 서비스를 감시하고 죽지 않게 유지합니다."),
            Web(CompanyTechnologyIds.AdminTool, "관리자 툴", "가게와 회사의 업무를 대신하는 내부 프로그램을 만듭니다."),
            Web(CompanyTechnologyIds.InventorySystem, "재고 전산", "입출고와 재고를 장부가 아닌 시스템으로 관리합니다."),

            Mobile(CompanyTechnologyIds.MidiSound, "벨소리·사운드", "적은 용량으로 귀에 남는 소리를 만듭니다."),
            Mobile(CompanyTechnologyIds.FeaturePhoneUi, "피처폰 UI", "작은 화면과 키패드에 맞는 메뉴와 아이콘을 설계합니다."),
            Mobile(CompanyTechnologyIds.DevicePorting, "다기종 포팅", "기종마다 다른 해상도와 성능에 같은 제품을 맞춥니다."),
            Mobile(CompanyTechnologyIds.MobileGameLogic, "모바일 게임 로직", "짧고 중독성 있는 플레이 규칙을 구현합니다."),

            Hardware(CompanyTechnologyIds.PcAssembly, "PC 조립·번인", "견적을 짜고 조립한 뒤 장시간 부하로 검증합니다."),
            Hardware(CompanyTechnologyIds.BoardAssembly, "기판·키패드 조립", "기판과 입력장치를 손으로 정밀하게 조립합니다."),
            Hardware(CompanyTechnologyIds.MediaDuplication, "미디어 복제·포장", "CD와 디스켓을 대량 복제하고 상품으로 포장합니다."),
            Hardware(CompanyTechnologyIds.NetworkSetup, "사무 네트워크", "사무실과 PC방의 회선과 장비를 깔고 유지합니다."),
            Hardware(CompanyTechnologyIds.MapScripting, "유즈맵·스크립팅", "기존 게임 위에 규칙과 이벤트를 얹어 콘텐츠를 만듭니다."),

            Retail(CompanyTechnologyIds.LogisticsSupply, "납품·물류", "정기 납품 경로와 재고 회전을 관리합니다."),
            Retail(CompanyTechnologyIds.EmbroideryPrint, "자수·인쇄", "천과 종이에 로고와 도안을 정확히 올립니다."),
            Retail(CompanyTechnologyIds.FlyerDesign, "전단·스티커 디자인", "눈에 띄는 오프라인 홍보물을 짧은 시간에 뽑습니다."),
            Retail(CompanyTechnologyIds.ProductPhotography, "상품 촬영", "팔리는 각도로 찍고 목록에 올릴 수 있게 다듬습니다."),

            Shared(CompanyTechnologyIds.QualityInspection, "품질 검수", "납품 전에 결함을 걸러 반품과 위약을 줄입니다."),
            Shared(CompanyTechnologyIds.ThreeDModeling, "3D 모델링", "3D 에셋과 제품 외형을 설계합니다."),
            Shared(CompanyTechnologyIds.AutomationLine, "자동화 라인", "반복 작업을 도구로 대체해 대형 작업을 감당합니다."),
            Shared(CompanyTechnologyIds.MarketAnalysis, "소비자 시장 분석", "무엇이 팔리는지 읽고 자체 제품을 기획합니다.")
        };

        private static readonly Dictionary<string, CompanyTechnologyDefinition> ById =
            Definitions.ToDictionary(item => item.TechnologyId, StringComparer.Ordinal);

        public static IReadOnlyList<CompanyTechnologyDefinition> All => Definitions;

        public static bool Exists(string technologyId) =>
            !string.IsNullOrEmpty(technologyId) && ById.ContainsKey(technologyId);

        public static CompanyTechnologyDefinition Get(string technologyId)
        {
            if (technologyId != null && ById.TryGetValue(technologyId, out var definition)) return definition;
            throw new KeyNotFoundException($"Unknown company technology: {technologyId}");
        }

        public static IReadOnlyList<CompanyTechnologyDefinition> ForTrack(CompanyTechnologyTrack track) =>
            Definitions.Where(item => item.Track == track).ToArray();

        /// <summary>
        /// Level for an accumulated point total. Zero points means the company has never done this
        /// kind of work; the first point earned puts it at level 1, and every
        /// <see cref="PointsPerLevel"/> after that adds a level up to <see cref="MaximumLevel"/>.
        /// </summary>
        public static int LevelFor(int points)
        {
            if (points <= 0) return 0;
            return Math.Min(MaximumLevel, 1 + points / PointsPerLevel);
        }

        /// <summary>Points earned inside the current level, for a "40/100" style progress readout.</summary>
        public static int PointsIntoLevel(int points)
        {
            if (points <= 0) return 0;
            if (LevelFor(points) >= MaximumLevel) return PointsPerLevel;
            return points % PointsPerLevel;
        }

        public static string DisplayLevelKo(int points)
        {
            var level = LevelFor(points);
            if (level <= 0) return "미습득";
            return level >= MaximumLevel
                ? $"Lv{MaximumLevel} 숙련"
                : $"Lv{level} {PointsIntoLevel(points)}/{PointsPerLevel}";
        }

        private static CompanyTechnologyDefinition Web(string id, string name, string description) =>
            new CompanyTechnologyDefinition(id, name, CompanyTechnologyTrack.WebAndSoftware, description);

        private static CompanyTechnologyDefinition Mobile(string id, string name, string description) =>
            new CompanyTechnologyDefinition(id, name, CompanyTechnologyTrack.Mobile, description);

        private static CompanyTechnologyDefinition Hardware(string id, string name, string description) =>
            new CompanyTechnologyDefinition(id, name, CompanyTechnologyTrack.Hardware, description);

        private static CompanyTechnologyDefinition Retail(string id, string name, string description) =>
            new CompanyTechnologyDefinition(id, name, CompanyTechnologyTrack.RetailAndOffline, description);

        private static CompanyTechnologyDefinition Shared(string id, string name, string description) =>
            new CompanyTechnologyDefinition(id, name, CompanyTechnologyTrack.Shared, description);
    }
}

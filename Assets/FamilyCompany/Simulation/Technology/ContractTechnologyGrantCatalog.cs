using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>One technology a finished subcontract teaches, and how much of it.</summary>
    public readonly struct ContractTechnologyGrant
    {
        public ContractTechnologyGrant(string technologyId, int points)
        {
            if (!CompanyTechnologyCatalog.Exists(technologyId))
                throw new ArgumentException($"Unknown company technology: {technologyId}", nameof(technologyId));
            if (points <= 0) throw new ArgumentOutOfRangeException(nameof(points));
            TechnologyId = technologyId;
            Points = points;
        }

        public string TechnologyId { get; }
        public int Points { get; }

        public string DisplayKo =>
            $"{CompanyTechnologyCatalog.Get(TechnologyId).DisplayNameKo} +{Points}pt";
    }

    /// <summary>
    /// What each of the 21 bootstrap subcontracts teaches, authored per job rather than derived from
    /// a category, so a contract card can state exactly which technology the work builds up.
    ///
    /// Money and technology are separate rewards: the cash is on
    /// <c>SubcontractOffer.RewardWon</c> and never mixes with these points. A player can take a
    /// well-paid job that teaches nothing useful, or a cheap job that opens a product path.
    ///
    /// The grants are indexed by the legacy template index that
    /// <c>BootstrapContractCatalog</c> assigns, which is stable and already used by
    /// <c>LegacyContractTemplateCatalog</c>.
    /// </summary>
    public static class ContractTechnologyGrantCatalog
    {
        private static readonly ContractTechnologyGrant[][] GrantsByTemplateIndex =
        {
            // 0 미니홈피용 64×64 아바타 도트
            Grants((CompanyTechnologyIds.DotPixelArt, 40), (CompanyTechnologyIds.WebPublishing, 10)),
            // 1 이모티콘 인터넷 소설 타이핑
            Grants((CompanyTechnologyIds.DataEntry, 45), (CompanyTechnologyIds.QualityInspection, 10)),
            // 2 타자 연습 프로그램 단어 DB 입력
            Grants((CompanyTechnologyIds.DatabaseDesign, 40), (CompanyTechnologyIds.DataEntry, 15)),
            // 3 엽기 플래시 애니메이션 프레임 채색
            Grants((CompanyTechnologyIds.FlashAnimation, 55), (CompanyTechnologyIds.DotPixelArt, 15)),
            // 4 초창기 P2P 서버 야간 모니터링
            Grants((CompanyTechnologyIds.ServerOperations, 70), (CompanyTechnologyIds.NetworkSetup, 20),
                (CompanyTechnologyIds.AutomationLine, 25)),
            // 5 쇼핑몰용 3D 회전 상품 이미지
            Grants((CompanyTechnologyIds.ThreeDModeling, 60), (CompanyTechnologyIds.ProductPhotography, 20)),

            // 6 64화음 벨소리 MIDI 입력
            Grants((CompanyTechnologyIds.MidiSound, 40), (CompanyTechnologyIds.DataEntry, 10)),
            // 7 문자 메시지 도트 배경화면
            Grants((CompanyTechnologyIds.DotPixelArt, 35), (CompanyTechnologyIds.FeaturePhoneUi, 15)),
            // 8 피처폰 천지인 키패드 고무판 조립
            Grants((CompanyTechnologyIds.BoardAssembly, 45), (CompanyTechnologyIds.QualityInspection, 15)),
            // 9 컬러폰 메뉴 아이콘 도트 변환
            Grants((CompanyTechnologyIds.FeaturePhoneUi, 50), (CompanyTechnologyIds.DotPixelArt, 20)),
            // 10 모바일 맞고·타이쿤 다기종 포팅
            Grants((CompanyTechnologyIds.DevicePorting, 70), (CompanyTechnologyIds.MobileGameLogic, 30),
                (CompanyTechnologyIds.AutomationLine, 15)),

            // 11 교육용 CD·디스켓 복제와 포장
            Grants((CompanyTechnologyIds.MediaDuplication, 40), (CompanyTechnologyIds.QualityInspection, 10)),
            // 12 PC방 재떨이·컵라면 정기 납품
            Grants((CompanyTechnologyIds.LogisticsSupply, 45), (CompanyTechnologyIds.NetworkSetup, 10)),
            // 13 게임 방송 이벤트용 RTS 유즈맵 제작
            Grants((CompanyTechnologyIds.MapScripting, 55), (CompanyTechnologyIds.MobileGameLogic, 15)),
            // 14 조립 PC 견적·24시간 번인 테스트
            Grants((CompanyTechnologyIds.PcAssembly, 55), (CompanyTechnologyIds.QualityInspection, 20)),
            // 15 MP3 플레이어 메인보드 조립·용량 검수
            Grants((CompanyTechnologyIds.BoardAssembly, 65), (CompanyTechnologyIds.QualityInspection, 25),
                (CompanyTechnologyIds.AutomationLine, 20)),

            // 16 오버핏 의류 브랜드 로고 자수
            Grants((CompanyTechnologyIds.EmbroideryPrint, 45), (CompanyTechnologyIds.DotPixelArt, 10)),
            // 17 야식·휴대폰 전단지와 스티커 제작
            Grants((CompanyTechnologyIds.FlyerDesign, 40), (CompanyTechnologyIds.EmbroideryPrint, 10)),
            // 18 비디오·만화책 대여점 연체 관리
            Grants((CompanyTechnologyIds.AdminTool, 55), (CompanyTechnologyIds.DatabaseDesign, 20)),
            // 19 동대문 도매 의류 재고 전산화
            Grants((CompanyTechnologyIds.InventorySystem, 55), (CompanyTechnologyIds.DatabaseDesign, 25)),
            // 20 초기 인터넷 쇼핑몰 상품 촬영·등록
            Grants((CompanyTechnologyIds.ProductPhotography, 55), (CompanyTechnologyIds.WebPublishing, 25),
                (CompanyTechnologyIds.MarketAnalysis, 20))
        };

        public static int TemplateCount => GrantsByTemplateIndex.Length;

        public static IReadOnlyList<ContractTechnologyGrant> ForTemplateIndex(int legacyGlobalIndex)
        {
            if (legacyGlobalIndex < 0 || legacyGlobalIndex >= GrantsByTemplateIndex.Length)
                return Array.Empty<ContractTechnologyGrant>();
            return GrantsByTemplateIndex[legacyGlobalIndex];
        }

        /// <summary>
        /// Grant line for a contract card, e.g. <c>DB 설계 +40pt · 자료 입력 +15pt</c>.
        /// </summary>
        public static string DisplayKo(int legacyGlobalIndex)
        {
            var grants = ForTemplateIndex(legacyGlobalIndex);
            return grants.Count == 0
                ? "기술 습득 없음"
                : string.Join(" · ", grants.Select(item => item.DisplayKo));
        }

        /// <summary>Every technology reachable through subcontract work, for validation.</summary>
        public static IReadOnlyCollection<string> TaughtTechnologyIds =>
            GrantsByTemplateIndex
                .SelectMany(item => item)
                .Select(item => item.TechnologyId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        private static ContractTechnologyGrant[] Grants(params (string TechnologyId, int Points)[] entries)
        {
            var grants = entries
                .Select(item => new ContractTechnologyGrant(item.TechnologyId, item.Points))
                .ToArray();
            if (grants.Select(item => item.TechnologyId).Distinct(StringComparer.Ordinal).Count() != grants.Length)
                throw new InvalidOperationException("A subcontract cannot grant the same technology twice.");
            return grants;
        }
    }
}

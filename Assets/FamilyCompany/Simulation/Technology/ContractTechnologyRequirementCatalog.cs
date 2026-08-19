using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>
    /// The proven experience a client asks for before handing over the harder jobs. This is the other
    /// half of <see cref="ContractTechnologyGrantCatalog"/>: easy work teaches a technology, and the
    /// technology is what opens the work that pays more.
    ///
    /// Starter jobs are deliberately left open so a company with nothing can always earn. Only the
    /// mid and upper jobs are gated, and every gate is reachable from the jobs that stay open.
    /// <c>PrototypeValidation</c> proves that reachability rather than trusting this table.
    /// </summary>
    public static class ContractTechnologyRequirementCatalog
    {
        private static readonly Dictionary<int, TechnologyLevelRequirement[]> RequirementsByTemplateIndex =
            new Dictionary<int, TechnologyLevelRequirement[]>
            {
                // 3 엽기 플래시 애니메이션 프레임 채색 — 아바타·배경 도트를 그려 본 손이 필요하다.
                { 3, Require((CompanyTechnologyIds.DotPixelArt, 2)) },
                // 4 초창기 P2P 서버 야간 모니터링 — 회선을 만져 본 적이 있어야 맡긴다.
                { 4, Require((CompanyTechnologyIds.NetworkSetup, 1)) },
                // 5 쇼핑몰용 3D 회전 상품 이미지
                { 5, Require((CompanyTechnologyIds.DotPixelArt, 2)) },
                // 9 컬러폰 메뉴 아이콘 도트 변환
                { 9, Require((CompanyTechnologyIds.DotPixelArt, 2)) },
                // 10 모바일 맞고·타이쿤 다기종 포팅 — 피처폰 화면을 다뤄 본 실적을 본다.
                { 10, Require((CompanyTechnologyIds.FeaturePhoneUi, 2)) },
                // 13 게임 방송 이벤트용 RTS 유즈맵 제작
                { 13, Require((CompanyTechnologyIds.MobileGameLogic, 1)) },
                // 14 조립 PC 견적·24시간 번인 테스트 — 검수 이력이 있어야 번인을 맡긴다.
                { 14, Require((CompanyTechnologyIds.QualityInspection, 1)) },
                // 15 MP3 플레이어 메인보드 조립·용량 검수
                { 15, Require((CompanyTechnologyIds.BoardAssembly, 2), (CompanyTechnologyIds.QualityInspection, 1)) },
                // 18 비디오·만화책 대여점 연체 관리
                { 18, Require((CompanyTechnologyIds.DatabaseDesign, 1)) },
                // 19 동대문 도매 의류 재고 전산화
                { 19, Require((CompanyTechnologyIds.DatabaseDesign, 2)) },
                // 20 초기 인터넷 쇼핑몰 상품 촬영·등록
                { 20, Require((CompanyTechnologyIds.WebPublishing, 1)) }
            };

        public static IReadOnlyList<TechnologyLevelRequirement> ForTemplateIndex(int legacyGlobalIndex)
        {
            return RequirementsByTemplateIndex.TryGetValue(legacyGlobalIndex, out var requirements)
                ? requirements
                : Array.Empty<TechnologyLevelRequirement>();
        }

        /// <summary>Template indices that any company can take from day one.</summary>
        public static bool IsOpenToEveryone(int legacyGlobalIndex) =>
            !RequirementsByTemplateIndex.ContainsKey(legacyGlobalIndex);

        public static IReadOnlyCollection<int> GatedTemplateIndices => RequirementsByTemplateIndex.Keys;

        private static TechnologyLevelRequirement[] Require(
            params (string TechnologyId, int Level)[] entries)
        {
            var requirements = entries
                .Select(item => new TechnologyLevelRequirement(item.TechnologyId, item.Level))
                .ToArray();
            if (requirements.Select(item => item.TechnologyId).Distinct(StringComparer.Ordinal).Count() != requirements.Length)
                throw new InvalidOperationException("A subcontract cannot require the same technology twice.");
            return requirements;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Company
{
    public enum BusinessIndustry
    {
        WebAndSoftware = 0,
        FeaturePhoneAndMobile = 1,
        HardwareAndPc = 2,
        FashionRetailAndOffline = 3
    }

    public sealed class BusinessIndustryDefinition
    {
        public BusinessIndustryDefinition(
            BusinessIndustry industry,
            string displayName,
            string ownBusinessName,
            string description,
            string[] recruitableRoles,
            string[] starterExamples)
        {
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            Industry = industry;
            DisplayName = displayName ?? string.Empty;
            OwnBusinessName = ownBusinessName ?? string.Empty;
            Description = description ?? string.Empty;
            RecruitableRoles = recruitableRoles ?? Array.Empty<string>();
            StarterExamples = starterExamples ?? Array.Empty<string>();
        }

        public BusinessIndustry Industry { get; }
        public string DisplayName { get; }
        public string OwnBusinessName { get; }
        public string Description { get; }
        public IReadOnlyList<string> RecruitableRoles { get; }
        public IReadOnlyList<string> StarterExamples { get; }
    }

    public static class BusinessIndustryCatalog
    {
        private static readonly BusinessIndustryDefinition[] Definitions =
        {
            new BusinessIndustryDefinition(
                BusinessIndustry.WebAndSoftware,
                "웹 & 소프트웨어",
                "우리집 닷컴 스튜디오",
                "초고속 인터넷과 개인 홈페이지 붐을 타는 가장 빠른 벤처 분야",
                new[] { "프로그래머", "웹 디자이너", "도트 아티스트", "서비스 기획자" },
                new[] { "64×64 아바타 도트", "인터넷 소설 타이핑", "플래시 애니메이션" }),
            new BusinessIndustryDefinition(
                BusinessIndustry.FeaturePhoneAndMobile,
                "피처폰 & 모바일",
                "포켓 모바일 랩",
                "64화음·컬러 화면·기종별 포팅이 돈이 되던 WIPI 이전 모바일 시장",
                new[] { "모바일 프로그래머", "MIDI 작곡가", "도트 디자이너", "단말 QA" },
                new[] { "64화음 벨소리", "문자 배경화면", "폴더폰 게임 포팅" }),
            new BusinessIndustryDefinition(
                BusinessIndustry.HardwareAndPc,
                "하드웨어 & PC",
                "밀레니엄 디지털 공방",
                "PC방·MP3·CD 패키지가 성장하던 용산식 조립·유통·유지보수 분야",
                new[] { "전자 엔지니어", "생산직", "PC 정비사", "품질관리자" },
                new[] { "CD 패키징", "PC방 유지보수", "MP3 보드 검수" }),
            new BusinessIndustryDefinition(
                BusinessIndustry.FashionRetailAndOffline,
                "패션·유통 & 오프라인",
                "패밀리 스트리트 컴퍼니",
                "밀레니엄 스트릿 패션과 동네 상권을 연결하는 제조·디자인·유통 분야",
                new[] { "패션 디자이너", "생산관리자", "인쇄 디자이너", "마케터" },
                new[] { "오버핏 의류 자수", "배달 전단지", "대여점 관리 시스템" })
        };

        public static IReadOnlyList<BusinessIndustryDefinition> All => Definitions;

        public static BusinessIndustryDefinition Get(BusinessIndustry industry)
        {
            var definition = Definitions.FirstOrDefault(item => item.Industry == industry);
            if (definition == null) throw new KeyNotFoundException($"Unknown industry: {industry}");
            return definition;
        }
    }
}

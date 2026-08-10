using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Leisure
{
    [Flags]
    public enum LeisureDayMask
    {
        None = 0,
        Monday = 1 << 0,
        Tuesday = 1 << 1,
        Wednesday = 1 << 2,
        Thursday = 1 << 3,
        Friday = 1 << 4,
        Saturday = 1 << 5,
        Sunday = 1 << 6,
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekend = Saturday | Sunday,
        EveryDay = Weekdays | Weekend
    }

    public enum LeisureActivityCategory
    {
        Snack = 0,
        Digital = 1,
        Rental = 2,
        Bath = 3,
        Meal = 4,
        Outdoors = 5,
        Neighborhood = 6,
        Home = 7,
        Music = 8
    }

    public sealed class LeisureActivityDefinition
    {
        public LeisureActivityDefinition(
            string id,
            string titleKo,
            string descriptionKo,
            LeisureActivityCategory category,
            long costWon,
            int durationMinutes,
            int energyDelta,
            int stressDelta,
            int sharedFamilyBondDelta,
            int minimumYear,
            int maximumYearInclusive,
            LeisureDayMask allowedDays,
            int minimumParticipants,
            int maximumParticipants)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Activity ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKo))
                throw new ArgumentException("Korean title is required.", nameof(titleKo));
            if (string.IsNullOrWhiteSpace(descriptionKo))
                throw new ArgumentException("Korean description is required.", nameof(descriptionKo));
            if (!Enum.IsDefined(typeof(LeisureActivityCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category));
            if (costWon < 0) throw new ArgumentOutOfRangeException(nameof(costWon));
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            ValidateDelta(energyDelta, nameof(energyDelta));
            ValidateDelta(stressDelta, nameof(stressDelta));
            ValidateDelta(sharedFamilyBondDelta, nameof(sharedFamilyBondDelta));
            if (minimumYear < 1 || minimumYear > DateTime.MaxValue.Year)
                throw new ArgumentOutOfRangeException(nameof(minimumYear));
            if (maximumYearInclusive < minimumYear || maximumYearInclusive > DateTime.MaxValue.Year)
                throw new ArgumentOutOfRangeException(nameof(maximumYearInclusive));
            if (allowedDays == LeisureDayMask.None || (allowedDays & ~LeisureDayMask.EveryDay) != 0)
                throw new ArgumentOutOfRangeException(nameof(allowedDays));
            if (minimumParticipants < 1 || minimumParticipants > 4)
                throw new ArgumentOutOfRangeException(nameof(minimumParticipants));
            if (maximumParticipants < minimumParticipants || maximumParticipants > 4)
                throw new ArgumentOutOfRangeException(nameof(maximumParticipants));

            Id = id.Trim();
            TitleKo = titleKo.Trim();
            DescriptionKo = descriptionKo.Trim();
            Category = category;
            CostWon = costWon;
            DurationMinutes = durationMinutes;
            EnergyDelta = energyDelta;
            StressDelta = stressDelta;
            SharedFamilyBondDelta = sharedFamilyBondDelta;
            MinimumYear = minimumYear;
            MaximumYearInclusive = maximumYearInclusive;
            AllowedDays = allowedDays;
            MinimumParticipants = minimumParticipants;
            MaximumParticipants = maximumParticipants;
        }

        public string Id { get; }
        public string TitleKo { get; }
        public string DescriptionKo { get; }
        public LeisureActivityCategory Category { get; }
        public long CostWon { get; }
        public int DurationMinutes { get; }
        public int EnergyDelta { get; }
        public int StressDelta { get; }

        // This value is applied only when two or more family members participate.
        public int SharedFamilyBondDelta { get; }

        public int MinimumYear { get; }
        public int MaximumYearInclusive { get; }
        public LeisureDayMask AllowedDays { get; }
        public int MinimumParticipants { get; }
        public int MaximumParticipants { get; }

        public bool IsAvailableOn(DateTime at, int participantCount)
        {
            if (participantCount < MinimumParticipants || participantCount > MaximumParticipants)
                return false;
            if (at.Year < MinimumYear || at.Year > MaximumYearInclusive)
                return false;
            return (AllowedDays & MaskFor(at.DayOfWeek)) != 0;
        }

        public static LeisureDayMask MaskFor(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: return LeisureDayMask.Monday;
                case DayOfWeek.Tuesday: return LeisureDayMask.Tuesday;
                case DayOfWeek.Wednesday: return LeisureDayMask.Wednesday;
                case DayOfWeek.Thursday: return LeisureDayMask.Thursday;
                case DayOfWeek.Friday: return LeisureDayMask.Friday;
                case DayOfWeek.Saturday: return LeisureDayMask.Saturday;
                case DayOfWeek.Sunday: return LeisureDayMask.Sunday;
                default: throw new ArgumentOutOfRangeException(nameof(dayOfWeek));
            }
        }

        private static void ValidateDelta(int value, string parameterName)
        {
            if (value < -100 || value > 100)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public static class LeisureActivityCatalog
    {
        public const int CampaignFirstYear = 2000;
        public const int CampaignLastYear = 2026;

        private static readonly IReadOnlyList<LeisureActivityDefinition> Definitions =
            Array.AsReadOnly(new[]
            {
                new LeisureActivityDefinition(
                    "convenience_store_snack_run",
                    "편의점 간식 한 봉지",
                    "컵라면과 삼각김밥, 작은 음료를 골라 사무실에서 함께 먹는다.",
                    LeisureActivityCategory.Snack,
                    4_000,
                    45,
                    7,
                    -10,
                    2,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.EveryDay,
                    1,
                    4),
                new LeisureActivityDefinition(
                    "pc_bang_team_match",
                    "동네 PC방 한 판",
                    "두꺼운 CRT 모니터 앞에 나란히 앉아 전략 게임 한 판으로 머리를 식힌다.",
                    LeisureActivityCategory.Digital,
                    6_000,
                    90,
                    -4,
                    -16,
                    2,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Friday | LeisureDayMask.Weekend,
                    1,
                    4),
                new LeisureActivityDefinition(
                    "video_tape_rental_night",
                    "비디오테이프 대여",
                    "동네 비디오 가게에서 한 편을 빌려 과자를 놓고 거실 상영회를 연다.",
                    LeisureActivityCategory.Rental,
                    3_500,
                    120,
                    4,
                    -15,
                    3,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Friday | LeisureDayMask.Weekend,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "comic_book_rental_stack",
                    "만화책 대여점 들르기",
                    "연재 단행본 몇 권을 빌려 소파와 휴게실에서 조용히 돌려 읽는다.",
                    LeisureActivityCategory.Rental,
                    2_500,
                    75,
                    5,
                    -12,
                    1,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.EveryDay,
                    1,
                    4),
                new LeisureActivityDefinition(
                    "neighborhood_public_bath",
                    "동네 목욕탕",
                    "뜨거운 탕에 몸을 풀고 바나나우유를 나눠 마시며 한 주의 피로를 턴다.",
                    LeisureActivityCategory.Bath,
                    12_000,
                    90,
                    16,
                    -22,
                    3,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Weekend,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "family_restaurant_dinner",
                    "가족 외식",
                    "동네 기사식당이나 분식집에서 메뉴를 하나씩 고르고 회사 이야기는 잠시 내려놓는다.",
                    LeisureActivityCategory.Meal,
                    32_000,
                    100,
                    14,
                    -18,
                    5,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Friday | LeisureDayMask.Weekend,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "neighborhood_evening_walk",
                    "동네 저녁 산책",
                    "오피스텔 골목과 작은 공원을 천천히 걸으며 막힌 생각을 말로 정리한다.",
                    LeisureActivityCategory.Outdoors,
                    0,
                    60,
                    5,
                    -10,
                    2,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.EveryDay,
                    1,
                    4),
                new LeisureActivityDefinition(
                    "riverside_picnic",
                    "강변 돗자리 소풍",
                    "김밥과 보온병을 챙겨 강변에 앉고, 마감과 전화가 없는 오후를 보낸다.",
                    LeisureActivityCategory.Outdoors,
                    8_000,
                    150,
                    10,
                    -20,
                    4,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Weekend,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "stationery_arcade_break",
                    "문방구 오락기",
                    "동전을 나눠 쥐고 짧은 대전 게임과 뽑기 한 번으로 승부욕을 가볍게 턴다.",
                    LeisureActivityCategory.Neighborhood,
                    2_000,
                    45,
                    -2,
                    -11,
                    1,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.Friday | LeisureDayMask.Weekend,
                    1,
                    4),
                new LeisureActivityDefinition(
                    "home_radio_snack_chat",
                    "라디오와 야식 수다",
                    "카세트 라디오를 작게 틀고 집에 있는 간식을 꺼내 서로의 한 주를 듣는다.",
                    LeisureActivityCategory.Home,
                    5_000,
                    90,
                    8,
                    -14,
                    4,
                    2000,
                    CampaignLastYear,
                    LeisureDayMask.EveryDay,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "family_singing_room",
                    "가족 노래방",
                    "한 시간짜리 작은 방을 잡고 서로의 애창곡을 들으며 마이크를 돌린다.",
                    LeisureActivityCategory.Music,
                    20_000,
                    90,
                    -2,
                    -20,
                    4,
                    2001,
                    CampaignLastYear,
                    LeisureDayMask.Friday | LeisureDayMask.Weekend,
                    2,
                    4),
                new LeisureActivityDefinition(
                    "adsl_coop_game_night",
                    "ADSL 협동 게임 밤",
                    "전화선 걱정 없이 사무실 PC를 연결해 짧은 협동 게임과 간식을 즐긴다.",
                    LeisureActivityCategory.Digital,
                    8_000,
                    120,
                    -6,
                    -21,
                    3,
                    2002,
                    CampaignLastYear,
                    LeisureDayMask.Weekend,
                    2,
                    4)
            });

        public static IReadOnlyList<LeisureActivityDefinition> All => Definitions;

        public static LeisureActivityDefinition FindById(string activityId)
        {
            if (string.IsNullOrWhiteSpace(activityId)) return null;
            return Definitions.FirstOrDefault(
                item => string.Equals(item.Id, activityId, StringComparison.Ordinal));
        }

        // Presentation code should use this filtered view so later-year activities are not exposed early.
        public static IReadOnlyList<LeisureActivityDefinition> AvailableOn(
            DateTime at,
            int participantCount)
        {
            if (participantCount < 1 || participantCount > 4)
                throw new ArgumentOutOfRangeException(nameof(participantCount));

            return Definitions
                .Where(item => item.IsAvailableOn(at, participantCount))
                .ToArray();
        }
    }
}

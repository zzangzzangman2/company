using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Leisure
{
    public enum LeisureRelationshipTag
    {
        Recovery = 0,
        EverydayJoy = 1,
        Play = 2,
        FriendlyCompetition = 3,
        QuietRest = 4,
        Nostalgia = 5,
        Comfort = 6,
        Conversation = 7,
        Outdoors = 8,
        Celebration = 9,
        Cooperation = 10,
        SharedTime = 11,
        BondStrengthened = 12
    }

    public sealed class LeisureMemoryResult
    {
        internal LeisureMemoryResult(
            string memoryId,
            string activityId,
            long elapsedMinute,
            IReadOnlyList<string> participantFamilyIds,
            int appliedBondDelta,
            string summaryKo,
            IReadOnlyList<LeisureRelationshipTag> relationshipTags,
            int recallImportance,
            IReadOnlyList<string> phraseVariantsKo)
        {
            MemoryId = memoryId ?? throw new ArgumentNullException(nameof(memoryId));
            ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            ElapsedMinute = elapsedMinute;
            ParticipantFamilyIds = participantFamilyIds ?? throw new ArgumentNullException(nameof(participantFamilyIds));
            AppliedBondDelta = appliedBondDelta;
            SummaryKo = summaryKo ?? throw new ArgumentNullException(nameof(summaryKo));
            RelationshipTags = relationshipTags ?? throw new ArgumentNullException(nameof(relationshipTags));
            RecallImportance = recallImportance;
            PhraseVariantsKo = phraseVariantsKo ?? throw new ArgumentNullException(nameof(phraseVariantsKo));
        }

        public string MemoryId { get; }
        public string ActivityId { get; }
        public long ElapsedMinute { get; }
        public IReadOnlyList<string> ParticipantFamilyIds { get; }
        public int AppliedBondDelta { get; }
        public string SummaryKo { get; }
        public IReadOnlyList<LeisureRelationshipTag> RelationshipTags { get; }
        public int RecallImportance { get; }
        public IReadOnlyList<string> PhraseVariantsKo { get; }
    }

    public static class LeisureMemoryRules
    {
        private const string MemoryVersion = "leisure-memory-v1";

        private static readonly IReadOnlyList<MemoryTemplate> TemplateDefinitions =
            Array.AsReadOnly(new[]
            {
                Template(
                    "convenience_store_snack_run",
                    35,
                    new[] { LeisureRelationshipTag.Recovery, LeisureRelationshipTag.EverydayJoy },
                    "컵라면과 삼각김밥을 고르며 잠시 숨을 돌렸다.",
                    "편의점 간식 봉지를 열고 바쁜 마음을 내려놓았다.",
                    "작은 음료와 간식으로 짧지만 든든하게 쉬었다."),
                Template(
                    "pc_bang_team_match",
                    38,
                    new[] { LeisureRelationshipTag.Play, LeisureRelationshipTag.FriendlyCompetition },
                    "두꺼운 CRT 앞에서 전략 게임 한 판을 즐겼다.",
                    "PC방 키보드 소리 속에서 승부욕을 가볍게 풀었다.",
                    "나란한 모니터 앞에서 짧은 팀 경기를 마쳤다."),
                Template(
                    "video_tape_rental_night",
                    42,
                    new[] { LeisureRelationshipTag.QuietRest, LeisureRelationshipTag.Nostalgia },
                    "빌린 비디오 한 편과 과자로 거실 상영회를 즐겼다.",
                    "비디오테이프를 고르고 편안한 상영 시간을 보냈다.",
                    "작은 화면 앞에 모여 영화 한 편을 끝까지 보았다."),
                Template(
                    "comic_book_rental_stack",
                    36,
                    new[] { LeisureRelationshipTag.QuietRest, LeisureRelationshipTag.Nostalgia },
                    "빌린 만화책을 넘기며 조용히 쉬었다.",
                    "단행본 몇 권을 골라 편안한 독서 시간을 보냈다.",
                    "소파에 기대어 만화 속 이야기에 잠시 빠졌다."),
                Template(
                    "neighborhood_public_bath",
                    48,
                    new[] { LeisureRelationshipTag.Recovery, LeisureRelationshipTag.Comfort },
                    "뜨거운 탕에서 피로를 풀고 바나나우유를 마셨다.",
                    "동네 목욕탕의 따뜻한 물로 지친 몸을 쉬게 했다.",
                    "목욕을 마친 뒤 개운한 기분으로 한숨 돌렸다."),
                Template(
                    "family_restaurant_dinner",
                    55,
                    new[] { LeisureRelationshipTag.Conversation, LeisureRelationshipTag.EverydayJoy },
                    "맛있는 한 끼를 고르며 회사 걱정을 잠시 내려놓았다.",
                    "동네 식당에 둘러앉아 편안한 식사를 나눴다.",
                    "각자 고른 메뉴를 먹으며 느긋하게 이야기를 나눴다."),
                Template(
                    "neighborhood_evening_walk",
                    40,
                    new[] { LeisureRelationshipTag.Outdoors, LeisureRelationshipTag.Conversation },
                    "저녁 골목을 천천히 걸으며 복잡한 생각을 풀었다.",
                    "작은 공원을 거닐며 차분하게 숨을 골랐다.",
                    "오피스텔 주변을 산책하며 막힌 마음을 정리했다."),
                Template(
                    "riverside_picnic",
                    54,
                    new[] { LeisureRelationshipTag.Outdoors, LeisureRelationshipTag.Recovery },
                    "강변 돗자리에서 김밥을 먹으며 느긋한 오후를 보냈다.",
                    "보온병과 간식을 펼쳐 놓고 강바람을 즐겼다.",
                    "전화와 마감을 내려놓고 강변에서 푹 쉬었다."),
                Template(
                    "stationery_arcade_break",
                    34,
                    new[] { LeisureRelationshipTag.Play, LeisureRelationshipTag.FriendlyCompetition },
                    "문방구 오락기 앞에서 짧고 신나는 승부를 즐겼다.",
                    "동전 한 닢으로 대전 게임 한 판을 마쳤다.",
                    "작은 오락기 버튼을 두드리며 가볍게 웃었다."),
                Template(
                    "home_radio_snack_chat",
                    50,
                    new[] { LeisureRelationshipTag.Conversation, LeisureRelationshipTag.QuietRest },
                    "작은 라디오를 틀고 야식을 먹으며 이야기를 나눴다.",
                    "카세트 라디오 소리 곁에서 서로의 한 주를 들었다.",
                    "집에 있던 간식을 꺼내 놓고 편안하게 수다를 떨었다."),
                Template(
                    "family_singing_room",
                    52,
                    new[] { LeisureRelationshipTag.Play, LeisureRelationshipTag.Celebration },
                    "작은 노래방에서 애창곡을 부르며 마음껏 웃었다.",
                    "마이크를 돌려 가며 신나는 노래 시간을 보냈다.",
                    "서로의 노래를 듣고 박수치며 스트레스를 풀었다."),
                Template(
                    "adsl_coop_game_night",
                    46,
                    new[] { LeisureRelationshipTag.Play, LeisureRelationshipTag.Cooperation },
                    "연결된 PC 앞에서 협동 게임 한 판을 완주했다.",
                    "ADSL로 이어진 게임에서 힘을 합쳐 목표를 끝냈다.",
                    "간식을 곁에 두고 짧은 협동 게임 밤을 즐겼다.")
            });

        private static readonly IReadOnlyDictionary<string, MemoryTemplate> Templates = BuildTemplateIndex();
        private static readonly IReadOnlyList<string> ActivityIds = BuildActivityIds();

        public static IReadOnlyList<string> SupportedActivityIds => ActivityIds;

        /// <summary>
        /// Creates meaning data only after an activity has completed. The caller supplies
        /// the actually applied bond delta; this method never predicts later activities.
        /// </summary>
        public static LeisureMemoryResult CreateCompletedMemory(
            string activityId,
            long elapsedMinute,
            IEnumerable<string> participantFamilyIds,
            int appliedBondDelta,
            int worldSeed)
        {
            if (string.IsNullOrWhiteSpace(activityId))
                throw new ArgumentException("Activity ID is required.", nameof(activityId));
            if (elapsedMinute < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            if (appliedBondDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(appliedBondDelta));

            var normalizedActivityId = activityId.Trim();
            if (!Templates.TryGetValue(normalizedActivityId, out var template))
                throw new ArgumentException("The activity is not a completed canonical leisure activity.", nameof(activityId));

            var activity = LeisureActivityCatalog.FindById(normalizedActivityId);
            if (activity == null)
                throw new InvalidOperationException("Memory template has no canonical leisure activity.");

            var participants = NormalizeParticipants(participantFamilyIds);
            if (participants.Count < activity.MinimumParticipants || participants.Count > activity.MaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(participantFamilyIds));
            if (participants.Count < 2 && appliedBondDelta != 0)
                throw new ArgumentException("Solo activities cannot apply a shared bond change.", nameof(appliedBondDelta));
            if (appliedBondDelta > activity.SharedFamilyBondDelta)
                throw new ArgumentOutOfRangeException(nameof(appliedBondDelta));

            var occurredAt = new GameTime(elapsedMinute).Now;
            if (occurredAt.Year < activity.MinimumYear || occurredAt.Year > activity.MaximumYearInclusive)
                throw new ArgumentException("The activity is unavailable at the supplied elapsed minute.", nameof(elapsedMinute));

            var participantKey = string.Join(
                "|",
                participants.Select(id => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}",
                    id.Length,
                    id)));
            var canonicalKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}",
                MemoryVersion,
                worldSeed,
                elapsedMinute,
                normalizedActivityId,
                appliedBondDelta,
                participantKey);

            var primaryHash = StableRandom.StableHash31(canonicalKey);
            var secondaryHash = StableRandom.StableRandomWord31(canonicalKey, 1);
            var memoryId = string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:D12}:{2}:{3:X8}{4:X8}",
                MemoryVersion,
                elapsedMinute,
                normalizedActivityId,
                primaryHash,
                secondaryHash);

            var phraseStart = StableRandom.StableRandomInt(canonicalKey + "|phrases", template.PhrasesKo.Count);
            var phrases = new string[template.PhrasesKo.Count];
            for (var phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
            {
                phrases[phraseIndex] = template.PhrasesKo[(phraseStart + phraseIndex) % template.PhrasesKo.Count];
            }

            var tags = new List<LeisureRelationshipTag>(template.BaseTags);
            if (participants.Count >= 2)
            {
                tags.Add(LeisureRelationshipTag.SharedTime);
            }

            if (appliedBondDelta > 0)
            {
                tags.Add(LeisureRelationshipTag.BondStrengthened);
            }

            var importance = Math.Min(
                100,
                template.BaseImportance + (participants.Count - 1) * 5 + Math.Min(40, appliedBondDelta * 4));

            return new LeisureMemoryResult(
                memoryId,
                normalizedActivityId,
                elapsedMinute,
                Array.AsReadOnly(participants.ToArray()),
                appliedBondDelta,
                phrases[0],
                Array.AsReadOnly(tags.Distinct().ToArray()),
                importance,
                Array.AsReadOnly(phrases));
        }

        private static IReadOnlyList<string> NormalizeParticipants(IEnumerable<string> participantFamilyIds)
        {
            if (participantFamilyIds == null)
                throw new ArgumentNullException(nameof(participantFamilyIds));

            var participants = participantFamilyIds
                .Select(id => id == null ? string.Empty : id.Trim())
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (participants.Length < 1 || participants.Length > 4)
                throw new ArgumentOutOfRangeException(
                    nameof(participantFamilyIds),
                    "One to four distinct family participant IDs are required.");
            return participants;
        }

        private static MemoryTemplate Template(
            string activityId,
            int baseImportance,
            IReadOnlyList<LeisureRelationshipTag> baseTags,
            params string[] phrasesKo)
        {
            return new MemoryTemplate(activityId, baseImportance, baseTags, phrasesKo);
        }

        private static IReadOnlyDictionary<string, MemoryTemplate> BuildTemplateIndex()
        {
            var index = new Dictionary<string, MemoryTemplate>(StringComparer.Ordinal);
            foreach (var template in TemplateDefinitions)
            {
                index.Add(template.ActivityId, template);
            }

            return index;
        }

        private static IReadOnlyList<string> BuildActivityIds()
        {
            return Array.AsReadOnly(TemplateDefinitions.Select(template => template.ActivityId).ToArray());
        }

        private sealed class MemoryTemplate
        {
            public MemoryTemplate(
                string activityId,
                int baseImportance,
                IReadOnlyList<LeisureRelationshipTag> baseTags,
                IReadOnlyList<string> phrasesKo)
            {
                if (string.IsNullOrWhiteSpace(activityId)) throw new ArgumentException(nameof(activityId));
                if (baseImportance < 0 || baseImportance > 100) throw new ArgumentOutOfRangeException(nameof(baseImportance));
                if (baseTags == null || baseTags.Count < 1) throw new ArgumentException(nameof(baseTags));
                if (phrasesKo == null || phrasesKo.Count < 2 || phrasesKo.Count > 3)
                    throw new ArgumentException("Two or three phrases are required.", nameof(phrasesKo));
                if (phrasesKo.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException(nameof(phrasesKo));

                ActivityId = activityId;
                BaseImportance = baseImportance;
                BaseTags = Array.AsReadOnly(baseTags.Distinct().ToArray());
                PhrasesKo = Array.AsReadOnly(phrasesKo.ToArray());
            }

            public string ActivityId { get; }
            public int BaseImportance { get; }
            public IReadOnlyList<LeisureRelationshipTag> BaseTags { get; }
            public IReadOnlyList<string> PhrasesKo { get; }
        }
    }
}

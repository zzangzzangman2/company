using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>
    /// What accumulated know-how is worth while the work is being done.
    ///
    /// A company that has typed a hundred word databases finishes the next one faster and cleaner
    /// than one doing it for the first time. The proficiency for a job is the company's average level
    /// across the technologies that job uses, weighted by how much of the job each technology is, so
    /// a contract that is mostly DB work is carried by the DB level rather than by its side skill.
    ///
    /// All integer maths in basis points, like the rest of the workforce rules, so the same history
    /// always produces the same speed.
    /// </summary>
    public static class CompanyTechnologyBonusRules
    {
        /// <summary>No bonus and no penalty.</summary>
        public const int NeutralBasisPoints = 10_000;

        /// <summary>Work rate added per proficiency level above the first: 5% a level.</summary>
        public const int WorkRateBasisPointsPerLevel = 500;

        /// <summary>Quality points added per proficiency level above the first.</summary>
        public const int QualityPointsPerLevel = 3;

        /// <summary>
        /// Company proficiency for a job, in hundredths of a level, weighted by the grant points that
        /// job carries. Returns 0 when the company has never touched any of the technologies.
        /// </summary>
        public static int ProficiencyCentilevels(
            CompanyTechnologyState technology,
            IReadOnlyList<ContractTechnologyGrant> grants)
        {
            if (technology == null || grants == null || grants.Count == 0) return 0;
            long weighted = 0;
            long total = 0;
            for (var index = 0; index < grants.Count; index++)
            {
                var grant = grants[index];
                weighted += (long)technology.LevelFor(grant.TechnologyId) * grant.Points * 100L;
                total += grant.Points;
            }

            return total <= 0 ? 0 : checked((int)(weighted / total));
        }

        /// <summary>
        /// Multiplier applied to the work rate, in basis points. Level 1 or none is neutral; each
        /// further level adds <see cref="WorkRateBasisPointsPerLevel"/>, so a fully mastered job runs
        /// 20% faster. Never below neutral: experience does not slow anyone down.
        /// </summary>
        public static int WorkRateBasisPoints(
            CompanyTechnologyState technology,
            IReadOnlyList<ContractTechnologyGrant> grants)
        {
            var centilevels = ProficiencyCentilevels(technology, grants);
            if (centilevels <= 100) return NeutralBasisPoints;
            var above = centilevels - 100;
            var maximumAbove = (CompanyTechnologyCatalog.MaximumLevel - 1) * 100;
            if (above > maximumAbove) above = maximumAbove;
            return NeutralBasisPoints + above * WorkRateBasisPointsPerLevel / 100;
        }

        /// <summary>Quality points the company's experience is worth on this job.</summary>
        public static int QualityBonus(
            CompanyTechnologyState technology,
            IReadOnlyList<ContractTechnologyGrant> grants)
        {
            var centilevels = ProficiencyCentilevels(technology, grants);
            if (centilevels <= 100) return 0;
            var above = centilevels - 100;
            var maximumAbove = (CompanyTechnologyCatalog.MaximumLevel - 1) * 100;
            if (above > maximumAbove) above = maximumAbove;
            return above * QualityPointsPerLevel / 100;
        }

        /// <summary>
        /// Applies the work rate bonus to a minutes-per-person-hour figure. Fewer minutes per person
        /// hour means the same working day produces more progress.
        /// </summary>
        public static int ApplyWorkRate(int neutralMinutesPerPersonHour, int workRateBasisPoints)
        {
            if (neutralMinutesPerPersonHour <= 0) throw new ArgumentOutOfRangeException(nameof(neutralMinutesPerPersonHour));
            if (workRateBasisPoints <= 0) throw new ArgumentOutOfRangeException(nameof(workRateBasisPoints));
            var scaled = (long)neutralMinutesPerPersonHour * NeutralBasisPoints / workRateBasisPoints;
            // A person hour always costs at least one game minute.
            return checked((int)Math.Max(1L, scaled));
        }

        /// <summary>"기술 숙련 Lv3 · 작업 속도 +10%" for the work panel.</summary>
        public static string DisplayKo(
            CompanyTechnologyState technology,
            IReadOnlyList<ContractTechnologyGrant> grants)
        {
            var centilevels = ProficiencyCentilevels(technology, grants);
            if (centilevels <= 0) return "기술 숙련 없음 · 작업 속도 기본";
            var rate = WorkRateBasisPoints(technology, grants);
            var percent = (rate - NeutralBasisPoints) / 100;
            var level = centilevels / 100;
            return percent <= 0
                ? $"기술 숙련 Lv{level} · 작업 속도 기본"
                : $"기술 숙련 Lv{level} · 작업 속도 +{percent}%";
        }
    }
}

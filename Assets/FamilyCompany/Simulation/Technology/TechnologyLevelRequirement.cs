using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>
    /// A technology level something needs before it can be taken on. Used both by own-product paths
    /// and by the higher subcontracts, so a client asking for proven experience and a product asking
    /// for in-house capability are expressed the same way.
    /// </summary>
    public readonly struct TechnologyLevelRequirement
    {
        public TechnologyLevelRequirement(string technologyId, int requiredLevel)
        {
            if (!CompanyTechnologyCatalog.Exists(technologyId))
                throw new ArgumentException($"Unknown company technology: {technologyId}", nameof(technologyId));
            if (requiredLevel < 1 || requiredLevel > CompanyTechnologyCatalog.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(requiredLevel));
            TechnologyId = technologyId;
            RequiredLevel = requiredLevel;
        }

        public string TechnologyId { get; }
        public int RequiredLevel { get; }

        public string DisplayNameKo => CompanyTechnologyCatalog.Get(TechnologyId).DisplayNameKo;

        /// <summary>"DB 설계 Lv2"</summary>
        public string DisplayKo => $"{DisplayNameKo} Lv{RequiredLevel}";

        /// <summary>"DB 설계 Lv1/2" — what the company has against what is needed.</summary>
        public string ProgressKo(CompanyTechnologyState technology) =>
            $"{DisplayNameKo} Lv{technology?.LevelFor(TechnologyId) ?? 0}/{RequiredLevel}";

        public bool IsMetBy(CompanyTechnologyState technology) =>
            technology != null && technology.HasLevel(TechnologyId, RequiredLevel);
    }

    public static class TechnologyLevelRequirementExtensions
    {
        public static bool AllMetBy(
            this IReadOnlyList<TechnologyLevelRequirement> requirements,
            CompanyTechnologyState technology)
        {
            if (requirements == null || requirements.Count == 0) return true;
            for (var index = 0; index < requirements.Count; index++)
                if (!requirements[index].IsMetBy(technology)) return false;
            return true;
        }

        /// <summary>The requirements the company cannot meet yet, for a "무엇이 모자란지" label.</summary>
        public static IReadOnlyList<TechnologyLevelRequirement> UnmetBy(
            this IReadOnlyList<TechnologyLevelRequirement> requirements,
            CompanyTechnologyState technology)
        {
            if (requirements == null || requirements.Count == 0)
                return Array.Empty<TechnologyLevelRequirement>();
            return requirements.Where(item => !item.IsMetBy(technology)).ToArray();
        }

        public static string DisplayKo(this IReadOnlyList<TechnologyLevelRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0) return string.Empty;
            return string.Join(" · ", requirements.Select(item => item.DisplayKo));
        }
    }
}

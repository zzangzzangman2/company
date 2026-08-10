using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Family
{
    public enum CareerMemoryKind
    {
        ContractCompleted = 0,
        BusinessFounded = 1,
        ProductLaunched = 2,
        ContractFailed = 3
    }

    public sealed class CareerMemoryState
    {
        public CareerMemoryState(
            string memoryId,
            BusinessIndustry industry,
            CareerMemoryKind kind,
            string summary,
            long occurredMinute,
            int bondDelta,
            IEnumerable<string> colleagueMemberIds = null)
        {
            if (string.IsNullOrWhiteSpace(memoryId)) throw new ArgumentException("Memory ID is required.", nameof(memoryId));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            if (!Enum.IsDefined(typeof(CareerMemoryKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (occurredMinute < 0) throw new ArgumentOutOfRangeException(nameof(occurredMinute));
            MemoryId = memoryId;
            Industry = industry;
            Kind = kind;
            Summary = summary ?? string.Empty;
            OccurredMinute = occurredMinute;
            BondDelta = bondDelta;
            ColleagueMemberIds = colleagueMemberIds == null
                ? Array.Empty<string>()
                : colleagueMemberIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        }

        public string MemoryId { get; }
        public BusinessIndustry Industry { get; }
        public CareerMemoryKind Kind { get; }
        public string Summary { get; }
        public long OccurredMinute { get; }
        public int BondDelta { get; }
        public IReadOnlyList<string> ColleagueMemberIds { get; }
    }
}

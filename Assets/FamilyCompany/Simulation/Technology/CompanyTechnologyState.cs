using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Technology
{
    /// <summary>What a single finished contract added, so the UI can report it without recomputing.</summary>
    public readonly struct CompanyTechnologyGainRecord
    {
        public CompanyTechnologyGainRecord(string technologyId, int pointsAdded, int levelBefore, int levelAfter)
        {
            TechnologyId = technologyId ?? string.Empty;
            PointsAdded = pointsAdded;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
        }

        public string TechnologyId { get; }
        public int PointsAdded { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>
    /// The company's accumulated know-how. Points only ever come from finishing work, so this is a
    /// plain integer ledger with no time dependence: the same contracts in the same order always
    /// produce the same levels.
    ///
    /// This is deliberately separate from money. Cash lives on <c>CompanyState</c> and is spent;
    /// technology is earned by doing the job and never decreases.
    /// </summary>
    public sealed class CompanyTechnologyState
    {
        private readonly Dictionary<string, int> _points;

        public CompanyTechnologyState(IEnumerable<KeyValuePair<string, int>> points = null)
        {
            _points = new Dictionary<string, int>(StringComparer.Ordinal);
            if (points == null) return;
            foreach (var entry in points)
            {
                if (!CompanyTechnologyCatalog.Exists(entry.Key))
                    throw new InvalidOperationException($"Unknown company technology: {entry.Key}");
                if (entry.Value < 0) throw new InvalidOperationException("Technology points cannot be negative.");
                if (entry.Value == 0) continue;
                _points[entry.Key] = entry.Value;
            }
        }

        /// <summary>Raw points for one technology; zero when the company has never done that work.</summary>
        public int PointsFor(string technologyId)
        {
            if (string.IsNullOrEmpty(technologyId)) return 0;
            return _points.TryGetValue(technologyId, out var value) ? value : 0;
        }

        public int LevelFor(string technologyId) => CompanyTechnologyCatalog.LevelFor(PointsFor(technologyId));

        public bool HasLevel(string technologyId, int requiredLevel) => LevelFor(technologyId) >= requiredLevel;

        /// <summary>Technologies the company has any experience in, in catalog order.</summary>
        public IReadOnlyList<string> LearnedTechnologyIds =>
            CompanyTechnologyCatalog.All
                .Select(item => item.TechnologyId)
                .Where(id => PointsFor(id) > 0)
                .ToArray();

        /// <summary>
        /// Applies one finished contract's grants and reports what changed. Returns an empty list
        /// when the contract teaches nothing, which keeps "완료했지만 배운 것은 없음" a real outcome
        /// rather than a silent no-op.
        /// </summary>
        public IReadOnlyList<CompanyTechnologyGainRecord> ApplyGrants(
            IEnumerable<ContractTechnologyGrant> grants)
        {
            if (grants == null) return Array.Empty<CompanyTechnologyGainRecord>();
            var records = new List<CompanyTechnologyGainRecord>();
            foreach (var grant in grants)
            {
                var before = PointsFor(grant.TechnologyId);
                var after = checked(before + grant.Points);
                _points[grant.TechnologyId] = after;
                records.Add(new CompanyTechnologyGainRecord(
                    grant.TechnologyId,
                    grant.Points,
                    CompanyTechnologyCatalog.LevelFor(before),
                    CompanyTechnologyCatalog.LevelFor(after)));
            }

            return records;
        }

        /// <summary>Stable snapshot for saving: catalog order, zero entries omitted.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Snapshot() =>
            CompanyTechnologyCatalog.All
                .Select(item => item.TechnologyId)
                .Where(id => PointsFor(id) > 0)
                .Select(id => new KeyValuePair<string, int>(id, PointsFor(id)))
                .ToArray();
    }
}

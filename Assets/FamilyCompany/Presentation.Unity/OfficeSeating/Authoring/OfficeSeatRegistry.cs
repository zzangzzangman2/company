using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.Authoring
{
    [DisallowMultipleComponent]
    public sealed class OfficeSeatRegistry : MonoBehaviour
    {
        [SerializeField] private OfficeSeatAuthoring[] seats = Array.Empty<OfficeSeatAuthoring>();

        private readonly Dictionary<string, OfficeSeatAuthoring> _authoringById =
            new Dictionary<string, OfficeSeatAuthoring>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeSeatDefinition> _definitionById =
            new Dictionary<string, OfficeSeatDefinition>(StringComparer.Ordinal);
        private readonly List<OfficeSeatDefinition> _definitions =
            new List<OfficeSeatDefinition>();
        private bool _snapshotBuilt;
        private int _runtimeRevision;

        public int SeatCount
        {
            get
            {
                EnsureRuntimeSnapshot();
                return _definitions.Count;
            }
        }

        public IReadOnlyList<OfficeSeatDefinition> Definitions
        {
            get
            {
                EnsureRuntimeSnapshot();
                return _definitions.ToArray();
            }
        }

        public int RuntimeRevision
        {
            get
            {
                EnsureRuntimeSnapshot();
                return _runtimeRevision;
            }
        }

        public void Configure(OfficeSeatAuthoring[] configuredSeats)
        {
            seats = configuredSeats == null
                ? Array.Empty<OfficeSeatAuthoring>()
                : (OfficeSeatAuthoring[])configuredSeats.Clone();
            _snapshotBuilt = false;
        }

        public OfficeSeatValidationReport ValidateRegistry()
        {
            return ValidateAuthoringCollection(seats);
        }

        public void Rebuild()
        {
            RefreshRuntimeSnapshot(true);
        }

        public bool TryGetAuthoring(string seatId, out OfficeSeatAuthoring authoring)
        {
            EnsureRuntimeSnapshot();
            return _authoringById.TryGetValue(CanonicalLookupId(seatId), out authoring);
        }

        public bool TryGetDefinition(string seatId, out OfficeSeatDefinition definition)
        {
            EnsureRuntimeSnapshot();
            return _definitionById.TryGetValue(CanonicalLookupId(seatId), out definition);
        }

        public OfficeSeatDefinition GetRequiredDefinition(string seatId)
        {
            if (TryGetDefinition(seatId, out var definition)) return definition;
            throw new KeyNotFoundException($"Office seat is not registered: {seatId}.");
        }

        public static OfficeSeatValidationReport ValidateAuthoringCollection(
            IEnumerable<OfficeSeatAuthoring> authoredSeats)
        {
            if (authoredSeats == null) throw new ArgumentNullException(nameof(authoredSeats));
            var report = new OfficeSeatValidationReport();
            var seatIds = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var seat in authoredSeats)
            {
                if (seat == null)
                {
                    report.AddError(
                        "null_seat_authoring",
                        string.Empty,
                        $"Office seat registry entry {index} is null.");
                    index++;
                    continue;
                }

                var seatReport = seat.ValidateAuthoring();
                report.Merge(seatReport);
                var seatId = seat.SeatId;
                if (seatId.Length > 0 && !seatIds.Add(seatId))
                {
                    report.AddError(
                        "duplicate_seat_id",
                        seatId,
                        $"Office seat ID is duplicated: {seatId}.");
                }

                index++;
            }

            return report;
        }

        private void Awake()
        {
            Rebuild();
        }

        private void EnsureRuntimeSnapshot()
        {
            RefreshRuntimeSnapshot(false);
        }

        private void RefreshRuntimeSnapshot(bool logExcludedSeats)
        {
            var nextAuthoring = new Dictionary<string, OfficeSeatAuthoring>(StringComparer.Ordinal);
            var nextDefinitions = new Dictionary<string, OfficeSeatDefinition>(StringComparer.Ordinal);
            var orderedDefinitions = new List<OfficeSeatDefinition>();
            var excludedCount = 0;
            var source = seats ?? Array.Empty<OfficeSeatAuthoring>();
            foreach (var seat in source)
            {
                if (seat == null || !seat.IsRuntimeValid)
                {
                    excludedCount++;
                    continue;
                }
                if (!seat.TryBuildDefinition(out var definition, out var report) || report.HasErrors ||
                    nextAuthoring.ContainsKey(definition.SeatId))
                {
                    excludedCount++;
                    continue;
                }

                nextAuthoring.Add(definition.SeatId, seat);
                nextDefinitions.Add(definition.SeatId, definition);
                orderedDefinitions.Add(definition);
            }
            orderedDefinitions.Sort((left, right) => string.CompareOrdinal(left.SeatId, right.SeatId));

            var changed = !_snapshotBuilt || !SnapshotMatches(nextAuthoring, orderedDefinitions);
            if (changed)
            {
                _authoringById.Clear();
                _definitionById.Clear();
                _definitions.Clear();
                foreach (var pair in nextAuthoring) _authoringById.Add(pair.Key, pair.Value);
                foreach (var pair in nextDefinitions) _definitionById.Add(pair.Key, pair.Value);
                _definitions.AddRange(orderedDefinitions);
                _runtimeRevision++;
                _snapshotBuilt = true;
            }

            if (logExcludedSeats && changed && excludedCount > 0)
                Debug.LogWarning($"OFFICE_SEAT_RUNTIME_EXCLUDED count={excludedCount}", this);
        }

        private bool SnapshotMatches(
            IReadOnlyDictionary<string, OfficeSeatAuthoring> nextAuthoring,
            IReadOnlyList<OfficeSeatDefinition> nextDefinitions)
        {
            if (_authoringById.Count != nextAuthoring.Count || _definitions.Count != nextDefinitions.Count)
                return false;
            foreach (var pair in nextAuthoring)
            {
                if (!_authoringById.TryGetValue(pair.Key, out var current) || current != pair.Value)
                    return false;
            }
            for (var index = 0; index < nextDefinitions.Count; index++)
            {
                if (!DefinitionMatches(_definitions[index], nextDefinitions[index])) return false;
            }
            return true;
        }

        private static bool DefinitionMatches(OfficeSeatDefinition left, OfficeSeatDefinition right)
        {
            return string.Equals(left.SeatId, right.SeatId, StringComparison.Ordinal) &&
                   left.ApproachPosition.X.Equals(right.ApproachPosition.X) &&
                   left.ApproachPosition.Y.Equals(right.ApproachPosition.Y) &&
                   left.ApproachPosition.Z.Equals(right.ApproachPosition.Z) &&
                   left.SitPosition.X.Equals(right.SitPosition.X) &&
                   left.SitPosition.Y.Equals(right.SitPosition.Y) &&
                   left.SitPosition.Z.Equals(right.SitPosition.Z) &&
                   left.ComputerLookPosition.X.Equals(right.ComputerLookPosition.X) &&
                   left.ComputerLookPosition.Y.Equals(right.ComputerLookPosition.Y) &&
                   left.ComputerLookPosition.Z.Equals(right.ComputerLookPosition.Z) &&
                   left.ResolvedFacing == right.ResolvedFacing &&
                   left.ForegroundOcclusionMode == right.ForegroundOcclusionMode;
        }

        private static string CanonicalLookupId(string seatId)
        {
            return (seatId ?? string.Empty).Trim();
        }
    }
}

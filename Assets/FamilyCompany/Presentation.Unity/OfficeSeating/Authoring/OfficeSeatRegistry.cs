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

        public int SeatCount
        {
            get
            {
                PruneDestroyedAuthoring();
                return _definitions.Count;
            }
        }

        public IReadOnlyList<OfficeSeatDefinition> Definitions
        {
            get
            {
                PruneDestroyedAuthoring();
                return _definitions;
            }
        }

        public void Configure(OfficeSeatAuthoring[] configuredSeats)
        {
            seats = configuredSeats == null
                ? Array.Empty<OfficeSeatAuthoring>()
                : (OfficeSeatAuthoring[])configuredSeats.Clone();
        }

        public OfficeSeatValidationReport ValidateRegistry()
        {
            return ValidateAuthoringCollection(seats);
        }

        public void Rebuild()
        {
            seats = RemoveDestroyedAuthoring(seats);
            var report = ValidateRegistry();
            foreach (var issue in report.Issues)
            {
                if (issue.Severity == OfficeSeatValidationSeverity.Warning)
                    Debug.LogWarning($"OFFICE_SEAT_AUTHORING_WARNING [{issue.Code}] {issue.Message}", this);
            }

            if (report.HasErrors)
                throw new InvalidOperationException("Invalid office seat authoring:\n" + report.FormatErrors());

            _authoringById.Clear();
            _definitionById.Clear();
            _definitions.Clear();
            foreach (var seat in seats)
            {
                if (!seat.TryBuildDefinition(out var definition, out var seatReport) || seatReport.HasErrors)
                    throw new InvalidOperationException($"Office seat failed after registry validation: {seat.SeatId}.");
                _authoringById.Add(definition.SeatId, seat);
                _definitionById.Add(definition.SeatId, definition);
                _definitions.Add(definition);
            }

            _definitions.Sort((left, right) => string.CompareOrdinal(left.SeatId, right.SeatId));
        }

        public bool TryGetAuthoring(string seatId, out OfficeSeatAuthoring authoring)
        {
            var canonicalId = CanonicalLookupId(seatId);
            if (!_authoringById.TryGetValue(canonicalId, out authoring)) return false;
            if (authoring != null && authoring.HasRuntimeAnchors) return true;

            if (authoring == null) RemoveRegisteredSeat(canonicalId);
            authoring = null;
            return false;
        }

        public bool TryGetDefinition(string seatId, out OfficeSeatDefinition definition)
        {
            var canonicalId = CanonicalLookupId(seatId);
            if (!TryGetAuthoring(canonicalId, out _))
            {
                definition = null;
                return false;
            }
            return _definitionById.TryGetValue(canonicalId, out definition);
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

        private void PruneDestroyedAuthoring()
        {
            if (_authoringById.Count == 0) return;
            var staleIds = new List<string>();
            foreach (var pair in _authoringById)
            {
                if (pair.Value == null) staleIds.Add(pair.Key);
            }
            foreach (var seatId in staleIds) RemoveRegisteredSeat(seatId);
        }

        private void RemoveRegisteredSeat(string canonicalId)
        {
            _authoringById.Remove(canonicalId);
            _definitionById.Remove(canonicalId);
            _definitions.RemoveAll(item => string.Equals(item.SeatId, canonicalId, StringComparison.Ordinal));
        }

        private static OfficeSeatAuthoring[] RemoveDestroyedAuthoring(OfficeSeatAuthoring[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<OfficeSeatAuthoring>();
            var liveOrActualNull = new List<OfficeSeatAuthoring>(source.Length);
            foreach (var seat in source)
            {
                if (ReferenceEquals(seat, null) || seat != null) liveOrActualNull.Add(seat);
            }
            return liveOrActualNull.ToArray();
        }

        private static string CanonicalLookupId(string seatId)
        {
            return (seatId ?? string.Empty).Trim();
        }
    }
}

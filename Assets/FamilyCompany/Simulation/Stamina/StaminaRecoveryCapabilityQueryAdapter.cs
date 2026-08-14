using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Stamina
{
    /// <summary>
    /// Pure request passed to the integration adapter for the building/furniture read-only
    /// capability query. It deliberately contains no location, cell, furniture kind, or instance ID.
    /// </summary>
    public readonly struct StaminaRecoveryCapabilityQuery
    {
        public StaminaRecoveryCapabilityQuery(
            string characterId,
            string recoveryRequestKey,
            long gameTimeMinute)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Character ID is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(recoveryRequestKey))
                throw new ArgumentException(
                    "Recovery request key is required.",
                    nameof(recoveryRequestKey));
            if (gameTimeMinute < 0) throw new ArgumentOutOfRangeException(nameof(gameTimeMinute));
            CharacterId = characterId.Trim();
            RecoveryRequestKey = recoveryRequestKey.Trim();
            GameTimeMinute = gameTimeMinute;
        }

        public string CharacterId { get; }
        public string RecoveryRequestKey { get; }
        public long GameTimeMinute { get; }
    }

    /// <summary>
    /// Immutable snapshot of external query output. SourceRevision identifies the furniture/layout
    /// view used for advisory planning; the runtime lifecycle must query/revalidate again when it
    /// atomically claims a concrete instance.
    /// </summary>
    public sealed class StaminaRecoveryCapabilityQueryResult
    {
        private readonly StaminaRecoveryCandidate[] _candidates;

        public StaminaRecoveryCapabilityQueryResult(
            StaminaRecoveryCapabilityQuery query,
            long sourceRevision,
            IEnumerable<StaminaRecoveryCandidate> candidates)
        {
            if (sourceRevision < 0) throw new ArgumentOutOfRangeException(nameof(sourceRevision));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (string.IsNullOrWhiteSpace(query.CharacterId) ||
                string.IsNullOrWhiteSpace(query.RecoveryRequestKey) ||
                query.GameTimeMinute < 0)
                throw new ArgumentException("Capability query context is invalid.", nameof(query));
            _candidates = candidates.ToArray();
            if (_candidates.Any(item => item == null))
                throw new ArgumentException(
                    "Capability query candidates cannot contain null.",
                    nameof(candidates));
            Query = query;
            SourceRevision = sourceRevision;
        }

        public StaminaRecoveryCapabilityQuery Query { get; }
        public long SourceRevision { get; }
        public IReadOnlyList<StaminaRecoveryCandidate> Candidates =>
            Array.AsReadOnly(_candidates);
    }

    /// <summary>
    /// Adapter boundary to the building/furniture owner's finalized read-only capability query API.
    /// The implementation maps that owner's capabilities (including WaterSource, DrinkVending, and
    /// RestSeat) into transient stamina candidates. It must not maintain a second furniture catalog,
    /// scan scene objects, or synthesize instance IDs.
    /// </summary>
    public interface IStaminaRecoveryCapabilityQueryAdapter
    {
        StaminaRecoveryCapabilityQueryResult Query(
            StaminaRecoveryCapabilityQuery query);
    }
}

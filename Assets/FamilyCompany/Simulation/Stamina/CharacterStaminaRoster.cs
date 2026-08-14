using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Stamina
{
    [Serializable]
    public sealed class CharacterStaminaRosterSnapshotDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int worldSeed;
        public long lastProcessedMinute;
        public List<CharacterStaminaSnapshotDto> characters =
            new List<CharacterStaminaSnapshotDto>();
    }

    public sealed class CharacterStaminaRosterAdvanceResult
    {
        internal CharacterStaminaRosterAdvanceResult(
            long requestedToMinute,
            long processedToMinute,
            IReadOnlyDictionary<string, StaminaAdvanceResult> characters)
        {
            RequestedToMinute = requestedToMinute;
            ProcessedToMinute = processedToMinute;
            Characters = characters ??
                         throw new ArgumentNullException(nameof(characters));
        }

        public long RequestedToMinute { get; }
        public long ProcessedToMinute { get; }
        public bool ReachedRequestedMinute => ProcessedToMinute == RequestedToMinute;
        public bool RequiresRuntimeDecision => Characters.Values.Any(item =>
            item.RequiresRuntimeDecision);
        public IReadOnlyDictionary<string, StaminaAdvanceResult> Characters { get; }
    }

    /// <summary>
    /// Pure semantic owner for family and future employee stamina. Every member advances in one
    /// atomic GameTime step; the roster yields at the earliest member decision boundary.
    /// </summary>
    public sealed class CharacterStaminaRoster : ICharacterStaminaReadModel
    {
        private readonly int _worldSeed;
        private readonly CharacterStaminaCatalog _catalog;
        private readonly Dictionary<string, CharacterStaminaSimulation> _simulations =
            new Dictionary<string, CharacterStaminaSimulation>(StringComparer.Ordinal);
        private string[] _orderedIds = Array.Empty<string>();

        public CharacterStaminaRoster(
            int worldSeed,
            CharacterStaminaCatalog catalog,
            IEnumerable<string> characterIds,
            long startMinute = 0)
        {
            _worldSeed = worldSeed;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (characterIds == null) throw new ArgumentNullException(nameof(characterIds));
            if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            LastProcessedMinute = startMinute;
            foreach (string characterId in characterIds)
                AddCharacter(characterId, startMinute);
            if (_simulations.Count == 0)
                throw new ArgumentException(
                    "A stamina roster requires at least one character.",
                    nameof(characterIds));
        }

        private CharacterStaminaRoster(
            int worldSeed,
            CharacterStaminaCatalog catalog,
            long lastProcessedMinute)
        {
            _worldSeed = worldSeed;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (lastProcessedMinute < 0)
                throw new ArgumentOutOfRangeException(nameof(lastProcessedMinute));
            LastProcessedMinute = lastProcessedMinute;
        }

        public int WorldSeed => _worldSeed;
        public long LastProcessedMinute { get; private set; }
        public CharacterStaminaCatalog Catalog => _catalog;
        public IReadOnlyList<string> CharacterIds => Array.AsReadOnly(_orderedIds);
        public int Count => _simulations.Count;

        public CharacterStaminaSimulation AddCharacter(string characterId, long startMinute)
        {
            if (startMinute != LastProcessedMinute)
                throw new InvalidOperationException(
                    $"A roster member must start at authoritative minute {LastProcessedMinute}.");
            string normalized = NormalizeId(characterId);
            if (_simulations.ContainsKey(normalized))
                throw new InvalidOperationException("Duplicate stamina character: " + normalized);
            CharacterStaminaSimulation simulation = CharacterStaminaSimulation.CreateDefault(
                _worldSeed,
                normalized,
                _catalog,
                startMinute);
            _simulations.Add(normalized, simulation);
            RefreshOrder();
            return simulation;
        }

        public CharacterStaminaSimulation GetSimulation(string characterId)
        {
            string normalized = NormalizeId(characterId);
            if (!_simulations.TryGetValue(normalized, out CharacterStaminaSimulation simulation))
                throw new KeyNotFoundException("Unknown stamina character: " + normalized);
            return simulation;
        }

        public bool TryGetSimulation(
            string characterId,
            out CharacterStaminaSimulation simulation)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                simulation = null;
                return false;
            }
            return _simulations.TryGetValue(characterId.Trim(), out simulation);
        }

        public bool TryRead(string characterId, out CharacterStaminaReadSnapshot snapshot)
        {
            if (TryGetSimulation(characterId, out CharacterStaminaSimulation simulation))
            {
                snapshot = simulation.Read();
                return true;
            }
            snapshot = default;
            return false;
        }

        /// <summary>
        /// Records all activity changes at the roster's current authoritative minute. Delegate
        /// output is fully validated before any state is mutated.
        /// </summary>
        public IReadOnlyDictionary<string, StaminaTransition> SetActivitiesAtCurrentMinute(
            Func<string, StaminaActivityKind> activityForCharacter)
        {
            EnsureRosterClockInvariant();
            if (activityForCharacter == null)
                throw new ArgumentNullException(nameof(activityForCharacter));
            var activities = new Dictionary<string, StaminaActivityKind>(StringComparer.Ordinal);
            foreach (string characterId in _orderedIds)
            {
                StaminaActivityKind activity = activityForCharacter(characterId);
                if (!Enum.IsDefined(typeof(StaminaActivityKind), activity) ||
                    activity == StaminaActivityKind.None)
                    throw new ArgumentOutOfRangeException(
                        nameof(activityForCharacter),
                        "Every roster activity must be a concrete stamina activity.");
                activities.Add(characterId, activity);
            }

            var transitions = new Dictionary<string, StaminaTransition>(StringComparer.Ordinal);
            foreach (string characterId in _orderedIds)
                transitions.Add(
                    characterId,
                    _simulations[characterId].SetActivity(
                        activities[characterId],
                        LastProcessedMinute));
            return transitions;
        }

        /// <summary>
        /// Advances every member to the earliest threshold/performance decision boundary. The
        /// caller handles the emitted runtime decision, then calls again for the remaining time.
        /// </summary>
        public CharacterStaminaRosterAdvanceResult AdvanceAllTo(
            long targetMinute,
            Func<string, bool> allowOfficeRecoveryForCharacter = null)
        {
            EnsureRosterClockInvariant();
            if (targetMinute < LastProcessedMinute)
                throw new InvalidOperationException("Stamina roster time cannot move backwards.");

            var allowed = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string characterId in _orderedIds)
                allowed.Add(
                    characterId,
                    allowOfficeRecoveryForCharacter == null ||
                    allowOfficeRecoveryForCharacter(characterId));

            long stepTarget = targetMinute;
            foreach (string characterId in _orderedIds)
            {
                long boundary = _simulations[characterId].PreviewNextDecisionMinute(
                    targetMinute,
                    allowed[characterId]);
                if (boundary < stepTarget) stepTarget = boundary;
            }

            var results = new Dictionary<string, StaminaAdvanceResult>(StringComparer.Ordinal);
            foreach (string characterId in _orderedIds)
            {
                StaminaAdvanceResult result = _simulations[characterId].AdvanceTo(
                    stepTarget,
                    allowed[characterId]);
                if (result.ProcessedToMinute != stepTarget)
                    throw new InvalidOperationException(
                        "Stamina preview/advance boundary mismatch for " + characterId + ".");
                results.Add(characterId, result);
            }
            LastProcessedMinute = stepTarget;
            EnsureRosterClockInvariant();
            return new CharacterStaminaRosterAdvanceResult(
                targetMinute,
                stepTarget,
                results);
        }

        public CharacterStaminaRosterSnapshotDto ExportSnapshot()
        {
            EnsureRosterClockInvariant();
            return new CharacterStaminaRosterSnapshotDto
            {
                schemaVersion = CharacterStaminaRosterSnapshotDto.CurrentSchemaVersion,
                worldSeed = _worldSeed,
                lastProcessedMinute = LastProcessedMinute,
                characters = _orderedIds.Select(characterId =>
                    _simulations[characterId].ExportSnapshot()).ToList()
            };
        }

        public static CharacterStaminaRoster Restore(
            CharacterStaminaRosterSnapshotDto snapshot,
            CharacterStaminaCatalog catalog)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot.schemaVersion != CharacterStaminaRosterSnapshotDto.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported stamina roster schema: {snapshot.schemaVersion}");
            if (snapshot.lastProcessedMinute < 0)
                throw new InvalidOperationException("Stamina roster time is invalid.");
            if (snapshot.characters == null || snapshot.characters.Count == 0)
                throw new InvalidOperationException("Stamina roster snapshot is empty.");
            if (snapshot.characters.Any(item => item == null))
                throw new InvalidOperationException("Stamina roster snapshot contains null.");
            if (snapshot.characters.Any(item => item.worldSeed != snapshot.worldSeed))
                throw new InvalidOperationException("Stamina roster world seeds must match.");
            if (snapshot.characters.Any(item =>
                    item.lastProcessedMinute != snapshot.lastProcessedMinute))
                throw new InvalidOperationException("Stamina roster member clocks must match.");

            var roster = new CharacterStaminaRoster(
                snapshot.worldSeed,
                catalog,
                snapshot.lastProcessedMinute);
            foreach (CharacterStaminaSnapshotDto item in snapshot.characters
                         .OrderBy(item => item.characterId, StringComparer.Ordinal))
            {
                CharacterStaminaSimulation simulation =
                    CharacterStaminaSimulation.Restore(item, catalog);
                if (!roster._simulations.TryAdd(simulation.CharacterId, simulation))
                    throw new InvalidOperationException(
                        "Duplicate stamina character in snapshot: " + simulation.CharacterId);
            }
            roster.RefreshOrder();
            roster.EnsureRosterClockInvariant();
            return roster;
        }

        public static CharacterStaminaRoster MigrateLegacyEnergyPercents(
            int worldSeed,
            CharacterStaminaCatalog catalog,
            IEnumerable<KeyValuePair<string, int>> legacyEnergyPercents,
            long elapsedMinute)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (legacyEnergyPercents == null)
                throw new ArgumentNullException(nameof(legacyEnergyPercents));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            var roster = new CharacterStaminaRoster(worldSeed, catalog, elapsedMinute);
            foreach (KeyValuePair<string, int> entry in legacyEnergyPercents
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                CharacterStaminaSimulation simulation =
                    CharacterStaminaSimulation.MigrateLegacyEnergyPercent(
                        worldSeed,
                        entry.Key,
                        catalog,
                        entry.Value,
                        elapsedMinute);
                if (!roster._simulations.TryAdd(simulation.CharacterId, simulation))
                    throw new InvalidOperationException(
                        "Duplicate legacy stamina character: " + simulation.CharacterId);
            }
            if (roster._simulations.Count == 0)
                throw new InvalidOperationException("Legacy stamina roster is empty.");
            roster.RefreshOrder();
            roster.EnsureRosterClockInvariant();
            return roster;
        }

        private void EnsureRosterClockInvariant()
        {
            foreach (CharacterStaminaSimulation simulation in _simulations.Values)
                if (simulation.State.LastProcessedMinute != LastProcessedMinute)
                    throw new InvalidOperationException(
                        $"Stamina roster clock mismatch for {simulation.CharacterId}.");
        }

        private void RefreshOrder()
        {
            _orderedIds = _simulations.Keys
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Character ID is required.", nameof(value));
            return value.Trim();
        }
    }
}

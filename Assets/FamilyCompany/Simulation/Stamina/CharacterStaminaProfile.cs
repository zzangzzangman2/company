using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Stamina
{
    public enum StaminaActivityKind
    {
        None = 0,
        Idle = 1,
        Walking = 2,
        DeskWork = 3,
        Typing = 4,
        Meeting = 5,
        Administration = 6,
        Reception = 7,
        Printing = 8,
        OutsideWork = 9,
        OffDuty = 10,
        Sleep = 11
    }

    public enum StaminaRecoveryActivity
    {
        None = 0,
        Water = 1,
        Restroom = 2,
        Lounge = 3
    }

    public sealed class StaminaDrainDefinition
    {
        public StaminaDrainDefinition(StaminaActivityKind activity, int unitsPerGameMinute)
        {
            if (!Enum.IsDefined(typeof(StaminaActivityKind), activity) ||
                activity == StaminaActivityKind.None)
                throw new ArgumentOutOfRangeException(nameof(activity));
            if (unitsPerGameMinute < 0) throw new ArgumentOutOfRangeException(nameof(unitsPerGameMinute));
            Activity = activity;
            UnitsPerGameMinute = unitsPerGameMinute;
        }

        public StaminaActivityKind Activity { get; }
        public int UnitsPerGameMinute { get; }
    }

    public sealed class StaminaRecoveryDefinition
    {
        private readonly int _maximumRecoveryUnits;
        private readonly string[] _interactionIds;

        public StaminaRecoveryDefinition(
            StaminaRecoveryActivity activity,
            string interactionId,
            int durationGameMinutes,
            int recoveryUnitsPerGameMinute,
            int selectionWeight,
            IEnumerable<string> additionalInteractionIds = null)
        {
            if (!Enum.IsDefined(typeof(StaminaRecoveryActivity), activity) ||
                activity == StaminaRecoveryActivity.None)
                throw new ArgumentOutOfRangeException(nameof(activity));
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
            if (durationGameMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationGameMinutes));
            if (recoveryUnitsPerGameMinute <= 0)
                throw new ArgumentOutOfRangeException(nameof(recoveryUnitsPerGameMinute));
            if (selectionWeight <= 0) throw new ArgumentOutOfRangeException(nameof(selectionWeight));
            long maximumRecoveryUnits = (long)durationGameMinutes * recoveryUnitsPerGameMinute;
            if (maximumRecoveryUnits > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(recoveryUnitsPerGameMinute),
                    "Recovery duration multiplied by rate must fit in Int32 units.");

            Activity = activity;
            InteractionId = interactionId.Trim();
            _interactionIds = new[] { InteractionId }
                .Concat(additionalInteractionIds ?? Enumerable.Empty<string>())
                .Select(item => (item ?? string.Empty).Trim())
                .ToArray();
            if (_interactionIds.Any(string.IsNullOrWhiteSpace) ||
                _interactionIds.Distinct(StringComparer.Ordinal).Count() != _interactionIds.Length)
                throw new ArgumentException(
                    "Recovery interaction IDs must be non-empty and unique.",
                    nameof(additionalInteractionIds));
            DurationGameMinutes = durationGameMinutes;
            RecoveryUnitsPerGameMinute = recoveryUnitsPerGameMinute;
            SelectionWeight = selectionWeight;
            _maximumRecoveryUnits = (int)maximumRecoveryUnits;
        }

        public StaminaRecoveryActivity Activity { get; }
        public string InteractionId { get; }
        public int DurationGameMinutes { get; }
        public int RecoveryUnitsPerGameMinute { get; }
        public int SelectionWeight { get; }
        public int MaximumRecoveryUnits => _maximumRecoveryUnits;
        public IReadOnlyList<string> InteractionIds => Array.AsReadOnly(_interactionIds);

        public bool SupportsInteractionId(string interactionId) =>
            !string.IsNullOrWhiteSpace(interactionId) &&
            _interactionIds.Contains(interactionId.Trim(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Immutable ability data. Current stamina is not stored here; the live semantic state owns it.
    /// One common profile is used for every ID unless the character/employee catalog supplies an
    /// override, so adding a hired employee does not require a role switch or duplicated code.
    /// </summary>
    public sealed class CharacterStaminaProfile
    {
        public const int BasisPointDenominator = 10_000;

        private readonly IReadOnlyDictionary<StaminaActivityKind, StaminaDrainDefinition> _drainByActivity;
        private readonly IReadOnlyDictionary<StaminaRecoveryActivity, StaminaRecoveryDefinition> _recoveryByActivity;
        private readonly StaminaDrainDefinition[] _drains;
        private readonly StaminaRecoveryDefinition[] _recoveries;

        public CharacterStaminaProfile(
            int maxUnits,
            int initialUnits,
            int recoveryThresholdBasisPoints,
            int resumeThresholdBasisPoints,
            int cautionThresholdBasisPoints,
            IEnumerable<StaminaDrainDefinition> drainDefinitions,
            IEnumerable<StaminaRecoveryDefinition> recoveryDefinitions)
        {
            if (maxUnits <= 0) throw new ArgumentOutOfRangeException(nameof(maxUnits));
            if (initialUnits < 0 || initialUnits > maxUnits)
                throw new ArgumentOutOfRangeException(nameof(initialUnits));
            if (recoveryThresholdBasisPoints <= 0 ||
                recoveryThresholdBasisPoints >= BasisPointDenominator)
                throw new ArgumentOutOfRangeException(nameof(recoveryThresholdBasisPoints));
            if (resumeThresholdBasisPoints <= recoveryThresholdBasisPoints ||
                resumeThresholdBasisPoints >= cautionThresholdBasisPoints)
                throw new ArgumentOutOfRangeException(nameof(resumeThresholdBasisPoints));
            if (cautionThresholdBasisPoints <= recoveryThresholdBasisPoints ||
                cautionThresholdBasisPoints >= BasisPointDenominator)
                throw new ArgumentOutOfRangeException(nameof(cautionThresholdBasisPoints));
            if (drainDefinitions == null) throw new ArgumentNullException(nameof(drainDefinitions));
            if (recoveryDefinitions == null) throw new ArgumentNullException(nameof(recoveryDefinitions));

            StaminaDrainDefinition[] suppliedDrains = drainDefinitions.ToArray();
            if (suppliedDrains.Any(item => item == null))
                throw new ArgumentException("Drain definitions cannot contain null.", nameof(drainDefinitions));
            _drains = suppliedDrains.OrderBy(item => item.Activity).ToArray();
            if (_drains.Select(item => item.Activity).Distinct().Count() != _drains.Length)
                throw new ArgumentException("Drain activities must be unique.", nameof(drainDefinitions));
            StaminaActivityKind[] requiredActivities = Enum.GetValues(typeof(StaminaActivityKind))
                .Cast<StaminaActivityKind>()
                .Where(item => item != StaminaActivityKind.None)
                .ToArray();
            if (!requiredActivities.SequenceEqual(_drains.Select(item => item.Activity)))
                throw new ArgumentException("Every stamina activity requires exactly one drain definition.",
                    nameof(drainDefinitions));

            StaminaRecoveryDefinition[] suppliedRecoveries = recoveryDefinitions.ToArray();
            if (suppliedRecoveries.Any(item => item == null))
                throw new ArgumentException("Recovery definitions cannot contain null.", nameof(recoveryDefinitions));
            _recoveries = suppliedRecoveries.OrderBy(item => item.Activity).ToArray();
            if (_recoveries.Select(item => item.Activity).Distinct().Count() != _recoveries.Length)
                throw new ArgumentException("Recovery activities must be unique.", nameof(recoveryDefinitions));
            StaminaRecoveryActivity[] requiredRecoveries = Enum.GetValues(typeof(StaminaRecoveryActivity))
                .Cast<StaminaRecoveryActivity>()
                .Where(item => item != StaminaRecoveryActivity.None)
                .ToArray();
            if (!requiredRecoveries.SequenceEqual(_recoveries.Select(item => item.Activity)))
                throw new ArgumentException("Water, Restroom, and Lounge recovery definitions are required.",
                    nameof(recoveryDefinitions));
            long totalSelectionWeight = _recoveries.Sum(item => (long)item.SelectionWeight);
            if (totalSelectionWeight > int.MaxValue)
                throw new ArgumentException(
                    "The total recovery selection weight must fit in Int32.",
                    nameof(recoveryDefinitions));

            MaxUnits = maxUnits;
            InitialUnits = initialUnits;
            RecoveryThresholdBasisPoints = recoveryThresholdBasisPoints;
            ResumeThresholdBasisPoints = resumeThresholdBasisPoints;
            CautionThresholdBasisPoints = cautionThresholdBasisPoints;
            _drainByActivity = _drains.ToDictionary(item => item.Activity);
            _recoveryByActivity = _recoveries.ToDictionary(item => item.Activity);
            int resumeThresholdUnits = ResumeThresholdUnits;
            if (_recoveries.Any(item => item.MaximumRecoveryUnits < resumeThresholdUnits))
                throw new ArgumentException(
                    "Every recovery activity must restore a zero-stamina character to the resume threshold.",
                    nameof(recoveryDefinitions));
            ProfileFingerprint = BuildFingerprint();
        }

        public int MaxUnits { get; }
        public int InitialUnits { get; }
        public int RecoveryThresholdBasisPoints { get; }
        public int ResumeThresholdBasisPoints { get; }
        public int CautionThresholdBasisPoints { get; }
        public int RecoveryThresholdUnits => RatioThresholdUnits(RecoveryThresholdBasisPoints);
        public int ResumeThresholdUnits => RatioThresholdUnitsCeiling(ResumeThresholdBasisPoints);
        public int CautionThresholdUnits => RatioThresholdUnits(CautionThresholdBasisPoints);
        public IReadOnlyList<StaminaDrainDefinition> DrainDefinitions => Array.AsReadOnly(_drains);
        public IReadOnlyList<StaminaRecoveryDefinition> RecoveryDefinitions => Array.AsReadOnly(_recoveries);
        public string ProfileFingerprint { get; }

        public int DrainUnitsPerGameMinute(StaminaActivityKind activity)
        {
            if (activity == StaminaActivityKind.None) return 0;
            if (!_drainByActivity.TryGetValue(activity, out StaminaDrainDefinition definition))
                throw new ArgumentOutOfRangeException(nameof(activity));
            return definition.UnitsPerGameMinute;
        }

        public StaminaRecoveryDefinition Recovery(StaminaRecoveryActivity activity)
        {
            if (!_recoveryByActivity.TryGetValue(activity, out StaminaRecoveryDefinition definition))
                throw new ArgumentOutOfRangeException(nameof(activity));
            return definition;
        }

        public int RatioBasisPoints(int currentUnits)
        {
            int clamped = Math.Max(0, Math.Min(MaxUnits, currentUnits));
            return checked((int)((long)clamped * BasisPointDenominator / MaxUnits));
        }

        public bool IsAtOrBelowRecoveryThreshold(int currentUnits)
        {
            int clamped = Math.Max(0, Math.Min(MaxUnits, currentUnits));
            return (long)clamped * BasisPointDenominator <=
                   (long)MaxUnits * RecoveryThresholdBasisPoints;
        }

        public int LegacyPercent(int currentUnits)
        {
            int clamped = Math.Max(0, Math.Min(MaxUnits, currentUnits));
            return checked((int)(((long)clamped * 100 + MaxUnits / 2L) / MaxUnits));
        }

        public int UnitsFromLegacyPercent(int percent)
        {
            int clamped = Math.Max(0, Math.Min(100, percent));
            return checked((int)(((long)clamped * MaxUnits + 50L) / 100L));
        }

        private int RatioThresholdUnits(int basisPoints)
        {
            return checked((int)((long)MaxUnits * basisPoints / BasisPointDenominator));
        }

        private int RatioThresholdUnitsCeiling(int basisPoints)
        {
            return checked((int)(((long)MaxUnits * basisPoints + BasisPointDenominator - 1L) /
                                 BasisPointDenominator));
        }

        private string BuildFingerprint()
        {
            string drains = string.Join(",", _drains.Select(item =>
                $"{(int)item.Activity}:{item.UnitsPerGameMinute}"));
            string recoveries = string.Join(",", _recoveries.Select(item =>
                $"{(int)item.Activity}:{string.Join(";", item.InteractionIds.Select(Encode))}:" +
                $"{item.DurationGameMinutes}:" +
                $"{item.RecoveryUnitsPerGameMinute}:{item.SelectionWeight}"));
            return $"stamina-profile-v1|{MaxUnits}|{InitialUnits}|" +
                   $"{RecoveryThresholdBasisPoints}|{ResumeThresholdBasisPoints}|" +
                   $"{CautionThresholdBasisPoints}|" +
                   $"{drains}|{recoveries}";
        }

        private static string Encode(string value)
        {
            return value.Length + ":" + value;
        }
    }

    public sealed class CharacterStaminaCatalog
    {
        public const int DefaultMaxUnits = 10_000;
        public const int DefaultInitialUnits = 10_000;
        public const int DefaultRecoveryThresholdBasisPoints = 2_500;
        public const int DefaultResumeThresholdBasisPoints = 3_500;
        public const int DefaultCautionThresholdBasisPoints = 5_000;

        private readonly Dictionary<string, CharacterStaminaProfile> _overrides;

        public CharacterStaminaCatalog(
            CharacterStaminaProfile defaultProfile,
            IEnumerable<KeyValuePair<string, CharacterStaminaProfile>> overrides = null)
        {
            DefaultProfile = defaultProfile ?? throw new ArgumentNullException(nameof(defaultProfile));
            _overrides = new Dictionary<string, CharacterStaminaProfile>(StringComparer.Ordinal);
            if (overrides == null) return;
            foreach (KeyValuePair<string, CharacterStaminaProfile> entry in overrides)
            {
                string characterId = NormalizeId(entry.Key, nameof(overrides));
                if (entry.Value == null)
                    throw new ArgumentException("Stamina profile overrides cannot be null.", nameof(overrides));
                if (!_overrides.TryAdd(characterId, entry.Value))
                    throw new ArgumentException("Stamina profile override IDs must be unique.", nameof(overrides));
            }
        }

        public CharacterStaminaProfile DefaultProfile { get; }
        public IReadOnlyList<string> OverrideCharacterIds => _overrides.Keys
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        public CharacterStaminaProfile Resolve(string characterId)
        {
            string normalized = NormalizeId(characterId, nameof(characterId));
            return _overrides.TryGetValue(normalized, out CharacterStaminaProfile profile)
                ? profile
                : DefaultProfile;
        }

        public bool HasOverride(string characterId)
        {
            string normalized = NormalizeId(characterId, nameof(characterId));
            return _overrides.ContainsKey(normalized);
        }

        public static CharacterStaminaCatalog CreateCommonDefault()
        {
            return new CharacterStaminaCatalog(CreateCommonDefaultProfile());
        }

        public static CharacterStaminaProfile CreateCommonDefaultProfile()
        {
            return new CharacterStaminaProfile(
                DefaultMaxUnits,
                DefaultInitialUnits,
                DefaultRecoveryThresholdBasisPoints,
                DefaultResumeThresholdBasisPoints,
                DefaultCautionThresholdBasisPoints,
                new[]
                {
                    new StaminaDrainDefinition(StaminaActivityKind.Idle, 0),
                    new StaminaDrainDefinition(StaminaActivityKind.Walking, 4),
                    new StaminaDrainDefinition(StaminaActivityKind.DeskWork, 12),
                    // 16 units/minute reaches the 25% threshold after 469 minutes of focused
                    // work: 75% of the common bar is consumed within a normal office day while
                    // still leaving time for a placed-facility recovery before attendance ends.
                    new StaminaDrainDefinition(StaminaActivityKind.Typing, 16),
                    new StaminaDrainDefinition(StaminaActivityKind.Meeting, 16),
                    new StaminaDrainDefinition(StaminaActivityKind.Administration, 11),
                    new StaminaDrainDefinition(StaminaActivityKind.Reception, 10),
                    new StaminaDrainDefinition(StaminaActivityKind.Printing, 10),
                    new StaminaDrainDefinition(StaminaActivityKind.OutsideWork, 14),
                    new StaminaDrainDefinition(StaminaActivityKind.OffDuty, 0),
                    new StaminaDrainDefinition(StaminaActivityKind.Sleep, 0)
                },
                new[]
                {
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Water,
                        "water-drink",
                        4,
                        875,
                        35,
                        new[] { "vending-drink" }),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Restroom,
                        "restroom-use",
                        8,
                        500,
                        30),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Lounge,
                        "lounge-rest",
                        20,
                        250,
                        35)
                });
        }

        private static string NormalizeId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Character ID is required.", parameterName);
            return value.Trim();
        }
    }
}

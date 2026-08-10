using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.AutonomyNeeds
{
    public enum AutonomyNeedsMode
    {
        Work = 0,
        BreakRequest = 1,
        GoingToRecovery = 2,
        Recovering = 3,
        ReturnToWork = 4,
        Collapsed = 5,
        Absent = 6
    }

    public enum AutonomyNeedsDecisionKind
    {
        Work = 0,
        BreakRequest = 1,
        GoToLounge = 2,
        GoToWater = 3,
        GoToStretchArea = 4,
        LoungeRest = 5,
        DrinkWater = 6,
        Stretch = 7,
        ReturnToWork = 8,
        Collapse = 9,
        Absent = 10
    }

    public enum AutonomyNeedsDestination
    {
        None = 0,
        Workstation = 1,
        Lounge = 2,
        Water = 3,
        StretchArea = 4,
        Outside = 5
    }

    public enum AutonomyRecoveryActivity
    {
        None = 0,
        LoungeRest = 1,
        DrinkWater = 2,
        Stretch = 3
    }

    public enum AutonomyBreakCause
    {
        None = 0,
        Energy = 1,
        Stress = 2,
        Focus = 3,
        OptionalNeed = 4,
        Multiple = 5
    }

    public enum AutonomyCollapseCause
    {
        None = 0,
        EnergyDepleted = 1,
        FocusDepleted = 2,
        StressOverload = 3
    }

    public enum AutonomyWorkIntensity
    {
        OffDuty = 0,
        Light = 1,
        Normal = 2,
        Heavy = 3,
        Crunch = 4
    }

    public enum AutonomyRelationshipEventKind
    {
        Support = 0,
        Conflict = 1,
        Reconciliation = 2
    }

    public static class AutonomyOptionalNeedIds
    {
        public const string Hunger = "hunger";
        public const string Toilet = "toilet";
    }

    public readonly struct AutonomyOptionalNeedSignal
    {
        public AutonomyOptionalNeedSignal(string needId, int urgencyBasisPoints)
        {
            if (string.IsNullOrWhiteSpace(needId))
                throw new ArgumentException("Optional need ID is required.", nameof(needId));
            if (urgencyBasisPoints < 0 || urgencyBasisPoints > AutonomyNeedsRules.BasisPointDenominator)
                throw new ArgumentOutOfRangeException(nameof(urgencyBasisPoints));

            NeedId = needId;
            UrgencyBasisPoints = urgencyBasisPoints;
        }

        public string NeedId { get; }
        public int UrgencyBasisPoints { get; }
    }

    public sealed class AutonomyNeedsWorkContext
    {
        private readonly AutonomyOptionalNeedSignal[] _optionalNeeds;

        public AutonomyNeedsWorkContext(
            AutonomyWorkIntensity intensity,
            bool forceCrunch = false,
            IEnumerable<AutonomyOptionalNeedSignal> optionalNeeds = null)
        {
            if (!Enum.IsDefined(typeof(AutonomyWorkIntensity), intensity))
                throw new ArgumentOutOfRangeException(nameof(intensity));
            if (forceCrunch && intensity == AutonomyWorkIntensity.OffDuty)
                throw new ArgumentException("Off-duty time cannot force crunch work.", nameof(forceCrunch));

            Intensity = intensity;
            ForceCrunch = forceCrunch;
            _optionalNeeds = optionalNeeds == null
                ? Array.Empty<AutonomyOptionalNeedSignal>()
                : optionalNeeds.OrderBy(item => item.NeedId, StringComparer.Ordinal).ToArray();
            if (_optionalNeeds.Select(item => item.NeedId).Distinct(StringComparer.Ordinal).Count() != _optionalNeeds.Length)
                throw new ArgumentException("Optional need IDs must be unique.", nameof(optionalNeeds));
        }

        public AutonomyWorkIntensity Intensity { get; }
        public bool ForceCrunch { get; }
        public IReadOnlyList<AutonomyOptionalNeedSignal> OptionalNeeds => _optionalNeeds;

        public static AutonomyNeedsWorkContext NormalWork { get; } =
            new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Normal);

        public static AutonomyNeedsWorkContext OffDuty { get; } =
            new AutonomyNeedsWorkContext(AutonomyWorkIntensity.OffDuty);
    }

    public sealed class AutonomyTimedRelationshipEvent
    {
        public AutonomyTimedRelationshipEvent(
            string eventId,
            long dueMinute,
            AutonomyRelationshipEventKind kind,
            int magnitude)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("Relationship event ID is required.", nameof(eventId));
            if (dueMinute < 0) throw new ArgumentOutOfRangeException(nameof(dueMinute));
            if (!Enum.IsDefined(typeof(AutonomyRelationshipEventKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (magnitude < 1 || magnitude > 100)
                throw new ArgumentOutOfRangeException(nameof(magnitude));

            EventId = eventId;
            DueMinute = dueMinute;
            Kind = kind;
            Magnitude = magnitude;
        }

        public string EventId { get; }
        public long DueMinute { get; }
        public AutonomyRelationshipEventKind Kind { get; }
        public int Magnitude { get; }
    }

    public readonly struct AutonomyNeedsDecisionEvent
    {
        public AutonomyNeedsDecisionEvent(
            long minute,
            long sequence,
            AutonomyNeedsDecisionKind decision,
            AutonomyNeedsMode mode,
            AutonomyNeedsDestination destination,
            AutonomyRecoveryActivity recoveryActivity,
            AutonomyBreakCause breakCause,
            AutonomyCollapseCause collapseCause)
        {
            Minute = minute;
            Sequence = sequence;
            Decision = decision;
            Mode = mode;
            Destination = destination;
            RecoveryActivity = recoveryActivity;
            BreakCause = breakCause;
            CollapseCause = collapseCause;
        }

        public long Minute { get; }
        public long Sequence { get; }
        public AutonomyNeedsDecisionKind Decision { get; }
        public AutonomyNeedsMode Mode { get; }
        public AutonomyNeedsDestination Destination { get; }
        public AutonomyRecoveryActivity RecoveryActivity { get; }
        public AutonomyBreakCause BreakCause { get; }
        public AutonomyCollapseCause CollapseCause { get; }
    }

    public sealed class AutonomyNeedsAdvanceResult
    {
        internal AutonomyNeedsAdvanceResult(
            long fromMinute,
            long toMinute,
            int workedMinutes,
            int crunchMinutes,
            long effectiveWorkBasisPointMinutes,
            int peakRiskBasisPoints,
            IReadOnlyList<AutonomyNeedsDecisionEvent> decisions)
        {
            FromMinute = fromMinute;
            ToMinute = toMinute;
            WorkedMinutes = workedMinutes;
            CrunchMinutes = crunchMinutes;
            EffectiveWorkBasisPointMinutes = effectiveWorkBasisPointMinutes;
            PeakRiskBasisPoints = peakRiskBasisPoints;
            Decisions = decisions ?? Array.Empty<AutonomyNeedsDecisionEvent>();
        }

        public long FromMinute { get; }
        public long ToMinute { get; }
        public int WorkedMinutes { get; }
        public int CrunchMinutes { get; }
        public long EffectiveWorkBasisPointMinutes { get; }
        public int PeakRiskBasisPoints { get; }
        public IReadOnlyList<AutonomyNeedsDecisionEvent> Decisions { get; }
    }

    public sealed class AutonomyNeedsState
    {
        internal AutonomyNeedsState(
            int energyBasisPoints,
            int stressBasisPoints,
            int focusBasisPoints,
            long lastProcessedMinute)
        {
            EnergyBasisPoints = AutonomyNeedsRules.ClampBasisPoints(energyBasisPoints);
            StressBasisPoints = AutonomyNeedsRules.ClampBasisPoints(stressBasisPoints);
            FocusBasisPoints = AutonomyNeedsRules.ClampBasisPoints(focusBasisPoints);
            LastProcessedMinute = lastProcessedMinute;
            Mode = AutonomyNeedsMode.Work;
            ModeStartedMinute = lastProcessedMinute;
            AbsenceUntilMinute = -1;
        }

        public int EnergyBasisPoints { get; internal set; }
        public int StressBasisPoints { get; internal set; }
        public int FocusBasisPoints { get; internal set; }
        public int FatigueBasisPoints => AutonomyNeedsRules.BasisPointDenominator - EnergyBasisPoints;
        public AutonomyNeedsMode Mode { get; internal set; }
        public AutonomyRecoveryActivity RecoveryActivity { get; internal set; }
        public AutonomyBreakCause BreakCause { get; internal set; }
        public AutonomyCollapseCause LastCollapseCause { get; internal set; }
        public long LastProcessedMinute { get; internal set; }
        public long ModeStartedMinute { get; internal set; }
        public long CooldownUntilMinute { get; internal set; }
        public long AbsenceUntilMinute { get; internal set; }
        public int CurrentRiskBasisPoints { get; internal set; }
        public int CollapseCount { get; internal set; }
        public int BreakSequence { get; internal set; }
        public int CompletedBreaks { get; internal set; }
        public long CumulativeCrunchMinutes { get; internal set; }
        public long CumulativeEffectiveWorkBasisPointMinutes { get; internal set; }
    }

    public sealed class AutonomyNeedsTransientState
    {
        internal AutonomyNeedsTransientState()
        {
            ActiveRequestToken = string.Empty;
        }

        public long LastDecisionSequence { get; internal set; }
        public string ActiveRequestToken { get; internal set; }
        public AutonomyNeedsDecisionEvent? LastDecision { get; internal set; }

        internal void Reset()
        {
            LastDecisionSequence = 0;
            ActiveRequestToken = string.Empty;
            LastDecision = null;
        }
    }

    [Serializable]
    public sealed class AutonomyNeedsPersistentSnapshotDto
    {
        public int schemaVersion = AutonomyNeedsRules.PersistentSnapshotSchemaVersion;
        public string memberId = string.Empty;
        public int worldSeed = 0;
        public int energyBasisPoints = 0;
        public int stressBasisPoints = 0;
        public int focusBasisPoints = 0;
        public int mode = 0;
        public int recoveryActivity = 0;
        public int breakCause = 0;
        public int lastCollapseCause = 0;
        public long lastProcessedMinute = 0;
        public long modeStartedMinute = 0;
        public long cooldownUntilMinute = 0;
        public long absenceUntilMinute = -1;
        public int currentRiskBasisPoints = 0;
        public int collapseCount = 0;
        public int breakSequence = 0;
        public int completedBreaks = 0;
        public long cumulativeCrunchMinutes = 0;
        public long cumulativeEffectiveWorkBasisPointMinutes = 0;
    }
}

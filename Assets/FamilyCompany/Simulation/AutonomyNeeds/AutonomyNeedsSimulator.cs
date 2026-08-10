using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.AutonomyNeeds
{
    public static class AutonomyNeedsRules
    {
        public const int BasisPointDenominator = 10_000;
        public const int PersistentSnapshotSchemaVersion = 1;
        public const int OptionalNeedBreakUrgencyBasisPoints = 8_500;
        public const int TransitToRecoveryMinutes = 2;
        public const int ReturnToWorkMinutes = 1;
        public const int CollapseTransitionMinutes = 1;

        internal static int ClampBasisPoints(int value)
        {
            return Math.Max(0, Math.Min(BasisPointDenominator, value));
        }
    }

    public sealed class AutonomyNeedsSimulator
    {
        private readonly int _worldSeed;
        private readonly FamilyAutonomyNeedsProfile _profile;

        private AutonomyNeedsSimulator(
            int worldSeed,
            FamilyAutonomyNeedsProfile profile,
            AutonomyNeedsState state)
        {
            _worldSeed = worldSeed;
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Transient = new AutonomyNeedsTransientState();
        }

        public int WorldSeed => _worldSeed;
        public string MemberId => _profile.MemberId;
        public FamilyAutonomyNeedsProfile Profile => _profile;
        public AutonomyNeedsState State { get; }
        public AutonomyNeedsTransientState Transient { get; }

        public static AutonomyNeedsSimulator CreateDefault(
            int worldSeed,
            string memberId,
            long startMinute = 0)
        {
            if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            var profile = FamilyAutonomyNeedsProfileCatalog.Get(memberId);
            return new AutonomyNeedsSimulator(
                worldSeed,
                profile,
                new AutonomyNeedsState(
                    profile.InitialEnergyBasisPoints,
                    profile.InitialStressBasisPoints,
                    profile.InitialFocusBasisPoints,
                    startMinute));
        }

        public static AutonomyNeedsSimulator Create(
            int worldSeed,
            string memberId,
            long startMinute,
            int energyBasisPoints,
            int stressBasisPoints,
            int focusBasisPoints)
        {
            if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            ValidateBasisPoints(energyBasisPoints, nameof(energyBasisPoints));
            ValidateBasisPoints(stressBasisPoints, nameof(stressBasisPoints));
            ValidateBasisPoints(focusBasisPoints, nameof(focusBasisPoints));
            var profile = FamilyAutonomyNeedsProfileCatalog.Get(memberId);
            return new AutonomyNeedsSimulator(
                worldSeed,
                profile,
                new AutonomyNeedsState(
                    energyBasisPoints,
                    stressBasisPoints,
                    focusBasisPoints,
                    startMinute));
        }

        public static AutonomyNeedsSimulator Restore(AutonomyNeedsPersistentSnapshotDto snapshot)
        {
            ValidateSnapshot(snapshot);
            var profile = FamilyAutonomyNeedsProfileCatalog.Get(snapshot.memberId);
            var state = new AutonomyNeedsState(
                snapshot.energyBasisPoints,
                snapshot.stressBasisPoints,
                snapshot.focusBasisPoints,
                snapshot.lastProcessedMinute)
            {
                Mode = (AutonomyNeedsMode)snapshot.mode,
                RecoveryActivity = (AutonomyRecoveryActivity)snapshot.recoveryActivity,
                BreakCause = (AutonomyBreakCause)snapshot.breakCause,
                LastCollapseCause = (AutonomyCollapseCause)snapshot.lastCollapseCause,
                ModeStartedMinute = snapshot.modeStartedMinute,
                CooldownUntilMinute = snapshot.cooldownUntilMinute,
                AbsenceUntilMinute = snapshot.absenceUntilMinute,
                CurrentRiskBasisPoints = snapshot.currentRiskBasisPoints,
                CollapseCount = snapshot.collapseCount,
                BreakSequence = snapshot.breakSequence,
                CompletedBreaks = snapshot.completedBreaks,
                CumulativeCrunchMinutes = snapshot.cumulativeCrunchMinutes,
                CumulativeEffectiveWorkBasisPointMinutes = snapshot.cumulativeEffectiveWorkBasisPointMinutes
            };
            return new AutonomyNeedsSimulator(snapshot.worldSeed, profile, state);
        }

        public AutonomyNeedsPersistentSnapshotDto ExportPersistentSnapshot()
        {
            return new AutonomyNeedsPersistentSnapshotDto
            {
                schemaVersion = AutonomyNeedsRules.PersistentSnapshotSchemaVersion,
                memberId = MemberId,
                worldSeed = _worldSeed,
                energyBasisPoints = State.EnergyBasisPoints,
                stressBasisPoints = State.StressBasisPoints,
                focusBasisPoints = State.FocusBasisPoints,
                mode = (int)State.Mode,
                recoveryActivity = (int)State.RecoveryActivity,
                breakCause = (int)State.BreakCause,
                lastCollapseCause = (int)State.LastCollapseCause,
                lastProcessedMinute = State.LastProcessedMinute,
                modeStartedMinute = State.ModeStartedMinute,
                cooldownUntilMinute = State.CooldownUntilMinute,
                absenceUntilMinute = State.AbsenceUntilMinute,
                currentRiskBasisPoints = State.CurrentRiskBasisPoints,
                collapseCount = State.CollapseCount,
                breakSequence = State.BreakSequence,
                completedBreaks = State.CompletedBreaks,
                cumulativeCrunchMinutes = State.CumulativeCrunchMinutes,
                cumulativeEffectiveWorkBasisPointMinutes = State.CumulativeEffectiveWorkBasisPointMinutes
            };
        }

        public AutonomyNeedsAdvanceResult AdvanceTo(
            long targetMinute,
            AutonomyNeedsWorkContext context,
            IEnumerable<AutonomyTimedRelationshipEvent> relationshipEvents = null)
        {
            if (targetMinute < State.LastProcessedMinute)
                throw new InvalidOperationException("Autonomy-needs time cannot move backwards.");
            if (context == null) throw new ArgumentNullException(nameof(context));

            var fromMinute = State.LastProcessedMinute;
            var sortedEvents = PrepareEvents(relationshipEvents);
            var eventIndex = 0;
            while (eventIndex < sortedEvents.Length && sortedEvents[eventIndex].DueMinute <= fromMinute)
                eventIndex++;

            var decisions = new List<AutonomyNeedsDecisionEvent>();
            var workedMinutes = 0;
            var crunchMinutes = 0;
            var effectiveWork = 0L;
            var peakRisk = State.CurrentRiskBasisPoints;

            for (var minute = checked(fromMinute + 1); minute <= targetMinute; minute++)
            {
                while (eventIndex < sortedEvents.Length && sortedEvents[eventIndex].DueMinute == minute)
                {
                    ApplyRelationshipEvent(sortedEvents[eventIndex]);
                    eventIndex++;
                }

                if (!IsProtectedRecoveryMode(State.Mode) && TryCollapse(minute, decisions))
                {
                    State.LastProcessedMinute = minute;
                    peakRisk = Math.Max(peakRisk, State.CurrentRiskBasisPoints);
                    continue;
                }

                switch (State.Mode)
                {
                    case AutonomyNeedsMode.Work:
                        ApplyWorkMinute(minute, context, decisions, ref workedMinutes, ref crunchMinutes, ref effectiveWork);
                        break;
                    case AutonomyNeedsMode.BreakRequest:
                        ApplyBreakRequestMinute(minute, decisions);
                        break;
                    case AutonomyNeedsMode.GoingToRecovery:
                        ApplyTransitMinute(minute, decisions);
                        break;
                    case AutonomyNeedsMode.Recovering:
                        ApplyRecoveryMinute(minute, decisions);
                        break;
                    case AutonomyNeedsMode.ReturnToWork:
                        ApplyReturnMinute(minute, decisions);
                        break;
                    case AutonomyNeedsMode.Collapsed:
                        ApplyCollapsedMinute(minute, decisions);
                        break;
                    case AutonomyNeedsMode.Absent:
                        ApplyAbsentMinute(minute, decisions);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported autonomy-needs mode: {State.Mode}");
                }

                State.LastProcessedMinute = minute;
                peakRisk = Math.Max(peakRisk, State.CurrentRiskBasisPoints);
            }

            State.CumulativeCrunchMinutes = checked(State.CumulativeCrunchMinutes + crunchMinutes);
            State.CumulativeEffectiveWorkBasisPointMinutes =
                checked(State.CumulativeEffectiveWorkBasisPointMinutes + effectiveWork);

            return new AutonomyNeedsAdvanceResult(
                fromMinute,
                targetMinute,
                workedMinutes,
                crunchMinutes,
                effectiveWork,
                peakRisk,
                decisions.ToArray());
        }

        private void ApplyWorkMinute(
            long minute,
            AutonomyNeedsWorkContext context,
            List<AutonomyNeedsDecisionEvent> decisions,
            ref int workedMinutes,
            ref int crunchMinutes,
            ref long effectiveWork)
        {
            if (context.Intensity == AutonomyWorkIntensity.OffDuty)
            {
                ChangeCoreNeeds(3, -3, 2);
                ReduceRisk(5);
                return;
            }

            var rates = WorkRates(context.Intensity);
            ChangeCoreNeeds(
                -ScalePositiveRate(rates.energyDrain, _profile.EnergyDrainMultiplierBasisPoints),
                ScalePositiveRate(rates.stressGain, _profile.StressGainMultiplierBasisPoints),
                -ScalePositiveRate(rates.focusDrain, _profile.FocusDrainMultiplierBasisPoints));

            workedMinutes++;
            var isCrunch = context.ForceCrunch || context.Intensity == AutonomyWorkIntensity.Crunch;
            if (isCrunch) crunchMinutes++;
            UpdateWorkRisk(context);

            if (TryCollapse(minute, decisions)) return;

            var efficiency = CalculateEfficiencyBasisPoints(context);
            effectiveWork = checked(effectiveWork + efficiency);
            if (ShouldRequestBreak(context, out var cause))
            {
                State.BreakSequence = checked(State.BreakSequence + 1);
                State.BreakCause = cause;
                State.RecoveryActivity = ChooseRecoveryActivity(minute);
                TransitionTo(AutonomyNeedsMode.BreakRequest, minute);
                Transient.ActiveRequestToken =
                    $"autonomy-needs:{_worldSeed}:{MemberId}:{State.BreakSequence}:{minute}";
                Emit(
                    decisions,
                    minute,
                    AutonomyNeedsDecisionKind.BreakRequest,
                    AutonomyNeedsDestination.None,
                    State.RecoveryActivity,
                    State.BreakCause,
                    AutonomyCollapseCause.None);
            }
        }

        private void ApplyBreakRequestMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            ChangeCoreNeeds(-1, 0, 0);
            if (minute - State.ModeStartedMinute < 1) return;
            TransitionTo(AutonomyNeedsMode.GoingToRecovery, minute);
            Emit(
                decisions,
                minute,
                MoveDecision(State.RecoveryActivity),
                DestinationFor(State.RecoveryActivity),
                State.RecoveryActivity,
                State.BreakCause,
                AutonomyCollapseCause.None);
        }

        private void ApplyTransitMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            ChangeCoreNeeds(-1, 0, 0);
            if (TryCollapse(minute, decisions)) return;
            if (minute - State.ModeStartedMinute < AutonomyNeedsRules.TransitToRecoveryMinutes) return;
            TransitionTo(AutonomyNeedsMode.Recovering, minute);
            Emit(
                decisions,
                minute,
                RecoveryDecision(State.RecoveryActivity),
                DestinationFor(State.RecoveryActivity),
                State.RecoveryActivity,
                State.BreakCause,
                AutonomyCollapseCause.None);
        }

        private void ApplyRecoveryMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            var rates = RecoveryRates(State.RecoveryActivity);
            ChangeCoreNeeds(
                ScalePositiveRate(rates.energyGain, _profile.RecoveryMultiplierBasisPoints),
                -ScalePositiveRate(rates.stressReduction, _profile.RecoveryMultiplierBasisPoints),
                ScalePositiveRate(rates.focusGain, _profile.RecoveryMultiplierBasisPoints));
            ReduceRisk(18);

            if (minute - State.ModeStartedMinute < _profile.MinimumBreakMinutes || !CanResumeWork()) return;
            TransitionTo(AutonomyNeedsMode.ReturnToWork, minute);
            Emit(
                decisions,
                minute,
                AutonomyNeedsDecisionKind.ReturnToWork,
                AutonomyNeedsDestination.Workstation,
                State.RecoveryActivity,
                State.BreakCause,
                AutonomyCollapseCause.None);
        }

        private void ApplyReturnMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            if (minute - State.ModeStartedMinute < AutonomyNeedsRules.ReturnToWorkMinutes) return;
            if (State.BreakCause != AutonomyBreakCause.None)
                State.CompletedBreaks = checked(State.CompletedBreaks + 1);
            State.CooldownUntilMinute = checked(minute + _profile.BreakCooldownMinutes);
            State.RecoveryActivity = AutonomyRecoveryActivity.None;
            State.BreakCause = AutonomyBreakCause.None;
            Transient.ActiveRequestToken = string.Empty;
            TransitionTo(AutonomyNeedsMode.Work, minute);
            Emit(
                decisions,
                minute,
                AutonomyNeedsDecisionKind.Work,
                AutonomyNeedsDestination.Workstation,
                AutonomyRecoveryActivity.None,
                AutonomyBreakCause.None,
                AutonomyCollapseCause.None);
        }

        private void ApplyCollapsedMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            ChangeCoreNeeds(8, -8, 6);
            ReduceRisk(8);
            if (minute - State.ModeStartedMinute < AutonomyNeedsRules.CollapseTransitionMinutes) return;
            TransitionTo(AutonomyNeedsMode.Absent, minute);
            Emit(
                decisions,
                minute,
                AutonomyNeedsDecisionKind.Absent,
                AutonomyNeedsDestination.Outside,
                AutonomyRecoveryActivity.None,
                AutonomyBreakCause.None,
                State.LastCollapseCause);
        }

        private void ApplyAbsentMinute(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            ChangeCoreNeeds(14, -12, 10);
            ReduceRisk(25);
            if (minute < State.AbsenceUntilMinute || !CanResumeWork()) return;
            TransitionTo(AutonomyNeedsMode.ReturnToWork, minute);
            Emit(
                decisions,
                minute,
                AutonomyNeedsDecisionKind.ReturnToWork,
                AutonomyNeedsDestination.Workstation,
                AutonomyRecoveryActivity.None,
                AutonomyBreakCause.None,
                State.LastCollapseCause);
        }

        private bool TryCollapse(long minute, List<AutonomyNeedsDecisionEvent> decisions)
        {
            var cause = CollapseCause();
            if (cause == AutonomyCollapseCause.None) return false;
            State.CollapseCount = checked(State.CollapseCount + 1);
            State.LastCollapseCause = cause;
            State.AbsenceUntilMinute = checked(minute + _profile.AbsenceMinutes);
            State.RecoveryActivity = AutonomyRecoveryActivity.None;
            State.BreakCause = AutonomyBreakCause.None;
            State.CurrentRiskBasisPoints = AutonomyNeedsRules.BasisPointDenominator;
            Transient.ActiveRequestToken = string.Empty;
            TransitionTo(AutonomyNeedsMode.Collapsed, minute);
            Emit(
                decisions,
                minute,
                AutonomyNeedsDecisionKind.Collapse,
                AutonomyNeedsDestination.None,
                AutonomyRecoveryActivity.None,
                AutonomyBreakCause.None,
                cause);
            return true;
        }

        private AutonomyCollapseCause CollapseCause()
        {
            if (State.EnergyBasisPoints <= 0) return AutonomyCollapseCause.EnergyDepleted;
            if (State.FocusBasisPoints <= 0) return AutonomyCollapseCause.FocusDepleted;
            if (State.StressBasisPoints >= AutonomyNeedsRules.BasisPointDenominator)
                return AutonomyCollapseCause.StressOverload;
            return AutonomyCollapseCause.None;
        }

        private bool ShouldRequestBreak(AutonomyNeedsWorkContext context, out AutonomyBreakCause cause)
        {
            cause = DetermineBreakCause(context.OptionalNeeds);
            if (cause == AutonomyBreakCause.None) return false;
            if (context.ForceCrunch || State.LastProcessedMinute + 1 < State.CooldownUntilMinute) return false;
            return true;
        }

        private AutonomyBreakCause DetermineBreakCause(IReadOnlyList<AutonomyOptionalNeedSignal> optionalNeeds)
        {
            var count = 0;
            var result = AutonomyBreakCause.None;
            if (State.EnergyBasisPoints <= _profile.BreakEnergyBasisPoints)
            {
                count++;
                result = AutonomyBreakCause.Energy;
            }
            if (State.StressBasisPoints >= _profile.BreakStressBasisPoints)
            {
                count++;
                result = AutonomyBreakCause.Stress;
            }
            if (State.FocusBasisPoints <= _profile.BreakFocusBasisPoints)
            {
                count++;
                result = AutonomyBreakCause.Focus;
            }
            if (optionalNeeds.Any(item => item.UrgencyBasisPoints >= AutonomyNeedsRules.OptionalNeedBreakUrgencyBasisPoints))
            {
                count++;
                result = AutonomyBreakCause.OptionalNeed;
            }
            return count > 1 ? AutonomyBreakCause.Multiple : result;
        }

        private bool CanResumeWork()
        {
            return State.EnergyBasisPoints >= _profile.ResumeEnergyBasisPoints &&
                   State.StressBasisPoints <= _profile.ResumeStressBasisPoints &&
                   State.FocusBasisPoints >= _profile.ResumeFocusBasisPoints;
        }

        private AutonomyRecoveryActivity ChooseRecoveryActivity(long minute)
        {
            if (State.EnergyBasisPoints <= _profile.BreakEnergyBasisPoints - 800)
                return AutonomyRecoveryActivity.LoungeRest;
            if (State.FocusBasisPoints <= _profile.BreakFocusBasisPoints - 600)
                return AutonomyRecoveryActivity.DrinkWater;
            if (State.StressBasisPoints >= _profile.BreakStressBasisPoints + 800)
                return AutonomyRecoveryActivity.Stretch;

            var roll = StableRandom.StableRandomInt(
                $"autonomy-needs-recovery-v1:{_worldSeed}:{MemberId}:{State.BreakSequence}:{minute}",
                100);
            if (roll < _profile.LoungeWeight) return AutonomyRecoveryActivity.LoungeRest;
            if (roll < _profile.LoungeWeight + _profile.WaterWeight)
                return AutonomyRecoveryActivity.DrinkWater;
            return AutonomyRecoveryActivity.Stretch;
        }

        private int CalculateEfficiencyBasisPoints(AutonomyNeedsWorkContext context)
        {
            var intensityBase = context.Intensity switch
            {
                AutonomyWorkIntensity.Light => 8_500,
                AutonomyWorkIntensity.Normal => 10_000,
                AutonomyWorkIntensity.Heavy => 10_800,
                AutonomyWorkIntensity.Crunch => 11_200,
                _ => 0
            };
            var energyFactor = 4_000 + State.EnergyBasisPoints * 6_000 / AutonomyNeedsRules.BasisPointDenominator;
            var focusFactor = 2_500 + State.FocusBasisPoints * 7_500 / AutonomyNeedsRules.BasisPointDenominator;
            var stressFactor = 10_000 - State.StressBasisPoints * 5_500 / AutonomyNeedsRules.BasisPointDenominator;
            var result = ScaleBasisPoints(intensityBase, energyFactor);
            result = ScaleBasisPoints(result, focusFactor);
            result = ScaleBasisPoints(result, stressFactor);
            if (context.ForceCrunch && DetermineBreakCause(context.OptionalNeeds) != AutonomyBreakCause.None)
                result = ScaleBasisPoints(result, 8_500);
            return Math.Max(0, Math.Min(12_000, result));
        }

        private void UpdateWorkRisk(AutonomyNeedsWorkContext context)
        {
            var delta = 1;
            if (context.Intensity == AutonomyWorkIntensity.Heavy) delta += 2;
            if (context.Intensity == AutonomyWorkIntensity.Crunch) delta += 5;
            if (context.ForceCrunch) delta += 8;
            if (State.EnergyBasisPoints <= _profile.BreakEnergyBasisPoints) delta += 7;
            if (State.StressBasisPoints >= _profile.BreakStressBasisPoints) delta += 7;
            if (State.FocusBasisPoints <= _profile.BreakFocusBasisPoints) delta += 6;
            State.CurrentRiskBasisPoints = AutonomyNeedsRules.ClampBasisPoints(State.CurrentRiskBasisPoints + delta);
        }

        private void ReduceRisk(int amount)
        {
            State.CurrentRiskBasisPoints = Math.Max(0, State.CurrentRiskBasisPoints - amount);
        }

        private void ApplyRelationshipEvent(AutonomyTimedRelationshipEvent relationshipEvent)
        {
            switch (relationshipEvent.Kind)
            {
                case AutonomyRelationshipEventKind.Support:
                    ChangeCoreNeeds(
                        relationshipEvent.Magnitude * 2,
                        -relationshipEvent.Magnitude * 10,
                        relationshipEvent.Magnitude * 5);
                    break;
                case AutonomyRelationshipEventKind.Conflict:
                    ChangeCoreNeeds(
                        -relationshipEvent.Magnitude * 2,
                        relationshipEvent.Magnitude * 12,
                        -relationshipEvent.Magnitude * 6);
                    break;
                case AutonomyRelationshipEventKind.Reconciliation:
                    ChangeCoreNeeds(
                        relationshipEvent.Magnitude,
                        -relationshipEvent.Magnitude * 8,
                        relationshipEvent.Magnitude * 4);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relationshipEvent));
            }
        }

        private void ChangeCoreNeeds(int energyDelta, int stressDelta, int focusDelta)
        {
            State.EnergyBasisPoints = AutonomyNeedsRules.ClampBasisPoints(State.EnergyBasisPoints + energyDelta);
            State.StressBasisPoints = AutonomyNeedsRules.ClampBasisPoints(State.StressBasisPoints + stressDelta);
            State.FocusBasisPoints = AutonomyNeedsRules.ClampBasisPoints(State.FocusBasisPoints + focusDelta);
        }

        private void TransitionTo(AutonomyNeedsMode mode, long minute)
        {
            State.Mode = mode;
            State.ModeStartedMinute = minute;
        }

        private void Emit(
            List<AutonomyNeedsDecisionEvent> decisions,
            long minute,
            AutonomyNeedsDecisionKind decision,
            AutonomyNeedsDestination destination,
            AutonomyRecoveryActivity recoveryActivity,
            AutonomyBreakCause breakCause,
            AutonomyCollapseCause collapseCause)
        {
            var sequence = checked(Transient.LastDecisionSequence + 1);
            var item = new AutonomyNeedsDecisionEvent(
                minute,
                sequence,
                decision,
                State.Mode,
                destination,
                recoveryActivity,
                breakCause,
                collapseCause);
            Transient.LastDecisionSequence = sequence;
            Transient.LastDecision = item;
            decisions.Add(item);
        }

        private static AutonomyTimedRelationshipEvent[] PrepareEvents(
            IEnumerable<AutonomyTimedRelationshipEvent> relationshipEvents)
        {
            if (relationshipEvents == null) return Array.Empty<AutonomyTimedRelationshipEvent>();
            var items = relationshipEvents.ToArray();
            if (items.Any(item => item == null))
                throw new ArgumentException("Relationship events cannot contain null.", nameof(relationshipEvents));
            if (items.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("Relationship event IDs must be unique.", nameof(relationshipEvents));
            return items
                .OrderBy(item => item.DueMinute)
                .ThenBy(item => item.EventId, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ValidateSnapshot(AutonomyNeedsPersistentSnapshotDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.schemaVersion != AutonomyNeedsRules.PersistentSnapshotSchemaVersion)
                throw new InvalidOperationException($"Unsupported autonomy-needs snapshot schema: {snapshot.schemaVersion}");
            FamilyAutonomyNeedsProfileCatalog.Get(snapshot.memberId);
            ValidateBasisPoints(snapshot.energyBasisPoints, nameof(snapshot.energyBasisPoints));
            ValidateBasisPoints(snapshot.stressBasisPoints, nameof(snapshot.stressBasisPoints));
            ValidateBasisPoints(snapshot.focusBasisPoints, nameof(snapshot.focusBasisPoints));
            ValidateBasisPoints(snapshot.currentRiskBasisPoints, nameof(snapshot.currentRiskBasisPoints));
            ValidateEnum<AutonomyNeedsMode>(snapshot.mode, nameof(snapshot.mode));
            ValidateEnum<AutonomyRecoveryActivity>(snapshot.recoveryActivity, nameof(snapshot.recoveryActivity));
            ValidateEnum<AutonomyBreakCause>(snapshot.breakCause, nameof(snapshot.breakCause));
            ValidateEnum<AutonomyCollapseCause>(snapshot.lastCollapseCause, nameof(snapshot.lastCollapseCause));
            if (snapshot.lastProcessedMinute < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.lastProcessedMinute));
            if (snapshot.modeStartedMinute < 0 || snapshot.modeStartedMinute > snapshot.lastProcessedMinute)
                throw new ArgumentOutOfRangeException(nameof(snapshot.modeStartedMinute));
            if (snapshot.cooldownUntilMinute < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.cooldownUntilMinute));
            if (snapshot.absenceUntilMinute < -1) throw new ArgumentOutOfRangeException(nameof(snapshot.absenceUntilMinute));
            if (snapshot.collapseCount < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.collapseCount));
            if (snapshot.breakSequence < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.breakSequence));
            if (snapshot.completedBreaks < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.completedBreaks));
            if (snapshot.cumulativeCrunchMinutes < 0) throw new ArgumentOutOfRangeException(nameof(snapshot.cumulativeCrunchMinutes));
            if (snapshot.cumulativeEffectiveWorkBasisPointMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(snapshot.cumulativeEffectiveWorkBasisPointMinutes));
        }

        private static void ValidateBasisPoints(int value, string parameterName)
        {
            if (value < 0 || value > AutonomyNeedsRules.BasisPointDenominator)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(parameterName);
        }

        private static bool IsProtectedRecoveryMode(AutonomyNeedsMode mode)
        {
            return mode == AutonomyNeedsMode.Recovering ||
                   mode == AutonomyNeedsMode.Collapsed ||
                   mode == AutonomyNeedsMode.Absent;
        }

        private static (int energyDrain, int stressGain, int focusDrain) WorkRates(AutonomyWorkIntensity intensity)
        {
            return intensity switch
            {
                AutonomyWorkIntensity.Light => (4, 2, 3),
                AutonomyWorkIntensity.Normal => (7, 4, 5),
                AutonomyWorkIntensity.Heavy => (11, 8, 8),
                AutonomyWorkIntensity.Crunch => (14, 12, 11),
                _ => (0, 0, 0)
            };
        }

        private static (int energyGain, int stressReduction, int focusGain) RecoveryRates(
            AutonomyRecoveryActivity activity)
        {
            return activity switch
            {
                AutonomyRecoveryActivity.LoungeRest => (12, 10, 7),
                AutonomyRecoveryActivity.DrinkWater => (7, 6, 14),
                AutonomyRecoveryActivity.Stretch => (8, 8, 8),
                _ => throw new InvalidOperationException("A recovery activity is required while recovering.")
            };
        }

        private static int ScalePositiveRate(int rate, int multiplierBasisPoints)
        {
            if (rate <= 0) return 0;
            return Math.Max(1, checked((rate * multiplierBasisPoints + 5_000) / 10_000));
        }

        private static int ScaleBasisPoints(int value, int multiplierBasisPoints)
        {
            return checked((int)((long)value * multiplierBasisPoints / AutonomyNeedsRules.BasisPointDenominator));
        }

        private static AutonomyNeedsDestination DestinationFor(AutonomyRecoveryActivity activity)
        {
            return activity switch
            {
                AutonomyRecoveryActivity.LoungeRest => AutonomyNeedsDestination.Lounge,
                AutonomyRecoveryActivity.DrinkWater => AutonomyNeedsDestination.Water,
                AutonomyRecoveryActivity.Stretch => AutonomyNeedsDestination.StretchArea,
                _ => AutonomyNeedsDestination.None
            };
        }

        private static AutonomyNeedsDecisionKind MoveDecision(AutonomyRecoveryActivity activity)
        {
            return activity switch
            {
                AutonomyRecoveryActivity.LoungeRest => AutonomyNeedsDecisionKind.GoToLounge,
                AutonomyRecoveryActivity.DrinkWater => AutonomyNeedsDecisionKind.GoToWater,
                AutonomyRecoveryActivity.Stretch => AutonomyNeedsDecisionKind.GoToStretchArea,
                _ => throw new InvalidOperationException("A recovery activity is required for movement.")
            };
        }

        private static AutonomyNeedsDecisionKind RecoveryDecision(AutonomyRecoveryActivity activity)
        {
            return activity switch
            {
                AutonomyRecoveryActivity.LoungeRest => AutonomyNeedsDecisionKind.LoungeRest,
                AutonomyRecoveryActivity.DrinkWater => AutonomyNeedsDecisionKind.DrinkWater,
                AutonomyRecoveryActivity.Stretch => AutonomyNeedsDecisionKind.Stretch,
                _ => throw new InvalidOperationException("A recovery activity is required.")
            };
        }
    }
}

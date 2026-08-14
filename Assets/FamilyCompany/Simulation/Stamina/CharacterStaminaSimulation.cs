using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Stamina
{
    public enum StaminaRecoveryPhase
    {
        Working = 0,
        RecoveryRequested = 1,
        SafeStopping = 2,
        StandingUp = 3,
        Traveling = 4,
        Aligning = 5,
        Performing = 6,
        ReturningToAssignedSeat = 7
    }

    public enum StaminaTransitionKind
    {
        RecoveryRequested = 0,
        RecoveryPlanAccepted = 1,
        SafeStopCompleted = 2,
        StandUpCompleted = 3,
        FacilityArrived = 4,
        PerformingStarted = 5,
        RecoveryProgressed = 6,
        RecoveryReadyToComplete = 7,
        InteractionCompleted = 8,
        AssignedSeatReturned = 9,
        RecoveryPlanAborted = 10,
        RecoverySelectionFailed = 11,
        AssignedSeatReturnRetryRequested = 12,
        ActivityChanged = 13,
        RecoveryRetryReady = 14
    }

    public enum StaminaRecoveryAbortReason
    {
        None = 0,
        ReservationUnavailable = 1,
        PathUnavailable = 2,
        ArrivalInvalid = 3,
        LayoutChanged = 4,
        Interrupted = 5,
        RuntimeReset = 6,
        CompletionRejected = 7,
        AssignedSeatUnavailable = 8
    }

    public readonly struct StaminaTransition
    {
        public StaminaTransition(
            long minute,
            StaminaTransitionKind kind,
            StaminaRecoveryPhase phase,
            StaminaRecoveryActivity activity,
            int currentUnits,
            int unitsDelta = 0,
            StaminaRecoveryAbortReason abortReason = StaminaRecoveryAbortReason.None)
        {
            Minute = minute;
            Kind = kind;
            Phase = phase;
            Activity = activity;
            CurrentUnits = currentUnits;
            UnitsDelta = unitsDelta;
            AbortReason = abortReason;
        }

        public long Minute { get; }
        public StaminaTransitionKind Kind { get; }
        public StaminaRecoveryPhase Phase { get; }
        public StaminaRecoveryActivity Activity { get; }
        public int CurrentUnits { get; }
        public int UnitsDelta { get; }
        public StaminaRecoveryAbortReason AbortReason { get; }
    }

    public sealed class StaminaAdvanceResult
    {
        internal StaminaAdvanceResult(
            long fromMinute,
            long requestedToMinute,
            long processedToMinute,
            int drainedUnits,
            bool requiresRuntimeDecision,
            IReadOnlyList<StaminaTransition> transitions)
        {
            FromMinute = fromMinute;
            RequestedToMinute = requestedToMinute;
            ProcessedToMinute = processedToMinute;
            DrainedUnits = drainedUnits;
            RequiresRuntimeDecision = requiresRuntimeDecision;
            Transitions = transitions ?? Array.Empty<StaminaTransition>();
        }

        public long FromMinute { get; }
        public long RequestedToMinute { get; }
        public long ProcessedToMinute { get; }
        public int DrainedUnits { get; }
        public int RecoveredUnits => 0;
        public bool RequiresRuntimeDecision { get; }
        public bool ReachedRequestedMinute => ProcessedToMinute == RequestedToMinute;
        public IReadOnlyList<StaminaTransition> Transitions { get; }
    }

    [Serializable]
    public sealed class CharacterStaminaSnapshotDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int worldSeed;
        public string characterId = string.Empty;
        public string profileFingerprint = string.Empty;
        public int profileMaxUnits;
        public int currentUnits;
        public int currentActivity;
        public long lastProcessedMinute;
        public int recoveryPhase;
        public int recoveryActivity;
        public long phaseStartedMinute;
        public long performingStartedMinute = -1;
        public int recoveryMinutesApplied;
        public int requestSequence;
        public int selectionAttempt;
        public int completedRecoveries;
        public int returnAttempt;
        public long recoveryRetryMinute = -1;
    }

    /// <summary>
    /// Saveable semantic state only. Runtime claims, concrete offer/facility IDs, paths, scene
    /// transforms, and presentation references intentionally never enter this object.
    /// </summary>
    public sealed class CharacterStaminaState
    {
        internal CharacterStaminaState(
            int currentUnits,
            StaminaActivityKind currentActivity,
            long lastProcessedMinute,
            StaminaRecoveryPhase recoveryPhase = StaminaRecoveryPhase.Working,
            StaminaRecoveryActivity recoveryActivity = StaminaRecoveryActivity.None,
            long phaseStartedMinute = 0,
            long performingStartedMinute = -1,
            int recoveryMinutesApplied = 0,
            int requestSequence = 0,
            int selectionAttempt = 0,
            int completedRecoveries = 0,
            int returnAttempt = 0,
            long recoveryRetryMinute = -1)
        {
            CurrentUnits = currentUnits;
            CurrentActivity = currentActivity;
            LastProcessedMinute = lastProcessedMinute;
            RecoveryPhase = recoveryPhase;
            RecoveryActivity = recoveryActivity;
            PhaseStartedMinute = phaseStartedMinute;
            PerformingStartedMinute = performingStartedMinute;
            RecoveryMinutesApplied = recoveryMinutesApplied;
            RequestSequence = requestSequence;
            SelectionAttempt = selectionAttempt;
            CompletedRecoveries = completedRecoveries;
            ReturnAttempt = returnAttempt;
            RecoveryRetryMinute = recoveryRetryMinute;
        }

        public int CurrentUnits { get; internal set; }
        public StaminaActivityKind CurrentActivity { get; internal set; }
        public long LastProcessedMinute { get; internal set; }
        public StaminaRecoveryPhase RecoveryPhase { get; internal set; }
        public StaminaRecoveryActivity RecoveryActivity { get; internal set; }
        public long PhaseStartedMinute { get; internal set; }
        public long PerformingStartedMinute { get; internal set; }
        public int RecoveryMinutesApplied { get; internal set; }
        public int RequestSequence { get; internal set; }
        public int SelectionAttempt { get; internal set; }
        public int CompletedRecoveries { get; internal set; }
        public int ReturnAttempt { get; internal set; }
        public long RecoveryRetryMinute { get; internal set; }
        public bool HasRecoveryLifecycle => RecoveryPhase != StaminaRecoveryPhase.Working;
        public bool CanCreateDepartureIntent =>
            RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested &&
            RecoveryRetryMinute >= 0 &&
            LastProcessedMinute >= RecoveryRetryMinute;
    }

    public readonly struct CharacterStaminaReadSnapshot
    {
        public CharacterStaminaReadSnapshot(
            string characterId,
            int currentUnits,
            int maxUnits,
            int ratioBasisPoints,
            int recoveryThresholdBasisPoints,
            int cautionThresholdBasisPoints,
            long lastProcessedMinute,
            StaminaRecoveryPhase recoveryPhase,
            StaminaRecoveryActivity recoveryActivity)
        {
            CharacterId = characterId ?? string.Empty;
            CurrentUnits = currentUnits;
            MaxUnits = maxUnits;
            RatioBasisPoints = ratioBasisPoints;
            RecoveryThresholdBasisPoints = recoveryThresholdBasisPoints;
            CautionThresholdBasisPoints = cautionThresholdBasisPoints;
            LastProcessedMinute = lastProcessedMinute;
            RecoveryPhase = recoveryPhase;
            RecoveryActivity = recoveryActivity;
        }

        public string CharacterId { get; }
        public int CurrentUnits { get; }
        public int MaxUnits { get; }
        public int RatioBasisPoints { get; }
        public int RecoveryThresholdBasisPoints { get; }
        public int CautionThresholdBasisPoints { get; }
        public long LastProcessedMinute { get; }
        public StaminaRecoveryPhase RecoveryPhase { get; }
        public StaminaRecoveryActivity RecoveryActivity { get; }
    }

    public interface ICharacterStaminaReadModel
    {
        IReadOnlyList<string> CharacterIds { get; }
        bool TryRead(string characterId, out CharacterStaminaReadSnapshot snapshot);
    }

    public sealed class CharacterStaminaSimulation
    {
        private readonly int _worldSeed;
        private readonly string _characterId;
        private readonly CharacterStaminaProfile _profile;

        private CharacterStaminaSimulation(
            int worldSeed,
            string characterId,
            CharacterStaminaProfile profile,
            CharacterStaminaState state)
        {
            _worldSeed = worldSeed;
            _characterId = NormalizeId(characterId);
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            State = state ?? throw new ArgumentNullException(nameof(state));
            ValidateState(_profile, State);
        }

        public int WorldSeed => _worldSeed;
        public string CharacterId => _characterId;
        public CharacterStaminaProfile Profile => _profile;
        public CharacterStaminaState State { get; }
        public bool IsAtOrBelowRecoveryThreshold =>
            _profile.IsAtOrBelowRecoveryThreshold(State.CurrentUnits);
        public bool IsRecoveryReadyToComplete =>
            State.RecoveryPhase == StaminaRecoveryPhase.Performing &&
            State.RecoveryActivity != StaminaRecoveryActivity.None &&
            State.RecoveryMinutesApplied >=
            _profile.Recovery(State.RecoveryActivity).DurationGameMinutes;
        public string RecoveryRequestKey => State.RequestSequence <= 0 ||
                                            State.RecoveryPhase < StaminaRecoveryPhase.RecoveryRequested ||
                                            State.RecoveryPhase > StaminaRecoveryPhase.Performing
            ? string.Empty
            : $"stamina-recovery-v1:{_worldSeed}:{_characterId}:" +
              $"{State.RequestSequence}:{State.SelectionAttempt}";
        public string AssignedSeatReturnRequestKey =>
            State.RecoveryPhase != StaminaRecoveryPhase.ReturningToAssignedSeat
                ? string.Empty
                : $"stamina-return-v1:{_worldSeed}:{_characterId}:" +
                  $"{State.CompletedRecoveries}:{State.ReturnAttempt}";
        public bool HasPendingRuntimeDecision =>
            (State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested &&
             State.RecoveryRetryMinute >= 0 &&
             State.LastProcessedMinute >= State.RecoveryRetryMinute) ||
            IsRecoveryReadyToComplete;

        public static CharacterStaminaSimulation CreateDefault(
            int worldSeed,
            string characterId,
            CharacterStaminaCatalog catalog,
            long startMinute = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            CharacterStaminaProfile profile = catalog.Resolve(characterId);
            return new CharacterStaminaSimulation(
                worldSeed,
                characterId,
                profile,
                new CharacterStaminaState(
                    profile.InitialUnits,
                    StaminaActivityKind.Idle,
                    startMinute,
                    phaseStartedMinute: startMinute));
        }

        public static CharacterStaminaSimulation CreateAt(
            int worldSeed,
            string characterId,
            CharacterStaminaCatalog catalog,
            long startMinute,
            int currentUnits)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
            CharacterStaminaProfile profile = catalog.Resolve(characterId);
            if (currentUnits < 0 || currentUnits > profile.MaxUnits)
                throw new ArgumentOutOfRangeException(nameof(currentUnits));
            return new CharacterStaminaSimulation(
                worldSeed,
                characterId,
                profile,
                new CharacterStaminaState(
                    currentUnits,
                    StaminaActivityKind.Idle,
                    startMinute,
                    phaseStartedMinute: startMinute));
        }

        /// <summary>
        /// Save-v1..v7 migration boundary. Legacy Energy becomes the canonical stamina value at
        /// the save's elapsed minute, so historical game time is never replayed.
        /// </summary>
        public static CharacterStaminaSimulation MigrateLegacyEnergyPercent(
            int worldSeed,
            string characterId,
            CharacterStaminaCatalog catalog,
            int legacyEnergyPercent,
            long elapsedMinute)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            CharacterStaminaProfile profile = catalog.Resolve(characterId);
            return CreateAt(
                worldSeed,
                characterId,
                catalog,
                elapsedMinute,
                profile.UnitsFromLegacyPercent(legacyEnergyPercent));
        }

        public static CharacterStaminaSimulation Restore(
            CharacterStaminaSnapshotDto snapshot,
            CharacterStaminaCatalog catalog)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (snapshot.schemaVersion != CharacterStaminaSnapshotDto.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported character stamina snapshot schema: {snapshot.schemaVersion}");
            string characterId = NormalizeId(snapshot.characterId);
            CharacterStaminaProfile profile = catalog.Resolve(characterId);
            if (!string.Equals(snapshot.profileFingerprint, profile.ProfileFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Stamina profile changed for {characterId}; an explicit data migration is required.");
            if (snapshot.profileMaxUnits != profile.MaxUnits)
                throw new InvalidOperationException(
                    $"Stamina profile max changed for {characterId}: " +
                    $"save={snapshot.profileMaxUnits}, catalog={profile.MaxUnits}.");
            ValidateEnum<StaminaActivityKind>(snapshot.currentActivity, nameof(snapshot.currentActivity));
            ValidateEnum<StaminaRecoveryPhase>(snapshot.recoveryPhase, nameof(snapshot.recoveryPhase));
            ValidateEnum<StaminaRecoveryActivity>(snapshot.recoveryActivity, nameof(snapshot.recoveryActivity));
            var simulation = new CharacterStaminaSimulation(
                snapshot.worldSeed,
                characterId,
                profile,
                new CharacterStaminaState(
                    snapshot.currentUnits,
                    (StaminaActivityKind)snapshot.currentActivity,
                    snapshot.lastProcessedMinute,
                    (StaminaRecoveryPhase)snapshot.recoveryPhase,
                    (StaminaRecoveryActivity)snapshot.recoveryActivity,
                    snapshot.phaseStartedMinute,
                    snapshot.performingStartedMinute,
                    snapshot.recoveryMinutesApplied,
                    snapshot.requestSequence,
                    snapshot.selectionAttempt,
                    snapshot.completedRecoveries,
                    snapshot.returnAttempt,
                    snapshot.recoveryRetryMinute));
            simulation.NormalizeAfterTransientRuntimeLoss();
            ValidateState(profile, simulation.State);
            return simulation;
        }

        public CharacterStaminaSnapshotDto ExportSnapshot()
        {
            return new CharacterStaminaSnapshotDto
            {
                schemaVersion = CharacterStaminaSnapshotDto.CurrentSchemaVersion,
                worldSeed = _worldSeed,
                characterId = _characterId,
                profileFingerprint = _profile.ProfileFingerprint,
                profileMaxUnits = _profile.MaxUnits,
                currentUnits = State.CurrentUnits,
                currentActivity = (int)State.CurrentActivity,
                lastProcessedMinute = State.LastProcessedMinute,
                recoveryPhase = (int)State.RecoveryPhase,
                recoveryActivity = (int)State.RecoveryActivity,
                phaseStartedMinute = State.PhaseStartedMinute,
                performingStartedMinute = State.PerformingStartedMinute,
                recoveryMinutesApplied = State.RecoveryMinutesApplied,
                requestSequence = State.RequestSequence,
                selectionAttempt = State.SelectionAttempt,
                completedRecoveries = State.CompletedRecoveries,
                returnAttempt = State.ReturnAttempt,
                recoveryRetryMinute = State.RecoveryRetryMinute
            };
        }

        public CharacterStaminaReadSnapshot Read()
        {
            return new CharacterStaminaReadSnapshot(
                _characterId,
                State.CurrentUnits,
                _profile.MaxUnits,
                _profile.RatioBasisPoints(State.CurrentUnits),
                _profile.RecoveryThresholdBasisPoints,
                _profile.CautionThresholdBasisPoints,
                State.LastProcessedMinute,
                State.RecoveryPhase,
                State.RecoveryActivity);
        }

        /// <summary>
        /// Changes the authoritative activity at an exact GameTime boundary. The integration owner
        /// must AdvanceTo that minute first; no frame clock is accepted here.
        /// </summary>
        public StaminaTransition SetActivity(StaminaActivityKind activity, long minute)
        {
            RequireSignalMinute(minute);
            ValidateActivity(activity);
            State.CurrentActivity = activity;
            return Transition(minute, StaminaTransitionKind.ActivityChanged);
        }

        /// <summary>
        /// Advances over the stored activity segment. It yields at the first threshold or recovery
        /// duration boundary, giving the runtime coordinator a deterministic chance to react before
        /// the remaining GameTime is processed.
        /// </summary>
        public StaminaAdvanceResult AdvanceTo(
            long targetMinute,
            bool allowOfficeRecoveryRequest = true)
        {
            if (targetMinute < State.LastProcessedMinute)
                throw new InvalidOperationException("Stamina time cannot move backwards.");
            ValidateActivity(State.CurrentActivity);

            long fromMinute = State.LastProcessedMinute;
            var transitions = new List<StaminaTransition>();
            if (HasPendingRuntimeDecision)
                return AdvanceResult(fromMinute, targetMinute, 0, true, transitions);
            if (TryRequestRecovery(fromMinute, allowOfficeRecoveryRequest, transitions))
                return AdvanceResult(fromMinute, targetMinute, 0, true, transitions);
            if (targetMinute == fromMinute)
                return AdvanceResult(fromMinute, targetMinute, 0, false, transitions);

            long span = targetMinute - fromMinute;
            if (State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested)
            {
                long retryMinute = State.RecoveryRetryMinute;
                long stepTarget = retryMinute > fromMinute && retryMinute < targetMinute
                    ? retryMinute
                    : targetMinute;
                int pendingDrainRate = _profile.DrainUnitsPerGameMinute(State.CurrentActivity);
                int pendingDrained = ApplyDrain(stepTarget - fromMinute, pendingDrainRate);
                State.LastProcessedMinute = stepTarget;
                bool retryReady = State.LastProcessedMinute >= retryMinute;
                if (retryReady)
                    transitions.Add(Transition(
                        State.LastProcessedMinute,
                        StaminaTransitionKind.RecoveryRetryReady));
                return AdvanceResult(
                    fromMinute,
                    targetMinute,
                    pendingDrained,
                    retryReady,
                    transitions);
            }

            if (State.RecoveryPhase == StaminaRecoveryPhase.Performing)
            {
                StaminaRecoveryDefinition recovery = _profile.Recovery(State.RecoveryActivity);
                int remainingMinutes = recovery.DurationGameMinutes - State.RecoveryMinutesApplied;
                if (remainingMinutes <= 0)
                    return AdvanceResult(fromMinute, targetMinute, 0, true, transitions);

                long appliedMinutes = Math.Min(span, remainingMinutes);
                State.RecoveryMinutesApplied += (int)appliedMinutes;
                State.LastProcessedMinute = checked(fromMinute + appliedMinutes);
                transitions.Add(Transition(
                    State.LastProcessedMinute,
                    StaminaTransitionKind.RecoveryProgressed));
                bool ready = State.RecoveryMinutesApplied == recovery.DurationGameMinutes;
                if (ready)
                    transitions.Add(Transition(
                        State.LastProcessedMinute,
                        StaminaTransitionKind.RecoveryReadyToComplete));
                return AdvanceResult(fromMinute, targetMinute, 0, ready, transitions);
            }

            int drainRate = _profile.DrainUnitsPerGameMinute(State.CurrentActivity);
            if (State.RecoveryPhase == StaminaRecoveryPhase.Working &&
                allowOfficeRecoveryRequest && drainRate > 0 &&
                !IsAtOrBelowRecoveryThreshold)
            {
                int unitsAboveThreshold = State.CurrentUnits - _profile.RecoveryThresholdUnits;
                long minutesToThreshold = CeilingDivide(unitsAboveThreshold, drainRate);
                if (minutesToThreshold <= span)
                {
                    EnsureCanIncrement(State.RequestSequence, nameof(State.RequestSequence));
                    int drainedToThreshold = ApplyDrain(minutesToThreshold, drainRate);
                    State.LastProcessedMinute = checked(fromMinute + minutesToThreshold);
                    bool requested = TryRequestRecovery(
                        State.LastProcessedMinute,
                        true,
                        transitions);
                    return AdvanceResult(
                        fromMinute,
                        targetMinute,
                        drainedToThreshold,
                        requested,
                        transitions);
                }
            }

            int drained = ApplyDrain(span, drainRate);
            State.LastProcessedMinute = targetMinute;
            bool requiresDecision = TryRequestRecovery(
                targetMinute,
                allowOfficeRecoveryRequest,
                transitions);
            return AdvanceResult(
                fromMinute,
                targetMinute,
                drained,
                requiresDecision,
                transitions);
        }

        /// <summary>
        /// Call only after the existing interaction lifecycle atomically reserved a live offer.
        /// The plan chooses an interaction kind; the lifecycle remains the sole facility owner.
        /// </summary>
        public StaminaTransition AcceptRecoveryPlan(StaminaRecoveryPlan plan, long minute)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.RecoveryRequested);
            RequireRecoveryRequestKey(plan.RequestKey);
            if (!State.CanCreateDepartureIntent)
                throw new InvalidOperationException(
                    "Recovery plan cannot be accepted before its deterministic retry minute.");
            StaminaRecoveryDefinition definition = _profile.Recovery(plan.Activity);
            if (!string.Equals(definition.InteractionId, plan.InteractionId, StringComparison.Ordinal))
                throw new InvalidOperationException("Recovery plan interaction does not match its profile.");
            State.RecoveryActivity = plan.Activity;
            State.RecoveryMinutesApplied = 0;
            State.PerformingStartedMinute = -1;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = -1;
            SetPhase(StaminaRecoveryPhase.SafeStopping, minute);
            return Transition(minute, StaminaTransitionKind.RecoveryPlanAccepted);
        }

        public StaminaTransition RecordRecoverySelectionFailure(
            string requestKey,
            StaminaRecoveryAbortReason reason,
            long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.RecoveryRequested);
            RequireRecoveryRequestKey(requestKey);
            ValidateAbortReason(reason);
            int nextAttempt = Incremented(State.SelectionAttempt, nameof(State.SelectionAttempt));
            long retryMinute = minute == long.MaxValue ? long.MaxValue : minute + 1;
            State.SelectionAttempt = nextAttempt;
            State.RecoveryRetryMinute = retryMinute;
            State.PhaseStartedMinute = minute;
            return Transition(
                minute,
                StaminaTransitionKind.RecoverySelectionFailed,
                abortReason: reason);
        }

        public StaminaTransition ConfirmSafeStopCompleted(string requestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.SafeStopping);
            RequireRecoveryRequestKey(requestKey);
            SetPhase(StaminaRecoveryPhase.StandingUp, minute);
            return Transition(minute, StaminaTransitionKind.SafeStopCompleted);
        }

        public StaminaTransition ConfirmStandUpCompleted(string requestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.StandingUp);
            RequireRecoveryRequestKey(requestKey);
            SetPhase(StaminaRecoveryPhase.Traveling, minute);
            return Transition(minute, StaminaTransitionKind.StandUpCompleted);
        }

        public StaminaTransition ConfirmFacilityArrived(string requestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.Traveling);
            RequireRecoveryRequestKey(requestKey);
            SetPhase(StaminaRecoveryPhase.Aligning, minute);
            return Transition(minute, StaminaTransitionKind.FacilityArrived);
        }

        public StaminaTransition ConfirmFacingAlignedAndPerforming(
            string requestKey,
            long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.Aligning);
            RequireRecoveryRequestKey(requestKey);
            State.PerformingStartedMinute = minute;
            SetPhase(StaminaRecoveryPhase.Performing, minute);
            return Transition(minute, StaminaTransitionKind.PerformingStarted);
        }

        /// <summary>
        /// Preflight this before asking the runtime lifecycle to Complete/release its claim. A true
        /// result makes the subsequent same-minute ConfirmInteractionCompleted mutation non-throwing
        /// unless a stale runtime event changes the correlated state in between.
        /// </summary>
        public bool CanCompleteRuntimeInteraction(string requestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.Performing);
            RequireRecoveryRequestKey(requestKey);
            return IsRecoveryReadyToComplete && State.CompletedRecoveries < int.MaxValue;
        }

        /// <summary>
        /// Call only after the runtime lifecycle successfully completed and released the claimed
        /// interaction. Recovery is committed here, never on travel, arrival, or partial performing.
        /// </summary>
        public StaminaTransition ConfirmInteractionCompleted(string requestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.Performing);
            RequireRecoveryRequestKey(requestKey);
            if (!IsRecoveryReadyToComplete)
                throw new InvalidOperationException(
                    "Recovery interaction cannot complete before its GameTime duration.");
            int nextCompleted = Incremented(
                State.CompletedRecoveries,
                nameof(State.CompletedRecoveries));
            int before = State.CurrentUnits;
            long candidate = (long)before +
                             _profile.Recovery(State.RecoveryActivity).MaximumRecoveryUnits;
            int after = (int)Math.Min(_profile.MaxUnits, candidate);
            State.CurrentUnits = after;
            State.CompletedRecoveries = nextCompleted;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = -1;
            SetPhase(StaminaRecoveryPhase.ReturningToAssignedSeat, minute);
            return Transition(
                minute,
                StaminaTransitionKind.InteractionCompleted,
                State.CurrentUnits - before);
        }

        public StaminaTransition ConfirmAssignedSeatReturned(string returnRequestKey, long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.ReturningToAssignedSeat);
            RequireAssignedSeatReturnRequestKey(returnRequestKey);
            State.RecoveryActivity = StaminaRecoveryActivity.None;
            State.RecoveryMinutesApplied = 0;
            State.PerformingStartedMinute = -1;
            State.SelectionAttempt = 0;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = -1;
            SetPhase(StaminaRecoveryPhase.Working, minute);
            return Transition(minute, StaminaTransitionKind.AssignedSeatReturned);
        }

        /// <summary>
        /// Records a released/failed assigned-seat attempt and produces a new correlation key.
        /// The coordinator must resolve the current assigned seat and retry; stamina cannot silently
        /// leave the return lifecycle after a layout rebuild.
        /// </summary>
        public StaminaTransition RequestAssignedSeatReturnRetry(
            string returnRequestKey,
            StaminaRecoveryAbortReason reason,
            long minute)
        {
            RequireSignalMinute(minute);
            RequirePhase(StaminaRecoveryPhase.ReturningToAssignedSeat);
            RequireAssignedSeatReturnRequestKey(returnRequestKey);
            ValidateAbortReason(reason);
            int nextAttempt = Incremented(State.ReturnAttempt, nameof(State.ReturnAttempt));
            State.ReturnAttempt = nextAttempt;
            State.PhaseStartedMinute = minute;
            return Transition(
                minute,
                StaminaTransitionKind.AssignedSeatReturnRetryRequested,
                abortReason: reason);
        }

        /// <summary>
        /// Call only after runtime abort has reached a terminal state and released every claim.
        /// Pending performance grants are discarded because completion never succeeded.
        /// </summary>
        public StaminaTransition AbortRecoveryPlan(
            string requestKey,
            StaminaRecoveryAbortReason reason,
            long minute)
        {
            RequireSignalMinute(minute);
            RequireRecoveryRequestKey(requestKey);
            ValidateAbortReason(reason);
            if (State.RecoveryPhase == StaminaRecoveryPhase.Working ||
                State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested ||
                State.RecoveryPhase == StaminaRecoveryPhase.ReturningToAssignedSeat)
                throw new InvalidOperationException(
                    $"Recovery plan cannot abort from {State.RecoveryPhase}.");

            int nextAttempt = Incremented(State.SelectionAttempt, nameof(State.SelectionAttempt));
            State.RecoveryActivity = StaminaRecoveryActivity.None;
            State.RecoveryMinutesApplied = 0;
            State.PerformingStartedMinute = -1;
            State.SelectionAttempt = nextAttempt;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = minute;
            SetPhase(StaminaRecoveryPhase.RecoveryRequested, minute);
            return Transition(
                minute,
                StaminaTransitionKind.RecoveryPlanAborted,
                abortReason: reason);
        }

        internal long PreviewNextDecisionMinute(
            long targetMinute,
            bool allowOfficeRecoveryRequest)
        {
            if (targetMinute < State.LastProcessedMinute)
                throw new InvalidOperationException("Stamina time cannot move backwards.");
            if (HasPendingRuntimeDecision) return State.LastProcessedMinute;
            if (State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested)
                return Math.Min(targetMinute, State.RecoveryRetryMinute);
            if (targetMinute == State.LastProcessedMinute) return targetMinute;
            if (State.RecoveryPhase == StaminaRecoveryPhase.Working &&
                allowOfficeRecoveryRequest)
            {
                if (IsAtOrBelowRecoveryThreshold)
                {
                    EnsureCanIncrement(State.RequestSequence, nameof(State.RequestSequence));
                    return State.LastProcessedMinute;
                }
                int rate = _profile.DrainUnitsPerGameMinute(State.CurrentActivity);
                if (rate > 0)
                {
                    int unitsAboveThreshold = State.CurrentUnits - _profile.RecoveryThresholdUnits;
                    long crossing = CeilingDivide(unitsAboveThreshold, rate);
                    long span = targetMinute - State.LastProcessedMinute;
                    if (crossing <= span)
                    {
                        EnsureCanIncrement(State.RequestSequence, nameof(State.RequestSequence));
                        return checked(State.LastProcessedMinute + crossing);
                    }
                }
            }
            if (State.RecoveryPhase == StaminaRecoveryPhase.Performing &&
                !IsRecoveryReadyToComplete)
            {
                int remaining = _profile.Recovery(State.RecoveryActivity).DurationGameMinutes -
                                State.RecoveryMinutesApplied;
                long span = targetMinute - State.LastProcessedMinute;
                if (remaining <= span)
                    return checked(State.LastProcessedMinute + remaining);
            }
            return targetMinute;
        }

        private void NormalizeAfterTransientRuntimeLoss()
        {
            if (State.RecoveryPhase < StaminaRecoveryPhase.SafeStopping ||
                State.RecoveryPhase > StaminaRecoveryPhase.Performing) return;
            int nextAttempt = Incremented(State.SelectionAttempt, nameof(State.SelectionAttempt));
            State.RecoveryActivity = StaminaRecoveryActivity.None;
            State.RecoveryMinutesApplied = 0;
            State.PerformingStartedMinute = -1;
            State.SelectionAttempt = nextAttempt;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = State.LastProcessedMinute;
            SetPhase(StaminaRecoveryPhase.RecoveryRequested, State.LastProcessedMinute);
        }

        private bool TryRequestRecovery(
            long minute,
            bool allowed,
            ICollection<StaminaTransition> transitions)
        {
            if (!allowed || State.RecoveryPhase != StaminaRecoveryPhase.Working ||
                !_profile.IsAtOrBelowRecoveryThreshold(State.CurrentUnits)) return false;
            int nextSequence = Incremented(State.RequestSequence, nameof(State.RequestSequence));
            State.RequestSequence = nextSequence;
            State.SelectionAttempt = 0;
            State.RecoveryActivity = StaminaRecoveryActivity.None;
            State.RecoveryMinutesApplied = 0;
            State.PerformingStartedMinute = -1;
            State.ReturnAttempt = 0;
            State.RecoveryRetryMinute = minute;
            SetPhase(StaminaRecoveryPhase.RecoveryRequested, minute);
            transitions.Add(Transition(minute, StaminaTransitionKind.RecoveryRequested));
            return true;
        }

        private int ApplyDrain(long minutes, int unitsPerMinute)
        {
            if (minutes <= 0 || unitsPerMinute <= 0 || State.CurrentUnits <= 0) return 0;
            long minutesToEmpty = CeilingDivide(State.CurrentUnits, unitsPerMinute);
            int applied = minutes >= minutesToEmpty
                ? State.CurrentUnits
                : checked((int)(minutes * unitsPerMinute));
            State.CurrentUnits -= applied;
            return applied;
        }

        private StaminaAdvanceResult AdvanceResult(
            long fromMinute,
            long requestedToMinute,
            int drained,
            bool requiresDecision,
            IReadOnlyList<StaminaTransition> transitions)
        {
            return new StaminaAdvanceResult(
                fromMinute,
                requestedToMinute,
                State.LastProcessedMinute,
                drained,
                requiresDecision,
                transitions);
        }

        private void SetPhase(StaminaRecoveryPhase phase, long minute)
        {
            State.RecoveryPhase = phase;
            State.PhaseStartedMinute = minute;
        }

        private StaminaTransition Transition(
            long minute,
            StaminaTransitionKind kind,
            int unitsDelta = 0,
            StaminaRecoveryAbortReason abortReason = StaminaRecoveryAbortReason.None)
        {
            return new StaminaTransition(
                minute,
                kind,
                State.RecoveryPhase,
                State.RecoveryActivity,
                State.CurrentUnits,
                unitsDelta,
                abortReason);
        }

        private void RequireSignalMinute(long minute)
        {
            if (minute != State.LastProcessedMinute)
                throw new InvalidOperationException(
                    $"Runtime stamina signal must use the current authoritative GameTime minute " +
                    $"({State.LastProcessedMinute}), not {minute}.");
        }

        private void RequirePhase(StaminaRecoveryPhase phase)
        {
            if (State.RecoveryPhase != phase)
                throw new InvalidOperationException(
                    $"Expected stamina phase {phase}, actual {State.RecoveryPhase}.");
        }

        private void RequireRecoveryRequestKey(string requestKey)
        {
            if (string.IsNullOrEmpty(RecoveryRequestKey) ||
                !string.Equals(requestKey, RecoveryRequestKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Stale or mismatched stamina recovery request key.");
        }

        private void RequireAssignedSeatReturnRequestKey(string requestKey)
        {
            if (string.IsNullOrEmpty(AssignedSeatReturnRequestKey) ||
                !string.Equals(requestKey, AssignedSeatReturnRequestKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Stale or mismatched assigned-seat return request key.");
        }

        private static void ValidateState(
            CharacterStaminaProfile profile,
            CharacterStaminaState state)
        {
            if (state.CurrentUnits < 0 || state.CurrentUnits > profile.MaxUnits)
                throw new InvalidOperationException("Stamina current units are outside the profile range.");
            ValidateActivity(state.CurrentActivity);
            if (state.LastProcessedMinute < 0)
                throw new InvalidOperationException("Stamina time is invalid.");
            if (state.PhaseStartedMinute < 0 || state.PhaseStartedMinute > state.LastProcessedMinute)
                throw new InvalidOperationException("Stamina phase time is invalid.");
            if (state.PerformingStartedMinute < -1 ||
                state.PerformingStartedMinute > state.LastProcessedMinute)
                throw new InvalidOperationException("Stamina performing time is invalid.");
            if (state.RecoveryMinutesApplied < 0 || state.RequestSequence < 0 ||
                state.SelectionAttempt < 0 || state.CompletedRecoveries < 0 ||
                state.ReturnAttempt < 0)
                throw new InvalidOperationException("Stamina counters cannot be negative.");
            bool waitingForSelection =
                state.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested;
            if (waitingForSelection != (state.RecoveryRetryMinute >= 0))
                throw new InvalidOperationException(
                    "Recovery retry time must exist only while selection is requested.");
            if (waitingForSelection && state.RecoveryRetryMinute < state.PhaseStartedMinute)
                throw new InvalidOperationException("Recovery retry time precedes its phase.");

            bool selected = state.RecoveryPhase != StaminaRecoveryPhase.Working &&
                            state.RecoveryPhase != StaminaRecoveryPhase.RecoveryRequested;
            if (selected != (state.RecoveryActivity != StaminaRecoveryActivity.None))
                throw new InvalidOperationException("Stamina recovery activity does not match its phase.");
            if (state.RecoveryPhase != StaminaRecoveryPhase.ReturningToAssignedSeat &&
                state.ReturnAttempt != 0)
                throw new InvalidOperationException("Return attempts require the assigned-seat return phase.");

            if (!selected)
            {
                if (state.RecoveryMinutesApplied != 0 || state.PerformingStartedMinute != -1)
                    throw new InvalidOperationException("Unselected recovery cannot contain performance progress.");
            }
            else
            {
                StaminaRecoveryDefinition recovery = profile.Recovery(state.RecoveryActivity);
                if (state.RecoveryMinutesApplied > recovery.DurationGameMinutes)
                    throw new InvalidOperationException("Stamina recovery minutes exceed the activity duration.");
                bool beforePerforming = state.RecoveryPhase >= StaminaRecoveryPhase.SafeStopping &&
                                        state.RecoveryPhase <= StaminaRecoveryPhase.Aligning;
                if (beforePerforming &&
                    (state.RecoveryMinutesApplied != 0 || state.PerformingStartedMinute != -1))
                    throw new InvalidOperationException(
                        "Pre-performing recovery cannot contain performance progress.");
                if (state.RecoveryPhase == StaminaRecoveryPhase.Performing)
                {
                    if (state.PerformingStartedMinute < 0 ||
                        state.PerformingStartedMinute != state.PhaseStartedMinute)
                        throw new InvalidOperationException("Performing timestamps are inconsistent.");
                    long elapsed = state.LastProcessedMinute - state.PerformingStartedMinute;
                    int expected = (int)Math.Min(recovery.DurationGameMinutes, elapsed);
                    if (state.RecoveryMinutesApplied != expected)
                        throw new InvalidOperationException("Performing progress does not match GameTime.");
                }
                if (state.RecoveryPhase == StaminaRecoveryPhase.ReturningToAssignedSeat &&
                    (state.PerformingStartedMinute < 0 ||
                     state.PerformingStartedMinute > state.PhaseStartedMinute ||
                     state.RecoveryMinutesApplied != recovery.DurationGameMinutes))
                    throw new InvalidOperationException(
                        "Assigned-seat return requires a completed recovery interaction.");
            }

            if (state.RecoveryPhase != StaminaRecoveryPhase.Working && state.RequestSequence <= 0)
                throw new InvalidOperationException("Active stamina recovery requires a request sequence.");
            if (state.RecoveryPhase == StaminaRecoveryPhase.ReturningToAssignedSeat &&
                state.CompletedRecoveries <= 0)
                throw new InvalidOperationException("Assigned-seat return requires a completed recovery count.");
        }

        private static void ValidateActivity(StaminaActivityKind activity)
        {
            if (!Enum.IsDefined(typeof(StaminaActivityKind), activity) ||
                activity == StaminaActivityKind.None)
                throw new ArgumentOutOfRangeException(nameof(activity));
        }

        private static void ValidateAbortReason(StaminaRecoveryAbortReason reason)
        {
            if (!Enum.IsDefined(typeof(StaminaRecoveryAbortReason), reason) ||
                reason == StaminaRecoveryAbortReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));
        }

        private static long CeilingDivide(int numerator, int denominator)
        {
            if (numerator <= 0) return 0;
            return ((long)numerator + denominator - 1L) / denominator;
        }

        private static void EnsureCanIncrement(int value, string counterName)
        {
            if (value == int.MaxValue)
                throw new InvalidOperationException(
                    "Stamina counter cannot advance beyond Int32: " + counterName + ".");
        }

        private static int Incremented(int value, string counterName)
        {
            EnsureCanIncrement(value, counterName);
            return value + 1;
        }

        private static string NormalizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Character ID is required.", nameof(value));
            return value.Trim();
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

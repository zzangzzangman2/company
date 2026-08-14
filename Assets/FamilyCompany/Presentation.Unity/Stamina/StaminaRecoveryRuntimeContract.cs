using System;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    public enum StaminaRuntimeTransitionKind
    {
        RecoveryReservationAccepted = 0,
        RecoveryReservationRejected = 1,
        SafeStopCompleted = 2,
        StandUpCompleted = 3,
        TravelStarted = 4,
        FacilityArrived = 5,
        PerformingStarted = 6,
        RecoveryCompletedAndReleased = 7,
        RecoveryAbortedAndReleased = 8,
        AssignedSeatReturnStarted = 9,
        AssignedSeatWorking = 10,
        AssignedSeatReturnFailedAndReleased = 11,
        RuntimeResetAndReleased = 12,
        AssignedWorkResumed = 13
    }

    public enum StaminaRuntimeFailureReason
    {
        None = 0,
        AgentUnavailable = 1,
        AssignedWorkPauseRejected = 2,
        NoReachableOffer = 3,
        ReservationRejected = 4,
        PathUnavailable = 5,
        ArrivalInvalid = 6,
        LayoutChanged = 7,
        CompletionRejected = 8,
        AssignedSeatUnavailable = 9,
        StaleCorrelationKey = 10,
        RuntimeReset = 11
    }

    /// <summary>
    /// Correlated runtime fact. The emitting owner stamps the exact authoritative GameTime minute;
    /// frame time and polling timestamps are never accepted as substitutes.
    /// </summary>
    public readonly struct StaminaRuntimeTransition
    {
        public StaminaRuntimeTransition(
            string characterId,
            string correlationKey,
            long gameTimeMinute,
            StaminaRuntimeTransitionKind kind,
            StaminaRuntimeFailureReason failureReason = StaminaRuntimeFailureReason.None,
            string interactionId = "",
            string runtimeOfferId = "",
            string assignedSeatId = "")
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Character ID is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(correlationKey))
                throw new ArgumentException("Correlation key is required.", nameof(correlationKey));
            if (gameTimeMinute < 0) throw new ArgumentOutOfRangeException(nameof(gameTimeMinute));
            if (!Enum.IsDefined(typeof(StaminaRuntimeTransitionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(StaminaRuntimeFailureReason), failureReason))
                throw new ArgumentOutOfRangeException(nameof(failureReason));
            CharacterId = characterId.Trim();
            CorrelationKey = correlationKey.Trim();
            GameTimeMinute = gameTimeMinute;
            Kind = kind;
            FailureReason = failureReason;
            InteractionId = interactionId ?? string.Empty;
            RuntimeOfferId = runtimeOfferId ?? string.Empty;
            AssignedSeatId = assignedSeatId ?? string.Empty;
        }

        public string CharacterId { get; }
        public string CorrelationKey { get; }
        public long GameTimeMinute { get; }
        public StaminaRuntimeTransitionKind Kind { get; }
        public StaminaRuntimeFailureReason FailureReason { get; }
        public string InteractionId { get; }
        public string RuntimeOfferId { get; }
        public string AssignedSeatId { get; }
        public bool IsFailure => FailureReason != StaminaRuntimeFailureReason.None;
    }

    public readonly struct StaminaRuntimeCommandResult
    {
        public StaminaRuntimeCommandResult(
            bool accepted,
            StaminaRuntimeTransition transition)
        {
            Accepted = accepted;
            Transition = transition;
        }

        public bool Accepted { get; }
        public StaminaRuntimeTransition Transition { get; }
    }

    /// <summary>
    /// Post-seating integration seam. The implementing Agent/adapter remains the sole owner of
    /// interaction handles and claims. Every terminal result guarantees release before returning.
    /// Begin re-queries the building/furniture owner's finalized capability API and atomically selects
    /// and claims a currently valid instance; an advisory stamina candidate or instance ID is never
    /// accepted as authority. Begin is all-or-nothing: Accepted means both assigned work is paused and
    /// the live interaction claim is owned; Rejected means claim count is zero and the pause was rolled
    /// back with work active. A command's synchronous Transition is not emitted again through Transitioned.
    /// Asynchronous phase events may begin only after the command has returned, preventing reentrant
    /// safe-stop/stand callbacks before the caller has accepted the pure plan.
    /// </summary>
    public interface IStaminaRecoveryRuntimePort
    {
        event Action<StaminaRuntimeTransition> Transitioned;

        StaminaRuntimeCommandResult TryPauseWorkReserveAndBeginRecovery(
            string characterId,
            string recoveryRequestKey,
            string interactionId,
            long expectedGameTimeMinute);

        StaminaRuntimeCommandResult TryCompleteRecoveryAndRelease(
            string characterId,
            string recoveryRequestKey,
            long expectedGameTimeMinute);

        StaminaRuntimeCommandResult TryAbortRecoveryAndRelease(
            string characterId,
            string recoveryRequestKey,
            StaminaRuntimeFailureReason reason,
            long expectedGameTimeMinute);

        StaminaRuntimeCommandResult TryBeginAssignedSeatReturn(
            string characterId,
            string returnRequestKey,
            long expectedGameTimeMinute);

        /// <summary>
        /// AssignedSeatWorking proves the exact assigned seat but leaves preserved work paused. The
        /// coordinator first confirms the pure seat return, then calls this method in the same
        /// correlated GameTime transaction to resume the saved task and remaining work.
        /// </summary>
        StaminaRuntimeCommandResult TryResumePausedWorkAtAssignedSeat(
            string characterId,
            string returnRequestKey,
            long expectedGameTimeMinute);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.Stamina;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    /// <summary>
    /// Correlates the pure stamina lifecycle with the existing actor/interaction owners. It never
    /// stores a furniture ID, path, claim, or transform in GameState or save data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaminaRecoveryRuntimeCoordinator : MonoBehaviour,
        ICharacterStaminaRuntimeBridge,
        IStaminaRecoveryRuntimePort
    {
        private sealed class RuntimeSession
        {
            public string CharacterId;
            public string RecoveryKey;
            public string InteractionId;
            public OfficeRuntimeAgentLayoutSnapshot PausedSnapshot;
            public int BaselineCompleted;
            public int BaselineAborted;
            public bool ReservationAccepted;
            public bool FacilityReleased;
            public string ReturnKey = string.Empty;
        }

        private readonly Dictionary<string, RuntimeSession> _sessions =
            new Dictionary<string, RuntimeSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, StaminaRuntimeTransition> _lastCommandByCharacter =
            new Dictionary<string, StaminaRuntimeTransition>(StringComparer.Ordinal);
        private PrototypeBootstrap _bootstrap;
        private StarterOfficeRuntimeBootstrap _runtime;
        private GameState _state;
        private StaminaRecoveryFurnitureCapabilityAdapter _capabilityAdapter;
        private static StaminaRecoveryRuntimeCoordinator _priorityOwner;

        public event Action<StaminaRuntimeTransition> Transitioned;

        public int ActiveSessionCount => _sessions.Count;
        public StaminaRuntimeTransition LastCommandTransitionForQa { get; private set; }

        public bool TryGetLastCommandTransitionForQa(
            string characterId,
            out StaminaRuntimeTransition transition) =>
            _lastCommandByCharacter.TryGetValue(characterId ?? string.Empty, out transition);

        public static bool BlocksRoutineAutonomy(string characterId) =>
            _priorityOwner != null && !string.IsNullOrWhiteSpace(characterId) &&
            _priorityOwner._sessions.ContainsKey(characterId.Trim());

        private void OnEnable() => _priorityOwner = this;

        public bool TryGetPausedWorkForQa(
            string characterId,
            out string taskId,
            out float remainingGameMinutes)
        {
            if (_sessions.TryGetValue(characterId ?? string.Empty, out RuntimeSession session) &&
                session.PausedSnapshot.HasAssignedTask)
            {
                taskId = session.PausedSnapshot.AssignedTaskId;
                remainingGameMinutes = session.PausedSnapshot.AssignedWorkRemainingMinutes;
                return true;
            }
            taskId = string.Empty;
            remainingGameMinutes = 0f;
            return false;
        }

        private void Update()
        {
            TryBindCurrentSession();
            if (_state == null || _runtime == null || !_runtime.IsReady ||
                _state.Stamina.LastProcessedMinute != _state.Time.ElapsedMinutes) return;

            ProcessPendingDecisions(_state.Stamina, _state.Time.ElapsedMinutes);
            foreach (RuntimeSession session in _sessions.Values.ToArray())
                PollSession(session, _state.Time.ElapsedMinutes);
            _state.RefreshLegacyEnergyProjection();
        }

        public bool IsOfficeRecoveryAllowed(string characterId)
        {
            if (_state == null || _runtime == null || !_runtime.IsReady ||
                string.IsNullOrWhiteSpace(characterId) ||
                !_runtime.World.Registry.TryGet(characterId.Trim(), out OfficeRuntimeAgent actor) ||
                actor == null || actor.IsPlayerControlled || actor.IsPresentationAway) return false;
            FamilyMemberState member = _state.Family.Get(characterId.Trim());
            FamilyScheduleSlot schedule = FamilyScheduleRules.Resolve(member.Role, _state.Time.Now);
            return schedule.CanPerformCompanyWork;
        }

        public StaminaActivityKind ResolveActivity(string characterId)
        {
            if (_runtime == null || !_runtime.IsReady ||
                !_runtime.World.Registry.TryGet(characterId, out OfficeRuntimeAgent actor) ||
                actor == null || actor.IsPresentationAway)
            {
                return _state != null && FamilyScheduleRules.Resolve(
                    _state.Family.Get(characterId).Role, _state.Time.Now).Kind == FamilyScheduleKind.Sleep
                    ? StaminaActivityKind.Sleep : StaminaActivityKind.OffDuty;
            }

            if (actor.Phase == OfficeRuntimeAgentPhase.Navigating ||
                actor.IsEnteringSeat ||
                actor.Phase == OfficeRuntimeAgentPhase.LeavingSeat)
                return StaminaActivityKind.Walking;
            switch (actor.CurrentActivity)
            {
                case OfficeActivity.Work:
                    return actor.CurrentOfficeWorkMicroAction ==
                           FamilyCompany.Simulation.OfficeWorkActions.OfficeWorkMicroAction.Typing
                        ? StaminaActivityKind.Typing
                        : StaminaActivityKind.DeskWork;
                case OfficeActivity.Meeting:
                    return StaminaActivityKind.Meeting;
                case OfficeActivity.Reception:
                    return StaminaActivityKind.Reception;
                case OfficeActivity.Printing:
                    return StaminaActivityKind.Printing;
                case OfficeActivity.Outside:
                    return StaminaActivityKind.OutsideWork;
                default:
                    return StaminaActivityKind.Idle;
            }
        }

        public void ProcessPendingDecisions(CharacterStaminaRoster roster, long gameTimeMinute)
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.StaminaDecision);
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (_state == null || !ReferenceEquals(roster, _state.Stamina))
                throw new InvalidOperationException("Stamina runtime bridge is bound to another roster.");
            if (roster.LastProcessedMinute != gameTimeMinute ||
                _state.Time.ElapsedMinutes != gameTimeMinute)
                throw new InvalidOperationException("Stamina decisions require the authoritative GameTime minute.");

            foreach (string characterId in roster.CharacterIds)
            {
                CharacterStaminaSimulation simulation = roster.GetSimulation(characterId);
                if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested &&
                    simulation.HasPendingRuntimeDecision)
                {
                    bool hasPendingPause = _sessions.TryGetValue(
                        characterId, out RuntimeSession pendingSession) &&
                        !pendingSession.ReservationAccepted;
                    IStaminaRecoveryCapabilityQueryAdapter planningAdapter = hasPendingPause
                        ? ConstrainPendingCapabilityQuery(simulation, pendingSession)
                        : _capabilityAdapter;
                    if (!StaminaRecoveryPlanner.TrySelect(
                            simulation, planningAdapter, out StaminaRecoveryPlan plan))
                    {
                        if (hasPendingPause)
                        {
                            OfficeRuntimeAgent pausedActor = FindActor(characterId);
                            if (pausedActor != null)
                                RestorePausedSnapshot(pausedActor, pendingSession.PausedSnapshot);
                            _sessions.Remove(characterId);
                        }
                        simulation.RecordRecoverySelectionFailure(
                            simulation.RecoveryRequestKey,
                            StaminaRecoveryAbortReason.ReservationUnavailable,
                            gameTimeMinute);
                        continue;
                    }

                    StaminaRuntimeCommandResult begun = TryPauseWorkReserveAndBeginRecovery(
                        characterId,
                        plan.RequestKey,
                        plan.InteractionId,
                        gameTimeMinute);
                    LastCommandTransitionForQa = begun.Transition;
                    _lastCommandByCharacter[characterId] = begun.Transition;
                    Debug.Log("STAMINA_RUNTIME_COMMAND | member=" + characterId +
                              " | kind=" + begun.Transition.Kind +
                              " | failure=" + begun.Transition.FailureReason +
                              " | interaction=" + begun.Transition.InteractionId +
                              " | minute=" + gameTimeMinute);
                    if (begun.Accepted)
                        simulation.AcceptRecoveryPlan(plan, gameTimeMinute);
                    else
                    {
                        simulation.RecordRecoverySelectionFailure(
                            plan.RequestKey,
                            MapAbortReason(begun.Transition.FailureReason),
                            gameTimeMinute);
                        if (_sessions.TryGetValue(characterId, out RuntimeSession waiting) &&
                            !waiting.ReservationAccepted)
                            waiting.RecoveryKey = simulation.RecoveryRequestKey;
                    }
                }

                if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.Performing &&
                    simulation.IsRecoveryReadyToComplete)
                {
                    string key = simulation.RecoveryRequestKey;
                    if (!simulation.CanCompleteRuntimeInteraction(key, gameTimeMinute)) continue;
                    StaminaRuntimeCommandResult completed = TryCompleteRecoveryAndRelease(
                        characterId,
                        key,
                        gameTimeMinute);
                    if (!completed.Accepted)
                    {
                        AbortPureAfterRuntimeFailure(
                            simulation,
                            key,
                            completed.Transition.FailureReason,
                            gameTimeMinute);
                        continue;
                    }

                    simulation.ConfirmInteractionCompleted(key, gameTimeMinute);
                    BeginSeatReturnOrRecordRetry(simulation, gameTimeMinute);
                }
                else if (simulation.State.RecoveryPhase ==
                         StaminaRecoveryPhase.ReturningToAssignedSeat &&
                         (!_sessions.TryGetValue(characterId, out RuntimeSession session) ||
                          session.ReturnKey.Length == 0))
                {
                    BeginSeatReturnOrRecordRetry(simulation, gameTimeMinute);
                }
            }
        }

        public StaminaRuntimeCommandResult TryPauseWorkReserveAndBeginRecovery(
            string characterId,
            string recoveryRequestKey,
            string interactionId,
            long expectedGameTimeMinute)
        {
            if (!TryCommandContext(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    out OfficeRuntimeAgent actor, out StaminaRuntimeCommandResult failure)) return failure;
            if (!OfficeInteractionCatalog.TryGetDefinition(interactionId, out OfficeInteractionDefinition definition) ||
                (definition.ReservationPolicy != OfficeInteractionReservationPolicy.ExclusiveFurniture &&
                 definition.ReservationPolicy != OfficeInteractionReservationPolicy.SharedFurnitureCapacity))
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                    StaminaRuntimeFailureReason.ReservationRejected, interactionId);
            if (_sessions.TryGetValue(characterId, out RuntimeSession pending))
            {
                if (pending.ReservationAccepted ||
                    !string.Equals(pending.InteractionId, interactionId, StringComparison.Ordinal))
                    return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                        StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                        StaminaRuntimeFailureReason.ReservationRejected, interactionId);
                pending.RecoveryKey = recoveryRequestKey;
                bool alreadyClaimed = actor.HasActiveInteractionClaim &&
                    string.Equals(actor.ActiveInteractionId, interactionId, StringComparison.Ordinal);
                if (!alreadyClaimed)
                {
                    actor.SetAutonomousDestination(
                        recoveryRequestKey,
                        definition.SemanticLocation,
                        interactionId,
                        "체력 회복");
                    alreadyClaimed = actor.HasActiveInteractionClaim &&
                                     string.Equals(actor.ActiveInteractionId, interactionId,
                                         StringComparison.Ordinal);
                }
                if (!alreadyClaimed)
                    return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                        StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                        StaminaRuntimeFailureReason.NoReachableOffer, interactionId);
                pending.ReservationAccepted = true;
                pending.BaselineCompleted = actor.InteractionCompletedCount;
                pending.BaselineAborted = actor.InteractionAbortedCount;
                return Accepted(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryReservationAccepted,
                    interactionId, actor.ActiveInteractionOfferId, actor.ActiveSeatId);
            }

            OfficeRuntimeAgentLayoutSnapshot snapshot = actor.CaptureLayoutSnapshot();
            if (snapshot.HasAssignedTask && !actor.IsSeated)
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                    StaminaRuntimeFailureReason.AssignedWorkPauseRejected, interactionId);
            // CancelAssignedTask resumes the previously queued autonomy request immediately.
            // Clear that request first so it cannot acquire a competing offer between the task
            // cancellation and this correlated recovery reservation. The snapshot restores it
            // only after the exact assigned work item has resumed.
            actor.ClearAutonomousDestination();
            if (snapshot.HasAssignedTask) actor.CancelAssignedTask();
            actor.SetAutonomousDestination(
                recoveryRequestKey,
                definition.SemanticLocation,
                interactionId,
                "체력 회복");
            bool accepted = actor.HasActiveInteractionClaim &&
                            string.Equals(actor.ActiveInteractionId, interactionId, StringComparison.Ordinal) &&
                            actor.ActiveInteractionOfferId.Length > 0;
            _sessions.Add(characterId, new RuntimeSession
            {
                CharacterId = characterId,
                RecoveryKey = recoveryRequestKey,
                InteractionId = interactionId,
                PausedSnapshot = snapshot,
                BaselineCompleted = actor.InteractionCompletedCount,
                BaselineAborted = actor.InteractionAbortedCount,
                ReservationAccepted = accepted
            });
            if (!accepted)
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                    StaminaRuntimeFailureReason.NoReachableOffer, interactionId);
            return Accepted(characterId, recoveryRequestKey, expectedGameTimeMinute,
                StaminaRuntimeTransitionKind.RecoveryReservationAccepted,
                interactionId,
                actor.ActiveInteractionOfferId,
                actor.ActiveSeatId);
        }

        public StaminaRuntimeCommandResult TryCompleteRecoveryAndRelease(
            string characterId,
            string recoveryRequestKey,
            long expectedGameTimeMinute)
        {
            if (!TrySessionCommand(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    out OfficeRuntimeAgent actor, out RuntimeSession session,
                    out StaminaRuntimeCommandResult failure)) return failure;
            if (actor.InteractionPhase != OfficeRuntimeInteractionPhase.Performing ||
                !actor.HasActiveInteractionClaim)
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryAbortedAndReleased,
                    StaminaRuntimeFailureReason.CompletionRejected, session.InteractionId);

            actor.SetAutonomousDestination(
                recoveryRequestKey + ":complete",
                OfficeSemanticLocation.None,
                string.Empty,
                "회복 완료");
            bool released = !actor.HasActiveInteractionClaim &&
                            actor.InteractionCompletedCount > session.BaselineCompleted;
            if (!released)
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryAbortedAndReleased,
                    StaminaRuntimeFailureReason.CompletionRejected, session.InteractionId);
            session.FacilityReleased = true;
            return Accepted(characterId, recoveryRequestKey, expectedGameTimeMinute,
                StaminaRuntimeTransitionKind.RecoveryCompletedAndReleased,
                session.InteractionId);
        }

        public StaminaRuntimeCommandResult TryAbortRecoveryAndRelease(
            string characterId,
            string recoveryRequestKey,
            StaminaRuntimeFailureReason reason,
            long expectedGameTimeMinute)
        {
            if (!_sessions.TryGetValue(characterId ?? string.Empty, out RuntimeSession session) ||
                !string.Equals(session.RecoveryKey, recoveryRequestKey, StringComparison.Ordinal))
                return Rejected(characterId, recoveryRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.RecoveryAbortedAndReleased,
                    StaminaRuntimeFailureReason.StaleCorrelationKey);
            OfficeRuntimeAgent actor = FindActor(characterId);
            if (actor != null)
            {
                actor.ClearAutonomousDestination();
                RestorePausedSnapshot(actor, session.PausedSnapshot);
            }
            _sessions.Remove(characterId);
            return Accepted(characterId, recoveryRequestKey, expectedGameTimeMinute,
                StaminaRuntimeTransitionKind.RecoveryAbortedAndReleased,
                session.InteractionId,
                failureReason: reason);
        }

        public StaminaRuntimeCommandResult TryBeginAssignedSeatReturn(
            string characterId,
            string returnRequestKey,
            long expectedGameTimeMinute)
        {
            if (!_sessions.TryGetValue(characterId ?? string.Empty, out RuntimeSession session) ||
                !session.FacilityReleased)
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.StaleCorrelationKey);
            OfficeRuntimeAgent actor = FindActor(characterId);
            if (actor == null)
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.AgentUnavailable);
            actor.SetAutonomousDestination(
                returnRequestKey,
                OfficeSemanticLocation.Desk,
                "return-assigned-desk",
                "자리로 복귀");
            if (!string.Equals(actor.ActiveInteractionId, "return-assigned-desk", StringComparison.Ordinal))
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.AssignedSeatUnavailable);
            session.ReturnKey = returnRequestKey;
            session.BaselineAborted = actor.InteractionAbortedCount;
            return Accepted(characterId, returnRequestKey, expectedGameTimeMinute,
                StaminaRuntimeTransitionKind.AssignedSeatReturnStarted,
                "return-assigned-desk", actor.ActiveInteractionOfferId, actor.ActiveSeatId);
        }

        public StaminaRuntimeCommandResult TryResumePausedWorkAtAssignedSeat(
            string characterId,
            string returnRequestKey,
            long expectedGameTimeMinute)
        {
            if (!_sessions.TryGetValue(characterId ?? string.Empty, out RuntimeSession session) ||
                !string.Equals(session.ReturnKey, returnRequestKey, StringComparison.Ordinal))
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.StaleCorrelationKey);
            OfficeRuntimeAgent actor = FindActor(characterId);
            if (actor == null || !actor.IsSeated)
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.AssignedSeatUnavailable);

            bool resumed = true;
            OfficeRuntimeAgentLayoutSnapshot snapshot = session.PausedSnapshot;
            if (snapshot.HasAssignedTask)
            {
                resumed = actor.AssignOfficeTask(
                    snapshot.AssignedTaskId,
                    snapshot.AssignedActivity,
                    snapshot.AssignedWorkRemainingMinutes);
                if (resumed) RestoreAutonomyRequest(actor, snapshot);
            }
            else
            {
                RestoreAutonomyRequest(actor, snapshot);
            }
            if (!resumed)
                return Rejected(characterId, returnRequestKey, expectedGameTimeMinute,
                    StaminaRuntimeTransitionKind.AssignedSeatReturnFailedAndReleased,
                    StaminaRuntimeFailureReason.AssignedWorkPauseRejected);
            _sessions.Remove(characterId);
            return Accepted(characterId, returnRequestKey, expectedGameTimeMinute,
                StaminaRuntimeTransitionKind.AssignedWorkResumed,
                "return-assigned-desk", assignedSeatId: actor.ActiveSeatId);
        }

        private void PollSession(RuntimeSession session, long minute)
        {
            if (!session.ReservationAccepted) return;
            if (!_state.Stamina.TryGetSimulation(
                    session.CharacterId,
                    out CharacterStaminaSimulation simulation)) return;
            OfficeRuntimeAgent actor = FindActor(session.CharacterId);
            if (actor == null)
            {
                AbortPureAfterRuntimeFailure(
                    simulation, session.RecoveryKey,
                    StaminaRuntimeFailureReason.RuntimeReset, minute);
                return;
            }

            if (!session.FacilityReleased &&
                (actor.InteractionAbortedCount > session.BaselineAborted ||
                  (actor.InteractionPhase == OfficeRuntimeInteractionPhase.None &&
                   !actor.HasActiveInteractionClaim)))
            {
                AbortPureAfterRuntimeFailure(
                    simulation, session.RecoveryKey,
                    MapRuntimeFailure(actor.LastInteractionEndReason), minute);
                return;
            }

            bool progressed;
            do
            {
                progressed = false;
                switch (simulation.State.RecoveryPhase)
                {
                    case StaminaRecoveryPhase.SafeStopping:
                        if (actor.Phase == OfficeRuntimeAgentPhase.StandingUp ||
                            actor.Phase == OfficeRuntimeAgentPhase.LeavingSeat ||
                            actor.Phase == OfficeRuntimeAgentPhase.Navigating ||
                            actor.Phase == OfficeRuntimeAgentPhase.Idle)
                        {
                            simulation.ConfirmSafeStopCompleted(session.RecoveryKey, minute);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.SafeStopCompleted, actor);
                            progressed = true;
                        }
                        break;
                    case StaminaRecoveryPhase.StandingUp:
                        if (actor.Phase == OfficeRuntimeAgentPhase.Navigating ||
                            actor.Phase == OfficeRuntimeAgentPhase.Idle)
                        {
                            simulation.ConfirmStandUpCompleted(session.RecoveryKey, minute);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.StandUpCompleted, actor);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.TravelStarted, actor);
                            progressed = true;
                        }
                        break;
                    case StaminaRecoveryPhase.Traveling:
                        if (actor.InteractionPhase == OfficeRuntimeInteractionPhase.Aligning ||
                            actor.InteractionPhase == OfficeRuntimeInteractionPhase.Performing)
                        {
                            simulation.ConfirmFacilityArrived(session.RecoveryKey, minute);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.FacilityArrived, actor);
                            progressed = true;
                        }
                        break;
                    case StaminaRecoveryPhase.Aligning:
                        if (actor.InteractionPhase == OfficeRuntimeInteractionPhase.Performing)
                        {
                            simulation.ConfirmFacingAlignedAndPerforming(session.RecoveryKey, minute);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.PerformingStarted, actor);
                            progressed = true;
                        }
                        break;
                    case StaminaRecoveryPhase.ReturningToAssignedSeat:
                        if (session.ReturnKey.Length > 0 && actor.IsSeated &&
                            actor.InteractionPhase == OfficeRuntimeInteractionPhase.Performing &&
                            string.Equals(actor.ActiveInteractionId, "return-assigned-desk", StringComparison.Ordinal))
                        {
                            string returnKey = session.ReturnKey;
                            simulation.ConfirmAssignedSeatReturned(returnKey, minute);
                            EmitAsync(session, minute, StaminaRuntimeTransitionKind.AssignedSeatWorking, actor, returnKey);
                            TryResumePausedWorkAtAssignedSeat(
                                session.CharacterId, returnKey, minute);
                        }
                        break;
                }
            } while (progressed && _sessions.ContainsKey(session.CharacterId));
        }

        private void BeginSeatReturnOrRecordRetry(
            CharacterStaminaSimulation simulation,
            long minute)
        {
            string returnKey = simulation.AssignedSeatReturnRequestKey;
            StaminaRuntimeCommandResult returning = TryBeginAssignedSeatReturn(
                simulation.CharacterId,
                returnKey,
                minute);
            if (!returning.Accepted)
                simulation.RequestAssignedSeatReturnRetry(
                    returnKey,
                    StaminaRecoveryAbortReason.AssignedSeatUnavailable,
                    minute);
        }

        private void AbortPureAfterRuntimeFailure(
            CharacterStaminaSimulation simulation,
            string recoveryKey,
            StaminaRuntimeFailureReason reason,
            long minute)
        {
            StaminaRuntimeCommandResult aborted = TryAbortRecoveryAndRelease(
                simulation.CharacterId,
                recoveryKey,
                reason,
                minute);
            LastCommandTransitionForQa = aborted.Transition;
            _lastCommandByCharacter[simulation.CharacterId] = aborted.Transition;
            Debug.Log("STAMINA_RUNTIME_COMMAND | member=" + simulation.CharacterId +
                      " | kind=" + aborted.Transition.Kind +
                      " | failure=" + aborted.Transition.FailureReason +
                      " | interaction=" + aborted.Transition.InteractionId +
                      " | minute=" + minute);
            if (simulation.State.RecoveryPhase >= StaminaRecoveryPhase.SafeStopping &&
                simulation.State.RecoveryPhase <= StaminaRecoveryPhase.Performing)
                simulation.AbortRecoveryPlan(
                    recoveryKey,
                    MapAbortReason(reason),
                    minute);
        }

        private void TryBindCurrentSession()
        {
            PrototypeBootstrap bootstrap = FindFirstObjectByType<PrototypeBootstrap>(FindObjectsInactive.Include);
            StarterOfficeRuntimeBootstrap runtime =
                FindFirstObjectByType<StarterOfficeRuntimeBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null || bootstrap.State == null || runtime == null || !runtime.IsReady) return;
            if (ReferenceEquals(_state, bootstrap.State) && ReferenceEquals(_runtime, runtime)) return;

            UnbindCurrentState();
            _bootstrap = bootstrap;
            _runtime = runtime;
            _state = bootstrap.State;
            _capabilityAdapter = new StaminaRecoveryFurnitureCapabilityAdapter(runtime, _state);
            _state.BindStaminaRuntimeBridge(this);
            CharacterStaminaPresentationBinding.Bind(this, _state.Stamina);
        }

        private IStaminaRecoveryCapabilityQueryAdapter ConstrainPendingCapabilityQuery(
            CharacterStaminaSimulation simulation,
            RuntimeSession session)
        {
            var query = new StaminaRecoveryCapabilityQuery(
                simulation.CharacterId,
                simulation.RecoveryRequestKey,
                simulation.State.LastProcessedMinute);
            StaminaRecoveryCapabilityQueryResult current = _capabilityAdapter.Query(query);
            StaminaRecoveryCandidate[] matching = current.Candidates
                .Where(item => string.Equals(
                    item.InteractionId, session.InteractionId, StringComparison.Ordinal))
                .ToArray();
            return new FixedCapabilityQueryAdapter(
                new StaminaRecoveryCapabilityQueryResult(
                    query, current.SourceRevision, matching));
        }

        private sealed class FixedCapabilityQueryAdapter :
            IStaminaRecoveryCapabilityQueryAdapter
        {
            private readonly StaminaRecoveryCapabilityQueryResult _result;

            public FixedCapabilityQueryAdapter(StaminaRecoveryCapabilityQueryResult result) =>
                _result = result ?? throw new ArgumentNullException(nameof(result));

            public StaminaRecoveryCapabilityQueryResult Query(
                StaminaRecoveryCapabilityQuery query) => _result;
        }

        private void UnbindCurrentState()
        {
            if (_state != null) _state.UnbindStaminaRuntimeBridge(this);
            CharacterStaminaPresentationBinding.Unbind(this);
            _sessions.Clear();
            _lastCommandByCharacter.Clear();
            _capabilityAdapter = null;
            _state = null;
            _runtime = null;
            _bootstrap = null;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_priorityOwner, this)) _priorityOwner = null;
            UnbindCurrentState();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_priorityOwner, this)) _priorityOwner = null;
            UnbindCurrentState();
        }

        private bool TryCommandContext(
            string characterId,
            string key,
            long minute,
            out OfficeRuntimeAgent actor,
            out StaminaRuntimeCommandResult failure)
        {
            actor = FindActor(characterId);
            if (_state == null || minute != _state.Time.ElapsedMinutes || actor == null ||
                actor.IsPlayerControlled)
            {
                failure = Rejected(characterId, key, Math.Max(0, minute),
                    StaminaRuntimeTransitionKind.RecoveryReservationRejected,
                    StaminaRuntimeFailureReason.AgentUnavailable);
                return false;
            }
            failure = default;
            return true;
        }

        private bool TrySessionCommand(
            string characterId,
            string key,
            long minute,
            out OfficeRuntimeAgent actor,
            out RuntimeSession session,
            out StaminaRuntimeCommandResult failure)
        {
            actor = FindActor(characterId);
            session = null;
            if (_state == null || minute != _state.Time.ElapsedMinutes || actor == null ||
                !_sessions.TryGetValue(characterId ?? string.Empty, out session) ||
                !string.Equals(session.RecoveryKey, key, StringComparison.Ordinal))
            {
                failure = Rejected(characterId, key, Math.Max(0, minute),
                    StaminaRuntimeTransitionKind.RecoveryAbortedAndReleased,
                    StaminaRuntimeFailureReason.StaleCorrelationKey);
                return false;
            }
            failure = default;
            return true;
        }

        private OfficeRuntimeAgent FindActor(string characterId)
        {
            if (_runtime == null || !_runtime.IsReady || _runtime.World == null ||
                !_runtime.World.Registry.TryGet(characterId ?? string.Empty, out OfficeRuntimeAgent actor))
                return null;
            return actor;
        }

        private static bool RestorePausedSnapshot(
            OfficeRuntimeAgent actor,
            OfficeRuntimeAgentLayoutSnapshot snapshot)
        {
            actor.ClearAutonomousDestination();
            if (snapshot.HasAssignedTask)
            {
                if (!actor.AssignOfficeTask(
                        snapshot.AssignedTaskId,
                        snapshot.AssignedActivity,
                        snapshot.AssignedWorkRemainingMinutes)) return false;
                RestoreAutonomyRequest(actor, snapshot);
                return true;
            }
            RestoreAutonomyRequest(actor, snapshot);
            return true;
        }

        private static void RestoreAutonomyRequest(
            OfficeRuntimeAgent actor,
            OfficeRuntimeAgentLayoutSnapshot snapshot)
        {
            if (snapshot.HasAutonomyRequest)
                actor.SetAutonomousDestination(
                    snapshot.AutonomyIntentId,
                    snapshot.AutonomyLocation,
                    snapshot.AutonomyInteractionId,
                    snapshot.AutonomyStatus);
            else
                actor.ClearAutonomousDestination();
        }

        private void EmitAsync(
            RuntimeSession session,
            long minute,
            StaminaRuntimeTransitionKind kind,
            OfficeRuntimeAgent actor,
            string correlationKey = null)
        {
            Transitioned?.Invoke(new StaminaRuntimeTransition(
                session.CharacterId,
                correlationKey ?? session.RecoveryKey,
                minute,
                kind,
                interactionId: session.InteractionId,
                runtimeOfferId: actor?.ActiveInteractionOfferId ?? string.Empty,
                assignedSeatId: actor?.ActiveSeatId ?? string.Empty));
        }

        private static StaminaRuntimeCommandResult Accepted(
            string characterId,
            string key,
            long minute,
            StaminaRuntimeTransitionKind kind,
            string interactionId = "",
            string offerId = "",
            string assignedSeatId = "",
            StaminaRuntimeFailureReason failureReason = StaminaRuntimeFailureReason.None) =>
            new StaminaRuntimeCommandResult(
                true,
                new StaminaRuntimeTransition(
                    RequiredId(characterId), RequiredKey(key), Math.Max(0, minute), kind,
                    failureReason, interactionId, offerId, assignedSeatId));

        private static StaminaRuntimeCommandResult Rejected(
            string characterId,
            string key,
            long minute,
            StaminaRuntimeTransitionKind kind,
            StaminaRuntimeFailureReason reason,
            string interactionId = "") =>
            new StaminaRuntimeCommandResult(
                false,
                new StaminaRuntimeTransition(
                    RequiredId(characterId), RequiredKey(key), Math.Max(0, minute), kind,
                    reason, interactionId));

        private static string RequiredId(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown-character" : value.Trim();

        private static string RequiredKey(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown-correlation" : value.Trim();

        private static StaminaRecoveryAbortReason MapAbortReason(StaminaRuntimeFailureReason reason)
        {
            switch (reason)
            {
                case StaminaRuntimeFailureReason.PathUnavailable:
                    return StaminaRecoveryAbortReason.PathUnavailable;
                case StaminaRuntimeFailureReason.ArrivalInvalid:
                    return StaminaRecoveryAbortReason.ArrivalInvalid;
                case StaminaRuntimeFailureReason.LayoutChanged:
                    return StaminaRecoveryAbortReason.LayoutChanged;
                case StaminaRuntimeFailureReason.CompletionRejected:
                    return StaminaRecoveryAbortReason.CompletionRejected;
                case StaminaRuntimeFailureReason.AssignedSeatUnavailable:
                    return StaminaRecoveryAbortReason.AssignedSeatUnavailable;
                case StaminaRuntimeFailureReason.RuntimeReset:
                case StaminaRuntimeFailureReason.AgentUnavailable:
                    return StaminaRecoveryAbortReason.RuntimeReset;
                default:
                    return StaminaRecoveryAbortReason.ReservationUnavailable;
            }
        }

        private static StaminaRuntimeFailureReason MapRuntimeFailure(
            OfficeRuntimeInteractionEndReason reason)
        {
            switch (reason)
            {
                case OfficeRuntimeInteractionEndReason.PathUnavailable:
                    return StaminaRuntimeFailureReason.PathUnavailable;
                case OfficeRuntimeInteractionEndReason.ArrivalRevalidationFailed:
                    return StaminaRuntimeFailureReason.ArrivalInvalid;
                case OfficeRuntimeInteractionEndReason.LayoutChanged:
                    return StaminaRuntimeFailureReason.LayoutChanged;
                case OfficeRuntimeInteractionEndReason.RuntimeReset:
                case OfficeRuntimeInteractionEndReason.Disabled:
                case OfficeRuntimeInteractionEndReason.Destroyed:
                    return StaminaRuntimeFailureReason.RuntimeReset;
                default:
                    return StaminaRuntimeFailureReason.ReservationRejected;
            }
        }
    }
}

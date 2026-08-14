using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating.UI;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.Stamina;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeAutonomyCoordinator : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private OfficeWorkerAgent[] agents = Array.Empty<OfficeWorkerAgent>();
        [SerializeField] private OfficeWaypoint[] waypoints = Array.Empty<OfficeWaypoint>();
        private IOfficeRuntimeAgent[] _runtimeAgents = Array.Empty<IOfficeRuntimeAgent>();
        [SerializeField] private OfficeSeatRegistry seatRegistry;
        [SerializeField] private OfficeSeatPlacementPanel seatPlacementPanel;
        [SerializeField] private float refreshIntervalSeconds = 0.35f;
        private float _refreshRemaining;
        private bool _initialized;
        private bool _seatingInitialized;
        private OfficeSeatingState _seatingState;
        private OfficePlayerSeatingPresenter _playerSeatingPresenter;
        private readonly Dictionary<string, string> _retainedSeatAssignments =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private int _seatingRegistryRevision = -1;
        private GameState _boundGameState;
        private OfficeAttendancePhase? _lastRuntimeAttendancePhase;
        private int _nextAttendanceArrivalIndex;
        private float _arrivalReleaseRemaining;
        private IOfficeRuntimeAgent _lastAttendanceEntrant;
        private Vector2 _lastAttendanceEntryPosition;
        private GameState _attendanceAudioState;
        private long _attendanceDoorSfxShiftKey = long.MinValue;
        private long _attendanceDoorSfxArmedShiftKey = long.MinValue;
        private DateTime? _attendanceAudioObservedAt;
        private int _attendanceDoorSfxPlayCount;
        private const float MinimumAttendanceEntranceClearance = 0.72f;

        public OfficeSeatingState SeatingState => _seatingState;
        public bool IsSeatingRuntimeReady =>
            _seatingInitialized && _seatingState != null &&
            seatRegistry != null && seatRegistry.isActiveAndEnabled &&
            seatRegistry.SeatCount > 0 && seatRegistry.RuntimeRevision == _seatingRegistryRevision;
        public int AttendanceDoorSfxPlayCount => _attendanceDoorSfxPlayCount;

        public static bool IsAttendanceDoorSfxEligibleAt(DateTime now)
        {
            int minuteOfDay = checked(now.Hour * 60 + now.Minute);
            return OfficeAttendanceRules.Resolve(now) == OfficeAttendancePhase.Working &&
                   minuteOfDay >= OfficeAttendanceRules.WorkStartsMinuteOfDay &&
                   minuteOfDay < OfficeAttendanceRules.WorkEndsMinuteOfDay;
        }

        public static bool ShouldArmAttendanceDoorSfxOnStateBind(DateTime now)
        {
            return OfficeAttendanceRules.Resolve(now) == OfficeAttendancePhase.BeforeWork;
        }

        public static bool ShouldArmAttendanceDoorSfxAfterObservedTransition(
            DateTime previous,
            DateTime current)
        {
            return previous.Date != current.Date &&
                   OfficeAttendanceRules.Resolve(previous) != OfficeAttendancePhase.Working &&
                   OfficeAttendanceRules.Resolve(current) == OfficeAttendancePhase.Working;
        }

        public void Configure(
            PrototypeBootstrap newBootstrap,
            OfficeWorkerAgent[] newAgents,
            OfficeWaypoint[] newWaypoints)
        {
            ResetSeatingRuntimeBindings();
            bootstrap = newBootstrap;
            agents = newAgents ?? Array.Empty<OfficeWorkerAgent>();
            waypoints = newWaypoints ?? Array.Empty<OfficeWaypoint>();
            _runtimeAgents = Array.Empty<IOfficeRuntimeAgent>();
            _boundGameState = null;
            _lastRuntimeAttendancePhase = null;
            _nextAttendanceArrivalIndex = 0;
            _arrivalReleaseRemaining = 0f;
            _lastAttendanceEntrant = null;
            _lastAttendanceEntryPosition = Vector2.zero;
            BindAttendanceAudioState(newBootstrap != null ? newBootstrap.State : null);
            _initialized = false;
        }

        public void ConfigureRuntime(
            PrototypeBootstrap newBootstrap,
            IOfficeRuntimeAgent[] newAgents)
        {
            ResetSeatingRuntimeBindings();
            bootstrap = newBootstrap;
            agents = Array.Empty<OfficeWorkerAgent>();
            waypoints = Array.Empty<OfficeWaypoint>();
            _runtimeAgents = newAgents ?? Array.Empty<IOfficeRuntimeAgent>();
            seatRegistry = null;
            seatPlacementPanel = null;
            _seatingState = null;
            _boundGameState = null;
            _lastRuntimeAttendancePhase = null;
            _nextAttendanceArrivalIndex = 0;
            _arrivalReleaseRemaining = 0f;
            _lastAttendanceEntrant = null;
            _lastAttendanceEntryPosition = Vector2.zero;
            BindAttendanceAudioState(newBootstrap != null ? newBootstrap.State : null);
            _initialized = false;
        }

        public void ConfigureSeatingRuntime(
            OfficeSeatRegistry newSeatRegistry,
            OfficeSeatingState existingState = null,
            OfficeSeatPlacementPanel placementPanel = null)
        {
            ResetSeatingRuntimeBindings();
            seatRegistry = newSeatRegistry == null
                ? throw new ArgumentNullException(nameof(newSeatRegistry))
                : newSeatRegistry;
            _seatingState = existingState;
            _retainedSeatAssignments.Clear();
            CapturePersistentAssignments(_seatingState);
            _seatingRegistryRevision = -1;
            _boundGameState = null;
            if (placementPanel != null) seatPlacementPanel = placementPanel;
            _seatingInitialized = false;
        }

        public void InitializeNow()
        {
            if (bootstrap == null || bootstrap.State == null) return;
            AutonomousOfficeSimulation.EnsureIntents(
                bootstrap.State.WorldSeed,
                bootstrap.State.Family,
                bootstrap.State.Time.ElapsedMinutes);
            if (_runtimeAgents.Length == 0) InitializeSeatingRuntime();
            _initialized = true;
            RefreshNow();
        }

        public void RefreshNow()
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.AutonomyRefresh);
            if (bootstrap == null || bootstrap.State == null) return;
            BindAttendanceAudioState(bootstrap.State);
            if (!_initialized) _initialized = true;
            AutonomousOfficeSimulation.EnsureIntents(
                bootstrap.State.WorldSeed,
                bootstrap.State.Family,
                bootstrap.State.Time.ElapsedMinutes);
            if (_runtimeAgents.Length > 0)
            {
                RefreshRuntimeAgents();
                return;
            }
            InitializeSeatingRuntime();

            var reserved = new HashSet<OfficeWaypoint>(
                agents.Where(item => item != null && item.isActiveAndEnabled &&
                                             item.HasAssignedTask && item.TargetWaypoint != null)
                    .Select(item => item.TargetWaypoint));
            foreach (var agent in agents
                         .Where(item => item != null && item.isActiveAndEnabled)
                         .OrderBy(item => item.AgentId, StringComparer.Ordinal))
            {
                var member = bootstrap.State.Family.Members.FirstOrDefault(item => item.MemberId == agent.AgentId);
                if (member == null)
                {
                    agent.SetOfficeSeatingRuntimeEnabled(false);
                    agent.ClearAutonomousDestination();
                    agent.ClearSeatDestination();
                    continue;
                }

                var memberCanUseSeating =
                    IsSeatingRuntimeReady &&
                    agent.HasOfficeSeatingAnimation &&
                    OfficeSeatRuntimeEligibility.HasClaimableSeat(
                        _seatingState,
                        member.MemberId,
                        seatId => seatRegistry.TryGetAuthoring(seatId, out var authoring) &&
                                  authoring != null && authoring.IsRuntimeValid);
                agent.SetOfficeSeatingRuntimeEnabled(memberCanUseSeating);

                OfficeSemanticLocation presentationLocation = PresentationLocation(member);
                var candidates = ResolveCandidates(presentationLocation);
                OfficeWaypoint autonomyWaypoint = null;
                if (candidates.Length > 0)
                {
                    autonomyWaypoint = PickWaypoint(member, candidates, reserved);
                }

                var status =
                    $"{PresentationActionLabel(member)} · {member.Autonomy.MoodLabel(member.Energy, member.Stress)} · " +
                    $"체{member.Energy}/스{member.Stress}";
                var autonomyIntentId = autonomyWaypoint == null
                    ? string.Empty
                    : PresentationIntentId(member, autonomyWaypoint.WaypointId);

                var contractSeatWaypoint = agent.HasAssignedTask &&
                                           agent.TargetWaypoint != null &&
                                           agent.TargetWaypoint.Activity == OfficeActivity.Work
                    ? agent.TargetWaypoint
                    : null;
                if (contractSeatWaypoint != null)
                {
                    EnsureSeatDestination(
                        agent,
                        member,
                        $"contract:{agent.AssignedTaskId}",
                        contractSeatWaypoint,
                        $"계약 · {status}");
                }
                else if (!agent.HasAssignedTask &&
                         presentationLocation == OfficeSemanticLocation.Desk &&
                         autonomyWaypoint != null)
                {
                    EnsureSeatDestination(agent, member, autonomyIntentId, autonomyWaypoint, status);
                }
                else
                {
                    agent.ClearSeatDestination();
                }

                if (autonomyWaypoint == null)
                {
                    if (!agent.HasAssignedTask) agent.ClearAutonomousDestination();
                    continue;
                }

                agent.SetAutonomousDestination(autonomyIntentId, autonomyWaypoint, status);
                if (presentationLocation != OfficeSemanticLocation.Lounge &&
                    presentationLocation != OfficeSemanticLocation.MeetingRoom)
                {
                    reserved.Add(autonomyWaypoint);
                }
            }
        }

        private void Awake()
        {
            InitializeNow();
        }

        private void Update()
        {
            if (!_initialized) InitializeNow();
            if (!_initialized) return;
            _refreshRemaining -= Time.unscaledDeltaTime;
            if (_refreshRemaining > 0f) return;
            _refreshRemaining = Mathf.Max(0.05f, refreshIntervalSeconds);
            RefreshNow();
        }

        private void OnDisable()
        {
            ResetSeatingRuntimeBindings();
            _initialized = false;
        }

        private void OnDestroy()
        {
            ResetSeatingRuntimeBindings();
        }

        private void InitializeSeatingRuntime()
        {
            if (seatRegistry == null)
                seatRegistry = FindFirstObjectByType<OfficeSeatRegistry>();
            if (seatRegistry == null || !seatRegistry.isActiveAndEnabled)
            {
                if (_seatingInitialized) ResetSeatingRuntimeBindings();
                return;
            }

            seatRegistry.Rebuild();
            var revision = seatRegistry.RuntimeRevision;
            var definitions = seatRegistry.Definitions;
            if (definitions.Count == 0)
            {
                if (_seatingState != null) CapturePersistentAssignments(_seatingState);
                if (_seatingInitialized || _seatingState != null) ResetSeatingRuntimeBindings();
                _seatingState = null;
                _seatingRegistryRevision = revision;
                seatPlacementPanel?.ResetOfficeSeatingRuntime();
                return;
            }

            var sessionChanged = OfficeSeatRuntimeEligibility.SessionIdentityChanged(
                _boundGameState,
                bootstrap == null ? null : bootstrap.State);
            var stateMatches = StateMatchesDefinitions(_seatingState, definitions);
            if (sessionChanged)
            {
                CapturePersistentAssignments(_seatingState);
                ResetSeatingRuntimeBindings();
                _seatingState = CreateSeatingState(definitions);
                RestorePersistentAssignments(_seatingState, definitions);
                _boundGameState = bootstrap.State;
                stateMatches = true;
            }
            else if (!stateMatches)
            {
                CapturePersistentAssignments(_seatingState);
                ResetSeatingRuntimeBindings();
                _seatingState = CreateSeatingState(definitions);
                RestorePersistentAssignments(_seatingState, definitions);
                _boundGameState = bootstrap.State;
                stateMatches = true;
            }
            else if (_seatingInitialized && revision != _seatingRegistryRevision)
            {
                ResetSeatingRuntimeBindings();
            }

            if (_seatingInitialized && revision == _seatingRegistryRevision && stateMatches) return;

            if (seatPlacementPanel == null)
                seatPlacementPanel = FindFirstObjectByType<OfficeSeatPlacementPanel>();
            seatPlacementPanel?.Configure(_seatingState);
            var playerController = FindFirstObjectByType<PrototypePlayerController>();
            if (playerController != null)
            {
                _playerSeatingPresenter = playerController.GetComponent<OfficePlayerSeatingPresenter>();
                if (_playerSeatingPresenter == null)
                    _playerSeatingPresenter = playerController.gameObject.AddComponent<OfficePlayerSeatingPresenter>();
                _playerSeatingPresenter.Configure(bootstrap, seatRegistry, _seatingState);
            }
            _seatingRegistryRevision = revision;
            _seatingInitialized = true;
        }

        private void RefreshRuntimeAgents()
        {
            if (StarterOfficeRuntimeBootstrap.IsLayoutRebuilding) return;
            DateTime now = bootstrap.State.Time.Now;
            OfficeAttendancePhase attendance = OfficeAttendanceRules.Resolve(now);
            RefreshAttendanceDoorSfxArm(now, attendance);
            IOfficeRuntimeAgent[] orderedAgents = _runtimeAgents
                          .Where(item => item != null && item is Component component &&
                                         component != null && component.gameObject.activeInHierarchy)
                          .OrderBy(item => AttendanceOrder(item.AgentId))
                          .ThenBy(item => item.AgentId, StringComparer.Ordinal)
                          .ToArray();
            if (attendance != OfficeAttendancePhase.Working)
            {
                _nextAttendanceArrivalIndex = 0;
                _arrivalReleaseRemaining = 0f;
                _lastAttendanceEntrant = null;
                _lastAttendanceEntryPosition = Vector2.zero;
            }
            else
            {
                _arrivalReleaseRemaining = Mathf.Max(0f, _arrivalReleaseRemaining - refreshIntervalSeconds);
                while (_nextAttendanceArrivalIndex < orderedAgents.Length &&
                       !orderedAgents[_nextAttendanceArrivalIndex].IsPresentationAway)
                    _nextAttendanceArrivalIndex++;
                bool entranceIsClear = _lastAttendanceEntrant == null ||
                                       _lastAttendanceEntrant.IsPresentationAway ||
                                       Vector2.Distance(
                                           _lastAttendanceEntrant.Position,
                                           _lastAttendanceEntryPosition) >=
                                       MinimumAttendanceEntranceClearance;
                if (_nextAttendanceArrivalIndex < orderedAgents.Length &&
                    OfficeAttendanceRules.HasArrived(now, _nextAttendanceArrivalIndex) &&
                    _arrivalReleaseRemaining <= 0f &&
                    entranceIsClear)
                {
                    IOfficeRuntimeAgent entrant = orderedAgents[_nextAttendanceArrivalIndex];
                    entrant.SetAttendanceOutside(false, false);
                    if (!entrant.IsPresentationAway)
                    {
                        TryPlayAttendanceDoorSfx(now);
                        _lastAttendanceEntrant = entrant;
                        _lastAttendanceEntryPosition = entrant.Position;
                        _nextAttendanceArrivalIndex++;
                        _arrivalReleaseRemaining = 0.35f;
                    }
                }
            }
            for (int index = 0; index < orderedAgents.Length; index++)
            {
                IOfficeRuntimeAgent agent = orderedAgents[index];
                bool shouldBeOutside = attendance != OfficeAttendancePhase.Working ||
                                       agent.IsPresentationAway;
                agent.SetAttendanceOutside(
                    shouldBeOutside,
                    shouldBeOutside && attendance == OfficeAttendancePhase.AfterWork);
                if (shouldBeOutside) continue;
                FamilyMemberState member = bootstrap.State.Family.Members.FirstOrDefault(item =>
                    string.Equals(item.MemberId, agent.AgentId, StringComparison.Ordinal));
                if (member == null)
                {
                    long shiftBlock = bootstrap.State.Time.ElapsedMinutes / 20L;
                    agent.SetAutonomousDestination(
                        "employee-shift:" + agent.AgentId + ":" + shiftBlock,
                        OfficeSemanticLocation.OpenArea,
                        string.Empty,
                        "사무실 업무 · 기분 좋음");
                    continue;
                }
                // Attendance and contract ownership are evaluated above/before this routine
                // autonomy projection. While stamina owns a correlated claim/return session,
                // defer only the replaceable family-autonomy intent so it cannot supersede the
                // live facility handle or restart its path every refresh.
                if (StaminaRecoveryRuntimeCoordinator.BlocksRoutineAutonomy(agent.AgentId))
                    continue;
                if (agent.IsPlayerControlled)
                {
                    agent.ClearAutonomousDestination();
                    continue;
                }
                OfficeSemanticLocation presentationLocation = PresentationLocation(member);
                string status =
                    $"{PresentationActionLabel(member)} · {member.Autonomy.MoodLabel(member.Energy, member.Stress)} · " +
                    $"체{member.Energy}/스{member.Stress}";
                string intentId = PresentationIntentId(member, ((int)presentationLocation).ToString());
                string interactionId = OfficeInteractionCatalog.TryResolveActiveDefinition(
                    member,
                    out OfficeInteractionDefinition interaction)
                    ? interaction.InteractionId
                    : string.Empty;
                agent.SetAutonomousDestination(intentId, presentationLocation, interactionId, status);
            }
            if (_lastRuntimeAttendancePhase != attendance)
            {
                Debug.Log(
                    "STARTER_OFFICE_ATTENDANCE | phase=" + attendance +
                    " time=" + now.ToString("HH:mm") +
                    " actors=" + orderedAgents.Length);
                _lastRuntimeAttendancePhase = attendance;
            }
        }

        private void BindAttendanceAudioState(GameState state)
        {
            if (ReferenceEquals(_attendanceAudioState, state)) return;
            long previousShiftKey = _attendanceDoorSfxShiftKey;
            _attendanceAudioState = state;
            if (state == null)
            {
                _attendanceDoorSfxShiftKey = long.MinValue;
                _attendanceDoorSfxArmedShiftKey = long.MinValue;
                _attendanceAudioObservedAt = null;
                _attendanceDoorSfxPlayCount = 0;
                return;
            }

            DateTime now = state.Time.Now;
            _attendanceAudioObservedAt = now;
            bool preservesCurrentShift = previousShiftKey == now.Date.Ticks &&
                                         IsAttendanceDoorSfxEligibleAt(now);
            if (preservesCurrentShift)
            {
                _attendanceDoorSfxArmedShiftKey = long.MinValue;
                return;
            }

            _attendanceDoorSfxPlayCount = 0;
            if (ShouldArmAttendanceDoorSfxOnStateBind(now))
            {
                _attendanceDoorSfxShiftKey = long.MinValue;
                _attendanceDoorSfxArmedShiftKey = now.Date.Ticks;
                return;
            }

            // A newly loaded state that is already inside the work window is not a shift-start
            // event. Consume that date without emitting a cue when its actors are rebound.
            _attendanceDoorSfxShiftKey = IsAttendanceDoorSfxEligibleAt(now)
                ? now.Date.Ticks
                : long.MinValue;
            _attendanceDoorSfxArmedShiftKey = long.MinValue;
        }

        private void RefreshAttendanceDoorSfxArm(
            DateTime now,
            OfficeAttendancePhase attendance)
        {
            bool crossedFromObservedNonWorkDay = _attendanceAudioObservedAt.HasValue &&
                                                 ShouldArmAttendanceDoorSfxAfterObservedTransition(
                                                     _attendanceAudioObservedAt.Value,
                                                     now);
            if (attendance == OfficeAttendancePhase.BeforeWork || crossedFromObservedNonWorkDay)
                ArmAttendanceDoorSfxForUpcomingShift(now);
            _attendanceAudioObservedAt = now;
        }

        private void ArmAttendanceDoorSfxForUpcomingShift(DateTime now)
        {
            long shiftKey = now.Date.Ticks;
            if (_attendanceDoorSfxShiftKey != shiftKey)
                _attendanceDoorSfxArmedShiftKey = shiftKey;
        }

        private void TryPlayAttendanceDoorSfx(DateTime now)
        {
            // This method is called only after an entrant was successfully released. Keep the
            // cue valid across normal clock jumps (for example 08:50 -> 09:50 via +1 hour), then
            // let the per-shift key debounce every later family member and runtime rebind.
            if (!IsAttendanceDoorSfxEligibleAt(now)) return;
            long shiftKey = now.Date.Ticks;
            if (_attendanceDoorSfxShiftKey == shiftKey ||
                _attendanceDoorSfxArmedShiftKey != shiftKey) return;
            bool played = GameAudioCoordinator.Instance.PlaySfx("door_open", 0.28f);
            if (played)
            {
                _attendanceDoorSfxShiftKey = shiftKey;
                _attendanceDoorSfxArmedShiftKey = long.MinValue;
                _attendanceDoorSfxPlayCount++;
            }
            Debug.Log(
                "STARTER_OFFICE_ATTENDANCE_DOOR_SFX | cue=door_open " +
                "visualAnimation=false closeCue=false shift=" + now.ToString("yyyy-MM-dd") +
                " trigger=first-successful-entrant-release" +
                " playCount=" + _attendanceDoorSfxPlayCount +
                " played=" + played);
        }

        private static int AttendanceOrder(string memberId)
        {
            return memberId switch
            {
                "player" => 0,
                "older_sister" => 1,
                "father" => 2,
                "mother" => 3,
                _ => 100
            };
        }

        private static OfficeSeatingState CreateSeatingState(
            IReadOnlyList<OfficeSeating.Authoring.OfficeSeatDefinition> definitions)
        {
            return new OfficeSeatingState(definitions.Select(item =>
                new FamilyCompany.Simulation.OfficeSeating.OfficeSeatDefinition(
                    item.SeatId,
                    new FamilyCompany.Simulation.OfficeSeating.OfficeSeatPosition(
                        item.SitPosition.X,
                        item.SitPosition.Z))));
        }

        private static bool StateMatchesDefinitions(
            OfficeSeatingState state,
            IReadOnlyList<OfficeSeating.Authoring.OfficeSeatDefinition> definitions)
        {
            if (state == null || state.SeatCount != definitions.Count) return false;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (!state.TryGetSeat(definition.SeatId, out var seat) ||
                    !seat.Position.X.Equals((double)definition.SitPosition.X) ||
                    !seat.Position.Z.Equals((double)definition.SitPosition.Z))
                {
                    return false;
                }
            }
            return true;
        }

        private void CapturePersistentAssignments(OfficeSeatingState state)
        {
            if (state == null) return;
            var seatsInState = new HashSet<string>(
                state.GetSeats().Select(item => item.SeatId),
                StringComparer.Ordinal);
            foreach (var seatId in seatsInState) _retainedSeatAssignments.Remove(seatId);

            var assignments = state.ExportPersistentAssignments().Assignments;
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var duplicateMemberSeats = _retainedSeatAssignments
                    .Where(item => string.Equals(item.Value, assignment.MemberId, StringComparison.Ordinal))
                    .Select(item => item.Key)
                    .ToArray();
                foreach (var duplicateSeat in duplicateMemberSeats)
                    _retainedSeatAssignments.Remove(duplicateSeat);
                _retainedSeatAssignments[assignment.SeatId] = assignment.MemberId;
            }
        }

        private void RestorePersistentAssignments(
            OfficeSeatingState state,
            IReadOnlyList<OfficeSeating.Authoring.OfficeSeatDefinition> definitions)
        {
            var validIds = new HashSet<string>(definitions.Select(item => item.SeatId), StringComparer.Ordinal);
            var candidates = new Dictionary<string, string>(_retainedSeatAssignments, StringComparer.Ordinal);
            for (var index = 0; index < definitions.Count; index++)
            {
                var seatId = definitions[index].SeatId;
                if (candidates.ContainsKey(seatId) ||
                    !seatRegistry.TryGetAuthoring(seatId, out var authoring) ||
                    string.IsNullOrEmpty(authoring.LongTermAssignedMemberId) ||
                    candidates.Values.Contains(authoring.LongTermAssignedMemberId, StringComparer.Ordinal))
                {
                    continue;
                }
                candidates.Add(seatId, authoring.LongTermAssignedMemberId);
            }
            var seenMembers = new HashSet<string>(StringComparer.Ordinal);
            var assignments = candidates
                .Where(item => validIds.Contains(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Where(item => seenMembers.Add(item.Value))
                .Select(item => new OfficeSeatAssignment(item.Key, item.Value))
                .ToArray();
            state.TryImportPersistentAssignments(new OfficeSeatingAssignmentSnapshot(assignments), out _);
        }

        private void ResetSeatingRuntimeBindings()
        {
            if (_playerSeatingPresenter != null)
                _playerSeatingPresenter.ResetOfficeSeatingRuntime();
            _playerSeatingPresenter = null;
            seatPlacementPanel?.ResetOfficeSeatingRuntime();
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.ResetOfficeSeatingRuntime();
                agent.SetOfficeSeatingRuntimeEnabled(false);
            }
            _seatingInitialized = false;
            _refreshRemaining = 0f;
        }

        private bool EnsureSeatDestination(
            OfficeWorkerAgent agent,
            FamilyMemberState member,
            string intentId,
            OfficeWaypoint navigationWaypoint,
            string status)
        {
            if (!IsSeatingRuntimeReady || !agent.IsOfficeSeatingRuntimeEnabled) return false;
            if (agent.HasActiveSeatClaim)
            {
                if (!seatRegistry.TryGetAuthoring(agent.ActiveSeatId, out _))
                {
                    agent.ClearSeatDestination();
                    return false;
                }

                return agent.UpdateActiveSeatIntent(intentId, status);
            }

            var origin = navigationWaypoint.transform.position;
            var candidates = _seatingState.GetSeats()
                .Where(item => SeatCanBeClaimedBy(item, member.MemberId))
                .Where(item => seatRegistry.TryGetAuthoring(item.SeatId, out _))
                .OrderBy(item => string.Equals(item.AssignedMemberId, member.MemberId, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(item => FlatDistanceSquared(item.Position, origin))
                .ThenBy(item => item.SeatId, StringComparer.Ordinal)
                .ToArray();
            foreach (var candidate in candidates)
            {
                var token =
                    $"office-seat-runtime-v1:{bootstrap.State.WorldSeed}:{member.MemberId}:{intentId}:{candidate.SeatId}";
                if (!OfficeSeatRuntimeClaim.TryReserve(
                        _seatingState,
                        candidate.SeatId,
                        member.MemberId,
                        token,
                        out var claim,
                        out _))
                {
                    continue;
                }

                if (!seatRegistry.TryGetAuthoring(candidate.SeatId, out var authoring) ||
                    !agent.SetSeatDestination(
                        intentId,
                        authoring,
                        authoring.SemanticDestination == null
                            ? navigationWaypoint
                            : authoring.SemanticDestination,
                        claim,
                        status))
                {
                    claim.Dispose();
                    continue;
                }

                return true;
            }

            return false;
        }

        private OfficeWaypoint[] ResolveCandidates(OfficeSemanticLocation location)
        {
            var activity = OfficeActivity.Break;
            switch (location)
            {
                case OfficeSemanticLocation.Desk:
                    activity = OfficeActivity.Work;
                    break;
                case OfficeSemanticLocation.Reception:
                    activity = OfficeActivity.Reception;
                    break;
                case OfficeSemanticLocation.Printer:
                    activity = OfficeActivity.Printing;
                    break;
                case OfficeSemanticLocation.MeetingRoom:
                    activity = OfficeActivity.Meeting;
                    break;
                case OfficeSemanticLocation.Lounge:
                    activity = OfficeActivity.Break;
                    break;
                case OfficeSemanticLocation.Filing:
                    activity = OfficeActivity.Printing;
                    break;
                case OfficeSemanticLocation.Water:
                case OfficeSemanticLocation.Coffee:
                case OfficeSemanticLocation.OpenArea:
                    activity = OfficeActivity.Break;
                    break;
                case OfficeSemanticLocation.Exit:
                    activity = OfficeActivity.Outside;
                    break;
            }

            return waypoints.Where(item => item != null && item.Activity == activity).ToArray();
        }

        private static OfficeSemanticLocation PresentationLocation(FamilyMemberState member)
        {
            OfficeMicroActionState micro = member.Autonomy.MicroAction;
            return micro.Action != OfficeMicroAction.None &&
                   micro.TargetLocation != OfficeSemanticLocation.None
                ? micro.TargetLocation
                : member.Autonomy.TargetLocation;
        }

        private static string PresentationActionLabel(FamilyMemberState member)
        {
            string micro = member.Autonomy.MicroAction.ActionLabel;
            return string.IsNullOrEmpty(micro) ? member.Autonomy.ActionLabel : micro;
        }

        private static string PresentationIntentId(FamilyMemberState member, string destinationKey)
        {
            OfficeMicroActionState micro = member.Autonomy.MicroAction;
            return $"{(int)member.Autonomy.CurrentAction}:{member.Autonomy.ActionStartedMinute}:" +
                   $"micro:{(int)micro.Action}:{micro.StartedMinute}:{micro.SequenceIndex}:" +
                   $"{micro.TargetId}:{destinationKey}";
        }

        private OfficeWaypoint PickWaypoint(
            FamilyMemberState member,
            OfficeWaypoint[] candidates,
            HashSet<OfficeWaypoint> reserved)
        {
            var start = StableRandom.StableRandomInt(
                $"office-autonomy-waypoint:{bootstrap.State.WorldSeed}:{member.MemberId}:" +
                $"{member.Autonomy.MicroAction.SequenceIndex}:{member.Autonomy.MicroAction.TargetId}",
                candidates.Length);
            for (var offset = 0; offset < candidates.Length; offset++)
            {
                var candidate = candidates[(start + offset) % candidates.Length];
                if (!reserved.Contains(candidate)) return candidate;
            }

            return candidates[start];
        }

        private static bool SeatCanBeClaimedBy(OfficeSeatView seat, string memberId)
        {
            if (seat == null) return false;
            if (seat.State == OfficeSeatMeaningState.Reserved ||
                seat.State == OfficeSeatMeaningState.Occupied)
            {
                return false;
            }

            return string.IsNullOrEmpty(seat.AssignedMemberId) ||
                   string.Equals(seat.AssignedMemberId, memberId, StringComparison.Ordinal);
        }

        private static double FlatDistanceSquared(
            FamilyCompany.Simulation.OfficeSeating.OfficeSeatPosition seat,
            Vector3 origin)
        {
            var deltaX = seat.X - origin.x;
            var deltaZ = seat.Z - origin.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}

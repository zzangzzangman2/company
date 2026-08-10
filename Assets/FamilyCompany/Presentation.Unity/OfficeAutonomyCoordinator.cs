using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating.UI;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeAutonomyCoordinator : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private OfficeWorkerAgent[] agents = Array.Empty<OfficeWorkerAgent>();
        [SerializeField] private OfficeWaypoint[] waypoints = Array.Empty<OfficeWaypoint>();
        [SerializeField] private OfficeSeatRegistry seatRegistry;
        [SerializeField] private OfficeSeatPlacementPanel seatPlacementPanel;
        [SerializeField] private float refreshIntervalSeconds = 0.35f;
        private float _refreshRemaining;
        private bool _initialized;
        private bool _seatingInitialized;
        private OfficeSeatingState _seatingState;
        private OfficePlayerSeatingPresenter _playerSeatingPresenter;

        public OfficeSeatingState SeatingState => _seatingState;
        public bool IsSeatingRuntimeReady => _seatingInitialized && _seatingState != null;

        public void Configure(
            PrototypeBootstrap newBootstrap,
            OfficeWorkerAgent[] newAgents,
            OfficeWaypoint[] newWaypoints)
        {
            ResetSeatingRuntimeBindings();
            bootstrap = newBootstrap;
            agents = newAgents ?? Array.Empty<OfficeWorkerAgent>();
            waypoints = newWaypoints ?? Array.Empty<OfficeWaypoint>();
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
            InitializeSeatingRuntime();
            _initialized = true;
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (bootstrap == null || bootstrap.State == null) return;
            if (!_initialized) _initialized = true;
            AutonomousOfficeSimulation.EnsureIntents(
                bootstrap.State.WorldSeed,
                bootstrap.State.Family,
                bootstrap.State.Time.ElapsedMinutes);
            InitializeSeatingRuntime();

            var reserved = new HashSet<OfficeWaypoint>(
                agents.Where(item => item != null && item.isActiveAndEnabled &&
                                             item.HasAssignedTask && item.TargetWaypoint != null)
                    .Select(item => item.TargetWaypoint));
            foreach (var agent in agents
                         .Where(item => item != null && item.isActiveAndEnabled)
                         .OrderBy(item => item.AgentId, StringComparer.Ordinal))
            {
                agent.SetOfficeSeatingRuntimeEnabled(IsSeatingRuntimeReady && agent.HasOfficeSeatingAnimation);
                var member = bootstrap.State.Family.Members.FirstOrDefault(item => item.MemberId == agent.AgentId);
                if (member == null)
                {
                    agent.ClearAutonomousDestination();
                    agent.ClearSeatDestination();
                    continue;
                }

                var candidates = ResolveCandidates(member.Autonomy.TargetLocation);
                OfficeWaypoint autonomyWaypoint = null;
                if (candidates.Length > 0)
                {
                    autonomyWaypoint = PickWaypoint(member, candidates, reserved);
                }

                var status =
                    $"{member.Autonomy.ActionLabel} · {member.Autonomy.MoodLabel(member.Energy, member.Stress)} · " +
                    $"체{member.Energy}/스{member.Stress}";
                var autonomyIntentId = autonomyWaypoint == null
                    ? string.Empty
                    : $"{(int)member.Autonomy.CurrentAction}:{member.Autonomy.ActionStartedMinute}:{autonomyWaypoint.WaypointId}";

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
                         member.Autonomy.TargetLocation == OfficeSemanticLocation.Desk &&
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
                if (member.Autonomy.TargetLocation != OfficeSemanticLocation.Lounge &&
                    member.Autonomy.TargetLocation != OfficeSemanticLocation.MeetingRoom)
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
            if (_seatingInitialized) return;
            if (seatRegistry == null)
                seatRegistry = FindFirstObjectByType<OfficeSeatRegistry>();
            if (seatRegistry == null) return;

            seatRegistry.Rebuild();
            if (seatRegistry.SeatCount == 0) return;
            if (_seatingState == null)
            {
                var definitions = seatRegistry.Definitions
                    .Select(item => new FamilyCompany.Simulation.OfficeSeating.OfficeSeatDefinition(
                        item.SeatId,
                        new FamilyCompany.Simulation.OfficeSeating.OfficeSeatPosition(
                            item.SitPosition.X,
                            item.SitPosition.Z)))
                    .ToArray();
                _seatingState = new OfficeSeatingState(definitions);
            }
            else
            {
                ValidateSeatingStateMatchesRegistry();
            }

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
            _seatingInitialized = true;
        }

        private void ValidateSeatingStateMatchesRegistry()
        {
            if (_seatingState.SeatCount != seatRegistry.SeatCount)
                throw new InvalidOperationException("Office seating state and authored registry have different seat counts.");
            foreach (var definition in seatRegistry.Definitions)
            {
                if (!_seatingState.TryGetSeat(definition.SeatId, out _))
                {
                    throw new InvalidOperationException(
                        $"Office seating state is missing authored seat '{definition.SeatId}'.");
                }
            }
        }

        private void ResetSeatingRuntimeBindings()
        {
            if (_playerSeatingPresenter != null)
                _playerSeatingPresenter.ResetOfficeSeatingRuntime();
            _playerSeatingPresenter = null;
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
                    !agent.SetSeatDestination(intentId, authoring, navigationWaypoint, claim, status))
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
                case OfficeSemanticLocation.Exit:
                    activity = OfficeActivity.Outside;
                    break;
            }

            return waypoints.Where(item => item != null && item.Activity == activity).ToArray();
        }

        private OfficeWaypoint PickWaypoint(
            FamilyMemberState member,
            OfficeWaypoint[] candidates,
            HashSet<OfficeWaypoint> reserved)
        {
            var start = StableRandom.StableRandomInt(
                $"office-autonomy-waypoint:{bootstrap.State.WorldSeed}:{member.MemberId}:" +
                $"{member.Autonomy.ActionStartedMinute}:{(int)member.Autonomy.TargetLocation}",
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

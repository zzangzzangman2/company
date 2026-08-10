using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeAutonomyCoordinator : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private OfficeWorkerAgent[] agents = Array.Empty<OfficeWorkerAgent>();
        [SerializeField] private OfficeWaypoint[] waypoints = Array.Empty<OfficeWaypoint>();
        [SerializeField] private float refreshIntervalSeconds = 0.35f;
        private float _refreshRemaining;
        private bool _initialized;

        public void Configure(
            PrototypeBootstrap newBootstrap,
            OfficeWorkerAgent[] newAgents,
            OfficeWaypoint[] newWaypoints)
        {
            bootstrap = newBootstrap;
            agents = newAgents ?? Array.Empty<OfficeWorkerAgent>();
            waypoints = newWaypoints ?? Array.Empty<OfficeWaypoint>();
            _initialized = false;
        }

        public void InitializeNow()
        {
            if (bootstrap == null || bootstrap.State == null) return;
            AutonomousOfficeSimulation.EnsureIntents(
                bootstrap.State.WorldSeed,
                bootstrap.State.Family,
                bootstrap.State.Time.ElapsedMinutes);
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

            var reserved = new HashSet<OfficeWaypoint>(
                agents.Where(item => item != null && item.HasAssignedTask && item.TargetWaypoint != null)
                    .Select(item => item.TargetWaypoint));
            foreach (var agent in agents.Where(item => item != null).OrderBy(item => item.AgentId, StringComparer.Ordinal))
            {
                var member = bootstrap.State.Family.Members.FirstOrDefault(item => item.MemberId == agent.AgentId);
                if (member == null)
                {
                    agent.ClearAutonomousDestination();
                    continue;
                }

                var candidates = ResolveCandidates(member.Autonomy.TargetLocation);
                if (candidates.Length == 0)
                {
                    agent.ClearAutonomousDestination();
                    continue;
                }

                var waypoint = PickWaypoint(member, candidates, reserved);
                var status = $"{member.Autonomy.ActionLabel} · {member.Autonomy.MoodLabel(member.Energy, member.Stress)} · 체{member.Energy}/스{member.Stress}";
                var intentId = $"{(int)member.Autonomy.CurrentAction}:{member.Autonomy.ActionStartedMinute}:{waypoint.WaypointId}";
                agent.SetAutonomousDestination(intentId, waypoint, status);
                if (member.Autonomy.TargetLocation != OfficeSemanticLocation.Lounge &&
                    member.Autonomy.TargetLocation != OfficeSemanticLocation.MeetingRoom)
                {
                    reserved.Add(waypoint);
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
                $"office-autonomy-waypoint:{bootstrap.State.WorldSeed}:{member.MemberId}:{member.Autonomy.ActionStartedMinute}:{(int)member.Autonomy.TargetLocation}",
                candidates.Length);
            for (var offset = 0; offset < candidates.Length; offset++)
            {
                var candidate = candidates[(start + offset) % candidates.Length];
                if (!reserved.Contains(candidate)) return candidate;
            }

            return candidates[start];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public enum OfficeAssignmentFailure
    {
        None = 0,
        StateUnavailable = 1,
        ContractNotActive = 2,
        MemberUnavailable = 3,
        AgentNotFound = 4,
        AgentBusy = 5,
        WaypointUnavailable = 6,
        AgentRejected = 7
    }

    public sealed class OfficeContractTaskCoordinator : MonoBehaviour
    {
        private sealed class PendingWork
        {
            public PendingWork(string offerId, string memberId, int personHours)
            {
                OfferId = offerId;
                MemberId = memberId;
                PersonHours = personHours;
            }

            public string OfferId { get; }
            public string MemberId { get; }
            public int PersonHours { get; }
        }

        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private OfficeWorkerAgent[] agents = Array.Empty<OfficeWorkerAgent>();
        [SerializeField] private OfficeWaypoint[] waypoints = Array.Empty<OfficeWaypoint>();
        private IOfficeRuntimeAgent[] _runtimeAgents = Array.Empty<IOfficeRuntimeAgent>();
        [SerializeField] private float secondsPerPersonHour = 0.25f;
        private readonly Dictionary<string, PendingWork> _pending = new Dictionary<string, PendingWork>(StringComparer.Ordinal);
        private int _taskSequence;
        private bool _initialized;

        public int PendingCount => _pending.Count;
        public int CompletedTaskCount { get; private set; }
        public string LastCompletedOfferId { get; private set; } = string.Empty;
        public ContractWorkResult LastWorkResult { get; private set; }
        public OfficeAssignmentFailure LastAssignmentFailure { get; private set; }
        public string LastAssignmentFailureLabel { get; private set; } = string.Empty;

        public void Configure(
            PrototypeBootstrap newBootstrap,
            OfficeWorkerAgent[] newAgents,
            OfficeWaypoint[] newWaypoints,
            float workSecondsPerPersonHour = 0.25f)
        {
            Unsubscribe();
            bootstrap = newBootstrap;
            agents = newAgents ?? Array.Empty<OfficeWorkerAgent>();
            waypoints = newWaypoints ?? Array.Empty<OfficeWaypoint>();
            _runtimeAgents = Array.Empty<IOfficeRuntimeAgent>();
            secondsPerPersonHour = Mathf.Max(0.01f, workSecondsPerPersonHour);
            _initialized = false;
        }

        public void ConfigureRuntime(
            PrototypeBootstrap newBootstrap,
            IOfficeRuntimeAgent[] newAgents,
            float workSecondsPerPersonHour = 0.25f)
        {
            Unsubscribe();
            bootstrap = newBootstrap;
            agents = Array.Empty<OfficeWorkerAgent>();
            waypoints = Array.Empty<OfficeWaypoint>();
            _runtimeAgents = newAgents ?? Array.Empty<IOfficeRuntimeAgent>();
            secondsPerPersonHour = Mathf.Max(0.01f, workSecondsPerPersonHour);
            _initialized = false;
        }

        public void InitializeNow()
        {
            if (_initialized) return;
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.AssignedTaskCompleted -= OnAssignedTaskCompleted;
                agent.AssignedTaskCompleted += OnAssignedTaskCompleted;
            }
            foreach (var agent in _runtimeAgents.Where(item => item != null))
            {
                agent.AssignedTaskCompleted -= OnRuntimeAssignedTaskCompleted;
                agent.AssignedTaskCompleted += OnRuntimeAssignedTaskCompleted;
            }

            _initialized = true;
        }

        public bool AssignContractWork(string offerId, string memberId, int personHours)
        {
            if (string.IsNullOrWhiteSpace(offerId)) throw new ArgumentException("Offer ID is required.", nameof(offerId));
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (personHours <= 0) throw new ArgumentOutOfRangeException(nameof(personHours));
            InitializeNow();
            if (bootstrap == null || bootstrap.State == null)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.StateUnavailable,
                    "게임 상태를 준비하지 못했습니다.");
            }

            var contract = bootstrap.State.Contracts.Get(offerId);
            if (contract.Status != SubcontractStatus.Active)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.ContractNotActive,
                    "진행 중인 계약이 아닙니다.");
            }

            var member = bootstrap.State.Family.Get(memberId);
            var schedule = FamilyScheduleRules.Resolve(member.Role, bootstrap.State.Time.Now);
            if (!schedule.CanPerformCompanyWork)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.MemberUnavailable,
                    $"현재 {schedule.Label}이라 회사 업무를 할 수 없습니다.");
            }

            if (_runtimeAgents.Length > 0)
                return AssignRuntimeContractWork(offerId, memberId, personHours, contract);

            var agent = agents.FirstOrDefault(item => item != null && item.AgentId == memberId);
            if (agent == null)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.AgentNotFound,
                    "해당 가족은 직접 조작 대상이거나 사무실 이동 에이전트가 없습니다.");
            }

            if (agent.HasAssignedTask)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.AgentBusy,
                    "해당 가족은 이미 계약 업무 중입니다.");
            }

            var activity = ResolveActivity(contract);
            var candidates = ResolveWaypointCandidates(agent, activity);
            if (candidates.Length == 0)
            {
                return FailAssignment(
                    OfficeAssignmentFailure.WaypointUnavailable,
                    "사용할 업무 지점이 없습니다.");
            }

            var waypointIndex = StableRandom.StableRandomInt(
                $"office-contract:{bootstrap.State.WorldSeed}:{offerId}:{memberId}:{_taskSequence}",
                candidates.Length);
            var taskId = $"office-contract:{offerId}:{memberId}:{_taskSequence:D6}";
            _taskSequence++;
            var appliedHours = Math.Min(personHours, contract.RemainingPersonHours);
            _pending.Add(taskId, new PendingWork(offerId, memberId, appliedHours));
            if (agent.AssignOfficeTask(taskId, candidates[waypointIndex], appliedHours * secondsPerPersonHour))
            {
                ClearAssignmentFailure();
                return true;
            }

            _pending.Remove(taskId);
            return FailAssignment(
                OfficeAssignmentFailure.AgentRejected,
                "이동 에이전트가 업무 배정을 받지 못했습니다.");
        }

        public void ResetAssignments()
        {
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.CancelAssignedTask();
            }
            foreach (var agent in _runtimeAgents.Where(item => item != null))
                agent.CancelAssignedTask();

            _pending.Clear();
            _taskSequence = 0;
            CompletedTaskCount = 0;
            LastWorkResult = null;
            LastCompletedOfferId = string.Empty;
            ClearAssignmentFailure();
        }

        private void Awake()
        {
            InitializeNow();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnAssignedTaskCompleted(OfficeWorkerAgent agent, string taskId)
        {
            if (!_pending.TryGetValue(taskId, out var pending)) return;
            _pending.Remove(taskId);
            LastWorkResult = bootstrap.State.Contracts.RecordWork(
                pending.OfferId,
                pending.MemberId,
                pending.PersonHours,
                bootstrap.State.Time.ElapsedMinutes,
                bootstrap.State.Family,
                bootstrap.State.Company);
            LastCompletedOfferId = pending.OfferId;
            CompletedTaskCount++;
        }

        private void OnRuntimeAssignedTaskCompleted(IOfficeRuntimeAgent agent, string taskId)
        {
            CompletePendingWork(taskId);
        }

        private bool AssignRuntimeContractWork(
            string offerId,
            string memberId,
            int personHours,
            SubcontractState contract)
        {
            IOfficeRuntimeAgent agent = _runtimeAgents.FirstOrDefault(item =>
                item != null && string.Equals(item.AgentId, memberId, StringComparison.Ordinal));
            if (agent == null || agent.IsPlayerControlled)
                return FailAssignment(
                    OfficeAssignmentFailure.AgentNotFound,
                    "해당 가족은 직접 조작 대상이거나 Starter Office Actor가 없습니다.");
            if (agent.HasAssignedTask)
                return FailAssignment(OfficeAssignmentFailure.AgentBusy, "해당 가족은 이미 계약 업무 중입니다.");

            OfficeActivity activity = ResolveActivity(contract);
            string taskId = $"office-contract:{offerId}:{memberId}:{_taskSequence:D6}";
            _taskSequence++;
            int appliedHours = Math.Min(personHours, contract.RemainingPersonHours);
            _pending.Add(taskId, new PendingWork(offerId, memberId, appliedHours));
            // Runtime contract production is governed by the authoritative GameTime.  Real frames
            // only animate travel and work; one person-hour always consumes sixty game minutes.
            if (agent.AssignOfficeTask(taskId, activity, appliedHours * 60f))
            {
                ClearAssignmentFailure();
                return true;
            }
            _pending.Remove(taskId);
            return FailAssignment(
                OfficeAssignmentFailure.AgentRejected,
                "Starter Office Actor가 업무 배정을 받지 못했습니다.");
        }

        private void CompletePendingWork(string taskId)
        {
            if (!_pending.TryGetValue(taskId, out var pending)) return;
            _pending.Remove(taskId);
            LastWorkResult = bootstrap.State.Contracts.RecordWork(
                pending.OfferId,
                pending.MemberId,
                pending.PersonHours,
                bootstrap.State.Time.ElapsedMinutes,
                bootstrap.State.Family,
                bootstrap.State.Company);
            LastCompletedOfferId = pending.OfferId;
            CompletedTaskCount++;
        }

        private void Unsubscribe()
        {
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.AssignedTaskCompleted -= OnAssignedTaskCompleted;
            }
            foreach (var agent in _runtimeAgents.Where(item => item != null))
                agent.AssignedTaskCompleted -= OnRuntimeAssignedTaskCompleted;

            _initialized = false;
        }

        private OfficeWaypoint[] ResolveWaypointCandidates(OfficeWorkerAgent assignedAgent, OfficeActivity activity)
        {
            var candidates = waypoints
                .Where(item => item != null && item.Activity == activity)
                .OrderBy(item => item.WaypointId, StringComparer.Ordinal)
                .ToArray();
            if (activity != OfficeActivity.Work && activity != OfficeActivity.Printing)
            {
                return candidates;
            }

            var occupiedTargets = new HashSet<OfficeWaypoint>(
                agents
                    .Where(item => item != null && item != assignedAgent && item.TargetWaypoint != null)
                    .Select(item => item.TargetWaypoint));
            var unoccupiedCandidates = candidates
                .Where(item => !occupiedTargets.Contains(item))
                .ToArray();
            return unoccupiedCandidates.Length > 0 ? unoccupiedCandidates : candidates;
        }

        private bool FailAssignment(OfficeAssignmentFailure failure, string label)
        {
            LastAssignmentFailure = failure;
            LastAssignmentFailureLabel = label ?? string.Empty;
            return false;
        }

        private void ClearAssignmentFailure()
        {
            LastAssignmentFailure = OfficeAssignmentFailure.None;
            LastAssignmentFailureLabel = string.Empty;
        }

        private static OfficeActivity ResolveActivity(SubcontractState contract)
        {
            if (contract.CompletedPersonHours == 0 && contract.Offer.RequiredWorkers > 1)
            {
                return OfficeActivity.Meeting;
            }

            if (contract.RemainingPersonHours <= 4)
            {
                return OfficeActivity.Printing;
            }

            return OfficeActivity.Work;
        }
    }
}

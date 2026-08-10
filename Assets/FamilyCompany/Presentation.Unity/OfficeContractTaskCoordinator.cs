using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
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
        [SerializeField] private float secondsPerPersonHour = 0.25f;
        private readonly Dictionary<string, PendingWork> _pending = new Dictionary<string, PendingWork>(StringComparer.Ordinal);
        private int _taskSequence;
        private bool _initialized;

        public int PendingCount => _pending.Count;
        public int CompletedTaskCount { get; private set; }
        public string LastCompletedOfferId { get; private set; } = string.Empty;
        public ContractWorkResult LastWorkResult { get; private set; }

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

            _initialized = true;
        }

        public bool AssignContractWork(string offerId, string memberId, int personHours)
        {
            if (string.IsNullOrWhiteSpace(offerId)) throw new ArgumentException("Offer ID is required.", nameof(offerId));
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (personHours <= 0) throw new ArgumentOutOfRangeException(nameof(personHours));
            InitializeNow();
            if (bootstrap == null || bootstrap.State == null) return false;

            var contract = bootstrap.State.Contracts.Get(offerId);
            if (contract.Status != SubcontractStatus.Active) return false;
            var agent = agents.FirstOrDefault(item => item != null && item.AgentId == memberId);
            if (agent == null || agent.HasAssignedTask) return false;

            var activity = ResolveActivity(contract);
            var candidates = waypoints.Where(item => item != null && item.Activity == activity).ToArray();
            if (candidates.Length == 0) return false;
            var waypointIndex = StableRandom.StableRandomInt(
                $"office-contract:{bootstrap.State.WorldSeed}:{offerId}:{memberId}:{_taskSequence}",
                candidates.Length);
            var taskId = $"office-contract:{offerId}:{memberId}:{_taskSequence:D6}";
            _taskSequence++;
            var appliedHours = Math.Min(personHours, contract.RemainingPersonHours);
            _pending.Add(taskId, new PendingWork(offerId, memberId, appliedHours));
            if (agent.AssignOfficeTask(taskId, candidates[waypointIndex], appliedHours * secondsPerPersonHour))
            {
                return true;
            }

            _pending.Remove(taskId);
            return false;
        }

        public void ResetAssignments()
        {
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.CancelAssignedTask();
            }

            _pending.Clear();
            _taskSequence = 0;
            CompletedTaskCount = 0;
            LastWorkResult = null;
            LastCompletedOfferId = string.Empty;
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

        private void Unsubscribe()
        {
            foreach (var agent in agents.Where(item => item != null))
            {
                agent.AssignedTaskCompleted -= OnAssignedTaskCompleted;
            }

            _initialized = false;
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

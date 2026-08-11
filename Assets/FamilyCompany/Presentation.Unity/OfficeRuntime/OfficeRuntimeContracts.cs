using System;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public readonly struct OfficeRuntimeDestination
    {
        public OfficeRuntimeDestination(
            string destinationId,
            OfficeSemanticLocation semanticLocation,
            OfficeActivity activity,
            OfficeGridCoordinate cell,
            string seatId = "")
        {
            DestinationId = string.IsNullOrWhiteSpace(destinationId)
                ? throw new ArgumentException("Destination ID is required.", nameof(destinationId))
                : destinationId.Trim();
            SemanticLocation = semanticLocation;
            Activity = activity;
            Cell = cell;
            SeatId = (seatId ?? string.Empty).Trim();
        }

        public string DestinationId { get; }
        public OfficeSemanticLocation SemanticLocation { get; }
        public OfficeActivity Activity { get; }
        public OfficeGridCoordinate Cell { get; }
        public string SeatId { get; }
        public bool RequiresSeat => SeatId.Length > 0;
    }

    public interface IOfficeRuntimeAgent : IOfficeObservationStatusSource
    {
        event Action<IOfficeRuntimeAgent, string> AssignedTaskCompleted;

        string AgentId { get; }
        bool IsPlayerControlled { get; }
        bool HasAssignedTask { get; }
        string AssignedTaskId { get; }
        bool IsSeated { get; }
        bool IsBusy { get; }
        OfficeActivity CurrentActivity { get; }
        Vector2 Position { get; }

        bool AssignOfficeTask(string taskId, OfficeActivity activity, float workSeconds);
        void CancelAssignedTask();
        void SetAutonomousDestination(
            string intentId,
            OfficeSemanticLocation location,
            string statusLabel);
        void ClearAutonomousDestination();
        void ResetRuntimeState();
    }
}

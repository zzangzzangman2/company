using System;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeRuntimeInteractionPhase
    {
        None = 0,
        Reserving = 1,
        Navigating = 2,
        Aligning = 3,
        Performing = 4,
        Finishing = 5
    }

    public enum OfficeRuntimeInteractionTermination
    {
        None = 0,
        Completed = 1,
        Aborted = 2
    }

    public enum OfficeRuntimeInteractionEndReason
    {
        None = 0,
        IntentAdvanced = 1,
        SupersededBeforeArrival = 2,
        LayoutChanged = 3,
        Cleared = 4,
        ContractOverride = 5,
        PathUnavailable = 6,
        ArrivalRevalidationFailed = 7,
        SeatUnavailable = 8,
        RuntimeReset = 9,
        Disabled = 10,
        Destroyed = 11
    }

    public readonly struct OfficeRuntimeDestination
    {
        public OfficeRuntimeDestination(
            string destinationId,
            OfficeSemanticLocation semanticLocation,
            OfficeActivity activity,
            OfficeGridCoordinate cell,
            string seatId = "",
            string interactionOfferId = "",
            string furnitureId = "")
        {
            DestinationId = string.IsNullOrWhiteSpace(destinationId)
                ? throw new ArgumentException("Destination ID is required.", nameof(destinationId))
                : destinationId.Trim();
            SemanticLocation = semanticLocation;
            Activity = activity;
            Cell = cell;
            SeatId = (seatId ?? string.Empty).Trim();
            InteractionOfferId = (interactionOfferId ?? string.Empty).Trim();
            FurnitureId = (furnitureId ?? string.Empty).Trim();
        }

        public string DestinationId { get; }
        public OfficeSemanticLocation SemanticLocation { get; }
        public OfficeActivity Activity { get; }
        public OfficeGridCoordinate Cell { get; }
        public string SeatId { get; }
        public string InteractionOfferId { get; }
        public string FurnitureId { get; }
        public bool RequiresSeat => SeatId.Length > 0;
    }

    /// <summary>
    /// Transient hand-off used only while a semantic layout rebuild replaces presentation objects.
    /// It is deliberately not persisted: GameState remains the only save authority.
    /// </summary>
    public readonly struct OfficeRuntimeAgentLayoutSnapshot
    {
        public OfficeRuntimeAgentLayoutSnapshot(
            string memberId,
            OfficeGridCoordinate cell,
            bool wasOutside,
            int direction,
            string assignedTaskId,
            OfficeActivity assignedActivity,
            float assignedWorkRemainingMinutes,
            string autonomyIntentId,
            OfficeSemanticLocation autonomyLocation,
            string autonomyInteractionId,
            string autonomyStatus)
        {
            MemberId = (memberId ?? string.Empty).Trim();
            Cell = cell;
            WasOutside = wasOutside;
            Direction = direction;
            AssignedTaskId = (assignedTaskId ?? string.Empty).Trim();
            AssignedActivity = assignedActivity;
            AssignedWorkRemainingMinutes = Math.Max(0f, assignedWorkRemainingMinutes);
            AutonomyIntentId = (autonomyIntentId ?? string.Empty).Trim();
            AutonomyLocation = autonomyLocation;
            AutonomyInteractionId = (autonomyInteractionId ?? string.Empty).Trim();
            AutonomyStatus = (autonomyStatus ?? string.Empty).Trim();
        }

        public string MemberId { get; }
        public OfficeGridCoordinate Cell { get; }
        public bool WasOutside { get; }
        public int Direction { get; }
        public string AssignedTaskId { get; }
        public OfficeActivity AssignedActivity { get; }
        public float AssignedWorkRemainingMinutes { get; }
        public string AutonomyIntentId { get; }
        public OfficeSemanticLocation AutonomyLocation { get; }
        public string AutonomyInteractionId { get; }
        public string AutonomyStatus { get; }
        public bool HasAssignedTask => AssignedTaskId.Length > 0 && AssignedWorkRemainingMinutes > 0f;
        public bool HasAutonomyRequest => AutonomyIntentId.Length > 0;
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
        OfficeRuntimeInteractionPhase InteractionPhase { get; }
        string ActiveInteractionId { get; }
        string ActiveInteractionOfferId { get; }
        string ActiveInteractionFurnitureId { get; }
        int InteractionCompletedCount { get; }
        int InteractionAbortedCount { get; }
        OfficeRuntimeInteractionEndReason LastInteractionEndReason { get; }

        bool AssignOfficeTask(string taskId, OfficeActivity activity, float workMinutes);
        void CancelAssignedTask();
        void SetAutonomousDestination(
            string intentId,
            OfficeSemanticLocation location,
            string interactionId,
            string statusLabel);
        void ClearAutonomousDestination();
        void ResetRuntimeState();
    }
}

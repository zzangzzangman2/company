using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Locks the Agent-side ownership seams that are easy to regress while the pure lifecycle and
    /// reservation validators exercise capacity, deterministic selection, and terminal cleanup.
    /// </summary>
    public static class OfficeRuntimeAgentInteractionValidation
    {
        private const string AgentPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeAgent.cs";
        private const string WorkstationsPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeWorkstationService.cs";
        private const string AnimatorPath =
            "Assets/FamilyCompany/Presentation.Unity/DirectionalSpriteAnimator.cs";
        private const string StarterBootstrapPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/StarterOfficeRuntimeBootstrap.cs";

        [MenuItem("Family Company/Validate Office Runtime Agent Interactions")]
        public static void Run()
        {
            string agent = File.ReadAllText(Path.GetFullPath(AgentPath));
            string workstations = File.ReadAllText(Path.GetFullPath(WorkstationsPath));
            string animator = File.ReadAllText(Path.GetFullPath(AnimatorPath));
            string starterBootstrap = File.ReadAllText(Path.GetFullPath(StarterBootstrapPath));

            string autonomy = Section(
                agent,
                "public void SetAutonomousDestination(",
                "public void ClearAutonomousDestination()");
            Require(autonomy.Contains("if (!sameRequest && HasAssignedTask)"),
                "Contract-owned actors must cache a changed autonomy request without claiming it.");
            Require(autonomy.Contains(
                    "if (!HasAssignedTask && !_attendanceArrivalActive) TryStartAutonomyRequest();"),
                "Only an available actor outside attendance arrival may begin its cached autonomy request.");
            Require(autonomy.Contains("OfficeRuntimeInteractionTermination.Completed"),
                "Advancing an arrived intent must complete its presentation interaction.");
            Require(autonomy.Contains("OfficeRuntimeInteractionTermination.Aborted"),
                "Superseding a travelling intent must abort its presentation interaction.");

            string start = Section(
                agent,
                "private bool TryStartAutonomyRequest()",
                "private bool BeginDestination(OfficeRuntimeDestination destination)");
            Require(start.Contains("TryBeginInteraction("),
                "Interaction offer selection and reservation must be one runtime operation.");
            Require(start.IndexOf("TryBeginInteraction(", StringComparison.Ordinal) <
                    start.IndexOf("BeginDestination(destination)", StringComparison.Ordinal),
                "Furniture capacity must be claimed before the first navigation frame.");
            Require(start.Contains("AbortInteractionAttempt(OfficeRuntimeInteractionEndReason.PathUnavailable)"),
                "A failed initial path must release its interaction claim.");

            string arrival = Section(agent, "private void CompleteNavigation()", "private void TickSeating");
            Require(arrival.IndexOf("TryValidateArrival", StringComparison.Ordinal) <
                    arrival.IndexOf("_world.NotifyArrival();", StringComparison.Ordinal),
                "Live furniture and approach-cell validation must precede a successful arrival.");
            Require(arrival.Contains("OfficeRuntimeInteractionPhase.Performing"),
                "A valid standing interaction must enter Performing.");
            Require(arrival.Contains("TryResolveStandingInteractionFacing"),
                "A standing interaction must resolve a live facing toward its furniture.");

            string standing = Section(
                agent,
                "private void TickArrivedWork(float deltaTime)",
                "private void AdvanceAssignedWork()");
            Require(standing.Contains("TickStandingAlignment(deltaTime)"),
                "Standing work must wait until the furniture-facing pivot completes.");
            Require(animator.Contains("AccumulateStandingFacingRequest") &&
                    animator.Contains("Vector2.zero, deltaTime, false"),
                "Facility alignment must reuse the planted stationary pivot instead of moving the root.");

            string resume = Section(agent, "private void ResumeAutonomy()", "private void TickDirectPlayerMovement");
            Require(resume.Contains("TryStartAutonomyRequest();") &&
                    !resume.Contains("BeginDestination(_autonomyDestination.Value)"),
                "Contract completion must resolve the latest intent against the live layout.");

            string ending = Section(agent, "private void EndInteraction(", "private void ClearInteractionExecutionState");
            Require(ending.Contains("_interactionHandle.TryComplete") &&
                    ending.Contains("_interactionHandle.TryAbort") &&
                    ending.Contains("_interactionHandle.TryRelease"),
                "One idempotent terminal path must own every interaction release.");
            Require(agent.Contains("OfficeRuntimeInteractionEndReason.Disabled") &&
                    agent.Contains("OfficeRuntimeInteractionEndReason.Destroyed") &&
                    agent.Contains("OfficeRuntimeInteractionEndReason.RuntimeReset") &&
                    agent.Contains("OfficeRuntimeInteractionEndReason.ContractOverride"),
                "Disable, destroy, reset, and contract override must all terminate active claims.");

            Require(workstations.Contains("string requestedSeatId,") &&
                    workstations.Contains("_seats.TryGetValue(requestedSeatId.Trim()"),
                "Assigned-seat interactions need an exact-seat reservation overload.");
            Require(workstations.Contains("destination.FurnitureId") &&
                    workstations.Contains("furnitureWorld.x - actorWorld.x"),
                "Standing facing must target the selected physical furniture instance.");

            string seating = Section(agent, "private void TickSeating(float deltaTime)", "private void TickArrivedWork");
            Require(seating.IndexOf("AccumulateStandingFacingRequest(_seatDirection", StringComparison.Ordinal) <
                    seating.IndexOf("_seatClaim.TryOccupy", StringComparison.Ordinal),
                "An actor must finish a planted seat-facing pivot before occupying and sitting.");
            int leavingCase = seating.IndexOf(
                "case OfficeRuntimeAgentPhase.LeavingSeat:", StringComparison.Ordinal);
            int leavingRelease = leavingCase < 0
                ? -1
                : seating.IndexOf(
                    "ReleaseSeatImmediately();",
                    leavingCase,
                    StringComparison.Ordinal);
            Require(agent.Contains("Phase == OfficeRuntimeAgentPhase.LeavingSeat") &&
                    leavingRelease > leavingCase,
                "Chair occlusion and the seat claim must remain active throughout the exit step.");

            Require(starterBootstrap.Contains("CacheWorkActionFrameSets();") &&
                    starterBootstrap.Contains("AddComponent<OfficeSeatedWorkMicroActionAdapter>()") &&
                    starterBootstrap.Contains("ConfigureOfficeWorkAnimationHook(adapter)"),
                "Starter runtime actors must retain and reconnect their seated work-action frame sets.");
            string rebuild = Section(
                starterBootstrap,
                "private IEnumerator RebuildForLayoutChange()",
                "private void CaptureLayoutSnapshots()");
            Require(rebuild.IndexOf("CaptureLayoutSnapshots();", StringComparison.Ordinal) <
                    rebuild.IndexOf("Destroy(_generated)", StringComparison.Ordinal),
                "A layout rebuild must capture actors before destroying the old runtime.");
            Require(starterBootstrap.Contains("actor.RestoreLayoutSnapshot(snapshot)") &&
                    starterBootstrap.Contains("_layoutSnapshots.Clear();"),
                "Rebuilt actors must restore transient location, contract work, and autonomy state once.");
            Require(agent.Contains("snapshot.AssignedWorkRemainingMinutes") &&
                    agent.Contains("snapshot.AutonomyIntentId") &&
                    agent.Contains("RestoreStandingFacing(snapshot.Direction)"),
                "Layout snapshots must preserve facing, in-flight contract work, and the latest autonomy intent.");
            string navigation = Section(
                agent,
                "private void TickNavigation(float deltaTime)",
                "private bool TryTickGridYield(");
            Require(navigation.Contains("Stay on the semantic segment until its exact cell-center arrival") &&
                    navigation.Contains("_desiredVelocity = desiredDirection *") &&
                    navigation.Contains("presentationSemanticDirection * targetVelocity.magnitude"),
                "Root motion must stay on the validated semantic segment while presentation keeps its facing vector.");

            string assignedWork = Section(
                agent,
                "private void AdvanceAssignedWork()",
                "private void RequestStopAndStand()");
            Require(assignedWork.Contains("_bootstrap.State.Time.ElapsedMinutes") &&
                    !assignedWork.Contains("deltaTime"),
                "Contract production must consume authoritative game minutes, never rendered seconds.");

            Debug.Log("OFFICE_RUNTIME_AGENT_INTERACTION_VALIDATION: PASS");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string Section(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = start < 0
                ? -1
                : source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException(
                    $"Unable to resolve source section '{startMarker}' -> '{endMarker}'.");
            return source.Substring(start, end - start);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

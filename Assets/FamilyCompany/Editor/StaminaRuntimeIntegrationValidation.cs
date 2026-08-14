#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.Stamina;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Stamina;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [InitializeOnLoad]
    public static class StaminaRuntimeIntegrationValidation
    {
        private const string ActiveKey = "FamilyCompany.StaminaRuntimeQa.Active";
        private const string StageKey = "FamilyCompany.StaminaRuntimeQa.Stage";
        private const string PausedTaskKey = "FamilyCompany.StaminaRuntimeQa.PausedTask";
        private const string PausedRemainingKey = "FamilyCompany.StaminaRuntimeQa.PausedRemaining";
        private const string ClockPreparedKey = "FamilyCompany.StaminaRuntimeQa.ClockPrepared";
        private const string RoutineRefreshGateCheckedKey =
            "FamilyCompany.StaminaRuntimeQa.RoutineRefreshGateChecked";
        private const string MemberId = "older_sister";
        private static double _deadline;

        static StaminaRuntimeIntegrationValidation()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Stamina Runtime Integration")]
        public static void StartMenu() => Start(false);
        public static void StartBatch() => Start(true);

        private static void Start(bool batch)
        {
            if (SessionState.GetBool(ActiveKey, false) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stamina runtime QA is already active.");
            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ActiveKey + ".Batch", batch);
            SessionState.SetInt(StageKey, 1);
            SessionState.EraseString(PausedTaskKey);
            SessionState.SetFloat(PausedRemainingKey, 0f);
            SessionState.SetBool(ClockPreparedKey, false);
            SessionState.SetBool(RoutineRefreshGateCheckedKey, false);
            _deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.EnterPlaymode();
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                int stage = SessionState.GetInt(StageKey, 0);
                if (stage == 1 && EditorApplication.isPlaying)
                {
                    PrototypeBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
                    if (bootstrap == null) throw new InvalidOperationException("PrototypeBootstrap missing.");
                    bootstrap.StartNewGameNow(1, false);
                    new GameObject("Stamina Runtime QA Audio Listener")
                        .AddComponent<AudioListener>();
                    ScenePreviewJump.ShowStarterOffice();
                    bootstrap.SetWorldTimeScaleNow(4f);
                    _deadline = EditorApplication.timeSinceStartup + 45d;
                    SessionState.SetInt(StageKey, 2);
                    return;
                }
                if (!EditorApplication.isPlaying)
                {
                    if (stage == 7 && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        bool batch = SessionState.GetBool(ActiveKey + ".Batch", false);
                        Debug.Log("FAMILY_COMPANY_STAMINA_RUNTIME_INTEGRATION: PASS | " +
                                  "bars=4 | capability=placed+reachable+capacity | restroom=fail-closed | " +
                                  "contract=pause/resume | recovery=complete+return | speed=4x");
                        Clear();
                        if (batch) EditorApplication.Exit(0);
                    }
                    return;
                }

                RequireBeforeDeadline();
                switch (stage)
                {
                    case 2:
                        PrepareAndValidateCapabilities();
                        break;
                    case 3:
                        BeginThresholdScenarioWhenSeated();
                        break;
                    case 4:
                        ValidateThresholdDeparture();
                        break;
                    case 5:
                        CompleteRecoveryWhenPerforming();
                        break;
                    case 6:
                        ValidateReturnAndFinish();
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private static void PrepareAndValidateCapabilities()
        {
            if (!TryGetRuntime(out PrototypeBootstrap bootstrap,
                    out StarterOfficeRuntimeBootstrap runtime,
                    out StaminaRecoveryRuntimeCoordinator coordinator)) return;
            if (!SessionState.GetBool(ClockPreparedKey, false))
            {
                bootstrap.AdvanceTimeNow(13);
                bootstrap.enabled = false;
                SessionState.SetBool(ClockPreparedKey, true);
                return;
            }
            OverheadStaminaBarPresenter bars =
                UnityEngine.Object.FindFirstObjectByType<OverheadStaminaBarPresenter>();
            if (bars == null || coordinator == null || bars.BoundBarCount != 4)
            {
                RequireBeforeDeadline();
                return;
            }
            bars.RefreshImmediateForQa();
            foreach (string id in bootstrap.State.Stamina.CharacterIds)
            {
                if (!bars.TryGetDebugSnapshot(id, out OverheadStaminaBarDebugSnapshot snapshot) ||
                    !snapshot.Visible)
                {
                    RequireBeforeDeadline();
                    return;
                }
            }
            OfficeRuntimeAgent actor = runtime.Actors.Single(item => item.AgentId == MemberId);
            OfficeRuntimeAgent father = runtime.Actors.Single(item => item.AgentId == "father");
            if (actor.IsPresentationAway || father.IsPresentationAway ||
                actor.AttendanceSeatArrivalCount == 0 || father.AttendanceSeatArrivalCount == 0 ||
                !actor.IsSeated)
            {
                RequireBeforeDeadline();
                return;
            }

            string artifactDirectory = Path.GetFullPath("Artifacts/StaminaRuntimeQa");
            Directory.CreateDirectory(artifactDirectory);
            CaptureFourFamilyBars(bars, Path.Combine(
                artifactDirectory, "four-family-overhead-bars.png"));

            var adapter = new StaminaRecoveryFurnitureCapabilityAdapter(runtime, bootstrap.State);
            var query = new StaminaRecoveryCapabilityQuery(
                MemberId, "qa-capability", bootstrap.State.Time.ElapsedMinutes);
            StaminaRecoveryCapabilityQueryResult available = adapter.Query(query);
            if (!available.Candidates.Any(item => item.Activity == StaminaRecoveryActivity.Water) ||
                !available.Candidates.Any(item => item.Activity == StaminaRecoveryActivity.Lounge) ||
                available.Candidates.Any(item => item.Activity == StaminaRecoveryActivity.Restroom))
                throw new InvalidOperationException(
                    "Capability mapping must expose water/lounge and fail closed for restroom. " +
                    "candidates=[" + string.Join(",", available.Candidates.Select(item =>
                        item.Activity + ":" + item.InteractionId + ":" + item.RuntimeFurnitureInstanceId)) +
                    "] inventory=[" + string.Join(",", bootstrap.State.OfficeFurnitureInventory.Instances
                        .Where(item => item.PlacementState == OfficeFurniturePlacementState.Placed)
                        .Select(item => item.InstanceId + ":" + item.DefinitionId)) + "]");

            OfficeGridCoordinate fatherCell = runtime.World.Presenter.NearestCell(father.transform.position);
            if (!runtime.World.Workstations.TryBeginInteraction(
                    "water-drink", father.AgentId, "qa-water-capacity", fatherCell,
                    father.ActiveSeatId, father.AgentRadius,
                    out _, out OfficeRuntimeInteractionHandle handle, out OfficeRuntimeInteractionFailure failure))
                throw new InvalidOperationException("Capacity claim setup failed: " + failure);
            StaminaRecoveryCapabilityQueryResult claimed = adapter.Query(query);
            if (claimed.Candidates.Any(item =>
                    string.Equals(item.InteractionId, "water-drink", StringComparison.Ordinal)))
                throw new InvalidOperationException("Claimed capacity remained available.");
            handle.TryAbort(out _);
            handle.TryRelease(out _);

            OfficeFurnitureInstanceState water = bootstrap.State.OfficeFurnitureInventory.Instances
                .Single(item => string.Equals(
                    item.DefinitionId, OfficeGridLayouts.WaterDispenserKind, StringComparison.Ordinal));
            OfficeGridCoordinate origin = water.GridOrigin;
            OfficeFurnitureFacing facing = water.Rotation;
            OfficeFurnitureCommandResult stored = OfficeFurnitureTransactionService.Store(
                bootstrap.State, water.InstanceId);
            if (!stored.Success) throw new InvalidOperationException("Water deletion QA failed: " + stored.Message);
            if (adapter.Query(query).Candidates.Any(item =>
                    string.Equals(item.InteractionId, "water-drink", StringComparison.Ordinal)))
                throw new InvalidOperationException("Deleted water facility remained selectable.");
            OfficeFurnitureCommandResult restored = OfficeFurnitureTransactionService.PlaceStored(
                bootstrap.State, water.InstanceId, origin, facing);
            if (!restored.Success) throw new InvalidOperationException("Water restore QA failed: " + restored.Message);

            if (!actor.AssignOfficeTask("qa-stamina-contract", OfficeActivity.Work, 1200f))
                throw new InvalidOperationException("Could not assign preserved contract-style work.");
            _deadline = EditorApplication.timeSinceStartup + 45d;
            SessionState.SetInt(StageKey, 3);
        }

        private static void BeginThresholdScenarioWhenSeated()
        {
            GetRuntime(out PrototypeBootstrap bootstrap, out StarterOfficeRuntimeBootstrap runtime,
                out StaminaRecoveryRuntimeCoordinator coordinator);
            OfficeRuntimeAgent actor = runtime.Actors.Single(item => item.AgentId == MemberId);
            if (!actor.IsSeated || !actor.HasAssignedTask) return;
            CharacterStaminaSimulation simulation = bootstrap.State.Stamina.GetSimulation(MemberId);
            StaminaActivityKind activity = coordinator.ResolveActivity(MemberId);
            int rate = simulation.Profile.DrainUnitsPerGameMinute(activity);
            if (rate <= 0)
                throw new InvalidOperationException("Assigned work did not resolve to a draining stamina activity.");
            int above = simulation.State.CurrentUnits - simulation.Profile.RecoveryThresholdUnits;
            long toThreshold = (above + (long)rate - 1L) / rate;
            if (toThreshold <= 0) throw new InvalidOperationException("Invalid threshold setup.");
            bootstrap.AdvanceTimeNow(toThreshold - 1L);
            if (!actor.HasAssignedTask || !actor.IsSeated || simulation.HasPendingRuntimeDecision ||
                simulation.IsAtOrBelowRecoveryThreshold)
                throw new InvalidOperationException("Actor departed before the 25% threshold.");
            bootstrap.AdvanceTimeNow(1L);
            _deadline = EditorApplication.timeSinceStartup + 45d;
            SessionState.SetInt(StageKey, 4);
        }

        private static void ValidateThresholdDeparture()
        {
            GetRuntime(out PrototypeBootstrap bootstrap, out StarterOfficeRuntimeBootstrap runtime,
                out StaminaRecoveryRuntimeCoordinator coordinator);
            CharacterStaminaSimulation simulation = bootstrap.State.Stamina.GetSimulation(MemberId);
            OfficeRuntimeAgent actor = runtime.Actors.Single(item => item.AgentId == MemberId);
            if (simulation.HasPendingRuntimeDecision)
            {
                coordinator.ProcessPendingDecisions(
                    bootstrap.State.Stamina,
                    bootstrap.State.Time.ElapsedMinutes);
                simulation = bootstrap.State.Stamina.GetSimulation(MemberId);
            }
            if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested)
            {
                bool hasMemberPendingPause = coordinator.TryGetPausedWorkForQa(
                    MemberId, out _, out _);
                if (!simulation.HasPendingRuntimeDecision &&
                    simulation.State.RecoveryRetryMinute > bootstrap.State.Time.ElapsedMinutes &&
                    (actor.HasActiveInteractionClaim ||
                     (hasMemberPendingPause && actor.Phase == OfficeRuntimeAgentPhase.Idle) ||
                     (!hasMemberPendingPause && actor.IsSeated)))
                    bootstrap.AdvanceTimeNow(
                        simulation.State.RecoveryRetryMinute - bootstrap.State.Time.ElapsedMinutes);
                return;
            }
            if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.Working)
                throw new InvalidOperationException("Threshold did not create recovery lifecycle.");
            if (actor.HasAssignedTask)
                throw new InvalidOperationException("Assigned work was not paused after reservation.");
            if (!coordinator.TryGetPausedWorkForQa(MemberId, out string taskId, out float remaining) ||
                !string.Equals(taskId, "qa-stamina-contract", StringComparison.Ordinal) || remaining <= 0f)
                throw new InvalidOperationException("Paused task identity/remaining work was not preserved.");
            SessionState.SetString(PausedTaskKey, taskId);
            SessionState.SetFloat(PausedRemainingKey, remaining);
            _deadline = EditorApplication.timeSinceStartup + 45d;
            SessionState.SetInt(StageKey, 5);
        }

        private static void CompleteRecoveryWhenPerforming()
        {
            GetRuntime(out PrototypeBootstrap bootstrap, out StarterOfficeRuntimeBootstrap runtime,
                out _);
            OfficeRuntimeAgent actor = runtime.Actors.Single(item => item.AgentId == MemberId);
            if (!SessionState.GetBool(RoutineRefreshGateCheckedKey, false))
            {
                if (!actor.HasActiveInteractionClaim) return;
                string interactionId = actor.ActiveInteractionId;
                string offerId = actor.ActiveInteractionOfferId;
                int aborted = actor.InteractionAbortedCount;
                OfficeAutonomyCoordinator autonomy =
                    UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>();
                if (autonomy == null)
                    throw new InvalidOperationException("OfficeAutonomyCoordinator missing.");
                autonomy.RefreshNow();
                autonomy.RefreshNow();
                autonomy.RefreshNow();
                if (!actor.HasActiveInteractionClaim ||
                    !string.Equals(actor.ActiveInteractionId, interactionId, StringComparison.Ordinal) ||
                    !string.Equals(actor.ActiveInteractionOfferId, offerId, StringComparison.Ordinal) ||
                    actor.InteractionAbortedCount != aborted)
                    throw new InvalidOperationException(
                        "Routine autonomy refresh restarted the sticky stamina recovery path.");
                Debug.Log("STAMINA_RUNTIME_REFRESH_GATE: PASS | member=" + MemberId +
                          " | interaction=" + interactionId + " | offer=" + offerId);
                SessionState.SetBool(RoutineRefreshGateCheckedKey, true);
            }
            CharacterStaminaSimulation simulation = bootstrap.State.Stamina.GetSimulation(MemberId);
            if (simulation.State.RecoveryPhase != StaminaRecoveryPhase.Performing) return;
            int before = simulation.State.CurrentUnits;
            int duration = simulation.Profile.Recovery(simulation.State.RecoveryActivity)
                .DurationGameMinutes;
            bootstrap.AdvanceTimeNow(duration);
            if (simulation.State.CurrentUnits <= before)
                throw new InvalidOperationException("Completion did not commit stamina recovery.");
            _deadline = EditorApplication.timeSinceStartup + 45d;
            SessionState.SetInt(StageKey, 6);
        }

        private static void ValidateReturnAndFinish()
        {
            GetRuntime(out PrototypeBootstrap bootstrap, out StarterOfficeRuntimeBootstrap runtime,
                out _);
            CharacterStaminaSimulation simulation = bootstrap.State.Stamina.GetSimulation(MemberId);
            OfficeRuntimeAgent actor = runtime.Actors.Single(item => item.AgentId == MemberId);
            if (simulation.State.RecoveryPhase != StaminaRecoveryPhase.Working ||
                !actor.IsSeated || !actor.HasAssignedTask) return;
            OfficeRuntimeAgentLayoutSnapshot resumed = actor.CaptureLayoutSnapshot();
            string expectedTask = SessionState.GetString(PausedTaskKey, string.Empty);
            float expectedRemaining = SessionState.GetFloat(PausedRemainingKey, -1f);
            if (!string.Equals(resumed.AssignedTaskId, expectedTask, StringComparison.Ordinal) ||
                Mathf.Abs(resumed.AssignedWorkRemainingMinutes - expectedRemaining) > 0.01f)
                throw new InvalidOperationException(
                    "Assigned task did not resume with exact remaining GameTime work.");

            GameSaveDto save = GameSaveMapper.ToDto(bootstrap.State);
            GameState restored = GameSaveMapper.FromDto(save);
            if (save.schemaVersion != 10 || save.staminaState == null ||
                JsonUtility.ToJson(save.staminaState) !=
                JsonUtility.ToJson(restored.Stamina.ExportSnapshot()))
                throw new InvalidOperationException("Runtime stamina save/load roundtrip failed.");
            File.WriteAllText(
                Path.GetFullPath("Artifacts/StaminaRuntimeQa/summary.txt"),
                "PASS\n" +
                "bars=4\n" +
                "speed=4x\n" +
                "thresholdBasisPoints=2500\n" +
                "restroom=fail-closed\n" +
                "routineAutonomyRefreshGate=sticky\n" +
                "contractTask=" + resumed.AssignedTaskId + "\n" +
                "remainingMinutes=" + resumed.AssignedWorkRemainingMinutes.ToString("0.###") + "\n");
            bootstrap.enabled = true;
            SessionState.SetInt(StageKey, 7);
            EditorApplication.ExitPlaymode();
        }

        private static void GetRuntime(
            out PrototypeBootstrap bootstrap,
            out StarterOfficeRuntimeBootstrap runtime,
            out StaminaRecoveryRuntimeCoordinator coordinator)
        {
            bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            runtime = UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            coordinator = UnityEngine.Object.FindFirstObjectByType<StaminaRecoveryRuntimeCoordinator>();
            if (bootstrap?.State == null || runtime == null || !runtime.IsReady ||
                runtime.World == null || coordinator == null)
                throw new InvalidOperationException("Starter stamina runtime is not ready.");
        }

        private static bool TryGetRuntime(
            out PrototypeBootstrap bootstrap,
            out StarterOfficeRuntimeBootstrap runtime,
            out StaminaRecoveryRuntimeCoordinator coordinator)
        {
            bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            runtime = UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            coordinator = UnityEngine.Object.FindFirstObjectByType<StaminaRecoveryRuntimeCoordinator>();
            return bootstrap?.State != null && runtime != null && runtime.IsReady &&
                   runtime.World != null && coordinator != null;
        }

        private static void CaptureFourFamilyBars(
            OverheadStaminaBarPresenter presenter,
            string path)
        {
            Camera camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null) throw new InvalidOperationException("Office camera missing for QA capture.");

            const int width = 1280;
            const int height = 720;
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D capture = null;
            try
            {
                camera.targetTexture = target;
                presenter.RefreshImmediateForQa();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                capture.Apply(false, false);
                byte[] png = capture.EncodeToPNG();
                if (png == null || png.Length < 1024)
                    throw new InvalidOperationException("Four-family stamina capture was empty.");
                File.WriteAllBytes(path, png);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                if (capture != null) UnityEngine.Object.Destroy(capture);
                presenter.RefreshImmediateForQa();
            }
        }

        private static void RequireBeforeDeadline()
        {
            if (EditorApplication.timeSinceStartup > _deadline)
                throw new TimeoutException("Stamina runtime QA timed out. " + DescribeCurrentStage());
        }

        private static string DescribeCurrentStage()
        {
            int stage = SessionState.GetInt(StageKey, -1);
            PrototypeBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            StarterOfficeRuntimeBootstrap runtime =
                UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            StaminaRecoveryRuntimeCoordinator coordinator =
                UnityEngine.Object.FindFirstObjectByType<StaminaRecoveryRuntimeCoordinator>();
            OfficeRuntimeAgent actor = runtime?.Actors.FirstOrDefault(item => item.AgentId == MemberId);
            CharacterStaminaSimulation simulation = bootstrap?.State?.Stamina?.GetSimulation(MemberId);
            string candidates = "none";
            string navigation = "none";
            if (runtime?.IsReady == true && bootstrap?.State != null)
            {
                var adapter = new StaminaRecoveryFurnitureCapabilityAdapter(runtime, bootstrap.State);
                StaminaRecoveryCapabilityQueryResult result = adapter.Query(
                    new StaminaRecoveryCapabilityQuery(
                        MemberId, "qa-timeout", bootstrap.State.Time.ElapsedMinutes));
                candidates = string.Join(",", result.Candidates.Select(item =>
                    item.InteractionId + ":" + item.RuntimeFurnitureInstanceId));
                if (actor != null)
                {
                    OfficeGridCoordinate cell = runtime.World.Presenter.NearestCell(actor.transform.position);
                    int reachable = runtime.World.Paths.FindStaticallyReachableCells(
                        actor.AgentId, cell, actor.ActiveSeatId, actor.AgentRadius).Count;
                    bool water = runtime.World.Workstations.TryResolveInteractionDestination(
                        "water-drink", actor.AgentId, "qa-timeout-water", cell,
                        actor.ActiveSeatId, actor.AgentRadius, out _);
                    bool lounge = runtime.World.Workstations.TryResolveInteractionDestination(
                        "lounge-rest", actor.AgentId, "qa-timeout-lounge", cell,
                        actor.ActiveSeatId, actor.AgentRadius, out _);
                    navigation = cell + "/seat=" + actor.ActiveSeatId +
                                 "/reachable=" + reachable +
                                 "/water=" + water + "/lounge=" + lounge;
                }
            }
            return "stage=" + stage +
                   " time=" + (bootstrap?.State?.Time.Now.ToString() ?? "none") +
                   " actor=" + (actor == null ? "none" :
                       actor.Phase + "/seated=" + actor.IsSeated +
                       "/assigned=" + actor.HasAssignedTask +
                       "/interaction=" + actor.ActiveInteractionId) +
                   " stamina=" + (simulation == null ? "none" :
                       simulation.State.RecoveryPhase + "/" + simulation.State.CurrentUnits +
                       "/pending=" + simulation.HasPendingRuntimeDecision +
                       "/attempt=" + simulation.State.SelectionAttempt) +
                   " candidates=" + candidates +
                   " navigation=" + navigation +
                   " command=" + (coordinator == null ||
                       !coordinator.TryGetLastCommandTransitionForQa(
                           MemberId, out StaminaRuntimeTransition command) ? "none" :
                       command.Kind + "/" + command.FailureReason + "/" +
                       command.InteractionId);
        }

        private static void Fail(Exception exception)
        {
            bool batch = SessionState.GetBool(ActiveKey + ".Batch", false);
            Debug.LogException(exception);
            Debug.LogError("FAMILY_COMPANY_STAMINA_RUNTIME_INTEGRATION: FAIL");
            Clear();
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            if (batch) EditorApplication.Exit(1);
        }

        private static void Clear()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(ActiveKey + ".Batch");
            SessionState.EraseInt(StageKey);
            SessionState.EraseString(PausedTaskKey);
            SessionState.EraseFloat(PausedRemainingKey);
            SessionState.EraseBool(ClockPreparedKey);
            SessionState.EraseBool(RoutineRefreshGateCheckedKey);
        }
    }
}
#endif

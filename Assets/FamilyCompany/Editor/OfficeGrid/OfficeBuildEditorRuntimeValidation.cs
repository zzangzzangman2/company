using System;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    /// <summary>
    /// PlayMode integration gate for the company-hub adapter, pause lifecycle, transactional
    /// purchase, actor-preserving runtime rebuild, sprite presentation and capability discovery.
    /// </summary>
    [InitializeOnLoad]
    public static class OfficeBuildEditorRuntimeValidation
    {
        private const string ActiveKey = "FamilyCompany.OfficeBuildEditorRuntimeQa.Active";
        private const string StageKey = "FamilyCompany.OfficeBuildEditorRuntimeQa.Stage";
        private const string BatchKey = "FamilyCompany.OfficeBuildEditorRuntimeQa.Batch";
        private const string FailedKey = "FamilyCompany.OfficeBuildEditorRuntimeQa.Failed";
        private const string InstanceId = "qa_runtime_drink_vending";
        private static double _deadline;
        private static long _cashBefore;
        private static int[] _energyBefore;
        private static string[] _familyBefore;
        private static OfficeGridCoordinate _origin;

        static OfficeBuildEditorRuntimeValidation()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Office Build Editor Runtime")]
        public static void StartMenu() => Start(false);

        public static void StartBatch() => Start(true);

        private static void Start(bool batch)
        {
            try
            {
                if (SessionState.GetBool(ActiveKey, false) ||
                    EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Office build editor runtime QA is already active.");
                EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetBool(BatchKey, batch);
                SessionState.SetBool(FailedKey, false);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogError("OFFICE_BUILD_EDITOR_RUNTIME_QA: FAIL | preparation");
                Debug.LogException(exception);
                ClearSession();
                if (batch) EditorApplication.Exit(1);
                else throw;
            }
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
                    ScenePreviewJump.ShowStarterOffice();
                    _deadline = EditorApplication.timeSinceStartup + 45d;
                    SessionState.SetInt(StageKey, 2);
                    return;
                }
                if (stage == 2 && EditorApplication.isPlaying)
                {
                    BeginRuntimeMutationWhenReady();
                    return;
                }
                if (stage == 3 && EditorApplication.isPlaying)
                {
                    ValidateRuntimeRebuildWhenReady();
                    return;
                }
                if (stage == 4 && !EditorApplication.isPlaying &&
                    !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool batch = SessionState.GetBool(BatchKey, false);
                    bool failed = SessionState.GetBool(FailedKey, true);
                    if (!failed)
                        Debug.Log("OFFICE_BUILD_EDITOR_RUNTIME_QA: PASS | navigation=pause/reopen | " +
                                  "purchase=vending | presenter=tile-anchor | capability=reachable | actors=4");
                    ClearSession();
                    if (batch) EditorApplication.Exit(failed ? 1 : 0);
                    return;
                }
                if (stage < 1 || stage > 4)
                    throw new InvalidOperationException("Invalid runtime QA stage: " + stage);
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Play Mode stopped before runtime QA completed.");
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private static void BeginRuntimeMutationWhenReady()
        {
            PrototypeBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            StarterOfficeRuntimeBootstrap runtime =
                UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            OfficeLayoutEditModeController controller =
                UnityEngine.Object.FindFirstObjectByType<OfficeLayoutEditModeController>();
            if (bootstrap?.State == null || runtime == null || !runtime.IsReady ||
                runtime.World == null || controller == null)
            {
                RequireBeforeDeadline("Starter office/editor activation");
                return;
            }

            GameState state = bootstrap.State;
            _familyBefore = state.Family.Members.Select(item => item.MemberId).ToArray();
            _energyBefore = state.Family.Members.Select(item => item.Energy).ToArray();
            _cashBefore = state.Company.CashWon;
            _origin = FindValidOrigin(state.OfficeGrid);

            if (OfficeBuildEditorNavigationAdapter.TryOpen(
                    "company.hub.invalid", out string invalidFailure) ||
                string.IsNullOrWhiteSpace(invalidFailure) || controller.IsOpen)
                throw new InvalidOperationException("Navigation adapter accepted an unknown entry ID.");
            if (!OfficeBuildEditorNavigationAdapter.TryOpen(
                    OfficeBuildEditorNavigationAdapter.EntryId, out string failure))
                throw new InvalidOperationException("Navigation adapter refused: " + failure);
            if (!controller.IsOpen || Time.timeScale != 0f || bootstrap.enabled)
                throw new InvalidOperationException("Editor pause lifecycle did not engage.");

            OfficeFurnitureCommandResult purchase = OfficeFurnitureTransactionService.PurchaseAndPlace(
                state,
                "qa-runtime-buy-vending",
                InstanceId,
                OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId,
                _origin,
                OfficeFurnitureFacing.SouthEast);
            if (!purchase.Success)
                throw new InvalidOperationException("Runtime vending purchase failed: " + purchase.Message);
            runtime.ApplyLayout(state.OfficeGrid);
            _deadline = EditorApplication.timeSinceStartup + 30d;
            SessionState.SetInt(StageKey, 3);
        }

        private static void ValidateRuntimeRebuildWhenReady()
        {
            PrototypeBootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            StarterOfficeRuntimeBootstrap runtime =
                UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            OfficeLayoutEditModeController controller =
                UnityEngine.Object.FindFirstObjectByType<OfficeLayoutEditModeController>();
            if (bootstrap?.State == null || runtime == null || !runtime.IsReady || runtime.World == null)
            {
                RequireBeforeDeadline("Starter office rebuild");
                return;
            }
            if (controller == null || !controller.IsOpen || Time.timeScale != 0f || bootstrap.enabled)
                throw new InvalidOperationException("Editor pause state was lost during rebuild.");

            GameState state = bootstrap.State;
            long price = OfficeFurnitureEconomyConfig.GameplayPrice(
                OfficeFurnitureCatalog.Require(
                    OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId).PurchasePriceWon);
            if (state.Company.CashWon != _cashBefore - price)
                throw new InvalidOperationException("Runtime purchase did not debit exactly once.");
            if (!state.Family.Members.Select(item => item.MemberId).SequenceEqual(_familyBefore) ||
                !state.Family.Members.Select(item => item.Energy).SequenceEqual(_energyBefore))
                throw new InvalidOperationException("Family identity or energy changed during layout rebuild.");
            if (runtime.Actors.Count != 4 ||
                !runtime.Actors.Select(item => item.AgentId).OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(_familyBefore.OrderBy(item => item, StringComparer.Ordinal)))
                throw new InvalidOperationException("Runtime actor set was not preserved.");

            PlacedOfficeFurniture placed = state.OfficeGrid.Furniture.Single(item =>
                string.Equals(item.FurnitureId, InstanceId, StringComparison.Ordinal));
            if (!placed.Origin.Equals(_origin))
                throw new InvalidOperationException("Runtime placement origin changed.");
            if (!runtime.World.FurniturePresenter.TryGetRenderer(InstanceId, out SpriteRenderer renderer) ||
                renderer == null || renderer.sprite == null || !renderer.enabled)
                throw new InvalidOperationException("Purchased vending sprite was not presented.");
            Transform semanticRoot = renderer.transform.parent == null
                ? null
                : renderer.transform.parent.parent;
            Vector3 expected = runtime.World.Presenter.SubcellAnchorWorld(placed.PlacementAnchor);
            if (semanticRoot == null || Vector3.Distance(semanticRoot.position, expected) > 0.0001f)
                throw new InvalidOperationException("Presented vending root is not on its semantic tile anchor.");

            var capabilities = new OfficeRuntimeFurnitureCapabilityAdapter(runtime, state);
            OfficeFurnitureCapabilityCandidate candidate = capabilities.FindAvailableForAgent(
                    OfficeFurnitureCapability.DrinkVending,
                    "player",
                    OfficeLayoutEditRules.CanonicalInteriorEntrance)
                .SingleOrDefault(item => string.Equals(item.InstanceId, InstanceId, StringComparison.Ordinal));
            if (candidate == null || candidate.AccessCells.Count == 0 || candidate.Capacity != 1)
                throw new InvalidOperationException("Runtime capability adapter did not expose vending access.");

            controller.Close();
            if (controller.IsOpen || Time.timeScale != 1f || !bootstrap.enabled)
                throw new InvalidOperationException("Editor pause lifecycle did not restore.");
            SessionState.SetInt(StageKey, 4);
            EditorApplication.ExitPlaymode();
        }

        private static OfficeGridCoordinate FindValidOrigin(
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid)
        {
            for (int y = 1; y < grid.Height - 1; y++)
            for (int x = 1; x < grid.Width - 1; x++)
            {
                var origin = new OfficeGridCoordinate(x, y);
                if (OfficeLayoutEditRules.PlaceFurniture(
                        grid,
                        InstanceId,
                        OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId,
                        origin,
                        OfficeFurnitureFacing.SouthEast).Success)
                    return origin;
            }
            throw new InvalidOperationException("No valid runtime vending placement exists.");
        }

        private static void RequireBeforeDeadline(string operation)
        {
            if (EditorApplication.timeSinceStartup > _deadline)
                throw new TimeoutException(operation + " timed out.");
        }

        private static void Fail(Exception exception)
        {
            bool batch = SessionState.GetBool(BatchKey, false);
            Debug.LogError("OFFICE_BUILD_EDITOR_RUNTIME_QA: FAIL");
            Debug.LogException(exception);
            SessionState.SetBool(FailedKey, true);
            SessionState.SetInt(StageKey, 4);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else
            {
                ClearSession();
                if (batch) EditorApplication.Exit(1);
            }
        }

        private static void ClearSession()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(StageKey);
            SessionState.EraseBool(BatchKey);
            SessionState.EraseBool(FailedKey);
            _deadline = 0d;
            _cashBefore = 0;
            _energyBefore = null;
            _familyBefore = null;
        }
    }
}

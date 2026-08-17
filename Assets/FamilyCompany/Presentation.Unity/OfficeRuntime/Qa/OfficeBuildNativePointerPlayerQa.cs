using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;
using Process = System.Diagnostics.Process;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Player-build regression for the exact first-click purchase path. Test setup opens the
    /// editor and prepares a purchasable preview; the commit itself is a native Windows mouse
    /// click that must reach OfficeLayoutEditModeController.HandlePointer exactly once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeBuildNativePointerPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyOfficeBuildNativePointerQa";
        public const string ArtifactDirectoryArgument = "-familyCompanyOfficeBuildNativePointerArtifacts";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint NativeMouseLeftDown = 0x0002;
        private const uint NativeMouseLeftUp = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
#endif

        private static OfficeBuildNativePointerPlayerQa _instance;
        private string _artifactDirectory = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasCommandLineFlag(CommandLineFlag)) return;
            var host = new GameObject("~OfficeBuildNativePointerPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeBuildNativePointerPlayerQa>();
        }

        private void Start()
        {
            _artifactDirectory = ResolveArtifactDirectory();
            StartCoroutine(RunGuarded());
        }

        private IEnumerator RunGuarded()
        {
            IEnumerator run = Run();
            while (true)
            {
                object yielded;
                try
                {
                    if (!run.MoveNext()) yield break;
                    yielded = run.Current;
                }
                catch (Exception exception)
                {
                    Finish(90, "unhandled=" + exception.GetType().Name + ":" + exception.Message, null);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_artifactDirectory);
            Debug.Log("FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER_QA: START | artifacts=" + _artifactDirectory);
            yield return null;

            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(91, "PrototypeBootstrap missing", null);
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();

            float readyDeadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime = null;
            OfficeLayoutEditModeController controller = null;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                controller = Object.FindFirstObjectByType<OfficeLayoutEditModeController>();
                if (runtime != null && runtime.IsReady && runtime.World != null &&
                    controller != null && bootstrap.State != null && Camera.main != null) break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null || controller == null ||
                bootstrap.State == null || Camera.main == null)
            {
                Finish(92, "normal empty-office runtime or build controller did not become ready", bootstrap);
                yield break;
            }

            GameState state = bootstrap.State;
            int editableBefore = state.OfficeGrid.Furniture.Count(item =>
                OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true);
            if (state.OfficeGrid.SeatSlots.Count != 0 || editableBefore != 0 ||
                state.OfficeFurnitureInventory.Instances.Count != 0)
            {
                Finish(93, "normal new game is not an empty office", bootstrap);
                yield break;
            }

            if (!controller.Open(out string openFailure))
            {
                Finish(94, "build editor open failed:" + openFailure, bootstrap);
                yield break;
            }

            string definitionId = OfficeGridLayouts.PottedPlantKind;
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(definitionId);
            Camera camera = Camera.main;
            if (!TryFindVisibleValidCell(state.OfficeGrid, definition, camera, runtime, out OfficeGridCoordinate target))
            {
                Finish(95, "no visible valid green preview cell was found", bootstrap);
                yield break;
            }
            if (!controller.BeginPurchaseForPlayerQa(definitionId, out string previewFailure))
            {
                Finish(96, "purchase preview setup failed:" + previewFailure, bootstrap);
                yield break;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr window = IntPtr.Zero;
            for (var frame = 0; frame < 120 && window == IntPtr.Zero; frame++)
            {
                Process process = Process.GetCurrentProcess();
                process.Refresh();
                window = process.MainWindowHandle;
                if (window == IntPtr.Zero) yield return null;
            }
            Vector3 projected = camera.WorldToScreenPoint(runtime.World.Presenter.CellCenterWorld(target));
            var nativePoint = new NativePoint
            {
                X = Mathf.RoundToInt(projected.x),
                Y = Screen.height - Mathf.RoundToInt(projected.y)
            };
            bool foreground = window != IntPtr.Zero && SetForegroundWindow(window);
            bool clientConverted = window != IntPtr.Zero && ClientToScreen(window, ref nativePoint);
            bool cursorMoved = clientConverted && SetCursorPos(nativePoint.X, nativePoint.Y);
            if (!foreground || !clientConverted || !cursorMoved)
            {
                Finish(
                    97,
                    "native pointer positioning failed:window=" + window +
                    ";foreground=" + foreground +
                    ";client=" + clientConverted +
                    ";cursor=" + cursorMoved,
                    bootstrap);
                yield break;
            }
#else
            Finish(97, "native pointer gate requires a Windows player", bootstrap);
            yield break;
#endif

            float previewDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < previewDeadline &&
                   (!controller.PreviewValidForPlayerQa ||
                    !controller.PreviewOriginForPlayerQa.Equals(target)))
                yield return null;
            if (!controller.PreviewValidForPlayerQa ||
                !controller.PreviewOriginForPlayerQa.Equals(target))
            {
                Finish(
                    98,
                    "native cursor did not create a valid preview at target:" + target +
                    ";actual=" + controller.PreviewOriginForPlayerQa +
                    ";valid=" + controller.PreviewValidForPlayerQa,
                    bootstrap);
                yield break;
            }

            yield return new WaitForEndOfFrame();
            if (!TryCaptureOverview(ArtifactPath("office-build-green-preview.png"), out string previewCaptureFailure))
            {
                Finish(99, "green preview capture failed:" + previewCaptureFailure, bootstrap);
                yield break;
            }

            var before = new StateSnapshot(state);
            long expectedPrice = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            mouse_event(NativeMouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            yield return null;
            mouse_event(NativeMouseLeftUp, 0, 0, 0, UIntPtr.Zero);
#endif
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            var after = new StateSnapshot(state);
            string instanceId = controller.DiagnosticLastMutationInstanceId;
            PlacedOfficeFurniture placed = state.OfficeGrid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, instanceId, StringComparison.Ordinal));
            OfficeFurnitureInstanceState owned = state.OfficeFurnitureInventory.Find(instanceId);
            float anchorError = float.PositiveInfinity;
            Vector3 expectedCenter = runtime.World.Presenter.CellCenterWorld(target);
            Vector3 renderedCenter = new Vector3(float.NaN, float.NaN, float.NaN);
            if (placed != null && runtime.World.FurniturePresenter.TryGetSemanticRoot(
                    instanceId, out Transform semanticRoot) && semanticRoot != null)
            {
                renderedCenter = semanticRoot.position;
                anchorError = Vector3.Distance(expectedCenter, renderedCenter);
            }

            bool passed =
                controller.DiagnosticPointerCommitCount == 1 &&
                controller.DiagnosticStateMutationCount == 1 &&
                controller.DiagnosticLastPointerCommitCell.Equals(target) &&
                before.Cash - after.Cash == expectedPrice &&
                after.Ledger == before.Ledger + 1 &&
                after.Inventory == before.Inventory + 1 &&
                after.Furniture == before.Furniture + 1 &&
                after.EditableFurniture == before.EditableFurniture + 1 &&
                !string.Equals(before.GridHash, after.GridHash, StringComparison.Ordinal) &&
                string.Equals(runtime.World.Grid.ComputeLayoutHash(), after.GridHash, StringComparison.Ordinal) &&
                placed != null && placed.Origin.Equals(target) &&
                placed.PlacementAnchor.Equals(OfficeGridSubcellAnchor.FromCellCenter(target)) &&
                owned != null && owned.PlacementState == OfficeFurniturePlacementState.Placed &&
                owned.GridOrigin.Equals(target) &&
                anchorError <= 0.001f;

            if (!TryCaptureOverview(ArtifactPath("office-build-placed.png"), out string placedCaptureFailure))
            {
                Finish(99, "placed furniture capture failed:" + placedCaptureFailure, bootstrap);
                yield break;
            }

            var result = new StringBuilder(2048);
            result.AppendLine(passed
                ? "FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: PASS"
                : "FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: FAIL");
            result.AppendLine("nativePointer=true");
            result.AppendLine("definition=" + definitionId);
            result.AppendLine("targetCell=" + target.X + ":" + target.Y);
            result.AppendLine("pointerCommitCount=" + controller.DiagnosticPointerCommitCount);
            result.AppendLine("stateMutationCount=" + controller.DiagnosticStateMutationCount);
            result.AppendLine("pointerCommitCell=" +
                              controller.DiagnosticLastPointerCommitCell.X + ":" +
                              controller.DiagnosticLastPointerCommitCell.Y);
            result.AppendLine("instance=" + instanceId);
            result.AppendLine("price=" + expectedPrice);
            result.AppendLine("cash=" + before.Cash + "->" + after.Cash);
            result.AppendLine("ledger=" + before.Ledger + "->" + after.Ledger);
            result.AppendLine("inventory=" + before.Inventory + "->" + after.Inventory);
            result.AppendLine("furniture=" + before.Furniture + "->" + after.Furniture);
            result.AppendLine("editable=" + before.EditableFurniture + "->" + after.EditableFurniture);
            result.AppendLine("gridHash=" + before.GridHash + "->" + after.GridHash);
            result.AppendLine("runtimeGridHash=" + runtime.World.Grid.ComputeLayoutHash());
            result.AppendLine("expectedCenter=" + expectedCenter.ToString("F6"));
            result.AppendLine("renderedCenter=" + renderedCenter.ToString("F6"));
            result.AppendLine("anchorError=" + anchorError.ToString("F8", CultureInfo.InvariantCulture));
            File.WriteAllText(ArtifactPath("office-build-native-pointer-result.txt"), result.ToString());

            Finish(passed ? 0 : 100, passed ? "single native click atomically purchased and placed" :
                "state or tile-center invariant failed", bootstrap, result.ToString());
        }

        private static bool TryFindVisibleValidCell(
            OfficeGrid grid,
            OfficeFurnitureDefinition definition,
            Camera camera,
            StarterOfficeRuntimeBootstrap runtime,
            out OfficeGridCoordinate target)
        {
            for (var y = 1; y < grid.Height - 1; y++)
            for (var x = 1; x < grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                OfficeLayoutEditResult edit = OfficeLayoutEditRules.PlaceFurniture(
                    grid, "__native_pointer_qa__", definition.DefinitionId, cell, definition.DesiredFacing);
                if (!edit.Success) continue;
                Vector3 screen = camera.WorldToScreenPoint(runtime.World.Presenter.CellCenterWorld(cell));
                if (screen.z <= 0f || screen.x < 48f || screen.y < 48f ||
                    screen.x > Screen.width - 500f || screen.y > Screen.height - 48f) continue;
                target = cell;
                return true;
            }
            target = default;
            return false;
        }

        private void Finish(int exitCode, string reason, PrototypeBootstrap bootstrap, string details = "")
        {
            try
            {
                if (!File.Exists(ArtifactPath("office-build-native-pointer-result.txt")))
                {
                    var result = new StringBuilder();
                    result.AppendLine(exitCode == 0
                        ? "FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: PASS"
                        : "FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: FAIL");
                    result.AppendLine("reason=" + reason);
                    if (bootstrap?.State != null)
                        result.AppendLine("clock=" + bootstrap.State.Time.Now.ToString("HH:mm", CultureInfo.InvariantCulture));
                    if (!string.IsNullOrEmpty(details)) result.Append(details);
                    File.WriteAllText(ArtifactPath("office-build-native-pointer-result.txt"), result.ToString());
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER_ARTIFACT_FAIL | " + exception.Message);
                if (exitCode == 0) exitCode = 101;
            }

            if (exitCode == 0)
                Debug.Log("FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: PASS | " + reason);
            else
                Debug.LogError("FAMILY_COMPANY_OFFICE_BUILD_NATIVE_POINTER: FAIL | code=" + exitCode + " | " + reason);
            Application.Quit(exitCode);
        }

        private string ArtifactPath(string fileName)
        {
            Directory.CreateDirectory(_artifactDirectory);
            return Path.Combine(_artifactDirectory, fileName);
        }

        private static bool TryCaptureOverview(string path, out string failure)
        {
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }
            const int width = 1280;
            const int height = 720;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject captureHost = null;
            try
            {
                captureHost = new GameObject("OfficeBuildNativePointerCapture") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = captureHost.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = width / (float)height;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 1024L;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (captureHost != null) Object.Destroy(captureHost);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static bool HasCommandLineFlag(string flag) => Array.Exists(
            Environment.GetCommandLineArgs(),
            argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));

        private static string ResolveArtifactDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], ArtifactDirectoryArgument, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(arguments[index + 1]);
            return Path.Combine(Application.persistentDataPath, "OfficeBuildNativePointerPlayerQa");
        }

        private readonly struct StateSnapshot
        {
            public StateSnapshot(GameState state)
            {
                Cash = state.Company.CashWon;
                Ledger = state.Company.Ledger.Count;
                Inventory = state.OfficeFurnitureInventory.Instances.Count;
                Furniture = state.OfficeGrid.Furniture.Count;
                EditableFurniture = state.OfficeGrid.Furniture.Count(item =>
                    OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true);
                GridHash = state.OfficeGrid.ComputeLayoutHash();
            }

            public long Cash { get; }
            public int Ledger { get; }
            public int Inventory { get; }
            public int Furniture { get; }
            public int EditableFurniture { get; }
            public string GridHash { get; }
        }
    }
}

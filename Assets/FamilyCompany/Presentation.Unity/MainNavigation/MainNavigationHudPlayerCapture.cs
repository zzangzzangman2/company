using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public sealed class MainNavigationHudPlayerCapture : MonoBehaviour
    {
        public const string EnableArgument = "-familyCompanyMainNavigationHudQa";
        public const string OutputArgument = "-familyCompanyMainNavigationHudQaOutput";

        private string _outputFolder;
        private string _reportPath;
        private bool _fontErrorSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForQa()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), EnableArgument) < 0) return;
            var host = new GameObject("~MainNavigationHudPlayerCapture");
            DontDestroyOnLoad(host);
            host.AddComponent<MainNavigationHudPlayerCapture>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            _outputFolder = ReadArgument(OutputArgument);
            if (string.IsNullOrWhiteSpace(_outputFolder))
                _outputFolder = Path.GetFullPath("Artifacts/MainNavigationHudQa");
            _outputFolder = Path.GetFullPath(_outputFolder);
            Directory.CreateDirectory(_outputFolder);
            foreach (var path in Directory.GetFiles(_outputFolder, "*.png", SearchOption.TopDirectoryOnly))
                File.Delete(path);
            _reportPath = Path.Combine(_outputFolder, "main-navigation-hud-player-qa.txt");
            File.WriteAllText(
                _reportPath,
                $"Main Navigation HUD D3D11 Player QA | {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                System.Text.Encoding.UTF8);
            StartCoroutine(RunSafely());
        }

        private void Awake()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private IEnumerator RunSafely()
        {
            var routine = RunQa();
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception exception)
                {
                    Append("PLAYER_QA_FAIL | " + exception);
                    Debug.LogException(exception);
                    Application.Quit(1);
                    yield break;
                }
                yield return current;
            }

            Append("PLAYER_QA_PASS | renderer=D3D11 captures=11 pointerRoutes=13 stockRoute=investment-only spriteStates=hover,pressed,selected");
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(0);
        }

        private IEnumerator RunQa()
        {
            Append($"PLAYER_START | graphics={SystemInfo.graphicsDeviceType} | device={SystemInfo.graphicsDeviceName}");
            Require(SystemInfo.graphicsDeviceType.ToString().IndexOf("Direct3D11", StringComparison.OrdinalIgnoreCase) >= 0,
                "QA player is not running on D3D11: " + SystemInfo.graphicsDeviceType);

            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 30f;
            PrototypeBootstrap bootstrap = null;
            MainNavigationHudPresenter presenter = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
                presenter = FindFirstObjectByType<MainNavigationHudPresenter>();
                if (bootstrap != null && presenter != null) break;
                yield return null;
            }
            Require(bootstrap != null, "PrototypeBootstrap is missing in the QA player.");
            Require(presenter != null, "MainNavigationHudPresenter is missing in the QA player.");
            bootstrap.StartNewGameNow(1, false);
            bootstrap.SetWorldTimeScaleNow(1f);
            presenter.ReturnToOfficeNow();

            deadline = Time.realtimeSinceStartup + 30f;
            while ((ScenePreviewJump.IsPresentationLoading ||
                    !presenter.GetTabButtonForQa(MainNavigationTabId.Company).gameObject.activeInHierarchy) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(!ScenePreviewJump.IsPresentationLoading, "Starter office did not finish loading within 30 seconds.");
            yield return new WaitForSecondsRealtime(1f);
            Canvas.ForceUpdateCanvases();
            Require(Screen.width == 1920 && Screen.height == 1080,
                $"Primary resolution mismatch: {Screen.width}x{Screen.height}");

            var adapter = presenter.GetStockMarketNavigationForQa();
            Require(adapter != null && adapter.CanonicalPanel != null,
                "Stock-market navigation adapter or canonical panel is missing.");
            Require(presenter.GetFeatureButtonForQa("investment-stocks") == null &&
                    !adapter.OpenFromInvestment() && !adapter.IsStockMarketOpen,
                "Main HUD exposes a direct stock-market route outside the Investment hub.");
            Append("STOCK_ROUTE_GUARD_PASS | mainHudDirectButtons=0 entry=investment-only");

            foreach (var speed in new[] { 2, 4, 1 })
            {
                ClickButton(presenter.GetSpeedButtonForQa(speed), speed + "x");
                Require(Mathf.Approximately(bootstrap.WorldTimeScale, speed),
                    $"Speed pointer route expected {speed}x, got {bootstrap.WorldTimeScale:0.##}x.");
            }
            Append("SPEED_PASS | routes=1x,2x,4x source=PrototypeBootstrap.WorldTimeScale");

            RequestCapture("closed-hud-1920x1080.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("closed-hud-1920x1080.png", 1920, 1080);

            foreach (var definition in MainNavigationCatalog.All)
            {
                ClickButton(presenter.GetTabButtonForQa(definition.TabId), definition.Id);
                Require(presenter.HasOpenPanel && presenter.ActiveTabId == definition.Id,
                    $"Pointer route expected '{definition.Id}', got '{presenter.ActiveTabId}'.");
                Require(definition.TabId == MainNavigationTabId.Investment
                        ? presenter.GetFeatureButtonForQa("investment-stocks") != null
                        : presenter.GetFeatureButtonForQa("investment-stocks") == null,
                    "Stock-market feature button escaped the Investment hub.");
                if (definition.TabId == MainNavigationTabId.Company)
                    Require(presenter.GetFeatureButtonForQa("company-building-editor") == null,
                        "Building-editor placeholder became interactive before its public adapter was integrated.");
                if (definition.TabId == MainNavigationTabId.Projects)
                    Require(presenter.GetFeatureButtonForQa("projects-contracts") == null &&
                            presenter.GetFeatureButtonForQa("projects-products") == null,
                        "Business placeholders became interactive before the canonical route adapter was integrated.");
                yield return null;
                var fileName = definition.TabId == MainNavigationTabId.Investment
                    ? "menu-investment-hub-1920x1080.png"
                    : $"menu-{definition.Id}-1920x1080.png";
                RequestCapture(fileName);
                yield return new WaitForSecondsRealtime(0.75f);
                VerifyCapture(fileName, 1920, 1080);
                if (definition.TabId == MainNavigationTabId.Investment)
                {
                    RequestCapture("interaction-selected-investment-1920x1080.png");
                    yield return new WaitForSecondsRealtime(0.75f);
                    VerifyCapture("interaction-selected-investment-1920x1080.png", 1920, 1080);
                }
            }

            Require(FindObjectsByType<StockMarketFullscreenPanel>(FindObjectsSortMode.None).Length == 1,
                "Exactly one canonical StockMarketFullscreenPanel must exist.");
            Require(presenter.ActiveTabId == "investment",
                "Stock-market card may only be tested from the Investment hub.");
            var stockButton = presenter.GetFeatureButtonForQa("investment-stocks");
            var stockPointer = CreatePointer(stockButton, "investment stock market");
            Require(ExecuteEvents.Execute(stockButton.gameObject, stockPointer, ExecuteEvents.pointerEnterHandler),
                "Pointer hover handler did not execute for the Investment stock card.");
            yield return null;
            RequestCapture("interaction-hover-stock-card-1920x1080.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("interaction-hover-stock-card-1920x1080.png", 1920, 1080);
            Require(ExecuteEvents.Execute(stockButton.gameObject, stockPointer, ExecuteEvents.pointerDownHandler),
                "Pointer press handler did not execute for the Investment stock card.");
            yield return null;
            RequestCapture("interaction-pressed-stock-card-1920x1080.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("interaction-pressed-stock-card-1920x1080.png", 1920, 1080);
            ExecuteEvents.Execute(stockButton.gameObject, stockPointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(stockButton.gameObject, stockPointer, ExecuteEvents.pointerExitHandler);
            ClickButton(stockButton, "investment stock market");
            yield return null;
            var stockPanel = adapter.CanonicalPanel;
            Require(stockPanel.IsOpen, "Investment stock-market card did not open the canonical panel.");
            Require(ReferenceEquals(stockPanel.BoundGameStateForQa, bootstrap.State) &&
                    ReferenceEquals(adapter.CanonicalGameState, bootstrap.State),
                "Stock-market route is not bound to the current canonical GameState instance.");
            Require(stockPanel.RuntimeSessionForQa != null && stockPanel.RuntimeSessionCreationCountForQa == 1,
                "Stock-market route created a missing or duplicate runtime session.");
            var canonicalRuntimeSession = stockPanel.RuntimeSessionForQa;
            var loadedState = ValidateStockSaveRoundTrip(bootstrap.State);
            Append("STOCK_STATE_PASS | gameState=same-instance saveRoundTrip=portfolio-preserved runtimeSessions=1");
            RequestCapture("stock-market-from-investment-1920x1080.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("stock-market-from-investment-1920x1080.png", 1920, 1080);

            Require(presenter.TryHandleEscape() && !stockPanel.IsOpen &&
                    presenter.HasOpenPanel && presenter.ActiveTabId == "investment",
                "First ESC did not return from Stock Market to the Investment hub.");
            Require(presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                "Second ESC did not return from the Investment hub to the office.");
            Append("BACK_STACK_PASS | stock-market>investment>office");
            yield return new WaitForSecondsRealtime(0.25f);

            ClickButton(presenter.GetTabButtonForQa(MainNavigationTabId.Investment), "investment reopen");
            yield return new WaitForSecondsRealtime(0.1f);
            ClickButton(presenter.GetFeatureButtonForQa("investment-stocks"), "investment stock market reopen");
            yield return null;
            Require(ReferenceEquals(stockPanel.RuntimeSessionForQa, canonicalRuntimeSession) &&
                    stockPanel.RuntimeSessionCreationCountForQa == 1,
                "Reopening through Investment created a duplicate runtime session or update owner.");
            Require(presenter.TryHandleEscape() && presenter.ActiveTabId == "investment",
                "Reopened stock market did not return to Investment.");
            yield return new WaitForSecondsRealtime(0.25f);
            ClickButton(presenter.GetOfficeReturnButtonForQa(), "office return");
            Require(!presenter.HasOpenPanel, "Office return pointer route did not close the menu panel.");

            ReplaceBootstrapStateForQa(bootstrap, loadedState);
            presenter.ResetSessionView();
            presenter.OpenTabNow(MainNavigationTabId.Investment);
            Require(adapter.OpenFromInvestment(),
                "Stock-market route did not reopen after isolated JSON save/load.");
            yield return null;
            Require(ReferenceEquals(adapter.CanonicalGameState, loadedState) &&
                    ReferenceEquals(stockPanel.BoundGameStateForQa, loadedState),
                "Stock-market panel did not rebind to the loaded canonical GameState instance.");
            Require(stockPanel.RuntimeSessionCreationCountForQa == 2,
                "Loaded state must create exactly one replacement runtime session.");
            Require(presenter.TryHandleEscape() && presenter.ActiveTabId == "investment" &&
                    presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                "Loaded state did not preserve the stock-market > investment > office route.");
            Append("STOCK_LOAD_ROUTE_PASS | jsonRepository=isolated portfolio=preserved route=stock>investment>office runtimeSessionsPerState=1");
            Screen.SetResolution(1680, 1050, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(1f);
            Canvas.ForceUpdateCanvases();
            Require(Screen.width == 1680 && Screen.height == 1050,
                $"Alternate resolution mismatch: {Screen.width}x{Screen.height}");
            RequestCapture("closed-hud-1680x1050.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("closed-hud-1680x1050.png", 1680, 1050);

            ClickButton(presenter.GetTabButtonForQa(MainNavigationTabId.Company), "company");
            Require(presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                "ESC priority did not close the main navigation panel first.");
            Require(!_fontErrorSeen, "Main navigation emitted a Korean glyph error during player QA.");
            Append("INPUT_PASS | pointerTabs=5 pointerSpeeds=3 officeReturn=pointer escapePriority=PASS");
        }

        private GameState ValidateStockSaveRoundTrip(GameState state)
        {
            var expected = state.StockMarket;
            var saveQaFolder = Path.Combine(_outputFolder, "stock-save-route-qa");
            Directory.CreateDirectory(saveQaFolder);
            var repository = new UnityJsonSaveRepository(3, saveQaFolder);
            repository.Save(GameSaveMapper.ToDto(state));
            Require(repository.TryLoad(out var loadedDto),
                "Isolated JSON repository did not load the stock-market QA save.");
            var loadedState = GameSaveMapper.FromDto(loadedDto);
            var restored = loadedState.StockMarket;
            Require(expected.Initialized == restored.Initialized && expected.Date == restored.Date &&
                    expected.MarketMinute == restored.MarketMinute &&
                    expected.Brokerage.CashWon == restored.Brokerage.CashWon,
                "Stock-market session or brokerage cash changed during save mapping.");
            Require(expected.Brokerage.Positions.Count == restored.Brokerage.Positions.Count,
                "Portfolio position count changed during save mapping.");
            for (var index = 0; index < expected.Brokerage.Positions.Count; index++)
            {
                var left = expected.Brokerage.Positions[index];
                var right = restored.Brokerage.Positions[index];
                Require(left.AssetId == right.AssetId && left.Units == right.Units &&
                        Math.Abs(left.AverageCostWon - right.AverageCostWon) < 0.0001d,
                    "Portfolio position changed during save mapping: " + left.AssetId);
            }
            return loadedState;
        }

        private static void ReplaceBootstrapStateForQa(PrototypeBootstrap bootstrap, GameState loadedState)
        {
            var stateField = typeof(PrototypeBootstrap).GetField(
                "_state",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(stateField != null, "PrototypeBootstrap canonical state field is unavailable to QA.");
            stateField.SetValue(bootstrap, loadedState);
        }

        private void RequestCapture(string fileName)
        {
            var path = Path.Combine(_outputFolder, fileName);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path, 1);
        }

        private void VerifyCapture(string fileName, int expectedWidth, int expectedHeight)
        {
            var path = Path.Combine(_outputFolder, fileName);
            Require(File.Exists(path) && new FileInfo(path).Length > 0L, "Screenshot is missing: " + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Require(texture.LoadImage(File.ReadAllBytes(path), false), "Screenshot PNG could not be decoded: " + path);
            var pixels = texture.GetPixels32();
            var stride = Math.Max(1, pixels.Length / 4096);
            var nonBlackSamples = 0;
            for (var index = 0; index < pixels.Length; index += stride)
            {
                var pixel = pixels[index];
                if (pixel.r + pixel.g + pixel.b > 24) nonBlackSamples++;
            }
            Destroy(texture);
            Require(nonBlackSamples >= 16,
                "Screenshot is effectively black; the D3D11 player window must be visible for capture: " + path);
            Append($"CAPTURE_PASS | {path.Replace('\\', '/')} | {expectedWidth}x{expectedHeight} | bytes={new FileInfo(path).Length}");
        }

        private void ClickButton(Button button, string label)
        {
            var pointer = CreatePointer(button, label);
            Require(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler),
                "Pointer click handler did not execute for: " + label);
            Append($"POINTER_CLICK_PASS | target={label} position={pointer.position.x:0.0},{pointer.position.y:0.0}");
        }

        private PointerEventData CreatePointer(Button button, string label)
        {
            Require(button != null && button.interactable && button.gameObject.activeInHierarchy,
                "Pointer target is unavailable: " + label);
            var eventSystem = EventSystem.current;
            Require(eventSystem != null, "EventSystem is missing for pointer QA.");
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) * 0.5f;
            var pointer = new PointerEventData(eventSystem)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, center),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Require(hits.Exists(hit => hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform)),
                $"Pointer raycast missed '{label}' at {pointer.position}.");
            return pointer;
        }

        private void Append(string line)
        {
            File.AppendAllText(_reportPath, line + Environment.NewLine, System.Text.Encoding.UTF8);
            Debug.Log("MAIN_NAVIGATION_D3D11_QA: " + line);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!string.IsNullOrEmpty(condition) &&
                condition.IndexOf("MAIN_NAVIGATION_MISSING_GLYPH", StringComparison.Ordinal) >= 0)
                _fontErrorSeen = true;
        }

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal)) return arguments[index + 1];
            }
            return string.Empty;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Presentation.Unity.ContractGrowth;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Save;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Game;
using TMPro;
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
        private int _captureCount;
        private int _pointerRouteCount;
        private int _keyboardRouteCount;
        private bool _buildAdapterAvailable;

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

            Append($"PLAYER_QA_PASS | renderer=D3D11 captures={_captureCount} pointerRoutes={_pointerRouteCount} keyboardRoutes={_keyboardRouteCount} " +
                   $"adapters=contract,stock build={(_buildAdapterAvailable ? "integrated" : "dependency-placeholder")} " +
                   "stockRoute=investment-only spriteStates=hover,pressed,selected");
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
            var contractAdapter = presenter.GetContractBusinessNavigationForQa();
            Require(contractAdapter != null && contractAdapter.IsReady,
                "ContractBusinessRuntimeAdapter is missing or not ready.");
            var buildController = presenter.GetOfficeBuildControllerForQa();
            _buildAdapterAvailable = buildController != null;
            if (!_buildAdapterAvailable)
                Append("BUILD_ADAPTER_DEPENDENCY_SKIP | route=office.build-editor placeholder=clickable integrationCandidate=required");
            Require(presenter.GetFeatureButtonForQa("investment-stocks") == null &&
                    !adapter.OpenFromInvestment() && !adapter.IsStockMarketOpen,
                "Main HUD exposes a direct stock-market route outside the Investment hub.");
            Require(OfficeBuildEditorNavigationAdapter.EntryId == MainNavigationRouteIds.BuildingEditor,
                "Build-editor route ID does not match the dependency adapter.");
            Append($"ADAPTER_READY_PASS | contract=ContractBusinessRuntimeAdapter " +
                   $"build={(_buildAdapterAvailable ? "OfficeBuildEditorNavigationAdapter" : "dependency-placeholder")} " +
                   "stock=investment-only");

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
                if (definition.TabId == MainNavigationTabId.People)
                {
                    foreach (var memberId in new[] { "player", "older_sister", "father", "mother" })
                        Require(presenter.GetWorkforceButtonForQa(memberId) != null,
                            "Employed workforce card is not clickable: " + memberId);
                }
                else
                {
                    foreach (var feature in definition.Features)
                        Require(presenter.GetFeatureButtonForQa(feature.Id) != null,
                            "Visible feature card is not clickable: " + feature.Id);
                }
                yield return null;
                var fileName = definition.TabId == MainNavigationTabId.Investment
                    ? "menu-investment-hub-1920x1080.png"
                    : $"menu-{definition.Id}-1920x1080.png";
                RequestCapture(fileName);
                yield return new WaitForSecondsRealtime(0.75f);
                VerifyCapture(fileName, 1920, 1080);
                if (definition.TabId == MainNavigationTabId.People)
                {
                    ValidateWorkforceTypography(presenter, Screen.width, Screen.height);
                    ClickButton(presenter.GetWorkforceButtonForQa("mother"), "select mother workforce card");
                    Require(presenter.SelectedWorkforceMemberIdForQa == "mother",
                        "Employee selection did not update the shared roster detail.");
                    RequestCapture("menu-people-mother-selected-1920x1080.png");
                    yield return new WaitForSecondsRealtime(0.75f);
                    VerifyCapture("menu-people-mother-selected-1920x1080.png", 1920, 1080);
                    Append("WORKFORCE_ROSTER_PASS | employed=4 candidates=0 skills=6 potential=letter-only state=separate");
                    continue;
                }
                if (definition.TabId == MainNavigationTabId.Investment)
                {
                    RequestCapture("interaction-selected-investment-1920x1080.png");
                    yield return new WaitForSecondsRealtime(0.75f);
                    VerifyCapture("interaction-selected-investment-1920x1080.png", 1920, 1080);
                }

                foreach (var feature in definition.Features)
                {
                    if (feature.Action == MainNavigationFeatureAction.OpenStockMarket) continue;
                    var featureButton = presenter.GetFeatureButtonForQa(feature.Id);
                    var keyboardRoute = feature.Id == "people-hiring";
                    if (keyboardRoute) SubmitButton(featureButton, feature.Id);
                    else ClickButton(featureButton, feature.Id);
                    yield return null;

                    if (feature.Action == MainNavigationFeatureAction.OpenBuildingEditor && buildController != null)
                    {
                        Require(buildController.IsOpen && Mathf.Approximately(Time.timeScale, 0f),
                            "Build adapter did not open its dedicated paused editor.");
                        RequestCapture("build-editor-from-company-1920x1080.png");
                        yield return RequestFullFrameCapture("build-editor-imgui-1920x1080.png");
                        yield return new WaitForSecondsRealtime(0.75f);
                        VerifyCapture("build-editor-from-company-1920x1080.png", 1920, 1080);
                        buildController.Close();
                        yield return new WaitForSecondsRealtime(0.25f);
                        Require(!buildController.IsOpen && Mathf.Approximately(Time.timeScale, 1f) &&
                                presenter.ActiveTabId == "company" && !presenter.HasOpenFeature,
                            "Build editor did not restore time and return to the Company hub.");
                        continue;
                    }

                    Require(presenter.HasOpenFeature && presenter.ActiveFeatureId == feature.Id,
                        "Feature did not enter its dedicated screen: " + feature.Id);
                    if (feature.Action == MainNavigationFeatureAction.OpenContractBoard)
                        Require(contractAdapter.CurrentRoute == ContractBusinessRoute.ContractBoard &&
                                contractAdapter.GetBoardViewModel().Cards.Count > 0,
                            "Contract route did not consume the canonical contract board.");
                    if (feature.Action == MainNavigationFeatureAction.OpenProductOpportunities)
                        Require(contractAdapter.CurrentRoute == ContractBusinessRoute.ProductOpportunities &&
                                contractAdapter.GetProductOpportunities().Count > 0,
                            "Product route did not consume canonical opportunity progress.");

                    var detailCapture = FeatureCaptureName(feature.Id);
                    if (!string.IsNullOrEmpty(detailCapture))
                    {
                        RequestCapture(detailCapture);
                        yield return new WaitForSecondsRealtime(0.75f);
                        VerifyCapture(detailCapture, 1920, 1080);
                    }

                    if (keyboardRoute)
                    {
                        Require(presenter.TryHandleEscape(), "Keyboard ESC did not leave " + feature.Id);
                    }
                    else
                    {
                        ClickButton(presenter.GetOfficeReturnButtonForQa(), "back to " + definition.Id);
                    }
                    yield return null;
                    Require(presenter.HasOpenPanel && !presenter.HasOpenFeature &&
                            presenter.ActiveTabId == definition.Id,
                        "Feature back route did not return to its hub: " + feature.Id);
                    if (definition.TabId == MainNavigationTabId.Projects)
                        Require(contractAdapter.CurrentRoute == ContractBusinessRoute.BusinessHub,
                            "Contract adapter back stack did not return to BusinessHub.");
                }
            }
            Append($"FEATURE_ROUTE_PASS | visibleCards=21 dedicatedStatus=people,research,company,investment " +
                   $"contract=board products=progress build={(_buildAdapterAvailable ? "editor" : "clickable-placeholder")} " +
                   "keyboardSubmit=people-hiring");

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
            Require(stockPanel.IsOpen && stockPanel.WorldInteractionSuppressed &&
                    Mathf.Approximately(Time.timeScale, 1f) && Mathf.Approximately(bootstrap.WorldTimeScale, 1f),
                "Stock market did not suppress world input while preserving canonical game-speed semantics.");
            Require(ReferenceEquals(stockPanel.BoundGameStateForQa, bootstrap.State) &&
                    ReferenceEquals(adapter.CanonicalGameState, bootstrap.State),
                "Stock-market route is not bound to the current canonical GameState instance.");
            Require(stockPanel.RuntimeSessionForQa != null && stockPanel.RuntimeSessionCreationCountForQa == 1,
                "Stock-market route created a missing or duplicate runtime session.");
            var canonicalRuntimeSession = stockPanel.RuntimeSessionForQa;
            var loadedState = ValidateStockSaveRoundTrip(bootstrap.State);
            Append("STOCK_STATE_PASS | gameState=same-instance saveRoundTrip=portfolio-preserved runtimeSessions=1");
            RequestCapture("stock-market-from-investment-1920x1080.png");
            yield return RequestFullFrameCapture("stock-market-imgui-1920x1080.png");
            yield return new WaitForSecondsRealtime(0.75f);
            VerifyCapture("stock-market-from-investment-1920x1080.png", 1920, 1080);

            Require(presenter.TryHandleEscape() && !stockPanel.IsOpen &&
                    !stockPanel.WorldInteractionSuppressed && presenter.HasOpenPanel &&
                    presenter.ActiveTabId == "investment",
                "First ESC did not return from Stock Market to the Investment hub.");
            Require(presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                "Second ESC did not return from the Investment hub to the office.");
            Append("BACK_STACK_PASS | stock-market>investment>office");
            yield return new WaitForSecondsRealtime(0.25f);

            presenter.OpenTabNow(MainNavigationTabId.Investment);
            Require(presenter.ActiveTabId == "investment", "Investment hub did not reopen programmatically.");
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

            presenter.OpenTabNow(MainNavigationTabId.Projects);
            yield return new WaitForSecondsRealtime(0.1f);
            ClickButton(presenter.GetFeatureButtonForQa("projects-contracts"), "contract after loaded state");
            Require(presenter.ActiveFeatureId == "projects-contracts" &&
                    contractAdapter.CurrentRoute == ContractBusinessRoute.ContractBoard &&
                    contractAdapter.GetBoardViewModel().Cards.Count > 0,
                "Contract adapter route regressed after canonical GameState load.");
            Require(presenter.TryHandleEscape() && presenter.ActiveTabId == "projects" &&
                    presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                "Loaded state did not preserve contract-board > business-hub > office route.");
            Append("CONTRACT_LOAD_ROUTE_PASS | gameState=rebound route=contract-board>business-hub>office");

            foreach (var resolution in new[]
                     {
                         new[] { 1600, 900, 1600, 900, 1 },
                         new[] { 1600, 1000, 1600, 1000, 1 },
                         new[] { 2560, 1440, 1280, 720, 2 },
                         new[] { 1392, 768, 1392, 768, 1 },
                         new[] { 1280, 720, 1280, 720, 1 }
                     })
            {
                var targetWidth = resolution[0];
                var targetHeight = resolution[1];
                var windowWidth = resolution[2];
                var windowHeight = resolution[3];
                var superSize = resolution[4];
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(1f);
                Canvas.ForceUpdateCanvases();
                Require(Screen.width == windowWidth && Screen.height == windowHeight,
                    $"Render-window mismatch: requested={windowWidth}x{windowHeight} actual={Screen.width}x{Screen.height}");
                var closedName = $"closed-hud-{targetWidth}x{targetHeight}.png";
                RequestCapture(closedName, superSize);
                yield return new WaitForSecondsRealtime(0.75f);
                VerifyCapture(closedName, targetWidth, targetHeight);
                ClickButton(presenter.GetTabButtonForQa(MainNavigationTabId.Company),
                    $"company {targetWidth}x{targetHeight}");
                yield return null;
                Canvas.ForceUpdateCanvases();
                var hubName = $"menu-company-{targetWidth}x{targetHeight}.png";
                RequestCapture(hubName, superSize);
                yield return new WaitForSecondsRealtime(0.75f);
                VerifyCapture(hubName, targetWidth, targetHeight);
                ClickButton(presenter.GetTabButtonForQa(MainNavigationTabId.People),
                    $"people {targetWidth}x{targetHeight}");
                yield return null;
                Canvas.ForceUpdateCanvases();
                ValidateWorkforceTypography(presenter, windowWidth, windowHeight);
                var peopleName = $"menu-people-{targetWidth}x{targetHeight}.png";
                RequestCapture(peopleName, superSize);
                yield return new WaitForSecondsRealtime(0.75f);
                VerifyCapture(peopleName, targetWidth, targetHeight);
                Require(presenter.TryHandleEscape() && !presenter.HasOpenPanel,
                    $"ESC priority did not close the main navigation panel first at {targetWidth}x{targetHeight}");
                Append($"RESOLUTION_ROUTE_PASS | target={targetWidth}x{targetHeight} window={windowWidth}x{windowHeight} supersize={superSize}");
            }
            Require(!_fontErrorSeen, "Main navigation emitted a Korean glyph error during player QA.");
            Append("INPUT_PASS | pointerTabs=5 workforceCards=4 pointerFeatures=16 pointerSpeeds=3 officeReturn=pointer escapePriority=PASS pause=build-only worldInput=stock-suppressed");
        }

        private void ValidateWorkforceTypography(
            MainNavigationHudPresenter presenter,
            int pixelWidth,
            int pixelHeight)
        {
            Canvas.ForceUpdateCanvases();
            var texts = presenter.GetComponentsInChildren<TMP_Text>(true);
            var panelTitles = 0;
            var employeeNames = 0;
            var bodyTexts = 0;
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (text == null || !text.gameObject.name.StartsWith("Workforce ", StringComparison.Ordinal))
                    continue;

                float minimumPixels;
                if (text.gameObject.name == "Workforce Panel Title")
                {
                    panelTitles++;
                    minimumPixels = 28f;
                }
                else if (text.gameObject.name == "Workforce Employee Name")
                {
                    employeeNames++;
                    minimumPixels = 18f;
                }
                else
                {
                    bodyTexts++;
                    minimumPixels = 16f;
                }

                text.ForceMeshUpdate();
                var scale = text.canvas != null ? text.canvas.scaleFactor : 1f;
                var pixelFontSize = text.fontSize * scale;
                Require(!text.enableAutoSizing,
                    $"Workforce text uses forbidden auto-size: {text.gameObject.name} at {pixelWidth}x{pixelHeight}.");
                Require(pixelFontSize + 0.05f >= minimumPixels,
                    $"Workforce text is too small: {text.gameObject.name}={pixelFontSize:0.00}px, " +
                    $"minimum={minimumPixels:0}px at {pixelWidth}x{pixelHeight}.");
                Require(!text.isTextOverflowing,
                    $"Workforce text overflows bounds: {text.gameObject.name}='{text.text}' " +
                    $"at {pixelWidth}x{pixelHeight}; rect={text.rectTransform.rect.width:0.0}x" +
                    $"{text.rectTransform.rect.height:0.0} preferred={text.preferredWidth:0.0}x" +
                    $"{text.preferredHeight:0.0}.");
            }

            ValidateWorkforceTextCollisions(texts, pixelWidth, pixelHeight);

            Require(panelTitles == 1 && employeeNames == 4 && bodyTexts >= 20,
                $"Workforce typography coverage mismatch at {pixelWidth}x{pixelHeight}: " +
                $"title={panelTitles} names={employeeNames} body={bodyTexts}.");
            Append($"WORKFORCE_TYPOGRAPHY_PASS | resolution={pixelWidth}x{pixelHeight} " +
                   "font=Maplestory panel>=28px names>=18px body>=16px autosize=off overflow=0 collisions=0");
        }

        private static void ValidateWorkforceTextCollisions(
            IReadOnlyList<TMP_Text> allTexts,
            int pixelWidth,
            int pixelHeight)
        {
            var visible = new List<KeyValuePair<TMP_Text, Rect>>();
            for (var index = 0; index < allTexts.Count; index++)
            {
                var text = allTexts[index];
                if (text == null || !text.gameObject.activeInHierarchy ||
                    !text.gameObject.name.StartsWith("Workforce ", StringComparison.Ordinal))
                    continue;
                visible.Add(new KeyValuePair<TMP_Text, Rect>(text, ScreenRect(text.rectTransform)));
            }

            for (var leftIndex = 0; leftIndex < visible.Count; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < visible.Count; rightIndex++)
                {
                    var left = visible[leftIndex];
                    var right = visible[rightIndex];
                    var overlapWidth = Mathf.Min(left.Value.xMax, right.Value.xMax) -
                                       Mathf.Max(left.Value.xMin, right.Value.xMin);
                    var overlapHeight = Mathf.Min(left.Value.yMax, right.Value.yMax) -
                                        Mathf.Max(left.Value.yMin, right.Value.yMin);
                    Require(overlapWidth <= 0.5f || overlapHeight <= 0.5f,
                        $"Workforce text rectangles collide at {pixelWidth}x{pixelHeight}: " +
                        $"{left.Key.gameObject.name}='{left.Key.text}' {left.Value} vs " +
                        $"{right.Key.gameObject.name}='{right.Key.text}' {right.Value}.");
                }
            }
        }

        private static Rect ScreenRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));
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

        private void RequestCapture(string fileName, int superSize = 1)
        {
            var path = Path.Combine(_outputFolder, fileName);
            if (File.Exists(path)) File.Delete(path);
            CaptureOffscreen(path, superSize);
            _captureCount++;
        }

        /// <summary>
        /// Captures the presented frame, including IMGUI. <see cref="CaptureOffscreen"/> renders the
        /// camera into a RenderTexture, which never contains <c>OnGUI</c> output, so the build editor
        /// and the stock market panel come out as an empty office. Those are the screens this is for.
        /// </summary>
        private IEnumerator RequestFullFrameCapture(string fileName)
        {
            var path = Path.Combine(_outputFolder, fileName);
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (!File.Exists(path) || new FileInfo(path).Length <= 1024L) continue;
                // A file on disk is not evidence. ScreenCapture has no swapchain to read under
                // -batchmode and writes a fully black frame, which would otherwise be recorded as a
                // pass for a screen nobody has actually looked at.
                Require(IsFrameVisible(path),
                    "Full frame screenshot is effectively black, so this screen was not really " +
                    "captured. ScreenCapture needs a presented frame; re-run without -batchmode: " + path);
                _captureCount++;
                Append($"FULL_FRAME_CAPTURE_PASS | {path}");
                yield break;
            }

            Require(false, "Full frame screenshot was never written: " + path);
        }

        private static void CaptureOffscreen(string path, int superSize)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (var index = 0; index < cameras.Length; index++)
                {
                    if (!cameras[index].enabled) continue;
                    camera = cameras[index];
                    break;
                }
            }

            Require(camera != null, "An enabled camera is required for offscreen player capture.");
            superSize = Math.Max(1, superSize);
            var width = Math.Max(1, Screen.width * superSize);
            var height = Math.Max(1, Screen.height * superSize);
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var canvasStates = new List<OverlayCanvasCaptureState>();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                for (var index = 0; index < canvases.Length; index++)
                {
                    var canvas = canvases[index];
                    if (!canvas.isRootCanvas) continue;
                    canvasStates.Add(new OverlayCanvasCaptureState(canvas));
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + 0.01f;
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = short.MaxValue;
                    }
                    else
                    {
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = short.MinValue;
                    }
                }

                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                for (var index = 0; index < canvasStates.Count; index++)
                    canvasStates[index].Restore();
                Canvas.ForceUpdateCanvases();
                if (texture != null) Destroy(texture);
                if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static bool IsFrameVisible(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) return false;
                var pixels = texture.GetPixels32();
                var stride = Math.Max(1, pixels.Length / 4096);
                var nonBlackSamples = 0;
                for (var index = 0; index < pixels.Length; index += stride)
                {
                    var pixel = pixels[index];
                    if (pixel.r + pixel.g + pixel.b > 24) nonBlackSamples++;
                }

                return nonBlackSamples >= 16;
            }
            finally
            {
                Destroy(texture);
            }
        }

        private void VerifyCapture(string fileName, int expectedWidth, int expectedHeight)
        {
            var path = Path.Combine(_outputFolder, fileName);
            Require(File.Exists(path) && new FileInfo(path).Length > 0L, "Screenshot is missing: " + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Require(texture.LoadImage(File.ReadAllBytes(path), false), "Screenshot PNG could not be decoded: " + path);
            Require(texture.width == expectedWidth && texture.height == expectedHeight,
                $"Screenshot dimension mismatch for {fileName}: {texture.width}x{texture.height}");
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
                "Offscreen D3D11 screenshot is effectively black: " + path);
            Append($"CAPTURE_PASS | {path.Replace('\\', '/')} | {expectedWidth}x{expectedHeight} | bytes={new FileInfo(path).Length}");
        }

        private readonly struct OverlayCanvasCaptureState
        {
            private readonly Canvas _canvas;
            private readonly RenderMode _renderMode;
            private readonly Camera _worldCamera;
            private readonly float _planeDistance;
            private readonly bool _overrideSorting;
            private readonly int _sortingOrder;

            public OverlayCanvasCaptureState(Canvas canvas)
            {
                _canvas = canvas;
                _renderMode = canvas.renderMode;
                _worldCamera = canvas.worldCamera;
                _planeDistance = canvas.planeDistance;
                _overrideSorting = canvas.overrideSorting;
                _sortingOrder = canvas.sortingOrder;
            }

            public void Restore()
            {
                if (_canvas == null) return;
                _canvas.renderMode = _renderMode;
                _canvas.worldCamera = _worldCamera;
                _canvas.planeDistance = _planeDistance;
                _canvas.overrideSorting = _overrideSorting;
                _canvas.sortingOrder = _sortingOrder;
            }
        }

        private static string FeatureCaptureName(string featureId)
        {
            switch (featureId)
            {
                case "people-hiring": return "detail-people-coming-soon-1920x1080.png";
                case "research-tech-tree": return "detail-research-coming-soon-1920x1080.png";
                case "projects-contracts": return "detail-contract-board-1920x1080.png";
                case "projects-products": return "detail-product-opportunities-1920x1080.png";
                case "investment-loans": return "detail-investment-coming-soon-1920x1080.png";
                default: return string.Empty;
            }
        }

        private void SubmitButton(Button button, string label)
        {
            Require(button != null && button.interactable && button.gameObject.activeInHierarchy,
                "Keyboard target is unavailable: " + label);
            var eventSystem = EventSystem.current;
            Require(eventSystem != null, "EventSystem is missing for keyboard QA.");
            eventSystem.SetSelectedGameObject(button.gameObject);
            var submitted = ExecuteEvents.Execute(button.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            Require(submitted, "Keyboard submit handler did not execute for: " + label);
            _keyboardRouteCount++;
            Append("KEYBOARD_SUBMIT_PASS | target=" + label);
        }

        private void ClickButton(Button button, string label)
        {
            var pointer = CreatePointer(button, label);
            Require(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler),
                "Pointer click handler did not execute for: " + label);
            _pointerRouteCount++;
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

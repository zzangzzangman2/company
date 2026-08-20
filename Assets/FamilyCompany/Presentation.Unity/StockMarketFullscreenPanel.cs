using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.History;
using FamilyCompany.Simulation.Market;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// PC-landscape projection of the original SIMUL stock screen. The source
    /// screen's information architecture and observable order-book rules remain
    /// intact; the mobile routes are spread across a desktop four-column canvas.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class StockMarketFullscreenPanel : MonoBehaviour
    {
        private enum PrimarySection { Home, Explore, Account }
        private enum DetailTab { Quote, Order, Chart, Info }
        private enum OrderSection { Buy, Sell, AmendCancel, OpenOrders, Balance }

        private static readonly Color MarketInk = Html("24313A");
        private static readonly Color MarketMuted = Html("687984");
        private static readonly Color MarketLine = Html("D9E8E8");
        private static readonly Color MarketSurface = Html("F4FAF8");
        private static readonly Color PanelSurface = Html("FFFCF5");
        private static readonly Color CreamHighlight = Html("FFF1D6");
        private static readonly Color MarketAccent = Html("2F9B83");
        private static readonly Color SellBlue = Html("3F7FD9");
        private static readonly Color UpRed = Html("F05F6B");
        private static readonly Color AskTint = Html("EAF4FF");
        private static readonly Color BidTint = Html("FFF0F1");
        private static readonly Color PositiveGreen = Html("27A879");
        private static readonly string[] PlaybackLabels = { "정지", "5분", "15분", "50분" };
        private static readonly int[] PlaybackMinutes = { 0, 5, 15, 50 };
        private static readonly int[] PlaybackAnimationRates = { 0, 1, 3, 10 };
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint NativeMouseLeftDown = 0x0002;
        private const uint NativeMouseLeftUp = 0x0004;
        private const uint NativeMouseWheel = 0x0800;

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

        private PrototypeBootstrap _bootstrap;
        private KoreaHistoryV1RuntimeCatalog _catalog;
        private PrototypePlayerController _playerController;
        private PlayerOfficeWorkInteractor _workInteractor;
        private IsometricCameraFollow _cameraFollow;
        private bool _playerControllerWasEnabled;
        private bool _workInteractorWasEnabled;
        private bool _cameraFollowWasEnabled;
        private bool _worldInteractionSuppressed;
        private readonly List<MarketSecurityDefinition> _securities = new List<MarketSecurityDefinition>();
        private readonly HashSet<string> _knownBrokerageAssetIds = new HashSet<string>(StringComparer.Ordinal);
        private GameState _boundGameState;
        private StockMarketRuntimeSession _runtimeSession;
        private CompanyBrokerageTransferService _transferService;
        private int _selectedSecurityIndex;
        private bool _open;
        private PrimarySection _primarySection = PrimarySection.Explore;
        private DetailTab _detailTab = DetailTab.Quote;
        private OrderSection _orderSection = OrderSection.Buy;
        private int _marketCategory;
        private int _marketSort;
        private int _playbackIndex = 1;
        private int _marketMinute = MarketSessionClock.DayStartMinute;
        private readonly StockMarketRealtimeClock _realtimeClock = new StockMarketRealtimeClock();
        private bool _realtimeQaObserving;
        private long _realtimeQaAdvancedMinutes;
        private bool _realtimeQaPassed;
        private string _searchText = string.Empty;
        private string _priceText = string.Empty;
        private string _quantityText = "1";
        private string _amendOrderId = string.Empty;
        private string _amendPriceText = string.Empty;
        private string _amendQuantityText = string.Empty;
        private string _transferAmountText = "100000";
        private string _orderNotice = "호가를 누르면 지정가가 입력됩니다.";
        private string _transferNotice = "회사 현금과 증권 예수금 사이에서 이체합니다.";
        private bool _marketOrder;
        private Vector2 _watchlistScroll;
        private Vector2 _activityScroll;
        private MarketOrderBookSnapshot _snapshot;
        private long _previousClose;
        private long _lastTradePrice;
        private MarketOrderBookSide _lastTradeLevelSide = MarketOrderBookSide.Bid;

        /// <summary>
        /// Replays each minute's fills across the ladder the way SIMUL does. Without this the
        /// outline sits on the touch, the ladder recentres on the touch every frame, and the border
        /// never appears to move even though the price is changing.
        /// </summary>
        private MarketOrderBookReplayQueue _sweepQueue;

        private long _sweepPreviousTradePrice;
        private string _sweepPreviousKey = string.Empty;

        /// <summary>Realtime seconds since the active step arrived, for the 420ms trade flash.</summary>
        private float _sweepFlashSeconds;

        private string _sweepFlashToken = string.Empty;

        /// <summary>Depth tweens and the 520ms quantity badges the source draws inside each row.</summary>
        private readonly OrderBookRowMotion _rowMotion = new OrderBookRowMotion();
        private string _catalogError;

        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _numberStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _tinyStyle;
        private Texture2D _whiteTexture;
        private Font _koreanFont;
        private Font _boldKoreanFont;
        private Font _fallbackKoreanFont;
        private Texture2D _skinTexture;

        public bool IsOpen => _open;
        public bool WorldInteractionSuppressed => _worldInteractionSuppressed;
        public GameState BoundGameStateForQa => _boundGameState;
        public StockMarketRuntimeSession RuntimeSessionForQa => _runtimeSession;
        public int RuntimeSessionCreationCountForQa { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap != null && bootstrap.GetComponent<StockMarketFullscreenPanel>() == null)
                bootstrap.gameObject.AddComponent<StockMarketFullscreenPanel>();
        }

        private void Awake()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-familyCompanyCaptureStock") >= 0)
                Application.runInBackground = true;
            _bootstrap = GetComponent<PrototypeBootstrap>() ?? FindFirstObjectByType<PrototypeBootstrap>();
            _catalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
            var resources = UnityEngine.Resources.Load<StockMarketUiResources>("StockMarketUiResources");
            _koreanFont = resources?.PrimaryFont;
            _boldKoreanFont = resources?.BoldFont;
            _fallbackKoreanFont = resources?.FallbackFont;
            _skinTexture = UnityEngine.Resources.Load<Texture2D>("StockMarket/stock_market_landscape_skin_v1");
            StockMarketLandscapeLayout.Create(1920f).ValidateOrThrow();
        }

        private void Start()
        {
            TryStartStockQaCapture();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            SynchronizeGameStateBinding();
            var canShow = _bootstrap != null &&
                          _bootstrap.State != null &&
                          _bootstrap.UiScreen == PrototypeUiScreen.Playing;
            if (!canShow)
            {
                if (_open || _worldInteractionSuppressed) CloseNow();
                return;
            }

            if (_open && Input.GetKeyDown(KeyCode.F3)) CloseNow();
            if (!_open)
            {
                FlushRuntimeToGameState();
                return;
            }

            // The sweep is presentation timing, so it runs on real seconds and keeps advancing
            // even on the minutes where the clock produces no new canonical minute.
            AdvanceSweepPlayback(Time.unscaledDeltaTime);
            var minutesToAdvance = ConsumeRealtimeGameMinutes(Time.unscaledDeltaTime);
            if (_realtimeQaObserving)
            {
                _realtimeQaAdvancedMinutes += minutesToAdvance;
                return;
            }
            if (minutesToAdvance <= 0)
            {
                FlushRuntimeToGameState();
                return;
            }

            var date = CurrentDate;
            var clock = MarketSessionClock.At(_marketMinute, MarketTradingCalendar.IsTradingDay(date));
            if (clock.Phase == MarketSessionPhase.Closed || clock.Phase == MarketSessionPhase.Holiday)
            {
                FlushRuntimeToGameState();
                return;
            }

            EnsureRuntimeSession();
            _runtimeSession?.AdvanceMinutes(
                minutesToAdvance,
                PlaybackAnimationRates[_playbackIndex]);
            SyncSelectedRuntimeView();
            FlushRuntimeToGameState();
        }

        private int ConsumeRealtimeGameMinutes(double unscaledDeltaSeconds)
        {
            return _realtimeClock.Consume(unscaledDeltaSeconds, PlaybackMinutes[_playbackIndex]);
        }

        private void OnDestroy()
        {
            FlushRuntimeToGameState();
            RestoreWorldInteraction();
            if (_whiteTexture != null) Destroy(_whiteTexture);
        }

        private void OnDisable()
        {
            FlushRuntimeToGameState();
            RestoreWorldInteraction();
        }

        private void TryStartStockQaCapture()
        {
            const string argumentName = "-familyCompanyCaptureStock";
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, argumentName);
            if (index < 0 || index + 1 >= arguments.Length) return;
            var skipRealtimeQa = Array.IndexOf(arguments, "-familyCompanySkipRealtimeQa") >= 0;
            StartCoroutine(CaptureStockForQa(Path.GetFullPath(arguments[index + 1]), skipRealtimeQa));
        }

        private IEnumerator CaptureStockForQa(string outputPath, bool skipRealtimeQa)
        {
            var arguments = Environment.GetCommandLineArgs();
            var requestedWidth = ReadPositiveArgument(arguments, "-familyCompanyQaWidth", Screen.width);
            var requestedHeight = ReadPositiveArgument(arguments, "-familyCompanyQaHeight", Screen.height);
            Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
            for (var frame = 0; frame < 120 &&
                                (Screen.width != requestedWidth || Screen.height != requestedHeight); frame += 1)
                yield return null;
            if (Screen.width != requestedWidth || Screen.height != requestedHeight)
            {
                Debug.LogError(
                    "FAMILY_COMPANY_STOCK_CAPTURE: FAIL " +
                    $"(requested {requestedWidth}x{requestedHeight}, got {Screen.width}x{Screen.height})");
                Application.Quit(1);
                yield break;
            }

            _bootstrap.StartNewGameNow(1, false);
            yield return null;
            var player = FindFirstObjectByType<PrototypePlayerController>();
            var interactor = FindFirstObjectByType<PlayerOfficeWorkInteractor>();
            var cameraFollow = FindFirstObjectByType<IsometricCameraFollow>();
            if (player == null || interactor == null || cameraFollow == null || Camera.main == null)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (world interaction components missing)");
                Application.Quit(1);
                yield break;
            }
            var playerWasEnabled = player.enabled;
            var interactorWasEnabled = interactor.enabled;
            var cameraFollowWasEnabled = cameraFollow.enabled;
            OpenNow();
            EnsureStyles();
            if (!ValidateTypographyForQa(out var typographyError))
            {
                Debug.LogError($"FAMILY_COMPANY_STOCK_TYPOGRAPHY: FAIL ({typographyError})");
                Application.Quit(1);
                yield break;
            }
            Debug.Log(
                "FAMILY_COMPANY_STOCK_TYPOGRAPHY: PASS " +
                $"(font={_koreanFont.name}/{_boldKoreanFont.name}, fallback={_fallbackKoreanFont.name}, " +
                $"companies={_securities.Count}, " +
                $"skin={(_skinTexture != null ? "ImageGen" : "bright-fallback")})");
            if (!ValidateOpeningAuctionForQa(out var openingError))
            {
                Debug.LogError($"FAMILY_COMPANY_STOCK_OPENING: FAIL ({openingError})");
                Application.Quit(1);
                yield break;
            }
            if (!WorldInteractionSuppressed || player.enabled || interactor.enabled || cameraFollow.enabled)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (stock modal did not suppress world interaction)");
                Application.Quit(1);
                yield break;
            }
            CloseNow();
            if (WorldInteractionSuppressed || player.enabled != playerWasEnabled ||
                interactor.enabled != interactorWasEnabled || cameraFollow.enabled != cameraFollowWasEnabled)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (stock modal did not restore world interaction)");
                Application.Quit(1);
                yield break;
            }
            OpenNow();
            _bootstrap.ShowPauseMenuNow();
            yield return null;
            if (IsOpen || WorldInteractionSuppressed || player.enabled != playerWasEnabled ||
                interactor.enabled != interactorWasEnabled || cameraFollow.enabled != cameraFollowWasEnabled)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (menu transition did not restore world interaction)");
                Application.Quit(1);
                yield break;
            }
            _bootstrap.ResumeGameNow();
            OpenNow();

            var nativeClickShieldVerified = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var gameTimeBeforeShieldClick = _bootstrap.State.Time.Now;
            var companyCashBeforeShieldClick = _bootstrap.State.Company.CashWon;
            var companyReputationBeforeShieldClick = _bootstrap.State.Company.Reputation;
            var nativeWindow = IntPtr.Zero;
            for (var frame = 0; frame < 60 && nativeWindow == IntPtr.Zero; frame += 1)
            {
                nativeWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (nativeWindow == IntPtr.Zero) yield return null;
            }
            var behindHudOneHourButton = new NativePoint
            {
                X = Mathf.RoundToInt(1317.5f * Screen.width / 1680f),
                Y = Mathf.RoundToInt(769f * Screen.height / 945f)
            };
            if (nativeWindow != IntPtr.Zero) SetForegroundWindow(nativeWindow);
            var clientCoordinateConverted = nativeWindow != IntPtr.Zero &&
                                            ClientToScreen(nativeWindow, ref behindHudOneHourButton);
            var cursorMoved = clientCoordinateConverted &&
                              SetCursorPos(behindHudOneHourButton.X, behindHudOneHourButton.Y);
            if (!clientCoordinateConverted || !cursorMoved)
            {
                Debug.LogError(
                    "FAMILY_COMPANY_STOCK_CLICK_SHIELD: FAIL " +
                    $"(window={nativeWindow}, client={clientCoordinateConverted}, cursor={cursorMoved})");
                Application.Quit(1);
                yield break;
            }
            yield return null;
            mouse_event(NativeMouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            yield return null;
            mouse_event(NativeMouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            yield return null;
            yield return null;
            if (!IsOpen || _bootstrap.UiScreen != PrototypeUiScreen.Playing ||
                _bootstrap.State.Time.Now != gameTimeBeforeShieldClick ||
                _bootstrap.State.Company.CashWon != companyCashBeforeShieldClick ||
                _bootstrap.State.Company.Reputation != companyReputationBeforeShieldClick)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CLICK_SHIELD: FAIL (behind-HUD +1 hour click leaked through modal)");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("FAMILY_COMPANY_STOCK_CLICK_SHIELD: PASS (+1 hour HUD coordinate was consumed)");
            var cameraZoomBeforeWheel = Camera.main.orthographicSize;
            mouse_event(NativeMouseWheel, 0, 0, 120, UIntPtr.Zero);
            yield return null;
            yield return null;
            if (Math.Abs(Camera.main.orthographicSize - cameraZoomBeforeWheel) > 0.0001f)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_WHEEL_SHIELD: FAIL (camera zoom changed behind modal)");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("FAMILY_COMPANY_STOCK_WHEEL_SHIELD: PASS (camera zoom stayed fixed)");
            nativeClickShieldVerified = true;
#else
            Debug.LogError("FAMILY_COMPANY_STOCK_CLICK_SHIELD: FAIL (Windows standalone QA required)");
#endif
            if (!nativeClickShieldVerified)
            {
                Application.Quit(1);
                yield break;
            }

            if (!skipRealtimeQa)
            {
                yield return VerifyRealtimePlaybackForQa();
                if (!_realtimeQaPassed)
                {
                    Application.Quit(1);
                    yield break;
                }
            }

            _runtimeSession?.SetMarketMinute(MarketSessionClock.OpenMinute + 30);
            SyncSelectedRuntimeView();

            var qaSecurityIndex = -1;
            for (var securityIndex = 0; securityIndex < _securities.Count; securityIndex += 1)
            {
                var security = _securities[securityIndex];
                var view = _runtimeSession.ViewFor(security.CompanyId);
                var range = MarketPricingRules.DailyPriceRange(
                    view.PreviousClose,
                    CurrentDate,
                    security.PriceRuleMarket);
                var reservation = MarketTradingCosts.BuyReservation(CurrentDate, range.Upper);
                if (view.Snapshot.Asks.Count > 0 && view.Snapshot.Asks[0].Quantity > 0 &&
                    reservation <= _runtimeSession.AvailableBrokerageCash)
                {
                    qaSecurityIndex = securityIndex;
                    break;
                }
            }
            if (qaSecurityIndex < 0)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (no affordable practice-account security)");
                Application.Quit(1);
                yield break;
            }

            _selectedSecurityIndex = qaSecurityIndex;
            SyncSelectedRuntimeView();
            var qaAssetId = SelectedSecurity.CompanyId;
            var cashBeforeQaOrder = _runtimeSession.BrokerageCash;
            var unitsBeforeQaOrder = _runtimeSession.PositionUnits(qaAssetId);
            _marketOrder = true;
            _quantityText = "1";
            ValidateAndStageOrder(true);
            if (_runtimeSession.PositionUnits(qaAssetId) != unitsBeforeQaOrder + 1 ||
                _runtimeSession.BrokerageCash >= cashBeforeQaOrder)
            {
                Debug.LogError("FAMILY_COMPANY_STOCK_CAPTURE: FAIL (live market order did not settle)");
                Application.Quit(1);
                yield break;
            }

            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ScreenCapture.CaptureScreenshot(outputPath);
            for (var frame = 0; frame < 180 && !File.Exists(outputPath); frame += 1)
                yield return null;
            var success = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            Debug.Log(success
                ? $"FAMILY_COMPANY_STOCK_CAPTURE: PASS ({Screen.width}x{Screen.height}; {outputPath})"
                : $"FAMILY_COMPANY_STOCK_CAPTURE: FAIL ({outputPath})");
            Application.Quit(success ? 0 : 1);
        }

        private IEnumerator VerifyRealtimePlaybackForQa()
        {
            const double targetRealSeconds = 12d;
            var originalPlaybackIndex = _playbackIndex;
            var originalTimeScale = Time.timeScale;
            var originalRunInBackground = Application.runInBackground;
            _realtimeQaPassed = false;
            _realtimeQaObserving = true;
            Application.runInBackground = true;

            try
            {
                for (var index = 0; index < PlaybackMinutes.Length; index += 1)
                {
                    _playbackIndex = index;
                    _realtimeClock.Reset();
                    _realtimeQaAdvancedMinutes = 0L;
                    Time.timeScale = index == 2 ? 0.25f : 1f;

                    var startedAt = Time.realtimeSinceStartupAsDouble;
                    while (Time.realtimeSinceStartupAsDouble - startedAt < targetRealSeconds)
                        yield return null;

                    var elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                    var expected = PlaybackMinutes[index] * 12L;
                    var tolerance = PlaybackMinutes[index];
                    var error = _realtimeQaAdvancedMinutes - expected;
                    Debug.Log(
                        "FAMILY_COMPANY_STOCK_REALTIME: " +
                        $"speed={PlaybackLabels[index]}, real={elapsed:F3}s, " +
                        $"gameMinutes={_realtimeQaAdvancedMinutes}, expected={expected}, " +
                        $"error={error}, tolerance={tolerance}, timeScale={Time.timeScale:F2}");
                    if (Math.Abs(error) > tolerance)
                    {
                        Debug.LogError(
                            "FAMILY_COMPANY_STOCK_REALTIME: FAIL " +
                            $"({PlaybackLabels[index]} emitted {_realtimeQaAdvancedMinutes}, expected {expected}±{tolerance})");
                        yield break;
                    }
                }

                _playbackIndex = 1;
                _realtimeClock.Reset();
                var catchUpMinutes = ConsumeRealtimeGameMinutes(2.4d);
                var retainedResidual = _realtimeClock.AccumulatedSeconds;
                catchUpMinutes += ConsumeRealtimeGameMinutes(0.6d);
                if (catchUpMinutes != 15 || Math.Abs(retainedResidual - 0.4d) > 0.000001d ||
                    Math.Abs(_realtimeClock.AccumulatedSeconds) > 0.000001d)
                {
                    Debug.LogError(
                        "FAMILY_COMPANY_STOCK_REALTIME_CATCHUP: FAIL " +
                        $"(minutes={catchUpMinutes}, residualAfterDrop={retainedResidual:F6}, " +
                        $"finalResidual={_realtimeClock.AccumulatedSeconds:F6})");
                    yield break;
                }

                Debug.Log(
                    "FAMILY_COMPANY_STOCK_REALTIME_CATCHUP: PASS " +
                    "(2.4s=>10분+0.4s 잔여, 다음 0.6s=>5분; 잔여시간 보존)");
                _realtimeQaPassed = true;
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Application.runInBackground = originalRunInBackground;
                _playbackIndex = originalPlaybackIndex;
                _realtimeClock.Reset();
                _realtimeQaObserving = false;
                _realtimeQaAdvancedMinutes = 0L;
            }
        }

        private static int ReadPositiveArgument(string[] arguments, string name, int fallback)
        {
            var index = Array.IndexOf(arguments, name);
            if (index < 0 || index + 1 >= arguments.Length ||
                !int.TryParse(arguments[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                value <= 0)
                return fallback;
            return value;
        }

        public void OpenNow()
        {
            if (_bootstrap == null || _bootstrap.State == null) return;
            SynchronizeGameStateBinding();
            SuppressWorldInteraction();
            _open = true;
            _marketMinute = Mathf.Clamp(
                _bootstrap.State.Time.Now.Hour * 60 + _bootstrap.State.Time.Now.Minute,
                MarketSessionClock.DayStartMinute,
                MarketSessionClock.DayEndMinute);
            RefreshSecurities();
            EnsureRuntimeSession();
            SyncSelectedRuntimeView();
        }

        public void CloseNow()
        {
            FlushRuntimeToGameState();
            _open = false;
            RestoreWorldInteraction();
        }

        public void ToggleNow()
        {
            if (_open) CloseNow();
            else OpenNow();
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -100;
            if (_bootstrap == null || _bootstrap.State == null ||
                _bootstrap.UiScreen != PrototypeUiScreen.Playing) return;

            if (!_open)
            {
                // MainNavigationHudPresenter owns the only office-to-market route:
                // Investment hub -> Stock Market. F3 remains a close shortcut only while open.
                return;
            }

            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), MarketSurface);
            if (_skinTexture != null)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.22f);
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    _skinTexture,
                    ScaleMode.StretchToFill,
                    false);
                GUI.color = previousColor;
            }
            if (StockMarketLandscapeLayout.RequiresMinimumSizeNotice(Screen.width, Screen.height))
            {
                DrawMinimumSizeNotice();
                ConsumeModalInput();
                return;
            }
            var viewport = StockMarketLandscapeLayout.CalculateViewport(Screen.width, Screen.height);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(viewport.OffsetX, viewport.OffsetY, 0f),
                Quaternion.identity,
                new Vector3(viewport.Scale, viewport.Scale, 1f));
            try
            {
                var layout = StockMarketLandscapeLayout.Create(viewport.LogicalWidth);
                DrawSolid(
                    new Rect(0f, 0f, layout.CanvasWidth, StockMarketLandscapeLayout.ReferenceHeight),
                    _skinTexture == null
                        ? MarketSurface
                        : new Color(MarketSurface.r, MarketSurface.g, MarketSurface.b, 0.76f));
                DrawHeader(layout);
                DrawWatchlist(ToRect(layout.Watchlist));
                DrawPrimaryPanel(ToRect(layout.Chart));
                DrawActivityPanel(ToRect(layout.Activity));
                DrawOrderBook(ToRect(layout.OrderBook));
                DrawOrderWorkspace(ToRect(layout.OrderTicket));
                ConsumeModalInput();
            }
            finally
            {
                GUI.matrix = previousMatrix;
                FlushRuntimeToGameState();
            }
        }

        private void DrawHeader(StockMarketLandscapeLayout layout)
        {
            var rect = ToRect(layout.Header);
            DrawSolid(
                rect,
                _skinTexture == null
                    ? PanelSurface
                    : new Color(PanelSurface.r, PanelSurface.g, PanelSurface.b, 0.90f));
            DrawSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), MarketLine);
            GUI.Label(new Rect(24f, 13f, 280f, 34f), "가족회사 주식시장", _titleStyle);

            var tradingDay = MarketTradingCalendar.IsTradingDay(CurrentDate);
            var clock = MarketSessionClock.At(_marketMinute, tradingDay);
            DrawSolid(new Rect(25f, 54f, 8f, 8f), clock.Tradable ? PositiveGreen : Html("9AA3B1"));
            GUI.Label(
                new Rect(41f, 45f, 310f, 28f),
                $"{clock.Label} · {MarketSessionClock.TimeLabel(_marketMinute)}",
                _smallStyle);

            var selected = SelectedSecurity;
            if (selected != null)
            {
                GUI.Label(
                    new Rect(365f, 12f, 410f, 32f),
                    FitSingleLine(selected.DisplayNameKo, _headingStyle, 410f),
                    _headingStyle);
                GUI.Label(new Rect(365f, 46f, 410f, 24f), $"{selected.Exchange} · {selected.Ticker}", _mutedStyle);
                var delta = _lastTradePrice - _previousClose;
                var color = PriceColor(delta);
                var priceStyle = new GUIStyle(_headingStyle) { normal = { textColor = color }, alignment = TextAnchor.UpperRight };
                GUI.Label(new Rect(760f, 10f, 250f, 32f), $"{_lastTradePrice:N0}원", priceStyle);
                var rate = MarketOrderBookRules.PriceChangePercent(_lastTradePrice, _previousClose);
                var rateStyle = new GUIStyle(_smallStyle) { normal = { textColor = color }, alignment = TextAnchor.UpperRight };
                GUI.Label(new Rect(760f, 47f, 250f, 24f), $"{Signed(delta)} · {SignedPercent(rate)}", rateStyle);
            }

            var controlsX = layout.CanvasWidth - 530f;
            for (var index = 0; index < PlaybackLabels.Length; index += 1)
            {
                var capture = index;
                if (DrawButton(
                        new Rect(controlsX + index * 62f, 22f, 56f, 42f),
                        PlaybackLabels[index],
                        _playbackIndex == index,
                        MarketAccent))
                {
                    _playbackIndex = capture;
                    _realtimeClock.Reset();
                }
            }
            GUI.Label(
                new Rect(layout.CanvasWidth - 510f, 4f, 252f, 18f),
                $"회사 증권 예수금 {(_runtimeSession?.BrokerageCash ?? 0L):N0}원",
                _tinyStyle);
            GUI.Label(
                new Rect(controlsX, 66f, 300f, 16f),
                "현실 1초 기준 · 게임 5/15/50분",
                _tinyStyle);
            if (DrawButton(new Rect(layout.CanvasWidth - 118f, 22f, 94f, 42f), "F3 닫기", false)) CloseNow();
        }

        private void DrawWatchlist(Rect rect)
        {
            DrawPanel(rect);
            var x = rect.x + 14f;
            var width = rect.width - 28f;
            DrawThreeTabs(
                new Rect(x, rect.y + 14f, width, 40f),
                new[] { "홈", "종목", "내 투자" },
                (int)_primarySection,
                value => _primarySection = (PrimarySection)value);

            switch (_primarySection)
            {
                case PrimarySection.Home:
                    DrawMarketHome(new Rect(x, rect.y + 72f, width, rect.height - 88f));
                    break;
                case PrimarySection.Account:
                    DrawAccountSummary(new Rect(x, rect.y + 72f, width, rect.height - 88f));
                    break;
                default:
                    DrawExploreList(new Rect(x, rect.y + 68f, width, rect.height - 82f));
                    break;
            }
        }

        private void DrawMarketHome(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "오늘의 시장", _headingStyle);
            var labels = new[] { "유가 종합", "코스닥 종합", "거래대금" };
            var values = new[]
            {
                AggregateChangeRate(growthMarket: false),
                AggregateChangeRate(growthMarket: true),
                _runtimeSession == null
                    ? 0d
                    : _securities.Sum(security => _runtimeSession.ViewFor(security.CompanyId).Snapshot.TurnoverEok)
            };
            for (var index = 0; index < labels.Length; index += 1)
            {
                var card = new Rect(rect.x, rect.y + 44f + index * 86f, rect.width, 72f);
                DrawSolid(card, MarketSurface);
                GUI.Label(new Rect(card.x + 12f, card.y + 10f, 145f, 22f), labels[index], _bodyStyle);
                var style = new GUIStyle(_numberStyle)
                {
                    normal = { textColor = index == 2 ? MarketInk : PriceColor(values[index]) }
                };
                GUI.Label(
                    new Rect(card.x + 160f, card.y + 9f, card.width - 172f, 24f),
                    index == 2 ? $"{values[index]:N1}억" : SignedPercent(values[index]),
                    style);
                GUI.Label(new Rect(card.x + 12f, card.y + 40f, card.width - 24f, 20f), "현재 세션 정본 호가 합산", _mutedStyle);
            }
            GUI.Label(new Rect(rect.x, rect.y + 322f, rect.width, 28f), "주요 종목", _headingStyle);
            DrawCompactSecurityRows(new Rect(rect.x, rect.y + 360f, rect.width, rect.height - 360f), 6);
        }

        private void DrawAccountSummary(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "내 투자", _headingStyle);
            DrawMetricCard(new Rect(rect.x, rect.y + 44f, rect.width, 70f), "회사 증권 예수금", $"{(_runtimeSession?.BrokerageCash ?? 0L):N0}원");
            DrawMetricCard(new Rect(rect.x, rect.y + 124f, rect.width, 70f), "세션 전체", $"보유 {_runtimeSession?.Positions.Count ?? 0} · 미체결 {_runtimeSession?.PendingOrders.Count ?? 0} · 관심 {_runtimeSession?.FavoriteAssetIds.Count ?? 0}");
            GUI.Label(new Rect(rect.x, rect.y + 212f, rect.width, 26f), "전체 보유 종목", _headingStyle);
            var y = rect.y + 246f;
            var positions = _runtimeSession?.Positions.Take(3).ToArray() ?? Array.Empty<StockMarketPositionView>();
            if (positions.Length == 0)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), "아직 보유 종목이 없습니다.", _mutedStyle);
                y += 40f;
            }
            foreach (var position in positions)
            {
                var security = _securities.FirstOrDefault(item => item.CompanyId == position.AssetId);
                var positionText = $"{security?.DisplayNameKo ?? position.AssetId} · {position.Units:N0}주";
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), FitSingleLine(positionText, _bodyStyle, rect.width), _bodyStyle);
                GUI.Label(new Rect(rect.x, y + 23f, rect.width, 20f), $"수수료 포함 평균 {position.AverageCost:N0}원", _mutedStyle);
                y += 52f;
            }
            GUI.Label(new Rect(rect.x, y + 4f, rect.width, 26f), "전체 미체결", _headingStyle);
            y += 36f;
            var pending = _runtimeSession?.PendingOrders.Take(2).ToArray() ?? Array.Empty<MarketPendingOrder>();
            if (pending.Length == 0)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 20f), "미체결 없음", _mutedStyle);
                y += 30f;
            }
            foreach (var order in pending)
            {
                var security = _securities.FirstOrDefault(item => item.CompanyId == order.AssetId);
                var pendingText = $"{(order.Side == MarketPendingOrderSide.Buy ? "매수" : "매도")} · {security?.DisplayNameKo ?? order.AssetId}";
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), FitSingleLine(pendingText, _bodyStyle, rect.width), _bodyStyle);
                GUI.Label(new Rect(rect.x, y + 21f, rect.width, 18f), $"{order.LimitPrice:N0}원 · 잔 {order.RemainingQuantity:N0}주", _mutedStyle);
                y += 44f;
            }
            GUI.Label(new Rect(rect.x, y + 4f, rect.width, 26f), "세션 거래일지", _headingStyle);
            y += 38f;
            var journal = _runtimeSession?.OrderJournal.Take(3).ToArray() ?? Array.Empty<StockMarketOrderJournalEntry>();
            foreach (var entry in journal)
            {
                var security = _securities.FirstOrDefault(item => item.CompanyId == entry.AssetId);
                var journalText = $"{(entry.IsBuy ? "매수" : "매도")} · {security?.DisplayNameKo ?? entry.AssetId}";
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), FitSingleLine(journalText, _bodyStyle, rect.width), _bodyStyle);
                GUI.Label(new Rect(rect.x, y + 23f, rect.width, 20f), $"{entry.FilledQuantity:N0}/{entry.RequestedQuantity:N0}주 · {MarketSessionClock.TimeLabel(entry.MarketMinute)}", _mutedStyle);
                y += 52f;
            }
        }

        private void DrawExploreList(Rect rect)
        {
            _searchText = GUI.TextField(new Rect(rect.x, rect.y, rect.width, 44f), _searchText, 40, _inputStyle);
            if (string.IsNullOrEmpty(_searchText))
                GUI.Label(new Rect(rect.x + 13f, rect.y + 10f, rect.width - 26f, 22f), "회사명이나 종목코드 검색", _mutedStyle);

            var categoryLabels = new[] { "국내", "유가", "코스닥", "신규", "관심" };
            for (var index = 0; index < categoryLabels.Length; index += 1)
            {
                var capture = index;
                if (DrawButton(
                        new Rect(rect.x + index * (rect.width / 5f), rect.y + 56f, rect.width / 5f - 4f, 36f),
                        categoryLabels[index],
                        _marketCategory == index,
                        MarketAccent))
                    _marketCategory = capture;
            }

            var sortLabels = new[] { "거래대금", "상승", "하락", "이름" };
            for (var index = 0; index < sortLabels.Length; index += 1)
            {
                var capture = index;
                if (DrawButton(
                        new Rect(rect.x + index * (rect.width / 4f), rect.y + 102f, rect.width / 4f - 4f, 36f),
                        sortLabels[index],
                        _marketSort == index,
                        Html("7B8491")))
                    _marketSort = capture;
            }

            GUI.Label(new Rect(rect.x, rect.y + 154f, rect.width, 32f), "실제 역사 상장 종목", _headingStyle);
            var listRect = new Rect(rect.x, rect.y + 198f, rect.width, rect.height - 198f);
            var filtered = FilteredSecurities();
            var contentHeight = Mathf.Max(listRect.height, filtered.Count * 74f);
            _watchlistScroll = GUI.BeginScrollView(listRect, _watchlistScroll, new Rect(0f, 0f, listRect.width - 18f, contentHeight));
            if (filtered.Count == 0)
            {
                GUI.Label(new Rect(8f, 20f, listRect.width - 36f, 50f), _catalogError ?? "검색 결과가 없습니다.", _mutedStyle);
            }
            for (var index = 0; index < filtered.Count; index += 1)
            {
                var security = filtered[index];
                var selected = SelectedSecurity?.CompanyId == security.CompanyId;
                var row = new Rect(0f, index * 74f, listRect.width - 20f, 66f);
                DrawSolid(row, selected ? Html("E7F7F0") : PanelSurface);
                DrawSolid(new Rect(row.x, row.yMax - 1f, row.width, 1f), MarketLine);
                if (GUI.Button(new Rect(row.x, row.y, row.width - 32f, row.height), GUIContent.none, _buttonStyle))
                    SelectSecurity(security.CompanyId);
                var nameWidth = row.width - 145f;
                GUI.Label(
                    new Rect(row.x + 10f, row.y + 8f, nameWidth, 28f),
                    FitSingleLine(security.DisplayNameKo, _bodyStyle, nameWidth),
                    _bodyStyle);
                GUI.Label(new Rect(row.x + 10f, row.y + 39f, row.width - 145f, 20f), $"{security.Exchange} · {security.Ticker}", _tinyStyle);
                var quote = PreviewPriceFor(security, out var close);
                var delta = quote - close;
                var style = new GUIStyle(_numberStyle) { normal = { textColor = PriceColor(delta) } };
                GUI.Label(new Rect(row.x + row.width - 132f, row.y + 8f, 95f, 28f), $"{quote:N0}", style);
                GUI.Label(new Rect(row.x + row.width - 132f, row.y + 38f, 95f, 22f), SignedPercent(MarketOrderBookRules.PriceChangePercent(quote, close)), style);
                var favorite = _runtimeSession?.IsFavorite(security.CompanyId) ?? false;
                if (GUI.Button(
                        new Rect(row.xMax - 30f, row.y + 20f, 26f, 26f),
                        favorite ? "★" : "☆",
                        new GUIStyle(_centerStyle) { normal = { textColor = favorite ? Html("F4A62A") : MarketMuted } }))
                    _runtimeSession?.SetFavorite(security.CompanyId, !favorite);
            }
            GUI.EndScrollView();
        }

        private void DrawPrimaryPanel(Rect rect)
        {
            DrawPanel(rect);
            var tabs = new[] { "호가", "주문", "차트", "정보" };
            for (var index = 0; index < tabs.Length; index += 1)
            {
                var capture = index;
                if (DrawButton(
                        new Rect(rect.x + 14f + index * ((rect.width - 28f) / 4f), rect.y + 12f, (rect.width - 28f) / 4f - 5f, 42f),
                        tabs[index],
                        _detailTab == (DetailTab)index,
                        MarketAccent))
                    _detailTab = (DetailTab)capture;
            }

            var body = new Rect(rect.x + 18f, rect.y + 70f, rect.width - 36f, rect.height - 88f);
            switch (_detailTab)
            {
                case DetailTab.Info:
                    DrawCompanyInfo(body);
                    break;
                case DetailTab.Order:
                    DrawOrderOverview(body);
                    break;
                default:
                    DrawChart(body, _detailTab == DetailTab.Chart);
                    break;
            }
        }

        private void DrawChart(Rect rect, bool expanded)
        {
            var selected = SelectedSecurity;
            if (selected == null)
            {
                GUI.Label(rect, "종목을 선택하세요.", _mutedStyle);
                return;
            }
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), expanded ? "분봉·일봉 차트" : "실시간 가격 흐름", _headingStyle);
            GUI.Label(new Rect(rect.x, rect.y + 32f, rect.width, 22f), "분 · 일 · 주 · 월 · 년", _mutedStyle);
            var chart = new Rect(rect.x, rect.y + 68f, rect.width, rect.height - 88f);
            DrawSolid(chart, Html("FBFCFD"));
            for (var grid = 1; grid < 5; grid += 1)
                DrawSolid(new Rect(chart.x, chart.y + chart.height * grid / 5f, chart.width, 1f), MarketLine);

            var points = _runtimeSession?.PriceHistoryFor(selected.CompanyId, 48)
                ?? Array.Empty<StockMarketPricePoint>();
            if (points.Count == 0)
            {
                GUI.Label(chart, "가격 경로 준비 중", _mutedStyle);
                return;
            }
            var minimum = long.MaxValue;
            var maximum = long.MinValue;
            var market = selected.PriceRuleMarket;
            var tick = MarketPricingRules.TickSize(_previousClose, market);
            for (var index = 0; index < points.Count; index += 1)
            {
                var open = index == 0 ? _previousClose : points[index - 1].Price;
                var close = points[index].Price;
                minimum = Math.Min(minimum, Math.Max(1L, Math.Min(open, close) - tick));
                maximum = Math.Max(maximum, Math.Max(open, close) + tick);
            }
            if (maximum <= minimum) maximum = minimum + tick;
            var candleWidth = chart.width / points.Count;
            for (var index = 0; index < points.Count; index += 1)
            {
                var open = index == 0 ? _previousClose : points[index - 1].Price;
                var close = points[index].Price;
                var high = Math.Max(open, close) + tick;
                var low = Math.Max(1L, Math.Min(open, close) - tick);
                var color = close >= open ? UpRed : SellBlue;
                var x = chart.x + index * candleWidth + candleWidth * 0.5f;
                var highY = MapPrice(high, minimum, maximum, chart);
                var lowY = MapPrice(low, minimum, maximum, chart);
                DrawSolid(new Rect(x, highY, 1.5f, Mathf.Max(1f, lowY - highY)), color);
                var openY = MapPrice(open, minimum, maximum, chart);
                var closeY = MapPrice(close, minimum, maximum, chart);
                DrawSolid(new Rect(x - candleWidth * 0.28f, Math.Min(openY, closeY), Mathf.Max(2f, candleWidth * 0.56f), Mathf.Max(2f, Math.Abs(closeY - openY))), color);
            }
            GUI.Label(new Rect(chart.x + 8f, chart.y + 6f, 140f, 20f), $"고가 {maximum:N0}", _tinyStyle);
            GUI.Label(new Rect(chart.x + 8f, chart.yMax - 26f, 140f, 20f), $"저가 {minimum:N0}", _tinyStyle);
        }

        private void DrawCompanyInfo(Rect rect)
        {
            var selected = SelectedSecurity;
            if (selected == null) return;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "기업 정보", _headingStyle);
            DrawMetricCard(new Rect(rect.x, rect.y + 46f, rect.width, 74f), "회사명", selected.DisplayNameKo);
            DrawMetricCard(new Rect(rect.x, rect.y + 132f, rect.width, 74f), "시장·종목코드", $"{selected.Exchange} · {selected.Ticker}");
            DrawMetricCard(new Rect(rect.x, rect.y + 218f, rect.width, 74f), "상장일", selected.ListingDate.ToString("yyyy.MM.dd"));
            DrawMetricCard(new Rect(rect.x, rect.y + 304f, rect.width, 74f), "가격 규칙", $"{selected.PriceRuleMarket} 호가 단위 적용");
            GUI.Label(new Rect(rect.x, rect.y + 402f, rect.width, 90f), "실제 회사 역사와 사건은 Korea History V1의 날짜별 법인명을 사용합니다. 미래 사건은 해당 날짜 전에는 노출하지 않습니다.", new GUIStyle(_bodyStyle) { wordWrap = true });
            var favorite = _runtimeSession?.IsFavorite(selected.CompanyId) ?? false;
            if (DrawButton(
                    new Rect(rect.x, rect.y + 508f, Math.Min(240f, rect.width), 42f),
                    favorite ? "★ 관심종목 해제" : "☆ 관심종목 등록",
                    favorite,
                    Html("F4A62A")))
                _runtimeSession?.SetFavorite(selected.CompanyId, !favorite);
        }

        private void DrawOrderOverview(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "주문 작업공간", _headingStyle);
            GUI.Label(new Rect(rect.x, rect.y + 40f, rect.width, 54f), "SIMUL의 매수·매도·정정/취소·미체결·잔고 순서를 오른쪽 고정 패널에 그대로 펼쳤습니다.", new GUIStyle(_bodyStyle) { wordWrap = true });
            if (_snapshot != null)
            {
                DrawMetricCard(new Rect(rect.x, rect.y + 112f, rect.width, 74f), "체결 강도", $"{_snapshot.TradeStrength:0.0}%");
                DrawMetricCard(new Rect(rect.x, rect.y + 198f, rect.width, 74f), "분당 체결 한도", $"{_snapshot.ExecutionCapacity:N0}주");
                DrawMetricCard(new Rect(rect.x, rect.y + 284f, rect.width, 74f), "호가 내부 깊이", $"매도 {_snapshot.Asks.Count} · 매수 {_snapshot.Bids.Count} (화면 7+7)");
            }
        }

        private void DrawActivityPanel(Rect rect)
        {
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, 180f, 26f), "최근 체결", _headingStyle);
            GUI.Label(new Rect(rect.x + 206f, rect.y + 15f, rect.width - 222f, 22f), "가격 · 수량 · 시간", _mutedStyle);
            var content = new Rect(rect.x + 12f, rect.y + 48f, rect.width - 24f, rect.height - 60f);
            var tape = _runtimeSession?.TradeTape
                .Where(print => print.AssetId == SelectedSecurity?.CompanyId)
                .Take(50)
                .ToArray() ?? Array.Empty<StockMarketTradePrint>();
            var contentHeight = Mathf.Max(content.height, tape.Length * 34f);
            _activityScroll = GUI.BeginScrollView(content, _activityScroll, new Rect(0f, 0f, content.width - 18f, contentHeight));
            if (tape.Length == 0)
                GUI.Label(new Rect(8f, 14f, content.width - 36f, 28f), "체결 대기 · 호가가 움직이면 여기에 기록됩니다", _mutedStyle);
            for (var index = 0; index < tape.Length; index += 1)
            {
                var print = tape[index];
                var y = index * 34f;
                var color = print.IsBuy ? UpRed : SellBlue;
                GUI.Label(new Rect(8f, y + 5f, 82f, 22f), print.IsPlayer ? "내 체결" : print.IsBuy ? "매수체결" : "매도체결", new GUIStyle(_smallStyle) { normal = { textColor = color } });
                GUI.Label(new Rect(100f, y + 5f, 115f, 22f), $"{print.Price:N0}원", _numberStyle);
                GUI.Label(new Rect(225f, y + 5f, 100f, 22f), $"{print.Quantity:N0}주", _numberStyle);
                GUI.Label(new Rect(content.width - 105f, y + 5f, 90f, 22f), MarketSessionClock.TimeLabel(print.MarketMinute), _mutedStyle);
                DrawSolid(new Rect(0f, y + 33f, content.width - 18f, 1f), MarketLine);
            }
            GUI.EndScrollView();
        }

        private void DrawOrderBook(Rect rect)
        {
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 28f), "호가", _headingStyle);
            if (_snapshot == null || SelectedSecurity == null)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 58f, rect.width - 28f, 40f), "호가 데이터 준비 중", _mutedStyle);
                return;
            }

            var range = MarketPricingRules.DailyPriceRange(_previousClose, CurrentDate, SelectedSecurity.PriceRuleMarket);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 46f, rect.width - 28f, 22f), $"체결강도 {_snapshot.TradeStrength:0.0}% · 거래대금 {_snapshot.TurnoverEok:0.0}억", _smallStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 72f, rect.width - 28f, 22f), $"상한가 {range.Upper:N0}  ·  하한가 {range.Lower:N0}", _tinyStyle);

            var tableX = rect.x + 8f;
            var tableWidth = rect.width - 16f;
            var headerY = rect.y + 102f;
            DrawSolid(new Rect(tableX, headerY, tableWidth, 34f), MarketSurface);
            var sideWidth = 126f;
            GUI.Label(new Rect(tableX, headerY + 7f, sideWidth, 20f), "매도잔량", new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = SellBlue } });
            GUI.Label(new Rect(tableX + sideWidth, headerY + 7f, tableWidth - sideWidth * 2f, 20f), "가격", _centerStyle);
            GUI.Label(new Rect(tableX + tableWidth - sideWidth, headerY + 7f, sideWidth, 20f), "매수잔량", new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleLeft, normal = { textColor = UpRed } });

            var cursor = _sweepQueue?.Cursor;
            var sweepStep = cursor?.Step;
            var sweepArrived = cursor != null && cursor.Arrived;
            // While a sweep runs the ladder is anchored to the step being replayed instead of the
            // live touch. Anchoring on the touch is what made the border look frozen: the ladder
            // recentred every frame, so the outlined row was always the same screen row.
            var anchorPrice = sweepStep.HasValue
                ? sweepStep.Value.Price
                : (long?)null;
            var anchorSide = sweepStep.HasValue
                ? sweepStep.Value.Side
                : (MarketOrderBookSide?)null;
            var marketLevels = MarketOrderBookPresentationRules.BuildVisibleLevels(
                _snapshot,
                SelectedSecurity.PriceRuleMarket,
                MarketOrderBookReplayQueue.VisibleRowsPerSide,
                marketLevels: null,
                // A level the sweep just emptied still has to render as a row for the arrived frame.
                preserveEmptyMarketLevelPrices: sweepStep.HasValue,
                touchReferencePrice: anchorPrice,
                touchReferenceSide: anchorSide);
            var playerOrders = _runtimeSession?.PendingOrders
                .Where(order => order.AssetId == SelectedSecurity.CompanyId)
                .ToArray() ?? Array.Empty<MarketPendingOrder>();
            var levels = MarketOrderBookPresentationRules.WithPlayerOrders(
                marketLevels,
                playerOrders);
            // SIMUL's rule: the active replay targets its exact execution row, and
            // CentralOutlineLevel is only the idle fallback.
            var outline = sweepStep.HasValue && sweepArrived
                ? FindLevel(levels, sweepStep.Value.Side, sweepStep.Value.Price)
                : null;
            if (outline == null)
            {
                outline = MarketOrderBookPresentationRules.CentralOutlineLevel(
                    levels,
                    sweepStep?.Price ?? _lastTradePrice,
                    sweepStep?.Side ?? _lastTradeLevelSide);
            }

            // The price the player picked keeps its own border, so it stays visible as the ladder
            // scrolls under it rather than being lost the moment the price moves.
            long? selectedPrice = null;
            if (!_marketOrder &&
                long.TryParse(_priceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var typed) &&
                typed > 0)
                selectedPrice = typed;
            var motionNow = Time.unscaledTime;
            _rowMotion.Sweep(motionNow);
            // Same choice the source makes: the draining row animates over the sweep step, so a
            // faster playback speed drains proportionally faster.
            var sweepStepSeconds = _sweepQueue == null
                ? OrderBookRowMotion.MotionSeconds
                : Math.Max(
                    0.001f,
                    _sweepQueue.CurrentPhaseDurationMicroseconds / 1_000_000f);
            var maxDepth = Math.Max(1, levels.Count == 0
                ? 1
                : levels.Max(level => level.Quantity + PlayerQuantityAt(level, playerOrders)));
            var rowHeight = 48f;
            var rowsY = headerY + 34f;
            for (var index = 0; index < levels.Count; index += 1)
            {
                var level = levels[index];
                var row = new Rect(tableX, rowsY + index * rowHeight, tableWidth, rowHeight);
                var ask = level.Side == MarketOrderBookSide.Ask;
                var playerQuantity = PlayerQuantityAt(level, playerOrders);
                var displayedQuantity = level.Quantity + playerQuantity;
                DrawSolid(row, PanelSurface);
                var priceCell = new Rect(tableX + sideWidth, row.y, tableWidth - sideWidth * 2f, row.height);
                DrawSolid(priceCell, ask ? AskTint : BidTint);
                if (selectedPrice.HasValue && level.Price == selectedPrice.Value)
                    DrawSolid(priceCell, SelectedQuoteFill);
                if (sweepArrived && sweepStep.HasValue &&
                    sweepStep.Value.Side == level.Side && sweepStep.Value.Price == level.Price)
                {
                    // 420ms ease-out fade from 0.58 alpha, as in the source screen.
                    var flashProgress = Mathf.Clamp01(_sweepFlashSeconds / SweepFlashSeconds);
                    var flashAlpha = 0.58f * (1f - Mathf.Pow(flashProgress, 3f)) * 0.42f;
                    if (flashAlpha > 0.001f)
                    {
                        var flashColor = ask ? SellBlue : UpRed;
                        DrawSolid(priceCell, new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha));
                    }
                }

                DrawSolid(new Rect(row.x, row.yMax - 1f, row.width, 1f), MarketLine);
                var isTradeDrain = sweepArrived && sweepStep.HasValue &&
                    sweepStep.Value.Side == level.Side && sweepStep.Value.Price == level.Price;
                var depthTarget = Mathf.Clamp01(displayedQuantity / (float)maxDepth);
                _rowMotion.Observe(
                    level.Side,
                    level.Price,
                    displayedQuantity,
                    isTradeDrain,
                    depthTarget,
                    // The draining level tracks the sweep step; every other row uses the ordinary
                    // quote motion, which is how the source picks between the two.
                    isTradeDrain ? sweepStepSeconds : OrderBookRowMotion.MotionSeconds,
                    motionNow,
                    Time.unscaledDeltaTime);
                var depthWidth = sideWidth * _rowMotion.DepthFor(level.Side, level.Price, depthTarget);
                if (ask)
                    DrawSolid(new Rect(tableX + sideWidth - depthWidth, row.y + 5f, depthWidth, row.height - 10f), new Color(0.55f, 0.72f, 0.95f, 0.45f));
                else
                    DrawSolid(new Rect(tableX + tableWidth - sideWidth, row.y + 5f, depthWidth, row.height - 10f), new Color(0.94f, 0.60f, 0.72f, 0.45f));

                if (ask)
                    GUI.Label(new Rect(tableX + 5f, row.y + 13f, sideWidth - 10f, 22f), displayedQuantity > 0 ? $"{displayedQuantity:N0}" : "-", new GUIStyle(_numberStyle) { alignment = TextAnchor.MiddleRight });
                else
                    GUI.Label(new Rect(tableX + tableWidth - sideWidth + 6f, row.y + 13f, sideWidth - 10f, 22f), displayedQuantity > 0 ? $"{displayedQuantity:N0}" : "-", new GUIStyle(_numberStyle) { alignment = TextAnchor.MiddleLeft });
                var deltaLabel = _rowMotion.DeltaLabel(level.Side, level.Price, motionNow);
                if (deltaLabel.Length > 0)
                {
                    var deltaColor = _rowMotion.DeltaIsIncrease(level.Side, level.Price)
                        ? QuantityDeltaUp
                        : QuantityDeltaTrade;
                    var deltaRect = ask
                        ? new Rect(tableX + 5f, row.y + 31f, sideWidth - 10f, 14f)
                        : new Rect(tableX + tableWidth - sideWidth + 6f, row.y + 31f, sideWidth - 10f, 14f);
                    GUI.Label(
                        deltaRect,
                        deltaLabel,
                        new GUIStyle(_tinyStyle)
                        {
                            alignment = ask ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                            normal = { textColor = deltaColor }
                        });
                }
                if (playerQuantity > 0)
                {
                    var markerRect = ask
                        ? new Rect(tableX + 5f, row.y + 2f, sideWidth - 10f, 13f)
                        : new Rect(tableX + tableWidth - sideWidth + 6f, row.y + 2f, sideWidth - 10f, 13f);
                    GUI.Label(markerRect, ask ? $"◆매도 {playerQuantity:N0}주" : $"◆매수 {playerQuantity:N0}주", _tinyStyle);
                }

                var delta = level.Price - _previousClose;
                var priceStyle = new GUIStyle(_numberStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = PriceColor(delta) } };
                var priceRect = new Rect(tableX + sideWidth, row.y, tableWidth - sideWidth * 2f, row.height);
                if (GUI.Button(priceRect, $"{level.Price:N0}원\n{Math.Abs(MarketOrderBookRules.PriceChangePercent(level.Price, _previousClose)):0.00}%", priceStyle))
                {
                    _priceText = level.Price.ToString(CultureInfo.InvariantCulture);
                    _marketOrder = false;
                    _detailTab = DetailTab.Order;
                    _orderSection = ask ? OrderSection.Buy : OrderSection.Sell;
                    _orderNotice = $"{level.Price:N0}원을 지정가로 선택했습니다.";
                }
                if (selectedPrice.HasValue && level.Price == selectedPrice.Value)
                    DrawOutline(priceRect, SelectedQuoteGold, 2f);
                if (outline != null && outline.Side == level.Side && outline.Price == level.Price)
                    DrawOutline(priceRect, outline.Side == MarketOrderBookSide.Ask ? SellBlue : UpRed, 2f);
            }

            var footerY = rowsY + levels.Count * rowHeight + 8f;
            GUI.Label(new Rect(rect.x + 14f, footerY, rect.width - 28f, 22f), $"공개 매도잔량 {_snapshot.TotalAskQuantity:N0}주", new GUIStyle(_smallStyle) { normal = { textColor = SellBlue } });
            GUI.Label(new Rect(rect.x + 14f, footerY + 25f, rect.width - 28f, 22f), $"공개 매수잔량 {_snapshot.TotalBidQuantity:N0}주", new GUIStyle(_smallStyle) { normal = { textColor = UpRed } });
            GUI.Label(new Rect(rect.x + 14f, footerY + 52f, rect.width - 28f, 32f), "내부 10단계 체결 · 화면 7매도+7매수", _tinyStyle);
        }

        private void DrawOrderWorkspace(Rect rect)
        {
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 28f), "주문", _headingStyle);
            var labels = new[] { "매수", "매도", "정정/취소", "미체결", "잔고" };
            var tabWidth = (rect.width - 20f) / labels.Length;
            for (var index = 0; index < labels.Length; index += 1)
            {
                var capture = index;
                var accent = index == 0 ? UpRed : index == 1 ? SellBlue : MarketAccent;
                if (DrawButton(
                        new Rect(rect.x + 10f + index * tabWidth, rect.y + 50f, tabWidth - 3f, 38f),
                        labels[index],
                        _orderSection == (OrderSection)index,
                        accent))
                    _orderSection = (OrderSection)capture;
            }

            var body = new Rect(rect.x + 18f, rect.y + 106f, rect.width - 36f, rect.height - 124f);
            if (_orderSection == OrderSection.Buy || _orderSection == OrderSection.Sell)
                DrawOrderForm(body, _orderSection == OrderSection.Buy);
            else if (_orderSection == OrderSection.Balance)
                DrawOrderBalance(body);
            else
                DrawPendingOrders(body, _orderSection == OrderSection.AmendCancel);
        }

        private void DrawOrderForm(Rect rect, bool isBuy)
        {
            var actionColor = isBuy ? UpRed : SellBlue;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), isBuy ? "매수 주문" : "매도 주문", new GUIStyle(_headingStyle) { normal = { textColor = actionColor } });
            var clock = MarketSessionClock.At(_marketMinute, MarketTradingCalendar.IsTradingDay(CurrentDate));
            var orderSessionLabel = clock.Phase == MarketSessionPhase.OpeningTransition
                ? $"개장 동시호가 접수 · 주문 가능 {(_runtimeSession?.AvailableBrokerageCash ?? 0L):N0}원"
                : $"회사 주문 가능 {(_runtimeSession?.AvailableBrokerageCash ?? 0L):N0}원";
            GUI.Label(new Rect(rect.x, rect.y + 38f, rect.width, 22f), orderSessionLabel, _smallStyle);

            if (DrawButton(new Rect(rect.x, rect.y + 76f, rect.width * 0.5f - 5f, 40f), "지정가", !_marketOrder, actionColor)) _marketOrder = false;
            if (DrawButton(new Rect(rect.x + rect.width * 0.5f + 5f, rect.y + 76f, rect.width * 0.5f - 5f, 40f), "시장가", _marketOrder, actionColor)) _marketOrder = true;

            GUI.Label(new Rect(rect.x, rect.y + 137f, rect.width, 22f), "주문 가격", _bodyStyle);
            var priceField = new Rect(rect.x, rect.y + 165f, rect.width, 43f);
            if (_marketOrder)
            {
                DrawSolid(priceField, MarketSurface);
                DrawOutline(priceField, MarketLine, 1f);
                GUI.Label(
                    new Rect(rect.x + 12f, rect.y + 176f, rect.width - 24f, 22f),
                    clock.Phase == MarketSessionPhase.OpeningTransition
                        ? "09:00 시초가에서 동시호가 체결 판정"
                        : "보이는 호가부터 즉시 체결",
                    _mutedStyle);
            }
            else
            {
                _priceText = GUI.TextField(priceField, _priceText, 16, _inputStyle);
            }

            GUI.Label(new Rect(rect.x, rect.y + 227f, rect.width, 22f), "주문 수량", _bodyStyle);
            _quantityText = GUI.TextField(new Rect(rect.x, rect.y + 255f, rect.width, 43f), _quantityText, 12, _inputStyle);
            var percents = StockMarketQuantityShortcutRules.Percentages;
            for (var index = 0; index < percents.Count; index += 1)
            {
                if (DrawButton(new Rect(rect.x + index * (rect.width / 4f), rect.y + 309f, rect.width / 4f - 4f, 34f), $"{percents[index]}%", false))
                    FillQuantityPercent(isBuy, percents[index]);
            }

            var parsedPrice = ParsePrice(_marketOrder, isBuy);
            var parsedQuantity = ParseQuantity();
            var notional = parsedPrice > 0 && parsedQuantity > 0 ? parsedPrice * (long)parsedQuantity : 0L;
            DrawMetricCard(new Rect(rect.x, rect.y + 365f, rect.width, 66f), "주문 금액", $"{notional:N0}원");
            DrawMetricCard(new Rect(rect.x, rect.y + 442f, rect.width, 66f), "수수료", $"{MarketTradingCosts.TradingFee(CurrentDate, notional):N0}원");
            if (!isBuy) DrawMetricCard(new Rect(rect.x, rect.y + 519f, rect.width, 66f), "거래세", $"{MarketTradingCosts.SecuritiesTransactionTax(CurrentDate, notional):N0}원");

            var noticeY = isBuy ? rect.y + 530f : rect.y + 607f;
            GUI.Label(new Rect(rect.x, noticeY, rect.width, 58f), _orderNotice, new GUIStyle(_smallStyle) { wordWrap = true });
            if (DrawButton(new Rect(rect.x, noticeY + 68f, rect.width, 50f), isBuy ? "매수 주문" : "매도 주문", true, actionColor))
                ValidateAndStageOrder(isBuy);
            GUI.Label(new Rect(rect.x, noticeY + 130f, rect.width, 52f), "주문·체결·미체결 예약금은 회사 증권계좌와 Save에 즉시 반영됩니다. 입출금은 잔고 탭에서 처리합니다.", new GUIStyle(_tinyStyle) { wordWrap = true });
        }

        private void DrawPendingOrders(Rect rect, bool correctionMode)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), correctionMode ? "정정/취소" : "미체결 주문", _headingStyle);
            var pending = _runtimeSession?.PendingOrders.Reverse().ToArray() ?? Array.Empty<MarketPendingOrder>();
            if (pending.Length == 0)
            {
                GUI.Label(new Rect(rect.x, rect.y + 54f, rect.width, 48f), "미체결 주문이 없습니다.", _mutedStyle);
                _amendOrderId = string.Empty;
                return;
            }

            var y = rect.y + 52f;
            if (correctionMode && !string.IsNullOrEmpty(_amendOrderId))
            {
                var selectedOrder = pending.FirstOrDefault(order => order.Id == _amendOrderId);
                if (selectedOrder == null)
                {
                    _amendOrderId = string.Empty;
                }
                else
                {
                    var security = _securities.FirstOrDefault(item => item.CompanyId == selectedOrder.AssetId);
                    DrawSolid(new Rect(rect.x, y, rect.width, 168f), MarketSurface);
                    GUI.Label(new Rect(rect.x + 10f, y + 8f, rect.width - 20f, 22f), $"정정 · {security?.DisplayNameKo ?? selectedOrder.AssetId}", _bodyStyle);
                    GUI.Label(new Rect(rect.x + 10f, y + 36f, rect.width * 0.5f - 15f, 20f), "새 가격", _mutedStyle);
                    GUI.Label(new Rect(rect.x + rect.width * 0.5f + 5f, y + 36f, rect.width * 0.5f - 15f, 20f), "새 잔여수량", _mutedStyle);
                    _amendPriceText = GUI.TextField(
                        new Rect(rect.x + 10f, y + 59f, rect.width * 0.5f - 15f, 38f),
                        _amendPriceText,
                        16,
                        _inputStyle);
                    _amendQuantityText = GUI.TextField(
                        new Rect(rect.x + rect.width * 0.5f + 5f, y + 59f, rect.width * 0.5f - 15f, 38f),
                        _amendQuantityText,
                        12,
                        _inputStyle);
                    if (DrawButton(new Rect(rect.x + 10f, y + 110f, rect.width - 110f, 40f), "기존 취소 후 신규 FIFO 정정", true, MarketAccent))
                        SubmitAmendment();
                    if (DrawButton(new Rect(rect.xMax - 90f, y + 110f, 80f, 40f), "닫기", false))
                        _amendOrderId = string.Empty;
                    y += 182f;
                }
            }

            foreach (var order in pending.Take(correctionMode ? 6 : 9))
            {
                var security = _securities.FirstOrDefault(item => item.CompanyId == order.AssetId);
                var row = new Rect(rect.x, y, rect.width, 76f);
                DrawSolid(row, MarketSurface);
                GUI.Label(new Rect(row.x + 10f, row.y + 8f, row.width - (correctionMode ? 148f : 20f), 22f), $"{(order.Side == MarketPendingOrderSide.Buy ? "매수" : "매도")} · {security?.DisplayNameKo ?? order.AssetId}", _bodyStyle);
                GUI.Label(new Rect(row.x + 10f, row.y + 36f, row.width - (correctionMode ? 148f : 20f), 20f), $"{order.LimitPrice:N0}원 · 잔 {order.RemainingQuantity:N0}주", _mutedStyle);
                if (correctionMode && DrawButton(new Rect(row.xMax - 136f, row.y + 19f, 62f, 36f), "정정", false))
                {
                    _amendOrderId = order.Id;
                    _amendPriceText = order.LimitPrice.ToString(CultureInfo.InvariantCulture);
                    _amendQuantityText = ((int)Math.Floor(order.RemainingQuantity)).ToString(CultureInfo.InvariantCulture);
                }
                if (correctionMode && DrawButton(new Rect(row.xMax - 68f, row.y + 19f, 58f, 36f), "취소", false))
                {
                    _orderNotice = _runtimeSession != null && _runtimeSession.CancelPendingOrder(order.Id)
                        ? "주문을 취소했습니다. 예약금·예약수량을 해제했습니다."
                        : "주문 취소에 실패했습니다.";
                    if (_amendOrderId == order.Id) _amendOrderId = string.Empty;
                }
                y += 86f;
            }
        }

        private void SubmitAmendment()
        {
            if (_runtimeSession == null || string.IsNullOrEmpty(_amendOrderId)) return;
            var priceParsed = long.TryParse(
                _amendPriceText.Replace(",", string.Empty),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var price);
            var quantityParsed = int.TryParse(
                _amendQuantityText.Replace(",", string.Empty),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var quantity);
            if (!priceParsed || !quantityParsed || price <= 0 || quantity <= 0)
            {
                _orderNotice = "정정 가격과 잔여수량을 확인하세요.";
                return;
            }
            var result = _runtimeSession.AmendPendingOrder(
                _amendOrderId,
                price,
                quantity);
            _orderNotice = result.Accepted
                ? $"정정 완료 · {result.Message} · 새 주문은 FIFO 맨 뒤에 배치됩니다."
                : result.Message;
            if (result.Accepted) _amendOrderId = string.Empty;
            SyncSelectedRuntimeView();
        }

        private void DrawOrderBalance(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 30f), "잔고", _headingStyle);
            DrawMetricCard(new Rect(rect.x, rect.y + 48f, rect.width, 72f), "회사 현금", $"{(_boundGameState?.Company.CashWon ?? 0L):N0}원");
            DrawMetricCard(new Rect(rect.x, rect.y + 132f, rect.width, 72f), "증권 예수금", $"{(_runtimeSession?.BrokerageCash ?? 0L):N0}원");
            DrawMetricCard(new Rect(rect.x, rect.y + 216f, rect.width, 72f), "주문 가능", $"{(_runtimeSession?.AvailableBrokerageCash ?? 0L):N0}원");
            var selected = SelectedSecurity;
            var owned = selected == null ? 0 : _runtimeSession?.PositionUnits(selected.CompanyId) ?? 0;
            var average = selected == null ? 0d : _runtimeSession?.AverageCost(selected.CompanyId) ?? 0d;
            DrawMetricCard(new Rect(rect.x, rect.y + 300f, rect.width, 72f), "보유 수량", $"{owned:N0}주");
            DrawMetricCard(new Rect(rect.x, rect.y + 384f, rect.width, 72f), "평균 매입가", $"{average:N0}원");
            DrawMetricCard(new Rect(rect.x, rect.y + 468f, rect.width, 72f), "미체결", $"{_runtimeSession?.PendingOrders.Count ?? 0}건");

            GUI.Label(new Rect(rect.x, rect.y + 558f, rect.width, 24f), "회사계좌 이체", _headingStyle);
            _transferAmountText = GUI.TextField(
                new Rect(rect.x, rect.y + 592f, rect.width, 43f),
                _transferAmountText,
                19,
                _inputStyle);
            var halfWidth = rect.width * 0.5f - 5f;
            if (DrawButton(new Rect(rect.x, rect.y + 647f, halfWidth, 42f), "증권계좌 입금", true, UpRed))
                SubmitBrokerageTransfer(true);
            if (DrawButton(new Rect(rect.x + halfWidth + 10f, rect.y + 647f, halfWidth, 42f), "회사계좌 출금", true, SellBlue))
                SubmitBrokerageTransfer(false);
            GUI.Label(
                new Rect(rect.x, rect.y + 700f, rect.width, 64f),
                _transferNotice,
                new GUIStyle(_tinyStyle) { wordWrap = true });
        }

        private void SubmitBrokerageTransfer(bool companyToBrokerage)
        {
            if (_boundGameState == null || _runtimeSession == null || _transferService == null)
            {
                _transferNotice = "회사 증권계좌를 불러오지 못했습니다.";
                return;
            }

            if (!long.TryParse(
                    _transferAmountText.Replace(",", string.Empty),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var amountWon) || amountWon <= 0)
            {
                _transferNotice = "이체 금액은 1원 이상의 정수 원으로 입력하세요.";
                return;
            }

            var result = companyToBrokerage
                ? _transferService.Deposit(_boundGameState.Time.ElapsedMinutes, amountWon)
                : _transferService.Withdraw(_boundGameState.Time.ElapsedMinutes, amountWon);
            _transferNotice = result.Accepted
                ? $"{(companyToBrokerage ? "입금" : "출금")} 완료 · {amountWon:N0}원 · 회사 현금 {result.CompanyCashWon:N0}원 · 증권 예수금 {result.BrokerageCashWon:N0}원"
                : TransferRejectionMessage(result.RejectionReason);
            FlushRuntimeToGameState();
        }

        private static string TransferRejectionMessage(BrokerageTransferRejectionReason reason)
        {
            switch (reason)
            {
                case BrokerageTransferRejectionReason.InsufficientCompanyCash:
                    return "회사 현금이 부족합니다.";
                case BrokerageTransferRejectionReason.InsufficientAvailableBrokerageCash:
                    return "미체결 매수 예약금을 제외한 출금 가능 예수금이 부족합니다.";
                case BrokerageTransferRejectionReason.DuplicateTransaction:
                    return "이미 처리된 이체입니다.";
                case BrokerageTransferRejectionReason.Overflow:
                    return "이체 후 금액이 long 원 범위를 벗어납니다.";
                default:
                    return "이체 금액을 확인하세요.";
            }
        }

        private void ValidateAndStageOrder(bool isBuy)
        {
            if (_snapshot == null || SelectedSecurity == null || _runtimeSession == null) return;
            var quantity = ParseQuantity();
            if (quantity <= 0)
            {
                _orderNotice = "주문 수량을 1주 이상 입력하세요.";
                return;
            }
            var price = ParsePrice(_marketOrder, isBuy);
            var range = MarketPricingRules.DailyPriceRange(_previousClose, CurrentDate, SelectedSecurity.PriceRuleMarket);
            if (price <= 0 || price < range.Lower || price > range.Upper ||
                !MarketPricingRules.IsValidOrderPrice(price, SelectedSecurity.PriceRuleMarket))
            {
                _orderNotice = $"오늘 주문 범위 {range.Lower:N0}~{range.Upper:N0}원과 호가 단위를 확인하세요.";
                return;
            }
            var result = _runtimeSession.PlaceOrder(
                SelectedSecurity.CompanyId,
                isBuy,
                _marketOrder,
                price,
                quantity);
            _orderNotice = result.Accepted && result.FilledQuantity > 0
                ? $"{result.Message} · 평균 {result.AveragePrice:N0}원"
                : result.Message;
            SyncSelectedRuntimeView();
        }

        private void FillQuantityPercent(bool isBuy, int percent)
        {
            var selected = SelectedSecurity;
            if (selected == null || _runtimeSession == null)
            {
                _quantityText = "0";
                return;
            }
            var maximum = _runtimeSession.MaximumOrderQuantity(
                selected.CompanyId,
                isBuy,
                _marketOrder,
                ParsePrice(_marketOrder, isBuy));
            var quantity = StockMarketQuantityShortcutRules.QuantityFor(maximum, percent);
            _quantityText = Math.Max(0, quantity).ToString(CultureInfo.InvariantCulture);
        }

        private long ParsePrice(bool marketOrder, bool isBuy)
        {
            if (marketOrder && SelectedSecurity != null)
            {
                var range = MarketPricingRules.DailyPriceRange(_previousClose, CurrentDate, SelectedSecurity.PriceRuleMarket);
                return isBuy ? range.Upper : range.Lower;
            }
            return long.TryParse(_priceText.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0L;
        }

        private int ParseQuantity()
        {
            return int.TryParse(_quantityText.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0, value)
                : 0;
        }

        private void RefreshSecurities()
        {
            var selectedId = SelectedSecurity?.CompanyId;
            _securities.Clear();
            _knownBrokerageAssetIds.Clear();
            _catalogError = null;
            try
            {
                if (_catalog == null) _catalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
                if (_catalog == null || !_catalog.IsConfigured)
                    throw new InvalidOperationException("Korea History V1 카탈로그가 씬에 없습니다.");
                foreach (var company in _catalog.Registry.Companies)
                {
                    if (company.ListingHistory.Any(listing => listing.IsDomesticExchange))
                        _knownBrokerageAssetIds.Add(company.CompanyId);
                }
                _securities.AddRange(_catalog.ListedSecuritiesAt(CurrentDate));
                _securities.Sort((left, right) => string.Compare(left.DisplayNameKo, right.DisplayNameKo, StringComparison.Ordinal));
            }
            catch (Exception exception)
            {
                _catalogError = exception.Message;
            }

            _selectedSecurityIndex = 0;
            if (!string.IsNullOrEmpty(selectedId))
            {
                var restored = _securities.FindIndex(item => item.CompanyId == selectedId);
                if (restored >= 0) _selectedSecurityIndex = restored;
            }
        }

        private void SelectSecurity(string companyId)
        {
            var index = _securities.FindIndex(item => item.CompanyId == companyId);
            if (index < 0 || index == _selectedSecurityIndex) return;
            _selectedSecurityIndex = index;
            SyncSelectedRuntimeView();
        }

        private void EnsureRuntimeSession()
        {
            SynchronizeGameStateBinding();
            if (_runtimeSession != null && _runtimeSession.Date == CurrentDate) return;
            if (_securities.Count == 0 || _boundGameState == null) return;

            try
            {
                if (_runtimeSession == null)
                {
                    var binding = StockMarketGameStateBridge.Load(
                        _boundGameState,
                        CurrentDate,
                        _securities,
                        _marketMinute,
                        _knownBrokerageAssetIds);
                    _runtimeSession = binding.Session;
                    RuntimeSessionCreationCountForQa++;
                    _playbackIndex = Mathf.Clamp(binding.PlaybackIndex, 0, PlaybackLabels.Length - 1);
                    _realtimeClock.Restore(binding.RealtimeResidualSeconds);
                }
                else
                {
                    var accountState = _runtimeSession.ExportBrokerageState();
                    var nextSession = new StockMarketRuntimeSession(
                        _boundGameState.WorldSeed,
                        CurrentDate,
                        0L,
                        _securities,
                        _marketMinute,
                        _knownBrokerageAssetIds);
                    if (!nextSession.TryApplyBrokerageState(accountState, out var error))
                        throw new InvalidOperationException($"Trading-date brokerage carry failed: {error}");
                    _runtimeSession = nextSession;
                    RuntimeSessionCreationCountForQa++;
                    _realtimeClock.Reset();
                }

                _transferService = new CompanyBrokerageTransferService(
                    _boundGameState.Company,
                    _runtimeSession);
                if (_runtimeSession.InactivePendingOrderCancellationCount > 0)
                {
                    _orderNotice = $"현재 거래할 수 없는 종목의 미체결 주문 {_runtimeSession.InactivePendingOrderCancellationCount:N0}건을 취소하고 예약금을 해제했습니다.";
                }
                FlushRuntimeToGameState();
            }
            catch (Exception exception)
            {
                _runtimeSession = null;
                _transferService = null;
                _orderNotice = $"회사 증권계좌 복원 실패 · {exception.Message}";
                _transferNotice = _orderNotice;
            }
        }

        private void SynchronizeGameStateBinding()
        {
            var current = _bootstrap?.State;
            if (ReferenceEquals(_boundGameState, current)) return;
            _boundGameState = current;
            _runtimeSession = null;
            _transferService = null;
            _snapshot = null;
            _playbackIndex = 1;
            _realtimeClock.Reset();
        }

        private void FlushRuntimeToGameState()
        {
            if (_runtimeSession == null || _boundGameState == null ||
                !ReferenceEquals(_boundGameState, _bootstrap?.State))
                return;
            StockMarketGameStateBridge.Flush(
                _boundGameState,
                _runtimeSession,
                _realtimeClock.AccumulatedSeconds,
                _playbackIndex);
        }

        private void SyncSelectedRuntimeView()
        {
            EnsureRuntimeSession();
            var selected = SelectedSecurity;
            if (_runtimeSession == null || selected == null)
            {
                _snapshot = null;
                return;
            }
            var view = _runtimeSession.ViewFor(selected.CompanyId);
            _marketMinute = _runtimeSession.MarketMinute;
            _previousClose = view.PreviousClose;
            var previousSnapshot = _snapshot;
            var previousTradePrice = _lastTradePrice;
            _lastTradePrice = view.LastTradePrice;
            _lastTradeLevelSide = view.LastTradeLevelSide;
            _snapshot = view.Snapshot;
            EnqueueSweepForMinute(selected, previousTradePrice, previousSnapshot);
            if (string.IsNullOrWhiteSpace(_priceText))
                _priceText = _lastTradePrice.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Queues one batch per canonical minute per security. The identity carries the day, the
        /// minute, the pulse and the traded price, so re-entering the screen or switching back to a
        /// security never replays a minute that already played.
        /// </summary>
        private void EnqueueSweepForMinute(
            MarketSecurityDefinition selected,
            long previousTradePrice,
            MarketOrderBookSnapshot previousSnapshot)
        {
            if (_runtimeSession == null || selected == null || _lastTradePrice <= 0) return;
            var sessionKey = $"{selected.CompanyId}:{CurrentDate:yyyyMMdd}";
            if (_sweepQueue == null) _sweepQueue = new MarketOrderBookReplayQueue(sessionKey);
            else _sweepQueue.EnsureSession(sessionKey);

            var key = $"{sessionKey}:{_marketMinute}:{_lastTradePrice}";
            if (string.Equals(_sweepPreviousKey, key, StringComparison.Ordinal)) return;
            _sweepPreviousKey = key;

            var reference = previousTradePrice > 0 ? previousTradePrice : _sweepPreviousTradePrice;
            _sweepPreviousTradePrice = _lastTradePrice;
            var batch = MarketOrderBookSweepBuilder.Build(
                selected.CompanyId,
                CurrentDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                _marketMinute,
                _runtimeSession.LiquidityPulse,
                reference,
                _lastTradePrice,
                _lastTradeLevelSide,
                selected.PriceRuleMarket,
                previousSnapshot ?? _snapshot);
            if (batch != null) _sweepQueue.Enqueue(batch);
        }

        private void AdvanceSweepPlayback(float unscaledDeltaSeconds)
        {
            if (_sweepQueue == null) return;
            _sweepQueue.SetPlayback(
                PlaybackAnimationRates[_playbackIndex] <= 0,
                Math.Max(1, PlaybackAnimationRates[_playbackIndex]));
            var previousToken = _sweepFlashToken;
            _sweepQueue.TickMicroseconds((long)(Math.Max(0f, unscaledDeltaSeconds) * 1_000_000d));
            _sweepFlashToken = SweepFlashToken();
            // Restart the fade whenever the cursor lands on a new arrived step, which is the frame
            // the border, the price axis and the tape all move together.
            if (!string.Equals(previousToken, _sweepFlashToken, StringComparison.Ordinal))
                _sweepFlashSeconds = 0f;
            else if (_sweepFlashToken.Length > 0)
                _sweepFlashSeconds += Math.Max(0f, unscaledDeltaSeconds);
        }

        private string SweepFlashToken()
        {
            var cursor = _sweepQueue?.Cursor;
            if (cursor == null || !cursor.Arrived || !cursor.Step.HasValue) return string.Empty;
            return $"{cursor.Batch.Identity}:{cursor.Step.Value.Sequence}";
        }

        private List<MarketSecurityDefinition> FilteredSecurities()
        {
            IEnumerable<MarketSecurityDefinition> query = _securities;
            if (_marketCategory == 1) query = query.Where(item => item.PriceRuleMarket != MarketPricingRules.GrowthMarketName);
            else if (_marketCategory == 2) query = query.Where(item => item.PriceRuleMarket == MarketPricingRules.GrowthMarketName);
            else if (_marketCategory == 3) query = query.OrderByDescending(item => item.ListingDate);
            else if (_marketCategory == 4) query = query.Where(item => _runtimeSession?.IsFavorite(item.CompanyId) ?? false);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var term = _searchText.Trim();
                query = query.Where(item =>
                    item.DisplayNameKo.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Ticker.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Exchange.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            var rows = query.ToList();
            rows.Sort((left, right) =>
            {
                if (_marketSort == 3) return string.Compare(left.DisplayNameKo, right.DisplayNameKo, StringComparison.Ordinal);
                var leftPrice = PreviewPriceFor(left, out var leftClose);
                var rightPrice = PreviewPriceFor(right, out var rightClose);
                if (_marketSort == 1) return MarketOrderBookRules.PriceChangePercent(rightPrice, rightClose).CompareTo(MarketOrderBookRules.PriceChangePercent(leftPrice, leftClose));
                if (_marketSort == 2) return MarketOrderBookRules.PriceChangePercent(leftPrice, leftClose).CompareTo(MarketOrderBookRules.PriceChangePercent(rightPrice, rightClose));
                var leftTurnover = _runtimeSession?.ViewFor(left.CompanyId).Snapshot.TurnoverEok ?? 0d;
                var rightTurnover = _runtimeSession?.ViewFor(right.CompanyId).Snapshot.TurnoverEok ?? 0d;
                return rightTurnover.CompareTo(leftTurnover);
            });
            return rows;
        }

        private void DrawCompactSecurityRows(Rect rect, int maximum)
        {
            var count = Math.Min(maximum, _securities.Count);
            for (var index = 0; index < count; index += 1)
            {
                var security = _securities[index];
                var row = new Rect(rect.x, rect.y + index * 65f, rect.width, 56f);
                DrawSolid(row, MarketSurface);
                if (GUI.Button(row, GUIContent.none, _buttonStyle))
                {
                    _primarySection = PrimarySection.Explore;
                    SelectSecurity(security.CompanyId);
                }
                var compactNameWidth = row.width - 120f;
                GUI.Label(
                    new Rect(row.x + 10f, row.y + 7f, compactNameWidth, 24f),
                    FitSingleLine(security.DisplayNameKo, _bodyStyle, compactNameWidth),
                    _bodyStyle);
                GUI.Label(new Rect(row.x + 10f, row.y + 31f, row.width - 120f, 18f), security.Ticker, _tinyStyle);
                var price = PreviewPriceFor(security, out var close);
                GUI.Label(new Rect(row.x + row.width - 108f, row.y + 15f, 98f, 24f), $"{price:N0}", new GUIStyle(_numberStyle) { normal = { textColor = PriceColor(price - close) } });
            }
        }

        private void DrawMetricCard(Rect rect, string label, string value)
        {
            DrawSolid(rect, MarketSurface);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 20f), label, _mutedStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, rect.height - 39f), value, _bodyStyle);
        }

        private long PreviewPriceFor(MarketSecurityDefinition security, out long close)
        {
            if (_runtimeSession != null)
            {
                var view = _runtimeSession.ViewFor(security.CompanyId);
                close = view.PreviousClose;
                return view.LastTradePrice;
            }
            close = 0L;
            return 0L;
        }

        private double AggregateChangeRate(bool growthMarket)
        {
            if (_runtimeSession == null) return 0d;
            var rates = _securities
                .Where(security =>
                    (security.PriceRuleMarket == MarketPricingRules.GrowthMarketName) == growthMarket)
                .Select(security => _runtimeSession.ViewFor(security.CompanyId))
                .Select(view => MarketOrderBookRules.PriceChangePercent(
                    view.LastTradePrice,
                    view.PreviousClose))
                .ToArray();
            return rates.Length == 0 ? 0d : rates.Average();
        }

        private static int PlayerQuantityAt(
            MarketOrderBookLevel level,
            IEnumerable<MarketPendingOrder> orders)
        {
            var pendingSide = level.Side == MarketOrderBookSide.Ask
                ? MarketPendingOrderSide.Sell
                : MarketPendingOrderSide.Buy;
            return orders
                .Where(order => order.Side == pendingSide && order.LimitPrice == level.Price)
                .Sum(order => (int)Math.Floor(order.RemainingQuantity));
        }

        private MarketSecurityDefinition SelectedSecurity =>
            _selectedSecurityIndex >= 0 && _selectedSecurityIndex < _securities.Count
                ? _securities[_selectedSecurityIndex]
                : null;

        private DateTime CurrentDate => _bootstrap?.State?.Time.Now.Date ?? new DateTime(2000, 1, 3);

        private void DrawPanel(Rect rect)
        {
            DrawSolid(
                rect,
                _skinTexture == null
                    ? PanelSurface
                    : new Color(PanelSurface.r, PanelSurface.g, PanelSurface.b, 0.90f));
            DrawOutline(rect, MarketLine, 1f);
        }

        private void DrawMinimumSizeNotice()
        {
            var cardWidth = Mathf.Min(Screen.width - 32f, 620f);
            var cardHeight = Mathf.Min(Screen.height - 32f, 260f);
            var card = new Rect(
                (Screen.width - cardWidth) * 0.5f,
                (Screen.height - cardHeight) * 0.5f,
                cardWidth,
                cardHeight);
            DrawSolid(card, PanelSurface);
            DrawOutline(card, MarketAccent, 3f);
            GUI.Label(
                new Rect(card.x + 28f, card.y + 26f, card.width - 56f, 36f),
                "주식시장 창 너비가 너무 좁아요",
                _headingStyle);
            GUI.Label(
                new Rect(card.x + 28f, card.y + 78f, card.width - 56f, 86f),
                $"현재 {Screen.width}×{Screen.height}\n최소 {StockMarketLandscapeLayout.MinimumReadablePixelWidth}×{StockMarketLandscapeLayout.MinimumReadablePixelHeight} 이상으로 창을 넓혀 주세요. 주문과 호가가 찌그러지지 않도록 안전하게 표시를 멈췄습니다.",
                new GUIStyle(_bodyStyle) { wordWrap = true });
            if (DrawButton(
                    new Rect(card.x + 28f, card.yMax - 66f, card.width - 56f, 42f),
                    "F3로 닫기",
                    false,
                    MarketAccent))
                CloseNow();
        }

        private void DrawThreeTabs(Rect rect, string[] labels, int selected, Action<int> onSelected)
        {
            var width = rect.width / labels.Length;
            for (var index = 0; index < labels.Length; index += 1)
            {
                var capture = index;
                if (DrawButton(new Rect(rect.x + index * width, rect.y, width - 4f, rect.height), labels[index], selected == index, MarketAccent))
                    onSelected(capture);
            }
        }

        private void SuppressWorldInteraction()
        {
            if (_worldInteractionSuppressed) return;
            _playerController = FindFirstObjectByType<PrototypePlayerController>();
            _workInteractor = FindFirstObjectByType<PlayerOfficeWorkInteractor>();
            _cameraFollow = FindFirstObjectByType<IsometricCameraFollow>();
            if (_playerController != null)
            {
                _playerControllerWasEnabled = _playerController.enabled;
                _playerController.enabled = false;
            }
            if (_workInteractor != null)
            {
                _workInteractorWasEnabled = _workInteractor.enabled;
                _workInteractor.enabled = false;
            }
            if (_cameraFollow != null)
            {
                _cameraFollowWasEnabled = _cameraFollow.enabled;
                _cameraFollow.enabled = false;
            }
            _worldInteractionSuppressed = true;
        }

        private void RestoreWorldInteraction()
        {
            if (!_worldInteractionSuppressed) return;
            if (_playerController != null) _playerController.enabled = _playerControllerWasEnabled;
            if (_workInteractor != null) _workInteractor.enabled = _workInteractorWasEnabled;
            if (_cameraFollow != null) _cameraFollow.enabled = _cameraFollowWasEnabled;
            _playerController = null;
            _workInteractor = null;
            _cameraFollow = null;
            _worldInteractionSuppressed = false;
        }

        /// <summary>
        /// Runs after all stock controls for the current IMGUI event. With this
        /// component executing before the shared HUD, controls receive the event
        /// first and the consumed event cannot click through to Bootstrap UI.
        /// </summary>
        private static void ConsumeModalInput()
        {
            var current = Event.current;
            if (current == null) return;
            switch (current.type)
            {
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseDrag:
                case EventType.ScrollWheel:
                case EventType.ContextClick:
                case EventType.KeyDown:
                case EventType.KeyUp:
                    current.Use();
                    break;
            }
        }

        private bool DrawButton(Rect rect, string label, bool active, Color? accent = null)
        {
            var color = accent ?? MarketAccent;
            var hovered = Event.current != null && rect.Contains(Event.current.mousePosition);
            DrawSolid(
                rect,
                active
                    ? new Color(color.r, color.g, color.b, 0.16f)
                    : hovered ? CreamHighlight : MarketSurface);
            DrawOutline(rect, active ? color : MarketLine, active ? 2f : 1f);
            var style = new GUIStyle(_buttonStyle) { normal = { textColor = active ? color : MarketInk } };
            return GUI.Button(rect, label, style);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { font = _boldKoreanFont, fontSize = 24, fontStyle = FontStyle.Normal, clipping = TextClipping.Clip, normal = { textColor = MarketInk } };
            _headingStyle = new GUIStyle(GUI.skin.label) { font = _boldKoreanFont, fontSize = 20, fontStyle = FontStyle.Normal, clipping = TextClipping.Clip, normal = { textColor = MarketInk }, wordWrap = false };
            _bodyStyle = new GUIStyle(GUI.skin.label) { font = _koreanFont, fontSize = 16, fontStyle = FontStyle.Normal, clipping = TextClipping.Clip, normal = { textColor = MarketInk }, wordWrap = false };
            _smallStyle = new GUIStyle(_bodyStyle) { fontSize = 13, normal = { textColor = MarketMuted } };
            _mutedStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleLeft };
            _tinyStyle = new GUIStyle(_smallStyle) { fontSize = 11, normal = { textColor = Html("8A919E") } };
            _numberStyle = new GUIStyle(_bodyStyle) { font = _boldKoreanFont, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Normal };
            _centerStyle = new GUIStyle(_smallStyle) { font = _boldKoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            _buttonStyle = new GUIStyle(GUI.skin.label) { font = _boldKoreanFont, fontSize = 14, fontStyle = FontStyle.Normal, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleCenter, normal = { textColor = MarketInk } };
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                font = _koreanFont,
                clipping = TextClipping.Clip,
                padding = new RectOffset(12, 12, 10, 8),
                normal = { textColor = MarketInk, background = _whiteTexture },
                hover = { textColor = MarketInk, background = _whiteTexture },
                active = { textColor = MarketInk, background = _whiteTexture },
                focused = { textColor = MarketInk, background = _whiteTexture }
            };
        }

        private bool ValidateTypographyForQa(out string error)
        {
            error = string.Empty;
            if (_koreanFont == null || _boldKoreanFont == null || _fallbackKoreanFont == null ||
                _koreanFont.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) < 0 ||
                _boldKoreanFont.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) < 0 ||
                _fallbackKoreanFont.name.IndexOf("Pretendard", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = "Maplestory primary/bold or Pretendard fallback font is not loaded";
                return false;
            }
            const string requiredKoreanGlyphs = "핸디소프트실제역사상장종목거래대금상승하락이름호가체결";
            foreach (var glyph in requiredKoreanGlyphs)
            {
                if (!_koreanFont.HasCharacter(glyph) || !_boldKoreanFont.HasCharacter(glyph))
                {
                    error = $"Maplestory font is missing required Korean glyph U+{(int)glyph:X4}";
                    return false;
                }
            }
            if (_titleStyle.lineHeight > 34f || _headingStyle.lineHeight > 32f ||
                _bodyStyle.lineHeight > 25f || _tinyStyle.lineHeight > 18f)
            {
                error = "style lineHeight exceeds its smallest allocated text rectangle";
                return false;
            }
            if (_headingStyle.CalcHeight(new GUIContent("실제 역사 상장 종목"), 300f) > 32f ||
                _buttonStyle.CalcHeight(new GUIContent("거래대금"), 70f) > 36f ||
                _bodyStyle.CalcHeight(new GUIContent("핸디소프트"), 155f) > 28f)
            {
                error = "actual Maplestory metrics overflow filter/title/company row rectangles";
                return false;
            }

            var fixedStrings = new[]
            {
                "가족회사 증권 실습", "홈", "종목", "내 투자", "호가", "주문", "차트", "정보",
                "유가", "코스닥", "정정/취소", "미체결", "잔고", "분 · 일 · 주 · 월 · 년",
                "현실 1초 기준 · 게임 5/15/50분"
            };
            foreach (var value in fixedStrings.Concat(_securities.Select(security => security.DisplayNameKo)))
            {
                if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\uFFFD') >= 0 || value.Any(char.IsControl))
                {
                    error = $"invalid display string: {value}";
                    return false;
                }
            }
            var handy = _securities.FirstOrDefault(security => security.CompanyId == "kr_handysoft");
            if (handy != null && handy.DisplayNameKo != "핸디소프트")
            {
                error = $"canonical Handysoft name mismatch: {handy.DisplayNameKo}";
                return false;
            }
            foreach (var security in _securities)
            {
                var fitted = FitSingleLine(security.DisplayNameKo, _bodyStyle, 155f);
                if (_bodyStyle.CalcSize(new GUIContent(fitted)).x > 155.5f)
                {
                    error = $"company name overflows fitted row: {security.DisplayNameKo}";
                    return false;
                }
            }
            return true;
        }

        private bool ValidateOpeningAuctionForQa(out string error)
        {
            error = string.Empty;
            var security = SelectedSecurity;
            if (security == null)
            {
                error = "no security selected";
                return false;
            }

            var session = new StockMarketRuntimeSession(
                _bootstrap.State.WorldSeed,
                CurrentDate,
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute - 1);
            var preOpenView = session.ViewFor(security.CompanyId);
            var range = MarketPricingRules.DailyPriceRange(
                preOpenView.PreviousClose,
                CurrentDate,
                security.PriceRuleMarket);
            var cashBefore = session.BrokerageCash;
            var limit = session.PlaceOrder(security.CompanyId, true, false, range.Lower, 1);
            var market = session.PlaceOrder(security.CompanyId, true, true, 0, 1);
            if (!limit.Accepted || !market.Accepted || limit.PendingOrderId == null ||
                market.PendingOrderId == null || session.PositionUnits(security.CompanyId) != 0 ||
                session.BrokerageCash != cashBefore || session.TradeTape.Count != 0)
            {
                error = "08:59:59 orders were rejected or settled before opening";
                return false;
            }
            if (!session.CancelPendingOrder(limit.PendingOrderId) || session.PendingOrders.Count != 1)
            {
                error = "08:59:59 cancellation did not remove only the selected order";
                return false;
            }
            var preOpenOrders = session.PendingOrders.Count;

            session.AdvanceMinutes(1, 1);
            var openingView = session.ViewFor(security.CompanyId);
            var openingTrades = session.OpeningTradeCountFor(security.CompanyId);
            var openingPrice = session.OpeningPriceFor(security.CompanyId);
            var chartAtOpen = session.PriceHistoryFor(security.CompanyId, 64).Last().Price;
            if (session.OpeningAuctionProcessCount != 1 || openingTrades <= 0 ||
                openingPrice <= 0 || openingView.LastTradePrice != openingPrice ||
                chartAtOpen != openingPrice || session.PositionUnits(security.CompanyId) != 1)
            {
                error = "09:00 opening price, tape, chart, and position were not canonical";
                return false;
            }

            session.SetMarketMinute(MarketSessionClock.OpenMinute);
            if (session.OpeningAuctionProcessCount != 1 ||
                session.OpeningTradeCountFor(security.CompanyId) != openingTrades)
            {
                error = "09:00:01 repeated callback duplicated the opening auction";
                return false;
            }

            var visibleAtOpen = MarketOrderBookPresentationRules.BuildVisibleLevels(
                openingView.Snapshot,
                security.PriceRuleMarket);
            if (visibleAtOpen.Count != 14)
            {
                error = $"09:00 order book exposed {visibleAtOpen.Count} rows instead of 7+7";
                return false;
            }
            var topSevenAsks = string.Join(",", visibleAtOpen.Take(7).Select(level => $"{level.Price}:{level.Quantity}"));
            var topSevenBids = string.Join(",", visibleAtOpen.Skip(7).Take(7).Select(level => $"{level.Price}:{level.Quantity}"));

            session.AdvanceMinutes(1, 1);
            var tapeAtNineOhOne = session.TradeTape.Count(print =>
                print.AssetId == security.CompanyId && print.MarketMinute == MarketSessionClock.OpenMinute + 1);
            if (session.MarketMinute != MarketSessionClock.OpenMinute + 1 || tapeAtNineOhOne <= 0)
            {
                error = "09:01 continuous session did not append its canonical tape";
                return false;
            }

            var speedOpeningPrices = new List<long>();
            foreach (var speed in PlaybackMinutes.Skip(1))
            {
                var candidate = new StockMarketRuntimeSession(
                    _bootstrap.State.WorldSeed,
                    CurrentDate,
                    1_000_000_000,
                    new[] { security },
                    MarketSessionClock.OpenMinute - 1);
                var candidateOrder = candidate.PlaceOrder(security.CompanyId, true, true, 0, 1);
                if (!candidateOrder.Accepted)
                {
                    error = $"{speed}-minute boundary order was rejected";
                    return false;
                }
                candidate.AdvanceMinutes(speed, speed == 5 ? 1 : speed == 15 ? 3 : 10);
                if (candidate.OpeningAuctionProcessCount != 1 ||
                    candidate.PositionUnits(security.CompanyId) != 1)
                {
                    error = $"{speed}-minute boundary skipped or duplicated 09:00";
                    return false;
                }
                speedOpeningPrices.Add(candidate.OpeningPriceFor(security.CompanyId));
            }
            if (speedOpeningPrices.Distinct().Count() != 1)
            {
                error = "5/15/50-minute boundary crossings produced different opening prices";
                return false;
            }

            Debug.Log(
                "FAMILY_COMPANY_STOCK_OPENING_TABLE: PASS\n" +
                $"08:59:59 | phase=개장 준비 | preopenOrders={preOpenOrders} | cashDelta=0 | positionDelta=0 | tape=0\n" +
                $"09:00:00 | openingProcessCount=1 | openingTradeCount={openingTrades} | currentPrice={openingPrice} | chartPrice={chartAtOpen}\n" +
                $"09:00:01 | openingProcessCount={session.OpeningAuctionProcessCount} | duplicateOpening=0\n" +
                $"09:00 top7 asks={topSevenAsks}\n" +
                $"09:00 top7 bids={topSevenBids}\n" +
                $"09:01 | tape={tapeAtNineOhOne} | phase={MarketSessionClock.At(session.MarketMinute, true).Label}\n" +
                $"boundary speeds | 5/15/50 openingPrice={string.Join("/", speedOpeningPrices)}");
            return true;
        }

        private static string FitSingleLine(string value, GUIStyle style, float maximumWidth)
        {
            var text = value ?? string.Empty;
            if (style == null || style.CalcSize(new GUIContent(text)).x <= maximumWidth) return text;
            const string ellipsis = "…";
            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                var candidate = text.Substring(0, middle).TrimEnd() + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= maximumWidth) low = middle;
                else high = middle - 1;
            }
            return text.Substring(0, low).TrimEnd() + ellipsis;
        }

        private void DrawSolid(Rect rect, Color color)
        {
            if (_whiteTexture == null) EnsureStyles();
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTexture);
            GUI.color = previous;
        }

        /// <summary>Trade flash length from the source screen.</summary>
        private const float SweepFlashSeconds = 0.42f;

        /// <summary>Border colour SIMUL uses for the price the player selected.</summary>
        private static readonly Color SelectedQuoteGold = new Color(0.878f, 0.663f, 0f, 1f);

        /// <summary>Plate behind the selected price, #FFF2B8 in the source.</summary>
        private static readonly Color SelectedQuoteFill = new Color(1f, 0.949f, 0.722f, 1f);

        /// <summary>#16794E, a quote that grew.</summary>
        private static readonly Color QuantityDeltaUp = new Color(0.086f, 0.475f, 0.306f, 1f);

        /// <summary>#B42332, depth taken by an execution.</summary>
        private static readonly Color QuantityDeltaTrade = new Color(0.706f, 0.137f, 0.196f, 1f);

        private static MarketOrderBookLevel FindLevel(
            IReadOnlyList<MarketOrderBookLevel> levels,
            MarketOrderBookSide side,
            long price)
        {
            for (var index = 0; index < levels.Count; index += 1)
            {
                var level = levels[index];
                if (level.Side == side && level.Price == price) return level;
            }

            return null;
        }

        private void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static Rect ToRect(StockMarketPanelRect rect) => new Rect(rect.X, rect.Y, rect.Width, rect.Height);

        private static float MapPrice(long value, long minimum, long maximum, Rect chart)
        {
            var normalized = (value - minimum) / (float)Math.Max(1L, maximum - minimum);
            return chart.yMax - 14f - normalized * (chart.height - 28f);
        }

        private static Color PriceColor(double change) => change > 0d ? UpRed : change < 0d ? SellBlue : MarketInk;
        private static string Signed(long value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");
        private static string SignedPercent(double value) => Math.Abs(value) < 0.005d ? "0.00%" : $"{(value > 0d ? "+" : string.Empty)}{value:0.00}%";

        private static Color Html(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.white;
        }
    }
}

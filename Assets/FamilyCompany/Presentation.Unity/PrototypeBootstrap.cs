using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Presentation.Unity.MainNavigation;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.UIRemaster;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public enum PrototypeUiScreen
    {
        MainMenu,
        Playing,
        Management,
        PauseMenu,
        NewGameSlots,
        SaveSlots,
        LoadSlots,
        ConfirmNewGame
    }

    public enum OfficeManagementTab
    {
        Contracts,
        Research,
        Products
    }

    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private const int ReferenceHeight = 1080;
        private static readonly string[] CanonicalFamilyIds =
            { "player", "older_sister", "father", "mother" };
        private GameState _state;
        private SimulationRunner _runner;
        private UnityJsonSaveRepository[] _saveSlots;
        private string _notice = "가족회사에 오신 것을 환영합니다.";
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _slotTextStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _mainTitleStyle;
        private GUIStyle _mainTitleShadowStyle;
        private GUIStyle _mainSubtitleStyle;
        private GUIStyle _mainChipStyle;
        private GUIStyle _mainButtonStyle;
        private GUIStyle _mainButtonPrimaryStyle;
        private GUIStyle _mainButtonDangerStyle;
        private GUIStyle _mainButtonDisabledStyle;
        private GUIStyle _mainButtonTitleStyle;
        private GUIStyle _mainButtonDetailStyle;
        private GUIStyle _mainButtonIndexStyle;
        private GUIStyle _mainInfoStyle;
        private GUIStyle _mainShortcutStyle;
        private GUIStyle _mainCompactTextButtonStyle;
        private GUIStyle _mainCompactDangerButtonStyle;
        private GUIStyle _mainInfoBoxStyle;
        private GUIStyle _mainChipBoxStyle;
        private Texture2D _solidTexture;
        private Texture2D _mainButtonTexture;
        private Texture2D _mainButtonHoverTexture;
        private Texture2D _mainButtonActiveTexture;
        private Texture2D _mainButtonPrimaryTexture;
        private Texture2D _mainButtonPrimaryHoverTexture;
        private Texture2D _mainButtonDangerTexture;
        private Texture2D _mainButtonDisabledTexture;
        private Texture2D _mainInfoTexture;
        private Texture2D _mainChipTexture;
        private Texture2D _mainLogoTexture;
        private Texture2D _slotNormalTexture;
        private Texture2D _slotSelectedTexture;
        private Texture2D _modalFrameTexture;
        private readonly Texture2D[] _mainMenuIcons = new Texture2D[5];
        private Font _uiBodyFont;
        private Font _uiHeadingFont;
        private Font _uiFallbackFont;
        private bool _uiRemasterAssetsReady;
        private bool _uiRemasterFailureLogged;
        private int _styleHeight;
        private int _styleWidth;
        private int _activeSlot = UnityJsonSaveRepository.MinimumSlot;
        private int _pendingNewGameSlot;
        private bool _hasSession;
        private PrototypeUiScreen _screen = PrototypeUiScreen.MainMenu;
        private PrototypeUiScreen _slotReturnScreen = PrototypeUiScreen.MainMenu;
        private OfficeContractTaskCoordinator _contractTaskCoordinator;
        private TitleMoneyRainRenderer _titleMoneyRainRenderer;
        private OfficeAutonomyCoordinator _officeAutonomyCoordinator;
        private IOfficeRuntimeAgent[] _starterOfficeRuntimeAgents =
            Array.Empty<IOfficeRuntimeAgent>();
        private PlayerOfficeWorkInteractor _playerWorkInteractor;
        private int _reportedOfficeTaskCount;
        private ManagementUiV2Presenter _managementUiPresenter;
        private MainNavigationHudPresenter _mainNavigationHudPresenter;
        private float _worldTimeScale = 1f;
        private double _officeRealtimeAccumulatorSeconds;
        private bool _officePresentationWasLoading;
        private bool _officeObservationCamera = true;
        private const double OfficeSecondsPerGameMinute = 1d;

        public GameState State => _state;
        public PrototypeUiScreen UiScreen => _screen;
        public int ActiveSlot => _activeSlot;
        public bool HasSession => _hasSession;
        public bool HasAnySave => GetLatestSaveSlot() != null;
        public string WorldNotice => _notice;
        public float WorldTimeScale => _worldTimeScale;
        public bool IsOfficeObservationCamera => _officeObservationCamera;

        private void Awake()
        {
            InitializeNow();
            EnsureTitleMoneyRainRenderer();
            EnsureManagementUiPresenter();
            EnsureMainNavigationHudPresenter();
            if (!Application.isPlaying) return;
            ConfigureDisplayDefaults();
            ShowMainMenuNow();
            TryStartFrontendQaCapture();
        }

        private void Start()
        {
            InitializeOfficeTaskBridgeNow();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (GamePatchBootstrap.IsBlocking) return;
            if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreenNow();
            if (_hasSession && _screen == PrototypeUiScreen.Playing && Input.GetKeyDown(KeyCode.C))
                ToggleOfficeObservationCameraNow();
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S) &&
                (_screen == PrototypeUiScreen.Playing || _screen == PrototypeUiScreen.Management))
            {
                SaveSlotNow(_activeSlot);
            }

            if (_hasSession) SyncOfficeTaskNotice();
            TickOfficeRealtimeClock();

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_mainNavigationHudPresenter != null && _mainNavigationHudPresenter.TryHandleEscape()) return;
            switch (_screen)
            {
                case PrototypeUiScreen.Playing:
                    ShowManagementNow();
                    break;
                case PrototypeUiScreen.Management:
                    CloseManagementNow();
                    break;
                case PrototypeUiScreen.PauseMenu:
                    ResumeGameNow();
                    break;
                case PrototypeUiScreen.SaveSlots:
                case PrototypeUiScreen.LoadSlots:
                case PrototypeUiScreen.NewGameSlots:
                case PrototypeUiScreen.ConfirmNewGame:
                    ReturnFromSlotScreen();
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && Time.timeScale == 0f) Time.timeScale = 1f;
            if (_solidTexture != null) Destroy(_solidTexture);
            DestroyRuntimeTexture(_mainButtonTexture);
            DestroyRuntimeTexture(_mainButtonHoverTexture);
            DestroyRuntimeTexture(_mainButtonActiveTexture);
            DestroyRuntimeTexture(_mainButtonPrimaryTexture);
            DestroyRuntimeTexture(_mainButtonPrimaryHoverTexture);
            DestroyRuntimeTexture(_mainButtonDangerTexture);
            DestroyRuntimeTexture(_mainButtonDisabledTexture);
            DestroyRuntimeTexture(_mainInfoTexture);
            DestroyRuntimeTexture(_mainChipTexture);
        }

        private void EnsureTitleMoneyRainRenderer()
        {
            if (_titleMoneyRainRenderer != null) return;
            _titleMoneyRainRenderer = GetComponent<TitleMoneyRainRenderer>();
            if (_titleMoneyRainRenderer == null) _titleMoneyRainRenderer = gameObject.AddComponent<TitleMoneyRainRenderer>();
        }

        private void EnsureManagementUiPresenter()
        {
            if (_managementUiPresenter == null) _managementUiPresenter = GetComponent<ManagementUiV2Presenter>();
            if (_managementUiPresenter == null) _managementUiPresenter = gameObject.AddComponent<ManagementUiV2Presenter>();
            _managementUiPresenter.Configure(this);
        }

        private void EnsureMainNavigationHudPresenter()
        {
            if (_mainNavigationHudPresenter == null)
                _mainNavigationHudPresenter = GetComponent<MainNavigationHudPresenter>();
            if (_mainNavigationHudPresenter == null)
                _mainNavigationHudPresenter = gameObject.AddComponent<MainNavigationHudPresenter>();
            _mainNavigationHudPresenter.Configure(this);
        }

        public void InitializeNow()
        {
            if (_saveSlots == null)
            {
                _saveSlots = Enumerable.Range(
                        UnityJsonSaveRepository.MinimumSlot,
                        UnityJsonSaveRepository.MaximumSlot - UnityJsonSaveRepository.MinimumSlot + 1)
                    .Select(slot => new UnityJsonSaveRepository(slot))
                    .ToArray();
            }

            if (_state == null) ResetState();
        }

        public OfficeContractTaskCoordinator InitializeOfficeTaskBridgeNow()
        {
            InitializeNow();
            var agents = FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None);
            RemapLegacyFamilyAgents(agents);
            var waypoints = FindObjectsByType<OfficeWaypoint>(FindObjectsSortMode.None);
            waypoints = EnsureOfficeExitWaypoint(waypoints);
            _contractTaskCoordinator = GetComponent<OfficeContractTaskCoordinator>();
            if (_contractTaskCoordinator == null)
            {
                _contractTaskCoordinator = gameObject.AddComponent<OfficeContractTaskCoordinator>();
            }

            _contractTaskCoordinator.Configure(this, agents, waypoints);
            _contractTaskCoordinator.InitializeNow();
            _officeAutonomyCoordinator = GetComponent<OfficeAutonomyCoordinator>();
            if (_officeAutonomyCoordinator == null)
            {
                _officeAutonomyCoordinator = gameObject.AddComponent<OfficeAutonomyCoordinator>();
            }

            _officeAutonomyCoordinator.Configure(this, agents, waypoints);
            _officeAutonomyCoordinator.InitializeNow();
            var playerController = FindFirstObjectByType<PrototypePlayerController>();
            if (playerController != null)
            {
                _playerWorkInteractor = playerController.GetComponent<PlayerOfficeWorkInteractor>();
                if (_playerWorkInteractor == null)
                    _playerWorkInteractor = playerController.gameObject.AddComponent<PlayerOfficeWorkInteractor>();
                _playerWorkInteractor.Configure(this, waypoints);
            }
            return _contractTaskCoordinator;
        }

        public void BindStarterOfficeRuntime(IOfficeRuntimeAgent[] runtimeAgents)
        {
            try
            {
                UnbindStarterOfficeRuntime();
                InitializeNow();
                if (runtimeAgents == null || runtimeAgents.Length < CanonicalFamilyIds.Length)
                    throw new ArgumentException(
                        "Starter Office requires the four canonical family actors.",
                        nameof(runtimeAgents));
                if (runtimeAgents.Any(item => item == null) ||
                    runtimeAgents.Select(item => item.AgentId)
                        .Distinct(StringComparer.Ordinal).Count() != runtimeAgents.Length)
                    throw new InvalidOperationException("Starter Office runtime actor IDs must be unique.");
                var runtimeIds = new HashSet<string>(
                    runtimeAgents.Select(item => item.AgentId),
                    StringComparer.Ordinal);
                if (CanonicalFamilyIds.Any(id => !runtimeIds.Contains(id)))
                    throw new InvalidOperationException(
                        "Starter Office runtime is missing a canonical family actor.");

                _starterOfficeRuntimeAgents = runtimeAgents.ToArray();
                _contractTaskCoordinator = GetComponent<OfficeContractTaskCoordinator>();
                if (_contractTaskCoordinator == null)
                    _contractTaskCoordinator = gameObject.AddComponent<OfficeContractTaskCoordinator>();
                _contractTaskCoordinator.enabled = false;
                _contractTaskCoordinator.ConfigureRuntime(this, _starterOfficeRuntimeAgents);
                _contractTaskCoordinator.InitializeNow();

                _officeAutonomyCoordinator = GetComponent<OfficeAutonomyCoordinator>();
                if (_officeAutonomyCoordinator == null)
                    _officeAutonomyCoordinator = gameObject.AddComponent<OfficeAutonomyCoordinator>();
                _officeAutonomyCoordinator.enabled = false;
                _officeAutonomyCoordinator.ConfigureRuntime(this, _starterOfficeRuntimeAgents);
                _officeAutonomyCoordinator.InitializeNow();

                _contractTaskCoordinator.enabled = true;
                _officeAutonomyCoordinator.enabled = true;
                if (_playerWorkInteractor != null) _playerWorkInteractor.enabled = false;
                EnsureManagementUiPresenter();
            }
            catch
            {
                UnbindStarterOfficeRuntime();
                throw;
            }
        }

        public void UnbindStarterOfficeRuntime()
        {
            if (_contractTaskCoordinator == null)
                _contractTaskCoordinator = GetComponent<OfficeContractTaskCoordinator>();
            if (_contractTaskCoordinator != null)
            {
                _contractTaskCoordinator.enabled = false;
                RunStarterOfficeUnbindStepNoThrow(_contractTaskCoordinator.ResetAssignments);
                RunStarterOfficeUnbindStepNoThrow(() =>
                    _contractTaskCoordinator.Configure(
                        this,
                        Array.Empty<OfficeWorkerAgent>(),
                        Array.Empty<OfficeWaypoint>()));
            }

            if (_officeAutonomyCoordinator == null)
                _officeAutonomyCoordinator = GetComponent<OfficeAutonomyCoordinator>();
            if (_officeAutonomyCoordinator != null)
            {
                _officeAutonomyCoordinator.enabled = false;
                RunStarterOfficeUnbindStepNoThrow(() =>
                    _officeAutonomyCoordinator.Configure(
                        this,
                        Array.Empty<OfficeWorkerAgent>(),
                        Array.Empty<OfficeWaypoint>()));
            }

            _starterOfficeRuntimeAgents = Array.Empty<IOfficeRuntimeAgent>();
        }

        private static void RunStarterOfficeUnbindStepNoThrow(Action step)
        {
            try
            {
                step?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static OfficeWaypoint[] EnsureOfficeExitWaypoint(OfficeWaypoint[] waypoints)
        {
            if (waypoints.Any(item => item != null && item.Activity == OfficeActivity.Outside)) return waypoints;
            var reception = waypoints.FirstOrDefault(item => item != null && item.Activity == OfficeActivity.Reception);
            if (reception == null) return waypoints;
            var exitObject = new GameObject("Runtime Office Exit Waypoint");
            exitObject.transform.position = reception.transform.position + Vector3.left * 1.4f + Vector3.forward * 3.35f;
            var exit = exitObject.AddComponent<OfficeWaypoint>();
            exit.Configure("office_exit_runtime", OfficeActivity.Outside, 0f, 0f);
            return waypoints.Concat(new[] { exit }).ToArray();
        }

        public void SetWorldNotice(string message)
        {
            _notice = message ?? string.Empty;
        }

        public void ShowMainMenuNow()
        {
            _screen = PrototypeUiScreen.MainMenu;
            SetSimulationPaused(true);
        }

        public void ShowPauseMenuNow()
        {
            if (!_hasSession) return;
            _screen = PrototypeUiScreen.PauseMenu;
            SetSimulationPaused(true);
        }

        public void ShowManagementNow()
        {
            if (!_hasSession) return;
            _screen = PrototypeUiScreen.Management;
            SetSimulationPaused(true);
        }

        public void CloseManagementNow()
        {
            if (!_hasSession) return;
            _screen = PrototypeUiScreen.Playing;
            SetSimulationPaused(false);
        }

        public void SetWorldTimeScaleNow(float scale)
        {
            if (scale != 1f && scale != 2f && scale != 4f)
                throw new ArgumentOutOfRangeException(nameof(scale), "Supported world time scales are 1x, 2x, and 4x.");
            _worldTimeScale = scale;
            if (Application.isPlaying && _screen == PrototypeUiScreen.Playing) Time.timeScale = _worldTimeScale;
            _notice = $"시간배속을 {_worldTimeScale:0}×로 설정했습니다.";
        }

        public void ToggleOfficeObservationCameraNow()
        {
            if (!_hasSession) return;
            _officeObservationCamera = !_officeObservationCamera;
            ApplyOfficeObservationCamera(true);
            _notice = _officeObservationCamera
                ? "사무실 전체 관찰 카메라로 전환했습니다."
                : "플레이어 추적 카메라로 전환했습니다.";
        }

        public void ResumeGameNow()
        {
            if (!_hasSession) return;
            _screen = PrototypeUiScreen.Playing;
            SetSimulationPaused(false);
        }

        public void ShowNewGameSlotsNow()
        {
            _slotReturnScreen = PrototypeUiScreen.MainMenu;
            _screen = PrototypeUiScreen.NewGameSlots;
            SetSimulationPaused(true);
        }

        public void ShowSaveSlotsNow()
        {
            if (!_hasSession) return;
            _slotReturnScreen = _screen;
            _screen = PrototypeUiScreen.SaveSlots;
            SetSimulationPaused(true);
        }

        public void ShowLoadSlotsNow()
        {
            _slotReturnScreen = _hasSession ? _screen : PrototypeUiScreen.MainMenu;
            _screen = PrototypeUiScreen.LoadSlots;
            SetSimulationPaused(true);
        }

        public void SelectNewGameSlotNow(int slot)
        {
            var repository = GetRepository(slot);
            if (repository.Exists)
            {
                _pendingNewGameSlot = slot;
                _screen = PrototypeUiScreen.ConfirmNewGame;
                return;
            }

            StartNewGameNow(slot);
        }

        public void StartNewGameNow(int slot, bool createInitialSave = true)
        {
            GetRepository(slot);
            ResetState();
            _activeSlot = slot;
            _hasSession = true;
            _screen = PrototypeUiScreen.Playing;
            _managementUiPresenter?.ResetSessionView();
            _mainNavigationHudPresenter?.ResetSessionView();
            _officeObservationCamera = true;
            ApplyOfficeObservationCamera(true);
            SetSimulationPaused(false);
            ScenePreviewJump.ShowStarterOffice();
            _notice = $"아빠 퇴직금 {PrototypeStateFactory.StartingCapitalWon:N0}원 · 네 식구의 오피스텔 회사를 시작합니다.";
            if (createInitialSave) SaveSlotNow(slot);
        }

        public bool ContinueLatestNow()
        {
            var latest = GetLatestSaveSlot();
            if (!latest.HasValue)
            {
                _notice = "이어할 저장 데이터가 없습니다.";
                return false;
            }

            return LoadSlotNow(latest.Value);
        }

        public bool SaveSlotNow(int slot)
        {
            if (!_hasSession || _state == null)
            {
                _notice = "먼저 게임을 시작해주세요.";
                return false;
            }

            try
            {
                var repository = GetRepository(slot);
                repository.Save(GameSaveMapper.ToDto(_state));
                _activeSlot = slot;
                _notice = $"슬롯 {slot} 저장 완료 · {DateTime.Now:HH:mm:ss}";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Confirm);
                return true;
            }
            catch (Exception exception)
            {
                _notice = $"저장 실패: {exception.Message}";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
                return false;
            }
        }

        public bool LoadSlotNow(int slot)
        {
            try
            {
                var repository = GetRepository(slot);
                if (!repository.TryLoad(out var save))
                {
                    _notice = $"슬롯 {slot}에 저장 데이터가 없습니다.";
                    if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
                    return false;
                }

                _state = GameSaveMapper.FromDto(save);
                _runner = new SimulationRunner(_state);
                _officeRealtimeAccumulatorSeconds = 0d;
                _contractTaskCoordinator?.ResetAssignments();
                _officeAutonomyCoordinator?.RefreshNow();
                _activeSlot = slot;
                _hasSession = true;
                _screen = PrototypeUiScreen.Playing;
                _mainNavigationHudPresenter?.ResetSessionView();
                _officeObservationCamera = true;
                ApplyOfficeObservationCamera(true);
                SetSimulationPaused(false);
                ScenePreviewJump.ShowStarterOffice();
                _notice = $"슬롯 {slot} 불러오기 완료";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Confirm);
                return true;
            }
            catch (Exception exception)
            {
                _notice = $"불러오기 실패: {exception.Message}";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
                return false;
            }
        }

        public void ToggleFullscreenNow()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                var resolution = Screen.currentResolution;
                Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);
                _notice = "전체 화면으로 전환했습니다.";
            }
            else
            {
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                _notice = "창 모드 1600×900으로 전환했습니다.";
            }
        }

        private void ResetState()
        {
            _state = PrototypeStateFactory.Create();
            _runner = new SimulationRunner(_state);
            _officeRealtimeAccumulatorSeconds = 0d;
            _contractTaskCoordinator?.ResetAssignments();
            _officeAutonomyCoordinator?.RefreshNow();
            _reportedOfficeTaskCount = 0;
        }

        private void ConfigureDisplayDefaults()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-familyCompanyCaptureMoneyRain") >= 0 ||
                Array.IndexOf(arguments, "-familyCompanyCaptureUiRemasterV3") >= 0)
                return;
            if (!Application.isEditor && Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                var resolution = Screen.currentResolution;
                Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);
            }
        }

        private void TryStartFrontendQaCapture()
        {
            const string argumentName = "-familyCompanyCaptureFrontend";
            var arguments = Environment.GetCommandLineArgs();
            var argumentIndex = Array.IndexOf(arguments, argumentName);
            if (argumentIndex < 0 || argumentIndex + 1 >= arguments.Length) return;
            var outputPath = Path.GetFullPath(arguments[argumentIndex + 1]);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            StartCoroutine(CaptureFrontendForQa(outputPath));
        }

        private IEnumerator CaptureFrontendForQa(string outputPath)
        {
            for (var frame = 0; frame < 10; frame++) yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(outputPath);
            for (var frame = 0; frame < 300; frame++)
            {
                yield return null;
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 1024) continue;
                Debug.Log($"FAMILY_COMPANY_FRONTEND_CAPTURE: PASS ({outputPath})");
                Application.Quit(0);
                yield break;
            }

            Debug.LogError($"FAMILY_COMPANY_FRONTEND_CAPTURE: FAIL ({outputPath})");
            Application.Quit(1);
        }

        private void SetSimulationPaused(bool paused)
        {
            if (Application.isPlaying) Time.timeScale = paused ? 0f : _worldTimeScale;
        }

        private void ApplyOfficeObservationCamera(bool snapImmediately)
        {
            var viewCamera = Camera.main;
            var follow = viewCamera != null ? viewCamera.GetComponent<IsometricCameraFollow>() : null;
            if (follow != null) follow.SetOfficeObservationForced(_officeObservationCamera, snapImmediately);
        }

        private void ReturnFromSlotScreen()
        {
            _screen = _slotReturnScreen;
            SetSimulationPaused(_screen != PrototypeUiScreen.Playing);
        }

        private UnityJsonSaveRepository GetRepository(int slot)
        {
            InitializeNow();
            if (slot < UnityJsonSaveRepository.MinimumSlot || slot > UnityJsonSaveRepository.MaximumSlot)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return _saveSlots[slot - UnityJsonSaveRepository.MinimumSlot];
        }

        private int? GetLatestSaveSlot()
        {
            InitializeNow();
            UnityJsonSaveRepository latest = null;
            foreach (var repository in _saveSlots)
            {
                if (!repository.Exists) continue;
                if (latest == null || repository.LastWriteTimeLocal > latest.LastWriteTimeLocal) latest = repository;
            }

            return latest?.Slot;
        }

        private void OnGUI()
        {
            if (GamePatchBootstrap.IsBlocking) return;
            EnsureStyles();
            switch (_screen)
            {
                case PrototypeUiScreen.MainMenu:
                    DrawMainMenu();
                    break;
                case PrototypeUiScreen.Playing:
                case PrototypeUiScreen.Management:
                    break;
                case PrototypeUiScreen.PauseMenu:
                    DrawPauseMenu();
                    break;
                case PrototypeUiScreen.NewGameSlots:
                    DrawMenuBackground(null);
                    DrawSlotPicker("처음하기", "새 회사를 시작할 저장 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.SaveSlots:
                    DrawSlotPicker("저장하기", "현재 회사를 저장할 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.LoadSlots:
                    if (!_hasSession) DrawMenuBackground(null);
                    DrawSlotPicker("불러오기", "이어갈 회사를 선택하세요.", false);
                    break;
                case PrototypeUiScreen.ConfirmNewGame:
                    DrawMenuBackground(null);
                    DrawNewGameConfirmation();
                    break;
            }
        }

        private void DrawMainMenu()
        {
            DrawMenuBackground(null);
            DrawLeftMainMenu(TitleMoneyRainRenderer.IsCompactLayout(Screen.width, Screen.height));
        }

        private void DrawLeftMainMenu(bool compact)
        {
            _ = compact;
            if (!_uiRemasterAssetsReady)
            {
                var errorRect = new Rect(32f, 32f, Mathf.Max(1f, Screen.width - 64f), 80f);
                GUI.Label(errorRect, "UI 자산을 불러오지 못했습니다.", _headingStyle);
                return;
            }

            var scale = UiRemasterTypography.CalculateScale(Screen.width, Screen.height);
            var layout = UiRemasterLayout.CalculateTitle(Screen.width, Screen.height);
            GUI.DrawTexture(layout.Logo, _mainLogoTexture, ScaleMode.StretchToFill, true);
            var titleInset = UiRemasterTypography.Pixels(42f, scale);
            var titleBounds = new Rect(
                layout.Logo.x + titleInset,
                layout.Logo.y + UiRemasterTypography.Pixels(10f, scale),
                layout.Logo.width - titleInset * 2f,
                layout.Logo.height - UiRemasterTypography.Pixels(20f, scale));
            var titleContent = new GUIContent("우리 가족회사");
            GUI.Label(UiRemasterTypography.CenterUsingFontMetrics(titleBounds, titleContent, _mainTitleStyle),
                titleContent, _mainTitleStyle);
            GUI.Label(layout.Subtitle, "네 식구가 함께 만드는 작은 회사의 역사", _mainSubtitleStyle);

            var latestSlot = GetLatestSaveSlot();

            if (DrawLeftMenuRow(layout.Buttons[0], _mainMenuIcons[0], "처음 하기", true,
                    MainMenuButtonKind.Primary))
                ShowNewGameSlotsNow();
            if (DrawLeftMenuRow(layout.Buttons[1], _mainMenuIcons[1], "이어하기", latestSlot.HasValue,
                    MainMenuButtonKind.Standard))
                ContinueLatestNow();
            if (DrawLeftMenuRow(layout.Buttons[2], _mainMenuIcons[2], "불러오기", true,
                    MainMenuButtonKind.Standard))
                ShowLoadSlotsNow();
            if (DrawLeftMenuRow(layout.Buttons[3], _mainMenuIcons[3], "설정", true,
                    MainMenuButtonKind.Standard))
                ToggleFullscreenNow();
            if (DrawLeftMenuRow(layout.Buttons[4], _mainMenuIcons[4], "종료", true,
                    MainMenuButtonKind.Danger))
                Application.Quit();

            GUI.Label(layout.Footer, "2000년 1월 3일 · 가족 네 명의 첫 출근", _mainInfoStyle);
        }

        private bool DrawLeftMenuRow(
            Rect rect,
            Texture2D icon,
            string title,
            bool enabled,
            MainMenuButtonKind kind)
        {
            // The frame is drawn from its measured content window instead of a GUIStyle background:
            // the authored sheet is ~2x taller than the frame, so a stretched background rendered a
            // thin sliver and a sliced one smeared the corner ornaments across the middle.
            var hovered = enabled && rect.Contains(Event.current.mousePosition);
            var pressed = hovered && GUI.enabled && Input.GetMouseButton(0);
            var texture = !enabled
                ? _mainButtonDisabledTexture
                : pressed
                    ? _mainButtonActiveTexture
                    : hovered
                        ? kind == MainMenuButtonKind.Primary ? _mainButtonPrimaryHoverTexture : _mainButtonHoverTexture
                        : kind == MainMenuButtonKind.Primary
                            ? _mainButtonPrimaryTexture
                            : kind == MainMenuButtonKind.Danger ? _mainButtonDangerTexture : _mainButtonTexture;
            var window = !enabled
                ? UiRemasterTitleArt.ButtonDisabled
                : pressed
                    ? UiRemasterTitleArt.ButtonPressed
                    : hovered ? UiRemasterTitleArt.ButtonHover : UiRemasterTitleArt.ButtonNormal;
            UiRemasterTitleArt.Draw(rect, texture, window, UiRemasterTitleArt.ButtonNormal);

            var previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            var clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = previousEnabled;

            var danger = kind == MainMenuButtonKind.Danger;
            var titleColor = enabled
                ? danger ? new Color(0.64f, 0.18f, 0.14f, 1f) : new Color(0.125f, 0.23f, 0.23f, 1f)
                : new Color(0.39f, 0.47f, 0.45f, 1f);
            var scale = UiRemasterTypography.CalculateScale(Screen.width, Screen.height);
            // The frame's gold end cap occupies about 7.6% of the width, so the icon starts past it.
            var iconSize = Mathf.Min(rect.height - UiRemasterTypography.Pixels(18f, scale),
                UiRemasterTypography.Pixels(44f, scale));
            var iconRect = UiRemasterTypography.PixelSnap(new Rect(
                rect.x + rect.width * 0.085f,
                rect.y + (rect.height - iconSize) * 0.5f,
                iconSize,
                iconSize));
            if (icon != null)
            {
                var previousColor = GUI.color;
                GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }

            var textBounds = new Rect(
                iconRect.xMax + UiRemasterTypography.Pixels(UiRemasterLayout.IconTextGap, scale),
                rect.y,
                rect.xMax - iconRect.xMax - rect.width * 0.10f,
                rect.height);
            var content = new GUIContent(title);
            DrawColoredLabel(UiRemasterTypography.CenterUsingFontMetrics(textBounds, content, _mainButtonTitleStyle),
                title, _mainButtonTitleStyle, titleColor);
            return clicked;
        }

        private static void DrawColoredLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private void DrawMenuBackground(string eyebrow)
        {
            var fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
            EnsureTitleMoneyRainRenderer();
            if (_titleMoneyRainRenderer != null) _titleMoneyRainRenderer.Draw(fullScreen);
            else DrawSolid(fullScreen, new Color(0.055f, 0.047f, 0.045f, 1f));
            if (!string.IsNullOrWhiteSpace(eyebrow))
            {
                GUI.Label(new Rect(Screen.width * 0.075f, 46f, Screen.width * 0.6f, 40f), eyebrow, _mainSubtitleStyle);
            }
        }


        public void AcceptOfferNow(SubcontractOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            var result = _state.Contracts.Accept(
                offer,
                _state.Company,
                _state.Family,
                _state.Growth,
                _state.Time.ElapsedMinutes);
            _notice = result.Accepted
                ? $"계약 수락 · {offer.ExactClientDisplayName} / {offer.Title} · 나는 해당 작업 장소에서 E로 직접 참여 가능"
                : ContractRejectionLabel(result.Decision.RejectionReason);
            if (!Application.isPlaying) return;
            if (result.Accepted) GameAudioCoordinator.Instance.PlayPaperSfx(GamePaperSfx.Place);
            else GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
        }

        public bool AssignContractWorkNow(string offerId, string memberId)
        {
            var member = _state.Family.Get(memberId);
            if (member.Role == FamilyRole.Player)
            {
                _notice = "나는 월드에서 직접 책상과 상호작용해 작업에 참여합니다.";
                return false;
            }

            var schedule = FamilyScheduleRules.Resolve(member.Role, _state.Time.Now);
            if (!schedule.CanPerformCompanyWork)
            {
                _notice = $"{member.DisplayName}은 지금 {schedule.Label} 중이라 회사 작업을 맡을 수 없습니다.";
                return false;
            }

            if (_contractTaskCoordinator == null) InitializeOfficeTaskBridgeNow();
            if (_contractTaskCoordinator != null && _contractTaskCoordinator.AssignContractWork(offerId, memberId, 4))
            {
                _notice = $"{member.DisplayName}에게 최대 4인시 작업을 배정했습니다. 이동·착석 후 시작합니다.";
                return true;
            }

            _notice = _contractTaskCoordinator != null &&
                      !string.IsNullOrWhiteSpace(_contractTaskCoordinator.LastAssignmentFailureLabel)
                ? _contractTaskCoordinator.LastAssignmentFailureLabel
                : "해당 가족이 이미 작업 중이거나 사용할 책상이 없습니다.";
            return false;
        }

        public void OpenResearchCenterNow()
        {
            _state.Growth.TryOpenResearchCenter(_state.Company, _state.Time.ElapsedMinutes, out _notice);
        }

        public void ResearchTechnologyNow(string technologyId)
        {
            _state.Growth.TryResearch(technologyId, _state.Company, _state.Time.ElapsedMinutes, out _notice);
        }

        public void PurchaseMarketReportNow(BusinessIndustry industry)
        {
            _state.Growth.TryPurchaseMarketReport(
                _state.WorldSeed,
                industry,
                _state.Company,
                _state.Time.ElapsedMinutes,
                out _notice);
        }

        public void FoundBusinessNow(BusinessIndustry industry)
        {
            _state.Growth.TryFoundBusiness(
                industry,
                _state.Company,
                _state.Family,
                _state.Time.ElapsedMinutes,
                out _notice);
        }

        public void StartProductNow(BusinessIndustry industry, string title, long budgetWon)
        {
            _state.Growth.TryStartProduct(
                industry,
                title,
                budgetWon,
                _state.Company,
                _state.Time.ElapsedMinutes,
                out _notice);
        }

        public void AdvanceTimeNow(long minutes)
        {
            AdvanceTime(minutes, true);
        }

        private void TickOfficeRealtimeClock()
        {
            if (!_hasSession || _screen != PrototypeUiScreen.Playing || _state == null || _runner == null)
                return;
            if (OfficeRuntimePerformanceProbe.IsDrivingClock) return;
            if (ScenePreviewJump.IsPresentationLoading)
            {
                _officePresentationWasLoading = true;
                return;
            }
            if (_officePresentationWasLoading)
            {
                // Script Update order is not fixed. ScenePreviewJump can clear its loading flag
                // earlier in the same long frame in which this clock runs, but that frame's
                // unscaledDeltaTime still spans presentation rebuild work. Discard that boundary
                // delta once so loading/rebind time never advances the authoritative game clock.
                _officePresentationWasLoading = false;
                return;
            }
            double delta = Time.unscaledDeltaTime;
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta <= 0d) return;

            _officeRealtimeAccumulatorSeconds += delta * _worldTimeScale;
            long minutes = (long)Math.Floor(
                (_officeRealtimeAccumulatorSeconds + 0.000000001d) /
                OfficeSecondsPerGameMinute);
            if (minutes <= 0L) return;
            _officeRealtimeAccumulatorSeconds -= minutes * OfficeSecondsPerGameMinute;
            if (_officeRealtimeAccumulatorSeconds < 0d &&
                _officeRealtimeAccumulatorSeconds > -0.00000001d)
                _officeRealtimeAccumulatorSeconds = 0d;
            AdvanceTime(minutes, false);
        }

        private void AdvanceTime(long minutes, bool announcePassage)
        {
            using var measurement = OfficePerformanceTelemetry.Measure(
                OfficePerformancePath.SimulationAdvance);
            if (minutes <= 0L) throw new ArgumentOutOfRangeException(nameof(minutes));
            var previousMinute = _state.Time.ElapsedMinutes;
            var failedBefore = _state.Contracts.Contracts.Count(item => item.Status == SubcontractStatus.Failed);
            var productWasResolved = _state.Growth.ProductProject?.Resolved ?? false;
            _runner.AdvanceMinutes(minutes);
            if (!StarterOfficeRuntimeBootstrap.IsLayoutRebuilding)
                _officeAutonomyCoordinator?.RefreshNow();
            var failedAfter = _state.Contracts.Contracts.Count(item => item.Status == SubcontractStatus.Failed);
            var latestIncident = _state.Family.Members
                .Where(item => item.Autonomy.LastIncidentMinute > previousMinute)
                .OrderByDescending(item => item.Autonomy.LastIncidentMinute)
                .FirstOrDefault();
            if (failedAfter > failedBefore)
            {
                _notice = $"마감 실패 {failedAfter - failedBefore}건 · 위약금과 평판이 반영됐습니다.";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
            }
            else if (!productWasResolved && (_state.Growth.ProductProject?.Resolved ?? false))
            {
                _notice = $"제품 출시 완료 · 매출 {_state.Growth.ProductProject.RevenueWon:N0}원";
                if (Application.isPlaying) GameAudioCoordinator.Instance.PlayCoinsSfx(true);
            }
            else if (latestIncident != null)
            {
                _notice = latestIncident.Autonomy.LastIncidentSummary;
            }
            else if (announcePassage)
            {
                _notice = minutes >= 1440 ? "하루가 지났습니다." : "1시간이 지났습니다.";
            }
        }

        private void SyncOfficeTaskNotice()
        {
            if (_contractTaskCoordinator == null || _reportedOfficeTaskCount == _contractTaskCoordinator.CompletedTaskCount) return;
            _reportedOfficeTaskCount = _contractTaskCoordinator.CompletedTaskCount;
            var result = _contractTaskCoordinator.LastWorkResult;
            if (result == null) return;
            _notice = result.Completed
                ? result.RewardWon > 0
                    ? $"계약 완료 · {result.RewardWon:N0}원 입금 · 기술 {result.TechnologyGains.Count}종 습득"
                    : "자체 제품 업무 완료 · 사업 → 자체 제품에서 다음 단계를 확인하세요."
                : result.Applied
                    ? $"작업 {result.AppliedPersonHours}시간 반영"
                    : "작업을 반영하지 못했습니다. 체력과 마감을 확인하세요.";
            if (Application.isPlaying)
            {
                if (result.Completed) GameAudioCoordinator.Instance.PlayCoinsSfx(result.RewardWon >= 500_000);
                else if (result.Applied) GameAudioCoordinator.Instance.PlayPaperSfx(GamePaperSfx.Rustle);
                else GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
            }
        }

        private static string ContractStatusLabel(SubcontractStatus status)
        {
            switch (status)
            {
                case SubcontractStatus.Active: return "진행 중";
                case SubcontractStatus.Completed: return "완료";
                case SubcontractStatus.Failed: return "실패";
                default: return status.ToString();
            }
        }

        private static string ContractRejectionLabel(ContractRejectionReason reason)
        {
            switch (reason)
            {
                case ContractRejectionReason.TooManyConcurrentContracts: return "동시에 진행할 수 있는 계약은 2건입니다.";
                case ContractRejectionReason.UpfrontCashInsufficient: return "착수 비용이 부족합니다.";
                case ContractRejectionReason.ReputationInsufficient: return "요구 평판이 부족합니다.";
                case ContractRejectionReason.DevelopmentInsufficient: return "개발/제작 능력이 부족합니다.";
                case ContractRejectionReason.SpeedInsufficient: return "작업 속도가 부족합니다.";
                case ContractRejectionReason.RequiredTechnologyMissing: return "R&D 센터에서 필요한 기술을 먼저 연구하세요.";
                case ContractRejectionReason.ScheduleCapacityExceeded: return "현재 계약 일정으로는 마감을 맞추기 어렵습니다.";
                default: return $"계약을 받을 수 없습니다 · {reason}";
            }
        }


        private void DrawPauseMenu()
        {
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.20f, 0.40f, 0.42f, 0.46f));
            var rect = CenteredRect(540f, 570f);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 50f, rect.y + 42f, rect.width - 100f, rect.height - 84f));
            GUILayout.Label("게임 메뉴", _headingStyle);
            GUILayout.Label($"현재 저장 슬롯 · {_activeSlot}", _smallStyle);
            GUILayout.Space(25f);
            if (GUILayout.Button("계속하기", _buttonStyle, GUILayout.Height(58f))) ResumeGameNow();
            if (GUILayout.Button("저장하기", _buttonStyle, GUILayout.Height(58f))) ShowSaveSlotsNow();
            if (GUILayout.Button("불러오기", _buttonStyle, GUILayout.Height(58f))) ShowLoadSlotsNow();
            var mode = Screen.fullScreenMode == FullScreenMode.Windowed ? "창 모드" : "전체 화면";
            if (GUILayout.Button($"화면 설정 · {mode}", _buttonStyle, GUILayout.Height(52f))) ToggleFullscreenNow();
            if (GUILayout.Button("메인 화면", _buttonStyle, GUILayout.Height(52f))) ShowMainMenuNow();
            GUILayout.EndArea();
        }

        private void DrawSlotPicker(string title, string description, bool canUseEmptySlot)
        {
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.20f, 0.40f, 0.42f, _hasSession ? 0.46f : 0.08f));
            var scale = UiRemasterTypography.CalculateScale(Screen.width, Screen.height);
            var panelWidth = Mathf.Min(UiRemasterTypography.Pixels(1050f, scale), Screen.width - 48f);
            var panelHeight = Mathf.Min(UiRemasterTypography.Pixels(650f, scale), Screen.height - 48f);
            var panel = CenteredRect(panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            var inset = UiRemasterTypography.Pixels(48f, scale);
            var headerInset = UiRemasterTypography.Pixels(72f, scale);
            var headerTop = UiRemasterTypography.Pixels(48f, scale);
            var titleHeight = UiRemasterTypography.Pixels(42f, scale);
            var descriptionTop = headerTop + titleHeight + UiRemasterTypography.Pixels(4f, scale);
            GUI.Label(new Rect(panel.x + headerInset, panel.y + headerTop, panel.width - headerInset * 2f,
                titleHeight), title, _headingStyle);
            GUI.Label(new Rect(panel.x + headerInset, panel.y + descriptionTop, panel.width - headerInset * 2f,
                UiRemasterTypography.Pixels(30f, scale)), description, _bodyStyle);

            var gap = UiRemasterTypography.Pixels(10f, scale);
            var cardY = panel.y + UiRemasterTypography.Pixels(138f, scale);
            var backHeight = UiRemasterTypography.Pixels(52f, scale);
            var backY = panel.yMax - UiRemasterTypography.Pixels(96f, scale);
            var cardBottom = backY - UiRemasterTypography.Pixels(16f, scale);
            var cardWidth = panel.width - inset * 2f;
            var cardHeight = (cardBottom - cardY - gap * 2f) / 3f;
            for (var slot = UnityJsonSaveRepository.MinimumSlot; slot <= UnityJsonSaveRepository.MaximumSlot; slot++)
            {
                var repository = GetRepository(slot);
                var card = UiRemasterTypography.PixelSnap(new Rect(
                    panel.x + inset,
                    cardY + (slot - 1) * (cardHeight + gap),
                    cardWidth,
                    cardHeight));
                var label = BuildSlotLabel(repository);
                GUI.enabled = canUseEmptySlot || repository.Exists;
                // The card art packs a teal spine, a thumbnail frame and two gold rules into a 3.2:1
                // sheet that has to fill a 7:1 rect, so it is nine-sliced with the decoration drawn
                // at its authored proportions and only the cream field stretched.
                var slotHovered = GUI.enabled && card.Contains(Event.current.mousePosition);
                var slotWindow = slotHovered ? UiRemasterTitleArt.SlotSelected : UiRemasterTitleArt.SlotNormal;
                var slotTexture = slotHovered ? _slotSelectedTexture : _slotNormalTexture;
                UiRemasterTitleArt.DrawSliced(card, slotTexture, slotWindow);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    if (_screen == PrototypeUiScreen.NewGameSlots) SelectNewGameSlotNow(slot);
                    else if (_screen == PrototypeUiScreen.SaveSlots)
                    {
                        SaveSlotNow(slot);
                        _screen = _slotReturnScreen;
                        SetSimulationPaused(_screen != PrototypeUiScreen.Playing);
                    }
                    else LoadSlotNow(slot);
                }
                // Centre the slot number on the thumbnail frame the art draws in its left border.
                var slotColumnWidth = Mathf.Max(
                    UiRemasterTypography.Pixels(96f, scale),
                    slotWindow.LeftBorderFor(card.height, slotTexture));
                var slotBounds = new Rect(
                    card.x + slotColumnWidth * 0.16f,
                    card.y,
                    slotColumnWidth * 0.78f,
                    card.height);
                var slotContent = new GUIContent("슬롯 " + slot);
                GUI.Label(UiRemasterTypography.CenterUsingFontMetrics(slotBounds, slotContent, _mainButtonTitleStyle),
                    slotContent, _mainButtonTitleStyle);
                var labelBounds = new Rect(
                    card.x + slotColumnWidth + UiRemasterTypography.Pixels(20f, scale),
                    card.y + UiRemasterTypography.Pixels(12f, scale),
                    card.width - slotColumnWidth - UiRemasterTypography.Pixels(44f, scale),
                    card.height - UiRemasterTypography.Pixels(24f, scale));
                GUI.Label(labelBounds, label, _slotTextStyle);
                GUI.enabled = true;
            }

            if (GUI.Button(new Rect(panel.x + inset, backY,
                    UiRemasterTypography.Pixels(190f, scale), backHeight), "뒤로", _buttonStyle))
                ReturnFromSlotScreen();
        }

        private string BuildSlotLabel(UnityJsonSaveRepository repository)
        {
            if (!repository.TryLoad(out var save))
            {
                return "빈 슬롯\n새 가족회사를 시작할 수 있습니다.";
            }

            var campaignDate = GameTime.CampaignStart.AddMinutes(save.elapsedMinutes);
            var savedAt = repository.LastWriteTimeLocal;
            var businessCount = save.schemaVersion >= 4 ? save.growth?.ownedBusinesses?.Count ?? 0 : 0;
            return $"{save.company.companyName} · {campaignDate:yyyy.MM.dd HH:mm}\n" +
                   $"현금 {save.company.cashWon:N0}원 · 평판 {save.company.reputation}\n" +
                   $"계약 {save.contracts?.Count ?? 0}건 · 자체 사업 {businessCount}개 · 저장 {savedAt:yyyy.MM.dd HH:mm}";
        }

        private void DrawNewGameConfirmation()
        {
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.20f, 0.40f, 0.42f, 0.34f));
            var panel = CenteredRect(620f, 340f);
            GUI.Box(panel, GUIContent.none, _panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 48f, panel.y + 42f, panel.width - 96f, panel.height - 84f));
            GUILayout.Label($"SLOT {_pendingNewGameSlot} 덮어쓰기", _headingStyle);
            GUILayout.Space(16f);
            GUILayout.Label("기존 저장은 백업 파일로 한 번 보관됩니다. 이 슬롯에서 새 게임을 시작할까요?", _bodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("취소", _buttonStyle, GUILayout.Height(52f))) _screen = PrototypeUiScreen.NewGameSlots;
            if (GUILayout.Button("덮어쓰고 시작", _buttonStyle, GUILayout.Height(52f))) StartNewGameNow(_pendingNewGameSlot);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private Rect CenteredRect(float width, float height)
        {
            width = Mathf.Min(width, Screen.width - 40f);
            height = Mathf.Min(height, Screen.height - 40f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawSolid(Rect rect, Color color)
        {
            EnsureSolidTexture();

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _solidTexture);
            GUI.color = previous;
        }

        private void EnsureSolidTexture()
        {
            if (_solidTexture != null) return;
            _solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _solidTexture.SetPixel(0, 0, Color.white);
            _solidTexture.Apply(false, true);
        }

        private void EnsureStyles()
        {
            var targetHeight = Mathf.Max(1, Screen.height);
            var targetWidth = Mathf.Max(1, Screen.width);
            if (_titleStyle != null && _styleHeight == targetHeight && _styleWidth == targetWidth) return;
            _styleHeight = targetHeight;
            _styleWidth = targetWidth;
            var scale = UiRemasterTypography.CalculateScale(targetWidth, targetHeight);
            EnsureSolidTexture();
            EnsureMainMenuTextures();
            if (!UiRemasterTypography.TryLoadFonts(out _uiBodyFont, out _uiHeadingFont, out _uiFallbackFont, out var fontError))
            {
                _uiRemasterAssetsReady = false;
                if (!_uiRemasterFailureLogged)
                {
                    Debug.LogError("UI_REMASTER_V3_FONT_MISSING | " + fontError);
                    _uiRemasterFailureLogged = true;
                }
            }

            var headingFont = _uiHeadingFont != null ? _uiHeadingFont : GUI.skin.font;
            var bodyFont = _uiBodyFont != null ? _uiBodyFont : GUI.skin.font;
            var ink = new Color(0.125f, 0.23f, 0.23f, 1f);
            var mutedInk = new Color(0.22f, 0.34f, 0.33f, 1f);
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = headingFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.MainTitlePixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ink }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = bodyFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.BodyPixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = mutedInk }
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = headingFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.PanelTitlePixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ink }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = bodyFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.BodyPixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = ink }
            };
            _smallStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.CaptionPixels, scale),
                normal = { textColor = mutedInk }
            };

            _mainTitleStyle = new GUIStyle(_titleStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.MainTitlePixels, scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ink }
            };
            _mainTitleShadowStyle = new GUIStyle(_mainTitleStyle);
            _mainSubtitleStyle = new GUIStyle(_subtitleStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.BodyPixels, scale),
                normal = { textColor = mutedInk }
            };
            _mainChipStyle = new GUIStyle(_smallStyle)
            {
                font = headingFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.CaptionPixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = mutedInk }
            };
            _mainButtonTitleStyle = new GUIStyle(_headingStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.ButtonPixels, scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ink }
            };
            _mainButtonDetailStyle = new GUIStyle(_smallStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.CaptionPixels, scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = mutedInk }
            };
            _mainButtonIndexStyle = new GUIStyle(_headingStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.ButtonPixels, scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ink }
            };
            _mainInfoStyle = new GUIStyle(_smallStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.CaptionPixels, scale),
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Clip,
                normal = { textColor = mutedInk }
            };
            _mainShortcutStyle = new GUIStyle(_smallStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.CaptionPixels, scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = mutedInk }
            };
            _mainCompactTextButtonStyle = new GUIStyle(_mainShortcutStyle)
            {
                font = headingFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.ButtonPixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = ink },
                hover = { textColor = new Color(0.12f, 0.43f, 0.40f, 1f) },
                active = { textColor = new Color(0.65f, 0.20f, 0.16f, 1f) }
            };
            _mainCompactDangerButtonStyle = new GUIStyle(_mainCompactTextButtonStyle)
            {
                normal = { textColor = new Color(1f, 0.58f, 0.50f, 0.96f) },
                hover = { textColor = new Color(1f, 0.80f, 0.66f, 1f) },
                active = { textColor = new Color(0.90f, 0.30f, 0.26f, 1f) }
            };

            var roundedBorder = new RectOffset(48, 48, 24, 24);
            _mainButtonStyle = CreateMainMenuButtonStyle(
                _mainButtonTexture,
                _mainButtonHoverTexture,
                _mainButtonActiveTexture,
                roundedBorder);
            _mainButtonPrimaryStyle = CreateMainMenuButtonStyle(
                _mainButtonPrimaryTexture,
                _mainButtonPrimaryHoverTexture,
                _mainButtonActiveTexture,
                roundedBorder);
            _mainButtonDangerStyle = CreateMainMenuButtonStyle(
                _mainButtonDangerTexture,
                _mainButtonHoverTexture,
                _mainButtonActiveTexture,
                roundedBorder);
            _mainButtonDisabledStyle = CreateMainMenuButtonStyle(
                _mainButtonDisabledTexture,
                _mainButtonDisabledTexture,
                _mainButtonDisabledTexture,
                roundedBorder);
            _buttonStyle = new GUIStyle(_mainButtonStyle)
            {
                font = headingFont,
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.ButtonPixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(20, 20, 8, 8),
                normal = { textColor = ink },
                hover = { textColor = new Color(0.12f, 0.43f, 0.40f, 1f) },
                active = { textColor = new Color(0.65f, 0.20f, 0.16f, 1f) }
            };
            _slotStyle = CreateMainMenuButtonStyle(
                _slotNormalTexture,
                _slotSelectedTexture,
                _slotSelectedTexture,
                new RectOffset(44, 44, 28, 28));
            _slotTextStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = UiRemasterTypography.Pixels(UiRemasterTypography.BodyPixels, scale),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = ink }
            };
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(40, 40, 40, 40),
                padding = new RectOffset(0, 0, 0, 0),
                normal = { background = _modalFrameTexture }
            };
            _mainInfoBoxStyle = new GUIStyle(GUI.skin.box)
            {
                border = roundedBorder,
                normal = { background = _mainInfoTexture }
            };
            _mainChipBoxStyle = new GUIStyle(GUI.skin.box)
            {
                border = roundedBorder,
                normal = { background = _mainChipTexture }
            };
        }

        private static GUIStyle CreateMainMenuButtonStyle(
            Texture2D normal,
            Texture2D hover,
            Texture2D active,
            RectOffset border)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                border = border,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.onFocused.background = hover;
            return style;
        }

        private void EnsureMainMenuTextures()
        {
            if (_mainLogoTexture != null) return;
            const string root = "UiRemasterV3/Title/";
            _mainLogoTexture = Resources.Load<Texture2D>(root + "title_logo_frame_v3");
            _mainButtonTexture = Resources.Load<Texture2D>(root + "title_button_normal_v3");
            _mainButtonHoverTexture = Resources.Load<Texture2D>(root + "title_button_hover_v3");
            _mainButtonActiveTexture = Resources.Load<Texture2D>(root + "title_button_pressed_v3");
            _mainButtonDisabledTexture = Resources.Load<Texture2D>(root + "title_button_disabled_v3");
            _slotNormalTexture = Resources.Load<Texture2D>(root + "save_slot_normal_v3");
            _slotSelectedTexture = Resources.Load<Texture2D>(root + "save_slot_selected_v3");
            _modalFrameTexture = Resources.Load<Texture2D>("UiRemasterV3/Common/modal_frame_v3");
            _mainMenuIcons[0] = Resources.Load<Texture2D>(root + "Icons/new_company_v3");
            _mainMenuIcons[1] = Resources.Load<Texture2D>(root + "Icons/continue_v3");
            _mainMenuIcons[2] = Resources.Load<Texture2D>(root + "Icons/load_v3");
            _mainMenuIcons[3] = Resources.Load<Texture2D>(root + "Icons/settings_v3");
            _mainMenuIcons[4] = Resources.Load<Texture2D>(root + "Icons/exit_v3");

            _mainButtonPrimaryTexture = _mainButtonTexture;
            _mainButtonPrimaryHoverTexture = _mainButtonHoverTexture;
            _mainButtonDangerTexture = _mainButtonTexture;
            _mainInfoTexture = _mainButtonTexture;
            _mainChipTexture = _mainButtonTexture;

            _uiRemasterAssetsReady = _mainLogoTexture != null && _mainButtonTexture != null &&
                                       _mainButtonHoverTexture != null && _mainButtonActiveTexture != null &&
                                       _mainButtonDisabledTexture != null && _slotNormalTexture != null &&
                                       _slotSelectedTexture != null && _modalFrameTexture != null &&
                                       _mainMenuIcons.All(item => item != null);
            if (_uiRemasterAssetsReady || _uiRemasterFailureLogged) return;
            Debug.LogError("UI_REMASTER_V3_ASSET_MISSING | one or more generated title/common assets failed to load");
            _uiRemasterFailureLogged = true;
        }

        private static void DestroyRuntimeTexture(Texture2D texture)
        {
            if (texture == null) return;
            if ((texture.hideFlags & HideFlags.DontSave) == 0) return;
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        private enum MainMenuButtonKind
        {
            Standard,
            Primary,
            Danger
        }


        private static void RemapLegacyFamilyAgents(OfficeWorkerAgent[] agents)
        {
            foreach (var agent in agents)
            {
                if (agent.AgentId == "employee_a") agent.SetAgentId("father");
                if (agent.AgentId == "employee_b") agent.SetAgentId("mother");
            }

            foreach (var label in FindObjectsByType<OfficeStatusLabel>(FindObjectsSortMode.None))
            {
                if (label.Agent == null) continue;
                if (label.Agent.AgentId == "father") label.SetDisplayName("아빠 · 46살 · 임시 에셋");
                if (label.Agent.AgentId == "mother") label.SetDisplayName("엄마 · 44살 · 임시 에셋");
            }

            var oldFather = GameObject.Find("Father Placeholder (46)");
            var oldMother = GameObject.Find("Mother Placeholder (44)");
            if (oldFather != null) oldFather.SetActive(false);
            if (oldMother != null) oldMother.SetActive(false);
        }
    }
}

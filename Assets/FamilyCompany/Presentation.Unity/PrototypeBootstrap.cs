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
using FamilyCompany.Presentation.Unity.OfficeRuntime;
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
        private int _styleHeight;
        private int _activeSlot = UnityJsonSaveRepository.MinimumSlot;
        private int _pendingNewGameSlot;
        private bool _hasSession;
        private PrototypeUiScreen _screen = PrototypeUiScreen.MainMenu;
        private PrototypeUiScreen _slotReturnScreen = PrototypeUiScreen.MainMenu;
        private OfficeContractTaskCoordinator _contractTaskCoordinator;
        private TitleMoneyRainRenderer _titleMoneyRainRenderer;
        private OfficeAutonomyCoordinator _officeAutonomyCoordinator;
        private PlayerOfficeWorkInteractor _playerWorkInteractor;
        private int _reportedOfficeTaskCount;
        private ManagementUiV2Presenter _managementUiPresenter;
        private float _worldTimeScale = 1f;
        private double _officeRealtimeAccumulatorSeconds;
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
            InitializeNow();
            if (runtimeAgents == null || runtimeAgents.Length < CanonicalFamilyIds.Length)
                throw new ArgumentException("Starter Office requires the four canonical family actors.", nameof(runtimeAgents));
            if (runtimeAgents.Any(item => item == null) ||
                runtimeAgents.Select(item => item.AgentId).Distinct(StringComparer.Ordinal).Count() != runtimeAgents.Length)
                throw new InvalidOperationException("Starter Office runtime actor IDs must be unique.");
            var runtimeIds = new HashSet<string>(
                runtimeAgents.Select(item => item.AgentId),
                StringComparer.Ordinal);
            if (CanonicalFamilyIds.Any(id => !runtimeIds.Contains(id)))
                throw new InvalidOperationException("Starter Office runtime is missing a canonical family actor.");

            _contractTaskCoordinator = GetComponent<OfficeContractTaskCoordinator>();
            if (_contractTaskCoordinator == null)
                _contractTaskCoordinator = gameObject.AddComponent<OfficeContractTaskCoordinator>();
            _contractTaskCoordinator.ConfigureRuntime(this, runtimeAgents);
            _contractTaskCoordinator.InitializeNow();

            _officeAutonomyCoordinator = GetComponent<OfficeAutonomyCoordinator>();
            if (_officeAutonomyCoordinator == null)
                _officeAutonomyCoordinator = gameObject.AddComponent<OfficeAutonomyCoordinator>();
            _officeAutonomyCoordinator.ConfigureRuntime(this, runtimeAgents);
            _officeAutonomyCoordinator.InitializeNow();

            if (_playerWorkInteractor != null) _playerWorkInteractor.enabled = false;
            EnsureManagementUiPresenter();
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
            _officeObservationCamera = true;
            ApplyOfficeObservationCamera(true);
            SetSimulationPaused(false);
            ScenePreviewJump.ShowStarterOffice();
            _notice = $"창업 자본 {PrototypeStateFactory.StartingCapitalWon:N0}원 · 네 식구의 오피스텔 회사를 시작합니다.";
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-familyCompanyCaptureMoneyRain") >= 0) return;
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
                    DrawMenuBackground("새로운 2000년");
                    DrawSlotPicker("처음하기", "새 회사를 시작할 저장 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.SaveSlots:
                    DrawSlotPicker("저장하기", "현재 회사를 저장할 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.LoadSlots:
                    if (!_hasSession) DrawMenuBackground("기록에서 이어지는 회사");
                    DrawSlotPicker("불러오기", "이어갈 회사를 선택하세요.", false);
                    break;
                case PrototypeUiScreen.ConfirmNewGame:
                    DrawMenuBackground("새로운 2000년");
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
            var scale = compact
                ? Mathf.Clamp(Mathf.Min(Screen.width / 440f, Screen.height / 481f), 0.82f, 1.18f)
                : Mathf.Clamp(Screen.height / (float)ReferenceHeight, 0.72f, 1.25f);
            var x = compact ? 20f * scale : Mathf.Max(72f, Screen.width * 0.065f);
            var top = compact ? 21f * scale : 62f * scale;
            var menuWidth = compact
                ? Mathf.Min(190f * scale, Screen.width * 0.48f)
                : Mathf.Clamp(288f * scale, 230f, 340f);
            var titleWidth = compact ? Mathf.Min(280f * scale, Screen.width - x - 16f) : 520f * scale;

            DrawColoredLabel(new Rect(x + 2f, top, titleWidth, 20f * scale),
                "FAMILY COMPANY  /  SEOUL 2000", _mainChipStyle, new Color(1f, 0.48f, 0.34f, 1f));

            var titleRect = new Rect(x, top + 20f * scale, titleWidth, 74f * scale);
            var shadowRect = titleRect;
            shadowRect.x += 3f * scale;
            shadowRect.y += 4f * scale;
            GUI.Label(shadowRect, "가족회사", _mainTitleShadowStyle);
            GUI.Label(titleRect, "가족회사", _mainTitleStyle);
            DrawColoredLabel(new Rect(x + 2f, top + 90f * scale, titleWidth, 24f * scale),
                "네 식구가 함께 만드는 작은 회사의 역사", _mainSubtitleStyle,
                new Color(0.93f, 0.86f, 0.72f, 0.96f));

            var menuY = compact ? 141f * scale : 276f * scale;
            var rowHeight = compact ? 33f * scale : 47f * scale;
            var gap = compact ? 5f * scale : 8f * scale;
            var latestSlot = GetLatestSaveSlot();

            if (DrawLeftMenuRow(new Rect(x, menuY, menuWidth, rowHeight), "01", "새 회사", true,
                    MainMenuButtonKind.Primary))
                ShowNewGameSlotsNow();
            menuY += rowHeight + gap;
            if (DrawLeftMenuRow(new Rect(x, menuY, menuWidth, rowHeight), "02", "이어하기", latestSlot.HasValue,
                    MainMenuButtonKind.Standard))
                ContinueLatestNow();
            menuY += rowHeight + gap;
            if (DrawLeftMenuRow(new Rect(x, menuY, menuWidth, rowHeight), "03", "불러오기", true,
                    MainMenuButtonKind.Standard))
                ShowLoadSlotsNow();
            menuY += rowHeight + gap;
            if (DrawLeftMenuRow(new Rect(x, menuY, menuWidth, rowHeight), "04", "화면 설정", true,
                    MainMenuButtonKind.Standard))
                ToggleFullscreenNow();
            menuY += rowHeight + gap;
            if (DrawLeftMenuRow(new Rect(x, menuY, menuWidth, rowHeight), "05", "종료", true,
                    MainMenuButtonKind.Danger))
                Application.Quit();

            var statusY = compact ? Screen.height - 59f * scale : menuY + rowHeight + 24f * scale;
            DrawSolid(new Rect(x, statusY, menuWidth, 1f), new Color(1f, 0.48f, 0.34f, 0.72f));
            DrawColoredLabel(new Rect(x, statusY + 7f * scale, menuWidth, 19f * scale),
                "2000.01.03  ·  가족 4명  ·  500만원", _mainInfoStyle,
                new Color(1f, 0.89f, 0.69f, 1f));
            DrawColoredLabel(new Rect(x, statusY + 27f * scale, menuWidth, 18f * scale),
                string.IsNullOrWhiteSpace(_notice) ? "작은 의뢰 하나부터 시작하세요." : _notice,
                _mainButtonDetailStyle, new Color(0.83f, 0.78f, 0.70f, 0.92f));
        }

        private bool DrawLeftMenuRow(
            Rect rect,
            string index,
            string title,
            bool enabled,
            MainMenuButtonKind kind)
        {
            var style = kind == MainMenuButtonKind.Primary
                ? _mainButtonPrimaryStyle
                : kind == MainMenuButtonKind.Danger ? _mainButtonDangerStyle : _mainButtonStyle;
            if (!enabled) style = _mainButtonDisabledStyle;
            var previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            var clicked = GUI.Button(rect, GUIContent.none, style);
            GUI.enabled = previousEnabled;

            var primary = kind == MainMenuButtonKind.Primary;
            var danger = kind == MainMenuButtonKind.Danger;
            var indexColor = enabled
                ? primary ? new Color(0.35f, 0.12f, 0.10f, 0.84f) : new Color(1f, 0.48f, 0.34f, 0.92f)
                : new Color(0.49f, 0.48f, 0.46f, 0.78f);
            var titleColor = enabled
                ? primary ? new Color(0.17f, 0.08f, 0.07f, 1f)
                    : danger ? new Color(1f, 0.58f, 0.48f, 1f) : new Color(1f, 0.94f, 0.82f, 1f)
                : new Color(0.49f, 0.48f, 0.46f, 0.88f);
            var side = Mathf.Clamp(rect.height * 0.95f, 28f, 46f);
            DrawColoredLabel(new Rect(rect.x + 5f, rect.y, side, rect.height),
                index, _mainButtonIndexStyle, indexColor);
            DrawColoredLabel(new Rect(rect.x + side, rect.y, rect.width - side - 12f, rect.height),
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

        public void AssignContractWorkNow(string offerId, string memberId)
        {
            var member = _state.Family.Get(memberId);
            if (member.Role == FamilyRole.Player)
            {
                _notice = "나는 월드에서 직접 책상과 상호작용해 작업에 참여합니다.";
                return;
            }

            var schedule = FamilyScheduleRules.Resolve(member.Role, _state.Time.Now);
            if (!schedule.CanPerformCompanyWork)
            {
                _notice = $"{member.DisplayName}은 지금 {schedule.Label} 중이라 회사 작업을 맡을 수 없습니다.";
                return;
            }

            if (_contractTaskCoordinator == null) InitializeOfficeTaskBridgeNow();
            if (_contractTaskCoordinator != null && _contractTaskCoordinator.AssignContractWork(offerId, memberId, 4))
            {
                _notice = $"{member.DisplayName}에게 4시간 작업을 배정했습니다.";
                return;
            }

            _notice = _contractTaskCoordinator != null &&
                      !string.IsNullOrWhiteSpace(_contractTaskCoordinator.LastAssignmentFailureLabel)
                ? _contractTaskCoordinator.LastAssignmentFailureLabel
                : "해당 가족이 이미 작업 중이거나 사용할 책상이 없습니다.";
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
            if (ScenePreviewJump.IsPresentationLoading) return;
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
                ? $"계약 완료 · 보상 {result.RewardWon:N0}원 입금"
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
            DrawSolid(rect, new Color(1f, 0.97f, 0.88f, 0.98f));
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
            var panelWidth = Mathf.Min(1380f, Screen.width - 120f);
            var panelHeight = Mathf.Min(650f, Screen.height - 130f);
            var panel = CenteredRect(panelWidth, panelHeight);
            DrawSolid(panel, new Color(1f, 0.97f, 0.88f, 0.98f));
            GUI.Label(new Rect(panel.x + 44f, panel.y + 35f, panel.width - 88f, 45f), title, _headingStyle);
            GUI.Label(new Rect(panel.x + 44f, panel.y + 82f, panel.width - 88f, 34f), description, _bodyStyle);

            const float gap = 22f;
            var cardY = panel.y + 145f;
            var cardWidth = (panel.width - 88f - gap * 2f) / 3f;
            var cardHeight = panel.height - 245f;
            var previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.78f, 0.92f, 0.96f, 1f);
            for (var slot = UnityJsonSaveRepository.MinimumSlot; slot <= UnityJsonSaveRepository.MaximumSlot; slot++)
            {
                var repository = GetRepository(slot);
                var card = new Rect(panel.x + 44f + (slot - 1) * (cardWidth + gap), cardY, cardWidth, cardHeight);
                var label = BuildSlotLabel(repository);
                GUI.enabled = canUseEmptySlot || repository.Exists;
                if (GUI.Button(card, label, _slotStyle))
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
                GUI.enabled = true;
            }

            if (GUI.Button(new Rect(panel.x + 44f, panel.yMax - 72f, 180f, 42f), "뒤로", _buttonStyle)) ReturnFromSlotScreen();
            GUI.backgroundColor = previousBackgroundColor;
        }

        private string BuildSlotLabel(UnityJsonSaveRepository repository)
        {
            if (!repository.TryLoad(out var save))
            {
                return $"SLOT {repository.Slot}\n\n빈 슬롯\n\n새 가족회사를 시작할 수 있습니다.";
            }

            var campaignDate = GameTime.CampaignStart.AddMinutes(save.elapsedMinutes);
            var savedAt = repository.LastWriteTimeLocal;
            var businessCount = save.schemaVersion >= 4 ? save.growth?.ownedBusinesses?.Count ?? 0 : 0;
            return $"SLOT {repository.Slot}\n\n{save.company.companyName}\n{campaignDate:yyyy.MM.dd HH:mm}\n\n현금  {save.company.cashWon:N0}원\n평판  {save.company.reputation}\n계약  {save.contracts?.Count ?? 0}건 · 자체 사업 {businessCount}개\n\n저장 {savedAt:yyyy.MM.dd HH:mm}";
        }

        private void DrawNewGameConfirmation()
        {
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.20f, 0.40f, 0.42f, 0.34f));
            var panel = CenteredRect(620f, 340f);
            DrawSolid(panel, new Color(1f, 0.97f, 0.88f, 0.99f));
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
            var targetHeight = Mathf.Max(720, Screen.height);
            if (_titleStyle != null && _styleHeight == targetHeight) return;
            _styleHeight = targetHeight;
            var scale = Mathf.Clamp(targetHeight / (float)ReferenceHeight, 0.75f, 1.35f);
            EnsureSolidTexture();
            EnsureMainMenuTextures();
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(70f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.08f, 0.30f, 0.31f) }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(27f * scale),
                normal = { textColor = new Color(0.20f, 0.48f, 0.46f) }
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.08f, 0.30f, 0.31f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                wordWrap = true,
                normal = { textColor = new Color(0.12f, 0.27f, 0.28f) }
            };
            _smallStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = new Color(0.25f, 0.46f, 0.45f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(20f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(22, 18, 8, 8),
                margin = new RectOffset(0, 0, 0, 10),
                normal = { textColor = new Color(0.08f, 0.28f, 0.29f) },
                hover = { textColor = new Color(0.89f, 0.28f, 0.25f) }
            };
            _buttonStyle.normal.background = _solidTexture;
            _buttonStyle.hover.background = _solidTexture;
            _buttonStyle.active.background = _solidTexture;
            _slotStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(24, 24, 24, 20),
                normal = { textColor = new Color(0.08f, 0.28f, 0.29f) },
                hover = { textColor = new Color(0.89f, 0.28f, 0.25f) }
            };
            _slotStyle.normal.background = _solidTexture;
            _slotStyle.hover.background = _solidTexture;
            _slotStyle.active.background = _solidTexture;
            _panelStyle = new GUIStyle(GUI.skin.box);

            _mainTitleStyle = new GUIStyle(_titleStyle)
            {
                fontSize = Mathf.RoundToInt(76f * scale),
                normal = { textColor = new Color(1f, 0.94f, 0.78f, 1f) }
            };
            _mainTitleShadowStyle = new GUIStyle(_mainTitleStyle)
            {
                normal = { textColor = new Color(0.01f, 0.09f, 0.10f, 0.80f) }
            };
            _mainSubtitleStyle = new GUIStyle(_subtitleStyle)
            {
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.86f, 0.72f, 1f) }
            };
            _mainChipStyle = new GUIStyle(_smallStyle)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.48f, 0.34f, 1f) }
            };
            _mainButtonTitleStyle = new GUIStyle(_headingStyle)
            {
                fontSize = Mathf.RoundToInt(21f * scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            _mainButtonDetailStyle = new GUIStyle(_smallStyle)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.83f, 0.78f, 0.70f, 0.92f) }
            };
            _mainButtonIndexStyle = new GUIStyle(_headingStyle)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.48f, 0.34f, 0.92f) }
            };
            _mainInfoStyle = new GUIStyle(_smallStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 0.89f, 0.69f, 1f) }
            };
            _mainShortcutStyle = new GUIStyle(_smallStyle)
            {
                fontSize = Mathf.RoundToInt(13f * scale),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.69f, 0.87f, 0.82f, 0.92f) }
            };
            _mainCompactTextButtonStyle = new GUIStyle(_mainShortcutStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = new Color(0.72f, 0.91f, 0.85f, 0.96f) },
                hover = { textColor = new Color(1f, 0.91f, 0.63f, 1f) },
                active = { textColor = new Color(1f, 0.60f, 0.43f, 1f) }
            };
            _mainCompactDangerButtonStyle = new GUIStyle(_mainCompactTextButtonStyle)
            {
                normal = { textColor = new Color(1f, 0.58f, 0.50f, 0.96f) },
                hover = { textColor = new Color(1f, 0.80f, 0.66f, 1f) },
                active = { textColor = new Color(0.90f, 0.30f, 0.26f, 1f) }
            };

            var roundedBorder = new RectOffset(24, 24, 24, 24);
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
            if (_mainButtonTexture != null) return;
            _mainButtonTexture = BuildRoundedUiTexture(
                "Main Menu Charcoal",
                new Color(0.11f, 0.095f, 0.09f, 0.92f),
                new Color(0.055f, 0.047f, 0.045f, 0.96f),
                new Color(0.58f, 0.49f, 0.40f, 0.72f));
            _mainButtonHoverTexture = BuildRoundedUiTexture(
                "Main Menu Warm Hover",
                new Color(0.98f, 0.79f, 0.57f, 1f),
                new Color(0.94f, 0.46f, 0.31f, 1f),
                new Color(1f, 0.91f, 0.71f, 1f));
            _mainButtonActiveTexture = BuildRoundedUiTexture(
                "Main Menu Active",
                new Color(0.98f, 0.57f, 0.36f, 1f),
                new Color(0.86f, 0.35f, 0.28f, 1f),
                new Color(1f, 0.88f, 0.66f, 1f));
            _mainButtonPrimaryTexture = BuildRoundedUiTexture(
                "Main Menu Coral Primary",
                new Color(1f, 0.66f, 0.46f, 1f),
                new Color(0.93f, 0.35f, 0.27f, 1f),
                new Color(1f, 0.88f, 0.67f, 1f));
            _mainButtonPrimaryHoverTexture = BuildRoundedUiTexture(
                "Main Menu Coral Primary Hover",
                new Color(1f, 0.83f, 0.58f, 1f),
                new Color(1f, 0.49f, 0.31f, 1f),
                new Color(1f, 0.96f, 0.78f, 1f));
            _mainButtonDangerTexture = BuildRoundedUiTexture(
                "Main Menu Danger",
                new Color(0.20f, 0.24f, 0.25f, 0.96f),
                new Color(0.11f, 0.13f, 0.14f, 0.98f),
                new Color(0.91f, 0.39f, 0.34f, 0.92f));
            _mainButtonDisabledTexture = BuildRoundedUiTexture(
                "Main Menu Disabled",
                new Color(0.105f, 0.095f, 0.09f, 0.82f),
                new Color(0.065f, 0.058f, 0.055f, 0.88f),
                new Color(0.30f, 0.27f, 0.24f, 0.62f));
            _mainInfoTexture = BuildRoundedUiTexture(
                "Main Menu Info",
                new Color(0.11f, 0.095f, 0.09f, 0.90f),
                new Color(0.055f, 0.047f, 0.045f, 0.94f),
                new Color(0.75f, 0.50f, 0.36f, 0.72f));
            _mainChipTexture = BuildRoundedUiTexture(
                "Main Menu Chip",
                new Color(1f, 0.88f, 0.67f, 0.98f),
                new Color(0.97f, 0.57f, 0.38f, 0.98f),
                new Color(1f, 0.96f, 0.82f, 1f));
        }

        private static Texture2D BuildRoundedUiTexture(string textureName, Color top, Color bottom, Color borderColor)
        {
            const int width = 96;
            const int height = 64;
            const int radius = 15;
            const int border = 3;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (var y = 0; y < height; y++)
            {
                var progress = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    if (!IsInsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var insideFill = IsInsideRoundedRect(
                        x - border,
                        y - border,
                        width - border * 2,
                        height - border * 2,
                        radius - border);
                    var color = insideFill ? Color.Lerp(top, bottom, progress) : borderColor;
                    if (insideFill && y <= border + 1) color = Color.Lerp(color, Color.white, 0.12f);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            if (x >= radius && x < width - radius) return true;
            if (y >= radius && y < height - radius) return true;
            var centerX = x < radius ? radius : width - radius - 1;
            var centerY = y < radius ? radius : height - radius - 1;
            var dx = x - centerX;
            var dy = y - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static void DestroyRuntimeTexture(Texture2D texture)
        {
            if (texture == null) return;
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

using System;
using System.Collections;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public enum PrototypeUiScreen
    {
        MainMenu,
        Playing,
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
        private const string TitleHeroResourcePath = "Title/family_company_title_hero_v1";
        private const string ManagementDashboardResourcePath = "OfficeManagementDashboard_v1";
        private const string BusinessExpansionDashboardResourcePath = "BusinessExpansionDashboard_v1";
        private static readonly string[] ContractClientIds = { "samsung-electronics", "lg-electronics", "sk-telecom" };
        private static readonly string[] ContractClientNames = { "삼성전자", "LG전자", "SK텔레콤" };
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
        private Texture2D _solidTexture;
        private Texture2D _titleHeroTexture;
        private Texture2D _menuGradientTexture;
        private Texture2D _managementDashboardTexture;
        private Texture2D _businessExpansionDashboardTexture;
        private int _styleHeight;
        private int _activeSlot = UnityJsonSaveRepository.MinimumSlot;
        private int _pendingNewGameSlot;
        private bool _hasSession;
        private PrototypeUiScreen _screen = PrototypeUiScreen.MainMenu;
        private PrototypeUiScreen _slotReturnScreen = PrototypeUiScreen.MainMenu;
        private OfficeContractTaskCoordinator _contractTaskCoordinator;
        private OfficeManagementTab _managementTab = OfficeManagementTab.Contracts;
        private GUIStyle _managementHeadingStyle;
        private GUIStyle _managementBodyStyle;
        private GUIStyle _managementSmallStyle;
        private GUIStyle _managementButtonStyle;
        private GUIStyle _managementTabStyle;
        private int _contractBoardPage;
        private int _reportedOfficeTaskCount;
        private string _productTitle = "우리 가족 업무도우미";
        private BusinessIndustry _contractIndustry = BusinessIndustry.WebAndSoftware;
        private BusinessIndustry _selectedBusinessIndustry = BusinessIndustry.WebAndSoftware;

        public GameState State => _state;
        public PrototypeUiScreen UiScreen => _screen;
        public int ActiveSlot => _activeSlot;
        public bool HasSession => _hasSession;
        public bool HasAnySave => GetLatestSaveSlot() != null;

        private void Awake()
        {
            InitializeNow();
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
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S) && _screen == PrototypeUiScreen.Playing)
            {
                SaveSlotNow(_activeSlot);
            }

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            switch (_screen)
            {
                case PrototypeUiScreen.Playing:
                    ShowPauseMenuNow();
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
            if (_menuGradientTexture != null) Destroy(_menuGradientTexture);
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
            _contractTaskCoordinator = GetComponent<OfficeContractTaskCoordinator>();
            if (_contractTaskCoordinator == null)
            {
                _contractTaskCoordinator = gameObject.AddComponent<OfficeContractTaskCoordinator>();
            }

            _contractTaskCoordinator.Configure(this, agents, waypoints);
            _contractTaskCoordinator.InitializeNow();
            return _contractTaskCoordinator;
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
            _slotReturnScreen = PrototypeUiScreen.PauseMenu;
            _screen = PrototypeUiScreen.SaveSlots;
            SetSimulationPaused(true);
        }

        public void ShowLoadSlotsNow()
        {
            _slotReturnScreen = _hasSession ? PrototypeUiScreen.PauseMenu : PrototypeUiScreen.MainMenu;
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
            SetSimulationPaused(false);
            _notice = $"창업 자본 {PrototypeStateFactory.StartingCapitalWon:N0}원 · 네 식구의 오피스텔 회사를 시작합니다.";
            _managementTab = OfficeManagementTab.Contracts;
            _contractBoardPage = 0;
            _contractIndustry = BusinessIndustry.WebAndSoftware;
            _selectedBusinessIndustry = BusinessIndustry.WebAndSoftware;
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
                return true;
            }
            catch (Exception exception)
            {
                _notice = $"저장 실패: {exception.Message}";
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
                    return false;
                }

                _state = GameSaveMapper.FromDto(save);
                _runner = new SimulationRunner(_state);
                _contractTaskCoordinator?.ResetAssignments();
                _activeSlot = slot;
                _hasSession = true;
                _screen = PrototypeUiScreen.Playing;
                SetSimulationPaused(false);
                _notice = $"슬롯 {slot} 불러오기 완료";
                return true;
            }
            catch (Exception exception)
            {
                _notice = $"불러오기 실패: {exception.Message}";
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
            _contractTaskCoordinator?.ResetAssignments();
            _reportedOfficeTaskCount = 0;
        }

        private void ConfigureDisplayDefaults()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
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
            if (Application.isPlaying) Time.timeScale = paused ? 0f : 1f;
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
                    DrawGameHud();
                    break;
                case PrototypeUiScreen.PauseMenu:
                    DrawGameHud();
                    DrawPauseMenu();
                    break;
                case PrototypeUiScreen.NewGameSlots:
                    DrawMenuBackground("새로운 2000년");
                    DrawSlotPicker("처음하기", "새 회사를 시작할 저장 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.SaveSlots:
                    DrawGameHud();
                    DrawSlotPicker("저장하기", "현재 회사를 저장할 슬롯을 선택하세요.", true);
                    break;
                case PrototypeUiScreen.LoadSlots:
                    if (_hasSession) DrawGameHud();
                    else DrawMenuBackground("기록에서 이어지는 회사");
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
            DrawMenuBackground("2000년 1월 3일 · 대한민국");
            var width = Mathf.Clamp(Screen.width * 0.31f, 450f, 620f);
            var x = Mathf.Max(70f, Screen.width * 0.075f);
            var top = Mathf.Max(105f, Screen.height * 0.12f);
            GUILayout.BeginArea(new Rect(x, top, width, Screen.height - top - 70f));
            GUILayout.Label("가족회사", _titleStyle);
            GUILayout.Label("네 식구가 시작한 가장 작은 회사", _subtitleStyle);
            GUILayout.Space(34f);
            if (GUILayout.Button("처음하기", _buttonStyle, GUILayout.Height(62f))) ShowNewGameSlotsNow();

            GUI.enabled = HasAnySave;
            if (GUILayout.Button("이어하기", _buttonStyle, GUILayout.Height(62f))) ContinueLatestNow();
            if (GUILayout.Button("불러오기", _buttonStyle, GUILayout.Height(62f))) ShowLoadSlotsNow();
            GUI.enabled = true;

            var mode = Screen.fullScreenMode == FullScreenMode.Windowed ? "창 모드" : "전체 화면";
            if (GUILayout.Button($"화면 설정 · {mode}", _buttonStyle, GUILayout.Height(56f))) ToggleFullscreenNow();
            if (GUILayout.Button("종료", _buttonStyle, GUILayout.Height(56f))) Application.Quit();
            GUILayout.Space(18f);
            GUILayout.Label(_notice, _bodyStyle);
            GUILayout.Space(7f);
            GUILayout.Label("2000년의 작은 하청 사무실에서 시작해 실제 회사들과 경쟁하고 역사를 바꾸세요.", _smallStyle);
            GUILayout.EndArea();
            GUI.Label(
                new Rect(Screen.width - 360f, Screen.height - 108f, 320f, 72f),
                "F11  전체 화면 전환\nESC  게임 메뉴\nCtrl+S  현재 슬롯 빠른 저장",
                _smallStyle);
        }

        private void DrawMenuBackground(string eyebrow)
        {
            var fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (_titleHeroTexture == null) _titleHeroTexture = Resources.Load<Texture2D>(TitleHeroResourcePath);
            if (_titleHeroTexture != null) DrawTextureAspectFill(fullScreen, _titleHeroTexture);
            else DrawSolid(fullScreen, new Color(0.025f, 0.055f, 0.075f, 1f));
            DrawSolid(fullScreen, new Color(0.01f, 0.025f, 0.035f, 0.12f));
            DrawMenuGradient(new Rect(0f, 0f, Screen.width * 0.64f, Screen.height));
            DrawSolid(new Rect(0f, Screen.height - 14f, Screen.width, 14f), new Color(0.96f, 0.49f, 0.38f, 1f));
            GUI.Label(new Rect(Screen.width * 0.075f, 46f, Screen.width * 0.6f, 40f), eyebrow, _smallStyle);
        }

        private void DrawMenuGradient(Rect target)
        {
            if (_menuGradientTexture == null)
            {
                _menuGradientTexture = new Texture2D(64, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                for (var index = 0; index < _menuGradientTexture.width; index++)
                {
                    var progress = index / (float)(_menuGradientTexture.width - 1);
                    var alpha = Mathf.Pow(1f - progress, 1.7f) * 0.62f;
                    _menuGradientTexture.SetPixel(index, 0, new Color(0.01f, 0.025f, 0.035f, alpha));
                }
                _menuGradientTexture.Apply(false, true);
            }

            GUI.DrawTexture(target, _menuGradientTexture, ScaleMode.StretchToFill, true);
        }

        private static void DrawTextureAspectFill(Rect target, Texture texture)
        {
            var targetAspect = target.width / target.height;
            var textureAspect = texture.width / (float)texture.height;
            var source = new Rect(0f, 0f, 1f, 1f);
            if (targetAspect > textureAspect)
            {
                var visibleHeight = textureAspect / targetAspect;
                source.y = (1f - visibleHeight) * 0.5f;
                source.height = visibleHeight;
            }
            else
            {
                var visibleWidth = targetAspect / textureAspect;
                source.x = (1f - visibleWidth) * 0.5f;
                source.width = visibleWidth;
            }

            GUI.DrawTextureWithTexCoords(target, texture, source, true);
        }

        private void DrawGameHud()
        {
            if (_state == null) return;
            if (_managementDashboardTexture == null)
            {
                _managementDashboardTexture = Resources.Load<Texture2D>(ManagementDashboardResourcePath);
            }
            if (_businessExpansionDashboardTexture == null)
            {
                _businessExpansionDashboardTexture = Resources.Load<Texture2D>(BusinessExpansionDashboardResourcePath);
            }
            var dashboardTexture = _managementTab == OfficeManagementTab.Products
                ? _businessExpansionDashboardTexture
                : _managementDashboardTexture;
            if (dashboardTexture != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), dashboardTexture, ScaleMode.StretchToFill, true);
            }
            else
            {
                DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.89f, 0.91f, 0.84f));
            }

            EnsureManagementStyles();
            SyncOfficeTaskNotice();
            GUI.Label(DashboardRect(48f, 25f, 520f, 48f), $"{_state.Company.CompanyName} · 임차 오피스텔", _managementHeadingStyle);
            GUI.Label(DashboardRect(600f, 27f, 430f, 42f), _state.Time.Now.ToString("yyyy년 MM월 dd일 ddd HH:mm"), _managementBodyStyle);
            GUI.Label(
                DashboardRect(1160f, 27f, 430f, 42f),
                $"현금 {_state.Company.CashWon:N0}원  ·  평판 {_state.Company.Reputation}",
                _managementBodyStyle);

            DrawFamilyRoster();
            DrawManagementTabs();
            switch (_managementTab)
            {
                case OfficeManagementTab.Contracts:
                    DrawContractBoard();
                    break;
                case OfficeManagementTab.Research:
                    DrawResearchCenter();
                    break;
                case OfficeManagementTab.Products:
                    DrawProductPlanning();
                    break;
            }
            DrawManagementFooter();
        }

        private void DrawFamilyRoster()
        {
            GUI.Label(DashboardRect(55f, 160f, 220f, 45f), "우리 식구 · 4명", _managementHeadingStyle);
            for (var index = 0; index < _state.Family.Members.Count; index++)
            {
                var member = _state.Family.Members[index];
                var y = 236f + index * 127f;
                GUI.Label(DashboardRect(142f, y, 124f, 30f), $"{member.DisplayName} · {member.AgeAt(_state.Time)}살", _managementBodyStyle);
                GUI.Label(DashboardRect(142f, y + 32f, 124f, 45f), member.CompanyDuty, _managementSmallStyle);
                GUI.Label(
                    DashboardRect(60f, y + 80f, 205f, 34f),
                    $"개발 {member.Stats.Development}  속도 {member.Stats.Speed}  체력 {member.Energy}",
                    _managementSmallStyle);
            }
        }

        private void DrawManagementTabs()
        {
            DrawTabButton(OfficeManagementTab.Contracts, "의뢰 게시판", 330f);
            DrawTabButton(OfficeManagementTab.Research, "R&D 센터", 560f);
            DrawTabButton(OfficeManagementTab.Products, "시장·자체 제품", 790f);
            if (_managementTab == OfficeManagementTab.Contracts)
            {
                var definition = BusinessIndustryCatalog.Get(_contractIndustry);
                if (GUI.Button(DashboardRect(1030f, 94f, 300f, 34f), $"분야 · {definition.DisplayName}  ▶", _managementTabStyle))
                {
                    _contractIndustry = (BusinessIndustry)(((int)_contractIndustry + 1) % BusinessIndustryCatalog.All.Count);
                    _contractBoardPage = 0;
                    _notice = $"{BusinessIndustryCatalog.Get(_contractIndustry).DisplayName} 하청 목록으로 전환했습니다.";
                }
                GUI.Label(DashboardRect(1340f, 98f, 220f, 30f), _notice, _managementSmallStyle);
            }
            else
            {
                GUI.Label(DashboardRect(1060f, 98f, 500f, 30f), _notice, _managementSmallStyle);
            }
        }

        private void DrawTabButton(OfficeManagementTab tab, string label, float x)
        {
            var previous = GUI.color;
            GUI.color = _managementTab == tab ? new Color(0.72f, 0.93f, 0.86f) : new Color(1f, 0.96f, 0.88f);
            if (GUI.Button(DashboardRect(x, 94f, 210f, 34f), label, _managementTabStyle)) _managementTab = tab;
            GUI.color = previous;
        }

        private void DrawContractBoard()
        {
            for (var index = 0; index < 3; index++)
            {
                var offer = CreateBoardOffer(index);
                DrawContractOfferCard(offer, 330f + index * 420f);
            }

            if (GUI.Button(DashboardRect(1390f, 620f, 170f, 40f), "다른 의뢰 보기", _managementButtonStyle))
            {
                var pageCount = Math.Max(1, (BootstrapContractCatalog.OfferCountForIndustry(_contractIndustry) + 2) / 3);
                _contractBoardPage = (_contractBoardPage + 1) % pageCount;
                _notice = _contractBoardPage == 0 ? "초보자용 무위약금 목록입니다." : "고수익 의뢰는 연구와 위약금 확인이 필수입니다.";
            }

            var activeContracts = _state.Contracts.Contracts
                .Where(item => item.Status == SubcontractStatus.Active)
                .Take(2)
                .ToArray();
            GUI.Label(DashboardRect(335f, 706f, 310f, 38f), $"진행 중인 계약 · {activeContracts.Length}/2", _managementHeadingStyle);
            if (activeContracts.Length == 0)
            {
                GUI.Label(DashboardRect(335f, 755f, 820f, 70f), "위의 의뢰를 수락한 뒤 가족에게 4시간 작업을 배정하세요.", _managementBodyStyle);
                return;
            }

            for (var index = 0; index < activeContracts.Length; index++)
            {
                DrawActiveContract(activeContracts[index], 335f + index * 445f);
            }
        }

        private SubcontractOffer CreateBoardOffer(int cardIndex)
        {
            var sequence = _contractBoardPage * 3L + cardIndex;
            var clientIndex = (int)(sequence % ContractClientIds.Length);
            return BootstrapContractCatalog.CreateIndustryOffer(
                _state.WorldSeed,
                ContractClientIds[clientIndex],
                ContractClientNames[clientIndex],
                _contractIndustry,
                sequence);
        }

        private void DrawContractOfferCard(SubcontractOffer offer, float x)
        {
            var existing = _state.Contracts.Contracts.FirstOrDefault(item => item.Offer.OfferId == offer.OfferId);
            GUI.Label(DashboardRect(x, 169f, 370f, 36f), offer.ExactClientDisplayName, _managementHeadingStyle);
            GUI.Label(DashboardRect(x, 201f, 370f, 24f), BusinessIndustryCatalog.Get(offer.Industry).DisplayName, _managementSmallStyle);
            GUI.Label(DashboardRect(x, 230f, 370f, 65f), offer.Title, _managementBodyStyle);
            GUI.Label(
                DashboardRect(x, 310f, 370f, 70f),
                $"요구 능력\n개발/제작 {offer.RequiredDevelopment}  ·  작업 속도 {offer.RequiredSpeed}",
                _managementBodyStyle);
            GUI.Label(
                DashboardRect(x, 392f, 370f, 92f),
                $"마감 {offer.DeadlineDays}일  ·  작업량 {offer.EstimatedPersonHours}시간\n착수비 {offer.UpfrontCostWon:N0}원\n보상 {offer.RewardWon:N0}원",
                _managementBodyStyle);
            var penaltyColor = GUI.color;
            GUI.color = offer.PenaltyWon > 0 ? new Color(0.78f, 0.25f, 0.23f) : new Color(0.18f, 0.48f, 0.40f);
            GUI.Label(
                DashboardRect(x, 497f, 370f, 34f),
                offer.PenaltyWon == 0 ? "위약금 없음" : $"실패 위약금 {offer.PenaltyWon:N0}원",
                _managementBodyStyle);
            GUI.color = penaltyColor;
            if (!string.IsNullOrEmpty(offer.RequiredTechnologyId))
            {
                GUI.Label(
                    DashboardRect(x, 538f, 370f, 34f),
                    $"필요 연구 · {ResearchTechnologyCatalog.Get(offer.RequiredTechnologyId).DisplayName}",
                    _managementSmallStyle);
            }

            GUI.enabled = existing == null;
            var buttonLabel = existing == null ? "계약 검토 후 수락" : ContractStatusLabel(existing.Status);
            if (GUI.Button(DashboardRect(x, 585f, 370f, 46f), buttonLabel, _managementButtonStyle)) TryAcceptOffer(offer);
            GUI.enabled = true;
        }

        private void TryAcceptOffer(SubcontractOffer offer)
        {
            var result = _state.Contracts.Accept(
                offer,
                _state.Company,
                _state.Family,
                _state.Growth,
                _state.Time.ElapsedMinutes);
            _notice = result.Accepted
                ? $"계약 수락 · {offer.ExactClientDisplayName} / {offer.Title}"
                : ContractRejectionLabel(result.Decision.RejectionReason);
        }

        private void DrawActiveContract(SubcontractState contract, float x)
        {
            var dueDays = Math.Max(0, (int)Math.Ceiling((contract.DueMinute - _state.Time.ElapsedMinutes) / 1440.0));
            GUI.Label(DashboardRect(x, 750f, 420f, 28f), contract.Offer.Title, _managementBodyStyle);
            GUI.Label(
                DashboardRect(x, 781f, 420f, 28f),
                $"진행 {contract.CompletedPersonHours}/{contract.Offer.EstimatedPersonHours}시간 · 마감까지 {dueDays}일",
                _managementSmallStyle);
            for (var memberIndex = 0; memberIndex < _state.Family.Members.Count; memberIndex++)
            {
                var member = _state.Family.Members[memberIndex];
                GUI.enabled = member.Energy >= 8;
                if (GUI.Button(
                        DashboardRect(x + memberIndex * 101f, 820f, 94f, 39f),
                        $"{member.DisplayName} 4h",
                        _managementButtonStyle))
                {
                    TryAssignContractWork(contract.Offer.OfferId, member.MemberId);
                }
                GUI.enabled = true;
            }
        }

        private void TryAssignContractWork(string offerId, string memberId)
        {
            if (_contractTaskCoordinator == null) InitializeOfficeTaskBridgeNow();
            if (_contractTaskCoordinator != null && _contractTaskCoordinator.AssignContractWork(offerId, memberId, 4))
            {
                _notice = $"{_state.Family.Get(memberId).DisplayName}에게 4시간 작업을 배정했습니다.";
            }
            else
            {
                _notice = "해당 가족이 이미 작업 중이거나 사용할 책상이 없습니다.";
            }
        }

        private void DrawResearchCenter()
        {
            if (!_state.Growth.ResearchCenterUnlocked)
            {
                GUI.Label(DashboardRect(360f, 205f, 930f, 62f), "처음에는 잠겨 있는 R&D 센터", _managementHeadingStyle);
                GUI.Label(
                    DashboardRect(360f, 290f, 920f, 150f),
                    "자본을 투자해 연구 조직을 만들면 고급 능력치가 공개되고,\n3D 모델링·자동화·시장 분석 기술을 연구할 수 있습니다.",
                    _managementBodyStyle);
                GUI.Label(
                    DashboardRect(360f, 470f, 600f, 40f),
                    $"설립 투자금 · {CompanyGrowthState.ResearchCenterOpeningCostWon:N0}원",
                    _managementHeadingStyle);
                GUI.enabled = _state.Company.CashWon >= CompanyGrowthState.ResearchCenterOpeningCostWon;
                if (GUI.Button(DashboardRect(360f, 540f, 360f, 52f), "R&D 센터 설립", _managementButtonStyle))
                {
                    _state.Growth.TryOpenResearchCenter(_state.Company, _state.Time.ElapsedMinutes, out _notice);
                }
                GUI.enabled = true;
                return;
            }

            var definitions = ResearchTechnologyCatalog.All;
            for (var index = 0; index < definitions.Count; index++)
            {
                DrawResearchCard(definitions[index], 330f + index * 420f);
            }
            GUI.Label(DashboardRect(335f, 706f, 390f, 38f), "공개된 고급 능력치", _managementHeadingStyle);
            for (var index = 0; index < _state.Family.Members.Count; index++)
            {
                var member = _state.Family.Members[index];
                var relationship = member.MemberId == "player"
                    ? string.Empty
                    : $" · 나와 {_state.Family.RelationshipLabel("player", member.MemberId)}";
                GUI.Label(
                    DashboardRect(335f + (index % 2) * 440f, 752f + (index / 2) * 45f, 420f, 38f),
                    $"{member.DisplayName} · 기획{member.Stats.Planning} 아트{member.Stats.Art} 영업{member.Stats.Sales} 멘탈{member.Stats.Mental} 팀{member.Stats.Teamwork} · 기억{member.CareerMemories.Count}{relationship}",
                    _managementSmallStyle);
            }
        }

        private void DrawResearchCard(ResearchTechnologyDefinition definition, float x)
        {
            var researched = _state.Growth.HasTechnology(definition.TechnologyId);
            var prerequisiteReady = string.IsNullOrEmpty(definition.PrerequisiteId)
                                    || _state.Growth.HasTechnology(definition.PrerequisiteId);
            GUI.Label(DashboardRect(x, 170f, 370f, 65f), definition.DisplayName, _managementHeadingStyle);
            GUI.Label(DashboardRect(x, 255f, 370f, 130f), definition.Description, _managementBodyStyle);
            GUI.Label(DashboardRect(x, 410f, 370f, 40f), $"연구비 {definition.CostWon:N0}원", _managementBodyStyle);
            if (!string.IsNullOrEmpty(definition.PrerequisiteId))
            {
                GUI.Label(
                    DashboardRect(x, 465f, 370f, 55f),
                    $"선행 · {ResearchTechnologyCatalog.Get(definition.PrerequisiteId).DisplayName}",
                    _managementSmallStyle);
            }
            GUI.enabled = !researched && prerequisiteReady && _state.Company.CashWon >= definition.CostWon;
            if (GUI.Button(
                    DashboardRect(x, 570f, 370f, 52f),
                    researched ? "연구 완료" : prerequisiteReady ? "연구 투자" : "선행 연구 필요",
                    _managementButtonStyle))
            {
                _state.Growth.TryResearch(definition.TechnologyId, _state.Company, _state.Time.ElapsedMinutes, out _notice);
            }
            GUI.enabled = true;
        }

        private void DrawProductPlanning()
        {
            var definitions = BusinessIndustryCatalog.All;
            for (var index = 0; index < definitions.Count; index++)
            {
                var x = 335f + (index % 2) * 625f;
                var y = 135f + (index / 2) * 280f;
                DrawOwnedBusinessCard(definitions[index], x, y);
            }

            DrawBusinessPortfolioStrip();
        }

        private void DrawOwnedBusinessCard(BusinessIndustryDefinition definition, float x, float y)
        {
            var owned = _state.Growth.HasOwnedBusiness(definition.Industry);
            var selected = owned && _selectedBusinessIndustry == definition.Industry;
            GUI.Label(DashboardRect(x + 108f, y + 9f, 430f, 38f), definition.DisplayName, _managementHeadingStyle);
            GUI.Label(DashboardRect(x + 20f, y + 72f, 530f, 48f), definition.Description, _managementSmallStyle);
            GUI.Label(
                DashboardRect(x + 20f, y + 123f, 530f, 36f),
                $"초기 일감 · {string.Join(" · ", definition.StarterExamples)}",
                _managementSmallStyle);
            GUI.Label(
                DashboardRect(x + 20f, y + 164f, 405f, 50f),
                $"주요 직군 · {string.Join(" / ", definition.RecruitableRoles)}",
                _managementSmallStyle);

            if (owned)
            {
                var business = _state.Growth.GetOwnedBusiness(definition.Industry);
                GUI.Label(
                    DashboardRect(x + 20f, y + 213f, 390f, 30f),
                    $"운영 중 · Lv.{business.Level} · 누적 매출 {business.TotalRevenueWon:N0}원",
                    _managementSmallStyle);
                var previous = GUI.color;
                if (selected) GUI.color = new Color(0.72f, 0.93f, 0.86f);
                if (GUI.Button(DashboardRect(x + 425f, y + 205f, 130f, 40f), selected ? "선택됨" : "운영 선택", _managementButtonStyle))
                {
                    _selectedBusinessIndustry = definition.Industry;
                    _notice = $"{business.BusinessName} 운영 화면을 선택했습니다.";
                }
                GUI.color = previous;
                return;
            }

            var analysisUnlocked = _state.Growth.HasTechnology(ResearchTechnologyIds.MarketAnalysis);
            var marketReady = analysisUnlocked
                              && _state.Growth.MarketReport != null
                              && _state.Growth.MarketReport.Industry == definition.Industry;
            var costWon = _state.Growth.FoundingCostFor(definition.Industry);
            GUI.Label(
                DashboardRect(x + 20f, y + 213f, 390f, 30f),
                marketReady ? $"창업 필요 자금 · {costWon:N0}원" : "이 업종의 시장 조사가 필요합니다.",
                _managementSmallStyle);
            GUI.enabled = analysisUnlocked && _state.Company.CashWon >= (marketReady ? costWon : CompanyGrowthState.MarketReportCostWon);
            if (GUI.Button(
                    DashboardRect(x + 425f, y + 205f, 130f, 40f),
                    marketReady ? "이 사업 시작" : "이 분야 조사",
                    _managementButtonStyle))
            {
                _selectedBusinessIndustry = definition.Industry;
                if (!marketReady)
                {
                    _state.Growth.TryPurchaseMarketReport(
                        _state.WorldSeed,
                        definition.Industry,
                        _state.Company,
                        _state.Time.ElapsedMinutes,
                        out _notice);
                }
                else if (_state.Growth.TryFoundBusiness(
                             definition.Industry,
                             _state.Company,
                             _state.Family,
                             _state.Time.ElapsedMinutes,
                             out _notice))
                {
                    _selectedBusinessIndustry = definition.Industry;
                }
            }
            GUI.enabled = true;
        }

        private void DrawBusinessPortfolioStrip()
        {
            if (_state.Growth.OwnedBusinesses.Count > 0 && !_state.Growth.HasOwnedBusiness(_selectedBusinessIndustry))
            {
                _selectedBusinessIndustry = _state.Growth.OwnedBusinesses[0].Industry;
            }

            var marketUnlocked = _state.Growth.HasTechnology(ResearchTechnologyIds.MarketAnalysis);
            GUI.Label(
                DashboardRect(335f, 704f, 300f, 35f),
                _state.Growth.CorporateStage,
                _managementHeadingStyle);
            GUI.Label(
                DashboardRect(335f, 735f, 300f, 28f),
                $"사업 {_state.Growth.OwnedBusinesses.Count}/4 · 글로벌 준비 {_state.Growth.GlobalExpansionReadiness(_state.Company)}%",
                _managementSmallStyle);
            if (_state.Growth.MarketReport == null)
            {
                GUI.Label(DashboardRect(335f, 764f, 285f, 48f), "시장 보고서를 사야 첫 사업을 선택할 수 있습니다.", _managementSmallStyle);
            }
            else
            {
                var report = _state.Growth.MarketReport;
                GUI.Label(
                    DashboardRect(335f, 764f, 300f, 55f),
                    $"{BusinessIndustryCatalog.Get(report.Industry).DisplayName} · {report.Genre}\n핵심 · {report.DesiredFeature} · 수요 {report.Demand}/100",
                    _managementSmallStyle);
            }
            GUI.enabled = marketUnlocked && _state.Company.CashWon >= CompanyGrowthState.MarketReportCostWon;
            if (GUI.Button(DashboardRect(335f, 824f, 285f, 34f), "시장 재조사 · 100,000원", _managementButtonStyle))
            {
                _state.Growth.TryPurchaseMarketReport(
                    _state.WorldSeed,
                    _selectedBusinessIndustry,
                    _state.Company,
                    _state.Time.ElapsedMinutes,
                    out _notice);
            }
            GUI.enabled = true;

            if (_state.Growth.OwnedBusinesses.Count == 0)
            {
                GUI.Label(
                    DashboardRect(650f, 727f, 555f, 110f),
                    "하청으로 현금을 모으고 R&D·시장조사를 마친 뒤 첫 사업을 창업하세요.\n두 번째 분야부터 창업비가 3,000,000원씩 올라가며, 네 분야를 모두 확장하면 글로벌 기업 도전 기반이 열립니다.",
                    _managementBodyStyle);
                return;
            }

            var selectedBusiness = _state.Growth.GetOwnedBusiness(_selectedBusinessIndustry);
            GUI.Label(
                DashboardRect(650f, 704f, 300f, 35f),
                $"{selectedBusiness.BusinessName} · 제품 기획",
                _managementHeadingStyle);
            _productTitle = GUI.TextField(DashboardRect(650f, 747f, 300f, 38f), _productTitle, 28);
            var canStart = _state.Growth.MarketReport != null
                           && _state.Growth.MarketReport.Industry == _selectedBusinessIndustry
                           && (_state.Growth.ProductProject == null || _state.Growth.ProductProject.Resolved);
            GUI.enabled = canStart;
            DrawProductBudgetButton(_selectedBusinessIndustry, 1_000_000, 650f, 808f, 94f);
            DrawProductBudgetButton(_selectedBusinessIndustry, 2_000_000, 750f, 808f, 94f);
            DrawProductBudgetButton(_selectedBusinessIndustry, 4_000_000, 850f, 808f, 100f);
            GUI.enabled = true;

            var project = _state.Growth.ProductProject;
            if (project == null)
            {
                GUI.Label(DashboardRect(970f, 720f, 235f, 105f), "제품 예산을 선택하면 개발이 시작됩니다. 완성도와 시장 변동에 따라 손익이 달라집니다.", _managementSmallStyle);
            }
            else if (!project.Resolved)
            {
                var remainingDays = Math.Max(0, (int)Math.Ceiling((project.DueMinute - _state.Time.ElapsedMinutes) / 1440.0));
                GUI.Label(
                    DashboardRect(970f, 715f, 235f, 125f),
                    $"개발 중 · {project.Title}\n{BusinessIndustryCatalog.Get(project.Industry).DisplayName}\n예산 {project.BudgetWon:N0}원\n출시까지 {remainingDays}일",
                    _managementSmallStyle);
            }
            else
            {
                GUI.Label(
                    DashboardRect(970f, 715f, 235f, 125f),
                    $"출시 완료 · {project.Title}\n완성도 {project.Quality}/100\n매출 {project.RevenueWon:N0}원\n손익 {(project.RevenueWon - project.BudgetWon):N0}원",
                    _managementSmallStyle);
            }
        }

        private void DrawProductBudgetButton(
            BusinessIndustry industry,
            long budgetWon,
            float x,
            float y,
            float width)
        {
            if (GUI.Button(DashboardRect(x, y, width, 38f), $"{budgetWon / 10_000}만", _managementButtonStyle))
            {
                _state.Growth.TryStartProduct(
                    industry,
                    _productTitle,
                    budgetWon,
                    _state.Company,
                    _state.Time.ElapsedMinutes,
                    out _notice);
            }
        }

        private void DrawManagementFooter()
        {
            GUI.Label(DashboardRect(1270f, 704f, 300f, 35f), $"SLOT {_activeSlot}", _managementSmallStyle);
            if (GUI.Button(DashboardRect(1270f, 748f, 95f, 42f), "+1시간", _managementButtonStyle)) AdvanceTime(60);
            if (GUI.Button(DashboardRect(1372f, 748f, 95f, 42f), "+1일", _managementButtonStyle)) AdvanceTime(1440);
            if (GUI.Button(DashboardRect(1474f, 748f, 95f, 42f), "저장", _managementButtonStyle)) ShowSaveSlotsNow();
            if (GUI.Button(DashboardRect(1270f, 806f, 95f, 42f), "불러오기", _managementButtonStyle)) ShowLoadSlotsNow();
            if (GUI.Button(DashboardRect(1372f, 806f, 95f, 42f), "일시정지", _managementButtonStyle)) ShowPauseMenuNow();
            if (GUI.Button(DashboardRect(1474f, 806f, 95f, 42f), "F11", _managementButtonStyle)) ToggleFullscreenNow();
        }

        private void AdvanceTime(long minutes)
        {
            var failedBefore = _state.Contracts.Contracts.Count(item => item.Status == SubcontractStatus.Failed);
            var productWasResolved = _state.Growth.ProductProject?.Resolved ?? false;
            _runner.AdvanceMinutes(minutes);
            var failedAfter = _state.Contracts.Contracts.Count(item => item.Status == SubcontractStatus.Failed);
            if (failedAfter > failedBefore)
            {
                _notice = $"마감 실패 {failedAfter - failedBefore}건 · 위약금과 평판이 반영됐습니다.";
            }
            else if (!productWasResolved && (_state.Growth.ProductProject?.Resolved ?? false))
            {
                _notice = $"제품 출시 완료 · 매출 {_state.Growth.ProductProject.RevenueWon:N0}원";
            }
            else
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

        private Rect DashboardRect(float x, float y, float width, float height)
        {
            return new Rect(
                x * Screen.width / 1680f,
                y * Screen.height / 945f,
                width * Screen.width / 1680f,
                height * Screen.height / 945f);
        }

        private void DrawPauseMenu()
        {
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.66f));
            var rect = CenteredRect(540f, 570f);
            DrawSolid(rect, new Color(0.035f, 0.075f, 0.095f, 0.98f));
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
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, _hasSession ? 0.68f : 0.18f));
            var panelWidth = Mathf.Min(1380f, Screen.width - 120f);
            var panelHeight = Mathf.Min(650f, Screen.height - 130f);
            var panel = CenteredRect(panelWidth, panelHeight);
            DrawSolid(panel, new Color(0.035f, 0.075f, 0.095f, 0.98f));
            GUI.Label(new Rect(panel.x + 44f, panel.y + 35f, panel.width - 88f, 45f), title, _headingStyle);
            GUI.Label(new Rect(panel.x + 44f, panel.y + 82f, panel.width - 88f, 34f), description, _bodyStyle);

            const float gap = 22f;
            var cardY = panel.y + 145f;
            var cardWidth = (panel.width - 88f - gap * 2f) / 3f;
            var cardHeight = panel.height - 245f;
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
                        _screen = PrototypeUiScreen.PauseMenu;
                    }
                    else LoadSlotNow(slot);
                }
                GUI.enabled = true;
            }

            if (GUI.Button(new Rect(panel.x + 44f, panel.yMax - 72f, 180f, 42f), "뒤로", _buttonStyle)) ReturnFromSlotScreen();
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
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.58f));
            var panel = CenteredRect(620f, 340f);
            DrawSolid(panel, new Color(0.035f, 0.075f, 0.095f, 0.99f));
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
            if (_solidTexture == null)
            {
                _solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _solidTexture.SetPixel(0, 0, Color.white);
                _solidTexture.Apply();
            }

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _solidTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            var targetHeight = Mathf.Max(720, Screen.height);
            if (_titleStyle != null && _styleHeight == targetHeight) return;
            _styleHeight = targetHeight;
            _managementHeadingStyle = null;
            _managementBodyStyle = null;
            _managementSmallStyle = null;
            _managementButtonStyle = null;
            _managementTabStyle = null;
            var scale = Mathf.Clamp(targetHeight / (float)ReferenceHeight, 0.75f, 1.35f);
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(70f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.91f, 0.77f) }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(27f * scale),
                normal = { textColor = new Color(0.68f, 0.83f, 0.80f) }
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.91f, 0.77f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _smallStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = new Color(0.68f, 0.83f, 0.80f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(20f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(22, 18, 8, 8),
                margin = new RectOffset(0, 0, 0, 10),
                normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 0.82f, 0.55f) }
            };
            _slotStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(24, 24, 24, 20),
                normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 0.82f, 0.55f) }
            };
            _panelStyle = new GUIStyle(GUI.skin.box);
        }

        private void EnsureManagementStyles()
        {
            if (_managementHeadingStyle != null) return;
            var scale = Mathf.Clamp(Screen.height / 945f, 0.72f, 1.35f);
            var ink = new Color(0.16f, 0.22f, 0.23f);
            var mutedInk = new Color(0.28f, 0.40f, 0.39f);
            _managementHeadingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = ink }
            };
            _managementBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(17f * scale),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = ink }
            };
            _managementSmallStyle = new GUIStyle(_managementBodyStyle)
            {
                fontSize = Mathf.RoundToInt(14f * scale),
                normal = { textColor = mutedInk }
            };
            _managementButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = ink },
                hover = { textColor = new Color(0.72f, 0.20f, 0.18f) },
                active = { textColor = new Color(0.12f, 0.42f, 0.36f) }
            };
            _managementTabStyle = new GUIStyle(_managementButtonStyle)
            {
                fontSize = Mathf.RoundToInt(17f * scale)
            };
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

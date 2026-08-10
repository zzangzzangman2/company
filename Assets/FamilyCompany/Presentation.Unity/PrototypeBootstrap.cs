using System;
using System.Collections;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
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

    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private const int ReferenceHeight = 1080;
        private const string TitleHeroResourcePath = "Title/family_company_title_hero_v1";
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
        private int _styleHeight;
        private int _activeSlot = UnityJsonSaveRepository.MinimumSlot;
        private int _pendingNewGameSlot;
        private bool _hasSession;
        private PrototypeUiScreen _screen = PrototypeUiScreen.MainMenu;
        private PrototypeUiScreen _slotReturnScreen = PrototypeUiScreen.MainMenu;
        private OfficeContractTaskCoordinator _contractTaskCoordinator;

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
            _notice = $"슬롯 {slot}에서 새 게임을 시작했습니다.";
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
            DrawSolid(new Rect(0f, 0f, Screen.width * 0.53f, Screen.height), new Color(0.01f, 0.025f, 0.035f, 0.48f));
            DrawSolid(new Rect(0f, Screen.height - 14f, Screen.width, 14f), new Color(0.96f, 0.49f, 0.38f, 1f));
            GUI.Label(new Rect(Screen.width * 0.075f, 46f, Screen.width * 0.6f, 40f), eyebrow, _smallStyle);
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
            var margin = Mathf.Clamp(Screen.width * 0.012f, 14f, 24f);
            var topRect = new Rect(margin, margin, Screen.width - margin * 2f, 76f);
            DrawSolid(topRect, new Color(0.035f, 0.075f, 0.095f, 0.94f));
            GUI.Label(new Rect(topRect.x + 20f, topRect.y + 12f, 310f, 50f), _state.Company.CompanyName, _headingStyle);
            GUI.Label(new Rect(topRect.center.x - 190f, topRect.y + 18f, 380f, 40f), _state.Time.Now.ToString("yyyy년 MM월 dd일 ddd HH:mm"), _bodyStyle);
            GUI.Label(new Rect(topRect.xMax - 420f, topRect.y + 18f, 395f, 40f), $"현금 {_state.Company.CashWon:N0}원  ·  평판 {_state.Company.Reputation}", _bodyStyle);

            var familyRect = new Rect(margin, topRect.yMax + 14f, Mathf.Clamp(Screen.width * 0.22f, 330f, 410f), Mathf.Min(500f, Screen.height - 190f));
            DrawSolid(familyRect, new Color(0.04f, 0.08f, 0.10f, 0.91f));
            GUILayout.BeginArea(new Rect(familyRect.x + 18f, familyRect.y + 16f, familyRect.width - 36f, familyRect.height - 32f));
            GUILayout.Label("가족 구성원", _headingStyle);
            GUILayout.Space(7f);
            foreach (var member in _state.Family.Members)
            {
                GUILayout.Label($"{member.DisplayName} · {member.AgeAt(_state.Time)}살", _bodyStyle);
                GUILayout.Label($"체력 {member.Energy}  신뢰 {member.Trust}  스트레스 {member.Stress}", _smallStyle);
                GUILayout.Space(5f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1시간", _buttonStyle, GUILayout.Height(38f)))
            {
                var events = _runner.AdvanceMinutes(60);
                _notice = events.Count > 0 ? $"{events.Count}개 이벤트 처리" : "1시간 경과";
            }

            if (GUILayout.Button("하루", _buttonStyle, GUILayout.Height(38f)))
            {
                _runner.AdvanceMinutes(1440);
                _notice = "하루가 지났습니다.";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            var actionWidth = 360f;
            var actionRect = new Rect(Screen.width - actionWidth - margin, Screen.height - 84f - margin, actionWidth, 84f);
            DrawSolid(actionRect, new Color(0.04f, 0.08f, 0.10f, 0.92f));
            GUI.Label(new Rect(actionRect.x + 12f, actionRect.y + 7f, actionRect.width - 24f, 24f), $"SLOT {_activeSlot} · {_notice}", _smallStyle);
            if (GUI.Button(new Rect(actionRect.x + 12f, actionRect.y + 37f, 102f, 35f), "저장", _buttonStyle)) ShowSaveSlotsNow();
            if (GUI.Button(new Rect(actionRect.x + 129f, actionRect.y + 37f, 102f, 35f), "불러오기", _buttonStyle)) ShowLoadSlotsNow();
            if (GUI.Button(new Rect(actionRect.x + 246f, actionRect.y + 37f, 102f, 35f), "메뉴", _buttonStyle)) ShowPauseMenuNow();
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
            return $"SLOT {repository.Slot}\n\n{save.company.companyName}\n{campaignDate:yyyy.MM.dd HH:mm}\n\n현금  {save.company.cashWon:N0}원\n평판  {save.company.reputation}\n계약  {save.contracts?.Count ?? 0}건\n\n저장 {savedAt:yyyy.MM.dd HH:mm}";
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

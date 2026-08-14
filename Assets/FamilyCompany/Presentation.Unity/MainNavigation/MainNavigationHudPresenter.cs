using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Presentation.Unity.ContractGrowth;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.ManagementUi;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    [DisallowMultipleComponent]
    public sealed class MainNavigationHudPresenter : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution =
            new Vector2(MainNavigationLayoutMetrics.ReferenceWidth, MainNavigationLayoutMetrics.ReferenceHeight);
        public const float CanvasMatchWidthOrHeight = (float)MainNavigationLayoutMetrics.MatchWidthOrHeight;
        public const float MinimumBodyFontSize = 15f;

        private const string FrameRoot = "MainNavigationV2/Frames/";
        private const string MarkerRoot = "MainNavigationV2/Markers/";

        private static readonly Color DeepInk = Hex("203B3A");
        private static readonly Color Cream = Hex("FFF4D8");
        private static readonly Color WorldDim = new Color(0.125f, 0.231f, 0.227f, 0.26f);

        private readonly MainNavigationSession _session = new MainNavigationSession();
        private readonly Dictionary<MainNavigationTabId, Button> _tabButtons =
            new Dictionary<MainNavigationTabId, Button>();
        private readonly Dictionary<MainNavigationTabId, TMP_Text> _tabLabels =
            new Dictionary<MainNavigationTabId, TMP_Text>();
        private readonly Dictionary<int, Button> _speedButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, TMP_Text> _speedLabels = new Dictionary<int, TMP_Text>();
        private readonly Dictionary<string, Button> _featureButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> _workforceButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);

        private PrototypeBootstrap _bootstrap;
        private StockMarketNavigationAdapter _stockMarketNavigation;
        private ContractBusinessRuntimeAdapter _contractBusinessNavigation;
        private OfficeLayoutEditModeController _officeBuildController;
        private GameObject _root;
        private RectTransform _safeRoot;
        private RectTransform _topHud;
        private RectTransform _contentPanel;
        private RectTransform _bottomNavigation;
        private RectTransform _featureHost;
        private RectTransform _worldDim;
        private TMP_Text _companyText;
        private TMP_Text _timeText;
        private TMP_Text _panelTitle;
        private TMP_Text _panelDescription;
        private TMP_Text _officeReturnLabel;
        private Image _panelIcon;
        private Button _officeReturnButton;
        private TMP_FontAsset _bodyFont;
        private TMP_FontAsset _headingFont;
        private TMP_FontAsset _fallbackFont;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Rect _lastSafeArea;
        private float _nextLabelRefresh;
        private bool _built;
        private string _featureRouteFailureKo = string.Empty;
        private string _selectedWorkforceMemberId = string.Empty;

        private Sprite _topBackplate;
        private Sprite _companyBadge;
        private Sprite _timeBadge;
        private Sprite _speedNormal;
        private Sprite _speedHover;
        private Sprite _speedSelected;
        private Sprite _speedPressed;
        private Sprite _bottomDock;
        private Sprite _tabNormal;
        private Sprite _tabHover;
        private Sprite _tabSelected;
        private Sprite _tabPressed;
        private Sprite _modalFrame;
        private Sprite _modalHeader;
        private Sprite _cardNormal;
        private Sprite _cardHover;
        private Sprite _cardDisabled;
        private Sprite _cardFeatured;
        private Sprite _cardFeaturedHover;
        private Sprite _closeNormal;
        private Sprite _closeHover;
        private Sprite _closePressed;
        private Sprite _notificationBadge;
        private Sprite _comingSoonRibbon;

        public bool HasOpenPanel => _session.HasActiveTab;
        public bool HasOpenFeature => _session.HasActiveFeature;
        public string ActiveFeatureId => _session.ActiveFeatureId;
        public string ActiveTabId => _session.HasActiveTab
            ? MainNavigationCatalog.Get(_session.ActiveTab).Id
            : string.Empty;

        public void Configure(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));
            _stockMarketNavigation = bootstrap.GetComponent<StockMarketNavigationAdapter>();
            if (_stockMarketNavigation == null)
                _stockMarketNavigation = bootstrap.gameObject.AddComponent<StockMarketNavigationAdapter>();
            _stockMarketNavigation.Configure(bootstrap, this);
            _contractBusinessNavigation = bootstrap.GetComponent<ContractBusinessRuntimeAdapter>();
            if (_contractBusinessNavigation == null)
                _contractBusinessNavigation = bootstrap.gameObject.AddComponent<ContractBusinessRuntimeAdapter>();
            var historyCatalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
            if (historyCatalog != null)
            {
                _contractBusinessNavigation.Configure(bootstrap, historyCatalog);
                _contractBusinessNavigation.StateChanged -= HandleContractBusinessStateChanged;
                _contractBusinessNavigation.StateChanged += HandleContractBusinessStateChanged;
            }
            _officeBuildController = FindFirstObjectByType<OfficeLayoutEditModeController>();
            if (Application.isPlaying && !_built) BuildRuntimeUi();
        }

        public void ResetSessionView()
        {
            _stockMarketNavigation?.CloseForSessionReset();
            _contractBusinessNavigation?.ReturnToOffice();
            if (_officeBuildController != null && _officeBuildController.IsOpen) _officeBuildController.Close();
            _session.CloseToOffice();
            _featureRouteFailureKo = string.Empty;
            if (_built) RefreshOpenPanel();
        }

        public void OpenTabNow(MainNavigationTabId tabId)
        {
            _stockMarketNavigation?.CloseForSessionReset();
            _contractBusinessNavigation?.ReturnToOffice();
            _session.Open(tabId);
            if (tabId == MainNavigationTabId.Projects && _contractBusinessNavigation?.IsReady == true)
                _contractBusinessNavigation.OpenBusinessHub();
            _featureRouteFailureKo = string.Empty;
            if (_built) RefreshOpenPanel();
        }

        public void ReturnToOfficeNow()
        {
            _stockMarketNavigation?.CloseForSessionReset();
            _contractBusinessNavigation?.ReturnToOffice();
            if (_session.CloseToOffice() && _built) RefreshOpenPanel();
        }

        public void NavigateBackNow()
        {
            if (_session.BackToHub())
            {
                if (_session.ActiveTab == MainNavigationTabId.Projects &&
                    _contractBusinessNavigation?.CurrentRoute != ContractBusinessRoute.BusinessHub)
                    _contractBusinessNavigation?.TryBack();
                _featureRouteFailureKo = string.Empty;
                if (_built) RefreshOpenPanel();
                return;
            }
            ReturnToOfficeNow();
        }

        public bool TryHandleEscape()
        {
            if (_bootstrap == null || _bootstrap.UiScreen != PrototypeUiScreen.Playing) return false;
            if (_stockMarketNavigation != null && _stockMarketNavigation.TryHandleBackToInvestment()) return true;
            if (_session.HasActiveFeature)
            {
                NavigateBackNow();
                return true;
            }
            if (!_session.HandleEscape()) return false;
            _contractBusinessNavigation?.ReturnToOffice();
            if (_built) RefreshOpenPanel();
            return true;
        }

        public Button GetTabButtonForQa(MainNavigationTabId tabId) =>
            _tabButtons.TryGetValue(tabId, out var button) ? button : null;

        public Button GetOfficeReturnButtonForQa() => _officeReturnButton;

        public Button GetSpeedButtonForQa(int speed) =>
            _speedButtons.TryGetValue(speed, out var button) ? button : null;

        public Button GetFeatureButtonForQa(string featureId) =>
            !string.IsNullOrEmpty(featureId) && _featureButtons.TryGetValue(featureId, out var button)
                ? button
                : null;

        public StockMarketNavigationAdapter GetStockMarketNavigationForQa() => _stockMarketNavigation;
        public ContractBusinessRuntimeAdapter GetContractBusinessNavigationForQa() => _contractBusinessNavigation;
        public OfficeLayoutEditModeController GetOfficeBuildControllerForQa() =>
            _officeBuildController != null
                ? _officeBuildController
                : _officeBuildController = FindFirstObjectByType<OfficeLayoutEditModeController>();
        public Button GetWorkforceButtonForQa(string memberId) =>
            !string.IsNullOrEmpty(memberId) && _workforceButtons.TryGetValue(memberId, out var button) ? button : null;
        public string SelectedWorkforceMemberIdForQa => _selectedWorkforceMemberId;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = GetComponent<PrototypeBootstrap>();
        }

        private void Start()
        {
            if (Application.isPlaying && !_built) BuildRuntimeUi();
        }

        private void Update()
        {
            if (!Application.isPlaying || _bootstrap == null) return;
            if (!_built) BuildRuntimeUi();
            RefreshSafeAreaIfNeeded();
            var playing = _bootstrap.UiScreen == PrototypeUiScreen.Playing;
            if (_officeBuildController == null) _officeBuildController = FindFirstObjectByType<OfficeLayoutEditModeController>();
            var externalBuildOpen = _officeBuildController != null && _officeBuildController.IsOpen;
            var visible = playing && !(_stockMarketNavigation?.IsStockMarketOpen ?? false) && !externalBuildOpen;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!playing && _session.HasActiveTab)
            {
                _stockMarketNavigation?.CloseForSessionReset();
                _session.CloseToOffice();
                RefreshOpenPanel();
            }
            if (!visible || Time.unscaledTime < _nextLabelRefresh) return;
            _nextLabelRefresh = Time.unscaledTime + 0.2f;
            RefreshLiveLabels();
        }

        private void OnDestroy()
        {
            if (_contractBusinessNavigation != null)
                _contractBusinessNavigation.StateChanged -= HandleContractBusinessStateChanged;
            if (_bodyFont != null) Destroy(_bodyFont);
            if (_headingFont != null && _headingFont != _bodyFont) Destroy(_headingFont);
            if (_fallbackFont != null && _fallbackFont != _bodyFont && _fallbackFont != _headingFont)
                Destroy(_fallbackFont);
        }

        private void HandleContractBusinessStateChanged()
        {
            if (_built && _session.HasActiveTab && _session.ActiveTab == MainNavigationTabId.Projects)
                RefreshOpenPanel();
        }

        private void BuildRuntimeUi()
        {
            if (_built || _bootstrap == null) return;
            MainNavigationCatalog.ValidateOrThrow();
            EnsureEventSystem();
            LoadFonts();
            LoadRequiredArt();

            _root = new GameObject(
                "Main Navigation HUD V2",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CanvasMatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;

            _safeRoot = CreateRect("Main Navigation Safe Area", _root.transform);
            Stretch(_safeRoot);
            BuildWorldDim();
            BuildTopHud();
            BuildBottomNavigation();
            BuildContentPanel();
            _built = true;
            RefreshSafeAreaIfNeeded(true);
            RefreshOpenPanel();
            RefreshLiveLabels();
            _root.SetActive(false);
            Debug.Log("MAIN_NAVIGATION_HUD_RUNTIME: READY V2 generated-sprite-only");
        }

        private void LoadRequiredArt()
        {
            _topBackplate = LoadRequiredSprite(FrameRoot + "top_hud_backplate_v2");
            _companyBadge = LoadRequiredSprite(FrameRoot + "company_badge_v2");
            _timeBadge = LoadRequiredSprite(FrameRoot + "time_badge_v2");
            _speedNormal = LoadRequiredSprite(FrameRoot + "speed_normal_v2");
            _speedHover = LoadRequiredSprite(FrameRoot + "speed_hover_v2");
            _speedSelected = LoadRequiredSprite(FrameRoot + "speed_selected_v2");
            _speedPressed = LoadRequiredSprite(FrameRoot + "speed_pressed_v2");
            _bottomDock = LoadRequiredSprite(FrameRoot + "bottom_dock_v2");
            _tabNormal = LoadRequiredSprite(FrameRoot + "tab_normal_v2");
            _tabHover = LoadRequiredSprite(FrameRoot + "tab_hover_v2");
            _tabSelected = LoadRequiredSprite(FrameRoot + "tab_selected_v2");
            _tabPressed = LoadRequiredSprite(FrameRoot + "tab_pressed_v2");
            _modalFrame = LoadRequiredSprite(FrameRoot + "modal_frame_v2");
            _modalHeader = LoadRequiredSprite(FrameRoot + "modal_header_v2");
            _cardNormal = LoadRequiredSprite(FrameRoot + "card_normal_v2");
            _cardHover = LoadRequiredSprite(FrameRoot + "card_hover_v2");
            _cardDisabled = LoadRequiredSprite(FrameRoot + "card_disabled_v2");
            _cardFeatured = LoadRequiredSprite(FrameRoot + "card_featured_v2");
            _cardFeaturedHover = LoadRequiredSprite(FrameRoot + "card_featured_hover_v2");
            _closeNormal = LoadRequiredSprite(FrameRoot + "close_normal_v2");
            _closeHover = LoadRequiredSprite(FrameRoot + "close_hover_v2");
            _closePressed = LoadRequiredSprite(FrameRoot + "close_pressed_v2");
            _notificationBadge = LoadRequiredSprite(MarkerRoot + "notification_badge_v2");
            _comingSoonRibbon = LoadRequiredSprite(MarkerRoot + "coming_soon_ribbon_v2");
        }

        private void BuildWorldDim()
        {
            _worldDim = CreateRect("Main Navigation World Dim 26 Percent", _safeRoot);
            Stretch(_worldDim);
            var image = _worldDim.gameObject.AddComponent<Image>();
            image.color = WorldDim;
            image.raycastTarget = true;
            _worldDim.gameObject.SetActive(false);
        }

        private void BuildTopHud()
        {
            _topHud = CreateSpritePanel("Main Navigation Top HUD", _safeRoot, _topBackplate, true);
            var layout = _topHud.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(12, 12, 6, 6), 10f);
            layout.childAlignment = TextAnchor.MiddleCenter;

            var company = CreateSpritePanel("Company Name Badge", _topHud, _companyBadge, true);
            AddLayout(company, 470f, 56f, 420f, 0f);
            var companyIcon = AddIcon(
                company,
                LoadRequiredSprite(MainNavigationCatalog.Get(MainNavigationTabId.Company).IconResourcePath),
                46f);
            companyIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            companyIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            companyIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            companyIcon.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            _companyText = AddText(company, "우리 가족회사", 24f, true, TextAlignmentOptions.MidlineLeft, DeepInk);
            _companyText.rectTransform.anchorMin = Vector2.zero;
            _companyText.rectTransform.anchorMax = Vector2.one;
            _companyText.rectTransform.offsetMin = new Vector2(76f, 0f);
            _companyText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            _companyText.textWrappingMode = TextWrappingModes.NoWrap;

            AddFlexibleSpacer(_topHud);

            var time = CreateSpritePanel("Canonical Date Time Badge", _topHud, _timeBadge, true);
            AddLayout(time, 520f, 56f, 440f, 0f);
            _timeText = AddText(time, string.Empty, 20f, false, TextAlignmentOptions.Midline, DeepInk);
            _timeText.rectTransform.anchorMin = Vector2.zero;
            _timeText.rectTransform.anchorMax = Vector2.one;
            _timeText.rectTransform.offsetMin = new Vector2(72f, 0f);
            _timeText.rectTransform.offsetMax = new Vector2(-18f, 0f);
            _timeText.textWrappingMode = TextWrappingModes.NoWrap;

            AddFlexibleSpacer(_topHud);

            var speedHost = CreateRect("Canonical Time Speed Segments", _topHud);
            AddLayout(speedHost, 320f, 52f, 320f, 0f);
            var speedLayout = speedHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(speedLayout, null, 8f);
            speedLayout.childAlignment = TextAnchor.MiddleRight;
            _speedButtons.Clear();
            _speedLabels.Clear();
            foreach (var speed in new[] { 1, 2, 4 })
            {
                var capturedSpeed = speed;
                var button = CreateSpriteButton(
                    speedHost,
                    $"Main Navigation Speed {speed}x",
                    _speedNormal,
                    _speedHover,
                    _speedPressed,
                    _speedSelected,
                    () => _bootstrap.SetWorldTimeScaleNow(capturedSpeed),
                    96f,
                    50f);
                var label = AddText(button.GetComponent<RectTransform>(), $"{speed}x", 20f, true, TextAlignmentOptions.Midline, DeepInk);
                Stretch(label.rectTransform);
                _speedButtons[speed] = button;
                _speedLabels[speed] = label;
            }
        }

        private void BuildBottomNavigation()
        {
            _bottomNavigation = CreateSpritePanel("Main Navigation Floating Dock", _safeRoot, _bottomDock, true);
            var layout = _bottomNavigation.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(28, 28, 9, 9), 10f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            _tabButtons.Clear();
            _tabLabels.Clear();

            foreach (var definition in MainNavigationCatalog.All)
            {
                var capturedTab = definition.TabId;
                var button = CreateSpriteButton(
                    _bottomNavigation,
                    $"Main Navigation Tab {definition.Id}",
                    _tabNormal,
                    _tabHover,
                    _tabPressed,
                    _tabSelected,
                    () => OpenTabNow(capturedTab),
                    200f,
                    82f);
                var buttonLayout = button.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureLayout(buttonLayout, new RectOffset(12, 12, 7, 6), 2f);
                buttonLayout.childAlignment = TextAnchor.MiddleCenter;
                var icon = AddIcon(button.GetComponent<RectTransform>(), LoadRequiredSprite(definition.IconResourcePath), 42f);
                AddLayout(icon.rectTransform, 42f, 42f, 42f, 0f);
                var label = AddText(
                    button.GetComponent<RectTransform>(),
                    definition.DisplayNameKo,
                    18f,
                    true,
                    TextAlignmentOptions.Midline,
                    DeepInk);
                AddLayout(label.rectTransform, -1f, 24f, 0f, 1f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                _tabButtons[definition.TabId] = button;
                _tabLabels[definition.TabId] = label;
            }
        }

        private void BuildContentPanel()
        {
            _contentPanel = CreateSpritePanel("Main Navigation Content Modal", _safeRoot, _modalFrame, true);
            var panelLayout = _contentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(panelLayout, new RectOffset(30, 30, 26, 30), 16f);
            panelLayout.childAlignment = TextAnchor.UpperCenter;

            var header = CreateSpritePanel("Main Navigation Modal Header", _contentPanel, _modalHeader, true);
            AddLayout(header, -1f, 104f, 104f, 0f);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(headerLayout, new RectOffset(18, 14, 10, 10), 16f);
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            _panelIcon = AddIcon(header, null, 78f);
            AddLayout(_panelIcon.rectTransform, 78f, 78f, 78f, 0f);

            var titleHost = CreateRect("Panel Titles", header);
            AddLayout(titleHost, -1f, 82f, 520f, 1f);
            var titleLayout = titleHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(titleLayout, null, 3f);
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            _panelTitle = AddText(titleHost, string.Empty, 32f, true, TextAlignmentOptions.MidlineLeft, DeepInk);
            AddLayout(_panelTitle.rectTransform, -1f, 40f, 0f, 1f);
            _panelDescription = AddText(titleHost, string.Empty, 18f, false, TextAlignmentOptions.MidlineLeft, DeepInk);
            AddLayout(_panelDescription.rectTransform, -1f, 34f, 0f, 1f);

            _officeReturnButton = CreateSpriteButton(
                header,
                "Main Navigation Office Return",
                _closeNormal,
                _closeHover,
                _closePressed,
                _closeHover,
                NavigateBackNow,
                150f,
                54f);
            _officeReturnLabel = AddText(
                _officeReturnButton.GetComponent<RectTransform>(),
                "← 사무실",
                17f,
                true,
                TextAlignmentOptions.Midline,
                DeepInk);
            Stretch(_officeReturnLabel.rectTransform);

            _featureHost = CreateRect("Main Navigation Feature Cards", _contentPanel);
            var featureLayout = AddLayout(_featureHost, -1f, -1f, 0f, 1f);
            featureLayout.flexibleHeight = 1f;
            _contentPanel.gameObject.SetActive(false);
        }

        private void RefreshOpenPanel()
        {
            if (!_built) return;
            _featureButtons.Clear();
            _workforceButtons.Clear();
            var open = _session.HasActiveTab;
            if (_worldDim.gameObject.activeSelf != open) _worldDim.gameObject.SetActive(open);
            if (_contentPanel.gameObject.activeSelf != open) _contentPanel.gameObject.SetActive(open);
            if (open)
            {
                var definition = MainNavigationCatalog.Get(_session.ActiveTab);
                _panelIcon.sprite = LoadRequiredSprite(definition.IconResourcePath);
                ClearChildren(_featureHost);
                if (_session.HasActiveFeature)
                {
                    var feature = definition.Features.First(item =>
                        string.Equals(item.Id, _session.ActiveFeatureId, StringComparison.Ordinal));
                    SetText(_panelTitle, feature.DisplayNameKo);
                    SetText(_panelDescription, definition.DisplayNameKo + " 허브의 전용 화면");
                    SetText(_officeReturnLabel, "← " + definition.DisplayNameKo);
                    BuildFeatureDetail(feature, definition);
                }
                else
                {
                    SetText(_panelTitle, definition.DisplayNameKo);
                    SetText(_panelDescription, definition.DescriptionKo);
                    SetText(_officeReturnLabel, "← 사무실");
                    if (definition.TabId == MainNavigationTabId.People)
                        BuildWorkforceRoster();
                    else if (definition.TabId == MainNavigationTabId.Investment)
                        BuildInvestmentCards(definition);
                    else
                        BuildStandardCards(definition);
                }
            }
            RefreshTabStyles();
            if (open && EventSystem.current != null)
            {
                var focus = _session.HasActiveFeature
                    ? _officeReturnButton
                    : _featureButtons.Values.FirstOrDefault(button => button != null && button.interactable);
                if (focus != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
            }
        }

        private void BuildInvestmentCards(MainNavigationTabDefinition definition)
        {
            var root = CreateRect("Investment Responsive Cards", _featureHost);
            Stretch(root);
            var vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vertical, null, 14f);
            vertical.childAlignment = TextAnchor.UpperCenter;
            BuildFeatureCard(definition.Features[0], root, definition, true, 178f);

            var supportGrid = CreateRect("Investment Support Grid", root);
            AddLayout(supportGrid, -1f, 286f, 0f, 1f);
            var grid = supportGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.spacing = new Vector2(14f, 14f);
            grid.cellSize = new Vector2(506f, 136f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            for (var index = 1; index < definition.Features.Count; index++)
                BuildFeatureCard(definition.Features[index], supportGrid, definition, false, 136f);
        }

        private void BuildStandardCards(MainNavigationTabDefinition definition)
        {
            var gridHost = CreateRect($"{definition.DisplayNameKo} Feature Grid", _featureHost);
            Stretch(gridHost);
            var grid = gridHost.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.spacing = new Vector2(16f, 16f);
            grid.cellSize = new Vector2(505f, 232f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            foreach (var feature in definition.Features)
                BuildFeatureCard(feature, gridHost, definition, false, 232f);
        }

        private void BuildWorkforceRoster()
        {
            if (_bootstrap?.State == null)
            {
                var unavailable = AddText(_featureHost, "직원 정보를 불러오는 중입니다.", 18f, true,
                    TextAlignmentOptions.Midline, DeepInk);
                Stretch(unavailable.rectTransform);
                return;
            }

            var roster = WorkforceRosterViewModelRules.Create(_bootstrap.State);
            if (roster.Count == 0) return;
            if (string.IsNullOrEmpty(_selectedWorkforceMemberId) || roster.All(item => item.MemberId != _selectedWorkforceMemberId))
                _selectedWorkforceMemberId = roster[0].MemberId;
            var selected = roster.First(item => item.MemberId == _selectedWorkforceMemberId);

            var root = CreateRect("Workforce Roster", _featureHost);
            Stretch(root);
            var rosterBacking = root.gameObject.AddComponent<Image>();
            rosterBacking.sprite = _cardDisabled;
            rosterBacking.type = Image.Type.Sliced;
            rosterBacking.color = Color.white;
            rosterBacking.raycastTarget = false;
            var horizontal = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(horizontal, null, 14f);
            horizontal.childAlignment = TextAnchor.UpperLeft;

            var list = CreateRect("Employee Card List", root);
            AddLayout(list, 350f, -1f, 350f, 0f);
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(listLayout, null, 8f);
            listLayout.childAlignment = TextAnchor.UpperCenter;
            foreach (var member in roster)
                BuildWorkforceMemberCard(list, member, member.MemberId == selected.MemberId);

            var detail = CreateSpritePanel("Employee Capability Detail", root, _cardNormal, true);
            AddLayout(detail, -1f, -1f, 700f, 1f);
            var detailLayout = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(detailLayout, new RectOffset(20, 20, 12, 10), 5f);
            detailLayout.childAlignment = TextAnchor.UpperLeft;

            var identity = AddText(detail,
                $"{selected.DisplayName}  ·  {selected.RoleKo}   {selected.EmploymentTypeKo}",
                WorkforceFont(24f, 24f), true, TextAlignmentOptions.MidlineLeft, DeepInk);
            identity.gameObject.name = "Workforce Panel Title";
            AddLayout(identity.rectTransform, -1f, WorkforceHeight(32f, 30f), 0f, 0f);
            var potential = AddText(detail,
                $"잠재력  {selected.PotentialGrade}등급   ·   현재 XP와 다음 성장",
                WorkforceFont(17f, 14f), true, TextAlignmentOptions.MidlineLeft, DeepInk);
            potential.gameObject.name = "Workforce Body Potential";
            AddLayout(potential.rectTransform, -1f, WorkforceHeight(24f, 20f), 0f, 0f);

            var compactSkills = WorkforceCanvasScale() < 0.8f;
            var skillGrid = CreateRect("Six Work Skills", detail);
            AddLayout(skillGrid, -1f, compactSkills ? 168f : 256f, 0f, 1f);
            var grid = skillGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.spacing = compactSkills ? new Vector2(8f, 8f) : new Vector2(10f, 8f);
            grid.cellSize = compactSkills ? new Vector2(174f, 80f) : new Vector2(351f, 80f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = compactSkills ? 3 : 2;
            foreach (var skill in selected.Skills) BuildWorkforceSkillCard(skillGrid, skill);

            var state = CreateSpritePanel("Current State Separate", detail, _cardFeatured, true);
            AddLayout(state, -1f, WorkforceHeight(68f, 48f), 0f, 0f);
            var stateLayout = state.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(stateLayout, new RectOffset(14, 14, 7, 6), 1f);
            stateLayout.childAlignment = TextAnchor.MiddleLeft;
            var stateHeading = AddText(state, "현재 상태 · 업무 능력과 별도", WorkforceFont(14f, 14f), true,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            stateHeading.gameObject.name = "Workforce Body State Heading";
            AddLayout(stateHeading.rectTransform, -1f, WorkforceHeight(20f, 18f), 0f, 0f);
            var stateMetrics = CreateRect("Current State Metrics", state);
            AddLayout(stateMetrics, -1f, WorkforceHeight(28f, 20f), 0f, 0f);
            var metricsLayout = stateMetrics.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(metricsLayout, null, 12f);
            metricsLayout.childAlignment = TextAnchor.MiddleLeft;
            var primaryState = AddText(stateMetrics,
                $"체력 {selected.StaminaBasisPoints / 100}% · 스트레스 {selected.Stress}",
                WorkforceFont(15f, 14f), true, TextAlignmentOptions.MidlineLeft, DeepInk);
            primaryState.gameObject.name = "Workforce Body State Primary";
            AddLayout(primaryState.rectTransform, -1f, WorkforceHeight(28f, 20f), 260f, 1f);
            var relationshipState = AddText(stateMetrics,
                $"신뢰 {selected.Trust} · 스트레스 저항 {selected.StressResistancePercent}%",
                WorkforceFont(15f, 14f), true, TextAlignmentOptions.MidlineLeft, DeepInk);
            relationshipState.gameObject.name = "Workforce Body State Relationship";
            AddLayout(relationshipState.rectTransform, -1f, WorkforceHeight(28f, 20f), 360f, 1f);
            var education = AddText(detail, "교육 준비 중 · 현재 XP는 실제 업무 기여 시간으로만 오릅니다.",
                WorkforceFont(14f, 14f), false, TextAlignmentOptions.MidlineLeft, DeepInk);
            education.gameObject.name = "Workforce Body Education";
            var educationHeight = WorkforceHeight(20f, 18f);
            var educationElement = AddLayout(education.rectTransform, -1f, educationHeight, 0f, 0f);
            educationElement.minHeight = educationHeight;
        }

        private void BuildWorkforceMemberCard(RectTransform parent, WorkforceRosterMemberViewModel member, bool selected)
        {
            var card = CreateSpritePanel("Employee " + member.MemberId, parent,
                selected ? _cardFeatured : _cardNormal, true);
            var cardHeight = WorkforceHeight(104f, 76f);
            var cardElement = AddLayout(card, -1f, cardHeight, 0f, 0f);
            cardElement.minHeight = cardHeight;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var capturedId = member.MemberId;
            button.onClick.AddListener(() =>
            {
                _selectedWorkforceMemberId = capturedId;
                RefreshOpenPanel();
            });
            ConfigureSpriteSwap(button, selected ? _cardFeatured : _cardNormal, _cardHover,
                selected ? _cardFeatured : _cardNormal, _cardHover);
            _workforceButtons[member.MemberId] = button;

            var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(10, 10, 9, 9), 10f);
            layout.childAlignment = TextAnchor.MiddleLeft;
            var portraitSize = WorkforcePortraitSize();
            var portrait = AddIcon(card, ResolveWorkforcePortrait(member.MemberId), portraitSize);
            AddLayout(portrait.rectTransform, portraitSize, portraitSize, portraitSize, 0f);
            var copy = CreateRect("Employee Summary", card);
            var copyHeight = WorkforceHeight(82f, 64f);
            var copyElement = AddLayout(copy, -1f, copyHeight, 150f, 1f);
            copyElement.minHeight = copyHeight;
            var copyLayout = copy.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(copyLayout, null, 1f);
            copyLayout.childAlignment = TextAnchor.MiddleLeft;
            var name = AddText(copy,
                member.DisplayName + " <size=78%>· " + member.EmploymentTypeKo + "</size>",
                WorkforceFont(18f, 18f), true,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            name.gameObject.name = "Workforce Employee Name";
            var nameHeight = WorkforceHeight(26f, 26f);
            var nameElement = AddLayout(name.rectTransform, -1f, nameHeight, 0f, 0f);
            nameElement.minHeight = nameHeight;
            var role = AddText(copy, member.RoleKo, WorkforceFont(15f, 14f), false,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            role.gameObject.name = "Workforce Body Role";
            var roleHeight = WorkforceHeight(22f, 17f);
            var roleElement = AddLayout(role.rectTransform, -1f, roleHeight, 0f, 0f);
            roleElement.minHeight = roleHeight;
            var stamina = AddText(copy, $"체력 {member.StaminaBasisPoints / 100}%",
                WorkforceFont(15f, 14f), true,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            stamina.gameObject.name = "Workforce Body Stamina";
            var staminaHeight = WorkforceHeight(22f, 17f);
            var staminaElement = AddLayout(stamina.rectTransform, -1f, staminaHeight, 0f, 0f);
            staminaElement.minHeight = staminaHeight;
        }

        private void BuildWorkforceSkillCard(RectTransform parent, WorkforceSkillViewModel skill)
        {
            var card = CreateSpritePanel("Skill " + skill.SkillId, parent, _cardDisabled, true);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(12, 12, 6, 5), 1f);
            var heading = AddText(card, $"{skill.LabelKo}  {skill.Value}", WorkforceFont(16f, 14f), true,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            heading.gameObject.name = "Workforce Body Skill";
            AddLayout(heading.rectTransform, -1f, WorkforceHeight(21f, 18f), 0f, 0f);
            var bar = CreateRect("Skill Value Bar", card);
            AddLayout(bar, -1f, 12f, 0f, 0f);
            var back = bar.gameObject.AddComponent<Image>();
            back.sprite = _cardNormal;
            back.type = Image.Type.Sliced;
            back.raycastTarget = false;
            var fill = CreateRect("Skill Value Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(skill.Value / 100f, 1f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = _notificationBadge;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;
            var next = skill.NextExperience <= 0
                ? "최대 숙련"
                : $"XP {skill.Experience:N0} / {skill.NextExperience:N0}";
            var xp = AddText(card, next, WorkforceFont(15f, 14f), false,
                TextAlignmentOptions.MidlineLeft, DeepInk);
            xp.gameObject.name = "Workforce Body Experience";
            AddLayout(xp.rectTransform, -1f, WorkforceHeight(19f, 18f), 0f, 0f);
        }

        private Sprite ResolveWorkforcePortrait(string memberId)
        {
            var runtime = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            var actor = runtime?.Actors.FirstOrDefault(item =>
                string.Equals(item.AgentId, memberId, StringComparison.Ordinal));
            return actor?.PresentationRenderer != null && actor.PresentationRenderer.sprite != null
                ? actor.PresentationRenderer.sprite
                : LoadRequiredSprite(MainNavigationCatalog.Get(MainNavigationTabId.People).IconResourcePath);
        }

        private void BuildFeatureDetail(
            MainNavigationFeatureDefinition feature,
            MainNavigationTabDefinition tab)
        {
            switch (feature.Action)
            {
                case MainNavigationFeatureAction.OpenContractBoard:
                    BuildContractBoardDetail(feature, tab);
                    return;
                case MainNavigationFeatureAction.OpenProductOpportunities:
                    BuildProductOpportunityDetail(feature, tab);
                    return;
                default:
                    BuildComingSoonDetail(feature, tab);
                    return;
            }
        }

        private void BuildContractBoardDetail(
            MainNavigationFeatureDefinition feature,
            MainNavigationTabDefinition tab)
        {
            if (_contractBusinessNavigation?.IsReady != true)
            {
                _featureRouteFailureKo = "계약 고객 카탈로그를 연결하는 중입니다.";
                BuildComingSoonDetail(feature, tab);
                return;
            }

            var board = _contractBusinessNavigation.GetBoardViewModel();
            var root = CreateRect("Contract Board Adapter View", _featureHost);
            Stretch(root);
            var vertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vertical, null, 12f);
            vertical.childAlignment = TextAnchor.UpperCenter;
            var guidance = AddText(
                root,
                board.HeadingKo + "\n" + board.GuidanceKo,
                16f,
                false,
                TextAlignmentOptions.TopLeft,
                DeepInk);
            AddLayout(guidance.rectTransform, -1f, 72f, 0f, 0f);

            var cards = board.Cards.Take(3).ToArray();
            var gridHost = CreateRect("Canonical Contract Offers", root);
            AddLayout(gridHost, -1f, 354f, 0f, 1f);
            var grid = gridHost.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.spacing = new Vector2(12f, 0f);
            grid.cellSize = new Vector2(cards.Length <= 1 ? 1030f : cards.Length == 2 ? 509f : 335f, 350f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Math.Max(1, cards.Length);
            foreach (var offer in cards)
            {
                var card = CreateSpritePanel("Contract Offer " + offer.OfferId, gridHost, _cardNormal, true);
                var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureLayout(cardLayout, new RectOffset(18, 18, 18, 16), 5f);
                cardLayout.childAlignment = TextAnchor.UpperLeft;
                var tier = AddText(card, offer.TierKo, 15f, true, TextAlignmentOptions.MidlineLeft, DeepInk);
                AddLayout(tier.rectTransform, -1f, 24f, 0f, 0f);
                var client = AddText(card, offer.ClientNameKo, 21f, true, TextAlignmentOptions.MidlineLeft, DeepInk);
                AddLayout(client.rectTransform, -1f, 30f, 0f, 0f);
                var title = AddText(card, offer.TitleKo, 17f, true, TextAlignmentOptions.TopLeft, DeepInk);
                AddLayout(title.rectTransform, -1f, 46f, 0f, 0f);
                var facts = AddText(
                    card,
                    offer.RewardKo + "\n" + offer.DeadlineKo + "\n" + offer.WorkKo + "\n" + offer.CapabilityKo,
                    15f,
                    false,
                    TextAlignmentOptions.TopLeft,
                    DeepInk);
                AddLayout(facts.rectTransform, -1f, 132f, 0f, 1f);
                var ready = offer.MemberChoices.Count(item => item.Available);
                var status = AddText(
                    card,
                    "지금 배정 가능 " + ready + "명",
                    15f,
                    true,
                    TextAlignmentOptions.BottomLeft,
                    DeepInk);
                AddLayout(status.rectTransform, -1f, 36f, 0f, 0f);
            }
        }

        private void BuildProductOpportunityDetail(
            MainNavigationFeatureDefinition feature,
            MainNavigationTabDefinition tab)
        {
            if (_contractBusinessNavigation?.IsReady != true)
            {
                _featureRouteFailureKo = "사업 성장 상태를 연결하는 중입니다.";
                BuildComingSoonDetail(feature, tab);
                return;
            }

            var opportunities = _contractBusinessNavigation.GetProductOpportunities().Take(4).ToArray();
            var root = CreateRect("Product Opportunity Adapter View", _featureHost);
            Stretch(root);
            var grid = root.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.spacing = new Vector2(16f, 16f);
            grid.cellSize = new Vector2(505f, 226f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            foreach (var opportunity in opportunities)
            {
                var card = CreateSpritePanel(
                    "Product Opportunity " + opportunity.Definition.ProductPathId,
                    root,
                    opportunity.Unlocked ? _cardNormal : _cardDisabled,
                    true);
                var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureLayout(layout, new RectOffset(30, 30, 24, 18), 5f);
                layout.childAlignment = TextAnchor.UpperLeft;
                var title = AddText(card, opportunity.Definition.DisplayNameKo, 21f, true, TextAlignmentOptions.MidlineLeft, DeepInk);
                AddLayout(title.rectTransform, -1f, 30f, 0f, 0f);
                var progress = AddText(
                    card,
                    $"진행 {opportunity.ProgressBasisPoints / 100}% · {(opportunity.Unlocked ? "해금 조건 충족" : "조건 축적 중")}",
                    16f,
                    true,
                    TextAlignmentOptions.MidlineLeft,
                    DeepInk);
                AddLayout(progress.rectTransform, -1f, 26f, 0f, 0f);
                var conditions = AddText(
                    card,
                    string.Join("\n", opportunity.ConditionLabels.Take(3)) + "\n수익 구조 · " + opportunity.Definition.RevenueModelKo,
                    15f,
                    false,
                    TextAlignmentOptions.TopLeft,
                    DeepInk);
                AddLayout(conditions.rectTransform, -1f, 112f, 0f, 1f);
            }
        }

        private void BuildComingSoonDetail(
            MainNavigationFeatureDefinition feature,
            MainNavigationTabDefinition tab)
        {
            var card = CreateSpritePanel("Dedicated Status " + feature.Id, _featureHost, _cardDisabled, true);
            card.anchorMin = new Vector2(0.07f, 0.07f);
            card.anchorMax = new Vector2(0.93f, 0.93f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(54, 54, 30, 28), 10f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            var iconPath = string.IsNullOrEmpty(feature.IconResourcePath) ? tab.IconResourcePath : feature.IconResourcePath;
            var icon = AddIcon(card, LoadRequiredSprite(iconPath), 88f);
            AddLayout(icon.rectTransform, 88f, 88f, 88f, 0f);
            var title = AddText(card, feature.DisplayNameKo + " 전용 화면", 26f, true, TextAlignmentOptions.Midline, DeepInk);
            AddLayout(title.rectTransform, -1f, 38f, 0f, 0f);
            var message = string.IsNullOrEmpty(_featureRouteFailureKo)
                ? feature.DescriptionKo + "\n현재 버전에서는 준비 중이며, 구현 상태를 숨기지 않습니다."
                : _featureRouteFailureKo + "\n사무실로 돌아가 다시 시도할 수 있습니다.";
            var body = AddText(card, message, 17f, false, TextAlignmentOptions.Midline, DeepInk);
            AddLayout(body.rectTransform, -1f, 80f, 0f, 0f);
            var marker = CreateSpritePanel("Explicit Coming Soon State", card, _comingSoonRibbon, true);
            AddLayout(marker, 140f, 34f, 140f, 0f);
            var markerText = AddText(marker, "준비 중", 16f, true, TextAlignmentOptions.Midline, DeepInk);
            Stretch(markerText.rectTransform);
        }

        private void BuildFeatureCard(
            MainNavigationFeatureDefinition feature,
            RectTransform parent,
            MainNavigationTabDefinition tab,
            bool featured,
            float preferredHeight)
        {
            var actionable = feature.Action != MainNavigationFeatureAction.None;
            var available = feature.Action == MainNavigationFeatureAction.OpenStockMarket ||
                            feature.Action == MainNavigationFeatureAction.OpenBuildingEditor ||
                            feature.Action == MainNavigationFeatureAction.OpenContractBoard ||
                            feature.Action == MainNavigationFeatureAction.OpenProductOpportunities;
            var cardSprite = featured ? _cardFeatured : _cardNormal;
            var card = CreateSpritePanel($"Feature {feature.Id}", parent, cardSprite, true);
            AddLayout(card, -1f, preferredHeight, 0f, 1f);
            ConfigureFeatureRoute(feature, card, featured);

            var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            var horizontalPadding = featured ? 24 : 18;
            var verticalPadding = featured ? 18 : 12;
            ConfigureLayout(
                layout,
                new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding),
                featured ? 22f : 14f);
            layout.childAlignment = TextAnchor.MiddleLeft;

            var iconPath = string.IsNullOrEmpty(feature.IconResourcePath) ? tab.IconResourcePath : feature.IconResourcePath;
            var iconSize = featured ? 92f : preferredHeight >= 200f ? 68f : 60f;
            var icon = AddIcon(card, LoadRequiredSprite(iconPath), iconSize);
            AddLayout(icon.rectTransform, iconSize, iconSize, iconSize, 0f);

            var textHost = CreateRect("Feature Copy", card);
            AddLayout(textHost, -1f, preferredHeight - verticalPadding * 2f, 260f, 1f);
            var textLayout = textHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(textLayout, null, featured ? 4f : 3f);
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            var title = AddText(
                textHost,
                feature.DisplayNameKo,
                featured ? 26f : 21f,
                true,
                TextAlignmentOptions.MidlineLeft,
                DeepInk);
            AddLayout(title.rectTransform, -1f, featured ? 34f : 28f, 0f, 1f);

            ResolveFeaturePresentation(feature, out var descriptionKo, out var statusKo);
            var descriptionHeight = featured ? 60f : preferredHeight >= 200f ? 92f : 48f;
            var description = AddText(
                textHost,
                descriptionKo,
                featured ? 17f : 16f,
                false,
                TextAlignmentOptions.TopLeft,
                DeepInk);
            AddLayout(description.rectTransform, -1f, descriptionHeight, 0f, 1f);
            description.textWrappingMode = TextWrappingModes.Normal;

            var marker = CreateSpritePanel(
                available ? "Available Marker" : "Coming Soon Marker",
                textHost,
                available ? _notificationBadge : _comingSoonRibbon,
                true);
            AddLayout(marker, available ? 126f : 116f, 28f, available ? 126f : 116f, 0f);
            var markerText = AddText(
                marker,
                available ? "이용 가능" : "준비 중",
                15f,
                true,
                TextAlignmentOptions.Midline,
                available ? Cream : DeepInk);
            Stretch(markerText.rectTransform);
        }

        private void ConfigureFeatureRoute(
            MainNavigationFeatureDefinition feature,
            RectTransform card,
            bool featured)
        {
            if (feature.Action == MainNavigationFeatureAction.None) return;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() => OpenFeatureRoute(feature));
            ConfigureSpriteSwap(
                button,
                featured ? _cardFeatured : _cardNormal,
                featured ? _cardFeaturedHover : _cardHover,
                featured ? _cardFeatured : _cardNormal,
                featured ? _cardFeatured : _cardHover);
            _featureButtons[feature.Id] = button;
        }

        private void OpenFeatureRoute(MainNavigationFeatureDefinition feature)
        {
            _featureRouteFailureKo = string.Empty;
            switch (feature.Action)
            {
                case MainNavigationFeatureAction.OpenStockMarket:
                    _stockMarketNavigation?.OpenFromInvestment();
                    return;
                case MainNavigationFeatureAction.OpenBuildingEditor:
                    if (!string.Equals(feature.RouteId, OfficeBuildEditorNavigationAdapter.EntryId, StringComparison.Ordinal))
                    {
                        _featureRouteFailureKo = "건축·편집 경로 ID가 연결되지 않았습니다.";
                        break;
                    }
                    if (OfficeBuildEditorNavigationAdapter.TryOpen(feature.RouteId, out var buildFailure))
                    {
                        _officeBuildController = FindFirstObjectByType<OfficeLayoutEditModeController>();
                        return;
                    }
                    _featureRouteFailureKo = buildFailure;
                    break;
                case MainNavigationFeatureAction.OpenContractBoard:
                    if (_contractBusinessNavigation?.IsReady == true)
                    {
                        _contractBusinessNavigation.OpenContractBoard();
                        _session.OpenFeature(feature.Id);
                        if (_built) RefreshOpenPanel();
                        return;
                    }
                    _featureRouteFailureKo = "계약 고객 카탈로그가 아직 준비되지 않았습니다.";
                    break;
                case MainNavigationFeatureAction.OpenProductOpportunities:
                    if (_contractBusinessNavigation?.IsReady == true)
                    {
                        _contractBusinessNavigation.OpenProductOpportunities();
                        _session.OpenFeature(feature.Id);
                        if (_built) RefreshOpenPanel();
                        return;
                    }
                    _featureRouteFailureKo = "자체 제품 성장 상태가 아직 준비되지 않았습니다.";
                    break;
                case MainNavigationFeatureAction.OpenStatus:
                    break;
                default:
                    _featureRouteFailureKo = "알 수 없는 기능 경로입니다.";
                    break;
            }
            _session.OpenFeature(feature.Id);
            if (_built) RefreshOpenPanel();
        }

        private void ResolveFeaturePresentation(
            MainNavigationFeatureDefinition feature,
            out string descriptionKo,
            out string statusKo)
        {
            descriptionKo = feature.DescriptionKo;
            statusKo = feature.StatusKo;
            if (feature.Action == MainNavigationFeatureAction.OpenStockMarket)
            {
                descriptionKo = _stockMarketNavigation?.ReadOnlyPortfolioSummaryKo() ?? feature.DescriptionKo;
                return;
            }
            if (feature.Action == MainNavigationFeatureAction.OpenBuildingEditor)
            {
                statusKo = "이용 가능";
                return;
            }
            if (feature.Action == MainNavigationFeatureAction.OpenContractBoard &&
                _contractBusinessNavigation?.IsReady == true)
            {
                var board = _contractBusinessNavigation.GetBoardViewModel();
                var first = board.Cards.FirstOrDefault();
                if (first != null)
                    descriptionKo = $"현재 제안 · {first.ClientNameKo} / {first.TitleKo}";
                statusKo = "이용 가능";
                return;
            }
            if (feature.Action == MainNavigationFeatureAction.OpenProductOpportunities &&
                _contractBusinessNavigation?.IsReady == true)
            {
                var product = _contractBusinessNavigation.GetProductOpportunities()
                    .OrderByDescending(item => item.ProgressBasisPoints)
                    .FirstOrDefault();
                if (product != null)
                    descriptionKo = $"{product.Definition.DisplayNameKo} · 진행 {product.ProgressBasisPoints / 100}%";
                statusKo = "진행 보기";
            }
        }

        private void RefreshLiveLabels()
        {
            if (!_built || _bootstrap.State == null) return;
            SetText(_companyText, _bootstrap.State.Company.CompanyName);
            SetText(_timeText, _bootstrap.State.Time.Now.ToString("yyyy년 MM월 dd일 ddd HH:mm"));
            RefreshSpeedStyles();
        }

        private void RefreshSpeedStyles()
        {
            var selected = Mathf.RoundToInt(_bootstrap.WorldTimeScale);
            foreach (var pair in _speedButtons)
            {
                var active = pair.Key == selected;
                ConfigureSpriteSwap(
                    pair.Value,
                    active ? _speedSelected : _speedNormal,
                    active ? _speedSelected : _speedHover,
                    _speedPressed,
                    _speedSelected);
                if (_speedLabels.TryGetValue(pair.Key, out var label))
                {
                    SetText(label, $"{pair.Key}x");
                    label.color = active ? Cream : DeepInk;
                }
            }
        }

        private void RefreshTabStyles()
        {
            foreach (var definition in MainNavigationCatalog.All)
            {
                var active = _session.HasActiveTab && _session.ActiveTab == definition.TabId;
                if (_tabButtons.TryGetValue(definition.TabId, out var button))
                    ConfigureSpriteSwap(
                        button,
                        active ? _tabSelected : _tabNormal,
                        active ? _tabSelected : _tabHover,
                        _tabPressed,
                        _tabSelected);
                if (_tabLabels.TryGetValue(definition.TabId, out var label)) label.color = DeepInk;
            }
        }

        private static Sprite LoadRequiredSprite(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
                throw new InvalidOperationException($"MAIN_NAVIGATION_V2_ASSET_MISSING: {resourcePath}");
            return sprite;
        }

        private static Button CreateSpriteButton(
            RectTransform parent,
            string objectName,
            Sprite normal,
            Sprite hover,
            Sprite pressed,
            Sprite selected,
            Action onClick,
            float preferredWidth,
            float preferredHeight)
        {
            var rect = CreateSpritePanel(objectName, parent, normal, true);
            AddLayout(rect, preferredWidth, preferredHeight, preferredWidth, 0f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            ConfigureSpriteSwap(button, normal, hover, pressed, selected);
            return button;
        }

        private static void ConfigureSpriteSwap(
            Button button,
            Sprite normal,
            Sprite hover,
            Sprite pressed,
            Sprite selected)
        {
            if (button == null || button.image == null) return;
            button.transition = Selectable.Transition.SpriteSwap;
            button.image.sprite = normal;
            button.image.overrideSprite = null;
            button.image.color = Color.white;
            button.image.raycastTarget = true;
            var state = button.spriteState;
            state.highlightedSprite = hover;
            state.pressedSprite = pressed;
            state.selectedSprite = selected;
            state.disabledSprite = normal;
            button.spriteState = state;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        private static RectTransform CreateSpritePanel(
            string objectName,
            Transform parent,
            Sprite sprite,
            bool sliced)
        {
            if (sprite == null) throw new InvalidOperationException($"Generated Sprite is required for {objectName}.");
            var rect = CreateRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            return rect;
        }

        private static Image AddIcon(RectTransform parent, Sprite sprite, float size)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            return icon;
        }

        private static float WorkforceFont(float preferredCanvasSize, float minimumPixelSize)
        {
            return Mathf.Max(preferredCanvasSize, minimumPixelSize / WorkforceCanvasScale());
        }

        private static float WorkforceHeight(float preferredCanvasSize, float minimumPixelSize)
        {
            return Mathf.Max(preferredCanvasSize, minimumPixelSize / WorkforceCanvasScale());
        }

        private static float WorkforcePortraitSize()
        {
            return WorkforceCanvasScale() < 0.8f ? 56f : 74f;
        }

        private static float WorkforceCanvasScale()
        {
            var widthScale = Mathf.Max(1f, Screen.width) / ReferenceResolution.x;
            var heightScale = Mathf.Max(1f, Screen.height) / ReferenceResolution.y;
            return Mathf.Max(0.01f, Mathf.Sqrt(widthScale * heightScale));
        }

        private TMP_Text AddText(
            RectTransform parent,
            string value,
            float fontSize,
            bool heading,
            TextAlignmentOptions alignment,
            Color color)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = heading ? _headingFont : _bodyFont;
            text.fontSize = Mathf.Max(MinimumBodyFontSize, fontSize);
            text.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
            text.enableAutoSizing = false;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            text.lineSpacing = 1.5f;
            SetText(text, value);
            return text;
        }

        private void RefreshSafeAreaIfNeeded(bool force = false)
        {
            var safe = Screen.safeArea;
            if (!force && Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight && safe == _lastSafeArea)
                return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = safe;
            ApplySafeArea(_safeRoot, safe);
            ApplyAnchoredLayout();
        }

        private void ApplyAnchoredLayout()
        {
            if (_safeRoot == null || _topHud == null || _contentPanel == null || _bottomNavigation == null) return;
            var width = _safeRoot.rect.width > 1f ? _safeRoot.rect.width : MainNavigationLayoutMetrics.ReferenceWidth;
            var height = _safeRoot.rect.height > 1f ? _safeRoot.rect.height : MainNavigationLayoutMetrics.ReferenceHeight;
            const float margin = (float)MainNavigationLayoutMetrics.OuterMargin;
            const float topHeight = (float)MainNavigationLayoutMetrics.TopHudHeight;
            const float bottomHeight = (float)MainNavigationLayoutMetrics.BottomNavigationHeight;
            const float gap = (float)MainNavigationLayoutMetrics.RegionGap;

            _topHud.anchorMin = new Vector2(0f, 1f);
            _topHud.anchorMax = new Vector2(1f, 1f);
            _topHud.pivot = new Vector2(0.5f, 1f);
            _topHud.offsetMin = new Vector2(margin, -margin - topHeight);
            _topHud.offsetMax = new Vector2(-margin, -margin);

            var bottomWidth = Mathf.Min((float)MainNavigationLayoutMetrics.BottomNavigationWidth, width - margin * 2f);
            _bottomNavigation.anchorMin = new Vector2(0.5f, 0f);
            _bottomNavigation.anchorMax = new Vector2(0.5f, 0f);
            _bottomNavigation.pivot = new Vector2(0.5f, 0f);
            _bottomNavigation.sizeDelta = new Vector2(bottomWidth, bottomHeight);
            _bottomNavigation.anchoredPosition = new Vector2(0f, margin);

            var availablePanelHeight = height - margin * 2f - topHeight - bottomHeight - gap * 2f;
            var panelWidth = Mathf.Min((float)MainNavigationLayoutMetrics.ContentPanelWidth, width - margin * 2f);
            var panelHeight = Mathf.Min((float)MainNavigationLayoutMetrics.ContentPanelHeight, availablePanelHeight);
            _contentPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _contentPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _contentPanel.pivot = new Vector2(0.5f, 0.5f);
            _contentPanel.sizeDelta = new Vector2(panelWidth, panelHeight);
            _contentPanel.anchoredPosition = Vector2.zero;
        }

        private static void ApplySafeArea(RectTransform target, Rect safe)
        {
            if (target == null || Screen.width <= 0 || Screen.height <= 0) return;
            target.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            target.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private void LoadFonts()
        {
            var catalog = Resources.Load<ManagementUiFontCatalog>(ManagementUiLayoutMetrics.FontCatalogResourcePath);
            if (catalog != null && catalog.IsComplete)
            {
                _bodyFont = CreateFontAsset(catalog.BodySource, "Main Navigation Pretendard V2");
                _headingFont = CreateFontAsset(catalog.HeadingSource, "Main Navigation Maplestory Bold V2");
                _fallbackFont = CreateFontAsset(catalog.FallbackSource, "Main Navigation Maplestory Light Fallback V2");
            }
            if (_bodyFont == null || _headingFont == null || _fallbackFont == null)
            {
                var builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bodyFont = _bodyFont != null ? _bodyFont : CreateFontAsset(builtIn, "Main Navigation Builtin Fallback");
                _headingFont = _headingFont != null ? _headingFont : _bodyFont;
                _fallbackFont = _fallbackFont != null ? _fallbackFont : _bodyFont;
                Debug.LogError("MAIN_NAVIGATION_FONT_FALLBACK: bundled Korean font catalog is missing or incomplete.");
            }
            if (_bodyFont != null && _fallbackFont != null && _fallbackFont != _bodyFont)
                _bodyFont.fallbackFontAssetTable.Add(_fallbackFont);
            if (_headingFont != null && _bodyFont != null && _bodyFont != _headingFont)
                _headingFont.fallbackFontAssetTable.Add(_bodyFont);
        }

        private static TMP_FontAsset CreateFontAsset(Font source, string assetName)
        {
            if (source == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(source);
            if (asset == null) return null;
            asset.name = assetName;
            asset.hideFlags = HideFlags.DontSave;
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.isMultiAtlasTexturesEnabled = true;
            if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            return asset;
        }

        private void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var eventObject = new GameObject(
                "Main Navigation Event System",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventObject);
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void AddFlexibleSpacer(RectTransform parent)
        {
            var spacer = CreateRect("Flexible Spacer", parent);
            AddLayout(spacer, -1f, 1f, 0f, 1f);
        }

        private static LayoutElement AddLayout(
            RectTransform rect,
            float preferredWidth,
            float preferredHeight,
            float minimumWidth,
            float flexibleWidth)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (minimumWidth > 0f) element.minWidth = minimumWidth;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup layout, RectOffset padding, float spacing)
        {
            layout.padding = padding ?? new RectOffset();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out var color))
                throw new ArgumentException($"Invalid RGB color: {value}", nameof(value));
            return color;
        }
    }
}

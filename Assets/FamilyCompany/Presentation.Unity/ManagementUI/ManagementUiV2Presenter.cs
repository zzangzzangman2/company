using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.ManagementUi;
using FamilyCompany.Presentation.Unity.UIRemaster;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.ManagementUI
{
    [DisallowMultipleComponent]
    public sealed class ManagementUiV2Presenter : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution =
            new Vector2(ManagementUiLayoutMetrics.ReferenceWidth, ManagementUiLayoutMetrics.ReferenceHeight);
        public const float CanvasMatchWidthOrHeight = (float)ManagementUiLayoutMetrics.MatchWidthOrHeight;

        private static readonly Color PageColor = Hex(ManagementUiAccessibility.PageHex);
        private static readonly Color PanelColor = Hex(ManagementUiAccessibility.PanelHex);
        private static readonly Color CardColor = Hex(ManagementUiAccessibility.CardHex);
        private static readonly Color TextColor = Hex(ManagementUiAccessibility.TextHex);
        private static readonly Color SecondaryTextColor = Hex(ManagementUiAccessibility.SecondaryTextHex);
        private static readonly Color AccentColor = Hex(ManagementUiAccessibility.AccentHex);
        private static readonly Color AccentHoverColor = Hex("0A7C70");
        private static readonly Color PeachColor = Hex("D75D45");
        private static readonly Color BorderColor = Hex("9CAFAA");
        private static readonly Color DisabledColor = Hex(ManagementUiAccessibility.DisabledHex);
        private static readonly Color DisabledTextColor = Hex(ManagementUiAccessibility.DisabledTextHex);
        private static readonly Color OfficeHudShellColor = Hex("142729");
        private static readonly Color OfficeHudCardColor = Hex("203B3B");
        private static readonly Color OfficeHudTextColor = Hex("FFF6E2");
        private static readonly Color OfficeHudMutedColor = Hex("B7CBC4");
        private static readonly Color OfficeHudAccentColor = Hex("EF7558");
        private static readonly string[] ContractClientIds = { "samsung-electronics", "lg-electronics", "sk-telecom" };
        private static readonly string[] ContractClientNames = { "삼성전자", "LG전자", "SK텔레콤" };

        private readonly Dictionary<string, TMP_Text> _officeMemberStatus =
            new Dictionary<string, TMP_Text>(StringComparer.Ordinal);
        private readonly Dictionary<string, IOfficeObservationStatusSource> _statusSources =
            new Dictionary<string, IOfficeObservationStatusSource>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeWorkerAgent> _agents =
            new Dictionary<string, OfficeWorkerAgent>(StringComparer.Ordinal);
        private readonly Dictionary<int, Button> _officeSpeedButtons =
            new Dictionary<int, Button>();

        private PrototypeBootstrap _bootstrap;
        private GameObject _officeHudRoot;
        private GameObject _managementRoot;
        private RectTransform _officeSafeRoot;
        private RectTransform _managementSafeRoot;
        private RectTransform _familyHost;
        private RectTransform _contentHost;
        private RectTransform _progressHost;
        private TMP_Text _officeCompanyText;
        private TMP_Text _officeTimeText;
        private TMP_Text _officeNoticeText;
        private TMP_Text _officeSpeedText;
        private TMP_Text _managementCompanyText;
        private TMP_Text _managementTimeText;
        private TMP_Text _managementCashText;
        private TMP_Text _managementNoticeText;
        private TMP_FontAsset _bodyFont;
        private TMP_FontAsset _headingFont;
        private TMP_FontAsset _fallbackFont;
        private ManagementUiSkinCatalog _skin;
        private Texture2D _fallbackTexture;
        private Sprite _fallbackSprite;
        private PrototypeUiScreen _lastScreen = (PrototypeUiScreen)(-1);
        private OfficeManagementTab _tab = OfficeManagementTab.Contracts;
        private BusinessIndustry _contractIndustry = BusinessIndustry.WebAndSoftware;
        private BusinessIndustry _selectedBusinessIndustry = BusinessIndustry.WebAndSoftware;
        private int _contractBoardPage;
        private string _productTitle = "우리 가족 업무도우미";
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Rect _lastSafeArea;
        private float _nextLiveRefresh;
        private float _nextSourceRefresh;
        private bool _built;
        private bool _reportedFontFailure;
        private bool _reportedSkinFallback;

        public void Configure(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));
            if (Application.isPlaying && !_built) BuildRuntimeUi();
        }

        public void ResetSessionView()
        {
            _tab = OfficeManagementTab.Contracts;
            _contractIndustry = BusinessIndustry.WebAndSoftware;
            _selectedBusinessIndustry = BusinessIndustry.WebAndSoftware;
            _contractBoardPage = 0;
            _productTitle = "우리 가족 업무도우미";
            _nextLiveRefresh = 0f;
            _nextSourceRefresh = 0f;
        }

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

            var screen = _bootstrap.UiScreen;
            // The compact top/bottom main navigation HUD owns the normal office screen.
            // Keep this legacy observation HUD built for regression compatibility, but hidden.
            var officeVisible = false;
            var managementVisible = screen == PrototypeUiScreen.Management;
            if (_officeHudRoot.activeSelf != officeVisible) _officeHudRoot.SetActive(officeVisible);
            if (_managementRoot.activeSelf != managementVisible) _managementRoot.SetActive(managementVisible);
            if (_lastScreen != screen)
            {
                _lastScreen = screen;
                if (managementVisible) RebuildManagementData();
                _nextLiveRefresh = 0f;
            }

            if (!officeVisible && !managementVisible) return;
            if (Time.unscaledTime < _nextLiveRefresh) return;
            _nextLiveRefresh = Time.unscaledTime + 0.2f;
            RefreshLiveLabels();
        }

        private void OnDestroy()
        {
            if (_bodyFont != null) Destroy(_bodyFont);
            if (_headingFont != null && _headingFont != _bodyFont) Destroy(_headingFont);
            if (_fallbackFont != null && _fallbackFont != _bodyFont && _fallbackFont != _headingFont) Destroy(_fallbackFont);
            if (_fallbackSprite != null) Destroy(_fallbackSprite);
            if (_fallbackTexture != null) Destroy(_fallbackTexture);
        }

        private void BuildRuntimeUi()
        {
            if (_built || _bootstrap == null) return;
            EnsureEventSystem();
            LoadFonts();
            LoadSkin();
            _officeHudRoot = CreateCanvas("Office Observation HUD", 210, out _officeSafeRoot);
            _managementRoot = CreateCanvas("Management Overlay V2", 220, out _managementSafeRoot);
            BuildOfficeHud();
            BuildManagementOverlay();
            _officeHudRoot.SetActive(false);
            _managementRoot.SetActive(false);
            _built = true;
            RefreshSafeAreaIfNeeded(true);
            Debug.Log("MANAGEMENT_UI_V2_RUNTIME: READY");
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var eventObject = new GameObject("Management UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventObject);
        }

        private GameObject CreateCanvas(string objectName, int sortingOrder, out RectTransform safeRoot)
        {
            var canvasObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CanvasMatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;
            safeRoot = CreateRect("Safe Area", canvasObject.transform);
            Stretch(safeRoot);
            return canvasObject;
        }

        private void BuildOfficeHud()
        {
            var layout = _officeSafeRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(20, 20, 16, 16), 0f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var top = CreatePanel("Office HUD", _officeSafeRoot, OfficeHudShellColor, PanelSprite);
            AddLayout(top, -1f, 172f, 172f, 0f);
            var topVertical = top.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(topVertical, new RectOffset(18, 18, 14, 14), 8f);
            topVertical.childControlHeight = true;
            topVertical.childForceExpandHeight = false;

            var firstRow = CreateRect("Company Row", top);
            AddLayout(firstRow, -1f, 42f, 42f, 0f);
            var firstLayout = firstRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(firstLayout, null, 14f);
            firstLayout.childAlignment = TextAnchor.MiddleLeft;
            _officeCompanyText = AddText(
                firstRow, string.Empty, 25f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f, OfficeHudTextColor);
            _officeTimeText = AddText(
                firstRow, string.Empty, 19f, false, TextAlignmentOptions.Midline, 460f, 42f, OfficeHudMutedColor);
            var managementButton = AddButton(
                firstRow, "관리 화면   ESC", _bootstrap.ShowManagementNow, true, 210f, 42f);
            StyleOfficeHudButton(managementButton, true);

            var secondRow = CreateRect("Status Row", top);
            AddLayout(secondRow, -1f, 54f, 54f, 0f);
            var statusLayout = secondRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(statusLayout, null, 8f);
            statusLayout.childAlignment = TextAnchor.MiddleLeft;
            _officeMemberStatus.Clear();
            if (_bootstrap.State != null)
            {
                foreach (var member in _bootstrap.State.Family.Members)
                {
                    var card = CreatePanel($"Member Status {member.MemberId}", secondRow, OfficeHudCardColor, CardSprite);
                    AddLayout(card, -1f, 54f, 54f, 1f);
                    _officeMemberStatus[member.MemberId] = AddText(
                        card, string.Empty, 16f, false, TextAlignmentOptions.MidlineLeft, -1f, -1f, OfficeHudTextColor);
                    var memberLabel = _officeMemberStatus[member.MemberId];
                    memberLabel.margin = new Vector4(12f, 0f, 10f, 0f);
                    memberLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    Stretch(memberLabel.rectTransform);
                }
            }

            var speedCard = CreatePanel("Time Speed", secondRow, OfficeHudCardColor, CardSprite);
            AddLayout(speedCard, 310f, 54f, 54f, 0f);
            var speedLayout = speedCard.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(speedLayout, new RectOffset(10, 8, 7, 7), 6f);
            speedLayout.childAlignment = TextAnchor.MiddleLeft;
            _officeSpeedText = AddText(
                speedCard, string.Empty, 15f, false, TextAlignmentOptions.MidlineLeft, 96f, 40f, OfficeHudMutedColor);
            _officeSpeedButtons.Clear();
            foreach (var speed in new[] { 1, 2, 4 })
            {
                var capturedSpeed = speed;
                var button = AddButton(
                    speedCard,
                    $"{speed}×",
                    () => _bootstrap.SetWorldTimeScaleNow(capturedSpeed),
                    false,
                    56f,
                    40f);
                StyleOfficeHudButton(button, false);
                _officeSpeedButtons[speed] = button;
            }

            var noticeRow = CreateRect("Notice Row", top);
            AddLayout(noticeRow, -1f, 26f, 26f, 0f);
            var noticeLayout = noticeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(noticeLayout, null, 16f);
            noticeLayout.childAlignment = TextAnchor.MiddleLeft;
            var live = AddText(
                noticeRow, "●  LIVE", 14f, true, TextAlignmentOptions.MidlineLeft, 92f, 26f, OfficeHudAccentColor);
            live.characterSpacing = 1.5f;
            _officeNoticeText = AddText(
                noticeRow, string.Empty, 15f, false, TextAlignmentOptions.MidlineLeft, -1f, 26f, OfficeHudMutedColor);
            AddText(
                noticeRow,
                "C  시점 전환     E  작업     F11  전체 화면",
                14f,
                false,
                TextAlignmentOptions.MidlineRight,
                430f,
                26f,
                OfficeHudMutedColor);

            var spacer = CreateRect("World Input Space", _officeSafeRoot);
            AddLayout(spacer, -1f, -1f, -1f, 1f);
        }

        private void StyleOfficeHudButton(Button button, bool primary)
        {
            if (button == null) return;
            var normal = primary ? OfficeHudAccentColor : Hex("2C4B49");
            var highlighted = primary ? Hex("FA8C6D") : Hex("3A5E5A");
            var pressed = primary ? Hex("C8553E") : Hex("173331");
            if (button.image != null) button.image.color = normal;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = highlighted,
                pressedColor = pressed,
                selectedColor = highlighted,
                disabledColor = Hex("253333"),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = OfficeHudTextColor;
        }

        private void BuildManagementOverlay()
        {
            var background = _managementSafeRoot.gameObject.AddComponent<Image>();
            background.color = PageColor;
            background.sprite = PanelSprite;
            background.type = Image.Type.Sliced;
            var rootLayout = _managementSafeRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(rootLayout, new RectOffset(24, 24, 24, 24), 16f);
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandHeight = false;

            var top = CreatePanel("Management Top HUD", _managementSafeRoot, PanelColor, PanelSprite);
            AddLayout(top, -1f, 88f, 88f, 0f);
            var topLayout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(topLayout, new RectOffset(20, 20, 14, 14), 16f);
            _managementCompanyText = AddText(top, string.Empty, 28f, true, TextAlignmentOptions.MidlineLeft, -1f, 60f);
            _managementTimeText = AddText(top, string.Empty, 20f, false, TextAlignmentOptions.Midline, 430f, 60f);
            _managementCashText = AddText(top, string.Empty, 20f, false, TextAlignmentOptions.MidlineRight, 370f, 60f);
            AddButton(top, "사무실 관찰  ESC", _bootstrap.CloseManagementNow, true, 230f, 54f);

            var body = CreateRect("Management Body Grid", _managementSafeRoot);
            AddLayout(body, -1f, -1f, -1f, 1f);
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(bodyLayout, null, 16f);
            bodyLayout.childControlWidth = true;
            bodyLayout.childForceExpandWidth = false;

            _familyHost = CreatePanel("Family Rail", body, PanelColor, PanelSprite);
            AddLayout(_familyHost, 288f, -1f, -1f, 0f);

            var center = CreateRect("Management Center", body);
            AddLayout(center, -1f, -1f, -1f, 1f);
            var centerLayout = center.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(centerLayout, null, 16f);
            centerLayout.childControlHeight = true;
            centerLayout.childForceExpandHeight = false;
            BuildTabRow(center);
            _contentHost = CreateRect("Primary Content", center);
            AddLayout(_contentHost, -1f, -1f, -1f, 1f);
            _progressHost = CreatePanel("Progress Strip", center, PanelColor, PanelSprite);
            AddLayout(_progressHost, -1f, 280f, 280f, 0f);

            BuildQuickActions(body);
        }

        private void BuildTabRow(RectTransform center)
        {
            var tabs = CreatePanel("Management Tabs", center, PanelColor, PanelSprite);
            AddLayout(tabs, -1f, 56f, 56f, 0f);
            var layout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(8, 8, 8, 8), 8f);
            AddTabButton(tabs, "계약", () => SelectTab(OfficeManagementTab.Contracts));
            AddTabButton(tabs, "R&D", () => SelectTab(OfficeManagementTab.Research));
            AddTabButton(tabs, "시장·제품", () => SelectTab(OfficeManagementTab.Products));
        }

        private void BuildQuickActions(RectTransform body)
        {
            var quick = CreatePanel("Quick Actions", body, PanelColor, PanelSprite);
            AddLayout(quick, 288f, -1f, -1f, 0f);
            var layout = quick.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(16, 16, 16, 16), 10f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(quick, "빠른 조작", 25f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f);
            _managementNoticeText = AddText(quick, string.Empty, 17f, false, TextAlignmentOptions.TopLeft, -1f, 120f);
            AddButton(quick, "사무실 관찰", _bootstrap.CloseManagementNow, true, -1f, 52f);
            AddButton(quick, "+1시간", () => RunAndRefresh(() => _bootstrap.AdvanceTimeNow(60)), false, -1f, 48f);
            AddButton(quick, "+1일", () => RunAndRefresh(() => _bootstrap.AdvanceTimeNow(1440)), false, -1f, 48f);
            AddButton(quick, "저장", _bootstrap.ShowSaveSlotsNow, false, -1f, 48f);
            AddButton(quick, "불러오기", _bootstrap.ShowLoadSlotsNow, false, -1f, 48f);
            AddButton(quick, "게임 메뉴", _bootstrap.ShowPauseMenuNow, false, -1f, 48f);
            AddButton(quick, "전체 화면  F11", _bootstrap.ToggleFullscreenNow, false, -1f, 48f);
            AddText(quick, "ESC  관리 화면 닫기\nF11  전체 화면 전환\nCtrl+S  현재 슬롯 빠른 저장", 16f, false, TextAlignmentOptions.BottomLeft, -1f, 90f);
        }

        private void SelectTab(OfficeManagementTab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            RebuildManagementData();
        }

        private void RebuildManagementData()
        {
            if (!_built && _contentHost == null) return;
            RebuildFamilyRail();
            ClearChildren(_contentHost);
            ClearLayoutGroups(_contentHost);
            ClearChildren(_progressHost);
            ClearLayoutGroups(_progressHost);
            switch (_tab)
            {
                case OfficeManagementTab.Contracts:
                    BuildContractContent();
                    break;
                case OfficeManagementTab.Research:
                    BuildResearchContent();
                    break;
                case OfficeManagementTab.Products:
                    BuildProductContent();
                    break;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_managementSafeRoot);
            RefreshLiveLabels();
        }

        private void RebuildFamilyRail()
        {
            ClearChildren(_familyHost);
            var layout = _familyHost.gameObject.GetComponent<VerticalLayoutGroup>() ??
                         _familyHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(14, 14, 14, 14), 10f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(_familyHost, "가족 4명", 25f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f);
            if (_bootstrap.State == null) return;
            foreach (var member in _bootstrap.State.Family.Members)
            {
                var card = CreatePanel($"Family {member.MemberId}", _familyHost, CardColor, CardSprite);
                AddLayout(card, -1f, 188f, 188f, 0f);
                var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureLayout(cardLayout, new RectOffset(14, 14, 12, 12), 4f);
                cardLayout.childControlHeight = true;
                cardLayout.childForceExpandHeight = false;
                AddText(card, $"{member.DisplayName} · {member.AgeAt(_bootstrap.State.Time)}살", 21f, true, TextAlignmentOptions.MidlineLeft, -1f, 30f);
                AddText(card, member.CompanyDuty, 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
                AddText(card, $"기술개발 {member.Capability.Skills.Engineering}   운영 {member.Capability.Skills.Operations}", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 26f);
                AddText(card, $"체력 {member.Energy}   스트레스 {member.Stress}", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 26f);
                AddText(card, AutonomyPresentationLabel(member), 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            }
        }

        private void BuildContractContent()
        {
            var contentLayout = _contentHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(contentLayout, null, 16f);
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            for (var index = 0; index < 3; index++) BuildContractCard(index);

            var progressLayout = _progressHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(progressLayout, new RectOffset(16, 16, 14, 14), 10f);
            progressLayout.childControlHeight = true;
            progressLayout.childForceExpandHeight = false;
            var titleRow = CreateRect("Contract Progress Title", _progressHost);
            AddLayout(titleRow, -1f, 44f, 44f, 0f);
            var titleLayout = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(titleLayout, null, 10f);
            var active = _bootstrap.State.Contracts.Contracts
                .Where(item => item.Status == SubcontractStatus.Active)
                .Take(2)
                .ToArray();
            AddText(titleRow, $"진행 계약  {active.Length}/2", 24f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f);
            AddButton(titleRow, $"{BusinessIndustryCatalog.Get(_contractIndustry).DisplayName}  ▶", NextContractIndustry, false, 230f, 40f);
            AddButton(titleRow, "다른 의뢰", NextContractPage, false, 150f, 40f);
            if (active.Length == 0)
            {
                AddText(_progressHost, "위 의뢰를 수락한 뒤 가족에게 업무를 배정하세요. 플레이어는 사무실의 의미 장소에서 E로 직접 참여합니다.", 19f, false, TextAlignmentOptions.TopLeft, -1f, 150f);
                return;
            }

            var contractsRow = CreateRect("Active Contracts", _progressHost);
            AddLayout(contractsRow, -1f, -1f, -1f, 1f);
            var contractsLayout = contractsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(contractsLayout, null, 12f);
            foreach (var contract in active) BuildActiveContractCard(contractsRow, contract);
        }

        private void BuildContractCard(int cardIndex)
        {
            var sequence = _contractBoardPage * 3L + cardIndex;
            var clientIndex = (int)(sequence % ContractClientIds.Length);
            var offer = BootstrapContractCatalog.CreateIndustryOffer(
                _bootstrap.State.WorldSeed,
                ContractClientIds[clientIndex],
                ContractClientNames[clientIndex],
                _contractIndustry,
                sequence);
            var existing = _bootstrap.State.Contracts.Contracts.FirstOrDefault(item => item.Offer.OfferId == offer.OfferId);
            var card = CreatePanel($"Offer {cardIndex + 1}", _contentHost, CardColor, CardSprite);
            AddLayout(card, -1f, -1f, -1f, 1f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(18, 18, 16, 16), 7f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(card, offer.ExactClientDisplayName, 26f, true, TextAlignmentOptions.MidlineLeft, -1f, 38f);
            AddText(card, BusinessIndustryCatalog.Get(offer.Industry).DisplayName, 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 26f);
            AddText(card, offer.Title, 20f, false, TextAlignmentOptions.TopLeft, -1f, 58f);
            AddText(card, $"요구  업무 적합도 {offer.RequiredCapability}", 18f, false, TextAlignmentOptions.MidlineLeft, -1f, 30f);
            AddText(card, $"마감 {offer.DeadlineDays}일 · 작업 {offer.EstimatedPersonHours}시간\n착수비 {offer.UpfrontCostWon:N0}원 · 보상 {offer.RewardWon:N0}원", 18f, false, TextAlignmentOptions.TopLeft, -1f, 62f);
            AddText(card, offer.PenaltyWon == 0 ? "위약금 없음" : $"실패 위약금 {offer.PenaltyWon:N0}원", 18f, false, TextAlignmentOptions.MidlineLeft, -1f, 30f, offer.PenaltyWon == 0 ? AccentColor : PeachColor);
            if (!string.IsNullOrEmpty(offer.RequiredTechnologyId))
                AddText(card, $"필요 연구 · {ResearchTechnologyCatalog.Get(offer.RequiredTechnologyId).DisplayName}", 16f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            var spacer = CreateRect("Card Flexible Space", card);
            AddLayout(spacer, -1f, -1f, -1f, 1f);
            var button = AddButton(
                card,
                existing == null ? "계약 검토 후 수락" : ContractStatusLabel(existing.Status),
                () => RunAndRefresh(() => _bootstrap.AcceptOfferNow(offer)),
                true,
                -1f,
                50f);
            SetButtonInteractable(button, existing == null);
        }

        private void BuildActiveContractCard(RectTransform parent, SubcontractState contract)
        {
            var dueDays = Math.Max(0, (int)Math.Ceiling((contract.DueMinute - _bootstrap.State.Time.ElapsedMinutes) / 1440.0));
            var card = CreatePanel($"Active {contract.Offer.OfferId}", parent, CardColor, CardSprite);
            AddLayout(card, -1f, -1f, -1f, 1f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(12, 12, 10, 10), 6f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(card, contract.Offer.Title, 19f, true, TextAlignmentOptions.MidlineLeft, -1f, 30f);
            AddText(card, $"진행 {contract.CompletedPersonHours}/{contract.Offer.EstimatedPersonHours}시간 · 마감 {dueDays}일", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 26f);
            var buttons = CreateRect("Member Assignment", card);
            AddLayout(buttons, -1f, 42f, 42f, 0f);
            var buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(buttonLayout, null, 6f);
            foreach (var member in _bootstrap.State.Family.Members)
            {
                var localMember = member;
                var schedule = FamilyScheduleRules.Resolve(member.Role, _bootstrap.State.Time.Now);
                var isPlayer = member.Role == FamilyRole.Player;
                var label = isPlayer ? "나 · 직접" : schedule.CanPerformCompanyWork ? $"{member.DisplayName} 4h" : $"{member.DisplayName} · {ScheduleShortLabel(schedule.Kind)}";
                var button = AddButton(buttons, label, () => RunAndRefresh(() => _bootstrap.AssignContractWorkNow(contract.Offer.OfferId, localMember.MemberId)), false, -1f, 40f);
                SetButtonInteractable(button, !isPlayer && schedule.CanPerformCompanyWork && member.Energy >= 8);
            }
        }

        private void BuildResearchContent()
        {
            if (!_bootstrap.State.Growth.ResearchCenterUnlocked)
            {
                var panel = CreatePanel("Research Locked", _contentHost, CardColor, CardSprite);
                Stretch(panel);
                var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureLayout(layout, new RectOffset(36, 36, 36, 36), 18f);
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                AddText(panel, "R&D 센터 설립", 32f, true, TextAlignmentOptions.MidlineLeft, -1f, 52f);
                AddText(panel, "자본을 투자해 연구 조직을 만들면 고급 능력치와 3D 모델링·자동화·시장 분석 기술이 열립니다.", 22f, false, TextAlignmentOptions.TopLeft, -1f, 120f);
                AddText(panel, $"설립 투자금  {CompanyGrowthState.ResearchCenterOpeningCostWon:N0}원", 24f, true, TextAlignmentOptions.MidlineLeft, -1f, 44f);
                var button = AddButton(panel, "R&D 센터 설립", () => RunAndRefresh(_bootstrap.OpenResearchCenterNow), true, 340f, 54f);
                SetButtonInteractable(button, _bootstrap.State.Company.CashWon >= CompanyGrowthState.ResearchCenterOpeningCostWon);
            }
            else
            {
                var layout = _contentHost.gameObject.AddComponent<HorizontalLayoutGroup>();
                ConfigureLayout(layout, null, 16f);
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                foreach (var definition in ResearchTechnologyCatalog.All) BuildResearchCard(definition);
            }

            var progressLayout = _progressHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(progressLayout, new RectOffset(16, 16, 14, 14), 8f);
            progressLayout.childControlHeight = true;
            progressLayout.childForceExpandHeight = false;
            AddText(_progressHost, "공개된 가족 역량", 24f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f);
            foreach (var member in _bootstrap.State.Family.Members)
            {
                var relationship = member.MemberId == "player" ? string.Empty : $" · 나와 {_bootstrap.State.Family.RelationshipLabel("player", member.MemberId)}";
                AddText(_progressHost, $"{member.DisplayName} · 기획 {member.Capability.Skills.Planning}  창작 {member.Capability.Skills.Creative}  사업 {member.Capability.Skills.Business}  협업 {member.Capability.Skills.Collaboration} · 기억 {member.CareerMemories.Count}{relationship}", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 30f);
            }
        }

        private void BuildResearchCard(ResearchTechnologyDefinition definition)
        {
            var researched = _bootstrap.State.Growth.HasTechnology(definition.TechnologyId);
            var prerequisiteReady = string.IsNullOrEmpty(definition.PrerequisiteId) || _bootstrap.State.Growth.HasTechnology(definition.PrerequisiteId);
            var card = CreatePanel($"Research {definition.TechnologyId}", _contentHost, CardColor, CardSprite);
            AddLayout(card, -1f, -1f, -1f, 1f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(18, 18, 16, 16), 10f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(card, definition.DisplayName, 26f, true, TextAlignmentOptions.MidlineLeft, -1f, 42f);
            AddText(card, definition.Description, 20f, false, TextAlignmentOptions.TopLeft, -1f, 130f);
            AddText(card, $"연구비  {definition.CostWon:N0}원", 20f, true, TextAlignmentOptions.MidlineLeft, -1f, 36f);
            if (!string.IsNullOrEmpty(definition.PrerequisiteId))
                AddText(card, $"선행 · {ResearchTechnologyCatalog.Get(definition.PrerequisiteId).DisplayName}", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 30f);
            var spacer = CreateRect("Research Flexible Space", card);
            AddLayout(spacer, -1f, -1f, -1f, 1f);
            var button = AddButton(card, researched ? "연구 완료" : prerequisiteReady ? "연구 투자" : "선행 연구 필요", () => RunAndRefresh(() => _bootstrap.ResearchTechnologyNow(definition.TechnologyId)), true, -1f, 52f);
            SetButtonInteractable(button, !researched && prerequisiteReady && _bootstrap.State.Company.CashWon >= definition.CostWon);
        }

        private void BuildProductContent()
        {
            var vertical = _contentHost.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vertical, null, 14f);
            vertical.childControlHeight = true;
            vertical.childForceExpandHeight = true;
            var definitions = BusinessIndustryCatalog.All;
            for (var rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = CreateRect($"Business Row {rowIndex + 1}", _contentHost);
                AddLayout(row, -1f, -1f, -1f, 1f);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                ConfigureLayout(rowLayout, null, 14f);
                rowLayout.childControlWidth = true;
                rowLayout.childForceExpandWidth = true;
                for (var column = 0; column < 2; column++) BuildBusinessCard(row, definitions[rowIndex * 2 + column]);
            }
            BuildProductProgress();
        }

        private void BuildBusinessCard(RectTransform parent, BusinessIndustryDefinition definition)
        {
            var owned = _bootstrap.State.Growth.HasOwnedBusiness(definition.Industry);
            var selected = owned && _selectedBusinessIndustry == definition.Industry;
            var card = CreatePanel($"Business {definition.Industry}", parent, selected ? Hex("E5F4F0") : CardColor, CardSprite);
            AddLayout(card, -1f, -1f, -1f, 1f);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(14, 14, 12, 12), 5f);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddText(card, definition.DisplayName, 23f, true, TextAlignmentOptions.MidlineLeft, -1f, 34f);
            AddText(card, definition.Description, 17f, false, TextAlignmentOptions.TopLeft, -1f, 42f);
            AddText(card, $"초기 일감 · {string.Join(" · ", definition.StarterExamples)}", 16f, false, TextAlignmentOptions.TopLeft, -1f, 34f);
            if (owned)
            {
                var business = _bootstrap.State.Growth.GetOwnedBusiness(definition.Industry);
                AddText(card, $"운영 중 · Lv.{business.Level} · 누적 매출 {business.TotalRevenueWon:N0}원", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 30f);
                AddButton(card, selected ? "선택됨" : "운영 선택", () => { _selectedBusinessIndustry = definition.Industry; RebuildManagementData(); }, false, -1f, 40f);
                return;
            }
            var analysisUnlocked = _bootstrap.State.Growth.HasTechnology(ResearchTechnologyIds.MarketAnalysis);
            var marketReady = analysisUnlocked && _bootstrap.State.Growth.MarketReport != null && _bootstrap.State.Growth.MarketReport.Industry == definition.Industry;
            var costWon = _bootstrap.State.Growth.FoundingCostFor(definition.Industry);
            AddText(card, marketReady ? $"창업 필요 자금 · {costWon:N0}원" : "이 업종의 시장 조사가 필요합니다.", 16f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            var button = AddButton(card, marketReady ? "이 사업 시작" : "이 분야 조사", () => RunAndRefresh(() =>
            {
                _selectedBusinessIndustry = definition.Industry;
                if (marketReady) _bootstrap.FoundBusinessNow(definition.Industry);
                else _bootstrap.PurchaseMarketReportNow(definition.Industry);
            }), false, -1f, 40f);
            SetButtonInteractable(button, analysisUnlocked && _bootstrap.State.Company.CashWon >= (marketReady ? costWon : CompanyGrowthState.MarketReportCostWon));
        }

        private void BuildProductProgress()
        {
            var layout = _progressHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, new RectOffset(16, 16, 14, 14), 14f);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            var portfolio = CreateRect("Business Portfolio", _progressHost);
            AddLayout(portfolio, -1f, -1f, -1f, 1f);
            var portfolioLayout = portfolio.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(portfolioLayout, null, 6f);
            portfolioLayout.childControlHeight = true;
            portfolioLayout.childForceExpandHeight = false;
            AddText(portfolio, _bootstrap.State.Growth.CorporateStage, 24f, true, TextAlignmentOptions.MidlineLeft, -1f, 38f);
            AddText(portfolio, $"사업 {_bootstrap.State.Growth.OwnedBusinesses.Count}/4 · 글로벌 준비 {_bootstrap.State.Growth.GlobalExpansionReadiness(_bootstrap.State.Company)}%", 17f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            var report = _bootstrap.State.Growth.MarketReport;
            AddText(portfolio, report == null ? "시장 보고서가 없습니다." : $"{BusinessIndustryCatalog.Get(report.Industry).DisplayName} · {report.Genre}\n핵심 {report.DesiredFeature} · 수요 {report.Demand}/100", 17f, false, TextAlignmentOptions.TopLeft, -1f, 58f);
            var marketUnlocked = _bootstrap.State.Growth.HasTechnology(ResearchTechnologyIds.MarketAnalysis);
            var reportButton = AddButton(portfolio, "시장 재조사 · 100,000원", () => RunAndRefresh(() => _bootstrap.PurchaseMarketReportNow(_selectedBusinessIndustry)), false, -1f, 42f);
            SetButtonInteractable(reportButton, marketUnlocked && _bootstrap.State.Company.CashWon >= CompanyGrowthState.MarketReportCostWon);

            var product = CreateRect("Product Project", _progressHost);
            AddLayout(product, -1f, -1f, -1f, 1f);
            var productLayout = product.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(productLayout, null, 6f);
            productLayout.childControlHeight = true;
            productLayout.childForceExpandHeight = false;
            if (_bootstrap.State.Growth.OwnedBusinesses.Count == 0)
            {
                AddText(product, "제품 기획", 24f, true, TextAlignmentOptions.MidlineLeft, -1f, 38f);
                AddText(product, "하청으로 현금을 모으고 R&D·시장조사를 마친 뒤 첫 사업을 시작하세요.", 18f, false, TextAlignmentOptions.TopLeft, -1f, 90f);
                return;
            }
            if (!_bootstrap.State.Growth.HasOwnedBusiness(_selectedBusinessIndustry))
                _selectedBusinessIndustry = _bootstrap.State.Growth.OwnedBusinesses[0].Industry;
            var selectedBusiness = _bootstrap.State.Growth.GetOwnedBusiness(_selectedBusinessIndustry);
            AddText(product, $"{selectedBusiness.BusinessName} · 제품 기획", 23f, true, TextAlignmentOptions.MidlineLeft, -1f, 36f);
            var input = AddInputField(product, _productTitle, value => _productTitle = value, 46f);
            var canStart = _bootstrap.State.Growth.MarketReport != null &&
                           _bootstrap.State.Growth.MarketReport.Industry == _selectedBusinessIndustry &&
                           (_bootstrap.State.Growth.ProductProject == null || _bootstrap.State.Growth.ProductProject.Resolved);
            var budgetRow = CreateRect("Product Budgets", product);
            AddLayout(budgetRow, -1f, 42f, 42f, 0f);
            var budgetLayout = budgetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(budgetLayout, null, 6f);
            foreach (var budget in new long[] { 1_000_000, 2_000_000, 4_000_000 })
            {
                var localBudget = budget;
                var button = AddButton(budgetRow, $"{budget / 10_000}만", () => RunAndRefresh(() => _bootstrap.StartProductNow(_selectedBusinessIndustry, input.text, localBudget)), false, -1f, 40f);
                SetButtonInteractable(button, canStart);
            }
            var project = _bootstrap.State.Growth.ProductProject;
            if (project == null) AddText(product, "예산을 선택하면 제품 개발이 시작됩니다.", 16f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            else if (!project.Resolved)
            {
                var remainingDays = Math.Max(0, (int)Math.Ceiling((project.DueMinute - _bootstrap.State.Time.ElapsedMinutes) / 1440.0));
                AddText(product, $"개발 중 · {project.Title} · 출시까지 {remainingDays}일", 16f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
            }
            else AddText(product, $"출시 완료 · 완성도 {project.Quality}/100 · 손익 {(project.RevenueWon - project.BudgetWon):N0}원", 16f, false, TextAlignmentOptions.MidlineLeft, -1f, 28f);
        }

        private void NextContractIndustry()
        {
            _contractIndustry = (BusinessIndustry)(((int)_contractIndustry + 1) % BusinessIndustryCatalog.All.Count);
            _contractBoardPage = 0;
            _bootstrap.SetWorldNotice($"{BusinessIndustryCatalog.Get(_contractIndustry).DisplayName} 하청 목록으로 전환했습니다.");
            RebuildManagementData();
        }

        private void NextContractPage()
        {
            var pageCount = Math.Max(1, (BootstrapContractCatalog.OfferCountForIndustry(_contractIndustry) + 2) / 3);
            _contractBoardPage = (_contractBoardPage + 1) % pageCount;
            _bootstrap.SetWorldNotice(_contractBoardPage == 0 ? "초보자용 무위약금 목록입니다." : "고수익 의뢰는 연구와 위약금 확인이 필수입니다.");
            RebuildManagementData();
        }

        private void RunAndRefresh(Action action)
        {
            action?.Invoke();
            RebuildManagementData();
        }

        private void RefreshLiveLabels()
        {
            var state = _bootstrap.State;
            if (state == null) return;
            SetText(_officeCompanyText, state.Company.CompanyName);
            SetText(_officeTimeText, state.Time.Now.ToString("yyyy년 MM월 dd일 ddd HH:mm"));
            SetText(_officeNoticeText, _bootstrap.WorldNotice);
            SetText(_officeSpeedText, $"시간배속 {_bootstrap.WorldTimeScale:0}×");
            RefreshOfficeSpeedButtons();
            SetText(_managementCompanyText, $"{state.Company.CompanyName} · 관리 화면");
            SetText(_managementTimeText, state.Time.Now.ToString("yyyy년 MM월 dd일 ddd HH:mm"));
            SetText(_managementCashText, $"현금 {state.Company.CashWon:N0}원  ·  평판 {state.Company.Reputation}");
            SetText(_managementNoticeText, $"SLOT {_bootstrap.ActiveSlot}\n{_bootstrap.WorldNotice}");
            RefreshObservationStatuses();
        }

        private void RefreshOfficeSpeedButtons()
        {
            var selected = Mathf.RoundToInt(_bootstrap.WorldTimeScale);
            foreach (var pair in _officeSpeedButtons)
            {
                var button = pair.Value;
                if (button == null || button.image == null) continue;
                var active = pair.Key == selected;
                var normal = active ? OfficeHudAccentColor : Hex("2C4B49");
                var colors = button.colors;
                colors.normalColor = normal;
                colors.selectedColor = active ? Hex("FA8C6D") : Hex("3A5E5A");
                button.colors = colors;
                button.image.color = normal;
            }
        }

        private void RefreshObservationStatuses()
        {
            if (Time.unscaledTime >= _nextSourceRefresh)
            {
                _nextSourceRefresh = Time.unscaledTime + 2f;
                _statusSources.Clear();
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (!(behaviour is IOfficeObservationStatusSource source) || string.IsNullOrWhiteSpace(source.MemberId)) continue;
                    _statusSources[source.MemberId] = source;
                }
                _agents.Clear();
                foreach (var agent in FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None))
                    if (agent != null && !string.IsNullOrWhiteSpace(agent.AgentId)) _agents[agent.AgentId] = agent;
            }

            foreach (var member in _bootstrap.State.Family.Members)
            {
                if (!_officeMemberStatus.TryGetValue(member.MemberId, out var label)) continue;
                var status = ResolveObservationStatus(member);
                SetText(label, $"{member.DisplayName}\n{status}");
            }
        }

        private string ResolveObservationStatus(FamilyMemberState member)
        {
            if (_statusSources.TryGetValue(member.MemberId, out var source))
            {
                var status = ObservationStatusLabel(source.StatusKind);
                return string.IsNullOrWhiteSpace(source.StatusDetail)
                    ? status
                    : $"{status} · {source.StatusDetail}";
            }
            if (member.MemberId == "player")
            {
                var interactor = FindFirstObjectByType<PlayerOfficeWorkInteractor>();
                if (interactor != null && interactor.IsWorking) return "타이핑 · 직접 작업";
                return AutonomyPresentationLabel(member);
            }
            if (_agents.TryGetValue(member.MemberId, out var agent))
            {
                if (agent.IsPresentationAway) return AutonomyPresentationLabel(member);
                if (agent.CurrentActivity == OfficeActivity.Walking) return $"이동 · {agent.CurrentActivityLabel}";
                if (agent.CurrentActivity == OfficeActivity.Work) return $"타이핑 · {agent.CurrentActivityLabel}";
                return agent.CurrentActivityLabel;
            }
            return AutonomyPresentationLabel(member);
        }

        private static string AutonomyPresentationLabel(FamilyMemberState member)
        {
            string micro = member.Autonomy.MicroAction.ActionLabel;
            return string.IsNullOrEmpty(micro) ? member.Autonomy.ActionLabel : micro;
        }

        private static string ObservationStatusLabel(OfficeObservationStatusKind kind)
        {
            switch (kind)
            {
                case OfficeObservationStatusKind.Moving: return "이동";
                case OfficeObservationStatusKind.Seated: return "착석";
                case OfficeObservationStatusKind.Typing: return "타이핑";
                case OfficeObservationStatusKind.Mouse: return "마우스";
                case OfficeObservationStatusKind.Drinking: return "물 마시기";
                case OfficeObservationStatusKind.Meeting: return "회의";
                case OfficeObservationStatusKind.Printing: return "출력";
                case OfficeObservationStatusKind.Break: return "휴식";
                case OfficeObservationStatusKind.Outside: return "외부 일정";
                default: return "대기";
            }
        }

        private void RefreshSafeAreaIfNeeded(bool force = false)
        {
            var safe = Screen.safeArea;
            if (!force && Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight && safe == _lastSafeArea) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = safe;
            ApplySafeArea(_officeSafeRoot, safe);
            ApplySafeArea(_managementSafeRoot, safe);
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
            var catalog = Resources.Load<UiRemasterFontCatalog>(UiRemasterTypography.FontCatalogResourcePath);
            if (catalog != null && catalog.IsComplete)
            {
                _bodyFont = CreateFontAsset(catalog.BodySource, "Management UI Maplestory Light V3");
                _headingFont = CreateFontAsset(catalog.HeadingSource, "Management UI Maplestory Bold V3");
                _fallbackFont = CreateFontAsset(catalog.FallbackSource, "Management UI Pretendard Fallback V3");
            }
            if (_bodyFont == null || _headingFont == null || _fallbackFont == null)
            {
                var builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bodyFont = _bodyFont != null ? _bodyFont : CreateFontAsset(builtIn, "Management UI Builtin Fallback");
                _headingFont = _headingFont != null ? _headingFont : _bodyFont;
                _fallbackFont = _fallbackFont != null ? _fallbackFont : _bodyFont;
                Debug.LogError("MANAGEMENT_UI_FONT_FALLBACK: bundled Korean font catalog is missing or incomplete.");
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
            // CreateFontAsset leaves the fallback table null on runtime-created assets,
            // and LoadFonts chains the Korean fallback into it right after this returns.
            if (asset.fallbackFontAssetTable == null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            return asset;
        }

        private void LoadSkin()
        {
            _skin = Resources.Load<ManagementUiSkinCatalog>(ManagementUiLayoutMetrics.SkinResourcePath);
            if (_skin != null && _skin.IsComplete) return;
            _skin = null;
            _fallbackTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "Management UI Solid Fallback",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = Enumerable.Repeat(Color.white, 64).ToArray();
            _fallbackTexture.SetPixels(pixels);
            _fallbackTexture.Apply(false, false);
            _fallbackSprite = Sprite.Create(
                _fallbackTexture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(2f, 2f, 2f, 2f));
            _fallbackSprite.name = "Management UI Solid 9-Slice Fallback";
            _fallbackSprite.hideFlags = HideFlags.DontSave;
            if (!_reportedSkinFallback)
            {
                _reportedSkinFallback = true;
                Debug.LogWarning("MANAGEMENT_UI_SKIN_FALLBACK: versioned 9-slice skin is absent; using opaque high-contrast solid panels.");
            }
        }

        private Sprite PanelSprite => _skin != null ? _skin.Panel : _fallbackSprite;
        private Sprite CardSprite => _skin != null ? _skin.Card : _fallbackSprite;
        private Sprite ButtonSprite => _skin != null ? _skin.Button : _fallbackSprite;
        private Sprite DisabledButtonSprite => _skin != null ? _skin.ButtonDisabled : _fallbackSprite;
        private Sprite TabSprite => _skin != null ? _skin.Tab : _fallbackSprite;

        private TMP_Text AddText(
            RectTransform parent,
            string value,
            float size,
            bool heading,
            TextAlignmentOptions alignment,
            float preferredWidth,
            float preferredHeight,
            Color? color = null)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = heading ? _headingFont : _bodyFont;
            text.fontSize = size;
            text.color = color ?? (heading ? TextColor : SecondaryTextColor);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.richText = false;
            AddLayout(rect, preferredWidth, preferredHeight, preferredHeight, preferredWidth < 0f ? 1f : 0f);
            SetText(text, value);
            return text;
        }

        private Button AddButton(
            RectTransform parent,
            string label,
            Action onClick,
            bool primary,
            float preferredWidth,
            float preferredHeight)
        {
            var rect = CreateRect($"Button {label}", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ButtonSprite;
            image.type = Image.Type.Sliced;
            image.color = primary ? AccentColor : CardColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = primary ? AccentColor : CardColor,
                highlightedColor = primary ? AccentHoverColor : Hex("E9F3F0"),
                pressedColor = primary ? Hex("07564F") : Hex("DDEAE6"),
                selectedColor = primary ? AccentHoverColor : Hex("E9F3F0"),
                disabledColor = DisabledColor,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            if (onClick != null) button.onClick.AddListener(() => onClick());
            AddLayout(rect, preferredWidth, preferredHeight, preferredHeight, preferredWidth < 0f ? 1f : 0f);
            var text = AddText(rect, label, 18f, true, TextAlignmentOptions.Center, -1f, -1f, primary ? Color.white : TextColor);
            Stretch(text.rectTransform);
            return button;
        }

        private Button AddTabButton(RectTransform parent, string label, Action onClick)
        {
            var button = AddButton(parent, label, onClick, false, -1f, 40f);
            if (button.image != null) button.image.sprite = TabSprite;
            return button;
        }

        private void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;
            if (button.image != null) button.image.sprite = interactable ? ButtonSprite : DisabledButtonSprite;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && !interactable) label.color = DisabledTextColor;
        }

        private TMP_InputField AddInputField(RectTransform parent, string value, Action<string> onValueChanged, float height)
        {
            var root = CreatePanel("Product Title Input", parent, CardColor, CardSprite);
            AddLayout(root, -1f, height, height, 0f);
            var viewport = CreateRect("Text Area", root);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12f, 6f);
            viewport.offsetMax = new Vector2(-12f, -6f);
            var text = AddText(viewport, value, 18f, false, TextAlignmentOptions.MidlineLeft, -1f, -1f);
            Stretch(text.rectTransform);
            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.text = value;
            input.characterLimit = 28;
            input.lineType = TMP_InputField.LineType.SingleLine;
            if (onValueChanged != null) input.onValueChanged.AddListener(valueChanged => onValueChanged(valueChanged));
            return input;
        }

        private RectTransform CreatePanel(string objectName, Transform parent, Color color, Sprite sprite)
        {
            var rect = CreateRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : _fallbackSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static LayoutElement AddLayout(RectTransform rect, float preferredWidth, float preferredHeight, float minimumHeight, float flexibleWidth)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
            if (minimumHeight >= 0f) element.minHeight = minimumHeight;
            element.flexibleWidth = flexibleWidth;
            if (preferredHeight < 0f) element.flexibleHeight = 1f;
            return element;
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup layout, RectOffset padding, float spacing)
        {
            layout.padding = padding ?? new RectOffset();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void SetText(TMP_Text target, string value)
        {
            if (target == null) return;
            var normalized = value ?? string.Empty;
            EnsureGlyphs(target.font, normalized);
            target.text = normalized;
        }

        private void EnsureGlyphs(TMP_FontAsset font, string value)
        {
            if (font == null || string.IsNullOrEmpty(value)) return;
            if (font.TryAddCharacters(value, out var missing) || string.IsNullOrEmpty(missing)) return;
            if (_fallbackFont != null && _fallbackFont != font &&
                (_fallbackFont.TryAddCharacters(missing, out var fallbackMissing) || string.IsNullOrEmpty(fallbackMissing))) return;
            if (_reportedFontFailure) return;
            _reportedFontFailure = true;
            Debug.LogError($"MANAGEMENT_UI_MISSING_GLYPH: {missing}");
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void ClearLayoutGroups(RectTransform parent)
        {
            foreach (var group in parent.GetComponents<HorizontalOrVerticalLayoutGroup>())
            {
                group.enabled = false;
                Destroy(group);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private static string ScheduleShortLabel(FamilyScheduleKind kind)
        {
            switch (kind)
            {
                case FamilyScheduleKind.School: return "학교";
                case FamilyScheduleKind.OutsideSales: return "영업";
                case FamilyScheduleKind.HouseholdDuty: return "가사";
                case FamilyScheduleKind.OutsideCommitment: return "외출";
                case FamilyScheduleKind.Sleep: return "수면";
                default: return "자리 비움";
            }
        }

        private static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString($"#{rgb}", out var color) ? color : Color.magenta;
        }
    }
}

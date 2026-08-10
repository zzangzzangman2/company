using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeSeatPlacementPanel : MonoBehaviour
    {
        public const string SkinAssetPath =
            "Assets/Art/UI/Resources/OfficeSeating/office_seat_assignment_panel_v1.png";
        public const string SkinResourcePath =
            "OfficeSeating/office_seat_assignment_panel_v1";

        private static readonly Color Overlay = new Color(0.05f, 0.10f, 0.14f, 0.52f);
        private static readonly Color FallbackSurface = new Color(0.96f, 0.99f, 0.95f, 1f);
        private static readonly Color SkinReadability = new Color(1f, 1f, 1f, 0.76f);
        private static readonly Color Mint = new Color(0.17f, 0.65f, 0.57f, 1f);
        private static readonly Color Peach = new Color(0.98f, 0.65f, 0.49f, 1f);
        private static readonly Color Ink = new Color(0.08f, 0.16f, 0.19f, 1f);
        private static readonly Color Muted = new Color(0.31f, 0.42f, 0.43f, 1f);
        private static readonly Color Line = new Color(0.67f, 0.78f, 0.75f, 1f);
        private static readonly Color Warning = new Color(0.78f, 0.32f, 0.24f, 1f);

        [SerializeField] private OfficeSeatClickController clickController;

        private readonly List<OfficeSeatPlacementMemberOption> _members =
            new List<OfficeSeatPlacementMemberOption>();
        private readonly OfficeSeatPlacementSession _session = new OfficeSeatPlacementSession();
        private OfficeSeatPlacementActions _actions;
        private OfficeSeatView _seatView;
        private Texture2D _skin;
        private Vector2 _memberScroll;
        private int _selectedMemberIndex;
        private string _message = string.Empty;
        private bool _subscribed;
        private float _styleScale = -1f;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _warningStyle;

        public bool IsOpen => _session.IsOpen;
        public bool IsImageGenSkinReady => _skin != null;
        public bool RequiresImageGenSkinForFinalQa => _skin == null;
        public string SelectedSeatId => _session.Selection?.SeatId ?? string.Empty;
        public string Message => _message;

        public void Configure(
            OfficeSeatingState seatingState,
            IEnumerable<OfficeSeatPlacementMemberOption> memberOptions = null)
        {
            _actions = seatingState == null ? null : new OfficeSeatPlacementActions(seatingState);
            SetMembers(memberOptions ?? CreateDefaultFamilyMembers());
            RefreshSeatView();
        }

        public void ResetOfficeSeatingRuntime()
        {
            _actions = null;
            Close();
        }

        public void BindClickController(OfficeSeatClickController controller)
        {
            DetachController();
            clickController = controller;
            AttachController();
        }

        public void ReloadImageGenSkin()
        {
            _skin = Resources.Load<Texture2D>(SkinResourcePath);
        }

        public void OpenForSeat(OfficeSeatSelection selection)
        {
            if (selection == null) return;
            EnsureMembers();
            _session.Open(selection);
            _message = string.Empty;
            _memberScroll = Vector2.zero;
            RefreshSeatView();
            SelectAssignedMemberIfPresent();
        }

        public void Close()
        {
            _session.Close();
            _seatView = null;
            _message = string.Empty;
        }

        public bool SelectMember(string memberId)
        {
            if (string.IsNullOrWhiteSpace(memberId)) return false;
            var normalized = memberId.Trim();
            for (var index = 0; index < _members.Count; index++)
            {
                if (!string.Equals(_members[index].MemberId, normalized, StringComparison.Ordinal)) continue;
                _selectedMemberIndex = index;
                return true;
            }
            return false;
        }

        public OfficeSeatPlacementActionResult TryAssignSelected()
        {
            if (_actions == null || _session.Selection == null || _members.Count == 0)
            {
                var unavailable = new OfficeSeatPlacementActionResult(
                    false,
                    false,
                    OfficeSeatOperationFailure.UnknownSeat,
                    "좌석 규칙이 아직 연결되지 않았습니다.");
                _message = unavailable.KoreanMessage;
                return unavailable;
            }

            var result = _actions.TryAssign(
                _session.Selection.SeatId,
                _members[Mathf.Clamp(_selectedMemberIndex, 0, _members.Count - 1)].MemberId);
            _message = result.KoreanMessage;
            RefreshSeatView();
            return result;
        }

        public OfficeSeatPlacementActionResult TryUnassignCurrent()
        {
            if (_actions == null || _session.Selection == null)
            {
                var unavailable = new OfficeSeatPlacementActionResult(
                    false,
                    false,
                    OfficeSeatOperationFailure.UnknownSeat,
                    "좌석 규칙이 아직 연결되지 않았습니다.");
                _message = unavailable.KoreanMessage;
                return unavailable;
            }

            var result = _actions.TryUnassign(_session.Selection.SeatId);
            _message = result.KoreanMessage;
            RefreshSeatView();
            return result;
        }

        private void Awake()
        {
            EnsureMembers();
            ReloadImageGenSkin();
        }

        private void OnEnable()
        {
            EnsureMembers();
            if (_skin == null) ReloadImageGenSkin();
            AttachController();
        }

        private void OnDisable()
        {
            DetachController();
            Close();
        }

        private void Update()
        {
            if (_session.HandleEscape(Input.GetKeyDown(KeyCode.Escape)))
            {
                _seatView = null;
                _message = string.Empty;
            }
        }

        private void OnGUI()
        {
            if (!_session.IsOpen) return;
            var current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                Close();
                current.Use();
                return;
            }

            var layout = OfficeSeatingUiLayout.Calculate(Screen.width, Screen.height);
            EnsureStyles(layout.Scale);
            GUI.depth = -240;
            DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), Overlay);
            DrawPanelSurface(ToRect(layout.Panel));
            DrawHeader(layout);
            DrawSeatSummary(layout);
            DrawMemberList(layout);
            DrawMessage(layout);
            DrawActions(layout);
            ConsumeModalInput();
        }

        private void DrawPanelSurface(Rect rect)
        {
            if (_skin != null)
            {
                GUI.DrawTexture(rect, _skin, ScaleMode.ScaleAndCrop, false);
                DrawSolid(rect, SkinReadability);
            }
            else
            {
                DrawSolid(rect, FallbackSurface);
                DrawSolid(new Rect(rect.x, rect.y, 8f, rect.height), Mint);
                DrawSolid(new Rect(rect.x + 8f, rect.y, rect.width - 8f, 12f), Peach);
            }
            DrawOutline(rect, Line, 2f);
        }

        private void DrawHeader(OfficeSeatPlacementLayout layout)
        {
            var rect = ToRect(layout.Title);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height * 0.56f), "좌석 배치", _titleStyle);
            GUI.Label(
                new Rect(rect.x, rect.y + rect.height * 0.52f, rect.width, rect.height * 0.42f),
                "자리를 누르고 구성원을 선택하세요.",
                _smallStyle);
            if (_skin == null)
            {
                var badge = new Rect(rect.x + rect.width * 0.57f, rect.y + 2f, rect.width * 0.43f, rect.height * 0.42f);
                DrawSolid(badge, new Color(1f, 0.88f, 0.72f, 0.96f));
                GUI.Label(badge, "최종 ImageGen 스킨 대기", _warningStyle);
            }
        }

        private void DrawSeatSummary(OfficeSeatPlacementLayout layout)
        {
            var rect = ToRect(layout.SeatSummary);
            DrawSolid(rect, new Color(1f, 1f, 1f, 0.84f));
            DrawOutline(rect, Line, 1f);
            var selection = _session.Selection;
            GUI.Label(
                new Rect(rect.x + 14f * layout.Scale, rect.y + 10f * layout.Scale, rect.width - 28f * layout.Scale, 34f * layout.Scale),
                selection == null ? "선택 좌석 없음" : selection.DisplayName,
                _headingStyle);

            var stateLabel = _seatView == null ? "연결 대기" : OfficeSeatKoreanText.State(_seatView.State);
            var assigned = _seatView == null || string.IsNullOrEmpty(_seatView.AssignedMemberId)
                ? "없음"
                : MemberDisplayName(_seatView.AssignedMemberId);
            GUI.Label(
                new Rect(rect.x + 14f * layout.Scale, rect.y + 53f * layout.Scale, rect.width - 28f * layout.Scale, 46f * layout.Scale),
                "상태  " + stateLabel + "    장기 배정  " + assigned,
                _bodyStyle);
        }

        private void DrawMemberList(OfficeSeatPlacementLayout layout)
        {
            var rect = ToRect(layout.MemberList);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 32f * layout.Scale), "구성원 선택", _headingStyle);
            var viewport = new Rect(rect.x, rect.y + 40f * layout.Scale, rect.width, rect.height - 40f * layout.Scale);
            var itemHeight = 52f * layout.Scale;
            var contentHeight = Mathf.Max(viewport.height, _members.Count * itemHeight);
            _memberScroll = GUI.BeginScrollView(
                viewport,
                _memberScroll,
                new Rect(0f, 0f, viewport.width - 18f * layout.Scale, contentHeight));
            try
            {
                for (var index = 0; index < _members.Count; index++)
                {
                    var member = _members[index];
                    var selected = index == _selectedMemberIndex;
                    var buttonRect = new Rect(0f, index * itemHeight, viewport.width - 24f * layout.Scale, 42f * layout.Scale);
                    if (GUI.Button(buttonRect, member.DisplayName, selected ? _selectedButtonStyle : _buttonStyle))
                        _selectedMemberIndex = index;
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private void DrawMessage(OfficeSeatPlacementLayout layout)
        {
            var rect = ToRect(layout.Message);
            var canChange = OfficeSeatPlacementActions.CanChangeAssignment(_seatView);
            var message = _message;
            if (!canChange && _seatView != null)
                message = "예약 또는 사용 중에는 좌석 배정을 바꿀 수 없습니다.";
            if (string.IsNullOrEmpty(message)) message = "장기 배정은 저장되며, 이동 예약과 점유는 임시 상태입니다.";
            GUI.Label(rect, message, canChange ? _smallStyle : _warningStyle);
        }

        private void DrawActions(OfficeSeatPlacementLayout layout)
        {
            var rect = ToRect(layout.Actions);
            var gap = 10f * layout.Scale;
            var buttonWidth = (rect.width - gap * 2f) / 3f;
            var previousEnabled = GUI.enabled;
            try
            {
                var canChange = OfficeSeatPlacementActions.CanChangeAssignment(_seatView);
                GUI.enabled = canChange && _members.Count > 0;
                if (GUI.Button(new Rect(rect.x, rect.y, buttonWidth, rect.height), "배정", _buttonStyle))
                    TryAssignSelected();

                GUI.enabled = canChange && _seatView != null && !string.IsNullOrEmpty(_seatView.AssignedMemberId);
                if (GUI.Button(new Rect(rect.x + buttonWidth + gap, rect.y, buttonWidth, rect.height), "해제", _buttonStyle))
                    TryUnassignCurrent();

                GUI.enabled = true;
                if (GUI.Button(new Rect(rect.x + (buttonWidth + gap) * 2f, rect.y, buttonWidth, rect.height), "취소", _buttonStyle))
                    Close();
            }
            finally
            {
                GUI.enabled = previousEnabled;
            }
        }

        private void RefreshSeatView()
        {
            _seatView = null;
            if (_actions == null || _session.Selection == null) return;
            _actions.TryGetSeat(_session.Selection.SeatId, out _seatView);
        }

        private void SelectAssignedMemberIfPresent()
        {
            if (_seatView == null || string.IsNullOrEmpty(_seatView.AssignedMemberId)) return;
            SelectMember(_seatView.AssignedMemberId);
        }

        private void SetMembers(IEnumerable<OfficeSeatPlacementMemberOption> memberOptions)
        {
            if (memberOptions == null) throw new ArgumentNullException(nameof(memberOptions));
            var normalized = new List<OfficeSeatPlacementMemberOption>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in memberOptions)
            {
                if (option == null) throw new ArgumentException("Member options cannot contain null.", nameof(memberOptions));
                if (!seen.Add(option.MemberId))
                    throw new ArgumentException("Duplicate member option: " + option.MemberId, nameof(memberOptions));
                normalized.Add(option);
            }
            if (normalized.Count == 0) throw new ArgumentException("At least one member option is required.", nameof(memberOptions));
            _members.Clear();
            _members.AddRange(normalized);
            _selectedMemberIndex = Mathf.Clamp(_selectedMemberIndex, 0, _members.Count - 1);
        }

        private void EnsureMembers()
        {
            if (_members.Count == 0) SetMembers(CreateDefaultFamilyMembers());
        }

        private static IEnumerable<OfficeSeatPlacementMemberOption> CreateDefaultFamilyMembers()
        {
            return new[]
            {
                new OfficeSeatPlacementMemberOption("player", "나 (플레이어)"),
                new OfficeSeatPlacementMemberOption("older_sister", "누나"),
                new OfficeSeatPlacementMemberOption("father", "아빠"),
                new OfficeSeatPlacementMemberOption("mother", "엄마")
            };
        }

        private string MemberDisplayName(string memberId)
        {
            for (var index = 0; index < _members.Count; index++)
            {
                if (string.Equals(_members[index].MemberId, memberId, StringComparison.Ordinal))
                    return _members[index].DisplayName;
            }
            return memberId;
        }

        private void AttachController()
        {
            if (_subscribed || clickController == null || !isActiveAndEnabled) return;
            clickController.SeatSelected += OpenForSeat;
            _subscribed = true;
        }

        private void DetachController()
        {
            if (!_subscribed || clickController == null) return;
            clickController.SeatSelected -= OpenForSeat;
            _subscribed = false;
        }

        private void EnsureStyles(float scale)
        {
            if (_titleStyle != null && Mathf.Abs(_styleScale - scale) < 0.001f) return;
            _styleScale = scale;
            _titleStyle = LabelStyle(Mathf.RoundToInt(34f * scale), FontStyle.Bold, Ink, TextAnchor.UpperLeft);
            _headingStyle = LabelStyle(Mathf.RoundToInt(24f * scale), FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
            _bodyStyle = LabelStyle(Mathf.RoundToInt(19f * scale), FontStyle.Normal, Ink, TextAnchor.MiddleLeft);
            _smallStyle = LabelStyle(Mathf.RoundToInt(17f * scale), FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            _warningStyle = LabelStyle(Mathf.RoundToInt(16f * scale), FontStyle.Bold, Warning, TextAnchor.MiddleLeft);
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(19f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink },
                hover = { textColor = Ink },
                active = { textColor = Ink }
            };
            _selectedButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = Mint },
                hover = { textColor = Mint },
                active = { textColor = Mint }
            };
        }

        private static GUIStyle LabelStyle(int fontSize, FontStyle fontStyle, Color color, TextAnchor anchor)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(11, fontSize),
                fontStyle = fontStyle,
                alignment = anchor,
                wordWrap = true,
                normal = { textColor = color }
            };
        }

        private static Rect ToRect(OfficeSeatingUiRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = previousColor;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

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
    }
}

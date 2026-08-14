using System;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.ContractGrowth;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.ContractGrowth
{
    /// <summary>Isolated, opt-in click presenter for QA builds. It is never attached to the shared scene automatically.</summary>
    [DisallowMultipleComponent]
    public sealed class ContractGrowthQaPresenter : MonoBehaviour
    {
        private const float LogicalWidth = 1920f;
        private const float LogicalHeight = 1080f;
        [SerializeField] private ContractBusinessRuntimeAdapter _adapter;
        private Vector2 _scroll;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _card;

        public void Configure(ContractBusinessRuntimeAdapter adapter)
        {
            _adapter = adapter != null ? adapter : throw new ArgumentNullException(nameof(adapter));
        }

        private void OnGUI()
        {
            if (_adapter == null || !_adapter.IsReady) return;
            EnsureStyles();
            var scale = Mathf.Min(Screen.width / LogicalWidth, Screen.height / LogicalHeight);
            var offsetX = (Screen.width - LogicalWidth * scale) * 0.5f;
            var offsetY = (Screen.height - LogicalHeight * scale) * 0.5f;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.Box(new Rect(0f, 0f, LogicalWidth, LogicalHeight), GUIContent.none);
            DrawHeader();
            switch (_adapter.CurrentRoute)
            {
                case ContractBusinessRoute.OfficeWorld: DrawOfficeHook(); break;
                case ContractBusinessRoute.BusinessHub: DrawHub(); break;
                case ContractBusinessRoute.ContractBoard: DrawBoard(); break;
                case ContractBusinessRoute.ProductOpportunities: DrawProducts(); break;
            }
            GUI.matrix = previous;
        }

        private void DrawHeader()
        {
            GUI.Label(new Rect(64f, 30f, 1100f, 55f), "가족회사 사업 성장 QA", _title);
            GUI.Label(new Rect(64f, 84f, 1300f, 38f), $"경로: {_adapter.CurrentRoute} · 날짜 고정 제안 · 자동 수락 없음", _body);
            if (_adapter.CurrentRoute != ContractBusinessRoute.OfficeWorld && GUI.Button(new Rect(1650f, 38f, 200f, 56f), "뒤로")) _adapter.TryBack();
        }

        private void DrawOfficeHook()
        {
            GUI.Label(new Rect(120f, 250f, 1680f, 100f), "사무실 월드의 하단 ‘사업’ 버튼이 호출할 공개 진입점입니다.", _title);
            if (GUI.Button(new Rect(700f, 430f, 520f, 110f), _adapter.HasBusinessBadge ? "사업  •  새 제안 3" : "사업"))
                _adapter.OpenBusinessHub();
        }

        private void DrawHub()
        {
            var hub = _adapter.GetHubViewModel();
            GUI.Label(new Rect(80f, 150f, 1760f, 55f), $"{hub.StageKo}  ·  {hub.CashKo}  ·  {hub.ReputationKo}", _title);
            GUI.Label(new Rect(80f, 215f, 1760f, 48f), hub.NotificationKo, _body);
            if (GUI.Button(new Rect(120f, 330f, 780f, 260f), hub.ShowFirstContractBadge ? "하청 계약\n첫 계약 제안 3건" : "하청 계약\n실적 기반 고객 게시판"))
                _adapter.OpenContractBoard();
            if (GUI.Button(new Rect(1020f, 330f, 780f, 260f), "자체 제품\n현금·연구·하청 경험 해금 조건"))
                _adapter.OpenProductOpportunities();
            GUI.Label(new Rect(120f, 660f, 1680f, 120f), $"현재 최고 고객 단계: {ContractRewardBalanceRules.TierLabel(hub.HighestUnlockedTier)}", _title);
        }

        private void DrawBoard()
        {
            var board = _adapter.GetBoardViewModel();
            GUI.Label(new Rect(70f, 140f, 1780f, 50f), board.HeadingKo, _title);
            GUI.Label(new Rect(70f, 195f, 1780f, 45f), board.GuidanceKo, _body);
            _scroll = GUI.BeginScrollView(new Rect(55f, 260f, 1810f, 730f), _scroll, new Rect(0f, 0f, 1770f, board.Cards.Count <= 3 ? 690f : 1340f));
            for (var index = 0; index < board.Cards.Count; index++)
            {
                var card = board.Cards[index];
                var column = index % 3;
                var row = index / 3;
                var rect = new Rect(column * 590f, row * 650f, 560f, 610f);
                GUI.Box(rect, GUIContent.none, _card);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 22f, 512f, 42f), card.TierKo, _body);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 70f, 512f, 48f), card.ClientNameKo, _title);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 128f, 512f, 82f), card.TitleKo, _body);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 225f, 512f, 210f),
                    $"{card.RewardKo}\n{card.DeadlineKo}\n{card.WorkKo}\n{card.CapabilityKo}\n{card.RiskKo}\n{card.ReputationKo}", _body);
                var assignable = string.Join(", ", card.MemberChoices.Where(item => item.Available).Select(item => item.DisplayName));
                GUI.Label(new Rect(rect.x + 24f, rect.y + 448f, 512f, 46f), $"지금 배정 가능: {(string.IsNullOrEmpty(assignable) ? "없음" : assignable)}", _body);
                if (GUI.Button(new Rect(rect.x + 24f, rect.y + 518f, 512f, 64f), "이 계약 수락"))
                    _adapter.TryAcceptOffer(card.OfferId);
            }
            GUI.EndScrollView();
        }

        private void DrawProducts()
        {
            var opportunities = _adapter.GetProductOpportunities();
            GUI.Label(new Rect(70f, 140f, 1780f, 55f), "자체 제품 성장 경로", _title);
            GUI.Label(new Rect(70f, 198f, 1780f, 45f), "기존 자체 사업·연구·제품 시스템의 실제 조건입니다. 미충족 조건은 진행률로 표시합니다.", _body);
            for (var index = 0; index < opportunities.Count; index++)
            {
                var item = opportunities[index];
                var column = index % 2;
                var row = index / 2;
                var rect = new Rect(90f + column * 885f, 285f + row * 350f, 835f, 310f);
                GUI.Box(rect, GUIContent.none, _card);
                GUI.Label(new Rect(rect.x + 25f, rect.y + 20f, 780f, 45f), item.Definition.DisplayNameKo, _title);
                GUI.Label(new Rect(rect.x + 25f, rect.y + 77f, 780f, 180f),
                    $"진행 {item.ProgressBasisPoints / 100}% · {(item.Unlocked ? "해금 조건 충족" : "조건 축적 중")}\n" +
                    string.Join("\n", item.ConditionLabels.Take(4)) + $"\n수익 구조: {item.Definition.RevenueModelKo}", _body);
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = new Color(0.12f, 0.15f, 0.2f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true, normal = { textColor = new Color(0.16f, 0.19f, 0.24f) } };
            _card = new GUIStyle(GUI.skin.box) { normal = { background = Texture2D.whiteTexture } };
        }
    }
}

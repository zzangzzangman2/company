using System;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        private GameState _state;
        private SimulationRunner _runner;
        private ISaveRepository _saveRepository;
        private string _notice = "Prototype 0.1";
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private OfficeContractTaskCoordinator _contractTaskCoordinator;

        public GameState State => _state;

        private void Awake()
        {
            InitializeNow();
        }

        private void Start()
        {
            InitializeOfficeTaskBridgeNow();
        }

        public void InitializeNow()
        {
            if (_saveRepository == null) _saveRepository = new UnityJsonSaveRepository();
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

        private void ResetState()
        {
            _state = PrototypeStateFactory.Create();
            _runner = new SimulationRunner(_state);
            _contractTaskCoordinator?.ResetAssignments();
            _notice = "새 게임 시작";
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(18, 18, 390, 590), GUI.skin.box);
            GUILayout.Label("가족회사 Prototype 0.1", _titleStyle);
            GUILayout.Label(_state.Time.Now.ToString("yyyy-MM-dd ddd HH:mm"), _bodyStyle);
            GUILayout.Label($"회사 현금  {_state.Company.CashWon:N0}원", _bodyStyle);
            GUILayout.Label($"평판  {_state.Company.Reputation}", _bodyStyle);
            GUILayout.Space(8);
            foreach (var member in _state.Family.Members)
            {
                GUILayout.Label($"{member.DisplayName}  {member.AgeAt(_state.Time)}살  체력 {member.Energy}  신뢰 {member.Trust}  스트레스 {member.Stress}", _bodyStyle);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("1시간 보내기", GUILayout.Height(32)))
            {
                var events = _runner.AdvanceMinutes(60);
                _notice = events.Count > 0 ? $"{events.Count}개 이벤트 처리" : "1시간 경과";
            }

            if (GUILayout.Button("하루 보내기", GUILayout.Height(32)))
            {
                _runner.AdvanceMinutes(1440);
                _notice = "하루 경과: 가족 체력과 스트레스 반영";
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장", GUILayout.Height(30)))
            {
                try
                {
                    _saveRepository.Save(GameSaveMapper.ToDto(_state));
                    _notice = "저장 완료";
                }
                catch (Exception exception)
                {
                    _notice = $"저장 실패: {exception.Message}";
                }
            }

            if (GUILayout.Button("불러오기", GUILayout.Height(30)))
            {
                try
                {
                    if (_saveRepository.TryLoad(out var save))
                    {
                        _state = GameSaveMapper.FromDto(save);
                        _runner = new SimulationRunner(_state);
                        _contractTaskCoordinator?.ResetAssignments();
                        _notice = "불러오기 완료";
                    }
                    else
                    {
                        _notice = "저장 파일 없음";
                    }
                }
                catch (Exception exception)
                {
                    _notice = $"불러오기 실패: {exception.Message}";
                }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("새 게임", GUILayout.Height(28))) ResetState();
            GUILayout.Space(8);
            GUILayout.Label(_notice, _bodyStyle);
            GUILayout.Label("이동: WASD / 방향키", _bodyStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
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

using System;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Workforce;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class PlayerOfficeWorkInteractor : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private OfficeWaypoint[] waypoints = Array.Empty<OfficeWaypoint>();
        [SerializeField] private float interactionRadius = 1.15f;
        [SerializeField] private KeyCode workKey = KeyCode.E;
        private float _workProgress;
        private string _workingOfferId = string.Empty;
        private OfficeWaypoint _workingWaypoint;
        private long _creditedThroughMinute;
        private int _requiredGameMinutesPerPersonHour = 60;
        private readonly OfficePlayerWorkGate _workGate = new OfficePlayerWorkGate();

        public bool IsWorking => _workGate.HasActiveWork;
        public OfficeActivity CurrentActivity => _workGate.CurrentActivity;
        public bool WantsOfficeSeat => _workGate.WantsOfficeSeat;
        public bool IsSeatedWorkReady => _workGate.IsSeatedWorkReady;
        public bool IsSeatingTransitionBlocked => _workGate.IsTransitionBlocked;
        public float WorkProgress01 => Mathf.Clamp01(_workProgress / Mathf.Max(1f, _requiredGameMinutesPerPersonHour));

        public void SetSeatedWorkGateRequired(bool required)
        {
            _workGate.SetSeatedWorkGateRequired(required);
        }

        public void SetSeatedWorkReady(bool ready)
        {
            _workGate.SetSeatedWorkReady(ready);
        }

        public void SetSeatingTransitionBlocked(bool blocked)
        {
            _workGate.SetTransitionBlocked(blocked);
        }

        public void Configure(PrototypeBootstrap newBootstrap, OfficeWaypoint[] newWaypoints)
        {
            bootstrap = newBootstrap;
            waypoints = newWaypoints ?? Array.Empty<OfficeWaypoint>();
            ResetWork();
        }

        private void Update()
        {
            if (bootstrap == null || bootstrap.State == null || bootstrap.UiScreen != PrototypeUiScreen.Playing)
            {
                ResetWork();
                return;
            }

            var contract = bootstrap.State.Contracts.Contracts
                .Where(item => item.Status == SubcontractStatus.Active)
                .OrderBy(item => item.DueMinute)
                .ThenBy(item => item.Offer.OfferId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (contract == null)
            {
                if (Input.GetKeyDown(workKey)) bootstrap.SetWorldNotice("진행 중인 계약이 없습니다.");
                ResetWork();
                return;
            }

            var player = bootstrap.State.Family.Get("player");
            var schedule = FamilyScheduleRules.Resolve(player.Role, bootstrap.State.Time.Now);
            if (!schedule.CanPerformCompanyWork)
            {
                if (Input.GetKeyDown(workKey))
                    bootstrap.SetWorldNotice($"지금은 {schedule.Label} 중이라 회사 작업을 할 수 없습니다.");
                ResetWork();
                return;
            }

            var requiredActivity = ResolveActivity(contract);
            var waypoint = FindNearbyWaypoint(requiredActivity);
            if (waypoint == null)
            {
                if (Input.GetKeyDown(workKey))
                    bootstrap.SetWorldNotice($"{ActivityPlaceLabel(requiredActivity)} 가까이 가서 E를 누르면 직접 작업합니다.");
                ResetWork();
                return;
            }

            if (!Input.GetKey(workKey))
            {
                ResetWork();
                return;
            }

            if (_workingOfferId != contract.Offer.OfferId || _workingWaypoint != waypoint)
            {
                _workingOfferId = contract.Offer.OfferId;
                _workingWaypoint = waypoint;
                _workProgress = 0f;
                _creditedThroughMinute = bootstrap.State.Time.ElapsedMinutes;
                _requiredGameMinutesPerPersonHour = RequiredGameMinutesPerPersonHour(contract, player);
                _workGate.Begin(requiredActivity);
                bootstrap.SetWorldNotice($"{contract.Offer.Title} 직접 작업 중 · E를 계속 누르세요.");
            }

            if (!_workGate.CanAccumulateProgress) return;
            var currentMinute = bootstrap.State.Time.ElapsedMinutes;
            _workProgress = Math.Max(0L, currentMinute - _creditedThroughMinute);
            while (_workProgress >= _requiredGameMinutesPerPersonHour)
            {
                var creditMinute = checked(_creditedThroughMinute + _requiredGameMinutesPerPersonHour);
                var result = bootstrap.State.Contracts.RecordWork(
                    contract.Offer.OfferId,
                    "player",
                    1,
                    creditMinute,
                    bootstrap.State.Family,
                    bootstrap.State.Company);
                if (!result.Applied)
                {
                    bootstrap.SetWorldNotice(WorkFailureLabel(result.RejectionReason));
                    if (Application.isPlaying) GameAudioCoordinator.Instance.PlayUiSfx(GameUiSfx.Error);
                    ResetWork();
                    return;
                }

                _creditedThroughMinute = creditMinute;
                bootstrap.SetWorldNotice(result.Completed
                    ? $"직접 작업으로 계약 완료 · 보상 {result.RewardWon:N0}원"
                    : $"직접 작업 1시간 반영 · 남은 작업 {contract.RemainingPersonHours}시간");
                if (Application.isPlaying)
                {
                    if (result.Completed) GameAudioCoordinator.Instance.PlayCoinsSfx(result.RewardWon >= 500_000);
                    else GameAudioCoordinator.Instance.PlayPaperSfx(GamePaperSfx.Rustle);
                }
                if (result.Completed)
                {
                    ResetWork();
                    return;
                }
                _requiredGameMinutesPerPersonHour = RequiredGameMinutesPerPersonHour(contract, player);
                _workProgress = Math.Max(0L, currentMinute - _creditedThroughMinute);
            }
        }

        private OfficeWaypoint FindNearbyWaypoint(OfficeActivity requiredActivity)
        {
            return waypoints
                .Where(item => item != null && item.Activity == requiredActivity)
                .Select(item => new
                {
                    Waypoint = item,
                    Distance = FlatDistance(transform.position, item.transform.position)
                })
                .Where(item => item.Distance <= interactionRadius)
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Waypoint.WaypointId, StringComparer.Ordinal)
                .Select(item => item.Waypoint)
                .FirstOrDefault();
        }

        private void ResetWork()
        {
            _workProgress = 0f;
            _workingOfferId = string.Empty;
            _workingWaypoint = null;
            _creditedThroughMinute = 0L;
            _requiredGameMinutesPerPersonHour = 60;
            _workGate.End();
        }

        private static int RequiredGameMinutesPerPersonHour(SubcontractState contract, FamilyMemberState member)
        {
            return ContractPortfolio.MinutesPerPersonHour(contract, member);
        }

        private static float FlatDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static OfficeActivity ResolveActivity(SubcontractState contract)
        {
            if (ContractPortfolio.UsesOnlyDesk(contract)) return OfficeActivity.Work;
            if (contract.CompletedPersonHours == 0 && contract.Offer.RequiredWorkers > 1)
                return OfficeActivity.Meeting;
            return contract.RemainingPersonHours <= 4
                ? OfficeActivity.Printing
                : OfficeActivity.Work;
        }

        private static string ActivityPlaceLabel(OfficeActivity activity)
        {
            switch (activity)
            {
                case OfficeActivity.Meeting: return "회의실";
                case OfficeActivity.Printing: return "프린터";
                default: return "업무 책상";
            }
        }

        private static string WorkFailureLabel(ContractWorkRejectionReason reason)
        {
            switch (reason)
            {
                case ContractWorkRejectionReason.MemberUnavailable: return "지금은 학교나 외부 일정 때문에 작업할 수 없습니다.";
                case ContractWorkRejectionReason.MemberEnergyInsufficient: return "체력이 부족합니다. 먼저 쉬어야 합니다.";
                case ContractWorkRejectionReason.DeadlinePassed: return "작업하는 사이 계약 마감을 넘겼습니다.";
                case ContractWorkRejectionReason.AuthoritativeTimeInsufficient: return "게임 시간이 아직 1인시만큼 지나지 않았습니다.";
                default: return "직접 작업을 반영하지 못했습니다.";
            }
        }
    }
}

using System;
using System.Linq;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Workforce;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OfficeRuntimeAgent))]
    public sealed class OfficeRuntimePlayerController : MonoBehaviour
    {
        [SerializeField] private KeyCode workKey = KeyCode.E;
        private PrototypeBootstrap _bootstrap;
        private OfficeRuntimeAgent _actor;
        private float _workProgress;
        private string _workingOfferId = string.Empty;
        private string _selectedOfferId = string.Empty;
        private long _creditedThroughMinute;
        private int _requiredGameMinutesPerPersonHour = 60;

        public bool IsWorking => _workingOfferId.Length > 0;
        public void SelectContract(string offerId)
        {
            ResetWork(true);
            _selectedOfferId = offerId ?? string.Empty;
        }
        public float WorkProgress01 => Mathf.Clamp01(_workProgress / Mathf.Max(1f, _requiredGameMinutesPerPersonHour));

        public void Configure(PrototypeBootstrap bootstrap, OfficeRuntimeAgent actor)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            ResetWork(false);
        }

        private void Update()
        {
            if (_bootstrap == null || _actor == null ||
                _bootstrap.State == null || _bootstrap.UiScreen != PrototypeUiScreen.Playing)
            {
                _actor?.SetPlayerInput(Vector2.zero);
                ResetWork(true);
                return;
            }

            var input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            _actor.SetPlayerInput(Vector2.ClampMagnitude(input, 1f));

            SubcontractState contract = _bootstrap.State.Contracts.Contracts
                .Where(item => item.Status == SubcontractStatus.Active)
                .OrderByDescending(item => item.Offer.OfferId == _selectedOfferId)
                .ThenBy(item => item.DueMinute)
                .ThenBy(item => item.Offer.OfferId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (contract == null)
            {
                if (Input.GetKeyDown(workKey)) _bootstrap.SetWorldNotice("진행 중인 계약이 없습니다.");
                ResetWork(true);
                return;
            }

            FamilyMemberState player = _bootstrap.State.Family.Get("player");
            FamilyScheduleSlot schedule = FamilyScheduleRules.Resolve(player.Role, _bootstrap.State.Time.Now);
            if (!schedule.CanPerformCompanyWork)
            {
                if (Input.GetKeyDown(workKey))
                    _bootstrap.SetWorldNotice($"지금은 {schedule.Label} 중이라 회사 작업을 할 수 없습니다.");
                ResetWork(true);
                return;
            }

            if (!Input.GetKey(workKey))
            {
                ResetWork(true);
                return;
            }

            OfficeActivity activity = ResolveActivity(contract);
            if (!_actor.TryBeginPlayerWork(activity))
            {
                if (Input.GetKeyDown(workKey))
                    _bootstrap.SetWorldNotice(ActivityPrompt(activity));
                ResetWork(false);
                return;
            }

            if (_workingOfferId != contract.Offer.OfferId)
            {
                _workingOfferId = contract.Offer.OfferId;
                _workProgress = 0f;
                _creditedThroughMinute = _bootstrap.State.Time.ElapsedMinutes;
                _requiredGameMinutesPerPersonHour = RequiredGameMinutesPerPersonHour(contract, player);
                _bootstrap.SetWorldNotice($"{contract.Offer.Title} 직접 작업 중 · E를 계속 누르세요.");
            }
            var currentMinute = _bootstrap.State.Time.ElapsedMinutes;
            _workProgress = Math.Max(0L, currentMinute - _creditedThroughMinute);
            while (_workProgress >= _requiredGameMinutesPerPersonHour)
            {
                var creditMinute = checked(_creditedThroughMinute + _requiredGameMinutesPerPersonHour);
                ContractWorkResult result = _bootstrap.State.Contracts.RecordWork(
                    contract.Offer.OfferId,
                    "player",
                    1,
                    creditMinute,
                    _bootstrap.State.Family,
                    _bootstrap.State.Company);
                if (!result.Applied)
                {
                    _bootstrap.SetWorldNotice("직접 작업을 반영하지 못했습니다.");
                    ResetWork(true);
                    return;
                }

                _creditedThroughMinute = creditMinute;
                _bootstrap.SetWorldNotice(result.Completed
                    ? contract.Offer.IsExternal
                        ? $"직접 작업으로 계약 완료 · {result.RewardWon:N0}원 · 기술 {result.TechnologyGains.Count}종 습득"
                        : "자체 제품 업무 완료 · 사업 → 자체 제품에서 다음 단계를 확인하세요."
                    : $"직접 작업 1시간 반영 · 남은 작업 {contract.RemainingPersonHours}시간");
                if (result.Completed)
                {
                    ResetWork(true);
                    return;
                }
                _requiredGameMinutesPerPersonHour = RequiredGameMinutesPerPersonHour(contract, player);
                _workProgress = Math.Max(0L, currentMinute - _creditedThroughMinute);
            }
        }

        private void ResetWork(bool leaveSeat)
        {
            bool releaseControllerOwnedSeat = leaveSeat && _workingOfferId.Length > 0;
            _workProgress = 0f;
            _workingOfferId = string.Empty;
            _creditedThroughMinute = 0L;
            _requiredGameMinutesPerPersonHour = 60;
            // Attendance and autonomy may seat the player independently of the hold-E contract
            // interaction. Only release a seat that this controller actually acquired for one of
            // its own manual work sessions.
            if (releaseControllerOwnedSeat) _actor?.EndPlayerWork();
        }

        private static int RequiredGameMinutesPerPersonHour(SubcontractState contract, FamilyMemberState member)
        {
            return ContractPortfolio.MinutesPerPersonHour(contract, member);
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

        private static string ActivityPrompt(OfficeActivity activity)
        {
            return activity switch
            {
                OfficeActivity.Meeting => "회의 탁자 가까이에서 E를 누르면 직접 작업합니다.",
                OfficeActivity.Printing => "팩스·복사기 가까이에서 E를 누르면 직접 작업합니다.",
                _ => "내 워크스테이션 접근 칸 가까이에서 E를 누르면 앉아서 작업합니다."
            };
        }
    }
}

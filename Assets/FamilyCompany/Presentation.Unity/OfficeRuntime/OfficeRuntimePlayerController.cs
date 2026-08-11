using System;
using System.Linq;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OfficeRuntimeAgent))]
    public sealed class OfficeRuntimePlayerController : MonoBehaviour
    {
        [SerializeField] private KeyCode workKey = KeyCode.E;
        [SerializeField] private float secondsPerPersonHour = 0.8f;
        private PrototypeBootstrap _bootstrap;
        private OfficeRuntimeAgent _actor;
        private float _workProgress;
        private string _workingOfferId = string.Empty;

        public bool IsWorking => _workProgress > 0f;
        public float WorkProgress01 => Mathf.Clamp01(_workProgress / Mathf.Max(0.05f, secondsPerPersonHour));

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
                .OrderBy(item => item.DueMinute)
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
                _workProgress = 0f;
                return;
            }

            if (_workingOfferId != contract.Offer.OfferId)
            {
                _workingOfferId = contract.Offer.OfferId;
                _workProgress = 0f;
                _bootstrap.SetWorldNotice($"{contract.Offer.Title} 직접 작업 중 · E를 계속 누르세요.");
            }
            _workProgress += Time.deltaTime;
            if (_workProgress < secondsPerPersonHour) return;
            _workProgress = 0f;
            ContractWorkResult result = _bootstrap.State.Contracts.RecordWork(
                contract.Offer.OfferId,
                "player",
                1,
                _bootstrap.State.Time.ElapsedMinutes,
                _bootstrap.State.Family,
                _bootstrap.State.Company);
            if (!result.Applied)
            {
                _bootstrap.SetWorldNotice("직접 작업을 반영하지 못했습니다.");
                ResetWork(true);
                return;
            }

            _bootstrap.SetWorldNotice(result.Completed
                ? $"직접 작업으로 계약 완료 · 보상 {result.RewardWon:N0}원"
                : $"직접 작업 1시간 반영 · 남은 작업 {contract.RemainingPersonHours}시간");
            if (result.Completed) ResetWork(true);
        }

        private void ResetWork(bool leaveSeat)
        {
            _workProgress = 0f;
            _workingOfferId = string.Empty;
            if (leaveSeat) _actor?.EndPlayerWork();
        }

        private static OfficeActivity ResolveActivity(SubcontractState contract)
        {
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

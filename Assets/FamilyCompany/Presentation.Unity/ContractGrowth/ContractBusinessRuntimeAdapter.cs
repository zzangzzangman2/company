using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Technology;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.ContractGrowth
{
    public sealed class ContractBusinessActionResult
    {
        public ContractBusinessActionResult(bool succeeded, string messageKo, string offerId = "")
        {
            Succeeded = succeeded;
            MessageKo = messageKo ?? string.Empty;
            OfferId = offerId ?? string.Empty;
        }
        public bool Succeeded { get; }
        public string MessageKo { get; }
        public string OfferId { get; }
    }

    /// <summary>
    /// Public boundary owned by the contract feature. Main HUD/Navigation may call this component
    /// without taking a dependency on offer generation, historical-company mapping or progression rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ContractBusinessRuntimeAdapter : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap _bootstrap;
        [SerializeField] private KoreaHistoryV1RuntimeCatalog _historyCatalog;
        [SerializeField] private BusinessIndustry _selectedIndustry = BusinessIndustry.WebAndSoftware;

        private ContractClientTierCatalog _clients;
        private DateTime _clientCatalogDate;
        private readonly ContractBusinessRouteStack _routes = new ContractBusinessRouteStack();
        private AuthoritativeContractWorkSession _playerWorkSession;

        public event Action<ContractBusinessRoute> RouteChanged;
        public event Action StateChanged;

        public ContractBusinessRoute CurrentRoute => _routes.Current;
        public BusinessIndustry SelectedIndustry => _selectedIndustry;
        public string NotificationKo { get; private set; } = string.Empty;
        public bool HasBusinessBadge => IsReady && _bootstrap.State.Contracts.Contracts.Count == 0;
        public bool IsReady => _bootstrap != null && _bootstrap.State != null && _historyCatalog != null;

        public void Configure(PrototypeBootstrap bootstrap, KoreaHistoryV1RuntimeCatalog historyCatalog)
        {
            _bootstrap = bootstrap != null ? bootstrap : throw new ArgumentNullException(nameof(bootstrap));
            _historyCatalog = historyCatalog != null ? historyCatalog : throw new ArgumentNullException(nameof(historyCatalog));
            _clients = null;
            _clientCatalogDate = default;
            EnsureReady();
        }

        public void SelectIndustry(BusinessIndustry industry)
        {
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            _selectedIndustry = industry;
            StateChanged?.Invoke();
        }

        public void OpenBusinessHub()
        {
            EnsureReady();
            if (_routes.Current == ContractBusinessRoute.OfficeWorld)
                _routes.Open(ContractBusinessRoute.BusinessHub);
            else
                _routes.Open(ContractBusinessRoute.BusinessHub);
            NotifyRoute();
        }

        public void OpenContractBoard()
        {
            EnsureReady();
            if (_routes.Current == ContractBusinessRoute.OfficeWorld)
                _routes.Open(ContractBusinessRoute.BusinessHub);
            _routes.Open(ContractBusinessRoute.ContractBoard);
            NotifyRoute();
        }

        public void OpenProductOpportunities()
        {
            EnsureReady();
            if (_routes.Current == ContractBusinessRoute.OfficeWorld)
                _routes.Open(ContractBusinessRoute.BusinessHub);
            _routes.Open(ContractBusinessRoute.ProductOpportunities);
            NotifyRoute();
        }

        public bool TryBack()
        {
            var changed = _routes.TryBack();
            if (changed) NotifyRoute();
            return changed;
        }

        public void ReturnToOffice()
        {
            _routes.ResetToOffice();
            NotifyRoute();
        }

        public BusinessHubViewModel GetHubViewModel()
        {
            EnsureReady();
            return ContractBusinessViewModelRules.CreateHub(_bootstrap.State, Clients, _selectedIndustry);
        }

        public ContractBoardViewModel GetBoardViewModel()
        {
            EnsureReady();
            return ContractBusinessViewModelRules.CreateBoard(_bootstrap.State, Clients, _selectedIndustry);
        }

        public IReadOnlyList<ProductOpportunityProgress> GetProductOpportunities()
        {
            return GetHubViewModel().ProductProgress;
        }

        public ContractBusinessActionResult TryAcceptOffer(string offerId)
        {
            EnsureReady();
            var definition = GetBoardViewModel().Snapshot.Offers.FirstOrDefault(item => item.Offer.OfferId == offerId);
            if (definition == null)
                return Fail("현재 게시판에 없는 제안입니다.", offerId);
            var result = _bootstrap.State.Contracts.Accept(
                definition.Offer,
                _bootstrap.State.Company,
                _bootstrap.State.Family,
                _bootstrap.State.Growth,
                _bootstrap.State.Time.ElapsedMinutes);
            if (!result.Accepted)
                return Fail(AcceptanceFailureKo(result.Decision.RejectionReason), offerId);
            NotificationKo = $"계약 수락 · {definition.Client.DisplayNameKo} / {definition.Offer.Title}";
            StateChanged?.Invoke();
            return new ContractBusinessActionResult(true, NotificationKo, offerId);
        }

        public ContractBusinessActionResult RequestFamilyAssignment(string offerId, string memberId)
        {
            EnsureReady();
            var contract = _bootstrap.State.Contracts.Contracts.FirstOrDefault(item => item.Offer.OfferId == offerId);
            if (contract == null || contract.Status != SubcontractStatus.Active)
                return Fail("진행 중인 계약이 아닙니다.", offerId);
            var member = _bootstrap.State.Family.Get(memberId);
            var schedule = FamilyScheduleRules.Resolve(member.Role, _bootstrap.State.Time.Now);
            if (!schedule.CanPerformCompanyWork)
                return Fail($"{member.DisplayName}은 지금 {schedule.Label} 중이라 배정할 수 없습니다.", offerId);
            if (member.Role == FamilyRole.Player)
            {
                _playerWorkSession = new AuthoritativeContractWorkSession(offerId, memberId, _bootstrap.State.Time.ElapsedMinutes);
                NotificationKo = "플레이어 작업 명령을 시작했습니다. GameTime이 흐를 때만 작업량이 생깁니다.";
                StateChanged?.Invoke();
                return new ContractBusinessActionResult(true, NotificationKo, offerId);
            }
            _bootstrap.AssignContractWorkNow(offerId, memberId);
            NotificationKo = $"{member.DisplayName}에게 계약 작업을 요청했습니다.";
            StateChanged?.Invoke();
            return new ContractBusinessActionResult(true, NotificationKo, offerId);
        }

        public AuthoritativeContractWorkAdvanceResult AdvancePlayerWorkFromGameTime()
        {
            EnsureReady();
            if (_playerWorkSession == null)
                return new AuthoritativeContractWorkAdvanceResult(0, 0, false, 0, ContractWorkRejectionReason.ContractNotActive);
            var result = _playerWorkSession.AdvanceTo(
                _bootstrap.State.Time.ElapsedMinutes,
                _bootstrap.State.Contracts,
                _bootstrap.State.Family,
                _bootstrap.State.Company);
            if (result.Completed)
            {
                // The two rewards are reported separately on purpose: cash settles into the company
                // account, technology settles into the company's know-how and is what opens own
                // products later.
                var gains = _bootstrap.State.Growth.Technology.ApplyGrants(result.TechnologyGrants);
                var money = ContractBusinessViewModelRules.Won(result.RewardWon);
                NotificationKo = gains.Count == 0
                    ? $"계약 완료 · {money} 정산 · 습득 기술 없음"
                    : $"계약 완료 · {money} 정산 · 기술 {DescribeGains(gains)}";
                _playerWorkSession = null;
            }
            if (result.AppliedHours > 0 || result.Completed) StateChanged?.Invoke();
            return result;
        }

        /// <summary>
        /// "DB 설계 +40pt (Lv2 달성) · 자료 입력 +15pt" — the level-up is called out because that is the
        /// moment a product requirement can flip to satisfied.
        /// </summary>
        private static string DescribeGains(IReadOnlyList<CompanyTechnologyGainRecord> gains)
        {
            return string.Join(" · ", gains.Select(item =>
            {
                var name = CompanyTechnologyCatalog.Get(item.TechnologyId).DisplayNameKo;
                var text = $"{name} +{item.PointsAdded}pt";
                return item.LeveledUp ? $"{text} (Lv{item.LevelAfter} 달성)" : text;
            }));
        }

        public void CancelPlayerWork()
        {
            _playerWorkSession = null;
        }

        private ContractClientTierCatalog Clients
        {
            get
            {
                var date = _bootstrap.State.Time.Now.Date;
                if (_clients != null && _clientCatalogDate == date) return _clients;
                _historyCatalog.InitializeNow();
                _clients = ContractClientTierCatalog.Create(_historyCatalog.Registry, date);
                _clientCatalogDate = date;
                return _clients;
            }
        }

        private void EnsureReady()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            if (_historyCatalog == null) _historyCatalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
            if (_bootstrap == null || _bootstrap.State == null)
                throw new InvalidOperationException("PrototypeBootstrap GameState is not ready.");
            if (_historyCatalog == null)
                throw new InvalidOperationException("Korea History V1 runtime catalog is not ready.");
        }

        private void NotifyRoute()
        {
            RouteChanged?.Invoke(_routes.Current);
            StateChanged?.Invoke();
        }

        private ContractBusinessActionResult Fail(string message, string offerId)
        {
            NotificationKo = message;
            StateChanged?.Invoke();
            return new ContractBusinessActionResult(false, message, offerId);
        }

        private static string AcceptanceFailureKo(ContractRejectionReason reason)
        {
            switch (reason)
            {
                case ContractRejectionReason.TooManyConcurrentContracts: return "동시에 진행할 수 있는 계약 수를 넘었습니다.";
                case ContractRejectionReason.ScheduleCapacityExceeded: return "가족 일정으로 납기 안에 끝낼 업무 시간이 부족합니다.";
                case ContractRejectionReason.UpfrontCashInsufficient: return "착수 비용을 낼 현금이 부족합니다.";
                case ContractRejectionReason.ReputationInsufficient: return "이 고객이 요구하는 평판에 아직 못 미칩니다.";
                case ContractRejectionReason.DevelopmentInsufficient: return "개발 역량이 부족합니다.";
                case ContractRejectionReason.SpeedInsufficient: return "작업 속도 역량이 부족합니다.";
                case ContractRejectionReason.RequiredTechnologyMissing: return "필요 연구가 아직 완료되지 않았습니다.";
                case ContractRejectionReason.DuplicateOffer: return "이미 수락한 계약입니다.";
                default: return "현재 회사 여건으로 이 계약을 수락할 수 없습니다.";
            }
        }
    }
}

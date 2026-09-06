using System;
using System.Linq;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Simulation.Company
{
    public enum StarterProductPhase { Learning = 0, Developing = 1, ReadyForTrial = 2, Trading = 3 }

    /// <summary>A small, desk-only first business. Work comes from the normal office pipeline;
    /// advancing the calendar never invents development or maintenance contributions.</summary>
    public sealed class StarterProductState
    {
        public const string Title = "우리가게 관리 프로그램";
        public const long DevelopmentCostWon = 300_000;
        public const int DevelopmentPersonHours = 24;
        public const int MaintenancePersonHours = 2;
        public const long LicencePriceWon = 60_000;
        public const long WeeklySupportPriceWon = 20_000;
        public const long WeekMinutes = 7 * 1440L;

        public StarterProductState(StarterProductPhase phase = StarterProductPhase.Learning,
            int developmentAttempt = 0, string developmentOrderId = "", int quality = 0,
            int customers = 0, int satisfaction = 50, long nextBillingMinute = -1,
            int billingPeriod = 0, string maintenanceOrderId = "", long totalRevenueWon = 0,
            long lastPeriodRevenueWon = 0, int missedPeriods = 0)
        {
            if (!Enum.IsDefined(typeof(StarterProductPhase), phase) || developmentAttempt < 0 ||
                quality < 0 || quality > 100 || customers < 0 || customers > 8 ||
                satisfaction < 0 || satisfaction > 100 || billingPeriod < 0 ||
                totalRevenueWon < 0 || lastPeriodRevenueWon < 0 || missedPeriods < 0)
                throw new ArgumentOutOfRangeException(nameof(phase), "Invalid starter business snapshot.");
            if (phase != StarterProductPhase.Learning && (developmentAttempt == 0 || string.IsNullOrEmpty(developmentOrderId)))
                throw new InvalidOperationException("A product requires its development order.");
            if (phase == StarterProductPhase.Trading ? nextBillingMinute < 0 : nextBillingMinute != -1)
                throw new InvalidOperationException("Only a trading product has a billing clock.");
            Phase = phase;
            DevelopmentAttempt = developmentAttempt;
            DevelopmentOrderId = developmentOrderId ?? string.Empty;
            Quality = quality;
            Customers = customers;
            Satisfaction = satisfaction;
            NextBillingMinute = nextBillingMinute;
            BillingPeriod = billingPeriod;
            MaintenanceOrderId = maintenanceOrderId ?? string.Empty;
            TotalRevenueWon = totalRevenueWon;
            LastPeriodRevenueWon = lastPeriodRevenueWon;
            MissedPeriods = missedPeriods;
        }

        public StarterProductPhase Phase { get; private set; }
        public int DevelopmentAttempt { get; private set; }
        public string DevelopmentOrderId { get; private set; }
        public int Quality { get; private set; }
        public int Customers { get; private set; }
        public int Satisfaction { get; private set; }
        public long NextBillingMinute { get; private set; }
        public int BillingPeriod { get; private set; }
        public string MaintenanceOrderId { get; private set; }
        public long TotalRevenueWon { get; private set; }
        public long LastPeriodRevenueWon { get; private set; }
        public int MissedPeriods { get; private set; }

        public static bool HasLesson(GameState state, int templateIndex) => state.Contracts.Contracts.Any(c =>
            c.Offer.IsExternal && c.Status == SubcontractStatus.Completed &&
            LegacyContractTemplateCatalog.TryResolve(c.Offer, out var template) && template.LegacyGlobalIndex == templateIndex);

        public static bool HasRequiredKnowHow(GameState state) =>
            HasLesson(state, 2) && HasLesson(state, 18);

        // Pin the two starter lessons after the daily board changes. Failed work can be retried,
        // with a new stable ID and the original costs, never by resetting its old settlement.
        public static SubcontractOffer NextLessonOffer(GameState state, ContractClientTierCatalog clients)
        {
            foreach (var index in new[] { 2, 18 })
            {
                if (HasLesson(state, index)) continue;
                var existing = state.Contracts.Contracts.Where(c => c.Offer.IsExternal &&
                    LegacyContractTemplateCatalog.TryResolve(c.Offer, out var t) && t.LegacyGlobalIndex == index).ToArray();
                if (existing.Any(c => c.Status == SubcontractStatus.Active)) return null;
                var original = ContractOfferBoardRules.GenerateOnboarding(state.WorldSeed, clients)
                    .Single(d => d.Template.LegacyGlobalIndex == index).Offer;
                if (existing.Length == 0) return original;
                return original.WithOfferId(original.OfferId + ":retry:" + existing.Length);
            }
            return null;
        }

        public bool TryStartDevelopment(GameState state, out string message)
        {
            Synchronize(state);
            if (Phase != StarterProductPhase.Learning) return Reject("이미 제품 개발을 시작했습니다.", out message);
            if (!HasRequiredKnowHow(state)) return Reject("단어 DB와 대여점 관리 도구 하청을 먼저 완료하세요.", out message);
            if (state.Company.CashWon < DevelopmentCostWon) return Reject("개발비 30만 원이 필요합니다.", out message);
            var sequence = checked(DevelopmentAttempt + 1);
            var id = "starter-product:development:" + sequence;
            var offer = InternalOffer(id, Title + " · 개발·시험", DevelopmentPersonHours, 30, CompanyWorkPurpose.StarterDevelopment);
            if (!state.Contracts.TryAddInternalWork(offer, state.Time.ElapsedMinutes))
                return Reject("진행 중인 업무를 끝내세요. 하청·제품 합계 2건까지 맡을 수 있습니다.", out message);
            state.Company.PayOperatingExpense(id + ":investment", state.Time.ElapsedMinutes, DevelopmentCostWon, Title + " 개발비");
            DevelopmentAttempt = sequence;
            DevelopmentOrderId = id;
            Phase = StarterProductPhase.Developing;
            message = "개발비 30만 원 지출 · 책상에서 24인시 작업 후 시험 판매할 수 있습니다.";
            return true;
        }

        public bool TryStartTrial(GameState state, out string message)
        {
            Synchronize(state);
            if (Phase != StarterProductPhase.ReadyForTrial) return Reject("제품 개발·시험 작업을 먼저 끝내세요.", out message);
            Phase = StarterProductPhase.Trading;
            Customers = 3;
            Satisfaction = Math.Max(40, Quality);
            NextBillingMinute = checked(state.Time.ElapsedMinutes + WeekMinutes);
            AddRevenue(state, "starter-product:trial", state.Time.ElapsedMinutes, Customers * LicencePriceWon);
            message = "시험 고객 3곳 · 사용권 매출 18만 원. 매주 유지보수 2인시를 끝내야 주간 요금이 정산됩니다.";
            return true;
        }

        public bool TryStartMaintenance(GameState state, out string message)
        {
            Synchronize(state);
            if (Phase != StarterProductPhase.Trading) return Reject("시험 판매를 먼저 시작하세요.", out message);
            if (state.Time.ElapsedMinutes >= NextBillingMinute) return Reject("이번 주 마감 시각입니다. 다음 정산 후 접수하세요.", out message);
            if (!string.IsNullOrEmpty(MaintenanceOrderId)) return Reject("이번 주 유지보수는 이미 접수했습니다.", out message);
            var id = "starter-product:support:" + BillingPeriod;
            var days = checked((int)Math.Max(1, (NextBillingMinute - state.Time.ElapsedMinutes + 1439) / 1440));
            if (!state.Contracts.TryAddInternalWork(InternalOffer(id, Title + " · 주간 유지보수",
                    MaintenancePersonHours, days, CompanyWorkPurpose.StarterMaintenance), state.Time.ElapsedMinutes, NextBillingMinute))
                return Reject("업무 슬롯이 가득 찼습니다. 진행 중인 업무부터 끝내세요.", out message);
            MaintenanceOrderId = id;
            message = "주간 유지보수 2인시 접수 · 아래 정산 시각 전까지 가족을 배정해 완료하세요.";
            return true;
        }

        public void Synchronize(GameState state)
        {
            if (Phase == StarterProductPhase.Developing)
            {
                var development = state.Contracts.Get(DevelopmentOrderId);
                if (development.Status == SubcontractStatus.Completed)
                {
                    Quality = ContractPerformanceRules.CalculateQuality(development, state.Family);
                    Phase = StarterProductPhase.ReadyForTrial;
                }
                else if (development.Status == SubcontractStatus.Failed) Phase = StarterProductPhase.Learning;
            }
            while (Phase == StarterProductPhase.Trading && state.Time.ElapsedMinutes > NextBillingMinute)
            {
                // Strictly after the boundary permits a real task finishing exactly at the deadline.
                var support = string.IsNullOrEmpty(MaintenanceOrderId) ? null : state.Contracts.Get(MaintenanceOrderId);
                bool serviced = support != null && support.Status == SubcontractStatus.Completed &&
                    support.ResolvedMinute <= NextBillingMinute;
                long revenue = 0;
                if (serviced)
                {
                    Satisfaction = Math.Min(100, Satisfaction + 10);
                    revenue = Customers * WeeklySupportPriceWon;
                    if (Customers == 0 || (Quality >= 55 && Satisfaction >= 60 && Customers < 8))
                    {
                        Customers++;
                        revenue += LicencePriceWon;
                    }
                }
                else
                {
                    Satisfaction = Math.Max(0, Satisfaction - 20);
                    Customers = Math.Max(0, Customers - 1);
                    MissedPeriods++;
                }
                LastPeriodRevenueWon = revenue;
                if (revenue > 0) AddRevenue(state, "starter-product:billing:" + BillingPeriod, NextBillingMinute, revenue);
                BillingPeriod = checked(BillingPeriod + 1);
                NextBillingMinute = checked(NextBillingMinute + WeekMinutes);
                MaintenanceOrderId = string.Empty;
            }
        }

        public SubcontractState CurrentWork(GameState state)
        {
            var id = Phase == StarterProductPhase.Developing ? DevelopmentOrderId : MaintenanceOrderId;
            return string.IsNullOrEmpty(id) ? null : state.Contracts.Get(id);
        }

        public void ValidateOrders(ContractPortfolio portfolio)
        {
            if (!string.IsNullOrEmpty(DevelopmentOrderId))
            {
                var order = portfolio.Get(DevelopmentOrderId);
                if (order.Offer.Purpose != CompanyWorkPurpose.StarterDevelopment ||
                    (Phase >= StarterProductPhase.ReadyForTrial && order.Status != SubcontractStatus.Completed))
                    throw new InvalidOperationException("Starter development order does not match product state.");
            }
            if (!string.IsNullOrEmpty(MaintenanceOrderId) &&
                (Phase != StarterProductPhase.Trading ||
                 portfolio.Get(MaintenanceOrderId).Offer.Purpose != CompanyWorkPurpose.StarterMaintenance))
                throw new InvalidOperationException("Starter maintenance order does not match product state.");
        }

        private void AddRevenue(GameState state, string id, long minute, long amount)
        {
            state.Company.RecordSale(id, minute, amount);
            TotalRevenueWon = checked(TotalRevenueWon + amount);
        }

        private static SubcontractOffer InternalOffer(string id, string title, int hours, int days, CompanyWorkPurpose purpose) =>
            new SubcontractOffer(id, "family_company_internal", "우리 가족회사", ContractServiceType.SmallBusinessTool,
                title, 1, hours, days, 0, 0, 0, purpose: purpose);

        private static bool Reject(string reason, out string message) { message = reason; return false; }
    }
}

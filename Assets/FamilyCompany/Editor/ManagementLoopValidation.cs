using System;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.ManagementUi;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class ManagementLoopValidation
    {
        [MenuItem("Family Company/Validate Management Loop")]
        public static void Run()
        {
            try
            {
                var state = PrototypeStateFactory.Create(20260810);
                AssertEqual(4, state.Family.Members.Count, "starting family count");
                AssertEqual(PrototypeStateFactory.StartingCapitalWon, state.Company.CashWon, "starting capital");
                AssertEqual(false, state.Growth.ResearchCenterUnlocked, "research center starts locked");
                AssertEqual(21, BootstrapContractCatalog.TotalOfferTemplateCount, "2000s offer template count");

                var starter = BootstrapContractCatalog.CreateOffer(20260810, "starter-client", "초기 고객사", 0);
                AssertEqual(0L, starter.PenaltyWon, "starter penalty");
                AssertEqual(true, state.Contracts.Accept(starter, state.Company, state.Family, state.Growth, 0).Accepted, "starter accepted");
                AssertEqual(true, state.Contracts.RecordWork(starter.OfferId, "player", starter.EstimatedPersonHours, 0, state.Family, state.Company).Completed, "starter completed");

                var gateState = PrototypeStateFactory.Create(20260810);
                gateState.Company.ChangeReputation(10);
                var advanced = BootstrapContractCatalog.CreateOffer(20260810, "advanced-client", "고급 고객사", 4);
                var lockedDecision = gateState.Contracts.Accept(advanced, gateState.Company, gateState.Family, gateState.Growth, 0);
                AssertEqual(ContractRejectionReason.RequiredTechnologyMissing, lockedDecision.Decision.RejectionReason, "advanced technology gate");
                AssertEqual(true, gateState.Growth.TryOpenResearchCenter(gateState.Company, 0, out _), "gate research center");
                AssertEqual(true, gateState.Growth.TryResearch(ResearchTechnologyIds.AutomationLine, gateState.Company, 1, out _), "gate automation research");
                AssertEqual(true, gateState.Contracts.Accept(advanced, gateState.Company, gateState.Family, gateState.Growth, 2).Accepted, "advanced accepted after research");

                var penaltyOffer = new SubcontractOffer(
                    "validation-penalty",
                    "risk-client",
                    "고위험 고객사",
                    ContractServiceType.SmallBusinessTool,
                    "마감 위약금 검증",
                    2,
                    12,
                    1,
                    0,
                    500_000,
                    0,
                    300_000,
                    20,
                    20);
                AssertEqual(true, state.Contracts.Accept(penaltyOffer, state.Company, state.Family, state.Growth, 0).Accepted, "penalty contract accepted");
                state.Contracts.RecordWork(penaltyOffer.OfferId, "player", 4, 0, state.Family, state.Company);
                state.Contracts.RecordWork(penaltyOffer.OfferId, "older_sister", 4, 0, state.Family, state.Company);
                var cashBeforePenalty = state.Company.CashWon;
                state.Contracts.FailOverdue(penaltyOffer.DeadlineDays * 1440L + 1, state.Company, state.Family);
                AssertEqual(cashBeforePenalty - 300_000, state.Company.CashWon, "penalty charged");
                AssertEqual(-2, state.Family.RelationshipScore("player", "older_sister"), "failed contract relationship");

                AssertEqual(true, state.Growth.TryOpenResearchCenter(state.Company, 10, out _), "research center opened");
                AssertEqual(true, state.Growth.TryResearch(ResearchTechnologyIds.AutomationLine, state.Company, 11, out _), "automation researched");
                AssertEqual(true, state.Growth.TryResearch(ResearchTechnologyIds.MarketAnalysis, state.Company, 12, out _), "market analysis researched");
                AssertEqual(true, state.Growth.TryPurchaseMarketReport(state.WorldSeed, state.Company, 13, out _), "market report purchased");
                state.Company.RecordSale("validation:saved-profits", 14, 6_000_000);
                AssertEqual(true, state.Growth.TryFoundBusiness(BusinessIndustry.WebAndSoftware, state.Company, state.Family, 15, out _), "owned business founded");
                state.Company.RecordSale("validation:expansion-profits", 16, 10_000_000);
                AssertEqual(true, state.Growth.TryPurchaseMarketReport(state.WorldSeed, BusinessIndustry.FeaturePhoneAndMobile, state.Company, 17, out _), "mobile market report");
                AssertEqual(true, state.Growth.TryFoundBusiness(BusinessIndustry.FeaturePhoneAndMobile, state.Company, state.Family, 18, out _), "second industry founded");
                AssertEqual(true, state.Growth.TryPurchaseMarketReport(state.WorldSeed, BusinessIndustry.WebAndSoftware, state.Company, 19, out _), "web market report refresh");
                AssertEqual(true, state.Growth.TryStartProduct(BusinessIndustry.WebAndSoftware, "검증 제품", 1_000_000, state.Company, 20, out _), "product started");
                var runner = new SimulationRunner(state);
                runner.AdvanceMinutes(state.Growth.ProductProject.DueMinute - state.Time.ElapsedMinutes);
                AssertEqual(true, state.Growth.ProductProject.Resolved, "product resolved");

                var dto = GameSaveMapper.ToDto(state);
                AssertEqual(9, dto.schemaVersion, "save schema");
                var restored = GameSaveMapper.FromDto(dto);
                AssertEqual(true, restored.Growth.ResearchCenterUnlocked, "research center round trip");
                AssertEqual(true, restored.Growth.HasTechnology(ResearchTechnologyIds.MarketAnalysis), "research round trip");
                AssertEqual(true, restored.Growth.ProductProject.Resolved, "product round trip");
                AssertEqual(true, restored.Growth.HasOwnedBusiness(BusinessIndustry.WebAndSoftware), "owned business round trip");
                AssertEqual(2, restored.Growth.OwnedBusinesses.Count, "business expansion round trip");
                AssertEqual(
                    state.Family.Get("older_sister").Autonomy.CompletedWorkBlocks,
                    restored.Family.Get("older_sister").Autonomy.CompletedWorkBlocks,
                    "office autonomy round trip");
                if (restored.Family.Get("player").CareerMemories.Count < 4)
                {
                    throw new InvalidOperationException("Career and relationship memories did not accumulate.");
                }
                AssertEqual(starter.RequiredDevelopment, restored.Contracts.Get(starter.OfferId).Offer.RequiredDevelopment, "contract stats round trip");
                ManagementUiLayoutMetrics.Validate(
                    ManagementUiLayoutMetrics.Calculate(1920, 1080, UiSafeInsets.None));
                Debug.Log("FAMILY_COMPANY_MANAGEMENT_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_MANAGEMENT_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.History;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Simulation.Workforce;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class ContractGrowthValidation
    {
        public const string RegistryAssetPath = "Assets/FamilyCompany/Content/History/company_registry_korea_2000_2026.json";

        [MenuItem("Family Company/Validate/Contract Growth V1")]
        public static void RunMenu()
        {
            try
            {
                var report = RunAll();
                Debug.Log(report);
                EditorUtility.DisplayDialog("Contract Growth V1", "PASS\n상세 결과는 Console과 Artifacts를 확인하세요.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Contract Growth V1", "FAIL\n" + exception.Message, "확인");
                throw;
            }
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Debug.Log(RunAll());
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string RunAll()
        {
            var registry = LoadRegistry();
            var clients = ContractClientTierCatalog.Create(registry, FamilyCompany.Simulation.Core.GameTime.CampaignStart);
            var lines = new List<string>
            {
                "Contract Growth V1 validation",
                $"registry={registry.Companies.Count}, active-contract-clients={clients.Clients.Count}"
            };

            ValidateLegacyTemplates(lines);
            ValidateActualClientMapping(clients, lines);
            ValidateDayOneAndOnboarding(clients, lines);
            ValidateSettlementAndSave(clients, lines);
            ValidateProgressionAndRecovery(clients, lines);
            ValidateGameTimeWorkGate(clients, lines);
            ValidateRoutesAndProducts(clients, lines);

            lines.Add("RESULT=PASS");
            var report = string.Join(Environment.NewLine, lines);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputDirectory = Path.Combine(projectRoot, "Artifacts", "ContractGrowthQa");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "contract-growth-validation.txt"), report, new System.Text.UTF8Encoding(false));
            return report;
        }

        private static HistoricalCompanyRegistry LoadRegistry()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(RegistryAssetPath);
            Require(asset != null, "Historical company registry asset is missing.");
            return KoreaHistoryV1RegistryLoader.FromTextAsset(asset);
        }

        private static void ValidateLegacyTemplates(ICollection<string> lines)
        {
            Require(BootstrapContractCatalog.TotalOfferTemplateCount == 21, "Original bootstrap template count changed.");
            Require(LegacyContractTemplateCatalog.All.Count == 21, "Metadata must cover all 21 original templates.");
            Require(LegacyContractTemplateCatalog.All.Select(item => item.TemplateId).Distinct(StringComparer.Ordinal).Count() == 21,
                "Legacy template IDs are not unique.");
            for (var index = 0; index < 21; index++)
            {
                var original = BootstrapContractCatalog.CreateOffer(1, "qa", "QA", index);
                var metadata = LegacyContractTemplateCatalog.Get(index).Baseline;
                Require(original.Title == metadata.Title && original.RewardWon == metadata.RewardWon &&
                        original.EstimatedPersonHours == metadata.EstimatedPersonHours,
                    $"Legacy template metadata drifted at index {index}.");
            }
            lines.Add("PASS legacy-21=preserved-and-covered");
        }

        private static void ValidateActualClientMapping(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            var samsung = clients.Get("kr_samsung_electronics");
            Require(samsung.DisplayNameKo.Contains("삼성전자"), "Samsung display name was not preserved.");
            Require(samsung.Tier == ContractClientTier.T4NationalEnterprise, "Samsung Electronics must be T4.");
            Require(clients.Get(ContractClientTierCatalog.LegacySamsungElectronicsId).ClientId == samsung.ClientId,
                "Legacy Samsung ID alias is broken.");
            Require(clients.Get("kr_samsung_sds").Tier == ContractClientTier.T3PrimeVendor, "Samsung SDS must map to T3 prime vendor.");
            Require(clients.Get("kr_ncsoft").Tier == ContractClientTier.T2GrowthCompany, "NCsoft must map to T2.");
            Require(clients.Get("kr_dreamwiz").Tier == ContractClientTier.T1RegionalSmallBusiness, "DreamWiz must map to T1.");
            foreach (var tier in Enum.GetValues(typeof(ContractClientTier)).Cast<ContractClientTier>())
            {
                var tierClients = clients.Clients.Where(item => item.Tier == tier).ToArray();
                Require(tierClients.Length > 0, $"Client tier {tier} is empty.");
                lines.Add($"CATALOG {tier}={tierClients.Length}:" + string.Join(",", tierClients.Select(item => $"{item.ClientId}|{item.DisplayNameKo}")));
            }
        }

        private static void ValidateDayOneAndOnboarding(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            foreach (var seed in new[] { 1, 17, 20000103, 777777, int.MaxValue })
            {
                var state = PrototypeStateFactory.Create(seed);
                var first = ContractBusinessViewModelRules.CreateBoard(state, clients, BusinessIndustry.WebAndSoftware);
                var again = ContractBusinessViewModelRules.CreateBoard(state, clients, BusinessIndustry.WebAndSoftware);
                Require(first.Cards.Count == 3 && first.Snapshot.FirstContractRecommendation, "New game must show three recommended offers.");
                Require(first.Cards.All(item => item.Definition.ClientTier == ContractClientTier.T0LocalBusiness),
                    "Day-one onboarding exposed a non-T0 client.");
                Require(first.Cards.All(item => item.Definition.ClientTier < ContractClientTier.T3PrimeVendor),
                    "Day-one onboarding exposed enterprise clients.");
                Require(first.Cards.Select(item => item.OfferId).SequenceEqual(again.Cards.Select(item => item.OfferId)),
                    "UI reopen rerolled onboarding offers.");
                Require(first.Cards.Select(item => item.Definition.Offer.EstimatedPersonHours).Distinct().Count() == 3 &&
                        first.Cards.Select(item => item.Definition.Offer.RewardWon).Distinct().Count() == 3 &&
                        first.Cards.Select(item => item.Definition.Offer.DeadlineDays).Distinct().Count() == 3,
                    "Onboarding choices do not have three distinct work/reward/deadline tradeoffs.");
            }
            var reference = ContractBusinessViewModelRules.CreateBoard(
                PrototypeStateFactory.Create(20000103), clients, BusinessIndustry.WebAndSoftware);
            lines.Add("PASS day-one-seeds=T0-only,T3-T4-zero,deterministic");
            lines.Add("FIRST-3=" + string.Join(" / ", reference.Cards.Select(item =>
                $"{item.ClientNameKo}:{item.TitleKo}:{item.Definition.Offer.EstimatedPersonHours}h:{item.Definition.Offer.DeadlineDays}d:{item.Definition.Offer.RewardWon}won")));
        }

        private static void ValidateSettlementAndSave(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            var state = PrototypeStateFactory.Create(20000103);
            new SimulationRunner(state).AdvanceMinutes(10);
            var card = ContractBusinessViewModelRules.CreateBoard(state, clients, BusinessIndustry.WebAndSoftware).Cards[0];
            var before = state.Company.CashWon;
            var accepted = state.Contracts.Accept(card.Definition.Offer, state.Company, state.Family, state.Growth, state.Time.ElapsedMinutes);
            Require(accepted.Accepted, "First contract could not be manually accepted.");
            Require(state.Contracts.Contracts.Count == 1, "Accepted contract count is wrong.");
            var workSession = new AuthoritativeContractWorkSession(
                card.OfferId,
                "player",
                state.Time.ElapsedMinutes);
            var work = workSession.AdvanceTo(
                accepted.Contract.DueMinute,
                state.Contracts,
                state.Family,
                state.Company);
            Require(work.Completed, "First contract did not complete.");
            var expected = before - card.Definition.Offer.UpfrontCostWon + card.Definition.Offer.RewardWon;
            Require(state.Company.CashWon == expected, "Settlement amount is not exact.");
            var ledgerId = $"contract:{card.OfferId}:settlement";
            Require(state.Company.Ledger.Count(item => item.TransactionId == ledgerId) == 1, "Settlement ledger must be exactly once.");
            var second = state.Contracts.RecordWork(card.OfferId, "player", 1, accepted.Contract.DueMinute,
                state.Family, state.Company);
            Require(second.RejectionReason == ContractWorkRejectionReason.ContractNotActive && state.Company.CashWon == expected,
                "Retry paid or worked a completed contract.");

            var beforeBoard = ContractBusinessViewModelRules.CreateBoard(state, clients, BusinessIndustry.WebAndSoftware);
            var dto = GameSaveMapper.ToDto(state);
            var loaded = GameSaveMapper.FromDto(dto);
            var afterBoard = ContractBusinessViewModelRules.CreateBoard(loaded, clients, BusinessIndustry.WebAndSoftware);
            Require(beforeBoard.Cards.Select(item => item.OfferId).SequenceEqual(afterBoard.Cards.Select(item => item.OfferId)),
                "Save/load changed deterministic offer pool.");
            Require(!afterBoard.Snapshot.FirstContractRecommendation && loaded.Contracts.Contracts.Count == 1,
                "First-contract state returned after save/load.");
            var beforeSummary = ContractPerformanceRules.Rebuild(state.Contracts, state.Family, clients);
            var afterSummary = ContractPerformanceRules.Rebuild(loaded.Contracts, loaded.Family, clients);
            Require(beforeSummary.CompletedContracts == afterSummary.CompletedContracts &&
                    beforeSummary.EarnedContractRevenueWon == afterSummary.EarnedContractRevenueWon,
                "Save/load changed reconstructed performance ledger.");
            lines.Add("PASS accept-assign-complete=single-settlement,save-v10-reconstructs-growth");
        }

        private static void ValidateProgressionAndRecovery(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            var weakRecords = BuildRecords(30, BusinessIndustry.HardwareAndPc, 50, 40, false, 30);
            var weakSummary = ContractPerformanceRules.Summarize(weakRecords);
            var strongSummary = ContractPerformanceRules.Summarize(BuildRecords(32, BusinessIndustry.HardwareAndPc, 92, 90, true, 30));
            var family = CreateExpertFamily();
            var company = new CompanyState("QA", 20_000_000, 60);
            var growth = CreateMatureGrowth();
            var weakProfile = ContractPerformanceRules.BuildCompanyProfile(weakSummary, company, family, growth);
            var weak = ContractProgressionRules.EvaluateAll(weakSummary, weakProfile, BusinessIndustry.HardwareAndPc);
            Require(!weak.Single(item => item.Tier == ContractClientTier.T1RegionalSmallBusiness).Unlocked,
                "Completion count alone unlocked upper clients despite poor quality/on-time results.");

            var strongProfile = ContractPerformanceRules.BuildCompanyProfile(strongSummary, company, family, growth);
            var strong = ContractProgressionRules.EvaluateAll(strongSummary, strongProfile, BusinessIndustry.HardwareAndPc);
            Require(strong.All(item => item.Unlocked), "Strong performance did not unlock T0 through T4 sequentially.");
            var sawSamsung = false;
            var sawT0AfterUnlock = false;
            for (var day = 1; day <= 500; day++)
            {
                var board = ContractOfferBoardRules.Generate(20000103, day * 1440L, BusinessIndustry.HardwareAndPc,
                    true, strongSummary, strongProfile, clients);
                sawSamsung |= board.Offers.Any(item => item.Client.ClientId == "kr_samsung_electronics");
                sawT0AfterUnlock |= board.Offers.Any(item => item.ClientTier == ContractClientTier.T0LocalBusiness);
            }
            Require(sawSamsung, "Unlocked T4 pool never produced existing Samsung Electronics test client.");
            Require(sawT0AfterUnlock, "Recovery T0 offers disappeared after upper tiers unlocked.");
            var recoveryBoard = ContractOfferBoardRules.Generate(77, 12 * 1440L, BusinessIndustry.HardwareAndPc,
                true, weakSummary, weakProfile, clients);
            Require(recoveryBoard.Offers.Any(item => item.ClientTier == ContractClientTier.T0LocalBusiness),
                "Failure state has no recovery contract.");
            lines.Add("PASS progression=multi-metric,T1-T4-sequential,Samsung-after-gate,recovery-T0-always");
        }

        private static void ValidateGameTimeWorkGate(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            var state = PrototypeStateFactory.Create(9);
            state.Time.Advance(10);
            var offer = ContractBusinessViewModelRules.CreateBoard(state, clients, BusinessIndustry.WebAndSoftware).Cards[0].Definition.Offer;
            Require(state.Contracts.Accept(offer, state.Company, state.Family, state.Growth, 10).Accepted, "Work-gate contract accept failed.");
            var session = new AuthoritativeContractWorkSession(offer.OfferId, "player", 10);
            var task = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(offer));
            var minutesPerPersonHour = WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(
                state.Family.Get("player").Capability,
                task);
            Require(session.AdvanceTo(10, state.Contracts, state.Family, state.Company).AppliedHours == 0, "Paused GameTime produced work.");
            Require(session.AdvanceTo(10 + minutesPerPersonHour - 1, state.Contracts, state.Family, state.Company).AppliedHours == 0,
                "Sub-hour skill-weighted GameTime produced work.");
            var oneHour = session.AdvanceTo(10 + minutesPerPersonHour, state.Contracts, state.Family, state.Company);
            Require(oneHour.AppliedHours == 1, "Skill-weighted authoritative minutes did not produce one person-hour.");
            Require(session.AdvanceTo(10 + minutesPerPersonHour, state.Contracts, state.Family, state.Company).AppliedHours == 0,
                "Same GameTime tick produced duplicate work.");
            lines.Add($"PASS work-authority=pause/frame/UI-zero,skill-weighted-game-minutes={minutesPerPersonHour}");
        }

        private static void ValidateRoutesAndProducts(ContractClientTierCatalog clients, ICollection<string> lines)
        {
            var routes = new ContractBusinessRouteStack();
            routes.Open(ContractBusinessRoute.BusinessHub);
            routes.Open(ContractBusinessRoute.ContractBoard);
            Require(routes.Current == ContractBusinessRoute.ContractBoard && routes.TryBack() && routes.Current == ContractBusinessRoute.BusinessHub,
                "Contract to business back stack failed.");
            Require(routes.TryBack() && routes.Current == ContractBusinessRoute.OfficeWorld && !routes.TryBack(),
                "Business to office back stack failed.");

            var summary = ContractPerformanceRules.Summarize(BuildRecords(8, BusinessIndustry.WebAndSoftware, 90, 90, true, 20));
            var company = new CompanyState("QA", 10_000_000, 20);
            var locked = ProductOpportunityRules.EvaluateAll(summary, company, new CompanyGrowthState())
                .Single(item => item.Definition.Industry == BusinessIndustry.WebAndSoftware);
            Require(!locked.Unlocked && locked.ProgressBasisPoints > 0, "Product path must show real accumulated progress but remain research-gated.");
            var report = new MarketReportState("QA", "QA", 70, 1, BusinessIndustry.WebAndSoftware);
            var readyGrowth = new CompanyGrowthState(true,
                new[] { ResearchTechnologyIds.ThreeDModeling, ResearchTechnologyIds.AutomationLine, ResearchTechnologyIds.MarketAnalysis },
                report);
            var ready = ProductOpportunityRules.EvaluateAll(summary, company, readyGrowth)
                .Single(item => item.Definition.Industry == BusinessIndustry.WebAndSoftware);
            Require(ready.Unlocked && ready.ProductSystemReady, "Real existing product system did not unlock from cash/research/contract experience.");
            lines.Add("PASS routes=office-business-contract-products-back-stack;own-product=real-progress-inputs");
        }

        private static IEnumerable<ContractPerformanceRecord> BuildRecords(
            int count,
            BusinessIndustry industry,
            int quality,
            int satisfaction,
            bool onTime,
            int hours)
        {
            for (var index = 0; index < count; index++)
            {
                yield return new ContractPerformanceRecord(
                    $"synthetic:{index:D4}", "qa-client", ContractClientTier.T0LocalBusiness, industry,
                    true, onTime, quality, satisfaction, hours, index * 10_000L, index * 10_000L + 100, 500_000);
            }
        }

        private static FamilyState CreateExpertFamily()
        {
            return new FamilyState(new[]
            {
                Expert("player", "나", FamilyRole.Player, 1985),
                Expert("older_sister", "누나", FamilyRole.OlderSister, 1979),
                Expert("father", "아빠", FamilyRole.Father, 1953),
                Expert("mother", "엄마", FamilyRole.Mother, 1955)
            });
        }

        private static FamilyMemberState Expert(string id, string name, FamilyRole role, int birthYear)
        {
            return new FamilyMemberState(id, name, role, new DateTime(birthYear, 1, 1), "QA",
                capability: new WorkforceCapabilityState(id, new WorkSkillSet(95, 95, 95, 95, 95, 95),
                    95, WorkforceStressRules.MinimumStressGainBasisPoints));
        }

        private static CompanyGrowthState CreateMatureGrowth()
        {
            var businesses = BusinessIndustryCatalog.All.Select((item, index) =>
                new OwnedBusinessState(item.Industry, item.OwnBusinessName, index + 1, 5_000_000)).ToArray();
            return new CompanyGrowthState(true,
                new[] { ResearchTechnologyIds.ThreeDModeling, ResearchTechnologyIds.AutomationLine, ResearchTechnologyIds.MarketAnalysis },
                ownedBusinesses: businesses);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

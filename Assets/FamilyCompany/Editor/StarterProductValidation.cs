using System;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.History;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Simulation.Technology;
using FamilyCompany.Simulation.Stamina;

namespace FamilyCompany.Editor
{
    // No engine dependency: run both in the fast pure harness and the editor regression suite.
    public static class StarterProductValidation
    {
        public static string RunAll()
        {
            var clients = ContractClientTierCatalog.Create(
                new HistoricalCompanyRegistry(1, Array.Empty<HistoricalCompanyDefinition>()), GameTime.CampaignStart);
            foreach (var memberId in new[] { "player", "father", "mother", "older_sister" })
                ValidateCommonSettlement(memberId);
            ValidateFailedAndTimedWork();
            ValidateWholeBusiness(clients);
            ValidateOldSaveAndCorruption();
            ValidateScheduledSleep();
            return "STARTER_PRODUCT: PASS | four-member settlement, exactly-once, time gate, pinned lessons, actual credited work, trial, weekly support, save/reload, v11 migration";
        }

        private static void ValidateCommonSettlement(string memberId)
        {
            var state = PrototypeStateFactory.Create();
            var baseline = LegacyContractTemplateCatalog.Get(2).Baseline;
            var offer = new SubcontractOffer("single-" + memberId, baseline.ClientCompanyId, baseline.ExactClientDisplayName,
                baseline.ServiceType, baseline.Title, 1, 1, 14, 0, 100, 0, industry: baseline.Industry);
            var accepted = state.Contracts.Accept(offer, state.Company, state.Family, state.Growth, 0);
            Require(accepted.Accepted, "single worker accepted");
            long before = state.Company.CashWon;
            CompleteWork(state, accepted.Contract, memberId);
            Require(state.Company.CashWon == before + 100, memberId + " cash once");
            var points = state.Growth.Technology.Snapshot().Sum(p => p.Value);
            Require(points == ContractPortfolio.TechnologyGrantsFor(offer).Sum(g => g.Points), memberId + " core technology awarded");
            var loaded = GameSaveMapper.FromDto(GameSaveMapper.ToDto(state));
            var duplicate = loaded.Contracts.RecordWork(offer.OfferId, memberId, 1, loaded.Time.ElapsedMinutes, loaded.Family, loaded.Company);
            Require(!duplicate.Applied && loaded.Company.CashWon == state.Company.CashWon &&
                loaded.Growth.Technology.Snapshot().Sum(p => p.Value) == points, "replay after load cannot pay twice");
            Require(loaded.Contracts.Get(offer.OfferId).ResolvedQuality >= 0, "completion quality snapshot survives save");
        }

        private static void ValidateFailedAndTimedWork()
        {
            Require(!OfficeNavigationTrafficRules.KeepMovingPeerPriorityForRailYield("father", "player", true, false),
                "stationary player with stale intent cannot block the priority actor forever");
            Require(OfficeNavigationTrafficRules.KeepMovingPeerPriorityForRailYield("father", "player", true, true),
                "moving peers retain existing ordinal priority");
            var state = PrototypeStateFactory.Create();
            var offer = BootstrapContractCatalog.CreateOffer(0, "local", "시험 고객", 2);
            Require(state.Contracts.Accept(offer, state.Company, 0).Accepted, "time gate setup");
            var work = state.Contracts.Get(offer.OfferId);
            Require(!state.Contracts.RecordWork(offer.OfferId, "father", 1, 0, state.Family, state.Company).Applied,
                "no elapsed time cannot work");
            state.Contracts.FailOverdue(work.DueMinute + 1, state.Company, state.Family);
            Require(state.Growth.Technology.LearnedTechnologyIds.Count == 0, "failed work awards no technology");

            var skilled = PrototypeStateFactory.Create();
            for (int i = 0; i < 20; i++) skilled.Growth.Technology.ApplyGrants(ContractPortfolio.TechnologyGrantsFor(offer));
            Require(skilled.Contracts.Accept(offer, skilled.Company, skilled.Family, skilled.Growth, 0).Accepted, "skilled offer accepted");
            work = skilled.Contracts.Get(offer.OfferId);
            Require(work.WorkRateBasisPoints > 10000 && work.QualityBonus > 0, "experience freezes on acceptance");
            int rate = work.WorkRateBasisPoints;
            var session = new AuthoritativeContractWorkSession(offer.OfferId, "father", 0);
            var minute = ContractPortfolio.MinutesPerPersonHour(work, skilled.Family.Get("father"));
            var result = session.AdvanceTo(minute, skilled.Contracts, skilled.Family, skilled.Company, skilled.Growth.Technology);
            Require(result.AppliedHours == 1 && result.WorkRateBasisPoints == rate, "session and core share boosted time gate");
        }

        private static void ValidateWholeBusiness(ContractClientTierCatalog clients)
        {
            var state = PrototypeStateFactory.Create();
            var p = state.Growth.StarterProduct;
            Require(!p.TryStartDevelopment(state, out _), "development locked before lessons");
            foreach (var template in new[] { 2, 18 })
            {
                var offer = StarterProductState.NextLessonOffer(state, clients);
                Require(offer != null && LegacyContractTemplateCatalog.TryResolve(offer, out var t) && t.LegacyGlobalIndex == template,
                    "pinned lesson stays available after first acceptance");
                var accepted = state.Contracts.Accept(offer, state.Company, state.Family, state.Growth, state.Time.ElapsedMinutes);
                Require(accepted.Accepted, "lesson accepted " + template + " / " + accepted.Decision.RejectionReason);
                Require(StarterProductState.NextLessonOffer(state, clients) == null, "no duplicate active lesson");
                CompleteWork(state, accepted.Contract, "father");
            }
            var points = state.Growth.Technology.Snapshot().Sum(x => x.Value);
            var reputation = state.Company.Reputation;
            var cash = state.Company.CashWon;
            Require(p.TryStartDevelopment(state, out _), "prototype starts after two lessons");
            var experienced = GameSaveMapper.FromDto(GameSaveMapper.ToDto(state));
            for (int n = 0; n < 20; n++)
            {
                experienced.Growth.Technology.ApplyGrants(ContractTechnologyGrantCatalog.ForTemplateIndex(2));
                experienced.Growth.Technology.ApplyGrants(ContractTechnologyGrantCatalog.ForTemplateIndex(18));
            }
            new SimulationRunner(experienced).AdvanceMinutes(31 * 1440);
            Require(experienced.Growth.StarterProduct.TryStartDevelopment(experienced, out _),
                "experienced product starts through the public workflow");
            var experiencedWork = experienced.Growth.StarterProduct.CurrentWork(experienced);
            Require(experiencedWork.WorkRateBasisPoints > 10000 && experiencedWork.QualityBonus > 0 &&
                ContractPortfolio.TechnologyGrantsFor(experiencedWork.Offer).Count == 0,
                "own software benefits from learned technology without farming more grants");
            Require(state.Company.CashWon == cash - StarterProductState.DevelopmentCostWon, "development investment once");
            Require(!p.TryStartDevelopment(state, out _) && !p.TryStartTrial(state, out _), "no repeated charge or early trial");
            var failedDevelopment = GameSaveMapper.FromDto(GameSaveMapper.ToDto(state));
            var fp = failedDevelopment.Growth.StarterProduct;
            string failedId = fp.DevelopmentOrderId;
            new SimulationRunner(failedDevelopment).AdvanceMinutes(31 * 1440);
            Require(fp.Phase == StarterProductPhase.Learning && failedDevelopment.Company.Reputation == reputation,
                "missed internal development deadline does not damage external reputation");
            long retryCash = failedDevelopment.Company.CashWon;
            Require(fp.TryStartDevelopment(failedDevelopment, out _) && fp.DevelopmentOrderId != failedId &&
                failedDevelopment.Company.CashWon == retryCash - StarterProductState.DevelopmentCostWon,
                "failed development retries with a new paid attempt, never the old settlement");
            new SimulationRunner(state).AdvanceMinutes(1440);
            Require(p.CurrentWork(state).CompletedPersonHours == 0 && p.Phase == StarterProductPhase.Developing,
                "calendar passage does not invent development");
            var dev = p.CurrentWork(state);
            CompleteWork(state, dev, "father");
            p.Synchronize(state);
            Require(p.Phase == StarterProductPhase.ReadyForTrial && p.Quality == dev.ResolvedQuality, "real work makes prototype ready");
            Require(ContractPerformanceRules.Rebuild(state.Contracts, state.Family, clients).CompletedContracts == 2 &&
                state.Company.Reputation == reputation && state.Growth.Technology.Snapshot().Sum(x => x.Value) == points,
                "internal development never inflates subcontract reputation, points, or performance");
            Require(p.TryStartTrial(state, out _) && p.TotalRevenueWon == 180000, "first three licences settle");
            cash = state.Company.CashWon;
            Require(!p.TryStartTrial(state, out _) && state.Company.CashWon == cash, "cannot repeat initial sale");
            Require(p.TryStartMaintenance(state, out _) && !p.TryStartMaintenance(state, out _), "one support order per period");
            var support = p.CurrentWork(state);
            Require(ContractPortfolio.UsesOnlyDesk(support), "support uses available PC equipment");
            CompleteWork(state, support, "father");
            var snapshot = GameSaveMapper.ToDto(state);
#if UNITY_EDITOR
            snapshot = UnityEngine.JsonUtility.FromJson<GameSaveDto>(UnityEngine.JsonUtility.ToJson(snapshot));
#endif
            var a = GameSaveMapper.FromDto(snapshot);
            var b = GameSaveMapper.FromDto(snapshot);
            long target = p.NextBillingMinute + 1;
            new SimulationRunner(a).AdvanceMinutes(target - a.Time.ElapsedMinutes);
            var runner = new SimulationRunner(b);
            while (b.Time.ElapsedMinutes < target) runner.AdvanceMinutes(Math.Min(31, target - b.Time.ElapsedMinutes));
            Require(a.Company.CashWon == b.Company.CashWon && a.Growth.StarterProduct.TotalRevenueWon == b.Growth.StarterProduct.TotalRevenueWon &&
                a.Growth.StarterProduct.BillingPeriod == 1, "weekly revenue independent of save and jump size");
            Require(a.Growth.StarterProduct.LastPeriodRevenueWon >= 60000, "completed maintenance earns support fees");
            cash = a.Company.CashWon;
            new SimulationRunner(a).AdvanceMinutes(0);
            var reloaded = GameSaveMapper.FromDto(GameSaveMapper.ToDto(a));
            new SimulationRunner(reloaded).AdvanceMinutes(0);
            Require(reloaded.Company.CashWon == cash, "opening UI or reloading does not repeat billing");
            var ap = reloaded.Growth.StarterProduct;
            int customers = ap.Customers;
            new SimulationRunner(reloaded).AdvanceMinutes(ap.NextBillingMinute + 1 - reloaded.Time.ElapsedMinutes);
            Require(ap.LastPeriodRevenueWon == 0 && reloaded.Company.CashWon == cash && ap.Customers == customers - 1,
                "unworked week has zero revenue and loses a customer");
            Require(ContractPerformanceRules.Rebuild(reloaded.Contracts, reloaded.Family, clients).CompletedContracts == 2,
                "maintenance is not a fake external client achievement");
            Require(ap.TryStartMaintenance(reloaded, out _), "next support period can start");
            var missedOrder = ap.CurrentWork(reloaded);
            Require(missedOrder.DueMinute == ap.NextBillingMinute, "support work and billing have one exact deadline");
            new SimulationRunner(reloaded).AdvanceMinutes(ap.NextBillingMinute + 1 - reloaded.Time.ElapsedMinutes);
            Require(missedOrder.Status == SubcontractStatus.Failed && reloaded.Contracts.ActiveCount == 0,
                "expired support releases its slot at billing, not the next day");
            new SimulationRunner(reloaded).AdvanceMinutes(10 * StarterProductState.WeekMinutes);
            Require(ap.Customers == 0 && ap.TryStartMaintenance(reloaded, out _), "zero-customer business can recover");
            CompleteWork(reloaded, ap.CurrentWork(reloaded), "father");
            new SimulationRunner(reloaded).AdvanceMinutes(ap.NextBillingMinute + 1 - reloaded.Time.ElapsedMinutes);
            Require(ap.Customers == 1 && ap.LastPeriodRevenueWon == StarterProductState.LicencePriceWon,
                "actual support work recruits one recovery customer");

            var retryState = PrototypeStateFactory.Create();
            var retryOffer = StarterProductState.NextLessonOffer(retryState, clients);
            var retryContract = retryState.Contracts.Accept(retryOffer, retryState.Company, retryState.Family,
                retryState.Growth, retryState.Time.ElapsedMinutes).Contract;
            new SimulationRunner(retryState).AdvanceMinutes(retryContract.DueMinute + 1);
            var nextTry = StarterProductState.NextLessonOffer(retryState, clients);
            Require(nextTry.OfferId != retryOffer.OfferId && nextTry.UpfrontCostWon == retryOffer.UpfrontCostWon &&
                retryState.Contracts.Accept(nextTry, retryState.Company, retryState.Family, retryState.Growth,
                    retryState.Time.ElapsedMinutes).Accepted, "failed pinned lesson can be accepted again with original terms");
        }

        private static void ValidateOldSaveAndCorruption()
        {
            var source = PrototypeStateFactory.Create();
            source.Growth.Technology.ApplyGrants(ContractTechnologyGrantCatalog.ForTemplateIndex(2));
            var dto = GameSaveMapper.ToDto(source);
            dto.schemaVersion = 11;
            dto.growth.starterProduct = null;
            var old = GameSaveMapper.FromDto(dto);
            Require(old.Growth.StarterProduct.Phase == StarterProductPhase.Learning &&
                old.Growth.Technology.Snapshot().Sum(p => p.Value) == source.Growth.Technology.Snapshot().Sum(p => p.Value) &&
                old.Company.CashWon == source.Company.CashWon, "v11 preserves money and technology without retroactive grants");
            dto.schemaVersion = 12;
            bool rejected = false;
            try { GameSaveMapper.FromDto(dto); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "missing v12 product state must fail clearly");
        }

        private static void ValidateScheduledSleep()
        {
            var catalog = CharacterStaminaCatalog.CreateCommonDefault();
            var one = CharacterStaminaSimulation.CreateAt(1, "father", catalog, 100, 0);
            one.SetActivity(StaminaActivityKind.Sleep, 100);
            Require(one.AdvanceTo(580, false).RecoveredUnits == one.Profile.MaxUnits,
                "eight actual sleeping hours replenish one full stamina bar");
            foreach (int partition in new[] { 1, 2, 4, 17 })
            {
                var split = CharacterStaminaSimulation.CreateAt(1, "father", catalog, 100, 0);
                split.SetActivity(StaminaActivityKind.Sleep, 100);
                while (split.State.LastProcessedMinute < 580)
                {
                    split.AdvanceTo(Math.Min(580, split.State.LastProcessedMinute + partition), false);
                    split = CharacterStaminaSimulation.Restore(split.ExportSnapshot(), catalog);
                }
                Require(split.State.CurrentUnits == one.State.CurrentUnits, "sleep partition and save restore " + partition);
            }
            var offDuty = CharacterStaminaSimulation.CreateAt(1, "father", catalog, 0, 100);
            offDuty.SetActivity(StaminaActivityKind.OffDuty, 0);
            Require(offDuty.AdvanceTo(480, false).RecoveredUnits == 0 && offDuty.State.CurrentUnits == 100,
                "ordinary off-duty time does not invent sleep recovery");
            var pending = CharacterStaminaSimulation.CreateAt(1, "father", catalog, 0, 100);
            pending.AdvanceTo(0, true);
            pending.SetActivity(StaminaActivityKind.Sleep, 0);
            Require(pending.AdvanceTo(480, false).ReachedRequestedMinute &&
                pending.State.RecoveryPhase == StaminaRecoveryPhase.Working && pending.State.CurrentUnits == pending.Profile.MaxUnits,
                "sleep clears only unclaimed facility selection requests");
            var state = PrototypeStateFactory.Create();
            var runner = new SimulationRunner(state);
            runner.AdvanceMinutes(850); // first 23:00; existing work consumes stamina
            Require(state.Family.Members.Any(m => m.Energy < 100), "normal first day consumed stamina");
            runner.AdvanceMinutes(480);
            Require(state.Family.Members.All(m => m.Energy == 100), "normal next morning works without unavailable 3D recovery furniture / " +
                string.Join(";", state.Family.Members.Select(m => m.MemberId + "=" + m.Energy + "/" + m.Autonomy.CurrentAction)));
        }

        private static void CompleteWork(GameState state, SubcontractState work, string memberId)
        {
            var runner = new SimulationRunner(state);
            int safety = 0;
            while (work.Status == SubcontractStatus.Active && safety++ < 2000)
            {
                runner.AdvanceMinutes(60);
                if (work.Status != SubcontractStatus.Active) break;
                state.Contracts.RecordWork(work.Offer.OfferId, memberId, 1, state.Time.ElapsedMinutes, state.Family, state.Company);
            }
            Require(work.Status == SubcontractStatus.Completed, memberId + " credited work completed before deadline / " + work.Offer.Title);
        }

        private static void Require(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("StarterProduct: " + label);
        }
    }
}

using System;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Finance;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Save
{
    public static class GameSaveMapper
    {
        public static GameSaveDto ToDto(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new GameSaveDto
            {
                worldSeed = state.WorldSeed,
                elapsedMinutes = state.Time.ElapsedMinutes,
                company = new CompanySaveDto
                {
                    companyName = state.Company.CompanyName,
                    cashWon = state.Company.CashWon,
                    reputation = state.Company.Reputation
                },
                family = state.Family.Members.Select(member => new FamilyMemberSaveDto
                {
                    memberId = member.MemberId,
                    displayName = member.DisplayName,
                    role = (int)member.Role,
                    birthYear = member.BirthDate.Year,
                    birthMonth = member.BirthDate.Month,
                    birthDay = member.BirthDate.Day,
                    companyDuty = member.CompanyDuty,
                    energy = member.Energy,
                    trust = member.Trust,
                    stress = member.Stress,
                    development = member.Stats.Development,
                    speed = member.Stats.Speed,
                    stamina = member.Stats.Stamina,
                    planning = member.Stats.Planning,
                    art = member.Stats.Art,
                    sales = member.Stats.Sales,
                    mental = member.Stats.Mental,
                    teamwork = member.Stats.Teamwork,
                    loyalty = member.Stats.Loyalty,
                    potential = member.Stats.Potential,
                    autonomy = new OfficeAutonomySaveDto
                    {
                        currentAction = (int)member.Autonomy.CurrentAction,
                        targetLocation = (int)member.Autonomy.TargetLocation,
                        actionStartedMinute = member.Autonomy.ActionStartedMinute,
                        actionEndsMinute = member.Autonomy.ActionEndsMinute,
                        lastProcessedMinute = member.Autonomy.LastProcessedMinute,
                        completedWorkBlocks = member.Autonomy.CompletedWorkBlocks,
                        completedBreaks = member.Autonomy.CompletedBreaks,
                        burnoutCount = member.Autonomy.BurnoutCount,
                        lastIncidentSummary = member.Autonomy.LastIncidentSummary,
                        lastIncidentMinute = member.Autonomy.LastIncidentMinute,
                        lastSocialEventDay = member.Autonomy.LastSocialEventDay
                    },
                    careerMemories = member.CareerMemories.Select(memory => new CareerMemorySaveDto
                    {
                        memoryId = memory.MemoryId,
                        industry = (int)memory.Industry,
                        kind = (int)memory.Kind,
                        summary = memory.Summary,
                        occurredMinute = memory.OccurredMinute,
                        bondDelta = memory.BondDelta,
                        colleagueMemberIds = memory.ColleagueMemberIds.ToList()
                    }).ToList()
                }).ToList(),
                events = state.Events.Snapshot().Select(item => new ScheduledEventSaveDto
                {
                    eventId = item.EventId,
                    dueMinute = item.DueMinute,
                    priority = item.Priority,
                    kind = item.Kind,
                    payload = item.Payload
                }).ToList(),
                ledger = state.Company.Ledger.Select(transaction => new LedgerTransactionSaveDto
                {
                    transactionId = transaction.TransactionId,
                    elapsedMinute = transaction.ElapsedMinute,
                    memo = transaction.Memo,
                    lines = transaction.Lines.Select(line => new LedgerLineSaveDto
                    {
                        accountCode = line.AccountCode,
                        debitWon = line.DebitWon,
                        creditWon = line.CreditWon
                    }).ToList()
                }).ToList(),
                contracts = state.Contracts.Contracts.Select(contract => new SubcontractSaveDto
                {
                    offerId = contract.Offer.OfferId,
                    clientCompanyId = contract.Offer.ClientCompanyId,
                    exactClientDisplayName = contract.Offer.ExactClientDisplayName,
                    serviceType = (int)contract.Offer.ServiceType,
                    title = contract.Offer.Title,
                    requiredWorkers = contract.Offer.RequiredWorkers,
                    estimatedPersonHours = contract.Offer.EstimatedPersonHours,
                    deadlineDays = contract.Offer.DeadlineDays,
                    upfrontCostWon = contract.Offer.UpfrontCostWon,
                    rewardWon = contract.Offer.RewardWon,
                    reputationRequired = contract.Offer.ReputationRequired,
                    penaltyWon = contract.Offer.PenaltyWon,
                    requiredDevelopment = contract.Offer.RequiredDevelopment,
                    requiredSpeed = contract.Offer.RequiredSpeed,
                    requiredTechnologyId = contract.Offer.RequiredTechnologyId,
                    industry = (int)contract.Offer.Industry,
                    acceptedMinute = contract.AcceptedMinute,
                    status = (int)contract.Status,
                    completedPersonHours = contract.CompletedPersonHours,
                    resolvedMinute = contract.ResolvedMinute,
                    contributions = contract.Contributions.Select(item => new ContractWorkerContributionSaveDto
                    {
                        memberId = item.MemberId,
                        personHours = item.PersonHours
                    }).ToList()
                }).ToList(),
                growth = new CompanyGrowthSaveDto
                {
                    researchCenterUnlocked = state.Growth.ResearchCenterUnlocked,
                    researchedTechnologyIds = state.Growth.ResearchedTechnologyIds.ToList(),
                    marketReportSequence = state.Growth.MarketReportSequence,
                    productSequence = state.Growth.ProductSequence,
                    hasMarketReport = state.Growth.MarketReport != null,
                    hasProductProject = state.Growth.ProductProject != null,
                    marketReport = state.Growth.MarketReport == null ? null : new MarketReportSaveDto
                    {
                        genre = state.Growth.MarketReport.Genre,
                        desiredFeature = state.Growth.MarketReport.DesiredFeature,
                        demand = state.Growth.MarketReport.Demand,
                        purchasedMinute = state.Growth.MarketReport.PurchasedMinute,
                        industry = (int)state.Growth.MarketReport.Industry
                    },
                    productProject = state.Growth.ProductProject == null ? null : new ProductProjectSaveDto
                    {
                        sequence = state.Growth.ProductProject.Sequence,
                        title = state.Growth.ProductProject.Title,
                        targetGenre = state.Growth.ProductProject.TargetGenre,
                        targetFeature = state.Growth.ProductProject.TargetFeature,
                        budgetWon = state.Growth.ProductProject.BudgetWon,
                        startedMinute = state.Growth.ProductProject.StartedMinute,
                        dueMinute = state.Growth.ProductProject.DueMinute,
                        resolved = state.Growth.ProductProject.Resolved,
                        quality = state.Growth.ProductProject.Quality,
                        revenueWon = state.Growth.ProductProject.RevenueWon,
                        industry = (int)state.Growth.ProductProject.Industry
                    },
                    ownedBusinesses = state.Growth.OwnedBusinesses.Select(item => new OwnedBusinessSaveDto
                    {
                        industry = (int)item.Industry,
                        businessName = item.BusinessName,
                        foundedMinute = item.FoundedMinute,
                        foundingInvestmentWon = item.FoundingInvestmentWon,
                        totalRevenueWon = item.TotalRevenueWon,
                        launchedProductCount = item.LaunchedProductCount
                    }).ToList()
                }
            };
        }

        public static GameState FromDto(GameSaveDto save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.schemaVersion != 1 && save.schemaVersion != 2 && save.schemaVersion != 3 && save.schemaVersion != 4 && save.schemaVersion != 5)
            {
                throw new InvalidOperationException($"Unsupported save schema: {save.schemaVersion}");
            }
            if (save.company == null || save.family == null || save.events == null || save.ledger == null)
            {
                throw new InvalidOperationException("Save data is incomplete.");
            }

            var ledger = save.ledger.Select(transaction => new LedgerTransaction(
                transaction.transactionId,
                transaction.elapsedMinute,
                transaction.memo,
                transaction.lines.Select(line => new LedgerLine(line.accountCode, line.debitWon, line.creditWon))));
            var company = new CompanyState(save.company.companyName, save.company.cashWon, save.company.reputation, ledger);
            var family = new FamilyState(save.family.Select(member =>
            {
                var role = (FamilyRole)member.role;
                var stats = save.schemaVersion >= 3
                    ? new EmployeeStats(
                        member.development,
                        member.speed,
                        member.stamina,
                        member.planning,
                        member.art,
                        member.sales,
                        member.mental,
                        member.teamwork,
                        member.loyalty,
                        member.potential)
                    : EmployeeStats.StarterFor(role);
                var careerMemories = save.schemaVersion >= 4
                    ? (member.careerMemories ?? throw new InvalidOperationException("Career memory data is incomplete."))
                        .Select(memory => new CareerMemoryState(
                            memory.memoryId,
                            (BusinessIndustry)memory.industry,
                            (CareerMemoryKind)memory.kind,
                            memory.summary,
                            memory.occurredMinute,
                            memory.bondDelta,
                            memory.colleagueMemberIds))
                    : Enumerable.Empty<CareerMemoryState>();
                var autonomy = save.schemaVersion >= 5
                    ? member.autonomy == null
                        ? throw new InvalidOperationException("Office autonomy data is incomplete.")
                        : new OfficeAutonomyState(
                            (AutonomousOfficeAction)member.autonomy.currentAction,
                            (OfficeSemanticLocation)member.autonomy.targetLocation,
                            member.autonomy.actionStartedMinute,
                            member.autonomy.actionEndsMinute,
                            member.autonomy.lastProcessedMinute,
                            member.autonomy.completedWorkBlocks,
                            member.autonomy.completedBreaks,
                            member.autonomy.burnoutCount,
                            member.autonomy.lastIncidentSummary,
                            member.autonomy.lastIncidentMinute,
                            member.autonomy.lastSocialEventDay)
                    : new OfficeAutonomyState(lastProcessedMinute: save.elapsedMinutes);
                return new FamilyMemberState(
                    member.memberId,
                    member.displayName,
                    role,
                    new DateTime(member.birthYear, member.birthMonth, member.birthDay),
                    member.companyDuty,
                    member.energy,
                    member.trust,
                    member.stress,
                    stats,
                    careerMemories,
                    autonomy);
            }));
            var events = new DeterministicEventQueue(save.events.Select(item => new ScheduledEvent(
                item.eventId,
                item.dueMinute,
                item.priority,
                item.kind,
                item.payload)));
            var contractDtos = save.schemaVersion >= 2 && save.contracts != null
                ? save.contracts
                : new System.Collections.Generic.List<SubcontractSaveDto>();
            var contracts = new ContractPortfolio(family.Members.Count, contractDtos.Select(item =>
            {
                if (item.contributions == null) throw new InvalidOperationException("Contract contribution data is incomplete.");
                var offer = new SubcontractOffer(
                    item.offerId,
                    item.clientCompanyId,
                    item.exactClientDisplayName,
                    (ContractServiceType)item.serviceType,
                    item.title,
                    item.requiredWorkers,
                    item.estimatedPersonHours,
                    item.deadlineDays,
                    item.upfrontCostWon,
                    item.rewardWon,
                    item.reputationRequired,
                    item.penaltyWon,
                    item.requiredDevelopment,
                    item.requiredSpeed,
                    item.requiredTechnologyId,
                    save.schemaVersion >= 4 ? (BusinessIndustry)item.industry : BusinessIndustry.WebAndSoftware);
                return new SubcontractState(
                    offer,
                    item.acceptedMinute,
                    (SubcontractStatus)item.status,
                    item.completedPersonHours,
                    item.resolvedMinute,
                    item.contributions.Select(contribution => new ContractWorkerContribution(
                        contribution.memberId,
                        contribution.personHours)));
            }));
            CompanyGrowthState growth;
            if (save.schemaVersion < 3)
            {
                growth = new CompanyGrowthState();
            }
            else
            {
                if (save.growth == null || save.growth.researchedTechnologyIds == null)
                {
                    throw new InvalidOperationException("Growth data is incomplete.");
                }
                var hasMarketReport = save.growth.marketReport != null
                                      && (save.growth.hasMarketReport
                                          || !string.IsNullOrWhiteSpace(save.growth.marketReport.genre)
                                          || !string.IsNullOrWhiteSpace(save.growth.marketReport.desiredFeature));
                var marketReport = !hasMarketReport ? null : new MarketReportState(
                    save.growth.marketReport.genre,
                    save.growth.marketReport.desiredFeature,
                    save.growth.marketReport.demand,
                    save.growth.marketReport.purchasedMinute,
                    save.schemaVersion >= 4
                        ? (BusinessIndustry)save.growth.marketReport.industry
                        : BusinessIndustry.WebAndSoftware);
                var hasProductProject = save.growth.productProject != null
                                        && (save.growth.hasProductProject
                                            || !string.IsNullOrWhiteSpace(save.growth.productProject.title));
                var product = !hasProductProject ? null : new ProductProjectState(
                    save.growth.productProject.sequence,
                    save.growth.productProject.title,
                    save.growth.productProject.targetGenre,
                    save.growth.productProject.targetFeature,
                    save.growth.productProject.budgetWon,
                    save.growth.productProject.startedMinute,
                    save.growth.productProject.dueMinute,
                    save.growth.productProject.resolved,
                    save.growth.productProject.quality,
                    save.growth.productProject.revenueWon,
                    save.schemaVersion >= 4
                        ? (BusinessIndustry)save.growth.productProject.industry
                        : BusinessIndustry.WebAndSoftware);
                var ownedBusinesses = save.schemaVersion >= 4
                    ? (save.growth.ownedBusinesses ?? throw new InvalidOperationException("Owned business data is incomplete."))
                        .Select(item => new OwnedBusinessState(
                            (BusinessIndustry)item.industry,
                            item.businessName,
                            item.foundedMinute,
                            item.foundingInvestmentWon,
                            item.totalRevenueWon,
                            item.launchedProductCount))
                    : Enumerable.Empty<OwnedBusinessState>();
                growth = new CompanyGrowthState(
                    save.growth.researchCenterUnlocked,
                    save.growth.researchedTechnologyIds,
                    marketReport,
                    product,
                    save.growth.marketReportSequence,
                    save.growth.productSequence,
                    ownedBusinesses);
            }
            return new GameState(save.worldSeed, new GameTime(save.elapsedMinutes), family, company, events, contracts, growth);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Finance;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Market;
using FamilyCompany.Save.OfficeGrid;
using FamilyCompany.Save.OfficeFurniture;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Stamina;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Save
{
    public static class GameSaveMapper
    {
        private static StarterProductSaveDto ToStarterProductDto(StarterProductState p) => new StarterProductSaveDto
        {
            phase = (int)p.Phase, developmentAttempt = p.DevelopmentAttempt,
            developmentOrderId = p.DevelopmentOrderId, quality = p.Quality,
            customers = p.Customers, satisfaction = p.Satisfaction, nextBillingMinute = p.NextBillingMinute,
            billingPeriod = p.BillingPeriod, maintenanceOrderId = p.MaintenanceOrderId,
            totalRevenueWon = p.TotalRevenueWon, lastPeriodRevenueWon = p.LastPeriodRevenueWon,
            missedPeriods = p.MissedPeriods
        };

        private static StarterProductState FromStarterProductDto(StarterProductSaveDto p)
        {
            if (p == null) throw new InvalidOperationException("Starter product state is incomplete.");
            return new StarterProductState((StarterProductPhase)p.phase, p.developmentAttempt,
                p.developmentOrderId, p.quality, p.customers, p.satisfaction, p.nextBillingMinute,
                p.billingPeriod, p.maintenanceOrderId, p.totalRevenueWon, p.lastPeriodRevenueWon, p.missedPeriods);
        }

        public static GameSaveDto ToDto(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.RefreshLegacyEnergyProjection();
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
                    planning = member.Stats.Planning,
                    art = member.Stats.Art,
                    sales = member.Stats.Sales,
                    teamwork = member.Stats.Teamwork,
                    loyalty = member.Stats.Loyalty,
                    potential = member.Stats.Potential,
                    capability = member.Capability.ExportSnapshot(),
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
                        lastSocialEventDay = member.Autonomy.LastSocialEventDay,
                        microAction = new OfficeMicroActionSaveDto
                        {
                            action = (int)member.Autonomy.MicroAction.Action,
                            targetId = member.Autonomy.MicroAction.TargetId,
                            targetLocation = (int)member.Autonomy.MicroAction.TargetLocation,
                            startedMinute = member.Autonomy.MicroAction.StartedMinute,
                            endsMinute = member.Autonomy.MicroAction.EndsMinute,
                            sequenceIndex = member.Autonomy.MicroAction.SequenceIndex,
                            partnerMemberId = member.Autonomy.MicroAction.PartnerMemberId,
                            macroActionStartedMinute = member.Autonomy.MicroAction.MacroActionStartedMinute,
                            lastAction = (int)member.Autonomy.MicroAction.LastAction,
                            lastTargetId = member.Autonomy.MicroAction.LastTargetId,
                            lastTargetEndedMinute = member.Autonomy.MicroAction.LastTargetEndedMinute,
                            lastWaterStartedMinute = member.Autonomy.MicroAction.LastWaterStartedMinute,
                            lastCoffeeStartedMinute = member.Autonomy.MicroAction.LastCoffeeStartedMinute,
                            lastConversationStartedMinute = member.Autonomy.MicroAction.LastConversationStartedMinute,
                            lastConversationPartnerId = member.Autonomy.MicroAction.LastConversationPartnerId,
                            deskResidenceStartedMinute = member.Autonomy.MicroAction.DeskResidenceStartedMinute,
                            visitedLocationMask = member.Autonomy.MicroAction.VisitedLocationMask
                        }
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
                    workPurpose = (int)contract.Offer.Purpose,
                    workDueMinute = contract.DueMinute,
                    workRateBasisPoints = contract.WorkRateBasisPoints,
                    qualityBonus = contract.QualityBonus,
                    resolvedQuality = contract.ResolvedQuality,
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
                    requiredCapability = contract.Offer.RequiredCapability,
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
                    starterProduct = ToStarterProductDto(state.Growth.StarterProduct),
                    researchCenterUnlocked = state.Growth.ResearchCenterUnlocked,
                    researchedTechnologyIds = state.Growth.ResearchedTechnologyIds.ToList(),
                    marketReportSequence = state.Growth.MarketReportSequence,
                    productSequence = state.Growth.ProductSequence,
                    technologyPoints = state.Growth.Technology.Snapshot()
                        .Select(item => new TechnologyPointsSaveDto
                        {
                            technologyId = item.Key,
                            points = item.Value
                        }).ToList(),
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
                },
                stockMarket = ToStockMarketSaveDto(state.StockMarket),
                officeGrid = OfficeGridSaveAdapter.ToDto(state.OfficeGrid),
                officeFurnitureInventory = OfficeFurnitureInventorySaveAdapter.ToDto(state.OfficeFurnitureInventory),
                staminaState = state.Stamina.ExportSnapshot()
            };
        }

        public static GameState FromDto(GameSaveDto save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.schemaVersion < 1 || save.schemaVersion > 12)
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
                            member.autonomy.lastSocialEventDay,
                            save.schemaVersion >= 7 && member.autonomy.microAction != null
                                ? new OfficeMicroActionState(
                                    (OfficeMicroAction)member.autonomy.microAction.action,
                                    member.autonomy.microAction.targetId,
                                    (OfficeSemanticLocation)member.autonomy.microAction.targetLocation,
                                    member.autonomy.microAction.startedMinute,
                                    member.autonomy.microAction.endsMinute,
                                    member.autonomy.microAction.sequenceIndex,
                                    member.autonomy.microAction.partnerMemberId,
                                    member.autonomy.microAction.macroActionStartedMinute,
                                    (OfficeMicroAction)member.autonomy.microAction.lastAction,
                                    member.autonomy.microAction.lastTargetId,
                                    member.autonomy.microAction.lastTargetEndedMinute,
                                    member.autonomy.microAction.lastWaterStartedMinute,
                                    member.autonomy.microAction.lastCoffeeStartedMinute,
                                    member.autonomy.microAction.lastConversationStartedMinute,
                                    member.autonomy.microAction.lastConversationPartnerId,
                                    member.autonomy.microAction.deskResidenceStartedMinute,
                                    member.autonomy.microAction.visitedLocationMask)
                                : new OfficeMicroActionState())
                    : new OfficeAutonomyState(lastProcessedMinute: save.elapsedMinutes);
                var capability = save.schemaVersion >= 10
                    ? WorkforceCapabilityState.ImportSnapshot(
                        member.capability ?? throw new InvalidOperationException("Workforce capability data is incomplete."),
                        member.memberId)
                    : LegacyWorkforceCapabilityMigration.Migrate(member.memberId, stats);
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
                    autonomy,
                    capability);
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
                    save.schemaVersion >= 4 ? (BusinessIndustry)item.industry : BusinessIndustry.WebAndSoftware,
                    save.schemaVersion >= 10 ? item.requiredCapability : item.requiredDevelopment,
                    save.schemaVersion >= 12 ? (CompanyWorkPurpose)item.workPurpose : CompanyWorkPurpose.Subcontract);
                return new SubcontractState(
                    offer,
                    item.acceptedMinute,
                    (SubcontractStatus)item.status,
                    item.completedPersonHours,
                    item.resolvedMinute,
                    item.contributions.Select(contribution => new ContractWorkerContribution(
                        contribution.memberId,
                        contribution.personHours)),
                    save.schemaVersion >= 12 ? item.workRateBasisPoints : 10000,
                    save.schemaVersion >= 12 ? item.qualityBonus : 0,
                    save.schemaVersion >= 12 ? item.resolvedQuality : -1,
                    save.schemaVersion >= 12 ? item.workDueMinute : -1);
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
                // v10 and older have no technology ledger; those companies simply start with no
                // accumulated know-how and rebuild it by taking contracts.
                var technologyPoints = save.schemaVersion >= 11 && save.growth.technologyPoints != null
                    ? save.growth.technologyPoints
                        .Where(item => item != null && !string.IsNullOrEmpty(item.technologyId))
                        .Select(item => new KeyValuePair<string, int>(item.technologyId, item.points))
                    : Enumerable.Empty<KeyValuePair<string, int>>();
                growth = new CompanyGrowthState(
                    save.growth.researchCenterUnlocked,
                    save.growth.researchedTechnologyIds,
                    marketReport,
                    product,
                    save.growth.marketReportSequence,
                    save.growth.productSequence,
                    ownedBusinesses,
                    technologyPoints,
                    save.schemaVersion >= 12 ? FromStarterProductDto(save.growth.starterProduct) : null);
            }
            growth.StarterProduct.ValidateOrders(contracts);
            var stockMarket = save.stockMarket != null && save.stockMarket.initialized
                ? FromStockMarketSaveDto(save.stockMarket)
                : StockMarketSessionStateDto.Uninitialized();
            var officeGrid = save.schemaVersion >= 6
                ? save.officeGrid == null
                    ? throw new InvalidOperationException("Office grid data is incomplete.")
                    : OfficeGridSaveAdapter.Restore(save.officeGrid)
                : OfficeGridLayouts.CreateStarterOfficeV1();
            var officeFurnitureInventory = save.schemaVersion >= 8
                ? save.officeFurnitureInventory == null
                    ? throw new InvalidOperationException("Office furniture inventory data is incomplete.")
                    : OfficeFurnitureInventorySaveAdapter.Restore(save.officeFurnitureInventory)
                : OfficeFurnitureInventoryState.MigrateFromGrid(officeGrid, save.elapsedMinutes);
            CharacterStaminaCatalog staminaCatalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaRoster stamina = save.schemaVersion >= 9
                ? save.staminaState == null
                    ? throw new InvalidOperationException("Stamina state is incomplete.")
                    : CharacterStaminaRoster.Restore(save.staminaState, staminaCatalog)
                : CharacterStaminaRoster.MigrateLegacyEnergyPercents(
                    save.worldSeed,
                    staminaCatalog,
                    family.Members.Select(member =>
                        new System.Collections.Generic.KeyValuePair<string, int>(
                            member.MemberId,
                            member.Energy)),
                    save.elapsedMinutes);
            return new GameState(
                save.worldSeed,
                new GameTime(save.elapsedMinutes),
                family,
                company,
                events,
                contracts,
                growth,
                stockMarket,
                officeGrid,
                officeFurnitureInventory,
                stamina);
        }

        private static StockMarketSessionSaveDto ToStockMarketSaveDto(StockMarketSessionStateDto state)
        {
            if (state == null) throw new InvalidOperationException("Stock market state is missing.");
            var brokerage = state.Brokerage;
            return new StockMarketSessionSaveDto
            {
                schemaVersion = state.SchemaVersion,
                initialized = state.Initialized,
                dateTicks = state.Date.Ticks,
                marketMinute = state.MarketMinute,
                realtimeResidualSeconds = state.RealtimeResidualSeconds,
                playbackIndex = state.PlaybackIndex,
                openingAuctionProcessed = state.OpeningAuctionProcessed,
                openingAuctionProcessCount = state.OpeningAuctionProcessCount,
                canonicalMinuteUpdateCount = state.CanonicalMinuteUpdateCount,
                liquidityPulse = state.LiquidityPulse,
                brokerageCashWon = brokerage.CashWon,
                orderSequence = brokerage.OrderSequence,
                journalSequence = brokerage.JournalSequence,
                positions = brokerage.Positions.Select(item => new BrokeragePositionSaveDto
                {
                    assetId = item.AssetId,
                    units = item.Units,
                    averageCostWon = item.AverageCostWon
                }).ToList(),
                pendingOrders = brokerage.PendingOrders.Select(item => new BrokeragePendingOrderSaveDto
                {
                    id = item.Id,
                    side = (int)item.Side,
                    assetId = item.AssetId,
                    limitPrice = item.LimitPrice,
                    originalQuantity = item.OriginalQuantity,
                    remainingQuantity = item.RemainingQuantity,
                    placedDateTicks = item.PlacedDate.Ticks,
                    placedMinute = item.PlacedMinute,
                    placedSequence = item.PlacedSequence,
                    queueAheadQuantity = item.QueueAheadQuantity,
                    hasMaximumPositionUnits = item.MaximumPositionUnits.HasValue,
                    maximumPositionUnits = item.MaximumPositionUnits.GetValueOrDefault(),
                    isIpoFirstTradingDay = item.IsIpoFirstTradingDay
                }).ToList(),
                playerTrades = brokerage.PlayerTrades.Select(item => new BrokerageTradeSaveDto
                {
                    assetId = item.AssetId,
                    marketMinute = item.MarketMinute,
                    liquidityPulse = item.LiquidityPulse,
                    price = item.Price,
                    quantity = item.Quantity,
                    isBuy = item.IsBuy
                }).ToList(),
                orderJournal = brokerage.OrderJournal.Select(item => new BrokerageOrderJournalSaveDto
                {
                    sequence = item.Sequence,
                    assetId = item.AssetId,
                    marketMinute = item.MarketMinute,
                    isBuy = item.IsBuy,
                    isMarket = item.IsMarket,
                    limitPrice = item.LimitPrice,
                    requestedQuantity = item.RequestedQuantity,
                    filledQuantity = item.FilledQuantity,
                    remainingQuantity = item.RemainingQuantity,
                    averagePrice = item.AveragePrice
                }).ToList(),
                favoriteAssetIds = brokerage.FavoriteAssetIds.ToList()
            };
        }

        private static StockMarketSessionStateDto FromStockMarketSaveDto(StockMarketSessionSaveDto dto)
        {
            if (dto != null && !dto.initialized) return StockMarketSessionStateDto.Uninitialized();
            if (dto == null || dto.positions == null || dto.pendingOrders == null ||
                dto.playerTrades == null || dto.orderJournal == null || dto.favoriteAssetIds == null)
                throw new InvalidOperationException("Stock market save data is incomplete.");
            if (dto.schemaVersion != StockMarketSessionStateDto.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported stock market save schema: {dto.schemaVersion}");

            var brokerage = new BrokerageAccountStateDto(
                dto.brokerageCashWon,
                dto.positions.Select(item => new BrokeragePositionStateDto(
                    item.assetId,
                    item.units,
                    item.averageCostWon)),
                dto.pendingOrders.Select(item => new BrokeragePendingOrderStateDto(new MarketPendingOrder(
                    item.id,
                    (MarketPendingOrderSide)item.side,
                    item.assetId,
                    item.limitPrice,
                    item.originalQuantity,
                    item.remainingQuantity,
                    new DateTime(item.placedDateTicks, DateTimeKind.Unspecified),
                    item.placedMinute,
                    item.placedSequence,
                    item.queueAheadQuantity,
                    item.hasMaximumPositionUnits ? item.maximumPositionUnits : (int?)null,
                    item.isIpoFirstTradingDay))),
                dto.playerTrades.Select(item => new BrokerageTradeStateDto(new StockMarketTradePrint(
                    item.assetId,
                    item.marketMinute,
                    item.liquidityPulse,
                    item.price,
                    item.quantity,
                    item.isBuy,
                    true))),
                dto.orderJournal.Select(item => new BrokerageOrderJournalStateDto(new StockMarketOrderJournalEntry(
                    item.sequence,
                    item.assetId,
                    item.marketMinute,
                    item.isBuy,
                    item.isMarket,
                    item.limitPrice,
                    item.requestedQuantity,
                    item.filledQuantity,
                    item.remainingQuantity,
                    item.averagePrice))),
                dto.favoriteAssetIds,
                dto.orderSequence,
                dto.journalSequence);
            return new StockMarketSessionStateDto(
                dto.initialized,
                new DateTime(dto.dateTicks, DateTimeKind.Unspecified),
                dto.marketMinute,
                dto.realtimeResidualSeconds,
                dto.playbackIndex,
                dto.openingAuctionProcessed,
                dto.openingAuctionProcessCount,
                dto.canonicalMinuteUpdateCount,
                dto.liquidityPulse,
                brokerage,
                dto.schemaVersion);
        }
    }
}

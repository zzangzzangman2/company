using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.History;
using FamilyCompany.Simulation.Market;
using FamilyCompany.Simulation.Prototype;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FamilyCompany.Editor
{
    public static class StockMarketLandscapeValidation
    {
#if UNITY_EDITOR
        [MenuItem("Family Company/Validate Stock Market Landscape UI")]
#endif
        public static void Run()
        {
            AssertEqual(10, MarketOrderBookRules.LevelCount, "internal order-book depth");
            AssertEqual(7, MarketOrderBookPresentationRules.VisibleRowsPerSide, "visible rows per side");
            AssertEqual(7, MarketOrderBookReplayQueue.VisibleRowsPerSide, "replay visible rows per side");
            AssertSequence(new long[] { 10, 20, 50, 100 }, StockMarketQuantityShortcutRules.Percentages.Select(value => (long)value), "canonical quantity shortcuts");
            AssertEqual(2, StockMarketQuantityShortcutRules.QuantityFor(23, 10), "10 percent shortcut floor");
            AssertEqual(4, StockMarketQuantityShortcutRules.QuantityFor(23, 20), "20 percent shortcut floor");
            AssertEqual(11, StockMarketQuantityShortcutRules.QuantityFor(23, 50), "50 percent shortcut floor");
            AssertEqual(23, StockMarketQuantityShortcutRules.QuantityFor(23, 100), "100 percent shortcut exact");
            ValidateRealtimeClock();
            ValidateOpeningBoundary();
            AssertEqual(144_000L, MarketOrderBookReplayQueue.BaseMotionMicroseconds, "base quote motion");
            AssertEqual(480_000L, MarketOrderBookReplayQueue.TotalSweepMicroseconds, "sweep duration");

            ValidateViewport(1280, 720, 1920f);
            ValidateViewport(1600, 900, 1920f);
            ValidateViewport(1920, 1080, 1920f);
            ValidateViewport(2560, 1080, 2560f);
            ValidateViewport(3440, 1440, 2580f);
            ValidateViewport(1920, 1200, 1728f);
            if (!StockMarketLandscapeLayout.RequiresMinimumSizeNotice(568, 843) ||
                StockMarketLandscapeLayout.RequiresMinimumSizeNotice(1280, 720))
                throw new InvalidOperationException("Minimum-size stock UI guard does not distinguish 568px from supported desktop widths.");

            var asks = new List<MarketOrderBookLevel>
            {
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 13450, 100),
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 13500, 200),
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 13550, 300),
            };
            var bids = new List<MarketOrderBookLevel>
            {
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 13350, 5586),
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 13300, 744),
            };
            var snapshot = new MarketOrderBookSnapshot(asks, bids, 100d, 100d, 10000);
            var levels = MarketOrderBookPresentationRules.BuildVisibleLevels(
                snapshot,
                "미래시장",
                sideRowCount: 3);
            AssertSequence(
                new long[] { 13550, 13500, 13450, 13400, 13350, 13300 },
                levels.Select(level => level.Price),
                "continuous 3+3 price ladder");
            AssertEqual(0, levels.Single(level => level.Price == 13400).Quantity, "missing tick is presentation-only zero depth");

            var central = MarketOrderBookPresentationRules.CentralOutlineLevel(
                levels,
                13550,
                MarketOrderBookSide.Ask);
            AssertEqual(13450L, central?.Price ?? 0L, "idle outline clamps to best ask");

            var carryLevels = new List<MarketOrderBookLevel>
            {
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 101, 30),
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 100, 40),
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 99, 50),
            };
            central = MarketOrderBookPresentationRules.CentralOutlineLevel(
                carryLevels,
                100,
                MarketOrderBookSide.Ask);
            AssertEqual(100L, central?.Price ?? 0L, "consumed ask carries outline to same-price bid");

            ValidateLiveRuntimeSession();
            ValidateCompanyBrokerageSaveRoundTrip();

            Console.WriteLine("STOCK_MARKET_LANDSCAPE_VALIDATION: PASS");
        }

        private static void ValidateCompanyBrokerageSaveRoundTrip()
        {
            var state = PrototypeStateFactory.Create(99173);
            var security = new MarketSecurityDefinition(
                "brokerage-save-company",
                "증권저장검증회사",
                "KOSPI",
                "000003",
                new DateTime(1990, 1, 1),
                null,
                false);
            var date = new DateTime(2000, 1, 4);
            var emptyBinding = StockMarketGameStateBridge.Load(
                state,
                date,
                new[] { security },
                MarketSessionClock.DayStartMinute);
            AssertEqual(0L, emptyBinding.Session.BrokerageCash, "new company brokerage starts empty");
            if (emptyBinding.RestoredFullSession)
                throw new InvalidOperationException("Uninitialized stock state was reported as a full restore.");

            var session = new StockMarketRuntimeSession(
                state.WorldSeed,
                date,
                0,
                new[] { security },
                MarketSessionClock.OpenMinute - 1);
            var transfers = new CompanyBrokerageTransferService(state.Company, session);
            var companyCashBefore = state.Company.CashWon;
            var deposit = transfers.Deposit("brokerage-deposit-1", state.Time.ElapsedMinutes, 200_000);
            if (!deposit.Accepted || state.Company.CashWon != companyCashBefore - 200_000 ||
                session.BrokerageCash != 200_000)
                throw new InvalidOperationException("Company-to-brokerage deposit did not conserve cash.");
            var depositLedger = state.Company.Ledger.Last();
            AssertEqual(depositLedger.TotalDebitWon, depositLedger.TotalCreditWon, "brokerage deposit balanced ledger");
            if (depositLedger.Lines.All(line => line.AccountCode != Simulation.Finance.AccountCode.BrokerageAccount))
                throw new InvalidOperationException("Brokerage deposit did not use the brokerage account ledger code.");

            if (transfers.Deposit("brokerage-deposit-negative", 0, -1).Accepted ||
                transfers.Deposit("brokerage-deposit-1", 0, 1).Accepted ||
                transfers.Deposit("brokerage-deposit-excess", 0, state.Company.CashWon + 1).Accepted)
                throw new InvalidOperationException("Invalid brokerage deposit was accepted.");

            session.AdvanceMinutes(1, 1);
            var openingProcessCount = session.OpeningAuctionProcessCount;
            var view = session.ViewFor(security.CompanyId);
            var buy = session.PlaceOrder(
                security.CompanyId,
                true,
                true,
                view.LastTradePrice,
                1);
            if (!buy.Accepted || buy.FilledQuantity != 1 ||
                session.AverageCost(security.CompanyId) <= buy.AveragePrice)
                throw new InvalidOperationException("Fee-inclusive brokerage position setup failed.");
            var range = MarketPricingRules.DailyPriceRange(
                view.PreviousClose,
                date,
                security.PriceRuleMarket);
            var pending = session.PlaceOrder(
                security.CompanyId,
                true,
                false,
                range.Lower,
                1);
            if (!pending.Accepted || pending.RemainingQuantity != 1)
                throw new InvalidOperationException("Pending brokerage order setup failed.");

            var unavailableWithdrawal = checked(session.AvailableBrokerageCash + 1);
            if (transfers.Withdraw("brokerage-withdraw-reserved", 0, unavailableWithdrawal).Accepted)
                throw new InvalidOperationException("Reserved buy cash was withdrawable.");
            var withdrawal = transfers.Withdraw("brokerage-withdraw-1", 0, 10_000);
            if (!withdrawal.Accepted || state.Company.CashWon != companyCashBefore - 190_000)
                throw new InvalidOperationException("Brokerage-to-company withdrawal did not conserve cash.");
            AssertEqual(
                state.Company.Ledger.Last().TotalDebitWon,
                state.Company.Ledger.Last().TotalCreditWon,
                "brokerage withdrawal balanced ledger");

            StockMarketGameStateBridge.Flush(state, session, 0.4d, 2);
            var sourceStock = state.StockMarket;
            var restoredState = GameSaveMapper.FromDto(GameSaveMapper.ToDto(state));
            AssertEqual(state.Company.CashWon, restoredState.Company.CashWon, "company cash save round trip");
            AssertEqual(state.Company.Ledger.Count, restoredState.Company.Ledger.Count, "brokerage ledger save round trip");
            AssertEqual(sourceStock.Brokerage.CashWon, restoredState.StockMarket.Brokerage.CashWon, "brokerage cash save round trip");
            AssertEqual(sourceStock.Brokerage.Positions.Count, restoredState.StockMarket.Brokerage.Positions.Count, "brokerage positions save round trip");
            AssertEqual(sourceStock.Brokerage.PendingOrders.Count, restoredState.StockMarket.Brokerage.PendingOrders.Count, "brokerage pending save round trip");
            AssertEqual(sourceStock.Brokerage.PlayerTrades.Count, restoredState.StockMarket.Brokerage.PlayerTrades.Count, "brokerage trades save round trip");
            AssertEqual(sourceStock.MarketMinute, restoredState.StockMarket.MarketMinute, "stock clock save round trip");
            AssertEqual(sourceStock.OpeningAuctionProcessCount, restoredState.StockMarket.OpeningAuctionProcessCount, "opening idempotency counter save round trip");

            var restoredBinding = StockMarketGameStateBridge.Load(
                restoredState,
                date,
                new[] { security },
                restoredState.StockMarket.MarketMinute);
            var restoredSession = restoredBinding.Session;
            if (!restoredBinding.RestoredFullSession || restoredBinding.PlaybackIndex != 2 ||
                Math.Abs(restoredBinding.RealtimeResidualSeconds - 0.4d) > 0.000001d)
                throw new InvalidOperationException("GameState bridge did not restore the full same-day session.");
            restoredSession.SetMarketMinute(MarketSessionClock.OpenMinute);
            AssertEqual(openingProcessCount, restoredSession.OpeningAuctionProcessCount, "opening auction remained idempotent after load");
            AssertEqual(session.BrokerageCash, restoredSession.BrokerageCash, "live brokerage cash after load");
            AssertEqual(session.PositionUnits(security.CompanyId), restoredSession.PositionUnits(security.CompanyId), "live brokerage position after load");
            AssertEqual(session.PendingOrders.Count, restoredSession.PendingOrders.Count, "live brokerage pending after load");
            AssertEqual(session.PlayerTradeHistory().Count, restoredSession.PlayerTradeHistory().Count, "live brokerage trades after load");
            var restoredClock = new StockMarketRealtimeClock();
            restoredClock.Restore(restoredState.StockMarket.RealtimeResidualSeconds);
            if (Math.Abs(restoredClock.AccumulatedSeconds - 0.4d) > 0.000001d)
                throw new InvalidOperationException("Realtime stock clock residual was not restored.");

            var nextDateBinding = StockMarketGameStateBridge.Load(
                restoredState,
                date.AddDays(1),
                new[] { security },
                MarketSessionClock.DayStartMinute);
            if (nextDateBinding.RestoredFullSession || nextDateBinding.RealtimeResidualSeconds != 0d)
                throw new InvalidOperationException("Changed trading date incorrectly restored stale session timing.");
            AssertEqual(restoredSession.BrokerageCash, nextDateBinding.Session.BrokerageCash, "next-date brokerage cash carry");
            AssertEqual(restoredSession.PendingOrders.Count, nextDateBinding.Session.PendingOrders.Count, "next-date pending-order carry");

            var automaticIdState = PrototypeStateFactory.Create(99174);
            var automaticIdSession = new StockMarketRuntimeSession(
                automaticIdState.WorldSeed,
                date,
                0L,
                new[] { security });
            var automaticTransfers = new CompanyBrokerageTransferService(
                automaticIdState.Company,
                automaticIdSession);
            if (!automaticTransfers.Deposit(automaticIdState.Time.ElapsedMinutes, 1).Accepted ||
                !automaticTransfers.Deposit(automaticIdState.Time.ElapsedMinutes, 1).Accepted)
                throw new InvalidOperationException("Automatic stock transfer IDs rejected valid same-minute transfers.");
            var automaticIds = automaticIdState.Company.Ledger
                .Where(item => item.TransactionId.StartsWith("stock-transfer-deposit-", StringComparison.Ordinal))
                .Select(item => item.TransactionId)
                .ToArray();
            if (automaticIds.Length != 2 || automaticIds.Distinct(StringComparer.Ordinal).Count() != 2)
                throw new InvalidOperationException("Automatic stock transfer IDs were not unique.");
            foreach (var transaction in automaticIdState.Company.Ledger)
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "automatic transfer balanced ledger");

            var optionalV5 = GameSaveMapper.ToDto(PrototypeStateFactory.Create(99175));
            optionalV5.schemaVersion = 5;
            optionalV5.stockMarket = null;
            var optionalRestored = GameSaveMapper.FromDto(optionalV5);
            if (optionalRestored.StockMarket.Initialized || optionalRestored.StockMarket.Brokerage.CashWon != 0L)
                throw new InvalidOperationException("Save V5 without optional stockMarket did not restore safely.");
            Console.WriteLine("STOCK_MARKET_COMPANY_ACCOUNT_SAVE_VALIDATION: PASS");
        }

        private static void ValidateRealtimeClock()
        {
            var paused = new StockMarketRealtimeClock();
            AssertEqual(0, paused.Consume(12d, 0), "paused 12 real seconds");
            var normal = new StockMarketRealtimeClock();
            AssertEqual(60, normal.Consume(12d, 5), "12 real seconds at 5 game minutes per second");
            var triple = new StockMarketRealtimeClock();
            AssertEqual(180, triple.Consume(12d, 15), "12 real seconds at 15 game minutes per second");
            var tenfold = new StockMarketRealtimeClock();
            AssertEqual(600, tenfold.Consume(12d, 50), "12 real seconds at 50 game minutes per second");

            var catchUp = new StockMarketRealtimeClock();
            AssertEqual(10, catchUp.Consume(2.4d, 5), "dropped-frame catch-up whole ticks");
            if (Math.Abs(catchUp.AccumulatedSeconds - 0.4d) > 0.000001d)
                throw new InvalidOperationException("Dropped-frame residual was discarded.");
            AssertEqual(5, catchUp.Consume(0.6d, 5), "dropped-frame residual next tick");
            if (Math.Abs(catchUp.AccumulatedSeconds) > 0.000001d)
                throw new InvalidOperationException("Realtime residual did not settle at one full second.");
        }

        private static void ValidateOpeningBoundary()
        {
            var security = new MarketSecurityDefinition(
                "opening-company",
                "개장검증회사",
                "KOSPI",
                "000002",
                new DateTime(1990, 1, 1),
                null,
                false);
            var date = new DateTime(2000, 1, 4);
            var session = new StockMarketRuntimeSession(
                413,
                date,
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute - 1);
            var cashBefore = session.BrokerageCash;
            var tapeBefore = session.TradeTape.Count;
            var view = session.ViewFor(security.CompanyId);
            var range = MarketPricingRules.DailyPriceRange(
                view.PreviousClose,
                date,
                security.PriceRuleMarket);

            // 08:59:59 is represented by the final pre-open minute state. Both
            // order types are accepted, but no position/cash/tape may change.
            var cancelled = session.PlaceOrder(
                security.CompanyId,
                true,
                false,
                range.Lower,
                1);
            var openingMarket = session.PlaceOrder(
                security.CompanyId,
                true,
                true,
                0,
                1);
            if (!cancelled.Accepted || !openingMarket.Accepted ||
                cancelled.PendingOrderId == null || openingMarket.PendingOrderId == null)
                throw new InvalidOperationException("Pre-open limit/market orders were not accepted into the auction queue.");
            if (session.PositionUnits(security.CompanyId) != 0 ||
                session.BrokerageCash != cashBefore || session.TradeTape.Count != tapeBefore)
                throw new InvalidOperationException("Pre-open order changed cash, position, or tape before 09:00.");
            if (!session.CancelPendingOrder(cancelled.PendingOrderId) || session.PendingOrders.Count != 1)
                throw new InvalidOperationException("08:59:59 cancellation did not release the pre-open order.");

            session.AdvanceMinutes(1, 1); // exactly 09:00:00
            AssertEqual(MarketSessionClock.OpenMinute, session.MarketMinute, "opening boundary minute");
            AssertEqual(1, session.OpeningAuctionProcessCount, "opening auction exactly once");
            if (!session.OpeningAuctionProcessed || session.OpeningPriceFor(security.CompanyId) <= 0 ||
                session.PositionUnits(security.CompanyId) != 1 || session.BrokerageCash >= cashBefore)
                throw new InvalidOperationException("Opening call auction did not establish and settle the 09:00 price.");
            var openingTradeCount = session.OpeningTradeCountFor(security.CompanyId);
            if (openingTradeCount <= 0)
                throw new InvalidOperationException("Opening auction produced no canonical tape entry.");

            session.SetMarketMinute(MarketSessionClock.OpenMinute); // 09:00:01, same minute callback
            AssertEqual(1, session.OpeningAuctionProcessCount, "same-timestamp opening idempotence");
            AssertEqual(openingTradeCount, session.OpeningTradeCountFor(security.CompanyId), "same-timestamp opening tape idempotence");
            session.AdvanceMinutes(1, 1); // 09:01 continuous book
            AssertEqual(MarketSessionClock.OpenMinute + 1, session.MarketMinute, "09:01 continuous minute");
            AssertEqual(1, session.OpeningAuctionProcessCount, "09:01 opening remains single");
            var visible = MarketOrderBookPresentationRules.BuildVisibleLevels(
                session.ViewFor(security.CompanyId).Snapshot,
                security.PriceRuleMarket);
            AssertEqual(14, visible.Count, "09:01 top 7+7 book");
            AssertEqual(
                session.ViewFor(security.CompanyId).LastTradePrice,
                session.PriceHistoryFor(security.CompanyId, 64).Last().Price,
                "09:01 chart/current canonical endpoint");

            // A pre-owned position exercises a crossing sell in the same opening
            // call auction as a buy, preserving price/time ordering on both sides.
            var crossed = new StockMarketRuntimeSession(
                413,
                date,
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute - 1);
            var seeded = new BrokerageAccountStateDto(
                1_000_000_000,
                new[] { new BrokeragePositionStateDto(security.CompanyId, 2, view.PreviousClose) },
                Array.Empty<BrokeragePendingOrderStateDto>(),
                Array.Empty<BrokerageTradeStateDto>(),
                Array.Empty<BrokerageOrderJournalStateDto>(),
                Array.Empty<string>(),
                0,
                0);
            if (!crossed.TryApplyBrokerageState(seeded, out var seededError))
                throw new InvalidOperationException($"Opening sell setup failed: {seededError}");
            var openingBuy = crossed.PlaceOrder(security.CompanyId, true, true, 0, 1);
            var openingSell = crossed.PlaceOrder(security.CompanyId, false, true, 0, 1);
            if (!openingBuy.Accepted || !openingSell.Accepted || crossed.PendingOrders.Count != 2)
                throw new InvalidOperationException("Crossing pre-open buy/sell orders were not queued.");
            crossed.AdvanceMinutes(1, 1);
            if (crossed.PendingOrders.Count != 0 || crossed.OpeningTradeCountFor(security.CompanyId) < 2)
                throw new InvalidOperationException("Crossing opening buy/sell orders did not settle deterministically.");

            var speeds = new[] { 5, 15, 50 }
                .Select(minutes =>
                {
                    var candidate = new StockMarketRuntimeSession(
                        413,
                        date,
                        1_000_000_000,
                        new[] { security },
                        MarketSessionClock.OpenMinute - 1);
                    var order = candidate.PlaceOrder(security.CompanyId, true, true, 0, 1);
                    if (!order.Accepted) throw new InvalidOperationException("Opening speed setup order was rejected.");
                    candidate.AdvanceMinutes(minutes, minutes == 5 ? 1 : minutes == 15 ? 3 : 10);
                    return candidate;
                })
                .ToArray();
            foreach (var candidate in speeds)
            {
                AssertEqual(1, candidate.OpeningAuctionProcessCount, "boundary crossing opening count");
                AssertEqual(speeds[0].OpeningPriceFor(security.CompanyId), candidate.OpeningPriceFor(security.CompanyId), "5/15/50 opening price equivalence");
                AssertEqual(1, candidate.PositionUnits(security.CompanyId), "5/15/50 opening fill equivalence");
            }
        }

        private static void ValidateLiveRuntimeSession()
        {
            var security = new MarketSecurityDefinition(
                "runtime-company",
                "런타임회사",
                "KOSPI",
                "000001",
                new DateTime(1990, 1, 1),
                null,
                false);
            var first = new StockMarketRuntimeSession(
                771,
                new DateTime(2000, 1, 4),
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute);
            var second = new StockMarketRuntimeSession(
                771,
                new DateTime(2000, 1, 4),
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute);
            var firstView = first.ViewFor(security.CompanyId);
            var secondView = second.ViewFor(security.CompanyId);
            AssertEqual(10, firstView.Snapshot.Asks.Count, "live runtime asks");
            AssertEqual(10, firstView.Snapshot.Bids.Count, "live runtime bids");
            AssertEqual(firstView.LastTradePrice, secondView.LastTradePrice, "same seed live price");
            AssertSequence(
                firstView.Snapshot.Asks.Select(level => level.Price),
                secondView.Snapshot.Asks.Select(level => level.Price),
                "same seed live asks");

            var practice = new StockMarketRuntimeSession(
                771,
                new DateTime(2000, 1, 4),
                50_000,
                new[] { security },
                MarketSessionClock.OpenMinute);
            var practiceView = practice.ViewFor(security.CompanyId);
            var practiceRange = MarketPricingRules.DailyPriceRange(
                practiceView.PreviousClose,
                practice.Date,
                security.PriceRuleMarket);
            if (MarketTradingCosts.BuyReservation(practice.Date, practiceRange.Upper) > practice.BrokerageCash)
                throw new InvalidOperationException("The isolated 50,000 won practice account cannot afford one share.");
            var practiceBuy = practice.PlaceOrder(
                security.CompanyId,
                isBuy: true,
                isMarket: true,
                limitPrice: 0,
                quantity: 1);
            if (!practiceBuy.Accepted || practiceBuy.FilledQuantity != 1 ||
                practice.PositionUnits(security.CompanyId) != 1)
                throw new InvalidOperationException("Practice-account market buy did not settle one share.");

            var openingCash = first.BrokerageCash;
            var buy = first.PlaceOrder(
                security.CompanyId,
                isBuy: true,
                isMarket: true,
                limitPrice: 0,
                quantity: 10);
            if (!buy.Accepted || buy.FilledQuantity <= 0)
                throw new InvalidOperationException("Live market buy did not fill.");
            if (first.BrokerageCash >= openingCash || first.PositionUnits(security.CompanyId) != buy.FilledQuantity)
                throw new InvalidOperationException("Live buy did not update cash and position together.");

            var range = MarketPricingRules.DailyPriceRange(
                firstView.PreviousClose,
                first.Date,
                security.PriceRuleMarket);
            var pending = first.PlaceOrder(
                security.CompanyId,
                isBuy: true,
                isMarket: false,
                limitPrice: range.Lower,
                quantity: 5);
            if (!pending.Accepted || pending.RemainingQuantity <= 0 || first.PendingOrders.Count != 1)
                throw new InvalidOperationException("Live limit order did not enter the pending FIFO.");
            if (first.AvailableBrokerageCash >= first.BrokerageCash)
                throw new InvalidOperationException("Pending buy cash was not reserved.");
            if (!first.CancelPendingOrder(pending.PendingOrderId) || first.PendingOrders.Count != 0)
                throw new InvalidOperationException("Pending-order cancellation failed.");

            var cashBeforeSell = first.BrokerageCash;
            var sell = first.PlaceOrder(
                security.CompanyId,
                isBuy: false,
                isMarket: true,
                limitPrice: 0,
                quantity: Math.Min(3, first.PositionUnits(security.CompanyId)));
            if (!sell.Accepted || sell.FilledQuantity <= 0 || first.BrokerageCash <= cashBeforeSell)
                throw new InvalidOperationException("Live market sell did not update cash.");

            first.AdvanceMinutes(5, 1);
            second.AdvanceMinutes(5, 1);
            AssertEqual(first.MarketMinute, second.MarketMinute, "live minute progression");
            AssertEqual(
                first.ViewFor(security.CompanyId).LastTradePrice,
                second.ViewFor(security.CompanyId).LastTradePrice,
                "same seed advanced live price");

            ValidatePlaybackRateEquivalence(security);
            ValidateSameMinuteLiquidityWatermark(security);
            ValidateBrokerageSemantics(security);
        }

        private static void ValidatePlaybackRateEquivalence(MarketSecurityDefinition security)
        {
            var sessions = new[] { 1, 3, 10 }
                .Select(rate => new
                {
                    Rate = rate,
                    Session = new StockMarketRuntimeSession(
                        991,
                        new DateTime(2000, 1, 4),
                        1_000_000_000,
                        new[] { security },
                        MarketSessionClock.OpenMinute)
                })
                .ToArray();
            foreach (var item in sessions)
            {
                var view = item.Session.ViewFor(security.CompanyId);
                var range = MarketPricingRules.DailyPriceRange(
                    view.PreviousClose,
                    item.Session.Date,
                    security.PriceRuleMarket);
                var pending = item.Session.PlaceOrder(
                    security.CompanyId,
                    true,
                    false,
                    range.Lower,
                    7);
                if (!pending.Accepted || pending.RemainingQuantity != 7)
                    throw new InvalidOperationException("Playback equivalence setup order was not pending.");
                item.Session.AdvanceMinutes(8, item.Rate);
                AssertEqual(8, item.Session.CanonicalMinuteUpdateCount, $"canonical updates at animation rate {item.Rate}");
            }

            var reference = sessions[0].Session;
            foreach (var candidate in sessions.Skip(1).Select(item => item.Session))
            {
                AssertEqual(reference.MarketMinute, candidate.MarketMinute, "animation-rate minute equivalence");
                AssertEqual(reference.BrokerageCash, candidate.BrokerageCash, "animation-rate cash equivalence");
                AssertEqual(reference.PendingOrders.Count, candidate.PendingOrders.Count, "animation-rate pending equivalence");
                AssertEqual(reference.TradeTape.Count, candidate.TradeTape.Count, "animation-rate tape count equivalence");
                AssertSnapshotEqual(
                    reference.ViewFor(security.CompanyId).Snapshot,
                    candidate.ViewFor(security.CompanyId).Snapshot,
                    "animation-rate snapshot equivalence");
                AssertSequence(
                    reference.TradeTape.Select(print => print.Price),
                    candidate.TradeTape.Select(print => print.Price),
                    "animation-rate tape price equivalence");
            }
        }

        private static void ValidateSameMinuteLiquidityWatermark(MarketSecurityDefinition security)
        {
            var session = new StockMarketRuntimeSession(
                551,
                new DateTime(2000, 1, 4),
                10_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute);
            var before = session.ViewFor(security.CompanyId).Snapshot;
            var bestAsk = before.Asks[0];
            var maximum = session.MaximumOrderQuantity(security.CompanyId, true, true, 0);
            var requested = Math.Min(bestAsk.Quantity, maximum);
            if (requested <= 0) throw new InvalidOperationException("Liquidity watermark setup has no executable ask.");
            var first = session.PlaceOrder(security.CompanyId, true, true, 0, requested);
            if (!first.Accepted || first.FilledQuantity != requested)
                throw new InvalidOperationException("First watermark order did not fill its executable quantity.");
            var afterFirst = session.ViewFor(security.CompanyId).Snapshot;
            var remainingAtSamePrice = afterFirst.Asks.Single(level => level.Price == bestAsk.Price).Quantity;
            AssertEqual(bestAsk.Quantity - first.FilledQuantity, remainingAtSamePrice, "first same-price consumption");

            var second = session.PlaceOrder(security.CompanyId, true, true, 0, requested);
            var afterSecond = session.ViewFor(security.CompanyId).Snapshot;
            var afterSecondAtSamePrice = afterSecond.Asks.Single(level => level.Price == bestAsk.Price).Quantity;
            if (afterSecondAtSamePrice > remainingAtSamePrice)
                throw new InvalidOperationException("Consumed same-price liquidity reappeared in the same minute.");
            if (first.FilledQuantity + second.FilledQuantity > before.ExecutionCapacity)
                throw new InvalidOperationException("Same-minute fills exceeded the original execution capacity.");
            AssertEqual(
                before.ExecutionCapacity - first.FilledQuantity - second.FilledQuantity,
                afterSecond.ExecutionCapacity,
                "same-minute execution-capacity watermark");
        }

        private static void ValidateBrokerageSemantics(MarketSecurityDefinition security)
        {
            var session = new StockMarketRuntimeSession(
                337,
                new DateTime(2000, 1, 4),
                1_000_000_000,
                new[] { security },
                MarketSessionClock.OpenMinute);
            session.SetFavorite(security.CompanyId, true);
            var maximumBuy = session.MaximumOrderQuantity(security.CompanyId, true, true, 0);
            if (maximumBuy <= 0 || maximumBuy > session.ViewFor(security.CompanyId).Snapshot.ExecutionCapacity)
                throw new InvalidOperationException("Market-buy percentage ceiling ignored execution capacity.");
            var buyQuantity = Math.Min(10, maximumBuy);
            var buy = session.PlaceOrder(security.CompanyId, true, true, 0, buyQuantity);
            var expectedFee = MarketTradingCosts.TradingFee(session.Date, buy.Notional);
            var expectedAverage = (buy.Notional + expectedFee) / (double)buy.FilledQuantity;
            if (Math.Abs(session.AverageCost(security.CompanyId) - expectedAverage) > 0.000001d)
                throw new InvalidOperationException("Average acquisition cost excluded the buy fee.");

            var view = session.ViewFor(security.CompanyId);
            var range = MarketPricingRules.DailyPriceRange(
                view.PreviousClose,
                session.Date,
                security.PriceRuleMarket);
            var sellPending = session.PlaceOrder(
                security.CompanyId,
                false,
                false,
                range.Upper,
                buy.FilledQuantity);
            if (!sellPending.Accepted || sellPending.RemainingQuantity <= 0)
                throw new InvalidOperationException("Sell reservation setup did not remain pending.");
            AssertEqual(0, session.MaximumOrderQuantity(security.CompanyId, false, false, range.Upper), "reserved sell units shortcut ceiling");
            if (!session.CancelPendingOrder(sellPending.PendingOrderId))
                throw new InvalidOperationException("Sell reservation cancellation failed.");

            var buyPending = session.PlaceOrder(
                security.CompanyId,
                true,
                false,
                range.Lower,
                10);
            if (!buyPending.Accepted || buyPending.RemainingQuantity != 10)
                throw new InvalidOperationException("Amendment setup order did not remain pending.");
            var original = session.PendingOrders.Single(order => order.Id == buyPending.PendingOrderId);
            var availableBeforeAmendment = session.AvailableBrokerageCash;
            var amended = session.AmendPendingOrder(original.Id, range.Lower, 6);
            if (!amended.Accepted || session.PendingOrders.Any(order => order.Id == original.Id))
                throw new InvalidOperationException("Amendment did not cancel the original order.");
            var replacement = session.PendingOrders.Single(order => order.Id == amended.PendingOrderId);
            if (replacement.PlacedSequence <= original.PlacedSequence || replacement.RemainingQuantity != 6)
                throw new InvalidOperationException("Amendment did not create a new FIFO order.");
            if (session.AvailableBrokerageCash <= availableBeforeAmendment)
                throw new InvalidOperationException("Amendment did not release the old buy reservation before re-reserving.");

            var exported = session.ExportBrokerageState();
            var restored = new StockMarketRuntimeSession(
                337,
                new DateTime(2000, 1, 4),
                0,
                new[] { security },
                MarketSessionClock.OpenMinute);
            if (!restored.TryApplyBrokerageState(exported, out var importError))
                throw new InvalidOperationException($"Valid brokerage DTO import failed: {importError}");
            AssertEqual(session.BrokerageCash, restored.BrokerageCash, "brokerage DTO cash round trip");
            AssertEqual(session.PositionUnits(security.CompanyId), restored.PositionUnits(security.CompanyId), "brokerage DTO position round trip");
            AssertEqual(session.PendingOrders.Count, restored.PendingOrders.Count, "brokerage DTO pending round trip");
            AssertEqual(session.OrderJournal.Count, restored.OrderJournal.Count, "brokerage DTO journal round trip");
            if (!restored.IsFavorite(security.CompanyId))
                throw new InvalidOperationException("Brokerage DTO favorite round trip failed.");

            var cashBeforeInvalidImport = restored.BrokerageCash;
            var invalid = new BrokerageAccountStateDto(
                -1,
                exported.Positions,
                exported.PendingOrders,
                exported.PlayerTrades,
                exported.OrderJournal,
                exported.FavoriteAssetIds,
                exported.OrderSequence,
                exported.JournalSequence);
            if (restored.TryApplyBrokerageState(invalid, out _) || restored.BrokerageCash != cashBeforeInvalidImport)
                throw new InvalidOperationException("Invalid brokerage DTO was partially applied.");

            var history = session.PriceHistoryFor(security.CompanyId, 48);
            AssertEqual(session.ViewFor(security.CompanyId).LastTradePrice, history.Last().Price, "canonical chart endpoint");
        }

        private static void AssertSnapshotEqual(
            MarketOrderBookSnapshot expected,
            MarketOrderBookSnapshot actual,
            string label)
        {
            AssertEqual(expected.ExecutionCapacity, actual.ExecutionCapacity, $"{label} capacity");
            AssertSequence(expected.Asks.Select(level => level.Price), actual.Asks.Select(level => level.Price), $"{label} ask prices");
            AssertSequence(expected.Asks.Select(level => (long)level.Quantity), actual.Asks.Select(level => (long)level.Quantity), $"{label} ask quantities");
            AssertSequence(expected.Bids.Select(level => level.Price), actual.Bids.Select(level => level.Price), $"{label} bid prices");
            AssertSequence(expected.Bids.Select(level => (long)level.Quantity), actual.Bids.Select(level => (long)level.Quantity), $"{label} bid quantities");
        }

        private static void ValidateViewport(int width, int height, float expectedLogicalWidth)
        {
            var viewport = StockMarketLandscapeLayout.CalculateViewport(width, height);
            if (Math.Abs(viewport.LogicalWidth - expectedLogicalWidth) > 0.1f)
                throw new InvalidOperationException($"Unexpected logical width at {width}x{height}: {viewport.LogicalWidth}");
            var layout = StockMarketLandscapeLayout.Create(viewport.LogicalWidth);
            layout.ValidateOrThrow();
            if (viewport.OffsetX < -0.01f || viewport.OffsetY < -0.01f ||
                viewport.PixelWidth > width + 0.1f || viewport.PixelHeight > height + 0.1f)
                throw new InvalidOperationException($"Viewport escaped {width}x{height}.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }

        private static void AssertSequence(IEnumerable<long> expected, IEnumerable<long> actual, string label)
        {
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException($"{label}: sequence mismatch");
        }
    }
}

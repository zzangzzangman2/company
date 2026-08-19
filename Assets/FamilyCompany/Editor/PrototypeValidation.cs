using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.History;
using FamilyCompany.Simulation.Market;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Simulation.Technology;
using FamilyCompany.Simulation.Workforce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PrototypeValidation
    {
        [MenuItem("Family Company/Validate Prototype 0.1")]
        public static void Run()
        {
            try
            {
                ValidateStartingFamily();
                ValidateStableRandom();
                ValidateEventOrdering();
                ValidateTimeAndLedger();
                ValidateSimulMarketRules();
                ValidateKoreaHistoryV1();
                ValidateFourPersonContractScope();
                ValidateContractLifecycle();
                ValidateSaveRoundTrip();
            ValidateContractTechnologyRewards();
                ValidateSaveSlots();
                ValidateWideFrontendSettings();
                ValidateAssetsAndScene();
                Debug.Log("FAMILY_COMPANY_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateStartingFamily()
        {
            var state = PrototypeStateFactory.Create();
            AssertEqual(14, state.Family.Get("player").AgeAt(state.Time), "player age");
            AssertEqual(20, state.Family.Get("older_sister").AgeAt(state.Time), "sister age");
            AssertEqual(46, state.Family.Get("father").AgeAt(state.Time), "father age");
            AssertEqual(44, state.Family.Get("mother").AgeAt(state.Time), "mother age");
        }

        private static void ValidateStableRandom()
        {
            AssertEqual(1726110163, StableRandom.StableHash31("family-company"), "stable hash fixture");
            AssertEqual(877381839, StableRandom.StableRandomWord31("family-company"), "random word fixture");
            AssertEqual(25, StableRandom.StableRandomInt("family-company", 37), "random int fixture");
            for (var bound = 1; bound <= 100; bound++)
            {
                var key = $"validation:{bound}";
                var first = StableRandom.StableRandomInt(key, bound);
                AssertEqual(first, StableRandom.StableRandomInt(key, bound), "random replay");
                if (first < 0 || first >= bound) throw new InvalidOperationException("Random result is out of bounds.");
            }
        }

        private static void ValidateEventOrdering()
        {
            var queue = new DeterministicEventQueue(new[]
            {
                new ScheduledEvent("z", 10, 1, "test"),
                new ScheduledEvent("b", 10, 0, "test"),
                new ScheduledEvent("a", 10, 0, "test"),
                new ScheduledEvent("early", 5, 9, "test")
            });
            var order = string.Join(",", queue.DequeueDue(10).Select(item => item.EventId));
            AssertEqual("early,a,b,z", order, "event order");
        }

        private static void ValidateTimeAndLedger()
        {
            var state = PrototypeStateFactory.Create();
            var runner = new SimulationRunner(state);
            var due = runner.AdvanceMinutes(60);
            AssertEqual(60L, state.Time.ElapsedMinutes, "time advance");
            AssertEqual(1, due.Count, "due event count");
            AssertEqual(5_000_000L, state.Company.CashWon, "opening cash");
            foreach (var transaction in state.Company.Ledger)
            {
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "balanced ledger");
            }
        }

        private static void ValidateSimulMarketRules()
        {
            AssertEqual(
                MarketSessionPhase.OpeningTransition,
                MarketSessionClock.At(8 * 60).Phase,
                "market 08:00 phase");
            AssertEqual(false, MarketSessionClock.At(8 * 60 + 59).Tradable, "market 08:59 tradable");
            AssertEqual(MarketSessionPhase.Regular, MarketSessionClock.At(9 * 60).Phase, "market 09:00 phase");
            AssertEqual(true, MarketSessionClock.At(9 * 60).Tradable, "market 09:00 tradable");
            AssertEqual(
                MarketSessionPhase.ClosingAuction,
                MarketSessionClock.At(14 * 60 + 50).Phase,
                "market 14:50 phase");
            AssertEqual(
                MarketSessionPhase.CloseSettlement,
                MarketSessionClock.At(15 * 60).Phase,
                "market 15:00 phase");
            AssertEqual(MarketSessionPhase.Closed, MarketSessionClock.At(20 * 60).Phase, "market 20:00 phase");
            AssertEqual(0, MarketSessionClock.TickForMinute(8 * 60), "market opening tick");
            AssertEqual(420, MarketSessionClock.TickForMinute(15 * 60), "market close tick");
            AssertEqual(720, MarketSessionClock.TickForMinute(20 * 60), "market final tick");
            AssertEqual(8 * 60 + 1, MarketSessionClock.MinuteForTick(1), "market minute for tick");

            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2000, 1, 1)), "market weekend");
            AssertEqual(true, MarketTradingCalendar.IsTradingDay(new DateTime(2000, 1, 3)), "campaign first Monday");
            AssertEqual(true, MarketTradingCalendar.IsTradingDay(new DateTime(2000, 1, 4)), "corpus first day");
            AssertEqual(true, MarketTradingCalendar.IsTradingDay(new DateTime(2022, 9, 8)), "corpus trading date");
            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2022, 9, 9)), "corpus Chuseok");
            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2023, 1, 23)), "corpus lunar holiday");
            AssertEqual(true, MarketTradingCalendar.IsTradingDay(new DateTime(2026, 7, 23)), "corpus final day");
            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2026, 8, 17)), "post corpus substitute holiday");
            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2026, 9, 24)), "post corpus Chuseok eve");
            AssertEqual(false, MarketTradingCalendar.IsTradingDay(new DateTime(2026, 12, 31)), "campaign year end close");

            var corpusTradingDays = 0;
            for (var date = MarketTradingCalendar.CorpusFirstTradingDate;
                 date <= MarketTradingCalendar.CorpusLastTradingDate;
                 date = date.AddDays(1))
            {
                if (MarketTradingCalendar.IsTradingDay(date)) corpusTradingDays += 1;
            }
            AssertEqual(6545, corpusTradingDays, "market corpus trading day count");

            AssertEqual(
                new DateTime(2026, 6, 15),
                MarketTradingCalendar.SettlementDateFor(new DateTime(2026, 6, 11)),
                "market D+2 settlement");

            AssertEqual(0.15m, MarketPricingRules.DailyPriceLimitRate(new DateTime(2010, 12, 31)), "legacy price limit");
            AssertEqual(0.30m, MarketPricingRules.DailyPriceLimitRate(new DateTime(2015, 6, 15)), "modern price limit");
            AssertEqual(1L, MarketPricingRules.TickSize(999m), "tick 999");
            AssertEqual(5L, MarketPricingRules.TickSize(1000m), "tick 1000");
            AssertEqual(50L, MarketPricingRules.TickSize(10000m), "tick 10000");
            AssertEqual(100L, MarketPricingRules.TickSize(500000m, "도전시장"), "growth market tick");

            var legacyRange = MarketPricingRules.DailyPriceRange(10000m, new DateTime(2008, 1, 2));
            AssertEqual(8500L, legacyRange.Lower, "legacy lower price limit");
            AssertEqual(11500L, legacyRange.Upper, "legacy upper price limit");
            var lowPriceRange = MarketPricingRules.DailyPriceRange(330m, new DateTime(2008, 1, 2));
            AssertEqual(281L, lowPriceRange.Lower, "low-price lower limit");
            AssertEqual(379L, lowPriceRange.Upper, "low-price upper limit");
            var ipoRange = MarketPricingRules.DailyPriceRange(
                10000m,
                new DateTime(2023, 6, 26),
                isIpoFirstTradingDay: true);
            AssertEqual(6000L, ipoRange.Lower, "IPO lower range");
            AssertEqual(40000L, ipoRange.Upper, "IPO upper range");
            AssertEqual(true, MarketPricingRules.IsValidOrderPrice(10050m), "valid quote unit");
            AssertEqual(false, MarketPricingRules.IsValidOrderPrice(10025m), "invalid quote unit");

            AssertEqual(
                true,
                MarketSessionClock.DynamicVolatilityInterruptionActive(10 * 60, 10000m, 10300m),
                "three-percent VI");
            AssertEqual(
                false,
                MarketSessionClock.DynamicVolatilityInterruptionActive(14 * 60 + 50, 10000m, 11000m),
                "closing auction VI disabled");

            AssertEqual(0.0050m, MarketTradingCosts.TradingFeeRate(new DateTime(2000, 1, 3)), "2000 fee rate");
            AssertEqual(0.0030m, MarketTradingCosts.SecuritiesTransactionTaxRate(new DateTime(2000, 1, 3)), "2000 tax rate");
            AssertEqual(5000L, MarketTradingCosts.TradingFee(new DateTime(2000, 1, 3), 1_000_000L), "2000 fee");
            AssertEqual(3000L, MarketTradingCosts.SecuritiesTransactionTax(new DateTime(2000, 1, 3), 1_000_000L), "2000 tax");
            AssertEqual(1_005_000L, MarketTradingCosts.BuyReservation(new DateTime(2000, 1, 3), 1_000_000L), "buy reservation");

            var ordinaryTurnovers = new[] { 1d, 50d, 100d, 500d, 2000d, 5000d, 10000d, 15000d, 60000d };
            var ordinaryPulses = ordinaryTurnovers
                .Select(turnover => MarketOrderBookRules.PulsesPerMarketMinute(
                    turnover,
                    10000d,
                    10000d,
                    10000d))
                .ToArray();
            AssertEqual("1,1,1,1,2,2,3,4,4", string.Join(",", ordinaryPulses), "order book ordinary cadence");
            AssertEqual(5, MarketOrderBookRules.PulsesPerMarketMinute(1d, 10150d, 10000d, 10000d), "order book fast cadence");
            AssertEqual(7, MarketOrderBookRules.PulsesPerMarketMinute(1d, 10300d, 10000d, 10000d), "order book extreme cadence");
            AssertEqual(1, MarketOrderBookRules.PulsesPerMarketMinute(100d, 10500d, 10500d, 10000d), "order book five-percent cadence");
            AssertEqual(5, MarketOrderBookRules.PulsesPerMarketMinute(100d, 10610d, 10610d, 10000d), "order book six-percent cadence");
            AssertEqual(0, MarketOrderBookRules.PulsesPerMarketMinute(
                60000d,
                10000d,
                10000d,
                10000d,
                playbackActive: false), "order book pause freezes cadence");
            AssertEqual(4928, MarketOrderBookRules.LiquidityPulseFrame(617, 0), "order book frame zero");
            AssertEqual(4935, MarketOrderBookRules.LiquidityPulseFrame(617, 7), "order book frame seven");
            AssertEqual(
                "4931,4932,4933,4934,4935",
                string.Join(",", MarketOrderBookRules.PendingPulseFrames(617, 4930, 7)),
                "order book pending FIFO frames");
            AssertEqual(
                "0,250,501,751,1001",
                string.Join(",", Enumerable.Range(0, 5).Select(slot =>
                    MarketOrderBookRules.CumulativeSlotCapacity(1001, slot, 4))),
                "order book four-slot capacity");
            AssertEqual(
                "0,143,286,429,572,715,858,1001",
                string.Join(",", Enumerable.Range(0, 8).Select(slot =>
                    MarketOrderBookRules.CumulativeSlotCapacity(1001, slot, 7))),
                "order book seven-slot capacity");

            var asks = new[]
            {
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 10050, 100),
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 10100, 200),
                new MarketOrderBookLevel(MarketOrderBookSide.Ask, 10150, 300),
            };
            var bids = new[]
            {
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 10000, 120),
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 9950, 240),
                new MarketOrderBookLevel(MarketOrderBookSide.Bid, 9900, 360),
            };
            var book = new MarketOrderBookSnapshot(asks, bids, 1000d, 1000d, 1000);
            var buyPlan = MarketOrderBookRules.LimitFillPlan(book, true, 450d, 10150, maximumNotional: 2_000_000_000);
            AssertEqual(450, buyPlan.FilledQuantity, "order book buy filled quantity");
            AssertEqual(4_547_500L, buyPlan.Notional, "order book buy notional");
            AssertApproximately(10105.555555555555d, buyPlan.AveragePrice, 0.000000001d, "order book buy average");
            AssertEqual("100@10050,200@10100,150@10150", string.Join(",", buyPlan.Fills.Select(fill => $"{fill.Quantity}@{fill.Price}")), "order book buy ladder fills");
            var sellPlan = MarketOrderBookRules.LimitFillPlan(book, false, 250d, 9950, maximumNotional: 2_000_000_000);
            AssertEqual(2_493_500L, sellPlan.Notional, "order book sell notional");
            AssertApproximately(9974d, sellPlan.AveragePrice, 0.000000001d, "order book sell average");
            var skipped = MarketOrderBookRules.LimitFillPlan(
                book,
                true,
                150d,
                10100,
                maximumNotional: 2_000_000_000,
                alreadyConsumedByPrice: new System.Collections.Generic.Dictionary<long, double> { [10050] = 100d });
            AssertEqual("150@10100", string.Join(",", skipped.Fills.Select(fill => $"{fill.Quantity}@{fill.Price}")), "order book consumed quote skip");

            var rise = MarketOrderBookRules.PriceTransitionTowardTarget(book, 10000d, 10150d, 250, "main");
            AssertEqual(10100L, rise.Price, "order book partial rise price");
            AssertEqual(false, rise.TargetReached, "order book partial rise target");
            AssertEqual("100@10050,150@10100", string.Join(",", rise.OrderedFills.Select(fill => $"{fill.Quantity}@{fill.Price}")), "order book partial rise fills");
            var fall = MarketOrderBookRules.PriceTransitionTowardTarget(book, 10050d, 9950d, 360, "main");
            AssertEqual(9950L, fall.Price, "order book falling price");
            AssertEqual(true, fall.TargetReached, "order book falling target");

            var fractional = MarketOrderBookRules.SnapshotAfterConsumption(
                book,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 0.5d },
                consumedCapacityUnits: 1,
                latestConsumedSide: MarketOrderBookSide.Ask,
                latestConsumedPrice: 10050);
            AssertEqual(100, fractional.Asks[0].Quantity, "order book fractional consumption ignored");
            AssertApproximately(0.5d, fractional.AppliedAskConsumptionByPrice[10050], 0d, "order book fractional watermark");
            AssertEqual(1, fractional.AppliedCapacityConsumptionUnits, "order book capacity watermark");
            AssertEqual(null, fractional.SourceLastTradePrice, "order book fractional last price unchanged");

            var cumulative40 = MarketOrderBookRules.SnapshotAfterConsumption(
                book,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 40d },
                consumedCapacityUnits: 40,
                latestConsumedSide: MarketOrderBookSide.Ask,
                latestConsumedPrice: 10050);
            AssertEqual(60, cumulative40.Asks[0].Quantity, "order book cumulative 40 quantity");
            AssertEqual(100, cumulative40.Asks[0].QueueRecoveryTargetQuantity, "order book ordinary recovery target");
            AssertEqual(560, cumulative40.TotalAskQuantity, "order book cumulative 40 total ask");
            AssertApproximately(128.57142857142858d, cumulative40.TradeStrength, 0.000000001d, "order book cumulative 40 strength");
            var repeated40 = MarketOrderBookRules.SnapshotAfterConsumption(
                cumulative40,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 40d },
                consumedCapacityUnits: 40,
                latestConsumedSide: MarketOrderBookSide.Ask,
                latestConsumedPrice: 10050);
            AssertEqual(60, repeated40.Asks[0].Quantity, "order book repeated cumulative watermark idempotent");
            var cumulative70 = MarketOrderBookRules.SnapshotAfterConsumption(
                repeated40,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 70d },
                consumedCapacityUnits: 70,
                latestConsumedSide: MarketOrderBookSide.Ask,
                latestConsumedPrice: 10050);
            AssertEqual(30, cumulative70.Asks[0].Quantity, "order book cumulative watermark delta only");

            var hiddenTinyRemainder = MarketOrderBookRules.SnapshotAfterConsumption(
                book,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 95d },
                consumedCapacityUnits: 95,
                latestConsumedSide: MarketOrderBookSide.Ask);
            AssertEqual(2, hiddenTinyRemainder.Asks.Count, "order book tiny remainder row omitted");
            AssertEqual(10100L, hiddenTinyRemainder.Asks[0].Price, "order book next ask after tiny remainder");
            AssertEqual(500, hiddenTinyRemainder.TotalAskQuantity, "order book hidden remainder total ask");
            AssertEqual(10050L, hiddenTinyRemainder.SourceLastTradePrice.Value, "order book exhausted touch last price");

            var structuralBook = new MarketOrderBookSnapshot(
                new[]
                {
                    new MarketOrderBookLevel(
                        MarketOrderBookSide.Ask,
                        10050,
                        100,
                        isWall: true,
                        structuralStrength: 4d,
                        isStructuralWall: true,
                        queueRecoveryTargetQuantity: 100),
                },
                bids,
                1000d,
                1000d,
                1000);
            var structuralBreach = MarketOrderBookRules.SnapshotAfterConsumption(
                structuralBook,
                new System.Collections.Generic.Dictionary<long, double> { [10050] = 90d },
                consumedCapacityUnits: 90,
                latestConsumedSide: MarketOrderBookSide.Ask,
                latestConsumedPrice: 10050);
            AssertEqual(10, structuralBreach.Asks[0].Quantity, "order book structural breach remaining");
            AssertEqual(false, structuralBreach.Asks[0].IsWall, "order book structural breach clears wall");
            AssertEqual(false, structuralBreach.Asks[0].IsStructuralWall, "order book structural breach clears structural wall");
            AssertEqual(true, structuralBreach.Asks[0].IsStructuralBreached, "order book structural breach flag");
            AssertEqual(25, structuralBreach.Asks[0].QueueRecoveryTargetQuantity, "order book structural recovery ceiling");
            AssertApproximately(240d, structuralBreach.TradeStrength, 0d, "order book structural breach strength cap");

            var placedDate = new DateTime(2000, 1, 3);
            var firstPending = new MarketPendingOrder(
                "first",
                MarketPendingOrderSide.Buy,
                "queue_audit_stock",
                10000,
                20,
                20,
                placedDate,
                600,
                1,
                100);
            var secondPending = new MarketPendingOrder(
                "second",
                MarketPendingOrderSide.Buy,
                "queue_audit_stock",
                10000,
                30,
                30,
                placedDate,
                600,
                2,
                120);
            var cancelledPending = MarketPendingOrderRules.Cancel(
                new[] { firstPending, secondPending },
                firstPending.Id);
            AssertEqual(1, cancelledPending.Count, "pending cancel count");
            AssertApproximately(100d, cancelledPending[0].QueueAheadQuantity, 0d, "pending cancel FIFO release");
            AssertApproximately(
                140d,
                MarketPendingOrderRules.QueueAheadForNewOrder(
                    book,
                    "queue_audit_stock",
                    MarketPendingOrderSide.Buy,
                    10000,
                    new[] { firstPending },
                    immediateFillOccurred: false),
                0d,
                "pending new order queue ahead");
            var lowBuy = new MarketPendingOrder(
                "low",
                MarketPendingOrderSide.Buy,
                "queue_audit_stock",
                900,
                1,
                1,
                placedDate,
                600,
                1);
            var highBuy = new MarketPendingOrder(
                "high",
                MarketPendingOrderSide.Buy,
                "queue_audit_stock",
                950,
                1,
                1,
                placedDate,
                600,
                2);
            AssertEqual(
                "high,low",
                string.Join(",", MarketPendingOrderRules.InExchangePriority(new[] { lowBuy, highBuy }).Select(order => order.Id)),
                "pending buy price priority");
            var earlierSell = new MarketPendingOrder(
                "sell",
                MarketPendingOrderSide.Sell,
                "queue_audit_stock",
                1050,
                1,
                1,
                placedDate,
                600,
                0);
            AssertEqual(
                "sell,high",
                string.Join(",", MarketPendingOrderRules.InExchangePriority(new[] { highBuy, earlierSell }).Select(order => order.Id)),
                "pending cross-side chronological merge");
            var queueConsumption = MarketPendingOrderRules.ConsumeRestingQueue(
                secondPending,
                book,
                10000,
                50);
            AssertEqual(50, queueConsumption.ConsumedQuantity, "pending external queue consumption");
            AssertApproximately(70d, queueConsumption.QueueAheadQuantity, 0d, "pending external queue remainder");
            AssertEqual(0, queueConsumption.RemainingCapacity, "pending queue consumes capacity first");
            var afterPartialFill = MarketPendingOrderRules.AfterFill(
                new[] { firstPending, secondPending },
                firstPending,
                5);
            AssertApproximately(
                115d,
                afterPartialFill.Single(order => order.Id == secondPending.Id).QueueAheadQuantity,
                0d,
                "pending later FIFO advances on fill");
            AssertApproximately(
                15d,
                afterPartialFill.Single(order => order.Id == firstPending.Id).RemainingQuantity,
                0d,
                "pending partial remainder");
            var cashPending = new MarketPendingOrder(
                "cash",
                MarketPendingOrderSide.Buy,
                "queue_audit_stock",
                9000,
                10,
                10,
                placedDate,
                600,
                3);
            AssertEqual(
                90_450L,
                MarketPendingOrderRules.PendingBuyReservedCash(100_000, new[] { cashPending }, 0.005m),
                "pending buy reservation includes fee");
            AssertEqual(
                9_550L,
                MarketPendingOrderRules.AvailableBrokerageCash(100_000, new[] { cashPending }, 0.005m),
                "pending available brokerage cash");
            var sellPending = new MarketPendingOrder(
                "sell-reserve",
                MarketPendingOrderSide.Sell,
                "queue_audit_stock",
                10000,
                3,
                3,
                placedDate,
                600,
                4);
            AssertApproximately(
                3d,
                MarketPendingOrderRules.PendingReservedUnits(
                    new[] { cashPending, sellPending },
                    "queue_audit_stock",
                    MarketPendingOrderSide.Sell),
                0d,
                "pending sell reserved units");

            AssertEqual(
                "4,23,5,5,2,5,5,3,30,41",
                string.Join(",", MarketOrderBookRules.SplitTradeQuantity("hanbit_telecom", 6015, 617, 4929, 123)),
                "order book split prints 123");
            AssertEqual(
                "4,2515,5,5,2,5,5,3,3138,4318",
                string.Join(",", MarketOrderBookRules.SplitTradeQuantity("hanbit_telecom", 6015, 617, 4929, 10000)),
                "order book split prints 10000");

            AssertEqual(7, MarketOrderBookReplayQueue.VisibleRowsPerSide, "order book visible 7+7 rows");
            var replay = new MarketOrderBookReplayQueue("seed:2000-01-03");
            replay.SetPlayback(true, 0);
            var batchA = new MarketOrderBookReplayBatch(
                "market:A",
                "market",
                new[]
                {
                    new MarketOrderBookSweepStep(617, 4930, 1, MarketOrderBookSide.Ask, 10100, 200, 0),
                    new MarketOrderBookSweepStep(617, 4929, 0, MarketOrderBookSide.Ask, 10050, 100, 0),
                });
            var batchB = new MarketOrderBookReplayBatch(
                "market:B",
                "market",
                new[]
                {
                    new MarketOrderBookSweepStep(618, 4993, 0, MarketOrderBookSide.Bid, 10000, 120, 0),
                });
            AssertEqual(true, replay.Enqueue(batchA), "order book enqueue first batch");
            AssertEqual(true, replay.Enqueue(batchB), "order book enqueue second batch");
            AssertEqual(false, replay.HasActiveBatch, "paused order book does not ingest FIFO");
            AssertEqual(false, replay.TickMicroseconds(3_000_000), "paused order book timer frozen");
            replay.SetPlayback(false, 10);
            AssertEqual("market:A", replay.Cursor.Batch.Identity, "order book FIFO first identity");
            AssertEqual(0, replay.Cursor.Step.Value.Sequence, "order book sorts sweep sequence");
            AssertEqual(MarketOrderBookReplayPhase.Arriving, replay.Cursor.Phase, "order book arrival phase");
            AssertEqual(56_000L, replay.CurrentPhaseDurationMicroseconds, "order book 10x arrival duration floor");
            AssertEqual(false, replay.TickMicroseconds(55_000), "order book arrival does not skip");
            AssertEqual(true, replay.TickMicroseconds(1_000), "order book arrival boundary");
            AssertEqual(MarketOrderBookReplayPhase.Draining, replay.Cursor.Phase, "order book drain phase");
            replay.SetPlayback(true, 0);
            var frozenIdentity = replay.Cursor.Batch.Identity;
            var frozenSequence = replay.Cursor.Step.Value.Sequence;
            var frozenPhase = replay.Cursor.Phase;
            AssertEqual(false, replay.TickMicroseconds(3_000_000), "order book paused active step frozen");
            AssertEqual(frozenIdentity, replay.Cursor.Batch.Identity, "order book paused identity frozen");
            AssertEqual(frozenSequence, replay.Cursor.Step.Value.Sequence, "order book paused sequence frozen");
            AssertEqual(frozenPhase, replay.Cursor.Phase, "order book paused phase frozen");
            replay.SetPlayback(false, 10);
            AssertEqual(9_600L, replay.CurrentPhaseDurationMicroseconds, "order book 10x drain duration");
            replay.TickMicroseconds(9_600);
            AssertEqual(1, replay.Cursor.Step.Value.Sequence, "order book next step FIFO");
            AssertEqual(MarketOrderBookReplayPhase.Arriving, replay.Cursor.Phase, "order book next arrival phase");
            replay.TickMicroseconds(56_000);
            replay.TickMicroseconds(9_600);
            AssertEqual(MarketOrderBookReplayPhase.FinalHold, replay.Cursor.Phase, "order book final hold phase");
            AssertEqual(11_200L, replay.CurrentPhaseDurationMicroseconds, "order book 10x final hold duration");
            replay.TickMicroseconds(11_200);
            AssertEqual("market:B", replay.Cursor.Batch.Identity, "order book FIFO second identity");
            AssertEqual(false, replay.Enqueue(batchA), "completed order book batch cannot reappear");
        }

        private static void ValidateKoreaHistoryV1()
        {
            const string registryPath = "Assets/FamilyCompany/Content/History/company_registry_korea_2000_2026.json";
            var registry = KoreaHistoryV1RegistryLoader.FromJson(File.ReadAllText(registryPath));
            AssertEqual(1, registry.SchemaVersion, "Korea History schema version");
            AssertEqual(83, registry.Companies.Count, "Korea History registry rows");
            AssertEqual(82, registry.Companies.Count(company => company.CountryCode == "KR"), "Korea History domestic companies");
            var startDate = new DateTime(2000, 1, 3);
            AssertEqual("삼성전자", registry.Get("kr_samsung_electronics").DisplayNameAt(startDate), "Samsung historical display name");
            AssertEqual("한국통신", registry.Get("kr_kt").DisplayNameAt(startDate), "KT historical display name");
            var securities = registry.ListedSecuritiesAt(startDate);
            AssertEqual(10, securities.Count, "Korea History listed securities at campaign start");
            AssertEqual(true, securities.Any(item => item.CompanyId == "kr_samsung_electronics" && item.Ticker == "005930"), "Samsung market security");
            AssertEqual(true, securities.Any(item => item.CompanyId == "kr_daum" && item.DisplayNameKo == "다음커뮤니케이션"), "Daum market security");
            AssertEqual(
                MarketPricingRules.GrowthMarketName,
                securities.First(item => item.CompanyId == "kr_daum").PriceRuleMarket,
                "KOSDAQ price rule market");
        }

        /// <summary>
        /// The subcontract-to-own-product loop: a finished contract must pay cash AND teach a named
        /// technology, the two must stay separate, and the technology must survive a save round trip.
        /// </summary>
        private static void ValidateContractTechnologyRewards()
        {
            // Every technology in the catalog has to be reachable by doing work, otherwise a product
            // requirement could be impossible to satisfy through contracts alone.
            var taught = new HashSet<string>(ContractTechnologyGrantCatalog.TaughtTechnologyIds, StringComparer.Ordinal);
            foreach (var definition in CompanyTechnologyCatalog.All)
            {
                Require(taught.Contains(definition.TechnologyId),
                    $"Technology is not taught by any subcontract: {definition.TechnologyId}");
            }

            AssertEqual(
                BootstrapContractCatalog.TotalOfferTemplateCount,
                ContractTechnologyGrantCatalog.TemplateCount,
                "every bootstrap contract declares its technology grants");

            // Levels: no points is 미습득, the first point earned is Lv1, and each 100 adds a level.
            AssertEqual(0, CompanyTechnologyCatalog.LevelFor(0), "level at zero points");
            AssertEqual(1, CompanyTechnologyCatalog.LevelFor(1), "level at first point");
            AssertEqual(1, CompanyTechnologyCatalog.LevelFor(99), "level below one hundred");
            AssertEqual(2, CompanyTechnologyCatalog.LevelFor(100), "level at one hundred");
            AssertEqual(
                CompanyTechnologyCatalog.MaximumLevel,
                CompanyTechnologyCatalog.LevelFor(100_000),
                "level is capped");

            var state = new CompanyTechnologyState();
            var grants = ContractTechnologyGrantCatalog.ForTemplateIndex(2);
            Require(grants.Count > 0, "the word DB contract teaches something");
            Require(grants.Any(item => item.TechnologyId == CompanyTechnologyIds.DatabaseDesign),
                "the word DB contract teaches DB design");
            var gains = state.ApplyGrants(grants);
            AssertEqual(grants.Count, gains.Count, "one gain record per grant");
            AssertEqual(
                grants.First(item => item.TechnologyId == CompanyTechnologyIds.DatabaseDesign).Points,
                state.PointsFor(CompanyTechnologyIds.DatabaseDesign),
                "granted points land on the technology");

            // Repeating the same job levels it up; the reward is cumulative and never resets.
            for (var repeat = 0; repeat < 3; repeat++) state.ApplyGrants(grants);
            Require(state.LevelFor(CompanyTechnologyIds.DatabaseDesign) >= 2,
                "repeating a contract raises the technology level");

            var save = new GameSaveDto();
            AssertEqual(0, save.growth.technologyPoints.Count, "a new save carries no technology");

            var source = PrototypeStateFactory.Create();
            source.Growth.Technology.ApplyGrants(grants);
            var json = JsonUtility.ToJson(GameSaveMapper.ToDto(source));
            var restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            AssertEqual(
                source.Growth.Technology.PointsFor(CompanyTechnologyIds.DatabaseDesign),
                restored.Growth.Technology.PointsFor(CompanyTechnologyIds.DatabaseDesign),
                "technology points survive a save round trip");

            // Money and technology must not be the same number: a contract card reports them apart.
            var card = ContractBusinessViewModelRules.Won(720_000);
            Require(!string.IsNullOrEmpty(card), "money label is formatted");
            var technologyLine = ContractTechnologyGrantCatalog.DisplayKo(2);
            Require(technologyLine.Contains("pt"), "technology label reports points, not money");
            Require(!technologyLine.Contains("₩"), "technology label never carries a cash amount");

            ValidateContractTechnologyGates();
            ValidateContractTechnologyBonus();

            Debug.Log(
                $"CONTRACT_TECHNOLOGY_REWARD_VALIDATION: PASS | technologies={CompanyTechnologyCatalog.All.Count} " +
                $"contracts={ContractTechnologyGrantCatalog.TemplateCount} schema=11");
        }

        /// <summary>
        /// Experience has to pay off while the work is being done, not only at the unlock gate. The
        /// same job must take strictly fewer game minutes for a practised company than for a new one,
        /// and the bonus must never run the other way.
        /// </summary>
        private static void ValidateContractTechnologyBonus()
        {
            var grants = ContractTechnologyGrantCatalog.ForTemplateIndex(2);
            var novice = new CompanyTechnologyState();
            AssertEqual(
                CompanyTechnologyBonusRules.NeutralBasisPoints,
                CompanyTechnologyBonusRules.WorkRateBasisPoints(novice, grants),
                "a company with no experience works at the neutral rate");
            AssertEqual(0, CompanyTechnologyBonusRules.QualityBonus(novice, grants), "no experience, no quality bonus");

            // One contract's worth of points is level 1, which is still neutral: the payoff starts
            // when the company has genuinely repeated the work.
            var first = new CompanyTechnologyState();
            first.ApplyGrants(grants);
            AssertEqual(
                CompanyTechnologyBonusRules.NeutralBasisPoints,
                CompanyTechnologyBonusRules.WorkRateBasisPoints(first, grants),
                "the first contract does not yet speed up the next one");

            var practised = new CompanyTechnologyState();
            for (var repeat = 0; repeat < 12; repeat++) practised.ApplyGrants(grants);
            var practisedRate = CompanyTechnologyBonusRules.WorkRateBasisPoints(practised, grants);
            Require(practisedRate > CompanyTechnologyBonusRules.NeutralBasisPoints,
                "repeating a job speeds up the next one");
            Require(CompanyTechnologyBonusRules.QualityBonus(practised, grants) > 0,
                "repeating a job improves quality");

            var master = new CompanyTechnologyState();
            for (var repeat = 0; repeat < 200; repeat++) master.ApplyGrants(grants);
            var masterRate = CompanyTechnologyBonusRules.WorkRateBasisPoints(master, grants);
            AssertEqual(
                CompanyTechnologyBonusRules.NeutralBasisPoints +
                (CompanyTechnologyCatalog.MaximumLevel - 1) * CompanyTechnologyBonusRules.WorkRateBasisPointsPerLevel,
                masterRate,
                "the work rate bonus is capped at full mastery");

            // The bonus has to actually shorten the job, and never lengthen it.
            const int neutralMinutes = 60;
            var noviceMinutes = CompanyTechnologyBonusRules.ApplyWorkRate(
                neutralMinutes, CompanyTechnologyBonusRules.WorkRateBasisPoints(novice, grants));
            var masterMinutes = CompanyTechnologyBonusRules.ApplyWorkRate(neutralMinutes, masterRate);
            AssertEqual(neutralMinutes, noviceMinutes, "no experience costs the neutral minutes");
            Require(masterMinutes < noviceMinutes, "a mastered job costs fewer game minutes");
            Require(masterMinutes >= 1, "a person hour always costs at least one game minute");

            // Deterministic: the same history always produces the same rate.
            var replay = new CompanyTechnologyState();
            for (var repeat = 0; repeat < 12; repeat++) replay.ApplyGrants(grants);
            AssertEqual(
                practisedRate,
                CompanyTechnologyBonusRules.WorkRateBasisPoints(replay, grants),
                "the work rate is a pure function of the technology history");

            Debug.Log(
                $"CONTRACT_TECHNOLOGY_BONUS_VALIDATION: PASS | neutral={noviceMinutes}m " +
                $"mastered={masterMinutes}m rateCap={masterRate}bp");
        }

        /// <summary>
        /// The higher subcontracts ask for proven technology. That ladder has to be climbable from
        /// nothing: simulating only the jobs a company is currently allowed to take must eventually
        /// unlock every gated job. Otherwise a save can reach a state where no further work exists.
        /// </summary>
        private static void ValidateContractTechnologyGates()
        {
            var open = Enumerable.Range(0, BootstrapContractCatalog.TotalOfferTemplateCount)
                .Where(ContractTechnologyRequirementCatalog.IsOpenToEveryone)
                .ToArray();
            Require(open.Length > 0, "some subcontracts must be open to a company with no technology");

            var technology = new CompanyTechnologyState();
            var unlocked = new HashSet<int>(open);
            // Repeatedly take every currently available job. Each pass can only add technology, so
            // this converges; the bound just stops a broken table from looping forever.
            for (var pass = 0; pass < 40; pass++)
            {
                foreach (var index in unlocked.ToArray())
                    technology.ApplyGrants(ContractTechnologyGrantCatalog.ForTemplateIndex(index));
                for (var index = 0; index < BootstrapContractCatalog.TotalOfferTemplateCount; index++)
                {
                    if (unlocked.Contains(index)) continue;
                    if (ContractTechnologyRequirementCatalog.ForTemplateIndex(index).AllMetBy(technology))
                        unlocked.Add(index);
                }

                if (unlocked.Count == BootstrapContractCatalog.TotalOfferTemplateCount) break;
            }

            var blocked = Enumerable.Range(0, BootstrapContractCatalog.TotalOfferTemplateCount)
                .Where(index => !unlocked.Contains(index))
                .ToArray();
            Require(blocked.Length == 0,
                "subcontracts unreachable from the open jobs: " + string.Join(",", blocked));

            // Every own-product technology bar must also be reachable through contract work alone.
            foreach (var product in ProductOpportunityRules.All)
            {
                foreach (var requirement in product.RequiredTechnologyLevels)
                {
                    Require(technology.HasLevel(requirement.TechnologyId, requirement.RequiredLevel),
                        $"product requirement unreachable through contracts: {product.ProductPathId} {requirement.DisplayKo}");
                }
            }

            Debug.Log(
                $"CONTRACT_TECHNOLOGY_GATE_VALIDATION: PASS | open={open.Length} " +
                $"gated={ContractTechnologyRequirementCatalog.GatedTemplateIndices.Count} " +
                $"reachable={unlocked.Count}/{BootstrapContractCatalog.TotalOfferTemplateCount}");
        }

        private static void ValidateSaveRoundTrip()
        {
            var source = PrototypeStateFactory.Create(314159);
            new SimulationRunner(source).AdvanceMinutes(10);
            var offer = BootstrapContractCatalog.CreateOffer(
                source.WorldSeed,
                "save-validation-client",
                "저장 검증용 고객사",
                7);
            var acceptance = source.Contracts.Accept(offer, source.Company, source.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "save contract acceptance");
            var saveMember = source.Family.Get("older_sister");
            var saveTask = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(offer));
            var partialHours = Math.Min(3, offer.EstimatedPersonHours);
            var contributionMinute = checked(source.Time.ElapsedMinutes + (long)partialHours *
                WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(saveMember.Capability, saveTask));
            new SimulationRunner(source).AdvanceMinutes(contributionMinute - source.Time.ElapsedMinutes);
            var work = source.Contracts.RecordWork(
                offer.OfferId,
                "older_sister",
                partialHours,
                source.Time.ElapsedMinutes,
                source.Family,
                source.Company);
            AssertEqual(true, work.Applied, "save contract partial work");
            new SimulationRunner(source).AdvanceMinutes(1500);
            var json = JsonUtility.ToJson(GameSaveMapper.ToDto(source));
            var restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            AssertEqual(source.WorldSeed, restored.WorldSeed, "save seed");
            AssertEqual(source.Time.ElapsedMinutes, restored.Time.ElapsedMinutes, "save time");
            AssertEqual(source.Company.CashWon, restored.Company.CashWon, "save cash");
            AssertEqual(source.Family.Get("older_sister").Energy, restored.Family.Get("older_sister").Energy, "save sister energy");
            AssertEqual(source.Events.Count, restored.Events.Count, "save event count");
            AssertEqual(11, JsonUtility.FromJson<GameSaveDto>(json).schemaVersion, "save schema version");
            AssertEqual(source.OfficeGrid.ComputeLayoutHash(), restored.OfficeGrid.ComputeLayoutHash(), "office grid layout hash");
            AssertEqual(source.Contracts.Contracts.Count, restored.Contracts.Contracts.Count, "save contract count");
            var restoredContract = restored.Contracts.Get(offer.OfferId);
            AssertEqual(acceptance.Contract.Status, restoredContract.Status, "save contract status");
            AssertEqual(acceptance.Contract.CompletedPersonHours, restoredContract.CompletedPersonHours, "save contract work");
            AssertEqual(acceptance.Contract.Contributions.Count, restoredContract.Contributions.Count, "save contract contributions");
        }

        private static void ValidateSaveSlots()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"family-company-save-slots-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                for (var slot = UnityJsonSaveRepository.MinimumSlot; slot <= UnityJsonSaveRepository.MaximumSlot; slot++)
                {
                    var repository = new UnityJsonSaveRepository(slot, directory);
                    AssertEqual(false, repository.Exists, $"empty save slot {slot}");
                    var state = PrototypeStateFactory.Create(20000103 + slot);
                    new SimulationRunner(state).AdvanceMinutes(slot * 60);
                    repository.Save(GameSaveMapper.ToDto(state));
                    AssertEqual(true, repository.Exists, $"written save slot {slot}");
                    AssertEqual(true, repository.TryLoad(out var restored), $"load save slot {slot}");
                    AssertEqual(state.WorldSeed, restored.worldSeed, $"save slot {slot} seed");
                    AssertEqual(state.Time.ElapsedMinutes, restored.elapsedMinutes, $"save slot {slot} time");
                }

                var firstSlot = new UnityJsonSaveRepository(1, directory);
                firstSlot.Save(GameSaveMapper.ToDto(PrototypeStateFactory.Create(999)));
                AssertEqual(true, File.Exists(firstSlot.Location + ".bak"), "save slot backup");

                var legacyDirectory = Path.Combine(directory, "legacy");
                Directory.CreateDirectory(legacyDirectory);
                File.WriteAllText(
                    Path.Combine(legacyDirectory, "family-company-prototype-save.json"),
                    JsonUtility.ToJson(GameSaveMapper.ToDto(PrototypeStateFactory.Create(777)), true));
                var legacySlot = new UnityJsonSaveRepository(1, legacyDirectory);
                AssertEqual(true, legacySlot.Exists, "legacy save slot detection");
                AssertEqual(true, legacySlot.TryLoad(out var legacy), "legacy save slot load");
                AssertEqual(777, legacy.worldSeed, "legacy save slot seed");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void ValidateWideFrontendSettings()
        {
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            if (!projectSettings.Contains("defaultScreenWidth: 1920") ||
                !projectSettings.Contains("defaultScreenHeight: 1080") ||
                !projectSettings.Contains("defaultScreenWidthWeb: 1280") ||
                !projectSettings.Contains("defaultScreenHeightWeb: 720") ||
                !projectSettings.Contains("resizableWindow: 1") ||
                !projectSettings.Contains("allowFullscreenSwitch: 1") ||
                !projectSettings.Contains("fullscreenMode: 1"))
            {
                throw new InvalidOperationException("Wide fullscreen player settings are incomplete.");
            }
        }

        private static void ValidateContractLifecycle()
        {
            var state = PrototypeStateFactory.Create();
            new SimulationRunner(state).AdvanceMinutes(10);
            var offer = new SubcontractOffer(
                "lifecycle-contract",
                "lifecycle-validation-client",
                "계약 생명주기 검증용 고객사",
                ContractServiceType.DataEntryAndQualityAssurance,
                "소형 상품 데이터 입력",
                4,
                20,
                7,
                100_000,
                900_000,
                0);
            var acceptance = state.Contracts.Accept(offer, state.Company, state.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "contract accepted");
            AssertEqual(4_900_000L, state.Company.CashWon, "contract upfront cash");
            var memberIds = new[] { "player", "older_sister", "father", "mother" };
            var task = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(offer));
            var sharedWorkMinute = checked(state.Time.ElapsedMinutes + memberIds.Max(memberId =>
                5L * WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(
                    state.Family.Get(memberId).Capability,
                    task)));
            new SimulationRunner(state).AdvanceMinutes(sharedWorkMinute - state.Time.ElapsedMinutes);
            ContractWorkResult finalWork = null;
            foreach (var memberId in memberIds)
            {
                finalWork = state.Contracts.RecordWork(
                    offer.OfferId,
                    memberId,
                    5,
                    state.Time.ElapsedMinutes,
                    state.Family,
                    state.Company);
            }

            AssertEqual(true, finalWork != null && finalWork.Completed, "contract completion");
            AssertEqual(900_000L, finalWork.RewardWon, "contract settlement reward");
            AssertEqual(5_800_000L, state.Company.CashWon, "contract settled cash");
            AssertEqual(2, state.Company.Reputation, "contract completion reputation");
            AssertEqual(SubcontractStatus.Completed, acceptance.Contract.Status, "contract completed status");
            AssertEqual(20, acceptance.Contract.CompletedPersonHours, "contract completed hours");
            AssertEqual(4, acceptance.Contract.Contributions.Count, "contract contributor count");

            var duplicate = state.Contracts.Accept(offer, state.Company, state.Time.ElapsedMinutes);
            AssertEqual(false, duplicate.Accepted, "duplicate contract acceptance");
            AssertEqual(ContractRejectionReason.DuplicateOffer, duplicate.Decision.RejectionReason, "duplicate contract reason");
            foreach (var transaction in state.Company.Ledger)
            {
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "contract ledger balance");
            }

            var overdueState = PrototypeStateFactory.Create(20000104);
            overdueState.Company.ChangeReputation(10);
            var overdueOffer = new SubcontractOffer(
                "overdue-contract",
                "overdue-validation-client",
                "기한초과 검증용 고객사",
                ContractServiceType.WebsiteMaintenance,
                "긴급 홈페이지 갱신",
                2,
                16,
                1,
                50_000,
                500_000,
                0);
            AssertEqual(true, overdueState.Contracts.Accept(
                overdueOffer,
                overdueState.Company,
                overdueState.Time.ElapsedMinutes).Accepted, "overdue contract accepted");
            new SimulationRunner(overdueState).AdvanceMinutes(1441);
            AssertEqual(SubcontractStatus.Failed, overdueState.Contracts.Get(overdueOffer.OfferId).Status, "overdue contract failed");
            AssertEqual(8, overdueState.Company.Reputation, "overdue reputation penalty");
        }

        private static void ValidateFourPersonContractScope()
        {
            var state = PrototypeStateFactory.Create();
            var policy = new SmallTeamContractPolicy(state.Family.Members.Count);
            for (var sequence = 0; sequence < 32; sequence++)
            {
                var offer = BootstrapContractCatalog.CreateOffer(
                    state.WorldSeed,
                    "validation-client",
                    "계약 검증용 고객사",
                    sequence);
                var decision = policy.Evaluate(
                    offer,
                    state.Company.CashWon,
                    state.Company.Reputation,
                    0,
                    0);
                if (offer.ReputationRequired == 0)
                {
                    AssertEqual(true, decision.CanAccept, $"starter contract {offer.OfferId} acceptance");
                }
                else
                {
                    AssertEqual(false, decision.CanAccept, $"gated contract {offer.OfferId} acceptance");
                    AssertEqual(
                        ContractRejectionReason.ReputationInsufficient,
                        decision.RejectionReason,
                        $"gated contract {offer.OfferId} reason");
                }

                if (offer.RequiredWorkers > 4 || offer.EstimatedPersonHours > 80 || offer.RewardWon > 2_500_000)
                {
                    throw new InvalidOperationException("Starter contract exceeds the four-person bootstrap scope.");
                }

                var replay = BootstrapContractCatalog.CreateOffer(
                    state.WorldSeed,
                    "validation-client",
                    "계약 검증용 고객사",
                    sequence);
                AssertEqual(offer.OfferId, replay.OfferId, "contract deterministic ID");
                AssertEqual(offer.ServiceType, replay.ServiceType, "contract deterministic template");
            }

            var oversized = new SubcontractOffer(
                "oversized",
                "validation-client",
                "계약 검증용 고객사",
                ContractServiceType.SmallBusinessTool,
                "대기업 전사 시스템 구축",
                12,
                1000,
                30,
                10_000_000,
                100_000_000,
                0);
            var rejected = policy.Evaluate(
                oversized,
                state.Company.CashWon,
                state.Company.Reputation,
                0,
                0);
            AssertEqual(false, rejected.CanAccept, "oversized contract acceptance");
            AssertEqual(ContractRejectionReason.TeamTooSmall, rejected.RejectionReason, "oversized contract reason");
        }

        private static void ValidateAssetsAndScene()
        {
            HighMotionCharacterArtBuilder.Validate();
            var sisterFrames = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.SisterFrameFolder });
            AssertEqual(48, sisterFrames.Length, "sister high-motion directional frame count");
            var playerFrames = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.PlayerFrameFolder });
            AssertEqual(48, playerFrames.Length, "player high-motion directional frame count");
            var officeModules = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.OfficeModuleFolder });
            AssertEqual(12, officeModules.Length, "office pixel module count");
            var titleHero = AssetDatabase.LoadAssetAtPath<Texture2D>(PrototypeProjectBuilder.TitleHeroAssetPath);
            if (titleHero == null || titleHero.width < 1600 || titleHero.height < 900)
            {
                throw new InvalidOperationException("Widescreen generated title hero is missing or too small.");
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeProjectBuilder.ScenePath);
            if (scene == null) throw new InvalidOperationException("Prototype scene is missing.");

            CharacterOfficeRuntimeQa.ValidateSceneLinkage();

            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
            var camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                throw new InvalidOperationException("Orthographic main camera is missing.");
            }

            if (camera.GetComponent<Presentation.Unity.PixelatedCameraEffect>() == null)
            {
                throw new InvalidOperationException("Pixelated camera effect is missing.");
            }

            var playerController = UnityEngine.Object.FindFirstObjectByType<Presentation.Unity.PrototypePlayerController>();
            if (playerController == null || playerController.GetComponent<Presentation.Unity.DirectionalSpriteAnimator>() == null)
            {
                throw new InvalidOperationException("Player pixel movement visual is missing.");
            }

            var bootstrap = UnityEngine.Object.FindFirstObjectByType<Presentation.Unity.PrototypeBootstrap>();
            if (bootstrap == null) throw new InvalidOperationException("Prototype bootstrap is missing.");
            var historyCatalog = UnityEngine.Object.FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
            if (historyCatalog == null || !historyCatalog.IsConfigured)
                throw new InvalidOperationException("Korea History V1 runtime catalog is missing from the prototype scene.");
            historyCatalog.InitializeNow();
            AssertEqual(83, historyCatalog.Registry.Companies.Count, "scene Korea History registry rows");
            AssertEqual(10, historyCatalog.ListedSecuritiesAt(new DateTime(2000, 1, 3)).Count, "scene campaign-start securities");
            bootstrap.InitializeNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.MainMenu, bootstrap.UiScreen, "initial frontend screen");
            bootstrap.StartNewGameNow(2, false);
            AssertEqual(true, bootstrap.HasSession, "new game session");
            AssertEqual(2, bootstrap.ActiveSlot, "new game slot");
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Playing, bootstrap.UiScreen, "new game frontend screen");
            bootstrap.ShowManagementNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Management, bootstrap.UiScreen, "management overlay screen");
            bootstrap.CloseManagementNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Playing, bootstrap.UiScreen, "office observation return screen");
            bootstrap.ShowPauseMenuNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.PauseMenu, bootstrap.UiScreen, "pause frontend screen");
            bootstrap.ResumeGameNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Playing, bootstrap.UiScreen, "resume frontend screen");
            var coordinator = bootstrap.InitializeOfficeTaskBridgeNow();
            if (playerController.GetComponent<Presentation.Unity.PlayerOfficeWorkInteractor>() == null)
            {
                throw new InvalidOperationException("Player direct office work interaction is missing.");
            }
            if (UnityEngine.Object.FindFirstObjectByType<Presentation.Unity.OfficeAutonomyCoordinator>() == null)
            {
                throw new InvalidOperationException("Office autonomy coordinator is missing.");
            }
            OfficeSeatingBuilderValidation.ValidateCurrentScene();
            var agents = UnityEngine.Object.FindObjectsByType<Presentation.Unity.OfficeWorkerAgent>(FindObjectsSortMode.None);
            if (agents.Length < 3)
            {
                throw new InvalidOperationException($"Expected at least three moving office agents, got {agents.Length}.");
            }

            foreach (var agent in agents)
            {
                if (agent.RouteCount < 4)
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} has an incomplete route.");
                }
            }

            if (agents.All(agent => agent.AgentId != "older_sister"))
            {
                throw new InvalidOperationException("Moving older sister agent is missing.");
            }

            if (agents.All(agent => agent.AgentId != "father") || agents.All(agent => agent.AgentId != "mother"))
            {
                throw new InvalidOperationException("Moving parent pixel agents are missing.");
            }

            if (agents.Where(agent => agent.AgentId == "father" || agent.AgentId == "mother")
                .Any(agent => agent.GetComponent<Presentation.Unity.DirectionalSpriteAnimator>() == null))
                throw new InvalidOperationException("Parent high-motion pixel animation is missing.");

            if (agents.Any(agent => agent.AgentId == "employee_a" || agent.AgentId == "employee_b"))
            {
                throw new InvalidOperationException("The four-person starting company still contains hired employee placeholders.");
            }

            foreach (var movingAgent in agents)
            {
                foreach (var candidate in agents)
                {
                    candidate.GetComponent<CharacterController>().enabled = candidate == movingAgent;
                }

                movingAgent.InitializeNow();
                var start = movingAgent.transform.position;
                for (var index = 0; index < 600; index++)
                {
                    movingAgent.Tick(0.05f);
                }

                if (Vector3.Distance(start, movingAgent.transform.position) < 0.5f || movingAgent.CompletedStops < 1)
                {
                    throw new InvalidOperationException(
                        $"Agent {movingAgent.AgentId} did not physically traverse the office route.");
                }
            }

            ValidatePhysicalContractWork(bootstrap, coordinator, agents);
        }

        private static void ValidatePhysicalContractWork(
            Presentation.Unity.PrototypeBootstrap bootstrap,
            Presentation.Unity.OfficeContractTaskCoordinator coordinator,
            Presentation.Unity.OfficeWorkerAgent[] agents)
        {
            if (coordinator == null) throw new InvalidOperationException("Office contract task coordinator is missing.");
            bootstrap.AdvanceTimeNow(10);
            var offer = new SubcontractOffer(
                "physical-office-contract",
                "physical-validation-client",
                "실제 이동 검증용 고객사",
                ContractServiceType.WebsiteMaintenance,
                "홈페이지 출력물 최종 확인",
                1,
                4,
                2,
                50_000,
                300_000,
                0);
            var acceptance = bootstrap.State.Contracts.Accept(
                offer,
                bootstrap.State.Company,
                bootstrap.State.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "physical contract accepted");

            var sister = agents.First(agent => agent.AgentId == "older_sister");
            foreach (var candidate in agents)
            {
                candidate.GetComponent<CharacterController>().enabled = candidate == sister;
            }

            sister.InitializeNow();
            coordinator.ResetAssignments();
            coordinator.InitializeNow();
            var start = sister.transform.position;
            AssertEqual(true, coordinator.AssignContractWork(offer.OfferId, "older_sister", 4), "physical contract assigned");
            var task = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(offer));
            bootstrap.AdvanceTimeNow(checked(4 * WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(
                bootstrap.State.Family.Get("older_sister").Capability,
                task)));
            for (var index = 0; index < 1600 && coordinator.CompletedTaskCount == 0; index++)
            {
                sister.Tick(0.05f);
            }

            if (Vector3.Distance(start, sister.transform.position) < 0.5f)
            {
                throw new InvalidOperationException("Assigned family member did not physically move to contract work.");
            }

            AssertEqual(1, coordinator.CompletedTaskCount, "physical task completion count");
            AssertEqual(offer.OfferId, coordinator.LastCompletedOfferId, "physical task offer ID");
            AssertEqual(true, coordinator.LastWorkResult != null && coordinator.LastWorkResult.Completed, "physical task contract completion");
            AssertEqual(SubcontractStatus.Completed, bootstrap.State.Contracts.Get(offer.OfferId).Status, "physical contract status");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }

        private static void AssertApproximately(double expected, double actual, double tolerance, string label)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }
}

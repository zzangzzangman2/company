using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Simulation.Market
{
    public enum MarketOrderBookSide
    {
        Ask,
        Bid,
    }

    public sealed class MarketOrderBookLevel
    {
        public MarketOrderBookLevel(
            MarketOrderBookSide side,
            long price,
            int quantity,
            bool isWall = false,
            double structuralStrength = 1d,
            bool isStructuralWall = false,
            bool isStructuralBreached = false,
            int queueRecoveryTargetQuantity = 0)
        {
            Side = side;
            Price = price;
            Quantity = quantity;
            IsWall = isWall;
            StructuralStrength = structuralStrength;
            IsStructuralWall = isStructuralWall;
            IsStructuralBreached = isStructuralBreached;
            QueueRecoveryTargetQuantity = queueRecoveryTargetQuantity;
        }

        public MarketOrderBookSide Side { get; }
        public long Price { get; }
        public int Quantity { get; }
        public bool IsWall { get; }
        public double StructuralStrength { get; }
        public bool IsStructuralWall { get; }
        public bool IsStructuralBreached { get; }
        public int QueueRecoveryTargetQuantity { get; }
    }

    public sealed class MarketOrderBookSnapshot
    {
        public MarketOrderBookSnapshot(
            IReadOnlyList<MarketOrderBookLevel> asks,
            IReadOnlyList<MarketOrderBookLevel> bids,
            double turnoverEok,
            double fullDayTurnoverEok,
            int executionCapacity,
            IReadOnlyDictionary<long, double> appliedAskConsumptionByPrice = null,
            IReadOnlyDictionary<long, double> appliedBidConsumptionByPrice = null,
            int appliedCapacityConsumptionUnits = 0,
            long? sourceLastTradePrice = null)
        {
            Asks = asks ?? throw new ArgumentNullException(nameof(asks));
            Bids = bids ?? throw new ArgumentNullException(nameof(bids));
            TurnoverEok = turnoverEok;
            FullDayTurnoverEok = fullDayTurnoverEok;
            ExecutionCapacity = executionCapacity;
            AppliedAskConsumptionByPrice = CopyConsumptionMap(appliedAskConsumptionByPrice);
            AppliedBidConsumptionByPrice = CopyConsumptionMap(appliedBidConsumptionByPrice);
            AppliedCapacityConsumptionUnits = Math.Max(0, appliedCapacityConsumptionUnits);
            SourceLastTradePrice = sourceLastTradePrice;

            var totalAsk = 0;
            for (var index = 0; index < Asks.Count; index += 1)
                totalAsk = checked(totalAsk + Math.Max(0, Asks[index].Quantity));
            var totalBid = 0;
            for (var index = 0; index < Bids.Count; index += 1)
                totalBid = checked(totalBid + Math.Max(0, Bids[index].Quantity));
            TotalAskQuantity = totalAsk;
            TotalBidQuantity = totalBid;
            TradeStrength = totalAsk <= 0
                ? totalBid > 0 ? 240d : 100d
                : Math.Max(20d, Math.Min(240d, (double)totalBid / totalAsk * 100d));
        }

        public IReadOnlyList<MarketOrderBookLevel> Asks { get; }
        public IReadOnlyList<MarketOrderBookLevel> Bids { get; }
        public double TurnoverEok { get; }
        public double FullDayTurnoverEok { get; }
        public int ExecutionCapacity { get; }
        public int TotalAskQuantity { get; }
        public int TotalBidQuantity { get; }
        public double TradeStrength { get; }
        public IReadOnlyDictionary<long, double> AppliedAskConsumptionByPrice { get; }
        public IReadOnlyDictionary<long, double> AppliedBidConsumptionByPrice { get; }
        public int AppliedCapacityConsumptionUnits { get; }
        public long? SourceLastTradePrice { get; }

        private static IReadOnlyDictionary<long, double> CopyConsumptionMap(
            IReadOnlyDictionary<long, double> source)
        {
            var copy = new Dictionary<long, double>();
            if (source != null)
            {
                foreach (var entry in source)
                    copy[entry.Key] = entry.Value;
            }
            return new ReadOnlyDictionary<long, double>(copy);
        }
    }

    public readonly struct MarketOrderBookLevelFill
    {
        public MarketOrderBookLevelFill(int levelIndex, long price, int quantity)
        {
            LevelIndex = levelIndex;
            Price = price;
            Quantity = quantity;
        }

        public int LevelIndex { get; }
        public long Price { get; }
        public int Quantity { get; }
    }

    public sealed class MarketOrderBookFillPlan
    {
        public MarketOrderBookFillPlan(
            MarketOrderBookSide levelSide,
            IReadOnlyList<MarketOrderBookLevelFill> fills,
            int filledQuantity,
            long notional,
            double averagePrice,
            long worstPrice)
        {
            LevelSide = levelSide;
            Fills = fills;
            FilledQuantity = filledQuantity;
            Notional = notional;
            AveragePrice = averagePrice;
            WorstPrice = worstPrice;
        }

        public MarketOrderBookSide LevelSide { get; }
        public IReadOnlyList<MarketOrderBookLevelFill> Fills { get; }
        public int FilledQuantity { get; }
        public long Notional { get; }
        public double AveragePrice { get; }
        public long WorstPrice { get; }
        public bool HasFill => FilledQuantity > 0;
    }

    public readonly struct MarketOrderBookPriceTransitionFill
    {
        public MarketOrderBookPriceTransitionFill(
            MarketOrderBookSide side,
            long price,
            int quantity,
            int remainingQuantity,
            bool structuralBreach,
            bool boundaryCrossed)
        {
            Side = side;
            Price = price;
            Quantity = quantity;
            RemainingQuantity = remainingQuantity;
            StructuralBreach = structuralBreach;
            BoundaryCrossed = boundaryCrossed;
        }

        public MarketOrderBookSide Side { get; }
        public long Price { get; }
        public int Quantity { get; }
        public int RemainingQuantity { get; }
        public bool StructuralBreach { get; }
        public bool BoundaryCrossed { get; }
    }

    public sealed class MarketOrderBookPriceTransition
    {
        public MarketOrderBookPriceTransition(
            long price,
            IReadOnlyDictionary<long, int> consumedAskByPrice,
            IReadOnlyDictionary<long, int> consumedBidByPrice,
            int consumedUnits,
            bool targetReached,
            IReadOnlyList<MarketOrderBookPriceTransitionFill> orderedFills)
        {
            Price = price;
            ConsumedAskByPrice = consumedAskByPrice;
            ConsumedBidByPrice = consumedBidByPrice;
            ConsumedUnits = consumedUnits;
            TargetReached = targetReached;
            OrderedFills = orderedFills;
        }

        public long Price { get; }
        public IReadOnlyDictionary<long, int> ConsumedAskByPrice { get; }
        public IReadOnlyDictionary<long, int> ConsumedBidByPrice { get; }
        public int ConsumedUnits { get; }
        public bool TargetReached { get; }
        public IReadOnlyList<MarketOrderBookPriceTransitionFill> OrderedFills { get; }
    }

    /// <summary>
    /// Literal port of SIMUL order_book.dart cadence, frame, capacity, fill and
    /// price-walk primitives. Change only when a Dart-generated golden fixture
    /// is deliberately regenerated and the parity validator is updated.
    /// </summary>
    public static class MarketOrderBookRules
    {
        public const int LevelCount = 10;
        public const double StandingDepthMinutes = 0.45d;
        public const double MinuteTurnoverShare = 0.25d;
        public const double OrderTurnoverShare = 0.02d;
        public const int VisualCadenceDivisor = 3;
        public const int MinimumPulsesPerMarketMinute = 1;
        public const int MaximumOrdinaryPulsesPerMarketMinute = 4;
        public const int FastPulsesPerMarketMinute = 5;
        public const int MaximumPulsesPerMarketMinute = 7;
        public const int PulseFrameStride = 64;
        public const double SparseFullDayTurnoverEok = 75d;
        public const double SeverelySparseFullDayTurnoverEok = 20d;
        public const int MinimumDisplayedQuantity = 10;
        public const double MaximumQuoteOutstandingRate = 0.05d;
        public const int MaximumQuoteAbsoluteUnits = 100_000_000;
        public const double StructuralConsumptionBreachRatio = 0.90d;
        public const int MinimumImbalanceSamplePrints = 3;
        public const double MinimumImbalanceSampleTurnoverEok = 0.10d;
        public const int MaximumSyntheticPrintsPerPulse = 12;

        private const int FastMoveTicks = 3;

        public static int PulsesPerMarketMinute(
            double fullDayTurnoverEok,
            double currentPrice,
            double previousTradePrice,
            double previousClose,
            string market = "main",
            double executionStrength = 100d,
            int executionSamplePrints = 0,
            double executionSampleTurnoverEok = 0d,
            bool tradingSessionActive = true,
            bool playbackActive = true)
        {
            if (!tradingSessionActive || !playbackActive) return 0;
            var turnover = IsFinite(fullDayTurnoverEok)
                ? Math.Max(0d, fullDayTurnoverEok)
                : 0d;
            int rawSlots;
            if (turnover < 20d) rawSlots = 1;
            else if (turnover < 75d) rawSlots = 1;
            else if (turnover < 200d) rawSlots = 2;
            else if (turnover < 1000d) rawSlots = 3;
            else if (turnover < 3000d) rawSlots = 5;
            else if (turnover < 7000d) rawSlots = 7;
            else if (turnover < 12000d) rawSlots = 9;
            else if (turnover < 20000d) rawSlots = 11;
            else rawSlots = 12;

            var slots = Math.Max(
                MinimumPulsesPerMarketMinute,
                RoundAwayFromZero((double)rawSlots / VisualCadenceDivisor));
            var hasPrices = IsFinite(currentPrice) && currentPrice > 0d &&
                            IsFinite(previousTradePrice) && previousTradePrice > 0d;
            var crossedTicks = hasPrices
                ? Math.Abs(
                    PriceLadderIndex(currentPrice, market) -
                    PriceLadderIndex(previousTradePrice, market))
                : 0L;
            var sessionMoveRate = IsFinite(currentPrice) && currentPrice > 0d &&
                                  IsFinite(previousClose) && previousClose > 0d
                ? Math.Abs((currentPrice - previousClose) / previousClose)
                : 0d;
            var executionImbalance = !IsFinite(executionStrength)
                ? 1d
                : executionStrength <= 0d
                    ? double.PositiveInfinity
                    : Math.Max(executionStrength / 100d, 100d / executionStrength);
            var executionAccelerationAllowed =
                turnover >= SparseFullDayTurnoverEok &&
                executionSamplePrints >= MinimumImbalanceSamplePrints &&
                IsFinite(executionSampleTurnoverEok) &&
                executionSampleTurnoverEok >= MinimumImbalanceSampleTurnoverEok;
            var fast = crossedTicks >= FastMoveTicks ||
                       sessionMoveRate >= 0.06d ||
                       executionAccelerationAllowed && executionImbalance >= 1.65d;
            var extreme = crossedTicks >= FastMoveTicks * 2 ||
                          sessionMoveRate >= 0.08d ||
                          executionAccelerationAllowed && executionImbalance >= 2.1d;
            if (extreme) slots = MaximumPulsesPerMarketMinute;
            else if (fast) slots = Math.Max(slots, FastPulsesPerMarketMinute);
            return Clamp(slots, MinimumPulsesPerMarketMinute, MaximumPulsesPerMarketMinute);
        }

        public static int LiquidityPulseFrame(int marketMinute, int slotIndex)
        {
            var minuteOffset = Math.Max(0, marketMinute - MarketSessionClock.OpenMinute);
            var safeSlot = Clamp(slotIndex, 0, MaximumPulsesPerMarketMinute);
            return checked(minuteOffset * PulseFrameStride + safeSlot);
        }

        public static int PulseSlotForFrame(int marketMinute, int liquidityPulse)
        {
            var baseFrame = LiquidityPulseFrame(marketMinute, 0);
            return Clamp(liquidityPulse - baseFrame, 0, MaximumPulsesPerMarketMinute);
        }

        public static IReadOnlyList<int> PendingPulseFrames(
            int marketMinute,
            int afterLiquidityPulse,
            int throughSlotIndex)
        {
            var afterSlot = PulseSlotForFrame(marketMinute, afterLiquidityPulse);
            var targetSlot = Clamp(throughSlotIndex, 0, MaximumPulsesPerMarketMinute);
            if (targetSlot <= afterSlot) return Array.Empty<int>();
            var frames = new List<int>(targetSlot - afterSlot);
            for (var slot = afterSlot + 1; slot <= targetSlot; slot += 1)
                frames.Add(LiquidityPulseFrame(marketMinute, slot));
            return new ReadOnlyCollection<int>(frames);
        }

        public static int CumulativeSlotCapacity(
            int executionCapacity,
            int slotIndex,
            int pulsesPerMarketMinute)
        {
            var capacity = Math.Max(0, executionCapacity);
            var pulses = Math.Max(1, pulsesPerMarketMinute);
            var slot = Clamp(slotIndex, 0, pulses);
            if (slot <= 0 || capacity <= 0) return 0;
            if (slot >= pulses) return capacity;
            return Clamp(
                RoundAwayFromZero((double)capacity * slot / pulses),
                0,
                capacity);
        }

        public static double PriceChangePercent(double price, double previousClose)
        {
            if (!IsFinite(price) || price <= 0d ||
                !IsFinite(previousClose) || previousClose <= 0d)
                return 0d;
            return (price - previousClose) / previousClose * 100d;
        }

        public static double ExecutionStrength(double buyQuantity, double sellQuantity)
        {
            var buys = IsFinite(buyQuantity) ? Math.Max(0d, buyQuantity) : 0d;
            var sells = IsFinite(sellQuantity) ? Math.Max(0d, sellQuantity) : 0d;
            if (sells <= 0d) return buys > 0d ? 999d : 100d;
            return buys / sells * 100d;
        }

        public static int MaximumQuoteQuantity(int? sharesOutstanding)
        {
            if (!sharesOutstanding.HasValue || sharesOutstanding.Value <= 0)
                return MaximumQuoteAbsoluteUnits;
            return Math.Max(
                1,
                Math.Min(
                    MaximumQuoteAbsoluteUnits,
                    (int)Math.Floor(sharesOutstanding.Value * MaximumQuoteOutstandingRate)));
        }

        public static int MinuteCapacityUnits(double turnoverEok, double unitPrice)
        {
            if (!IsFinite(turnoverEok) || turnoverEok <= 0d ||
                !IsFinite(unitPrice) || unitPrice <= 0d)
                return 0;
            var units = turnoverEok * 100_000_000d * MinuteTurnoverShare / unitPrice;
            if (double.IsNaN(units) || units <= 0d) return 0;
            if (double.IsInfinity(units)) return int.MaxValue;
            return (int)Math.Min(int.MaxValue, Math.Floor(units));
        }

        public static int NotionalLimitForTurnover(
            double turnoverEok,
            int minimum = 5_000_000,
            int maximum = 2_000_000_000)
        {
            if (!IsFinite(turnoverEok) || turnoverEok <= 0d ||
                minimum < 0 || maximum < minimum)
                return 0;
            var raw = turnoverEok * 100_000_000d * OrderTurnoverShare;
            if (!IsFinite(raw) || raw <= 0d) return 0;
            return Clamp(RoundAwayFromZero(raw), minimum, maximum);
        }

        public static MarketOrderBookFillPlan LimitFillPlan(
            MarketOrderBookSnapshot snapshot,
            bool isBuy,
            double requestedQuantity,
            long limitPrice,
            int? availableCapacity = null,
            int? maximumNotional = null,
            IReadOnlyDictionary<long, double> alreadyConsumedByPrice = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var side = isBuy ? MarketOrderBookSide.Ask : MarketOrderBookSide.Bid;
            var capacity = Math.Min(
                snapshot.ExecutionCapacity,
                availableCapacity ?? snapshot.ExecutionCapacity);
            if (!IsFinite(requestedQuantity) || requestedQuantity <= 0d ||
                limitPrice <= 0 || capacity <= 0)
                return EmptyFillPlan(side);

            var levels = isBuy ? snapshot.Asks : snapshot.Bids;
            var turnoverNotionalLimit = NotionalLimitForTurnover(snapshot.TurnoverEok);
            var notionalLimit = maximumNotional.HasValue
                ? Math.Min(maximumNotional.Value, turnoverNotionalLimit)
                : turnoverNotionalLimit;
            var fills = new List<MarketOrderBookLevelFill>();
            var remaining = Math.Min((int)Math.Min(int.MaxValue, Math.Floor(requestedQuantity)), capacity);
            var filled = 0;
            long notional = 0;
            long worstPrice = 0;
            for (var index = 0; index < levels.Count && remaining > 0; index += 1)
            {
                var level = levels[index];
                var withinLimit = isBuy ? level.Price <= limitPrice : level.Price >= limitPrice;
                if (!withinLimit) break;
                var consumedAtLevel = 0d;
                if (alreadyConsumedByPrice != null &&
                    alreadyConsumedByPrice.TryGetValue(level.Price, out var consumed))
                    consumedAtLevel = consumed;
                var safeConsumed = IsFinite(consumedAtLevel)
                    ? Math.Max(0d, consumedAtLevel)
                    : 0d;
                var availableAtLevel = (int)Math.Max(
                    0d,
                    Math.Floor(level.Quantity - safeConsumed));
                var quantity = Math.Min(availableAtLevel, remaining);
                if (notionalLimit > 0)
                {
                    var remainingBudget = (long)notionalLimit - notional;
                    if (remainingBudget <= 0) break;
                    quantity = Math.Min(quantity, (int)(remainingBudget / level.Price));
                }
                else
                {
                    break;
                }
                if (quantity <= 0) continue;
                fills.Add(new MarketOrderBookLevelFill(index, level.Price, quantity));
                filled += quantity;
                remaining -= quantity;
                notional = checked(notional + level.Price * quantity);
                worstPrice = level.Price;
            }

            return new MarketOrderBookFillPlan(
                side,
                new ReadOnlyCollection<MarketOrderBookLevelFill>(fills),
                filled,
                notional,
                filled <= 0 ? 0d : (double)notional / filled,
                worstPrice);
        }

        public static MarketOrderBookPriceTransition PriceTransitionTowardTarget(
            MarketOrderBookSnapshot snapshot,
            double previousPrice,
            double targetPrice,
            int availableUnits,
            string market)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var safePrevious = SnapFinitePrice(previousPrice, market);
            var safeTarget = SnapFinitePrice(targetPrice, market);
            if (safePrevious <= 0 || safeTarget <= 0 || safePrevious == safeTarget)
                return EmptyTransition(
                    safeTarget > 0 ? safeTarget : safePrevious,
                    safeTarget == safePrevious);

            var asks = new Dictionary<long, int>();
            var bids = new Dictionary<long, int>();
            var orderedFills = new List<MarketOrderBookPriceTransitionFill>();
            var remaining = Math.Max(0, availableUnits);
            var reachedPrice = safePrevious;
            var levels = safeTarget > safePrevious ? snapshot.Asks : snapshot.Bids;
            var side = safeTarget > safePrevious ? MarketOrderBookSide.Ask : MarketOrderBookSide.Bid;
            for (var index = 0; index < levels.Count; index += 1)
            {
                var level = levels[index];
                if (side == MarketOrderBookSide.Ask)
                {
                    if (level.Price > safeTarget) break;
                    if (level.Price < safePrevious || level.Quantity <= 0) continue;
                }
                else
                {
                    if (level.Price < safeTarget) break;
                    if (level.Price > safePrevious || level.Quantity <= 0) continue;
                }
                if (remaining <= 0) break;
                var fill = Math.Min(remaining, level.Quantity);
                if (fill <= 0) continue;
                if (side == MarketOrderBookSide.Ask) asks[level.Price] = fill;
                else bids[level.Price] = fill;
                var remainingQuantity = Math.Max(0, level.Quantity - fill);
                var recoveryBaseline = Math.Max(
                    level.Quantity,
                    level.QueueRecoveryTargetQuantity);
                var structuralBreach = !level.IsStructuralBreached &&
                                       StructuralQueueBreached(
                                           level,
                                           recoveryBaseline,
                                           remainingQuantity);
                orderedFills.Add(new MarketOrderBookPriceTransitionFill(
                    side,
                    level.Price,
                    fill,
                    remainingQuantity,
                    structuralBreach,
                    remainingQuantity <= 0 || structuralBreach));
                remaining -= fill;
                reachedPrice = level.Price;
                if (fill < level.Quantity || level.Price == safeTarget) break;
            }

            return new MarketOrderBookPriceTransition(
                reachedPrice,
                new ReadOnlyDictionary<long, int>(asks),
                new ReadOnlyDictionary<long, int>(bids),
                Math.Max(0, availableUnits - remaining),
                reachedPrice == safeTarget,
                new ReadOnlyCollection<MarketOrderBookPriceTransitionFill>(orderedFills));
        }

        /// <summary>
        /// Applies SIMUL's cumulative ledger-consumption watermarks to a raw
        /// standing book. Reapplying the same cumulative maps is idempotent.
        /// </summary>
        public static MarketOrderBookSnapshot SnapshotAfterConsumption(
            MarketOrderBookSnapshot snapshot,
            IReadOnlyDictionary<long, double> consumedAskByPrice = null,
            IReadOnlyDictionary<long, double> consumedBidByPrice = null,
            int consumedCapacityUnits = 0,
            MarketOrderBookSide? latestConsumedSide = null,
            long? latestConsumedPrice = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var asksConsumed = consumedAskByPrice ?? EmptyConsumptionMap();
            var bidsConsumed = consumedBidByPrice ?? EmptyConsumptionMap();

            var netAskLevels = NetLevels(
                snapshot.Asks,
                asksConsumed,
                snapshot.AppliedAskConsumptionByPrice);
            var netBidLevels = NetLevels(
                snapshot.Bids,
                bidsConsumed,
                snapshot.AppliedBidConsumptionByPrice);
            var exhaustedAskPrice = ExhaustedTouchPrice(
                snapshot.Asks,
                netAskLevels,
                asksConsumed,
                snapshot.AppliedAskConsumptionByPrice);
            var exhaustedBidPrice = ExhaustedTouchPrice(
                snapshot.Bids,
                netBidLevels,
                bidsConsumed,
                snapshot.AppliedBidConsumptionByPrice);
            var hasNewAskConsumption = HasNewConsumption(
                snapshot.Asks,
                asksConsumed,
                snapshot.AppliedAskConsumptionByPrice);
            var hasNewBidConsumption = HasNewConsumption(
                snapshot.Bids,
                bidsConsumed,
                snapshot.AppliedBidConsumptionByPrice);
            MarketOrderBookSide? selectedSide = null;
            if (latestConsumedSide == MarketOrderBookSide.Ask && hasNewAskConsumption)
                selectedSide = MarketOrderBookSide.Ask;
            else if (latestConsumedSide == MarketOrderBookSide.Bid && hasNewBidConsumption)
                selectedSide = MarketOrderBookSide.Bid;

            var asks = FilterStandingLevels(netAskLevels);
            var bids = FilterStandingLevels(netBidLevels);
            long? sourceLastTradePrice = snapshot.SourceLastTradePrice;
            if (selectedSide.HasValue && latestConsumedPrice.HasValue && latestConsumedPrice.Value > 0)
                sourceLastTradePrice = latestConsumedPrice.Value;
            else if (selectedSide == MarketOrderBookSide.Ask && exhaustedAskPrice.HasValue)
                sourceLastTradePrice = exhaustedAskPrice.Value;
            else if (selectedSide == MarketOrderBookSide.Bid && exhaustedBidPrice.HasValue)
                sourceLastTradePrice = exhaustedBidPrice.Value;

            return new MarketOrderBookSnapshot(
                new ReadOnlyCollection<MarketOrderBookLevel>(asks),
                new ReadOnlyCollection<MarketOrderBookLevel>(bids),
                snapshot.TurnoverEok,
                snapshot.FullDayTurnoverEok,
                snapshot.ExecutionCapacity,
                MergedWatermark(snapshot.AppliedAskConsumptionByPrice, asksConsumed),
                MergedWatermark(snapshot.AppliedBidConsumptionByPrice, bidsConsumed),
                Math.Max(snapshot.AppliedCapacityConsumptionUnits, Math.Max(0, consumedCapacityUnits)),
                sourceLastTradePrice);
        }

        public static IReadOnlyList<int> SplitTradeQuantity(
            string assetId,
            int day,
            int minute,
            int liquidityPulse,
            int quantity,
            int maxPrints = MaximumSyntheticPrintsPerPulse)
        {
            var total = Math.Max(0, quantity);
            if (total <= 0 || maxPrints <= 0) return Array.Empty<int>();
            if (total == 1 || maxPrints == 1) return new[] { total };
            var seededAssetId = string.IsNullOrEmpty(assetId) ? "order-book-print" : assetId;
            var desiredCount = 7 + MixedHash(
                seededAssetId,
                day,
                (long)minute * 104729L + (long)liquidityPulse * 13007L + 17011L) % 6;
            var count = Math.Min(total, Math.Min(maxPrints, desiredCount));
            if (count <= 1) return new[] { total };

            var smallCount = Math.Min(count - 1, (int)Math.Ceiling(count * 0.65d));
            var prints = new List<int>(count);
            var remaining = total;
            for (var index = 0; index < smallCount; index += 1)
            {
                var remainingSlots = count - index;
                var maximumSmall = Math.Min(5, remaining - (remainingSlots - 1));
                var draw = MixedHash(
                    seededAssetId,
                    day,
                    (long)minute * 32452843L +
                    (long)liquidityPulse * 49999L +
                    (long)index * 7919L +
                    19001L);
                var print = 1 + draw % Math.Max(1, maximumSmall);
                prints.Add(print);
                remaining -= print;
            }

            var tailSlots = count - smallCount;
            var tailTotal = remaining;
            var tailWeights = new int[tailSlots];
            var totalTailWeight = 0;
            for (var index = 0; index < tailSlots; index += 1)
            {
                var bucket = MixedHash(
                    seededAssetId,
                    day,
                    (long)minute * 86028121L +
                    (long)liquidityPulse * 65537L +
                    (long)index * 12289L +
                    21001L) % 1000;
                tailWeights[index] = 400 + bucket;
                totalTailWeight += tailWeights[index];
            }
            for (var index = 0; index < tailSlots; index += 1)
            {
                var remainingSlots = tailSlots - index;
                if (remainingSlots == 1)
                {
                    prints.Add(remaining);
                    remaining = 0;
                    break;
                }
                var reserved = remainingSlots - 1;
                var proportional =
                    ((long)tailTotal * tailWeights[index] + totalTailWeight / 2) /
                    totalTailWeight;
                var print = (int)Math.Max(1L, Math.Min(proportional, remaining - reserved));
                prints.Add(print);
                remaining -= print;
            }
            for (var index = prints.Count - 1; index > 0; index -= 1)
            {
                var swapIndex = MixedHash(
                    seededAssetId,
                    day,
                    (long)minute * 67867967L +
                    (long)liquidityPulse * 8191L +
                    (long)index * 313L +
                    23003L) %
                                (index + 1);
                var value = prints[index];
                prints[index] = prints[swapIndex];
                prints[swapIndex] = value;
            }
            return new ReadOnlyCollection<int>(prints);
        }

        private static MarketOrderBookFillPlan EmptyFillPlan(MarketOrderBookSide side)
        {
            return new MarketOrderBookFillPlan(
                side,
                Array.Empty<MarketOrderBookLevelFill>(),
                0,
                0,
                0d,
                0);
        }

        private static MarketOrderBookPriceTransition EmptyTransition(long price, bool targetReached)
        {
            return new MarketOrderBookPriceTransition(
                price,
                new ReadOnlyDictionary<long, int>(new Dictionary<long, int>()),
                new ReadOnlyDictionary<long, int>(new Dictionary<long, int>()),
                0,
                targetReached,
                Array.Empty<MarketOrderBookPriceTransitionFill>());
        }

        private static bool StructuralQueueBreached(
            MarketOrderBookLevel level,
            int baselineQuantity,
            int remainingQuantity)
        {
            if (level.IsStructuralBreached) return true;
            if (!level.IsStructuralWall || baselineQuantity <= 0) return false;
            var consumedRatio =
                (double)(baselineQuantity - Math.Max(0, remainingQuantity)) /
                baselineQuantity;
            return consumedRatio + 0.000001d >= StructuralConsumptionBreachRatio;
        }

        private static IReadOnlyDictionary<long, double> EmptyConsumptionMap()
        {
            return new ReadOnlyDictionary<long, double>(new Dictionary<long, double>());
        }

        private static List<MarketOrderBookLevel> NetLevels(
            IReadOnlyList<MarketOrderBookLevel> levels,
            IReadOnlyDictionary<long, double> consumed,
            IReadOnlyDictionary<long, double> applied)
        {
            var result = new List<MarketOrderBookLevel>(levels.Count);
            for (var index = 0; index < levels.Count; index += 1)
                result.Add(NetLevel(levels[index], consumed, applied));
            return result;
        }

        private static MarketOrderBookLevel NetLevel(
            MarketOrderBookLevel level,
            IReadOnlyDictionary<long, double> consumed,
            IReadOnlyDictionary<long, double> applied)
        {
            var used = NewlyUsedAtPrice(level.Price, consumed, applied);
            var remaining = level.Quantity - used;
            var displayedRemaining = used > 0 && remaining > 0 && remaining < MinimumDisplayedQuantity
                ? 0
                : remaining;
            var recoveryBaseline = Math.Max(level.QueueRecoveryTargetQuantity, level.Quantity);
            var structuralBreached = level.IsStructuralBreached ||
                                     used > 0 && StructuralQueueBreached(
                                         level,
                                         recoveryBaseline,
                                         Math.Max(0, displayedRemaining));
            var ordinaryRecoveryTarget = used <= 0
                ? level.QueueRecoveryTargetQuantity
                : Math.Max(level.QueueRecoveryTargetQuantity, level.Quantity);
            var recoveryCeiling = structuralBreached
                ? StructuralRecoveryCeiling(level, recoveryBaseline)
                : 0;
            var recoveryTarget = structuralBreached
                ? Math.Max(0, displayedRemaining) < recoveryCeiling
                    ? Math.Min(ordinaryRecoveryTarget, recoveryCeiling)
                    : 0
                : ordinaryRecoveryTarget;
            return new MarketOrderBookLevel(
                level.Side,
                level.Price,
                Math.Max(0, displayedRemaining),
                structuralBreached ? false : level.IsWall,
                level.StructuralStrength,
                structuralBreached ? false : level.IsStructuralWall,
                structuralBreached,
                recoveryTarget);
        }

        private static int StructuralRecoveryCeiling(
            MarketOrderBookLevel level,
            int baselineQuantity)
        {
            var ordinaryDepth = baselineQuantity / Math.Max(1d, level.StructuralStrength);
            return Math.Max(MinimumDisplayedQuantity, RoundAwayFromZero(ordinaryDepth));
        }

        private static bool HasNewConsumption(
            IReadOnlyList<MarketOrderBookLevel> levels,
            IReadOnlyDictionary<long, double> consumed,
            IReadOnlyDictionary<long, double> applied)
        {
            for (var index = 0; index < levels.Count; index += 1)
            {
                if (NewlyUsedAtPrice(levels[index].Price, consumed, applied) > 0)
                    return true;
            }
            return false;
        }

        private static long? ExhaustedTouchPrice(
            IReadOnlyList<MarketOrderBookLevel> original,
            IReadOnlyList<MarketOrderBookLevel> net,
            IReadOnlyDictionary<long, double> consumed,
            IReadOnlyDictionary<long, double> applied)
        {
            long? exhausted = null;
            for (var index = 0; index < original.Count; index += 1)
            {
                var used = NewlyUsedAtPrice(original[index].Price, consumed, applied);
                if (used <= 0 || net[index].Quantity > 0) break;
                exhausted = net[index].Price;
            }
            return exhausted;
        }

        private static List<MarketOrderBookLevel> FilterStandingLevels(
            IReadOnlyList<MarketOrderBookLevel> levels)
        {
            var result = new List<MarketOrderBookLevel>(levels.Count);
            for (var index = 0; index < levels.Count; index += 1)
            {
                if (levels[index].Quantity > 0) result.Add(levels[index]);
            }
            return result;
        }

        private static int NewlyUsedAtPrice(
            long price,
            IReadOnlyDictionary<long, double> consumed,
            IReadOnlyDictionary<long, double> applied)
        {
            var cumulative = WholeUnits(QuantityAtPrice(consumed, price));
            var alreadyApplied = WholeUnits(QuantityAtPrice(applied, price));
            return (int)Math.Min(int.MaxValue, Math.Max(0L, cumulative - alreadyApplied));
        }

        private static long WholeUnits(double value)
        {
            if (!IsFinite(value) || value <= 0d) return 0;
            return (long)Math.Min(int.MaxValue, Math.Floor(value));
        }

        private static double QuantityAtPrice(
            IReadOnlyDictionary<long, double> quantities,
            long price)
        {
            if (quantities != null && quantities.TryGetValue(price, out var value)) return value;
            return 0d;
        }

        private static IReadOnlyDictionary<long, double> MergedWatermark(
            IReadOnlyDictionary<long, double> applied,
            IReadOnlyDictionary<long, double> consumed)
        {
            var merged = new Dictionary<long, double>();
            MergeWatermarkEntries(merged, applied);
            MergeWatermarkEntries(merged, consumed);
            return new ReadOnlyDictionary<long, double>(merged);
        }

        private static void MergeWatermarkEntries(
            IDictionary<long, double> target,
            IReadOnlyDictionary<long, double> source)
        {
            if (source == null) return;
            foreach (var entry in source)
            {
                if (entry.Key <= 0 || !IsFinite(entry.Value) || entry.Value <= 0d) continue;
                if (!target.TryGetValue(entry.Key, out var previous) || entry.Value > previous)
                    target[entry.Key] = entry.Value;
            }
        }

        private static long PriceLadderIndex(double price, string market)
        {
            var snapped = SnapFinitePrice(price, market);
            if (snapped < 1000) return snapped;
            long index = 1000;
            if (snapped < 5000) return index + (snapped - 1000) / 5;
            index += 800;
            if (snapped < 10000) return index + (snapped - 5000) / 10;
            index += 500;
            if (snapped < 50000) return index + (snapped - 10000) / 50;
            index += 800;
            if (snapped < 100000) return index + (snapped - 50000) / 100;
            index += 500;
            if (market == MarketPricingRules.GrowthMarketName)
                return index + (snapped - 100000) / 100;
            if (snapped < 500000) return index + (snapped - 100000) / 500;
            index += 800;
            return index + (snapped - 500000) / 1000;
        }

        private static long SnapFinitePrice(double price, string market)
        {
            if (!IsFinite(price) || price <= 0d) return 0;
            return MarketPricingRules.SnapPrice((decimal)price, market);
        }

        private static int MixedHash(string assetId, int day, long salt)
        {
            long value = OrderBookHash(assetId, day, salt);
            value ^= value >> 16;
            value = value * 0x45d9f3bL & 0x7fffffffL;
            value ^= value >> 15;
            value = value * 0x45d9f3bL & 0x7fffffffL;
            value ^= value >> 16;
            return (int)(value & 0x7fffffffL);
        }

        private static int OrderBookHash(string assetId, int day, long salt)
        {
            long hash = ((long)day * 1009L + (long)salt * 9176L) & 0x7fffffffL;
            foreach (var unit in assetId)
                hash = (hash * 31L ^ unit) & 0x7fffffffL;
            return (int)hash;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static int RoundAwayFromZero(double value)
        {
            return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}

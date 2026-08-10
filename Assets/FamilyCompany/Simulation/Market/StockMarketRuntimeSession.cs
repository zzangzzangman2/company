using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.History;

namespace FamilyCompany.Simulation.Market
{
    public sealed class StockMarketAssetView
    {
        internal StockMarketAssetView(
            MarketSecurityDefinition security,
            long previousClose,
            long lastTradePrice,
            MarketOrderBookSide lastTradeLevelSide,
            MarketOrderBookSnapshot snapshot)
        {
            Security = security;
            PreviousClose = previousClose;
            LastTradePrice = lastTradePrice;
            LastTradeLevelSide = lastTradeLevelSide;
            Snapshot = snapshot;
        }

        public MarketSecurityDefinition Security { get; }
        public long PreviousClose { get; }
        public long LastTradePrice { get; }
        public MarketOrderBookSide LastTradeLevelSide { get; }
        public MarketOrderBookSnapshot Snapshot { get; }
    }

    public sealed class StockMarketTradePrint
    {
        public StockMarketTradePrint(
            string assetId,
            int marketMinute,
            int liquidityPulse,
            long price,
            int quantity,
            bool isBuy,
            bool isPlayer)
        {
            AssetId = assetId ?? string.Empty;
            MarketMinute = marketMinute;
            LiquidityPulse = liquidityPulse;
            Price = price;
            Quantity = quantity;
            IsBuy = isBuy;
            IsPlayer = isPlayer;
        }

        public string AssetId { get; }
        public int MarketMinute { get; }
        public int LiquidityPulse { get; }
        public long Price { get; }
        public int Quantity { get; }
        public bool IsBuy { get; }
        public bool IsPlayer { get; }
    }

    public sealed class StockMarketOrderResult
    {
        internal StockMarketOrderResult(
            bool accepted,
            string message,
            int requestedQuantity,
            int filledQuantity,
            int remainingQuantity,
            long notional,
            double averagePrice,
            string pendingOrderId)
        {
            Accepted = accepted;
            Message = message ?? string.Empty;
            RequestedQuantity = requestedQuantity;
            FilledQuantity = filledQuantity;
            RemainingQuantity = remainingQuantity;
            Notional = notional;
            AveragePrice = averagePrice;
            PendingOrderId = pendingOrderId;
        }

        public bool Accepted { get; }
        public string Message { get; }
        public int RequestedQuantity { get; }
        public int FilledQuantity { get; }
        public int RemainingQuantity { get; }
        public long Notional { get; }
        public double AveragePrice { get; }
        public string PendingOrderId { get; }
    }

    public sealed class StockMarketPositionView
    {
        internal StockMarketPositionView(string assetId, int units, double averageCost)
        {
            AssetId = assetId ?? string.Empty;
            Units = units;
            AverageCost = averageCost;
        }

        public string AssetId { get; }
        public int Units { get; }
        public double AverageCost { get; }
    }

    public sealed class StockMarketOrderJournalEntry
    {
        public StockMarketOrderJournalEntry(
            int sequence,
            string assetId,
            int marketMinute,
            bool isBuy,
            bool isMarket,
            long limitPrice,
            int requestedQuantity,
            int filledQuantity,
            int remainingQuantity,
            double averagePrice)
        {
            Sequence = sequence;
            AssetId = assetId ?? string.Empty;
            MarketMinute = marketMinute;
            IsBuy = isBuy;
            IsMarket = isMarket;
            LimitPrice = limitPrice;
            RequestedQuantity = requestedQuantity;
            FilledQuantity = filledQuantity;
            RemainingQuantity = remainingQuantity;
            AveragePrice = averagePrice;
        }

        public int Sequence { get; }
        public string AssetId { get; }
        public int MarketMinute { get; }
        public bool IsBuy { get; }
        public bool IsMarket { get; }
        public long LimitPrice { get; }
        public int RequestedQuantity { get; }
        public int FilledQuantity { get; }
        public int RemainingQuantity { get; }
        public double AveragePrice { get; }
    }

    public readonly struct StockMarketPricePoint
    {
        public StockMarketPricePoint(int marketMinute, long price)
        {
            MarketMinute = marketMinute;
            Price = price;
        }

        public int MarketMinute { get; }
        public long Price { get; }
    }

    /// <summary>
    /// Pure C# stock runtime owned by the stock presentation. It turns the
    /// already-ported SIMUL pricing, 10-level book, fill, FIFO pending-order,
    /// fee and calendar rules into one live state shared by quote, order, tape
    /// and balance panels. GameState/save integration intentionally remains a
    /// separate boundary so the common save schema is not mutated here.
    /// </summary>
    public sealed class StockMarketRuntimeSession
    {
        private sealed class AssetState
        {
            public MarketSecurityDefinition Security;
            public long PreviousClose;
            public long LastTradePrice;
            public long OpeningPrice;
            public MarketOrderBookSide LastTradeLevelSide;
            public MarketOrderBookSnapshot Snapshot;
        }

        private sealed class PositionState
        {
            public int Units;
            public double AverageCost;
        }

        private readonly int _worldSeed;
        private readonly Dictionary<string, AssetState> _assets;
        private readonly Dictionary<string, PositionState> _positions =
            new Dictionary<string, PositionState>(StringComparer.Ordinal);
        private List<MarketPendingOrder> _pendingOrders = new List<MarketPendingOrder>();
        private readonly List<StockMarketTradePrint> _tradeTape = new List<StockMarketTradePrint>();
        private readonly List<StockMarketOrderJournalEntry> _orderJournal =
            new List<StockMarketOrderJournalEntry>();
        private readonly HashSet<string> _favoriteAssetIds =
            new HashSet<string>(StringComparer.Ordinal);
        private int _orderSequence;
        private int _journalSequence;
        private int _liquidityPulse;
        private int _canonicalMinuteUpdateCount;
        private bool _openingAuctionProcessed;
        private int _openingAuctionProcessCount;

        public StockMarketRuntimeSession(
            int worldSeed,
            DateTime date,
            long openingBrokerageCash,
            IEnumerable<MarketSecurityDefinition> securities,
            int initialMarketMinute = MarketSessionClock.DayStartMinute)
        {
            if (openingBrokerageCash < 0) throw new ArgumentOutOfRangeException(nameof(openingBrokerageCash));
            if (securities == null) throw new ArgumentNullException(nameof(securities));
            _worldSeed = worldSeed;
            Date = date.Date;
            BrokerageCash = openingBrokerageCash;
            MarketMinute = Clamp(initialMarketMinute, MarketSessionClock.DayStartMinute, MarketSessionClock.DayEndMinute);
            _openingAuctionProcessed = MarketMinute >= MarketSessionClock.OpenMinute;
            _liquidityPulse = MarketOrderBookRules.LiquidityPulseFrame(MarketMinute, 1);
            _assets = securities
                .GroupBy(security => security.CompanyId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToDictionary(
                    security => security.CompanyId,
                    security => new AssetState { Security = security },
                    StringComparer.Ordinal);
            foreach (var state in _assets.Values) RebuildAsset(state, recordTape: false);
        }

        public DateTime Date { get; }
        public int MarketMinute { get; private set; }
        public int LiquidityPulse => _liquidityPulse;
        public int CanonicalMinuteUpdateCount => _canonicalMinuteUpdateCount;
        public bool OpeningAuctionProcessed => _openingAuctionProcessed;
        public int OpeningAuctionProcessCount => _openingAuctionProcessCount;
        public long BrokerageCash { get; private set; }
        public IReadOnlyList<MarketPendingOrder> PendingOrders =>
            new ReadOnlyCollection<MarketPendingOrder>(_pendingOrders.ToArray());
        public IReadOnlyList<StockMarketTradePrint> TradeTape =>
            new ReadOnlyCollection<StockMarketTradePrint>(_tradeTape.ToArray());
        public IReadOnlyList<StockMarketOrderJournalEntry> OrderJournal =>
            new ReadOnlyCollection<StockMarketOrderJournalEntry>(_orderJournal.ToArray());
        public IReadOnlyList<StockMarketPositionView> Positions =>
            new ReadOnlyCollection<StockMarketPositionView>(_positions
                .Where(entry => entry.Value.Units > 0)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new StockMarketPositionView(
                    entry.Key,
                    entry.Value.Units,
                    entry.Value.AverageCost))
                .ToArray());
        public IReadOnlyList<string> FavoriteAssetIds =>
            new ReadOnlyCollection<string>(_favoriteAssetIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());

        public long AvailableBrokerageCash => MarketPendingOrderRules.AvailableBrokerageCash(
            BrokerageCash,
            _pendingOrders,
            MarketTradingCosts.TradingFeeRate(Date));

        public StockMarketAssetView ViewFor(string assetId)
        {
            if (!_assets.TryGetValue(assetId ?? string.Empty, out var state))
                throw new KeyNotFoundException($"Market asset not found: {assetId}");
            return new StockMarketAssetView(
                state.Security,
                state.PreviousClose,
                state.LastTradePrice,
                state.LastTradeLevelSide,
                state.Snapshot);
        }

        public int PositionUnits(string assetId)
        {
            return _positions.TryGetValue(assetId ?? string.Empty, out var position)
                ? position.Units
                : 0;
        }

        public long OpeningPriceFor(string assetId)
        {
            return _assets.TryGetValue(assetId ?? string.Empty, out var state)
                ? state.OpeningPrice
                : 0L;
        }

        public int OpeningTradeCountFor(string assetId)
        {
            return _tradeTape.Count(print => print.AssetId == assetId &&
                                             print.MarketMinute == MarketSessionClock.OpenMinute);
        }

        public double AverageCost(string assetId)
        {
            return _positions.TryGetValue(assetId ?? string.Empty, out var position)
                ? position.AverageCost
                : 0d;
        }

        public bool IsFavorite(string assetId)
        {
            return _favoriteAssetIds.Contains(assetId ?? string.Empty);
        }

        public bool SetFavorite(string assetId, bool favorite)
        {
            if (!_assets.ContainsKey(assetId ?? string.Empty)) return false;
            if (favorite) _favoriteAssetIds.Add(assetId);
            else _favoriteAssetIds.Remove(assetId);
            return true;
        }

        public IReadOnlyList<StockMarketTradePrint> PlayerTradeHistory(string assetId = null)
        {
            return new ReadOnlyCollection<StockMarketTradePrint>(_tradeTape
                .Where(print => print.IsPlayer &&
                                (string.IsNullOrEmpty(assetId) || print.AssetId == assetId))
                .ToArray());
        }

        public IReadOnlyList<StockMarketPricePoint> PriceHistoryFor(string assetId, int maximumPoints)
        {
            if (!_assets.TryGetValue(assetId ?? string.Empty, out var state))
                throw new KeyNotFoundException($"Market asset not found: {assetId}");
            var count = Math.Max(1, maximumPoints);
            var sessionStart = MarketMinute >= MarketSessionClock.OpenMinute
                ? MarketSessionClock.OpenMinute
                : MarketSessionClock.DayStartMinute;
            var firstMinute = Math.Max(sessionStart, MarketMinute - count + 1);
            var seed = StableHash($"{_worldSeed}:{state.Security.CompanyId}:{Date:yyyyMMdd}");
            var points = new List<StockMarketPricePoint>();
            for (var minute = firstMinute; minute <= MarketMinute; minute += 1)
            {
                var phase = MarketSessionClock.At(
                    minute,
                    MarketTradingCalendar.IsTradingDay(Date)).Phase;
                points.Add(new StockMarketPricePoint(
                    minute,
                    PriceAtMinute(state, seed, phase, minute)));
            }
            if (points.Count > 0)
                points[points.Count - 1] = new StockMarketPricePoint(MarketMinute, state.LastTradePrice);
            return new ReadOnlyCollection<StockMarketPricePoint>(points);
        }

        public int MaximumOrderQuantity(
            string assetId,
            bool isBuy,
            bool isMarket,
            long limitPrice)
        {
            if (!_assets.TryGetValue(assetId ?? string.Empty, out var state)) return 0;
            var range = MarketPricingRules.DailyPriceRange(
                state.PreviousClose,
                Date,
                state.Security.PriceRuleMarket);
            var executionLimit = isMarket
                ? isBuy ? range.Upper : range.Lower
                : limitPrice;
            if (executionLimit < range.Lower || executionLimit > range.Upper ||
                !MarketPricingRules.IsValidOrderPrice(executionLimit, state.Security.PriceRuleMarket))
                return 0;

            if (isBuy)
            {
                var availableCash = AvailableBrokerageCash;
                var affordable = MaximumAffordableQuantity(availableCash, executionLimit);
                var preOpen = MarketSessionClock.At(
                    MarketMinute,
                    MarketTradingCalendar.IsTradingDay(Date)).Phase == MarketSessionPhase.OpeningTransition;
                if (!isMarket || preOpen || affordable <= 0) return affordable;
                return MarketOrderBookRules.LimitFillPlan(
                    state.Snapshot,
                    true,
                    affordable,
                    executionLimit,
                    maximumNotional: (int)Math.Min(int.MaxValue, availableCash)).FilledQuantity;
            }

            var reserved = MarketPendingOrderRules.PendingReservedUnits(
                _pendingOrders,
                assetId,
                MarketPendingOrderSide.Sell);
            var availableUnits = Math.Max(0, (int)Math.Floor(PositionUnits(assetId) - reserved));
            var isPreOpen = MarketSessionClock.At(
                MarketMinute,
                MarketTradingCalendar.IsTradingDay(Date)).Phase == MarketSessionPhase.OpeningTransition;
            if (!isMarket || isPreOpen || availableUnits <= 0) return availableUnits;
            return MarketOrderBookRules.LimitFillPlan(
                state.Snapshot,
                false,
                availableUnits,
                executionLimit).FilledQuantity;
        }

        public BrokerageAccountStateDto ExportBrokerageState()
        {
            return new BrokerageAccountStateDto(
                BrokerageCash,
                _positions
                    .Where(entry => entry.Value.Units > 0)
                    .Select(entry => new BrokeragePositionStateDto(
                        entry.Key,
                        entry.Value.Units,
                        entry.Value.AverageCost)),
                _pendingOrders.Select(order => new BrokeragePendingOrderStateDto(order)),
                _tradeTape.Where(print => print.IsPlayer).Select(print => new BrokerageTradeStateDto(print)),
                _orderJournal.Select(entry => new BrokerageOrderJournalStateDto(entry)),
                _favoriteAssetIds,
                _orderSequence,
                _journalSequence);
        }

        public StockMarketSessionStateDto ExportSessionState(
            double realtimeResidualSeconds = 0d,
            int playbackIndex = 1)
        {
            return new StockMarketSessionStateDto(
                true,
                Date,
                MarketMinute,
                realtimeResidualSeconds,
                playbackIndex,
                _openingAuctionProcessed,
                _openingAuctionProcessCount,
                _canonicalMinuteUpdateCount,
                _liquidityPulse,
                ExportBrokerageState());
        }

        public bool TryApplySessionState(StockMarketSessionStateDto source, out string error)
        {
            error = string.Empty;
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                if (!source.Initialized) throw new InvalidOperationException("Stock market session is not initialized.");
                if (source.SchemaVersion != StockMarketSessionStateDto.CurrentSchemaVersion)
                    throw new InvalidOperationException($"Unsupported stock session schema: {source.SchemaVersion}");
                if (source.Date.Date != Date)
                    throw new InvalidOperationException("Stock session trading date does not match.");
                if (source.MarketMinute < MarketSessionClock.DayStartMinute ||
                    source.MarketMinute > MarketSessionClock.DayEndMinute)
                    throw new InvalidOperationException("Stock session minute is outside the market day.");
                if (source.PlaybackIndex < 0 || source.PlaybackIndex > 3 ||
                    source.OpeningAuctionProcessCount < 0 || source.CanonicalMinuteUpdateCount < 0)
                    throw new InvalidOperationException("Stock session counters are invalid.");
                if (source.MarketMinute < MarketSessionClock.OpenMinute && source.OpeningAuctionProcessed)
                    throw new InvalidOperationException("Pre-open session cannot have a processed opening auction.");
                if (source.MarketMinute >= MarketSessionClock.OpenMinute && !source.OpeningAuctionProcessed)
                    throw new InvalidOperationException("Post-open session must preserve opening-auction idempotency.");

                if (!TryApplyBrokerageState(source.Brokerage, out error)) return false;
                MarketMinute = source.MarketMinute;
                _liquidityPulse = source.LiquidityPulse;
                _openingAuctionProcessed = source.OpeningAuctionProcessed;
                _openingAuctionProcessCount = source.OpeningAuctionProcessCount;
                _canonicalMinuteUpdateCount = source.CanonicalMinuteUpdateCount;
                foreach (var state in _assets.Values) RebuildAsset(state, recordTape: false);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal void AdjustBrokerageCashForCompanyTransfer(long deltaWon)
        {
            var next = checked(BrokerageCash + deltaWon);
            if (next < 0) throw new InvalidOperationException("증권 예수금이 부족합니다.");
            BrokerageCash = next;
        }

        /// <summary>
        /// Validates every DTO member into temporary collections before any live
        /// account field is changed. A false result guarantees no partial apply.
        /// </summary>
        public bool TryApplyBrokerageState(BrokerageAccountStateDto source, out string error)
        {
            error = string.Empty;
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                if (source.SchemaVersion != BrokerageAccountStateDto.CurrentSchemaVersion)
                    throw new InvalidOperationException($"Unsupported brokerage schema: {source.SchemaVersion}");
                if (source.CashWon < 0) throw new InvalidOperationException("Brokerage cash cannot be negative.");
                if (source.OrderSequence < 0 || source.JournalSequence < 0)
                    throw new InvalidOperationException("Brokerage sequence cannot be negative.");

                var positions = new Dictionary<string, PositionState>(StringComparer.Ordinal);
                foreach (var item in source.Positions)
                {
                    if (!_assets.ContainsKey(item.AssetId) || positions.ContainsKey(item.AssetId) ||
                        item.Units <= 0 || !IsFinite(item.AverageCostWon) || item.AverageCostWon < 0d)
                        throw new InvalidOperationException($"Invalid brokerage position: {item.AssetId}");
                    positions.Add(item.AssetId, new PositionState
                    {
                        Units = item.Units,
                        AverageCost = item.AverageCostWon
                    });
                }

                var pending = source.PendingOrders.Select(item => item.ToDomain()).ToList();
                if (pending.Select(order => order.Id).Distinct(StringComparer.Ordinal).Count() != pending.Count ||
                    pending.Any(order => !_assets.ContainsKey(order.AssetId)))
                    throw new InvalidOperationException("Invalid or duplicate pending brokerage order.");
                var reservedBuy = TotalPendingBuyReservation(pending);
                if (reservedBuy > source.CashWon)
                    throw new InvalidOperationException("Pending buy reservation exceeds brokerage cash.");
                foreach (var group in pending
                             .Where(order => order.Side == MarketPendingOrderSide.Sell)
                             .GroupBy(order => order.AssetId, StringComparer.Ordinal))
                {
                    var units = positions.TryGetValue(group.Key, out var position) ? position.Units : 0;
                    if (group.Sum(order => order.RemainingQuantity) > units + 0.000001d)
                        throw new InvalidOperationException($"Pending sell reservation exceeds position: {group.Key}");
                }

                var trades = source.PlayerTrades.Select(item => item.ToDomain()).ToList();
                if (trades.Any(item => !_assets.ContainsKey(item.AssetId) || item.Quantity <= 0 || item.Price <= 0))
                    throw new InvalidOperationException("Invalid brokerage trade history.");
                var journal = source.OrderJournal.Select(item => item.ToDomain()).ToList();
                if (journal.Any(item => !_assets.ContainsKey(item.AssetId) || item.Sequence < 0))
                    throw new InvalidOperationException("Invalid brokerage order journal.");
                var favorites = new HashSet<string>(source.FavoriteAssetIds, StringComparer.Ordinal);
                if (favorites.Any(id => !_assets.ContainsKey(id)))
                    throw new InvalidOperationException("Favorite list contains an unknown asset.");

                BrokerageCash = source.CashWon;
                _positions.Clear();
                foreach (var entry in positions) _positions.Add(entry.Key, entry.Value);
                _pendingOrders = pending;
                _tradeTape.RemoveAll(print => print.IsPlayer);
                _tradeTape.InsertRange(0, trades.Take(50));
                if (_tradeTape.Count > 50) _tradeTape.RemoveRange(50, _tradeTape.Count - 50);
                _orderJournal.Clear();
                _orderJournal.AddRange(journal);
                _favoriteAssetIds.Clear();
                foreach (var favorite in favorites) _favoriteAssetIds.Add(favorite);
                _orderSequence = source.OrderSequence;
                _journalSequence = source.JournalSequence;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void SetMarketMinute(int minute)
        {
            var target = Clamp(minute, MarketSessionClock.DayStartMinute, MarketSessionClock.DayEndMinute);
            if (target == MarketMinute) return;
            if (target > MarketMinute)
            {
                AdvanceMinutes(target - MarketMinute, 1);
                return;
            }
            MarketMinute = target;
            _openingAuctionProcessed = target >= MarketSessionClock.OpenMinute;
            _liquidityPulse = MarketOrderBookRules.LiquidityPulseFrame(MarketMinute, 1);
            foreach (var state in _assets.Values) RebuildAsset(state, recordTape: false);
            _canonicalMinuteUpdateCount += 1;
            MatchPendingOrders();
        }

        /// <summary>
        /// Always processes the clock one game minute at a time. The caller's
        /// speed only controls how many exact minute steps occur in one real tick.
        /// </summary>
        public void AdvanceMinutes(int elapsedMinutes, int animationRate)
        {
            var remaining = Math.Max(0, elapsedMinutes);
            while (remaining > 0 && MarketMinute < MarketSessionClock.DayEndMinute)
            {
                MarketMinute += 1;
                remaining -= 1;
                // Animation rate is presentation-only. Every canonical market
                // minute gets exactly one quote/tape/matching state transition.
                _liquidityPulse = MarketOrderBookRules.LiquidityPulseFrame(MarketMinute, 1);
                foreach (var state in _assets.Values) RebuildAsset(state, recordTape: true);
                _canonicalMinuteUpdateCount += 1;
                if (MarketMinute == MarketSessionClock.OpenMinute && !_openingAuctionProcessed)
                    ProcessOpeningAuction();
                else
                    MatchPendingOrders();
            }
        }

        public StockMarketOrderResult PlaceOrder(
            string assetId,
            bool isBuy,
            bool isMarket,
            long limitPrice,
            int quantity)
        {
            if (!_assets.TryGetValue(assetId ?? string.Empty, out var state))
                return Rejected("거래할 종목을 찾지 못했습니다.", quantity);
            if (quantity <= 0) return Rejected("주문 수량은 1주 이상이어야 합니다.", quantity);

            var tradingDay = MarketTradingCalendar.IsTradingDay(Date);
            var clock = MarketSessionClock.At(MarketMinute, tradingDay);
            var isPreOpen = clock.Phase == MarketSessionPhase.OpeningTransition;
            if (!clock.Tradable && !isPreOpen)
                return Rejected("현재는 주문 가능한 거래 시간이 아닙니다.", quantity);

            var range = MarketPricingRules.DailyPriceRange(
                state.PreviousClose,
                Date,
                state.Security.PriceRuleMarket);
            var executionLimit = isMarket
                ? isBuy ? range.Upper : range.Lower
                : limitPrice;
            if (executionLimit < range.Lower || executionLimit > range.Upper ||
                !MarketPricingRules.IsValidOrderPrice(executionLimit, state.Security.PriceRuleMarket))
                return Rejected($"오늘 주문 범위 {range.Lower:N0}~{range.Upper:N0}원과 호가 단위를 확인하세요.", quantity);

            if (isBuy)
            {
                long reservation;
                try
                {
                    reservation = MarketTradingCosts.BuyReservation(
                        Date,
                        checked(executionLimit * (long)quantity));
                }
                catch (OverflowException)
                {
                    return Rejected("주문 금액이 허용 범위를 초과했습니다.", quantity);
                }
                if (reservation > AvailableBrokerageCash)
                    return Rejected("주문 가능 예수금이 부족합니다.", quantity);
            }
            else
            {
                var reserved = MarketPendingOrderRules.PendingReservedUnits(
                    _pendingOrders,
                    assetId,
                    MarketPendingOrderSide.Sell);
                if (quantity > Math.Max(0d, PositionUnits(assetId) - reserved))
                    return Rejected("매도 가능한 보유 수량이 부족합니다.", quantity);
            }

            var filledQuantity = 0;
            var filledNotional = 0L;
            var averagePrice = 0d;
            var immediateFillOccurred = false;
            if (!isPreOpen)
            {
                var plan = MarketOrderBookRules.LimitFillPlan(
                    state.Snapshot,
                    isBuy,
                    quantity,
                    executionLimit,
                    maximumNotional: isBuy
                        ? (int)Math.Min(int.MaxValue, AvailableBrokerageCash)
                        : (int?)null);
                if (plan.HasFill)
                {
                    ApplyFill(state, isBuy, plan.FilledQuantity, plan.Notional, plan.AveragePrice, true);
                    ConsumeAggressiveFillPlan(state, plan);
                    filledQuantity = plan.FilledQuantity;
                    filledNotional = plan.Notional;
                    averagePrice = plan.AveragePrice;
                    immediateFillOccurred = true;
                }
            }

            var remaining = Math.Max(0, quantity - filledQuantity);
            string pendingId = null;
            if (remaining > 0 && (!isMarket || isPreOpen))
            {
                pendingId = $"stock-{Date:yyyyMMdd}-{MarketMinute}-{_orderSequence + 1}";
                var side = isBuy ? MarketPendingOrderSide.Buy : MarketPendingOrderSide.Sell;
                var queueAhead = isPreOpen
                    ? _pendingOrders
                        .Where(order => order.AssetId == assetId &&
                                        order.Side == side &&
                                        order.LimitPrice == executionLimit)
                        .Sum(order => order.RemainingQuantity)
                    : MarketPendingOrderRules.QueueAheadForNewOrder(
                        state.Snapshot,
                        assetId,
                        side,
                        executionLimit,
                        _pendingOrders,
                        immediateFillOccurred);
                _orderSequence += 1;
                _pendingOrders.Add(new MarketPendingOrder(
                    pendingId,
                    side,
                    assetId,
                    executionLimit,
                    quantity,
                    remaining,
                    Date,
                    MarketMinute,
                    _orderSequence,
                    queueAhead));
            }

            var message = isPreOpen
                ? $"개장 동시호가 주문 접수 · {remaining:N0}주 · 09:00 시초가에서 판정"
                : filledQuantity == quantity
                ? $"{filledQuantity:N0}주 전체 체결"
                : filledQuantity > 0
                    ? $"{filledQuantity:N0}주 체결 · {remaining:N0}주 미체결"
                    : isMarket
                        ? "시장가 체결 수량이 없습니다."
                        : $"{remaining:N0}주 미체결 주문 등록";
            var result = new StockMarketOrderResult(
                true,
                message,
                quantity,
                filledQuantity,
                isMarket && !isPreOpen ? 0 : remaining,
                filledNotional,
                averagePrice,
                pendingId);
            RecordOrderJournal(assetId, isBuy, isMarket, executionLimit, result);
            return result;
        }

        public bool CancelPendingOrder(string orderId)
        {
            if (_pendingOrders.All(order => order.Id != orderId)) return false;
            _pendingOrders = MarketPendingOrderRules.Cancel(_pendingOrders, orderId).ToList();
            return true;
        }

        public StockMarketOrderResult AmendPendingOrder(
            string orderId,
            long newLimitPrice,
            int newRemainingQuantity)
        {
            var source = _pendingOrders.FirstOrDefault(order => order.Id == orderId);
            if (source == null) return Rejected("정정할 미체결 주문을 찾지 못했습니다.", newRemainingQuantity);
            if (newRemainingQuantity <= 0)
                return Rejected("정정 수량은 1주 이상이어야 합니다.", newRemainingQuantity);
            if (!_assets.TryGetValue(source.AssetId, out var state))
                return Rejected("정정할 종목을 찾지 못했습니다.", newRemainingQuantity);

            var range = MarketPricingRules.DailyPriceRange(
                state.PreviousClose,
                Date,
                state.Security.PriceRuleMarket);
            if (newLimitPrice < range.Lower || newLimitPrice > range.Upper ||
                !MarketPricingRules.IsValidOrderPrice(newLimitPrice, state.Security.PriceRuleMarket))
                return Rejected($"오늘 주문 범위 {range.Lower:N0}~{range.Upper:N0}원과 호가 단위를 확인하세요.", newRemainingQuantity);

            var originalPending = _pendingOrders;
            var afterCancel = MarketPendingOrderRules.Cancel(_pendingOrders, orderId).ToList();
            _pendingOrders = afterCancel;
            var isBuy = source.Side == MarketPendingOrderSide.Buy;
            var validationError = ValidateReservation(
                source.AssetId,
                isBuy,
                newLimitPrice,
                newRemainingQuantity);
            if (validationError != null)
            {
                _pendingOrders = originalPending;
                return Rejected(validationError, newRemainingQuantity);
            }

            var result = PlaceOrder(
                source.AssetId,
                isBuy,
                false,
                newLimitPrice,
                newRemainingQuantity);
            if (!result.Accepted)
                _pendingOrders = originalPending;
            return result;
        }

        private void MatchPendingOrders()
        {
            if (_pendingOrders.Count == 0) return;
            var priority = MarketPendingOrderRules.InExchangePriority(_pendingOrders).ToArray();
            foreach (var candidate in priority)
            {
                var current = _pendingOrders.FirstOrDefault(order => order.Id == candidate.Id);
                if (current == null || !_assets.TryGetValue(current.AssetId, out var state)) continue;
                var queue = MarketPendingOrderRules.ConsumeRestingQueue(
                    current,
                    state.Snapshot,
                    state.LastTradePrice,
                    state.Snapshot.ExecutionCapacity);
                if (!queue.Represented || !queue.Touched) continue;
                if (queue.ConsumedQuantity > 0)
                    ConsumeRestingQueueAhead(state, current, queue.ConsumedQuantity);
                if (Math.Abs(queue.QueueAheadQuantity - current.QueueAheadQuantity) > 0.000001d)
                {
                    current = current.With(queueAheadQuantity: queue.QueueAheadQuantity);
                    ReplacePending(current);
                }
                if (!queue.MayFill) continue;

                var fillQuantity = Math.Min((int)Math.Floor(current.RemainingQuantity), queue.RemainingCapacity);
                if (fillQuantity <= 0) continue;
                var isBuy = current.Side == MarketPendingOrderSide.Buy;
                var notional = checked(current.LimitPrice * (long)fillQuantity);
                ApplyFill(state, isBuy, fillQuantity, notional, current.LimitPrice, true);
                ConsumeExecutionCapacity(state, fillQuantity);
                _pendingOrders = MarketPendingOrderRules.AfterFill(
                    _pendingOrders,
                    current,
                    fillQuantity).ToList();
            }
        }

        private void ProcessOpeningAuction()
        {
            if (_openingAuctionProcessed) return;
            _openingAuctionProcessed = true;
            _openingAuctionProcessCount += 1;

            foreach (var state in _assets.Values)
            {
                state.OpeningPrice = state.LastTradePrice;
                var priority = MarketPendingOrderRules.InExchangePriority(
                        _pendingOrders.Where(order => order.AssetId == state.Security.CompanyId))
                    .ToArray();
                foreach (var candidate in priority)
                {
                    var current = _pendingOrders.FirstOrDefault(order => order.Id == candidate.Id);
                    if (current == null || state.Snapshot.ExecutionCapacity <= 0) continue;
                    var isBuy = current.Side == MarketPendingOrderSide.Buy;
                    var crosses = isBuy
                        ? current.LimitPrice >= state.OpeningPrice
                        : current.LimitPrice <= state.OpeningPrice;
                    var externalSide = isBuy ? state.Snapshot.Asks : state.Snapshot.Bids;
                    if (!crosses || externalSide.Count == 0 || externalSide.All(level => level.Quantity <= 0))
                        continue;

                    var requested = Math.Max(0, (int)Math.Floor(current.RemainingQuantity));
                    var visible = externalSide.Sum(level => Math.Max(0, level.Quantity));
                    var fillQuantity = Math.Min(requested, Math.Min(visible, state.Snapshot.ExecutionCapacity));
                    if (fillQuantity <= 0) continue;
                    var notional = checked(state.OpeningPrice * (long)fillQuantity);
                    ApplyFill(state, isBuy, fillQuantity, notional, state.OpeningPrice, true);
                    ConsumeExecutionCapacity(state, fillQuantity);
                    _pendingOrders = MarketPendingOrderRules.AfterFill(
                        _pendingOrders,
                        current,
                        fillQuantity).ToList();
                }
            }
        }

        private void ReplacePending(MarketPendingOrder replacement)
        {
            var index = _pendingOrders.FindIndex(order => order.Id == replacement.Id);
            if (index >= 0) _pendingOrders[index] = replacement;
        }

        private void ApplyFill(
            AssetState state,
            bool isBuy,
            int quantity,
            long notional,
            double averagePrice,
            bool isPlayer)
        {
            if (quantity <= 0 || notional <= 0) return;
            if (!_positions.TryGetValue(state.Security.CompanyId, out var position))
            {
                position = new PositionState();
                _positions.Add(state.Security.CompanyId, position);
            }
            var fee = MarketTradingCosts.TradingFee(Date, notional);
            if (isBuy)
            {
                BrokerageCash = checked(BrokerageCash - notional - fee);
                position.AverageCost = position.Units + quantity <= 0
                    ? 0d
                    : (position.AverageCost * position.Units + notional + fee) /
                      (position.Units + quantity);
                position.Units += quantity;
            }
            else
            {
                var tax = MarketTradingCosts.SecuritiesTransactionTax(Date, notional);
                BrokerageCash = checked(BrokerageCash + notional - fee - tax);
                position.Units = Math.Max(0, position.Units - quantity);
                if (position.Units == 0) position.AverageCost = 0d;
            }

            AddTape(new StockMarketTradePrint(
                state.Security.CompanyId,
                MarketMinute,
                _liquidityPulse,
                checked((long)Math.Round(averagePrice, MidpointRounding.AwayFromZero)),
                quantity,
                isBuy,
                isPlayer));
        }

        private string ValidateReservation(
            string assetId,
            bool isBuy,
            long limitPrice,
            int quantity)
        {
            if (isBuy)
            {
                try
                {
                    var reservation = MarketTradingCosts.BuyReservation(
                        Date,
                        checked(limitPrice * (long)quantity));
                    return reservation > AvailableBrokerageCash
                        ? "주문 가능 예수금이 부족합니다."
                        : null;
                }
                catch (OverflowException)
                {
                    return "주문 금액이 허용 범위를 초과했습니다.";
                }
            }

            var reserved = MarketPendingOrderRules.PendingReservedUnits(
                _pendingOrders,
                assetId,
                MarketPendingOrderSide.Sell);
            return quantity > Math.Max(0d, PositionUnits(assetId) - reserved)
                ? "매도 가능한 보유 수량이 부족합니다."
                : null;
        }

        private void RecordOrderJournal(
            string assetId,
            bool isBuy,
            bool isMarket,
            long executionLimit,
            StockMarketOrderResult result)
        {
            _journalSequence += 1;
            _orderJournal.Insert(0, new StockMarketOrderJournalEntry(
                _journalSequence,
                assetId,
                MarketMinute,
                isBuy,
                isMarket,
                executionLimit,
                result.RequestedQuantity,
                result.FilledQuantity,
                result.RemainingQuantity,
                result.AveragePrice));
            if (_orderJournal.Count > 100)
                _orderJournal.RemoveRange(100, _orderJournal.Count - 100);
        }

        private void ConsumeAggressiveFillPlan(AssetState state, MarketOrderBookFillPlan plan)
        {
            var consumed = plan.Fills
                .GroupBy(fill => fill.Price)
                .ToDictionary(group => group.Key, group => group.Sum(fill => fill.Quantity));
            var asks = state.Snapshot.Asks
                .Select(level => plan.LevelSide == MarketOrderBookSide.Ask && consumed.TryGetValue(level.Price, out var quantity)
                    ? WithQuantity(level, Math.Max(0, level.Quantity - quantity))
                    : level)
                .ToArray();
            var bids = state.Snapshot.Bids
                .Select(level => plan.LevelSide == MarketOrderBookSide.Bid && consumed.TryGetValue(level.Price, out var quantity)
                    ? WithQuantity(level, Math.Max(0, level.Quantity - quantity))
                    : level)
                .ToArray();
            ReplaceSnapshot(
                state,
                asks,
                bids,
                Math.Max(0, state.Snapshot.ExecutionCapacity - plan.FilledQuantity));
        }

        private void ConsumeRestingQueueAhead(
            AssetState state,
            MarketPendingOrder order,
            int quantity)
        {
            var consumeBid = order.Side == MarketPendingOrderSide.Buy;
            var asks = state.Snapshot.Asks
                .Select(level => !consumeBid && level.Price == order.LimitPrice
                    ? WithQuantity(level, Math.Max(0, level.Quantity - quantity))
                    : level)
                .ToArray();
            var bids = state.Snapshot.Bids
                .Select(level => consumeBid && level.Price == order.LimitPrice
                    ? WithQuantity(level, Math.Max(0, level.Quantity - quantity))
                    : level)
                .ToArray();
            ReplaceSnapshot(
                state,
                asks,
                bids,
                Math.Max(0, state.Snapshot.ExecutionCapacity - quantity));
        }

        private void ConsumeExecutionCapacity(AssetState state, int quantity)
        {
            ReplaceSnapshot(
                state,
                state.Snapshot.Asks,
                state.Snapshot.Bids,
                Math.Max(0, state.Snapshot.ExecutionCapacity - Math.Max(0, quantity)));
        }

        private static MarketOrderBookLevel WithQuantity(MarketOrderBookLevel source, int quantity)
        {
            return new MarketOrderBookLevel(
                source.Side,
                source.Price,
                quantity,
                source.IsWall,
                source.StructuralStrength,
                source.IsStructuralWall,
                source.IsStructuralBreached,
                source.QueueRecoveryTargetQuantity);
        }

        private static void ReplaceSnapshot(
            AssetState state,
            IReadOnlyList<MarketOrderBookLevel> asks,
            IReadOnlyList<MarketOrderBookLevel> bids,
            int executionCapacity)
        {
            var source = state.Snapshot;
            state.Snapshot = new MarketOrderBookSnapshot(
                asks,
                bids,
                source.TurnoverEok,
                source.FullDayTurnoverEok,
                executionCapacity,
                source.AppliedAskConsumptionByPrice,
                source.AppliedBidConsumptionByPrice,
                source.AppliedCapacityConsumptionUnits +
                Math.Max(0, source.ExecutionCapacity - executionCapacity),
                source.SourceLastTradePrice);
        }

        private int MaximumAffordableQuantity(long availableCash, long price)
        {
            if (availableCash <= 0 || price <= 0) return 0;
            var upper = (int)Math.Min(int.MaxValue, availableCash / price);
            var low = 0;
            var high = upper;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                long reservation;
                try
                {
                    reservation = MarketTradingCosts.BuyReservation(
                        Date,
                        checked(price * (long)middle));
                }
                catch (OverflowException)
                {
                    reservation = long.MaxValue;
                }
                if (reservation <= availableCash) low = middle;
                else high = middle - 1;
            }
            return low;
        }

        private void RebuildAsset(AssetState state, bool recordTape)
        {
            var market = state.Security.PriceRuleMarket;
            var seed = StableHash($"{_worldSeed}:{state.Security.CompanyId}:{Date:yyyyMMdd}");
            // SIMUL starts the 2000-01-03 practice run with only 50,000 won.
            // Until the historical-price corpus is connected, keep every seeded
            // opening reference affordable for at least one upper-limit share.
            state.PreviousClose = MarketPricingRules.SnapPrice(1000m + seed % 34000, market);
            var previousTrade = state.LastTradePrice > 0 ? state.LastTradePrice : state.PreviousClose;
            var clock = MarketSessionClock.At(MarketMinute, MarketTradingCalendar.IsTradingDay(Date));
            var current = PriceAtMinute(state, seed, clock.Phase, MarketMinute);
            state.LastTradePrice = current;
            if (MarketMinute >= MarketSessionClock.OpenMinute && state.OpeningPrice <= 0L)
                state.OpeningPrice = PriceAtMinute(
                    state,
                    seed,
                    MarketSessionPhase.Regular,
                    MarketSessionClock.OpenMinute);
            state.LastTradeLevelSide = current >= previousTrade
                ? MarketOrderBookSide.Ask
                : MarketOrderBookSide.Bid;

            var asks = new List<MarketOrderBookLevel>(MarketOrderBookRules.LevelCount);
            var bids = new List<MarketOrderBookLevel>(MarketOrderBookRules.LevelCount);
            var askPrice = MarketOrderBookPresentationRules.AdjacentPrice(current, 1, market);
            var bidPrice = MarketOrderBookPresentationRules.AdjacentPrice(askPrice, -1, market);
            for (var index = 0; index < MarketOrderBookRules.LevelCount; index += 1)
            {
                var askQuantity = QuoteQuantity(seed, MarketMinute, _liquidityPulse, index, true);
                var bidQuantity = QuoteQuantity(seed, MarketMinute, _liquidityPulse, index, false);
                asks.Add(new MarketOrderBookLevel(
                    MarketOrderBookSide.Ask,
                    askPrice,
                    askQuantity,
                    askQuantity >= 8500,
                    isStructuralWall: askQuantity >= 8500));
                bids.Add(new MarketOrderBookLevel(
                    MarketOrderBookSide.Bid,
                    bidPrice,
                    bidQuantity,
                    bidQuantity >= 8500,
                    isStructuralWall: bidQuantity >= 8500));
                askPrice = MarketOrderBookPresentationRules.AdjacentPrice(askPrice, 1, market);
                bidPrice = MarketOrderBookPresentationRules.AdjacentPrice(bidPrice, -1, market);
            }

            var fullDayTurnover = 80d + seed % 6000;
            var elapsedRegularMinutes = Math.Max(1, MarketMinute - MarketSessionClock.OpenMinute + 1);
            var turnover = fullDayTurnover * Math.Min(390, elapsedRegularMinutes) / 390d;
            var capacity = Math.Max(1, MarketOrderBookRules.MinuteCapacityUnits(turnover, current));
            state.Snapshot = new MarketOrderBookSnapshot(
                asks,
                bids,
                turnover,
                fullDayTurnover,
                capacity,
                sourceLastTradePrice: current);

            if (recordTape && clock.Phase == MarketSessionPhase.Regular)
            {
                var quantity = 10 + StableHash($"tape:{seed}:{MarketMinute}:{_liquidityPulse}") % 990;
                AddTape(new StockMarketTradePrint(
                    state.Security.CompanyId,
                    MarketMinute,
                    _liquidityPulse,
                    current,
                    quantity,
                    state.LastTradeLevelSide == MarketOrderBookSide.Ask,
                    false));
            }
        }

        private long PriceAtMinute(
            AssetState state,
            int seed,
            MarketSessionPhase phase,
            int marketMinute)
        {
            if (phase == MarketSessionPhase.OpeningTransition ||
                phase == MarketSessionPhase.Holiday ||
                phase == MarketSessionPhase.Closed)
                return state.PreviousClose;

            var market = state.Security.PriceRuleMarket;
            var tick = MarketPricingRules.TickSize(state.PreviousClose, market);
            var through = Clamp(
                marketMinute - MarketSessionClock.OpenMinute,
                0,
                MarketSessionClock.CloseMinute - MarketSessionClock.OpenMinute);
            var movementTicks = 0;
            for (var minute = 0; minute <= through; minute += 1)
                movementTicks += StableHash($"path:{seed}:{minute}") % 3 - 1;
            var range = MarketPricingRules.DailyPriceRange(state.PreviousClose, Date, market);
            return Math.Max(
                range.Lower,
                Math.Min(
                    range.Upper,
                    MarketPricingRules.SnapPrice(
                        state.PreviousClose + movementTicks * tick,
                        market)));
        }

        private void AddTape(StockMarketTradePrint print)
        {
            _tradeTape.Insert(0, print);
            if (_tradeTape.Count > 50) _tradeTape.RemoveRange(50, _tradeTape.Count - 50);
        }

        private static StockMarketOrderResult Rejected(string message, int requestedQuantity)
        {
            return new StockMarketOrderResult(
                false,
                message,
                Math.Max(0, requestedQuantity),
                0,
                0,
                0,
                0d,
                null);
        }

        private static int QuoteQuantity(int seed, int minute, int pulse, int index, bool ask)
        {
            var value = StableHash($"depth:{seed}:{minute}:{pulse}:{index}:{(ask ? 1 : 0)}");
            return Math.Max(
                MarketOrderBookRules.MinimumDisplayedQuantity,
                ((100 + value % 9900) / 10) * 10);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (var index = 0; index < value.Length; index += 1)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private long TotalPendingBuyReservation(IEnumerable<MarketPendingOrder> pending)
        {
            var total = 0L;
            foreach (var order in pending)
            {
                if (order.Side != MarketPendingOrderSide.Buy) continue;
                var notional = checked(order.LimitPrice * (long)Math.Ceiling(order.RemainingQuantity));
                total = checked(total + MarketTradingCosts.BuyReservation(Date, notional));
            }
            return total;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

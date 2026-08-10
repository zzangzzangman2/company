using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>
    /// Exact desktop projection of SIMUL's stockOrderBookPresentationLevels.
    /// The engine keeps ten executable levels per side; the screen projects a
    /// continuous seven-ask/seven-bid axis and represents missing ticks as
    /// zero-depth rows. Zero-depth rows are selectable prices, never liquidity.
    /// </summary>
    public static class MarketOrderBookPresentationRules
    {
        public const int VisibleRowsPerSide = 7;

        public static IReadOnlyList<MarketOrderBookLevel> BuildVisibleLevels(
            MarketOrderBookSnapshot snapshot,
            string market,
            int sideRowCount = VisibleRowsPerSide,
            IEnumerable<MarketOrderBookLevel> marketLevels = null,
            bool preserveEmptyMarketLevelPrices = false,
            long? touchReferencePrice = null,
            MarketOrderBookSide? touchReferenceSide = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var sideRows = Math.Max(0, sideRowCount);
            if (sideRows == 0) return Array.Empty<MarketOrderBookLevel>();

            var source = marketLevels ?? snapshot.Asks.Concat(snapshot.Bids);
            var asks = source
                .Where(level =>
                    level.Side == MarketOrderBookSide.Ask &&
                    (level.Quantity > 0 || preserveEmptyMarketLevelPrices))
                .OrderBy(level => level.Price)
                .ToArray();
            var bids = source
                .Where(level =>
                    level.Side == MarketOrderBookSide.Bid &&
                    (level.Quantity > 0 || preserveEmptyMarketLevelPrices))
                .OrderByDescending(level => level.Price)
                .ToArray();
            if (asks.Length == 0 && bids.Length == 0)
                return Array.Empty<MarketOrderBookLevel>();

            var actual = new Dictionary<(MarketOrderBookSide Side, long Price), MarketOrderBookLevel>();
            foreach (var level in asks) actual[(level.Side, level.Price)] = level;
            foreach (var level in bids) actual[(level.Side, level.Price)] = level;

            MarketOrderBookLevel RowAt(MarketOrderBookSide side, long price)
            {
                return actual.TryGetValue((side, price), out var level)
                    ? level
                    : new MarketOrderBookLevel(side, price, 0);
            }

            long bestAskPrice;
            if (touchReferencePrice.HasValue && touchReferenceSide.HasValue)
            {
                bestAskPrice = touchReferenceSide.Value == MarketOrderBookSide.Ask
                    ? touchReferencePrice.Value
                    : AdjacentPrice(touchReferencePrice.Value, 1, market);
            }
            else if (asks.Length > 0)
            {
                bestAskPrice = asks[0].Price;
            }
            else
            {
                bestAskPrice = AdjacentPrice(bids[0].Price, 1, market);
            }

            var ascendingAsks = new List<MarketOrderBookLevel>(sideRows);
            var price = bestAskPrice;
            for (var index = 0; index < sideRows; index += 1)
            {
                ascendingAsks.Add(RowAt(MarketOrderBookSide.Ask, price));
                price = AdjacentPrice(price, 1, market);
            }

            var descendingBids = new List<MarketOrderBookLevel>(sideRows);
            price = AdjacentPrice(bestAskPrice, -1, market);
            for (var index = 0; index < sideRows && price > 0; index += 1)
            {
                descendingBids.Add(RowAt(MarketOrderBookSide.Bid, price));
                price = AdjacentPrice(price, -1, market);
            }

            var result = new List<MarketOrderBookLevel>(ascendingAsks.Count + descendingBids.Count);
            for (var index = ascendingAsks.Count - 1; index >= 0; index -= 1)
                result.Add(ascendingAsks[index]);
            result.AddRange(descendingBids);
            return new ReadOnlyCollection<MarketOrderBookLevel>(result);
        }

        /// <summary>
        /// Selects the idle current-price outline at the central touch. Active
        /// replay targets its exact execution row instead of this fallback.
        /// </summary>
        public static MarketOrderBookLevel CentralOutlineLevel(
            IReadOnlyList<MarketOrderBookLevel> levels,
            long? referencePrice,
            MarketOrderBookSide? referenceSide)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            MarketOrderBookLevel bestAsk = null;
            MarketOrderBookLevel bestBid = null;
            for (var index = 0; index < levels.Count; index += 1)
            {
                var level = levels[index];
                if (level.Side == MarketOrderBookSide.Ask) bestAsk = level;
                else if (bestBid == null) bestBid = level;
            }

            if (referencePrice.HasValue && referenceSide.HasValue)
            {
                for (var index = 0; index < levels.Count; index += 1)
                {
                    var level = levels[index];
                    if (level.Side != referenceSide.Value && level.Price == referencePrice.Value)
                        return level;
                }
            }

            if (referenceSide == MarketOrderBookSide.Ask) return bestAsk ?? bestBid;
            if (referenceSide == MarketOrderBookSide.Bid) return bestBid ?? bestAsk;
            if (referencePrice.HasValue && bestAsk != null && referencePrice.Value >= bestAsk.Price)
                return bestAsk;
            return bestBid ?? bestAsk;
        }

        /// <summary>
        /// Exact port of SIMUL's orderBookPresentationLevelsWithPlayerOrders.
        /// Player quotes can add a zero-external-depth row, but their quantity is
        /// still kept separate by the caller and never becomes fake market depth.
        /// </summary>
        public static IReadOnlyList<MarketOrderBookLevel> WithPlayerOrders(
            IEnumerable<MarketOrderBookLevel> marketLevels,
            IEnumerable<MarketPendingOrder> playerOrders,
            int visibleSideRows = VisibleRowsPerSide)
        {
            if (marketLevels == null) throw new ArgumentNullException(nameof(marketLevels));
            if (playerOrders == null) throw new ArgumentNullException(nameof(playerOrders));
            var levels = marketLevels.ToList();
            foreach (var order in playerOrders)
            {
                if (order == null || order.LimitPrice <= 0 || order.RemainingQuantity <= 0d) continue;
                var side = order.Side == MarketPendingOrderSide.Sell
                    ? MarketOrderBookSide.Ask
                    : MarketOrderBookSide.Bid;
                if (levels.Any(level => level.Side == side && level.Price == order.LimitPrice)) continue;
                levels.Add(new MarketOrderBookLevel(side, order.LimitPrice, 0));
            }

            var sideRows = Math.Max(0, visibleSideRows);
            var asks = levels
                .Where(level => level.Side == MarketOrderBookSide.Ask)
                .OrderBy(level => level.Price)
                .Take(sideRows)
                .Reverse();
            var bids = levels
                .Where(level => level.Side == MarketOrderBookSide.Bid)
                .OrderByDescending(level => level.Price)
                .Take(sideRows);
            return new ReadOnlyCollection<MarketOrderBookLevel>(asks.Concat(bids).ToArray());
        }

        public static long AdjacentPrice(long price, int direction, string market)
        {
            if (direction == 0) return MarketPricingRules.SnapPrice(price, market);
            var tickReference = direction > 0 ? price : Math.Max(1L, price - 1L);
            var tick = MarketPricingRules.TickSize(tickReference, market);
            return MarketPricingRules.SnapPrice(price + direction * tick, market);
        }
    }
}

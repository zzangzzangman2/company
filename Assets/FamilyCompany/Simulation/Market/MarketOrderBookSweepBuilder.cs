using System;
using System.Collections.Generic;
using System.Globalization;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>
    /// Turns a minute's price move into the ordered level fills the order book replays.
    ///
    /// SIMUL builds a sweep from <c>minuteTransition.orderedFills</c>: one step per price level the
    /// aggressor consumed, in the order it consumed them, each carrying the quantity taken and what
    /// was left. The last fill is the one that crossed the boundary.
    ///
    /// This market regenerates its book from a seed each minute rather than matching an order queue,
    /// so there is no fill list to read. The equivalent is derived instead: a trade that moved the
    /// price from 21,100 to 21,250 necessarily took every ask level in between, so those levels
    /// become the steps in that same order. A minute that did not move the price is a single step at
    /// the traded price, which is what SIMUL emits for a synthetic per-pulse trade.
    ///
    /// The point of walking the levels rather than jumping is that the border, the price axis, the
    /// header and the tape all read the same cursor. Jumping would show the destination immediately
    /// and nothing would appear to move.
    /// </summary>
    public static class MarketOrderBookSweepBuilder
    {
        /// <summary>
        /// Levels one sweep may cross. A halt-sized gap would otherwise produce a sweep long enough
        /// to still be replaying when the next minute arrives.
        /// </summary>
        public const int MaximumSteps = 12;

        /// <summary>
        /// Builds the batch for one canonical market minute, or null when there is nothing to replay.
        /// </summary>
        /// <param name="assetId">Security the sweep belongs to; part of the replay identity.</param>
        /// <param name="dateKey">Session day, so a repeated minute on another day is a new batch.</param>
        /// <param name="marketMinute">Canonical minute that produced the move.</param>
        /// <param name="liquidityPulse">Pulse frame the book was rebuilt at.</param>
        /// <param name="previousTradePrice">Traded price before this minute; 0 when unknown.</param>
        /// <param name="tradePrice">Traded price after this minute.</param>
        /// <param name="tradeSide">Side the trade printed on.</param>
        /// <param name="market">Price rule market, that is, the tick ladder to walk.</param>
        /// <param name="previousSnapshot">Book as it stood before the move, for resting quantities.</param>
        public static MarketOrderBookReplayBatch Build(
            string assetId,
            string dateKey,
            int marketMinute,
            int liquidityPulse,
            long previousTradePrice,
            long tradePrice,
            MarketOrderBookSide tradeSide,
            string market,
            MarketOrderBookSnapshot previousSnapshot)
        {
            if (tradePrice <= 0) return null;

            var prices = WalkPrices(previousTradePrice, tradePrice, market);
            if (prices.Count == 0) return null;

            var steps = new List<MarketOrderBookSweepStep>(prices.Count);
            for (var index = 0; index < prices.Count; index += 1)
            {
                var price = prices[index];
                var last = index == prices.Count - 1;
                var resting = RestingQuantity(previousSnapshot, tradeSide, price);
                // Every level before the last is fully taken; the last one is where the aggressor
                // ran out, so it keeps whatever was not consumed.
                var consumed = last ? Math.Max(1, resting / 2) : Math.Max(1, resting);
                var remaining = last ? Math.Max(0, resting - consumed) : 0;
                steps.Add(new MarketOrderBookSweepStep(
                    marketMinute,
                    liquidityPulse,
                    index,
                    tradeSide,
                    price,
                    consumed,
                    remaining,
                    structuralBreach: false,
                    boundaryCrossed: last));
            }

            var identity = string.Join(
                ":",
                "sweep",
                assetId ?? string.Empty,
                dateKey ?? string.Empty,
                marketMinute.ToString(CultureInfo.InvariantCulture),
                liquidityPulse.ToString(CultureInfo.InvariantCulture),
                tradePrice.ToString(CultureInfo.InvariantCulture));
            return new MarketOrderBookReplayBatch(identity, "runtime-session", steps);
        }

        /// <summary>
        /// Price ladder from just past <paramref name="from"/> through <paramref name="to"/>
        /// inclusive. A move of zero, or an unknown previous price, yields the single traded level.
        /// </summary>
        private static List<long> WalkPrices(long from, long to, string market)
        {
            var prices = new List<long>(MaximumSteps);
            if (from <= 0 || from == to)
            {
                prices.Add(to);
                return prices;
            }

            var direction = to > from ? 1 : -1;
            var price = MarketOrderBookPresentationRules.AdjacentPrice(from, direction, market);
            var guard = 0;
            while (guard < MaximumSteps)
            {
                guard += 1;
                prices.Add(price);
                if (direction > 0 ? price >= to : price <= to) break;
                var next = MarketOrderBookPresentationRules.AdjacentPrice(price, direction, market);
                // A tick table that stops moving would otherwise spin here.
                if (next == price) break;
                price = next;
            }

            // A gap larger than the cap ends on the real traded price so the ladder, the header and
            // the tape agree on where the sweep finished.
            if (prices.Count > 0 && prices[prices.Count - 1] != to) prices[prices.Count - 1] = to;
            return prices;
        }

        private static int RestingQuantity(
            MarketOrderBookSnapshot snapshot,
            MarketOrderBookSide side,
            long price)
        {
            if (snapshot == null) return 1;
            var levels = side == MarketOrderBookSide.Ask ? snapshot.Asks : snapshot.Bids;
            if (levels == null) return 1;
            for (var index = 0; index < levels.Count; index += 1)
            {
                var level = levels[index];
                if (level.Price == price) return Math.Max(1, level.Quantity);
            }

            return 1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Simulation.Market
{
    public enum MarketPendingOrderSide
    {
        Buy,
        Sell,
    }

    public sealed class MarketPendingOrder
    {
        public MarketPendingOrder(
            string id,
            MarketPendingOrderSide side,
            string assetId,
            long limitPrice,
            double originalQuantity,
            double remainingQuantity,
            DateTime placedDate,
            int placedMinute,
            int placedSequence,
            double queueAheadQuantity = 0d,
            int? maximumPositionUnits = null,
            bool isIpoFirstTradingDay = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Pending order ID is required.", nameof(id));
            if (string.IsNullOrEmpty(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (!Enum.IsDefined(typeof(MarketPendingOrderSide), side)) throw new ArgumentOutOfRangeException(nameof(side));
            if (limitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(limitPrice));
            if (!IsFinite(originalQuantity) || originalQuantity <= 0d) throw new ArgumentOutOfRangeException(nameof(originalQuantity));
            if (!IsFinite(remainingQuantity) || remainingQuantity <= 0d || remainingQuantity > originalQuantity)
                throw new ArgumentOutOfRangeException(nameof(remainingQuantity));
            if (placedMinute < 0) throw new ArgumentOutOfRangeException(nameof(placedMinute));
            if (placedSequence < 0) throw new ArgumentOutOfRangeException(nameof(placedSequence));
            if (!IsFinite(queueAheadQuantity) || queueAheadQuantity < 0d)
                throw new ArgumentOutOfRangeException(nameof(queueAheadQuantity));
            if (maximumPositionUnits.HasValue && maximumPositionUnits.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumPositionUnits));

            Id = id;
            Side = side;
            AssetId = assetId;
            LimitPrice = limitPrice;
            OriginalQuantity = originalQuantity;
            RemainingQuantity = remainingQuantity;
            PlacedDate = placedDate.Date;
            PlacedMinute = placedMinute;
            PlacedSequence = placedSequence;
            QueueAheadQuantity = queueAheadQuantity;
            MaximumPositionUnits = maximumPositionUnits;
            IsIpoFirstTradingDay = isIpoFirstTradingDay;
        }

        public string Id { get; }
        public MarketPendingOrderSide Side { get; }
        public string AssetId { get; }
        public long LimitPrice { get; }
        public double OriginalQuantity { get; }
        public double RemainingQuantity { get; }
        public DateTime PlacedDate { get; }
        public int PlacedMinute { get; }
        public int PlacedSequence { get; }
        public double QueueAheadQuantity { get; }
        public int? MaximumPositionUnits { get; }
        public bool IsIpoFirstTradingDay { get; }
        public double FilledQuantity => OriginalQuantity - RemainingQuantity;

        public MarketPendingOrder With(
            double? remainingQuantity = null,
            double? queueAheadQuantity = null)
        {
            return new MarketPendingOrder(
                Id,
                Side,
                AssetId,
                LimitPrice,
                OriginalQuantity,
                remainingQuantity ?? RemainingQuantity,
                PlacedDate,
                PlacedMinute,
                PlacedSequence,
                queueAheadQuantity ?? QueueAheadQuantity,
                MaximumPositionUnits,
                IsIpoFirstTradingDay);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct MarketRestingQueueConsumption
    {
        public MarketRestingQueueConsumption(
            bool represented,
            bool touched,
            int consumedQuantity,
            double queueAheadQuantity,
            int remainingCapacity)
        {
            Represented = represented;
            Touched = touched;
            ConsumedQuantity = consumedQuantity;
            QueueAheadQuantity = queueAheadQuantity;
            RemainingCapacity = remainingCapacity;
        }

        public bool Represented { get; }
        public bool Touched { get; }
        public int ConsumedQuantity { get; }
        public double QueueAheadQuantity { get; }
        public int RemainingCapacity { get; }
        public bool MayFill => Represented && Touched && QueueAheadQuantity <= 0d && RemainingCapacity > 0;
    }

    /// <summary>
    /// Literal pure-C# port of SIMUL's pending limit-order priority, queue
    /// release, resting-queue consumption and reservation invariants.
    /// </summary>
    public static class MarketPendingOrderRules
    {
        private const double QuantityEpsilon = 0.000001d;

        public static IReadOnlyList<MarketPendingOrder> InExchangePriority(
            IEnumerable<MarketPendingOrder> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var buys = new List<MarketPendingOrder>();
            var sells = new List<MarketPendingOrder>();
            foreach (var order in source)
            {
                if (order == null) throw new ArgumentException("Pending order list contains null.", nameof(source));
                if (order.Side == MarketPendingOrderSide.Buy) buys.Add(order);
                else sells.Add(order);
            }
            buys.Sort(WithinSide);
            sells.Sort(WithinSide);

            var ordered = new List<MarketPendingOrder>(buys.Count + sells.Count);
            var buyIndex = 0;
            var sellIndex = 0;
            while (buyIndex < buys.Count || sellIndex < sells.Count)
            {
                if (buyIndex >= buys.Count) ordered.Add(sells[sellIndex++]);
                else if (sellIndex >= sells.Count) ordered.Add(buys[buyIndex++]);
                else if (Chronological(buys[buyIndex], sells[sellIndex]) <= 0)
                    ordered.Add(buys[buyIndex++]);
                else ordered.Add(sells[sellIndex++]);
            }
            return new ReadOnlyCollection<MarketPendingOrder>(ordered);
        }

        public static IReadOnlyList<MarketPendingOrder> AfterQueueRelease(
            IEnumerable<MarketPendingOrder> orders,
            MarketPendingOrder source,
            double releasedQuantity,
            bool removeSource)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            if (source == null) throw new ArgumentNullException(nameof(source));
            var released = IsFinite(releasedQuantity) ? Math.Max(0d, releasedQuantity) : 0d;
            var result = new List<MarketPendingOrder>();
            foreach (var order in orders)
            {
                if (order.Id == source.Id && removeSource) continue;
                var affected = order.Id != source.Id &&
                               released > 0d &&
                               order.AssetId == source.AssetId &&
                               order.Side == source.Side &&
                               order.LimitPrice == source.LimitPrice &&
                               order.PlacedSequence > source.PlacedSequence;
                result.Add(affected
                    ? order.With(queueAheadQuantity: Math.Max(0d, order.QueueAheadQuantity - released))
                    : order);
            }
            return new ReadOnlyCollection<MarketPendingOrder>(result);
        }

        public static IReadOnlyList<MarketPendingOrder> Cancel(
            IEnumerable<MarketPendingOrder> orders,
            string orderId)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            var snapshot = new List<MarketPendingOrder>(orders);
            var index = snapshot.FindIndex(order => order.Id == orderId);
            if (index < 0) throw new KeyNotFoundException($"Pending order not found: {orderId}");
            var source = snapshot[index];
            return AfterQueueRelease(snapshot, source, source.RemainingQuantity, removeSource: true);
        }

        public static double QueueAheadForNewOrder(
            MarketOrderBookSnapshot snapshot,
            string assetId,
            MarketPendingOrderSide side,
            long limitPrice,
            IEnumerable<MarketPendingOrder> existingOrders,
            bool immediateFillOccurred)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (existingOrders == null) throw new ArgumentNullException(nameof(existingOrders));
            if (immediateFillOccurred) return 0d;
            var levels = side == MarketPendingOrderSide.Buy ? snapshot.Bids : snapshot.Asks;
            var standing = 0d;
            for (var index = 0; index < levels.Count; index += 1)
            {
                if (levels[index].Price == limitPrice)
                {
                    standing = levels[index].Quantity;
                    break;
                }
            }
            var playerAhead = 0d;
            foreach (var pending in existingOrders)
            {
                if (pending.AssetId == assetId &&
                    pending.Side == side &&
                    pending.LimitPrice == limitPrice)
                    playerAhead += pending.RemainingQuantity;
            }
            return standing + playerAhead;
        }

        public static MarketRestingQueueConsumption ConsumeRestingQueue(
            MarketPendingOrder order,
            MarketOrderBookSnapshot snapshot,
            long currentPrice,
            int availableCapacity)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var capacity = Math.Max(0, availableCapacity);
            var levels = order.Side == MarketPendingOrderSide.Buy ? snapshot.Bids : snapshot.Asks;
            var represented = false;
            for (var index = 0; index < levels.Count; index += 1)
            {
                if (levels[index].Price == order.LimitPrice)
                {
                    represented = true;
                    break;
                }
            }
            var touched = order.Side == MarketPendingOrderSide.Buy
                ? order.LimitPrice >= currentPrice
                : order.LimitPrice <= currentPrice;
            if (!represented || !touched || capacity <= 0)
                return new MarketRestingQueueConsumption(
                    represented,
                    touched,
                    0,
                    order.QueueAheadQuantity,
                    capacity);

            var queueCeiling = order.QueueAheadQuantity >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Ceiling(order.QueueAheadQuantity);
            var consumed = Math.Min(queueCeiling, capacity);
            return new MarketRestingQueueConsumption(
                true,
                true,
                consumed,
                Math.Max(0d, order.QueueAheadQuantity - consumed),
                capacity - consumed);
        }

        public static IReadOnlyList<MarketPendingOrder> AfterFill(
            IEnumerable<MarketPendingOrder> orders,
            MarketPendingOrder source,
            double filledQuantity)
        {
            if (!IsFinite(filledQuantity) || filledQuantity <= 0d)
                throw new ArgumentOutOfRangeException(nameof(filledQuantity));
            if (filledQuantity > source.RemainingQuantity + QuantityEpsilon)
                throw new ArgumentOutOfRangeException(nameof(filledQuantity));
            var result = new List<MarketPendingOrder>(AfterQueueRelease(
                orders,
                source,
                filledQuantity,
                removeSource: true));
            var remaining = source.RemainingQuantity - filledQuantity;
            if (remaining > QuantityEpsilon)
                result.Add(source.With(remainingQuantity: remaining, queueAheadQuantity: 0d));
            return new ReadOnlyCollection<MarketPendingOrder>(result);
        }

        public static long PendingBuyReservedCash(
            long brokerageCash,
            IEnumerable<MarketPendingOrder> orders,
            decimal tradingFeeRate,
            decimal feeMultiplier = 1m)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            var ceiling = Math.Max(0L, brokerageCash);
            var reserved = 0L;
            foreach (var order in orders)
            {
                if (order.Side != MarketPendingOrderSide.Buy) continue;
                decimal reservation;
                try
                {
                    reservation = order.LimitPrice * (decimal)order.RemainingQuantity *
                                  (1m + tradingFeeRate * feeMultiplier);
                }
                catch (OverflowException)
                {
                    return ceiling;
                }
                if (reservation <= 0m) continue;
                var remainingCash = ceiling - reserved;
                if (remainingCash <= 0L || reservation >= remainingCash) return ceiling;
                var rounded = decimal.Ceiling(reservation);
                if (rounded >= long.MaxValue) return ceiling;
                reserved = checked(reserved + (long)rounded);
            }
            return reserved;
        }

        public static long AvailableBrokerageCash(
            long brokerageCash,
            IEnumerable<MarketPendingOrder> orders,
            decimal tradingFeeRate,
            decimal feeMultiplier = 1m)
        {
            var reserved = PendingBuyReservedCash(
                brokerageCash,
                orders,
                tradingFeeRate,
                feeMultiplier);
            return Math.Max(0L, brokerageCash - reserved);
        }

        public static double PendingReservedUnits(
            IEnumerable<MarketPendingOrder> orders,
            string assetId,
            MarketPendingOrderSide side)
        {
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            var reserved = 0d;
            foreach (var order in orders)
            {
                if (order.AssetId == assetId && order.Side == side)
                    reserved += order.RemainingQuantity;
            }
            return reserved;
        }

        private static int WithinSide(MarketPendingOrder left, MarketPendingOrder right)
        {
            var priceOrder = left.Side == MarketPendingOrderSide.Buy
                ? right.LimitPrice.CompareTo(left.LimitPrice)
                : left.LimitPrice.CompareTo(right.LimitPrice);
            if (priceOrder != 0) return priceOrder;
            return Chronological(left, right);
        }

        private static int Chronological(MarketPendingOrder left, MarketPendingOrder right)
        {
            var dateOrder = left.PlacedDate.CompareTo(right.PlacedDate);
            if (dateOrder != 0) return dateOrder;
            var minuteOrder = left.PlacedMinute.CompareTo(right.PlacedMinute);
            if (minuteOrder != 0) return minuteOrder;
            var sequenceOrder = left.PlacedSequence.CompareTo(right.PlacedSequence);
            return sequenceOrder != 0
                ? sequenceOrder
                : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

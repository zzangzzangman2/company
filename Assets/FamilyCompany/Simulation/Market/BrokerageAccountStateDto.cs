using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>
    /// Pure brokerage-only transfer object. It deliberately contains no
    /// CompanyState or save infrastructure dependency, so the stock module can
    /// validate and atomically apply it before the shared schema is approved.
    /// </summary>
    public sealed class BrokerageAccountStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public BrokerageAccountStateDto(
            long cashWon,
            IEnumerable<BrokeragePositionStateDto> positions,
            IEnumerable<BrokeragePendingOrderStateDto> pendingOrders,
            IEnumerable<BrokerageTradeStateDto> playerTrades,
            IEnumerable<BrokerageOrderJournalStateDto> orderJournal,
            IEnumerable<string> favoriteAssetIds,
            int orderSequence,
            int journalSequence,
            int schemaVersion = CurrentSchemaVersion)
        {
            SchemaVersion = schemaVersion;
            CashWon = cashWon;
            Positions = ReadOnly(positions);
            PendingOrders = ReadOnly(pendingOrders);
            PlayerTrades = ReadOnly(playerTrades);
            OrderJournal = ReadOnly(orderJournal);
            FavoriteAssetIds = ReadOnly(favoriteAssetIds);
            OrderSequence = orderSequence;
            JournalSequence = journalSequence;
        }

        public int SchemaVersion { get; }
        public long CashWon { get; }
        public IReadOnlyList<BrokeragePositionStateDto> Positions { get; }
        public IReadOnlyList<BrokeragePendingOrderStateDto> PendingOrders { get; }
        public IReadOnlyList<BrokerageTradeStateDto> PlayerTrades { get; }
        public IReadOnlyList<BrokerageOrderJournalStateDto> OrderJournal { get; }
        public IReadOnlyList<string> FavoriteAssetIds { get; }
        public int OrderSequence { get; }
        public int JournalSequence { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ReadOnlyCollection<T>(new List<T>(source));
        }
    }

    public sealed class BrokeragePositionStateDto
    {
        public BrokeragePositionStateDto(string assetId, int units, double averageCostWon)
        {
            AssetId = assetId ?? string.Empty;
            Units = units;
            AverageCostWon = averageCostWon;
        }

        public string AssetId { get; }
        public int Units { get; }
        public double AverageCostWon { get; }
    }

    public sealed class BrokeragePendingOrderStateDto
    {
        public BrokeragePendingOrderStateDto(MarketPendingOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            Id = order.Id;
            Side = order.Side;
            AssetId = order.AssetId;
            LimitPrice = order.LimitPrice;
            OriginalQuantity = order.OriginalQuantity;
            RemainingQuantity = order.RemainingQuantity;
            PlacedDate = order.PlacedDate;
            PlacedMinute = order.PlacedMinute;
            PlacedSequence = order.PlacedSequence;
            QueueAheadQuantity = order.QueueAheadQuantity;
            MaximumPositionUnits = order.MaximumPositionUnits;
            IsIpoFirstTradingDay = order.IsIpoFirstTradingDay;
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

        public MarketPendingOrder ToDomain()
        {
            return new MarketPendingOrder(
                Id,
                Side,
                AssetId,
                LimitPrice,
                OriginalQuantity,
                RemainingQuantity,
                PlacedDate,
                PlacedMinute,
                PlacedSequence,
                QueueAheadQuantity,
                MaximumPositionUnits,
                IsIpoFirstTradingDay);
        }
    }

    public sealed class BrokerageTradeStateDto
    {
        public BrokerageTradeStateDto(StockMarketTradePrint trade)
        {
            if (trade == null) throw new ArgumentNullException(nameof(trade));
            AssetId = trade.AssetId;
            MarketMinute = trade.MarketMinute;
            LiquidityPulse = trade.LiquidityPulse;
            Price = trade.Price;
            Quantity = trade.Quantity;
            IsBuy = trade.IsBuy;
        }

        public string AssetId { get; }
        public int MarketMinute { get; }
        public int LiquidityPulse { get; }
        public long Price { get; }
        public int Quantity { get; }
        public bool IsBuy { get; }

        public StockMarketTradePrint ToDomain()
        {
            return new StockMarketTradePrint(
                AssetId,
                MarketMinute,
                LiquidityPulse,
                Price,
                Quantity,
                IsBuy,
                true);
        }
    }

    public sealed class BrokerageOrderJournalStateDto
    {
        public BrokerageOrderJournalStateDto(StockMarketOrderJournalEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Sequence = entry.Sequence;
            AssetId = entry.AssetId;
            MarketMinute = entry.MarketMinute;
            IsBuy = entry.IsBuy;
            IsMarket = entry.IsMarket;
            LimitPrice = entry.LimitPrice;
            RequestedQuantity = entry.RequestedQuantity;
            FilledQuantity = entry.FilledQuantity;
            RemainingQuantity = entry.RemainingQuantity;
            AveragePrice = entry.AveragePrice;
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

        public StockMarketOrderJournalEntry ToDomain()
        {
            return new StockMarketOrderJournalEntry(
                Sequence,
                AssetId,
                MarketMinute,
                IsBuy,
                IsMarket,
                LimitPrice,
                RequestedQuantity,
                FilledQuantity,
                RemainingQuantity,
                AveragePrice);
        }
    }
}

using System;

namespace FamilyCompany.Simulation.Market
{
    public sealed class StockMarketSessionStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public StockMarketSessionStateDto(
            bool initialized,
            DateTime date,
            int marketMinute,
            double realtimeResidualSeconds,
            int playbackIndex,
            bool openingAuctionProcessed,
            int openingAuctionProcessCount,
            int canonicalMinuteUpdateCount,
            int liquidityPulse,
            BrokerageAccountStateDto brokerage,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (double.IsNaN(realtimeResidualSeconds) || double.IsInfinity(realtimeResidualSeconds) ||
                realtimeResidualSeconds < 0d || realtimeResidualSeconds >= StockMarketRealtimeClock.SecondsPerTick)
                throw new ArgumentOutOfRangeException(nameof(realtimeResidualSeconds));
            SchemaVersion = schemaVersion;
            Initialized = initialized;
            Date = date.Date;
            MarketMinute = marketMinute;
            RealtimeResidualSeconds = realtimeResidualSeconds;
            PlaybackIndex = playbackIndex;
            OpeningAuctionProcessed = openingAuctionProcessed;
            OpeningAuctionProcessCount = openingAuctionProcessCount;
            CanonicalMinuteUpdateCount = canonicalMinuteUpdateCount;
            LiquidityPulse = liquidityPulse;
            Brokerage = brokerage ?? throw new ArgumentNullException(nameof(brokerage));
        }

        public int SchemaVersion { get; }
        public bool Initialized { get; }
        public DateTime Date { get; }
        public int MarketMinute { get; }
        public double RealtimeResidualSeconds { get; }
        public int PlaybackIndex { get; }
        public bool OpeningAuctionProcessed { get; }
        public int OpeningAuctionProcessCount { get; }
        public int CanonicalMinuteUpdateCount { get; }
        public int LiquidityPulse { get; }
        public BrokerageAccountStateDto Brokerage { get; }

        public static StockMarketSessionStateDto Uninitialized()
        {
            return new StockMarketSessionStateDto(
                false,
                DateTime.MinValue,
                MarketSessionClock.DayStartMinute,
                0d,
                1,
                false,
                0,
                0,
                0,
                new BrokerageAccountStateDto(
                    0L,
                    Array.Empty<BrokeragePositionStateDto>(),
                    Array.Empty<BrokeragePendingOrderStateDto>(),
                    Array.Empty<BrokerageTradeStateDto>(),
                    Array.Empty<BrokerageOrderJournalStateDto>(),
                    Array.Empty<string>(),
                    0,
                    0));
        }
    }
}

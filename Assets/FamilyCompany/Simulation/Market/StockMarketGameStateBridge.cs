using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.History;

namespace FamilyCompany.Simulation.Market
{
    public sealed class StockMarketRuntimeBinding
    {
        internal StockMarketRuntimeBinding(
            StockMarketRuntimeSession session,
            double realtimeResidualSeconds,
            int playbackIndex,
            bool restoredFullSession)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            RealtimeResidualSeconds = realtimeResidualSeconds;
            PlaybackIndex = playbackIndex;
            RestoredFullSession = restoredFullSession;
        }

        public StockMarketRuntimeSession Session { get; }
        public double RealtimeResidualSeconds { get; }
        public int PlaybackIndex { get; }
        public bool RestoredFullSession { get; }
    }

    /// <summary>
    /// Pure boundary between the long-lived GameState snapshot and the live
    /// stock runtime. Same-day restores preserve session idempotency counters;
    /// a changed trading date carries only the company brokerage account.
    /// </summary>
    public static class StockMarketGameStateBridge
    {
        public static StockMarketRuntimeBinding Load(
            GameState state,
            DateTime date,
            IEnumerable<MarketSecurityDefinition> securities,
            int initialMarketMinute)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (securities == null) throw new ArgumentNullException(nameof(securities));

            var stored = state.StockMarket ?? StockMarketSessionStateDto.Uninitialized();
            var sameTradingDate = stored.Initialized && stored.Date.Date == date.Date;
            var session = new StockMarketRuntimeSession(
                state.WorldSeed,
                date,
                0L,
                securities,
                sameTradingDate ? stored.MarketMinute : initialMarketMinute);

            if (!stored.Initialized)
                return new StockMarketRuntimeBinding(session, 0d, 1, false);

            string error;
            if (sameTradingDate)
            {
                if (!session.TryApplySessionState(stored, out error))
                    throw new InvalidOperationException($"Stock session restore failed: {error}");
                return new StockMarketRuntimeBinding(
                    session,
                    stored.RealtimeResidualSeconds,
                    stored.PlaybackIndex,
                    true);
            }

            if (!session.TryApplyBrokerageState(stored.Brokerage, out error))
                throw new InvalidOperationException($"Brokerage carry restore failed: {error}");
            return new StockMarketRuntimeBinding(session, 0d, stored.PlaybackIndex, false);
        }

        public static void Flush(
            GameState state,
            StockMarketRuntimeSession session,
            double realtimeResidualSeconds,
            int playbackIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (session == null) throw new ArgumentNullException(nameof(session));
            state.ReplaceStockMarketState(
                session.ExportSessionState(realtimeResidualSeconds, playbackIndex));
        }
    }
}

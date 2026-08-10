using System;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>Exact campaign-era fee and securities transaction-tax schedule.</summary>
    public static class MarketTradingCosts
    {
        public static decimal TradingFeeRate(DateTime date)
        {
            if (date.Date < new DateTime(2003, 1, 1)) return 0.0050m;
            if (date.Date < new DateTime(2007, 1, 1)) return 0.0040m;
            if (date.Date < new DateTime(2011, 1, 1)) return 0.0030m;
            return 0.0025m;
        }

        public static decimal SecuritiesTransactionTaxRate(DateTime date)
        {
            if (date.Date < new DateTime(2019, 1, 1)) return 0.0030m;
            if (date.Date < new DateTime(2021, 1, 1)) return 0.0025m;
            if (date.Date < new DateTime(2023, 1, 1)) return 0.0023m;
            if (date.Date < new DateTime(2024, 1, 1)) return 0.0020m;
            if (date.Date < new DateTime(2025, 1, 1)) return 0.0018m;
            return 0.0015m;
        }

        public static long TradingFee(
            DateTime date,
            long notionalWon,
            decimal feeMultiplier = 1m)
        {
            if (notionalWon <= 0) return 0;
            var raw = notionalWon * TradingFeeRate(date) * feeMultiplier;
            return Math.Max(1L, checked((long)decimal.Round(raw, 0, MidpointRounding.AwayFromZero)));
        }

        public static long SecuritiesTransactionTax(DateTime date, long notionalWon)
        {
            if (notionalWon <= 0) return 0;
            var raw = notionalWon * SecuritiesTransactionTaxRate(date);
            return Math.Max(1L, checked((long)decimal.Round(raw, 0, MidpointRounding.AwayFromZero)));
        }

        public static long BuyReservation(
            DateTime date,
            long notionalWon,
            decimal feeMultiplier = 1m)
        {
            if (notionalWon <= 0) return 0;
            var rate = TradingFeeRate(date) * feeMultiplier;
            return checked((long)decimal.Ceiling(notionalWon * (1m + rate)));
        }
    }
}

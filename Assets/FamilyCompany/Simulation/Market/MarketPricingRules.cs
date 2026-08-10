using System;

namespace FamilyCompany.Simulation.Market
{
    public readonly struct MarketPriceRange
    {
        public MarketPriceRange(long lower, long upper)
        {
            Lower = lower;
            Upper = upper;
        }

        public long Lower { get; }
        public long Upper { get; }
    }

    /// <summary>Exact tick, price-limit and IPO first-day rules from SIMUL.</summary>
    public static class MarketPricingRules
    {
        public const string GrowthMarketName = "도전시장";
        public const string ModernIpoPriceRangeEffectiveDateKey = "2023-06-26";
        public const decimal ModernIpoFirstDayLowerPriceMultiple = 0.60m;
        public const decimal ModernIpoFirstDayUpperPriceMultiple = 4.00m;

        public static decimal DailyPriceLimitRate(DateTime date)
        {
            return date.Date < new DateTime(2015, 6, 15) ? 0.15m : 0.30m;
        }

        public static long TickSize(decimal price, string market = "미래시장")
        {
            if (price <= 0m) return 1;
            if (price < 1000m) return 1;
            if (price < 5000m) return 5;
            if (price < 10000m) return 10;
            if (price < 50000m) return 50;
            if (price < 100000m) return 100;
            if (price < 500000m) return market == GrowthMarketName ? 100 : 500;
            return market == GrowthMarketName ? 100 : 1000;
        }

        public static long SnapPrice(
            decimal price,
            string market = "미래시장",
            bool roundDown = false)
        {
            if (price <= 0m) return 0;
            var tick = TickSize(price, market);
            var units = price / tick;
            var snappedUnits = roundDown
                ? decimal.Floor(units)
                : decimal.Round(units, 0, MidpointRounding.AwayFromZero);
            return checked((long)snappedUnits * tick);
        }

        public static bool UsesModernIpoFirstDayPriceRange(
            DateTime date,
            bool isIpoFirstTradingDay)
        {
            return isIpoFirstTradingDay && date.Date >= new DateTime(2023, 6, 26);
        }

        public static MarketPriceRange DailyPriceRange(
            decimal previousClose,
            DateTime date,
            string market = "미래시장",
            bool isIpoFirstTradingDay = false)
        {
            if (previousClose <= 0m) return new MarketPriceRange(0, 0);

            var modernIpo = UsesModernIpoFirstDayPriceRange(date, isIpoFirstTradingDay);
            var rate = DailyPriceLimitRate(date);
            var rawLower = previousClose *
                           (modernIpo ? ModernIpoFirstDayLowerPriceMultiple : 1m - rate);
            var rawUpper = previousClose *
                           (modernIpo ? ModernIpoFirstDayUpperPriceMultiple : 1m + rate);
            var lowerTick = TickSize(rawLower, market);
            var lower = checked((long)decimal.Ceiling(rawLower / lowerTick) * lowerTick);
            var upper = SnapPrice(rawUpper, market, true);
            return new MarketPriceRange(lower, upper);
        }

        public static bool IsValidOrderPrice(decimal price, string market = "미래시장")
        {
            if (price <= 0m) return false;
            return SnapPrice(price, market) == price;
        }
    }
}

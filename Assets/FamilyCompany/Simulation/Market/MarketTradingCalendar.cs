using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>
    /// Exact campaign trading calendar of SIMUL. The generated 6,545-date
    /// corpus is stored as a compact bitset in MarketTradingCalendarCorpus.g.cs.
    /// </summary>
    public static class MarketTradingCalendar
    {
        public static readonly DateTime CorpusFirstTradingDate = new DateTime(2000, 1, 4);
        public static readonly DateTime CorpusLastTradingDate = new DateTime(2026, 7, 23);

        private static readonly byte[] CorpusTradingDays =
            Convert.FromBase64String(MarketTradingCalendarCorpus.Base64);

        private static readonly HashSet<string> PostCorpusCampaignHolidayKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "2026-08-17",
                "2026-09-24",
                "2026-09-25",
                "2026-10-05"
            };

        public static int CorpusTradingDateCount => MarketTradingCalendarCorpus.TradingDateCount;
        public static string CorpusSourceSha256 => MarketTradingCalendarCorpus.SourceSha256;

        public static bool IsTradingDay(DateTime date)
        {
            date = date.Date;
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            if (date >= CorpusFirstTradingDate && date <= CorpusLastTradingDate)
            {
                var index = (date - CorpusFirstTradingDate).Days;
                return (CorpusTradingDays[index / 8] & (1 << (index % 8))) != 0;
            }

            if (PostCorpusCampaignHolidayKeys.Contains(MarketSessionClock.DateKey(date)))
            {
                return false;
            }

            return !IsFixedHoliday(date.Month, date.Day);
        }

        public static DateTime SettlementDateFor(DateTime tradeDate)
        {
            var date = tradeDate.Date;
            var remaining = SettlementTradingDays(date);
            while (remaining > 0)
            {
                date = date.AddDays(1);
                if (IsTradingDay(date)) remaining -= 1;
            }

            return date;
        }

        public static int SettlementTradingDays(DateTime tradeDate)
        {
            _ = tradeDate;
            return 2;
        }

        private static bool IsFixedHoliday(int month, int day)
        {
            return (month == 1 && day == 1) ||
                   (month == 3 && day == 1) ||
                   (month == 5 && day == 1) ||
                   (month == 5 && day == 5) ||
                   (month == 6 && day == 6) ||
                   (month == 7 && day == 17) ||
                   (month == 8 && day == 15) ||
                   (month == 10 && day == 3) ||
                   (month == 10 && day == 9) ||
                   (month == 12 && day == 31) ||
                   (month == 12 && day == 25);
        }
    }
}

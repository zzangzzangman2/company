using System;

namespace FamilyCompany.Simulation.Market
{
    public enum MarketSessionPhase
    {
        OpeningTransition = 0,
        Regular = 1,
        ClosingAuction = 2,
        CloseSettlement = 3,
        Closed = 4,
        Holiday = 5
    }

    public sealed class MarketClockInfo
    {
        public MarketClockInfo(
            MarketSessionPhase phase,
            string label,
            string description,
            bool tradable)
        {
            Phase = phase;
            Label = label ?? string.Empty;
            Description = description ?? string.Empty;
            Tradable = tradable;
        }

        public MarketSessionPhase Phase { get; }
        public string Label { get; }
        public string Description { get; }
        public bool Tradable { get; }
    }

    /// <summary>
    /// Exact C# contract of simul/flutter_app/lib/game/market_clock.dart.
    /// One simulation tick is always one game minute, independent of UI speed.
    /// </summary>
    public static class MarketSessionClock
    {
        public const int CampaignStartYear = 2000;
        public const int CampaignEndYear = 2026;
        public const int DayStartMinute = 8 * 60;
        public const int OpenMinute = 9 * 60;
        public const int ContinuousEndMinute = 14 * 60 + 50;
        public const int CloseMinute = 15 * 60;
        public const int DayEndMinute = 20 * 60;
        public const int TickMinutes = 1;
        public const int CloseTick = 420;
        public const int GeneratedSessionTicks = 720;
        public const int GeneratedPreOpenTicks = 60;
        public const int DecisionActionMinutes = 30;
        public const int AcademyHelpActionMinutes = 30;
        public const int WorkActionMinutes = 60;
        public const decimal DynamicVolatilityInterruptionRate = 0.03m;

        public static int AdvanceGameTime(int currentMinute, int elapsedMinutes)
        {
            return Clamp(currentMinute + elapsedMinutes, DayStartMinute, DayEndMinute);
        }

        public static int LiquidityDayKey(DateTime date)
        {
            return (date.Date - new DateTime(2000, 1, 1)).Days + 1;
        }

        public static string DateKey(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static MarketClockInfo At(int minute, bool tradingDay = true)
        {
            if (!tradingDay)
            {
                return new MarketClockInfo(
                    MarketSessionPhase.Holiday,
                    "휴장",
                    "오늘은 거래소가 쉬는 날이에요.",
                    false);
            }

            var value = Clamp(minute, DayStartMinute, DayEndMinute);
            if (value < OpenMinute)
            {
                return new MarketClockInfo(
                    MarketSessionPhase.OpeningTransition,
                    "개장 준비",
                    "08:00~08:59 · 가격 고정 · 09:00 정규장 개장",
                    false);
            }

            if (value < ContinuousEndMinute)
            {
                return new MarketClockInfo(
                    MarketSessionPhase.Regular,
                    "미래거래소 정규장",
                    "09:00~14:50 · 접속매매",
                    true);
            }

            if (value < CloseMinute)
            {
                return new MarketClockInfo(
                    MarketSessionPhase.ClosingAuction,
                    "장마감 동시호가",
                    "14:50~15:00 · 종가를 결정하는 중",
                    true);
            }

            if (value < DayEndMinute)
            {
                return new MarketClockInfo(
                    MarketSessionPhase.CloseSettlement,
                    "오늘 장 마감",
                    "15:00 종가 확정 · 추가 거래 없음",
                    false);
            }

            return new MarketClockInfo(
                MarketSessionPhase.Closed,
                "오늘 장 종료",
                "20:00 · 오늘 신문을 확인할 시간",
                false);
        }

        public static string TimeLabel(int minute)
        {
            var value = Clamp(minute, 0, 23 * 60 + 59);
            return $"{value / 60:00}:{value % 60:00}";
        }

        public static int TickForMinute(int minute)
        {
            var value = Clamp(minute, DayStartMinute, DayEndMinute);
            return Clamp((value - DayStartMinute) / TickMinutes, 0, GeneratedSessionTicks);
        }

        public static int MinuteForTick(int tick)
        {
            return Clamp(
                DayStartMinute + Clamp(tick, 0, GeneratedSessionTicks) * TickMinutes,
                DayStartMinute,
                DayEndMinute);
        }

        public static bool DynamicVolatilityInterruptionActive(
            int minute,
            decimal previousTradePrice,
            decimal currentPrice,
            bool tradingDay = true)
        {
            if (At(minute, tradingDay).Phase != MarketSessionPhase.Regular ||
                previousTradePrice <= 0m ||
                currentPrice <= 0m)
            {
                return false;
            }

            return Math.Abs((currentPrice - previousTradePrice) / previousTradePrice) >=
                   DynamicVolatilityInterruptionRate;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}

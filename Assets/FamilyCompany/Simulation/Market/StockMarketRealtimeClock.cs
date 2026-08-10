using System;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>
    /// Converts unscaled real seconds into exact game-minute batches without
    /// discarding sub-second residuals. One canonical tick is exactly 1.000s.
    /// </summary>
    public sealed class StockMarketRealtimeClock
    {
        public const double SecondsPerTick = 1d;

        private double _accumulatedSeconds;

        public double AccumulatedSeconds => _accumulatedSeconds;

        public int Consume(double unscaledDeltaSeconds, int gameMinutesPerSecond)
        {
            if (double.IsNaN(unscaledDeltaSeconds) || double.IsInfinity(unscaledDeltaSeconds) ||
                unscaledDeltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            if (gameMinutesPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(gameMinutesPerSecond));
            if (gameMinutesPerSecond == 0)
            {
                _accumulatedSeconds = 0d;
                return 0;
            }

            _accumulatedSeconds += unscaledDeltaSeconds;
            var ticks = (int)Math.Floor((_accumulatedSeconds + 0.000000001d) / SecondsPerTick);
            if (ticks <= 0) return 0;
            _accumulatedSeconds -= ticks * SecondsPerTick;
            if (_accumulatedSeconds < 0d && _accumulatedSeconds > -0.00000001d)
                _accumulatedSeconds = 0d;
            return checked(ticks * gameMinutesPerSecond);
        }

        public void Reset()
        {
            _accumulatedSeconds = 0d;
        }
    }
}

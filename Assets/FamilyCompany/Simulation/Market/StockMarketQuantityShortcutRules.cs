using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FamilyCompany.Simulation.Market
{
    /// <summary>Canonical SIMUL quantity shortcuts: 10 / 20 / 50 / 100%.</summary>
    public static class StockMarketQuantityShortcutRules
    {
        private static readonly IReadOnlyList<int> CanonicalPercentages =
            new ReadOnlyCollection<int>(new[] { 10, 20, 50, 100 });

        public static IReadOnlyList<int> Percentages => CanonicalPercentages;

        public static int QuantityFor(int maximumQuantity, int percentage)
        {
            var maximum = Math.Max(0, maximumQuantity);
            if (!CanonicalPercentages.Contains(percentage))
                throw new ArgumentOutOfRangeException(nameof(percentage));
            return percentage == 100
                ? maximum
                : (int)Math.Floor(maximum * percentage / 100d);
        }

        private static bool Contains(this IReadOnlyList<int> values, int candidate)
        {
            for (var index = 0; index < values.Count; index += 1)
            {
                if (values[index] == candidate) return true;
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Market;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Per-level motion the order book rows need but IMGUI cannot hold for them.
    ///
    /// The source screen keeps this inside each row widget: a depth bar that tweens to its new
    /// length, and a signed quantity badge that appears for 520ms when the resting quantity changes.
    /// IMGUI rebuilds every row from scratch each frame, so the same state is tracked here, keyed by
    /// the level the row is drawing.
    ///
    /// The badge rule is copied exactly, including the case it deliberately hides: a quote whose
    /// depth simply shrank shows nothing, because painting a negative number there would read as a
    /// trade that never happened. Only a real execution drain gets a signed label.
    /// </summary>
    public sealed class OrderBookRowMotion
    {
        /// <summary>How long a quantity change stays labelled.</summary>
        public const float DeltaSeconds = 0.52f;

        /// <summary>Depth tween for an ordinary quote update.</summary>
        public const float MotionSeconds = 0.144f;

        /// <summary>Rows are dropped after this long unseen so the ladder cannot grow forever.</summary>
        private const float StaleSeconds = 30f;

        private struct Row
        {
            public int Quantity;
            public int Delta;
            public bool DeltaIsTrade;
            public float DeltaUntil;
            public float Depth;
            public float SeenAt;
        }

        private readonly Dictionary<(MarketOrderBookSide Side, long Price), Row> _rows =
            new Dictionary<(MarketOrderBookSide, long), Row>();

        private float _lastSweep;

        /// <summary>
        /// Records the quantity a row is showing this frame and advances its depth tween.
        /// </summary>
        /// <param name="isTradeDrain">
        /// True only when the sweep is draining this exact level, which is what separates an
        /// execution from a quote that merely shrank.
        /// </param>
        /// <param name="depthTarget">Depth as a 0..1 fraction of the deepest visible level.</param>
        /// <param name="tweenSeconds">
        /// Tween length: the sweep step duration on the draining level, the ordinary motion duration
        /// elsewhere, matching how the source picks between the two.
        /// </param>
        public void Observe(
            MarketOrderBookSide side,
            long price,
            int quantity,
            bool isTradeDrain,
            float depthTarget,
            float tweenSeconds,
            float now,
            float deltaTime)
        {
            var key = (side, price);
            if (!_rows.TryGetValue(key, out var row))
            {
                // A level appearing for the first time starts at its own depth rather than growing
                // from zero, so scrolling the ladder does not animate every row.
                _rows[key] = new Row
                {
                    Quantity = quantity,
                    Depth = depthTarget,
                    SeenAt = now
                };
                return;
            }

            if (quantity != row.Quantity)
            {
                row.Delta = quantity - row.Quantity;
                row.DeltaIsTrade = row.Delta < 0 && isTradeDrain;
                row.DeltaUntil = now + DeltaSeconds;
                row.Quantity = quantity;
            }
            else if (now >= row.DeltaUntil)
            {
                row.Delta = 0;
                row.DeltaIsTrade = false;
            }

            var span = Math.Max(0.001f, tweenSeconds);
            var step = Math.Min(1f, Math.Max(0f, deltaTime) / span);
            row.Depth += (depthTarget - row.Depth) * step;
            row.SeenAt = now;
            _rows[key] = row;
        }

        public float DepthFor(MarketOrderBookSide side, long price, float fallback) =>
            _rows.TryGetValue((side, price), out var row) ? row.Depth : fallback;

        /// <summary>
        /// The label the source would paint, or empty. Zero, and any non-execution decrease, are
        /// deliberately blank.
        /// </summary>
        public string DeltaLabel(MarketOrderBookSide side, long price, float now)
        {
            if (!_rows.TryGetValue((side, price), out var row)) return string.Empty;
            if (row.Delta == 0 || now >= row.DeltaUntil) return string.Empty;
            if (row.Delta < 0 && !row.DeltaIsTrade) return string.Empty;
            return row.Delta > 0
                ? "+" + row.Delta.ToString("N0")
                : row.Delta.ToString("N0");
        }

        /// <summary>True when the visible label describes an execution rather than a new quote.</summary>
        public bool DeltaIsTrade(MarketOrderBookSide side, long price) =>
            _rows.TryGetValue((side, price), out var row) && row.DeltaIsTrade;

        public bool DeltaIsIncrease(MarketOrderBookSide side, long price) =>
            _rows.TryGetValue((side, price), out var row) && row.Delta > 0;

        /// <summary>Drops levels the ladder has scrolled away from. Cheap and only runs now and then.</summary>
        public void Sweep(float now)
        {
            if (now - _lastSweep < StaleSeconds) return;
            _lastSweep = now;
            List<(MarketOrderBookSide, long)> stale = null;
            foreach (var pair in _rows)
            {
                if (now - pair.Value.SeenAt < StaleSeconds) continue;
                stale = stale ?? new List<(MarketOrderBookSide, long)>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (var index = 0; index < stale.Count; index += 1) _rows.Remove(stale[index]);
        }

        public void Clear()
        {
            _rows.Clear();
            _lastSweep = 0f;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.Market
{
    public enum MarketOrderBookReplayPhase
    {
        Idle,
        Arriving,
        Draining,
        FinalHold,
    }

    public readonly struct MarketOrderBookSweepStep
    {
        public MarketOrderBookSweepStep(
            int marketMinute,
            int liquidityPulse,
            int sequence,
            MarketOrderBookSide side,
            long price,
            int consumedQuantity,
            int remainingQuantity,
            bool structuralBreach = false,
            bool boundaryCrossed = false)
        {
            MarketMinute = marketMinute;
            LiquidityPulse = liquidityPulse;
            Sequence = sequence;
            Side = side;
            Price = price;
            ConsumedQuantity = consumedQuantity;
            RemainingQuantity = remainingQuantity;
            StructuralBreach = structuralBreach;
            BoundaryCrossed = boundaryCrossed;
        }

        public int MarketMinute { get; }
        public int LiquidityPulse { get; }
        public int Sequence { get; }
        public MarketOrderBookSide Side { get; }
        public long Price { get; }
        public int ConsumedQuantity { get; }
        public int RemainingQuantity { get; }
        public bool StructuralBreach { get; }
        public bool BoundaryCrossed { get; }
    }

    public sealed class MarketOrderBookReplayBatch
    {
        public MarketOrderBookReplayBatch(
            string identity,
            string source,
            IEnumerable<MarketOrderBookSweepStep> steps)
        {
            Identity = identity ?? string.Empty;
            Source = source ?? string.Empty;
            Steps = new ReadOnlyCollection<MarketOrderBookSweepStep>(
                (steps ?? Array.Empty<MarketOrderBookSweepStep>())
                    .Where(step => step.ConsumedQuantity > 0)
                    .OrderBy(step => step.Sequence)
                    .ToList());
        }

        public string Identity { get; }
        public string Source { get; }
        public IReadOnlyList<MarketOrderBookSweepStep> Steps { get; }
    }

    public sealed class MarketOrderBookReplayCursor
    {
        internal MarketOrderBookReplayCursor(
            MarketOrderBookReplayBatch batch,
            int stepIndex,
            MarketOrderBookReplayPhase phase)
        {
            Batch = batch;
            StepIndex = stepIndex;
            Phase = phase;
        }

        public MarketOrderBookReplayBatch Batch { get; }
        public int StepIndex { get; }
        public MarketOrderBookReplayPhase Phase { get; }
        public MarketOrderBookSweepStep? Step =>
            StepIndex >= 0 && StepIndex < Batch.Steps.Count
                ? Batch.Steps[StepIndex]
                : (MarketOrderBookSweepStep?)null;
        public bool Arrived => Phase == MarketOrderBookReplayPhase.Draining ||
                               Phase == MarketOrderBookReplayPhase.FinalHold;
    }

    internal sealed class MarketOrderBookSweepIdentityLedger
    {
        private readonly int _completedHistoryCapacity;
        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _completedOrder = new Queue<string>();

        public MarketOrderBookSweepIdentityLedger(int completedHistoryCapacity)
        {
            if (completedHistoryCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(completedHistoryCapacity));
            _completedHistoryCapacity = completedHistoryCapacity;
        }

        public bool Admit(string identity)
        {
            if (string.IsNullOrEmpty(identity) ||
                _inFlight.Contains(identity) ||
                _completed.Contains(identity))
                return false;
            _inFlight.Add(identity);
            return true;
        }

        public void Complete(string identity)
        {
            if (!_inFlight.Remove(identity) || !_completed.Add(identity)) return;
            _completedOrder.Enqueue(identity);
            while (_completedOrder.Count > _completedHistoryCapacity)
                _completed.Remove(_completedOrder.Dequeue());
        }

        public void ClearInFlight()
        {
            _inFlight.Clear();
        }

        public void Clear()
        {
            _inFlight.Clear();
            _completed.Clear();
            _completedOrder.Clear();
        }
    }

    /// <summary>
    /// Render-frame owner for the SIMUL sweep FIFO. Each step is published once
    /// as arriving and once as draining. Pause never mutates the cursor, and a
    /// completed identity cannot reappear until it ages out of the ledger.
    /// </summary>
    public sealed class MarketOrderBookReplayQueue
    {
        public const int VisibleRowsPerSide = 7;
        public const long BaseMotionMicroseconds = 144_000;
        public const long TotalSweepMicroseconds = 480_000;
        public const long MinimumStepMicroseconds = 56_000;
        public const long MaximumStepMicroseconds = 96_000;
        public const long FinalHoldMicroseconds = 112_000;
        public const long ArrivalSettleMarginMicroseconds = 20_000;
        public const long MinimumVisibleMotionMicroseconds = 36_000;

        private readonly Queue<MarketOrderBookReplayBatch> _pending =
            new Queue<MarketOrderBookReplayBatch>();
        private readonly MarketOrderBookSweepIdentityLedger _identityLedger;
        private MarketOrderBookReplayBatch _active;
        private int _stepIndex = -1;
        private MarketOrderBookReplayPhase _phase = MarketOrderBookReplayPhase.Idle;
        private long _elapsedInPhaseMicroseconds;
        private int _playbackRate = 1;
        private bool _paused;
        private string _sessionKey;

        public MarketOrderBookReplayQueue(
            string sessionKey,
            int completedHistoryCapacity = 256)
        {
            _sessionKey = sessionKey ?? string.Empty;
            _identityLedger = new MarketOrderBookSweepIdentityLedger(completedHistoryCapacity);
        }

        public bool IsPaused => _paused;
        public int PlaybackRate => _playbackRate;
        public int PendingBatchCount => _pending.Count;
        public bool HasActiveBatch => _active != null;
        public MarketOrderBookReplayCursor Cursor =>
            _active == null
                ? null
                : new MarketOrderBookReplayCursor(_active, _stepIndex, _phase);
        public long CurrentPhaseDurationMicroseconds => PhaseDurationMicroseconds();

        public void EnsureSession(string sessionKey)
        {
            var safe = sessionKey ?? string.Empty;
            if (string.Equals(_sessionKey, safe, StringComparison.Ordinal)) return;
            _sessionKey = safe;
            Reset(true);
        }

        public bool Enqueue(MarketOrderBookReplayBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (batch.Steps.Count == 0 || !_identityLedger.Admit(batch.Identity)) return false;
            _pending.Enqueue(batch);
            if (!_paused && _active == null) BeginNext();
            return true;
        }

        public void SetPlayback(bool paused, int animationRate)
        {
            var rate = animationRate > 0 ? animationRate : _playbackRate;
            if (_paused == paused && _playbackRate == rate) return;
            _paused = paused;
            _playbackRate = Math.Max(1, rate);
            // Dart cancels and reschedules the current Timer on every speed change.
            _elapsedInPhaseMicroseconds = 0;
            if (!_paused && _active == null) BeginNext();
        }

        /// <summary>
        /// Advances at most one presentation boundary per rendered Unity frame.
        /// This preserves the source UI's mandatory arrived frame for a zero row.
        /// </summary>
        public bool TickMicroseconds(long deltaMicroseconds)
        {
            if (_paused || deltaMicroseconds <= 0) return false;
            if (_active == null)
            {
                BeginNext();
                return _active != null;
            }
            _elapsedInPhaseMicroseconds = checked(
                _elapsedInPhaseMicroseconds + deltaMicroseconds);
            var duration = PhaseDurationMicroseconds();
            if (_elapsedInPhaseMicroseconds < duration) return false;
            _elapsedInPhaseMicroseconds -= duration;
            AdvanceOneBoundary();
            return true;
        }

        public void Reset(bool clearHistory = false)
        {
            _pending.Clear();
            _active = null;
            _stepIndex = -1;
            _phase = MarketOrderBookReplayPhase.Idle;
            _elapsedInPhaseMicroseconds = 0;
            if (clearHistory) _identityLedger.Clear();
            else _identityLedger.ClearInFlight();
        }

        private void BeginNext()
        {
            if (_paused || _active != null || _pending.Count == 0) return;
            _active = _pending.Dequeue();
            _stepIndex = 0;
            _phase = MarketOrderBookReplayPhase.Arriving;
            _elapsedInPhaseMicroseconds = 0;
        }

        private void AdvanceOneBoundary()
        {
            if (_active == null) return;
            switch (_phase)
            {
                case MarketOrderBookReplayPhase.Arriving:
                    _phase = MarketOrderBookReplayPhase.Draining;
                    break;
                case MarketOrderBookReplayPhase.Draining:
                    if (_stepIndex + 1 < _active.Steps.Count)
                    {
                        _stepIndex += 1;
                        _phase = MarketOrderBookReplayPhase.Arriving;
                    }
                    else
                    {
                        _phase = MarketOrderBookReplayPhase.FinalHold;
                    }
                    break;
                case MarketOrderBookReplayPhase.FinalHold:
                    var completedIdentity = _active.Identity;
                    _active = null;
                    _stepIndex = -1;
                    _phase = MarketOrderBookReplayPhase.Idle;
                    _identityLedger.Complete(completedIdentity);
                    BeginNext();
                    break;
            }
        }

        private long PhaseDurationMicroseconds()
        {
            switch (_phase)
            {
                case MarketOrderBookReplayPhase.Arriving:
                    return Math.Max(
                               MinimumVisibleMotionMicroseconds,
                               ScaleDuration(BaseMotionMicroseconds)) +
                           ArrivalSettleMarginMicroseconds;
                case MarketOrderBookReplayPhase.Draining:
                    var stepCount = Math.Max(1, _active?.Steps.Count ?? 1);
                    var baseStepMilliseconds = Clamp(
                        RoundAwayFromZero(480d / stepCount),
                        (int)(MinimumStepMicroseconds / 1000),
                        (int)(MaximumStepMicroseconds / 1000));
                    return ScaleDuration(baseStepMilliseconds * 1000L);
                case MarketOrderBookReplayPhase.FinalHold:
                    return ScaleDuration(FinalHoldMicroseconds);
                default:
                    return 0;
            }
        }

        private long ScaleDuration(long microseconds)
        {
            return Math.Max(
                1L,
                (long)Math.Round(
                    (double)microseconds / Math.Max(1, _playbackRate),
                    MidpointRounding.AwayFromZero));
        }

        private static int RoundAwayFromZero(double value)
        {
            return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}

using System;
using System.Diagnostics;
using UnityEngine.Profiling;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficePerformancePath
    {
        RuntimeWorldUpdate = 0,
        DepthSortApply = 1,
        DepthSortResolve = 2,
        NavigationFindPath = 3,
        AutonomyRefresh = 4,
        SimulationAdvance = 5,
        AnimatorTick = 6,
        ChairPresentationAlign = 7,
        HudRefresh = 8,
        NavigationReachability = 9,
        InteractionOfferResolve = 10,
        StaminaDecision = 11,
        StaminaCapabilityQuery = 12
    }

    public readonly struct OfficePerformanceMetric
    {
        public OfficePerformanceMetric(
            OfficePerformancePath path,
            long calls,
            long elapsedTicks,
            long maximumTicks,
            long allocatedBytes,
            long maximumAllocatedBytes)
        {
            Path = path;
            Calls = calls;
            ElapsedTicks = elapsedTicks;
            MaximumTicks = maximumTicks;
            AllocatedBytes = allocatedBytes;
            MaximumAllocatedBytes = maximumAllocatedBytes;
        }

        public OfficePerformancePath Path { get; }
        public long Calls { get; }
        public long ElapsedTicks { get; }
        public long MaximumTicks { get; }
        public long AllocatedBytes { get; }
        public long MaximumAllocatedBytes { get; }
        public double TotalMilliseconds => ElapsedTicks * 1000d / Stopwatch.Frequency;
        public double MaximumMilliseconds => MaximumTicks * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// Opt-in, allocation-aware counters for the unattended Windows performance QA. Ordinary
    /// gameplay pays one predictable branch per measured boundary and retains no samples.
    /// </summary>
    public static class OfficePerformanceTelemetry
    {
        private static readonly int PathCount = Enum.GetValues(typeof(OfficePerformancePath)).Length;
        private static readonly long[] Calls = new long[PathCount];
        private static readonly long[] ElapsedTicks = new long[PathCount];
        private static readonly long[] MaximumTicks = new long[PathCount];
        private static readonly long[] AllocatedBytes = new long[PathCount];
        private static readonly long[] MaximumAllocatedBytes = new long[PathCount];

        public static bool Enabled { get; private set; }

        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            Reset();
        }

        public static void Reset()
        {
            Array.Clear(Calls, 0, Calls.Length);
            Array.Clear(ElapsedTicks, 0, ElapsedTicks.Length);
            Array.Clear(MaximumTicks, 0, MaximumTicks.Length);
            Array.Clear(AllocatedBytes, 0, AllocatedBytes.Length);
            Array.Clear(MaximumAllocatedBytes, 0, MaximumAllocatedBytes.Length);
        }

        public static Measurement Measure(OfficePerformancePath path) =>
            Enabled ? new Measurement(path) : default;

        public static OfficePerformanceMetric[] Snapshot()
        {
            var result = new OfficePerformanceMetric[PathCount];
            for (var index = 0; index < PathCount; index++)
            {
                result[index] = new OfficePerformanceMetric(
                    (OfficePerformancePath)index,
                    Calls[index],
                    ElapsedTicks[index],
                    MaximumTicks[index],
                    AllocatedBytes[index],
                    MaximumAllocatedBytes[index]);
            }
            return result;
        }

        public readonly struct Measurement : IDisposable
        {
            private readonly int _path;
            private readonly long _startedAt;
            private readonly long _allocatedAtStart;
            private readonly long _monoUsedAtStart;
            private readonly bool _active;

            internal Measurement(OfficePerformancePath path)
            {
                _path = (int)path;
                _allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
                _monoUsedAtStart = TracksMonoAllocation(path)
                    ? Profiler.GetMonoUsedSizeLong()
                    : -1L;
                _startedAt = Stopwatch.GetTimestamp();
                _active = true;
            }

            public void Dispose()
            {
                if (!_active) return;
                long elapsed = Stopwatch.GetTimestamp() - _startedAt;
                long threadAllocated = Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - _allocatedAtStart);
                long monoAllocated = _monoUsedAtStart < 0L
                    ? 0L
                    : Math.Max(0L, Profiler.GetMonoUsedSizeLong() - _monoUsedAtStart);
                long allocated = Math.Max(threadAllocated, monoAllocated);
                Calls[_path]++;
                ElapsedTicks[_path] += elapsed;
                AllocatedBytes[_path] += allocated;
                if (elapsed > MaximumTicks[_path]) MaximumTicks[_path] = elapsed;
                if (allocated > MaximumAllocatedBytes[_path])
                    MaximumAllocatedBytes[_path] = allocated;
            }

            private static bool TracksMonoAllocation(OfficePerformancePath path)
            {
                return path != OfficePerformancePath.AnimatorTick &&
                       path != OfficePerformancePath.ChairPresentationAlign;
            }
        }
    }
}

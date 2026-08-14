using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Deterministic, capture-free player scenario for CPU frame time, GC allocation and the
    /// 09:00 attendance / seating / repeated-schedule boundaries. Enabled only by command line.
    /// </summary>
    public sealed class OfficeRuntimePerformanceProbe : MonoBehaviour
    {
        public const string CommandLineArgument = "-familyCompanyPerformanceQa";
        public const string UncachedNavigationArgument = "-familyCompanyUncachedNavigationQa";
        public const string FourTimesArgument = "-familyCompanyPerformance4xQa";
        private const int CaptureFrameRate = 60;
        private const int MaximumSamples = 12000;
        private static bool _installed;
        public static bool IsDrivingClock { get; private set; }
        public static bool UseUncachedNavigation { get; private set; }
        public static int ClockMultiplier { get; private set; } = 1;

        private readonly long[] _mainThreadSamples = new long[MaximumSamples];
        private readonly long[] _gcSamples = new long[MaximumSamples];
        private readonly long[] _gcCollectionSamples = new long[MaximumSamples];
        private readonly long[] _threadAllocationSamples = new long[MaximumSamples];
        private readonly long[] _monoGrowthSamples = new long[MaximumSamples];
        private readonly long[] _wallFrameSamples = new long[MaximumSamples];
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _gcRecorder;
        private bool _recording;
        private int _sampleCount;
        private long _previousFrameTimestamp;
        private int _previousGcCollectionCount;
        private long _previousThreadAllocated;
        private long _previousMonoUsed;
        private bool _failed;
        private int _windowStartSample;
        private OfficePerformanceMetric[] _windowMetricStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _installed = false;
            IsDrivingClock = false;
            UseUncachedNavigation = false;
            ClockMultiplier = 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_installed ||
                Array.IndexOf(Environment.GetCommandLineArgs(), CommandLineArgument) < 0) return;
            _installed = true;
            var host = new GameObject("~OfficeRuntimePerformanceProbe");
            DontDestroyOnLoad(host);
            host.AddComponent<OfficeRuntimePerformanceProbe>();
        }

        private void Awake()
        {
            IsDrivingClock = true;
            UseUncachedNavigation = Array.IndexOf(
                Environment.GetCommandLineArgs(),
                UncachedNavigationArgument) >= 0;
            ClockMultiplier = Array.IndexOf(
                Environment.GetCommandLineArgs(),
                FourTimesArgument) >= 0 ? 4 : 1;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Time.captureFramerate = CaptureFrameRate;
        }

        private IEnumerator Start()
        {
            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Fail("PrototypeBootstrap missing");
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            StarterOfficeRuntimeBootstrap runtime = null;
            for (var frame = 0; frame < 1200; frame++)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (!ScenePreviewJump.IsPresentationLoading && runtime != null && runtime.IsReady &&
                    runtime.World != null && runtime.Actors.Count == 4) break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null || runtime.Actors.Count != 4)
            {
                Fail("Starter runtime activation timeout");
                yield break;
            }

            for (var frame = 0; frame < 120; frame++) yield return null;
            if (ClockMultiplier == 4) bootstrap.SetWorldTimeScaleNow(4f);
            LogFirstNavigationCalls(runtime);
            BeginRecording();

            yield return RunMinutes(bootstrap, 30, "day1 attendance");
            if (_failed) yield break;
            if (!ValidateRuntime(runtime, "day1")) yield break;
            LogWindow("day1-normal-" + ClockMultiplier + "x", runtime);
            MarkWindowStart();

            AdvanceToClock(bootstrap, bootstrap.State.Time.Now.Date.AddHours(17).AddMinutes(50));
            yield return null;
            LogWindow("jump-to-17:50", runtime);
            MarkWindowStart();
            yield return null;
            MarkWindowStart();
            yield return RunMinutes(bootstrap, 20, "departure");
            if (_failed) yield break;
            if (!ValidateRuntime(runtime, "departure")) yield break;
            LogWindow("departure-normal-" + ClockMultiplier + "x", runtime);
            MarkWindowStart();

            AdvanceToClock(bootstrap, bootstrap.State.Time.Now.Date.AddDays(1).AddHours(8).AddMinutes(50));
            yield return null;
            LogWindow("jump-to-next-day-08:50", runtime);
            MarkWindowStart();
            yield return null;
            MarkWindowStart();
            yield return RunMinutes(bootstrap, 30, "day2 repeated attendance");
            if (_failed) yield break;
            if (!ValidateRuntime(runtime, "day2")) yield break;
            LogWindow("day2-normal-" + ClockMultiplier + "x", runtime);

            EndRecording();
            LogResult("overall", runtime, _sampleCount > 1 ? 1 : 0, null, false);
            Time.captureFramerate = 0;
            Application.Quit(0);
        }

        private void LateUpdate()
        {
            if (!_recording || _sampleCount >= MaximumSamples) return;
            long now = Stopwatch.GetTimestamp();
            _wallFrameSamples[_sampleCount] = _previousFrameTimestamp == 0L
                ? 0L
                : now - _previousFrameTimestamp;
            _previousFrameTimestamp = now;
            _mainThreadSamples[_sampleCount] = _mainThreadRecorder.Valid
                ? _mainThreadRecorder.LastValue
                : 0L;
            _gcSamples[_sampleCount] = _gcRecorder.Valid ? _gcRecorder.LastValue : 0L;
            int gcCollectionCount = TotalGcCollectionCount();
            _gcCollectionSamples[_sampleCount] = Math.Max(
                0,
                gcCollectionCount - _previousGcCollectionCount);
            _previousGcCollectionCount = gcCollectionCount;
            long threadAllocated = GC.GetAllocatedBytesForCurrentThread();
            _threadAllocationSamples[_sampleCount] = Math.Max(
                0L,
                threadAllocated - _previousThreadAllocated);
            _previousThreadAllocated = threadAllocated;
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            _monoGrowthSamples[_sampleCount] = Math.Max(0L, monoUsed - _previousMonoUsed);
            _previousMonoUsed = monoUsed;
            _sampleCount++;
        }

        private IEnumerator RunMinutes(
            PrototypeBootstrap bootstrap,
            int minutes,
            string stage)
        {
            if (minutes < 0)
            {
                Fail(stage + " requested negative minutes");
                yield break;
            }
            for (var minute = 0; minute < minutes; minute++)
            {
                bootstrap.AdvanceTimeNow(1L);
                for (var frame = 0;
                     frame < CaptureFrameRate / ClockMultiplier;
                     frame++) yield return null;
            }
        }

        private static void LogFirstNavigationCalls(StarterOfficeRuntimeBootstrap runtime)
        {
            OfficeRuntimePathService paths = runtime.World.Paths;
            long floodBefore = paths.ReachabilityFloodCount;
            long hitBefore = paths.ReachabilityCacheHitCount;
            long nodesBefore = paths.ReachabilityVisitedNodeCount;
            long graphBuildBefore = paths.StaticGraphBuildCount;
            long started = Stopwatch.GetTimestamp();
            HashSet<OfficeGridCoordinate> first = paths.FindStaticallyReachableCells(
                "performance-probe",
                OfficeRuntimeWorkstationService.StarterEntranceCell);
            double firstMilliseconds =
                (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            started = Stopwatch.GetTimestamp();
            HashSet<OfficeGridCoordinate> warm = paths.FindStaticallyReachableCells(
                "performance-probe",
                OfficeRuntimeWorkstationService.StarterEntranceCell);
            double warmMilliseconds =
                (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            UnityEngine.Debug.Log(
                "FAMILY_COMPANY_NAVIGATION_FIRST_CALL: PASS | " +
                "timeScale=" + ClockMultiplier + "x" +
                " first=" + firstMilliseconds.ToString("F3") + "ms" +
                " warm=" + warmMilliseconds.ToString("F3") + "ms" +
                " reachable=" + first.Count +
                " sameComponent=" + ReferenceEquals(first, warm) +
                " floodDelta=" + (paths.ReachabilityFloodCount - floodBefore) +
                " hitDelta=" + (paths.ReachabilityCacheHitCount - hitBefore) +
                " visitedDelta=" + (paths.ReachabilityVisitedNodeCount - nodesBefore) +
                " graphBuildDelta=" + (paths.StaticGraphBuildCount - graphBuildBefore));
        }

        private static void AdvanceToClock(PrototypeBootstrap bootstrap, DateTime target)
        {
            long minutes = checked((long)Math.Round(
                (target - bootstrap.State.Time.Now).TotalMinutes,
                MidpointRounding.AwayFromZero));
            if (minutes > 0L) bootstrap.AdvanceTimeNow(minutes);
        }

        private bool ValidateRuntime(StarterOfficeRuntimeBootstrap runtime, string stage)
        {
            if (runtime != null && runtime.IsReady && runtime.Actors.Count == 4) return true;
            Fail(stage + " runtime invariant failed");
            return false;
        }

        private void BeginRecording()
        {
            OfficePerformanceTelemetry.SetEnabled(true);
            _sampleCount = 0;
            _previousFrameTimestamp = 0L;
            _previousGcCollectionCount = TotalGcCollectionCount();
            _previousThreadAllocated = GC.GetAllocatedBytesForCurrentThread();
            _previousMonoUsed = Profiler.GetMonoUsedSizeLong();
            _mainThreadRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "Main Thread",
                1);
            _gcRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                1);
            StarterOfficeRuntimeBootstrap runtime =
                Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            runtime?.World?.Paths.ResetPerformanceCounters();
            _recording = true;
            MarkWindowStart();
        }

        private void EndRecording()
        {
            _recording = false;
            _mainThreadRecorder.Dispose();
            _gcRecorder.Dispose();
        }

        private void MarkWindowStart()
        {
            _windowStartSample = _sampleCount;
            _windowMetricStart = OfficePerformanceTelemetry.Snapshot();
        }

        private void LogWindow(string stage, StarterOfficeRuntimeBootstrap runtime)
        {
            LogResult(stage, runtime, _windowStartSample, _windowMetricStart, true);
        }

        private void LogResult(
            string stage,
            StarterOfficeRuntimeBootstrap runtime,
            int start,
            OfficePerformanceMetric[] metricStart,
            bool stageResult)
        {
            string frameSummary =
                "stage=" + stage +
                " navMode=" + (UseUncachedNavigation ? "uncached" : "cached") +
                " timeScale=" + ClockMultiplier + "x" +
                " samples=" + Math.Max(0, _sampleCount - start) +
                " mainP50=" + PercentileMilliseconds(_mainThreadSamples, start, 0.50d).ToString("F3") +
                "ms mainP95=" + PercentileMilliseconds(_mainThreadSamples, start, 0.95d).ToString("F3") +
                "ms mainP99=" + PercentileMilliseconds(_mainThreadSamples, start, 0.99d).ToString("F3") +
                "ms mainMax=" + MaximumMilliseconds(_mainThreadSamples, start, true).ToString("F3") +
                "ms wallP99=" + PercentileMilliseconds(_wallFrameSamples, start, 0.99d, false).ToString("F3") +
                "ms wallMax=" + MaximumMilliseconds(_wallFrameSamples, start, false).ToString("F3") +
                "ms gcTotal=" + Sum(_gcSamples, start) +
                "B gcMaxFrame=" + Maximum(_gcSamples, start) +
                "B gcCollections=" + Sum(_gcCollectionSamples, start) +
                " gcCollectionsMaxFrame=" + Maximum(_gcCollectionSamples, start) +
                " threadAllocTotal=" + Sum(_threadAllocationSamples, start) +
                "B threadAllocMaxFrame=" + Maximum(_threadAllocationSamples, start) +
                "B monoGrowthTotal=" + Sum(_monoGrowthSamples, start) +
                "B monoGrowthMaxFrame=" + Maximum(_monoGrowthSamples, start) + "B";
            OfficePerformanceMetric[] current = OfficePerformanceTelemetry.Snapshot();
            IEnumerable<string> paths = current.Select((metric, index) =>
            {
                OfficePerformanceMetric baseline = metricStart != null && index < metricStart.Length
                    ? metricStart[index]
                    : default;
                long calls = metric.Calls - baseline.Calls;
                double totalMilliseconds =
                    (metric.ElapsedTicks - baseline.ElapsedTicks) * 1000d / Stopwatch.Frequency;
                long allocated = metric.AllocatedBytes - baseline.AllocatedBytes;
                return metric.Path + ":calls=" + calls +
                       ",total=" + totalMilliseconds.ToString("F3") +
                       "ms,max=" + metric.MaximumMilliseconds.ToString("F3") +
                       "ms,alloc=" + allocated +
                       "B,maxAlloc=" + metric.MaximumAllocatedBytes + "B";
            });
            OfficeRuntimePathService pathService = runtime.World.Paths;
            UnityEngine.Debug.Log(
                (stageResult
                    ? "FAMILY_COMPANY_PERFORMANCE_STAGE: PASS | "
                    : "FAMILY_COMPANY_PERFORMANCE_QA: PASS | ") + frameSummary +
                " | replans=" + runtime.World.ReplanCount +
                " arrivals=" + runtime.World.ArrivalCount +
                " pathCalls=" + pathService.PathSearchCount +
                " pathVisited=" + pathService.PathVisitedNodeCount +
                " reachabilityCalls=" + pathService.ReachabilityCallCount +
                " reachabilityFloods=" + pathService.ReachabilityFloodCount +
                " reachabilityMisses=" + pathService.ReachabilityCacheMissCount +
                " reachabilityHits=" + pathService.ReachabilityCacheHitCount +
                " reachabilityVisited=" + pathService.ReachabilityVisitedNodeCount +
                " graphBuilds=" + pathService.StaticGraphBuildCount +
                " graphMisses=" + pathService.StaticGraphCacheMissCount +
                " graphHits=" + pathService.StaticGraphCacheHitCount +
                " graphNodes=" + pathService.StaticGraphNodeCheckCount +
                " graphEdges=" + pathService.StaticGraphEdgeCheckCount +
                " | " + string.Join(" | ", paths));
        }

        private static double PercentileMilliseconds(
            long[] source,
            int start,
            double percentile,
            bool nanoseconds = true)
        {
            int count = source.Length - start;
            while (count > 0 && source[start + count - 1] == 0L) count--;
            if (count <= 0) return 0d;
            var values = new long[count];
            Array.Copy(source, start, values, 0, count);
            Array.Sort(values);
            int index = Math.Min(count - 1, Math.Max(0, (int)Math.Ceiling(percentile * count) - 1));
            return nanoseconds
                ? values[index] / 1000000d
                : values[index] * 1000d / Stopwatch.Frequency;
        }

        private static double MaximumMilliseconds(
            long[] source,
            int start,
            bool nanoseconds)
        {
            long maximum = Maximum(source, start);
            return nanoseconds
                ? maximum / 1000000d
                : maximum * 1000d / Stopwatch.Frequency;
        }

        private static long Maximum(long[] source, int start)
        {
            long maximum = 0L;
            for (var index = start; index < source.Length; index++)
                if (source[index] > maximum) maximum = source[index];
            return maximum;
        }

        private static long Sum(long[] source, int start)
        {
            long sum = 0L;
            for (var index = start; index < source.Length; index++) sum += source[index];
            return sum;
        }

        private static int TotalGcCollectionCount() =>
            checked(GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2));

        private void Fail(string message)
        {
            if (_failed) return;
            _failed = true;
            if (_recording) EndRecording();
            Time.captureFramerate = 0;
            UnityEngine.Debug.LogError("FAMILY_COMPANY_PERFORMANCE_QA: FAIL | " + message);
            Application.Quit(71);
        }

        private void OnDestroy()
        {
            if (_recording) EndRecording();
            OfficePerformanceTelemetry.SetEnabled(false);
            IsDrivingClock = false;
            UseUncachedNavigation = false;
            ClockMultiplier = 1;
            Time.captureFramerate = 0;
        }
    }
}

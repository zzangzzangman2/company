using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Atomic R5e observer/visual driver. It never reads the legacy 4/6/4 seating clips. The
    /// performance flags only observe the normal game clock; the visual flag exclusively owns its
    /// deterministic QA-controlled cycle. All trace serialization starts after measurement closes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeSeatDockingR5ePlayerQa : MonoBehaviour
    {
        private const int MaximumMeasuredFrames = 7200;
        private const float StartupWatchdogSeconds = 20f;
        private const float PhaseWatchdogSeconds = 20f;
        private static OfficeSeatDockingR5ePlayerQa _instance;

        private readonly FrameSample[] _frameSamples = new FrameSample[MaximumMeasuredFrames];
        private readonly BoundarySample[] _boundaries = new BoundarySample[8];
        private StarterOfficeRuntimeBootstrap _runtime;
        private SpriteRenderer[] _bodyRenderers = Array.Empty<SpriteRenderer>();
        private int _frameCount;
        private int _boundaryCount;
        private int _frameOverflowCount;
        private int _forbiddenColliderCount;
        private int _forbiddenCollider2DCount;
        private int _forbiddenRigidbodyCount;
        private int _forbiddenRigidbody2DCount;
        private int _forbiddenNavMeshAgentCount;
        private int _initialBodyRendererCount;
        private string _artifactDirectory = string.Empty;
        private string _catalogSha256 = string.Empty;
        private string _failure = string.Empty;
        private bool _visualOwner;
        private bool _fourTimes;
        private bool _measurementOpen;
        private bool _flushed;
        private long _previousAllocatedBytes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            bool observer = arguments.Contains(
                OfficeSeatDockingR5eRuntimeQaContract.ObserverFlag,
                StringComparer.Ordinal);
            bool visual = arguments.Contains(
                OfficeSeatDockingR5eRuntimeQaContract.VisualRunnerFlag,
                StringComparer.Ordinal);
            if (_instance != null || (!observer && !visual)) return;
            var host = new GameObject("~OfficeSeatDockingR5ePlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeSeatDockingR5ePlayerQa>();
        }

        private void Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            _visualOwner = arguments.Contains(
                OfficeSeatDockingR5eRuntimeQaContract.VisualRunnerFlag,
                StringComparer.Ordinal);
            _fourTimes = arguments.Contains(
                OfficeSeatDockingR5eRuntimeQaContract.FourTimesFlag,
                StringComparer.Ordinal);
            _artifactDirectory = ResolveArgument(
                arguments,
                OfficeSeatDockingR5eRuntimeQaContract.ArtifactDirectoryArgument,
                Path.Combine(Application.persistentDataPath, "ChairR5eQa"));
            RecordBoundary("ProcessStart");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            RecordBoundary("SessionStart");
            float deadline = Time.realtimeSinceStartup + StartupWatchdogSeconds;
            while ((_runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>()) == null ||
                   !_runtime.IsReady)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Fail("runtime-ready watchdog exceeded");
                    yield break;
                }
                yield return null;
            }
            RecordBoundary("RuntimeReady");

            TextAsset catalog = Resources.Load<TextAsset>(
                OfficeSeatDockingR5eRuntimeQaContract.ScenarioCatalogResource);
            if (catalog == null)
            {
                Fail("R5e scenario catalog was not preloaded");
                yield break;
            }
            _catalogSha256 = Sha256(Encoding.UTF8.GetBytes(catalog.text));
            CacheRuntimeBaselines();
            RecordBoundary("PreloadComplete");

            if (_visualOwner)
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                Time.timeScale = _fourTimes ? 4f : 1f;
            }
            yield return null;
            _previousAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            _measurementOpen = true;
            RecordBoundary("GameplayMeasureBegin");

            if (!_visualOwner) yield break;
            yield return RunVisualCycle();
            _measurementOpen = false;
            RecordBoundary("GameplayMeasureEnd");
            FlushPostWindow();
            Application.Quit(_failure.Length == 0 ? 0 : 96);
        }

        private IEnumerator RunVisualCycle()
        {
            IReadOnlyList<OfficeRuntimeAgent> actors = _runtime.Actors;
            if (actors.Count != 4)
            {
                Fail("canonical actor count " + actors.Count + " != 4");
                yield break;
            }
            foreach (OfficeRuntimeAgent actor in actors)
            {
                actor.BeginQaControl();
                if (!actor.QaBeginSeatedWork("chair-r5e-visual-entry"))
                {
                    Fail("entry request rejected for " + actor.AgentId);
                    yield break;
                }
            }
            yield return WaitFor(() => actors.All(actor => actor.IsSeated), "AtomicSeat");
            if (_failure.Length != 0) yield break;
            for (var frame = 0; frame < 30; frame++) yield return null;

            foreach (OfficeRuntimeAgent actor in actors)
            {
                if (!actor.QaRequestStand())
                {
                    Fail("exit request rejected for " + actor.AgentId);
                    yield break;
                }
            }
            yield return WaitFor(
                () => actors.All(actor => !actor.IsOccupyingSeat &&
                                         actor.Phase != OfficeRuntimeAgentPhase.FinishingWork &&
                                         actor.Phase != OfficeRuntimeAgentPhase.LeavingSeat),
                "AtomicExitFirstWalk");
            if (_failure.Length != 0) yield break;
            for (var frame = 0; frame < 30; frame++) yield return null;
        }

        private IEnumerator WaitFor(Func<bool> predicate, string phase)
        {
            float deadline = Time.realtimeSinceStartup + PhaseWatchdogSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Fail(phase + " watchdog exceeded");
                    yield break;
                }
                yield return null;
            }
        }

        private void Update()
        {
            if (!_measurementOpen) return;
            if (_frameCount >= _frameSamples.Length)
            {
                _frameOverflowCount++;
                _measurementOpen = false;
                _failure = "performance frame buffer overflow";
                return;
            }

            int activeBodyCount = 0;
            for (var index = 0; index < _bodyRenderers.Length; index++)
            {
                SpriteRenderer renderer = _bodyRenderers[index];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    renderer.sprite != null) activeBodyCount++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long frameAllocated = allocated - _previousAllocatedBytes;
            _previousAllocatedBytes = allocated;
            _frameSamples[_frameCount++] = new FrameSample(
                Time.frameCount,
                Time.realtimeSinceStartupAsDouble,
                Time.unscaledDeltaTime * 1000f,
                frameAllocated,
                Profiler.GetMonoUsedSizeLong(),
                activeBodyCount,
                _forbiddenColliderCount,
                _forbiddenCollider2DCount,
                _forbiddenRigidbodyCount,
                _forbiddenRigidbody2DCount,
                _forbiddenNavMeshAgentCount);
        }

        private void CacheRuntimeBaselines()
        {
            var renderers = new List<SpriteRenderer>(_runtime.Actors.Count);
            foreach (OfficeRuntimeAgent actor in _runtime.Actors)
            {
                if (actor.PresentationRenderer != null) renderers.Add(actor.PresentationRenderer);
                _forbiddenColliderCount += actor.GetComponentsInChildren<Collider>(true).Length;
                _forbiddenCollider2DCount += actor.GetComponentsInChildren<Collider2D>(true).Length;
                _forbiddenRigidbodyCount += actor.GetComponentsInChildren<Rigidbody>(true).Length;
                _forbiddenRigidbody2DCount += actor.GetComponentsInChildren<Rigidbody2D>(true).Length;
                _forbiddenNavMeshAgentCount +=
                    actor.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length;
            }
            _bodyRenderers = renderers.ToArray();
            _initialBodyRendererCount = _bodyRenderers.Length;
        }

        private void OnApplicationQuit()
        {
            _measurementOpen = false;
            if (_runtime != null) RecordBoundary("GameplayMeasureEnd");
            FlushPostWindow();
        }

        private void FlushPostWindow()
        {
            if (_flushed) return;
            _flushed = true;
            try
            {
                Directory.CreateDirectory(_artifactDirectory);
                OfficeSeatDockingR5eTraceWriteSummary traces = _runtime == null
                    ? default
                    : OfficeSeatDockingR5eTraceWriter.Write(
                        _runtime.World.R5eTraceCoordinator,
                        _artifactDirectory);
                WriteBoundaries();
                WritePerformanceFrames();
                int over50 = 0;
                int activeBodyMismatch = 0;
                float maxFrameMs = 0f;
                for (var index = 0; index < _frameCount; index++)
                {
                    maxFrameMs = Mathf.Max(maxFrameMs, _frameSamples[index].FrameMs);
                    if (_frameSamples[index].FrameMs >= 50f) over50++;
                    if (_frameSamples[index].ActiveBodySprites != 4) activeBodyMismatch++;
                }
                bool passed = _failure.Length == 0 && traces.Passed && _frameOverflowCount == 0 &&
                              _frameCount > 0 && over50 == 0 &&
                              _initialBodyRendererCount == 4 && activeBodyMismatch == 0;
                string result =
                    "status=" + (passed ? "PASS" : "FAIL") + Environment.NewLine +
                    "mode=" + (_visualOwner ? "visual-owner" : "observer") + Environment.NewLine +
                    "timeScale=" + (_fourTimes ? "4" : "1") + Environment.NewLine +
                    "scenarioCatalogSha256=" + _catalogSha256 + Environment.NewLine +
                    "frameCount=" + _frameCount + Environment.NewLine +
                    "frameOver50MsCount=" + over50 + Environment.NewLine +
                    "maximumFrameMs=" + maxFrameMs.ToString("R", CultureInfo.InvariantCulture) + Environment.NewLine +
                    "activeBodyRendererBaseline=" + _initialBodyRendererCount + Environment.NewLine +
                    "activeBodyRendererMismatchFrameCount=" + activeBodyMismatch + Environment.NewLine +
                    "forbiddenColliderCount=" + _forbiddenColliderCount + Environment.NewLine +
                    "forbiddenCollider2DCount=" + _forbiddenCollider2DCount + Environment.NewLine +
                    "forbiddenRigidbodyCount=" + _forbiddenRigidbodyCount + Environment.NewLine +
                    "forbiddenRigidbody2DCount=" + _forbiddenRigidbody2DCount + Environment.NewLine +
                    "forbiddenNavMeshAgentCount=" + _forbiddenNavMeshAgentCount + Environment.NewLine +
                    "transitionRows=" + traces.TransitionRows + Environment.NewLine +
                    "seatedRows=" + traces.SeatedRows + Environment.NewLine +
                    "locomotionRows=" + traces.LocomotionRows + Environment.NewLine +
                    "traceOverflowCount=" + traces.OverflowCount + Environment.NewLine +
                    "traceDroppedRowCount=" + traces.DroppedRowCount + Environment.NewLine +
                    "traceProducerFailureCount=" + traces.ProducerFailureCount + Environment.NewLine +
                    "legacyClipOracle=unused" + Environment.NewLine +
                    "failure=" + _failure + Environment.NewLine;
                File.WriteAllText(
                    Path.Combine(_artifactDirectory, OfficeSeatDockingR5eRuntimeQaContract.RuntimeResultFile),
                    result,
                    new UTF8Encoding(false));
                WriteManifest();
                if (passed)
                {
                    File.WriteAllText(
                        Path.Combine(_artifactDirectory, OfficeSeatDockingR5eRuntimeQaContract.CompletionMarker),
                        "complete=true" + Environment.NewLine +
                        "schemaVersion=" + OfficeSeatDockingTraceSchemas.SchemaVersion + Environment.NewLine,
                        new UTF8Encoding(false));
                }
                else Debug.LogError("FAMILY_COMPANY_CHAIR_R5E_RUNTIME: FAIL | " + _failure);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void WriteBoundaries()
        {
            string path = Path.Combine(
                _artifactDirectory,
                OfficeSeatDockingR5eRuntimeQaContract.StartupBoundaryFile);
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("event,frame,realtime_seconds");
            for (var index = 0; index < _boundaryCount; index++)
            {
                BoundarySample sample = _boundaries[index];
                writer.WriteLine(sample.Event + "," + sample.Frame + "," +
                                 sample.RealtimeSeconds.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private void WritePerformanceFrames()
        {
            string path = Path.Combine(
                _artifactDirectory,
                OfficeSeatDockingR5eRuntimeQaContract.PerformanceFrameFile);
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(
                "frame,realtime_seconds,frame_ms,gc_alloc_bytes,mono_used_bytes,active_body_sprites," +
                "actor_collider_count,actor_collider2d_count,actor_rigidbody_count," +
                "actor_rigidbody2d_count,actor_navmeshagent_count");
            for (var index = 0; index < _frameCount; index++)
                writer.WriteLine(_frameSamples[index].ToCsv());
        }

        private void WriteManifest()
        {
            string manifest = Path.Combine(
                _artifactDirectory,
                OfficeSeatDockingR5eRuntimeQaContract.RuntimeManifestFile);
            string[] files = Directory.GetFiles(_artifactDirectory)
                .Where(path => !string.Equals(path, manifest, StringComparison.OrdinalIgnoreCase) &&
                               !path.EndsWith(OfficeSeatDockingR5eRuntimeQaContract.CompletionMarker,
                                   StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            using var writer = new StreamWriter(manifest, false, new UTF8Encoding(false));
            writer.WriteLine("file\tlength\tsha256");
            foreach (string file in files)
            {
                var info = new FileInfo(file);
                writer.WriteLine(info.Name + "\t" + info.Length + "\t" + Sha256(File.ReadAllBytes(file)));
            }
        }

        private void RecordBoundary(string name)
        {
            if (_boundaryCount >= _boundaries.Length)
            {
                _failure = "startup boundary buffer overflow";
                return;
            }
            _boundaries[_boundaryCount++] = new BoundarySample(
                name,
                Time.frameCount,
                Time.realtimeSinceStartupAsDouble);
        }

        private void Fail(string reason)
        {
            _failure = reason;
            _measurementOpen = false;
            RecordBoundary("GameplayMeasureEnd");
            FlushPostWindow();
            if (_visualOwner) Application.Quit(96);
        }

        private static string ResolveArgument(string[] arguments, string key, string fallback)
        {
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], key, StringComparison.Ordinal))
                    return Path.GetFullPath(arguments[index + 1]);
            return Path.GetFullPath(fallback);
        }

        private static string Sha256(byte[] bytes)
        {
            using SHA256 hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private readonly struct BoundarySample
        {
            public BoundarySample(string @event, int frame, double realtimeSeconds)
            {
                Event = @event;
                Frame = frame;
                RealtimeSeconds = realtimeSeconds;
            }

            public string Event { get; }
            public int Frame { get; }
            public double RealtimeSeconds { get; }
        }

        private readonly struct FrameSample
        {
            public FrameSample(
                int frame,
                double realtimeSeconds,
                float frameMs,
                long gcAllocBytes,
                long monoUsedBytes,
                int activeBodySprites,
                int colliders,
                int colliders2D,
                int rigidbodies,
                int rigidbodies2D,
                int navMeshAgents)
            {
                Frame = frame;
                RealtimeSeconds = realtimeSeconds;
                FrameMs = frameMs;
                GcAllocBytes = gcAllocBytes;
                MonoUsedBytes = monoUsedBytes;
                ActiveBodySprites = activeBodySprites;
                Colliders = colliders;
                Colliders2D = colliders2D;
                Rigidbodies = rigidbodies;
                Rigidbodies2D = rigidbodies2D;
                NavMeshAgents = navMeshAgents;
            }

            public int Frame { get; }
            public double RealtimeSeconds { get; }
            public float FrameMs { get; }
            public long GcAllocBytes { get; }
            public long MonoUsedBytes { get; }
            public int ActiveBodySprites { get; }
            public int Colliders { get; }
            public int Colliders2D { get; }
            public int Rigidbodies { get; }
            public int Rigidbodies2D { get; }
            public int NavMeshAgents { get; }

            public string ToCsv() =>
                Frame + "," + RealtimeSeconds.ToString("R", CultureInfo.InvariantCulture) + "," +
                FrameMs.ToString("R", CultureInfo.InvariantCulture) + "," + GcAllocBytes + "," +
                MonoUsedBytes + "," + ActiveBodySprites + "," + Colliders + "," + Colliders2D + "," +
                Rigidbodies + "," + Rigidbodies2D + "," + NavMeshAgents;
        }
    }
}

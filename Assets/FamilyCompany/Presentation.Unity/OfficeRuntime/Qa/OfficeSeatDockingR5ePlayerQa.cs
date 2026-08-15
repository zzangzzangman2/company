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
using FamilyCompany.Simulation.OfficeLayout;
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
        private ulong _furnitureTransformBaselineHash;
        private int _furnitureTransformBaselineCount;
        private bool _furnitureTransformBaselineValid;
        private string _artifactDirectory = string.Empty;
        private string _catalogSha256 = string.Empty;
        private string _failure = string.Empty;
        private bool _visualOwner;
        private bool _fourTimes;
        private bool _measurementOpen;
        private bool _flushed;
        private long _previousAllocatedBytes;
        private OfficeGrid _approvedBaseGrid;
        private OfficeSeatDockingR5eScenarioPlan _scenarioPlan;
        private OfficeRuntimeTraceArchive _traceArchive;
        private ScenarioResult[] _scenarioResults = Array.Empty<ScenarioResult>();
        private int _scenarioResultCount;
        private string _scenarioCatalogPath = string.Empty;

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
            _scenarioCatalogPath = ResolveOptionalArgument(
                arguments,
                OfficeSeatDockingR5eRuntimeQaContract.ScenarioCatalogArgument);
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

            TextAsset catalog = _scenarioCatalogPath.Length == 0
                ? Resources.Load<TextAsset>(OfficeSeatDockingR5eRuntimeQaContract.ScenarioCatalogResource)
                : new TextAsset(File.ReadAllText(_scenarioCatalogPath, Encoding.UTF8));
            if (catalog == null)
            {
                Fail("R5e scenario catalog was not preloaded");
                yield break;
            }
            try
            {
                _scenarioPlan = OfficeSeatDockingR5eScenarioCatalog.ParseAndValidate(catalog);
            }
            catch (Exception exception)
            {
                Fail("scenario catalog parse/identity failure: " + exception.GetType().Name + ":" + exception.Message);
                yield break;
            }
            _catalogSha256 = _scenarioPlan.Sha256;
            _approvedBaseGrid = _runtime.World.Grid;
            _traceArchive = new OfficeRuntimeTraceArchive(_runtime.Actors);
            _scenarioResults = new ScenarioResult[_scenarioPlan.Cases.Length];
            CacheRuntimeBaselines();
            RecordBoundary("PreloadComplete");

            if (_visualOwner)
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                Time.timeScale = _fourTimes ? 4f : 1f;
            }
            // Observer and visual-owner flags both own the deterministic 158-case measurement.
            // Only the visual owner changes window/time-scale; neither mode may silently install
            // an inert observer that later flushes a zero-denominator packet.
            yield return RunVisualCycle();
            _measurementOpen = false;
            RecordBoundary("GameplayMeasureEnd");
            FlushPostWindow();
            Application.Quit(_failure.Length == 0 ? 0 : 96);
        }

        private IEnumerator RunVisualCycle()
        {
            for (var index = 0; index < _scenarioPlan.Cases.Length; index++)
            {
                yield return RunScenarioCase(_scenarioPlan.Cases[index]);
                if (_failure.Length != 0) yield break;
            }
            if (_scenarioResultCount != _scenarioPlan.Cases.Length ||
                _traceArchive == null ||
                _traceArchive.ImportedScenarioCount != _scenarioPlan.Cases.Length)
                Fail("scenario coverage mismatch");
        }

        private IEnumerator RunScenarioCase(R5eScenarioCase scenario)
        {
            _measurementOpen = false;
            OfficeGrid layout = OfficeSeatDockingR5eScenarioCatalog.CreateLayoutForCase(
                _approvedBaseGrid,
                scenario);
            _runtime.ApplyLayoutForQa(layout);
            float readyDeadline = Time.realtimeSinceStartup + 120f;
            while (!_runtime.IsReady)
            {
                if (Time.realtimeSinceStartup > readyDeadline)
                {
                    Fail("scenario rebuild watchdog: " + scenario.CaseId);
                    yield break;
                }
                yield return null;
            }

            IReadOnlyList<OfficeRuntimeAgent> actors = _runtime.Actors;
            if (actors.Count != 4)
            {
                Fail("canonical actor count " + actors.Count + " != 4");
                yield break;
            }
            for (var actorIndex = 0; actorIndex < actors.Count; actorIndex++)
                actors[actorIndex].BeginQaControl();
            CacheRuntimeBaselines();
            OfficeRuntimeTraceCoordinator coordinator = _runtime.World.R5eTraceCoordinator;
            coordinator.BeginQaCapture(scenario.Id);
            coordinator.SetScenarioId(scenario.Id);

            if (scenario.Kind == R5eScenarioKind.Contention)
            {
                yield return RunContentionScenario(scenario, actors, coordinator);
                yield break;
            }

            OfficeRuntimeAgent actor = FindActor(actors, scenario.ActorId);
            OfficeSeatSlot seat = _runtime.World.Workstations.AssignedSeatForQa(actor.AgentId);
            if (seat == null)
            {
                Fail("assigned seat missing: " + scenario.CaseId);
                yield break;
            }
            actor.QaTeleportToCell(OfficeSeatDockingR5eScenarioCatalog.FindArrivalCell(
                layout,
                seat,
                scenario.ArrivalDirection));
            if (scenario.Kind == R5eScenarioKind.FaultEntry)
                actor.QaArmR5eFault(scenario.FaultInjectionId);
            if (scenario.Kind == R5eScenarioKind.VersionEntry)
                actor.QaInvalidateNextAtomicVersion();

            bool entryAccepted = actor.QaBeginSeatedWorkAtSeat(seat.SeatId, scenario.CaseId);
            if (!entryAccepted)
            {
                Fail("production entry request rejected: " + scenario.CaseId);
                yield break;
            }
            StartGameplayMeasurement();

            bool entryMustRollback = scenario.Kind == R5eScenarioKind.FaultEntry ||
                                     scenario.Kind == R5eScenarioKind.VersionEntry;
            float deadline = Time.realtimeSinceStartup + PhaseWatchdogSeconds;
            while (entryMustRollback
                       ? !HasEvent(actor, R5eSeatTransitionKind.Entry, R5eSeatTransitionEventKind.Rollback)
                       : !actor.IsSeated)
            {
                if (coordinator.FatalAbort || Time.realtimeSinceStartup > deadline)
                {
                    StopGameplayMeasurement();
                    Fail("entry terminal watchdog/fatal: " + scenario.CaseId + ":" + coordinator.FatalReason);
                    yield break;
                }
                yield return null;
            }
            if (entryMustRollback)
            {
                StopGameplayMeasurement();
                bool exact = LastRollbackIsExact(actor, R5eSeatTransitionKind.Entry);
                if (!ArchiveScenario(coordinator, scenario)) yield break;
                RecordScenario(scenario, exact, exact ? "entry-rollback-exact" : "entry-half-state");
                if (!exact) Fail("entry rollback was not exact: " + scenario.CaseId);
                yield break;
            }

            for (var frame = 0; frame < 4; frame++) yield return null;
            if (!coordinator.TryAppendVisualMetadata(actor, Time.frameCount))
            {
                StopGameplayMeasurement();
                Fail("visual metadata append failed: " + scenario.CaseId);
                yield break;
            }

            if (scenario.Kind == R5eScenarioKind.AllExitsBlocked)
            {
                StopGameplayMeasurement();
                if (!actor.QaTryGetActiveExitCells(
                        out OfficeGridCoordinate front,
                        out OfficeGridCoordinate left,
                        out OfficeGridCoordinate right))
                {
                    StopGameplayMeasurement();
                    Fail("blocked scenario exit cells missing");
                    yield break;
                }
                actors[(IndexOfActor(actors, actor) + 1) % 4].QaTeleportToCell(front);
                actors[(IndexOfActor(actors, actor) + 2) % 4].QaTeleportToCell(left);
                actors[(IndexOfActor(actors, actor) + 3) % 4].QaTeleportToCell(right);
                StartGameplayMeasurement();
            }
            if (scenario.Kind == R5eScenarioKind.FaultExit)
                actor.QaArmR5eFault(scenario.FaultInjectionId);
            if (scenario.Kind == R5eScenarioKind.VersionExit)
                actor.QaInvalidateNextAtomicVersion();

            int firstWalkBefore = actor.R5eFirstWalkCount;
            bool blockedOrRollback = scenario.Kind == R5eScenarioKind.AllExitsBlocked ||
                                     scenario.Kind == R5eScenarioKind.FaultExit ||
                                     scenario.Kind == R5eScenarioKind.VersionExit;
            if (!actor.QaRequestStandWithOutwardRoute())
            {
                StopGameplayMeasurement();
                Fail("production exit request rejected: " + scenario.CaseId);
                yield break;
            }
            deadline = Time.realtimeSinceStartup + PhaseWatchdogSeconds;
            while (blockedOrRollback
                       ? !HasEvent(actor, R5eSeatTransitionKind.Exit, R5eSeatTransitionEventKind.Rollback)
                       : actor.R5eFirstWalkCount == firstWalkBefore)
            {
                if (coordinator.FatalAbort || Time.realtimeSinceStartup > deadline)
                {
                    StopGameplayMeasurement();
                    Fail("exit terminal watchdog/fatal: " + scenario.CaseId + ":" + coordinator.FatalReason);
                    yield break;
                }
                yield return null;
            }
            bool passed;
            string detail;
            if (blockedOrRollback)
            {
                passed = actor.IsR5eSeatedPostState &&
                         actor.R5eFirstWalkCount == firstWalkBefore &&
                         LastRollbackIsExact(actor, R5eSeatTransitionKind.Exit);
                detail = passed ? "exit-rollback-exact-seated" : "exit-half-state-or-walk";
            }
            else
            {
                passed = actor.R5eLastFirstWalkTick > actor.R5eTurnCompleteTick &&
                         HasOrderedExitLifecycle(actor);
                detail = passed ? "entry-exit-firstwalk" : "firstwalk-order-invalid";
                if (passed) coordinator.TryAppendVisualMetadata(actor, Time.frameCount);
            }
            StopGameplayMeasurement();
            if (!ArchiveScenario(coordinator, scenario)) yield break;
            RecordScenario(scenario, passed, detail);
            if (!passed) Fail("scenario oracle failed: " + scenario.CaseId + ":" + detail);
        }

        private IEnumerator RunContentionScenario(
            R5eScenarioCase scenario,
            IReadOnlyList<OfficeRuntimeAgent> actors,
            OfficeRuntimeTraceCoordinator coordinator)
        {
            R5eContentionPermutation permutation = _scenarioPlan.Contention[scenario.ContentionIndex];
            OfficeSeatSlot targetSeat = _runtime.World.Workstations.AssignedSeatForQa("player");
            int accepted = 0;
            for (var order = 0; order < permutation.order.Length; order++)
            {
                OfficeRuntimeAgent actor = FindActor(actors, permutation.order[order]);
                actor.QaTeleportToCell(OfficeSeatDockingR5eScenarioCatalog.FindArrivalCell(
                    _runtime.World.Grid,
                    targetSeat,
                    order * 2));
            }
            for (var order = 0; order < permutation.order.Length; order++)
            {
                OfficeRuntimeAgent actor = FindActor(actors, permutation.order[order]);
                if (actor.QaBeginSeatedWorkAtSeat(targetSeat.SeatId, scenario.CaseId)) accepted++;
            }
            StartGameplayMeasurement();
            float deadline = Time.realtimeSinceStartup + PhaseWatchdogSeconds;
            int occupied = 0;
            while (occupied == 0)
            {
                occupied = 0;
                for (var index = 0; index < actors.Count; index++)
                    if (actors[index].IsSeated) occupied++;
                if (coordinator.FatalAbort || Time.realtimeSinceStartup > deadline)
                {
                    StopGameplayMeasurement();
                    Fail("contention watchdog/fatal: " + scenario.CaseId);
                    yield break;
                }
                yield return null;
            }
            StopGameplayMeasurement();
            bool passed = accepted == 1 && occupied == 1;
            if (!ArchiveScenario(coordinator, scenario)) yield break;
            RecordScenario(scenario, passed, "accepted=" + accepted + ";occupied=" + occupied);
            if (!passed) Fail("contention uniqueness failed: " + scenario.CaseId);
        }

        private void StartGameplayMeasurement()
        {
            if (_frameCount == 0) RecordBoundary("GameplayMeasureBegin");
            _previousAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            _measurementOpen = true;
        }

        private void StopGameplayMeasurement() => _measurementOpen = false;

        private bool ArchiveScenario(
            OfficeRuntimeTraceCoordinator coordinator,
            in R5eScenarioCase scenario)
        {
            if (_measurementOpen)
            {
                Fail("trace archive attempted inside gameplay measurement: " + scenario.CaseId);
                return false;
            }
            if (_traceArchive == null || !_traceArchive.TryImportCompletedScenario(coordinator))
            {
                Fail("trace archive capacity/producer failure: " + scenario.CaseId);
                return false;
            }
            return true;
        }

        private static OfficeRuntimeAgent FindActor(
            IReadOnlyList<OfficeRuntimeAgent> actors,
            string actorId)
        {
            for (var index = 0; index < actors.Count; index++)
                if (string.Equals(actors[index].AgentId, actorId, StringComparison.Ordinal))
                    return actors[index];
            throw new InvalidOperationException("R5e actor missing: " + actorId);
        }

        private static int IndexOfActor(
            IReadOnlyList<OfficeRuntimeAgent> actors,
            OfficeRuntimeAgent actor)
        {
            for (var index = 0; index < actors.Count; index++)
                if (ReferenceEquals(actors[index], actor)) return index;
            return -1;
        }

        private static bool HasEvent(
            OfficeRuntimeAgent actor,
            R5eSeatTransitionKind kind,
            R5eSeatTransitionEventKind eventKind)
        {
            R5eFixedBuffer<R5eSeatTransitionTraceRow> rows = actor.R5eTraceState.TransitionRows;
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                R5eSeatTransitionTraceRow row = rows.Rows[index];
                if (row.TransitionKind == kind && row.EventKind == eventKind) return true;
            }
            return false;
        }

        private static bool LastRollbackIsExact(
            OfficeRuntimeAgent actor,
            R5eSeatTransitionKind kind)
        {
            R5eFixedBuffer<R5eSeatTransitionTraceRow> rows = actor.R5eTraceState.TransitionRows;
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                R5eSeatTransitionTraceRow row = rows.Rows[index];
                if (row.TransitionKind != kind ||
                    row.EventKind != R5eSeatTransitionEventKind.Rollback) continue;
                R5eAgentStepSnapshot before = row.Before;
                R5eAgentStepSnapshot after = row.After;
                R5eProductionObservation beforeObserved = row.BeforeObservation;
                R5eProductionObservation afterObserved = row.AfterObservation;
                return row.RollbackSucceeded && !row.CommitSucceeded &&
                       before.Phase == after.Phase &&
                       Vector2.Distance(before.LogicalRoot, after.LogicalRoot) <= 0.000001f &&
                       Vector2.Distance(before.VisualRoot, after.VisualRoot) <= 0.000001f &&
                       Vector2.Distance(before.CurrentVelocity, after.CurrentVelocity) <= 0.000001f &&
                       Mathf.Abs(before.VisibleMotionDebtSeconds - after.VisibleMotionDebtSeconds) <= 0.000001f &&
                       beforeObserved.Occupancy.CurrentCell.Equals(afterObserved.Occupancy.CurrentCell) &&
                       Vector2.Distance(
                           beforeObserved.Occupancy.Position,
                           afterObserved.Occupancy.Position) <= 0.000001f &&
                       beforeObserved.Occupancy.Epoch == afterObserved.Occupancy.Epoch &&
                       beforeObserved.SeatReserved == afterObserved.SeatReserved &&
                       beforeObserved.SeatOccupied == afterObserved.SeatOccupied &&
                       beforeObserved.ChairSnapshotValid && afterObserved.ChairSnapshotValid &&
                       beforeObserved.Chair.Hash == afterObserved.Chair.Hash;
            }
            return false;
        }

        private static bool HasOrderedExitLifecycle(OfficeRuntimeAgent actor)
        {
            R5eFixedBuffer<R5eSeatTransitionTraceRow> rows = actor.R5eTraceState.TransitionRows;
            ulong transaction = 0;
            var next = 0;
            ulong turnTick = 0;
            for (var index = 0; index < rows.Count; index++)
            {
                R5eSeatTransitionTraceRow row = rows.Rows[index];
                if (row.TransitionKind != R5eSeatTransitionKind.Exit) continue;
                if (transaction == 0) transaction = row.TransactionId;
                if (row.TransactionId != transaction) continue;
                R5eSeatTransitionEventKind expected = next switch
                {
                    0 => R5eSeatTransitionEventKind.Prepare,
                    1 => R5eSeatTransitionEventKind.Commit,
                    2 => R5eSeatTransitionEventKind.Rebase,
                    3 => R5eSeatTransitionEventKind.TurnComplete,
                    _ => R5eSeatTransitionEventKind.FirstWalk
                };
                if (row.EventKind != expected) return false;
                if (row.LocomotionSample) return false;
                if (row.EventKind == R5eSeatTransitionEventKind.TurnComplete)
                    turnTick = row.Context.ActorRuntimeTick;
                if (row.EventKind == R5eSeatTransitionEventKind.FirstWalk)
                    return next == 4 && row.Context.ActorRuntimeTick > turnTick;
                next++;
            }
            return false;
        }

        private void RecordScenario(in R5eScenarioCase scenario, bool passed, string detail)
        {
            if (_scenarioResultCount >= _scenarioResults.Length)
            {
                _failure = "scenario result buffer overflow";
                return;
            }
            _scenarioResults[_scenarioResultCount++] = new ScenarioResult(
                scenario.Id,
                scenario.CaseId,
                scenario.Kind.ToString(),
                passed,
                detail);
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
            int colliders = 0;
            int colliders2D = 0;
            int rigidbodies = 0;
            int rigidbodies2D = 0;
            int navMeshAgents = 0;
            int floorInvalid = 0;
            int staticOverlap = 0;
            int dynamicOverlap = 0;
            int collisionViolations = 0;
            int legacySitMask = 0;
            int legacyStandMask = 0;
            float maximumStuckSeconds = 0f;
            float maximumSeatedDebt = 0f;
            float maximumSeatedVelocity = 0f;
            float maximumSeatedDisplacement = 0f;
            IReadOnlyList<OfficeRuntimeAgent> actors = _runtime.Actors;
            for (var actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                OfficeRuntimeAgent actor = actors[actorIndex];
                if (actor.GetComponentInChildren<Collider>(true) != null) colliders++;
                if (actor.GetComponentInChildren<Collider2D>(true) != null) colliders2D++;
                if (actor.GetComponentInChildren<Rigidbody>(true) != null) rigidbodies++;
                if (actor.GetComponentInChildren<Rigidbody2D>(true) != null) rigidbodies2D++;
                if (actor.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) != null)
                    navMeshAgents++;
                if (!actor.TryObserveR5eRuntimeClearance(
                        out bool floorValid,
                        out bool actorStaticOverlap,
                        out bool actorDynamicOverlap) || !floorValid) floorInvalid++;
                if (actorStaticOverlap) staticOverlap++;
                if (actorDynamicOverlap) dynamicOverlap++;
                collisionViolations += actor.R5eCollisionViolationCount;
                legacySitMask |= actor.R5eDeprecatedSitFrameMask;
                legacyStandMask |= actor.R5eDeprecatedStandFrameMask;
                maximumStuckSeconds = Mathf.Max(maximumStuckSeconds, actor.R5eStuckSeconds);
                if (actor.IsR5eSeatedPostState)
                {
                    maximumSeatedDebt = Mathf.Max(
                        maximumSeatedDebt,
                        Mathf.Abs(actor.R5eVisibleMotionDebtSeconds));
                    maximumSeatedVelocity = Mathf.Max(
                        maximumSeatedVelocity,
                        actor.R5eCurrentVelocityMagnitude);
                    maximumSeatedDisplacement = Mathf.Max(
                        maximumSeatedDisplacement,
                        actor.R5eLastActualDisplacementMagnitude);
                }
            }
            bool furnitureValid = _runtime.World.Workstations.TryCaptureFurnitureTransformAggregate(
                out ulong furnitureHash,
                out int furnitureCount);
            bool furnitureMutation = !furnitureValid || !_furnitureTransformBaselineValid ||
                                     furnitureCount != _furnitureTransformBaselineCount ||
                                     furnitureHash != _furnitureTransformBaselineHash;
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
                colliders,
                colliders2D,
                rigidbodies,
                rigidbodies2D,
                navMeshAgents,
                furnitureHash,
                furnitureCount,
                furnitureValid,
                furnitureMutation,
                floorInvalid,
                staticOverlap,
                dynamicOverlap,
                collisionViolations,
                legacySitMask,
                legacyStandMask,
                maximumStuckSeconds,
                maximumSeatedDebt,
                maximumSeatedVelocity,
                maximumSeatedDisplacement);
        }

        private void CacheRuntimeBaselines()
        {
            _forbiddenColliderCount = 0;
            _forbiddenCollider2DCount = 0;
            _forbiddenRigidbodyCount = 0;
            _forbiddenRigidbody2DCount = 0;
            _forbiddenNavMeshAgentCount = 0;
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
            _furnitureTransformBaselineValid =
                _runtime.World.Workstations.TryCaptureFurnitureTransformAggregate(
                    out _furnitureTransformBaselineHash,
                    out _furnitureTransformBaselineCount);
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
                OfficeSeatDockingR5eTraceWriteSummary traces = _runtime == null ||
                                                               _traceArchive == null
                    ? default
                    : OfficeSeatDockingR5eTraceWriter.WriteArchive(
                        _traceArchive,
                        _artifactDirectory);
                WriteBoundaries();
                WritePerformanceFrames();
                WriteScenarioResults();
                int over50 = 0;
                int activeBodyMismatch = 0;
                int gameplayAllocationFrames = 0;
                int runtimeInvariantFailureFrames = 0;
                float maxFrameMs = 0f;
                for (var index = 0; index < _frameCount; index++)
                {
                    FrameSample sample = _frameSamples[index];
                    maxFrameMs = Mathf.Max(maxFrameMs, sample.FrameMs);
                    if (sample.FrameMs >= 50f) over50++;
                    if (sample.ActiveBodySprites != 4) activeBodyMismatch++;
                    if (sample.GcAllocBytes != 0) gameplayAllocationFrames++;
                    if (!sample.FurnitureValid || sample.FurnitureMutation ||
                        sample.FloorInvalidCount != 0 || sample.StaticOverlapCount != 0 ||
                        sample.CollisionViolationCount != 0 || sample.LegacySitFrameMask != 0 ||
                        sample.LegacyStandFrameMask != 0 || sample.MaximumStuckSeconds > 0.000001f ||
                        sample.MaximumSeatedDebt > 0.000001f ||
                        sample.MaximumSeatedVelocity > 0.000001f ||
                        sample.MaximumSeatedDisplacement > 0.000001f ||
                        sample.Colliders != 0 || sample.Colliders2D != 0 ||
                        sample.Rigidbodies != 0 || sample.Rigidbodies2D != 0 ||
                        sample.NavMeshAgents != 0) runtimeInvariantFailureFrames++;
                }
                bool readyForPostProcess = _failure.Length == 0 && traces.ReadyForPostProcess &&
                               _scenarioResultCount == OfficeSeatDockingR5eScenarioCatalog.TotalCaseCount &&
                               AllScenarioResultsPassed() && _frameOverflowCount == 0 &&
                               _frameCount > 0 && over50 == 0 &&
                               gameplayAllocationFrames == 0 && runtimeInvariantFailureFrames == 0 &&
                               _initialBodyRendererCount == 4 && activeBodyMismatch == 0;
                string result =
                    "status=" + (readyForPostProcess ? "PENDING_POSTPROCESS" : "FAIL") + Environment.NewLine +
                    "mode=" + (_visualOwner ? "visual-owner" : "observer") + Environment.NewLine +
                    "timeScale=" + (_fourTimes ? "4" : "1") + Environment.NewLine +
                    "scenarioCatalogSha256=" + _catalogSha256 + Environment.NewLine +
                    "frameCount=" + _frameCount + Environment.NewLine +
                    "frameOver50MsCount=" + over50 + Environment.NewLine +
                    "gameplayAllocationFrameCount=" + gameplayAllocationFrames + Environment.NewLine +
                    "runtimeInvariantFailureFrameCount=" + runtimeInvariantFailureFrames + Environment.NewLine +
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
                    "visualMetadataRows=" + traces.VisualRows + Environment.NewLine +
                    "scenarioExpected=" + OfficeSeatDockingR5eScenarioCatalog.TotalCaseCount + Environment.NewLine +
                    "scenarioObserved=" + _scenarioResultCount + Environment.NewLine +
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
                // Completion is owned by the offline decoded-mask + normal-scale human gate.
                // Runtime can only declare a non-PASS PENDING packet.
                if (!readyForPostProcess)
                    Debug.LogError("FAMILY_COMPANY_CHAIR_R5E_RUNTIME: FAIL | " + _failure);
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
                "actor_rigidbody2d_count,actor_navmeshagent_count,furniture_transform_hash," +
                "furniture_count,furniture_snapshot_valid,furniture_transform_mutation," +
                "floor_invalid_count,static_overlap_count,dynamic_overlap_count," +
                "collision_violation_count,legacy_sit_frame_mask,legacy_stand_frame_mask," +
                "maximum_stuck_seconds,maximum_seated_debt,maximum_seated_velocity," +
                "maximum_seated_displacement");
            for (var index = 0; index < _frameCount; index++)
                writer.WriteLine(_frameSamples[index].ToCsv());
        }

        private void WriteScenarioResults()
        {
            string path = Path.Combine(_artifactDirectory, "chair-r5e-scenario-results.csv");
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("schemaVersion,scenarioId,caseId,kind,terminalObserved,passed,detail");
            for (var index = 0; index < _scenarioResultCount; index++)
            {
                ScenarioResult row = _scenarioResults[index];
                writer.WriteLine(
                    OfficeSeatDockingTraceSchemas.SchemaVersion + "," + row.ScenarioId + "," +
                    row.CaseId + "," + row.Kind + ",true," +
                    (row.Passed ? "true" : "false") + "," + row.Detail);
            }
        }

        private bool AllScenarioResultsPassed()
        {
            if (_scenarioResultCount == 0) return false;
            for (var index = 0; index < _scenarioResultCount; index++)
                if (!_scenarioResults[index].Passed) return false;
            return true;
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

        private static string ResolveOptionalArgument(string[] arguments, string key)
        {
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], key, StringComparison.Ordinal))
                    return Path.GetFullPath(arguments[index + 1]);
            return string.Empty;
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

        private readonly struct ScenarioResult
        {
            public ScenarioResult(
                ulong scenarioId,
                string caseId,
                string kind,
                bool passed,
                string detail)
            {
                ScenarioId = scenarioId;
                CaseId = caseId;
                Kind = kind;
                Passed = passed;
                Detail = detail;
            }
            public ulong ScenarioId { get; }
            public string CaseId { get; }
            public string Kind { get; }
            public bool Passed { get; }
            public string Detail { get; }
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
                int navMeshAgents,
                ulong furnitureHash,
                int furnitureCount,
                bool furnitureValid,
                bool furnitureMutation,
                int floorInvalidCount,
                int staticOverlapCount,
                int dynamicOverlapCount,
                int collisionViolationCount,
                int legacySitFrameMask,
                int legacyStandFrameMask,
                float maximumStuckSeconds,
                float maximumSeatedDebt,
                float maximumSeatedVelocity,
                float maximumSeatedDisplacement)
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
                FurnitureHash = furnitureHash;
                FurnitureCount = furnitureCount;
                FurnitureValid = furnitureValid;
                FurnitureMutation = furnitureMutation;
                FloorInvalidCount = floorInvalidCount;
                StaticOverlapCount = staticOverlapCount;
                DynamicOverlapCount = dynamicOverlapCount;
                CollisionViolationCount = collisionViolationCount;
                LegacySitFrameMask = legacySitFrameMask;
                LegacyStandFrameMask = legacyStandFrameMask;
                MaximumStuckSeconds = maximumStuckSeconds;
                MaximumSeatedDebt = maximumSeatedDebt;
                MaximumSeatedVelocity = maximumSeatedVelocity;
                MaximumSeatedDisplacement = maximumSeatedDisplacement;
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
            public ulong FurnitureHash { get; }
            public int FurnitureCount { get; }
            public bool FurnitureValid { get; }
            public bool FurnitureMutation { get; }
            public int FloorInvalidCount { get; }
            public int StaticOverlapCount { get; }
            public int DynamicOverlapCount { get; }
            public int CollisionViolationCount { get; }
            public int LegacySitFrameMask { get; }
            public int LegacyStandFrameMask { get; }
            public float MaximumStuckSeconds { get; }
            public float MaximumSeatedDebt { get; }
            public float MaximumSeatedVelocity { get; }
            public float MaximumSeatedDisplacement { get; }

            public string ToCsv() =>
                Frame + "," + RealtimeSeconds.ToString("R", CultureInfo.InvariantCulture) + "," +
                FrameMs.ToString("R", CultureInfo.InvariantCulture) + "," + GcAllocBytes + "," +
                MonoUsedBytes + "," + ActiveBodySprites + "," + Colliders + "," + Colliders2D + "," +
                Rigidbodies + "," + Rigidbodies2D + "," + NavMeshAgents + "," +
                FurnitureHash + "," + FurnitureCount + "," +
                (FurnitureValid ? "true" : "false") + "," +
                (FurnitureMutation ? "true" : "false") + "," +
                FloorInvalidCount + "," + StaticOverlapCount + "," + DynamicOverlapCount + "," +
                CollisionViolationCount + "," + LegacySitFrameMask + "," + LegacyStandFrameMask + "," +
                MaximumStuckSeconds.ToString("R", CultureInfo.InvariantCulture) + "," +
                MaximumSeatedDebt.ToString("R", CultureInfo.InvariantCulture) + "," +
                MaximumSeatedVelocity.ToString("R", CultureInfo.InvariantCulture) + "," +
                MaximumSeatedDisplacement.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}

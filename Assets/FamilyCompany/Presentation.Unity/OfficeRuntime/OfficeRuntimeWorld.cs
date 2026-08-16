using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime.Qa;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public readonly struct OfficeVisibleMotionBudget
    {
        public OfficeVisibleMotionBudget(float consumedSeconds, float remainingDebtSeconds)
        {
            ConsumedSeconds = consumedSeconds;
            RemainingDebtSeconds = remainingDebtSeconds;
        }

        public float ConsumedSeconds { get; }
        public float RemainingDebtSeconds { get; }
    }

    [DisallowMultipleComponent]
    public sealed class OfficeRuntimeWorld : MonoBehaviour
    {
        // 1.65 world units/s * 0.06s = 0.099 world unit, below the visible frame-step
        // quality bar. Simulation time may catch up after a hitch, but a rendered character root
        // must not consume that entire hitch as one on-screen teleport.
        public const float MaximumVisibleMotionDeltaSeconds = 0.06f;
        private readonly OfficeRuntimeActorRegistry _registry = new OfficeRuntimeActorRegistry();
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _presenter;
        private OfficeGridFurniturePresenter _furniturePresenter;
        private OfficeRuntimeOccupancy _occupancy;
        private OfficeRuntimePathService _paths;
        private OfficeRuntimeWorkstationService _workstations;
        private OfficeRuntimeDepthSorter _depthSorter;
        private bool _configured;
        private float[] _frameMotionDeltas = Array.Empty<float>();
        private int[] _frameStepCounts = Array.Empty<int>();
        private OfficeRuntimeTraceCoordinator _traceCoordinator;

        public OfficeGrid Grid => _grid;
        public OfficeGridTilemapPresenter Presenter => _presenter;
        public OfficeGridFurniturePresenter FurniturePresenter => _furniturePresenter;
        public OfficeRuntimeOccupancy Occupancy => _occupancy;
        public OfficeRuntimePathService Paths => _paths;
        public OfficeRuntimeWorkstationService Workstations => _workstations;
        public OfficeRuntimeInteractionLifecycleService Interactions =>
            _workstations?.InteractionLifecycle;
        public OfficeRuntimeDepthSorter DepthSorter => _depthSorter;
        public OfficeRuntimeActorRegistry Registry => _registry;
        internal OfficeRuntimeTraceCoordinator R5eTraceCoordinator => _traceCoordinator;
        public int ReplanCount { get; private set; }
        public int ArrivalCount { get; private set; }
        public float LastFrameDeltaTime { get; private set; }
        public float LastUnscaledFrameDeltaTime { get; private set; }
        public float LastMotionDeltaTime { get; private set; }
        public float MotionTimeDebtSeconds { get; private set; }

        public static OfficeVisibleMotionBudget ConsumeVisibleMotionBudget(
            float previousDebtSeconds,
            float unscaledFrameDeltaTime)
        {
            if (float.IsNaN(previousDebtSeconds) || float.IsInfinity(previousDebtSeconds) ||
                previousDebtSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(previousDebtSeconds));
            if (float.IsNaN(unscaledFrameDeltaTime) || float.IsInfinity(unscaledFrameDeltaTime) ||
                unscaledFrameDeltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(unscaledFrameDeltaTime));
            float available = previousDebtSeconds + unscaledFrameDeltaTime;
            float consumed = Mathf.Min(available, MaximumVisibleMotionDeltaSeconds);
            float remaining = Mathf.Max(0f, available - consumed);
            if (remaining <= 0.0000001f) remaining = 0f;
            return new OfficeVisibleMotionBudget(consumed, remaining);
        }

        public static OfficeVisibleMotionBudget ConsumeActorVisibleMotionBudget(
            bool hasActiveVisibleMotionIntent,
            float previousDebtSeconds,
            float unscaledFrameDeltaTime)
        {
            if (!hasActiveVisibleMotionIntent)
            {
                if (float.IsNaN(previousDebtSeconds) || float.IsInfinity(previousDebtSeconds) ||
                    previousDebtSeconds < 0f)
                    throw new ArgumentOutOfRangeException(nameof(previousDebtSeconds));
                if (float.IsNaN(unscaledFrameDeltaTime) || float.IsInfinity(unscaledFrameDeltaTime) ||
                    unscaledFrameDeltaTime < 0f)
                    throw new ArgumentOutOfRangeException(nameof(unscaledFrameDeltaTime));
                // Idle/work/failed reservation time is not traversable route distance. Runtime
                // logic still receives one bounded tick, while stale catch-up is cleared now.
                return new OfficeVisibleMotionBudget(
                    Mathf.Min(unscaledFrameDeltaTime, MaximumVisibleMotionDeltaSeconds),
                    0f);
            }
            return ConsumeVisibleMotionBudget(previousDebtSeconds, unscaledFrameDeltaTime);
        }

        public void Configure(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter)
        {
            _workstations?.InteractionLifecycle.AbortAll();
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            _occupancy = new OfficeRuntimeOccupancy();
            _occupancy.Rebuild(grid, presenter);
            _paths = new OfficeRuntimePathService(grid, _occupancy, presenter);
            _workstations = new OfficeRuntimeWorkstationService(
                grid,
                presenter,
                furniturePresenter,
                _occupancy,
                _paths);
            _depthSorter = new OfficeRuntimeDepthSorter(grid, presenter, furniturePresenter);
            _traceCoordinator = new OfficeRuntimeTraceCoordinator(
                1UL,
                0UL,
                OfficeRuntimeTraceCoordinator.MaximumActors,
                OfficeSeatDockingR5eRuntimeQaContract.IsRequested(
                    Environment.GetCommandLineArgs()));
            MotionTimeDebtSeconds = 0f;
            _configured = true;
        }

        public void RegisterActor(OfficeRuntimeAgent actor)
        {
            if (!_configured) throw new InvalidOperationException("Starter Office world is not configured.");
            _registry.Register(actor);
            _occupancy.RegisterActor(actor.AgentId, actor.Position, actor.AgentRadius);
            actor.BindR5eTrace(
                _traceCoordinator,
                _traceCoordinator.RegisterActor(actor));
        }

        public void ValidateCanonicalActors()
        {
            _registry.ValidateCanonicalFamily();
        }

        public IReadOnlyList<OfficeGridCoordinate> FindPath(
            string agentId,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal,
            string permittedSeatId,
            bool avoidDynamic = false,
            float radius = OfficeRuntimeAgent.DefaultRadius)
        {
            IReadOnlyList<OfficeGridCoordinate> result = _paths.FindPath(
                agentId,
                start,
                goal,
                permittedSeatId,
                avoidDynamic,
                radius);
            if (result.Count > 0) ReplanCount++;
            return result;
        }

        public OfficeTrafficDecision ResolveTraffic(
            string agentId,
            Vector2 position,
            Vector2 desiredVelocity,
            float radius,
            float stuckSeconds)
        {
            var self = new OfficeTrafficAgentState(
                agentId,
                new OfficeNavPoint(position.x, position.y),
                new OfficeNavPoint(desiredVelocity.x, desiredVelocity.y),
                radius,
                stuckSeconds);
            return OfficeNavigationTrafficRules.Resolve(self, _occupancy.TrafficSnapshot());
        }

        public void NotifyArrival()
        {
            ArrivalCount++;
        }

        public void RebuildOccupancy()
        {
            _workstations?.InteractionLifecycle.AbortAll();
            _occupancy.Rebuild(_grid, _presenter);
            foreach (OfficeRuntimeAgent actor in _registry.Actors) actor.InvalidatePath();
        }

        private void Update()
        {
            using (OfficePerformanceTelemetry.Measure(OfficePerformancePath.RuntimeWorldUpdate))
            {
                if (!_configured) return;
                float deltaTime = Time.deltaTime;
                if (deltaTime <= 0f) return;
                float unscaledDeltaTime = Time.unscaledDeltaTime;
                // PrototypeBootstrap advances the authoritative office clock separately. Runtime
                // navigation deliberately advances only the debt consumed here: every TickRuntime
                // substep moves the visible Transform and updates canonical occupancy together, so a
                // logical seat/work arrival cannot run ahead of the body or reserve through furniture.
                // Any hitch remainder stays on the actor that owns the active route and drains on
                // later renders. Idle time is never transferred to a future route.
                LastFrameDeltaTime = deltaTime;
                LastUnscaledFrameDeltaTime = unscaledDeltaTime;
                IReadOnlyList<OfficeRuntimeAgent> actors = _registry.Actors;
                EnsureFrameBuffers(actors.Count);
                LastMotionDeltaTime = 0f;
                MotionTimeDebtSeconds = 0f;
                int maximumStepCount = 0;
                for (var index = 0; index < actors.Count; index++)
                {
                    OfficeRuntimeAgent actor = actors[index];
                    if (actor == null || !actor.isActiveAndEnabled)
                    {
                        _frameMotionDeltas[index] = 0f;
                        _frameStepCounts[index] = 0;
                        continue;
                    }
                    actor.BeginPresentationFrame();
                    float actorDelta = actor.ConsumeVisibleMotionDelta(unscaledDeltaTime);
                    int actorSteps = OfficeNavigationMotionIntegrator.CalculateStepCount(actorDelta);
                    _frameMotionDeltas[index] = actorDelta;
                    _frameStepCounts[index] = actorSteps;
                    LastMotionDeltaTime = Mathf.Max(LastMotionDeltaTime, actorDelta);
                    MotionTimeDebtSeconds = Mathf.Max(
                        MotionTimeDebtSeconds,
                        actor.VisibleMotionDebtSeconds);
                    maximumStepCount = Mathf.Max(maximumStepCount, actorSteps);
                }
                for (var step = 0; step < maximumStepCount; step++)
                {
                    for (var index = 0; index < actors.Count; index++)
                    {
                        OfficeRuntimeAgent actor = actors[index];
                        int actorSteps = _frameStepCounts[index];
                        if (actor == null || !actor.isActiveAndEnabled || step >= actorSteps) continue;
                        float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(
                            _frameMotionDeltas[index],
                            step,
                            actorSteps);
                        R5eAgentStepSnapshot preStep = actor.CaptureR5eStepSnapshot();
                        Vector2 beforePosition = actor.Position;
                        if (!_traceCoordinator.TryBeginActorStep(
                                actor,
                                Time.frameCount,
                                step,
                                actorSteps,
                                _frameMotionDeltas[index],
                                stepDelta,
                                out OfficeRuntimeStepTraceContext traceContext)) continue;
                        actor.BeginR5eRuntimeStep(traceContext, beforePosition, preStep);
                        try
                        {
                            actor.TickRuntime(stepDelta);
                        }
                        catch (Exception)
                        {
                            actor.AbortR5eRuntimeStep(traceContext);
                            _traceCoordinator.RecordActorStepException(
                                traceContext,
                                actor.IsR5eSeatedPostState || preStep.Seated);
                            if (!_traceCoordinator.CaptureEnabled) throw;
                            continue;
                        }
                        R5eAgentStepSnapshot preClear = actor.CaptureR5eStepSnapshot();
                        bool expectedSeatedPreClear = actor.IsR5eSeatedPostState;
                        _traceCoordinator.CountExpectedPreClear(
                            traceContext,
                            expectedSeatedPreClear);
                        actor.AppendObservedPreClear(traceContext, preStep, preClear);
                        actor.ClearInactiveVisibleMotionDebt();
                        R5eAgentStepSnapshot postClear = actor.CaptureR5eStepSnapshot();
                        bool expectedSeatedPostClear = actor.IsR5eSeatedPostState;
                        bool expectedMoving =
                            !actor.AtomicPlacementOccurred(traceContext.ActorRuntimeTick) &&
                            Vector2.Distance(actor.Position, beforePosition) >
                            OfficeRuntimeTraceCoordinator.StationaryEpsilon;
                        _traceCoordinator.CountExpectedPostClearAndMoving(
                            traceContext,
                            expectedSeatedPostClear,
                            expectedMoving);
                        actor.FinalizeR5eRuntimeStepPostClear(
                            traceContext,
                            preStep,
                            preClear,
                            postClear,
                            expectedMoving);
                    }
                }
                for (var index = 0; index < actors.Count; index++)
                {
                    OfficeRuntimeAgent actor = actors[index];
                    if (actor != null && actor.isActiveAndEnabled)
                    {
                        _traceCoordinator.TryPreflightRender(actor);
                        _traceCoordinator.BeginRenderFrame(actor, Time.frameCount);
                        actor.TickPresentation(_frameMotionDeltas[index]);
                        _traceCoordinator.CountExpectedRender(actor);
                        _traceCoordinator.AppendRenderAdapter(actor, Time.frameCount);
                    }
                }
                MotionTimeDebtSeconds = 0f;
                for (var index = 0; index < actors.Count; index++)
                {
                    OfficeRuntimeAgent actor = actors[index];
                    if (actor != null && actor.isActiveAndEnabled)
                        MotionTimeDebtSeconds = Mathf.Max(
                            MotionTimeDebtSeconds,
                            actor.VisibleMotionDebtSeconds);
                }
                // One footprint sort owns every sorting order in the office, applied last so nothing
                // can leave a stale per-sprite order behind.
                _depthSorter.Apply(actors);
            }
        }

        private void EnsureFrameBuffers(int actorCount)
        {
            if (_frameMotionDeltas.Length >= actorCount && _frameStepCounts.Length >= actorCount)
                return;
            _frameMotionDeltas = new float[actorCount];
            _frameStepCounts = new int[actorCount];
        }

        private void OnDestroy()
        {
            _workstations?.InteractionLifecycle.AbortAll();
            foreach (OfficeRuntimeAgent actor in _registry.Actors)
                if (actor != null) _occupancy?.UnregisterActor(actor.AgentId);
        }
    }
}

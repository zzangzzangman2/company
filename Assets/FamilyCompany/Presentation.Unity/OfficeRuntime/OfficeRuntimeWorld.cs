using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
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
        private float _motionTimeDebtSeconds;

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
        public int ReplanCount { get; private set; }
        public int ArrivalCount { get; private set; }
        public float LastFrameDeltaTime { get; private set; }
        public float LastUnscaledFrameDeltaTime { get; private set; }
        public float LastMotionDeltaTime { get; private set; }
        public float MotionTimeDebtSeconds => _motionTimeDebtSeconds;

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
            _motionTimeDebtSeconds = 0f;
            _configured = true;
        }

        public void RegisterActor(OfficeRuntimeAgent actor)
        {
            if (!_configured) throw new InvalidOperationException("Starter Office world is not configured.");
            _registry.Register(actor);
            _occupancy.RegisterActor(actor.AgentId, actor.Position, actor.AgentRadius);
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
                // Any hitch remainder stays in _motionTimeDebtSeconds and drains on later renders.
                OfficeVisibleMotionBudget budget = ConsumeVisibleMotionBudget(
                    _motionTimeDebtSeconds,
                    unscaledDeltaTime);
                float motionDeltaTime = budget.ConsumedSeconds;
                _motionTimeDebtSeconds = budget.RemainingDebtSeconds;
                LastFrameDeltaTime = deltaTime;
                LastUnscaledFrameDeltaTime = unscaledDeltaTime;
                LastMotionDeltaTime = motionDeltaTime;
                foreach (OfficeRuntimeAgent actor in _registry.Actors)
                {
                    if (actor != null && actor.isActiveAndEnabled) actor.BeginPresentationFrame();
                }
                int stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(motionDeltaTime);
                for (var step = 0; step < stepCount; step++)
                {
                    float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(
                        motionDeltaTime,
                        step,
                        stepCount);
                    foreach (OfficeRuntimeAgent actor in _registry.Actors)
                    {
                        if (actor != null && actor.isActiveAndEnabled) actor.TickRuntime(stepDelta);
                    }
                }
                foreach (OfficeRuntimeAgent actor in _registry.Actors)
                {
                    if (actor != null && actor.isActiveAndEnabled) actor.TickPresentation(motionDeltaTime);
                }
                // One footprint sort owns every sorting order in the office, applied last so nothing
                // can leave a stale per-sprite order behind.
                _depthSorter.Apply(_registry.Actors);
            }
        }

        private void OnDestroy()
        {
            _workstations?.InteractionLifecycle.AbortAll();
            foreach (OfficeRuntimeAgent actor in _registry.Actors)
                if (actor != null) _occupancy?.UnregisterActor(actor.AgentId);
        }
    }
}

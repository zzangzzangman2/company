using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [DisallowMultipleComponent]
    public sealed class OfficeRuntimeWorld : MonoBehaviour
    {
        private readonly OfficeRuntimeActorRegistry _registry = new OfficeRuntimeActorRegistry();
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _presenter;
        private OfficeGridFurniturePresenter _furniturePresenter;
        private OfficeRuntimeOccupancy _occupancy;
        private OfficeRuntimePathService _paths;
        private OfficeRuntimeWorkstationService _workstations;
        private bool _configured;

        public OfficeGrid Grid => _grid;
        public OfficeGridTilemapPresenter Presenter => _presenter;
        public OfficeGridFurniturePresenter FurniturePresenter => _furniturePresenter;
        public OfficeRuntimeOccupancy Occupancy => _occupancy;
        public OfficeRuntimePathService Paths => _paths;
        public OfficeRuntimeWorkstationService Workstations => _workstations;
        public OfficeRuntimeActorRegistry Registry => _registry;
        public int ReplanCount { get; private set; }
        public int ArrivalCount { get; private set; }

        public void Configure(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            _occupancy = new OfficeRuntimeOccupancy();
            _occupancy.Rebuild(grid, presenter);
            _paths = new OfficeRuntimePathService(grid, _occupancy);
            _workstations = new OfficeRuntimeWorkstationService(
                grid,
                presenter,
                furniturePresenter,
                _occupancy);
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
            bool avoidDynamic = false)
        {
            IReadOnlyList<OfficeGridCoordinate> result = _paths.FindPath(
                agentId,
                start,
                goal,
                permittedSeatId,
                avoidDynamic);
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
            _occupancy.Rebuild(_grid, _presenter);
            foreach (OfficeRuntimeAgent actor in _registry.Actors) actor.InvalidatePath();
        }

        private void Update()
        {
            if (!_configured) return;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            int stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(deltaTime);
            for (var step = 0; step < stepCount; step++)
            {
                float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(deltaTime, step, stepCount);
                foreach (OfficeRuntimeAgent actor in _registry.Actors)
                {
                    if (actor != null && actor.isActiveAndEnabled) actor.TickRuntime(stepDelta);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (OfficeRuntimeAgent actor in _registry.Actors)
                if (actor != null) _occupancy?.UnregisterActor(actor.AgentId);
        }
    }
}

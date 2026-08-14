using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public interface IOfficeRuntimeFurnitureCapabilityQuery
    {
        IReadOnlyList<OfficeFurnitureCapabilityCandidate> FindAvailableForAgent(
            OfficeFurnitureCapability capability,
            string agentId,
            OfficeGridCoordinate startCell,
            float agentRadius = OfficeRuntimeAgent.DefaultRadius);
    }

    /// <summary>
    /// Read-only integration surface for the stamina/needs branch. It combines the data catalog
    /// with the live path component and current interaction reservations without owning recovery
    /// values, desire thresholds, or behavior scheduling.
    /// </summary>
    public sealed class OfficeRuntimeFurnitureCapabilityAdapter : IOfficeRuntimeFurnitureCapabilityQuery
    {
        private readonly StarterOfficeRuntimeBootstrap _runtime;
        private readonly GameState _state;

        public OfficeRuntimeFurnitureCapabilityAdapter(
            StarterOfficeRuntimeBootstrap runtime,
            GameState state)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IReadOnlyList<OfficeFurnitureCapabilityCandidate> FindAvailableForAgent(
            OfficeFurnitureCapability capability,
            string agentId,
            OfficeGridCoordinate startCell,
            float agentRadius = OfficeRuntimeAgent.DefaultRadius)
        {
            if (!_runtime.IsReady || _runtime.World == null)
                return Array.Empty<OfficeFurnitureCapabilityCandidate>();
            HashSet<OfficeGridCoordinate> reachable = _runtime.World.Paths.FindStaticallyReachableCells(
                agentId, startCell, radius: agentRadius);
            var query = new OfficeFurnitureCapabilityQuery(
                _state.OfficeGrid,
                _state.OfficeFurnitureInventory);
            return query.FindAvailable(
                capability,
                reachable.Contains,
                (instanceId, _) => HasCapacity(instanceId));
        }

        private bool HasCapacity(string instanceId)
        {
            OfficeFurnitureInstanceState instance =
                _state.OfficeFurnitureInventory.Find(instanceId);
            OfficeFurnitureDefinition definition =
                instance == null ? null : OfficeFurnitureCatalog.Find(instance.DefinitionId);
            if (definition == null || definition.Capacity <= 0) return false;
            int active = _runtime.World.Interactions?.ActiveHandles.Count(handle =>
                string.Equals(handle.FurnitureId, instanceId, StringComparison.Ordinal)) ?? 0;
            return active < definition.Capacity;
        }
    }
}

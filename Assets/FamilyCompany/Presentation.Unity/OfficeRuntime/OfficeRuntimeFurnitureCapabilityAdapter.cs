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
            string permittedSeatId = string.Empty;
            if (_runtime.World.Registry.TryGet(agentId ?? string.Empty, out OfficeRuntimeAgent actor) &&
                actor != null)
                permittedSeatId = actor.ActiveSeatId;
            HashSet<OfficeGridCoordinate> reachable = _runtime.World.Paths.FindStaticallyReachableCells(
                agentId, startCell, permittedSeatId, agentRadius);
            var query = new OfficeFurnitureCapabilityQuery(
                _state.OfficeGrid,
                _state.OfficeFurnitureInventory);
            return query.FindAvailable(
                capability,
                reachable.Contains,
                (instanceId, _) => HasCapacity(instanceId, agentId));
        }

        private bool HasCapacity(string instanceId, string agentId)
        {
            OfficeFurnitureInstanceState instance =
                _state.OfficeFurnitureInventory.Find(instanceId);
            OfficeFurnitureDefinition definition =
                instance == null ? null : OfficeFurnitureCatalog.Find(instance.DefinitionId);
            if (definition == null || definition.Capacity <= 0) return false;
            // A member may already own the correlated claim while the stamina simulation waits
            // for its next deterministic retry minute. That claim remains available to its owner;
            // only handles owned by other members consume capacity for this query.
            int active = _runtime.World.Interactions?.ActiveHandles.Count(handle =>
                string.Equals(handle.FurnitureId, instanceId, StringComparison.Ordinal) &&
                !string.Equals(handle.MemberId, agentId, StringComparison.Ordinal)) ?? 0;
            return active < definition.Capacity;
        }
    }
}

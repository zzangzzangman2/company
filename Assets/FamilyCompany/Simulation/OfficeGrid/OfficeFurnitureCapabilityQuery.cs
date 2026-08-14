using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public sealed class OfficeFurnitureCapabilityCandidate
    {
        internal OfficeFurnitureCapabilityCandidate(
            OfficeFurnitureInstanceState instance,
            OfficeFurnitureDefinition definition,
            IReadOnlyList<OfficeGridCoordinate> accessCells)
        {
            InstanceId = instance.InstanceId;
            DefinitionId = instance.DefinitionId;
            CapabilityTags = definition.Capabilities;
            Capacity = definition.Capacity;
            DesiredFacing = definition.DesiredFacing;
            AccessCells = accessCells;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public OfficeFurnitureCapability CapabilityTags { get; }
        public int Capacity { get; }
        public OfficeFurnitureFacing DesiredFacing { get; }
        public IReadOnlyList<OfficeGridCoordinate> AccessCells { get; }
    }

    public interface IReadOnlyOfficeFurnitureCapabilityQuery
    {
        IReadOnlyList<OfficeFurnitureCapabilityCandidate> FindAvailable(
            OfficeFurnitureCapability capability,
            Func<OfficeGridCoordinate, bool> isReachable = null,
            Func<string, OfficeGridCoordinate, bool> canClaim = null);
    }

    /// <summary>
    /// Capability-based discovery for needs/autonomy. Callers never depend on a magic instance ID
    /// such as "water-cooler-1"; they receive every currently placed, reachable, claimable option.
    /// Recovery amounts and needs policy deliberately remain outside this service.
    /// </summary>
    public sealed class OfficeFurnitureCapabilityQuery : IReadOnlyOfficeFurnitureCapabilityQuery
    {
        private readonly OfficeGrid _grid;
        private readonly OfficeFurnitureInventoryState _inventory;

        public OfficeFurnitureCapabilityQuery(
            OfficeGrid grid,
            OfficeFurnitureInventoryState inventory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public IReadOnlyList<OfficeFurnitureCapabilityCandidate> FindAvailable(
            OfficeFurnitureCapability capability,
            Func<OfficeGridCoordinate, bool> isReachable = null,
            Func<string, OfficeGridCoordinate, bool> canClaim = null)
        {
            if (capability == OfficeFurnitureCapability.None) return Array.Empty<OfficeFurnitureCapabilityCandidate>();
            var placedById = _grid.Furniture.ToDictionary(item => item.FurnitureId, StringComparer.Ordinal);
            var result = new List<OfficeFurnitureCapabilityCandidate>();
            foreach (OfficeFurnitureInstanceState instance in _inventory.Instances)
            {
                if (instance.PlacementState != OfficeFurniturePlacementState.Placed ||
                    !placedById.TryGetValue(instance.InstanceId, out PlacedOfficeFurniture placed)) continue;
                OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(instance.DefinitionId);
                if (definition == null || !definition.HasCapability(capability)) continue;
                IReadOnlyList<OfficeGridCoordinate> access = OfficeLayoutEditRules.AccessCells(_grid, placed)
                    .Where(cell => isReachable == null || isReachable(cell))
                    .Where(cell => canClaim == null || canClaim(instance.InstanceId, cell))
                    .ToList();
                if (access.Count == 0) continue;
                result.Add(new OfficeFurnitureCapabilityCandidate(instance, definition, access));
            }
            return result.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToList();
        }
    }
}

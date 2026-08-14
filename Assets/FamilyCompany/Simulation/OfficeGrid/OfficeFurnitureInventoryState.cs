using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeFurniturePlacementState
    {
        Placed = 0,
        Stored = 1
    }

    public enum OfficeFurniturePurchaseBasisState
    {
        LegacyIncluded = 0,
        Purchased = 1
    }

    public sealed class OfficeFurnitureInstanceState
    {
        public OfficeFurnitureInstanceState(
            string instanceId,
            string definitionId,
            OfficeFurniturePlacementState placementState,
            OfficeGridCoordinate gridOrigin,
            OfficeFurnitureFacing rotation,
            OfficeFurniturePurchaseBasisState purchaseBasisState,
            long purchaseBasisWon,
            long acquiredMinute,
            string purchaseTransactionId)
        {
            InstanceId = Required(instanceId, nameof(instanceId));
            DefinitionId = Required(definitionId, nameof(definitionId));
            if (!Enum.IsDefined(typeof(OfficeFurniturePlacementState), placementState))
                throw new ArgumentOutOfRangeException(nameof(placementState));
            if (!Enum.IsDefined(typeof(OfficeFurnitureFacing), rotation))
                throw new ArgumentOutOfRangeException(nameof(rotation));
            if (!Enum.IsDefined(typeof(OfficeFurniturePurchaseBasisState), purchaseBasisState))
                throw new ArgumentOutOfRangeException(nameof(purchaseBasisState));
            if (purchaseBasisWon < 0) throw new ArgumentOutOfRangeException(nameof(purchaseBasisWon));
            if (acquiredMinute < 0) throw new ArgumentOutOfRangeException(nameof(acquiredMinute));
            PlacementState = placementState;
            GridOrigin = gridOrigin;
            Rotation = rotation;
            PurchaseBasisState = purchaseBasisState;
            PurchaseBasisWon = purchaseBasisWon;
            AcquiredMinute = acquiredMinute;
            PurchaseTransactionId = (purchaseTransactionId ?? string.Empty).Trim();
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public OfficeFurniturePlacementState PlacementState { get; }
        public OfficeGridCoordinate GridOrigin { get; }
        public OfficeFurnitureFacing Rotation { get; }
        public OfficeFurniturePurchaseBasisState PurchaseBasisState { get; }
        public long PurchaseBasisWon { get; }
        public long AcquiredMinute { get; }
        public string PurchaseTransactionId { get; }

        public OfficeFurnitureInstanceState WithPlacement(
            OfficeFurniturePlacementState state,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing rotation) =>
            new OfficeFurnitureInstanceState(
                InstanceId, DefinitionId, state, origin, rotation, PurchaseBasisState,
                PurchaseBasisWon, AcquiredMinute, PurchaseTransactionId);

        private static string Required(string value, string parameter)
        {
            string canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("ID is required.", parameter);
            return canonical;
        }
    }

    public sealed class OfficeFurnitureInventoryState
    {
        private readonly ReadOnlyCollection<OfficeFurnitureInstanceState> _instances;

        public OfficeFurnitureInventoryState(IEnumerable<OfficeFurnitureInstanceState> instances = null)
        {
            var values = instances == null
                ? new List<OfficeFurnitureInstanceState>()
                : new List<OfficeFurnitureInstanceState>(instances);
            if (values.Any(item => item == null)) throw new ArgumentException("Furniture inventory contains null.");
            if (values.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new ArgumentException("Furniture instance IDs must be unique.");
            _instances = values.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public IReadOnlyList<OfficeFurnitureInstanceState> Instances => _instances;

        public OfficeFurnitureInstanceState Find(string instanceId) =>
            _instances.FirstOrDefault(item => string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal));

        public int CountOwned(string definitionId) =>
            _instances.Count(item => string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal));

        public int CountPlaced(string definitionId) =>
            _instances.Count(item => item.PlacementState == OfficeFurniturePlacementState.Placed &&
                                     string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal));

        public OfficeFurnitureInventoryState Add(OfficeFurnitureInstanceState instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (Find(instance.InstanceId) != null)
                throw new InvalidOperationException("Duplicate furniture instance ID: " + instance.InstanceId);
            return new OfficeFurnitureInventoryState(_instances.Concat(new[] { instance }));
        }

        public OfficeFurnitureInventoryState Replace(OfficeFurnitureInstanceState instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (Find(instance.InstanceId) == null)
                throw new KeyNotFoundException("Unknown furniture instance: " + instance.InstanceId);
            return new OfficeFurnitureInventoryState(_instances.Select(item =>
                string.Equals(item.InstanceId, instance.InstanceId, StringComparison.Ordinal) ? instance : item));
        }

        public OfficeFurnitureInventoryState Remove(string instanceId)
        {
            if (Find(instanceId) == null) throw new KeyNotFoundException("Unknown furniture instance: " + instanceId);
            return new OfficeFurnitureInventoryState(
                _instances.Where(item => !string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal)));
        }

        public static OfficeFurnitureInventoryState MigrateFromGrid(OfficeGrid grid, long elapsedMinute = 0)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return new OfficeFurnitureInventoryState(grid.Furniture
                .Where(item => OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true)
                .Select(item =>
                {
                    OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(item.KindId);
                    return new OfficeFurnitureInstanceState(
                        item.FurnitureId,
                        item.KindId,
                        OfficeFurniturePlacementState.Placed,
                        item.Origin,
                        item.Facing,
                        OfficeFurniturePurchaseBasisState.LegacyIncluded,
                        OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon),
                        Math.Max(0, elapsedMinute),
                        string.Empty);
                }));
        }
    }
}

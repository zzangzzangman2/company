using System;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Save.OfficeFurniture
{
    public static class OfficeFurnitureInventorySaveAdapter
    {
        public static OfficeFurnitureInventorySaveDto ToDto(OfficeFurnitureInventoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new OfficeFurnitureInventorySaveDto
            {
                instances = state.Instances.Select(item => new OfficeFurnitureInstanceSaveDto
                {
                    instanceId = item.InstanceId,
                    definitionId = item.DefinitionId,
                    placementState = (int)item.PlacementState,
                    gridX = item.GridOrigin.X,
                    gridY = item.GridOrigin.Y,
                    rotation = (int)item.Rotation,
                    purchaseBasisState = (int)item.PurchaseBasisState,
                    purchaseBasisWon = item.PurchaseBasisWon,
                    acquiredMinute = item.AcquiredMinute,
                    purchaseTransactionId = item.PurchaseTransactionId
                }).ToList()
            };
        }

        public static OfficeFurnitureInventoryState Restore(OfficeFurnitureInventorySaveDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.schemaVersion != OfficeFurnitureInventorySaveDto.CurrentSchemaVersion)
                throw new InvalidOperationException("Unsupported office furniture inventory schema: " + dto.schemaVersion);
            if (dto.instances == null)
                throw new InvalidOperationException("Office furniture inventory instances are missing.");
            return new OfficeFurnitureInventoryState(dto.instances.Select(item =>
            {
                if (item == null) throw new InvalidOperationException("Office furniture inventory contains null.");
                return new OfficeFurnitureInstanceState(
                    item.instanceId,
                    item.definitionId,
                    (OfficeFurniturePlacementState)item.placementState,
                    new OfficeGridCoordinate(item.gridX, item.gridY),
                    (OfficeFurnitureFacing)item.rotation,
                    (OfficeFurniturePurchaseBasisState)item.purchaseBasisState,
                    item.purchaseBasisWon,
                    item.acquiredMinute,
                    item.purchaseTransactionId);
            }));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    /// <summary>
    /// Engine-independent purchase/layout/save regression suite, exposed as a Unity batch entry.
    /// </summary>
    public static class OfficeFurnitureBuildSystemValidation
    {
        [MenuItem("Family Company/Validate/Office Furniture Build System")]
        public static void Run()
        {
            var failures = new List<string>();
            GameState state = PrototypeStateFactory.Create(20000103);
            string[] familyIds = state.Family.Members.Select(item => item.MemberId).ToArray();
            int[] energy = state.Family.Members.Select(item => item.Energy).ToArray();
            string originalHash = state.OfficeGrid.ComputeLayoutHash();
            ValidateCanonicalGeometry(failures);
            ValidateBlockedSocketPlacement(failures);
            ValidateEveryCatalogTransaction(failures);
            ValidateInsufficientFundsAndLegacyMigration(failures);

            OfficeLayoutEditResult rotationPreview =
                OfficeLayoutEditRules.RotateFurniture(state.OfficeGrid, "desk_player");
            Require(failures, rotationPreview.Success, "existing desk rotation preview");
            Require(failures, state.OfficeGrid.ComputeLayoutHash() == originalHash, "preview cancel immutable");
            for (int turn = 0; turn < 4; turn++)
                Require(failures,
                    OfficeFurnitureTransactionService.Rotate(state, "desk_player").Success,
                    "existing desk rotation " + (turn + 1));
            Require(failures, state.OfficeGrid.ComputeLayoutHash() == originalHash, "four rotations round trip");

            long beforeInvalid = state.Company.CashWon;
            OfficeFurnitureCommandResult invalid = OfficeFurnitureTransactionService.PurchaseAndPlace(
                state, "qa-invalid", "qa-invalid", OfficeGridLayouts.WaterDispenserKind,
                new OfficeGridCoordinate(-1, -1), OfficeFurnitureFacing.SouthEast);
            Require(failures, !invalid.Success, "invalid purchase rejected");
            Require(failures, state.Company.CashWon == beforeInvalid, "invalid purchase zero charge");

            var ids = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string kind in new[]
                     {
                         OfficeGridLayouts.DeskWithPcKind,
                         OfficeGridLayouts.WaterDispenserKind,
                         OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId
                     })
            {
                ids[kind] = new List<string>();
                for (int index = 0; index < 2; index++)
                {
                    string id = "qa_" + kind + "_" + index;
                    OfficeFurnitureCommandResult purchase = PurchaseAtFirstValid(state, id, kind);
                    Require(failures, purchase.Success, kind + " purchase " + index + ": " + purchase.Message);
                    ids[kind].Add(id);
                    long cash = state.Company.CashWon;
                    OfficeFurnitureInstanceState owned = state.OfficeFurnitureInventory.Find(id);
                    OfficeFurnitureCommandResult duplicate = OfficeFurnitureTransactionService.PurchaseAndPlace(
                        state, "qa-buy:" + id, id, kind, owned.GridOrigin, owned.Rotation);
                    Require(failures, duplicate.Success && duplicate.AlreadyApplied, "idempotent " + id);
                    Require(failures, state.Company.CashWon == cash, "duplicate zero charge " + id);
                }
            }

            var query = new OfficeFurnitureCapabilityQuery(state.OfficeGrid, state.OfficeFurnitureInventory);
            Require(failures, query.FindAvailable(OfficeFurnitureCapability.WaterSource).Count >= 3,
                "multiple WaterSource instances");
            Require(failures, query.FindAvailable(OfficeFurnitureCapability.DrinkVending).Count == 2,
                "multiple DrinkVending instances");
            Require(failures, query.FindAvailable(OfficeFurnitureCapability.RestSeat).Count >= 1,
                "RestSeat instance");

            string water = ids[OfficeGridLayouts.WaterDispenserKind][0];
            Require(failures,
                !OfficeFurnitureTransactionService.Store(state, water, _ => true).Success,
                "claimed storage refused");
            long beforeStore = state.Company.CashWon;
            Require(failures, OfficeFurnitureTransactionService.Store(state, water).Success, "store succeeds");
            Require(failures, state.Company.CashWon == beforeStore, "store free");
            Require(failures, PlaceStoredAtFirstValid(state, water).Success, "stored placement succeeds");
            OfficeFurnitureInstanceState saleBasis = state.OfficeFurnitureInventory.Find(water);
            long refund = OfficeFurnitureEconomyConfig.ResaleValue(
                saleBasis.PurchaseBasisWon,
                OfficeFurnitureCatalog.Require(saleBasis.DefinitionId).ResaleRateBasisPoints);
            OfficeFurnitureCommandResult sold = OfficeFurnitureTransactionService.Sell(
                state, "qa-sell:" + water, water);
            Require(failures, sold.Success && sold.RefundedWon == refund, "sale refund exact");
            long afterSale = state.Company.CashWon;
            OfficeFurnitureCommandResult soldTwice = OfficeFurnitureTransactionService.Sell(
                state, "qa-sell:" + water, water);
            Require(failures, soldTwice.AlreadyApplied && state.Company.CashWon == afterSale,
                "sale idempotent");

            string storedAcrossSave = ids[OfficeGridLayouts.WaterDispenserKind][1];
            Require(failures,
                OfficeFurnitureTransactionService.Store(state, storedAcrossSave).Success,
                "stored inventory prepared for save round trip");

            Require(failures,
                OfficeLayoutEditRules.PlaceFurniture(
                    state.OfficeGrid, "qa-boundary", OfficeGridLayouts.PottedPlantKind,
                    new OfficeGridCoordinate(0, 0), OfficeFurnitureFacing.SouthEast).Failure ==
                OfficeLayoutEditFailure.NotOnFloor,
                "outer wall/floor boundary rejected");
            Require(failures,
                OfficeLayoutEditRules.PlaceFurniture(
                    state.OfficeGrid, "qa-door", OfficeGridLayouts.PottedPlantKind,
                    OfficeLayoutEditRules.CanonicalInteriorEntrance,
                    OfficeFurnitureFacing.SouthEast).Failure == OfficeLayoutEditFailure.EntranceBlocked,
                "entrance blocking rejected");

            GameSaveDto dto = GameSaveMapper.ToDto(state);
            string json = JsonUtility.ToJson(dto);
            GameState restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            Require(failures, dto.schemaVersion == 10, "top-level schema v10");
            Require(failures, restored.Company.CashWon == state.Company.CashWon, "money round trip");
            Require(failures,
                restored.OfficeGrid.ComputeLayoutHash() == state.OfficeGrid.ComputeLayoutHash(),
                "layout round trip");
            Require(failures,
                restored.OfficeFurnitureInventory.Instances.Select(InventorySignature)
                    .SequenceEqual(state.OfficeFurnitureInventory.Instances.Select(InventorySignature)),
                "inventory placement/basis/IDs round trip");
            Require(failures, state.Family.Members.Select(item => item.MemberId).SequenceEqual(familyIds),
                "family identity preserved");
            Require(failures, state.Family.Members.Select(item => item.Energy).SequenceEqual(energy),
                "energy preserved");

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "OFFICE_FURNITURE_BUILD_SYSTEM_QA: FAIL | " + string.Join(" | ", failures.Take(12)));
            Debug.Log(
                "OFFICE_FURNITURE_BUILD_SYSTEM_QA: PASS | geometry=13x4 | catalogTransactions=13 | " +
                "bought=6 | schema=8+v7-migration | family=4 | cash=" +
                state.Company.CashWon);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                EditorApplication.Exit(1);
            }
        }

        private static OfficeFurnitureCommandResult PurchaseAtFirstValid(
            GameState state,
            string id,
            string kind)
        {
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            for (int y = 1; y < state.OfficeGrid.Height - 1; y++)
            for (int x = 1; x < state.OfficeGrid.Width - 1; x++)
            {
                var origin = new OfficeGridCoordinate(x, y);
                if (!OfficeLayoutEditRules.PlaceFurniture(
                        state.OfficeGrid, "preview_" + id, kind, origin, facing).Success) continue;
                return OfficeFurnitureTransactionService.PurchaseAndPlace(
                    state, "qa-buy:" + id, id, kind, origin, facing);
            }
            throw new InvalidOperationException("No valid placement for " + kind);
        }

        private static void ValidateCanonicalGeometry(ICollection<string> failures)
        {
            var query = (IReadOnlyOfficeFurnitureGeometryQuery)OfficeFurnitureGeometryQuery.Shared;
            foreach (OfficeFurnitureDefinition definition in OfficeFurnitureCatalog.Purchasable)
            {
                string definitionId = definition.DefinitionId;
                Require(failures, definition.Geometry != null, definitionId + " geometry exists");
                Require(failures, definition.Geometry.Profiles.Count == 4, definitionId + " four profiles");
                foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
                {
                    Require(failures,
                        query.TryResolve(definitionId, new OfficeGridCoordinate(10, 10), facing,
                            out OfficeFurnitureGeometrySnapshot snapshot),
                        definitionId + "/" + facing + " query");
                    if (snapshot == null) continue;
                    OfficeFurnitureGeometryProfile profile = snapshot.Profile;
                    OfficeGridCoordinate footprint = definition.FootprintFor(facing);
                    Require(failures,
                        profile.FootprintWidth == footprint.X && profile.FootprintHeight == footprint.Y,
                        definitionId + "/" + facing + " footprint");
                    Require(failures,
                        profile.BakedSolidGroundRows.Count == footprint.Y *
                        OfficeFurnitureGeometryProfile.SubcellsPerCell,
                        definitionId + "/" + facing + " row count");
                    Require(failures,
                        profile.BakedSolidGroundRows.All(row => row.Length == footprint.X *
                            OfficeFurnitureGeometryProfile.SubcellsPerCell),
                        definitionId + "/" + facing + " row width");
                    Require(failures,
                        profile.SolidSubcellCount > 0 &&
                        profile.SolidSubcellCount < footprint.X * footprint.Y *
                        OfficeFurnitureGeometryProfile.SubcellsPerCell *
                        OfficeFurnitureGeometryProfile.SubcellsPerCell,
                        definitionId + "/" + facing + " floor-contact mask is not sprite/full-tile alpha");
                    Require(failures,
                        !profile.SolidGroundPolygon.SequenceEqual(
                            profile.VisualOcclusion.GroundProjectionPolygon),
                        definitionId + "/" + facing + " collision/occlusion separation");
                    Require(failures,
                        profile.VisualOcclusion.GroundProjectionPolygonPixels.Count >= 4 &&
                        profile.VisualOcclusion.HeightPixels > 0,
                        definitionId + "/" + facing + " pixel occlusion envelope");
                    if (definition.AccessPolicy != OfficeFurnitureAccessPolicy.None)
                        Require(failures, profile.InteractionAccessSockets.Count > 0,
                            definitionId + "/" + facing + " access anchors");
                    if (!definition.HasCapability(OfficeFurnitureCapability.Seat)) continue;
                    for (var slot = 0; slot < Math.Max(1, definition.Capacity); slot++)
                    {
                        OfficeFurnitureGeometrySocketKind[] kinds = profile.SeatEgressSockets
                            .Where(item => item.SlotIndex == slot)
                            .Select(item => item.Kind)
                            .Distinct()
                            .ToArray();
                        Require(failures,
                            kinds.Contains(OfficeFurnitureGeometrySocketKind.SeatEgressFront) &&
                            kinds.Contains(OfficeFurnitureGeometrySocketKind.SeatEgressLeft) &&
                            kinds.Contains(OfficeFurnitureGeometrySocketKind.SeatEgressRight),
                            definitionId + "/" + facing + "/seat" + slot + " front-left-right egress");
                    }
                }
            }
        }

        private static void ValidateEveryCatalogTransaction(ICollection<string> failures)
        {
            GameState state = PrototypeStateFactory.Create(20000104);
            foreach (OfficeFurnitureDefinition definition in OfficeFurnitureCatalog.Purchasable)
            {
                string instanceId = "qa_catalog_" + definition.DefinitionId;
                FindRotateThenMovePlan(
                    state.OfficeGrid,
                    instanceId,
                    definition.DefinitionId,
                    out OfficeGridCoordinate origin,
                    out OfficeFurnitureFacing facing,
                    out OfficeGridCoordinate moveDelta);

                long cashBefore = state.Company.CashWon;
                OfficeFurnitureCommandResult bought = OfficeFurnitureTransactionService.PurchaseAndPlace(
                    state,
                    "qa-catalog-buy:" + definition.DefinitionId,
                    instanceId,
                    definition.DefinitionId,
                    origin,
                    facing);
                long price = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
                Require(failures,
                    bought.Success && bought.ChargedWon == price && state.Company.CashWon == cashBefore - price,
                    definition.DefinitionId + " exact purchase debit");

                OfficeFurnitureCommandResult rotated = OfficeFurnitureTransactionService.Rotate(state, instanceId);
                Require(failures, rotated.Success, definition.DefinitionId + " 90-degree rotation");
                OfficeFurnitureInstanceState afterRotation = state.OfficeFurnitureInventory.Find(instanceId);
                Require(failures,
                    afterRotation != null && afterRotation.Rotation ==
                    OfficeLayoutEditRules.QuarterTurnClockwise(facing),
                    definition.DefinitionId + " inventory rotation sync");

                OfficeFurnitureCommandResult moved = OfficeFurnitureTransactionService.Move(
                    state,
                    instanceId,
                    new OfficeGridCoordinate(
                        afterRotation.GridOrigin.X + moveDelta.X,
                        afterRotation.GridOrigin.Y + moveDelta.Y));
                Require(failures, moved.Success, definition.DefinitionId + " move");
                OfficeFurnitureInstanceState afterMove = state.OfficeFurnitureInventory.Find(instanceId);
                Require(failures,
                    afterMove != null && state.OfficeGrid.Furniture.Any(item =>
                        string.Equals(item.FurnitureId, instanceId, StringComparison.Ordinal) &&
                        item.Origin.Equals(afterMove.GridOrigin) && item.Facing == afterMove.Rotation),
                    definition.DefinitionId + " grid/inventory placement sync");

                long cashBeforeStorage = state.Company.CashWon;
                Require(failures,
                    OfficeFurnitureTransactionService.Store(state, instanceId).Success &&
                    state.Company.CashWon == cashBeforeStorage &&
                    state.OfficeGrid.Furniture.All(item =>
                        !string.Equals(item.FurnitureId, instanceId, StringComparison.Ordinal)),
                    definition.DefinitionId + " free storage");
                Require(failures,
                    PlaceStoredAtFirstValid(state, instanceId).Success,
                    definition.DefinitionId + " stored placement");

                OfficeFurnitureInstanceState saleBasis = state.OfficeFurnitureInventory.Find(instanceId);
                long refund = OfficeFurnitureEconomyConfig.ResaleValue(
                    saleBasis.PurchaseBasisWon,
                    definition.ResaleRateBasisPoints);
                long cashBeforeSale = state.Company.CashWon;
                OfficeFurnitureCommandResult sold = OfficeFurnitureTransactionService.Sell(
                    state,
                    "qa-catalog-sell:" + definition.DefinitionId,
                    instanceId);
                Require(failures,
                    sold.Success && sold.RefundedWon == refund &&
                    state.Company.CashWon == cashBeforeSale + refund &&
                    state.OfficeFurnitureInventory.Find(instanceId) == null,
                    definition.DefinitionId + " exact sale refund");
            }
        }

        private static void ValidateInsufficientFundsAndLegacyMigration(ICollection<string> failures)
        {
            GameState poor = PrototypeStateFactory.Create(20000105);
            poor.Company.PayOperatingExpense(
                "qa-drain-cash",
                poor.Time.ElapsedMinutes,
                poor.Company.CashWon - 1,
                "가구 자금 부족 검증");
            long cash = poor.Company.CashWon;
            OfficeFurnitureCommandResult refused = PurchaseAtFirstValid(
                poor,
                "qa-poor-plant",
                OfficeGridLayouts.PottedPlantKind);
            Require(failures,
                !refused.Success && refused.Failure == OfficeFurnitureCommandFailure.InsufficientFunds,
                "insufficient funds refused");
            Require(failures,
                poor.Company.CashWon == cash && poor.OfficeFurnitureInventory.Find("qa-poor-plant") == null,
                "insufficient funds is atomic");

            GameSaveDto legacy = GameSaveMapper.ToDto(PrototypeStateFactory.Create(20000106));
            legacy.schemaVersion = 7;
            legacy.officeFurnitureInventory = null;
            GameState migrated = GameSaveMapper.FromDto(legacy);
            int editablePlaced = migrated.OfficeGrid.Furniture.Count(item =>
                OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true);
            Require(failures,
                migrated.OfficeFurnitureInventory.Instances.Count == editablePlaced &&
                migrated.OfficeFurnitureInventory.Instances.All(item =>
                    item.PurchaseBasisState == OfficeFurniturePurchaseBasisState.LegacyIncluded &&
                    item.PlacementState == OfficeFurniturePlacementState.Placed),
                "schema v7 grid migrates to legacy-included inventory");
        }

        private static void FindRotateThenMovePlan(
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid,
            string instanceId,
            string definitionId,
            out OfficeGridCoordinate origin,
            out OfficeFurnitureFacing facing,
            out OfficeGridCoordinate moveDelta)
        {
            OfficeGridCoordinate[] deltas =
            {
                new OfficeGridCoordinate(1, 0),
                new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1),
                new OfficeGridCoordinate(0, -1)
            };
            foreach (OfficeFurnitureFacing candidateFacing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            for (int y = 1; y < grid.Height - 1; y++)
            for (int x = 1; x < grid.Width - 1; x++)
            {
                var candidateOrigin = new OfficeGridCoordinate(x, y);
                OfficeLayoutEditResult placed = OfficeLayoutEditRules.PlaceFurniture(
                    grid, instanceId, definitionId, candidateOrigin, candidateFacing);
                if (!placed.Success) continue;
                OfficeLayoutEditResult rotated = OfficeLayoutEditRules.RotateFurniture(placed.Grid, instanceId);
                if (!rotated.Success) continue;
                foreach (OfficeGridCoordinate delta in deltas)
                {
                    if (!OfficeLayoutEditRules.MoveFurniture(
                            rotated.Grid, instanceId, delta.X, delta.Y).Success) continue;
                    origin = candidateOrigin;
                    facing = candidateFacing;
                    moveDelta = delta;
                    return;
                }
            }
            throw new InvalidOperationException(
                "No purchase/rotate/move placement plan for " + definitionId);
        }

        private static void ValidateBlockedSocketPlacement(ICollection<string> failures)
        {
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid interactionBlocked = BuildSocketFixture(
                new[]
                {
                    new OfficeGridCoordinate(5, 4), new OfficeGridCoordinate(6, 5),
                    new OfficeGridCoordinate(5, 6), new OfficeGridCoordinate(4, 5)
                });
            OfficeLayoutEditResult interaction = OfficeLayoutEditRules.PlaceFurniture(
                interactionBlocked,
                "qa_blocked_vending",
                OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId,
                new OfficeGridCoordinate(5, 5),
                OfficeFurnitureFacing.SouthEast);
            Require(failures,
                !interaction.Success && interaction.Failure == OfficeLayoutEditFailure.AccessBlocked,
                "interaction with no safe access is rejected");

            FamilyCompany.Simulation.OfficeLayout.OfficeGrid egressBlocked = BuildSocketFixture(
                new[]
                {
                    new OfficeGridCoordinate(5, 4), new OfficeGridCoordinate(6, 4),
                    new OfficeGridCoordinate(4, 5), new OfficeGridCoordinate(7, 5)
                });
            OfficeLayoutEditResult egress = OfficeLayoutEditRules.PlaceFurniture(
                egressBlocked,
                "qa_blocked_sofa",
                OfficeGridLayouts.SofaKind,
                new OfficeGridCoordinate(5, 5),
                OfficeFurnitureFacing.SouthEast);
            Require(failures,
                !egress.Success && egress.Failure == OfficeLayoutEditFailure.AccessBlocked &&
                egress.Message.Contains("egress"),
                "seat with no safe front/left/right egress is rejected");
        }

        private static FamilyCompany.Simulation.OfficeLayout.OfficeGrid BuildSocketFixture(
            IEnumerable<OfficeGridCoordinate> blockers)
        {
            const int width = 12;
            const int height = 12;
            var floor = Enumerable.Repeat(OfficeFloorTileKind.WarmWoodA, width * height).ToArray();
            var walkable = Enumerable.Repeat(true, width * height).ToArray();
            var furniture = new List<PlacedOfficeFurniture>();
            var index = 0;
            foreach (OfficeGridCoordinate cell in blockers)
            {
                walkable[cell.Y * width + cell.X] = false;
                furniture.Add(new PlacedOfficeFurniture(
                    "qa_static_" + index++,
                    OfficeGridLayouts.EntranceWallKind,
                    cell,
                    1,
                    1,
                    OfficeFurnitureFacing.SouthEast,
                    true));
            }
            return new FamilyCompany.Simulation.OfficeLayout.OfficeGrid(
                width, height, floor, walkable, furniture);
        }

        private static OfficeFurnitureCommandResult PlaceStoredAtFirstValid(GameState state, string id)
        {
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(id);
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            for (int y = 1; y < state.OfficeGrid.Height - 1; y++)
            for (int x = 1; x < state.OfficeGrid.Width - 1; x++)
            {
                var origin = new OfficeGridCoordinate(x, y);
                if (!OfficeLayoutEditRules.PlaceFurniture(
                        state.OfficeGrid, id, instance.DefinitionId, origin, facing).Success) continue;
                return OfficeFurnitureTransactionService.PlaceStored(state, id, origin, facing);
            }
            throw new InvalidOperationException("No valid stored placement for " + id);
        }

        private static void Require(ICollection<string> failures, bool condition, string label)
        {
            if (!condition) failures.Add(label);
        }

        private static string InventorySignature(OfficeFurnitureInstanceState item) =>
            string.Join("|", item.InstanceId, item.DefinitionId, (int)item.PlacementState,
                item.GridOrigin, (int)item.Rotation, (int)item.PurchaseBasisState,
                item.PurchaseBasisWon, item.AcquiredMinute, item.PurchaseTransactionId);
    }
}

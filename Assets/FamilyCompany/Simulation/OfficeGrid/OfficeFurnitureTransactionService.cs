using System;
using System.Linq;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeFurnitureCommandFailure
    {
        None = 0,
        UnknownDefinition = 1,
        UnknownInstance = 2,
        DuplicateInstance = 3,
        InsufficientFunds = 4,
        MaximumOwned = 5,
        PlacementInvalid = 6,
        InUse = 7,
        IdempotencyConflict = 8,
        NotStored = 9,
        NotPlaced = 10
    }

    public sealed class OfficeFurnitureCommandResult
    {
        private OfficeFurnitureCommandResult(
            bool success,
            bool alreadyApplied,
            OfficeFurnitureCommandFailure failure,
            OfficeLayoutEditFailure placementFailure,
            string message,
            string instanceId,
            long chargedWon,
            long refundedWon,
            long balanceWon)
        {
            Success = success;
            AlreadyApplied = alreadyApplied;
            Failure = failure;
            PlacementFailure = placementFailure;
            Message = message ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            ChargedWon = chargedWon;
            RefundedWon = refundedWon;
            BalanceWon = balanceWon;
        }

        public bool Success { get; }
        public bool AlreadyApplied { get; }
        public OfficeFurnitureCommandFailure Failure { get; }
        public OfficeLayoutEditFailure PlacementFailure { get; }
        public string Message { get; }
        public string InstanceId { get; }
        public long ChargedWon { get; }
        public long RefundedWon { get; }
        public long BalanceWon { get; }

        internal static OfficeFurnitureCommandResult Ok(
            GameState state,
            string instanceId,
            long chargedWon = 0,
            long refundedWon = 0,
            bool alreadyApplied = false) =>
            new OfficeFurnitureCommandResult(
                true, alreadyApplied, OfficeFurnitureCommandFailure.None,
                OfficeLayoutEditFailure.None, string.Empty, instanceId,
                chargedWon, refundedWon, state.Company.CashWon);

        internal static OfficeFurnitureCommandResult Fail(
            GameState state,
            OfficeFurnitureCommandFailure failure,
            string message,
            string instanceId = "",
            OfficeLayoutEditFailure placementFailure = OfficeLayoutEditFailure.None) =>
            new OfficeFurnitureCommandResult(
                false, false, failure, placementFailure, message, instanceId,
                0, 0, state.Company.CashWon);
    }

    /// <summary>
    /// The only write API for owned furniture. Candidate layout and inventory values are built
    /// first. A purchase/sale ledger entry is posted only after every placement rule succeeds,
    /// then both immutable office values are swapped into GameState together.
    /// </summary>
    public static class OfficeFurnitureTransactionService
    {
        public const string WorkstationChairInstanceSuffix = ":chair";

        public static string WorkstationChairInstanceId(string workSurfaceInstanceId) =>
            RequiredId(workSurfaceInstanceId, nameof(workSurfaceInstanceId)) + WorkstationChairInstanceSuffix;

        public static OfficeFurnitureCommandResult PurchaseAndPlace(
            GameState state,
            string commandId,
            string instanceId,
            string definitionId,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing rotation)
        {
            Required(state, nameof(state));
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(definitionId);
            if (definition == null || !definition.IsPurchasable)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownDefinition, "구매할 수 없는 가구입니다.", instanceId);

            if (state.Company.HasTransaction(commandId))
            {
                OfficeFurnitureInstanceState applied = state.OfficeFurnitureInventory.Find(instanceId);
                if (applied != null && Same(applied.PurchaseTransactionId, commandId) &&
                    Same(applied.DefinitionId, definition.DefinitionId))
                    return OfficeFurnitureCommandResult.Ok(
                        state, instanceId, applied.PurchaseBasisWon, alreadyApplied: true);
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.IdempotencyConflict,
                    "같은 거래 ID가 다른 구매에 이미 사용되었습니다.", instanceId);
            }
            if (state.OfficeFurnitureInventory.Find(instanceId) != null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.DuplicateInstance,
                    "같은 가구 인스턴스 ID가 이미 있습니다.", instanceId);
            if (definition.MaximumOwned > 0 &&
                state.OfficeFurnitureInventory.CountOwned(definition.DefinitionId) >= definition.MaximumOwned)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.MaximumOwned, "최대 보유 수량에 도달했습니다.", instanceId);

            long priceWon = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
            if (state.Company.CashWon < priceWon)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.InsufficientFunds, "회사 자금이 부족합니다.", instanceId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.PlaceFurniture(
                state.OfficeGrid, instanceId, definition.DefinitionId, origin, rotation);
            if (!edit.Success) return PlacementFail(state, edit, instanceId);

            var instance = new OfficeFurnitureInstanceState(
                instanceId,
                definition.DefinitionId,
                OfficeFurniturePlacementState.Placed,
                origin,
                rotation,
                OfficeFurniturePurchaseBasisState.Purchased,
                priceWon,
                state.Time.ElapsedMinutes,
                commandId);
            OfficeFurnitureInventoryState inventory = state.OfficeFurnitureInventory.Add(instance);
            state.Company.PurchaseOfficeFurniture(
                RequiredId(commandId, nameof(commandId)),
                state.Time.ElapsedMinutes,
                priceWon,
                "사무 가구 구매: " + definition.KoreanDisplayName);
            state.ReplaceOfficeState(edit.Grid, inventory);
            return OfficeFurnitureCommandResult.Ok(state, instanceId, chargedWon: priceWon);
        }

        /// <summary>
        /// Purchases and places the production CRT desk, V31 open-back chair and usable seat as
        /// one ledger/layout/inventory transaction.  No money or partial inventory is published
        /// unless the complete four-direction workstation passes every placement/topology rule.
        /// </summary>
        public static OfficeFurnitureCommandResult PurchaseAndPlaceWorkstation(
            GameState state,
            string commandId,
            string workSurfaceInstanceId,
            OfficeGridCoordinate seatCell,
            OfficeFurnitureFacing deskFacing)
        {
            Required(state, nameof(state));
            string canonicalCommandId = RequiredId(commandId, nameof(commandId));
            string deskId = RequiredId(workSurfaceInstanceId, nameof(workSurfaceInstanceId));
            string chairId = WorkstationChairInstanceId(deskId);
            OfficeFurnitureDefinition deskDefinition =
                OfficeFurnitureCatalog.Require(OfficeGridLayouts.DeskWithPcKind);
            OfficeFurnitureDefinition chairDefinition =
                OfficeFurnitureCatalog.Require(OfficeGridLayouts.SwivelChairKind);

            if (state.Company.HasTransaction(canonicalCommandId))
            {
                OfficeFurnitureInstanceState appliedDesk = state.OfficeFurnitureInventory.Find(deskId);
                OfficeFurnitureInstanceState appliedChair = state.OfficeFurnitureInventory.Find(chairId);
                if (appliedDesk != null && appliedChair != null &&
                    Same(appliedDesk.PurchaseTransactionId, canonicalCommandId) &&
                    Same(appliedChair.PurchaseTransactionId, canonicalCommandId) &&
                    Same(appliedDesk.DefinitionId, deskDefinition.DefinitionId) &&
                    Same(appliedChair.DefinitionId, chairDefinition.DefinitionId))
                    return OfficeFurnitureCommandResult.Ok(
                        state,
                        deskId,
                        checked(appliedDesk.PurchaseBasisWon + appliedChair.PurchaseBasisWon),
                        alreadyApplied: true);
                return OfficeFurnitureCommandResult.Fail(
                    state,
                    OfficeFurnitureCommandFailure.IdempotencyConflict,
                    "같은 거래 ID가 다른 구매에 이미 사용되었습니다.",
                    deskId);
            }
            if (state.OfficeFurnitureInventory.Find(deskId) != null ||
                state.OfficeFurnitureInventory.Find(chairId) != null)
                return OfficeFurnitureCommandResult.Fail(
                    state,
                    OfficeFurnitureCommandFailure.DuplicateInstance,
                    "같은 책상·의자 세트 인스턴스 ID가 이미 있습니다.",
                    deskId);
            if ((deskDefinition.MaximumOwned > 0 &&
                 state.OfficeFurnitureInventory.CountOwned(deskDefinition.DefinitionId) >=
                 deskDefinition.MaximumOwned) ||
                (chairDefinition.MaximumOwned > 0 &&
                 state.OfficeFurnitureInventory.CountOwned(chairDefinition.DefinitionId) >=
                 chairDefinition.MaximumOwned))
                return OfficeFurnitureCommandResult.Fail(
                    state,
                    OfficeFurnitureCommandFailure.MaximumOwned,
                    "최대 보유 수량에 도달했습니다.",
                    deskId);

            long deskPriceWon = OfficeFurnitureEconomyConfig.GameplayPrice(deskDefinition.PurchasePriceWon);
            long chairPriceWon = OfficeFurnitureEconomyConfig.GameplayPrice(chairDefinition.PurchasePriceWon);
            long totalPriceWon = checked(deskPriceWon + chairPriceWon);
            if (state.Company.CashWon < totalPriceWon)
                return OfficeFurnitureCommandResult.Fail(
                    state,
                    OfficeFurnitureCommandFailure.InsufficientFunds,
                    "회사 자금이 부족합니다.",
                    deskId);

            string seatId = NextWorkstationSeatId(state, deskId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.PlaceWorkstation(
                state.OfficeGrid,
                deskId,
                chairId,
                seatId,
                seatCell,
                deskFacing);
            if (!edit.Success) return PlacementFail(state, edit, deskId);

            OfficeWorkstationPlacement placement = OfficeLayoutEditRules.CreateWorkstationPlacement(
                deskId,
                chairId,
                seatId,
                seatCell,
                deskFacing);
            long acquiredMinute = state.Time.ElapsedMinutes;
            var desk = new OfficeFurnitureInstanceState(
                deskId,
                deskDefinition.DefinitionId,
                OfficeFurniturePlacementState.Placed,
                placement.WorkSurface.Origin,
                placement.WorkSurface.Facing,
                OfficeFurniturePurchaseBasisState.Purchased,
                deskPriceWon,
                acquiredMinute,
                canonicalCommandId);
            var chair = new OfficeFurnitureInstanceState(
                chairId,
                chairDefinition.DefinitionId,
                OfficeFurniturePlacementState.Placed,
                placement.Chair.Origin,
                placement.Chair.Facing,
                OfficeFurniturePurchaseBasisState.Purchased,
                chairPriceWon,
                acquiredMinute,
                canonicalCommandId);
            var inventory = new OfficeFurnitureInventoryState(
                state.OfficeFurnitureInventory.Instances.Concat(new[] { desk, chair }));
            state.Company.PurchaseOfficeFurniture(
                canonicalCommandId,
                acquiredMinute,
                totalPriceWon,
                "사무 가구 구매: " + deskDefinition.KoreanDisplayName);
            state.ReplaceOfficeState(edit.Grid, inventory);
            return OfficeFurnitureCommandResult.Ok(state, deskId, chargedWon: totalPriceWon);
        }

        public static OfficeFurnitureCommandResult Move(
            GameState state,
            string instanceId,
            OfficeGridCoordinate destination,
            Func<string, bool> isInUse = null)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (instance.PlacementState != OfficeFurniturePlacementState.Placed)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.NotPlaced, "배치된 가구가 아닙니다.", instanceId);
            if (instance.GridOrigin.Equals(destination))
                return OfficeFurnitureCommandResult.Ok(state, instanceId, alreadyApplied: true);
            if (IsInUse(instanceId, isInUse)) return InUse(state, instanceId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.MoveFurniture(
                state.OfficeGrid,
                instanceId,
                destination.X - instance.GridOrigin.X,
                destination.Y - instance.GridOrigin.Y);
            if (!edit.Success) return PlacementFail(state, edit, instanceId);
            state.ReplaceOfficeState(edit.Grid, Synchronize(state.OfficeFurnitureInventory, edit.Grid));
            return OfficeFurnitureCommandResult.Ok(state, instanceId);
        }

        public static OfficeFurnitureCommandResult Relocate(
            GameState state,
            string instanceId,
            OfficeGridCoordinate destination,
            OfficeFurnitureFacing rotation,
            Func<string, bool> isInUse = null)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (instance.PlacementState != OfficeFurniturePlacementState.Placed)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.NotPlaced, "배치된 가구가 아닙니다.", instanceId);
            int turns = ((int)rotation - (int)instance.Rotation + 4) & 3;
            if (instance.GridOrigin.Equals(destination) && turns == 0)
                return OfficeFurnitureCommandResult.Ok(state, instanceId, alreadyApplied: true);
            if (IsInUse(instanceId, isInUse)) return InUse(state, instanceId);

            OfficeGrid candidate = state.OfficeGrid;
            if (!instance.GridOrigin.Equals(destination))
            {
                OfficeLayoutEditResult moved = OfficeLayoutEditRules.MoveFurniture(
                    candidate,
                    instanceId,
                    destination.X - instance.GridOrigin.X,
                    destination.Y - instance.GridOrigin.Y);
                if (!moved.Success) return PlacementFail(state, moved, instanceId);
                candidate = moved.Grid;
            }
            for (int turn = 0; turn < turns; turn++)
            {
                OfficeLayoutEditResult rotated = OfficeLayoutEditRules.RotateFurniture(candidate, instanceId);
                if (!rotated.Success) return PlacementFail(state, rotated, instanceId);
                candidate = rotated.Grid;
            }
            state.ReplaceOfficeState(candidate, Synchronize(state.OfficeFurnitureInventory, candidate));
            return OfficeFurnitureCommandResult.Ok(state, instanceId);
        }

        public static OfficeFurnitureCommandResult Rotate(
            GameState state,
            string instanceId,
            Func<string, bool> isInUse = null)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (instance.PlacementState != OfficeFurniturePlacementState.Placed)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.NotPlaced, "배치된 가구가 아닙니다.", instanceId);
            if (IsInUse(instanceId, isInUse)) return InUse(state, instanceId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.RotateFurniture(state.OfficeGrid, instanceId);
            if (!edit.Success) return PlacementFail(state, edit, instanceId);
            state.ReplaceOfficeState(edit.Grid, Synchronize(state.OfficeFurnitureInventory, edit.Grid));
            return OfficeFurnitureCommandResult.Ok(state, instanceId);
        }

        public static OfficeFurnitureCommandResult Store(
            GameState state,
            string instanceId,
            Func<string, bool> isInUse = null)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (instance.PlacementState == OfficeFurniturePlacementState.Stored)
                return OfficeFurnitureCommandResult.Ok(state, instanceId, alreadyApplied: true);
            if (IsInUse(instanceId, isInUse)) return InUse(state, instanceId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.RemoveFurniture(state.OfficeGrid, instanceId);
            if (!edit.Success) return PlacementFail(state, edit, instanceId);
            OfficeFurnitureInventoryState inventory = state.OfficeFurnitureInventory.Replace(
                instance.WithPlacement(OfficeFurniturePlacementState.Stored, instance.GridOrigin, instance.Rotation));
            state.ReplaceOfficeState(edit.Grid, inventory);
            return OfficeFurnitureCommandResult.Ok(state, instanceId);
        }

        public static OfficeFurnitureCommandResult PlaceStored(
            GameState state,
            string instanceId,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing rotation)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (instance.PlacementState != OfficeFurniturePlacementState.Stored)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.NotStored, "보관 중인 가구가 아닙니다.", instanceId);
            OfficeLayoutEditResult edit = OfficeLayoutEditRules.PlaceFurniture(
                state.OfficeGrid, instanceId, instance.DefinitionId, origin, rotation);
            if (!edit.Success) return PlacementFail(state, edit, instanceId);
            OfficeFurnitureInventoryState inventory = state.OfficeFurnitureInventory.Replace(
                instance.WithPlacement(OfficeFurniturePlacementState.Placed, origin, rotation));
            state.ReplaceOfficeState(edit.Grid, inventory);
            return OfficeFurnitureCommandResult.Ok(state, instanceId);
        }

        public static OfficeFurnitureCommandResult Sell(
            GameState state,
            string commandId,
            string instanceId,
            Func<string, bool> isInUse = null)
        {
            Required(state, nameof(state));
            OfficeFurnitureInstanceState instance = state.OfficeFurnitureInventory.Find(instanceId);
            if (state.Company.HasTransaction(commandId))
            {
                if (instance == null) return OfficeFurnitureCommandResult.Ok(state, instanceId, alreadyApplied: true);
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.IdempotencyConflict,
                    "같은 거래 ID가 다른 판매에 이미 사용되었습니다.", instanceId);
            }
            if (instance == null)
                return OfficeFurnitureCommandResult.Fail(
                    state, OfficeFurnitureCommandFailure.UnknownInstance, "보유하지 않은 가구입니다.", instanceId);
            if (IsInUse(instanceId, isInUse)) return InUse(state, instanceId);
            OfficeGrid nextGrid = state.OfficeGrid;
            if (instance.PlacementState == OfficeFurniturePlacementState.Placed)
            {
                OfficeLayoutEditResult edit = OfficeLayoutEditRules.RemoveFurniture(state.OfficeGrid, instanceId);
                if (!edit.Success) return PlacementFail(state, edit, instanceId);
                nextGrid = edit.Grid;
            }
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(instance.DefinitionId);
            long refundWon = OfficeFurnitureEconomyConfig.ResaleValue(
                instance.PurchaseBasisWon, definition.ResaleRateBasisPoints);
            OfficeFurnitureInventoryState inventory = state.OfficeFurnitureInventory.Remove(instanceId);
            state.Company.SellOfficeFurniture(
                RequiredId(commandId, nameof(commandId)),
                state.Time.ElapsedMinutes,
                instance.PurchaseBasisWon,
                refundWon,
                "사무 가구 판매: " + definition.KoreanDisplayName,
                instance.PurchaseBasisState == OfficeFurniturePurchaseBasisState.Purchased);
            state.ReplaceOfficeState(nextGrid, inventory);
            return OfficeFurnitureCommandResult.Ok(state, instanceId, refundedWon: refundWon);
        }

        private static OfficeFurnitureInventoryState Synchronize(
            OfficeFurnitureInventoryState inventory,
            OfficeGrid grid)
        {
            var byId = grid.Furniture.ToDictionary(item => item.FurnitureId, StringComparer.Ordinal);
            return new OfficeFurnitureInventoryState(inventory.Instances.Select(instance =>
            {
                if (instance.PlacementState != OfficeFurniturePlacementState.Placed ||
                    !byId.TryGetValue(instance.InstanceId, out PlacedOfficeFurniture placed)) return instance;
                return instance.WithPlacement(
                    OfficeFurniturePlacementState.Placed, placed.Origin, placed.Facing);
            }));
        }

        private static string NextWorkstationSeatId(GameState state, string workSurfaceInstanceId)
        {
            foreach (var member in state.Family.Members)
            {
                string candidate = "seat_" + member.MemberId;
                if (state.OfficeGrid.SeatSlots.All(item => !Same(item.SeatId, candidate))) return candidate;
            }
            return "seat_workstation_" + workSurfaceInstanceId;
        }

        private static OfficeFurnitureCommandResult PlacementFail(
            GameState state,
            OfficeLayoutEditResult edit,
            string instanceId) => OfficeFurnitureCommandResult.Fail(
                state, OfficeFurnitureCommandFailure.PlacementInvalid, edit.Message, instanceId, edit.Failure);

        private static bool IsInUse(string instanceId, Func<string, bool> isInUse) =>
            isInUse != null && isInUse(instanceId);

        private static OfficeFurnitureCommandResult InUse(GameState state, string instanceId) =>
            OfficeFurnitureCommandResult.Fail(
                state, OfficeFurnitureCommandFailure.InUse,
                "사용·예약·claim 중인 가구는 먼저 안전하게 해제해야 합니다.", instanceId);

        private static string RequiredId(string value, string parameter)
        {
            string canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("ID is required.", parameter);
            return canonical;
        }

        private static void Required(object value, string parameter)
        {
            if (value == null) throw new ArgumentNullException(parameter);
        }

        private static bool Same(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);
    }
}

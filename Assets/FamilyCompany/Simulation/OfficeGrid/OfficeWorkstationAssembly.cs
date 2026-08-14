using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    /// <summary>
    /// One immutable desk/chair/operator axis. Stable furniture IDs are the identity; sockets are
    /// resolved from the same rotation-aware geometry consumed by placement and movement.
    /// </summary>
    public sealed class OfficeWorkstationAssembly
    {
        internal OfficeWorkstationAssembly(
            OfficeSeatSlot seat,
            PlacedOfficeFurniture desk,
            PlacedOfficeFurniture chair,
            OfficeFurnitureWorldSocket seatContact,
            OfficeFurnitureWorldSocket operatorSocket,
            OfficeFurnitureWorldSocket keyboardSocket,
            OfficeFurnitureWorldSocket monitorSocket,
            IReadOnlyList<OfficeFurnitureWorldSocket> egressSockets)
        {
            Seat = seat ?? throw new ArgumentNullException(nameof(seat));
            Desk = desk ?? throw new ArgumentNullException(nameof(desk));
            Chair = chair ?? throw new ArgumentNullException(nameof(chair));
            SeatContact = seatContact ?? throw new ArgumentNullException(nameof(seatContact));
            OperatorSocket = operatorSocket ?? throw new ArgumentNullException(nameof(operatorSocket));
            KeyboardSocket = keyboardSocket ?? throw new ArgumentNullException(nameof(keyboardSocket));
            MonitorSocket = monitorSocket ?? throw new ArgumentNullException(nameof(monitorSocket));
            EgressSockets = new ReadOnlyCollection<OfficeFurnitureWorldSocket>(
                (egressSockets ?? throw new ArgumentNullException(nameof(egressSockets))).ToArray());
            AssemblyId = StableAssemblyId(desk.FurnitureId, chair.FurnitureId);
            int deltaX4 = KeyboardSocket.WorldAnchor.X4 - SeatContact.WorldAnchor.X4;
            int deltaY4 = KeyboardSocket.WorldAnchor.Y4 - SeatContact.WorldAnchor.Y4;
            ChairToKeyboardGap4 = Math.Abs(deltaX4) + Math.Abs(deltaY4);
        }

        public string AssemblyId { get; }
        public string DeskId => Desk.FurnitureId;
        public string PairedChairId => Chair.FurnitureId;
        public OfficeSeatSlot Seat { get; }
        public PlacedOfficeFurniture Desk { get; }
        public PlacedOfficeFurniture Chair { get; }
        public OfficeFurnitureWorldSocket SeatContact { get; }
        public OfficeFurnitureWorldSocket OperatorSocket { get; }
        public OfficeFurnitureWorldSocket KeyboardSocket { get; }
        public OfficeFurnitureWorldSocket MonitorSocket { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> EgressSockets { get; }
        public OfficeFurnitureFacing OperatorFacing => Seat.Facing;
        public int ChairToKeyboardGap4 { get; }

        public static string StableAssemblyId(string deskId, string chairId) =>
            "workstation:" + RequiredId(deskId, nameof(deskId)) + ":" +
            RequiredId(chairId, nameof(chairId));

        private static string RequiredId(string value, string name)
        {
            string canonical = (value ?? string.Empty).Trim();
            if (canonical.Length == 0) throw new ArgumentException("Stable ID is required.", name);
            return canonical;
        }
    }

    public static class OfficeWorkstationAssemblyQuery
    {
        public static IReadOnlyList<OfficeWorkstationAssembly> ResolveAll(OfficeGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            var result = new List<OfficeWorkstationAssembly>();
            foreach (OfficeSeatSlot seat in grid.SeatSlots.OrderBy(item => item.SeatId, StringComparer.Ordinal))
            {
                if (!seat.HasWorkstationBinding) continue;
                if (!TryResolve(grid, seat, out OfficeWorkstationAssembly assembly, out string failure))
                    throw new InvalidOperationException("Invalid workstation '" + seat.SeatId + "': " + failure);
                result.Add(assembly);
            }
            return result.AsReadOnly();
        }

        public static bool TryResolve(
            OfficeGrid grid,
            OfficeSeatSlot seat,
            out OfficeWorkstationAssembly assembly,
            out string failure)
        {
            assembly = null;
            failure = string.Empty;
            if (grid == null || seat == null || !seat.HasWorkstationBinding)
            {
                failure = "grid/seat/workstation binding is missing";
                return false;
            }
            PlacedOfficeFurniture chair = grid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, seat.ChairFurnitureId, StringComparison.Ordinal));
            PlacedOfficeFurniture desk = grid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, seat.WorkSurfaceFurnitureId, StringComparison.Ordinal));
            if (chair == null || desk == null)
            {
                failure = "desk or chair instance is missing";
                return false;
            }
            OfficeFurnitureDefinition chairDefinition = OfficeFurnitureCatalog.Find(chair.KindId);
            OfficeFurnitureDefinition deskDefinition = OfficeFurnitureCatalog.Find(desk.KindId);
            if (chairDefinition == null || !chairDefinition.HasCapability(OfficeFurnitureCapability.Seat) ||
                deskDefinition == null || !deskDefinition.HasCapability(OfficeFurnitureCapability.WorkDesk))
            {
                failure = "furniture capabilities do not form a workstation";
                return false;
            }
            if (chair.Facing != seat.Facing ||
                desk.Facing != OfficeFurnitureRotationTransform.Opposite(seat.Facing))
            {
                failure = "chair/desk/operator facing mismatch";
                return false;
            }

            OfficeFurnitureGeometrySnapshot chairGeometry = OfficeFurnitureGeometryQuery.Shared.Resolve(chair);
            OfficeFurnitureGeometrySnapshot deskGeometry = OfficeFurnitureGeometryQuery.Shared.Resolve(desk);
            OfficeFurnitureWorldSocket seatContact = chairGeometry.SeatContactSockets
                .FirstOrDefault(item => item.SlotIndex == 0);
            OfficeFurnitureWorldSocket operatorSocket = deskGeometry.WorkstationOperatorSockets
                .FirstOrDefault(item => item.SlotIndex == 0 &&
                    item.WorldCell.Equals(chair.Origin) && item.DesiredActorFacing == seat.Facing);
            OfficeFurnitureWorldSocket keyboardSocket = deskGeometry.KeyboardWorkSockets
                .FirstOrDefault(item => item.SlotIndex == 0 && item.DesiredActorFacing == seat.Facing);
            OfficeFurnitureWorldSocket monitorSocket = deskGeometry.MonitorCenterSockets
                .FirstOrDefault(item => item.SlotIndex == 0 && item.DesiredActorFacing == seat.Facing);
            if (seatContact == null || operatorSocket == null || keyboardSocket == null || monitorSocket == null)
            {
                failure = "seat/operator/keyboard/monitor socket is missing or misaligned";
                return false;
            }
            if (!seat.Cell.Equals(seatContact.WorldCell) ||
                !seat.OperatorAnchor.Equals(ToHalfCellAnchor(seatContact.WorldAnchor)))
            {
                failure = "seat contact/operator anchor does not match canonical geometry";
                return false;
            }
            OfficeFurnitureWorldSocket[] egress = chairGeometry.SeatEgressSockets
                .Where(item => item.SlotIndex == 0)
                .OrderBy(EgressOrder)
                .ToArray();
            if (egress.Length != 3 || !egress.Any(item => item.WorldCell.Equals(seat.ApproachCell)))
            {
                failure = "approach is not one of the three rotation-aware chair egress sockets";
                return false;
            }
            assembly = new OfficeWorkstationAssembly(
                seat, desk, chair, seatContact, operatorSocket, keyboardSocket, monitorSocket, egress);
            return true;
        }

        public static OfficeGridSubcellAnchor ToHalfCellAnchor(OfficeFurnitureLocalPoint worldAnchor)
        {
            if ((worldAnchor.X4 & 1) != 0 || (worldAnchor.Y4 & 1) != 0)
                throw new InvalidOperationException("Workstation seat contact must be representable at half-cell precision.");
            return new OfficeGridSubcellAnchor(worldAnchor.X4 / 2, worldAnchor.Y4 / 2);
        }

        private static int EgressOrder(OfficeFurnitureWorldSocket socket)
        {
            return socket.Kind switch
            {
                OfficeFurnitureGeometrySocketKind.SeatEgressFront => 0,
                OfficeFurnitureGeometrySocketKind.SeatEgressLeft => 1,
                OfficeFurnitureGeometrySocketKind.SeatEgressRight => 2,
                _ => 3
            };
        }
    }

    /// <summary>Deterministic pairing used by layout edits and save-compatible seat reconstruction.</summary>
    public static class OfficeWorkstationPairingRules
    {
        public const string DynamicSeatPrefix = "seat_pair:";

        public static IReadOnlyList<OfficeSeatSlot> Synchronize(OfficeGrid provisional)
        {
            if (provisional == null) throw new ArgumentNullException(nameof(provisional));
            var result = new List<OfficeSeatSlot>();
            var usedChairs = new HashSet<string>(StringComparer.Ordinal);
            var usedDesks = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeSeatSlot seat in provisional.SeatSlots.OrderBy(item => item.SeatId, StringComparer.Ordinal))
            {
                if (!seat.HasWorkstationBinding)
                {
                    result.Add(seat);
                    usedChairs.Add(seat.ChairFurnitureId);
                    continue;
                }
                if (OfficeWorkstationAssemblyQuery.TryResolve(
                        provisional, seat, out _, out _) || !IsDynamicSeat(seat.SeatId))
                {
                    result.Add(seat);
                    usedChairs.Add(seat.ChairFurnitureId);
                    usedDesks.Add(seat.WorkSurfaceFurnitureId);
                }
            }

            PlacedOfficeFurniture[] desks = provisional.Furniture
                .Where(item => string.Equals(
                    item.KindId, OfficeGridLayouts.DeskWithPcKind, StringComparison.Ordinal))
                .OrderBy(item => item.FurnitureId, StringComparer.Ordinal)
                .ToArray();
            PlacedOfficeFurniture[] chairs = provisional.Furniture
                .Where(item => string.Equals(
                    item.KindId, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal))
                .OrderBy(item => item.FurnitureId, StringComparer.Ordinal)
                .ToArray();
            foreach (PlacedOfficeFurniture chair in chairs)
            {
                if (usedChairs.Contains(chair.FurnitureId)) continue;
                OfficeFurnitureGeometrySnapshot chairGeometry = OfficeFurnitureGeometryQuery.Shared.Resolve(chair);
                OfficeFurnitureWorldSocket seatContact = chairGeometry.SeatContactSockets
                    .FirstOrDefault(item => item.SlotIndex == 0);
                if (seatContact == null) continue;
                PlacedOfficeFurniture desk = desks.FirstOrDefault(candidate =>
                    !usedDesks.Contains(candidate.FurnitureId) &&
                    HasMatchingOperatorSocket(candidate, chair));
                if (desk == null) continue;
                OfficeFurnitureWorldSocket approach = chairGeometry.SeatEgressSockets
                    .Where(item => item.SlotIndex == 0 && IsSafeCell(provisional, item.WorldCell))
                    .OrderBy(item => item.Kind == OfficeFurnitureGeometrySocketKind.SeatEgressFront ? 0 :
                        item.Kind == OfficeFurnitureGeometrySocketKind.SeatEgressLeft ? 1 : 2)
                    .ThenBy(item => item.WorldCell.Y)
                    .ThenBy(item => item.WorldCell.X)
                    .FirstOrDefault();
                if (approach == null) continue;
                result.Add(new OfficeSeatSlot(
                    DynamicSeatId(chair.FurnitureId, desk.FurnitureId),
                    chair.FurnitureId,
                    desk.FurnitureId,
                    chair.Origin,
                    approach.WorldCell,
                    OfficeWorkstationAssemblyQuery.ToHalfCellAnchor(seatContact.WorldAnchor),
                    chair.Facing));
                usedChairs.Add(chair.FurnitureId);
                usedDesks.Add(desk.FurnitureId);
            }
            return result.OrderBy(item => item.SeatId, StringComparer.Ordinal).ToArray();
        }

        public static bool TryRecommendChairFacing(
            OfficeGrid grid,
            OfficeGridCoordinate chairOrigin,
            out OfficeFurnitureFacing facing)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            foreach (PlacedOfficeFurniture desk in grid.Furniture
                         .Where(item => string.Equals(
                             item.KindId, OfficeGridLayouts.DeskWithPcKind, StringComparison.Ordinal))
                         .OrderBy(item => item.FurnitureId, StringComparer.Ordinal))
            {
                OfficeFurnitureWorldSocket socket = OfficeFurnitureGeometryQuery.Shared.Resolve(desk)
                    .WorkstationOperatorSockets.FirstOrDefault(item => item.WorldCell.Equals(chairOrigin));
                if (socket == null) continue;
                facing = socket.DesiredActorFacing;
                return true;
            }
            facing = default;
            return false;
        }

        public static bool IsDynamicSeat(string seatId) =>
            (seatId ?? string.Empty).StartsWith(DynamicSeatPrefix, StringComparison.Ordinal);

        private static bool HasMatchingOperatorSocket(
            PlacedOfficeFurniture desk,
            PlacedOfficeFurniture chair)
        {
            if (desk.Facing != OfficeFurnitureRotationTransform.Opposite(chair.Facing)) return false;
            return OfficeFurnitureGeometryQuery.Shared.Resolve(desk).WorkstationOperatorSockets.Any(item =>
                item.SlotIndex == 0 && item.WorldCell.Equals(chair.Origin) &&
                item.DesiredActorFacing == chair.Facing);
        }

        private static bool IsSafeCell(OfficeGrid grid, OfficeGridCoordinate cell) =>
            grid.Contains(cell) && grid.IsWalkable(cell);

        private static string DynamicSeatId(string chairId, string deskId) =>
            DynamicSeatPrefix + chairId + ":" + deskId;
    }
}

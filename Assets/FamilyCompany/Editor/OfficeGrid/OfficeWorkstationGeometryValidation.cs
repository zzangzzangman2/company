using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using OfficeGridModel = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>Rotation, pairing and socket coverage without presentation-specific guesses.</summary>
    public static class OfficeWorkstationGeometryValidation
    {
        [MenuItem("Family Company/Validate Office Workstation Geometry")]
        public static void Validate()
        {
            int geometryRotations = 0;
            foreach (OfficeFurnitureDefinition definition in OfficeFurnitureCatalog.Purchasable)
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            {
                OfficeFurnitureGeometryProfile profile = definition.Geometry.ForFacing(facing);
                OfficeGridCoordinate footprint = definition.FootprintFor(facing);
                Require(profile.Facing == facing, definition.DefinitionId + " facing drift");
                Require(profile.FootprintWidth == footprint.X && profile.FootprintHeight == footprint.Y,
                    definition.DefinitionId + " footprint drift");
                Require(profile.SolidSubcellCount > 0, definition.DefinitionId + " empty ground collision");
                geometryRotations++;
            }

            OfficeGridModel starter = OfficeGridLayouts.CreateStarterOfficeV1();
            IReadOnlyList<OfficeWorkstationAssembly> starterAssemblies =
                OfficeWorkstationAssemblyQuery.ResolveAll(starter);
            Require(starterAssemblies.Count == 4, "Starter office must retain four workstation assemblies.");
            Require(starterAssemblies.Select(item => item.AssemblyId)
                        .Distinct(StringComparer.Ordinal).Count() == 4,
                "Starter workstation assembly IDs are not unique.");

            int dynamicPairings = 0;
            int rotationTransactions = 0;
            foreach (OfficeFurnitureFacing chairFacing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            {
                OfficeGridModel paired = BuildIsolatedPair(chairFacing);
                OfficeWorkstationAssembly assembly = OfficeWorkstationAssemblyQuery.ResolveAll(paired).Single();
                Require(OfficeWorkstationPairingRules.IsDynamicSeat(assembly.Seat.SeatId),
                    "Free desk/chair did not receive a stable dynamic seat ID.");
                Require(assembly.Seat.Cell.Equals(assembly.OperatorSocket.WorldCell),
                    "Chair contact and desk operator socket diverged.");
                Require(assembly.OperatorFacing == assembly.KeyboardSocket.DesiredActorFacing &&
                        assembly.OperatorFacing == assembly.MonitorSocket.DesiredActorFacing,
                    "Monitor/keyboard/operator axes diverged.");
                Require(assembly.EgressSockets.Count == 3 &&
                        assembly.EgressSockets.Select(item => item.WorldCell).Distinct().Count() == 3,
                    "A workstation does not expose three distinct egress sockets.");

                string initialHash = paired.ComputeLayoutHash();
                for (var turn = 0; turn < 4; turn++)
                {
                    OfficeLayoutEditResult rotated = OfficeLayoutEditRules.RotateWorkstation(
                        paired, paired.SeatSlots.Single().SeatId);
                    Require(rotated.Success,
                        chairFacing + " rotation " + turn + " failed: " + rotated.Failure + " " + rotated.Message);
                    paired = rotated.Grid;
                    Require(OfficeWorkstationAssemblyQuery.ResolveAll(paired).Count == 1,
                        "Workstation assembly disappeared after rotation.");
                    rotationTransactions++;
                }
                Require(paired.ComputeLayoutHash() == initialHash,
                    "Four rotations did not restore the exact layout hash for " + chairFacing + ".");
                dynamicPairings++;
            }

            Debug.Log(
                "OFFICE_WORKSTATION_GEOMETRY_VALIDATION: PASS definitions=" +
                OfficeFurnitureCatalog.Purchasable.Count() +
                " geometryRotations=" + geometryRotations +
                " starterAssemblies=4 dynamicPairings=" + dynamicPairings +
                " rotationTransactions=" + rotationTransactions +
                " facingMismatch=0 wrongPairing=0 duplicateEgress=0");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static OfficeGridModel BuildIsolatedPair(OfficeFurnitureFacing chairFacing)
        {
            const int width = 20;
            const int height = 20;
            OfficeFurnitureFacing deskFacing = OfficeFurnitureRotationTransform.Opposite(chairFacing);
            OfficeFurnitureDefinition deskDefinition = OfficeFurnitureCatalog.Require(OfficeGridLayouts.DeskWithPcKind);
            OfficeFurnitureDefinition chairDefinition = OfficeFurnitureCatalog.Require(OfficeGridLayouts.SwivelChairKind);
            OfficeGridCoordinate deskFootprint = deskDefinition.FootprintFor(deskFacing);
            var desk = new PlacedOfficeFurniture(
                "desk_dynamic", OfficeGridLayouts.DeskWithPcKind, new OfficeGridCoordinate(9, 9),
                deskFootprint.X, deskFootprint.Y, deskFacing, true);
            OfficeFurnitureWorldSocket operatorSocket = OfficeFurnitureGeometryQuery.Shared.Resolve(desk)
                .WorkstationOperatorSockets.Single(item => item.SlotIndex == 0);
            OfficeGridCoordinate chairFootprint = chairDefinition.FootprintFor(chairFacing);
            var chair = new PlacedOfficeFurniture(
                "chair_dynamic", OfficeGridLayouts.SwivelChairKind, operatorSocket.WorldCell,
                chairFootprint.X, chairFootprint.Y, chairFacing, false);
            OfficeFloorTileKind[] floor = Enumerable.Repeat(
                OfficeFloorTileKind.WarmWoodA, width * height).ToArray();
            bool[] walkable = Enumerable.Repeat(true, width * height).ToArray();
            for (int y = desk.Origin.Y; y < desk.Origin.Y + desk.Height; y++)
            for (int x = desk.Origin.X; x < desk.Origin.X + desk.Width; x++)
                walkable[y * width + x] = false;
            var furniture = new[] { desk, chair };
            var provisional = new OfficeGridModel(width, height, floor, walkable, furniture);
            IReadOnlyList<OfficeSeatSlot> seats = OfficeWorkstationPairingRules.Synchronize(provisional);
            return new OfficeGridModel(width, height, floor, walkable, furniture, seats);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

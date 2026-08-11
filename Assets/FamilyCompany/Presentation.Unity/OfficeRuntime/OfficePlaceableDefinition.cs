using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [Serializable]
    public readonly struct OfficeFootprint
    {
        public OfficeFootprint(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
    }

    [Serializable]
    public sealed class OfficePlaceableDefinition
    {
        public OfficePlaceableDefinition(
            string kindId,
            OfficeFootprint hardFootprint,
            OfficeFootprint interactionFootprint,
            bool blocksMovement = true,
            float extraClearance = 0f,
            params OfficeFurnitureFacing[] supportedFacings)
        {
            KindId = string.IsNullOrWhiteSpace(kindId)
                ? throw new ArgumentException("Placeable kind ID is required.", nameof(kindId))
                : kindId.Trim();
            HardFootprint = hardFootprint;
            InteractionFootprint = interactionFootprint;
            BlocksMovement = blocksMovement;
            ExtraClearance = Math.Max(0f, extraClearance);
            SupportedFacings = supportedFacings == null || supportedFacings.Length == 0
                ? new[]
                {
                    OfficeFurnitureFacing.SouthEast,
                    OfficeFurnitureFacing.SouthWest,
                    OfficeFurnitureFacing.NorthWest,
                    OfficeFurnitureFacing.NorthEast
                }
                : (OfficeFurnitureFacing[])supportedFacings.Clone();
        }

        public string KindId { get; }
        public OfficeFootprint HardFootprint { get; }
        public OfficeFootprint InteractionFootprint { get; }
        public bool BlocksMovement { get; }
        public float ExtraClearance { get; }
        public IReadOnlyList<OfficeFurnitureFacing> SupportedFacings { get; }
    }

    public static class OfficePlaceableCatalog
    {
        private static readonly IReadOnlyList<OfficePlaceableDefinition> Definitions =
            new[]
            {
                Definition(OfficeGridLayouts.DeskWithPcKind, 2, 1),
                Definition(OfficeGridLayouts.SwivelChairKind, 1, 1, false, 1, 1),
                Definition(OfficeGridLayouts.ReceptionCounterKind, 2, 1),
                Definition(OfficeGridLayouts.MeetingTableKind, 2, 1),
                Definition(OfficeGridLayouts.DocumentBookcaseKind, 1, 1),
                Definition(OfficeGridLayouts.FaxCopierKind, 1, 1),
                Definition(OfficeGridLayouts.WaterDispenserKind, 1, 1),
                Definition(OfficeGridLayouts.SofaKind, 2, 1),
                Definition(OfficeGridLayouts.CoffeeTableKind, 2, 1),
                Definition(OfficeGridLayouts.PottedPlantKind, 1, 1),
                Definition(OfficeGridLayouts.PartitionKind, 1, 2),
                Definition(OfficeGridLayouts.FilingCabinetKind, 1, 1)
            };

        public static IReadOnlyList<OfficePlaceableDefinition> All => Definitions;

        public static OfficePlaceableDefinition Find(string kindId)
        {
            foreach (OfficePlaceableDefinition definition in Definitions)
            {
                if (string.Equals(definition.KindId, kindId, StringComparison.Ordinal)) return definition;
            }
            return null;
        }

        private static OfficePlaceableDefinition Definition(
            string kind,
            int width,
            int height,
            bool blocks = true,
            int interactionWidth = 1,
            int interactionHeight = 1) => new OfficePlaceableDefinition(
                kind,
                new OfficeFootprint(width, height),
                new OfficeFootprint(interactionWidth, interactionHeight),
                blocks);
    }
}

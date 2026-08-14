using System;

namespace FamilyCompany.Simulation.OfficeLayout
{
    /// <summary>
    /// The single quarter-turn rule shared by placement, ground geometry, sockets and workstation
    /// assembly. Presentation resolves authored art for the resulting facing; it never invents a
    /// second, independent rotation for pivots or interaction anchors.
    /// </summary>
    public static class OfficeFurnitureRotationTransform
    {
        public static OfficeFurnitureFacing QuarterTurnClockwise(OfficeFurnitureFacing facing) =>
            (OfficeFurnitureFacing)(((int)facing + 1) & 3);

        public static OfficeFurnitureFacing Opposite(OfficeFurnitureFacing facing) =>
            (OfficeFurnitureFacing)(((int)facing + 2) & 3);

        public static OfficeGridCoordinate RotateCellOffsetClockwise(
            OfficeGridCoordinate offset,
            int sourceFootprintWidth)
        {
            if (sourceFootprintWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceFootprintWidth));
            return new OfficeGridCoordinate(offset.Y, sourceFootprintWidth - 1 - offset.X);
        }

        public static OfficeFurnitureLocalPoint RotateLocalPointClockwise(
            OfficeFurnitureLocalPoint point,
            int sourceFootprintWidth)
        {
            if (sourceFootprintWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceFootprintWidth));
            int width4 = checked(sourceFootprintWidth * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            return new OfficeFurnitureLocalPoint(point.Y4, width4 - point.X4);
        }

        public static OfficeGridCoordinate FacingStep(OfficeFurnitureFacing facing)
        {
            return facing switch
            {
                OfficeFurnitureFacing.SouthEast => new OfficeGridCoordinate(0, -1),
                OfficeFurnitureFacing.SouthWest => new OfficeGridCoordinate(-1, 0),
                OfficeFurnitureFacing.NorthWest => new OfficeGridCoordinate(0, 1),
                OfficeFurnitureFacing.NorthEast => new OfficeGridCoordinate(1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }
    }
}

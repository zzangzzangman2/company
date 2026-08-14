using System;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    /// <summary>
    /// Presentation-only geometry for placing perimeter wall art on the outside edges of the
    /// immutable semantic floor. Furniture origins, navigation cells and placement anchors remain
    /// unchanged; only the generated visual root receives this half-cell exterior offset.
    /// </summary>
    public static class OfficePerimeterExteriorGeometry
    {
        public static bool IsPerimeterKind(string kindId)
        {
            return string.Equals(kindId, OfficeGridLayouts.EntranceDoorKind, StringComparison.Ordinal) ||
                   string.Equals(kindId, OfficeGridLayouts.EntranceWallKind, StringComparison.Ordinal) ||
                   string.Equals(kindId, OfficeGridLayouts.PerimeterCutawayWallKind, StringComparison.Ordinal);
        }

        public static Vector3 VisualOffsetWorld(
            PlacedOfficeFurniture furniture,
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter)
        {
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!IsPerimeterKind(furniture.KindId)) return Vector3.zero;

            Vector3 basisX = presenter.CellBasisXWorld();
            Vector3 basisY = presenter.CellBasisYWorld();
            if (furniture.Facing == OfficeFurnitureFacing.SouthEast)
            {
                if (furniture.Origin.Y == 0) return -0.5f * basisX - 0.5f * basisY;
                if (furniture.Origin.Y == grid.Height - 1) return -0.5f * basisX + 0.5f * basisY;
            }
            else if (furniture.Facing == OfficeFurnitureFacing.SouthWest)
            {
                if (furniture.Origin.X == 0) return -0.5f * basisX - 0.5f * basisY;
                if (furniture.Origin.X == grid.Width - 1) return 0.5f * basisX - 0.5f * basisY;
            }

            throw new InvalidOperationException(
                $"Perimeter visual is not on a canonical exterior edge: {furniture.FurnitureId} " +
                $"origin={furniture.Origin} facing={furniture.Facing}.");
        }

        public static void InnerEdgeWorld(
            PlacedOfficeFurniture furniture,
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            out Vector3 start,
            out Vector3 end)
        {
            if (!IsPerimeterKind(furniture?.KindId))
                throw new ArgumentException("Furniture is not a perimeter bay.", nameof(furniture));
            start = presenter.SubcellAnchorWorld(furniture.PlacementAnchor) +
                    VisualOffsetWorld(furniture, grid, presenter);
            end = start + (furniture.Facing == OfficeFurnitureFacing.SouthEast
                ? presenter.CellBasisXWorld()
                : presenter.CellBasisYWorld());
        }
    }
}

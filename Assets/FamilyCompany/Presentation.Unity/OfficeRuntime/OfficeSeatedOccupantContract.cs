using UnityEngine;
using FamilyCompany.Presentation.Unity.OfficeGridView;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// How a seated character is placed and sorted. Two rules, both measured against the art.
    ///
    /// PLACEMENT - the seat contact point of the character lands on the chair's cushion anchor.
    /// Standing the character on the chair's floor point instead does not work with this art: the
    /// cushion is 108.2px above the chair's floor anchor while the family sheets put the buttocks
    /// only 52..76 canvas px above their own feet, so a floor-anchored occupant sinks 5..38px
    /// through the seat. Contact-to-cushion is the only pinning that reads as sitting.
    ///
    /// DEPTH - every object keeps the sorting order of its own ground anchor and the occupant sorts
    /// one step in front of the chair it sits on. No furniture order is ever rewritten around a
    /// person. The previous stack (desk = character-2, deskFront = character+1) is what drew desk
    /// legs and drawers across a body sitting a full cell away from the desk.
    ///
    /// What is forbidden either way: rotation, per-pose scale, per-member scale, and moving an
    /// anatomy anchor until some furniture number passes.
    /// </summary>
    public static class OfficeSeatedOccupantContract
    {
        /// <summary>Canvas size of every character sheet, walking and seated alike.</summary>
        public const float CharacterCanvasPx = 256f;

        /// <summary>Pixels per unit shared by every office sprite.</summary>
        public const float PixelsPerUnit = OfficeGridTilemapPresenter.PixelsPerUnit;

        /// <summary>
        /// World point the seat contact of the drawn sprite actually landed on. Read back through
        /// the live SpriteRenderer - its sprite, pivot, pixelsPerUnit and transform - so a sheet
        /// imported with the wrong pivot or PPU shows up as a real error instead of cancelling out.
        /// </summary>
        public static Vector3 OccupantSeatContactWorld(SpriteRenderer renderer, Vector2 seatContactPx)
        {
            if (renderer == null) return Vector3.zero;
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(renderer, seatContactPx);
        }

        /// <summary>
        /// Sorting order for the occupant of a chair that renders at
        /// <paramref name="chairBaseSortingOrder"/>. Exactly one step in front of that chair, so the
        /// cushion and castor base stay behind the body while every other object keeps its own
        /// ground-anchor order.
        /// </summary>
        public static int OccupantSortingOrder(int chairBaseSortingOrder)
        {
            return chairBaseSortingOrder + 1;
        }
    }
}

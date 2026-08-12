using UnityEngine;
using FamilyCompany.Presentation.Unity.OfficeGridView;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// The single rule that places a seated character, in the form isometric tycoon games use.
    ///
    /// Every sprite is anchored by its floor contact point and nothing else:
    ///   furniture pivot  == its ground anchor      (OfficeGridFurniturePresenter)
    ///   character pivot  == bottom-centre of the 256px canvas
    ///
    /// Sitting therefore means "stand the character on the chair's floor point". There is no
    /// pelvis-to-seat pixel matching, no per-member offset, no per-frame correction and no
    /// hand-to-keyboard target. The only authored number is the column of the seated canvas that
    /// represents the floor under the occupant, which is a property of how the seated sheets were
    /// drawn - not of any individual family member.
    ///
    /// Depth follows from the same floor point: the occupant sorts one step in front of the chair
    /// it stands on, and every other object keeps its own ground-anchor order. Furniture is never
    /// re-sorted around a person, so a desk a cell away can no longer draw its legs over someone's
    /// lap.
    /// </summary>
    public static class OfficeSeatedOccupantContract
    {
        /// <summary>Canvas width of every character sheet, walking and seated alike.</summary>
        public const float CharacterCanvasPx = 256f;

        /// <summary>Sprite pivot column: bottom-centre.</summary>
        public const float CharacterPivotColumnPx = CharacterCanvasPx * 0.5f;

        /// <summary>
        /// Column of the seated canvas that stands on the chair's floor point.
        ///
        /// One authored number for the whole game, approved against the real sprites at
        /// Artifacts/OfficeVisualCoherenceV4/ (zoom_offset / final_check): at column 119 all four
        /// family sheets put their hips over the cushion, their feet on the floor in front of the
        /// castor base, and the base under the body. Columns below 110 slide the body off the left
        /// edge of the seat, columns above 128 push it onto the right armrest.
        ///
        /// It belongs to the seated sheets as a set, never to one member. If a new sheet needs a
        /// different column, that sheet is off-spec art.
        /// </summary>
        public const float SeatedFloorAnchorColumnPx = 119f;

        /// <summary>Pixels per unit shared by every office sprite.</summary>
        public const float PixelsPerUnit = OfficeGridTilemapPresenter.PixelsPerUnit;

        /// <summary>
        /// Visual translation that puts <see cref="SeatedFloorAnchorColumnPx"/> - rather than the
        /// bottom-centre pivot - on the chair floor point the actor stands at.
        /// Translation only: never a scale, never a rotation, never per member.
        /// </summary>
        public static Vector3 VisualOffset(float characterVisualScale)
        {
            float columns = CharacterPivotColumnPx - SeatedFloorAnchorColumnPx;
            return new Vector3(columns * characterVisualScale / PixelsPerUnit, 0f, 0f);
        }

        /// <summary>
        /// World point the floor column of the drawn sprite actually landed on. Read back through
        /// the live SpriteRenderer - its sprite, its pivot, its pixelsPerUnit, its transform - so a
        /// sheet imported with the wrong pivot or PPU, or a stray parent scale, shows up as a real
        /// error instead of cancelling out.
        /// </summary>
        public static Vector3 OccupantFloorWorld(SpriteRenderer renderer)
        {
            if (renderer == null) return Vector3.zero;
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                renderer,
                new Vector2(SeatedFloorAnchorColumnPx, 0f));
        }

        /// <summary>
        /// Sorting order for an occupant standing on <paramref name="chairFloorWorld"/>. One step in
        /// front of the chair, which shares the same floor point.
        /// </summary>
        public static int OccupantSortingOrder(Vector3 chairFloorWorld)
        {
            return OfficeGridCharacterMover.ResolveDynamicSortingOrder(chairFloorWorld) + 1;
        }
    }
}

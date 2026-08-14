using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Owns the final plane of the canonical seated occlusion contract: the base chair stays behind
    /// the actor, the single authored chair-part foreground provides complete lower-body contact,
    /// and this pose-defined actor crop restores the torso, hands and head above that foreground.
    /// </summary>
    public static class OfficeSeatedUpperBodyProtectionRules
    {
        // The pelvis anchor sits at the anatomical joint, while the visible waist/upper thigh must
        // remain in front of the chair back. Starting the redraw exactly at the joint produces a
        // straight chair-coloured band through the waist. This shared inset moves the seam down to
        // the physical seat contact without introducing character-specific coordinates.
        public const int ProtectionBelowPelvisPx = 12;

        public static int ResolveCutoffSourceY(Sprite source, Vector2 pelvisAnchorPx)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int height = Mathf.RoundToInt(source.rect.height);
            if (height < 2)
                throw new InvalidOperationException("A seating Sprite must be at least two pixels high.");
            return Mathf.Clamp(
                Mathf.FloorToInt(pelvisAnchorPx.y) - ProtectionBelowPelvisPx,
                0,
                height - 1);
        }

        public static Sprite CreateUpperBodySprite(Sprite source, int cutoffSourceY)
        {
            ValidateSprite(source, "Seating");

            Rect rect = source.rect;
            int height = Mathf.RoundToInt(rect.height);
            if (cutoffSourceY < 0 || cutoffSourceY >= height)
                throw new ArgumentOutOfRangeException(nameof(cutoffSourceY));

            var upperRect = new Rect(
                rect.x,
                rect.y + cutoffSourceY,
                rect.width,
                rect.height - cutoffSourceY);
            var clone = Sprite.Create(
                source.texture,
                upperRect,
                new Vector2(source.pivot.x / rect.width, 0f),
                source.pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect,
                Vector4.zero);
            clone.name = source.name + "_seated_upper_body_runtime_" + cutoffSourceY;
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }

        public static Vector3 LocalPosition(Sprite source, int cutoffSourceY)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new Vector3(
                0f,
                (cutoffSourceY - source.pivot.y) / source.pixelsPerUnit,
                0f);
        }

        private static void ValidateSprite(Sprite source, string label)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.texture == null)
                throw new InvalidOperationException(label + " Sprite texture is missing.");
            if (source.pixelsPerUnit <= 0f)
                throw new InvalidOperationException(label + " Sprite PPU must be positive.");
        }
    }
}

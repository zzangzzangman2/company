using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Owns the complete three-layer seated occlusion contract: the chair keeps its canonical
    /// foreground, a narrow seat-rim crop reinforces lower-body contact, and a pose-defined actor
    /// crop restores the torso, hands and head above both chair layers.
    /// </summary>
    public static class OfficeSeatedUpperBodyProtectionRules
    {
        public const int ChairLowerMinimumSourceX = 300;
        public const int ChairLowerMaximumSourceX = 342;
        public const int ChairLowerMinimumSourceY = 98;
        public const int ChairLowerMaximumSourceY = 140;
        public const int ExpectedChairLowerOpaquePixelCount = 1816;

        // The pelvis anchor sits at the anatomical joint, while the visible waist/upper thigh must
        // remain in front of the chair back. Starting the redraw exactly at the joint produces a
        // straight chair-coloured band through the waist. This shared inset moves the seam down to
        // the physical seat contact without introducing character-specific coordinates.
        public const int ProtectionBelowPelvisPx = 12;

        public static Rect ChairLowerTextureRect(Sprite baseSprite)
        {
            ValidateSprite(baseSprite, "Chair base");
            Rect source = baseSprite.rect;
            if (source.width <= ChairLowerMaximumSourceX ||
                source.height <= ChairLowerMaximumSourceY)
                throw new InvalidOperationException(
                    $"Chair base Sprite is too small for the lower-body redraw: {source.size}.");
            return new Rect(
                source.x + ChairLowerMinimumSourceX,
                source.y + ChairLowerMinimumSourceY,
                ChairLowerMaximumSourceX - ChairLowerMinimumSourceX + 1,
                ChairLowerMaximumSourceY - ChairLowerMinimumSourceY + 1);
        }

        public static Vector2 ChairLowerNormalizedPivot(Sprite baseSprite)
        {
            Rect crop = ChairLowerTextureRect(baseSprite);
            return new Vector2(
                (baseSprite.pivot.x - ChairLowerMinimumSourceX) / crop.width,
                0f);
        }

        public static Vector3 ChairLowerLocalPosition(Sprite baseSprite)
        {
            ValidateSprite(baseSprite, "Chair base");
            return new Vector3(
                0f,
                (ChairLowerMinimumSourceY - baseSprite.pivot.y) /
                baseSprite.pixelsPerUnit,
                0f);
        }

        public static bool IncludesChairLowerSourcePixel(int x, int y) =>
            x >= ChairLowerMinimumSourceX && x <= ChairLowerMaximumSourceX &&
            y >= ChairLowerMinimumSourceY && y <= ChairLowerMaximumSourceY;

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

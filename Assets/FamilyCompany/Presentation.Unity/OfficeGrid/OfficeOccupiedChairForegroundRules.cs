using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    /// <summary>
    /// Geometry for the lower-body-only chair redraw. It is rendered together with the complete
    /// canonical foreground and below the pose-split upper body, so its crop edge is never visible.
    /// </summary>
    public static class OfficeOccupiedChairForegroundRules
    {
        // Only the near seat rim/pedestal is allowed to redraw above the lower body. Extending
        // this crop into the backrest creates a conspicuous horizontal band through the torso.
        public const int MinimumSourceX = 300;
        public const int MaximumSourceX = 342;
        public const int MinimumSourceY = 98;
        public const int MaximumSourceY = 140;
        public const int ExpectedOpaquePixelCount = 1816;

        public static Rect TextureRect(Sprite baseSprite)
        {
            ValidateBaseSprite(baseSprite);
            Rect source = baseSprite.rect;
            if (source.width <= MaximumSourceX || source.height <= MaximumSourceY)
                throw new InvalidOperationException(
                    $"Chair base Sprite is too small for the lower-body redraw: {source.size}.");
            return new Rect(
                source.x + MinimumSourceX,
                source.y + MinimumSourceY,
                MaximumSourceX - MinimumSourceX + 1,
                MaximumSourceY - MinimumSourceY + 1);
        }

        public static Vector2 NormalizedPivot(Sprite baseSprite)
        {
            Rect crop = TextureRect(baseSprite);
            return new Vector2((baseSprite.pivot.x - MinimumSourceX) / crop.width, 0f);
        }

        public static Vector3 LocalPosition(Sprite baseSprite)
        {
            ValidateBaseSprite(baseSprite);
            return new Vector3(
                0f,
                (MinimumSourceY - baseSprite.pivot.y) / baseSprite.pixelsPerUnit,
                0f);
        }

        public static bool IncludesSourcePixel(int x, int y) =>
            x >= MinimumSourceX && x <= MaximumSourceX &&
            y >= MinimumSourceY && y <= MaximumSourceY;

        private static void ValidateBaseSprite(Sprite baseSprite)
        {
            if (baseSprite == null) throw new ArgumentNullException(nameof(baseSprite));
            if (baseSprite.texture == null)
                throw new InvalidOperationException("Chair base Sprite texture is missing.");
            if (baseSprite.pixelsPerUnit <= 0f)
                throw new InvalidOperationException("Chair base Sprite PPU must be positive.");
        }
    }
}

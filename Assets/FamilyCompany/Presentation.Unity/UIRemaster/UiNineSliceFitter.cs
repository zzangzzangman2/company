using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.UIRemaster
{
    /// <summary>
    /// Keeps a sliced <see cref="Image"/> readable when its authored 9-slice border is larger than
    /// the rect it has to fill.
    ///
    /// Unity's <c>Image.GetAdjustedBorders</c> shrinks the border to exactly the rect size when the
    /// two opposite borders do not fit. The centre stretch region then collapses to zero, so a
    /// rounded frame renders as nothing but its two end caps: a capsule or a blob instead of a
    /// panel. The generated MainNavigationV2 frames are authored at 2-8x their runtime size, so
    /// every badge, tab and card hit that path.
    ///
    /// <see cref="Image.pixelsPerUnitMultiplier"/> divides the border before that clamp runs, which
    /// scales the whole 9-slice art down uniformly instead of squashing it. This component drives
    /// the multiplier from the sprite height so the frame is drawn at the scale that makes its own
    /// height fit the rect: borders, corner ornaments and any medallion the border carries then keep
    /// their authored proportions, and only the flat centre stretches.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiNineSliceFitter : MonoBehaviour
    {
        /// <summary>Fraction of each axis that must remain for the stretchable centre.</summary>
        public const float MinimumCentreFraction = 0.10f;

        private Image _image;
        private Vector2 _appliedSize = new Vector2(float.NaN, float.NaN);
        private Sprite _appliedSprite;

        public static UiNineSliceFitter Attach(Image image)
        {
            if (image == null) return null;
            if (image.type != Image.Type.Sliced && image.type != Image.Type.Tiled) return null;
            var fitter = image.GetComponent<UiNineSliceFitter>();
            if (fitter == null) fitter = image.gameObject.AddComponent<UiNineSliceFitter>();
            fitter.Apply();
            return fitter;
        }

        private Image Target => _image != null ? _image : _image = GetComponent<Image>();

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        /// <summary>
        /// Recomputes the multiplier for the current rect. Safe to call repeatedly; it only touches
        /// the image when the rect or the sprite actually changed.
        /// </summary>
        public void Apply()
        {
            var image = Target;
            if (image == null) return;
            var sprite = image.sprite;
            if (sprite == null) return;
            if (image.type != Image.Type.Sliced && image.type != Image.Type.Tiled) return;

            var rect = image.rectTransform.rect;
            var size = new Vector2(rect.width, rect.height);
            if (size.x <= 0f || size.y <= 0f) return;
            if (sprite == _appliedSprite &&
                Mathf.Abs(size.x - _appliedSize.x) < 0.5f &&
                Mathf.Abs(size.y - _appliedSize.y) < 0.5f)
                return;

            var spriteRect = sprite.rect;
            image.pixelsPerUnitMultiplier = CalculateMultiplier(
                sprite.border,
                new Vector2(spriteRect.width, spriteRect.height),
                size,
                image.pixelsPerUnit,
                MinimumCentreFraction);
            _appliedSprite = sprite;
            _appliedSize = size;
        }

        /// <summary>
        /// The multiplier that draws the sprite at the scale which fits its own height into
        /// <paramref name="rectSize"/>, raised further if that would leave less than
        /// <paramref name="minimumCentreFraction"/> of either axis for the stretchable centre.
        /// Never below 1, so a frame is never enlarged past its authored resolution.
        ///
        /// Pure maths with no canvas dependency so the editor validator can assert it directly.
        /// </summary>
        public static float CalculateMultiplier(
            Vector4 spriteBorder,
            Vector2 spriteSize,
            Vector2 rectSize,
            float pixelsPerUnit,
            float minimumCentreFraction)
        {
            if (rectSize.x <= 0f || rectSize.y <= 0f) return 1f;
            var unit = pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
            var budget = Mathf.Clamp01(1f - Mathf.Clamp(minimumCentreFraction, 0.02f, 0.9f));

            // Sprite.border is x=left, y=bottom, z=right, w=top in sprite pixels.
            var horizontal = (spriteBorder.x + spriteBorder.z) / unit;
            var vertical = (spriteBorder.y + spriteBorder.w) / unit;

            var multiplier = 1f;
            if (spriteSize.y > 0f) multiplier = Mathf.Max(multiplier, spriteSize.y / unit / rectSize.y);
            if (horizontal > 0f) multiplier = Mathf.Max(multiplier, horizontal / (rectSize.x * budget));
            if (vertical > 0f) multiplier = Mathf.Max(multiplier, vertical / (rectSize.y * budget));
            return multiplier;
        }
    }
}

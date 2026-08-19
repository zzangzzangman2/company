using UnityEngine;

namespace FamilyCompany.Presentation.Unity.UIRemaster
{
    /// <summary>
    /// The opaque frame inside a generated UI texture, plus the slice insets that separate its fixed
    /// decoration from its stretchable middle. Everything is stored as a fraction of the texture.
    ///
    /// The title textures are authored on a transparent sheet up to twice as tall as the frame they
    /// contain, so drawing the whole texture into a rect renders the frame at about half the
    /// intended size. Slicing with <see cref="GUIStyle.border"/> is worse: IMGUI uses the border for
    /// both the source and the destination, so a border small enough to draw at a 100px button
    /// height cuts inside the transparent margin and smears the corner ornaments across the centre.
    ///
    /// Addressing the content window directly through
    /// <see cref="GUI.DrawTextureWithTexCoords(Rect, Texture, Rect, bool)"/> avoids both problems.
    /// The values are normalised rather than stored in pixels because the importer caps these sheets
    /// at <c>maxTextureSize 2048</c>, so the runtime texture is smaller than the authored PNG.
    /// </summary>
    public readonly struct UiRemasterArtWindow
    {
        private UiRemasterArtWindow(
            float x,
            float y,
            float width,
            float height,
            float borderLeft,
            float borderRight,
            float borderTop,
            float borderBottom)
        {
            NormalisedX = x;
            NormalisedY = y;
            NormalisedWidth = width;
            NormalisedHeight = height;
            BorderLeft = borderLeft;
            BorderRight = borderRight;
            BorderTop = borderTop;
            BorderBottom = borderBottom;
        }

        /// <summary>
        /// Builds a window from a measurement taken on the authored PNG. <paramref name="y"/> is the
        /// top edge counted downwards, matching how image tools report it. The border arguments are
        /// insets from the edges of the content window, also in authored pixels.
        /// </summary>
        public static UiRemasterArtWindow FromAuthoredPixels(
            int textureWidth,
            int textureHeight,
            int x,
            int y,
            int width,
            int height,
            int borderLeft = 0,
            int borderRight = 0,
            int borderTop = 0,
            int borderBottom = 0)
        {
            return new UiRemasterArtWindow(
                x / (float)textureWidth,
                y / (float)textureHeight,
                width / (float)textureWidth,
                height / (float)textureHeight,
                borderLeft / (float)textureWidth,
                borderRight / (float)textureWidth,
                borderTop / (float)textureHeight,
                borderBottom / (float)textureHeight);
        }

        public float NormalisedX { get; }

        /// <summary>Top edge as a fraction of the texture, counted downwards.</summary>
        public float NormalisedY { get; }

        public float NormalisedWidth { get; }
        public float NormalisedHeight { get; }

        public float BorderLeft { get; }
        public float BorderRight { get; }
        public float BorderTop { get; }
        public float BorderBottom { get; }

        public bool HasBorders =>
            BorderLeft > 0f || BorderRight > 0f || BorderTop > 0f || BorderBottom > 0f;

        /// <summary>The window as UVs, flipped to Unity's bottom-left texture origin.</summary>
        public Rect Uv => new Rect(
            NormalisedX,
            1f - (NormalisedY + NormalisedHeight),
            NormalisedWidth,
            NormalisedHeight);

        /// <summary>
        /// Scale that makes the window's own height fill <paramref name="destinationHeight"/>. Slice
        /// decoration drawn at this scale keeps its authored proportions, so only the flat middle
        /// stretches.
        /// </summary>
        public float ScaleFor(float destinationHeight, Texture texture)
        {
            if (texture == null) return 1f;
            var windowHeight = NormalisedHeight * texture.height;
            return windowHeight <= 0f ? 1f : destinationHeight / windowHeight;
        }

        /// <summary>On-screen width of the fixed left decoration for a destination of that height.</summary>
        public float LeftBorderFor(float destinationHeight, Texture texture) =>
            texture == null ? 0f : BorderLeft * texture.width * ScaleFor(destinationHeight, texture);
    }

    /// <summary>
    /// Content windows for the UI Remaster V3 title art, measured from the shipped PNGs: the window
    /// from the alpha channel (pixels above alpha 40, so the drop shadow is excluded) and the slice
    /// insets from the largest uniform run through the centre of the frame. Re-measure these if the
    /// art is re-exported.
    /// </summary>
    public static class UiRemasterTitleArt
    {
        public static readonly UiRemasterArtWindow ButtonNormal =
            UiRemasterArtWindow.FromAuthoredPixels(2172, 724, 18, 157, 2137, 357);

        public static readonly UiRemasterArtWindow ButtonHover =
            UiRemasterArtWindow.FromAuthoredPixels(2172, 724, 14, 154, 2143, 358);

        public static readonly UiRemasterArtWindow ButtonPressed =
            UiRemasterArtWindow.FromAuthoredPixels(2172, 724, 19, 128, 2134, 437);

        public static readonly UiRemasterArtWindow ButtonDisabled =
            UiRemasterArtWindow.FromAuthoredPixels(2172, 724, 60, 147, 2052, 390);

        // The save slot card is not a plain frame: a teal spine and a framed thumbnail fill its left
        // 575px, a decorative corner sits in the right 145px, and two gold rules sit a fixed
        // distance from the top and bottom. Only the cream field between them may stretch.
        public static readonly UiRemasterArtWindow SlotNormal =
            UiRemasterArtWindow.FromAuthoredPixels(2022, 778, 35, 60, 1951, 613, 575, 145, 180, 115);

        public static readonly UiRemasterArtWindow SlotSelected =
            UiRemasterArtWindow.FromAuthoredPixels(2020, 779, 30, 56, 1959, 609, 578, 145, 185, 118);

        /// <summary>
        /// Draws <paramref name="texture"/> so that <paramref name="window"/> lands on
        /// <paramref name="contentRect"/>. States whose frame is drawn heavier than the normal state
        /// (the pressed glow, the disabled plate) keep that extra weight by growing around the same
        /// centre instead of being squeezed back into the normal frame's box.
        /// </summary>
        public static void Draw(
            Rect contentRect,
            Texture2D texture,
            in UiRemasterArtWindow window,
            in UiRemasterArtWindow reference)
        {
            if (texture == null) return;
            var growX = reference.NormalisedWidth <= 0f
                ? 1f
                : window.NormalisedWidth / reference.NormalisedWidth;
            var growY = reference.NormalisedHeight <= 0f
                ? 1f
                : window.NormalisedHeight / reference.NormalisedHeight;
            var width = contentRect.width * growX;
            var height = contentRect.height * growY;
            var target = new Rect(
                contentRect.center.x - width * 0.5f,
                contentRect.center.y - height * 0.5f,
                width,
                height);
            GUI.DrawTextureWithTexCoords(target, texture, window.Uv, true);
        }

        /// <summary>
        /// Nine-slice draw where the corner size is chosen independently of the source border, which
        /// is what <see cref="GUIStyle"/> cannot do. Decoration is drawn at the scale that fits the
        /// window height into <paramref name="destination"/>, and only the middle row and column
        /// stretch.
        /// </summary>
        public static void DrawSliced(Rect destination, Texture2D texture, in UiRemasterArtWindow window)
        {
            if (texture == null) return;
            if (!window.HasBorders)
            {
                GUI.DrawTextureWithTexCoords(destination, texture, window.Uv, true);
                return;
            }

            var scale = window.ScaleFor(destination.height, texture);
            var left = window.BorderLeft * texture.width * scale;
            var right = window.BorderRight * texture.width * scale;
            var top = window.BorderTop * texture.height * scale;
            var bottom = window.BorderBottom * texture.height * scale;

            // Always leave a tenth of each axis for the stretchable middle.
            var horizontal = left + right;
            if (horizontal > destination.width * 0.9f && horizontal > 0f)
            {
                var factor = destination.width * 0.9f / horizontal;
                left *= factor;
                right *= factor;
            }

            var vertical = top + bottom;
            if (vertical > destination.height * 0.9f && vertical > 0f)
            {
                var factor = destination.height * 0.9f / vertical;
                top *= factor;
                bottom *= factor;
            }

            var sourceX = new[] { window.NormalisedX, window.NormalisedX + window.BorderLeft, window.NormalisedX + window.NormalisedWidth - window.BorderRight, window.NormalisedX + window.NormalisedWidth };
            var sourceY = new[] { window.NormalisedY, window.NormalisedY + window.BorderTop, window.NormalisedY + window.NormalisedHeight - window.BorderBottom, window.NormalisedY + window.NormalisedHeight };
            var destinationX = new[] { destination.x, destination.x + left, destination.xMax - right, destination.xMax };
            var destinationY = new[] { destination.y, destination.y + top, destination.yMax - bottom, destination.yMax };

            for (var column = 0; column < 3; column++)
            {
                for (var row = 0; row < 3; row++)
                {
                    var piece = new Rect(
                        destinationX[column],
                        destinationY[row],
                        destinationX[column + 1] - destinationX[column],
                        destinationY[row + 1] - destinationY[row]);
                    if (piece.width <= 0f || piece.height <= 0f) continue;
                    var uv = new Rect(
                        sourceX[column],
                        1f - sourceY[row + 1],
                        sourceX[column + 1] - sourceX[column],
                        sourceY[row + 1] - sourceY[row]);
                    GUI.DrawTextureWithTexCoords(piece, texture, uv, true);
                }
            }
        }
    }
}

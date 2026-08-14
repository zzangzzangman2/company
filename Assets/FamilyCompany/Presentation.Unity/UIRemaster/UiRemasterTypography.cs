using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.UIRemaster
{
    public static class UiRemasterTypography
    {
        public const string FontCatalogResourcePath = "UiRemasterV3/UiRemasterFontCatalog_v3";
        public const int ReferenceWidth = 1280;
        public const int ReferenceHeight = 720;

        public const int PanelTitlePixels = 28;
        public const int CardTitlePixels = 20;
        public const int BodyPixels = 16;
        public const int TopHudPixels = 18;
        public const int BottomNavigationPixels = 17;
        public const int ButtonPixels = 16;
        public const int CaptionPixels = 14;
        public const int MainTitlePixels = 44;

        public const float BodyLineHeight = 1.35f;
        public const float TitleLineHeight = 1.20f;

        public static float CalculateScale(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            var raw = Mathf.Min(pixelWidth / (float)ReferenceWidth, pixelHeight / (float)ReferenceHeight);
            return Mathf.Clamp(raw, 1f, 1.5f);
        }

        public static int Pixels(float referencePixels, float scale)
        {
            if (referencePixels < 0f) throw new ArgumentOutOfRangeException(nameof(referencePixels));
            return Mathf.Max(0, Mathf.RoundToInt(referencePixels * scale));
        }

        public static Rect PixelSnap(Rect rect)
        {
            return new Rect(
                Mathf.Round(rect.x),
                Mathf.Round(rect.y),
                Mathf.Round(rect.width),
                Mathf.Round(rect.height));
        }

        public static Rect CenterUsingFontMetrics(Rect bounds, GUIContent content, GUIStyle style)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            var measured = style.CalcSize(content ?? GUIContent.none);
            var height = Mathf.Min(bounds.height, Mathf.Ceil(measured.y));
            return PixelSnap(new Rect(bounds.x, bounds.y + (bounds.height - height) * 0.5f, bounds.width, height));
        }

        public static bool Fits(Rect bounds, GUIContent content, GUIStyle style, out Vector2 measured)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            measured = style.CalcSize(content ?? GUIContent.none);
            return measured.x <= bounds.width + 0.01f && measured.y <= bounds.height + 0.01f;
        }

        public static GUIStyle CreateLabel(
            GUIStyle source,
            Font font,
            int referencePixels,
            float scale,
            TextAnchor alignment,
            Color textColor,
            bool wordWrap = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (font == null) throw new ArgumentNullException(nameof(font));
            return new GUIStyle(source)
            {
                font = font,
                fontSize = Pixels(referencePixels, scale),
                fontStyle = FontStyle.Normal,
                alignment = alignment,
                wordWrap = wordWrap,
                clipping = TextClipping.Clip,
                normal = { textColor = textColor }
            };
        }

        public static bool TryLoadFonts(out Font body, out Font heading, out Font fallback, out string error)
        {
            var catalog = Resources.Load<UiRemasterFontCatalog>(FontCatalogResourcePath);
            if (catalog == null || !catalog.IsComplete)
            {
                body = null;
                heading = null;
                fallback = null;
                error = "UI Remaster V3 font catalog is missing or incomplete.";
                return false;
            }

            body = catalog.BodySource;
            heading = catalog.HeadingSource;
            fallback = catalog.FallbackSource;
            if (body.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) < 0 ||
                heading.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = "UI Remaster V3 primary fonts are not the canonical Maplestory Light/Bold family.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}

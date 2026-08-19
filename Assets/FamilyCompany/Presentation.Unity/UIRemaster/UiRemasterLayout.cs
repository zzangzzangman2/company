using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.UIRemaster
{
    public readonly struct UiRemasterTitleLayout
    {
        public UiRemasterTitleLayout(Rect logo, Rect[] buttons, Rect subtitle, Rect footer)
        {
            Logo = logo;
            Buttons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            Subtitle = subtitle;
            Footer = footer;
        }

        public Rect Logo { get; }
        public Rect[] Buttons { get; }
        public Rect Subtitle { get; }
        public Rect Footer { get; }
    }

    public readonly struct UiRemasterLoadingLayout
    {
        public UiRemasterLoadingLayout(Rect panel, Rect icon, Rect title, Rect status, Rect track, Rect percent, Rect detail)
        {
            Panel = panel;
            Icon = icon;
            Title = title;
            Status = status;
            Track = track;
            Percent = percent;
            Detail = detail;
        }

        public Rect Panel { get; }
        public Rect Icon { get; }
        public Rect Title { get; }
        public Rect Status { get; }
        public Rect Track { get; }
        public Rect Percent { get; }
        public Rect Detail { get; }
    }

    public static class UiRemasterLayout
    {
        public const float OuterSafeMargin = 24f;
        public const float SmallGap = 10f;
        public const float Gap = 16f;
        public const float LargeGap = 24f;
        public const float TextInset = 24f;
        public const float IconTextGap = 14f;

        /// <summary>Content aspect of <c>title_button_normal_v3</c>; see UiRemasterTitleArt.</summary>
        public const float TitleButtonAspect = 5.986f;

        public static UiRemasterTitleLayout CalculateTitle(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            var scale = UiRemasterTypography.CalculateScale(pixelWidth, pixelHeight);
            var margin = UiRemasterTypography.Pixels(OuterSafeMargin, scale);
            var logoWidth = Mathf.Min(UiRemasterTypography.Pixels(390f, scale), pixelWidth - margin * 2f);
            var logoHeight = Mathf.Round(logoWidth / 2.89f);
            var logo = UiRemasterTypography.PixelSnap(new Rect(margin, margin, logoWidth, logoHeight));
            var subtitle = UiRemasterTypography.PixelSnap(new Rect(
                margin + UiRemasterTypography.Pixels(12f, scale),
                logo.yMax + UiRemasterTypography.Pixels(4f, scale),
                logoWidth - UiRemasterTypography.Pixels(24f, scale),
                UiRemasterTypography.Pixels(26f, scale)));

            // The title button frame is authored at a 5.99:1 content aspect. Sizing the rect to that
            // ratio lets the art be drawn straight into it with no distortion and no 9-slice.
            var buttonWidth = Mathf.Min(UiRemasterTypography.Pixels(400f, scale), pixelWidth - margin * 2f);
            var buttonHeight = Mathf.Round(buttonWidth / TitleButtonAspect);
            var gap = UiRemasterTypography.Pixels(12f, scale);
            var firstY = Mathf.Max(
                subtitle.yMax + UiRemasterTypography.Pixels(18f, scale),
                UiRemasterTypography.Pixels(218f, scale));
            var buttons = new Rect[5];
            for (var index = 0; index < buttons.Length; index++)
            {
                buttons[index] = UiRemasterTypography.PixelSnap(new Rect(
                    margin + UiRemasterTypography.Pixels(12f, scale),
                    firstY + index * (buttonHeight + gap),
                    buttonWidth,
                    buttonHeight));
            }

            var footer = UiRemasterTypography.PixelSnap(new Rect(
                margin + UiRemasterTypography.Pixels(12f, scale),
                Mathf.Min(pixelHeight - margin - UiRemasterTypography.Pixels(24f, scale),
                    buttons[buttons.Length - 1].yMax + UiRemasterTypography.Pixels(16f, scale)),
                buttonWidth,
                UiRemasterTypography.Pixels(24f, scale)));
            return new UiRemasterTitleLayout(logo, buttons, subtitle, footer);
        }

        public static UiRemasterLoadingLayout CalculateLoading(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            var scale = UiRemasterTypography.CalculateScale(pixelWidth, pixelHeight);
            var panelWidth = Mathf.Min(UiRemasterTypography.Pixels(620f, scale), pixelWidth - 48f);
            var panelHeight = UiRemasterTypography.Pixels(270f, scale);
            var panel = UiRemasterTypography.PixelSnap(new Rect(
                UiRemasterTypography.Pixels(48f, scale),
                (pixelHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight));
            var inset = UiRemasterTypography.Pixels(TextInset, scale);
            var iconSize = UiRemasterTypography.Pixels(54f, scale);
            var icon = UiRemasterTypography.PixelSnap(new Rect(panel.x + inset, panel.y + inset, iconSize, iconSize));
            var textX = icon.xMax + UiRemasterTypography.Pixels(IconTextGap, scale);
            var textWidth = panel.xMax - inset - textX;
            var title = UiRemasterTypography.PixelSnap(new Rect(textX, panel.y + inset, textWidth, UiRemasterTypography.Pixels(38f, scale)));
            var status = UiRemasterTypography.PixelSnap(new Rect(textX, title.yMax + UiRemasterTypography.Pixels(4f, scale), textWidth, UiRemasterTypography.Pixels(30f, scale)));
            var track = UiRemasterTypography.PixelSnap(new Rect(panel.x + inset, panel.y + UiRemasterTypography.Pixels(112f, scale), panel.width - inset * 2f, UiRemasterTypography.Pixels(34f, scale)));
            var percent = UiRemasterTypography.PixelSnap(new Rect(panel.x + inset, track.yMax + UiRemasterTypography.Pixels(8f, scale), panel.width - inset * 2f, UiRemasterTypography.Pixels(28f, scale)));
            // Keep the final status copy on the same baseline axis as the title/status text.
            // The generated panel has coral corner tabs in the lower safe area, so the generic
            // panel inset is not sufficient for Korean glyphs at the minimum resolution.
            var detail = UiRemasterTypography.PixelSnap(new Rect(
                textX,
                percent.yMax + UiRemasterTypography.Pixels(4f, scale),
                textWidth,
                UiRemasterTypography.Pixels(48f, scale)));
            return new UiRemasterLoadingLayout(panel, icon, title, status, track, percent, detail);
        }
    }
}

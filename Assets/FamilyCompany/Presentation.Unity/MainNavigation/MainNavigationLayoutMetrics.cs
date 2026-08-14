using System;
using FamilyCompany.Simulation.ManagementUi;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public sealed class MainNavigationLayoutSnapshot
    {
        public MainNavigationLayoutSnapshot(
            double scaleFactor,
            UiPixelRect safeArea,
            UiPixelRect topHud,
            UiPixelRect contentPanel,
            UiPixelRect bottomNavigation,
            UiPixelRect[] tabHitTargets)
        {
            ScaleFactor = scaleFactor;
            SafeArea = safeArea;
            TopHud = topHud;
            ContentPanel = contentPanel;
            BottomNavigation = bottomNavigation;
            TabHitTargets = tabHitTargets ?? throw new ArgumentNullException(nameof(tabHitTargets));
        }

        public double ScaleFactor { get; }
        public UiPixelRect SafeArea { get; }
        public UiPixelRect TopHud { get; }
        public UiPixelRect ContentPanel { get; }
        public UiPixelRect BottomNavigation { get; }
        public UiPixelRect[] TabHitTargets { get; }
    }

    public static class MainNavigationLayoutMetrics
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        public const double MatchWidthOrHeight = 0.5d;
        public const double OuterMargin = 28d;
        public const double TopHudHeight = 68d;
        public const double BottomNavigationWidth = 1120d;
        public const double BottomNavigationHeight = 100d;
        public const double ContentPanelWidth = 1120d;
        public const double ContentPanelHeight = 660d;
        public const double RegionGap = 24d;
        public const double TabGap = 10d;
        public const double TabHorizontalPadding = 16d;
        public const double MinimumHitTarget = 44d;

        public static float ContentPanelWidthFor(int pixelWidth)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            return (float)Math.Max(1040d, Math.Min(1180d, pixelWidth * (1120d / ReferenceWidth)));
        }

        public static float ContentPanelHeightFor(int pixelHeight)
        {
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            return (float)Math.Max(500d, Math.Min(680d, pixelHeight * (680d / ReferenceHeight)));
        }

        public static MainNavigationLayoutSnapshot Calculate(
            int pixelWidth,
            int pixelHeight,
            UiSafeInsets safeInsets)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            if (safeInsets.Left + safeInsets.Right >= pixelWidth ||
                safeInsets.Top + safeInsets.Bottom >= pixelHeight)
                throw new ArgumentException("Safe area must leave a positive drawable region.", nameof(safeInsets));

            var widthScale = pixelWidth / (double)ReferenceWidth;
            var heightScale = pixelHeight / (double)ReferenceHeight;
            var scale = Math.Exp(
                Math.Log(widthScale) * (1d - MatchWidthOrHeight) +
                Math.Log(heightScale) * MatchWidthOrHeight);
            var safe = new UiPixelRect(
                safeInsets.Left,
                safeInsets.Top,
                pixelWidth - safeInsets.Left - safeInsets.Right,
                pixelHeight - safeInsets.Top - safeInsets.Bottom);
            var margin = Math.Max(18d, Math.Min(32d, OuterMargin * scale));
            var topHeight = Math.Max(56d, Math.Min(72d, TopHudHeight * scale));
            var bottomHeight = Math.Max(88d, Math.Min(104d, BottomNavigationHeight * scale));
            var regionGap = Math.Max(14d, Math.Min(24d, RegionGap * scale));
            var top = new UiPixelRect(
                safe.X + margin,
                safe.Y + margin,
                safe.Width - margin * 2d,
                topHeight);
            var bottomWidth = Math.Min(
                safe.Width - margin * 2d,
                Math.Max(760d, Math.Min(1120d, BottomNavigationWidth * scale)));
            var bottom = new UiPixelRect(
                safe.X + (safe.Width - bottomWidth) * 0.5d,
                safe.Bottom - margin - bottomHeight,
                bottomWidth,
                bottomHeight);
            var contentTop = top.Bottom + regionGap;
            var contentBottom = bottom.Y - regionGap;
            var panelWidth = Math.Min(safe.Width - margin * 2d, ContentPanelWidthFor(pixelWidth));
            var panelHeight = Math.Min(ContentPanelHeightFor(pixelHeight), contentBottom - contentTop);
            var panel = new UiPixelRect(
                safe.X + (safe.Width - panelWidth) * 0.5d,
                contentTop + Math.Max(0d, contentBottom - contentTop - panelHeight) * 0.5d,
                panelWidth,
                panelHeight);

            var hitTargets = new UiPixelRect[5];
            var hitGap = TabGap * scale;
            var horizontalPadding = TabHorizontalPadding * scale;
            var hitWidth = (bottom.Width - horizontalPadding * 2d - hitGap * 4d) / hitTargets.Length;
            var hitHeight = bottom.Height - horizontalPadding * 2d;
            for (var index = 0; index < hitTargets.Length; index++)
            {
                hitTargets[index] = new UiPixelRect(
                    bottom.X + horizontalPadding + index * (hitWidth + hitGap),
                    bottom.Y + horizontalPadding,
                    hitWidth,
                    hitHeight);
            }

            return new MainNavigationLayoutSnapshot(scale, safe, top, panel, bottom, hitTargets);
        }

        public static void Validate(MainNavigationLayoutSnapshot layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (!layout.SafeArea.Contains(layout.TopHud) ||
                !layout.SafeArea.Contains(layout.ContentPanel) ||
                !layout.SafeArea.Contains(layout.BottomNavigation))
                throw new InvalidOperationException("A main navigation region escapes the safe area.");
            if (layout.TopHud.Overlaps(layout.ContentPanel) ||
                layout.ContentPanel.Overlaps(layout.BottomNavigation) ||
                layout.TopHud.Overlaps(layout.BottomNavigation))
                throw new InvalidOperationException("Main navigation regions overlap.");
            if (layout.TabHitTargets.Length != 5)
                throw new InvalidOperationException("Exactly five navigation hit targets are required.");
            for (var index = 0; index < layout.TabHitTargets.Length; index++)
            {
                var target = layout.TabHitTargets[index];
                if (!layout.BottomNavigation.Contains(target))
                    throw new InvalidOperationException($"Navigation hit target {index} escapes the bottom bar.");
                if (target.Width < MinimumHitTarget * layout.ScaleFactor ||
                    target.Height < MinimumHitTarget * layout.ScaleFactor)
                    throw new InvalidOperationException($"Navigation hit target {index} is smaller than the 44px contract.");
                for (var other = index + 1; other < layout.TabHitTargets.Length; other++)
                    if (target.Overlaps(layout.TabHitTargets[other]))
                        throw new InvalidOperationException($"Navigation hit targets {index} and {other} overlap.");
            }
        }
    }
}

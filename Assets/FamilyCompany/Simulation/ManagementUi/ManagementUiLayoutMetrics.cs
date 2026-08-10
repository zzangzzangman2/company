using System;

namespace FamilyCompany.Simulation.ManagementUi
{
    public readonly struct UiRgb
    {
        public UiRgb(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }

        public static UiRgb FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length != 6)
                throw new ArgumentException("RGB colors must contain exactly six hexadecimal characters.", nameof(hex));
            return new UiRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }

        public double RelativeLuminance =>
            0.2126d * Linearize(Red) + 0.7152d * Linearize(Green) + 0.0722d * Linearize(Blue);

        private static double Linearize(byte component)
        {
            var value = component / 255d;
            return value <= 0.04045d
                ? value / 12.92d
                : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }
    }

    public static class ManagementUiAccessibility
    {
        public const string PageHex = "F2EDE2";
        public const string PanelHex = "FFFCF5";
        public const string CardHex = "FFFFFF";
        public const string TextHex = "172725";
        public const string SecondaryTextHex = "384946";
        public const string AccentHex = "086A60";
        public const string DisabledHex = "D8E0DD";
        public const string DisabledTextHex = "52615E";
        public const double MinimumBodyContrast = 4.5d;

        public static double ContrastRatio(string foregroundHex, string backgroundHex)
        {
            var foreground = UiRgb.FromHex(foregroundHex).RelativeLuminance;
            var background = UiRgb.FromHex(backgroundHex).RelativeLuminance;
            var light = Math.Max(foreground, background);
            var dark = Math.Min(foreground, background);
            return (light + 0.05d) / (dark + 0.05d);
        }

        public static void Validate()
        {
            ValidatePair("body/page", TextHex, PageHex);
            ValidatePair("body/panel", SecondaryTextHex, PanelHex);
            ValidatePair("body/card", SecondaryTextHex, CardHex);
            ValidatePair("primary button", CardHex, AccentHex);
            ValidatePair("disabled button", DisabledTextHex, DisabledHex);
        }

        private static void ValidatePair(string label, string foregroundHex, string backgroundHex)
        {
            var ratio = ContrastRatio(foregroundHex, backgroundHex);
            if (ratio < MinimumBodyContrast)
                throw new InvalidOperationException($"{label} contrast {ratio:0.##}:1 is below {MinimumBodyContrast:0.0}:1.");
        }
    }

    public readonly struct UiSafeInsets
    {
        public UiSafeInsets(double left, double top, double right, double bottom)
        {
            if (left < 0d || top < 0d || right < 0d || bottom < 0d)
                throw new ArgumentOutOfRangeException(nameof(left), "Safe-area insets cannot be negative.");
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }
        public static UiSafeInsets None => new UiSafeInsets(0d, 0d, 0d, 0d);
    }

    public readonly struct UiPixelRect
    {
        public UiPixelRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public bool Contains(UiPixelRect other, double tolerance = 0.001d)
        {
            return other.X >= X - tolerance &&
                   other.Y >= Y - tolerance &&
                   other.Right <= Right + tolerance &&
                   other.Bottom <= Bottom + tolerance;
        }

        public bool Overlaps(UiPixelRect other, double tolerance = 0.001d)
        {
            return X < other.Right - tolerance && Right > other.X + tolerance &&
                   Y < other.Bottom - tolerance && Bottom > other.Y + tolerance;
        }

        public override string ToString()
        {
            return $"({X:0.##}, {Y:0.##}, {Width:0.##}, {Height:0.##})";
        }
    }

    public sealed class ManagementUiLayoutSnapshot
    {
        public ManagementUiLayoutSnapshot(
            int pixelWidth,
            int pixelHeight,
            double scaleFactor,
            UiPixelRect safeArea,
            UiPixelRect topHud,
            UiPixelRect familyRail,
            UiPixelRect managementCenter,
            UiPixelRect quickActions,
            UiPixelRect tabs,
            UiPixelRect offers,
            UiPixelRect progress,
            UiPixelRect[] offerCards)
        {
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            ScaleFactor = scaleFactor;
            SafeArea = safeArea;
            TopHud = topHud;
            FamilyRail = familyRail;
            ManagementCenter = managementCenter;
            QuickActions = quickActions;
            Tabs = tabs;
            Offers = offers;
            Progress = progress;
            OfferCards = offerCards ?? throw new ArgumentNullException(nameof(offerCards));
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public double ScaleFactor { get; }
        public UiPixelRect SafeArea { get; }
        public UiPixelRect TopHud { get; }
        public UiPixelRect FamilyRail { get; }
        public UiPixelRect ManagementCenter { get; }
        public UiPixelRect QuickActions { get; }
        public UiPixelRect Tabs { get; }
        public UiPixelRect Offers { get; }
        public UiPixelRect Progress { get; }
        public UiPixelRect[] OfferCards { get; }
    }

    public static class ManagementUiLayoutMetrics
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        public const double MatchWidthOrHeight = 0.5d;
        public const double OuterMargin = 24d;
        public const double Gap = 16d;
        public const double TopHudHeight = 88d;
        public const double RailWidth = 288d;
        public const double TabsHeight = 56d;
        public const double ProgressHeight = 280d;
        public const double MinimumOfferCardWidthPixels = 264d;
        public const string SkinResourcePath = "ManagementUI/ManagementUiSkin_v1";
        public const string FontCatalogResourcePath = "ManagementUI/ManagementUiFontCatalog_v1";

        public static ManagementUiLayoutSnapshot Calculate(
            int pixelWidth,
            int pixelHeight,
            UiSafeInsets safeInsets)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            if (safeInsets.Left + safeInsets.Right >= pixelWidth ||
                safeInsets.Top + safeInsets.Bottom >= pixelHeight)
            {
                throw new ArgumentException("Safe area must leave a positive drawable region.", nameof(safeInsets));
            }

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
            var margin = OuterMargin * scale;
            var gap = Gap * scale;
            var topHeight = TopHudHeight * scale;
            var railWidth = RailWidth * scale;
            var tabsHeight = TabsHeight * scale;
            var progressHeight = ProgressHeight * scale;
            var contentX = safe.X + margin;
            var contentWidth = safe.Width - margin * 2d;
            var topHud = new UiPixelRect(contentX, safe.Y + margin, contentWidth, topHeight);
            var bodyY = topHud.Bottom + gap;
            var bodyHeight = safe.Bottom - margin - bodyY;
            var family = new UiPixelRect(contentX, bodyY, railWidth, bodyHeight);
            var quick = new UiPixelRect(contentX + contentWidth - railWidth, bodyY, railWidth, bodyHeight);
            var centerX = family.Right + gap;
            var centerWidth = quick.X - gap - centerX;
            var center = new UiPixelRect(centerX, bodyY, centerWidth, bodyHeight);
            var tabs = new UiPixelRect(center.X, center.Y, center.Width, tabsHeight);
            var progress = new UiPixelRect(center.X, center.Bottom - progressHeight, center.Width, progressHeight);
            var offers = new UiPixelRect(
                center.X,
                tabs.Bottom + gap,
                center.Width,
                progress.Y - gap - (tabs.Bottom + gap));
            var cardWidth = (offers.Width - gap * 2d) / 3d;
            var cards = new UiPixelRect[3];
            for (var index = 0; index < cards.Length; index++)
                cards[index] = new UiPixelRect(offers.X + index * (cardWidth + gap), offers.Y, cardWidth, offers.Height);

            return new ManagementUiLayoutSnapshot(
                pixelWidth,
                pixelHeight,
                scale,
                safe,
                topHud,
                family,
                center,
                quick,
                tabs,
                offers,
                progress,
                cards);
        }

        public static void Validate(ManagementUiLayoutSnapshot layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (!layout.SafeArea.Contains(layout.TopHud) ||
                !layout.SafeArea.Contains(layout.FamilyRail) ||
                !layout.SafeArea.Contains(layout.ManagementCenter) ||
                !layout.SafeArea.Contains(layout.QuickActions))
            {
                throw new InvalidOperationException("A primary management region escapes the safe area.");
            }

            if (layout.FamilyRail.Overlaps(layout.ManagementCenter) ||
                layout.ManagementCenter.Overlaps(layout.QuickActions) ||
                layout.FamilyRail.Overlaps(layout.QuickActions))
            {
                throw new InvalidOperationException("Primary management columns overlap.");
            }

            if (!layout.ManagementCenter.Contains(layout.Tabs) ||
                !layout.ManagementCenter.Contains(layout.Offers) ||
                !layout.ManagementCenter.Contains(layout.Progress) ||
                layout.Tabs.Overlaps(layout.Offers) ||
                layout.Offers.Overlaps(layout.Progress))
            {
                throw new InvalidOperationException("Center management rows overlap or escape their parent.");
            }

            for (var index = 0; index < layout.OfferCards.Length; index++)
            {
                var card = layout.OfferCards[index];
                if (!layout.Offers.Contains(card)) throw new InvalidOperationException($"Offer card {index} escapes its row.");
                if (card.Width < MinimumOfferCardWidthPixels)
                    throw new InvalidOperationException($"Offer card {index} is narrower than the readable minimum.");
                for (var other = index + 1; other < layout.OfferCards.Length; other++)
                    if (card.Overlaps(layout.OfferCards[other]))
                        throw new InvalidOperationException($"Offer cards {index} and {other} overlap.");
            }
        }
    }
}

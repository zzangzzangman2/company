using System;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Engine-independent rectangle used by the stock-market IMGUI presentation.
    /// Keeping the layout math free of Unity types lets validation cover multiple
    /// desktop aspect ratios without opening a scene.
    /// </summary>
    public readonly struct StockMarketPanelRect
    {
        public StockMarketPanelRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public bool Overlaps(StockMarketPanelRect other)
        {
            return X < other.Right && Right > other.X &&
                   Y < other.Bottom && Bottom > other.Y;
        }
    }

    public readonly struct StockMarketViewport
    {
        public StockMarketViewport(
            float scale,
            float offsetX,
            float offsetY,
            float logicalWidth,
            float logicalHeight)
        {
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            LogicalWidth = logicalWidth;
            LogicalHeight = logicalHeight;
        }

        public float Scale { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float LogicalWidth { get; }
        public float LogicalHeight { get; }
        public float PixelWidth => LogicalWidth * Scale;
        public float PixelHeight => LogicalHeight * Scale;
    }

    public sealed class StockMarketLandscapeLayout
    {
        public const float ReferenceHeight = 1080f;
        public const float MinimumCanvasWidth = 1600f;
        public const float MaximumCanvasWidth = 3200f;
        public const int MinimumReadablePixelWidth = 960;
        public const int MinimumReadablePixelHeight = 540;
        public const float OuterMargin = 24f;
        public const float ColumnGap = 18f;

        private const float HeaderHeight = 86f;
        private const float ContentTop = 104f;
        private const float ContentBottomMargin = 24f;
        private const float WatchlistWidth = 330f;
        private const float OrderBookWidth = 400f;
        private const float OrderTicketWidth = 400f;

        private StockMarketLandscapeLayout(float canvasWidth)
        {
            CanvasWidth = canvasWidth;
            Header = new StockMarketPanelRect(0f, 0f, canvasWidth, HeaderHeight);

            var contentHeight = ReferenceHeight - ContentTop - ContentBottomMargin;
            var centerWidth = canvasWidth -
                              OuterMargin * 2f -
                              ColumnGap * 3f -
                              WatchlistWidth -
                              OrderBookWidth -
                              OrderTicketWidth;
            Watchlist = new StockMarketPanelRect(
                OuterMargin,
                ContentTop,
                WatchlistWidth,
                contentHeight);
            Chart = new StockMarketPanelRect(
                Watchlist.Right + ColumnGap,
                ContentTop,
                centerWidth,
                610f);
            Activity = new StockMarketPanelRect(
                Chart.X,
                Chart.Bottom + ColumnGap,
                centerWidth,
                contentHeight - Chart.Height - ColumnGap);
            OrderBook = new StockMarketPanelRect(
                Chart.Right + ColumnGap,
                ContentTop,
                OrderBookWidth,
                contentHeight);
            OrderTicket = new StockMarketPanelRect(
                OrderBook.Right + ColumnGap,
                ContentTop,
                OrderTicketWidth,
                contentHeight);
        }

        public float CanvasWidth { get; }
        public StockMarketPanelRect Header { get; }
        public StockMarketPanelRect Watchlist { get; }
        public StockMarketPanelRect Chart { get; }
        public StockMarketPanelRect Activity { get; }
        public StockMarketPanelRect OrderBook { get; }
        public StockMarketPanelRect OrderTicket { get; }

        public static StockMarketViewport CalculateViewport(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));

            var heightScale = pixelHeight / ReferenceHeight;
            var minimumWidthScale = pixelWidth / MinimumCanvasWidth;
            var scale = Math.Max(0.01f, Math.Min(heightScale, minimumWidthScale));
            var logicalWidth = Math.Min(MaximumCanvasWidth, pixelWidth / scale);
            var pixelCanvasWidth = logicalWidth * scale;
            var pixelCanvasHeight = ReferenceHeight * scale;
            return new StockMarketViewport(
                scale,
                (pixelWidth - pixelCanvasWidth) * 0.5f,
                (pixelHeight - pixelCanvasHeight) * 0.5f,
                logicalWidth,
                ReferenceHeight);
        }

        public static bool RequiresMinimumSizeNotice(int pixelWidth, int pixelHeight)
        {
            return pixelWidth < MinimumReadablePixelWidth || pixelHeight < MinimumReadablePixelHeight;
        }

        public static StockMarketLandscapeLayout Create(float logicalCanvasWidth)
        {
            var width = Math.Max(MinimumCanvasWidth, Math.Min(MaximumCanvasWidth, logicalCanvasWidth));
            var layout = new StockMarketLandscapeLayout(width);
            layout.ValidateOrThrow();
            return layout;
        }

        public void ValidateOrThrow()
        {
            if (Chart.Width < 360f)
                throw new InvalidOperationException("Stock chart is too narrow for the desktop layout.");
            AssertInside(nameof(Header), Header);
            AssertInside(nameof(Watchlist), Watchlist);
            AssertInside(nameof(Chart), Chart);
            AssertInside(nameof(Activity), Activity);
            AssertInside(nameof(OrderBook), OrderBook);
            AssertInside(nameof(OrderTicket), OrderTicket);

            if (Watchlist.Overlaps(Chart) || Chart.Overlaps(OrderBook) || OrderBook.Overlaps(OrderTicket))
                throw new InvalidOperationException("Stock-market columns overlap.");
            if (Chart.Overlaps(Activity))
                throw new InvalidOperationException("Stock chart and activity panel overlap.");
        }

        private void AssertInside(string name, StockMarketPanelRect rect)
        {
            if (rect.X < 0f || rect.Y < 0f || rect.Width <= 0f || rect.Height <= 0f ||
                rect.Right > CanvasWidth + 0.01f || rect.Bottom > ReferenceHeight + 0.01f)
            {
                throw new InvalidOperationException($"{name} is outside the stock-market canvas.");
            }
        }
    }
}

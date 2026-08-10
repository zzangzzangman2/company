using System;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.UI
{
    public struct OfficeSeatingUiRect
    {
        public OfficeSeatingUiRect(float x, float y, float width, float height)
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
        public float XMax => X + Width;
        public float YMax => Y + Height;
    }

    public sealed class OfficeSeatPlacementLayout
    {
        internal OfficeSeatPlacementLayout(
            int screenWidth,
            int screenHeight,
            float scale,
            OfficeSeatingUiRect panel,
            OfficeSeatingUiRect title,
            OfficeSeatingUiRect seatSummary,
            OfficeSeatingUiRect memberList,
            OfficeSeatingUiRect message,
            OfficeSeatingUiRect actions)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Scale = scale;
            Panel = panel;
            Title = title;
            SeatSummary = seatSummary;
            MemberList = memberList;
            Message = message;
            Actions = actions;
        }

        public int ScreenWidth { get; }
        public int ScreenHeight { get; }
        public float Scale { get; }
        public OfficeSeatingUiRect Panel { get; }
        public OfficeSeatingUiRect Title { get; }
        public OfficeSeatingUiRect SeatSummary { get; }
        public OfficeSeatingUiRect MemberList { get; }
        public OfficeSeatingUiRect Message { get; }
        public OfficeSeatingUiRect Actions { get; }
    }

    public static class OfficeSeatingUiLayout
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;

        public static OfficeSeatPlacementLayout Calculate(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            if (screenHeight <= 0) throw new ArgumentOutOfRangeException(nameof(screenHeight));

            var scale = Math.Min(
                screenWidth / (float)ReferenceWidth,
                screenHeight / (float)ReferenceHeight);
            var panelWidth = 600f * scale;
            var panelHeight = 780f * scale;
            var margin = 36f * scale;
            var panel = new OfficeSeatingUiRect(
                screenWidth - margin - panelWidth,
                (screenHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            var inset = 34f * scale;
            var contentWidth = panel.Width - (inset * 2f);
            return new OfficeSeatPlacementLayout(
                screenWidth,
                screenHeight,
                scale,
                panel,
                new OfficeSeatingUiRect(panel.X + inset, panel.Y + 28f * scale, contentWidth, 62f * scale),
                new OfficeSeatingUiRect(panel.X + inset, panel.Y + 112f * scale, contentWidth, 112f * scale),
                new OfficeSeatingUiRect(panel.X + inset, panel.Y + 250f * scale, contentWidth, 302f * scale),
                new OfficeSeatingUiRect(panel.X + inset, panel.Y + 570f * scale, contentWidth, 74f * scale),
                new OfficeSeatingUiRect(panel.X + inset, panel.Y + 668f * scale, contentWidth, 76f * scale));
        }
    }
}

using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Look of the in-game layout editor. The palette is sampled from the generated
    /// MainNavigationV2 frames so the editor reads as the same game as the hubs it opens from:
    /// cream paper, a gold rim, deep teal ink and a coral accent for destructive actions.
    /// Textures are generated once and reused; nothing here is loaded from disk except the Korean
    /// font, which falls back to the built-in font when it is missing.
    ///
    /// These are IMGUI styles, so their backgrounds are nine-sliced by <see cref="GUIStyle.border"/>,
    /// which uses the same pixel count for source and destination. The generated textures are
    /// therefore authored at exactly the size their border expects rather than being scaled art.
    /// </summary>
    public sealed class OfficeLayoutEditModeSkin
    {
        // Sampled from Assets/Art/UI/Resources/MainNavigationV2/Frames.
        public static readonly Color Ink = new Color32(0x24, 0x54, 0x54, 0xFF);
        public static readonly Color InkSoft = new Color32(0x5E, 0x7C, 0x76, 0xFF);
        public static readonly Color Cream = new Color32(0xFC, 0xF0, 0xD8, 0xFF);
        public static readonly Color Panel = new Color32(0xFC, 0xF0, 0xD8, 0xFA);
        public static readonly Color PanelHeader = new Color32(0xF0, 0xE4, 0xCC, 0xFF);
        public static readonly Color Gold = new Color32(0xE4, 0x9C, 0x3C, 0xFF);
        public static readonly Color GoldSoft = new Color32(0xEC, 0xC8, 0x84, 0xFF);
        public static readonly Color Mint = new Color32(0xE4, 0xF0, 0xDC, 0xFF);
        public static readonly Color MintDeep = new Color32(0x30, 0x60, 0x60, 0xFF);
        public static readonly Color Danger = new Color32(0xF0, 0x78, 0x54, 0xFF);
        public static readonly Color Valid = new Color32(0x4A, 0xA0, 0x70, 0xFF);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.22f);

        private Texture2D _panel;
        private Texture2D _header;
        private Texture2D _button;
        private Texture2D _buttonHover;
        private Texture2D _buttonDown;
        private Texture2D _buttonOff;
        private Texture2D _chip;
        private Texture2D _scrollTrack;
        private Texture2D _scrollThumb;
        private Texture2D _shadow;
        private Font _font;
        private int _builtForHeight;

        public GUIStyle PanelStyle { get; private set; }
        public GUIStyle HeaderStyle { get; private set; }
        public GUIStyle TitleStyle { get; private set; }
        public GUIStyle CatalogTitleStyle { get; private set; }
        public GUIStyle CatalogHintStyle { get; private set; }
        public GUIStyle BodyStyle { get; private set; }
        public GUIStyle HintStyle { get; private set; }
        public GUIStyle ValueStyle { get; private set; }
        public GUIStyle ButtonStyle { get; private set; }
        public GUIStyle DangerButtonStyle { get; private set; }
        public GUIStyle DisabledButtonStyle { get; private set; }
        public GUIStyle ChipStyle { get; private set; }
        public GUIStyle ToastStyle { get; private set; }

        /// <summary>Track and thumb for the catalog scroll view; the built-in skin draws a black bar.</summary>
        public GUIStyle ScrollbarStyle { get; private set; }

        public GUIStyle ScrollbarThumbStyle { get; private set; }

        public Texture2D ShadowTexture => _shadow;

        public float Scale { get; private set; } = 1f;

        public void EnsureBuilt() => EnsureBuilt(Screen.height);

        // Explicit size keeps responsive typography measurable without changing the user's display.
        public void EnsureBuilt(int screenHeight)
        {
            int height = Mathf.Max(720, screenHeight);
            if (PanelStyle != null && _builtForHeight == height) return;
            _builtForHeight = height;
            Scale = Mathf.Clamp(height / 1080f, 0.72f, 1.4f);

            _panel = Rounded(Panel, 18, Gold, rim: 3, inner: GoldSoft);
            _header = Rounded(PanelHeader, 18, Gold, rim: 3, inner: GoldSoft, topOnly: true);
            _button = Rounded(Cream, 12, Gold, rim: 2, inner: GoldSoft);
            _buttonHover = Rounded(Mint, 12, Gold, rim: 2, inner: GoldSoft);
            _buttonDown = Rounded(MintDeep, 12, new Color32(0x1C, 0x40, 0x40, 0xFF), rim: 2);
            _buttonOff = Rounded(new Color32(0xE8, 0xE4, 0xD8, 0xFF), 12, new Color32(0xC8, 0xC0, 0xAC, 0xFF), rim: 2);
            _chip = Rounded(new Color32(0xF7, 0xEA, 0xCE, 0xFF), 12, GoldSoft, rim: 2);
            _scrollTrack = Rounded(new Color32(0xEC, 0xE0, 0xC4, 0xFF), 6, new Color32(0xDC, 0xCA, 0xA4, 0xFF), rim: 1);
            _scrollThumb = Rounded(GoldSoft, 6, Gold, rim: 1);
            _shadow = Solid(Shadow);
            _font = Resources.Load<Font>("StockMarket/Fonts/MaplestoryBold");

            PanelStyle = new GUIStyle
            {
                normal = { background = _panel },
                border = new RectOffset(20, 20, 20, 20),
                padding = new RectOffset(0, 0, 0, Round(14))
            };
            HeaderStyle = new GUIStyle
            {
                normal = { background = _header, textColor = Ink },
                border = new RectOffset(20, 20, 20, 6),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(Round(18), Round(18), 0, 0),
                fontSize = Round(21),
                fontStyle = FontStyle.Bold,
                font = _font
            };
            TitleStyle = new GUIStyle
            {
                normal = { textColor = Ink },
                fontSize = Round(18),
                fontStyle = FontStyle.Bold,
                font = _font
            };
            BodyStyle = new GUIStyle
            {
                normal = { textColor = Ink },
                fontSize = Round(15),
                wordWrap = true,
                font = _font
            };
            HintStyle = new GUIStyle(BodyStyle)
            {
                normal = { textColor = InkSoft },
                fontSize = Round(13)
            };
            // The catalog has a reserved action column. Text must never overflow into its buttons.
            CatalogTitleStyle = new GUIStyle(TitleStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            CatalogHintStyle = new GUIStyle(HintStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            ValueStyle = new GUIStyle(BodyStyle)
            {
                normal = { textColor = MintDeep },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            ButtonStyle = new GUIStyle
            {
                normal = { background = _button, textColor = Ink },
                hover = { background = _buttonHover, textColor = MintDeep },
                active = { background = _buttonDown, textColor = Cream },
                border = new RectOffset(14, 14, 14, 14),
                alignment = TextAnchor.MiddleCenter,
                fontSize = Round(15),
                fontStyle = FontStyle.Bold,
                font = _font
            };
            DangerButtonStyle = new GUIStyle(ButtonStyle)
            {
                normal = { background = Rounded(Danger, 12, new Color32(0xC0, 0x50, 0x38, 0xFF), rim: 2), textColor = Cream },
                hover = { background = Rounded(new Color32(0xF8, 0x94, 0x74, 0xFF), 12, new Color32(0xC0, 0x50, 0x38, 0xFF), rim: 2), textColor = Cream },
                active = { background = Rounded(new Color32(0xC0, 0x50, 0x38, 0xFF), 12, new Color32(0x98, 0x3C, 0x2C, 0xFF), rim: 2), textColor = Cream }
            };
            DisabledButtonStyle = new GUIStyle(ButtonStyle)
            {
                normal = { background = _buttonOff, textColor = new Color32(0x9C, 0x98, 0x8C, 0xFF) },
                hover = { background = _buttonOff, textColor = new Color32(0x9C, 0x98, 0x8C, 0xFF) },
                active = { background = _buttonOff, textColor = new Color32(0x9C, 0x98, 0x8C, 0xFF) }
            };
            ChipStyle = new GUIStyle(BodyStyle)
            {
                normal = { background = _chip, textColor = Ink },
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(Round(10), Round(10), Round(5), Round(5)),
                fontSize = Round(13)
            };
            ScrollbarStyle = new GUIStyle
            {
                normal = { background = _scrollTrack },
                border = new RectOffset(8, 8, 8, 8),
                fixedWidth = Round(12),
                margin = new RectOffset(Round(4), 0, 0, 0)
            };
            ScrollbarThumbStyle = new GUIStyle
            {
                normal = { background = _scrollThumb },
                hover = { background = _scrollThumb },
                active = { background = _scrollThumb },
                border = new RectOffset(8, 8, 8, 8),
                fixedWidth = Round(12)
            };
            ToastStyle = new GUIStyle(BodyStyle)
            {
                normal =
                {
                    background = Rounded(new Color32(0x24, 0x54, 0x54, 0xF0), 12, Gold, rim: 2),
                    textColor = Cream
                },
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(Round(16), Round(16), Round(9), Round(9)),
                alignment = TextAnchor.MiddleCenter,
                fontSize = Round(15),
                fontStyle = FontStyle.Bold
            };
        }

        public int Round(float value) => Mathf.RoundToInt(value * Scale);

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// A rounded plate with a <paramref name="rim"/>-thick outline and an optional lighter line
        /// just inside it, which is what gives the generated frames their embossed look. The texture
        /// is exactly <c>radius * 2 + 4</c> square so a border of <c>radius + 2</c> slices it into
        /// four corners and a one-pixel stretch band.
        /// </summary>
        private static Texture2D Rounded(
            Color fill,
            int radius,
            Color border,
            int rim = 2,
            Color? inner = null,
            bool topOnly = false)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var innerColor = inner ?? border;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float cornerX = x < radius ? radius - x : (x > size - radius - 1 ? x - (size - radius - 1) : 0f);
                float cornerY = y < radius ? radius - y : (y > size - radius - 1 ? y - (size - radius - 1) : 0f);
                if (topOnly && y < size * 0.5f) cornerY = 0f;
                float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                float edge = radius - distance;
                Color pixel;
                if (edge < -0.5f)
                {
                    pixel = new Color(fill.r, fill.g, fill.b, 0f);
                }
                else if (edge < rim - 0.5f)
                {
                    // Feather the outermost half pixel so the rim does not look stair-stepped.
                    var coverage = Mathf.Clamp01(edge + 0.5f);
                    pixel = new Color(border.r, border.g, border.b, fill.a * coverage);
                }
                else if (edge < rim + 0.5f)
                {
                    pixel = new Color(innerColor.r, innerColor.g, innerColor.b, fill.a);
                }
                else
                {
                    pixel = fill;
                }

                texture.SetPixel(x, y, pixel);
            }

            texture.Apply();
            return texture;
        }
    }
}

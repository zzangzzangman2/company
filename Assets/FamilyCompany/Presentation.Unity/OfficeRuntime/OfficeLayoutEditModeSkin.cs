using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Look of the in-game layout editor: the 2000s office palette the art already uses - warm wood,
    /// dusty mint, cream paper - as rounded IMGUI panels rather than default grey boxes.
    /// Textures are generated once and reused; nothing here is loaded from disk except the Korean
    /// font, which falls back to the built-in font when it is missing.
    /// </summary>
    public sealed class OfficeLayoutEditModeSkin
    {
        public static readonly Color Ink = new Color(0.13f, 0.24f, 0.25f);
        public static readonly Color InkSoft = new Color(0.35f, 0.47f, 0.47f);
        public static readonly Color Cream = new Color(0.98f, 0.96f, 0.90f);
        public static readonly Color Panel = new Color(0.99f, 0.97f, 0.92f, 0.97f);
        public static readonly Color PanelHeader = new Color(0.78f, 0.53f, 0.25f);
        public static readonly Color Mint = new Color(0.55f, 0.76f, 0.72f);
        public static readonly Color MintDeep = new Color(0.24f, 0.52f, 0.50f);
        public static readonly Color Danger = new Color(0.82f, 0.34f, 0.31f);
        public static readonly Color Valid = new Color(0.36f, 0.74f, 0.45f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.22f);

        private Texture2D _panel;
        private Texture2D _header;
        private Texture2D _button;
        private Texture2D _buttonHover;
        private Texture2D _buttonDown;
        private Texture2D _buttonOff;
        private Texture2D _chip;
        private Texture2D _shadow;
        private Font _font;
        private int _builtForHeight;

        public GUIStyle PanelStyle { get; private set; }
        public GUIStyle HeaderStyle { get; private set; }
        public GUIStyle TitleStyle { get; private set; }
        public GUIStyle BodyStyle { get; private set; }
        public GUIStyle HintStyle { get; private set; }
        public GUIStyle ValueStyle { get; private set; }
        public GUIStyle ButtonStyle { get; private set; }
        public GUIStyle DangerButtonStyle { get; private set; }
        public GUIStyle DisabledButtonStyle { get; private set; }
        public GUIStyle ChipStyle { get; private set; }
        public GUIStyle ToastStyle { get; private set; }
        public Texture2D ShadowTexture => _shadow;

        public float Scale { get; private set; } = 1f;

        public void EnsureBuilt()
        {
            int height = Mathf.Max(720, Screen.height);
            if (PanelStyle != null && _builtForHeight == height) return;
            _builtForHeight = height;
            Scale = Mathf.Clamp(height / 1080f, 0.72f, 1.4f);

            _panel = Rounded(Panel, 14, new Color(0.85f, 0.75f, 0.58f));
            _header = Rounded(PanelHeader, 14, new Color(0.62f, 0.40f, 0.18f), topOnly: true);
            _button = Rounded(Mint, 10, MintDeep);
            _buttonHover = Rounded(new Color(0.64f, 0.83f, 0.79f), 10, MintDeep);
            _buttonDown = Rounded(MintDeep, 10, MintDeep);
            _buttonOff = Rounded(new Color(0.85f, 0.85f, 0.82f), 10, new Color(0.72f, 0.72f, 0.70f));
            _chip = Rounded(new Color(0.93f, 0.90f, 0.82f), 8, new Color(0.80f, 0.74f, 0.62f));
            _shadow = Solid(Shadow);
            _font = Resources.Load<Font>("StockMarket/Fonts/MaplestoryBold");

            PanelStyle = new GUIStyle
            {
                normal = { background = _panel },
                border = new RectOffset(16, 16, 16, 16),
                padding = new RectOffset(0, 0, 0, Round(14))
            };
            HeaderStyle = new GUIStyle
            {
                normal = { background = _header, textColor = Cream },
                border = new RectOffset(16, 16, 16, 4),
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
            ValueStyle = new GUIStyle(BodyStyle)
            {
                normal = { textColor = MintDeep },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            ButtonStyle = new GUIStyle
            {
                normal = { background = _button, textColor = new Color(0.06f, 0.22f, 0.22f) },
                hover = { background = _buttonHover, textColor = new Color(0.06f, 0.22f, 0.22f) },
                active = { background = _buttonDown, textColor = Cream },
                border = new RectOffset(12, 12, 12, 12),
                alignment = TextAnchor.MiddleCenter,
                fontSize = Round(15),
                fontStyle = FontStyle.Bold,
                font = _font
            };
            DangerButtonStyle = new GUIStyle(ButtonStyle)
            {
                normal = { background = Rounded(Danger, 10, new Color(0.60f, 0.22f, 0.20f)), textColor = Cream },
                hover = { background = Rounded(new Color(0.88f, 0.44f, 0.40f), 10, new Color(0.60f, 0.22f, 0.20f)), textColor = Cream },
                active = { background = Rounded(new Color(0.62f, 0.24f, 0.22f), 10, new Color(0.60f, 0.22f, 0.20f)), textColor = Cream }
            };
            DisabledButtonStyle = new GUIStyle(ButtonStyle)
            {
                normal = { background = _buttonOff, textColor = new Color(0.55f, 0.55f, 0.53f) },
                hover = { background = _buttonOff, textColor = new Color(0.55f, 0.55f, 0.53f) },
                active = { background = _buttonOff, textColor = new Color(0.55f, 0.55f, 0.53f) }
            };
            ChipStyle = new GUIStyle(BodyStyle)
            {
                normal = { background = _chip, textColor = Ink },
                border = new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(Round(10), Round(10), Round(5), Round(5)),
                fontSize = Round(13)
            };
            ToastStyle = new GUIStyle(BodyStyle)
            {
                normal = { background = Rounded(new Color(0.13f, 0.24f, 0.25f, 0.94f), 10, new Color(0.08f, 0.16f, 0.17f)), textColor = Cream },
                border = new RectOffset(12, 12, 12, 12),
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

        private static Texture2D Rounded(Color fill, int radius, Color border, bool topOnly = false)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float cornerX = x < radius ? radius - x : (x > size - radius - 1 ? x - (size - radius - 1) : 0f);
                float cornerY = y < radius ? radius - y : (y > size - radius - 1 ? y - (size - radius - 1) : 0f);
                if (topOnly && y < size * 0.5f) cornerY = 0f;
                float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                float edge = radius - distance;
                Color pixel;
                if (edge < -0.5f) pixel = new Color(fill.r, fill.g, fill.b, 0f);
                else if (edge < 1.5f) pixel = new Color(border.r, border.g, border.b, fill.a);
                else pixel = fill;
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply();
            return texture;
        }
    }
}

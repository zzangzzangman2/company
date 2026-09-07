using System;
using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>Native white HUD surfaces and the approved, text-free office illustration atlas.</summary>
    internal sealed class OfficeHudVisuals : IDisposable
    {
        private readonly Texture2D _surfaceTexture;
        private readonly Sprite _surface;
        private readonly Sprite[] _icons = new Sprite[5];

        public OfficeHudVisuals()
        {
            _surfaceTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            { name = "Office HUD rounded surface", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var dx = Mathf.Max(0f, Mathf.Abs(x + .5f - 16f) - 6f);
                var dy = Mathf.Max(0f, Mathf.Abs(y + .5f - 16f) - 6f);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f,
                    Mathf.Clamp01(10f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            _surfaceTexture.SetPixels(pixels);
            _surfaceTexture.Apply(false, true);
            _surface = Sprite.Create(_surfaceTexture, new Rect(0, 0, 32, 32), Vector2.one * .5f,
                100f, 0, SpriteMeshType.FullRect, new Vector4(11, 11, 11, 11));
            var atlas = Resources.Load<Texture2D>("OfficeHudWhite/office-illustrations");
            if (atlas == null || atlas.width != 1536 || atlas.height != 1024)
                throw new InvalidOperationException("OFFICE_HUD_ATLAS_MISSING_OR_RESIZED");
            // Same measured crop windows as the approved preview, in Unity bottom-left coordinates.
            var crops = new[] { new Rect(16, 524, 500, 500), new Rect(518, 524, 500, 500),
                new Rect(1020, 524, 500, 500), new Rect(16, 0, 500, 500),
                new Rect(530, 69, 420, 420) };
            for (var i = 0; i < crops.Length; i++)
                _icons[i] = Sprite.Create(atlas, crops[i], Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        }

        public Sprite Icon(MainNavigationTabId tab) => _icons[(int)tab];

        public RectTransform Surface(string name, Transform parent, bool shadow = false)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            image.sprite = _surface;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            if (shadow)
            {
                var effect = rect.gameObject.AddComponent<Shadow>();
                effect.effectColor = new Color(.13f, .23f, .26f, .10f);
                effect.effectDistance = new Vector2(0, -3);
            }
            return rect;
        }

        public static void ButtonColors(Button button, Color normal, Color hover, Color pressed)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.selectedColor = normal;
            colors.pressedColor = pressed;
            colors.fadeDuration = .1f;
            button.colors = colors;
        }

        public void Dispose()
        {
            foreach (var icon in _icons) if (icon != null) UnityEngine.Object.Destroy(icon);
            UnityEngine.Object.Destroy(_surface);
            UnityEngine.Object.Destroy(_surfaceTexture);
        }
    }
}

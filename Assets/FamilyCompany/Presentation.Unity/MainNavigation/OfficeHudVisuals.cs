using System;
using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>Casual office UI: quiet native surfaces and one text-free illustrated icon family.</summary>
    internal sealed class OfficeHudVisuals : IDisposable
    {
        private readonly Texture2D _surfaceTexture;
        private readonly Sprite _surface;
        private readonly Sprite[] _icons = new Sprite[6];
        public Sprite SurfaceSprite => _surface;

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
            var atlas = Resources.Load<Texture2D>("OfficeCasual/office-icons-v1");
            if (atlas == null || atlas.width != 1536 || atlas.height != 1024)
                throw new InvalidOperationException("OFFICE_HUD_ATLAS_MISSING_OR_RESIZED");
            // Six non-overlapping atlas cells; keep the generated alpha and original pixels intact.
            for (var i = 0; i < _icons.Length; i++)
                _icons[i] = Sprite.Create(atlas, new Rect(i % 3 * 512, (1 - i / 3) * 512, 512, 512),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        }

        public Sprite Icon(MainNavigationTabId tab) => _icons[(int)tab];
        public Sprite WorkstationIcon => _icons[5];

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

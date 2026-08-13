using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class TitleMoneyRainRenderer : MonoBehaviour
    {
        public const string BackgroundResourcePath = "Title/MoneyRain/money_rain_tycoon_background_v2";
        public const string PortraitBackgroundResourcePath = "Title/MoneyRain/money_rain_tycoon_background_portrait_v3";
        public const string MintBundleResourcePath = "Title/MoneyRain/money_bundle_mint_v1";
        public const string CoralBundleResourcePath = "Title/MoneyRain/money_bundle_coral_v1";
        public const string SkyBundleResourcePath = "Title/MoneyRain/money_bundle_sky_v1";
        public const int BundleInstanceCount = 12;
        public const float LoopDuration = 2.8f;

        private const float ResourceRetryIntervalSeconds = 2f;

        private static readonly BundleSpec[] BundleSpecs =
        {
            new BundleSpec(0.09f, 0.04f, 0.66f, 1, 0.10f, -18f, 1, 0, 0.72f),
            new BundleSpec(0.18f, 0.42f, 0.84f, 1, 0.07f,  12f, -1, 1, 0.78f),
            new BundleSpec(0.30f, 0.76f, 1.04f, 2, 0.05f, -34f, 1, 2, 0.88f),
            new BundleSpec(0.41f, 0.18f, 0.72f, 1, 0.09f,  25f, 1, 1, 0.74f),
            new BundleSpec(0.52f, 0.57f, 1.12f, 2, 0.06f,  -8f, -1, 0, 0.94f),
            new BundleSpec(0.63f, 0.89f, 0.80f, 1, 0.08f,  38f, 1, 2, 0.82f),
            new BundleSpec(0.73f, 0.30f, 0.96f, 1, 0.04f, -27f, -1, 1, 0.90f),
            new BundleSpec(0.84f, 0.67f, 1.18f, 2, 0.07f,  16f, 1, 0, 0.96f),
            new BundleSpec(0.94f, 0.12f, 0.76f, 1, 0.05f, -42f, -1, 2, 0.76f),
            new BundleSpec(0.36f, 0.95f, 0.60f, 1, 0.10f,   6f, 1, 0, 0.66f),
            new BundleSpec(0.68f, 0.51f, 0.64f, 1, 0.09f,  31f, -1, 2, 0.68f),
            new BundleSpec(0.90f, 0.83f, 0.90f, 2, 0.06f, -13f, 1, 1, 0.86f)
        };

        private Texture2D _backgroundTexture;
        private Texture2D _portraitBackgroundTexture;
        private readonly Texture2D[] _bundleTextures = new Texture2D[3];
        private Texture2D _fallbackBackgroundTexture;
        private Texture2D _menuPanelTexture;
        private float _nextResourceRetryTime;

        public bool HasBackgroundAsset => _backgroundTexture != null;
        public int LoadedBundleAssetCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _bundleTextures.Length; index++)
                {
                    if (_bundleTextures[index] != null) count++;
                }

                return count;
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            const string argumentName = "-familyCompanyCaptureMoneyRain";
            var arguments = Environment.GetCommandLineArgs();
            var argumentIndex = Array.IndexOf(arguments, argumentName);
            if (argumentIndex < 0 || argumentIndex + 1 >= arguments.Length) return;
            StartCoroutine(CaptureForQa(Path.GetFullPath(arguments[argumentIndex + 1])));
        }

        public void Draw(Rect fullScreen)
        {
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                EnsureResources(Time.unscaledTime);
                DrawBackground(fullScreen);
                DrawBundles(fullScreen, Time.unscaledTime);
                DrawReadabilityPanel(fullScreen);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        public static TitleMoneyRainLayout CalculateLayout(float screenWidth, float screenHeight)
        {
            var width = Mathf.Max(1f, screenWidth);
            var height = Mathf.Max(1f, screenHeight);
            if (IsCompactLayout(width, height))
            {
                var heroHeight = CalculateCompactHeroHeight(width, height);
                var margin = Mathf.Clamp(width * 0.035f, 14f, 24f);
                var menuTop = Mathf.Max(132f, heroHeight - 12f);
                return new TitleMoneyRainLayout(
                    new Rect(0f, 0f, width, height),
                    new Rect(
                        margin,
                        menuTop,
                        Mathf.Max(1f, width - margin * 2f),
                        Mathf.Max(1f, height - menuTop - margin)));
            }

            var menuX = Mathf.Max(70f, width * 0.075f);
            var menuWidth = Mathf.Clamp(width * 0.31f, 450f, 620f);
            var panelWidth = Mathf.Clamp(width * 0.48f, 640f, 1120f);
            panelWidth = Mathf.Max(panelWidth, menuX + menuWidth + 54f);
            panelWidth = Mathf.Min(width, panelWidth);
            return new TitleMoneyRainLayout(
                new Rect(0f, 0f, panelWidth, height),
                new Rect(menuX, Mathf.Max(105f, height * 0.12f), menuWidth, Mathf.Max(1f, height - 175f)));
        }

        public static bool IsCompactLayout(float screenWidth, float screenHeight)
        {
            var width = Mathf.Max(1f, screenWidth);
            var height = Mathf.Max(1f, screenHeight);
            return width / height < 1.35f;
        }

        public static float CalculateCompactHeroHeight(float screenWidth, float screenHeight)
        {
            var width = Mathf.Max(1f, screenWidth);
            var height = Mathf.Max(1f, screenHeight);
            return Mathf.Clamp(
                Mathf.Min(width * 9f / 16f, height * 0.55f),
                Mathf.Min(180f, height * 0.42f),
                height * 0.62f);
        }

        public static TitleMoneyRainBundlePose CalculateBundlePose(int index, float unscaledTime, float screenWidth, float screenHeight)
        {
            if (index < 0 || index >= BundleSpecs.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var spec = BundleSpecs[index];
            var width = Mathf.Max(1f, screenWidth);
            var height = Mathf.Max(1f, screenHeight);
            var progress = Mathf.Repeat(unscaledTime / LoopDuration * spec.CycleCount + spec.Phase, 1f);
            var size = Mathf.Clamp(height * 0.125f * spec.DepthScale, 56f, 220f);
            var sway = Mathf.Sin((progress + spec.Phase * 0.37f) * Mathf.PI * 2f) * width * spec.Sway;
            var x = spec.NormalizedX * width + sway;
            var y = -size * 1.35f + progress * (height + size * 2.7f);
            var rect = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);
            var edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f)) *
                           Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.08f));
            var rotation = spec.BaseRotation + progress * spec.SpinTurns * 360f;
            return new TitleMoneyRainBundlePose(rect, rotation, spec.Alpha * edgeFade, spec.TextureIndex, LoopDuration);
        }

        public static float GetBundleLoopDuration(int index)
        {
            if (index < 0 || index >= BundleSpecs.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return LoopDuration;
        }

        private IEnumerator CaptureForQa(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            for (var frame = 0; frame < 10; frame++) yield return new WaitForEndOfFrame();

            var resolutionLabel = Screen.width + "x" + Screen.height;
            for (var captureIndex = 0; captureIndex < 3; captureIndex++)
            {
                var outputPath = Path.Combine(
                    outputFolder,
                    $"money-rain-{resolutionLabel}-frame-{captureIndex + 1}.png");
                if (File.Exists(outputPath)) File.Delete(outputPath);
                ScreenCapture.CaptureScreenshot(outputPath);
                yield return WaitForScreenshot(outputPath);
                if (captureIndex >= 2) continue;
                var nextCaptureTime = Time.realtimeSinceStartup + LoopDuration / 3f;
                while (Time.realtimeSinceStartup < nextCaptureTime) yield return null;
            }

            var bootstrap = GetComponent<PrototypeBootstrap>();
            Debug.Log("FAMILY_COMPANY_TITLE_MONEY_RAIN_CAPTURE_READY_FOR_CLICK: " + resolutionLabel);
            var clickDeadline = Time.realtimeSinceStartup + 12f;
            while (bootstrap != null && bootstrap.UiScreen == PrototypeUiScreen.MainMenu && Time.realtimeSinceStartup < clickDeadline)
            {
                yield return null;
            }

            if (bootstrap == null || bootstrap.UiScreen != PrototypeUiScreen.NewGameSlots)
            {
                Debug.LogError("FAMILY_COMPANY_TITLE_MONEY_RAIN_BUTTON_CLICK: FAIL · expected NewGameSlots");
                Application.Quit(1);
                yield break;
            }

            var clickedPath = Path.Combine(outputFolder, $"money-rain-{resolutionLabel}-after-click.png");
            if (File.Exists(clickedPath)) File.Delete(clickedPath);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(clickedPath);
            yield return WaitForScreenshot(clickedPath);
            Debug.Log("FAMILY_COMPANY_TITLE_MONEY_RAIN_BUTTON_CLICK: PASS · " + resolutionLabel);
            Debug.Log("FAMILY_COMPANY_TITLE_MONEY_RAIN_CAPTURE: PASS · " + resolutionLabel);
            Application.Quit(0);
        }

        private static IEnumerator WaitForScreenshot(string outputPath)
        {
            for (var frame = 0; frame < 300; frame++)
            {
                yield return null;
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length >= 1024) yield break;
            }

            throw new IOException("Timed out while writing title money-rain screenshot: " + outputPath);
        }

        private void EnsureResources(float unscaledTime)
        {
            var missingAny = _backgroundTexture == null;
            if (_portraitBackgroundTexture == null) missingAny = true;
            for (var index = 0; index < _bundleTextures.Length; index++)
            {
                if (_bundleTextures[index] == null) missingAny = true;
            }

            if (!missingAny || unscaledTime < _nextResourceRetryTime) return;
            _nextResourceRetryTime = unscaledTime + ResourceRetryIntervalSeconds;
            if (_backgroundTexture == null) _backgroundTexture = Resources.Load<Texture2D>(BackgroundResourcePath);
            if (_portraitBackgroundTexture == null)
                _portraitBackgroundTexture = Resources.Load<Texture2D>(PortraitBackgroundResourcePath);
            if (_bundleTextures[0] == null) _bundleTextures[0] = Resources.Load<Texture2D>(MintBundleResourcePath);
            if (_bundleTextures[1] == null) _bundleTextures[1] = Resources.Load<Texture2D>(CoralBundleResourcePath);
            if (_bundleTextures[2] == null) _bundleTextures[2] = Resources.Load<Texture2D>(SkyBundleResourcePath);
        }

        private void DrawBackground(Rect fullScreen)
        {
            if (_backgroundTexture != null)
            {
                if (IsCompactLayout(fullScreen.width, fullScreen.height))
                {
                    DrawTextureAspectFill(
                        fullScreen,
                        _portraitBackgroundTexture != null ? _portraitBackgroundTexture : _backgroundTexture);
                    return;
                }

                DrawTextureAspectFill(fullScreen, _backgroundTexture);
                return;
            }

            EnsureFallbackTextures();
            GUI.color = Color.white;
            GUI.DrawTexture(fullScreen, _fallbackBackgroundTexture, ScaleMode.StretchToFill, true);
        }

        private void DrawBundles(Rect fullScreen, float unscaledTime)
        {
            for (var index = 0; index < BundleInstanceCount; index++)
            {
                var pose = CalculateBundlePose(index, unscaledTime, fullScreen.width, fullScreen.height);
                var texture = _bundleTextures[pose.TextureIndex];
                if (texture == null || pose.Alpha <= 0.001f) continue;

                var rect = pose.Rect;
                if (texture.width > 0 && texture.height > 0)
                {
                    rect.height = rect.width * texture.height / texture.width;
                    rect.y -= (rect.height - pose.Rect.height) * 0.5f;
                }

                DrawRotatedTexture(rect, texture, pose.RotationDegrees, pose.Alpha);
            }
        }

        private void DrawReadabilityPanel(Rect fullScreen)
        {
            if (IsCompactLayout(fullScreen.width, fullScreen.height)) return;
            EnsureFallbackTextures();
            var layout = CalculateLayout(fullScreen.width, fullScreen.height);
            var panel = layout.ReadabilityPanel;
            panel.x += fullScreen.x;
            panel.y += fullScreen.y;
            GUI.color = Color.white;
            GUI.DrawTexture(panel, _menuPanelTexture, ScaleMode.StretchToFill, true);
        }

        private void EnsureFallbackTextures()
        {
            if (_fallbackBackgroundTexture == null)
            {
                _fallbackBackgroundTexture = BuildGradientTexture(
                    1,
                    64,
                    new Color(1f, 0.96f, 0.86f, 1f),
                    new Color(0.72f, 0.91f, 0.96f, 1f),
                    false,
                    "Title Money Rain Fallback");
            }

            if (_menuPanelTexture == null)
            {
                _menuPanelTexture = BuildMenuPanelTexture();
            }
        }

        private static Texture2D BuildMenuPanelTexture()
        {
            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "Title Money Rain Menu Panel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (var x = 0; x < width; x++)
            {
                var progress = x / (float)(width - 1);
                var alpha = progress <= 0.72f
                    ? Mathf.Lerp(0.72f, 0.46f, progress / 0.72f)
                    : Mathf.Lerp(0.46f, 0f, (progress - 0.72f) / 0.28f);
                texture.SetPixel(x, 0, new Color(0.055f, 0.047f, 0.045f, alpha));
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D BuildGradientTexture(
            int width,
            int height,
            Color start,
            Color end,
            bool horizontal,
            string textureName)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var denominator = horizontal ? width - 1f : height - 1f;
                    var progress = denominator <= 0f ? 0f : (horizontal ? x : y) / denominator;
                    texture.SetPixel(x, y, Color.Lerp(start, end, progress));
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static void DrawTextureAspectFill(Rect target, Texture texture)
        {
            var targetAspect = target.width / target.height;
            var textureAspect = texture.width / (float)texture.height;
            var source = new Rect(0f, 0f, 1f, 1f);
            if (targetAspect > textureAspect)
            {
                var visibleHeight = textureAspect / targetAspect;
                source.y = (1f - visibleHeight) * 0.5f;
                source.height = visibleHeight;
            }
            else
            {
                var visibleWidth = targetAspect / textureAspect;
                source.x = (1f - visibleWidth) * 0.5f;
                source.width = visibleWidth;
            }

            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(target, texture, source, true);
        }

        private static void DrawRotatedTexture(Rect rect, Texture texture, float rotationDegrees, float alpha)
        {
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                GUIUtility.RotateAroundPivot(rotationDegrees, rect.center);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedTexture(_fallbackBackgroundTexture);
            DestroyGeneratedTexture(_menuPanelTexture);
        }

        private static void DestroyGeneratedTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        private readonly struct BundleSpec
        {
            public readonly float NormalizedX;
            public readonly float Phase;
            public readonly float DepthScale;
            public readonly int CycleCount;
            public readonly float Sway;
            public readonly float BaseRotation;
            public readonly int SpinTurns;
            public readonly int TextureIndex;
            public readonly float Alpha;

            public BundleSpec(
                float normalizedX,
                float phase,
                float depthScale,
                int cycleCount,
                float sway,
                float baseRotation,
                int spinTurns,
                int textureIndex,
                float alpha)
            {
                NormalizedX = normalizedX;
                Phase = phase;
                DepthScale = depthScale;
                CycleCount = cycleCount;
                Sway = sway;
                BaseRotation = baseRotation;
                SpinTurns = spinTurns;
                TextureIndex = textureIndex;
                Alpha = alpha;
            }
        }
    }

    public readonly struct TitleMoneyRainLayout
    {
        public readonly Rect ReadabilityPanel;
        public readonly Rect MenuSafeArea;

        public TitleMoneyRainLayout(Rect readabilityPanel, Rect menuSafeArea)
        {
            ReadabilityPanel = readabilityPanel;
            MenuSafeArea = menuSafeArea;
        }
    }

    public readonly struct TitleMoneyRainBundlePose
    {
        public readonly Rect Rect;
        public readonly float RotationDegrees;
        public readonly float Alpha;
        public readonly int TextureIndex;
        public readonly float LoopDuration;

        public TitleMoneyRainBundlePose(Rect rect, float rotationDegrees, float alpha, int textureIndex, float loopDuration)
        {
            Rect = rect;
            RotationDegrees = rotationDegrees;
            Alpha = alpha;
            TextureIndex = textureIndex;
            LoopDuration = loopDuration;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.Rendering
{
    [DisallowMultipleComponent]
    public sealed class RenderClarityRuntimeQa : MonoBehaviour
    {
        private const string QaArgument = "-familyCompanyRenderClarityQa";
        private const int ComparisonCaptureCount = 4;
        private static bool _installed;
        private static int _captureWidth;
        private static int _captureHeight;
        private static bool _usesOffscreenTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _installed = false;
            _captureWidth = 0;
            _captureHeight = 0;
            _usesOffscreenTarget = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfRequested()
        {
            if (_installed || Array.IndexOf(Environment.GetCommandLineArgs(), QaArgument) < 0) return;
            _installed = true;
            var host = new GameObject("~RenderClarityRuntimeQa");
            if (Application.isPlaying) DontDestroyOnLoad(host);
            host.AddComponent<RenderClarityRuntimeQa>();
        }

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunCore());
            Exception failure = null;
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                bool moved;
                object yielded = null;
                try
                {
                    moved = current.MoveNext();
                    if (moved) yielded = current.Current;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (yielded is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return yielded;
            }

            Time.timeScale = 1f;
            if (failure != null)
            {
                Debug.LogError(
                    "RENDER_CLARITY_PLAYER_QA: FAIL | " +
                    failure.GetType().Name + ": " + failure.Message + "\n" + failure.StackTrace);
            }

            yield return null;
            Application.Quit(failure == null ? 0 : 91);
        }

        private IEnumerator RunCore()
        {
            int width = ReadPositiveArgument("-familyCompanyQaWidth", 1920);
            int height = ReadPositiveArgument("-familyCompanyQaHeight", 1080);
            _captureWidth = width;
            _captureHeight = height;
            string artifactFolder = ResolveArtifactFolder();
            Directory.CreateDirectory(artifactFolder);

            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float resolutionDeadline = Time.realtimeSinceStartup + 8f;
            while ((Screen.width != width || Screen.height != height) &&
                   Time.realtimeSinceStartup < resolutionDeadline)
                yield return null;
            if (Screen.width != width || Screen.height != height)
            {
                _usesOffscreenTarget = width > Screen.currentResolution.width ||
                                       height > Screen.currentResolution.height;
                if (!_usesOffscreenTarget)
                    throw new InvalidOperationException(
                        $"Requested {width}x{height}, got {Screen.width}x{Screen.height}.");
            }
            Debug.Log(
                "RENDER_CLARITY_RESOLUTION_PATH | " +
                $"requested={width}x{height} window={Screen.width}x{Screen.height} " +
                $"display={Screen.currentResolution.width}x{Screen.currentResolution.height} " +
                $"capture={_captureWidth}x{_captureHeight} offscreenTarget={_usesOffscreenTarget}");

            PrototypeBootstrap bootstrap = null;
            float bootstrapDeadline = Time.realtimeSinceStartup + 15f;
            while (bootstrap == null && Time.realtimeSinceStartup < bootstrapDeadline)
            {
                bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
                if (bootstrap == null) yield return null;
            }
            if (bootstrap == null) throw new InvalidOperationException("PrototypeBootstrap is missing.");
            bootstrap.StartNewGameNow(1, false);

            StarterOfficeRuntimeBootstrap starter = null;
            float starterDeadline = Time.realtimeSinceStartup + 20f;
            while ((starter == null || !starter.IsReady || starter.World == null) &&
                   Time.realtimeSinceStartup < starterDeadline)
            {
                starter = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                yield return null;
            }
            if (starter == null || !starter.IsReady || starter.World == null)
                throw new InvalidOperationException("Starter Office runtime did not become ready.");

            PixelClarityRuntime clarity = PixelClarityRuntime.Instance;
            if (clarity == null) throw new InvalidOperationException("PixelClarityRuntime is missing.");
            clarity.ForceReframe();
            yield return null;

            PrepareVisibleFamily(starter);
            Time.timeScale = 0f;
            yield return new WaitForEndOfFrame();

            LogRuntimeAudit(starter, clarity, width, height);

            int captured = 0;
            clarity.SetComparisonMode(PixelClarityComparisonMode.LegacyHalfHeight);
            yield return CaptureAfterModeSettles(
                artifactFolder,
                $"before-legacy-half-height-{width}x{height}.png");
            captured++;

            clarity.SetComparisonMode(PixelClarityComparisonMode.NativeUnsnapped);
            yield return CaptureAfterModeSettles(
                artifactFolder,
                $"compare-native-point-unsnapped-{width}x{height}.png");
            captured++;

            Dictionary<Texture, FilterMode> originalFilters = SetWorldTextureFilter(
                starter,
                FilterMode.Bilinear);
            try
            {
                yield return CaptureAfterModeSettles(
                    artifactFolder,
                    $"compare-native-bilinear-{width}x{height}.png");
                captured++;
            }
            finally
            {
                RestoreTextureFilters(originalFilters);
            }

            clarity.SetComparisonMode(PixelClarityComparisonMode.FinalNativeStable);
            yield return CaptureAfterModeSettles(
                artifactFolder,
                $"after-final-native-stable-{width}x{height}.png");
            captured++;
            if (captured != ComparisonCaptureCount)
                throw new InvalidOperationException("Render comparison capture count is incomplete.");

            if (width == clarity.Profile.ReferenceWidth && height == clarity.Profile.ReferenceHeight)
            {
                yield return CaptureCharacterMovementSequence(starter, clarity, artifactFolder, width, height);
                yield return CaptureCameraMovementSequence(clarity, artifactFolder, width, height);
            }

            clarity.SetComparisonMode(PixelClarityComparisonMode.FinalNativeStable);
            clarity.ForceReframe();
            Time.timeScale = 1f;
            Debug.Log(
                "RENDER_CLARITY_PLAYER_QA: PASS | " +
                $"resolution={width}x{height} comparisons={captured} " +
                $"offscreenTarget={_usesOffscreenTarget} " +
                $"mode={clarity.ComparisonMode} renderScale=" +
                $"{ScalableBufferManager.widthScaleFactor:F2}x{ScalableBufferManager.heightScaleFactor:F2} " +
                $"folder={artifactFolder}");
        }

        private static void PrepareVisibleFamily(StarterOfficeRuntimeBootstrap starter)
        {
            OfficeGridCoordinate[] cells =
            {
                new OfficeGridCoordinate(1, 2),
                new OfficeGridCoordinate(10, 4),
                new OfficeGridCoordinate(1, 9),
                new OfficeGridCoordinate(9, 6)
            };
            for (int index = 0; index < starter.Actors.Count; index++)
            {
                OfficeRuntimeAgent actor = starter.Actors[index];
                actor.BeginQaControl();
                if (index < cells.Length && starter.World.Grid.Contains(cells[index]))
                    actor.QaTeleportToCell(cells[index]);
            }
        }

        private static void LogRuntimeAudit(
            StarterOfficeRuntimeBootstrap starter,
            PixelClarityRuntime clarity,
            int requestedWidth,
            int requestedHeight)
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Camera.main is missing.");
            PixelatedCameraEffect effect = camera.GetComponent<PixelatedCameraEffect>();
            float baseSourcePixelRatio = camera.pixelHeight /
                                         (camera.orthographicSize * 2f * clarity.Profile.PixelArtPixelsPerUnit);
            Debug.Log(
                "RENDER_CLARITY_RUNTIME_AUDIT | " +
                $"requested={requestedWidth}x{requestedHeight} actual={Screen.width}x{Screen.height} " +
                $"currentDisplay={Screen.currentResolution.width}x{Screen.currentResolution.height} " +
                $"fullscreen={Screen.fullScreenMode} dpi={Screen.dpi:F2} " +
                $"cameraPixels={camera.pixelWidth}x{camera.pixelHeight} ortho={camera.orthographicSize:F6} " +
                $"cameraPosition=({camera.transform.position.x:F6},{camera.transform.position.y:F6},{camera.transform.position.z:F3}) " +
                $"allowDynamicResolution={camera.allowDynamicResolution} " +
                $"scalableBuffer={ScalableBufferManager.widthScaleFactor:F2}x{ScalableBufferManager.heightScaleFactor:F2} " +
                $"legacyEffectPresent={effect != null} legacyEffectEnabled={effect != null && effect.enabled} " +
                $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
                $"msaa={QualitySettings.antiAliasing} mipLimit={QualitySettings.globalTextureMipmapLimit} " +
                $"baseScreenPixelsPerSourcePixel={baseSourcePixelRatio:F6}");

            SpriteRenderer[] renderers = starter.GetComponentsInChildren<SpriteRenderer>(true);
            int point = 0, bilinear = 0, mipmapped = 0, fractional = 0;
            var textureNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null || renderer.sprite.texture == null) continue;
                Texture2D texture = renderer.sprite.texture;
                textureNames.Add(texture.name);
                if (texture.filterMode == FilterMode.Point) point++;
                if (texture.filterMode == FilterMode.Bilinear) bilinear++;
                if (texture.mipmapCount > 1) mipmapped++;
                Vector3 screen = camera.WorldToScreenPoint(renderer.transform.position);
                if (Mathf.Abs(screen.x - Mathf.Round(screen.x)) > 0.001f ||
                    Mathf.Abs(screen.y - Mathf.Round(screen.y)) > 0.001f) fractional++;
            }
            Debug.Log(
                "RENDER_CLARITY_SPRITE_AUDIT | " +
                $"renderers={renderers.Length} textures={textureNames.Count} point={point} bilinear={bilinear} " +
                $"mipmapped={mipmapped} fractionalPresentationAnchors={fractional}");

            foreach (OfficeRuntimeAgent actor in starter.Actors)
            {
                SpriteRenderer renderer = actor.PresentationRenderer;
                if (renderer == null || renderer.sprite == null) continue;
                Vector3 screen = camera.WorldToScreenPoint(renderer.transform.position);
                Sprite sprite = renderer.sprite;
                Debug.Log(
                    "RENDER_CLARITY_CHARACTER_SAMPLE | " +
                    $"member={actor.AgentId} world=({renderer.transform.position.x:F6},{renderer.transform.position.y:F6}) " +
                    $"screen=({screen.x:F4},{screen.y:F4}) " +
                    $"fraction=({Fraction(screen.x):F4},{Fraction(screen.y):F4}) " +
                    $"sprite={sprite.name} source={sprite.texture.width}x{sprite.texture.height} " +
                    $"ppu={sprite.pixelsPerUnit:F1} filter={sprite.texture.filterMode} " +
                    $"mips={sprite.texture.mipmapCount}");
            }

            CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            string scalerSummary = string.Join(
                ";",
                scalers.Select(item =>
                    item.name + ":" + item.uiScaleMode + ":" +
                    item.referenceResolution.x.ToString("F0", CultureInfo.InvariantCulture) + "x" +
                    item.referenceResolution.y.ToString("F0", CultureInfo.InvariantCulture) + ":" +
                    item.matchWidthOrHeight.ToString("F2", CultureInfo.InvariantCulture)));
            Debug.Log(
                "RENDER_CLARITY_UI_SEPARATION_AUDIT | " +
                $"canvasScalers={scalers.Length} details={scalerSummary}");
        }

        private IEnumerator CaptureAfterModeSettles(string folder, string fileName)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, fileName);
            yield return CaptureScreen(path);
            Debug.Log(
                "RENDER_CLARITY_COMPARISON_CAPTURE | " +
                $"mode={PixelClarityRuntime.Instance.ComparisonMode} resolution={Screen.width}x{Screen.height} " +
                $"path={path}");
        }

        private IEnumerator CaptureCharacterMovementSequence(
            StarterOfficeRuntimeBootstrap starter,
            PixelClarityRuntime clarity,
            string folder,
            int width,
            int height)
        {
            OfficeRuntimeAgent player = starter.Actors.FirstOrDefault(item => item != null && item.IsPlayerControlled);
            if (player == null) throw new InvalidOperationException("Player actor is missing for movement sequence.");
            player.QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            if (!player.QaMoveToCell(new OfficeGridCoordinate(7, 6), "render-clarity-sequence"))
                throw new InvalidOperationException("Player movement route could not be created for clarity QA.");
            Vector2 initialPosition = player.Position;
            const int frameCount = 12;
            for (int index = 0; index < frameCount; index++)
            {
                Time.timeScale = 1f;
                yield return new WaitForSecondsRealtime(0.05f);
                Time.timeScale = 0f;
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(
                    folder,
                    $"sequence-character-{index:D2}-{width}x{height}.png");
                yield return CaptureScreen(path);
                Camera camera = Camera.main;
                Vector3 screen = camera.WorldToScreenPoint(player.PresentationRenderer.transform.position);
                Debug.Log(
                    "RENDER_CLARITY_CHARACTER_SEQUENCE_FRAME | " +
                    $"frame={index:D2} rootScreen=({screen.x:F4},{screen.y:F4}) " +
                    $"rootFraction=({Fraction(screen.x):F4},{Fraction(screen.y):F4}) " +
                    $"renderSnapActors={clarity.LastSnappedCharacterCount} " +
                    $"maxSnapOffset={clarity.LastMaximumCharacterSnapOffsetPixels:F4}px path={path}");
            }
            player.QaSetPlayerInput(Vector2.zero);
            float movedDistance = Vector2.Distance(initialPosition, player.Position);
            if (movedDistance <= 0.01f)
                throw new InvalidOperationException("Player did not move during clarity QA sequence.");
            Time.timeScale = 0f;
            Debug.Log(
                "RENDER_CLARITY_CHARACTER_SEQUENCE_PASS | " +
                $"frames={frameCount} movedDistance={movedDistance:F6}");
        }

        private IEnumerator CaptureCameraMovementSequence(
            PixelClarityRuntime clarity,
            string folder,
            int width,
            int height)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
                throw new InvalidOperationException("Orthographic camera is missing for camera sequence.");
            clarity.ForceReframe();
            yield return null;
            Vector3 basePosition = camera.transform.position;
            float unitsPerPixel = camera.orthographicSize * 2f / camera.pixelHeight;
            const int frameCount = 8;
            for (int index = 0; index < frameCount; index++)
            {
                float requestedPixels = index * 0.35f;
                camera.transform.position = basePosition + camera.transform.right * (requestedPixels * unitsPerPixel);
                yield return null;
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(
                    folder,
                    $"sequence-camera-{index:D2}-{width}x{height}.png");
                yield return CaptureScreen(path);
                Vector3 origin = camera.WorldToScreenPoint(Vector3.zero);
                Debug.Log(
                    "RENDER_CLARITY_CAMERA_SEQUENCE_FRAME | " +
                    $"frame={index:D2} requestedOffset={requestedPixels:F2}px " +
                    $"originScreen=({origin.x:F4},{origin.y:F4}) " +
                    $"residual=({Fraction(origin.x):F4},{Fraction(origin.y):F4}) path={path}");
            }
            clarity.ForceReframe();
            yield return null;
            Debug.Log("RENDER_CLARITY_CAMERA_SEQUENCE_PASS | frames=" + frameCount);
        }

        private static Dictionary<Texture, FilterMode> SetWorldTextureFilter(
            StarterOfficeRuntimeBootstrap starter,
            FilterMode filter)
        {
            var previous = new Dictionary<Texture, FilterMode>();
            foreach (SpriteRenderer renderer in starter.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || renderer.sprite == null || renderer.sprite.texture == null) continue;
                Texture texture = renderer.sprite.texture;
                if (!previous.ContainsKey(texture)) previous.Add(texture, texture.filterMode);
                texture.filterMode = filter;
            }
            Debug.Log($"RENDER_CLARITY_RUNTIME_FILTER_OVERRIDE | filter={filter} textures={previous.Count}");
            return previous;
        }

        private static void RestoreTextureFilters(Dictionary<Texture, FilterMode> previous)
        {
            foreach (KeyValuePair<Texture, FilterMode> item in previous)
                if (item.Key != null) item.Key.filterMode = item.Value;
        }

        private static IEnumerator CaptureScreen(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(path)) File.Delete(path);

            Camera sourceCamera = Camera.main;
            PixelClarityRuntime clarity = PixelClarityRuntime.Instance;
            if (sourceCamera == null || clarity == null)
                throw new InvalidOperationException("Camera or pixel clarity runtime is missing for GPU capture.");

            int width = _captureWidth > 0 ? _captureWidth : Screen.width;
            int height = _captureHeight > 0 ? _captureHeight : Screen.height;
            var source = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "RenderClarityQaNativeSource",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            source.Create();

            var host = new GameObject("RenderClarityQaCaptureCamera");
            Camera captureCamera = host.AddComponent<Camera>();
            captureCamera.CopyFrom(sourceCamera);
            captureCamera.transform.SetPositionAndRotation(
                sourceCamera.transform.position,
                sourceCamera.transform.rotation);
            captureCamera.enabled = false;
            captureCamera.allowDynamicResolution = false;
            captureCamera.targetTexture = source;
            captureCamera.aspect = width / (float)height;
            if (_usesOffscreenTarget)
                ReframeCaptureCamera(captureCamera, width, height, clarity.Profile);
            if (clarity.IsFinalNativeStable && clarity.Profile.SnapCameraToPhysicalPixelGrid)
                PixelClarityRuntime.SnapCameraToPhysicalPixelGrid(captureCamera);
            Debug.Log(
                "RENDER_CLARITY_GPU_CAPTURE_CAMERA | " +
                $"target={width}x{height} aspect={captureCamera.aspect:F5} " +
                $"ortho={captureCamera.orthographicSize:F6} offscreenTarget={_usesOffscreenTarget}");

            RenderTexture readableTarget = source;
            RenderTexture lowResolution = null;
            RenderTexture legacyOutput = null;
            Texture2D readable = null;
            RenderTexture previousActive = RenderTexture.active;
            Camera productionOverlay = FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && string.Equals(
                    candidate.gameObject.name,
                    "Family3DProductionOverlayCamera",
                    StringComparison.Ordinal));
            RenderTexture previousOverlayTarget = null;
            CameraClearFlags previousOverlayClearFlags = CameraClearFlags.Depth;
            try
            {
                captureCamera.Render();
                // The production Player/Father layer is rendered by a second camera. A main-camera
                // only capture used to show two empty health bars and still pass. Composite the
                // overlay into the same target so Fast QA cannot silently omit the production bodies.
                if (productionOverlay != null && productionOverlay.isActiveAndEnabled)
                {
                    previousOverlayTarget = productionOverlay.targetTexture;
                    previousOverlayClearFlags = productionOverlay.clearFlags;
                    productionOverlay.targetTexture = source;
                    productionOverlay.clearFlags = CameraClearFlags.Depth;
                    productionOverlay.Render();
                }
                if (clarity.ComparisonMode == PixelClarityComparisonMode.LegacyHalfHeight)
                {
                    int targetHeight = clarity.Profile.LegacyComparisonHeight;
                    int targetWidth = Mathf.Max(320, Mathf.RoundToInt(targetHeight * (width / (float)height)));
                    lowResolution = RenderTexture.GetTemporary(
                        targetWidth,
                        targetHeight,
                        0,
                        source.format,
                        RenderTextureReadWrite.Default);
                    lowResolution.filterMode = FilterMode.Point;
                    legacyOutput = RenderTexture.GetTemporary(
                        width,
                        height,
                        0,
                        source.format,
                        RenderTextureReadWrite.Default);
                    legacyOutput.filterMode = FilterMode.Point;
                    Graphics.Blit(source, lowResolution);
                    Graphics.Blit(lowResolution, legacyOutput);
                    readableTarget = legacyOutput;
                }

                RenderTexture.active = readableTarget;
                readable = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readable.Apply(false, false);
                ValidateNonBlankCapture(readable, path);
                File.WriteAllBytes(path, readable.EncodeToPNG());
            }
            finally
            {
                if (productionOverlay != null)
                {
                    productionOverlay.targetTexture = previousOverlayTarget;
                    productionOverlay.clearFlags = previousOverlayClearFlags;
                }
                RenderTexture.active = previousActive;
                if (readable != null) DestroyImmediate(readable);
                if (legacyOutput != null) RenderTexture.ReleaseTemporary(legacyOutput);
                if (lowResolution != null) RenderTexture.ReleaseTemporary(lowResolution);
                captureCamera.targetTexture = null;
                DestroyImmediate(host);
                source.Release();
                DestroyImmediate(source);
            }

            if (!File.Exists(path) || new FileInfo(path).Length <= 1024L)
                throw new IOException("GPU camera capture was not written: " + path);
            yield return null;
        }

        private static void ReframeCaptureCamera(
            Camera captureCamera,
            int width,
            int height,
            PixelClarityProfile profile)
        {
            StarterOfficeRuntimeBootstrap starter = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            if (starter == null || !starter.IsReady || starter.World == null ||
                starter.World.Presenter == null || starter.World.FurniturePresenter == null)
                throw new InvalidOperationException("Starter Office is unavailable for offscreen camera fit.");
            Bounds bounds = starter.World.Presenter.FloorRenderer.bounds;
            bounds.Encapsulate(starter.World.FurniturePresenter.RenderBounds);
            float aspect = width / (float)height;
            captureCamera.orthographic = true;
            captureCamera.aspect = aspect;
            captureCamera.orthographicSize = OfficeGridCameraFitter.ResolveOrthographicSize(
                bounds,
                aspect,
                OfficeGridCameraFitter.DefaultOrthographicSize,
                profile.OfficeSafeFraction);
            captureCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            captureCamera.transform.rotation = Quaternion.identity;
        }

        private static void ValidateNonBlankCapture(Texture2D capture, string path)
        {
            Color32[] pixels = capture.GetPixels32();
            int minimum = 255;
            int maximum = 0;
            int visiblePixels = 0;
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                int luminance = (pixel.r * 54 + pixel.g * 183 + pixel.b * 19) >> 8;
                minimum = Mathf.Min(minimum, luminance);
                maximum = Mathf.Max(maximum, luminance);
                if (luminance >= 12) visiblePixels++;
            }
            if (maximum - minimum < 12 || visiblePixels < pixels.Length / 100)
                throw new InvalidOperationException(
                    $"GPU capture is blank or nearly uniform: range={minimum}..{maximum} " +
                    $"visible={visiblePixels}/{pixels.Length} path={path}");
            Debug.Log(
                "RENDER_CLARITY_GPU_CAPTURE_VALIDATED | " +
                $"range={minimum}..{maximum} visible={visiblePixels}/{pixels.Length} path={path}");
        }

        private static int ReadPositiveArgument(string name, int fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(arguments[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int value) && value > 0)
                    return value;
            }
            return fallback;
        }

        private static string ResolveArtifactFolder()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                string directory = Path.GetDirectoryName(Path.GetFullPath(arguments[index + 1]));
                if (!string.IsNullOrWhiteSpace(directory)) return directory;
            }
            return Path.Combine(Application.persistentDataPath, "RenderClarityQa");
        }

        private static float Fraction(float value)
        {
            return Mathf.Abs(value - Mathf.Round(value));
        }
    }
}

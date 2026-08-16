using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.UIRemaster;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Keeps Prototype01's simulation and IMGUI management layer alive while the Starter Office
    /// tile scene becomes the only rendered world. F9 is a one-way recovery shortcut; the removed
    /// OfficeVisualV2 presentation is never restored.
    /// </summary>
    public sealed class ScenePreviewJump : MonoBehaviour
    {
        public const KeyCode JumpKey = KeyCode.F9;
        public const string PreviewSceneName = "OfficeTileMigrationPreview";
        public static bool IsPresentationLoading => _instance != null && _instance._loading;

        private static ScenePreviewJump _instance;
        private bool _loading;
        private float _loadingProgress;
        private float _loadingDisplayedProgress;
        private float _loadingStartedAt;
        private string _loadingStage = "출근 준비를 시작하는 중";
        private bool _loadingUiLogged;
        private bool _loadingUiCapturePending;
        private bool _loadingUiCaptureComplete;
        private bool _loadingUiCapturePassed;
        private bool _tileOfficeActive;
        private Renderer[] _legacyRenderers = System.Array.Empty<Renderer>();
        private StarterOfficeRuntimeBootstrap _starterRuntime;
        private string _playerQaFailure = string.Empty;
        private int _playerQaExitCode;
        private ProfilerRecorder _qaGcAllocatedRecorder;
        private ProfilerRecorder _qaMainThreadRecorder;
        private bool _qaMovementProfilingActive;
        private long _qaMaximumGcAllocatedBytes;
        private long _qaMaximumMainThreadNanoseconds;
        private int _qaMovementProfileSamples;
        private Texture2D _loadingBackground;
        private Texture2D _loadingPanel;
        private Texture2D _loadingTrack;
        private Texture2D _loadingFill;
        private Texture2D _loadingIcon;
        private GUIStyle _loadingPanelStyle;
        private GUIStyle _loadingTrackStyle;
        private GUIStyle _loadingFillStyle;
        private GUIStyle _loadingTitleStyle;
        private GUIStyle _loadingBodyStyle;
        private GUIStyle _loadingPercentStyle;
        private int _loadingStyleWidth;
        private int _loadingStyleHeight;
        private bool _loadingAssetsReady;
        private bool _loadingAssetFailureLogged;

        private static readonly string[] QaMemberIds =
            { "player", "older_sister", "father", "mother" };
        private const int RuntimeActorCount = 4;
        private static readonly string[] QaDirectionNames =
            { "South", "SouthWest", "West", "NorthWest", "North", "NorthEast", "East", "SouthEast" };
        private static readonly Vector2[] QaDirectionVectors =
        {
            new Vector2(0f, -1f), new Vector2(-1f, -1f),
            new Vector2(-1f, 0f), new Vector2(-1f, 1f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 0f), new Vector2(1f, -1f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || FindPreviewBuildIndex() < 0) return;
            var host = new GameObject("~StarterOfficeTileRuntime");
            if (Application.isPlaying) DontDestroyOnLoad(host);
            _instance = host.AddComponent<ScenePreviewJump>();
        }

        public static void ShowStarterOffice()
        {
            if (!Application.isPlaying) return;
            if (_instance == null)
            {
                AutoInstall();
                if (_instance == null)
                {
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬이 빌드에 없습니다.");
                    return;
                }
            }

            _instance.BeginShowStarterOffice();
        }

        private void Start()
        {
            string[] commandLine = Environment.GetCommandLineArgs();
            bool extendedPlayerQa = Array.IndexOf(
                commandLine,
                "-familyCompanyTileRuntimeQa") >= 0;
            bool movementLayoutPlayerQa = Array.IndexOf(
                commandLine,
                "-familyCompanyMovementLayoutQa") >= 0;
            // The QA player is intentionally hosted in a hidden/background window. Keep normal
            // release focus behavior unchanged while allowing that capture route to render.
            if (extendedPlayerQa || movementLayoutPlayerQa)
                Application.runInBackground = true;
            Debug.Log("[StarterOfficeTileRuntime] 처음하기/불러오기 = Starter 타일 사무실 · F2 = 배치 편집 · F9 = 단방향 복구");
            // Warm the additive office while the full-screen title is still visible. New Game can
            // then rebind an already-built runtime instead of exposing Prototype01 for a few seconds.
            BeginShowStarterOffice();
            if (movementLayoutPlayerQa) StartCoroutine(RunMovementLayoutPlayerQa());
            else if (extendedPlayerQa) StartCoroutine(RunExtendedPlayerQa());
        }

        private void Update()
        {
            SampleMovementProfile();
            if (Input.GetKeyDown(JumpKey)) BeginShowStarterOffice();
            if (_loading)
            {
                _loadingDisplayedProgress = Mathf.MoveTowards(
                    _loadingDisplayedProgress,
                    Mathf.Max(_loadingDisplayedProgress, _loadingProgress),
                    Time.unscaledDeltaTime * 0.38f);
                var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
                if (_loadingUiCapturePending && bootstrap != null &&
                    bootstrap.UiScreen == PrototypeUiScreen.Playing)
                {
                    _loadingUiCapturePending = false;
                    Debug.Log("STARTER_OFFICE_LOADING_UI_QA_CAPTURE_SCHEDULED");
                    StartCoroutine(CaptureLoadingUiQa());
                }
            }
        }

        private void LateUpdate()
        {
            if (!_loading && !_tileOfficeActive) return;
            foreach (var renderer in _legacyRenderers)
            {
                if (renderer != null && renderer.enabled) renderer.enabled = false;
            }
        }

        private void OnGUI()
        {
            if (!_loading) return;
            var gameBootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (gameBootstrap == null || gameBootstrap.UiScreen != PrototypeUiScreen.Playing) return;
            if (!_loadingUiLogged)
            {
                Debug.Log("STARTER_OFFICE_LOADING_UI_VISIBLE | style=UiRemasterV3Maplestory");
                _loadingUiLogged = true;
            }
            DrawLoadingPresentation();
        }

        private void DrawLoadingPresentation()
        {
            EnsureLoadingPresentationResources();
            if (!_loadingAssetsReady) return;

            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawTextureAspectFill(screen, _loadingBackground);
            var layout = UiRemasterLayout.CalculateLoading(Screen.width, Screen.height);
            GUI.Box(layout.Panel, GUIContent.none, _loadingPanelStyle);

            var icon = layout.Icon;
            icon.y += Mathf.Round(Mathf.Sin(Time.unscaledTime * 3.2f) * 3f);
            GUI.DrawTexture(icon, _loadingIcon, ScaleMode.ScaleToFit, true);
            GUI.Label(layout.Title, "출근 준비 중", _loadingTitleStyle);
            GUI.Label(layout.Status, _loadingStage, _loadingBodyStyle);

            var progress = Mathf.Clamp01(Mathf.Max(_loadingProgress, _loadingDisplayedProgress));
            GUI.Box(layout.Track, GUIContent.none, _loadingTrackStyle);
            if (progress > 0.001f)
            {
                var fillInset = Mathf.Max(4f, Mathf.Round(layout.Track.height * 0.20f));
                var fill = UiRemasterTypography.PixelSnap(new Rect(
                    layout.Track.x + fillInset,
                    layout.Track.y + fillInset,
                    Mathf.Max(1f, (layout.Track.width - fillInset * 2f) * progress),
                    layout.Track.height - fillInset * 2f));
                GUI.Box(fill, GUIContent.none, _loadingFillStyle);
            }

            GUI.Label(layout.Percent, Mathf.RoundToInt(progress * 100f) + "%", _loadingPercentStyle);
            var dots = Mathf.FloorToInt(Time.unscaledTime * 2.4f) % 4;
            GUI.Label(layout.Detail,
                "가족별 출근 경로와 지정 좌석을 준비하고 있습니다" + new string('·', dots),
                _loadingBodyStyle);
        }

        private void EnsureLoadingPresentationResources()
        {
            if (_loadingStyleWidth == Screen.width && _loadingStyleHeight == Screen.height &&
                _loadingTitleStyle != null) return;
            _loadingStyleWidth = Screen.width;
            _loadingStyleHeight = Screen.height;
            const string root = "UiRemasterV3/Loading/";
            if (_loadingBackground == null) _loadingBackground = Resources.Load<Texture2D>(root + "loading_background_v3");
            if (_loadingPanel == null) _loadingPanel = Resources.Load<Texture2D>(root + "loading_panel_v4");
            if (_loadingTrack == null) _loadingTrack = Resources.Load<Texture2D>(root + "progress_track_v4");
            if (_loadingFill == null) _loadingFill = Resources.Load<Texture2D>(root + "progress_fill_v4");
            if (_loadingIcon == null) _loadingIcon = Resources.Load<Texture2D>(root + "loading_work_icon_v4");

            if (!UiRemasterTypography.TryLoadFonts(out var bodyFont, out var headingFont, out _, out var fontError))
            {
                _loadingAssetsReady = false;
                if (!_loadingAssetFailureLogged)
                {
                    Debug.LogError("UI_REMASTER_V3_LOADING_FONT_MISSING | " + fontError);
                    _loadingAssetFailureLogged = true;
                }
                return;
            }

            _loadingAssetsReady = _loadingBackground != null && _loadingPanel != null && _loadingTrack != null &&
                                  _loadingFill != null && _loadingIcon != null;
            if (!_loadingAssetsReady)
            {
                if (!_loadingAssetFailureLogged)
                {
                    Debug.LogError("UI_REMASTER_V3_LOADING_ASSET_MISSING | generated loading assets failed to load");
                    _loadingAssetFailureLogged = true;
                }
                return;
            }

            var scale = UiRemasterTypography.CalculateScale(Screen.width, Screen.height);
            var ink = new Color(0.125f, 0.23f, 0.23f, 1f);
            var mutedInk = new Color(0.22f, 0.34f, 0.33f, 1f);
            _loadingPanelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(36, 36, 32, 32),
                normal = { background = _loadingPanel }
            };
            _loadingTrackStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(32, 32, 14, 14),
                normal = { background = _loadingTrack }
            };
            _loadingFillStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(28, 28, 10, 10),
                normal = { background = _loadingFill }
            };
            _loadingTitleStyle = UiRemasterTypography.CreateLabel(
                GUI.skin.label, headingFont, UiRemasterTypography.PanelTitlePixels, scale,
                TextAnchor.MiddleLeft, ink);
            _loadingBodyStyle = UiRemasterTypography.CreateLabel(
                GUI.skin.label, bodyFont, UiRemasterTypography.BodyPixels, scale,
                TextAnchor.MiddleLeft, mutedInk, true);
            _loadingPercentStyle = UiRemasterTypography.CreateLabel(
                GUI.skin.label, headingFont, UiRemasterTypography.ButtonPixels, scale,
                TextAnchor.MiddleRight, ink);
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
            GUI.DrawTextureWithTexCoords(target, texture, source, true);
        }

        private void BeginShowStarterOffice()
        {
            if (_tileOfficeActive)
            {
                var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
                if (bootstrap == null) return;
                if (bootstrap.UiScreen == PrototypeUiScreen.Playing)
                {
                    if (_loading) return;
                    _loading = true;
                    _loadingProgress = 0.12f;
                    _loadingDisplayedProgress = 0f;
                    _loadingStartedAt = Time.unscaledTime;
                    _loadingStage = "가족 네 명의 출근 준비를 확인하는 중";
                    _loadingUiLogged = false;
                    _loadingUiCapturePending = Array.IndexOf(
                        Environment.GetCommandLineArgs(),
                        "-familyCompanyTileRuntimeQa") >= 0;
                    _loadingUiCaptureComplete = !_loadingUiCapturePending;
                    _loadingUiCapturePassed = !_loadingUiCapturePending;
                    CaptureAndHideLegacyRenderers();
                    StartCoroutine(RebindStarterOfficeWithLoading(bootstrap));
                }
                else _starterRuntime?.Rebind(bootstrap);
                return;
            }
            if (_loading) return;
            _loading = true;
            _loadingProgress = 0.02f;
            _loadingDisplayedProgress = 0f;
            _loadingStartedAt = Time.unscaledTime;
            _loadingStage = "오늘의 사무실을 확인하는 중";
            _loadingUiLogged = false;
            _loadingUiCapturePending = Array.IndexOf(
                Environment.GetCommandLineArgs(),
                "-familyCompanyTileRuntimeQa") >= 0;
            _loadingUiCaptureComplete = !_loadingUiCapturePending;
            _loadingUiCapturePassed = !_loadingUiCapturePending;
            CaptureAndHideLegacyRenderers();
            StartCoroutine(LoadStarterOffice());
        }

        private IEnumerator RebindStarterOfficeWithLoading(PrototypeBootstrap bootstrap)
        {
            // Always present one real frame before the synchronous rebind. This prevents the New
            // Game click from looking frozen even when the additive office was warmed at title.
            yield return null;
            _loadingProgress = 0.62f;
            _loadingStage = "문에서 지정 좌석까지 출근 동선을 미리 계산하는 중";
            yield return null;
            _starterRuntime?.Rebind(bootstrap);
            _loadingProgress = 1f;
            _loadingDisplayedProgress = 1f;
            _loadingStage = "09:00 출근 준비 완료";
            yield return null;
            float loadingCaptureDeadline = Time.unscaledTime + 2f;
            while (!_loadingUiCaptureComplete && Time.unscaledTime < loadingCaptureDeadline)
                yield return null;
            Debug.Log(
                "STARTER_OFFICE_LOADING_UI_COMPLETE | mode=WarmRebind elapsed=" +
                (Time.unscaledTime - _loadingStartedAt).ToString("F2") + "s");
            _loading = false;
        }

        private void CaptureAndHideLegacyRenderers()
        {
            if (_legacyRenderers.Length == 0 && SceneManager.sceneCount > 0)
                _legacyRenderers = CollectRenderers(SceneManager.GetSceneAt(0));
            foreach (var renderer in _legacyRenderers)
                if (renderer != null) renderer.enabled = false;
        }

        private IEnumerator LoadStarterOffice()
        {
            var previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            if (!previewScene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(PreviewSceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    _loading = false;
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드를 시작하지 못했습니다.");
                    yield break;
                }
                while (!operation.isDone)
                {
                    _loadingProgress = Mathf.Lerp(0.06f, 0.52f, Mathf.Clamp01(operation.progress / 0.9f));
                    _loadingStage = "사무실 타일과 가구를 불러오는 중";
                    yield return null;
                }
                previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            }

            _loadingProgress = 0.58f;
            _loadingStage = "현관과 외벽을 세우는 중";
            yield return null;

            if (!previewScene.IsValid() || !previewScene.isLoaded)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드 후 검증에 실패했습니다.");
                yield break;
            }

            var bootstrap = FindBootstrap(previewScene);
            if (bootstrap == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] OfficeTileMigrationPreviewBootstrap이 없습니다.");
                yield break;
            }

            bootstrap.DestroyGeneratedPreview();
            _loadingProgress = 0.68f;
            yield return null;

            Camera previewCamera = null;
            foreach (var root in previewScene.GetRootGameObjects())
            {
                var cameras = root.GetComponentsInChildren<Camera>(true);
                if (cameras.Length > 0 && previewCamera == null) previewCamera = cameras[0];
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }

            if (previewCamera == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 카메라가 없습니다.");
                yield break;
            }

            CaptureAndHideLegacyRenderers();

            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera == previewCamera) continue;
                camera.enabled = false;
                if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
            }

            previewCamera.tag = "MainCamera";
            previewCamera.enabled = true;
            var gameBootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (gameBootstrap == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] PrototypeBootstrap이 없습니다.");
                yield break;
            }
            _starterRuntime = bootstrap.GetComponent<StarterOfficeRuntimeBootstrap>();
            if (_starterRuntime == null)
                _starterRuntime = bootstrap.gameObject.AddComponent<StarterOfficeRuntimeBootstrap>();
            _loadingProgress = 0.76f;
            _loadingStage = "가족 네 명의 출근 동선을 미리 계산하는 중";
            yield return null;
            _starterRuntime.Configure(gameBootstrap, bootstrap, previewCamera, _legacyRenderers);
            while (_starterRuntime.IsPreparing)
            {
                _loadingProgress = Mathf.Lerp(
                    0.76f,
                    0.93f,
                    _starterRuntime.NavigationPrewarmProgress);
                _loadingStage = "이동 경로를 안전하게 사전 계산하는 중";
                yield return null;
            }
            _loadingProgress = 0.93f;
            _loadingStage = "09:00 출근 준비를 마무리하는 중";
            yield return null;
            float loadingCaptureDeadline = Time.unscaledTime + 2f;
            while (!_loadingUiCaptureComplete && Time.unscaledTime < loadingCaptureDeadline)
                yield return null;
            var layoutEditor = _starterRuntime.GetComponent<OfficeLayoutEditModeController>();
            if (layoutEditor == null)
                layoutEditor = _starterRuntime.gameObject.AddComponent<OfficeLayoutEditModeController>();
            layoutEditor.Configure(_starterRuntime, previewCamera);
            if (!_starterRuntime.IsReady)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] Starter Office Runtime 구성에 실패했습니다.");
                yield break;
            }
            _tileOfficeActive = true;
            _loadingProgress = 1f;
            _loadingDisplayedProgress = 1f;
            Debug.Log(
                "STARTER_OFFICE_LOADING_UI_COMPLETE | elapsed=" +
                (Time.unscaledTime - _loadingStartedAt).ToString("F2") + "s");
            _loading = false;
            Debug.Log(
                "[StarterOfficeTileRuntime] PASS · StarterOfficeV1 기본 표시 · " +
                $"legacyRenderers={_legacyRenderers.Length} actors={_starterRuntime.Actors.Count}");
        }

        private IEnumerator CaptureLoadingUiQa()
        {
            yield return new WaitForEndOfFrame();
            string path = QaArtifactPath("starter-office-loading.png");
            Texture2D capture = null;
            RenderTexture renderTexture = null;
            RenderTexture previousActive = null;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    Screen.width,
                    Screen.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
                previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0, false);
                capture.Apply(false, false);
                Color32[] pixels = capture.GetPixels32();
                for (int y = 0; y < capture.height / 2; y++)
                {
                    int oppositeY = capture.height - 1 - y;
                    for (int x = 0; x < capture.width; x++)
                    {
                        int sourceIndex = y * capture.width + x;
                        int targetIndex = oppositeY * capture.width + x;
                        (pixels[sourceIndex], pixels[targetIndex]) = (pixels[targetIndex], pixels[sourceIndex]);
                    }
                }
                capture.SetPixels32(pixels);
                capture.Apply(false, false);
                long luminance = 0L;
                int samples = 0;
                int stepX = Mathf.Max(1, Screen.width / 32);
                int stepY = Mathf.Max(1, Screen.height / 18);
                for (int y = stepY / 2; y < Screen.height; y += stepY)
                {
                    for (int x = stepX / 2; x < Screen.width; x += stepX)
                    {
                        Color32 pixel = capture.GetPixel(x, y);
                        luminance += pixel.r + pixel.g + pixel.b;
                        samples++;
                    }
                }

                if (samples == 0 || luminance <= samples * 6L)
                {
                    if (File.Exists(path)) File.Delete(path);
                    _loadingUiCapturePassed = false;
                    Debug.LogError("STARTER_OFFICE_LOADING_UI_QA_FAIL | capture framebuffer is black");
                }
                else
                {
                    File.WriteAllBytes(path, capture.EncodeToPNG());
                    _loadingUiCapturePassed = true;
                    Debug.Log("STARTER_OFFICE_LOADING_UI_QA_PASS | capture=" + path +
                              " resolution=" + Screen.width + "x" + Screen.height);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (capture != null) Destroy(capture);
                if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
                _loadingUiCaptureComplete = true;
            }
        }

        private IEnumerator RunPlayerQa()
        {
            yield return null;
            var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · PrototypeBootstrap missing");
                Application.Quit(31);
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            float activationDeadline = Time.unscaledTime + 15f;
            while (!_tileOfficeActive && Time.unscaledTime < activationDeadline) yield return null;
            if (!_tileOfficeActive)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · tile office activation timeout");
                Application.Quit(32);
                yield break;
            }

            if (_starterRuntime == null || !_starterRuntime.IsReady ||
                _starterRuntime.World == null || _starterRuntime.Actors.Count != RuntimeActorCount)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · Starter runtime invariant");
                Application.Quit(33);
                yield break;
            }

            Debug.Log(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS · " +
                $"layoutHash={_starterRuntime.LayoutHash} furniture={_starterRuntime.World.Grid.Furniture.Count} " +
                $"characters={_starterRuntime.Actors.Count} legacyRenderers={_legacyRenderers.Length}");
            yield return null;
            Application.Quit(0);
        }

        private IEnumerator RunExtendedPlayerQa()
        {
            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | PrototypeBootstrap missing");
                Application.Quit(31);
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            float activationDeadline = Time.unscaledTime + 15f;
            while (!_tileOfficeActive && Time.unscaledTime < activationDeadline) yield return null;
            if (!_tileOfficeActive || _starterRuntime == null || !_starterRuntime.IsReady ||
                _starterRuntime.World == null || _starterRuntime.Actors.Count != RuntimeActorCount)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | Starter runtime activation timeout");
                Application.Quit(33);
                yield break;
            }

            float loadingUiDeadline = Time.unscaledTime + 5f;
            while ((_loading || !_loadingUiCaptureComplete) && Time.unscaledTime < loadingUiDeadline)
                yield return null;
            if (!_loadingUiCaptureComplete || !_loadingUiCapturePassed)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | loading UI D3D capture missing or black");
                Application.Quit(34);
                yield break;
            }

            BeginMovementProfile();

            yield return RunAttendanceFlowQa(bootstrap);
            if (QuitIfPlayerQaFailed(Time.timeScale)) yield break;
            yield return RunRealtimeAutonomyClockQa(bootstrap);
            if (QuitIfPlayerQaFailed(Time.timeScale)) yield break;
            yield return CaptureOfficeHudQa();
            if (QuitIfPlayerQaFailed(Time.timeScale)) yield break;

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 4f;

            yield return RunAutonomousMeetingSeatingQa(bootstrap);
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunFourSeatWorkQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunFourWayIntersectionQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunRuntimeDeskPlacementQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunNarrowCorridorQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunEightDirectionMovementQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunReversalPivotQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;

            _starterRuntime.ApplyLayoutForQa(OfficeGridLayouts.CreateStarterOfficeV1());
            yield return WaitForRuntimeReady(46, "restore StarterOfficeV1");
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunMicroActionDestinationQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunPlayerCollisionQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunContractAndSaveLoadQa(bootstrap);
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;

            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            string movementProfile = EndMovementProfile();
            Debug.Log(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS | " +
                $"layoutHash={_starterRuntime.LayoutHash} furniture={_starterRuntime.World.Grid.Furniture.Count} " +
                $"characters={_starterRuntime.Actors.Count} legacyRenderers={_legacyRenderers.Length} " +
                $"replans={_starterRuntime.World.ReplanCount} arrivals={_starterRuntime.World.ArrivalCount} " +
                $"blockedStaticAttempts={occupancy.StaticViolationCount} " +
                $"blockedInteractionAttempts={occupancy.InteractionViolationCount} " +
                $"agentPenetrations={occupancy.AgentPenetrationCount} | {movementProfile}");
            Time.timeScale = previousTimeScale;
            yield return null;
            Application.Quit(0);
        }

        private IEnumerator RunMovementLayoutPlayerQa()
        {
            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_MOVEMENT_LAYOUT_QA: FAIL | PrototypeBootstrap missing");
                Application.Quit(61);
                yield break;
            }
            bootstrap.StartNewGameNow(1, false);
            float activationDeadline = Time.unscaledTime + 15f;
            while (!_tileOfficeActive && Time.unscaledTime < activationDeadline) yield return null;
            if (!_tileOfficeActive || _starterRuntime == null || !_starterRuntime.IsReady ||
                _starterRuntime.World == null || _starterRuntime.Actors.Count != RuntimeActorCount)
            {
                Debug.LogError("FAMILY_COMPANY_MOVEMENT_LAYOUT_QA: FAIL | runtime activation timeout");
                Application.Quit(62);
                yield break;
            }

            BeginMovementProfile();
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 4f;

            yield return RunFourWayIntersectionQa();
            if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            if (!CaptureMovementLayoutQa("default-four-way", 63))
            {
                if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            }

            yield return RunRuntimeDeskPlacementQa();
            if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;

            yield return RunNarrowCorridorQa();
            if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            if (!CaptureMovementLayoutQa("narrow-corridor", 64))
            {
                if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            }

            yield return RunEightDirectionMovementQa();
            if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            if (!CaptureMovementLayoutQa("open-direction", 65))
            {
                if (QuitIfMovementLayoutQaFailed(previousTimeScale)) yield break;
            }

            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            string movementProfile = EndMovementProfile();
            Debug.Log(
                "FAMILY_COMPANY_MOVEMENT_LAYOUT_QA: PASS | layouts=default-four-way," +
                "runtime-desk,narrow-corridor,open-direction captures=4 " +
                $"replans={_starterRuntime.World.ReplanCount} " +
                $"arrivals={_starterRuntime.World.ArrivalCount} " +
                $"blockedStaticAttempts={occupancy.StaticViolationCount} " +
                $"blockedInteractionAttempts={occupancy.InteractionViolationCount} " +
                $"agentPenetrations={occupancy.AgentPenetrationCount} | {movementProfile}");
            Time.timeScale = previousTimeScale;
            yield return null;
            Application.Quit(0);
        }

        private bool CaptureMovementLayoutQa(string layoutName, int exitCode)
        {
            string path = QaArtifactPath("movement-layout-" + layoutName + ".png");
            if (TryCaptureQaCameraFrame(path, out string failure))
            {
                Debug.Log("STARTER_OFFICE_MOVEMENT_LAYOUT_CAPTURE_QA_PASS | layout=" +
                          layoutName + " | capture=" + path);
                return true;
            }
            FailPlayerQa(exitCode, layoutName + " capture failed: " + failure);
            return false;
        }

        private bool QuitIfMovementLayoutQaFailed(float previousTimeScale)
        {
            if (_playerQaFailure.Length == 0) return false;
            string movementProfile = EndMovementProfile();
            Debug.LogError(
                "FAMILY_COMPANY_MOVEMENT_LAYOUT_QA: FAIL | code=" + _playerQaExitCode +
                " | " + _playerQaFailure + " | " + movementProfile);
            Time.timeScale = previousTimeScale;
            Application.Quit(_playerQaExitCode == 0 ? 60 : _playerQaExitCode);
            return true;
        }

        private IEnumerator RunAutonomousMeetingSeatingQa(PrototypeBootstrap bootstrap)
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            string[] meetingMembers = { "older_sister" };
            if (!actors["older_sister"].AssignOfficeTask(
                    "qa-meeting-seating:" + bootstrap.State.Time.ElapsedMinutes,
                    OfficeActivity.Meeting,
                    30f))
            {
                FailPlayerQa(37, "sister meeting seating task could not be assigned");
                yield break;
            }

            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < 45f && meetingMembers.Any(memberId =>
                       !actors[memberId].IsSeated ||
                       actors[memberId].CurrentActivity != OfficeActivity.Meeting))
                yield return null;

            foreach (string memberId in meetingMembers)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                string expectedSeatId = "seat_" + memberId;
                if (!actor.IsSeated || actor.CurrentActivity != OfficeActivity.Meeting ||
                    !string.Equals(actor.ActiveSeatId, expectedSeatId, StringComparison.Ordinal))
                {
                    FailPlayerQa(
                        38,
                        $"autonomous meeting did not remain seated for {memberId}: " +
                        $"phase={actor.Phase} activity={actor.CurrentActivity} seat={actor.ActiveSeatId}");
                    yield break;
                }
                OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
                if (!_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.ChairFurnitureId,
                        out SpriteRenderer chairRenderer) || !chairRenderer.enabled)
                {
                    FailPlayerQa(39, "occupied meeting chair renderer disappeared for " + memberId);
                    yield break;
                }
            }

            OfficeSeatSlot emptyPlayerSeat = _starterRuntime.World.Workstations.RequiredSeat("seat_player");
            if (!_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                    emptyPlayerSeat.ChairFurnitureId,
                    out SpriteRenderer emptyChairBase) || !emptyChairBase.enabled)
            {
                FailPlayerQa(39, "unoccupied player chair base sprite disappeared");
                yield break;
            }
            // Some chairs are authored as a complete single sprite while others are split into
            // base/front layers. A split front must remain visible when empty, but a single-sprite
            // chair must not fail merely because it has no redundant front renderer.
            if (_starterRuntime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    emptyPlayerSeat.ChairFurnitureId,
                    out SpriteRenderer emptyChairFront) && !emptyChairFront.enabled)
            {
                FailPlayerQa(39, "unoccupied player chair front sprite disappeared");
                yield break;
            }

            string capturePath = QaArtifactPath("starter-office-autonomous-meeting-seated.png");
            if (!TryCaptureQaCameraFrame(capturePath, out string captureFailure))
            {
                FailPlayerQa(39, "autonomous meeting capture failed: " + captureFailure);
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_AUTONOMOUS_MEETING_SEATING_QA_PASS | members=" +
                string.Join(",", meetingMembers) +
                " | activity=Meeting seatedAt=assigned-workstation " +
                "occupiedChairVisible=true emptyChairVisible=true | capture=" + capturePath);
        }

        private IEnumerator RunAttendanceFlowQa(PrototypeBootstrap bootstrap)
        {
            OfficeAutonomyCoordinator autonomy =
                Object.FindFirstObjectByType<OfficeAutonomyCoordinator>();
            if (autonomy == null)
            {
                FailPlayerQa(35, "attendance autonomy coordinator is missing");
                yield break;
            }
            GameAudioCoordinator audio = GameAudioCoordinator.Instance;
            int doorOpenSfxBefore = audio.DoorOpenSfxPlayCount;
            int doorCloseSfxBefore = audio.DoorCloseSfxPlayCount;
            PlacedOfficeFurniture[] openPassageMarkers = _starterRuntime.World.Grid.Furniture
                .Where(item => string.Equals(
                    item.KindId,
                    OfficeGridLayouts.EntranceDoorKind,
                    StringComparison.Ordinal))
                .ToArray();
            var expectedPassageCell = new OfficeGridCoordinate(8, 0);
            var expectedEntranceCell = OfficeRuntimeWorkstationService.StarterEntranceCell;
            if (openPassageMarkers.Length != 1 ||
                !openPassageMarkers[0].Origin.Equals(expectedPassageCell) ||
                openPassageMarkers[0].Width != 1 || openPassageMarkers[0].Height != 1 ||
                openPassageMarkers[0].BlocksMovement ||
                !expectedEntranceCell.Equals(new OfficeGridCoordinate(8, 1)) ||
                !_starterRuntime.World.Grid.IsWalkable(expectedEntranceCell))
            {
                FailPlayerQa(35, "starter office open-passage marker or interior entrance cell is invalid");
                yield break;
            }
            if (!_starterRuntime.World.FurniturePresenter.TryGetSemanticRoot(
                    openPassageMarkers[0].FurnitureId,
                    out Transform openPassageRoot) ||
                !_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                    openPassageMarkers[0].FurnitureId,
                    out SpriteRenderer openPassageRenderer) ||
                openPassageRenderer.sprite == null ||
                openPassageRenderer.sprite.texture.filterMode != FilterMode.Point ||
                openPassageRoot.GetComponentInChildren<Animator>(true) != null ||
                openPassageRoot.GetComponentInChildren<Animation>(true) != null)
            {
                FailPlayerQa(35, "open passage renderer is missing, filtered, or visually animated");
                yield break;
            }
            if (_starterRuntime.Actors.Count != RuntimeActorCount)
            {
                FailPlayerQa(35, "attendance roster did not contain the four starting family actors");
                yield break;
            }
            if (_starterRuntime.Actors.Any(actor => actor == null || !actor.IsPresentationAway))
            {
                FailPlayerQa(35, "actors were visible before the 09:00 office opening");
                yield break;
            }
            if (autonomy.AttendanceDoorSfxPlayCount != 0)
            {
                FailPlayerQa(35, "attendance door SFX played before the 09:00 office opening");
                yield break;
            }
            if (audio.DoorOpenSfxPlayCount != doorOpenSfxBefore ||
                audio.DoorCloseSfxPlayCount != doorCloseSfxBefore)
            {
                FailPlayerQa(35, "a door cue played before the 09:00 office opening");
                yield break;
            }

            bootstrap.AdvanceTimeNow(10L);
            yield return null;
            OfficeRuntimeAgent first = _starterRuntime.Actors.FirstOrDefault(actor => actor.AgentId == "player");
            if (first == null || first.IsPresentationAway)
            {
                FailPlayerQa(35, "the first family actor did not enter through the office door at 09:00");
                yield break;
            }
            if (_starterRuntime.Actors.Count(actor => !actor.IsPresentationAway) != 1)
            {
                FailPlayerQa(35, "09:00 attendance stagger did not begin with exactly one actor");
                yield break;
            }
            if (autonomy.AttendanceDoorSfxPlayCount != 1)
            {
                FailPlayerQa(35, "attendance start did not play exactly one door-open SFX");
                yield break;
            }
            if (audio.DoorOpenSfxPlayCount - doorOpenSfxBefore != 1 ||
                audio.DoorCloseSfxPlayCount - doorCloseSfxBefore != 0)
            {
                FailPlayerQa(35, "attendance start emitted an incorrect open/close door cue sequence");
                yield break;
            }
            OfficeRuntimeAgent firstEntrant = _starterRuntime.Actors.Single(item => !item.IsPresentationAway);
            float entranceDeadline = Time.unscaledTime + 5f;
            Vector2 previousEntrancePosition = firstEntrant.Position;
            float maximumEntranceRenderStep = 0f;
            while (!_starterRuntime.World.Presenter.NearestCell(
                       firstEntrant.transform.position).Equals(expectedEntranceCell) &&
                   Time.unscaledTime < entranceDeadline)
            {
                yield return null;
                float renderStep = Vector2.Distance(
                    previousEntrancePosition,
                    firstEntrant.Position);
                maximumEntranceRenderStep = Mathf.Max(maximumEntranceRenderStep, renderStep);
                if (renderStep > 0.099001f)
                {
                    FailPlayerQa(
                        35,
                        "first attendance actor exceeded the visible ingress frame budget: " +
                        renderStep.ToString("F6"));
                    yield break;
                }
                if (_starterRuntime.Actors.Count(actor => !actor.IsPresentationAway) != 1)
                {
                    FailPlayerQa(
                        35,
                        "another attendance actor became visible before the first reached the entrance");
                    yield break;
                }
                previousEntrancePosition = firstEntrant.Position;
            }
            if (!_starterRuntime.World.Presenter.NearestCell(firstEntrant.transform.position).Equals(expectedEntranceCell))
            {
                FailPlayerQa(
                    35,
                    "first attendance actor did not walk from the exterior to the canonical entrance " +
                    "within 5 real seconds: position=" + firstEntrant.Position);
                yield break;
            }
            string entranceCapturePath = QaArtifactPath(
                "starter-office-open-entrance-arrival-1920x1080.png");
            if (!TryCaptureStarterOfficeFrame(
                    entranceCapturePath,
                    1920,
                    1080,
                    out string entranceCaptureFailure))
            {
                FailPlayerQa(35, "open entrance arrival capture failed: " + entranceCaptureFailure);
                yield break;
            }

            bootstrap.AdvanceTimeNow(3L);
            float attendanceDeadline = Time.unscaledTime + 30f;
            while (_starterRuntime.Actors.Any(actor => actor.IsPresentationAway) &&
                   Time.unscaledTime < attendanceDeadline)
                yield return null;
            if (_starterRuntime.Actors.Any(actor => actor.IsPresentationAway))
            {
                FailPlayerQa(35, "all four family actors were not present by 09:03");
                yield break;
            }
            float seatDeadline = Time.unscaledTime + 45f;
            while (_starterRuntime.Actors.Any(actor => actor.AttendanceSeatArrivalCount < 1) &&
                   Time.unscaledTime < seatDeadline)
                yield return null;
            if (_starterRuntime.Actors.Any(actor => actor.AttendanceSeatArrivalCount < 1))
            {
                string incomplete = string.Join(",", _starterRuntime.Actors
                    .Where(actor => actor.AttendanceSeatArrivalCount < 1)
                    .Select(actor =>
                        actor.AgentId + ":" + actor.Phase +
                        ":cell=" + _starterRuntime.World.Occupancy.CurrentCell(actor.AgentId) +
                        ":position=" + actor.Position +
                        ":stuck=" + actor.StuckSeconds.ToString("F2") +
                        ":path=" + actor.PresentationPathIndex + "/" + actor.SemanticPathLength +
                        ":reservation=" + actor.LastReservationBlocker +
                        ":movement=" + actor.LastMovementBlocker));
                FailPlayerQa(35, "family did not complete door-to-assigned-seat arrival: " + incomplete);
                yield break;
            }
            if (autonomy.AttendanceDoorSfxPlayCount != 1)
            {
                FailPlayerQa(35, "staggered family arrivals repeated the attendance door SFX");
                yield break;
            }
            if (audio.DoorOpenSfxPlayCount - doorOpenSfxBefore != 1 ||
                audio.DoorCloseSfxPlayCount - doorCloseSfxBefore != 0)
            {
                FailPlayerQa(35, "staggered attendance emitted duplicate or closing door cues");
                yield break;
            }
            string overviewCapturePath = QaArtifactPath(
                "starter-office-perimeter-overview-1920x1080.png");
            if (!TryCaptureStarterOfficeFrame(
                    overviewCapturePath,
                    1920,
                    1080,
                    out string overviewCaptureFailure))
            {
                FailPlayerQa(35, "perimeter overview capture failed: " + overviewCaptureFailure);
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_ATTENDANCE_FLOW_QA_PASS | start=08:50 hidden=4 " +
                "openPassage=(8,0) oneTile=true nonBlocking=true doorAnimation=false " +
                "entrance=(8,1) firstActorAlignedEntrance=true maxIngressRenderStep=" +
                maximumEntranceRenderStep.ToString("F6") + " entry=09:00..09:03 present=4 " +
                "doorOpenSfx=1 doorCloseSfx=0 duplicateSfx=0 assignedSeatArrivals=4 " +
                "stagingStops=0 exit=18:00 captures=1920x1080");
        }

        private IEnumerator RunFourWayIntersectionQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            var starts = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
            {
                ["player"] = new OfficeGridCoordinate(4, 5),
                ["older_sister"] = new OfficeGridCoordinate(4, 7),
                ["father"] = new OfficeGridCoordinate(3, 6),
                ["mother"] = new OfficeGridCoordinate(5, 6)
            };
            var goals = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
            {
                ["player"] = starts["older_sister"],
                ["older_sister"] = starts["player"],
                ["father"] = starts["mother"],
                ["mother"] = starts["father"]
            };
            foreach (string memberId in QaMemberIds) actors[memberId].QaTeleportToCell(starts[memberId]);
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            foreach (string memberId in QaMemberIds)
            {
                if (actors[memberId].QaMoveToCell(goals[memberId], "four-way")) continue;
                FailPlayerQa(40, "four-way route could not be created for " + memberId);
                yield break;
            }

            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < 60f &&
                   QaMemberIds.Any(memberId => !actors[memberId].QaReachedCell(goals[memberId])))
                yield return null;
            if (QaMemberIds.Any(memberId => !actors[memberId].QaReachedCell(goals[memberId])))
            {
                FailPlayerQa(
                    41,
                    "four-way crossing did not finish within 60 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | " + OccupancyMetricSummary());
                yield break;
            }
            if (!RequireZeroActualViolations("four-way", 42)) yield break;
            Debug.Log(
                "STARTER_OFFICE_FOUR_WAY_QA_PASS | duration=" + (Time.unscaledTime - started).ToString("F2") +
                " | " + RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());
        }

        private IEnumerator RunRuntimeDeskPlacementQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            actors["player"].QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            if (!actors["player"].QaMoveToCell(new OfficeGridCoordinate(7, 6), "before-desk-placement"))
            {
                FailPlayerQa(43, "pre-placement route could not be created");
                yield break;
            }
            float motionStart = Time.unscaledTime;
            while (Time.unscaledTime - motionStart < 0.35f) yield return null;
            OfficeGridCoordinate preservedPlayerCell =
                _starterRuntime.World.Presenter.NearestCell(actors["player"].transform.position);

            string previousHash = _starterRuntime.LayoutHash;
            _starterRuntime.ApplyLayoutForQa(CreateRuntimeDeskQaLayout());
            yield return WaitForRuntimeReady(44, "runtime desk placement");
            if (_playerQaFailure.Length > 0) yield break;
            if (string.Equals(previousHash, _starterRuntime.LayoutHash, StringComparison.Ordinal))
            {
                FailPlayerQa(44, "runtime desk placement did not revise the semantic layout hash");
                yield break;
            }

            actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeGridCoordinate restoredPlayerCell =
                _starterRuntime.World.Presenter.NearestCell(actors["player"].transform.position);
            if (_starterRuntime.World.Occupancy.IsCellPassable(
                    preservedPlayerCell,
                    "player",
                    string.Empty,
                    false) &&
                !restoredPlayerCell.Equals(preservedPlayerCell))
            {
                FailPlayerQa(
                    44,
                    $"layout rebuild reset player location: {preservedPlayerCell} -> {restoredPlayerCell}");
                yield break;
            }
            actors["player"].QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            if (!actors["player"].QaMoveToCell(new OfficeGridCoordinate(7, 6), "after-desk-placement"))
            {
                FailPlayerQa(44, "post-placement detour route could not be created");
                yield break;
            }
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < 30f &&
                   !actors["player"].QaReachedCell(new OfficeGridCoordinate(7, 6)))
                yield return null;
            if (!actors["player"].QaReachedCell(new OfficeGridCoordinate(7, 6)))
            {
                FailPlayerQa(44, "post-placement route did not detour around the new desk");
                yield break;
            }
            if (!RequireZeroActualViolations("runtime-desk-placement", 44)) yield break;
            if (!CaptureMovementLayoutQa("runtime-desk", 44)) yield break;
            Debug.Log(
                "STARTER_OFFICE_RUNTIME_DESK_PLACEMENT_QA_PASS | previousHash=" + previousHash +
                " | revisedHash=" + _starterRuntime.LayoutHash + " | " +
                RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());

            _starterRuntime.ApplyLayoutForQa(OfficeGridLayouts.CreateStarterOfficeV1());
            yield return WaitForRuntimeReady(45, "runtime desk removal");
            if (_playerQaFailure.Length > 0) yield break;
            actors = RequiredQaActors();
            if (actors == null) yield break;
            var reopenedStart = new OfficeGridCoordinate(5, 6);
            var reopenedCenter = new OfficeGridCoordinate(6, 6);
            var reopenedGoal = new OfficeGridCoordinate(7, 6);
            actors["player"].QaTeleportToCell(reopenedStart);
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            IReadOnlyList<OfficeGridCoordinate> reopenedPath = _starterRuntime.World.FindPath(
                "player",
                reopenedStart,
                reopenedGoal,
                string.Empty);
            if (reopenedPath.Count != 3 || !reopenedPath[1].Equals(reopenedCenter) ||
                !actors["player"].QaMoveToCell(reopenedGoal, "after-desk-removal"))
            {
                FailPlayerQa(45, "desk removal did not reopen the direct center path");
                yield break;
            }
            started = Time.unscaledTime;
            while (Time.unscaledTime - started < 15f && !actors["player"].QaReachedCell(reopenedGoal))
                yield return null;
            if (!actors["player"].QaReachedCell(reopenedGoal))
            {
                FailPlayerQa(45, "player did not arrive after desk removal");
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_RUNTIME_DESK_REMOVAL_QA_PASS | restoredHash=" +
                _starterRuntime.LayoutHash + " | directPathCells=" + reopenedPath.Count);
        }

        private IEnumerator RunNarrowCorridorQa()
        {
            _starterRuntime.ApplyLayoutForQa(CreateNarrowCorridorQaLayout());
            yield return WaitForRuntimeReady(47, "narrow corridor layout");
            if (_playerQaFailure.Length > 0) yield break;
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            var playerStart = new OfficeGridCoordinate(3, 6);
            var sisterStart = new OfficeGridCoordinate(9, 6);
            var sisterGoal = new OfficeGridCoordinate(2, 6);
            actors["player"].QaTeleportToCell(playerStart);
            actors["older_sister"].QaTeleportToCell(sisterStart);
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(2, 2));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(10, 10));
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            if (!actors["player"].QaMoveToCell(sisterStart, "narrow-corridor") ||
                !actors["older_sister"].QaMoveToCell(sisterGoal, "narrow-corridor"))
            {
                FailPlayerQa(48, "narrow corridor routes could not be created");
                yield break;
            }
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < 60f &&
                   (!actors["player"].QaReachedCell(sisterStart) ||
                    !actors["older_sister"].QaReachedCell(sisterGoal)))
                yield return null;
            if (!actors["player"].QaReachedCell(sisterStart) ||
                !actors["older_sister"].QaReachedCell(sisterGoal))
            {
                var goals = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
                {
                    ["player"] = sisterStart,
                    ["older_sister"] = sisterGoal,
                    ["father"] = new OfficeGridCoordinate(2, 2),
                    ["mother"] = new OfficeGridCoordinate(10, 10)
                };
                FailPlayerQa(
                    49,
                    "narrow corridor deterministic yielding did not finish within 60 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | " + OccupancyMetricSummary());
                yield break;
            }
            if (!RequireZeroActualViolations("narrow-corridor", 50)) yield break;
            Debug.Log(
                "STARTER_OFFICE_NARROW_CORRIDOR_QA_PASS | duration=" + (Time.unscaledTime - started).ToString("F2") +
                " | " + RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());
        }

        private IEnumerator RunEightDirectionMovementQa()
        {
            _starterRuntime.ApplyLayoutForQa(CreateDirectionQaLayout());
            yield return WaitForRuntimeReady(51, "open direction layout");
            if (_playerQaFailure.Length > 0) yield break;
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(3, 3));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(21, 3));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(21, 21));
            _starterRuntime.World.Occupancy.ResetMetrics();
            for (var direction = 0; direction < QaDirectionVectors.Length; direction++)
            {
                player.QaTeleportToCell(new OfficeGridCoordinate(12, 12));
                player.QaSetPlayerInput(QaDirectionVectors[direction]);
                Vector2 observedDisplacement = Vector2.zero;
                Vector2 observedFrameDisplacement = Vector2.zero;
                Vector2 observedSemanticDisplacement = Vector2.zero;
                float observedSpeed = 0f;
                int observedSemanticDirection = player.SemanticDirection;
                int observedMotionDirection = player.MotionDirection;
                int observedVisualDirection = player.CurrentDirection;
                int observedWalkFrame = player.CurrentWalkFrame;
                float observedGaitDistance = player.GaitDistance;
                float observedGaitPhase = player.GaitPhase01;
                OfficeLocomotionPhase observedLocomotionPhase = player.LocomotionPhase;
                bool observedPivot = false;
                bool movedDuringPivot = false;
                bool observedProjection = false;
                string observedSprite = string.Empty;
                float started = Time.unscaledTime;
                var observationFrames = 0;
                // A cold Windows player can spend more than two scaled seconds in its first
                // rendered frame. The intentional planted pivot needs up to four presentation
                // frames, so retain a small real-time window and always sample at least eight
                // rendered frames before deciding that held input produced no movement.
                while (observationFrames < 8 || Time.unscaledTime - started < 0.5f)
                {
                    yield return null;
                    observationFrames++;
                    if (player.LocomotionPhase == OfficeLocomotionPhase.Pivot)
                    {
                        observedPivot = true;
                        movedDuringPivot |= player.LastActualDisplacement.sqrMagnitude > 0.0000000001f;
                    }
                    if (player.LastActualDisplacement.sqrMagnitude > observedDisplacement.sqrMagnitude)
                        observedDisplacement = player.LastActualDisplacement;
                    if (player.AccumulatedFrameDisplacement.sqrMagnitude <= 0.0000001f) continue;
                    observedFrameDisplacement = player.AccumulatedFrameDisplacement;
                    observedSemanticDisplacement = player.SemanticFrameDisplacement;
                    // Frame partitioning can leave the final sampled frame partially filled even
                    // while the held-input run reached cruise speed. Gate the run on its peak
                    // actual speed instead of whichever render frame happened to be last.
                    observedSpeed = Mathf.Max(observedSpeed, player.ActualPresentationSpeed);
                    observedSemanticDirection = player.SemanticDirection;
                    observedMotionDirection = player.MotionDirection;
                    observedVisualDirection = player.CurrentDirection;
                    observedWalkFrame = player.CurrentWalkFrame;
                    observedGaitDistance = player.GaitDistance;
                    observedGaitPhase = player.GaitPhase01;
                    observedLocomotionPhase = player.LocomotionPhase;
                    observedProjection = player.WasCollisionProjected;
                    observedSprite = player.CurrentSpriteName;
                }
                player.QaSetPlayerInput(Vector2.zero);
                yield return null;
                if (observedDisplacement.sqrMagnitude <= 0.0000001f)
                {
                    FailPlayerQa(
                        51,
                        $"player produced no displacement for {QaDirectionNames[direction]} " +
                        $"after {observationFrames} frames: phase={player.Phase} " +
                        $"locomotion={player.LocomotionPhase} direction={player.CurrentDirection} " +
                        $"desired={player.DesiredVelocity} blocker={player.LastMovementBlocker}");
                    yield break;
                }
                if (movedDuringPivot)
                {
                    FailPlayerQa(
                        52,
                        $"player translated during planted pivot for {QaDirectionNames[direction]}");
                    yield break;
                }
                int expected = DirectionalSpriteAnimator.ResolveTileDirection(observedDisplacement);
                int expectedWalkFrame = OfficeLocomotionGaitRules.DistanceFrame(
                    observedGaitDistance,
                    player.StrideLength,
                    6);
                float expectedGaitPhase = OfficeLocomotionGaitRules.Phase01(
                    observedGaitDistance,
                    player.StrideLength);
                if (expected != direction || player.CurrentDirection != direction ||
                    observedSemanticDirection != direction || observedMotionDirection != direction ||
                    observedVisualDirection != direction || observedProjection ||
                    observedSpeed < OfficeRuntimeAgent.DefaultMoveSpeed * 0.75f ||
                    observedWalkFrame != expectedWalkFrame ||
                    Mathf.Abs(Mathf.DeltaAngle(observedGaitPhase * 360f, expectedGaitPhase * 360f)) > 0.05f ||
                    (observedLocomotionPhase != OfficeLocomotionPhase.StartStep &&
                     observedLocomotionPhase != OfficeLocomotionPhase.Walk))
                {
                    FailPlayerQa(
                        52,
                        $"direction mismatch {QaDirectionNames[direction]}: vector={observedDisplacement} " +
                        $"frame={observedFrameDisplacement} semantic={observedSemanticDisplacement} " +
                        $"expected={direction} math={expected} semanticDir={observedSemanticDirection} " +
                        $"motionDir={observedMotionDirection} visualDir={observedVisualDirection} " +
                        $"projected={observedProjection} speed={observedSpeed:F3} " +
                        $"locomotion={observedLocomotionPhase} gaitDistance={observedGaitDistance:F3} " +
                        $"gaitPhase={observedGaitPhase:F4}/{expectedGaitPhase:F4} " +
                        $"walkFrame={observedWalkFrame}/{expectedWalkFrame}");
                    yield break;
                }
                Debug.Log(
                    $"STARTER_OFFICE_DIRECTION_SAMPLE_PASS | index={direction} name={QaDirectionNames[direction]} " +
                    $"stepDisplacement={observedDisplacement} frameDisplacement={observedFrameDisplacement} " +
                    $"semanticDisplacement={observedSemanticDisplacement} actualSpeed={observedSpeed:F3} " +
                    $"semanticDir={observedSemanticDirection} motionDir={observedMotionDirection} " +
                     $"visualDir={observedVisualDirection} projected={observedProjection} " +
                     $"pivotObserved={observedPivot} observationFrames={observationFrames} " +
                     $"locomotion={observedLocomotionPhase} gaitDistance={observedGaitDistance:F3} " +
                    $"gaitPhase={observedGaitPhase:F4} walkFrame={observedWalkFrame} " +
                    $"spriteAssetPath=Assets/Art/Characters/Player/Pixel/HighMotion/Frames/{observedSprite}.png");
            }
            if (!RequireZeroActualViolations("eight-direction-player", 53)) yield break;
            Debug.Log("STARTER_OFFICE_EIGHT_DIRECTION_QA_PASS | samples=8 | " + OccupancyMetricSummary());
        }

        private IEnumerator RunPlayerCollisionQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            var starts = new[]
            {
                new OfficeGridCoordinate(2, 5),
                new OfficeGridCoordinate(4, 2),
                new OfficeGridCoordinate(5, 6)
            };
            var targets = new[]
            {
                new OfficeGridCoordinate(2, 4),
                new OfficeGridCoordinate(4, 1),
                new OfficeGridCoordinate(7, 6)
            };
            var labels = new[] { "desk", "reception-counter", "npc" };
            for (var scenario = 0; scenario < labels.Length; scenario++)
            {
                player.QaTeleportToCell(starts[scenario]);
                actors["older_sister"].QaTeleportToCell(
                    scenario == 2 ? targets[scenario] : new OfficeGridCoordinate(9, 2));
                actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
                actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
                _starterRuntime.World.Occupancy.ResetMetrics();
                Vector3 startWorld = _starterRuntime.World.Presenter.CellCenterWorld(starts[scenario]);
                Vector3 targetWorld = _starterRuntime.World.Presenter.CellCenterWorld(targets[scenario]);
                player.QaSetPlayerInput(
                    new Vector2(targetWorld.x - startWorld.x, targetWorld.y - startWorld.y).normalized);
                float started = Time.time;
                Vector2 previous = player.Position;
                float maximumFrameDisplacement = 0f;
                float maximumFacingError = 0f;
                float minimumFacingDot = 1f;
                int reverseFacingFrames = 0;
                int projectedFrames = 0;
                while (Time.time - started < 10f)
                {
                    yield return null;
                    maximumFrameDisplacement = Mathf.Max(
                        maximumFrameDisplacement,
                        Vector2.Distance(previous, player.Position));
                    previous = player.Position;
                    if (player.AccumulatedFrameDisplacement.sqrMagnitude <= 0.0000000001f) continue;
                    if (player.WasCollisionProjected) projectedFrames++;
                    maximumFacingError = Mathf.Max(
                        maximumFacingError,
                        player.FacingAngularErrorDegrees);
                    minimumFacingDot = Mathf.Min(minimumFacingDot, player.FacingAlignmentDot);
                    int expectedDirection = player.MotionDirection;
                    int directionDelta = Mathf.Abs(player.CurrentDirection - expectedDirection);
                    directionDelta = Mathf.Min(directionDelta, DirectionalSpriteAnimator.DirectionCount - directionDelta);
                    // Every moving frame is strict: the displayed octant is the nearest octant of
                    // actual displacement. There is no semantic-facing grace window.
                    if (directionDelta != 0 ||
                        player.FacingAngularErrorDegrees >
                        OfficeSharedLocomotionRules.MaximumFacingErrorDegrees + 0.0001f ||
                        player.FacingAlignmentDot + 0.000001f <
                        OfficeSharedLocomotionRules.MinimumFacingAlignmentDot)
                    {
                        reverseFacingFrames++;
                        FailPlayerQa(
                            65 + scenario,
                            $"player {labels[scenario]} facing diverged: requestedDir={player.RequestedDirection} " +
                            $"motionDir={player.MotionDirection} visualDir={player.CurrentDirection} " +
                            $"usedRequested={player.UsedSemanticHeading} projected={player.WasCollisionProjected} " +
                            $"dot={player.FacingAlignmentDot:F6} " +
                            $"error={player.FacingAngularErrorDegrees:F4} reverseFrames={reverseFacingFrames}");
                        yield break;
                    }
                }
                player.QaSetPlayerInput(Vector2.zero);
                yield return null;
                if (!RequireZeroActualViolations("player-" + labels[scenario], 65 + scenario)) yield break;
                OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
                bool collisionWasExercised = scenario == 2
                    ? occupancy.BlockedAgentMoveCount > 0
                    : occupancy.BlockedStaticMoveCount > 0;
                if (!collisionWasExercised || maximumFrameDisplacement > 0.50f)
                {
                    FailPlayerQa(
                        65 + scenario,
                        $"player {labels[scenario]} collision was not safely exercised: " +
                        $"maxFrameDelta={maximumFrameDisplacement:F4} {OccupancyMetricSummary()}");
                    yield break;
                }
                Debug.Log(
                    $"STARTER_OFFICE_PLAYER_COLLISION_SAMPLE_PASS | target={labels[scenario]} " +
                    $"duration=10.00 timeScale={Time.timeScale:F1} maxFrameDelta={maximumFrameDisplacement:F4} " +
                    $"projectedFrames={projectedFrames} reverseFacingFrames={reverseFacingFrames} " +
                    $"strictFacingErrorMax={maximumFacingError:F4} " +
                    $"strictFacingDotMin={minimumFacingDot:F6} replans=0 arrivals=0 | " +
                    OccupancyMetricSummary());
            }
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            Debug.Log("STARTER_OFFICE_PLAYER_COLLISION_QA_PASS | scenarios=3 | timeScale=4");
            yield return null;
        }

        private IEnumerator RunReversalPivotQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            player.QaTeleportToCell(new OfficeGridCoordinate(12, 12));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(3, 3));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(21, 3));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(21, 21));

            player.QaSetPlayerInput(Vector2.down);
            float started = Time.time;
            bool establishedSouth = false;
            while (Time.time - started < 2f)
            {
                yield return null;
                Vector2 displacement = player.AccumulatedFrameDisplacement;
                if (displacement.sqrMagnitude <= 0.0000001f) continue;
                int motionDirection = DirectionalSpriteAnimator.ResolveTileDirection(displacement);
                if (motionDirection == 0 && player.CurrentDirection == 0)
                {
                    establishedSouth = true;
                    break;
                }
            }
            if (!establishedSouth)
            {
                player.QaSetPlayerInput(Vector2.zero);
                FailPlayerQa(54, "reversal QA could not establish southward walking");
                yield break;
            }

            player.QaSetPlayerInput(Vector2.up);
            bool plantedNorthFacingObserved = false;
            bool resumedNorth = false;
            int movingFrames = 0;
            started = Time.time;
            while (Time.time - started < 3f)
            {
                yield return null;
                Vector2 displacement = player.AccumulatedFrameDisplacement;
                if (displacement.sqrMagnitude <= 0.0000001f)
                {
                    if (player.CurrentDirection == 4) plantedNorthFacingObserved = true;
                    continue;
                }

                movingFrames++;
                int actualDirection = DirectionalSpriteAnimator.ResolveTileDirection(displacement);
                if (player.CurrentDirection != actualDirection)
                {
                    player.QaSetPlayerInput(Vector2.zero);
                    FailPlayerQa(
                        54,
                        $"reversal rendered a non-motion facing: actual={actualDirection} " +
                        $"visual={player.CurrentDirection} displacement={displacement} " +
                        $"locomotion={player.LocomotionPhase}");
                    yield break;
                }
                if (actualDirection != 4) continue;
                if (!plantedNorthFacingObserved)
                {
                    player.QaSetPlayerInput(Vector2.zero);
                    FailPlayerQa(54, "reverse acceleration started before a planted north-facing pivot");
                    yield break;
                }
                resumedNorth = true;
                break;
            }
            player.QaSetPlayerInput(Vector2.zero);
            yield return null;
            if (!plantedNorthFacingObserved || !resumedNorth)
            {
                FailPlayerQa(
                    54,
                    $"reversal did not complete stop-pivot-resume: planted={plantedNorthFacingObserved} " +
                    $"resumed={resumedNorth} movingFrames={movingFrames} direction={player.CurrentDirection} " +
                    $"locomotion={player.LocomotionPhase}");
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_REVERSAL_PIVOT_QA_PASS | southWalk=true plantedNorthPivot=true " +
                $"northResume=true movingFrames={movingFrames}");
        }

        private IEnumerator RunMicroActionDestinationQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            var locations = new[]
            {
                OfficeSemanticLocation.Filing,
                OfficeSemanticLocation.Printer,
                OfficeSemanticLocation.Water,
                OfficeSemanticLocation.Coffee,
                OfficeSemanticLocation.OpenArea
            };
            var playerStart = new OfficeGridCoordinate(5, 6);
            player.QaTeleportToCell(playerStart);
            ParkQaActorsOutsideMicroDestinationApproaches(actors, "player", playerStart);
            foreach (OfficeSemanticLocation location in locations)
            {
                string scenarioId = "micro-destination-" + location;
                // Clear the preceding interaction before resolving the next live offer. The other
                // family actors remain on cells that are outside every tested facility approach,
                // so the assertion measures the resolver rather than the QA parking layout.
                player.QaTeleportToCell(playerStart);
                if (!_starterRuntime.World.Workstations.TryResolveDestination(
                        location,
                        "player",
                        scenarioId,
                        out OfficeRuntimeDestination expectedDestination))
                {
                    FailPlayerQa(72, "micro-action destination could not be resolved: " + location);
                    yield break;
                }

                // Teleports intentionally bypass traversal. Start the measurement only after every
                // actor is parked on a radius-clear cell so setup cannot pollute collision metrics.
                _starterRuntime.World.Occupancy.ResetMetrics();
                if (!player.QaBeginSemanticLocation(
                        location,
                        scenarioId,
                        out OfficeGridCoordinate destination))
                {
                    FailPlayerQa(72, "micro-action destination could not be resolved: " + location);
                    yield break;
                }
                if (!destination.Equals(expectedDestination.Cell))
                {
                    FailPlayerQa(
                        72,
                        $"micro-action destination changed during deterministic resolution: {location} " +
                        $"expected={expectedDestination.Cell} actual={destination}");
                    yield break;
                }

                float started = Time.time;
                while (Time.time - started < 20f && !player.QaReachedCell(destination))
                    yield return null;
                if (!player.QaReachedCell(destination))
                {
                    FailPlayerQa(
                        73,
                        $"micro-action destination was unreachable: {location} target={destination} " +
                        $"position={player.Position} phase={player.Phase} stuck={player.StuckSeconds:F2} | " +
                        OccupancyMetricSummary());
                    yield break;
                }
                if (!RequireZeroActualViolations("micro-destination-" + location, 74)) yield break;
                Debug.Log(
                    $"STARTER_OFFICE_MICRO_DESTINATION_SAMPLE_PASS | location={location} " +
                    $"cell={destination} | {OccupancyMetricSummary()}");
            }
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            Debug.Log(
                "STARTER_OFFICE_MICRO_DESTINATION_QA_PASS | " +
                "locations=Filing,Printer,Water,Coffee,OpenArea unreachable=0");
            yield return null;
        }

        private void ParkQaActorsOutsideMicroDestinationApproaches(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            string activeMemberId,
            OfficeGridCoordinate activeStart)
        {
            OfficeRuntimeWorld world = _starterRuntime.World;
            var targetKinds = new HashSet<string>(StringComparer.Ordinal)
            {
                OfficeGridLayouts.FilingCabinetKind,
                OfficeGridLayouts.FaxCopierKind,
                OfficeGridLayouts.WaterDispenserKind,
                OfficeGridLayouts.CoffeeTableKind
            };
            var excluded = new HashSet<OfficeGridCoordinate>();
            OfficeGridCoordinate[] approachOffsets =
            {
                new OfficeGridCoordinate(1, 0),
                new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1),
                new OfficeGridCoordinate(0, -1),
                new OfficeGridCoordinate(2, 0),
                new OfficeGridCoordinate(-2, 0),
                new OfficeGridCoordinate(0, 2),
                new OfficeGridCoordinate(0, -2)
            };
            foreach (PlacedOfficeFurniture furniture in world.Grid.Furniture.Where(item =>
                         targetKinds.Contains(item.KindId)))
            {
                for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
                for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
                foreach (OfficeGridCoordinate offset in approachOffsets)
                    excluded.Add(new OfficeGridCoordinate(x + offset.X, y + offset.Y));
            }

            Vector2 activeWorld = world.Presenter.CellCenterWorld(activeStart);
            var reserved = new List<Vector2> { activeWorld };
            List<OfficeGridCoordinate> parkingCells = Enumerable.Range(1, world.Grid.Height - 2)
                .SelectMany(y => Enumerable.Range(1, world.Grid.Width - 2)
                    .Select(x => new OfficeGridCoordinate(x, y)))
                .Where(cell => world.Grid.IsWalkable(cell) && !excluded.Contains(cell))
                .Where(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return world.Occupancy.CanTraverseStatic(
                        center,
                        center,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty);
                })
                .OrderByDescending(cell =>
                    Vector2.SqrMagnitude((Vector2)world.Presenter.CellCenterWorld(cell) - activeWorld))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();

            foreach (KeyValuePair<string, OfficeRuntimeAgent> item in actors
                         .Where(item => !string.Equals(item.Key, activeMemberId, StringComparison.Ordinal))
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                OfficeGridCoordinate parkingCell = parkingCells.First(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return reserved.All(position => Vector2.Distance(position, center) >= 1f);
                });
                item.Value.QaTeleportToCell(parkingCell);
                reserved.Add(world.Presenter.CellCenterWorld(parkingCell));
            }
        }

        private void ParkQaActorsAwayFrom(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            string activeMemberId,
            OfficeGridCoordinate activeStart,
            OfficeGridCoordinate destination)
        {
            OfficeRuntimeWorld world = _starterRuntime.World;
            Vector2 destinationWorld = world.Presenter.CellCenterWorld(destination);
            var reserved = new List<Vector2>
            {
                world.Presenter.CellCenterWorld(activeStart)
            };
            List<OfficeGridCoordinate> parkingCells = Enumerable.Range(1, world.Grid.Height - 2)
                .SelectMany(y => Enumerable.Range(1, world.Grid.Width - 2)
                    .Select(x => new OfficeGridCoordinate(x, y)))
                .Where(cell => world.Occupancy.IsCellPassable(cell, string.Empty, string.Empty, false))
                .Where(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return world.Occupancy.CanTraverseStatic(
                        center,
                        center,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty);
                })
                .OrderByDescending(cell =>
                    Vector2.SqrMagnitude((Vector2)world.Presenter.CellCenterWorld(cell) - destinationWorld))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();

            foreach (KeyValuePair<string, OfficeRuntimeAgent> item in actors
                         .Where(item => !string.Equals(item.Key, activeMemberId, StringComparison.Ordinal))
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                OfficeGridCoordinate parkingCell = parkingCells.First(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return Vector2.Distance(center, destinationWorld) >= 1.5f &&
                           reserved.All(position => Vector2.Distance(position, center) >= 1.0f);
                });
                item.Value.QaTeleportToCell(parkingCell);
                reserved.Add(world.Presenter.CellCenterWorld(parkingCell));
            }
        }

        private IEnumerator RunFourSeatWorkQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            var initiallySeated = QaMemberIds.ToDictionary(
                memberId => memberId,
                memberId => actors[memberId].IsSeated,
                StringComparer.Ordinal);
            foreach (string memberId in QaMemberIds)
            {
                actors[memberId].BeginQaControl();
                if (!actors[memberId].IsSeated ||
                    actors[memberId].QaRequestStandWithOutwardRoute()) continue;
                FailPlayerQa(54, "could not establish a standing pre-dock state for " + memberId);
                yield break;
            }
            float resetStarted = Time.time;
            while (Time.time - resetStarted < 12f && QaMemberIds.Any(memberId =>
                       initiallySeated[memberId] &&
                       (actors[memberId].IsOccupyingSeat ||
                        !TryObserveClassicFirstWalk(actors[memberId], out _))))
                yield return null;
            if (QaMemberIds.Any(memberId =>
                    initiallySeated[memberId] &&
                    (actors[memberId].IsOccupyingSeat ||
                     !TryObserveClassicFirstWalk(actors[memberId], out int direction) ||
                     direction != actors[memberId].R5eLastAtomicExitDirection)))
            {
                FailPlayerQa(54, "could not complete the standing pre-dock reset for all actors");
                yield break;
            }

            var entryTicksBeforeDock = QaMemberIds.ToDictionary(
                memberId => memberId,
                memberId => actors[memberId].R5eAtomicPlacementTick,
                StringComparer.Ordinal);
            var preDockTicks = QaMemberIds.ToDictionary(
                memberId => memberId,
                _ => 0UL,
                StringComparer.Ordinal);
            foreach (string memberId in QaMemberIds)
            {
                if (actors[memberId].QaBeginSeatedWork("four-seat-work")) continue;
                FailPlayerQa(54, "seat work route could not be created for " + memberId);
                yield break;
            }
            float started = Time.time;
            while (Time.time - started < 45f && QaMemberIds.Any(memberId =>
                       !actors[memberId].IsSeated ||
                       actors[memberId].R5eAtomicPlacementTick <= entryTicksBeforeDock[memberId]))
            {
                foreach (string memberId in QaMemberIds)
                {
                    OfficeRuntimeAgent actor = actors[memberId];
                    if (actor.Phase == OfficeRuntimeAgentPhase.SittingDown ||
                        actor.Phase == OfficeRuntimeAgentPhase.StandingUp ||
                        actor.CurrentSeatingClip == OfficeSeatingAnimationClip.SitDown ||
                        actor.CurrentSeatingClip == OfficeSeatingAnimationClip.StandUp ||
                        actor.ObservedSitDownFrameCount != 0 ||
                        actor.ObservedStandUpFrameCount != 0 ||
                        IsClassicTransitionSprite(actor.CurrentSpriteName))
                    {
                        FailPlayerQa(55, "classic atomic dock rendered a forbidden transition for " +
                                         memberId);
                        yield break;
                    }
                    if (preDockTicks[memberId] == 0 &&
                        actor.Phase == OfficeRuntimeAgentPhase.RotatingToSeat &&
                        actor.IsSeatEntryPresentationPlanted &&
                        !actor.CurrentSeatingClip.HasValue &&
                        actor.R5eCurrentVelocityMagnitude <= 0.0001f &&
                        actor.R5eLastActualDisplacementMagnitude <= 0.0001f &&
                        actor.VisibleFrameMovementWorld <= 0.0001f)
                        preDockTicks[memberId] = actor.R5eRuntimeTick;
                }
                yield return null;
            }
            if (QaMemberIds.Any(memberId =>
                    !actors[memberId].IsSeated ||
                    preDockTicks[memberId] == 0 ||
                    actors[memberId].R5eAtomicPlacementTick <= preDockTicks[memberId] ||
                    actors[memberId].R5eAtomicPlacementTick <= entryTicksBeforeDock[memberId] ||
                    actors[memberId].CurrentSeatingClip != OfficeSeatingAnimationClip.Work ||
                    actors[memberId].ObservedSitDownFrameCount != 0 ||
                    actors[memberId].ObservedStandUpFrameCount != 0))
            {
                var goals = QaMemberIds.ToDictionary(
                    memberId => memberId,
                    memberId => _starterRuntime.World.Occupancy.CurrentCell(memberId),
                    StringComparer.Ordinal);
                FailPlayerQa(
                    55,
                    "all four assigned workstations did not perform one planted-to-seated atomic dock " +
                    "within 45 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | seats=" +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        memberId + ":" + actors[memberId].ActiveSeatId)) + " | " +
                    OccupancyMetricSummary());
                yield break;
            }
            // Route-to-seat is already covered by arrival/penetration checks elsewhere. Reset here
            // so the presentation assertion measures the occupied typing pose itself, not a stale
            // approach-frame sample from before the permitted seat claim became active.
            _starterRuntime.World.Occupancy.ResetMetrics();
            float workLoopStarted = Time.time;
            while (Time.time - workLoopStarted < 8f &&
                   QaMemberIds.Any(memberId =>
                       !HasObservedWorkPresentation(actors[memberId]) ||
                       actors[memberId].TypingContactSampleCount == 0))
                yield return null;
            if (QaMemberIds.Any(memberId => actors[memberId].ObservedSitDownFrameCount != 0 ||
                                                actors[memberId].CurrentSeatingClip !=
                                                OfficeSeatingAnimationClip.Work ||
                                                !HasObservedWorkPresentation(actors[memberId]) ||
                                                actors[memberId].TypingContactSampleCount == 0))
            {
                FailPlayerQa(
                    56,
                    "classic atomic docking rendered a transition clip or missed Work: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=sit{actors[memberId].ObservedSitDownFrameCount}/" +
                        $"clip{actors[memberId].CurrentSeatingClip}/" +
                        $"work{actors[memberId].ObservedWorkFrameCount}/" +
                        $"hook{actors[memberId].IsOfficeWorkAnimationHookActive}:" +
                        actors[memberId].ObservedOfficeWorkHookSpriteCount)));
                yield break;
            }
            string[] claims = QaMemberIds.Select(memberId => actors[memberId].ActiveSeatId).ToArray();
            if (claims.Any(string.IsNullOrWhiteSpace) || claims.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                FailPlayerQa(56, "seat claims are missing or duplicated: " + string.Join(",", claims));
                yield break;
            }
            foreach (string memberId in QaMemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
                string expectedSpritePrefix = memberId + "_northwest_sit_work_";
                int actualOrder = actor.PresentationRenderer == null
                    ? int.MinValue
                    : actor.PresentationRenderer.sortingOrder;
                int chairOrder = int.MaxValue;
                int deskOrder = int.MaxValue;
                if (_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.ChairFurnitureId, out SpriteRenderer chairRenderer))
                    chairOrder = chairRenderer.sortingOrder;
                if (seat.HasWorkstationBinding &&
                    _starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.WorkSurfaceFurnitureId, out SpriteRenderer deskRenderer))
                    deskOrder = deskRenderer.sortingOrder;
                bool depthCorrect = actualOrder > chairOrder && actualOrder > deskOrder;
                Debug.Log(
                    $"STARTER_OFFICE_WORKSTATION_ALIGNMENT_SAMPLE | member={memberId} " +
                    $"seatContact={actor.SeatContactErrorPx:F3}px chairDesk={actor.ChairDeskErrorPx:F3}px " +
                    $"chairDeskDelta=({actor.ChairDeskDeltaPx.x:F3},{actor.ChairDeskDeltaPx.y:F3})px " +
                    $"handWork={actor.HandWorkErrorPx:F3}px typingContact=" +
                    $"{actor.TypingContactSampleCount}:seat{actor.MaxTypingSeatContactErrorPx:F3}px/" +
                    $"hand{actor.MaxTypingHandWorkErrorPx:F3}px " +
                    $"rotation={actor.VisualRotationErrorDegrees:F4}deg " +
                    $"scaleDeviation={actor.VisualScaleDeviation:P3} direction={actor.CurrentDirection} " +
                    $"sprite={actor.CurrentSpriteName} mode={actor.SeatingPresentationMode} " +
                    $"classic=sit{actor.ObservedSitDownFrameCount}/stand" +
                    $"{actor.ObservedStandUpFrameCount} work={actor.ObservedWorkFrameCount}/6 " +
                    $"workHook={actor.IsOfficeWorkAnimationHookActive}:" +
                    $"{actor.ObservedOfficeWorkHookSpriteCount} " +
                    $"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px " +
                    $"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px " +
                    $"monotonicViolations={actor.TransitionMonotonicViolationCount} " +
                    $"sorting={actualOrder} chair={chairOrder} desk={deskOrder}");
                // A typing chair may be visually pulled out under its occupant. The authored
                // semantic socket remains exact while flat pixel characters keep scale/rotation 1/0.
                // The chair is presentation-followed to keep the authored hands planted. Its
                // semantic seat remains exact; taller/shorter bodies may pull the rendered chair
                // by up to one tenth of a 160px tile without leaving the workstation footprint.
                bool presentationMatches = actor.ChairDeskErrorPx <= 16f &&
                    actor.SeatContactErrorPx <= 1f &&
                    actor.TypingContactSampleCount > 0 &&
                    actor.MaxTypingSeatContactErrorPx <= 6f &&
                    actor.MaxTypingHandWorkErrorPx <= 4f &&
                    actor.VisualRotationErrorDegrees <= 0.01f &&
                    actor.VisualScaleDeviation <= 0.001f && actor.CurrentDirection == 3 &&
                    actor.SeatingPresentationMode == OfficeSeatingPresentationMode.Animated &&
                    actor.ObservedSitDownFrameCount == 0 &&
                    actor.ObservedStandUpFrameCount == 0 &&
                    actor.CurrentSeatingClip == OfficeSeatingAnimationClip.Work &&
                    actor.R5eAtomicPlacementTick != 0 &&
                    actor.WasSeatFacingAlignedBeforeSitDown &&
                    HasObservedWorkPresentation(actor) &&
                    actor.MaxAnimatedAnchorErrorPx <= 1f &&
                    actor.MaxTransitionPelvisStepPx <= 0.001f &&
                    actor.TransitionMonotonicViolationCount == 0 &&
                    (actor.IsOfficeWorkAnimationHookActive
                        ? IsOfficeWorkActionSprite(memberId, actor.CurrentSpriteName)
                        : actor.CurrentSpriteName.StartsWith(expectedSpritePrefix, StringComparison.Ordinal)) &&
                    depthCorrect;
                if (presentationMatches) continue;
                FailPlayerQa(
                    57,
                    $"seated contact placement failed for {memberId}: " +
                    $"seatContact={actor.SeatContactErrorPx:F2}px handWork={actor.HandWorkErrorPx:F2}px " +
                        $"chairDesk={actor.ChairDeskErrorPx:F2}px typingContact=" +
                        $"{actor.TypingContactSampleCount}:seat{actor.MaxTypingSeatContactErrorPx:F2}px/" +
                        $"hand{actor.MaxTypingHandWorkErrorPx:F2}px " +
                        $"rotation={actor.VisualRotationErrorDegrees:F4}deg " +
                        $"scaleDeviation={actor.VisualScaleDeviation:P3} direction={actor.CurrentDirection} " +
                        $"sprite={actor.CurrentSpriteName} mode={actor.SeatingPresentationMode} " +
                        $"classic=sit{actor.ObservedSitDownFrameCount}/stand" +
                        $"{actor.ObservedStandUpFrameCount} work={actor.ObservedWorkFrameCount}/6 " +
                        $"workHook={actor.IsOfficeWorkAnimationHookActive}:" +
                        $"{actor.ObservedOfficeWorkHookSpriteCount} " +
                        $"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px " +
                        $"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px " +
                        $"monotonicViolations={actor.TransitionMonotonicViolationCount} " +
                        $"sorting={actualOrder} chair={chairOrder} desk={deskOrder}");
                yield break;
            }
            string capturePath = QaArtifactPath("starter-office-four-seat-work.png");
            if (!TryCaptureQaCameraFrame(capturePath, out string captureFailure))
            {
                FailPlayerQa(58, "four-seat visual capture failed: " + captureFailure);
                yield break;
            }
            Debug.Log("STARTER_OFFICE_FOUR_SEAT_CAPTURE | path=" + capturePath);
            foreach (string memberId in QaMemberIds)
            {
                if (TryCaptureQaWorkstationCloseup(memberId, actors[memberId], out string closeupPath,
                        out string closeupFailure))
                {
                    Debug.Log($"SEATED_SPRITE_ROOT_CAUSE_V3_CLOSEUP | member={memberId} path={closeupPath}");
                    continue;
                }
                FailPlayerQa(58, $"{memberId} workstation close-up failed: {closeupFailure}");
                yield break;
            }
            foreach (string memberId in QaMemberIds)
            {
                if (TryCaptureQaChairOverlayComparison(
                        memberId,
                        actors[memberId],
                        out string overlayOnPath,
                        out string overlayOffPath,
                        out string overlayFailure))
                {
                    Debug.Log(
                        $"STARTER_OFFICE_CHAIR_OVERLAY_COMPARISON | member={memberId} " +
                        $"on={overlayOnPath} off={overlayOffPath}");
                    continue;
                }
                FailPlayerQa(58, $"{memberId} chair overlay comparison failed: {overlayFailure}");
                yield break;
            }
            if (!RequireZeroActualViolations("four-seat-work", 58)) yield break;
            var entryAtomicTicks = QaMemberIds.ToDictionary(
                memberId => memberId,
                memberId => actors[memberId].R5eAtomicPlacementTick,
                StringComparer.Ordinal);
            var firstWalkObserved = QaMemberIds.ToDictionary(
                memberId => memberId,
                _ => false,
                StringComparer.Ordinal);
            var firstWalkTicks = QaMemberIds.ToDictionary(
                memberId => memberId,
                _ => 0UL,
                StringComparer.Ordinal);
            var firstWalkDirections = QaMemberIds.ToDictionary(
                memberId => memberId,
                _ => -1,
                StringComparer.Ordinal);
            foreach (string memberId in QaMemberIds)
            {
                if (actors[memberId].QaRequestStandWithOutwardRoute()) continue;
                FailPlayerQa(58, "classic atomic exit could not begin for " + memberId);
                yield break;
            }
            float standStarted = Time.time;
            while (Time.time - standStarted < 12f && firstWalkObserved.Values.Any(value => !value))
            {
                foreach (string memberId in QaMemberIds)
                {
                    if (firstWalkObserved[memberId] ||
                        !TryObserveClassicFirstWalk(actors[memberId], out int direction)) continue;
                    firstWalkObserved[memberId] = true;
                    firstWalkTicks[memberId] = actors[memberId].R5eRuntimeTick;
                    firstWalkDirections[memberId] = direction;
                }
                yield return null;
            }
            if (QaMemberIds.Any(memberId =>
                    actors[memberId].ObservedSitDownFrameCount != 0 ||
                    actors[memberId].ObservedStandUpFrameCount != 0 ||
                    !actors[memberId].R5eLastAtomicExitReservationBacked ||
                    !actors[memberId].HasCompletedSeatEgress ||
                    actors[memberId].R5eLastAtomicExitTick <= entryAtomicTicks[memberId] ||
                    actors[memberId].R5eTurnCompleteTick <=
                    actors[memberId].R5eLastAtomicExitTick ||
                    !firstWalkObserved[memberId] ||
                    firstWalkTicks[memberId] <= actors[memberId].R5eTurnCompleteTick ||
                    firstWalkDirections[memberId] !=
                    actors[memberId].R5eLastAtomicExitDirection))
            {
                FailPlayerQa(
                    58,
                    "classic atomic exit/reservation/first-walk contract failed: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=clips{actors[memberId].ObservedSitDownFrameCount}/" +
                        $"{actors[memberId].ObservedStandUpFrameCount} reserved=" +
                        $"{actors[memberId].R5eLastAtomicExitReservationBacked} " +
                        $"ticks={entryAtomicTicks[memberId]}/" +
                        $"{actors[memberId].R5eLastAtomicExitTick}/" +
                        $"{actors[memberId].R5eTurnCompleteTick}/" +
                        $"{firstWalkTicks[memberId]} direction=" +
                        $"{actors[memberId].R5eLastAtomicExitDirection}/" +
                        firstWalkDirections[memberId])));
                yield break;
            }
            if (QaMemberIds.Any(memberId =>
                    actors[memberId].MaxTransitionPelvisStepPx > 0.001f ||
                    actors[memberId].TransitionMonotonicViolationCount != 0))
            {
                FailPlayerQa(
                    58,
                    "classic atomic path rendered intermediate pelvis motion: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=maxStep{actors[memberId].MaxTransitionPelvisStepPx:F3}px/" +
                        $"reverse{actors[memberId].TransitionMonotonicViolationCount}")));
                yield break;
            }
            string atomicExitCapture = QaArtifactPath("starter-office-four-seat-atomic-exit.png");
            if (!TryCaptureQaCameraFrame(atomicExitCapture, out string atomicExitCaptureFailure))
            {
                FailPlayerQa(58, "classic atomic exit capture failed: " + atomicExitCaptureFailure);
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_FOUR_SEAT_WORK_QA_PASS | seats=" + string.Join(",", claims) +
                " | classicAtomic=4x(Dock-Face-AtomicWork+ReservedAtomicExit-Turn-LaterFirstWalk) " +
                "transitionClips=0 intermediatePelvisMotion=0 anchorError<=1px " +
                "typingSeat<=6px,typingHand<=4px,chairPullout<=16px," +
                "rotation=0,scale=canonical,sorting=chairFloor+1 | " +
                OccupancyMetricSummary());
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            yield return null;
        }

        private static bool HasObservedWorkPresentation(OfficeRuntimeAgent actor)
        {
            return actor != null && (actor.IsOfficeWorkAnimationHookActive
                ? actor.ObservedOfficeWorkHookSpriteCount >= 6
                : actor.ObservedWorkFrameCount >= 6);
        }

        private static bool IsOfficeWorkActionSprite(string memberId, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(spriteName)) return false;
            return spriteName.StartsWith(memberId + "_typing_", StringComparison.Ordinal) ||
                   spriteName.StartsWith(memberId + "_mouse_", StringComparison.Ordinal) ||
                   spriteName.StartsWith(memberId + "_drink_", StringComparison.Ordinal);
        }

        private static bool IsClassicTransitionSprite(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return false;
            return spriteName.IndexOf("sit_down", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("sitdown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("stand_up", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("standup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryObserveClassicFirstWalk(
            OfficeRuntimeAgent actor,
            out int direction)
        {
            direction = -1;
            if (actor == null || !actor.HasCompletedSeatEgress ||
                actor.R5eTurnCompleteTick <= actor.R5eLastAtomicExitTick ||
                actor.R5eRuntimeTick <= actor.R5eTurnCompleteTick) return false;
            Vector2 displacement = actor.Position - actor.LastCompletedSeatEgressWorld;
            if (displacement.magnitude <= OfficeRuntimeTraceCoordinator.StationaryEpsilon)
                return false;
            direction = DirectionalSpriteAnimator.ResolveTileDirection(
                displacement,
                actor.R5eLastAtomicExitDirection);
            return true;
        }

        private static string QaArtifactPath(string fileName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                string directory = Path.GetDirectoryName(Path.GetFullPath(arguments[index + 1]));
                if (!string.IsNullOrWhiteSpace(directory)) return Path.Combine(directory, fileName);
            }
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private IEnumerator CaptureOfficeHudQa()
        {
            string path = QaArtifactPath("starter-office-modern-hud.png");
            yield return new WaitForEndOfFrame();
            if (!TryCaptureQaCameraFrame(path, 1392, 699, true, out string failure))
            {
                FailPlayerQa(34, "office HUD capture failed: " + failure);
                yield break;
            }
            Debug.Log(
                $"STARTER_OFFICE_MODERN_HUD_QA_PASS | resolution=1392x699 " +
                $"bytes={new FileInfo(path).Length} capture={path}");
        }

        private IEnumerator RunRealtimeAutonomyClockQa(PrototypeBootstrap bootstrap)
        {
            FamilyMemberState sister = bootstrap.State.Family.Get("older_sister");
            long startedMinute = bootstrap.State.Time.ElapsedMinutes;
            long initialSequence = sister.Autonomy.MicroAction.SequenceIndex;
            OfficeMicroAction initialAction = sister.Autonomy.MicroAction.Action;
            long initialEndsMinute = sister.Autonomy.MicroAction.EndsMinute;
            long requiredMinute = Math.Max(startedMinute + 1L, initialEndsMinute + 1L);
            float deadline = Time.unscaledTime + 7f;
            while (bootstrap.State.Time.ElapsedMinutes < requiredMinute && Time.unscaledTime < deadline)
                yield return null;

            long actualMinute = bootstrap.State.Time.ElapsedMinutes;
            long actualSequence = sister.Autonomy.MicroAction.SequenceIndex;
            bool actionLifecycleAdvanced = initialAction == OfficeMicroAction.None ||
                                           initialEndsMinute <= startedMinute ||
                                           actualSequence > initialSequence;
            if (actualMinute <= startedMinute || !actionLifecycleAdvanced)
            {
                FailPlayerQa(
                    35,
                    $"realtime autonomy clock did not advance: minute={startedMinute}->{actualMinute} " +
                    $"sister={initialAction}/{initialSequence}->{sister.Autonomy.MicroAction.Action}/{actualSequence} " +
                    $"initialEnds={initialEndsMinute}");
                yield break;
            }
            Debug.Log(
                $"STARTER_OFFICE_REALTIME_AUTONOMY_CLOCK_QA_PASS | " +
                $"minute={startedMinute}->{actualMinute} sister=" +
                $"{initialAction}/{initialSequence}->{sister.Autonomy.MicroAction.Action}/{actualSequence} " +
                $"initialEnds={initialEndsMinute}");
        }

        private string RouteMetricSummary(int replansBefore, int arrivalsBefore)
        {
            return $"replans={_starterRuntime.World.ReplanCount - replansBefore} " +
                   $"arrivals={_starterRuntime.World.ArrivalCount - arrivalsBefore}";
        }

        private static bool TryCaptureQaCameraFrame(string path, out string failure)
        {
            return TryCaptureQaCameraFrame(path, 1392, 699, out failure);
        }

        private bool TryCaptureStarterOfficeFrame(
            string path,
            int width,
            int height,
            out string failure)
        {
            if (_starterRuntime == null || _starterRuntime.World == null ||
                _starterRuntime.World.Presenter == null ||
                _starterRuntime.World.FurniturePresenter == null)
            {
                failure = "Starter office world is unavailable for capture";
                return false;
            }
            Bounds bounds = _starterRuntime.World.Presenter.FloorRenderer.bounds;
            bounds.Encapsulate(_starterRuntime.World.FurniturePresenter.RenderBounds);
            return TryCaptureQaCameraFrame(path, width, height, false, bounds, out failure);
        }

        private bool TryCaptureQaWorkstationCloseup(
            string memberId,
            OfficeRuntimeAgent actor,
            out string path,
            out string failure)
        {
            path = QaArtifactPath(memberId.Replace('_', '-') + "-work-closeup.png");
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null)
            {
                failure = "Camera.main is missing";
                return false;
            }
            if (actor == null || actor.PresentationRenderer == null)
            {
                failure = "actor presentation renderer is missing";
                return false;
            }

            OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            Bounds bounds = actor.PresentationRenderer.bounds;
            EncapsulateFurnitureRenderers(seat.ChairFurnitureId, ref bounds);
            if (seat.HasWorkstationBinding) EncapsulateFurnitureRenderers(seat.WorkSurfaceFurnitureId, ref bounds);

            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            float previousSize = camera.orthographicSize;
            try
            {
                CenterCameraOnWorldPoint(camera, bounds.center);
                camera.orthographicSize = Mathf.Max(1.1f, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f);
                return TryCaptureQaCameraFrame(path, 1024, 1024, out failure);
            }
            finally
            {
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.orthographicSize = previousSize;
            }
        }

        private bool TryCaptureQaChairOverlayComparison(
            string memberId,
            OfficeRuntimeAgent actor,
            out string overlayOnPath,
            out string overlayOffPath,
            out string failure)
        {
            string stem = memberId.Replace('_', '-');
            overlayOnPath = QaArtifactPath(stem + "-chair-overlay-on.png");
            overlayOffPath = QaArtifactPath(stem + "-chair-overlay-off.png");
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null || actor == null || actor.PresentationRenderer == null)
            {
                failure = "camera or actor presentation renderer is missing";
                return false;
            }

            OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            if (!_starterRuntime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    seat.ChairFurnitureId,
                    out SpriteRenderer chairOverlay) || chairOverlay == null)
            {
                // A complete single-sprite chair has no optional layer to compare. Its visibility
                // and ordering are already asserted by the base renderer and workstation capture.
                overlayOnPath = string.Empty;
                overlayOffPath = string.Empty;
                return true;
            }

            bool previousOverlayEnabled = chairOverlay.enabled;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            float previousSize = camera.orthographicSize;
            try
            {
                chairOverlay.enabled = true;
                Bounds bounds = actor.PresentationRenderer.bounds;
                EncapsulateFurnitureRenderers(seat.ChairFurnitureId, ref bounds);
                if (seat.HasWorkstationBinding)
                    EncapsulateFurnitureRenderers(seat.WorkSurfaceFurnitureId, ref bounds);
                CenterCameraOnWorldPoint(camera, bounds.center);
                camera.orthographicSize = Mathf.Max(
                    1.1f,
                    Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f);
                if (!TryCaptureQaCameraFrame(overlayOnPath, 1024, 1024, out failure)) return false;
                chairOverlay.enabled = false;
                return TryCaptureQaCameraFrame(overlayOffPath, 1024, 1024, out failure);
            }
            finally
            {
                chairOverlay.enabled = previousOverlayEnabled;
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.orthographicSize = previousSize;
            }
        }

        private void EncapsulateFurnitureRenderers(string furnitureId, ref Bounds bounds)
        {
            if (!_starterRuntime.World.FurniturePresenter.TryGetSemanticRoot(furnitureId, out Transform root)) return;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
            }
        }

        private static bool TryCaptureQaCameraFrame(
            string path,
            int width,
            int height,
            out string failure)
        {
            return TryCaptureQaCameraFrame(path, width, height, false, null, out failure);
        }

        private static bool TryCaptureQaCameraFrame(
            string path,
            int width,
            int height,
            bool includeOfficeHud,
            out string failure)
        {
            return TryCaptureQaCameraFrame(path, width, height, includeOfficeHud, null, out failure);
        }

        private static bool TryCaptureQaCameraFrame(
            string path,
            int width,
            int height,
            bool includeOfficeHud,
            Bounds? fitBounds,
            out string failure)
        {
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null)
            {
                failure = "Camera.main is missing";
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject captureObject = null;
            Canvas officeHudCanvas = null;
            RenderMode previousCanvasMode = RenderMode.ScreenSpaceOverlay;
            Camera previousCanvasCamera = null;
            float previousCanvasPlaneDistance = 0f;
            try
            {
                // Rendering the live camera directly invokes its pixelation post-effect with a
                // manually assigned destination and produced a uniform clear-colour PNG while the
                // on-screen game was healthy. A component-free camera clone renders the exact same
                // world view without that destination ownership conflict.
                captureObject = new GameObject("StarterOfficeQaCaptureCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Camera captureCamera = captureObject.AddComponent<Camera>();
                captureCamera.CopyFrom(camera);
                captureCamera.transform.SetPositionAndRotation(
                    camera.transform.position,
                    camera.transform.rotation);
                captureCamera.enabled = false;
                captureCamera.targetTexture = target;
                float captureAspect = width / (float)height;
                if (fitBounds.HasValue)
                    OfficeGridCameraFitter.Fit(captureCamera, fitBounds.Value, captureAspect);
                else
                    captureCamera.aspect = captureAspect;
                if (includeOfficeHud)
                {
                    officeHudCanvas = Object.FindObjectsByType<Canvas>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .FirstOrDefault(item => item != null &&
                            string.Equals(item.gameObject.name, "Main Navigation HUD V2", StringComparison.Ordinal));
                    if (officeHudCanvas == null)
                    {
                        failure = "Main Navigation HUD V2 canvas is missing or inactive";
                        return false;
                    }
                    previousCanvasMode = officeHudCanvas.renderMode;
                    previousCanvasCamera = officeHudCanvas.worldCamera;
                    previousCanvasPlaneDistance = officeHudCanvas.planeDistance;
                    officeHudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    officeHudCanvas.worldCamera = captureCamera;
                    officeHudCanvas.planeDistance = Mathf.Max(
                        captureCamera.nearClipPlane + 0.1f,
                        1f);
                    Canvas.ForceUpdateCanvases();
                }
                captureCamera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    failure = "capture file is missing or empty";
                    return false;
                }
                Color32[] captured = pixels.GetPixels32();
                byte minR = byte.MaxValue, minG = byte.MaxValue, minB = byte.MaxValue;
                byte maxR = byte.MinValue, maxG = byte.MinValue, maxB = byte.MinValue;
                for (int index = 0; index < captured.Length; index += 4)
                {
                    Color32 color = captured[index];
                    minR = Math.Min(minR, color.r);
                    minG = Math.Min(minG, color.g);
                    minB = Math.Min(minB, color.b);
                    maxR = Math.Max(maxR, color.r);
                    maxG = Math.Max(maxG, color.g);
                    maxB = Math.Max(maxB, color.b);
                }
                if (maxR - minR < 8 && maxG - minG < 8 && maxB - minB < 8)
                {
                    failure =
                        $"capture is visually blank: rgb=({minR}-{maxR},{minG}-{maxG},{minB}-{maxB})";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (officeHudCanvas != null)
                {
                    officeHudCanvas.renderMode = previousCanvasMode;
                    officeHudCanvas.worldCamera = previousCanvasCamera;
                    officeHudCanvas.planeDistance = previousCanvasPlaneDistance;
                    Canvas.ForceUpdateCanvases();
                }
                RenderTexture.active = previousActive;
                target.Release();
                if (captureObject != null) Object.Destroy(captureObject);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static void CenterCameraOnWorldPoint(Camera camera, Vector3 target)
        {
            float depth = Vector3.Dot(
                target - camera.transform.position,
                camera.transform.forward);
            if (depth <= camera.nearClipPlane) depth = Mathf.Max(1f, camera.farClipPlane * 0.01f);
            Vector3 currentViewCenter = camera.transform.position + camera.transform.forward * depth;
            camera.transform.position += target - currentViewCenter;
        }

        private IEnumerator RunContractAndSaveLoadQa(PrototypeBootstrap bootstrap)
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            // The preceding collision scenario intentionally parks the sister on (7,6), which is
            // the mother's assigned chair cell. Normalize positions before testing contract
            // priority so one QA scenario cannot make the next task appear path-unreachable.
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["player"].QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();

            SubcontractOffer offer = BootstrapContractCatalog.CreateOffer(
                bootstrap.State.WorldSeed,
                "starter-runtime-qa-client",
                "Starter Runtime QA Client",
                0);
            bootstrap.AcceptOfferNow(offer);
            SubcontractState contract = bootstrap.State.Contracts.Get(offer.OfferId);
            if (contract.Status != SubcontractStatus.Active)
            {
                FailPlayerQa(59, "QA contract was not accepted");
                yield break;
            }
            OfficeContractTaskCoordinator coordinator = Object.FindFirstObjectByType<OfficeContractTaskCoordinator>();
            int completedBefore = coordinator == null ? 0 : coordinator.CompletedTaskCount;
            bootstrap.AssignContractWorkNow(offer.OfferId, "mother");
            if (coordinator == null || coordinator.PendingCount == 0)
            {
                FailPlayerQa(
                    60,
                    "runtime contract assignment was not queued" +
                    (coordinator == null ? string.Empty : ": " + coordinator.LastAssignmentFailureLabel));
                yield break;
            }
            // Rendering frames may move the actor to its workstation, but may not create work while
            // authoritative game time is stopped.
            float started = Time.time;
            while (Time.time - started < 2f)
                yield return null;
            if (coordinator.CompletedTaskCount != completedBefore)
            {
                FailPlayerQa(61, "runtime contract work advanced while game time was stopped");
                yield break;
            }
            // AssignContractWorkNow queues one four-person-hour chunk, not the entire offer.
            // Advance exactly that authoritative duration so the worker remains inside the
            // 09:00-18:00 attendance window while proving that frames alone produce no work.
            long assignedMinutes = Math.Min(4, contract.RemainingPersonHours) * 60L;
            bootstrap.AdvanceTimeNow(assignedMinutes);
            started = Time.time;
            while (Time.time - started < 45f && coordinator.CompletedTaskCount == completedBefore)
                yield return null;
            if (coordinator.CompletedTaskCount == completedBefore ||
                !string.Equals(coordinator.LastCompletedOfferId, offer.OfferId, StringComparison.Ordinal))
            {
                FailPlayerQa(61, "runtime contract work did not complete through the canonical mother actor");
                yield break;
            }

            const int qaSaveSlot = 3;
            string savedLayoutHash = bootstrap.State.OfficeGrid.ComputeLayoutHash();
            long savedMinutes = bootstrap.State.Time.ElapsedMinutes;
            int doorOpenSfxBeforeLoad = GameAudioCoordinator.Instance.DoorOpenSfxPlayCount;
            int doorCloseSfxBeforeLoad = GameAudioCoordinator.Instance.DoorCloseSfxPlayCount;
            if (!bootstrap.SaveSlotNow(qaSaveSlot))
            {
                FailPlayerQa(62, "slot 3 save failed: " + bootstrap.WorldNotice);
                yield break;
            }
            bootstrap.AdvanceTimeNow(15);
            if (!bootstrap.LoadSlotNow(qaSaveSlot))
            {
                FailPlayerQa(63, "slot 3 load failed: " + bootstrap.WorldNotice);
                yield break;
            }
            string immediateStateHash = bootstrap.State.OfficeGrid.ComputeLayoutHash();
            if (bootstrap.State.Time.ElapsedMinutes != savedMinutes ||
                !string.Equals(immediateStateHash, savedLayoutHash, StringComparison.Ordinal))
            {
                FailPlayerQa(
                    64,
                    $"save/load state mismatch: minutes={bootstrap.State.Time.ElapsedMinutes}/{savedMinutes} " +
                    $"stateHash={immediateStateHash} expected={savedLayoutHash}");
                yield break;
            }

            var loadingFrames = 0;
            while (ScenePreviewJump.IsPresentationLoading && loadingFrames < 900)
            {
                yield return null;
                loadingFrames++;
            }
            if (ScenePreviewJump.IsPresentationLoading ||
                bootstrap.State.Time.ElapsedMinutes != savedMinutes)
            {
                FailPlayerQa(
                    64,
                    $"save/load presentation did not keep authoritative time paused: " +
                    $"loading={ScenePreviewJump.IsPresentationLoading} frames={loadingFrames} " +
                    $"minutes={bootstrap.State.Time.ElapsedMinutes}/{savedMinutes}");
                yield break;
            }

            // Once the loading presentation has closed, the normal realtime clock is expected to
            // resume. Give the rebuilt runtime its historical settling window without incorrectly
            // requiring that ordinary post-load playtime remain frozen.
            for (var frame = 0; frame < 60; frame++) yield return null;
            string restoredStateHash = bootstrap.State.OfficeGrid.ComputeLayoutHash();
            long postLoadMinutes = bootstrap.State.Time.ElapsedMinutes;
            if (!string.Equals(restoredStateHash, savedLayoutHash, StringComparison.Ordinal) ||
                !string.Equals(_starterRuntime.LayoutHash, savedLayoutHash, StringComparison.Ordinal))
            {
                FailPlayerQa(
                    64,
                    $"save/load runtime mismatch: minutes={postLoadMinutes}/{savedMinutes} " +
                    $"stateHash={restoredStateHash} runtimeHash={_starterRuntime.LayoutHash} expected={savedLayoutHash}");
                yield break;
            }
            if (GameAudioCoordinator.Instance.DoorOpenSfxPlayCount != doorOpenSfxBeforeLoad ||
                GameAudioCoordinator.Instance.DoorCloseSfxPlayCount != doorCloseSfxBeforeLoad)
            {
                FailPlayerQa(64, "same-shift save/load emitted a duplicate attendance door cue");
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_CONTRACT_SAVE_LOAD_QA_PASS | offer=" + offer.OfferId +
                " | member=mother | slot=3 | layoutHash=" + savedLayoutHash +
                " | loadedMinutes=" + savedMinutes + " | postLoadMinutes=" + postLoadMinutes +
                " | loadingFrames=" + loadingFrames + " | loadingClockDelta=0" +
                " | attendanceDoorCueDelta=0");
        }

        private IEnumerator WaitForRuntimeReady(int exitCode, string label)
        {
            for (var frame = 0; frame < 900 && (_starterRuntime == null || !_starterRuntime.IsReady); frame++)
                yield return null;
            if (_starterRuntime == null || !_starterRuntime.IsReady || _starterRuntime.World == null)
                FailPlayerQa(exitCode, label + " rebuild timed out");
        }

        private Dictionary<string, OfficeRuntimeAgent> RequiredQaActors()
        {
            if (_starterRuntime == null || !_starterRuntime.IsReady || _starterRuntime.World == null)
            {
                FailPlayerQa(35, "Starter runtime is not ready for a QA scenario");
                return null;
            }
            Dictionary<string, OfficeRuntimeAgent> result = _starterRuntime.Actors
                .Where(item => item != null)
                .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            if (result.Count < QaMemberIds.Length || QaMemberIds.Any(memberId => !result.ContainsKey(memberId)))
            {
                FailPlayerQa(36, "canonical family actors are missing from the shared runtime roster");
                return null;
            }
            foreach (KeyValuePair<string, OfficeRuntimeAgent> item in result.Where(item =>
                         Array.IndexOf(QaMemberIds, item.Key) < 0))
                item.Value.SetAttendanceOutside(true, false);
            return QaMemberIds.ToDictionary(memberId => memberId, memberId => result[memberId], StringComparer.Ordinal);
        }

        private string QaActorSummary(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            IReadOnlyDictionary<string, OfficeGridCoordinate> goals)
        {
            return string.Join(
                " ; ",
                QaMemberIds.Select(memberId =>
                {
                    OfficeRuntimeAgent actor = actors[memberId];
                    OfficeGridCoordinate cell = _starterRuntime.World.Occupancy.CurrentCell(memberId);
                    return $"{memberId}:cell={cell},goal={goals[memberId]},position={actor.Position}," +
                           $"phase={actor.Phase},stuck={actor.StuckSeconds:F2},desired={actor.DesiredVelocity}";
                }));
        }

        private bool RequireZeroActualViolations(string scenario, int exitCode)
        {
            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            if (occupancy.StaticViolationCount == 0 && occupancy.InteractionViolationCount == 0 &&
                occupancy.AgentPenetrationCount == 0) return true;
            FailPlayerQa(exitCode, scenario + " recorded actual occupancy violations: " + OccupancyMetricSummary());
            return false;
        }

        private string OccupancyMetricSummary()
        {
            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            return $"static={occupancy.StaticViolationCount} interaction={occupancy.InteractionViolationCount} " +
                   $"penetration={occupancy.AgentPenetrationCount} blockedStatic={occupancy.BlockedStaticMoveCount} " +
                   $"blockedInteraction={occupancy.BlockedInteractionMoveCount} " +
                   $"blockedAgent={occupancy.BlockedAgentMoveCount} " +
                   $"minimumSeparationMargin={occupancy.MinimumAgentSeparationMargin:F4}";
        }

        private static OfficeGrid CreateRuntimeDeskQaLayout()
        {
            OfficeGrid source = OfficeGridLayouts.CreateStarterOfficeV1();
            bool[] walkable = source.CopyWalkable();
            var blockedCell = new OfficeGridCoordinate(6, 6);
            walkable[blockedCell.Y * source.Width + blockedCell.X] = false;
            var secondBlockedCell = new OfficeGridCoordinate(6, 7);
            walkable[secondBlockedCell.Y * source.Width + secondBlockedCell.X] = false;
            List<PlacedOfficeFurniture> furniture = source.Furniture.ToList();
            furniture.Add(new PlacedOfficeFurniture(
                "qa_runtime_desk",
                OfficeGridLayouts.DeskWithPcKind,
                blockedCell,
                1,
                2,
                OfficeFurnitureFacing.NorthEast,
                true));
            return new OfficeGrid(
                source.Width,
                source.Height,
                source.CopyFloorTiles(),
                walkable,
                furniture,
                source.SeatSlots);
        }

        private static OfficeGrid CreateNarrowCorridorQaLayout()
        {
            const int width = 13;
            const int height = 13;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + (x * 3 + y * 5) % 3);
                bool leftRoom = x >= 1 && x <= 4 && y >= 1 && y <= 11;
                bool rightRoom = x >= 8 && x <= 11 && y >= 1 && y <= 11;
                bool corridor = y == 6 && x >= 4 && x <= 8;
                walkable[index] = leftRoom || rightRoom || corridor;
            }
            return new OfficeGrid(width, height, floor, walkable);
        }

        private static OfficeGrid CreateDirectionQaLayout()
        {
            const int width = 25;
            const int height = 25;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + (x * 3 + y * 5) % 3);
                walkable[index] = x > 0 && x < width - 1 && y > 0 && y < height - 1;
            }
            return new OfficeGrid(width, height, floor, walkable);
        }

        private void FailPlayerQa(int exitCode, string message)
        {
            if (_playerQaFailure.Length > 0) return;
            _playerQaExitCode = exitCode;
            _playerQaFailure = message ?? "unknown failure";
        }

        private bool QuitIfPlayerQaFailed(float previousTimeScale)
        {
            if (_playerQaFailure.Length == 0) return false;
            string movementProfile = EndMovementProfile();
            Debug.LogError(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | code=" + _playerQaExitCode +
                " | " + _playerQaFailure + " | " + movementProfile);
            Time.timeScale = previousTimeScale;
            Application.Quit(_playerQaExitCode == 0 ? 30 : _playerQaExitCode);
            return true;
        }

        private void BeginMovementProfile()
        {
            EndMovementProfile();
            _qaMaximumGcAllocatedBytes = 0L;
            _qaMaximumMainThreadNanoseconds = 0L;
            _qaMovementProfileSamples = 0;
            _qaGcAllocatedRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                1);
            _qaMainThreadRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "Main Thread",
                1);
            _qaMovementProfilingActive = true;
        }

        private void SampleMovementProfile()
        {
            if (!_qaMovementProfilingActive) return;
            if (_qaGcAllocatedRecorder.Valid)
                _qaMaximumGcAllocatedBytes = Math.Max(
                    _qaMaximumGcAllocatedBytes,
                    _qaGcAllocatedRecorder.LastValue);
            if (_qaMainThreadRecorder.Valid)
                _qaMaximumMainThreadNanoseconds = Math.Max(
                    _qaMaximumMainThreadNanoseconds,
                    _qaMainThreadRecorder.LastValue);
            _qaMovementProfileSamples++;
        }

        private string EndMovementProfile()
        {
            if (!_qaMovementProfilingActive) return "movementProfiler=inactive";
            SampleMovementProfile();
            string summary =
                $"movementProfilerSamples={_qaMovementProfileSamples} " +
                $"maxGcAllocFrame={_qaMaximumGcAllocatedBytes}B " +
                $"maxMainThread={_qaMaximumMainThreadNanoseconds / 1000000f:F3}ms";
            _qaGcAllocatedRecorder.Dispose();
            _qaMainThreadRecorder.Dispose();
            _qaMovementProfilingActive = false;
            return summary;
        }

        private void OnDestroy()
        {
            EndMovementProfile();
        }

        private static OfficeTileMigrationPreviewBootstrap FindBootstrap(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponentInChildren<OfficeTileMigrationPreviewBootstrap>(true);
                if (bootstrap != null) return bootstrap;
            }
            return null;
        }

        private static Renderer[] CollectRenderers(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return System.Array.Empty<Renderer>();
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Renderer>(true));
            return result.ToArray();
        }

        private static int FindPreviewBuildIndex()
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == PreviewSceneName) return index;
            }
            return -1;
        }
    }
}

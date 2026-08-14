using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Rendering
{
    public enum PixelClarityComparisonMode
    {
        FinalNativeStable = 0,
        LegacyHalfHeight = 1,
        NativeUnsnapped = 2
    }

    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class PixelClarityRuntime : MonoBehaviour
    {
        private static PixelClarityRuntime _instance;

        private readonly List<OfficeRuntimeAgent> _actors = new List<OfficeRuntimeAgent>();
        private readonly Dictionary<Transform, Vector3> _renderPositionRestore =
            new Dictionary<Transform, Vector3>();

        private PixelClarityProfile _profile;
        private PixelClarityComparisonMode _comparisonMode = PixelClarityComparisonMode.FinalNativeStable;
        private StarterOfficeRuntimeBootstrap _starterOffice;
        private int _originalAntiAliasing;
        private int _appliedQualityMode = -1;
        private int _lastEffectScanFrame = -1000;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private string _lastLayoutHash = string.Empty;
        private float _nextRuntimeScanAt;

        public static PixelClarityRuntime Instance => _instance;
        public PixelClarityProfile Profile => _profile;
        public PixelClarityComparisonMode ComparisonMode => _comparisonMode;
        public float LastMaximumCharacterSnapOffsetPixels { get; private set; }
        public int LastSnappedCharacterCount { get; private set; }
        public bool IsFinalNativeStable =>
            _comparisonMode == PixelClarityComparisonMode.FinalNativeStable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var host = new GameObject("~PixelClarityRuntime");
            _instance = host.AddComponent<PixelClarityRuntime>();
            if (Application.isPlaying) DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _profile = PixelClarityProfile.LoadDefault();
            _originalAntiAliasing = QualitySettings.antiAliasing;
            ApplyQualityPolicy(true);
            ApplyCameraRenderPolicy(true);
            Debug.Log(
                "PIXEL_CLARITY_PROFILE_ACTIVE | " +
                $"reference={_profile.ReferenceWidth}x{_profile.ReferenceHeight} " +
                $"renderScale={_profile.NativeRenderScale:F2} legacyPixelation=disabled " +
                $"cameraSnap={_profile.SnapCameraToPhysicalPixelGrid} " +
                $"actorSnap={_profile.SnapMovingCharacterPresentation} " +
                $"pixelArtPPU={_profile.PixelArtPixelsPerUnit:F0} aa={_profile.AntiAliasingSamples}");
        }

        private void OnEnable()
        {
            Camera.onPreCull += HandleCameraPreCull;
            Camera.onPostRender += HandleCameraPostRender;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= HandleCameraPreCull;
            Camera.onPostRender -= HandleCameraPostRender;
            RestorePresentationPositions();
        }

        private void LateUpdate()
        {
            ApplyQualityPolicy(false);
            ApplyActiveCameraRenderPolicy();
            ApplyCameraRenderPolicy(false);
            RefreshStarterOfficeIfNeeded();
            if (_starterOffice == null || !_starterOffice.IsReady || _starterOffice.World == null) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            bool frameChanged = Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight;
            bool layoutChanged = !string.Equals(
                _lastLayoutHash,
                _starterOffice.LayoutHash,
                StringComparison.Ordinal);
            if (frameChanged || layoutChanged)
                ReframeStarterOffice(camera);

            if (IsFinalNativeStable && _profile.SnapCameraToPhysicalPixelGrid)
                SnapCameraToPhysicalPixelGrid(camera);
        }

        public void SetComparisonMode(PixelClarityComparisonMode mode)
        {
            if (_comparisonMode == mode) return;
            _comparisonMode = mode;
            _appliedQualityMode = -1;
            _lastEffectScanFrame = -1000;
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastLayoutHash = string.Empty;
            ApplyQualityPolicy(true);
            ApplyCameraRenderPolicy(true);
            Camera camera = Camera.main;
            if (camera != null && _starterOffice != null && _starterOffice.IsReady)
                ReframeStarterOffice(camera);
            Debug.Log("PIXEL_CLARITY_COMPARISON_MODE | mode=" + mode);
        }

        public void ForceReframe()
        {
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastLayoutHash = string.Empty;
            RefreshStarterOfficeIfNeeded(true);
            Camera camera = Camera.main;
            if (camera != null && _starterOffice != null && _starterOffice.IsReady)
                ReframeStarterOffice(camera);
        }

        private void ApplyQualityPolicy(bool force)
        {
            int qualityMode = (int)_comparisonMode;
            if (!force && _appliedQualityMode == qualityMode) return;
            _appliedQualityMode = qualityMode;

            float scale = IsFinalNativeStable || _comparisonMode == PixelClarityComparisonMode.NativeUnsnapped
                ? _profile.NativeRenderScale
                : 1f;
            ScalableBufferManager.ResizeBuffers(scale, scale);
            QualitySettings.resolutionScalingFixedDPIFactor = 1f;
            QualitySettings.globalTextureMipmapLimit = _profile.GlobalTextureMipmapLimit;
            QualitySettings.antiAliasing = _comparisonMode == PixelClarityComparisonMode.LegacyHalfHeight
                ? _originalAntiAliasing
                : _profile.AntiAliasingSamples;
        }

        private void ApplyCameraRenderPolicy(bool force)
        {
            if (!force && Time.frameCount - _lastEffectScanFrame < 30) return;
            _lastEffectScanFrame = Time.frameCount;

            bool legacy = _comparisonMode == PixelClarityComparisonMode.LegacyHalfHeight;
            foreach (PixelatedCameraEffect effect in FindObjectsByType<PixelatedCameraEffect>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (effect == null) continue;
                if (legacy)
                {
                    effect.Configure(_profile.LegacyComparisonHeight);
                    effect.enabled = true;
                }
                else if (_profile.DisableLegacyHalfHeightPixelation)
                {
                    effect.enabled = false;
                }
            }

            foreach (Camera camera in FindObjectsByType<Camera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (camera != null) camera.allowDynamicResolution = false;
            }
        }

        private void ApplyActiveCameraRenderPolicy()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.allowDynamicResolution = false;
            PixelatedCameraEffect effect = camera.GetComponent<PixelatedCameraEffect>();
            if (effect == null) return;
            if (_comparisonMode == PixelClarityComparisonMode.LegacyHalfHeight)
            {
                effect.Configure(_profile.LegacyComparisonHeight);
                effect.enabled = true;
            }
            else if (_profile.DisableLegacyHalfHeightPixelation)
            {
                effect.enabled = false;
            }
        }

        private void RefreshStarterOfficeIfNeeded(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextRuntimeScanAt &&
                _starterOffice != null && _starterOffice.IsReady) return;
            _nextRuntimeScanAt = Time.unscaledTime + 0.25f;

            StarterOfficeRuntimeBootstrap next =
                FindFirstObjectByType<StarterOfficeRuntimeBootstrap>(FindObjectsInactive.Include);
            bool changed = next != _starterOffice;
            _starterOffice = next;
            if (_starterOffice == null || !_starterOffice.IsReady)
            {
                _actors.Clear();
                return;
            }

            if (!changed && !force && ActorListMatchesRuntime()) return;
            _actors.Clear();
            foreach (OfficeRuntimeAgent actor in _starterOffice.Actors)
                if (actor != null) _actors.Add(actor);
        }

        private bool ActorListMatchesRuntime()
        {
            if (_starterOffice == null || _actors.Count != _starterOffice.Actors.Count) return false;
            for (int index = 0; index < _actors.Count; index++)
                if (_actors[index] == null || _actors[index] != _starterOffice.Actors[index]) return false;
            return true;
        }

        private void ReframeStarterOffice(Camera camera)
        {
            if (camera == null || _starterOffice == null || !_starterOffice.IsReady ||
                _starterOffice.World == null || _starterOffice.World.Presenter == null ||
                _starterOffice.World.FurniturePresenter == null) return;

            Bounds bounds = _starterOffice.World.Presenter.FloorRenderer.bounds;
            bounds.Encapsulate(_starterOffice.World.FurniturePresenter.RenderBounds);
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : _profile.ReferenceAspect;
            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = OfficeGridCameraFitter.ResolveOrthographicSize(
                bounds,
                aspect,
                OfficeGridCameraFitter.DefaultOrthographicSize,
                _profile.OfficeSafeFraction);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            camera.transform.rotation = Quaternion.identity;
            if (IsFinalNativeStable && _profile.SnapCameraToPhysicalPixelGrid)
                SnapCameraToPhysicalPixelGrid(camera);

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastLayoutHash = _starterOffice.LayoutHash;
            Debug.Log(
                "PIXEL_CLARITY_FRAME | " +
                $"screen={Screen.width}x{Screen.height} aspect={aspect:F5} " +
                $"orthographicSize={camera.orthographicSize:F6} " +
                $"camera=({camera.transform.position.x:F6},{camera.transform.position.y:F6}) " +
                $"layoutHash={_lastLayoutHash}");
        }

        public static void SnapCameraToPhysicalPixelGrid(Camera camera)
        {
            if (camera == null || !camera.orthographic || camera.pixelHeight <= 0) return;
            float unitsPerPixel = camera.orthographicSize * 2f / camera.pixelHeight;
            Vector3 reference = Vector3.zero;
            Vector3 screen = camera.WorldToScreenPoint(reference);
            if (screen.z <= 0f) return;
            float deltaX = Mathf.Round(screen.x) - screen.x;
            float deltaY = Mathf.Round(screen.y) - screen.y;
            camera.transform.position -=
                camera.transform.right * (deltaX * unitsPerPixel) +
                camera.transform.up * (deltaY * unitsPerPixel);
        }

        private void HandleCameraPreCull(Camera camera)
        {
            LastMaximumCharacterSnapOffsetPixels = 0f;
            LastSnappedCharacterCount = 0;
            if (!IsFinalNativeStable || !_profile.SnapMovingCharacterPresentation ||
                !IsPresentationCamera(camera) || !camera.orthographic || camera.pixelHeight <= 0)
                return;

            RestorePresentationPositions();
            float unitsPerPixel = camera.orthographicSize * 2f / camera.pixelHeight;
            foreach (OfficeRuntimeAgent actor in _actors)
            {
                if (actor == null || actor.IsOccupyingSeat) continue;
                SpriteRenderer renderer = actor.PresentationRenderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Transform target = renderer.transform;
                Vector3 screen = camera.WorldToScreenPoint(target.position);
                if (screen.z <= 0f) continue;
                float deltaX = Mathf.Round(screen.x) - screen.x;
                float deltaY = Mathf.Round(screen.y) - screen.y;
                float magnitude = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
                LastMaximumCharacterSnapOffsetPixels = Mathf.Max(
                    LastMaximumCharacterSnapOffsetPixels,
                    magnitude);
                _renderPositionRestore[target] = target.position;
                target.position +=
                    camera.transform.right * (deltaX * unitsPerPixel) +
                    camera.transform.up * (deltaY * unitsPerPixel);
                LastSnappedCharacterCount++;
            }
        }

        private void HandleCameraPostRender(Camera camera)
        {
            if (IsPresentationCamera(camera)) RestorePresentationPositions();
        }

        private static bool IsPresentationCamera(Camera camera)
        {
            return camera != null &&
                   (camera == Camera.main ||
                    camera.name.StartsWith("RenderClarityQaCaptureCamera", StringComparison.Ordinal));
        }

        private void RestorePresentationPositions()
        {
            if (_renderPositionRestore.Count == 0) return;
            foreach (KeyValuePair<Transform, Vector3> item in _renderPositionRestore)
                if (item.Key != null) item.Key.position = item.Value;
            _renderPositionRestore.Clear();
        }
    }
}

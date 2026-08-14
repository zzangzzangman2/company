using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Stamina;
using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    public enum OverheadStaminaColorBand
    {
        Stable = 0,
        Caution = 1,
        Critical = 2
    }

    public readonly struct OverheadStaminaBarDebugSnapshot
    {
        public OverheadStaminaBarDebugSnapshot(
            string characterId,
            int currentUnits,
            int maxUnits,
            int ratioBasisPoints,
            long lastProcessedMinute,
            StaminaRecoveryPhase recoveryPhase,
            StaminaRecoveryActivity recoveryActivity,
            OverheadStaminaColorBand colorBand,
            bool visible,
            Vector3 worldAnchor,
            Rect screenRect,
            int actorSortingOrder)
        {
            CharacterId = characterId ?? string.Empty;
            CurrentUnits = currentUnits;
            MaxUnits = maxUnits;
            RatioBasisPoints = ratioBasisPoints;
            LastProcessedMinute = lastProcessedMinute;
            RecoveryPhase = recoveryPhase;
            RecoveryActivity = recoveryActivity;
            ColorBand = colorBand;
            Visible = visible;
            WorldAnchor = worldAnchor;
            ScreenRect = screenRect;
            ActorSortingOrder = actorSortingOrder;
        }

        public string CharacterId { get; }
        public int CurrentUnits { get; }
        public int MaxUnits { get; }
        public int RatioBasisPoints { get; }
        public long LastProcessedMinute { get; }
        public StaminaRecoveryPhase RecoveryPhase { get; }
        public StaminaRecoveryActivity RecoveryActivity { get; }
        public OverheadStaminaColorBand ColorBand { get; }
        public bool Visible { get; }
        public Vector3 WorldAnchor { get; }
        public Rect ScreenRect { get; }
        public int ActorSortingOrder { get; }
    }

    [DefaultExecutionOrder(10_000)]
    [DisallowMultipleComponent]
    public sealed class OverheadStaminaBarPresenter : MonoBehaviour
    {
        public const float BarWidthPixels = 48f;
        public const float BarHeightPixels = 8f;
        public const float InnerWidthPixels = 44f;
        public const float InnerHeightPixels = 4f;
        public const float HeadPaddingPixels = 8f;

        private const float ReconcileIntervalSeconds = 0.35f;
        private const int CanvasSortingOrder = 31_000;

        private static readonly Color ShellColor = new Color32(24, 34, 39, 245);
        private static readonly Color TrackColor = new Color32(55, 67, 70, 235);
        private static readonly Color StableColor = new Color32(51, 198, 145, 255);
        private static readonly Color CautionColor = new Color32(232, 191, 67, 255);
        private static readonly Color CriticalColor = new Color32(220, 77, 73, 255);

        private sealed class Entry
        {
            public string CharacterId;
            public OfficeRuntimeAgent Actor;
            public GameObject Root;
            public RectTransform Rect;
            public RectTransform FillRect;
            public Image Fill;
            public OverheadStaminaBarDebugSnapshot DebugSnapshot;
        }

        private sealed class EntryDepthComparer : IComparer<Entry>
        {
            public static readonly EntryDepthComparer Instance = new EntryDepthComparer();

            public int Compare(Entry left, Entry right)
            {
                int leftOrder = ResolveActorOrder(left);
                int rightOrder = ResolveActorOrder(right);
                int order = leftOrder.CompareTo(rightOrder);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left?.CharacterId, right?.CharacterId);
            }
        }

        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgent> _liveActors =
            new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);
        private readonly List<string> _removalBuffer = new List<string>();
        private readonly List<Entry> _sortBuffer = new List<Entry>();

        private PrototypeBootstrap _bootstrap;
        private StarterOfficeRuntimeBootstrap _starter;
        private OfficeRuntimeWorld _observedWorld;
        private Camera _camera;
        private ICharacterStaminaReadModel _readModel;
        private int _bindingRevision = -1;
        private float _reconcileRemaining;
        private bool _qaConfigured;
        private bool _warnedPerspective;
        private GameObject _canvasObject;
        private RectTransform _canvasRect;
        private Canvas _canvas;

        public int BoundBarCount => _entries.Count;

        public int VisibleBarCount
        {
            get
            {
                int count = 0;
                foreach (Entry entry in _entries.Values)
                    if (entry.DebugSnapshot.Visible) count++;
                return count;
            }
        }

        public IReadOnlyList<OverheadStaminaBarDebugSnapshot> DebugSnapshots =>
            _entries.Values
                .OrderBy(item => item.CharacterId, StringComparer.Ordinal)
                .Select(item => item.DebugSnapshot)
                .ToArray();

        /// <summary>Focused PlayMode QA seam. Production uses the transient owner binding.</summary>
        public void ConfigureForQa(
            PrototypeBootstrap bootstrap,
            StarterOfficeRuntimeBootstrap starter,
            Camera camera,
            ICharacterStaminaReadModel readModel)
        {
            _qaConfigured = true;
            _bootstrap = bootstrap;
            _starter = starter;
            _observedWorld = starter == null ? null : starter.World;
            _camera = camera;
            _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
            _bindingRevision = -1;
            _reconcileRemaining = 0f;
            EnsureCanvas();
            RefreshImmediateForQa();
        }

        public void ClearBinding()
        {
            _readModel = null;
            _bindingRevision = -1;
            ClearEntries();
        }

        public bool TryGetDebugSnapshot(
            string characterId,
            out OverheadStaminaBarDebugSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(characterId) &&
                _entries.TryGetValue(characterId.Trim(), out Entry entry))
            {
                snapshot = entry.DebugSnapshot;
                return true;
            }
            snapshot = default;
            return false;
        }

        public void RefreshImmediateForQa()
        {
            ResolveDependencies();
            ReconcileActors();
            bool canvasReady = UpdateCanvasPlane();
            bool screenAllowsBars = IsOfficePresentationReady();
            foreach (Entry entry in _entries.Values)
                UpdateEntry(entry, screenAllowsBars && canvasReady);
            ApplySiblingDepthOrder();
        }

        public static OverheadStaminaColorBand ResolveColorBand(
            CharacterStaminaReadSnapshot value)
        {
            long max = Math.Max(1, value.MaxUnits);
            long scaledCurrent = (long)Math.Max(0, value.CurrentUnits) * 10_000L;
            if (scaledCurrent <= max * value.RecoveryThresholdBasisPoints)
                return OverheadStaminaColorBand.Critical;
            if (scaledCurrent <= max * value.CautionThresholdBasisPoints)
                return OverheadStaminaColorBand.Caution;
            return OverheadStaminaColorBand.Stable;
        }

        public static float ResolveFillRatio(int ratioBasisPoints)
        {
            return Mathf.Clamp(ratioBasisPoints, 0, 10_000) / 10_000f;
        }

        private void OnEnable()
        {
            EnsureCanvas();
            _reconcileRemaining = 0f;
        }

        private void LateUpdate()
        {
            EnsureCanvas();
            bool bindingChanged = RefreshProductionBinding();
            _reconcileRemaining -= Time.unscaledDeltaTime;
            if (bindingChanged || _reconcileRemaining <= 0f)
            {
                _reconcileRemaining = ReconcileIntervalSeconds;
                ResolveDependencies();
                ReconcileActors();
            }

            bool canvasReady = UpdateCanvasPlane();
            bool screenAllowsBars = IsOfficePresentationReady();
            foreach (Entry entry in _entries.Values)
                UpdateEntry(entry, screenAllowsBars && canvasReady);
            ApplySiblingDepthOrder();
        }

        private bool RefreshProductionBinding()
        {
            if (_qaConfigured) return false;
            bool available = CharacterStaminaPresentationBinding.TryGet(
                out ICharacterStaminaReadModel model,
                out int revision);
            if (_bindingRevision == revision && ReferenceEquals(_readModel, model)) return false;
            _bindingRevision = revision;
            _readModel = available ? model : null;
            return true;
        }

        private void ResolveDependencies()
        {
            if (_bootstrap == null)
                _bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>(
                    FindObjectsInactive.Include);
            if (_starter == null)
                _starter = UnityEngine.Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>(
                    FindObjectsInactive.Include);

            if (!_qaConfigured || _camera == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != _camera) _camera = mainCamera;
            }

            OfficeRuntimeWorld liveWorld = _starter == null ? null : _starter.World;
            if (_observedWorld == liveWorld) return;
            _observedWorld = liveWorld;
            ClearEntries();
        }

        private void ReconcileActors()
        {
            if (_readModel == null || _starter == null || !_starter.IsReady ||
                _starter.World == null)
            {
                ClearEntries();
                return;
            }

            _liveActors.Clear();
            IReadOnlyList<OfficeRuntimeAgent> actors = _starter.Actors;
            for (int index = 0; index < actors.Count; index++)
            {
                OfficeRuntimeAgent actor = actors[index];
                if (actor == null || string.IsNullOrWhiteSpace(actor.AgentId)) continue;
                if (!_liveActors.TryAdd(actor.AgentId, actor))
                    throw new InvalidOperationException(
                        "Duplicate office runtime actor ID: " + actor.AgentId);
            }

            _removalBuffer.Clear();
            foreach (KeyValuePair<string, Entry> pair in _entries)
            {
                if (!_liveActors.TryGetValue(pair.Key, out OfficeRuntimeAgent actor) ||
                    actor == null || !_readModel.TryRead(pair.Key, out _))
                    _removalBuffer.Add(pair.Key);
            }
            for (int index = 0; index < _removalBuffer.Count; index++)
                RemoveEntry(_removalBuffer[index]);

            foreach (KeyValuePair<string, OfficeRuntimeAgent> pair in _liveActors
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!_readModel.TryRead(pair.Key, out _)) continue;
                if (_entries.TryGetValue(pair.Key, out Entry current) &&
                    ReferenceEquals(current.Actor, pair.Value)) continue;
                RemoveEntry(pair.Key);
                _entries.Add(pair.Key, CreateEntry(pair.Key, pair.Value));
            }
        }

        private Entry CreateEntry(string characterId, OfficeRuntimeAgent actor)
        {
            var root = new GameObject("StaminaBar_" + characterId, typeof(RectTransform));
            root.transform.SetParent(_canvasRect, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(BarWidthPixels, BarHeightPixels);

            Image shell = CreateImage("Shell", rect, ShellColor);
            Stretch((RectTransform)shell.transform);

            Image track = CreateImage("Track", rect, TrackColor);
            RectTransform trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = new Vector2(InnerWidthPixels, InnerHeightPixels);

            Image fill = CreateImage("Fill", trackRect, StableColor);
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            root.SetActive(false);
            return new Entry
            {
                CharacterId = characterId,
                Actor = actor,
                Root = root,
                Rect = rect,
                FillRect = fillRect,
                Fill = fill,
                DebugSnapshot = new OverheadStaminaBarDebugSnapshot(
                    characterId,
                    0,
                    1,
                    0,
                    0,
                    StaminaRecoveryPhase.Working,
                    StaminaRecoveryActivity.None,
                    OverheadStaminaColorBand.Critical,
                    false,
                    Vector3.zero,
                    default,
                    0)
            };
        }

        private void UpdateEntry(Entry entry, bool screenAllowsBars)
        {
            OfficeRuntimeAgent actor = entry.Actor;
            SpriteRenderer renderer = actor == null ? null : actor.PresentationRenderer;
            CharacterStaminaReadSnapshot value = default;
            bool hasValue = _readModel != null &&
                            _readModel.TryRead(entry.CharacterId, out value);
            bool actorVisible = actor != null && !actor.IsPresentationAway &&
                                actor.isActiveAndEnabled && actor.gameObject.activeInHierarchy &&
                                renderer != null && renderer.enabled &&
                                renderer.gameObject.activeInHierarchy;

            Vector3 worldAnchor = Vector3.zero;
            Vector2 screenAnchor = Vector2.zero;
            bool projected = actorVisible && ProjectHeadAnchor(
                renderer.bounds,
                out worldAnchor,
                out screenAnchor);
            bool visible = screenAllowsBars && hasValue && actorVisible && projected;
            if (entry.Root.activeSelf != visible) entry.Root.SetActive(visible);

            int current = hasValue ? value.CurrentUnits : 0;
            int max = hasValue ? Math.Max(1, value.MaxUnits) : 1;
            int ratio = hasValue ? Mathf.Clamp(value.RatioBasisPoints, 0, 10_000) : 0;
            long minute = hasValue ? value.LastProcessedMinute : 0;
            StaminaRecoveryPhase phase = hasValue
                ? value.RecoveryPhase
                : StaminaRecoveryPhase.Working;
            StaminaRecoveryActivity activity = hasValue
                ? value.RecoveryActivity
                : StaminaRecoveryActivity.None;
            OverheadStaminaColorBand band = hasValue
                ? ResolveColorBand(value)
                : OverheadStaminaColorBand.Critical;
            int actorOrder = ResolveActorOrder(entry);
            Rect screenRect = default;

            float fill = ResolveFillRatio(ratio);
            entry.FillRect.anchorMax = new Vector2(fill, 1f);
            entry.Fill.color = ColorFor(band);
            if (visible)
            {
                Rect pixelRect = _camera.pixelRect;
                entry.Rect.anchoredPosition = new Vector2(
                    screenAnchor.x - pixelRect.center.x,
                    screenAnchor.y - pixelRect.center.y);
                screenRect = new Rect(
                    screenAnchor.x - BarWidthPixels * 0.5f,
                    screenAnchor.y - BarHeightPixels * 0.5f,
                    BarWidthPixels,
                    BarHeightPixels);
            }

            entry.DebugSnapshot = new OverheadStaminaBarDebugSnapshot(
                entry.CharacterId,
                current,
                max,
                ratio,
                minute,
                phase,
                activity,
                band,
                visible,
                worldAnchor,
                screenRect,
                actorOrder);
        }

        private bool ProjectHeadAnchor(
            Bounds bounds,
            out Vector3 worldAnchor,
            out Vector2 screenAnchor)
        {
            Vector3 unpaddedAnchor = new Vector3(
                bounds.center.x,
                bounds.max.y,
                bounds.center.z);
            worldAnchor = unpaddedAnchor;
            screenAnchor = Vector2.zero;
            if (_camera == null) return false;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            bool anyInFront = false;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 projected = _camera.WorldToScreenPoint(point);
                if (projected.z <= 0f) continue;
                anyInFront = true;
                minX = Mathf.Min(minX, projected.x);
                maxX = Mathf.Max(maxX, projected.x);
                maxY = Mathf.Max(maxY, projected.y);
            }
            if (!anyInFront) return false;

            screenAnchor = new Vector2((minX + maxX) * 0.5f, maxY + HeadPaddingPixels);
            Vector3 unpaddedScreen = _camera.WorldToScreenPoint(unpaddedAnchor);
            if (unpaddedScreen.z > 0f)
                worldAnchor = _camera.ScreenToWorldPoint(new Vector3(
                    screenAnchor.x,
                    screenAnchor.y,
                    unpaddedScreen.z));
            Rect pixelRect = _camera.pixelRect;
            return screenAnchor.x >= pixelRect.xMin && screenAnchor.x <= pixelRect.xMax &&
                   screenAnchor.y >= pixelRect.yMin && screenAnchor.y <= pixelRect.yMax;
        }

        private bool UpdateCanvasPlane()
        {
            if (_canvas == null || _canvasRect == null) return false;
            bool usable = _camera != null && _camera.isActiveAndEnabled &&
                          _camera.orthographic && _camera.pixelWidth > 0 &&
                          _camera.pixelHeight > 0;
            _canvas.enabled = usable;
            if (!usable)
            {
                if (_camera != null && !_camera.orthographic && !_warnedPerspective)
                {
                    _warnedPerspective = true;
                    Debug.LogWarning(
                        "Overhead stamina bars are hidden because the office camera is not orthographic.",
                        this);
                }
                return false;
            }

            _warnedPerspective = false;
            float worldUnitsPerPixel = Mathf.Max(
                0.00001f,
                _camera.orthographicSize * 2f / _camera.pixelHeight);
            float distance = Mathf.Max(_camera.nearClipPlane + 0.25f, 1f);
            if (_camera.farClipPlane > _camera.nearClipPlane)
                distance = Mathf.Min(distance, _camera.farClipPlane - 0.01f);
            Transform cameraTransform = _camera.transform;
            _canvasRect.position = cameraTransform.position + cameraTransform.forward * distance;
            _canvasRect.rotation = cameraTransform.rotation;
            _canvasRect.localScale = Vector3.one * worldUnitsPerPixel;
            _canvasRect.sizeDelta = new Vector2(_camera.pixelWidth, _camera.pixelHeight);
            _canvas.worldCamera = _camera;
            return true;
        }

        private bool IsOfficePresentationReady()
        {
            return _bootstrap != null &&
                   _bootstrap.UiScreen == PrototypeUiScreen.Playing &&
                   _starter != null && _starter.IsReady && _starter.World != null;
        }

        private void ApplySiblingDepthOrder()
        {
            _sortBuffer.Clear();
            foreach (Entry entry in _entries.Values) _sortBuffer.Add(entry);
            _sortBuffer.Sort(EntryDepthComparer.Instance);
            for (int index = 0; index < _sortBuffer.Count; index++)
            {
                RectTransform rect = _sortBuffer[index].Rect;
                if (rect != null && rect.GetSiblingIndex() != index)
                    rect.SetSiblingIndex(index);
            }
        }

        private void EnsureCanvas()
        {
            if (_canvasObject != null) return;
            _canvasObject = new GameObject(
                "WorldCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            _canvasObject.transform.SetParent(transform, false);
            _canvasRect = (RectTransform)_canvasObject.transform;
            _canvasRect.anchorMin = _canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingLayerName = "Default";
            _canvas.sortingOrder = CanvasSortingOrder;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            root.transform.SetParent(parent, false);
            Image image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.maskable = false;
            image.type = Image.Type.Simple;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color ColorFor(OverheadStaminaColorBand band)
        {
            return band switch
            {
                OverheadStaminaColorBand.Critical => CriticalColor,
                OverheadStaminaColorBand.Caution => CautionColor,
                _ => StableColor
            };
        }

        private static int ResolveActorOrder(Entry entry)
        {
            SpriteRenderer renderer = entry?.Actor == null
                ? null
                : entry.Actor.PresentationRenderer;
            return renderer == null ? 0 : renderer.sortingOrder;
        }

        private void RemoveEntry(string characterId)
        {
            if (!_entries.TryGetValue(characterId, out Entry entry)) return;
            _entries.Remove(characterId);
            if (entry.Root != null) DestroyOwned(entry.Root);
        }

        private void ClearEntries()
        {
            foreach (Entry entry in _entries.Values)
                if (entry.Root != null) DestroyOwned(entry.Root);
            _entries.Clear();
        }

        private void OnDestroy()
        {
            _entries.Clear();
            if (_canvasObject != null) DestroyOwned(_canvasObject);
            _canvasObject = null;
            _canvasRect = null;
            _canvas = null;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}

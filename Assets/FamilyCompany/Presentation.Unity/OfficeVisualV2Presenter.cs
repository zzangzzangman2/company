using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [DefaultExecutionOrder(200)]
    public sealed class OfficeVisualV2Presenter : MonoBehaviour
    {
        private const int PresentationLayer = 31;
        public const string ResourceRoot = "OfficeVisualV2";
        public const string BaseResourceName = "office_base";
        public const string ForegroundResourceName = "office_foreground";
        public const float DefaultCharacterScale = 1f;
        public const float TargetAspect = OfficeVisualV2Calibration.ArtCropAspect;

        private static readonly string[] BaseCandidates =
        {
            ResourceRoot + "/" + BaseResourceName,
            ResourceRoot + "/office_base_v2",
            ResourceRoot + "/family_office_base_v2"
        };

        private static readonly string[] ForegroundCandidates =
        {
            ResourceRoot + "/" + ForegroundResourceName,
            ResourceRoot + "/office_foreground_v2",
            ResourceRoot + "/family_office_foreground_v2"
        };

        [SerializeField] private Transform player;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform blockoutRoot;
        [SerializeField] private Vector3 officeCenter = new Vector3(14f, 0f, 0f);
        [SerializeField] private Vector2 officeSize = new Vector2(16f, 14f);
        [SerializeField] private float projectionCenterHeight = 0.6f;
        [SerializeField] private float projectionDepth = 10f;
        [SerializeField] private float characterVisualScale = DefaultCharacterScale;
        [SerializeField] private int baseSortingOrder = -100;
        [SerializeField] private int characterSortingOrder = 20;
        [SerializeField] private int foregroundSortingOrder = 100;
        [SerializeField] private int labelSortingOrder = 200;

        private Renderer[] _blockoutRenderers = Array.Empty<Renderer>();
        private bool[] _blockoutInitialStates = Array.Empty<bool>();
        private SpriteRenderer _baseRenderer;
        private SpriteRenderer _foregroundRenderer;
        private Sprite _runtimeBaseSprite;
        private Sprite _runtimeForegroundSprite;
        private Camera _camera;
        private int _cameraCullingMask = -1;
        private CharacterVisualBinding[] _characterBindings = Array.Empty<CharacterVisualBinding>();

        public bool HasBaseVisual => _baseRenderer != null && _baseRenderer.sprite != null;
        public bool HasForegroundVisual => _foregroundRenderer != null && _foregroundRenderer.sprite != null;
        public bool IsEnhancedPresentationActive { get; private set; }
        public int BlockoutRendererCount => _blockoutRenderers.Length;
        public float CharacterVisualScale => characterVisualScale;
        public Vector3 OfficeCenter => officeCenter;
        public Vector2 OfficeSize => officeSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForCurrentOfficeScene()
        {
            var office = GameObject.Find("FAMILY OFFICE V0.2");
            var characters = GameObject.Find("Characters");
            var playerController = FindFirstObjectByType<PrototypePlayerController>();
            if (office == null || characters == null || playerController == null) return;

            var presenter = office.GetComponent<OfficeVisualV2Presenter>();
            if (presenter == null) presenter = office.AddComponent<OfficeVisualV2Presenter>();
            presenter.Configure(
                playerController.transform,
                characters.transform,
                office.transform,
                new Vector3(14f, 0f, 0f),
                new Vector2(16f, 14f));

            var camera = Camera.main;
            if (camera == null) return;
            var follow = camera.GetComponent<IsometricCameraFollow>();
            if (follow != null)
            {
                follow.ConfigureOfficeFraming(new Vector3(14f, 0f, 0f), new Vector2(16f, 14f), 6.6f);
            }

            var pixelEffect = camera.GetComponent<PixelatedCameraEffect>();
            if (pixelEffect != null) pixelEffect.ConfigureAdaptive(360, 540);
        }

        public void Configure(
            Transform playerTransform,
            Transform characters,
            Transform authoredBlockout,
            Vector3 center,
            Vector2 size,
            float visualScale = DefaultCharacterScale)
        {
            player = playerTransform;
            characterRoot = characters;
            blockoutRoot = authoredBlockout;
            officeCenter = center;
            officeSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            characterVisualScale = Mathf.Max(0.1f, visualScale);
            CacheBlockoutRenderers();
            CacheCharacterBindings();
            ApplyCharacterPresentation();
        }

        private void Awake()
        {
            CacheBlockoutRenderers();
            CacheCharacterBindings();
            ApplyCharacterPresentation();
            LoadVisualAssets();
            SetEnhancedPresentation(false);
        }

        private void LateUpdate()
        {
            _camera = _camera != null ? _camera : Camera.main;
            var shouldEnhance = HasBaseVisual && IsOfficeViewActive(_camera);
            SetEnhancedPresentation(shouldEnhance);
            if (!shouldEnhance) return;
            AlignVisualPlanes(_camera);
            ApplyCharacterProjection(_camera);
        }

        private void OnDestroy()
        {
            if (_runtimeBaseSprite != null) Destroy(_runtimeBaseSprite);
            if (_runtimeForegroundSprite != null) Destroy(_runtimeForegroundSprite);
        }

        private void CacheBlockoutRenderers()
        {
            if (blockoutRoot == null) blockoutRoot = transform;
            var visualRoot = transform.Find("Office Visual V2");
            _blockoutRenderers = blockoutRoot
                .GetComponentsInChildren<Renderer>(true)
                .Where(item => item != null &&
                               (visualRoot == null || !item.transform.IsChildOf(visualRoot)))
                .ToArray();
            _blockoutInitialStates = _blockoutRenderers.Select(item => item.enabled).ToArray();
        }

        private void ApplyCharacterPresentation()
        {
            if (characterRoot == null) return;
            if (_characterBindings.Length == 0) CacheCharacterBindings();
            foreach (var binding in _characterBindings)
            {
                var renderer = binding.Renderer;
                if (renderer == null) continue;
                renderer.transform.localScale = Vector3.one * characterVisualScale;
                renderer.gameObject.layer = PresentationLayer;
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, characterSortingOrder);
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }

            foreach (var label in characterRoot.GetComponentsInChildren<TextMesh>(true))
            {
                var renderer = label.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.gameObject.layer = PresentationLayer;
                    renderer.sortingOrder = labelSortingOrder;
                }
            }
        }

        public void SetCharacterVisualScaleForQa(float value)
        {
            characterVisualScale = Mathf.Max(0.1f, value);
            ApplyCharacterPresentation();
        }

        public bool TryGetCharacterArtFootPixel(
            DirectionalSpriteAnimator animator,
            Camera viewCamera,
            out Vector2 artPixel)
        {
            artPixel = default;
            if (animator == null || viewCamera == null) return false;
            var binding = _characterBindings.FirstOrDefault(item => item.Animator == animator);
            if (binding == null || binding.Renderer == null) return false;
            var viewport = viewCamera.WorldToViewportPoint(binding.Renderer.transform.position);
            if (viewport.z <= 0f) return false;
            artPixel = OfficeVisualV2Calibration.ViewportToArtPixel(viewport, viewCamera.aspect);
            return true;
        }

        private void CacheCharacterBindings()
        {
            if (characterRoot == null)
            {
                _characterBindings = Array.Empty<CharacterVisualBinding>();
                return;
            }

            var bindings = new List<CharacterVisualBinding>();
            foreach (var animator in characterRoot.GetComponentsInChildren<DirectionalSpriteAnimator>(true))
            {
                var renderer = animator.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer == null) continue;
                var labels = animator.GetComponentsInChildren<TextMesh>(true)
                    .Select(item => item.transform)
                    .ToArray();
                bindings.Add(new CharacterVisualBinding(
                    animator,
                    renderer,
                    labels,
                    labels.Select(item => item.localPosition).ToArray()));
            }

            _characterBindings = bindings.ToArray();
        }

        private void ApplyCharacterProjection(Camera viewCamera)
        {
            if (viewCamera == null) return;
            foreach (var binding in _characterBindings)
            {
                if (binding.Animator == null || binding.Renderer == null) continue;
                var root = binding.Animator.transform;
                var originalFootWorld = root.TransformPoint(binding.OriginalVisualLocalPosition);
                var originalFootViewport = viewCamera.WorldToViewportPoint(originalFootWorld);
                if (originalFootViewport.z <= 0f) continue;

                var agent = root.GetComponent<OfficeWorkerAgent>();
                var artPixel = agent == null
                    ? OfficeVisualV2Calibration.WorldToArtPixel(root.position)
                    : agent.ResolveVisualArtPixel();
                var targetViewport = OfficeVisualV2Calibration.ArtPixelToViewport(artPixel, viewCamera.aspect);
                targetViewport.y = Mathf.Clamp(targetViewport.y, -0.25f, 1.25f);
                var projectedFoot = viewCamera.ViewportToWorldPoint(new Vector3(
                    targetViewport.x,
                    targetViewport.y,
                    originalFootViewport.z));
                binding.Renderer.transform.position = projectedFoot;

                for (var index = 0; index < binding.Labels.Length; index++)
                {
                    var label = binding.Labels[index];
                    if (label == null) continue;
                    var originalLabelWorld = root.TransformPoint(binding.OriginalLabelLocalPositions[index]);
                    var originalLabelViewport = viewCamera.WorldToViewportPoint(originalLabelWorld);
                    var labelViewport = new Vector3(
                        targetViewport.x + originalLabelViewport.x - originalFootViewport.x,
                        targetViewport.y + originalLabelViewport.y - originalFootViewport.y,
                        originalLabelViewport.z);
                    label.position = viewCamera.ViewportToWorldPoint(labelViewport);
                }
            }
        }

        private void RestoreCharacterProjection()
        {
            foreach (var binding in _characterBindings)
            {
                if (binding.Renderer != null)
                    binding.Renderer.transform.localPosition = binding.OriginalVisualLocalPosition;
                for (var index = 0; index < binding.Labels.Length; index++)
                {
                    if (binding.Labels[index] != null)
                        binding.Labels[index].localPosition = binding.OriginalLabelLocalPositions[index];
                }
            }
        }

        private void LoadVisualAssets()
        {
            var baseSprite = LoadSprite(BaseCandidates, false, out _runtimeBaseSprite);
            if (baseSprite == null) return;

            var visualRoot = new GameObject("Office Visual V2");
            visualRoot.transform.SetParent(transform, false);
            _baseRenderer = CreateLayer("Office Base", visualRoot.transform, baseSprite, baseSortingOrder);

            var foregroundSprite = LoadSprite(ForegroundCandidates, true, out _runtimeForegroundSprite);
            if (foregroundSprite != null)
            {
                _foregroundRenderer = CreateLayer(
                    "Office Foreground",
                    visualRoot.transform,
                    foregroundSprite,
                    foregroundSortingOrder);
            }
        }

        private static SpriteRenderer CreateLayer(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var layer = new GameObject(name);
            layer.layer = PresentationLayer;
            layer.transform.SetParent(parent, false);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.enabled = false;
            return renderer;
        }

        private static Sprite LoadSprite(string[] candidates, bool foreground, out Sprite runtimeSprite)
        {
            runtimeSprite = null;
            foreach (var path in candidates)
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null && sprite.texture != null)
                {
                    runtimeSprite = CreateCroppedSprite(sprite.texture, foreground);
                    return runtimeSprite;
                }

                var texture = Resources.Load<Texture2D>(path);
                if (texture == null) continue;
                runtimeSprite = CreateCroppedSprite(texture, foreground);
                return runtimeSprite;
            }

            var sprites = Resources.LoadAll<Sprite>(ResourceRoot);
            var fallback = sprites.FirstOrDefault(item => IsLayerName(item.name, foreground));
            if (fallback != null && fallback.texture != null)
            {
                runtimeSprite = CreateCroppedSprite(fallback.texture, foreground);
                return runtimeSprite;
            }

            var textures = Resources.LoadAll<Texture2D>(ResourceRoot);
            var fallbackTexture = textures.FirstOrDefault(item => IsLayerName(item.name, foreground));
            if (fallbackTexture == null) return null;
            runtimeSprite = CreateCroppedSprite(fallbackTexture, foreground);
            return runtimeSprite;
        }

        private static Sprite CreateCroppedSprite(Texture2D texture, bool foreground)
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var cropWidth = Mathf.Min(OfficeVisualV2Calibration.ArtCropWidth, texture.width);
            var cropHeight = Mathf.Min(OfficeVisualV2Calibration.ArtCropHeight, texture.height);
            var cropX = Mathf.Clamp(OfficeVisualV2Calibration.ArtCropLeft, 0f, texture.width - cropWidth);
            var cropY = Mathf.Clamp(
                texture.height - OfficeVisualV2Calibration.ArtCropTop - cropHeight,
                0f,
                texture.height - cropHeight);
            var sprite = Sprite.Create(
                texture,
                new Rect(cropX, cropY, cropWidth, cropHeight),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name + (foreground ? " Runtime Foreground Crop" : " Runtime Base Crop");
            return sprite;
        }

        private static bool IsLayerName(string name, bool foreground)
        {
            var normalized = (name ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("guide")) return false;
            return foreground
                ? normalized.Contains("foreground") || normalized.EndsWith("_fg", StringComparison.Ordinal)
                : normalized.Contains("base") && !normalized.Contains("foreground");
        }

        private bool IsOfficeViewActive(Camera viewCamera)
        {
            if (player != null && ContainsOfficePoint(player.position)) return true;
            if (viewCamera == null) return false;
            var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
            var distance = (officeCenter.y - ray.origin.y) / ray.direction.y;
            return distance > 0f && ContainsOfficePoint(ray.origin + ray.direction * distance);
        }

        private bool ContainsOfficePoint(Vector3 point)
        {
            var halfWidth = officeSize.x * 0.5f;
            var halfDepth = officeSize.y * 0.5f;
            return point.x >= officeCenter.x - halfWidth && point.x <= officeCenter.x + halfWidth &&
                   point.z >= officeCenter.z - halfDepth && point.z <= officeCenter.z + halfDepth;
        }

        private void SetEnhancedPresentation(bool enabled)
        {
            if (IsEnhancedPresentationActive == enabled &&
                (_baseRenderer == null || _baseRenderer.enabled == enabled)) return;

            // Bootstrap can create the home/street presentation after this component's Awake.
            // Re-scan once when the office art first takes over so no late blockout renderer can
            // occlude the calibrated full-screen base. Colliders and behaviours remain untouched.
            if (enabled && !IsEnhancedPresentationActive) CacheBlockoutRenderers();

            IsEnhancedPresentationActive = enabled;
            if (_camera != null)
            {
                if (enabled)
                {
                    if (_cameraCullingMask < 0) _cameraCullingMask = _camera.cullingMask;
                    _camera.cullingMask = 1 << PresentationLayer;
                }
                else if (_cameraCullingMask >= 0)
                {
                    _camera.cullingMask = _cameraCullingMask;
                    _cameraCullingMask = -1;
                }
            }
            for (var index = 0; index < _blockoutRenderers.Length; index++)
            {
                var renderer = _blockoutRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = enabled ? false : _blockoutInitialStates[index];
                }
            }

            if (_baseRenderer != null) _baseRenderer.enabled = enabled;
            if (_foregroundRenderer != null) _foregroundRenderer.enabled = enabled;
            if (!enabled) RestoreCharacterProjection();
        }

        private void AlignVisualPlanes(Camera viewCamera)
        {
            var focus = officeCenter + Vector3.up * projectionCenterHeight;
            var planePosition = focus + viewCamera.transform.forward * projectionDepth;
            var planeRotation = viewCamera.transform.rotation;
            var visibleHeight = viewCamera.orthographicSize * 2f;
            // Both supported office targets are 16:9. Batchmode can report a stale 4:3
            // GameView aspect before a 16:9 RenderTexture capture, which previously left
            // large teal side bars even though the actual output was 1920x1080.
            var visibleWidth = visibleHeight * TargetAspect;
            AlignLayer(_baseRenderer, planePosition, planeRotation, visibleWidth, visibleHeight);
            AlignLayer(_foregroundRenderer, planePosition, planeRotation, visibleWidth, visibleHeight);
        }

        private static void AlignLayer(
            SpriteRenderer renderer,
            Vector3 position,
            Quaternion rotation,
            float targetWidth,
            float targetHeight)
        {
            if (renderer == null || renderer.sprite == null) return;
            renderer.transform.position = position;
            renderer.transform.rotation = rotation;
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                targetWidth / Mathf.Max(0.001f, size.x),
                targetHeight / Mathf.Max(0.001f, size.y),
                1f);
        }

        private sealed class CharacterVisualBinding
        {
            public CharacterVisualBinding(
                DirectionalSpriteAnimator animator,
                SpriteRenderer renderer,
                Transform[] labels,
                Vector3[] originalLabelLocalPositions)
            {
                Animator = animator;
                Renderer = renderer;
                OriginalVisualLocalPosition = renderer.transform.localPosition;
                Labels = labels ?? Array.Empty<Transform>();
                OriginalLabelLocalPositions = originalLabelLocalPositions ?? Array.Empty<Vector3>();
            }

            public DirectionalSpriteAnimator Animator { get; }
            public SpriteRenderer Renderer { get; }
            public Vector3 OriginalVisualLocalPosition { get; }
            public Transform[] Labels { get; }
            public Vector3[] OriginalLabelLocalPositions { get; }
        }
    }
}

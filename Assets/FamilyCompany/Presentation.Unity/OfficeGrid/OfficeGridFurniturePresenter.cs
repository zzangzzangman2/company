using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridFurniturePresenter : MonoBehaviour
    {
        private sealed class FurnitureVisual
        {
            public PlacedOfficeFurniture Furniture;
            public OfficeFurnitureVisualDefinition Definition;
            public Transform SemanticRoot;
            public Transform VisualRoot;
            public Transform SemanticParent;
            public Vector3 AuthoredSemanticLocalPosition;
            public Quaternion AuthoredSemanticLocalRotation;
            public Vector3 AuthoredSemanticLocalScale;
            public Vector3 AuthoredSemanticWorldPosition;
            public Quaternion AuthoredSemanticWorldRotation;
            public Vector3 AuthoredSemanticWorldScale;
            public Transform VisualParent;
            public Vector3 AuthoredVisualLocalPosition;
            public Quaternion AuthoredVisualLocalRotation;
            public Vector3 AuthoredVisualLocalScale;
            public Vector3 AuthoredVisualWorldPosition;
            public Quaternion AuthoredVisualWorldRotation;
            public Vector3 AuthoredVisualWorldScale;
            public SpriteRenderer BaseRenderer;
            public SpriteRenderer FrontRenderer;
            public SpriteRenderer OccupiedLowerBodyRenderer;
        }

        private sealed class RuntimeForegroundSprite
        {
            public Sprite Sprite;
            public Vector3 LocalPosition;
        }

        private readonly Dictionary<string, FurnitureVisual> _visuals =
            new Dictionary<string, FurnitureVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _renderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlacedOfficeFurniture> _furniture =
            new Dictionary<string, PlacedOfficeFurniture>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _frontOverlays =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _occupiedChairLowerBodyOverlays =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<Sprite, RuntimeForegroundSprite> _occupiedChairForegrounds =
            new Dictionary<Sprite, RuntimeForegroundSprite>();
        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeFurnitureVisualCatalog _visualCatalog;
        private Bounds _renderBounds;

        private const float TransformPositionTolerance = 0.000001f;
        private const float TransformRotationToleranceDegrees = 0.0001f;
        private const float TransformScaleTolerance = 0.000001f;

        public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;
        public IReadOnlyDictionary<string, SpriteRenderer> FrontOverlayRenderers => _frontOverlays;
        public IReadOnlyDictionary<string, SpriteRenderer> OccupiedChairLowerBodyRenderers =>
            _occupiedChairLowerBodyOverlays;
        public Bounds RenderBounds => _renderBounds;
        public OfficeFurnitureVisualCatalog VisualCatalog => _visualCatalog;
        public int TransformInvariantViolationCount { get; private set; }

        public void Configure(
            OfficeGrid semanticGrid,
            OfficeGridTilemapPresenter gridPresenter,
            OfficeFurnitureVisualCatalog visualCatalog)
        {
            _semanticGrid = semanticGrid ?? throw new ArgumentNullException(nameof(semanticGrid));
            _gridPresenter = gridPresenter ?? throw new ArgumentNullException(nameof(gridPresenter));
            _visualCatalog = visualCatalog ?? throw new ArgumentNullException(nameof(visualCatalog));
            visualCatalog.Validate();

            ClearGenerated();
            TransformInvariantViolationCount = 0;
            bool hasBounds = false;
            foreach (PlacedOfficeFurniture item in semanticGrid.Furniture)
            {
                if (!OfficeBuildFurnitureVisualLibrary.TryResolve(
                        visualCatalog,
                        item.KindId, item.Facing, out OfficeFurnitureVisualDefinition definition, out bool flipX))
                    throw new InvalidOperationException(
                        $"Furniture visual '{item.KindId}/{item.Facing}' has neither authored nor mirrored art.");
                var root = new GameObject("Furniture_" + item.FurnitureId);
                root.transform.SetParent(transform, false);
                root.transform.position = _gridPresenter.SubcellAnchorWorld(item.PlacementAnchor);
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var visualRootObject = new GameObject("VisualRoot");
                visualRootObject.transform.SetParent(root.transform, false);
                Vector3 exteriorOffset = OfficePerimeterExteriorGeometry.VisualOffsetWorld(
                    item,
                    semanticGrid,
                    gridPresenter);
                visualRootObject.transform.localPosition =
                    root.transform.InverseTransformVector(exteriorOffset);
                visualRootObject.transform.localRotation = Quaternion.identity;
                visualRootObject.transform.localScale = Vector3.one * definition.UniformScale;

                var baseRoot = new GameObject("BaseVisual");
                baseRoot.transform.SetParent(visualRootObject.transform, false);
                baseRoot.transform.localPosition = Vector3.zero;
                baseRoot.transform.localRotation = Quaternion.identity;
                baseRoot.transform.localScale = Vector3.one;
                var baseRenderer = baseRoot.AddComponent<SpriteRenderer>();
                baseRenderer.sprite = definition.BaseSprite;
                baseRenderer.flipX = flipX;
                baseRenderer.sortingLayerName = "Default";
                Vector3 sortAnchorWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    baseRenderer,
                    definition.SortAnchorPx);
                baseRenderer.sortingOrder = OfficeGridCharacterMover.ResolveDynamicSortingOrder(sortAnchorWorld);

                SpriteRenderer frontRenderer = null;
                if (definition.FrontOverlaySprite != null)
                {
                    var frontRoot = new GameObject("FrontOverlay");
                    frontRoot.transform.SetParent(visualRootObject.transform, false);
                    frontRoot.transform.localPosition = Vector3.zero;
                    frontRoot.transform.localRotation = Quaternion.identity;
                    frontRoot.transform.localScale = Vector3.one;
                    frontRenderer = frontRoot.AddComponent<SpriteRenderer>();
                    frontRenderer.sprite = definition.FrontOverlaySprite;
                    frontRenderer.flipX = flipX;
                    frontRenderer.sortingLayerName = "Default";
                    frontRenderer.sortingOrder = baseRenderer.sortingOrder + 1;
                    // The front sprite also contains the visible chair back/near edge, so hiding
                    // it makes an empty chair appear to vanish. Occupancy changes its depth, not
                    // whether the authored furniture piece exists.
                    frontRenderer.enabled = definition.FrontOverlayWhenOccupied;
                    _frontOverlays.Add(item.FurnitureId, frontRenderer);
                }

                var visual = new FurnitureVisual
                {
                    Furniture = item,
                    Definition = definition,
                    SemanticRoot = root.transform,
                    VisualRoot = visualRootObject.transform,
                    SemanticParent = root.transform.parent,
                    AuthoredSemanticLocalPosition = root.transform.localPosition,
                    AuthoredSemanticLocalRotation = root.transform.localRotation,
                    AuthoredSemanticLocalScale = root.transform.localScale,
                    AuthoredSemanticWorldPosition = root.transform.position,
                    AuthoredSemanticWorldRotation = root.transform.rotation,
                    AuthoredSemanticWorldScale = root.transform.lossyScale,
                    VisualParent = visualRootObject.transform.parent,
                    AuthoredVisualLocalPosition = visualRootObject.transform.localPosition,
                    AuthoredVisualLocalRotation = visualRootObject.transform.localRotation,
                    AuthoredVisualLocalScale = visualRootObject.transform.localScale,
                    AuthoredVisualWorldPosition = visualRootObject.transform.position,
                    AuthoredVisualWorldRotation = visualRootObject.transform.rotation,
                    AuthoredVisualWorldScale = visualRootObject.transform.lossyScale,
                    BaseRenderer = baseRenderer,
                    FrontRenderer = frontRenderer
                };
                _visuals.Add(item.FurnitureId, visual);
                _renderers.Add(item.FurnitureId, baseRenderer);
                _furniture.Add(item.FurnitureId, item);
                if (!hasBounds)
                {
                    _renderBounds = baseRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    _renderBounds.Encapsulate(baseRenderer.bounds);
                }
            }

            RecalculateRenderBounds();

            if (!hasBounds) _renderBounds = new Bounds(transform.position, Vector3.zero);
            if (!ValidateTransformInvariants(out string failure))
                throw new InvalidOperationException(failure);
        }

        public bool TryGetRenderer(string furnitureId, out SpriteRenderer renderer) =>
            _renderers.TryGetValue(furnitureId ?? string.Empty, out renderer);

        public bool TryGetFurniture(string furnitureId, out PlacedOfficeFurniture furniture) =>
            _furniture.TryGetValue(furnitureId ?? string.Empty, out furniture);

        public bool TryGetDefinition(string furnitureId, out OfficeFurnitureVisualDefinition definition)
        {
            if (_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual))
            {
                definition = visual.Definition;
                return true;
            }
            definition = null;
            return false;
        }

        public bool TryGetSemanticRoot(string furnitureId, out Transform semanticRoot)
        {
            if (_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual))
            {
                semanticRoot = visual.SemanticRoot;
                return true;
            }
            semanticRoot = null;
            return false;
        }

        public bool TryGetVisualRoot(string furnitureId, out Transform visualRoot)
        {
            if (_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual))
            {
                visualRoot = visual.VisualRoot;
                return true;
            }
            visualRoot = null;
            return false;
        }

        public Vector3 GroundAnchorWorld(string furnitureId) =>
            ResolveAnchorWorld(furnitureId, visual => visual.Definition.GroundAnchorPx);

        public Vector3 SortAnchorWorld(string furnitureId) =>
            ResolveAnchorWorld(furnitureId, visual => visual.Definition.SortAnchorPx);

        public Vector3 SeatAnchorWorld(string furnitureId)
        {
            FurnitureVisual visual = RequiredVisual(furnitureId);
            if (!visual.Definition.HasSeatAnchor)
                throw new InvalidOperationException("Furniture has no seat anchor: " + furnitureId);
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(visual.BaseRenderer, visual.Definition.SeatAnchorPx);
        }

        public Vector3 WorkSurfaceAnchorWorld(string furnitureId)
        {
            return OperatorWorkSocketWorld(furnitureId);
        }

        public Vector3 OperatorSeatSocketWorld(string furnitureId)
        {
            FurnitureVisual visual = RequiredVisual(furnitureId);
            if (!visual.Definition.HasOperatorSeatSocket)
                throw new InvalidOperationException("Furniture has no operator-seat socket: " + furnitureId);
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                visual.BaseRenderer,
                visual.Definition.OperatorSeatSocketPx);
        }

        public Vector3 OperatorWorkSocketWorld(string furnitureId)
        {
            FurnitureVisual visual = RequiredVisual(furnitureId);
            if (!visual.Definition.HasOperatorWorkSocket)
                throw new InvalidOperationException("Furniture has no operator-work socket: " + furnitureId);
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(visual.BaseRenderer, visual.Definition.OperatorWorkSocketPx);
        }

        public Vector3[] GroundFootprintWorld(string furnitureId)
        {
            FurnitureVisual visual = RequiredVisual(furnitureId);
            var points = new Vector3[visual.Definition.GroundFootprintPolygonPx.Count];
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    visual.BaseRenderer,
                    visual.Definition.GroundFootprintPolygonPx[index]);
            }
            return points;
        }

        /// <summary>
        /// True when this piece has a foreground layer that should be painted over an occupant -
        /// the chair backrest and near armrest, which the camera sees in front of the seated body.
        /// </summary>
        public bool HasEnabledFrontOverlay(string furnitureId)
        {
            return _visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual) &&
                   ((visual.FrontRenderer != null && visual.FrontRenderer.enabled) ||
                    (visual.OccupiedLowerBodyRenderer != null &&
                     visual.OccupiedLowerBodyRenderer.enabled));
        }

        /// <summary>
        /// Sets the order for the main body of one piece of furniture. The single owner of this is
        /// <see cref="OfficeRuntime.OfficeRuntimeDepthSorter"/>, which orders the whole office from
        /// its footprints once per frame.
        /// </summary>
        public void ApplyBaseSortingOrder(string furnitureId, int sortingOrder)
        {
            if (!_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual)) return;
            visual.BaseRenderer.sortingOrder = sortingOrder;
            if (visual.FrontRenderer != null && !visual.Definition.FrontOverlayWhenOccupied)
            {
                visual.FrontRenderer.enabled = false;
                visual.FrontRenderer.sortingOrder = sortingOrder + 1;
            }
        }

        /// <summary>Sets the order for the foreground layer, which the sorter keeps above occupants.</summary>
        public void ApplyFrontSortingOrder(string furnitureId, int sortingOrder)
        {
            if (!_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual)) return;
            if (visual.FrontRenderer == null) return;
            bool lowerBodyOnly = visual.OccupiedLowerBodyRenderer != null &&
                                 visual.OccupiedLowerBodyRenderer.enabled;
            visual.FrontRenderer.enabled = !lowerBodyOnly;
            visual.FrontRenderer.sortingOrder = sortingOrder;
            if (lowerBodyOnly)
                visual.OccupiedLowerBodyRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Keeps the authored foreground assigned for released/empty presentation and uses only the
        /// canonical seat-rim crop in front while the pose upper-body protection plane is engaged.
        /// </summary>
        public void ApplyOccupiedChairForeground(string furnitureId, bool foregroundEngaged)
        {
            if (!_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual) ||
                visual.FrontRenderer == null ||
                !string.Equals(
                    visual.Furniture.KindId,
                    OfficeGridLayouts.SwivelChairKind,
                    StringComparison.Ordinal)) return;

            visual.FrontRenderer.sprite = visual.Definition.FrontOverlaySprite;
            visual.FrontRenderer.transform.localPosition = Vector3.zero;
            if (!foregroundEngaged)
            {
                if (visual.OccupiedLowerBodyRenderer != null)
                    visual.OccupiedLowerBodyRenderer.enabled = false;
                visual.FrontRenderer.enabled = visual.Definition.FrontOverlayWhenOccupied;
                return;
            }

            EnsureOccupiedLowerBodyRenderer(visual);
            visual.OccupiedLowerBodyRenderer.enabled = true;
            visual.OccupiedLowerBodyRenderer.sortingOrder = visual.FrontRenderer.sortingOrder;
            // The authored chair foreground is a rectangular crop of the base Sprite. Drawing it
            // over an occupant creates a ruler-straight seam through hair, torso and feet. The
            // complete chair remains on the immutable base plane behind the actor; only the
            // authored seat-rim crop is allowed in front while occupied.
            visual.FrontRenderer.enabled = false;
        }

        public void ApplySeatOcclusion(OfficeSeatSlot seat, int characterSortingOrder)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            ApplyOccupiedChairForeground(seat.ChairFurnitureId, true);
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            chair.BaseRenderer.sortingOrder = characterSortingOrder - 1;
            if (chair.FrontRenderer != null)
            {
                chair.FrontRenderer.enabled = false;
                chair.FrontRenderer.sortingOrder = characterSortingOrder + 2;
                if (chair.OccupiedLowerBodyRenderer != null)
                    chair.OccupiedLowerBodyRenderer.sortingOrder = characterSortingOrder + 2;
            }

            if (!seat.HasWorkstationBinding) return;
            FurnitureVisual desk = RequiredVisual(seat.WorkSurfaceFurnitureId);
            desk.BaseRenderer.sortingOrder = characterSortingOrder - 2;
            if (desk.FrontRenderer != null)
            {
                desk.FrontRenderer.enabled = desk.Definition.FrontOverlayWhenOccupied;
                desk.FrontRenderer.sortingOrder = characterSortingOrder + 1;
            }
        }

        public void ClearSeatOcclusion(OfficeSeatSlot seat)
        {
            if (seat == null) return;
            ApplyOccupiedChairForeground(seat.ChairFurnitureId, false);
            RestoreVisualSorting(RequiredVisual(seat.ChairFurnitureId));
            if (seat.HasWorkstationBinding) RestoreVisualSorting(RequiredVisual(seat.WorkSurfaceFurnitureId));
        }

        public bool SeatOcclusionMatches(OfficeSeatSlot seat, int characterSortingOrder)
        {
            if (seat == null) return false;
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            if (chair.BaseRenderer.sortingOrder != characterSortingOrder - 1) return false;
            if (chair.FrontRenderer != null && chair.FrontRenderer.enabled) return false;
            if (chair.OccupiedLowerBodyRenderer == null ||
                !chair.OccupiedLowerBodyRenderer.enabled ||
                chair.OccupiedLowerBodyRenderer.sortingOrder != characterSortingOrder + 2)
                return false;
            if (!seat.HasWorkstationBinding) return true;
            FurnitureVisual desk = RequiredVisual(seat.WorkSurfaceFurnitureId);
            if (desk.BaseRenderer.sortingOrder != characterSortingOrder - 2) return false;
            return desk.FrontRenderer == null ||
                   (desk.FrontRenderer.enabled == desk.Definition.FrontOverlayWhenOccupied &&
                    desk.FrontRenderer.sortingOrder == characterSortingOrder + 1);
        }

        /// <summary>
        /// Verifies that semantic placement and the rendered visual hierarchy still match the
        /// authored layout. Seating may change renderer order/visibility, never Transform state.
        /// </summary>
        public bool ValidateTransformInvariants(out string failure)
        {
            foreach (FurnitureVisual visual in _visuals.Values)
            {
                if (!Matches(
                        visual.SemanticRoot,
                        visual.SemanticParent,
                        visual.AuthoredSemanticLocalPosition,
                        visual.AuthoredSemanticLocalRotation,
                        visual.AuthoredSemanticLocalScale,
                        visual.AuthoredSemanticWorldPosition,
                        visual.AuthoredSemanticWorldRotation,
                        visual.AuthoredSemanticWorldScale))
                {
                    failure = "Furniture semantic Transform changed: " +
                              visual.Furniture.FurnitureId;
                    return false;
                }
                if (!Matches(
                        visual.VisualRoot,
                        visual.VisualParent,
                        visual.AuthoredVisualLocalPosition,
                        visual.AuthoredVisualLocalRotation,
                        visual.AuthoredVisualLocalScale,
                        visual.AuthoredVisualWorldPosition,
                        visual.AuthoredVisualWorldRotation,
                        visual.AuthoredVisualWorldScale))
                {
                    failure = "Furniture VisualRoot changed: " +
                              visual.Furniture.FurnitureId;
                    return false;
                }
                if (visual.SemanticRoot.GetComponent<Rigidbody>() != null ||
                    visual.SemanticRoot.GetComponent<Rigidbody2D>() != null ||
                    visual.SemanticRoot.GetComponent<Collider>() != null ||
                    visual.SemanticRoot.GetComponent<Collider2D>() != null ||
                    visual.SemanticRoot.GetComponent<Animator>() != null ||
                    visual.VisualRoot.GetComponent<Rigidbody>() != null ||
                    visual.VisualRoot.GetComponent<Rigidbody2D>() != null ||
                    visual.VisualRoot.GetComponent<Collider>() != null ||
                    visual.VisualRoot.GetComponent<Collider2D>() != null ||
                    visual.VisualRoot.GetComponent<Animator>() != null)
                {
                    failure = "Furniture Transform has a physics/Animator owner: " +
                              visual.Furniture.FurnitureId;
                    return false;
                }
            }
            failure = string.Empty;
            return true;
        }

        private void LateUpdate()
        {
            foreach (FurnitureVisual visual in _visuals.Values)
            {
                bool semanticValid = Matches(
                    visual.SemanticRoot,
                    visual.SemanticParent,
                    visual.AuthoredSemanticLocalPosition,
                    visual.AuthoredSemanticLocalRotation,
                    visual.AuthoredSemanticLocalScale,
                    visual.AuthoredSemanticWorldPosition,
                    visual.AuthoredSemanticWorldRotation,
                    visual.AuthoredSemanticWorldScale);
                bool presentationValid = Matches(
                    visual.VisualRoot,
                    visual.VisualParent,
                    visual.AuthoredVisualLocalPosition,
                    visual.AuthoredVisualLocalRotation,
                    visual.AuthoredVisualLocalScale,
                    visual.AuthoredVisualWorldPosition,
                    visual.AuthoredVisualWorldRotation,
                    visual.AuthoredVisualWorldScale);
                if (semanticValid && presentationValid) continue;
                TransformInvariantViolationCount++;
                RestoreTransform(
                    visual.SemanticRoot,
                    visual.SemanticParent,
                    visual.AuthoredSemanticLocalPosition,
                    visual.AuthoredSemanticLocalRotation,
                    visual.AuthoredSemanticLocalScale);
                RestoreTransform(
                    visual.VisualRoot,
                    visual.VisualParent,
                    visual.AuthoredVisualLocalPosition,
                    visual.AuthoredVisualLocalRotation,
                    visual.AuthoredVisualLocalScale);
                Debug.LogError(
                    "OFFICE_FURNITURE_TRANSFORM_INVARIANT_REPAIRED | furniture=" +
                    visual.Furniture.FurnitureId);
            }
        }

        private static bool Matches(
            Transform candidate,
            Transform expectedParent,
            Vector3 expectedLocalPosition,
            Quaternion expectedLocalRotation,
            Vector3 expectedLocalScale,
            Vector3 expectedWorldPosition,
            Quaternion expectedWorldRotation,
            Vector3 expectedWorldScale)
        {
            return candidate != null && candidate.parent == expectedParent &&
                   Vector3.Distance(candidate.localPosition, expectedLocalPosition) <=
                   TransformPositionTolerance &&
                   Quaternion.Angle(candidate.localRotation, expectedLocalRotation) <=
                   TransformRotationToleranceDegrees &&
                   Vector3.Distance(candidate.localScale, expectedLocalScale) <=
                   TransformScaleTolerance &&
                   Vector3.Distance(candidate.position, expectedWorldPosition) <=
                   TransformPositionTolerance &&
                   Quaternion.Angle(candidate.rotation, expectedWorldRotation) <=
                   TransformRotationToleranceDegrees &&
                   Vector3.Distance(candidate.lossyScale, expectedWorldScale) <=
                   TransformScaleTolerance;
        }

        private static void RestoreTransform(
            Transform target,
            Transform expectedParent,
            Vector3 expectedLocalPosition,
            Quaternion expectedLocalRotation,
            Vector3 expectedLocalScale)
        {
            if (target == null) return;
            if (target.parent != expectedParent) target.SetParent(expectedParent, false);
            target.localPosition = expectedLocalPosition;
            target.localRotation = expectedLocalRotation;
            target.localScale = expectedLocalScale;
        }

        private Vector3 ResolveAnchorWorld(string furnitureId, Func<FurnitureVisual, Vector2> anchor)
        {
            FurnitureVisual visual = RequiredVisual(furnitureId);
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(visual.BaseRenderer, anchor(visual));
        }

        private FurnitureVisual RequiredVisual(string furnitureId)
        {
            if (!_visuals.TryGetValue(furnitureId ?? string.Empty, out FurnitureVisual visual))
                throw new ArgumentException("Unknown furniture visual: " + furnitureId, nameof(furnitureId));
            return visual;
        }

        private void RestoreVisualSorting(FurnitureVisual visual)
        {
            Vector3 sortAnchorWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                visual.BaseRenderer,
                visual.Definition.SortAnchorPx);
            visual.BaseRenderer.sortingOrder = OfficeGridCharacterMover.ResolveDynamicSortingOrder(sortAnchorWorld);
            if (visual.FrontRenderer != null)
            {
                visual.FrontRenderer.enabled = visual.Definition.FrontOverlayWhenOccupied;
                visual.FrontRenderer.sortingOrder = visual.BaseRenderer.sortingOrder + 1;
            }
            if (visual.OccupiedLowerBodyRenderer != null)
                visual.OccupiedLowerBodyRenderer.enabled = false;
        }

        private void EnsureOccupiedLowerBodyRenderer(FurnitureVisual visual)
        {
            if (visual.OccupiedLowerBodyRenderer != null) return;
            Sprite baseSprite = visual.Definition.BaseSprite;
            if (!_occupiedChairForegrounds.TryGetValue(
                    baseSprite,
                    out RuntimeForegroundSprite occupiedForeground))
            {
                Rect textureRect =
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerTextureRect(baseSprite);
                Sprite sprite = Sprite.Create(
                    baseSprite.texture,
                    textureRect,
                    OfficeSeatedUpperBodyProtectionRules.ChairLowerNormalizedPivot(baseSprite),
                    baseSprite.pixelsPerUnit,
                    0u,
                    SpriteMeshType.FullRect,
                    Vector4.zero);
                sprite.name = baseSprite.name + "_occupied_lower_body_runtime";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                occupiedForeground = new RuntimeForegroundSprite
                {
                    Sprite = sprite,
                    LocalPosition =
                        OfficeSeatedUpperBodyProtectionRules.ChairLowerLocalPosition(baseSprite)
                };
                _occupiedChairForegrounds.Add(baseSprite, occupiedForeground);
            }

            var lowerBodyRoot = new GameObject("OccupiedLowerBodyOverlay");
            lowerBodyRoot.transform.SetParent(visual.VisualRoot, false);
            lowerBodyRoot.transform.localPosition = occupiedForeground.LocalPosition;
            lowerBodyRoot.transform.localRotation = Quaternion.identity;
            lowerBodyRoot.transform.localScale = Vector3.one;
            SpriteRenderer renderer = lowerBodyRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = occupiedForeground.Sprite;
            renderer.flipX = visual.BaseRenderer.flipX;
            renderer.sortingLayerID = visual.FrontRenderer.sortingLayerID;
            renderer.enabled = false;
            visual.OccupiedLowerBodyRenderer = renderer;
            _occupiedChairLowerBodyOverlays[visual.Furniture.FurnitureId] = renderer;
        }

        private void RecalculateRenderBounds()
        {
            bool hasBounds = false;
            foreach (FurnitureVisual visual in _visuals.Values)
            {
                if (!hasBounds)
                {
                    _renderBounds = visual.BaseRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    _renderBounds.Encapsulate(visual.BaseRenderer.bounds);
                }
            }
            if (!hasBounds) _renderBounds = new Bounds(transform.position, Vector3.zero);
        }

        private void ClearGenerated()
        {
            _visuals.Clear();
            _renderers.Clear();
            _furniture.Clear();
            _frontOverlays.Clear();
            _occupiedChairLowerBodyOverlays.Clear();
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject child = transform.GetChild(index).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            DestroyRuntimeForegroundSprites();
        }

        private void OnDestroy()
        {
            DestroyRuntimeForegroundSprites();
        }

        private void DestroyRuntimeForegroundSprites()
        {
            foreach (RuntimeForegroundSprite runtime in _occupiedChairForegrounds.Values)
            {
                if (runtime?.Sprite == null) continue;
                if (Application.isPlaying) Destroy(runtime.Sprite);
                else DestroyImmediate(runtime.Sprite);
            }
            _occupiedChairForegrounds.Clear();
        }
    }
}

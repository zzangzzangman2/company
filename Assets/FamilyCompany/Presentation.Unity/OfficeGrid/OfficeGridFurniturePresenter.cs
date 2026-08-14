using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.OfficeSeating;
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
            public Vector3 AuthoredVisualLocalPosition;
            public SpriteRenderer BaseRenderer;
            public SpriteRenderer FrontRenderer;
            public SpriteRenderer OccupiedLowerBodyRenderer;
            public SpriteRenderer OccupiedBackFrameRenderer;
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
        private readonly Dictionary<Sprite, RuntimeForegroundSprite> _occupiedChairBackFrames =
            new Dictionary<Sprite, RuntimeForegroundSprite>();
        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeFurnitureVisualCatalog _visualCatalog;
        private Bounds _renderBounds;

        public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;
        public IReadOnlyDictionary<string, SpriteRenderer> FrontOverlayRenderers => _frontOverlays;
        public IReadOnlyDictionary<string, SpriteRenderer> OccupiedChairLowerBodyRenderers =>
            _occupiedChairLowerBodyOverlays;
        public Bounds RenderBounds => _renderBounds;
        public OfficeFurnitureVisualCatalog VisualCatalog => _visualCatalog;

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
                    AuthoredVisualLocalPosition = visualRootObject.transform.localPosition,
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

            ApplyAuthoredWorkstationChairBindings(semanticGrid);
            RecalculateRenderBounds();

            if (!hasBounds) _renderBounds = new Bounds(transform.position, Vector3.zero);
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

        /// <summary>
        /// Compatibility hook for seating callers. Chairs are authored world furniture and must
        /// stay on their semantic root; occupant motion can never translate their presentation.
        /// </summary>
        public void AlignSeatPresentationToWorld(OfficeSeatSlot seat, Vector3 desiredSeatWorld)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            chair.VisualRoot.localPosition = chair.AuthoredVisualLocalPosition;
            _ = desiredSeatWorld;
        }

        public void RestoreSeatPresentation(OfficeSeatSlot seat)
        {
            if (seat == null) return;
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            chair.VisualRoot.localPosition = chair.AuthoredVisualLocalPosition;
        }

        /// <summary>
        /// Applies the approved Starter Office member-to-seat calibration once, before the first
        /// actor is created or rendered. The resulting chair transform becomes its authored fixed
        /// position and is never updated from occupant motion.
        /// </summary>
        public void CalibrateFixedWorkstationChairs(OfficeCharacterSeatPoseCatalog poseCatalog)
        {
            if (poseCatalog == null) throw new ArgumentNullException(nameof(poseCatalog));
            foreach (OfficeSeatSlot seat in _semanticGrid.SeatSlots)
            {
                if (!seat.HasWorkstationBinding) continue;
                string memberId = MemberIdFromSeat(seat.SeatId);
                if (memberId.Length == 0) continue;

                OfficeCharacterSeatPoseProfile profile = poseCatalog.ResolveApproved(
                    memberId,
                    FacingDirection(seat.Facing),
                    OfficeSeatingAnimationClip.Work,
                    0);
                if (Mathf.Abs(profile.RotationDegrees) > 0.01f ||
                    Mathf.Abs(profile.UniformScale - 1f) > 0.0001f)
                    throw new InvalidOperationException(
                        "Fixed chair calibration requires an unrotated unit-scale pose: " + memberId);

                FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
                FurnitureVisual desk = RequiredVisual(seat.WorkSurfaceFurnitureId);
                Vector2 pelvisToHandPx = profile.HandAnchorPx - profile.PelvisAnchorPx;
                float actorScale = OfficeGridCharacterMover.UniformVisualScale;
                Vector3 pelvisToHandWorld = new Vector3(
                    pelvisToHandPx.x * actorScale / OfficeGridTilemapPresenter.PixelsPerUnit,
                    pelvisToHandPx.y * actorScale / OfficeGridTilemapPresenter.PixelsPerUnit,
                    0f);
                Vector3 fixedSeatWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    desk.BaseRenderer,
                    desk.Definition.OperatorWorkSocketPx) - pelvisToHandWorld;
                Vector3 currentSeatWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    chair.BaseRenderer,
                    chair.Definition.SeatAnchorPx);
                chair.VisualRoot.position += fixedSeatWorld - currentSeatWorld;
                chair.AuthoredVisualLocalPosition = chair.VisualRoot.localPosition;
                UpdateChairAuthoredSorting(chair);
            }

            RecalculateRenderBounds();
        }

        private void ApplyAuthoredWorkstationChairBindings(OfficeGrid semanticGrid)
        {
            foreach (OfficeSeatSlot seat in semanticGrid.SeatSlots)
            {
                if (!seat.HasWorkstationBinding) continue;
                FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
                FurnitureVisual desk = RequiredVisual(seat.WorkSurfaceFurnitureId);
                if (!chair.Definition.HasSeatAnchor || !desk.Definition.HasOperatorSeatSocket)
                    throw new InvalidOperationException(
                        "Bound workstation is missing its authored chair/desk seat sockets: " + seat.SeatId);

                Vector3 chairSeatWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    chair.BaseRenderer,
                    chair.Definition.SeatAnchorPx);
                Vector3 deskSeatWorld = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    desk.BaseRenderer,
                    desk.Definition.OperatorSeatSocketPx);
                chair.VisualRoot.position += deskSeatWorld - chairSeatWorld;
                chair.AuthoredVisualLocalPosition = chair.VisualRoot.localPosition;

                UpdateChairAuthoredSorting(chair);
            }
        }

        private static void UpdateChairAuthoredSorting(FurnitureVisual chair)
        {
            chair.BaseRenderer.sortingOrder = OfficeGridCharacterMover.ResolveDynamicSortingOrder(
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    chair.BaseRenderer,
                    chair.Definition.SortAnchorPx));
            if (chair.FrontRenderer != null)
                chair.FrontRenderer.sortingOrder = chair.BaseRenderer.sortingOrder + 1;
        }

        private static string MemberIdFromSeat(string seatId)
        {
            const string prefix = "seat_";
            return seatId != null && seatId.StartsWith(prefix, StringComparison.Ordinal)
                ? seatId.Substring(prefix.Length)
                : string.Empty;
        }

        private static int FacingDirection(OfficeFurnitureFacing facing)
        {
            return facing switch
            {
                OfficeFurnitureFacing.SouthEast => 7,
                OfficeFurnitureFacing.SouthWest => 1,
                OfficeFurnitureFacing.NorthWest => 3,
                OfficeFurnitureFacing.NorthEast => 5,
                _ => 4
            };
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
                   visual.FrontRenderer != null &&
                   visual.FrontRenderer.enabled;
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
            visual.FrontRenderer.enabled = true;
            visual.FrontRenderer.sortingOrder = sortingOrder;
            if (visual.OccupiedLowerBodyRenderer != null &&
                visual.OccupiedLowerBodyRenderer.enabled)
                visual.OccupiedLowerBodyRenderer.sortingOrder = sortingOrder;
            if (visual.OccupiedBackFrameRenderer != null &&
                visual.OccupiedBackFrameRenderer.enabled)
                visual.OccupiedBackFrameRenderer.sortingOrder = sortingOrder + 2;
        }

        /// <summary>
        /// Keeps the complete authored foreground assigned and adds the canonical seat-rim crop
        /// only while the pose upper-body protection plane is engaged.
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
                if (visual.OccupiedBackFrameRenderer != null)
                    visual.OccupiedBackFrameRenderer.enabled = false;
                return;
            }

            EnsureOccupiedLowerBodyRenderer(visual);
            EnsureOccupiedBackFrameRenderer(visual);
            visual.OccupiedLowerBodyRenderer.enabled = true;
            visual.OccupiedLowerBodyRenderer.sortingOrder = visual.FrontRenderer.sortingOrder;
            visual.OccupiedBackFrameRenderer.enabled = true;
            visual.OccupiedBackFrameRenderer.sortingOrder = visual.FrontRenderer.sortingOrder + 2;
        }

        public void ApplySeatOcclusion(OfficeSeatSlot seat, int characterSortingOrder)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            ApplyOccupiedChairForeground(seat.ChairFurnitureId, true);
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            chair.BaseRenderer.sortingOrder = characterSortingOrder - 1;
            if (chair.FrontRenderer != null)
            {
                chair.FrontRenderer.enabled = chair.Definition.FrontOverlayWhenOccupied;
                chair.FrontRenderer.sortingOrder = characterSortingOrder + 2;
                if (chair.OccupiedLowerBodyRenderer != null)
                    chair.OccupiedLowerBodyRenderer.sortingOrder = characterSortingOrder + 2;
                if (chair.OccupiedBackFrameRenderer != null)
                    chair.OccupiedBackFrameRenderer.sortingOrder = characterSortingOrder + 4;
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
            RestoreSeatPresentation(seat);
            RestoreVisualSorting(RequiredVisual(seat.ChairFurnitureId));
            if (seat.HasWorkstationBinding) RestoreVisualSorting(RequiredVisual(seat.WorkSurfaceFurnitureId));
        }

        public bool SeatOcclusionMatches(OfficeSeatSlot seat, int characterSortingOrder)
        {
            if (seat == null) return false;
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            if (chair.BaseRenderer.sortingOrder != characterSortingOrder - 1) return false;
            if (chair.FrontRenderer != null &&
                (chair.FrontRenderer.enabled != chair.Definition.FrontOverlayWhenOccupied ||
                 chair.FrontRenderer.sortingOrder != characterSortingOrder + 2)) return false;
            if (chair.OccupiedLowerBodyRenderer == null ||
                !chair.OccupiedLowerBodyRenderer.enabled ||
                chair.OccupiedLowerBodyRenderer.sortingOrder != characterSortingOrder + 2)
                return false;
            if (chair.OccupiedBackFrameRenderer == null ||
                !chair.OccupiedBackFrameRenderer.enabled ||
                chair.OccupiedBackFrameRenderer.sortingOrder != characterSortingOrder + 4)
                return false;
            if (!seat.HasWorkstationBinding) return true;
            FurnitureVisual desk = RequiredVisual(seat.WorkSurfaceFurnitureId);
            if (desk.BaseRenderer.sortingOrder != characterSortingOrder - 2) return false;
            return desk.FrontRenderer == null ||
                   (desk.FrontRenderer.enabled == desk.Definition.FrontOverlayWhenOccupied &&
                    desk.FrontRenderer.sortingOrder == characterSortingOrder + 1);
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
            if (visual.OccupiedBackFrameRenderer != null)
                visual.OccupiedBackFrameRenderer.enabled = false;
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

        private void EnsureOccupiedBackFrameRenderer(FurnitureVisual visual)
        {
            if (visual.OccupiedBackFrameRenderer != null) return;
            Sprite baseSprite = visual.Definition.BaseSprite;
            if (!_occupiedChairBackFrames.TryGetValue(
                    baseSprite,
                    out RuntimeForegroundSprite occupiedForeground))
            {
                Rect textureRect =
                    OfficeSeatedUpperBodyProtectionRules.ChairBackFrameTextureRect(baseSprite);
                Sprite sprite = Sprite.Create(
                    baseSprite.texture,
                    textureRect,
                    OfficeSeatedUpperBodyProtectionRules.ChairBackFrameNormalizedPivot(baseSprite),
                    baseSprite.pixelsPerUnit,
                    0u,
                    SpriteMeshType.FullRect,
                    Vector4.zero);
                sprite.name = baseSprite.name + "_occupied_back_frame_runtime";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                occupiedForeground = new RuntimeForegroundSprite
                {
                    Sprite = sprite,
                    LocalPosition =
                        OfficeSeatedUpperBodyProtectionRules.ChairBackFrameLocalPosition(baseSprite)
                };
                _occupiedChairBackFrames.Add(baseSprite, occupiedForeground);
            }

            var backFrameRoot = new GameObject("OccupiedBackFrameOverlay");
            backFrameRoot.transform.SetParent(visual.VisualRoot, false);
            backFrameRoot.transform.localPosition = occupiedForeground.LocalPosition;
            backFrameRoot.transform.localRotation = Quaternion.identity;
            backFrameRoot.transform.localScale = Vector3.one;
            SpriteRenderer renderer = backFrameRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = occupiedForeground.Sprite;
            renderer.flipX = visual.BaseRenderer.flipX;
            renderer.sortingLayerID = visual.FrontRenderer.sortingLayerID;
            renderer.enabled = false;
            visual.OccupiedBackFrameRenderer = renderer;
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
            foreach (RuntimeForegroundSprite runtime in _occupiedChairBackFrames.Values)
            {
                if (runtime?.Sprite == null) continue;
                if (Application.isPlaying) Destroy(runtime.Sprite);
                else DestroyImmediate(runtime.Sprite);
            }
            _occupiedChairBackFrames.Clear();
        }
    }
}

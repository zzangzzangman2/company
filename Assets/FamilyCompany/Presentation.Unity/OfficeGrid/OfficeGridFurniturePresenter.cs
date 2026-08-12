using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
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
            public SpriteRenderer BaseRenderer;
            public SpriteRenderer FrontRenderer;
        }

        private readonly Dictionary<string, FurnitureVisual> _visuals =
            new Dictionary<string, FurnitureVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _renderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlacedOfficeFurniture> _furniture =
            new Dictionary<string, PlacedOfficeFurniture>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _frontOverlays =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);

        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeFurnitureVisualCatalog _visualCatalog;
        private Bounds _renderBounds;

        public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;
        public IReadOnlyDictionary<string, SpriteRenderer> FrontOverlayRenderers => _frontOverlays;
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
                OfficeFurnitureVisualDefinition definition = visualCatalog.Resolve(item.KindId, item.Facing);
                var root = new GameObject("Furniture_" + item.FurnitureId);
                root.transform.SetParent(transform, false);
                root.transform.position = _gridPresenter.SubcellAnchorWorld(item.PlacementAnchor);
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var visualRootObject = new GameObject("VisualRoot");
                visualRootObject.transform.SetParent(root.transform, false);
                visualRootObject.transform.localPosition = Vector3.zero;
                visualRootObject.transform.localRotation = Quaternion.identity;
                visualRootObject.transform.localScale = Vector3.one * definition.UniformScale;

                var baseRoot = new GameObject("BaseVisual");
                baseRoot.transform.SetParent(visualRootObject.transform, false);
                baseRoot.transform.localPosition = Vector3.zero;
                baseRoot.transform.localRotation = Quaternion.identity;
                baseRoot.transform.localScale = Vector3.one;
                var baseRenderer = baseRoot.AddComponent<SpriteRenderer>();
                baseRenderer.sprite = definition.BaseSprite;
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
                    frontRenderer.sortingLayerName = "Default";
                    frontRenderer.sortingOrder = baseRenderer.sortingOrder + 1;
                    frontRenderer.enabled = false;
                    _frontOverlays.Add(item.FurnitureId, frontRenderer);
                }

                var visual = new FurnitureVisual
                {
                    Furniture = item,
                    Definition = definition,
                    SemanticRoot = root.transform,
                    VisualRoot = visualRootObject.transform,
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
                   visual.FrontRenderer != null &&
                   visual.Definition.FrontOverlayWhenOccupied;
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
        }

        public void ApplySeatOcclusion(OfficeSeatSlot seat, int characterSortingOrder)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            FurnitureVisual chair = RequiredVisual(seat.ChairFurnitureId);
            chair.BaseRenderer.sortingOrder = characterSortingOrder - 1;
            if (chair.FrontRenderer != null)
            {
                chair.FrontRenderer.enabled = chair.Definition.FrontOverlayWhenOccupied;
                chair.FrontRenderer.sortingOrder = characterSortingOrder + 2;
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
                visual.FrontRenderer.enabled = false;
                visual.FrontRenderer.sortingOrder = visual.BaseRenderer.sortingOrder + 1;
            }
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
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject child = transform.GetChild(index).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}

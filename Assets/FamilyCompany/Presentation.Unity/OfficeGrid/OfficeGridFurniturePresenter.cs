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
                root.transform.position = ResolveFootprintCenter(item);
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

            AlignWorkstationsToDeskSockets();
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

        private void AlignWorkstationsToDeskSockets()
        {
            foreach (OfficeWorkstationSlot workstation in _semanticGrid.Workstations)
            {
                FurnitureVisual desk = RequiredVisual(workstation.DeskFurnitureId);
                FurnitureVisual chair = RequiredVisual(workstation.ChairFurnitureId);
                if (!desk.Definition.HasOperatorSeatSocket)
                    throw new InvalidOperationException("Desk has no operator-seat socket: " + workstation.DeskFurnitureId);
                if (!chair.Definition.HasSeatAnchor)
                    throw new InvalidOperationException("Chair has no seat anchor: " + workstation.ChairFurnitureId);

                Vector3 target = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    desk.BaseRenderer,
                    desk.Definition.OperatorSeatSocketPx);
                Vector3 current = OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    chair.BaseRenderer,
                    chair.Definition.SeatAnchorPx);
                Vector3 localDelta = chair.SemanticRoot.InverseTransformVector(target - current);
                chair.VisualRoot.localPosition += localDelta;
                RestoreVisualSorting(chair);
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

        private Vector3 ResolveFootprintCenter(PlacedOfficeFurniture item)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int y = item.Origin.Y; y < item.Origin.Y + item.Height; y++)
            for (int x = item.Origin.X; x < item.Origin.X + item.Width; x++)
            {
                sum += _gridPresenter.CellCenterWorld(new OfficeGridCoordinate(x, y));
                count++;
            }
            return sum / Math.Max(1, count);
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

using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridFurniturePresenter : MonoBehaviour
    {
        private readonly Dictionary<string, SpriteRenderer> _renderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlacedOfficeFurniture> _furniture =
            new Dictionary<string, PlacedOfficeFurniture>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteRenderer> _chairBackrests =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);

        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private Bounds _renderBounds;

        public IReadOnlyDictionary<string, SpriteRenderer> Renderers => _renderers;
        public IReadOnlyDictionary<string, SpriteRenderer> ChairBackrestRenderers => _chairBackrests;
        public Bounds RenderBounds => _renderBounds;

        public void Configure(
            OfficeGrid semanticGrid,
            OfficeGridTilemapPresenter gridPresenter,
            IReadOnlyList<string> kindIds,
            IReadOnlyList<Sprite> sprites,
            Sprite chairBackrestSprite)
        {
            if (semanticGrid == null) throw new ArgumentNullException(nameof(semanticGrid));
            if (gridPresenter == null) throw new ArgumentNullException(nameof(gridPresenter));
            if (kindIds == null || sprites == null || kindIds.Count != sprites.Count)
                throw new ArgumentException("Furniture kind and sprite counts must match.");
            if (chairBackrestSprite == null) throw new ArgumentNullException(nameof(chairBackrestSprite));

            var spriteByKind = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            for (var index = 0; index < kindIds.Count; index++)
            {
                var kindId = (kindIds[index] ?? string.Empty).Trim();
                if (kindId.Length == 0 || sprites[index] == null || !spriteByKind.TryAdd(kindId, sprites[index]))
                    throw new ArgumentException($"Furniture sprite binding {index} is invalid.");
            }

            _semanticGrid = semanticGrid;
            _gridPresenter = gridPresenter;
            ClearGenerated();
            var hasBounds = false;
            foreach (var item in semanticGrid.Furniture)
            {
                if (!spriteByKind.TryGetValue(item.KindId, out var sprite))
                    throw new InvalidOperationException("Missing furniture sprite for kind: " + item.KindId);
                var root = new GameObject("Furniture_" + item.FurnitureId);
                root.transform.SetParent(transform, false);
                root.transform.position = ResolveFootprintCenter(item);
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = OfficeGridCharacterMover.ResolveDynamicSortingOrder(root.transform.position);
                _renderers.Add(item.FurnitureId, renderer);
                _furniture.Add(item.FurnitureId, item);
                if (item.KindId == OfficeGridLayouts.SwivelChairKind)
                {
                    var backrestRoot = new GameObject("BackrestOverlay");
                    backrestRoot.transform.SetParent(root.transform, false);
                    var backrest = backrestRoot.AddComponent<SpriteRenderer>();
                    backrest.sprite = chairBackrestSprite;
                    backrest.sortingLayerName = "Default";
                    backrest.sortingOrder = renderer.sortingOrder + 1;
                    backrest.enabled = false;
                    _chairBackrests.Add(item.FurnitureId, backrest);
                }
                if (!hasBounds)
                {
                    _renderBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    _renderBounds.Encapsulate(renderer.bounds);
                }
            }
            if (!hasBounds) _renderBounds = new Bounds(transform.position, Vector3.zero);
        }

        public bool TryGetRenderer(string furnitureId, out SpriteRenderer renderer) =>
            _renderers.TryGetValue(furnitureId ?? string.Empty, out renderer);

        public bool TryGetFurniture(string furnitureId, out PlacedOfficeFurniture furniture) =>
            _furniture.TryGetValue(furnitureId ?? string.Empty, out furniture);

        public void ApplyChairOcclusion(
            string chairFurnitureId,
            int characterSortingOrder,
            OfficeFurnitureFacing facing)
        {
            if (!TryGetRenderer(chairFurnitureId, out var renderer))
                throw new ArgumentException("Unknown chair renderer: " + chairFurnitureId, nameof(chairFurnitureId));
            if (!_chairBackrests.TryGetValue(chairFurnitureId, out var backrest))
                throw new ArgumentException("Unknown chair backrest: " + chairFurnitureId, nameof(chairFurnitureId));
            var chairInFront = facing == OfficeFurnitureFacing.NorthWest ||
                               facing == OfficeFurnitureFacing.NorthEast;
            renderer.sortingOrder = characterSortingOrder - 1;
            backrest.enabled = chairInFront;
            backrest.sortingOrder = characterSortingOrder + 1;
        }

        public bool ChairOcclusionMatches(
            string chairFurnitureId,
            int characterSortingOrder,
            OfficeFurnitureFacing facing)
        {
            if (!TryGetRenderer(chairFurnitureId, out var renderer)) return false;
            if (!_chairBackrests.TryGetValue(chairFurnitureId, out var backrest)) return false;
            var chairInFront = facing == OfficeFurnitureFacing.NorthWest ||
                               facing == OfficeFurnitureFacing.NorthEast;
            if (renderer.sortingOrder >= characterSortingOrder) return false;
            return chairInFront
                ? backrest.enabled && backrest.sortingOrder > characterSortingOrder
                : !backrest.enabled;
        }

        private Vector3 ResolveFootprintCenter(PlacedOfficeFurniture item)
        {
            var sum = Vector3.zero;
            var count = 0;
            for (var y = item.Origin.Y; y < item.Origin.Y + item.Height; y++)
            for (var x = item.Origin.X; x < item.Origin.X + item.Width; x++)
            {
                sum += _gridPresenter.CellCenterWorld(new OfficeGridCoordinate(x, y));
                count++;
            }
            return sum / Math.Max(1, count);
        }

        private void ClearGenerated()
        {
            _renderers.Clear();
            _furniture.Clear();
            _chairBackrests.Clear();
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}

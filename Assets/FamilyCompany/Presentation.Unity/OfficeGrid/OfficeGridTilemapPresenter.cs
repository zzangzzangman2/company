using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridTilemapPresenter : MonoBehaviour
    {
        public const int TilePixelWidth = 320;
        public const int TilePixelHeight = 160;
        public const float PixelsPerUnit = 180f;
        public const float TileWorldWidth = TilePixelWidth / PixelsPerUnit;
        public const float TileWorldHeight = TilePixelHeight / PixelsPerUnit;
        public const int FloorSortingOrder = -10000;

        private Grid _unityGrid;
        private Tilemap _floorTilemap;
        private TilemapRenderer _floorRenderer;
        private OfficeGrid _semanticGrid;

        public Grid UnityGrid => _unityGrid;
        public Tilemap FloorTilemap => _floorTilemap;
        public Renderer FloorRenderer => _floorRenderer;
        public OfficeGrid SemanticGrid => _semanticGrid;

        public void Configure(OfficeGrid semanticGrid, IReadOnlyList<TileBase> floorTiles)
        {
            if (semanticGrid == null) throw new ArgumentNullException(nameof(semanticGrid));
            if (floorTiles == null || floorTiles.Count < 3)
                throw new ArgumentException("Office floor requires three non-null tile variants.", nameof(floorTiles));
            for (var index = 0; index < 3; index++)
            {
                if (floorTiles[index] == null)
                    throw new ArgumentException($"Office floor tile {index} is null.", nameof(floorTiles));
            }

            _semanticGrid = semanticGrid;
            _unityGrid = GetComponent<Grid>();
            if (_unityGrid == null) _unityGrid = gameObject.AddComponent<Grid>();
            _unityGrid.cellLayout = GridLayout.CellLayout.Isometric;
            _unityGrid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            _unityGrid.cellSize = new Vector3(TileWorldWidth, TileWorldHeight, 1f);

            EnsureTilemap();
            _floorTilemap.ClearAllTiles();
            for (var y = 0; y < semanticGrid.Height; y++)
            for (var x = 0; x < semanticGrid.Width; x++)
            {
                var kind = semanticGrid.FloorAt(new OfficeGridCoordinate(x, y));
                if (kind == OfficeFloorTileKind.Void) continue;
                var variant = Mathf.Clamp((int)kind - (int)OfficeFloorTileKind.WarmWoodA, 0, 2);
                _floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTiles[variant]);
            }

            _floorTilemap.CompressBounds();
            _floorTilemap.RefreshAllTiles();
        }

        public Vector3 CellCenterWorld(OfficeGridCoordinate cell)
        {
            if (_semanticGrid == null) throw new InvalidOperationException("Office grid presenter is not configured.");
            if (!_semanticGrid.Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return _unityGrid.GetCellCenterWorld(new Vector3Int(cell.X, cell.Y, 0));
        }

        public Vector3 CellBasisXWorld()
        {
            if (_semanticGrid == null) throw new InvalidOperationException("Office grid presenter is not configured.");
            if (_semanticGrid.Width > 1)
                return CellCenterWorld(new OfficeGridCoordinate(1, 0)) -
                       CellCenterWorld(new OfficeGridCoordinate(0, 0));
            return new Vector3(TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
        }

        public Vector3 CellBasisYWorld()
        {
            if (_semanticGrid == null) throw new InvalidOperationException("Office grid presenter is not configured.");
            if (_semanticGrid.Height > 1)
                return CellCenterWorld(new OfficeGridCoordinate(0, 1)) -
                       CellCenterWorld(new OfficeGridCoordinate(0, 0));
            return new Vector3(-TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
        }

        public Vector3 SubcellAnchorWorld(OfficeGridSubcellAnchor anchor)
        {
            if (_semanticGrid == null) throw new InvalidOperationException("Office grid presenter is not configured.");
            if (!_semanticGrid.Contains(anchor)) throw new ArgumentOutOfRangeException(nameof(anchor));
            Vector3 origin = CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector3 basisX = _semanticGrid.Width > 1
                ? CellCenterWorld(new OfficeGridCoordinate(1, 0)) - origin
                : new Vector3(TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
            Vector3 basisY = _semanticGrid.Height > 1
                ? CellCenterWorld(new OfficeGridCoordinate(0, 1)) - origin
                : new Vector3(-TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
            return origin + basisX * (anchor.X2 * 0.5f) + basisY * (anchor.Y2 * 0.5f);
        }

        public Vector3[] FootprintCornersWorld(PlacedOfficeFurniture furniture)
        {
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            Vector3 first = CellCenterWorld(furniture.Origin);
            Vector3 basisX = _semanticGrid.Width > 1
                ? CellCenterWorld(new OfficeGridCoordinate(
                    Math.Min(furniture.Origin.X + 1, _semanticGrid.Width - 1),
                    furniture.Origin.Y)) - first
                : new Vector3(TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
            if (basisX.sqrMagnitude < 0.0001f)
                basisX = first - CellCenterWorld(new OfficeGridCoordinate(furniture.Origin.X - 1, furniture.Origin.Y));
            Vector3 basisY = _semanticGrid.Height > 1
                ? CellCenterWorld(new OfficeGridCoordinate(furniture.Origin.X, Math.Min(furniture.Origin.Y + 1, _semanticGrid.Height - 1))) - first
                : new Vector3(-TileWorldWidth * 0.5f, TileWorldHeight * 0.5f, 0f);
            if (basisY.sqrMagnitude < 0.0001f)
                basisY = first - CellCenterWorld(new OfficeGridCoordinate(furniture.Origin.X, furniture.Origin.Y - 1));

            Vector3 center = first + basisX * ((furniture.Width - 1) * 0.5f) +
                             basisY * ((furniture.Height - 1) * 0.5f);
            Vector3 extentX = basisX * (furniture.Width * 0.5f);
            Vector3 extentY = basisY * (furniture.Height * 0.5f);
            return new[]
            {
                center - extentX - extentY,
                center + extentX - extentY,
                center + extentX + extentY,
                center - extentX + extentY
            };
        }

        public OfficeGridCoordinate NearestCell(Vector3 worldPosition)
        {
            if (_semanticGrid == null) throw new InvalidOperationException("Office grid presenter is not configured.");
            var best = new OfficeGridCoordinate(0, 0);
            var bestDistance = float.PositiveInfinity;
            for (var y = 0; y < _semanticGrid.Height; y++)
            for (var x = 0; x < _semanticGrid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                var distance = (CellCenterWorld(cell) - worldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }

        private void EnsureTilemap()
        {
            var child = transform.Find("FloorTilemap");
            var target = child == null ? new GameObject("FloorTilemap") : child.gameObject;
            target.transform.SetParent(transform, false);
            _floorTilemap = target.GetComponent<Tilemap>();
            if (_floorTilemap == null) _floorTilemap = target.AddComponent<Tilemap>();
            _floorRenderer = target.GetComponent<TilemapRenderer>();
            if (_floorRenderer == null) _floorRenderer = target.AddComponent<TilemapRenderer>();
            _floorRenderer.sortingOrder = FloorSortingOrder;
            _floorRenderer.mode = TilemapRenderer.Mode.Chunk;
        }

    }
}

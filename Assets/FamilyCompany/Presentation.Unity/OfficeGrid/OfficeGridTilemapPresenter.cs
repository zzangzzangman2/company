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

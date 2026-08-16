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
        private Vector3 _nearestOrigin;
        private Vector3 _nearestBasisX;
        private Vector3 _nearestBasisY;
        private float _nearestDeterminant;

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
            _nearestOrigin = _unityGrid.GetCellCenterWorld(Vector3Int.zero);
            _nearestBasisX = _unityGrid.GetCellCenterWorld(Vector3Int.right) - _nearestOrigin;
            _nearestBasisY = _unityGrid.GetCellCenterWorld(Vector3Int.up) - _nearestOrigin;
            _nearestDeterminant = _nearestBasisX.x * _nearestBasisY.y -
                                  _nearestBasisX.y * _nearestBasisY.x;
            if (Mathf.Abs(_nearestDeterminant) <= 0.000001f)
                throw new InvalidOperationException("Office grid basis is singular.");

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

        public Vector2 WorldVectorToVisualFacingAxes(Vector2 worldVector) =>
            DefaultWorldVectorToVisualFacingAxes(worldVector);

        public static Vector2 DefaultWorldVectorToVisualFacingAxes(Vector2 worldVector)
        {
            // The authored walk atlases use character-left/right names: their `west` body looks
            // screen-right and their `east` body looks screen-left. Resolve against the projected
            // world displacement with that horizontal handedness so the visible head, torso and
            // feet point along the actual screen path. Grid coordinates still own routing.
            return new Vector2(-worldVector.x, worldVector.y);
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
            float basisYSquared = _nearestBasisY.sqrMagnitude;
            for (var x = 0; x < _semanticGrid.Width; x++)
            {
                Vector3 remaining = worldPosition - (_nearestOrigin + _nearestBasisX * x);
                float projectedY = Vector3.Dot(remaining, _nearestBasisY) / basisYSquared;
                int lowerY = Mathf.Clamp(Mathf.FloorToInt(projectedY), 0, _semanticGrid.Height - 1);
                int upperY = Mathf.Clamp(Mathf.CeilToInt(projectedY), 0, _semanticGrid.Height - 1);
                ConsiderNearestCandidate(x, lowerY, worldPosition, ref best, ref bestDistance);
                if (upperY != lowerY)
                    ConsiderNearestCandidate(x, upperY, worldPosition, ref best, ref bestDistance);
            }
            return best;
        }

        private void ConsiderNearestCandidate(
            int x,
            int y,
            Vector3 worldPosition,
            ref OfficeGridCoordinate best,
            ref float bestDistance)
        {
            Vector3 center = _unityGrid.GetCellCenterWorld(new Vector3Int(x, y, 0));
            float distance = (center - worldPosition).sqrMagnitude;
            if (distance > bestDistance) return;
            if (distance == bestDistance &&
                (y > best.Y || y == best.Y && x >= best.X)) return;
            bestDistance = distance;
            best = new OfficeGridCoordinate(x, y);
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

using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridAlignmentDebugOverlay : MonoBehaviour
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly Dictionary<string, TextMesh> _labels = new Dictionary<string, TextMesh>(StringComparer.Ordinal);
        private OfficeTileMigrationPreviewBootstrap _bootstrap;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private bool _overlayEnabled;

        public bool OverlayEnabled => _overlayEnabled;

        public void Configure(OfficeTileMigrationPreviewBootstrap bootstrap)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            EnsureRenderer();
            SetOverlayEnabled(false);
        }

        public void SetOverlayEnabled(bool enabled)
        {
            _overlayEnabled = enabled;
            EnsureRenderer();
            _renderer.enabled = enabled;
            foreach (TextMesh label in _labels.Values) label.gameObject.SetActive(enabled);
            if (enabled) RefreshImmediate();
        }

        public void RefreshImmediate()
        {
            if (!_overlayEnabled || _bootstrap == null || _bootstrap.Presenter == null) return;
            EnsureRenderer();
            _vertices.Clear();
            _colors.Clear();
            OfficeGrid grid = _bootstrap.Presenter.SemanticGrid;

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                AddCellDiamond(cell, grid.IsWalkable(cell) ? new Color(1f, 1f, 1f, 0.22f) : new Color(1f, 0.15f, 0.1f, 0.75f));
            }

            if (_bootstrap.FurniturePresenter != null)
            {
                foreach (PlacedOfficeFurniture furniture in grid.Furniture)
                {
                    AddCross(_bootstrap.FurniturePresenter.GroundAnchorWorld(furniture.FurnitureId), Color.green, 0.08f);
                    AddCross(_bootstrap.FurniturePresenter.SortAnchorWorld(furniture.FurnitureId), Color.cyan, 0.065f);
                    AddPolygon(
                        _bootstrap.Presenter.FootprintCornersWorld(furniture),
                        new Color(1f, 1f, 1f, 0.85f));
                    AddPolygon(
                        _bootstrap.FurniturePresenter.GroundFootprintWorld(furniture.FurnitureId),
                        new Color(0.1f, 1f, 0.25f, 1f));
                }

                foreach (OfficeSeatSlot seat in grid.SeatSlots)
                {
                    AddCross(_bootstrap.FurniturePresenter.SeatAnchorWorld(seat.ChairFurnitureId), Color.yellow, 0.09f);
                    Vector3 operatorSeat = _bootstrap.FurniturePresenter.OperatorSeatSocketWorld(seat.WorkSurfaceFurnitureId);
                    Vector3 operatorWork = _bootstrap.FurniturePresenter.OperatorWorkSocketWorld(seat.WorkSurfaceFurnitureId);
                    AddCross(operatorSeat, new Color(1f, 0.45f, 0f, 1f), 0.11f);
                    AddCross(operatorWork, new Color(0.2f, 0.55f, 1f, 1f), 0.11f);
                    AddCross(_bootstrap.Presenter.SubcellAnchorWorld(seat.OperatorAnchor), new Color(0.8f, 0.3f, 1f, 1f), 0.075f);
                    AddCellDiamond(seat.ApproachCell, new Color(0.1f, 0.35f, 1f, 1f));
                    AddSegment(
                        _bootstrap.FurniturePresenter.SeatAnchorWorld(seat.ChairFurnitureId),
                        operatorSeat,
                        Color.yellow);
                }
            }

            if (_bootstrap.SeatingState != null)
            {
                foreach (OfficeSeatView view in _bootstrap.SeatingState.GetSeats())
                {
                    if (view.State != OfficeSeatMeaningState.Reserved && view.State != OfficeSeatMeaningState.Occupied) continue;
                    OfficeSeatSlot seat = grid.SeatSlots.Single(item => string.Equals(item.SeatId, view.SeatId, StringComparison.Ordinal));
                    AddCellDiamond(seat.Cell, new Color(1f, 0.45f, 0f, 1f));
                }
            }

            foreach (OfficeGridSeatedWorker worker in _bootstrap.SeatedWorkers)
            {
                if (worker.PoseProfile == null) continue;
                var mover = worker.GetComponent<OfficeGridCharacterMover>();
                Vector3 pelvis = mover.SpriteAnchorWorld(worker.PoseProfile.PelvisAnchorPx);
                Vector3 hand = mover.SpriteAnchorWorld(worker.PoseProfile.HandAnchorPx);
                AddCross(pelvis, Color.magenta, 0.1f);
                AddCross(hand, new Color(0.2f, 0.55f, 1f, 1f), 0.1f);
                TextMesh label = EnsureLabel(worker.MemberId);
                label.transform.position = pelvis + new Vector3(0.14f, 0.25f, -0.5f);
                float error = Camera.main == null ? float.PositiveInfinity : worker.PelvisSeatScreenError(Camera.main);
                float handError = Camera.main == null ? float.PositiveInfinity : worker.HandWorkScreenError(Camera.main);
                float chairError = Camera.main == null ? float.PositiveInfinity : worker.ChairDeskSeatScreenError(Camera.main);
                label.text = $"{worker.MemberId}  chair {chairError:F2}px  pelvis {error:F2}px  hand {handError:F2}px  frame {mover.Animator.CurrentOfficeSeatingFrame}";
                label.color = error <= 2f && chairError <= 2f && handError <= 4f
                    ? new Color(0.15f, 1f, 0.25f, 1f)
                    : Color.red;
                label.gameObject.SetActive(true);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            int[] indices = Enumerable.Range(0, _vertices.Count).ToArray();
            _mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
            _mesh.RecalculateBounds();
        }

        private void LateUpdate()
        {
            if (_overlayEnabled) RefreshImmediate();
        }

        private void EnsureRenderer()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "OfficeAlignmentDebugLines" };
                _mesh.MarkDynamic();
                var filter = GetComponent<MeshFilter>();
                if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = _mesh;
            }

            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
                if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable for alignment QA.");
                _renderer.sharedMaterial = new Material(shader) { name = "OfficeAlignmentDebugMaterial" };
                _renderer.sortingLayerName = "Default";
                _renderer.sortingOrder = 20000;
            }
        }

        private TextMesh EnsureLabel(string memberId)
        {
            if (_labels.TryGetValue(memberId, out TextMesh label)) return label;
            var target = new GameObject("AlignmentLabel_" + memberId);
            target.transform.SetParent(transform, false);
            label = target.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerLeft;
            label.alignment = TextAlignment.Left;
            label.fontSize = 40;
            label.characterSize = 0.055f;
            label.fontStyle = FontStyle.Bold;
            var meshRenderer = target.GetComponent<MeshRenderer>();
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.sortingOrder = 20001;
            _labels.Add(memberId, label);
            return label;
        }

        private void AddCellDiamond(OfficeGridCoordinate cell, Color color)
        {
            Vector3 center = _bootstrap.Presenter.CellCenterWorld(cell);
            float halfWidth = OfficeGridTilemapPresenter.TileWorldWidth * 0.5f;
            float halfHeight = OfficeGridTilemapPresenter.TileWorldHeight * 0.5f;
            Vector3 left = center + new Vector3(-halfWidth, 0f, 0f);
            Vector3 top = center + new Vector3(0f, halfHeight, 0f);
            Vector3 right = center + new Vector3(halfWidth, 0f, 0f);
            Vector3 bottom = center + new Vector3(0f, -halfHeight, 0f);
            AddSegment(left, top, color);
            AddSegment(top, right, color);
            AddSegment(right, bottom, color);
            AddSegment(bottom, left, color);
        }

        private void AddCross(Vector3 center, Color color, float radius)
        {
            AddSegment(center + new Vector3(-radius, 0f), center + new Vector3(radius, 0f), color);
            AddSegment(center + new Vector3(0f, -radius), center + new Vector3(0f, radius), color);
        }

        private void AddPolygon(IReadOnlyList<Vector3> points, Color color)
        {
            if (points == null || points.Count < 2) return;
            for (int index = 0; index < points.Count; index++)
                AddSegment(points[index], points[(index + 1) % points.Count], color);
        }

        private void AddSegment(Vector3 first, Vector3 second, Color color)
        {
            first.z = -0.5f;
            second.z = -0.5f;
            _vertices.Add(first);
            _vertices.Add(second);
            _colors.Add(color);
            _colors.Add(color);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_renderer != null && _renderer.sharedMaterial != null) Destroy(_renderer.sharedMaterial);
        }
    }
}

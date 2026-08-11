using System;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridCharacterMover : MonoBehaviour
    {
        // The canonical 256px high-motion canvases contain different amounts of transparent
        // headroom. 1.69 keeps every family silhouette between 14% and 18% at ortho 6.6.
        public const float UniformVisualScale = 1.69f;
        public const float DefaultMoveSpeed = 1.75f;
        public const int DynamicSortingBase = 5000;

        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeGridCoordinate[] _route = Array.Empty<OfficeGridCoordinate>();
        private SpriteRenderer _renderer;
        private DirectionalSpriteAnimator _animator;
        private int _targetIndex;
        private float _moveSpeed;
        private float _distanceTravelled;

        public SpriteRenderer TargetRenderer => _renderer;
        public DirectionalSpriteAnimator Animator => _animator;
        public int TargetIndex => _targetIndex;
        public OfficeGridCoordinate TargetCell => _route.Length == 0 ? default : _route[_targetIndex];
        public float MoveSpeed => _moveSpeed;
        public float DistanceTravelled => _distanceTravelled;

        public void Configure(
            OfficeGrid semanticGrid,
            OfficeGridTilemapPresenter gridPresenter,
            Sprite[] walkFrames,
            OfficeGridCoordinate[] route,
            float moveSpeed = DefaultMoveSpeed)
        {
            if (semanticGrid == null) throw new ArgumentNullException(nameof(semanticGrid));
            if (gridPresenter == null) throw new ArgumentNullException(nameof(gridPresenter));
            if (walkFrames == null || walkFrames.Length != DirectionalSpriteAnimator.RequiredFrameCount)
                throw new ArgumentException("Grid character requires exactly 48 walk frames.", nameof(walkFrames));
            if (route == null || route.Length < 2)
                throw new ArgumentException("Grid character route requires at least two cells.", nameof(route));
            if (moveSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            for (var index = 0; index < route.Length; index++)
            {
                if (!semanticGrid.Contains(route[index]) || !semanticGrid.IsWalkable(route[index]))
                    throw new ArgumentException($"Grid character route contains blocked cell {route[index]}.", nameof(route));
                var next = route[(index + 1) % route.Length];
                if (!IsGridSegmentWalkable(semanticGrid, route[index], next))
                    throw new ArgumentException($"Grid character route crosses a blocked cell: {route[index]} -> {next}.", nameof(route));
            }

            _semanticGrid = semanticGrid;
            _gridPresenter = gridPresenter;
            _route = (OfficeGridCoordinate[])route.Clone();
            _moveSpeed = moveSpeed;
            _distanceTravelled = 0f;
            _targetIndex = 1;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingLayerName = "Default";
            transform.localScale = Vector3.one * UniformVisualScale;
            transform.position = _gridPresenter.CellCenterWorld(_route[0]);
            _animator = GetComponent<DirectionalSpriteAnimator>();
            if (_animator == null) _animator = gameObject.AddComponent<DirectionalSpriteAnimator>();
            _animator.Configure(_renderer, walkFrames);
            UpdateSortingOrder();
        }

        public bool CanEnter(OfficeGridCoordinate cell) =>
            _semanticGrid != null && _semanticGrid.Contains(cell) && _semanticGrid.IsWalkable(cell);

        public void TickMovement(float deltaTime)
        {
            if (_route.Length < 2 || _gridPresenter == null || deltaTime <= 0f) return;
            var target = _gridPresenter.CellCenterWorld(_route[_targetIndex]);
            var delta = target - transform.position;
            var maximumStep = _moveSpeed * deltaTime;
            Vector3 velocity;
            if (delta.magnitude <= maximumStep)
            {
                transform.position = target;
                _targetIndex = (_targetIndex + 1) % _route.Length;
                var next = _gridPresenter.CellCenterWorld(_route[_targetIndex]);
                velocity = (next - transform.position).normalized * _moveSpeed;
            }
            else
            {
                velocity = delta.normalized * _moveSpeed;
                var displacement = velocity * deltaTime;
                transform.position += displacement;
                _distanceTravelled += displacement.magnitude;
            }

            _animator.SetWorldVelocity(new Vector3(velocity.x, 0f, velocity.y));
            UpdateSortingOrder();
        }

        public float RenderedBoundsHeightRatio(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!camera.orthographic) throw new ArgumentException("Camera must be orthographic.", nameof(camera));
            return _renderer.bounds.size.y / (camera.orthographicSize * 2f);
        }

        private void Update()
        {
            TickMovement(Time.deltaTime);
        }

        private void UpdateSortingOrder()
        {
            if (_renderer == null) return;
            _renderer.sortingOrder = DynamicSortingBase - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private static bool IsGridSegmentWalkable(
            OfficeGrid grid,
            OfficeGridCoordinate start,
            OfficeGridCoordinate end)
        {
            var x = start.X;
            var y = start.Y;
            var deltaX = Mathf.Abs(end.X - start.X);
            var deltaY = Mathf.Abs(end.Y - start.Y);
            var stepX = start.X < end.X ? 1 : -1;
            var stepY = start.Y < end.Y ? 1 : -1;
            var error = deltaX - deltaY;
            while (true)
            {
                if (!grid.IsWalkable(new OfficeGridCoordinate(x, y))) return false;
                if (x == end.X && y == end.Y) return true;
                var doubled = error * 2;
                if (doubled > -deltaY)
                {
                    error -= deltaY;
                    x += stepX;
                }
                if (doubled < deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }
        }
    }
}

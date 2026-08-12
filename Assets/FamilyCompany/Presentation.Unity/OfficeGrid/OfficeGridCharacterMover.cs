using System;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridCharacterMover : MonoBehaviour
    {
        // Physical scale, not screen occupancy. The furniture art is the ruler: the swivel chair
        // seat sits 108.2px above its own floor anchor and the desk surface 161px, so one drawn
        // centimetre is about 2.2px. 1.35 is the largest scale at which a seated family member
        // still fits that chair - hips inside the cushion, feet reaching the floor in front of the
        // castor base. 1.50 already overhangs the seat and 1.69 dwarfs the chair entirely.
        // Measured with Tools/office_visual_coherence_v4_probe.py against the real sprites.
        public const float UniformVisualScale = 1.35f;
        public const float DefaultMoveSpeed = 1.75f;
        public const int DynamicSortingBase = 5000;

        private OfficeGrid _semanticGrid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeGridCoordinate[] _route = Array.Empty<OfficeGridCoordinate>();
        private Transform _visualRoot;
        private SpriteRenderer _renderer;
        private DirectionalSpriteAnimator _animator;
        private int _targetIndex;
        private float _moveSpeed;
        private float _distanceTravelled;
        private bool _routeMovementEnabled = true;

        public SpriteRenderer TargetRenderer => _renderer;
        public Transform VisualRoot => _visualRoot;
        public Vector3 VisualLocalOffset => _visualRoot == null ? Vector3.zero : _visualRoot.localPosition;
        public float SeatedVisualScaleMultiplier => _visualRoot == null
            ? 1f
            : _visualRoot.localScale.x / UniformVisualScale;
        public DirectionalSpriteAnimator Animator => _animator;
        public int TargetIndex => _targetIndex;
        public OfficeGridCoordinate TargetCell => _route.Length == 0 ? default : _route[_targetIndex];
        public float MoveSpeed => _moveSpeed;
        public float DistanceTravelled => _distanceTravelled;
        public bool RouteMovementEnabled => _routeMovementEnabled;

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
            _routeMovementEnabled = true;
            _targetIndex = 1;
            transform.localScale = Vector3.one;
            _visualRoot = transform.Find("VisualRoot");
            if (_visualRoot == null)
            {
                var visualObject = new GameObject("VisualRoot");
                _visualRoot = visualObject.transform;
                _visualRoot.SetParent(transform, false);
            }
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * UniformVisualScale;
            _renderer = _visualRoot.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingLayerName = "Default";
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
            if (!_routeMovementEnabled || _route.Length < 2 || _gridPresenter == null || deltaTime <= 0f) return;
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

        public void SetRouteMovementEnabled(bool enabled)
        {
            _routeMovementEnabled = enabled;
            if (!enabled && _animator != null) _animator.SetWorldVelocity(Vector3.zero);
        }

        public void RefreshSortingOrder(int offset = 0)
        {
            if (_renderer == null) return;
            _renderer.sortingOrder = ResolveDynamicSortingOrder(transform.position) + offset;
        }

        public void SetVisualLocalOffset(Vector3 offset)
        {
            if (_visualRoot == null) throw new InvalidOperationException("Character VisualRoot is not configured.");
            _visualRoot.localPosition = offset;
        }

        public void SetSeatedVisualPose(
            Vector3 offset,
            float uniformScaleMultiplier,
            float rotationDegrees = 0f)
        {
            if (_visualRoot == null) throw new InvalidOperationException("Character VisualRoot is not configured.");
            if (uniformScaleMultiplier <= 0f || float.IsNaN(uniformScaleMultiplier) || float.IsInfinity(uniformScaleMultiplier))
                throw new ArgumentOutOfRangeException(nameof(uniformScaleMultiplier));
            _visualRoot.localScale = Vector3.one * (UniformVisualScale * uniformScaleMultiplier);
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            _visualRoot.localPosition = offset;
        }

        public void ResetVisualLocalOffset()
        {
            SetVisualLocalOffset(Vector3.zero);
        }

        public void ResetVisualPose()
        {
            if (_visualRoot == null) throw new InvalidOperationException("Character VisualRoot is not configured.");
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one * UniformVisualScale;
        }

        public Vector3 SpriteAnchorWorld(Vector2 spriteRectAnchorPx)
        {
            return OfficeGridAlignmentMetrics.SpriteAnchorWorld(_renderer, spriteRectAnchorPx);
        }

        public static int ResolveDynamicSortingOrder(Vector3 groundPosition) =>
            DynamicSortingBase - Mathf.RoundToInt(groundPosition.y * 100f);

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
            RefreshSortingOrder();
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

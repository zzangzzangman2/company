using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    public enum OfficeGridSeatingPhase
    {
        WaitingForNavigation = 0,
        MovingToSeat = 1,
        SittingDown = 2,
        Working = 3
    }

    [DisallowMultipleComponent]
    public sealed class OfficeGridSeatedWorker : MonoBehaviour
    {
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeGridFurniturePresenter _furniturePresenter;
        private OfficeGridCharacterMover _mover;
        private DirectionalSpriteAnimator _animator;
        private OfficeSeatSlot _seat;
        private List<OfficeGridCoordinate> _path = new List<OfficeGridCoordinate>();
        private int _pathIndex;
        private float _navigationDelaySeconds;
        private float _configuredAt;
        private int _direction;
        private OfficeGridSeatingPhase _phase;

        public string MemberId { get; private set; }
        public string SeatId => _seat?.SeatId ?? string.Empty;
        public OfficeGridSeatingPhase Phase => _phase;
        public bool IsWorking => _phase == OfficeGridSeatingPhase.Working;
        public OfficeFurnitureFacing Facing => _seat == null ? default : _seat.Facing;
        public int DirectionIndex => _direction;

        public void Configure(
            string memberId,
            string seatId,
            OfficeGrid grid,
            OfficeGridTilemapPresenter gridPresenter,
            OfficeGridFurniturePresenter furniturePresenter,
            OfficeGridCharacterMover mover,
            Sprite[] sitDownFrames,
            Sprite[] workFrames,
            Sprite[] standUpFrames,
            float navigationDelaySeconds)
        {
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(seatId)) throw new ArgumentException("Seat ID is required.", nameof(seatId));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _gridPresenter = gridPresenter ?? throw new ArgumentNullException(nameof(gridPresenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            _animator = mover.Animator ?? throw new InvalidOperationException("Grid worker animator is missing.");
            _seat = FindSeat(grid, seatId);
            _direction = FacingDirection(_seat.Facing);
            _navigationDelaySeconds = Mathf.Max(0f, navigationDelaySeconds);
            _configuredAt = Time.time;
            _phase = OfficeGridSeatingPhase.WaitingForNavigation;
            MemberId = memberId.Trim();
            _animator.ConfigureOfficeSeating(sitDownFrames, workFrames, standUpFrames);
        }

        public float FootError()
        {
            if (_seat == null || _gridPresenter == null) return float.PositiveInfinity;
            return Vector3.Distance(transform.position, _gridPresenter.CellCenterWorld(_seat.Cell));
        }

        private void Update()
        {
            if (_seat == null) return;
            switch (_phase)
            {
                case OfficeGridSeatingPhase.WaitingForNavigation:
                    if (Time.time - _configuredAt < _navigationDelaySeconds) return;
                    BeginSeatApproach();
                    break;
                case OfficeGridSeatingPhase.MovingToSeat:
                    TickSeatApproach(Time.deltaTime);
                    break;
                case OfficeGridSeatingPhase.SittingDown:
                    if (!_animator.IsOfficeSeatingTransitionComplete) return;
                    if (!_animator.BeginSeatedWork())
                        throw new InvalidOperationException(MemberId + " could not begin seated work animation.");
                    _mover.RefreshSortingOrder();
                    _furniturePresenter.ApplyChairOcclusion(
                        _seat.FurnitureId,
                        _mover.TargetRenderer.sortingOrder,
                        _seat.Facing);
                    _phase = OfficeGridSeatingPhase.Working;
                    break;
            }
        }

        private void BeginSeatApproach()
        {
            _mover.SetRouteMovementEnabled(false);
            var start = _gridPresenter.NearestCell(transform.position);
            if (!_grid.IsWalkable(start))
                throw new InvalidOperationException($"{MemberId} starts seating from blocked cell {start}.");
            _path = FindPath(_grid, start, _seat.Cell);
            _pathIndex = _path.Count > 1 ? 1 : 0;
            _phase = OfficeGridSeatingPhase.MovingToSeat;
            if (_path.Count == 1) BeginSitDown();
        }

        private void TickSeatApproach(float deltaTime)
        {
            var target = _gridPresenter.CellCenterWorld(_path[_pathIndex]);
            var displacement = target - transform.position;
            var step = OfficeSeatPrecisionMotion.Advance(
                transform.position.x,
                transform.position.y,
                target.x,
                target.y,
                OfficeSeatPrecisionMotion.ApproachSpeedMetersPerSecond,
                deltaTime);
            transform.position = new Vector3((float)step.X, (float)step.Z, transform.position.z);
            var velocity = step.Arrived ? Vector3.zero : new Vector3(displacement.x, 0f, displacement.y).normalized;
            _animator.SetWorldVelocity(velocity);
            _mover.RefreshSortingOrder();
            if (!step.Arrived) return;
            if (_pathIndex < _path.Count - 1)
            {
                _pathIndex++;
                return;
            }
            BeginSitDown();
        }

        private void BeginSitDown()
        {
            transform.position = _gridPresenter.CellCenterWorld(_seat.Cell);
            _animator.SetWorldVelocity(Vector3.zero);
            if (!_animator.PrepareOfficeSeatingFacing(_direction, OfficeSeatForegroundOcclusionMode.Default))
                throw new InvalidOperationException(MemberId + " could not prepare seat facing.");
            _mover.RefreshSortingOrder();
            _furniturePresenter.ApplyChairOcclusion(
                _seat.FurnitureId,
                _mover.TargetRenderer.sortingOrder,
                _seat.Facing);
            if (!_animator.BeginSitDown(_direction))
                throw new InvalidOperationException(MemberId + " could not begin sit-down animation.");
            _phase = OfficeGridSeatingPhase.SittingDown;
        }

        private static OfficeSeatSlot FindSeat(OfficeGrid grid, string seatId)
        {
            foreach (var seat in grid.SeatSlots)
            {
                if (string.Equals(seat.SeatId, seatId, StringComparison.Ordinal)) return seat;
            }
            throw new ArgumentException("Unknown grid seat: " + seatId, nameof(seatId));
        }

        private static int FacingDirection(OfficeFurnitureFacing facing)
        {
            return facing switch
            {
                OfficeFurnitureFacing.SouthEast => (int)OfficeSeatFacing8.Southeast,
                OfficeFurnitureFacing.SouthWest => (int)OfficeSeatFacing8.Southwest,
                OfficeFurnitureFacing.NorthWest => (int)OfficeSeatFacing8.Northwest,
                OfficeFurnitureFacing.NorthEast => (int)OfficeSeatFacing8.Northeast,
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        private static List<OfficeGridCoordinate> FindPath(
            OfficeGrid grid,
            OfficeGridCoordinate start,
            OfficeGridCoordinate target)
        {
            var queue = new Queue<OfficeGridCoordinate>();
            var previous = new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate>();
            var visited = new HashSet<OfficeGridCoordinate> { start };
            queue.Enqueue(start);
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0),
                new OfficeGridCoordinate(0, -1),
                new OfficeGridCoordinate(-1, 0),
                new OfficeGridCoordinate(0, 1)
            };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Equals(target)) break;
                foreach (var offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!grid.Contains(next) || !grid.IsWalkable(next) || !visited.Add(next)) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            if (!visited.Contains(target))
                throw new InvalidOperationException($"No walkable seat path exists: {start} -> {target}.");
            var path = new List<OfficeGridCoordinate> { target };
            while (!path[path.Count - 1].Equals(start)) path.Add(previous[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }
    }
}

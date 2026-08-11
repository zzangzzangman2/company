using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    public enum OfficeGridSeatingPhase
    {
        WaitingForNavigation = 0,
        MovingToApproach = 1,
        AligningToSeat = 2,
        SittingDown = 3,
        Working = 4,
        StandingUp = 5,
        LeavingSeat = 6
    }

    [DisallowMultipleComponent]
    public sealed class OfficeGridSeatedWorker : MonoBehaviour
    {
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _gridPresenter;
        private OfficeGridFurniturePresenter _furniturePresenter;
        private OfficeGridCharacterMover _mover;
        private DirectionalSpriteAnimator _animator;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private OfficeCharacterSeatPoseProfile _poseProfile;
        private OfficeSeatingState _seatingState;
        private OfficeSeatRuntimeClaim _claim;
        private OfficeSeatSlot _seat;
        private List<OfficeGridCoordinate> _path = new List<OfficeGridCoordinate>();
        private int _pathIndex;
        private float _navigationDelaySeconds;
        private float _configuredAt;
        private int _direction;
        private OfficeGridSeatingPhase _phase;
        private bool _standAndReseatRequested;
        private float _visualResetError;
        private OfficeSeatingAnimationClip? _alignedClip;
        private int _alignedFrame = -1;
        private Vector3 _previousAlignedVisualPosition;
        private bool _hasPreviousAlignedVisualPosition;
        private float _maxFrameCorrectionJumpWorld;
        private float _maxWorkPelvisErrorPx;
        private float _maxWorkHandErrorPx;

        public string MemberId { get; private set; }
        public string SeatId => _seat?.SeatId ?? string.Empty;
        public OfficeGridSeatingPhase Phase => _phase;
        public bool IsWorking => _phase == OfficeGridSeatingPhase.Working;
        public bool HasActiveClaim => _claim != null && !_claim.IsReleased;
        public bool IsSeatOccupied => HasActiveClaim && _claim.IsOccupied;
        public OfficeFurnitureFacing Facing => _seat == null ? default : _seat.Facing;
        public int DirectionIndex => _direction;
        public float VisualResetError => _visualResetError;
        public OfficeCharacterSeatPoseProfile PoseProfile => _poseProfile;
        public float MaxFrameCorrectionJumpWorld => _maxFrameCorrectionJumpWorld;
        public float MaxWorkPelvisErrorPx => _maxWorkPelvisErrorPx;
        public float MaxWorkHandErrorPx => _maxWorkHandErrorPx;

        public void Configure(
            string memberId,
            string seatId,
            OfficeGrid grid,
            OfficeGridTilemapPresenter gridPresenter,
            OfficeGridFurniturePresenter furniturePresenter,
            OfficeGridCharacterMover mover,
            OfficeCharacterSeatPoseCatalog poseCatalog,
            OfficeSeatingState seatingState,
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
            _poseCatalog = poseCatalog ?? throw new ArgumentNullException(nameof(poseCatalog));
            _seatingState = seatingState ?? throw new ArgumentNullException(nameof(seatingState));
            _animator = mover.Animator ?? throw new InvalidOperationException("Grid worker animator is missing.");
            _seat = FindSeat(grid, seatId);
            _direction = FacingDirection(_seat.Facing);
            MemberId = memberId.Trim();
            _poseProfile = poseCatalog.Resolve(
                MemberId,
                _direction,
                OfficeSeatingAnimationClip.Work,
                0);
            _navigationDelaySeconds = Mathf.Max(0f, navigationDelaySeconds);
            _configuredAt = Time.time;
            _phase = OfficeGridSeatingPhase.WaitingForNavigation;
            _visualResetError = 0f;
            _alignedClip = null;
            _alignedFrame = -1;
            _hasPreviousAlignedVisualPosition = false;
            _maxFrameCorrectionJumpWorld = 0f;
            _maxWorkPelvisErrorPx = 0f;
            _maxWorkHandErrorPx = 0f;
            _animator.ConfigureOfficeSeating(sitDownFrames, workFrames, standUpFrames);
        }

        public float FootError()
        {
            if (_seat == null || _gridPresenter == null) return float.PositiveInfinity;
            return Vector3.Distance(transform.position, _gridPresenter.CellCenterWorld(_seat.Cell));
        }

        public float PelvisSeatScreenError(Camera camera)
        {
            if (_poseProfile == null || _mover == null || _furniturePresenter == null)
                return float.PositiveInfinity;
            return OfficeGridAlignmentMetrics.ScreenDistance(
                camera,
                _mover.SpriteAnchorWorld(_poseProfile.PelvisAnchorPx),
                _furniturePresenter.OperatorSeatSocketWorld(_seat.WorkSurfaceFurnitureId));
        }

        public float ChairDeskSeatScreenError(Camera camera)
        {
            if (_seat == null || _furniturePresenter == null) return float.PositiveInfinity;
            return OfficeGridAlignmentMetrics.ScreenDistance(
                camera,
                _furniturePresenter.SeatAnchorWorld(_seat.ChairFurnitureId),
                _furniturePresenter.OperatorSeatSocketWorld(_seat.WorkSurfaceFurnitureId));
        }

        public float HandWorkScreenError(Camera camera)
        {
            if (_poseProfile == null || _mover == null || _furniturePresenter == null)
                return float.PositiveInfinity;
            return OfficeGridAlignmentMetrics.ScreenDistance(
                camera,
                _mover.SpriteAnchorWorld(_poseProfile.HandAnchorPx),
                _furniturePresenter.OperatorWorkSocketWorld(_seat.WorkSurfaceFurnitureId));
        }

        public void RequestStandAndReseat()
        {
            if (_phase != OfficeGridSeatingPhase.Working)
                throw new InvalidOperationException(MemberId + " can only stand from Working state.");
            _standAndReseatRequested = true;
            _animator.RequestOfficeWorkSafeStop();
        }

        private void Update()
        {
            if (_seat == null) return;
            RefreshPoseAlignmentForCurrentFrame();
            switch (_phase)
            {
                case OfficeGridSeatingPhase.WaitingForNavigation:
                    if (Time.time - _configuredAt < _navigationDelaySeconds) return;
                    BeginSeatApproach();
                    break;
                case OfficeGridSeatingPhase.MovingToApproach:
                    TickApproachPath(Time.deltaTime);
                    break;
                case OfficeGridSeatingPhase.AligningToSeat:
                    TickPrecisionMove(_gridPresenter.CellCenterWorld(_seat.Cell), BeginSitDown, Time.deltaTime);
                    break;
                case OfficeGridSeatingPhase.SittingDown:
                    if (!_animator.IsOfficeSeatingTransitionComplete) return;
                    if (!_animator.BeginSeatedWork())
                        throw new InvalidOperationException(MemberId + " could not begin seated work animation.");
                    RefreshPoseAlignmentForCurrentFrame(force: true);
                    _mover.RefreshSortingOrder();
                    _furniturePresenter.ApplySeatOcclusion(_seat, _mover.TargetRenderer.sortingOrder);
                    _phase = OfficeGridSeatingPhase.Working;
                    break;
                case OfficeGridSeatingPhase.Working:
                    TrackWorkMetrics();
                    if (!_standAndReseatRequested || !_animator.IsOfficeWorkSafeToStand) return;
                    if (!_animator.BeginStandUp())
                        throw new InvalidOperationException(MemberId + " could not begin stand-up animation.");
                    RefreshPoseAlignmentForCurrentFrame(force: true);
                    _phase = OfficeGridSeatingPhase.StandingUp;
                    break;
                case OfficeGridSeatingPhase.StandingUp:
                    if (!_animator.IsOfficeSeatingTransitionComplete) return;
                    _mover.ResetVisualPose();
                    _visualResetError = _mover.VisualLocalOffset.magnitude;
                    _furniturePresenter.ClearSeatOcclusion(_seat);
                    _animator.ResumeWalkingAfterSeating();
                    _phase = OfficeGridSeatingPhase.LeavingSeat;
                    break;
                case OfficeGridSeatingPhase.LeavingSeat:
                    TickPrecisionMove(_gridPresenter.CellCenterWorld(_seat.ApproachCell), FinishLeavingSeat, Time.deltaTime);
                    break;
            }
        }

        private void BeginSeatApproach()
        {
            if (!OfficeSeatRuntimeClaim.TryReserve(
                    _seatingState,
                    _seat.SeatId,
                    MemberId,
                    "office-grid:" + MemberId + ":" + _seat.SeatId,
                    out _claim,
                    out OfficeSeatOperationResult result))
            {
                throw new InvalidOperationException(
                    $"{MemberId} could not reserve {_seat.SeatId}: {result?.Failure}.");
            }

            _mover.SetRouteMovementEnabled(false);
            OfficeGridCoordinate start = _gridPresenter.NearestCell(transform.position);
            if (!_grid.IsWalkable(start))
                throw new InvalidOperationException($"{MemberId} starts seating from blocked cell {start}.");
            _path = FindPath(start, _seat.ApproachCell);
            _pathIndex = _path.Count > 1 ? 1 : 0;
            _phase = OfficeGridSeatingPhase.MovingToApproach;
            if (_path.Count == 1) BeginSeatAlignment();
        }

        private void TickApproachPath(float deltaTime)
        {
            Vector3 target = _gridPresenter.CellCenterWorld(_path[_pathIndex]);
            TickPrecisionMove(target, () =>
            {
                if (_pathIndex < _path.Count - 1)
                {
                    _pathIndex++;
                    return;
                }
                BeginSeatAlignment();
            }, deltaTime);
        }

        private void BeginSeatAlignment()
        {
            transform.position = _gridPresenter.CellCenterWorld(_seat.ApproachCell);
            _animator.SetWorldVelocity(Vector3.zero);
            _phase = OfficeGridSeatingPhase.AligningToSeat;
        }

        private void TickPrecisionMove(Vector3 target, Action arrived, float deltaTime)
        {
            Vector3 displacement = target - transform.position;
            var step = OfficeSeatPrecisionMotion.Advance(
                transform.position.x,
                transform.position.y,
                target.x,
                target.y,
                OfficeSeatPrecisionMotion.ApproachSpeedMetersPerSecond,
                deltaTime);
            transform.position = new Vector3((float)step.X, (float)step.Z, transform.position.z);
            Vector3 velocity = step.Arrived
                ? Vector3.zero
                : new Vector3(displacement.x, 0f, displacement.y).normalized;
            _animator.SetWorldVelocity(velocity);
            _mover.RefreshSortingOrder();
            if (step.Arrived) arrived();
        }

        private void BeginSitDown()
        {
            transform.position = _gridPresenter.CellCenterWorld(_seat.Cell);
            _animator.SetWorldVelocity(Vector3.zero);
            if (!_claim.TryOccupy(out OfficeSeatOperationResult result))
                throw new InvalidOperationException($"{MemberId} could not occupy {_seat.SeatId}: {result?.Failure}.");
            if (!_animator.PrepareOfficeSeatingFacing(_direction, OfficeSeatForegroundOcclusionMode.Default))
                throw new InvalidOperationException(MemberId + " could not prepare seat facing.");
            if (!_animator.BeginSitDown(_direction))
                throw new InvalidOperationException(MemberId + " could not begin sit-down animation.");
            RefreshPoseAlignmentForCurrentFrame(force: true);
            _mover.RefreshSortingOrder();
            _furniturePresenter.ApplySeatOcclusion(_seat, _mover.TargetRenderer.sortingOrder);
            _phase = OfficeGridSeatingPhase.SittingDown;
        }

        private void RefreshPoseAlignmentForCurrentFrame(bool force = false)
        {
            if (!_animator.CurrentOfficeSeatingClip.HasValue) return;
            OfficeSeatingAnimationClip clip = _animator.CurrentOfficeSeatingClip.Value;
            int frame = _animator.CurrentOfficeSeatingFrame;
            if (!force && _alignedClip == clip && _alignedFrame == frame) return;
            _poseProfile = _poseCatalog.Resolve(MemberId, _direction, clip, frame);
            ApplyPoseAlignment(_poseProfile);
            _alignedClip = clip;
            _alignedFrame = frame;
        }

        private void ApplyPoseAlignment(OfficeCharacterSeatPoseProfile profile)
        {
            _mover.ResetVisualPose();
            _mover.SetSeatedVisualPose(
                Vector3.zero,
                profile.UniformScale,
                profile.RotationDegrees);
            Vector3 pelvisWorld = _mover.SpriteAnchorWorld(profile.PelvisAnchorPx);
            Vector3 seatWorld = _furniturePresenter.OperatorSeatSocketWorld(_seat.WorkSurfaceFurnitureId);
            Vector3 localDelta = transform.InverseTransformVector(seatWorld - pelvisWorld);
            _mover.SetSeatedVisualPose(
                localDelta,
                profile.UniformScale,
                profile.RotationDegrees);

            Vector3 currentVisualPosition = _mover.VisualRoot.position;
            if (_hasPreviousAlignedVisualPosition)
                _maxFrameCorrectionJumpWorld = Mathf.Max(
                    _maxFrameCorrectionJumpWorld,
                    Vector3.Distance(_previousAlignedVisualPosition, currentVisualPosition));
            _previousAlignedVisualPosition = currentVisualPosition;
            _hasPreviousAlignedVisualPosition = true;
        }

        private void TrackWorkMetrics()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            _maxWorkPelvisErrorPx = Mathf.Max(_maxWorkPelvisErrorPx, PelvisSeatScreenError(camera));
            _maxWorkHandErrorPx = Mathf.Max(_maxWorkHandErrorPx, HandWorkScreenError(camera));
        }

        private void FinishLeavingSeat()
        {
            transform.position = _gridPresenter.CellCenterWorld(_seat.ApproachCell);
            if (_claim != null && !_claim.TryRelease(out OfficeSeatOperationResult result))
                throw new InvalidOperationException($"{MemberId} could not release {_seat.SeatId}: {result?.Failure}.");
            _claim = null;
            _standAndReseatRequested = false;
            _navigationDelaySeconds = 0.1f;
            _configuredAt = Time.time;
            _phase = OfficeGridSeatingPhase.WaitingForNavigation;
        }

        private List<OfficeGridCoordinate> FindPath(OfficeGridCoordinate start, OfficeGridCoordinate target)
        {
            var queue = new Queue<OfficeGridCoordinate>();
            var previous = new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate>();
            var visited = new HashSet<OfficeGridCoordinate> { start };
            var claimedByOthers = ClaimedSeatCellsByOthers();
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
                OfficeGridCoordinate current = queue.Dequeue();
                if (current.Equals(target)) break;
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!_grid.Contains(next) || !_grid.IsWalkable(next) ||
                        claimedByOthers.Contains(next) || !visited.Add(next)) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }
            if (!visited.Contains(target))
                throw new InvalidOperationException($"No walkable approach path exists: {start} -> {target}.");
            var path = new List<OfficeGridCoordinate> { target };
            while (!path[path.Count - 1].Equals(start)) path.Add(previous[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }

        private HashSet<OfficeGridCoordinate> ClaimedSeatCellsByOthers()
        {
            var result = new HashSet<OfficeGridCoordinate>();
            foreach (OfficeSeatView view in _seatingState.GetSeats())
            {
                if ((view.State != OfficeSeatMeaningState.Reserved && view.State != OfficeSeatMeaningState.Occupied) ||
                    string.Equals(view.RuntimeMemberId, MemberId, StringComparison.Ordinal)) continue;
                result.Add(FindSeat(_grid, view.SeatId).Cell);
            }
            return result;
        }

        private void OnDestroy()
        {
            _claim?.Dispose();
            _claim = null;
        }

        private static OfficeSeatSlot FindSeat(OfficeGrid grid, string seatId)
        {
            foreach (OfficeSeatSlot seat in grid.SeatSlots)
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
    }
}

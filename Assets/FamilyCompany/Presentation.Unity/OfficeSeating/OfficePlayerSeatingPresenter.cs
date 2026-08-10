using System;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class OfficePlayerSeatingPresenter : MonoBehaviour
    {
        private const string PlayerId = "player";
        private const float MaximumApproachDistance = 1.75f;

        private PrototypeBootstrap _bootstrap;
        private OfficeSeatRegistry _registry;
        private OfficeSeatingState _state;
        private PlayerOfficeWorkInteractor _workInteractor;
        private PrototypePlayerController _playerController;
        private CharacterController _characterController;
        private DirectionalSpriteAnimator _animator;
        private OfficeSeatRuntimeClaim _claim;
        private OfficeSeatAuthoring _seat;
        private OfficeWorkerSeatingPhase _phase;
        private bool _releaseRequested;
        private bool _previousPlayerControllerEnabled;
        private bool _previousCharacterControllerEnabled;
        private bool _movementSuspended;

        public OfficeWorkerSeatingPhase SeatingPhase => _phase;
        public string ActiveSeatId => _claim == null || _claim.IsReleased ? string.Empty : _claim.SeatId;

        public void Configure(
            PrototypeBootstrap bootstrap,
            OfficeSeatRegistry registry,
            OfficeSeatingState state)
        {
            if (bootstrap == null) throw new ArgumentNullException(nameof(bootstrap));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_state != null && _state != state) ReleaseImmediately();
            _bootstrap = bootstrap;
            _registry = registry;
            _state = state;
            CacheComponents();
        }

        public void ResetOfficeSeatingRuntime()
        {
            ReleaseImmediately();
            _bootstrap = null;
            _registry = null;
            _state = null;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!IsReady()) return;
            var wantsSeat = _workInteractor.IsWorking;
            if (_phase == OfficeWorkerSeatingPhase.None)
            {
                if (wantsSeat) TryBeginSeating();
                return;
            }

            if (!wantsSeat) _releaseRequested = true;
            switch (_phase)
            {
                case OfficeWorkerSeatingPhase.SittingDown:
                    if (!_animator.IsOfficeSeatingTransitionComplete) return;
                    _animator.BeginSeatedWork();
                    _phase = OfficeWorkerSeatingPhase.Working;
                    if (_releaseRequested) BeginStandingUp();
                    break;
                case OfficeWorkerSeatingPhase.Working:
                    if (_releaseRequested) BeginStandingUp();
                    break;
                case OfficeWorkerSeatingPhase.StandingUp:
                    if (!_animator.IsOfficeSeatingTransitionComplete) return;
                    FinishStandingUp();
                    break;
            }
        }

        private void OnDestroy()
        {
            ReleaseImmediately();
        }

        private bool TryBeginSeating()
        {
            var seats = _state.GetSeats()
                .Where(item => item.State != OfficeSeatMeaningState.Reserved &&
                               item.State != OfficeSeatMeaningState.Occupied)
                .Where(item => string.IsNullOrEmpty(item.AssignedMemberId) ||
                               string.Equals(item.AssignedMemberId, PlayerId, StringComparison.Ordinal))
                .Where(item => _registry.TryGetAuthoring(item.SeatId, out _))
                .ToArray();
            var hasAssignedSeat = seats.Any(item =>
                string.Equals(item.AssignedMemberId, PlayerId, StringComparison.Ordinal));
            var candidate = seats
                .Where(item => !hasAssignedSeat ||
                               string.Equals(item.AssignedMemberId, PlayerId, StringComparison.Ordinal))
                .Select(item => new
                {
                    Seat = item,
                    Distance = ApproachDistanceSquared(item.SeatId)
                })
                .Where(item => item.Distance <= MaximumApproachDistance * MaximumApproachDistance)
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Seat.SeatId, StringComparer.Ordinal)
                .Select(item => item.Seat)
                .FirstOrDefault();
            if (candidate == null || !_registry.TryGetAuthoring(candidate.SeatId, out var authoring))
                return false;

            var token =
                $"office-seat-player-v1:{_bootstrap.State.WorldSeed}:" +
                $"{_bootstrap.State.Time.ElapsedMinutes}:{candidate.SeatId}";
            if (!OfficeSeatRuntimeClaim.TryReserve(
                    _state,
                    candidate.SeatId,
                    PlayerId,
                    token,
                    out var claim,
                    out _))
            {
                return false;
            }
            if (!authoring.TryResolveFacing(out var facing) || !claim.TryOccupy(out _))
            {
                claim.Dispose();
                return false;
            }

            _claim = claim;
            _seat = authoring;
            _releaseRequested = false;
            SuspendPlayerMovement();
            var sit = authoring.SitAnchor.position;
            transform.position = new Vector3(sit.x, transform.position.y, sit.z);
            if (!_animator.BeginSitDown((int)facing))
            {
                ReleaseImmediately();
                return false;
            }

            _phase = OfficeWorkerSeatingPhase.SittingDown;
            return true;
        }

        private void BeginStandingUp()
        {
            if (_phase == OfficeWorkerSeatingPhase.StandingUp) return;
            if (!_animator.BeginStandUp())
            {
                ReleaseImmediately();
                return;
            }
            _phase = OfficeWorkerSeatingPhase.StandingUp;
        }

        private void FinishStandingUp()
        {
            if (_seat != null)
            {
                var approach = _seat.ApproachAnchor.position;
                transform.position = new Vector3(approach.x, transform.position.y, approach.z);
            }
            ReleaseImmediately();
        }

        private void ReleaseImmediately()
        {
            _claim?.TryRelease(out _);
            _claim = null;
            _seat = null;
            _releaseRequested = false;
            _phase = OfficeWorkerSeatingPhase.None;
            _animator?.ResumeWalkingAfterSeating();
            RestorePlayerMovement();
        }

        private double ApproachDistanceSquared(string seatId)
        {
            if (!_registry.TryGetAuthoring(seatId, out var authoring)) return double.PositiveInfinity;
            var position = authoring.ApproachAnchor.position;
            var deltaX = position.x - transform.position.x;
            var deltaZ = position.z - transform.position.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private bool IsReady()
        {
            return _bootstrap != null &&
                   _bootstrap.State != null &&
                   _registry != null &&
                   _state != null &&
                   _workInteractor != null &&
                   _animator != null &&
                   _animator.HasOfficeSeatingFrames;
        }

        private void CacheComponents()
        {
            _workInteractor = GetComponent<PlayerOfficeWorkInteractor>();
            _playerController = GetComponent<PrototypePlayerController>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<DirectionalSpriteAnimator>();
        }

        private void SuspendPlayerMovement()
        {
            if (_movementSuspended) return;
            if (_playerController != null)
            {
                _previousPlayerControllerEnabled = _playerController.enabled;
                _playerController.enabled = false;
            }
            if (_characterController != null)
            {
                _previousCharacterControllerEnabled = _characterController.enabled;
                _characterController.enabled = false;
            }
            _movementSuspended = true;
        }

        private void RestorePlayerMovement()
        {
            if (!_movementSuspended) return;
            if (_characterController != null)
                _characterController.enabled = _previousCharacterControllerEnabled;
            if (_playerController != null)
                _playerController.enabled = _previousPlayerControllerEnabled;
            _movementSuspended = false;
        }
    }
}

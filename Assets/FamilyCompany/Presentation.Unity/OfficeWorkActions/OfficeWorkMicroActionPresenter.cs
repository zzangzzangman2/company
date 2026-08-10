using System;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeWorkActions
{
    [DisallowMultipleComponent]
    public sealed class OfficeWorkMicroActionPresenter : MonoBehaviour, IOfficeWorkSeatingPresentationHook
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Behaviour existingWorkLoopFrameWriter;
        [SerializeField] private OfficeWorkActionFrameSet frameSet;
        [SerializeField] private OfficeSeatFacing8 facing = OfficeSeatFacing8.North;

        private OfficeWorkMicroActionStateMachine _machine;
        private OfficeWorkMicroActionContext _context = OfficeWorkMicroActionContext.SitDown;
        private double _subMillisecondRemainder;
        private bool _usingExistingWorkLoop = true;
        private bool _ownsSpriteWriter;
        private bool _existingWriterWasEnabled;
        private bool _fallbackStandHandoffReady;
        private bool _readyEventRaised;
        private bool _sessionActive;
        private bool _handoffRequested;

        public event Action StandHandoffReady;

        public bool IsUsingExistingWorkLoop => _usingExistingWorkLoop;
        public bool IsStandHandoffReady => _machine == null
            ? _fallbackStandHandoffReady
            : _machine.IsStandHandoffReady;
        public bool OwnsSpriteWriter => _ownsSpriteWriter;
        public OfficeWorkMicroAction CurrentAction => _machine?.CurrentAction ?? OfficeWorkMicroAction.None;
        public long SessionElapsedMilliseconds => _machine?.ProcessedMilliseconds ?? 0L;
        public OfficeWorkMicroActionAvailability AvailableActions => frameSet == null
            ? OfficeWorkMicroActionAvailability.None
            : frameSet.Availability;

        /// <summary>
        /// Injects the art-owned frame set without touching seating, navigation, or gameplay state.
        /// The existing writer is suspended only while usable micro-action frames own the renderer.
        /// </summary>
        public void Configure(
            SpriteRenderer configuredRenderer,
            Behaviour configuredExistingWorkLoopFrameWriter,
            OfficeWorkActionFrameSet configuredFrameSet,
            OfficeSeatFacing8 configuredFacing)
        {
            if (_sessionActive && !IsStandHandoffReady)
                throw new InvalidOperationException("Cannot reconfigure during an active office work action.");
            if (configuredExistingWorkLoopFrameWriter == this)
                throw new ArgumentException("The presenter cannot be its own fallback frame writer.", nameof(configuredExistingWorkLoopFrameWriter));
            if (!OfficeSeatGeometryRules.IsValidFacing(configuredFacing))
                throw new ArgumentOutOfRangeException(nameof(configuredFacing));

            ReleaseSpriteWriter();
            targetRenderer = configuredRenderer;
            existingWorkLoopFrameWriter = configuredExistingWorkLoopFrameWriter;
            frameSet = configuredFrameSet;
            facing = configuredFacing;
            ResetSession();
        }

        public void SetFacing(OfficeSeatFacing8 configuredFacing)
        {
            if (!OfficeSeatGeometryRules.IsValidFacing(configuredFacing))
                throw new ArgumentOutOfRangeException(nameof(configuredFacing));
            facing = configuredFacing;
            if (!TrySynchronizeCurrentFrame()) FallBackToExistingWorkLoop();
        }

        public void NotifySitDownStarted()
        {
            if (_sessionActive && !IsStandHandoffReady)
                throw new InvalidOperationException("Cannot start sit-down during an active office work action.");
            ReleaseSpriteWriter();
            ResetSession();
            _context = OfficeWorkMicroActionContext.SitDown;
        }

        public bool NotifySeatedWorkStarted(int worldSeed, string memberId, long sessionStartedMinute)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (sessionStartedMinute < 0)
                throw new ArgumentOutOfRangeException(nameof(sessionStartedMinute));
            if (_sessionActive)
                throw new InvalidOperationException("A seated work presentation session is already active.");

            ReleaseSpriteWriter();
            ResetSession();
            _sessionActive = true;
            _context = OfficeWorkMicroActionContext.SeatedWork;
            var availability = ResolveAvailabilityFor(memberId);
            if (targetRenderer == null ||
                OfficeWorkMicroActionAvailabilityRules.ShouldUseExistingWorkLoop(availability))
            {
                _usingExistingWorkLoop = true;
                return false;
            }

            _machine = new OfficeWorkMicroActionStateMachine(
                worldSeed,
                memberId,
                sessionStartedMinute,
                availability);
            _usingExistingWorkLoop = false;
            var started = false;
            try
            {
                ProcessTransitions(_machine.AdvanceTo(0L, _context));
                started = TrySynchronizeCurrentFrame();
                return started;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (!started) FallBackToExistingWorkLoop();
            }
        }

        public OfficeWorkStandHandoffStatus RequestStandHandoff(OfficeWorkExitReason reason)
        {
            _context = ContextFor(reason);
            _handoffRequested = true;
            if (_machine == null)
            {
                _fallbackStandHandoffReady = true;
                RaiseReadyEventOnce();
                return OfficeWorkStandHandoffStatus.ReadyToStand;
            }

            try
            {
                ProcessTransitions(_machine.AdvanceTo(_machine.ProcessedMilliseconds, _context));
                if (!TrySynchronizeCurrentFrame()) FallBackToExistingWorkLoop();
            }
            catch (Exception)
            {
                FallBackToExistingWorkLoop();
            }
            return _machine == null || _machine.IsStandHandoffReady
                ? OfficeWorkStandHandoffStatus.ReadyToStand
                : OfficeWorkStandHandoffStatus.WaitingForCurrentAction;
        }

        public void NotifyStandUpStarted()
        {
            if (!IsStandHandoffReady)
            {
                throw new InvalidOperationException(
                    "Stand-up cannot start until the current office work micro-action completes.");
            }

            try
            {
                ReleaseSpriteWriter();
            }
            finally
            {
                ResetSession();
                _context = OfficeWorkMicroActionContext.StandUp;
            }
        }

        public void TickMilliseconds(long deltaMilliseconds)
        {
            if (deltaMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
            if (_machine == null || deltaMilliseconds == 0) return;

            try
            {
                var target = checked(_machine.ProcessedMilliseconds + deltaMilliseconds);
                ProcessTransitions(_machine.AdvanceTo(target, _context));
                if (!TrySynchronizeCurrentFrame()) FallBackToExistingWorkLoop();
            }
            catch (Exception)
            {
                FallBackToExistingWorkLoop();
                throw;
            }
        }

        private void Update()
        {
            if (_machine == null) return;
            _subMillisecondRemainder += Math.Max(0d, Time.deltaTime * 1000d);
            var wholeMilliseconds = (long)Math.Floor(_subMillisecondRemainder);
            if (wholeMilliseconds <= 0) return;
            _subMillisecondRemainder -= wholeMilliseconds;
            TickMilliseconds(wholeMilliseconds);
        }

        private void OnDisable()
        {
            CancelActiveSessionForLifecycle();
        }

        private void OnDestroy()
        {
            CancelActiveSessionForLifecycle();
        }

        private OfficeWorkMicroActionAvailability ResolveAvailabilityFor(string memberId)
        {
            if (frameSet == null) return OfficeWorkMicroActionAvailability.None;
            var configuredMemberId = frameSet.MemberId;
            if (configuredMemberId.Length > 0 &&
                !string.Equals(configuredMemberId, memberId.Trim(), StringComparison.Ordinal))
                return OfficeWorkMicroActionAvailability.None;
            return frameSet.Availability;
        }

        private bool TrySynchronizeCurrentFrame()
        {
            if (_machine == null) return true;
            var action = _machine.CurrentAction;
            if (action == OfficeWorkMicroAction.None)
            {
                ReleaseSpriteWriter();
                _usingExistingWorkLoop = true;
                return true;
            }
            if (targetRenderer == null || frameSet == null ||
                !frameSet.TryGetUsableClip(action, out var clip))
            {
                return false;
            }

            try
            {
                var sprite = clip.ResolveFrame(
                    (int)facing,
                    _machine.CurrentActionElapsedMilliseconds);
                if (sprite == null) return false;
                AcquireSpriteWriter();
                targetRenderer.sprite = sprite;
                _usingExistingWorkLoop = false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AcquireSpriteWriter()
        {
            if (_ownsSpriteWriter) return;
            if (existingWorkLoopFrameWriter != null)
            {
                var wasEnabled = existingWorkLoopFrameWriter.enabled;
                try
                {
                    existingWorkLoopFrameWriter.enabled = false;
                    _existingWriterWasEnabled = wasEnabled;
                }
                catch (Exception)
                {
                    existingWorkLoopFrameWriter.enabled = wasEnabled;
                    throw;
                }
            }
            _ownsSpriteWriter = true;
        }

        private void ReleaseSpriteWriter()
        {
            if (!_ownsSpriteWriter) return;
            try
            {
                if (existingWorkLoopFrameWriter != null)
                    existingWorkLoopFrameWriter.enabled = _existingWriterWasEnabled;
            }
            finally
            {
                _ownsSpriteWriter = false;
                _existingWriterWasEnabled = false;
            }
        }

        private void FallBackToExistingWorkLoop()
        {
            try
            {
                ReleaseSpriteWriter();
            }
            finally
            {
                _machine = null;
                _subMillisecondRemainder = 0d;
                _usingExistingWorkLoop = true;
                if (_handoffRequested)
                {
                    _fallbackStandHandoffReady = true;
                    RaiseReadyEventOnce();
                }
            }
        }

        private void CancelActiveSessionForLifecycle()
        {
            if (!_sessionActive)
            {
                ReleaseSpriteWriter();
                return;
            }

            var notifyReady = _handoffRequested;
            try
            {
                ReleaseSpriteWriter();
            }
            finally
            {
                _machine = null;
                _subMillisecondRemainder = 0d;
                _usingExistingWorkLoop = true;
                _sessionActive = false;
                _handoffRequested = false;
                if (notifyReady)
                {
                    _fallbackStandHandoffReady = true;
                    RaiseReadyEventOnce();
                }
            }
        }

        private void ProcessTransitions(System.Collections.Generic.IReadOnlyList<OfficeWorkMicroActionTransition> transitions)
        {
            for (var index = 0; index < transitions.Count; index++)
            {
                if (transitions[index].Kind == OfficeWorkMicroActionTransitionKind.StandHandoffReady)
                    RaiseReadyEventOnce();
            }
        }

        private void RaiseReadyEventOnce()
        {
            if (_readyEventRaised) return;
            _readyEventRaised = true;
            StandHandoffReady?.Invoke();
        }

        private void ResetSession()
        {
            _machine = null;
            _subMillisecondRemainder = 0d;
            _usingExistingWorkLoop = true;
            _fallbackStandHandoffReady = false;
            _readyEventRaised = false;
            _sessionActive = false;
            _handoffRequested = false;
        }

        private static OfficeWorkMicroActionContext ContextFor(OfficeWorkExitReason reason)
        {
            switch (reason)
            {
                case OfficeWorkExitReason.StandUp:
                    return OfficeWorkMicroActionContext.StandUp;
                case OfficeWorkExitReason.Meeting:
                    return OfficeWorkMicroActionContext.Meeting;
                case OfficeWorkExitReason.Printing:
                    return OfficeWorkMicroActionContext.Printing;
                case OfficeWorkExitReason.Moving:
                    return OfficeWorkMicroActionContext.Moving;
                case OfficeWorkExitReason.OutsideSchedule:
                    return OfficeWorkMicroActionContext.OutsideSchedule;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }
}

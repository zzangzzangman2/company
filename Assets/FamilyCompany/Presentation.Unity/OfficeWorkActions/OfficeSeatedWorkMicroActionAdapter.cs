using System;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeWorkActions
{
    /// <summary>
    /// Pull-only bridge between the office seating animation session and the deterministic
    /// micro-action state machine. DirectionalSpriteAnimator remains the only sprite writer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeSeatedWorkMicroActionAdapter : MonoBehaviour, IOfficeSeatedWorkAnimationHook
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private OfficeWorkActionFrameSet frameSet;

        private Session _activeSession;

        public string MemberId => (memberId ?? string.Empty).Trim();
        public OfficeWorkActionFrameSet FrameSet => frameSet;
        public bool HasActiveSession => _activeSession != null && !_activeSession.IsDisposed;

        public void Configure(
            PrototypeBootstrap configuredBootstrap,
            string configuredMemberId,
            OfficeWorkActionFrameSet configuredFrameSet)
        {
            DisposeActiveSession();
            bootstrap = configuredBootstrap;
            memberId = configuredMemberId ?? string.Empty;
            frameSet = configuredFrameSet;
        }

        public bool TryBegin(int lockedDirection, out IOfficeSeatedWorkAnimationSession session)
        {
            DisposeActiveSession();
            session = null;

            if (!isActiveAndEnabled ||
                lockedDirection < 0 ||
                lockedDirection >= OfficeWorkMicroActionAvailabilityRules.DirectionCount ||
                bootstrap == null ||
                bootstrap.State == null ||
                frameSet == null)
            {
                return false;
            }

            var normalizedMemberId = MemberId;
            if (normalizedMemberId.Length == 0 ||
                !string.Equals(frameSet.MemberId, normalizedMemberId, StringComparison.Ordinal))
            {
                return false;
            }

            var availability = frameSet.Availability;
            if (OfficeWorkMicroActionAvailabilityRules.ShouldUseExistingWorkLoop(availability))
                return false;

            var state = bootstrap.State;
            var created = new Session(
                state.WorldSeed,
                normalizedMemberId,
                state.Time.ElapsedMinutes,
                availability,
                lockedDirection,
                frameSet);
            _activeSession = created;
            session = created;
            return true;
        }

        private void OnDisable()
        {
            DisposeActiveSession();
        }

        private void OnDestroy()
        {
            DisposeActiveSession();
        }

        private void DisposeActiveSession()
        {
            _activeSession?.Dispose();
            _activeSession = null;
        }

        private sealed class Session : IOfficeSeatedWorkAnimationSession
        {
            private readonly OfficeWorkMicroActionStateMachine _machine;
            private readonly int _lockedDirection;
            private readonly OfficeWorkActionFrameSet _frameSet;
            private double _subMillisecondRemainder;
            private bool _stopRequested;
            private bool _disposed;
            private long _safeStopAtMilliseconds = -1L;

            public Session(
                int worldSeed,
                string configuredMemberId,
                long sessionStartedMinute,
                OfficeWorkMicroActionAvailability availability,
                int lockedDirection,
                OfficeWorkActionFrameSet configuredFrameSet)
            {
                _lockedDirection = lockedDirection;
                _frameSet = configuredFrameSet;
                _machine = new OfficeWorkMicroActionStateMachine(
                    worldSeed,
                    configuredMemberId,
                    sessionStartedMinute,
                    availability);
                _machine.AdvanceTo(0L, OfficeWorkMicroActionContext.SeatedWork);
            }

            public bool IsDisposed => _disposed;
            public OfficeWorkMicroAction CurrentAction =>
                _disposed ? OfficeWorkMicroAction.None : _machine.CurrentAction;

            public Sprite CurrentSprite
            {
                get
                {
                    if (_disposed || _frameSet == null) return null;
                    var action = _machine.CurrentAction;
                    if (action == OfficeWorkMicroAction.None ||
                        !_frameSet.TryGetUsableClip(action, out var clip))
                    {
                        return null;
                    }

                    return clip.ResolveFrame(
                        _lockedDirection,
                        _machine.CurrentActionElapsedMilliseconds);
                }
            }

            public bool IsSafeToStand =>
                _disposed ||
                _machine.IsStandHandoffReady ||
                (_stopRequested && _safeStopAtMilliseconds >= 0L &&
                 _machine.ProcessedMilliseconds >= _safeStopAtMilliseconds);

            public void Tick(float deltaTime)
            {
                if (_disposed || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                    return;

                _subMillisecondRemainder += deltaTime * 1000d;
                var wholeMilliseconds = (long)Math.Floor(_subMillisecondRemainder);
                if (wholeMilliseconds <= 0L) return;
                _subMillisecondRemainder -= wholeMilliseconds;

                var targetMilliseconds = checked(_machine.ProcessedMilliseconds + wholeMilliseconds);
                _machine.AdvanceTo(
                    targetMilliseconds,
                    _stopRequested
                        ? OfficeWorkMicroActionContext.StandUp
                        : OfficeWorkMicroActionContext.SeatedWork);
            }

            public void RequestSafeStop()
            {
                if (_disposed || _stopRequested) return;
                _stopRequested = true;
                if (_frameSet.TryGetUsableClip(_machine.CurrentAction, out OfficeWorkActionClip clip) &&
                    clip.Loop)
                {
                    long loopMilliseconds = checked((long)clip.FramesPerDirection * clip.MillisecondsPerFrame);
                    long phase = loopMilliseconds <= 0L
                        ? 0L
                        : _machine.CurrentActionElapsedMilliseconds % loopMilliseconds;
                    long remaining = phase == 0L ? 0L : loopMilliseconds - phase;
                    _safeStopAtMilliseconds = checked(_machine.ProcessedMilliseconds + remaining);
                }
                _machine.AdvanceTo(
                    _machine.ProcessedMilliseconds,
                    OfficeWorkMicroActionContext.StandUp);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _stopRequested = true;
                _safeStopAtMilliseconds = -1L;
                _subMillisecondRemainder = 0d;
            }
        }
    }
}

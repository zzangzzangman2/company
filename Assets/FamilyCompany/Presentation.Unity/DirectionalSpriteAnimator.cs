using System;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        public const int DirectionCount = 8;
        public const int WalkFrameCount = 6;
        public const int RequiredFrameCount = DirectionCount * WalkFrameCount;

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private float frameSeconds = 0.11f;
        [SerializeField, Range(0, WalkFrameCount - 1)] private int idleWalkFrame = 2;
        [SerializeField] private Sprite[] sitDownFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] seatedWorkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] standUpFrames = Array.Empty<Sprite>();
        [SerializeField] private float seatingTransitionFrameSeconds = 0.11f;
        [SerializeField] private float seatedWorkFrameSeconds = 0.14f;
        [SerializeField, Range(0f, 20f)] private float facingHysteresisDegrees = 7.5f;
        private Vector3 _worldVelocity;
        private float _frameClock;
        private int _walkFrame;
        private int _lastDirection;
        private OfficeSeatingAnimationClip? _seatingClip;
        private float _seatingFrameClock;
        private int _seatingFrame;
        private bool _seatingTransitionComplete;
        private IOfficeSeatedWorkAnimationHook _officeWorkHook;
        private IOfficeSeatedWorkAnimationSession _officeWorkSession;
        private int _preSeatingSortingOrder;
        private bool _seatingSortingOrderActive;
        private bool _navigationAnimationSuppressed;
        private bool _tileDisplacementDirection;

        public int CurrentDirection => _lastDirection;
        public int CurrentWalkFrame => _walkFrame;
        public bool IsMoving => _tileDisplacementDirection
            ? _worldVelocity.sqrMagnitude > 0.0000001f
            : _worldVelocity.sqrMagnitude > 0.0025f;
        public int ConfiguredFrameCount => walkFrames?.Length ?? 0;
        public float BaseFrameSeconds => frameSeconds;
        public float EffectiveFrameSeconds => ResolveEffectiveFrameSeconds();
        public int IdleWalkFrame => Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
        public Sprite CurrentSprite => targetRenderer == null ? null : targetRenderer.sprite;
        public bool HasOfficeSeatingFrames =>
            HasCompleteFrames(sitDownFrames, OfficeSeatingAnimationFrames.SitDownSpriteCount) &&
            HasCompleteFrames(seatedWorkFrames, OfficeSeatingAnimationFrames.WorkSpriteCount) &&
            HasCompleteFrames(standUpFrames, OfficeSeatingAnimationFrames.StandUpSpriteCount);
        public int ConfiguredOfficeSeatingFrameCount =>
            (sitDownFrames?.Length ?? 0) +
            (seatedWorkFrames?.Length ?? 0) +
            (standUpFrames?.Length ?? 0);
        public bool IsOfficeSeatingPoseActive => _seatingClip.HasValue;
        public bool IsOfficeSeatingTransitionComplete =>
            _seatingClip.HasValue &&
            _seatingClip.Value != OfficeSeatingAnimationClip.Work &&
            _seatingTransitionComplete;
        public OfficeSeatingAnimationClip? CurrentOfficeSeatingClip => _seatingClip;
        public int CurrentOfficeSeatingFrame => _seatingFrame;
        public bool SupportsOfficeWorkAnimationHook => true;
        public bool HasOfficeWorkFallback =>
            HasCompleteFrames(seatedWorkFrames, OfficeSeatingAnimationFrames.WorkSpriteCount);
        public bool IsOfficeWorkHookActive => _officeWorkSession != null;
        public bool IsOfficeWorkSafeToStand =>
            _officeWorkSession == null || _officeWorkSession.IsSafeToStand;
        public bool IsNavigationAnimationSuppressed => _navigationAnimationSuppressed;

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float secondsPerFrame = 0.11f)
        {
            targetRenderer = renderer;
            walkFrames = frames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(0.05f, secondsPerFrame);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            ApplyFrame();
        }

        public void ConfigureOfficeSeating(
            Sprite[] newSitDownFrames,
            Sprite[] newSeatedWorkFrames,
            Sprite[] newStandUpFrames,
            float transitionSecondsPerFrame = 0.11f,
            float workSecondsPerFrame = 0.14f)
        {
            RequireCompleteFrames(
                newSitDownFrames,
                OfficeSeatingAnimationFrames.SitDownSpriteCount,
                nameof(newSitDownFrames));
            RequireCompleteFrames(
                newSeatedWorkFrames,
                OfficeSeatingAnimationFrames.WorkSpriteCount,
                nameof(newSeatedWorkFrames));
            RequireCompleteFrames(
                newStandUpFrames,
                OfficeSeatingAnimationFrames.StandUpSpriteCount,
                nameof(newStandUpFrames));
            sitDownFrames = (Sprite[])newSitDownFrames.Clone();
            seatedWorkFrames = (Sprite[])newSeatedWorkFrames.Clone();
            standUpFrames = (Sprite[])newStandUpFrames.Clone();
            seatingTransitionFrameSeconds = Mathf.Max(0.05f, transitionSecondsPerFrame);
            seatedWorkFrameSeconds = Mathf.Max(0.05f, workSecondsPerFrame);
        }

        public void SetWorldVelocity(Vector3 velocity)
        {
            _tileDisplacementDirection = false;
            _worldVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        public void SetTileDisplacement(Vector2 actualDisplacement)
        {
            _tileDisplacementDirection = true;
            _worldVelocity = new Vector3(actualDisplacement.x, 0f, actualDisplacement.y);
        }

        public void StopTileMovementButKeepFacing()
        {
            _tileDisplacementDirection = true;
            _worldVelocity = Vector3.zero;
        }

        public void ConfigureOfficeWorkAnimationHook(IOfficeSeatedWorkAnimationHook hook)
        {
            EndOfficeWorkSession();
            _officeWorkHook = hook;
        }

        public bool PrepareOfficeSeatingFacing(
            int direction,
            OfficeSeatForegroundOcclusionMode occlusionMode)
        {
            if (!HasOfficeSeatingFrames || direction < 0 || direction >= DirectionCount) return false;
            EndOfficeWorkSession();
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            _seatingClip = null;
            _seatingFrameClock = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            ApplyOfficeSeatOcclusion(occlusionMode);
            ApplyFrame();
            return true;
        }

        public void SetNavigationAnimationSuppressed(bool suppressed)
        {
            if (_navigationAnimationSuppressed == suppressed) return;
            _navigationAnimationSuppressed = suppressed;
            _worldVelocity = Vector3.zero;
            _frameClock = 0f;
            if (!suppressed) ApplyFrame();
        }

        public Sprite GetFrame(int direction, int walkFrame)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (walkFrame < 0 || walkFrame >= WalkFrameCount)
                throw new ArgumentOutOfRangeException(nameof(walkFrame));
            if (walkFrames == null || walkFrames.Length < RequiredFrameCount) return null;
            return walkFrames[walkFrame * DirectionCount + direction];
        }

        public Sprite GetOfficeSeatingFrame(
            OfficeSeatingAnimationClip clip,
            int direction,
            int frame)
        {
            if (!HasOfficeSeatingFrames) return null;
            var frames = FramesFor(clip);
            return frames[OfficeSeatingAnimationFrames.FlattenedIndex(clip, direction, frame)];
        }

        public bool BeginSitDown(int direction)
        {
            if (!HasOfficeSeatingFrames) return false;
            BeginOfficeSeatingClip(OfficeSeatingAnimationClip.SitDown, direction);
            return true;
        }

        public bool BeginSeatedWork()
        {
            if (!HasOfficeSeatingFrames || !_seatingClip.HasValue) return false;
            EndOfficeWorkSession();
            if (_officeWorkHook != null &&
                _officeWorkHook.TryBegin(_lastDirection, out var session) &&
                session != null)
            {
                _officeWorkSession = session;
            }
            BeginOfficeSeatingClip(OfficeSeatingAnimationClip.Work, _lastDirection);
            return true;
        }

        public void RequestOfficeWorkSafeStop()
        {
            _officeWorkSession?.RequestSafeStop();
        }

        public bool BeginStandUp()
        {
            if (!HasOfficeSeatingFrames || !_seatingClip.HasValue || !IsOfficeWorkSafeToStand)
                return false;
            EndOfficeWorkSession();
            BeginOfficeSeatingClip(OfficeSeatingAnimationClip.StandUp, _lastDirection);
            return true;
        }

        public void ResumeWalkingAfterSeating()
        {
            EndOfficeWorkSession();
            _seatingClip = null;
            _seatingFrameClock = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            RestorePreSeatingSortingOrder();
            ApplyFrame();
        }

        public void Tick(float deltaTime)
        {
            if (targetRenderer == null) return;
            if (_seatingClip.HasValue)
            {
                if (!HasOfficeSeatingFrames) return;
                TickOfficeSeating(Mathf.Max(0f, deltaTime));
                ApplyFrame();
                return;
            }
            if (_navigationAnimationSuppressed) return;
            if (walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            if (IsMoving)
            {
                _lastDirection = _tileDisplacementDirection
                    ? ResolveTileDirection(
                        new Vector2(_worldVelocity.x, _worldVelocity.z),
                        _lastDirection,
                        facingHysteresisDegrees)
                    : ResolveDirection(_worldVelocity, _lastDirection, facingHysteresisDegrees);
                _frameClock += Mathf.Max(0f, deltaTime);
                var effectiveFrameSeconds = ResolveEffectiveFrameSeconds();
                while (_frameClock >= effectiveFrameSeconds)
                {
                    _frameClock -= effectiveFrameSeconds;
                    _walkFrame = (_walkFrame + 1) % WalkFrameCount;
                }
            }
            else
            {
                _frameClock = 0f;
                _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            }

            ApplyFrame();
        }

        public static int ResolveDirectionFromAxes(float horizontal, float vertical)
        {
            var angleFromSouth = Mathf.Atan2(-horizontal, -vertical) * Mathf.Rad2Deg;
            var octant = Mathf.RoundToInt(angleFromSouth / 45f);
            return (octant % DirectionCount + DirectionCount) % DirectionCount;
        }

        public static int ResolveTileDirection(Vector2 actualDisplacement)
        {
            if (float.IsNaN(actualDisplacement.x) || float.IsInfinity(actualDisplacement.x) ||
                float.IsNaN(actualDisplacement.y) || float.IsInfinity(actualDisplacement.y))
                throw new ArgumentOutOfRangeException(nameof(actualDisplacement));
            if (actualDisplacement.sqrMagnitude <= 0.000001f)
                throw new ArgumentException("Tile displacement must be non-zero.", nameof(actualDisplacement));
            return ResolveDirectionFromAxes(actualDisplacement.x, actualDisplacement.y);
        }

        public static int ResolveTileDirection(Vector2 actualDisplacement, int currentDirection)
        {
            if (currentDirection < 0 || currentDirection >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(currentDirection));
            if (actualDisplacement.sqrMagnitude <= 0.000001f) return currentDirection;
            return ResolveTileDirection(actualDisplacement, currentDirection, 7.5f);
        }

        public static int ResolveTileDirection(
            Vector2 actualDisplacement,
            int currentDirection,
            float hysteresisDegrees)
        {
            return OfficeFacingHysteresisRules.ResolveDirection(
                actualDisplacement.x,
                actualDisplacement.y,
                currentDirection,
                hysteresisDegrees);
        }

        public static int ResolveDirectionWithHysteresisFromAxes(
            float horizontal,
            float vertical,
            int currentDirection,
            float hysteresisDegrees = 7.5f)
        {
            return OfficeFacingHysteresisRules.ResolveDirection(
                horizontal,
                vertical,
                currentDirection,
                hysteresisDegrees);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            AbortOfficeSeatingPresentation();
        }

        private void OnDestroy()
        {
            AbortOfficeSeatingPresentation();
        }

        private void ApplyFrame()
        {
            if (targetRenderer == null) return;
            if (_seatingClip.HasValue && HasOfficeSeatingFrames)
            {
                var hookSprite = _seatingClip.Value == OfficeSeatingAnimationClip.Work
                    ? _officeWorkSession?.CurrentSprite
                    : null;
                targetRenderer.sprite = hookSprite != null
                    ? hookSprite
                    : GetOfficeSeatingFrame(_seatingClip.Value, _lastDirection, _seatingFrame);
                return;
            }
            if (walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            targetRenderer.sprite = walkFrames[_walkFrame * DirectionCount + _lastDirection];
        }

        private void BeginOfficeSeatingClip(OfficeSeatingAnimationClip clip, int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            _seatingClip = clip;
            _seatingFrameClock = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            ApplyFrame();
        }

        private void TickOfficeSeating(float deltaTime)
        {
            if (!_seatingClip.HasValue || _seatingTransitionComplete) return;
            var clip = _seatingClip.Value;
            if (clip == OfficeSeatingAnimationClip.Work && _officeWorkSession != null)
            {
                _officeWorkSession.Tick(deltaTime);
                return;
            }
            var secondsPerFrame = clip == OfficeSeatingAnimationClip.Work
                ? Mathf.Max(0.05f, seatedWorkFrameSeconds)
                : Mathf.Max(0.05f, seatingTransitionFrameSeconds);
            _seatingFrameClock += deltaTime;
            while (_seatingFrameClock >= secondsPerFrame)
            {
                _seatingFrameClock -= secondsPerFrame;
                var nextFrame = _seatingFrame + 1;
                if (nextFrame < OfficeSeatingAnimationFrames.FrameCount(clip))
                {
                    _seatingFrame = nextFrame;
                    continue;
                }

                if (clip == OfficeSeatingAnimationClip.Work)
                {
                    _seatingFrame = 0;
                    continue;
                }

                _seatingTransitionComplete = true;
                break;
            }
        }

        private Sprite[] FramesFor(OfficeSeatingAnimationClip clip)
        {
            return clip switch
            {
                OfficeSeatingAnimationClip.SitDown => sitDownFrames,
                OfficeSeatingAnimationClip.Work => seatedWorkFrames,
                OfficeSeatingAnimationClip.StandUp => standUpFrames,
                _ => throw new ArgumentOutOfRangeException(nameof(clip))
            };
        }

        private void ApplyOfficeSeatOcclusion(OfficeSeatForegroundOcclusionMode mode)
        {
            if (targetRenderer == null) return;
            if (!_seatingSortingOrderActive)
            {
                _preSeatingSortingOrder = targetRenderer.sortingOrder;
                _seatingSortingOrderActive = true;
            }
            const int officeForegroundSortingOrder = 100;
            targetRenderer.sortingOrder = mode == OfficeSeatForegroundOcclusionMode.InFrontOfForeground
                ? Mathf.Max(_preSeatingSortingOrder, officeForegroundSortingOrder + 1)
                : Mathf.Min(_preSeatingSortingOrder, officeForegroundSortingOrder - 1);
        }

        private void RestorePreSeatingSortingOrder()
        {
            if (!_seatingSortingOrderActive) return;
            if (targetRenderer != null) targetRenderer.sortingOrder = _preSeatingSortingOrder;
            _seatingSortingOrderActive = false;
        }

        private void EndOfficeWorkSession()
        {
            _officeWorkSession?.Dispose();
            _officeWorkSession = null;
        }

        private void AbortOfficeSeatingPresentation()
        {
            EndOfficeWorkSession();
            _seatingClip = null;
            _seatingFrameClock = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            RestorePreSeatingSortingOrder();
            ApplyFrame();
        }

        private static bool HasCompleteFrames(Sprite[] frames, int expectedCount)
        {
            if (frames == null || frames.Length != expectedCount) return false;
            for (var index = 0; index < frames.Length; index++)
            {
                if (frames[index] == null) return false;
            }
            return true;
        }

        private static void RequireCompleteFrames(Sprite[] frames, int expectedCount, string parameterName)
        {
            if (!HasCompleteFrames(frames, expectedCount))
            {
                throw new ArgumentException(
                    $"Office seating frames require exactly {expectedCount} non-null sprites.",
                    parameterName);
            }
        }

        private static int ResolveDirection(
            Vector3 velocity,
            int currentDirection,
            float hysteresisDegrees)
        {
            var targetCamera = Camera.main;
            var cameraRight = targetCamera == null ? Vector3.right : targetCamera.transform.right;
            var cameraForward = targetCamera == null ? Vector3.forward : targetCamera.transform.forward;
            cameraRight.y = 0f;
            cameraForward.y = 0f;
            cameraRight.Normalize();
            cameraForward.Normalize();
            var horizontal = Vector3.Dot(velocity, cameraRight);
            var vertical = Vector3.Dot(velocity, cameraForward);
            return ResolveDirectionWithHysteresisFromAxes(
                horizontal,
                vertical,
                currentDirection,
                hysteresisDegrees);
        }

        private float ResolveEffectiveFrameSeconds()
        {
            if (!IsMoving) return frameSeconds;
            const float referenceWalkSpeed = 1.8f;
            var speedScale = Mathf.Clamp(_worldVelocity.magnitude / referenceWalkSpeed, 0.65f, 1.75f);
            return frameSeconds / speedScale;
        }
    }
}

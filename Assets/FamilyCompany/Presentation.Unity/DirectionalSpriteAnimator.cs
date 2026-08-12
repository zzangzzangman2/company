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
        [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
        [SerializeField] private float frameSeconds = 0.11f;
        [SerializeField, Range(0, WalkFrameCount - 1)] private int idleWalkFrame = 2;
        [SerializeField] private Sprite[] sitDownFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] seatedWorkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] standUpFrames = Array.Empty<Sprite>();
        [SerializeField] private float seatingTransitionFrameSeconds = 0.11f;
        [SerializeField] private float seatedWorkFrameSeconds = 0.14f;
        [SerializeField] private OfficeSeatingPresentationMode seatingPresentationMode =
            OfficeSeatingPresentationMode.Animated;
        [SerializeField, Range(0f, 20f)] private float facingHysteresisDegrees = 7.5f;
        [SerializeField, Min(0.1f)] private float strideLength =
            OfficeLocomotionGaitRules.DefaultStrideLength;
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
        private bool _navigationAnimationSuppressed;
        private bool _tileDisplacementDirection;
        private bool _externallyTicked;
        private Vector2 _tileFrameDisplacement;
        private Vector2 _tileSemanticDisplacement;
        private float _tileFrameDeltaTime;
        private float _tileActualSpeed;
        private bool _tileFrameCollisionProjected;
        private bool _tilePresentationFrameOpen;
        private OfficeLocomotionFacingState _tileFacingState;
        private bool _tileFacingStateInitialized;
        private int _lastSemanticDirection;
        private int _lastMotionDirection;
        private bool _usedSemanticHeading;
        private OfficeLocomotionGaitState _tileGaitState;
        private bool _tileGaitStateInitialized;

        public event Action<OfficeSeatingAnimationClip, int, Sprite> OfficeFrameApplied;

        public int CurrentDirection => _lastDirection;
        public int CurrentWalkFrame => _walkFrame;
        public bool IsMoving => _tileDisplacementDirection
            ? _tileFrameDisplacement.sqrMagnitude > 0.0000001f
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
        public OfficeSeatingPresentationMode SeatingPresentationMode => seatingPresentationMode;
        public Vector2 AccumulatedTileDisplacement => _tileFrameDisplacement;
        public Vector2 SemanticTileDisplacement => _tileSemanticDisplacement;
        public float ActualTileSpeed => _tileActualSpeed;
        public bool WasCollisionProjected => _tileFrameCollisionProjected;
        public int SemanticDirection => _lastSemanticDirection;
        public int MotionDirection => _lastMotionDirection;
        public bool UsedSemanticHeading => _usedSemanticHeading;
        public OfficeLocomotionPhase LocomotionPhase => _tileGaitStateInitialized
            ? _tileGaitState.Phase
            : OfficeLocomotionPhase.Idle;
        public float GaitDistance => _tileGaitStateInitialized ? _tileGaitState.AccumulatedDistance : 0f;
        public float GaitPhase01 => OfficeLocomotionGaitRules.Phase01(
            GaitDistance,
            Mathf.Max(0.1f, strideLength));
        public float StrideLength => Mathf.Max(0.1f, strideLength);

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float secondsPerFrame = 0.11f)
        {
            targetRenderer = renderer;
            walkFrames = frames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(0.05f, secondsPerFrame);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            ResetTileFacingState(_lastDirection);
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
        }

        public void ConfigureLocomotion(Sprite[] newIdleFrames, float newStrideLength)
        {
            if (newIdleFrames != null && newIdleFrames.Length != 0 &&
                !HasCompleteFrames(newIdleFrames, DirectionCount))
                throw new ArgumentException(
                    $"Idle frames require exactly {DirectionCount} non-null sprites.",
                    nameof(newIdleFrames));
            if (newStrideLength <= 0f || float.IsNaN(newStrideLength) || float.IsInfinity(newStrideLength))
                throw new ArgumentOutOfRangeException(nameof(newStrideLength));
            idleFrames = newIdleFrames == null ? Array.Empty<Sprite>() : (Sprite[])newIdleFrames.Clone();
            strideLength = newStrideLength;
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
        }

        public void ConfigureOfficeSeating(
            Sprite[] newSitDownFrames,
            Sprite[] newSeatedWorkFrames,
            Sprite[] newStandUpFrames,
            float transitionSecondsPerFrame = 0.11f,
            float workSecondsPerFrame = 0.14f,
            OfficeSeatingPresentationMode presentationMode = OfficeSeatingPresentationMode.Animated)
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
            seatingPresentationMode = presentationMode;
        }

        public void SetExternallyTicked(bool externallyTicked)
        {
            _externallyTicked = externallyTicked;
        }

        public void SetWorldVelocity(Vector3 velocity)
        {
            _tileDisplacementDirection = false;
            _tilePresentationFrameOpen = false;
            _worldVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        public void SetTileDisplacement(Vector2 actualDisplacement)
        {
            _tileDisplacementDirection = true;
            _tilePresentationFrameOpen = false;
            _tileFrameDisplacement = actualDisplacement;
            _tileSemanticDisplacement = actualDisplacement;
            _tileFrameDeltaTime = 1f;
            _tileActualSpeed = actualDisplacement.magnitude;
            _tileFrameCollisionProjected = false;
            _worldVelocity = new Vector3(actualDisplacement.x, 0f, actualDisplacement.y);
        }

        public void BeginTilePresentationFrame()
        {
            _tileDisplacementDirection = true;
            _tilePresentationFrameOpen = true;
            _tileFrameDisplacement = Vector2.zero;
            _tileSemanticDisplacement = Vector2.zero;
            _tileFrameDeltaTime = 0f;
            _tileActualSpeed = 0f;
            _tileFrameCollisionProjected = false;
            _usedSemanticHeading = false;
        }

        public void AccumulateTileMotion(
            Vector2 semanticVelocity,
            Vector2 actualDisplacement,
            float deltaTime,
            bool collisionProjected)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!_tilePresentationFrameOpen) BeginTilePresentationFrame();
            _tileSemanticDisplacement += semanticVelocity * deltaTime;
            _tileFrameDisplacement += actualDisplacement;
            _tileFrameDeltaTime += deltaTime;
            _tileFrameCollisionProjected |= collisionProjected;
            _tileActualSpeed = _tileFrameDeltaTime > 0.000001f
                ? _tileFrameDisplacement.magnitude / _tileFrameDeltaTime
                : 0f;
            _worldVelocity = new Vector3(
                _tileFrameDisplacement.x,
                0f,
                _tileFrameDisplacement.y);
        }

        public void EndTilePresentationFrame()
        {
            _tilePresentationFrameOpen = false;
        }

        public void StopTileMovementButKeepFacing()
        {
            _tileDisplacementDirection = true;
            if (_tilePresentationFrameOpen) return;
            _tileFrameDisplacement = Vector2.zero;
            _tileSemanticDisplacement = Vector2.zero;
            _tileFrameDeltaTime = 0f;
            _tileActualSpeed = 0f;
            _tileFrameCollisionProjected = false;
            _worldVelocity = Vector3.zero;
        }

        public void ConfigureOfficeWorkAnimationHook(IOfficeSeatedWorkAnimationHook hook)
        {
            EndOfficeWorkSession();
            _officeWorkHook = hook;
        }

        public bool PrepareOfficeSeatingFacing(int direction)
        {
            if (!HasOfficeSeatingFrames || direction < 0 || direction >= DirectionCount) return false;
            if (seatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork && direction != 3)
                return false;
            EndOfficeWorkSession();
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            ResetTileFacingState(direction);
            ResetTileGaitState(direction);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            _seatingClip = null;
            _seatingFrameClock = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            ApplyFrame();
            return true;
        }

        public bool PrepareOfficeSeatingFacing(
            int direction,
            OfficeSeatForegroundOcclusionMode ignoredOcclusionMode)
        {
            return PrepareOfficeSeatingFacing(direction);
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
            ResetTileGaitState(_lastDirection);
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
            if (_tileDisplacementDirection)
            {
                EnsureTileFacingState();
                int resolvedVisualDirection = _tileFacingState.VisualDirection;
                if (IsMoving)
                {
                    OfficeLocomotionFacingResult facing =
                        OfficeLocomotionPresentationRules.ResolveFacing(
                            _tileFacingState,
                            new OfficeNavPoint(
                                _tileSemanticDisplacement.x,
                                _tileSemanticDisplacement.y),
                            new OfficeNavPoint(
                                _tileFrameDisplacement.x,
                                _tileFrameDisplacement.y),
                            _tileFrameDeltaTime > 0.000001f ? _tileFrameDeltaTime : Mathf.Max(0f, deltaTime),
                            _tileFrameCollisionProjected,
                            Mathf.Min(
                                facingHysteresisDegrees,
                                OfficeLocomotionPresentationRules.DefaultHysteresisDegrees));
                    _tileFacingState = facing.State;
                    resolvedVisualDirection = facing.State.VisualDirection;
                    _lastSemanticDirection = facing.SemanticDirection;
                    _lastMotionDirection = facing.MotionDirection;
                    _usedSemanticHeading = facing.UsedSemanticHeading;
                }
                EnsureTileGaitState();
                _tileGaitState = OfficeLocomotionGaitRules.Resolve(
                    _tileGaitState,
                    _tileFrameDisplacement.magnitude,
                    _tileFrameDeltaTime > 0.000001f ? _tileFrameDeltaTime : Mathf.Max(0f, deltaTime),
                    _tileSemanticDisplacement.sqrMagnitude > 0.0000001f,
                    resolvedVisualDirection,
                    Mathf.Max(0.1f, strideLength),
                    WalkFrameCount);
                _lastDirection = _tileGaitState.DisplayDirection;
                _walkFrame = _tileGaitState.Frame;
                _frameClock = 0f;
            }
            else
            {
                if (IsMoving)
                {
                    _lastDirection = ResolveDirection(
                        _worldVelocity,
                        _lastDirection,
                        facingHysteresisDegrees);
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
            if (_externallyTicked) return;
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
                bool safeStatic = seatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork;
                OfficeSeatingAnimationClip appliedClip = safeStatic
                    ? OfficeSeatingAnimationClip.Work
                    : _seatingClip.Value;
                int appliedFrame = safeStatic ? 0 : _seatingFrame;
                var hookSprite = !safeStatic && _seatingClip.Value == OfficeSeatingAnimationClip.Work
                    ? _officeWorkSession?.CurrentSprite
                    : null;
                targetRenderer.sprite = hookSprite != null
                    ? hookSprite
                    : GetOfficeSeatingFrame(appliedClip, _lastDirection, appliedFrame);
                OfficeFrameApplied?.Invoke(appliedClip, appliedFrame, targetRenderer.sprite);
                return;
            }
            if (walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            if (_tileDisplacementDirection &&
                _tileGaitStateInitialized &&
                _tileGaitState.Phase == OfficeLocomotionPhase.Idle &&
                HasCompleteFrames(idleFrames, DirectionCount))
            {
                targetRenderer.sprite = idleFrames[_lastDirection];
                return;
            }
            targetRenderer.sprite = walkFrames[_walkFrame * DirectionCount + _lastDirection];
        }

        private void BeginOfficeSeatingClip(OfficeSeatingAnimationClip clip, int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            ResetTileFacingState(direction);
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
            if (_seatingFrameClock >= secondsPerFrame)
            {
                _seatingFrameClock -= secondsPerFrame;
                // A Sprite can only be presented once per rendered tick. Advancing through several
                // indices here silently skipped SitDown/StandUp art under time scale or a long frame.
                // Keep the accumulated remainder, but expose exactly one authored frame per Tick.
                var nextFrame = _seatingFrame + 1;
                if (nextFrame < OfficeSeatingAnimationFrames.FrameCount(clip))
                {
                    _seatingFrame = nextFrame;
                    return;
                }

                if (clip == OfficeSeatingAnimationClip.Work)
                {
                    _seatingFrame = 0;
                    return;
                }

                _seatingTransitionComplete = true;
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
            float speed = _tileDisplacementDirection ? _tileActualSpeed : _worldVelocity.magnitude;
            var speedScale = Mathf.Clamp(speed / referenceWalkSpeed, 0.65f, 1.75f);
            return frameSeconds / speedScale;
        }

        private void EnsureTileFacingState()
        {
            if (!_tileFacingStateInitialized) ResetTileFacingState(_lastDirection);
        }

        private void ResetTileFacingState(int direction)
        {
            _tileFacingState = OfficeLocomotionFacingState.Initial(
                Mathf.Clamp(direction, 0, DirectionCount - 1));
            _tileFacingStateInitialized = true;
            _lastSemanticDirection = _tileFacingState.VisualDirection;
            _lastMotionDirection = _tileFacingState.VisualDirection;
            _usedSemanticHeading = false;
        }

        private void EnsureTileGaitState()
        {
            if (!_tileGaitStateInitialized) ResetTileGaitState(_lastDirection);
        }

        private void ResetTileGaitState(int direction)
        {
            _tileGaitState = OfficeLocomotionGaitState.Initial(
                Mathf.Clamp(direction, 0, DirectionCount - 1));
            _tileGaitStateInitialized = true;
        }
    }
}

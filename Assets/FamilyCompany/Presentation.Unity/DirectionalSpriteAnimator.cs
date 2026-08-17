using System;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public readonly struct DirectionalLocomotionFrameTrace
    {
        public DirectionalLocomotionFrameTrace(
            Vector2 actualDisplacement,
            float actualSpeed,
            int motionDirection,
            int displayDirection,
            OfficeLocomotionPhase phase,
            string clip,
            string spriteName,
            bool flipX,
            bool isMoving)
        {
            ActualDisplacement = actualDisplacement;
            ActualSpeed = actualSpeed;
            MotionDirection = motionDirection;
            DisplayDirection = displayDirection;
            Phase = phase;
            Clip = clip ?? string.Empty;
            SpriteName = spriteName ?? string.Empty;
            FlipX = flipX;
            IsMoving = isMoving;
        }

        public Vector2 ActualDisplacement { get; }
        public float ActualSpeed { get; }
        public int MotionDirection { get; }
        public int DisplayDirection { get; }
        public OfficeLocomotionPhase Phase { get; }
        public string Clip { get; }
        public string SpriteName { get; }
        public bool FlipX { get; }
        public bool IsMoving { get; }

        public override string ToString() =>
            $"actual=({ActualDisplacement.x:F5},{ActualDisplacement.y:F5}) " +
            $"speed={ActualSpeed:F4} motionDir={MotionDirection} displayDir={DisplayDirection} " +
            $"phase={Phase} clip={Clip} sprite={SpriteName} flipX={FlipX} moving={IsMoving}";
    }

    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        public const int DirectionCount = 8;
        public const int WalkFrameCount = 6;
        public const int RequiredFrameCount = DirectionCount * WalkFrameCount;
        public const int LocomotionTransitionPoseCount = 2;
        public const int LocomotionTransitionClipCount = 4;
        public const int LocomotionTransitionFramesPerClip =
            DirectionCount * LocomotionTransitionPoseCount;
        public const int RequiredLocomotionTransitionFrameCount =
            LocomotionTransitionClipCount * LocomotionTransitionFramesPerClip;

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] locomotionTransitionFrames = Array.Empty<Sprite>();
        [SerializeField] private float frameSeconds = 0.11f;
        [SerializeField, Range(0, WalkFrameCount - 1)] private int idleWalkFrame = 2;
        [SerializeField] private Sprite[] sitDownFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] seatedWorkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] standUpFrames = Array.Empty<Sprite>();
        [SerializeField] private float sitDownDurationSeconds = 0.62f;
        [SerializeField] private float standUpDurationSeconds = 0.56f;
        [SerializeField] private float seatedWorkFrameSeconds = 0.14f;
        [SerializeField] private OfficeSeatingPresentationMode seatingPresentationMode =
            OfficeSeatingPresentationMode.Animated;
        [SerializeField, Range(0f, 20f)] private float facingHysteresisDegrees = 7.5f;
        private Vector3 _worldVelocity;
        private float _frameClock;
        private int _walkFrame;
        private int _lastDirection;
        private OfficeSeatingAnimationClip? _seatingClip;
        private float _seatingElapsedSeconds;
        private float _seatingProgress01;
        private int _seatingFrame;
        private bool _seatingTransitionComplete;
        private IOfficeSeatedWorkAnimationHook _officeWorkHook;
        private IOfficeSeatedWorkAnimationSession _officeWorkSession;
        private bool _navigationAnimationSuppressed;
        private bool _continuousRouteLocomotionPresentation;
        private bool _tileDisplacementDirection;
        private bool _externallyTicked;
        private Vector2 _tileFrameDisplacement;
        private float _tileFrameTravelDistance;
        private Vector2 _tileSemanticDisplacement;
        private float _tileFrameDeltaTime;
        private float _tileActualSpeed;
        private bool _tileIsMoving;
        private bool _tileFrameCollisionProjected;
        private bool _tilePresentationFrameOpen;
        private OfficeLocomotionFacingState _tileFacingState;
        private bool _tileFacingStateInitialized;
        private int _lastSemanticDirection;
        private int _lastMotionDirection;
        private bool _usedSemanticHeading;
        private float _lastFacingAlignmentDot = 1f;
        private float _lastFacingAngularErrorDegrees;
        private OfficeLocomotionGaitState _tileGaitState;
        private bool _tileGaitStateInitialized;
        private bool _officeSeatingFacingLocked;
        private int _lockedOfficeSeatingDirection = -1;
        private int _officeSeatingFacingViolationCount;
        private int _maximumOfficeSeatingFacingDelta;
        private int _officeWorkSpriteDirectionViolationCount;
        private int _maximumOfficeWorkSpriteDirectionDelta;
        private int _officeAppliedSpriteDirectionViolationCount;
        private int _maximumOfficeAppliedSpriteDirectionDelta;
        private int _currentAppliedSpriteDirection = -1;
        private bool _currentAppliedSpriteDirectionMatchesLock = true;

        public event Action<OfficeSeatingAnimationClip, int, Sprite> OfficeFrameApplied;

        public int CurrentDirection => _lastDirection;
        public bool IsOfficeSeatingFacingLocked => _officeSeatingFacingLocked;
        public int LockedOfficeSeatingDirection =>
            _officeSeatingFacingLocked ? _lockedOfficeSeatingDirection : -1;
        public bool IsOfficeSeatingFacingConsistent =>
            !_officeSeatingFacingLocked || _lastDirection == _lockedOfficeSeatingDirection;
        public int OfficeSeatingFacingViolationCount => _officeSeatingFacingViolationCount;
        public int MaximumOfficeSeatingFacingDelta => _maximumOfficeSeatingFacingDelta;
        public int OfficeWorkSpriteDirectionViolationCount =>
            _officeWorkSpriteDirectionViolationCount;
        public int MaximumOfficeWorkSpriteDirectionDelta =>
            _maximumOfficeWorkSpriteDirectionDelta;
        public int OfficeAppliedSpriteDirectionViolationCount =>
            _officeAppliedSpriteDirectionViolationCount;
        public int MaximumOfficeAppliedSpriteDirectionDelta =>
            _maximumOfficeAppliedSpriteDirectionDelta;
        public int OfficeSeatingDirectionMismatchCount =>
            _officeSeatingFacingViolationCount +
            _officeWorkSpriteDirectionViolationCount +
            _officeAppliedSpriteDirectionViolationCount;
        public int MaximumOfficeSeatingDirectionOctantDelta => Mathf.Max(
            _maximumOfficeSeatingFacingDelta,
            Mathf.Max(
                _maximumOfficeWorkSpriteDirectionDelta,
                _maximumOfficeAppliedSpriteDirectionDelta));
        public int CurrentAppliedSpriteDirection => _currentAppliedSpriteDirection;
        public bool HasCurrentAppliedSpriteDirectionMetadata =>
            _currentAppliedSpriteDirection >= 0;
        public bool IsCurrentAppliedSpriteDirectionLocked =>
            _currentAppliedSpriteDirectionMatchesLock;
        public int CurrentWalkFrame => _walkFrame;
        public bool IsOfficeWorkAnimationHookActive => _officeWorkSession != null;
        public OfficeWorkMicroAction CurrentOfficeWorkMicroAction =>
            _officeWorkSession?.CurrentAction ?? OfficeWorkMicroAction.None;
        public bool IsMoving => _tileDisplacementDirection
            ? _tileIsMoving
            : _worldVelocity.sqrMagnitude > 0.0025f;
        public bool IsOfficeSeatingEntryPlanted =>
            !IsMoving &&
            (!_tileGaitStateInitialized || _tileGaitState.Phase == OfficeLocomotionPhase.Idle);
        public int ConfiguredFrameCount => walkFrames?.Length ?? 0;
        public int ConfiguredLocomotionTransitionFrameCount =>
            locomotionTransitionFrames?.Length ?? 0;
        public bool IsLocomotionTransitionSpriteActive =>
            !_seatingClip.HasValue &&
            !_continuousRouteLocomotionPresentation &&
            _tileDisplacementDirection &&
            _tileGaitStateInitialized &&
            _tileGaitState.Phase != OfficeLocomotionPhase.Walk &&
            HasCompleteFrames(
                locomotionTransitionFrames,
                RequiredLocomotionTransitionFrameCount);
        public int CurrentLocomotionTransitionPose =>
            IsLocomotionTransitionSpriteActive ? ResolveLocomotionTransitionPose() : -1;
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
        public float CurrentOfficeSeatingProgress01 => _seatingProgress01;
        public float SitDownDurationSeconds => Mathf.Max(0.05f, sitDownDurationSeconds);
        public float StandUpDurationSeconds => Mathf.Max(0.05f, standUpDurationSeconds);
        public bool SupportsOfficeWorkAnimationHook => true;
        public bool HasOfficeWorkFallback =>
            HasCompleteFrames(seatedWorkFrames, OfficeSeatingAnimationFrames.WorkSpriteCount);
        public bool IsOfficeWorkHookActive => _officeWorkSession != null;
        public bool IsOfficeWorkSafeToStand =>
            _officeWorkSession == null || _officeWorkSession.IsSafeToStand;
        public bool IsNavigationAnimationSuppressed => _navigationAnimationSuppressed;
        public bool UsesContinuousRouteLocomotionPresentation =>
            _continuousRouteLocomotionPresentation;
        public OfficeSeatingPresentationMode SeatingPresentationMode => seatingPresentationMode;
        public Vector2 AccumulatedTileDisplacement => _tileFrameDisplacement;
        public Vector2 ActualTileDisplacement => _tileFrameDisplacement;
        public Vector2 SemanticTileDisplacement => _tileSemanticDisplacement;
        public Vector2 RequestedTileDisplacement => _tileSemanticDisplacement;
        public float ActualTileSpeed => _tileActualSpeed;
        public bool WasCollisionProjected => _tileFrameCollisionProjected;
        public int SemanticDirection => _lastSemanticDirection;
        public int RequestedDirection => _lastSemanticDirection;
        public int MotionDirection => _lastMotionDirection;
        public bool UsedSemanticHeading => _usedSemanticHeading;
        public float FacingAlignmentDot => _lastFacingAlignmentDot;
        public float FacingAngularErrorDegrees => _lastFacingAngularErrorDegrees;
        public OfficeLocomotionPhase LocomotionPhase => _tileGaitStateInitialized
            ? _tileGaitState.Phase
            : OfficeLocomotionPhase.Idle;
        public float GaitDistance => _tileGaitStateInitialized ? _tileGaitState.AccumulatedDistance : 0f;
        public float GaitPhase01 => OfficeLocomotionGaitRules.Phase01(
            GaitDistance,
            OfficeLocomotionGaitRules.DefaultStrideLength);
        public float StrideLength => OfficeLocomotionGaitRules.DefaultStrideLength;

        public DirectionalLocomotionFrameTrace CaptureLocomotionFrameTrace()
        {
            string clip = _seatingClip.HasValue
                ? "Seating/" + _seatingClip.Value
                : IsLocomotionTransitionSpriteActive
                    ? "Transition/" + LocomotionPhase
                    : _tileIsMoving
                        ? "Walk"
                        : "Idle";
            return new DirectionalLocomotionFrameTrace(
                _tileFrameDisplacement,
                _tileActualSpeed,
                _lastMotionDirection,
                _lastDirection,
                LocomotionPhase,
                clip,
                targetRenderer == null || targetRenderer.sprite == null
                    ? string.Empty
                    : targetRenderer.sprite.name,
                targetRenderer != null && targetRenderer.flipX,
                IsMoving);
        }

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float secondsPerFrame = 0.11f)
        {
            targetRenderer = renderer;
            // Starter Office owns eight independently authored directions. Mirroring a stale
            // front/diagonal sprite would make the direction metadata and visible body disagree.
            if (targetRenderer != null) targetRenderer.flipX = false;
            walkFrames = frames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(0.05f, secondsPerFrame);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            ResetTileFacingState(_lastDirection);
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
        }

        public int ResolveConfiguredTileDirection(Vector2 worldDisplacement, int currentDirection)
        {
            if (currentDirection < 0 || currentDirection >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(currentDirection));
            Vector2 visualDisplacement = WorldVectorToFacingAxes(worldDisplacement);
            return visualDisplacement.sqrMagnitude <= 0.000001f
                ? currentDirection
                : ResolveTileDirection(visualDisplacement, currentDirection);
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
            if (Mathf.Abs(newStrideLength - OfficeLocomotionGaitRules.DefaultStrideLength) > 0.000001f)
                throw new ArgumentException(
                    "Starter Office locomotion uses the shared stride calibration for every actor.",
                    nameof(newStrideLength));
            idleFrames = newIdleFrames == null ? Array.Empty<Sprite>() : (Sprite[])newIdleFrames.Clone();
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
        }

        public void ConfigureLocomotionTransitions(Sprite[] newTransitionFrames)
        {
            if (newTransitionFrames != null && newTransitionFrames.Length != 0 &&
                !HasCompleteFrames(
                    newTransitionFrames,
                    RequiredLocomotionTransitionFrameCount))
            {
                throw new ArgumentException(
                    $"Locomotion transitions require exactly " +
                    $"{RequiredLocomotionTransitionFrameCount} non-null sprites.",
                    nameof(newTransitionFrames));
            }
            locomotionTransitionFrames = newTransitionFrames == null
                ? Array.Empty<Sprite>()
                : (Sprite[])newTransitionFrames.Clone();
            ApplyFrame();
        }

        public Sprite GetLocomotionTransitionFrame(
            OfficeLocomotionPhase phase,
            int direction,
            int pose)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (pose < 0 || pose >= LocomotionTransitionPoseCount)
                throw new ArgumentOutOfRangeException(nameof(pose));
            if (!HasCompleteFrames(
                    locomotionTransitionFrames,
                    RequiredLocomotionTransitionFrameCount)) return null;
            int clip = ResolveLocomotionTransitionClip(phase);
            return locomotionTransitionFrames[
                clip * LocomotionTransitionFramesPerClip +
                direction * LocomotionTransitionPoseCount + pose];
        }

        public void ConfigureOfficeSeating(
            Sprite[] newSitDownFrames,
            Sprite[] newSeatedWorkFrames,
            Sprite[] newStandUpFrames,
            float newSitDownDurationSeconds = 0.62f,
            float newStandUpDurationSeconds = 0.56f,
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
            sitDownDurationSeconds = Mathf.Max(0.05f, newSitDownDurationSeconds);
            standUpDurationSeconds = Mathf.Max(0.05f, newStandUpDurationSeconds);
            seatedWorkFrameSeconds = Mathf.Max(0.05f, workSecondsPerFrame);
            seatingPresentationMode = presentationMode;
        }

        public void SetExternallyTicked(bool externallyTicked)
        {
            _externallyTicked = externallyTicked;
        }

        public void SetContinuousRouteLocomotionPresentation(bool enabled)
        {
            if (_continuousRouteLocomotionPresentation == enabled) return;
            _continuousRouteLocomotionPresentation = enabled;
            if (enabled && _tileGaitStateInitialized && _tileIsMoving)
            {
                // Route corners retain the distance-owned six-frame gait phase. Replaying the
                // two-pose start clip at every cell-centre pivot made an otherwise linear walk
                // read as a hop and swapped to a differently proportioned body for a few frames.
                _walkFrame = OfficeLocomotionGaitRules.DistanceFrame(
                    _tileGaitState.AccumulatedDistance,
                    OfficeLocomotionGaitRules.DefaultStrideLength,
                    WalkFrameCount);
            }
            ApplyFrame();
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
            _tileFrameTravelDistance = actualDisplacement.magnitude;
            _tileSemanticDisplacement = actualDisplacement;
            _tileFrameDeltaTime = 1f;
            _tileActualSpeed = actualDisplacement.magnitude;
            // The legacy one-shot API represents a complete presentation sample. Its motion
            // state must therefore be authoritative immediately, before Tick resolves the next
            // shared gait frame; otherwise a translating actor can acquire the planted seating
            // lock during this one-frame window.
            _tileIsMoving = actualDisplacement.magnitude > 0.00001f;
            _tileFrameCollisionProjected = false;
            _worldVelocity = new Vector3(actualDisplacement.x, 0f, actualDisplacement.y);
        }

        public void BeginTilePresentationFrame()
        {
            _tileDisplacementDirection = true;
            _tilePresentationFrameOpen = true;
            _tileFrameDisplacement = Vector2.zero;
            _tileFrameTravelDistance = 0f;
            _tileSemanticDisplacement = Vector2.zero;
            _tileFrameDeltaTime = 0f;
            _tileActualSpeed = 0f;
            _tileIsMoving = false;
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
            _tileFrameTravelDistance += actualDisplacement.magnitude;
            _tileFrameDeltaTime += deltaTime;
            _tileFrameCollisionProjected |= collisionProjected;
            _tileActualSpeed = _tileFrameDeltaTime > 0.000001f
                ? _tileFrameTravelDistance / _tileFrameDeltaTime
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
            _tileFrameTravelDistance = 0f;
            _tileSemanticDisplacement = Vector2.zero;
            _tileFrameDeltaTime = 0f;
            _tileActualSpeed = 0f;
            _tileIsMoving = false;
            _tileFrameCollisionProjected = false;
            _worldVelocity = Vector3.zero;
        }

        public void AccumulateStandingFacingRequest(int direction, float deltaTime)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (_officeSeatingFacingLocked)
            {
                if (direction != _lockedOfficeSeatingDirection)
                    RecordOfficeSeatingFacingViolation(direction);
                return;
            }
            Vector2 heading = direction switch
            {
                0 => new Vector2(0f, -1f),
                1 => new Vector2(-1f, -1f),
                2 => new Vector2(-1f, 0f),
                3 => new Vector2(-1f, 1f),
                4 => new Vector2(0f, 1f),
                5 => new Vector2(1f, 1f),
                6 => new Vector2(1f, 0f),
                7 => new Vector2(1f, -1f),
                _ => Vector2.zero
            };
            // `heading` is a visible sprite-facing vector. Convert through the shared inverse so a
            // completed planted pivot republishes exactly that screen direction.
            AccumulateTileMotion(
                OfficeGridTilemapPresenter.VisualFacingAxesToWorldVector(heading),
                Vector2.zero,
                deltaTime,
                false);
        }

        public bool IsReadyForInteractionFacing(int desiredDirection)
        {
            if (desiredDirection < 0 || desiredDirection >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(desiredDirection));
            EnsureTileGaitState();
            return OfficeSharedLocomotionRules.IsInteractionFacingReady(
                _tileGaitState,
                _lastDirection,
                desiredDirection,
                _tileActualSpeed);
        }

        public void RestoreStandingFacing(int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (_officeSeatingFacingLocked)
            {
                if (direction != _lockedOfficeSeatingDirection)
                    RecordOfficeSeatingFacingViolation(direction);
                return;
            }
            if (_seatingClip.HasValue)
                throw new InvalidOperationException("Standing facing cannot be restored during a seating clip.");

            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            _frameClock = 0f;
            ResetTileFacingState(direction);
            ResetTileGaitState(direction);
            ApplyFrame();
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
            if (_officeSeatingFacingLocked)
            {
                if (direction != _lockedOfficeSeatingDirection)
                {
                    RecordOfficeSeatingFacingViolation(direction);
                    return false;
                }
                return !_seatingClip.HasValue;
            }
            return EstablishOfficeSeatingFacingLock(direction);
        }

        /// <summary>
        /// Strict runtime entry point. The caller must first finish the planted turn and stop
        /// locomotion; this method never rotates or snaps the character to satisfy the request.
        /// </summary>
        public bool TryLockOfficeSeatingFacingAfterPlantedRotation(int direction)
        {
            if (!HasOfficeSeatingFrames || direction < 0 || direction >= DirectionCount) return false;
            if (seatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork && direction != 3)
                return false;
            if (_officeSeatingFacingLocked)
            {
                if (direction == _lockedOfficeSeatingDirection) return !_seatingClip.HasValue;
                RecordOfficeSeatingFacingViolation(direction);
                return false;
            }
            if (_lastDirection != direction || !IsOfficeSeatingEntryPlanted) return false;
            return EstablishOfficeSeatingFacingLock(direction);
        }

        private bool EstablishOfficeSeatingFacingLock(int direction)
        {
            EndOfficeWorkSession();
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            _officeSeatingFacingLocked = true;
            _lockedOfficeSeatingDirection = direction;
            ResetOfficeSeatingDirectionMetrics();
            ResetTileFacingState(direction);
            ResetTileGaitState(direction);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            _seatingClip = null;
            _seatingElapsedSeconds = 0f;
            _seatingProgress01 = 0f;
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

        public bool CanEnterCompletedSeatedWorkAfterAtomicPlacement(int direction)
        {
            if (!HasOfficeSeatingFrames || targetRenderer == null ||
                direction < 0 || direction >= DirectionCount ||
                _seatingClip.HasValue ||
                _lastDirection != direction ||
                !IsOfficeSeatingEntryPlanted) return false;
            return true;
        }

        public void EnterCompletedSeatedWorkAfterAtomicPlacement(int direction)
        {
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            _officeSeatingFacingLocked = true;
            _lockedOfficeSeatingDirection = direction;
            ResetOfficeSeatingDirectionMetrics();
            ResetTileFacingState(direction);
            ResetTileGaitState(direction);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            _seatingClip = OfficeSeatingAnimationClip.Work;
            _seatingElapsedSeconds = 0f;
            _seatingProgress01 = 1f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            targetRenderer.flipX = false;
            targetRenderer.sprite = GetOfficeSeatingFrame(
                OfficeSeatingAnimationClip.Work,
                direction,
                0);
            CaptureAppliedSpriteDirection(targetRenderer.sprite);
        }

        public bool CanLeaveCompletedSeatedWorkAfterAtomicPlacement =>
            HasOfficeSeatingFrames &&
            targetRenderer != null &&
            _officeSeatingFacingLocked &&
            _seatingClip.HasValue &&
            _seatingClip.Value == OfficeSeatingAnimationClip.Work &&
            IsOfficeWorkSafeToStand;

        public void LeaveCompletedSeatedWorkAfterAtomicPlacement(int standingDirection)
        {
            _seatingClip = null;
            _seatingElapsedSeconds = 0f;
            _seatingProgress01 = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            _officeSeatingFacingLocked = false;
            _lockedOfficeSeatingDirection = -1;
            _worldVelocity = Vector3.zero;
            _lastDirection = Mathf.Clamp(standingDirection, 0, DirectionCount - 1);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            ResetTileFacingState(_lastDirection);
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
        }

        internal AtomicPresentationSnapshot CaptureAtomicPresentationSnapshot() =>
            new AtomicPresentationSnapshot(
                _worldVelocity,
                _frameClock,
                _walkFrame,
                _lastDirection,
                _seatingClip,
                _seatingElapsedSeconds,
                _seatingProgress01,
                _seatingFrame,
                _seatingTransitionComplete,
                _officeWorkSession,
                _tileDisplacementDirection,
                _tileFrameDisplacement,
                _tileFrameTravelDistance,
                _tileSemanticDisplacement,
                _tileFrameDeltaTime,
                _tileActualSpeed,
                _tileIsMoving,
                _tileFrameCollisionProjected,
                _tilePresentationFrameOpen,
                _tileFacingState,
                _tileFacingStateInitialized,
                _lastSemanticDirection,
                _lastMotionDirection,
                _usedSemanticHeading,
                _lastFacingAlignmentDot,
                _lastFacingAngularErrorDegrees,
                _tileGaitState,
                _tileGaitStateInitialized,
                _officeSeatingFacingLocked,
                _lockedOfficeSeatingDirection,
                _officeSeatingFacingViolationCount,
                _maximumOfficeSeatingFacingDelta,
                _officeWorkSpriteDirectionViolationCount,
                _maximumOfficeWorkSpriteDirectionDelta,
                _officeAppliedSpriteDirectionViolationCount,
                _maximumOfficeAppliedSpriteDirectionDelta,
                _currentAppliedSpriteDirection,
                _currentAppliedSpriteDirectionMatchesLock,
                targetRenderer == null ? null : targetRenderer.sprite,
                targetRenderer != null && targetRenderer.flipX);

        internal void RestoreAtomicPresentationSnapshot(in AtomicPresentationSnapshot snapshot)
        {
            _worldVelocity = snapshot.WorldVelocity;
            _frameClock = snapshot.FrameClock;
            _walkFrame = snapshot.WalkFrame;
            _lastDirection = snapshot.LastDirection;
            _seatingClip = snapshot.SeatingClip;
            _seatingElapsedSeconds = snapshot.SeatingElapsedSeconds;
            _seatingProgress01 = snapshot.SeatingProgress;
            _seatingFrame = snapshot.SeatingFrame;
            _seatingTransitionComplete = snapshot.SeatingTransitionComplete;
            _officeWorkSession = snapshot.WorkSession;
            _tileDisplacementDirection = snapshot.TileDisplacementDirection;
            _tileFrameDisplacement = snapshot.TileFrameDisplacement;
            _tileFrameTravelDistance = snapshot.TileFrameTravelDistance;
            _tileSemanticDisplacement = snapshot.TileSemanticDisplacement;
            _tileFrameDeltaTime = snapshot.TileFrameDeltaTime;
            _tileActualSpeed = snapshot.TileActualSpeed;
            _tileIsMoving = snapshot.TileIsMoving;
            _tileFrameCollisionProjected = snapshot.TileCollisionProjected;
            _tilePresentationFrameOpen = snapshot.TilePresentationFrameOpen;
            _tileFacingState = snapshot.TileFacingState;
            _tileFacingStateInitialized = snapshot.TileFacingStateInitialized;
            _lastSemanticDirection = snapshot.LastSemanticDirection;
            _lastMotionDirection = snapshot.LastMotionDirection;
            _usedSemanticHeading = snapshot.UsedSemanticHeading;
            _lastFacingAlignmentDot = snapshot.LastFacingAlignmentDot;
            _lastFacingAngularErrorDegrees = snapshot.LastFacingAngularErrorDegrees;
            _tileGaitState = snapshot.TileGaitState;
            _tileGaitStateInitialized = snapshot.TileGaitStateInitialized;
            _officeSeatingFacingLocked = snapshot.OfficeSeatingFacingLocked;
            _lockedOfficeSeatingDirection = snapshot.LockedOfficeSeatingDirection;
            _officeSeatingFacingViolationCount = snapshot.OfficeSeatingFacingViolationCount;
            _maximumOfficeSeatingFacingDelta = snapshot.MaximumOfficeSeatingFacingDelta;
            _officeWorkSpriteDirectionViolationCount = snapshot.OfficeWorkSpriteDirectionViolationCount;
            _maximumOfficeWorkSpriteDirectionDelta = snapshot.MaximumOfficeWorkSpriteDirectionDelta;
            _officeAppliedSpriteDirectionViolationCount = snapshot.OfficeAppliedSpriteDirectionViolationCount;
            _maximumOfficeAppliedSpriteDirectionDelta = snapshot.MaximumOfficeAppliedSpriteDirectionDelta;
            _currentAppliedSpriteDirection = snapshot.CurrentAppliedSpriteDirection;
            _currentAppliedSpriteDirectionMatchesLock = snapshot.CurrentAppliedSpriteDirectionMatchesLock;
            if (targetRenderer != null)
            {
                targetRenderer.sprite = snapshot.RendererSprite;
                targetRenderer.flipX = snapshot.RendererFlipX;
            }
        }

        internal void CompleteAtomicPresentationSessionNoThrow()
        {
            IDisposable session = _officeWorkSession;
            _officeWorkSession = null;
            if (session == null) return;
            try
            {
                session.Dispose();
            }
            catch (Exception)
            {
                // Presentation hooks are optional. Their teardown cannot invalidate an already
                // published canonical seat/exit transaction or re-enter the movement scheduler.
            }
        }

        internal void RebaseTileMotionAfterAtomicPlacement(int direction)
        {
            _tileDisplacementDirection = true;
            _tileFrameDisplacement = Vector2.zero;
            _tileFrameTravelDistance = 0f;
            _tileSemanticDisplacement = Vector2.zero;
            _tileFrameDeltaTime = 0f;
            _tileActualSpeed = 0f;
            _tileIsMoving = false;
            _tileFrameCollisionProjected = false;
            _worldVelocity = Vector3.zero;
            _usedSemanticHeading = false;
            _lastFacingAlignmentDot = 1f;
            _lastFacingAngularErrorDegrees = 0f;
            ResetTileFacingState(Mathf.Clamp(direction, 0, DirectionCount - 1));
            ResetTileGaitState(Mathf.Clamp(direction, 0, DirectionCount - 1));
        }

        internal readonly struct AtomicPresentationSnapshot
        {
            public AtomicPresentationSnapshot(
                Vector3 worldVelocity, float frameClock, int walkFrame, int lastDirection,
                OfficeSeatingAnimationClip? seatingClip, float seatingElapsedSeconds,
                float seatingProgress, int seatingFrame, bool seatingTransitionComplete,
                IOfficeSeatedWorkAnimationSession workSession, bool tileDisplacementDirection,
                Vector2 tileFrameDisplacement, float tileFrameTravelDistance,
                Vector2 tileSemanticDisplacement, float tileFrameDeltaTime, float tileActualSpeed,
                bool tileIsMoving, bool tileCollisionProjected, bool tilePresentationFrameOpen,
                OfficeLocomotionFacingState tileFacingState, bool tileFacingStateInitialized,
                int lastSemanticDirection, int lastMotionDirection, bool usedSemanticHeading,
                float lastFacingAlignmentDot, float lastFacingAngularErrorDegrees,
                OfficeLocomotionGaitState tileGaitState, bool tileGaitStateInitialized,
                bool officeSeatingFacingLocked, int lockedOfficeSeatingDirection,
                int officeSeatingFacingViolationCount, int maximumOfficeSeatingFacingDelta,
                int officeWorkSpriteDirectionViolationCount, int maximumOfficeWorkSpriteDirectionDelta,
                int officeAppliedSpriteDirectionViolationCount, int maximumOfficeAppliedSpriteDirectionDelta,
                int currentAppliedSpriteDirection, bool currentAppliedSpriteDirectionMatchesLock,
                Sprite rendererSprite, bool rendererFlipX)
            {
                WorldVelocity = worldVelocity;
                FrameClock = frameClock;
                WalkFrame = walkFrame;
                LastDirection = lastDirection;
                SeatingClip = seatingClip;
                SeatingElapsedSeconds = seatingElapsedSeconds;
                SeatingProgress = seatingProgress;
                SeatingFrame = seatingFrame;
                SeatingTransitionComplete = seatingTransitionComplete;
                WorkSession = workSession;
                TileDisplacementDirection = tileDisplacementDirection;
                TileFrameDisplacement = tileFrameDisplacement;
                TileFrameTravelDistance = tileFrameTravelDistance;
                TileSemanticDisplacement = tileSemanticDisplacement;
                TileFrameDeltaTime = tileFrameDeltaTime;
                TileActualSpeed = tileActualSpeed;
                TileIsMoving = tileIsMoving;
                TileCollisionProjected = tileCollisionProjected;
                TilePresentationFrameOpen = tilePresentationFrameOpen;
                TileFacingState = tileFacingState;
                TileFacingStateInitialized = tileFacingStateInitialized;
                LastSemanticDirection = lastSemanticDirection;
                LastMotionDirection = lastMotionDirection;
                UsedSemanticHeading = usedSemanticHeading;
                LastFacingAlignmentDot = lastFacingAlignmentDot;
                LastFacingAngularErrorDegrees = lastFacingAngularErrorDegrees;
                TileGaitState = tileGaitState;
                TileGaitStateInitialized = tileGaitStateInitialized;
                OfficeSeatingFacingLocked = officeSeatingFacingLocked;
                LockedOfficeSeatingDirection = lockedOfficeSeatingDirection;
                OfficeSeatingFacingViolationCount = officeSeatingFacingViolationCount;
                MaximumOfficeSeatingFacingDelta = maximumOfficeSeatingFacingDelta;
                OfficeWorkSpriteDirectionViolationCount = officeWorkSpriteDirectionViolationCount;
                MaximumOfficeWorkSpriteDirectionDelta = maximumOfficeWorkSpriteDirectionDelta;
                OfficeAppliedSpriteDirectionViolationCount = officeAppliedSpriteDirectionViolationCount;
                MaximumOfficeAppliedSpriteDirectionDelta = maximumOfficeAppliedSpriteDirectionDelta;
                CurrentAppliedSpriteDirection = currentAppliedSpriteDirection;
                CurrentAppliedSpriteDirectionMatchesLock = currentAppliedSpriteDirectionMatchesLock;
                RendererSprite = rendererSprite;
                RendererFlipX = rendererFlipX;
            }

            public Vector3 WorldVelocity { get; }
            public float FrameClock { get; }
            public int WalkFrame { get; }
            public int LastDirection { get; }
            public OfficeSeatingAnimationClip? SeatingClip { get; }
            public float SeatingElapsedSeconds { get; }
            public float SeatingProgress { get; }
            public int SeatingFrame { get; }
            public bool SeatingTransitionComplete { get; }
            public IOfficeSeatedWorkAnimationSession WorkSession { get; }
            public bool TileDisplacementDirection { get; }
            public Vector2 TileFrameDisplacement { get; }
            public float TileFrameTravelDistance { get; }
            public Vector2 TileSemanticDisplacement { get; }
            public float TileFrameDeltaTime { get; }
            public float TileActualSpeed { get; }
            public bool TileIsMoving { get; }
            public bool TileCollisionProjected { get; }
            public bool TilePresentationFrameOpen { get; }
            public OfficeLocomotionFacingState TileFacingState { get; }
            public bool TileFacingStateInitialized { get; }
            public int LastSemanticDirection { get; }
            public int LastMotionDirection { get; }
            public bool UsedSemanticHeading { get; }
            public float LastFacingAlignmentDot { get; }
            public float LastFacingAngularErrorDegrees { get; }
            public OfficeLocomotionGaitState TileGaitState { get; }
            public bool TileGaitStateInitialized { get; }
            public bool OfficeSeatingFacingLocked { get; }
            public int LockedOfficeSeatingDirection { get; }
            public int OfficeSeatingFacingViolationCount { get; }
            public int MaximumOfficeSeatingFacingDelta { get; }
            public int OfficeWorkSpriteDirectionViolationCount { get; }
            public int MaximumOfficeWorkSpriteDirectionDelta { get; }
            public int OfficeAppliedSpriteDirectionViolationCount { get; }
            public int MaximumOfficeAppliedSpriteDirectionDelta { get; }
            public int CurrentAppliedSpriteDirection { get; }
            public bool CurrentAppliedSpriteDirectionMatchesLock { get; }
            public Sprite RendererSprite { get; }
            public bool RendererFlipX { get; }
        }

        public bool BeginSitDown(int direction)
        {
            if (!HasOfficeSeatingFrames ||
                !_officeSeatingFacingLocked ||
                _seatingClip.HasValue ||
                direction != _lockedOfficeSeatingDirection)
            {
                if (_officeSeatingFacingLocked && direction != _lockedOfficeSeatingDirection)
                    RecordOfficeSeatingFacingViolation(direction);
                return false;
            }
            BeginOfficeSeatingClip(OfficeSeatingAnimationClip.SitDown, direction);
            return true;
        }

        public bool BeginSeatedWork()
        {
            if (!HasOfficeSeatingFrames ||
                !_officeSeatingFacingLocked ||
                !_seatingClip.HasValue ||
                _seatingClip.Value != OfficeSeatingAnimationClip.SitDown ||
                !_seatingTransitionComplete)
            {
                return false;
            }
            EndOfficeWorkSession();
            if (_officeWorkHook != null &&
                _officeWorkHook.TryBegin(_lockedOfficeSeatingDirection, out var session) &&
                session != null)
            {
                _officeWorkSession = session;
            }
            BeginOfficeSeatingClip(
                OfficeSeatingAnimationClip.Work,
                _lockedOfficeSeatingDirection);
            return true;
        }

        public void RequestOfficeWorkSafeStop()
        {
            _officeWorkSession?.RequestSafeStop();
        }

        public bool BeginStandUp()
        {
            if (!HasOfficeSeatingFrames ||
                !_officeSeatingFacingLocked ||
                !_seatingClip.HasValue ||
                _seatingClip.Value != OfficeSeatingAnimationClip.Work ||
                !IsOfficeWorkSafeToStand)
                return false;
            EndOfficeWorkSession();
            BeginOfficeSeatingClip(
                OfficeSeatingAnimationClip.StandUp,
                _lockedOfficeSeatingDirection);
            return true;
        }

        /// <summary>
        /// Ends the StandUp pose but deliberately retains the seat-facing lock while the actor
        /// traverses LeavingSeat. ReleaseOfficeSeatingFacingLock is the only normal unlock.
        /// </summary>
        public bool FinishOfficeSeatingPoseForLeavingSeat()
        {
            if (!_officeSeatingFacingLocked ||
                !_seatingClip.HasValue ||
                _seatingClip.Value != OfficeSeatingAnimationClip.StandUp ||
                !_seatingTransitionComplete)
            {
                return false;
            }

            ClearOfficeSeatingPose();
            ApplyFrame();
            return true;
        }

        public bool ReleaseOfficeSeatingFacingLock()
        {
            if (!_officeSeatingFacingLocked) return true;
            if (_seatingClip.HasValue) return false;
            _officeSeatingFacingLocked = false;
            _lockedOfficeSeatingDirection = -1;
            ResetTileFacingState(_lastDirection);
            ResetTileGaitState(_lastDirection);
            ApplyFrame();
            return true;
        }

        public void ResumeWalkingAfterSeating()
        {
            ClearOfficeSeatingPose();
            _officeSeatingFacingLocked = false;
            _lockedOfficeSeatingDirection = -1;
            ResetTileFacingState(_lastDirection);
            ApplyFrame();
        }

        public void ResumeWalkingAfterSeating(bool keepFacingLock)
        {
            if (!keepFacingLock)
            {
                ResumeWalkingAfterSeating();
                return;
            }
            if (!FinishOfficeSeatingPoseForLeavingSeat())
            {
                throw new InvalidOperationException(
                    "The facing lock can be retained only after a completed StandUp clip.");
            }
        }

        public void Tick(float deltaTime)
        {
            if (targetRenderer == null) return;
            EnforceOfficeSeatingFacingLock();
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
                EnsureTileGaitState();
                float resolvedDeltaTime = _tileFrameDeltaTime > 0.000001f
                    ? _tileFrameDeltaTime
                    : Mathf.Max(0f, deltaTime);
                Vector2 facingRequest = WorldVectorToFacingAxes(_tileSemanticDisplacement);
                Vector2 facingMotion = WorldVectorToFacingAxes(_tileFrameDisplacement);
                OfficeSharedLocomotionFrameResult locomotion =
                    OfficeSharedLocomotionRules.ResolveFrame(
                        _tileFacingState,
                        _tileGaitState,
                        new OfficeNavPoint(
                            facingRequest.x,
                            facingRequest.y),
                        new OfficeNavPoint(
                            facingMotion.x,
                            facingMotion.y),
                        _tileFrameTravelDistance,
                        resolvedDeltaTime,
                        _tileFrameCollisionProjected,
                        OfficeLocomotionGaitRules.DefaultStrideLength,
                        WalkFrameCount);
                _tileFacingState = locomotion.FacingState;
                _tileGaitState = locomotion.GaitState;
                _tileActualSpeed = locomotion.ActualSpeed;
                _tileIsMoving = locomotion.IsMoving;
                _lastSemanticDirection = locomotion.RequestedDirection;
                _lastMotionDirection = locomotion.MotionDirection;
                _usedSemanticHeading = locomotion.UsedRequestedFacing;
                _lastFacingAlignmentDot = locomotion.FacingAlignmentDot;
                _lastFacingAngularErrorDegrees = locomotion.FacingAngularErrorDegrees;
                if (_officeSeatingFacingLocked)
                {
                    // Seating is a planted transition, not free locomotion. Keep the chair-facing
                    // direction authoritative until the reserved safe egress anchor is reached,
                    // while still advancing the shared gait from actual travelled distance.
                    _tileFacingState = OfficeLocomotionFacingState.Initial(
                        _lockedOfficeSeatingDirection);
                    _tileGaitState = OfficeLocomotionGaitRules.Resolve(
                        _tileGaitState,
                        locomotion.IsMoving ? _tileFrameTravelDistance : 0f,
                        resolvedDeltaTime,
                        locomotion.HasRequest,
                        _lockedOfficeSeatingDirection,
                        OfficeLocomotionGaitRules.DefaultStrideLength,
                        WalkFrameCount);
                    _lastDirection = _lockedOfficeSeatingDirection;
                }
                else
                {
                    _tileFacingState = locomotion.FacingState;
                    _tileGaitState = locomotion.GaitState;
                    _lastDirection = _tileGaitState.DisplayDirection;
                }
                _walkFrame = _continuousRouteLocomotionPresentation && locomotion.IsMoving
                    ? OfficeLocomotionGaitRules.DistanceFrame(
                        _tileGaitState.AccumulatedDistance,
                        OfficeLocomotionGaitRules.DefaultStrideLength,
                        WalkFrameCount)
                    : _tileGaitState.Frame;
                _frameClock = 0f;
            }
            else
            {
                if (IsMoving)
                {
                    if (!_officeSeatingFacingLocked)
                    {
                        _lastDirection = ResolveDirection(
                            _worldVelocity,
                            _lastDirection,
                            facingHysteresisDegrees);
                    }
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

        private static Vector2 WorldVectorToFacingAxes(Vector2 worldVector) =>
            OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(worldVector);

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
            // Eight authored directions are the final visual authority. Reassert this at the
            // consumer boundary so a serialized/legacy/external flip cannot outlive Configure and
            // turn correct direction metadata into the wrong body silhouette for even one frame.
            targetRenderer.flipX = false;
            EnforceOfficeSeatingFacingLock();
            _currentAppliedSpriteDirection = -1;
            _currentAppliedSpriteDirectionMatchesLock = true;
            if (_seatingClip.HasValue && HasOfficeSeatingFrames)
            {
                bool safeStatic = seatingPresentationMode == OfficeSeatingPresentationMode.SafeStaticWork;
                OfficeSeatingAnimationClip appliedClip = safeStatic
                    ? OfficeSeatingAnimationClip.Work
                    : _seatingClip.Value;
                int appliedFrame = safeStatic ? 0 : _seatingFrame;
                bool applyingWorkHook =
                    !safeStatic &&
                    _seatingClip.Value == OfficeSeatingAnimationClip.Work &&
                    _officeWorkSession != null;
                var hookSprite = applyingWorkHook
                    ? _officeWorkSession?.CurrentSprite
                    : null;
                if (hookSprite != null &&
                    OfficeWorkActionFrameSet.TryResolveNamedDirection(
                        hookSprite,
                        out int hookDirection) &&
                    hookDirection != _lockedOfficeSeatingDirection)
                {
                    _officeWorkSpriteDirectionViolationCount++;
                    _maximumOfficeWorkSpriteDirectionDelta = Mathf.Max(
                        _maximumOfficeWorkSpriteDirectionDelta,
                        OctantDistance(_lockedOfficeSeatingDirection, hookDirection));
                    hookSprite = null;
                }
                targetRenderer.sprite = hookSprite != null
                    ? hookSprite
                    : GetOfficeSeatingFrame(appliedClip, _lastDirection, appliedFrame);
                CaptureAppliedSpriteDirection(targetRenderer.sprite);
                OfficeFrameApplied?.Invoke(appliedClip, appliedFrame, targetRenderer.sprite);
                return;
            }
            if (walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            if (IsLocomotionTransitionSpriteActive)
            {
                targetRenderer.sprite = GetLocomotionTransitionFrame(
                    _tileGaitState.Phase,
                    _lastDirection,
                    ResolveLocomotionTransitionPose());
                CaptureAppliedSpriteDirection(targetRenderer.sprite);
                return;
            }
            if (_tileDisplacementDirection &&
                _tileGaitStateInitialized &&
                _tileGaitState.Phase == OfficeLocomotionPhase.Idle &&
                HasCompleteFrames(idleFrames, DirectionCount))
            {
                targetRenderer.sprite = idleFrames[_lastDirection];
                CaptureAppliedSpriteDirection(targetRenderer.sprite);
                return;
            }
            targetRenderer.sprite = walkFrames[_walkFrame * DirectionCount + _lastDirection];
            CaptureAppliedSpriteDirection(targetRenderer.sprite);
        }

        private int ResolveLocomotionTransitionPose()
        {
            float shuffleDistance = OfficeLocomotionGaitRules.DefaultStrideLength *
                                    OfficeLocomotionGaitRules.ShortShuffleStrideFraction;
            return _tileGaitState.Phase switch
            {
                OfficeLocomotionPhase.Idle => 1,
                OfficeLocomotionPhase.StartStep =>
                    _tileGaitState.EpisodeDistance < shuffleDistance * 0.5f ? 0 : 1,
                OfficeLocomotionPhase.Stopping =>
                    _tileGaitState.StopSeconds < OfficeLocomotionGaitRules.StopSettleSeconds * 0.5f
                        ? 0
                        : 1,
                OfficeLocomotionPhase.ShortShuffle =>
                    _tileGaitState.StopSeconds <
                    OfficeLocomotionGaitRules.StopSettleSeconds * 0.5f ? 0 : 1,
                OfficeLocomotionPhase.Pivot =>
                    _tileGaitState.TransitionSeconds <
                    OfficeLocomotionGaitRules.PivotSeconds * 0.5f ? 0 : 1,
                _ => 0
            };
        }

        private static int ResolveLocomotionTransitionClip(OfficeLocomotionPhase phase)
        {
            return phase switch
            {
                OfficeLocomotionPhase.Pivot => 0,
                OfficeLocomotionPhase.StartStep => 1,
                OfficeLocomotionPhase.Stopping => 2,
                OfficeLocomotionPhase.ShortShuffle => 3,
                OfficeLocomotionPhase.Idle => 2,
                _ => 1
            };
        }

        private void BeginOfficeSeatingClip(OfficeSeatingAnimationClip clip, int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (!_officeSeatingFacingLocked || direction != _lockedOfficeSeatingDirection)
                throw new InvalidOperationException("Office seating clips require the immutable seat-facing lock.");
            _worldVelocity = Vector3.zero;
            _lastDirection = direction;
            ResetTileFacingState(direction);
            _seatingClip = clip;
            _seatingElapsedSeconds = 0f;
            _seatingProgress01 = 0f;
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

            if (clip == OfficeSeatingAnimationClip.Work)
            {
                _seatingElapsedSeconds += deltaTime;
                var secondsPerFrame = Mathf.Max(0.05f, seatedWorkFrameSeconds);
                if (_seatingElapsedSeconds < secondsPerFrame) return;
                _seatingElapsedSeconds -= secondsPerFrame;
                _seatingFrame = (_seatingFrame + 1) %
                    OfficeSeatingAnimationFrames.FrameCount(clip);
                return;
            }

            var durationSeconds = clip == OfficeSeatingAnimationClip.SitDown
                ? Mathf.Max(0.05f, sitDownDurationSeconds)
                : Mathf.Max(0.05f, standUpDurationSeconds);
            _seatingElapsedSeconds = Mathf.Min(durationSeconds, _seatingElapsedSeconds + deltaTime);
            _seatingProgress01 = Mathf.Clamp01(_seatingElapsedSeconds / durationSeconds);

            // Presentation advances at most one authored pose per rendered Tick. This preserves all
            // 4/4 transition poses even after a long frame while placement follows elapsed progress.
            var frameCount = OfficeSeatingAnimationFrames.FrameCount(clip);
            var desiredFrame = Mathf.Min(
                frameCount - 1,
                Mathf.FloorToInt(_seatingProgress01 * frameCount));
            if (_seatingFrame < desiredFrame)
            {
                _seatingFrame++;
            }

            _seatingTransitionComplete =
                _seatingProgress01 >= 1f && _seatingFrame >= frameCount - 1;
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
            ClearOfficeSeatingPose();
            _officeSeatingFacingLocked = false;
            _lockedOfficeSeatingDirection = -1;
            ApplyFrame();
        }

        private void ClearOfficeSeatingPose()
        {
            EndOfficeWorkSession();
            _seatingClip = null;
            _seatingElapsedSeconds = 0f;
            _seatingProgress01 = 0f;
            _seatingFrame = 0;
            _seatingTransitionComplete = false;
            ResetTileGaitState(_lastDirection);
        }

        private void EnforceOfficeSeatingFacingLock()
        {
            if (!_officeSeatingFacingLocked) return;
            if (_lastDirection != _lockedOfficeSeatingDirection)
                RecordOfficeSeatingFacingViolation(_lastDirection);
            _lastDirection = _lockedOfficeSeatingDirection;
            if (!_tileFacingStateInitialized ||
                _tileFacingState.VisualDirection != _lockedOfficeSeatingDirection)
            {
                ResetTileFacingState(_lockedOfficeSeatingDirection);
            }
        }

        private void CaptureAppliedSpriteDirection(Sprite sprite)
        {
            if (!OfficeWorkActionFrameSet.TryResolveNamedDirection(
                    sprite,
                    out _currentAppliedSpriteDirection))
            {
                return;
            }
            if (!_officeSeatingFacingLocked) return;
            _currentAppliedSpriteDirectionMatchesLock =
                _currentAppliedSpriteDirection == _lockedOfficeSeatingDirection;
            if (!_currentAppliedSpriteDirectionMatchesLock)
            {
                _officeAppliedSpriteDirectionViolationCount++;
                _maximumOfficeAppliedSpriteDirectionDelta = Mathf.Max(
                    _maximumOfficeAppliedSpriteDirectionDelta,
                    OctantDistance(
                        _lockedOfficeSeatingDirection,
                        _currentAppliedSpriteDirection));
            }
        }

        private void RecordOfficeSeatingFacingViolation(int requestedOrObservedDirection)
        {
            _officeSeatingFacingViolationCount++;
            if (!_officeSeatingFacingLocked ||
                requestedOrObservedDirection < 0 ||
                requestedOrObservedDirection >= DirectionCount)
            {
                return;
            }
            _maximumOfficeSeatingFacingDelta = Mathf.Max(
                _maximumOfficeSeatingFacingDelta,
                OctantDistance(_lockedOfficeSeatingDirection, requestedOrObservedDirection));
        }

        private void ResetOfficeSeatingDirectionMetrics()
        {
            _officeSeatingFacingViolationCount = 0;
            _maximumOfficeSeatingFacingDelta = 0;
            _officeWorkSpriteDirectionViolationCount = 0;
            _maximumOfficeWorkSpriteDirectionDelta = 0;
            _officeAppliedSpriteDirectionViolationCount = 0;
            _maximumOfficeAppliedSpriteDirectionDelta = 0;
            _currentAppliedSpriteDirection = -1;
            _currentAppliedSpriteDirectionMatchesLock = true;
        }

        private static int OctantDistance(int left, int right)
        {
            int direct = Mathf.Abs(left - right);
            return Mathf.Min(direct, DirectionCount - direct);
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
            _lastFacingAlignmentDot = 1f;
            _lastFacingAngularErrorDegrees = 0f;
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

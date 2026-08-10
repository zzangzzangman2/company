using System;
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
        private Vector3 _worldVelocity;
        private float _frameClock;
        private int _walkFrame;
        private int _lastDirection;

        public int CurrentDirection => _lastDirection;
        public int CurrentWalkFrame => _walkFrame;
        public bool IsMoving => _worldVelocity.sqrMagnitude > 0.0025f;
        public int ConfiguredFrameCount => walkFrames?.Length ?? 0;
        public float BaseFrameSeconds => frameSeconds;
        public float EffectiveFrameSeconds => ResolveEffectiveFrameSeconds();
        public int IdleWalkFrame => Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
        public Sprite CurrentSprite => targetRenderer == null ? null : targetRenderer.sprite;

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float secondsPerFrame = 0.11f)
        {
            targetRenderer = renderer;
            walkFrames = frames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(0.05f, secondsPerFrame);
            _walkFrame = Mathf.Clamp(idleWalkFrame, 0, WalkFrameCount - 1);
            ApplyFrame();
        }

        public void SetWorldVelocity(Vector3 velocity)
        {
            _worldVelocity = new Vector3(velocity.x, 0f, velocity.z);
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

        public void Tick(float deltaTime)
        {
            if (targetRenderer == null || walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            if (IsMoving)
            {
                _lastDirection = ResolveDirection(_worldVelocity);
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

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void ApplyFrame()
        {
            if (targetRenderer == null || walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            targetRenderer.sprite = walkFrames[_walkFrame * DirectionCount + _lastDirection];
        }

        private static int ResolveDirection(Vector3 velocity)
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
            return ResolveDirectionFromAxes(horizontal, vertical);
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

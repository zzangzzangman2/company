using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        private const int DirectionCount = 4;
        private const int RequiredFrameCount = 8;

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private float frameSeconds = 0.2f;
        private Vector3 _worldVelocity;
        private float _frameClock;
        private int _walkFrame;
        private int _lastDirection;

        public int CurrentDirection => _lastDirection;
        public bool IsMoving => _worldVelocity.sqrMagnitude > 0.0025f;

        public void Configure(SpriteRenderer renderer, Sprite[] frames, float secondsPerFrame = 0.2f)
        {
            targetRenderer = renderer;
            walkFrames = frames ?? Array.Empty<Sprite>();
            frameSeconds = Mathf.Max(0.08f, secondsPerFrame);
            ApplyFrame();
        }

        public void SetWorldVelocity(Vector3 velocity)
        {
            _worldVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        private void Update()
        {
            if (targetRenderer == null || walkFrames == null || walkFrames.Length < RequiredFrameCount) return;
            if (IsMoving)
            {
                _lastDirection = ResolveDirection(_worldVelocity);
                _frameClock += Time.deltaTime;
                if (_frameClock >= frameSeconds)
                {
                    _frameClock -= frameSeconds;
                    _walkFrame = 1 - _walkFrame;
                }
            }
            else
            {
                _frameClock = 0f;
                _walkFrame = 0;
            }

            ApplyFrame();
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
            if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
            {
                return horizontal < 0f ? 1 : 3;
            }

            return vertical < 0f ? 0 : 2;
        }
    }
}


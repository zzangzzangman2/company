using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        private CharacterController _controller;
        private DirectionalSpriteAnimator _spriteAnimator;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spriteAnimator = GetComponent<DirectionalSpriteAnimator>();
        }

        private void Update()
        {
            var x = Input.GetAxisRaw("Horizontal");
            var z = Input.GetAxisRaw("Vertical");
            var targetCamera = Camera.main;
            var cameraRight = targetCamera == null ? Vector3.right : targetCamera.transform.right;
            var cameraForward = targetCamera == null ? Vector3.forward : targetCamera.transform.forward;
            cameraRight.y = 0f;
            cameraForward.y = 0f;
            cameraRight.Normalize();
            cameraForward.Normalize();
            var movement = (cameraRight * x + cameraForward * z).normalized * moveSpeed;
            if (!_controller.isGrounded) movement.y = -2f;
            Vector3 before = transform.position;
            _controller.Move(movement * Time.deltaTime);
            Vector3 actual = transform.position - before;
            float inverseDelta = Time.deltaTime > 0.000001f ? 1f / Time.deltaTime : 0f;
            _spriteAnimator?.SetWorldVelocity(new Vector3(
                actual.x * inverseDelta,
                0f,
                actual.z * inverseDelta));
            if (movement.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, new Vector3(movement.x, 0f, movement.z), Time.deltaTime * 12f);
            }
        }
    }
}

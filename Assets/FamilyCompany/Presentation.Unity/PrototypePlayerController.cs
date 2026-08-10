using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            var x = Input.GetAxisRaw("Horizontal");
            var z = Input.GetAxisRaw("Vertical");
            var movement = new Vector3(x, 0f, z).normalized * moveSpeed;
            if (!_controller.isGrounded) movement.y = -2f;
            _controller.Move(movement * Time.deltaTime);
            if (movement.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, new Vector3(movement.x, 0f, movement.z), Time.deltaTime * 12f);
            }
        }
    }
}


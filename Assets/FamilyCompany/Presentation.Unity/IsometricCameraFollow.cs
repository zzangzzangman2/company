using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class IsometricCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(-10f, 13f, -10f);
        [SerializeField] private float smoothTime = 0.12f;
        private Vector3 _velocity;

        public void SetTarget(Transform value)
        {
            target = value;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref _velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }
    }
}


using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class IsometricCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(-10f, 13f, -10f);
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float minimumOrthographicSize = 5f;
        [SerializeField] private float maximumOrthographicSize = 10f;
        [SerializeField] private float zoomSpeed = 0.75f;
        private Vector3 _velocity;
        private Camera _camera;

        public void SetTarget(Transform value)
        {
            target = value;
        }

        public void Configure(Transform value, Vector3 newOffset, float orthographicSize)
        {
            target = value;
            offset = newOffset;
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = orthographicSize;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (_camera == null || !_camera.orthographic) return;
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize - scroll * zoomSpeed,
                    minimumOrthographicSize,
                    maximumOrthographicSize);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref _velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * 1.2f);
        }
    }
}

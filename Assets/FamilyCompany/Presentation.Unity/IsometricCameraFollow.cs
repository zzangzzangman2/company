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
        [SerializeField] private bool officeFramingEnabled;
        [SerializeField] private Vector3 officeCenter = new Vector3(14f, 0f, 0f);
        [SerializeField] private Vector2 officeSize = new Vector2(16f, 14f);
        [SerializeField] private Vector3 officeOffset = new Vector3(0f, 13.5f, -13.5f);
        [SerializeField] private float officeLookHeight = 0.6f;
        [SerializeField] private float defaultOrthographicSize = 7.2f;
        [SerializeField] private float officeOrthographicSize = 6.6f;
        [SerializeField] private float orthographicSmoothTime = 0.16f;
        private Vector3 _velocity;
        private Camera _camera;
        private float _orthographicVelocity;
        private float _zoomOffset;
        private bool _officeObservationForced;

        public bool IsOfficeFramingActive { get; private set; }
        public bool OfficeFramingEnabled => officeFramingEnabled;
        public Vector3 OfficeCenter => officeCenter;
        public Vector2 OfficeSize => officeSize;
        public float OfficeOrthographicSize => officeOrthographicSize;
        public bool IsOfficeObservationForced => _officeObservationForced;

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

            defaultOrthographicSize = orthographicSize;
            _zoomOffset = 0f;
        }

        public void ConfigureOfficeFraming(Vector3 center, Vector2 size, float orthographicSize)
        {
            officeFramingEnabled = true;
            officeCenter = center;
            officeSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            officeOrthographicSize = Mathf.Clamp(
                orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        public void SetOfficeObservationForced(bool value, bool snapImmediately)
        {
            _officeObservationForced = value;
            _zoomOffset = 0f;
            if (!snapImmediately || target == null) return;

            var frameOffice = value || (officeFramingEnabled && IsTargetInsideOfficeBounds());
            var focus = frameOffice ? officeCenter : target.position;
            var activeOffset = frameOffice ? officeOffset : offset;
            var lookHeight = frameOffice ? officeLookHeight : 1.2f;
            transform.position = focus + activeOffset;
            transform.LookAt(focus + Vector3.up * lookHeight);
            _velocity = Vector3.zero;
            _orthographicVelocity = 0f;
            IsOfficeFramingActive = frameOffice;
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null && _camera.orthographic)
                _camera.orthographicSize = frameOffice ? officeOrthographicSize : defaultOrthographicSize;
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
                var baseSize = IsTargetInsideOffice() ? officeOrthographicSize : defaultOrthographicSize;
                _zoomOffset = Mathf.Clamp(
                    _zoomOffset - scroll * zoomSpeed,
                    minimumOrthographicSize - baseSize,
                    maximumOrthographicSize - baseSize);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            IsOfficeFramingActive = IsTargetInsideOffice();
            var focus = IsOfficeFramingActive ? officeCenter : target.position;
            var activeOffset = IsOfficeFramingActive ? officeOffset : offset;
            var lookHeight = IsOfficeFramingActive ? officeLookHeight : 1.2f;
            transform.position = Vector3.SmoothDamp(transform.position, focus + activeOffset, ref _velocity, smoothTime);
            transform.LookAt(focus + Vector3.up * lookHeight);

            if (_camera == null || !_camera.orthographic) return;
            var baseSize = IsOfficeFramingActive ? officeOrthographicSize : defaultOrthographicSize;
            var desiredSize = Mathf.Clamp(baseSize + _zoomOffset, minimumOrthographicSize, maximumOrthographicSize);
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                desiredSize,
                ref _orthographicVelocity,
                orthographicSmoothTime);
        }

        private bool IsTargetInsideOffice()
        {
            return officeFramingEnabled && (_officeObservationForced || IsTargetInsideOfficeBounds());
        }

        private bool IsTargetInsideOfficeBounds()
        {
            if (target == null) return false;
            var halfWidth = officeSize.x * 0.5f;
            var halfDepth = officeSize.y * 0.5f;
            var position = target.position;
            return position.x >= officeCenter.x - halfWidth &&
                   position.x <= officeCenter.x + halfWidth &&
                   position.z >= officeCenter.z - halfDepth &&
                   position.z <= officeCenter.z + halfDepth;
        }
    }
}

using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    public static class OfficeGridCameraFitter
    {
        public const float DefaultOrthographicSize = 6.6f;
        public const float DefaultSafeFraction = 0.96f;

        public static float ResolveOrthographicSize(
            Bounds contentBounds,
            float aspect,
            float minimumSize = DefaultOrthographicSize,
            float safeFraction = DefaultSafeFraction)
        {
            if (aspect <= 0f || float.IsNaN(aspect) || float.IsInfinity(aspect))
                throw new ArgumentOutOfRangeException(nameof(aspect));
            if (minimumSize <= 0f) throw new ArgumentOutOfRangeException(nameof(minimumSize));
            if (safeFraction <= 0f || safeFraction > 1f) throw new ArgumentOutOfRangeException(nameof(safeFraction));
            var vertical = contentBounds.extents.y / safeFraction;
            var horizontal = contentBounds.extents.x / (aspect * safeFraction);
            return Mathf.Max(minimumSize, vertical, horizontal);
        }

        public static void Fit(Camera camera, Bounds contentBounds, float aspect)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            camera.orthographic = true;
            camera.aspect = aspect;
            camera.orthographicSize = ResolveOrthographicSize(contentBounds, aspect);
            camera.transform.position = new Vector3(contentBounds.center.x, contentBounds.center.y, -10f);
            camera.transform.rotation = Quaternion.identity;
        }
    }
}

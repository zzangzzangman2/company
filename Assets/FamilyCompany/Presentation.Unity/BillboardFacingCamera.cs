using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class BillboardFacingCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var targetCamera = Camera.main;
            if (targetCamera == null) return;
            var direction = transform.position - targetCamera.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}


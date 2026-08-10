using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeSeatClickController : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask seatHotspotMask = ~0;
        [SerializeField] private float maximumRayDistance = 500f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        public event Action<OfficeSeatSelection> SeatSelected;

        public Camera RaycastCamera => raycastCamera;
        public LayerMask SeatHotspotMask => seatHotspotMask;

        public void Configure(Camera targetCamera, LayerMask hotspotMask)
        {
            raycastCamera = targetCamera;
            seatHotspotMask = hotspotMask;
        }

        public void SetRaycastCamera(Camera targetCamera)
        {
            raycastCamera = targetCamera;
        }

        private void Update()
        {
            if (OfficeModalInputState.IsInputBlocked || !Input.GetMouseButtonDown(0)) return;
            if (!TrySelectAt(Input.mousePosition, out var selection)) return;
            SeatSelected?.Invoke(selection);
        }

        public bool TrySelectAt(Vector2 screenPoint, out OfficeSeatSelection selection)
        {
            selection = null;
            if (OfficeModalInputState.IsInputBlocked) return false;
            var cameraToUse = raycastCamera != null ? raycastCamera : Camera.main;
            if (cameraToUse == null) return false;

            var ray = cameraToUse.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(
                    ray,
                    out var hit,
                    Mathf.Max(0.01f, maximumRayDistance),
                    seatHotspotMask.value,
                    triggerInteraction))
            {
                return false;
            }

            return TryResolveHotspot(hit.collider, out selection);
        }

        public static bool TryResolveHotspot(Collider hitCollider, out OfficeSeatSelection selection)
        {
            selection = null;
            if (hitCollider == null) return false;

            var components = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component == null || !(component is IOfficeSeatHotspotProvider provider)) continue;
                if (!provider.OwnsSeatHotspot(hitCollider)) continue;

                OfficeSeatSelection candidate;
                try
                {
                    candidate = new OfficeSeatSelection(provider.SeatId, provider.SeatDisplayName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (selection == null || string.CompareOrdinal(candidate.SeatId, selection.SeatId) < 0)
                    selection = candidate;
            }

            return selection != null;
        }
    }
}

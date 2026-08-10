using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeWaypoint : MonoBehaviour
    {
        [SerializeField] private string waypointId = string.Empty;
        [SerializeField] private OfficeActivity activity = OfficeActivity.Work;
        [SerializeField] private float minimumStaySeconds = 1.5f;
        [SerializeField] private float maximumStaySeconds = 3.5f;
        [SerializeField] private OfficeWaypoint[] approachPath = Array.Empty<OfficeWaypoint>();
        [SerializeField] private bool hasArtAnchor;
        [SerializeField] private Vector2 artAnchorPixel;

        public string WaypointId => waypointId;
        public OfficeActivity Activity => activity;
        public float MinimumStaySeconds => minimumStaySeconds;
        public float MaximumStaySeconds => maximumStaySeconds;
        public OfficeWaypoint[] ApproachPath => approachPath ?? Array.Empty<OfficeWaypoint>();
        public bool IsMainCorridor => activity == OfficeActivity.Walking &&
                                      waypointId.StartsWith("corridor_", StringComparison.Ordinal);
        public bool HasArtAnchor => hasArtAnchor;
        public Vector2 ArtAnchorPixel => artAnchorPixel;

        public void Configure(string id, OfficeActivity newActivity, float minimumStay, float maximumStay)
        {
            waypointId = id ?? string.Empty;
            activity = newActivity;
            minimumStaySeconds = Mathf.Max(0f, minimumStay);
            maximumStaySeconds = Mathf.Max(minimumStaySeconds, maximumStay);
        }

        public void ConfigureApproach(params OfficeWaypoint[] path)
        {
            approachPath = path ?? Array.Empty<OfficeWaypoint>();
        }

        public void ConfigureArtAnchor(Vector2 artPixel)
        {
            hasArtAnchor = true;
            artAnchorPixel = artPixel;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = ActivityColor(activity);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.08f, 0.16f);
        }

        private static Color ActivityColor(OfficeActivity value)
        {
            switch (value)
            {
                case OfficeActivity.Reception: return new Color(1f, 0.55f, 0.66f);
                case OfficeActivity.Printing: return new Color(0.45f, 0.78f, 0.95f);
                case OfficeActivity.Meeting: return new Color(0.52f, 0.78f, 0.68f);
                case OfficeActivity.Break: return new Color(1f, 0.78f, 0.38f);
                case OfficeActivity.Outside: return new Color(0.75f, 0.75f, 0.82f);
                default: return new Color(0.42f, 0.72f, 0.95f);
            }
        }
    }
}

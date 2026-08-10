using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [DisallowMultipleComponent]
    public sealed class OfficeNavigationObstacle : MonoBehaviour
    {
        [SerializeField] private bool passableDecoration;
        [SerializeField] private bool useRendererFootprintWhenColliderMissing = true;
        [SerializeField, Min(0f)] private float extraClearance;

        public bool PassableDecoration => passableDecoration;
        public bool UseRendererFootprintWhenColliderMissing => useRendererFootprintWhenColliderMissing;
        public float ExtraClearance => Mathf.Max(0f, extraClearance);

        public void Configure(
            bool isPassableDecoration,
            bool includeRendererFootprint = true,
            float additionalClearance = 0f)
        {
            passableDecoration = isPassableDecoration;
            useRendererFootprintWhenColliderMissing = includeRendererFootprint;
            extraClearance = Mathf.Max(0f, additionalClearance);
            OfficeNavigationWorld.NotifyObstacleMutation();
        }

        private void OnEnable() => OfficeNavigationWorld.NotifyObstacleMutation();
        private void OnDisable() => OfficeNavigationWorld.NotifyObstacleMutation();
        private void OnValidate() => OfficeNavigationWorld.NotifyObstacleMutation();
        private void OnTransformParentChanged() => OfficeNavigationWorld.NotifyObstacleMutation();
        private void OnTransformChildrenChanged() => OfficeNavigationWorld.NotifyObstacleMutation();
    }
}

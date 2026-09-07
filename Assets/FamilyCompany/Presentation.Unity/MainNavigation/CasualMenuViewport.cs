using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>Presentation-only scroll extent. Small windows scroll instead of shrinking Korean copy.</summary>
    public sealed class CasualMenuViewport : MonoBehaviour
    {
        public RectTransform Content;
        public float MinimumPixels;
        private void LateUpdate()
        {
            if (Content == null) return;
            var viewport = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var scale = Mathf.Max(.01f, canvas.scaleFactor);
            var height = Mathf.Max(viewport.rect.height, MinimumPixels / scale);
            var targetSize = new Vector2(-16f / scale, height);
            if ((Content.sizeDelta - targetSize).sqrMagnitude > .01f)
                Content.sizeDelta = targetSize;
        }
    }
}

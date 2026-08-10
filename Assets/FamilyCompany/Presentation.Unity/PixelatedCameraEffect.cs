using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelatedCameraEffect : MonoBehaviour
    {
        [SerializeField, Range(180, 720)] private int internalHeight = 360;
        [SerializeField] private bool adaptiveHalfOutputHeight;
        [SerializeField, Range(180, 720)] private int minimumAdaptiveHeight = 360;
        [SerializeField, Range(180, 720)] private int maximumAdaptiveHeight = 540;

        public bool AdaptiveHalfOutputHeight => adaptiveHalfOutputHeight;
        public int MinimumAdaptiveHeight => minimumAdaptiveHeight;
        public int MaximumAdaptiveHeight => maximumAdaptiveHeight;

        public void Configure(int height)
        {
            internalHeight = Mathf.Clamp(height, 180, 720);
            adaptiveHalfOutputHeight = false;
        }

        public void ConfigureAdaptive(int minimumHeight, int maximumHeight)
        {
            minimumAdaptiveHeight = Mathf.Clamp(minimumHeight, 180, 720);
            maximumAdaptiveHeight = Mathf.Clamp(maximumHeight, minimumAdaptiveHeight, 720);
            adaptiveHalfOutputHeight = true;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var targetHeight = adaptiveHalfOutputHeight
                ? Mathf.Clamp(Mathf.RoundToInt(source.height * 0.5f), minimumAdaptiveHeight, maximumAdaptiveHeight)
                : internalHeight;
            var aspect = source.width / (float)Mathf.Max(1, source.height);
            var internalWidth = Mathf.Max(320, Mathf.RoundToInt(targetHeight * aspect));
            var lowResolution = RenderTexture.GetTemporary(
                internalWidth,
                targetHeight,
                0,
                source.format,
                RenderTextureReadWrite.Default);
            lowResolution.filterMode = FilterMode.Point;
            try
            {
                Graphics.Blit(source, lowResolution);
                Graphics.Blit(lowResolution, destination);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(lowResolution);
            }
        }
    }
}

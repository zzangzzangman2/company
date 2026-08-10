using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelatedCameraEffect : MonoBehaviour
    {
        [SerializeField, Range(180, 720)] private int internalHeight = 360;

        public void Configure(int height)
        {
            internalHeight = Mathf.Clamp(height, 180, 720);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var aspect = source.width / (float)Mathf.Max(1, source.height);
            var internalWidth = Mathf.Max(320, Mathf.RoundToInt(internalHeight * aspect));
            var lowResolution = RenderTexture.GetTemporary(
                internalWidth,
                internalHeight,
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


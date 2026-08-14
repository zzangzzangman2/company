using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Rendering
{
    [CreateAssetMenu(
        fileName = "PixelClarityDefault",
        menuName = "Family Company/Rendering/Pixel Clarity Profile")]
    public sealed class PixelClarityProfile : ScriptableObject
    {
        public const string DefaultResourcePath = "PixelClarityDefault";

        [Header("Native output")]
        [SerializeField, Min(1)] private int referenceWidth = 1920;
        [SerializeField, Min(1)] private int referenceHeight = 1080;
        [SerializeField, Range(0.5f, 1f)] private float nativeRenderScale = 1f;
        [SerializeField] private bool disableLegacyHalfHeightPixelation = true;
        [SerializeField, Range(180, 720)] private int legacyComparisonHeight = 540;

        [Header("Stable presentation grid")]
        [SerializeField] private bool snapCameraToPhysicalPixelGrid = true;
        [SerializeField] private bool snapMovingCharacterPresentation = true;
        [SerializeField, Min(1f)] private float pixelArtPixelsPerUnit = 180f;
        [SerializeField, Range(0.8f, 1f)] private float officeSafeFraction = 0.96f;

        [Header("Pixel-art quality")]
        [SerializeField] private int antiAliasingSamples;
        [SerializeField] private int globalTextureMipmapLimit;

        public int ReferenceWidth => referenceWidth;
        public int ReferenceHeight => referenceHeight;
        public float NativeRenderScale => nativeRenderScale;
        public bool DisableLegacyHalfHeightPixelation => disableLegacyHalfHeightPixelation;
        public int LegacyComparisonHeight => legacyComparisonHeight;
        public bool SnapCameraToPhysicalPixelGrid => snapCameraToPhysicalPixelGrid;
        public bool SnapMovingCharacterPresentation => snapMovingCharacterPresentation;
        public float PixelArtPixelsPerUnit => pixelArtPixelsPerUnit;
        public float OfficeSafeFraction => officeSafeFraction;
        public int AntiAliasingSamples => antiAliasingSamples;
        public int GlobalTextureMipmapLimit => globalTextureMipmapLimit;

        public float ReferenceAspect => referenceWidth / (float)referenceHeight;

        public static PixelClarityProfile LoadDefault()
        {
            PixelClarityProfile profile = Resources.Load<PixelClarityProfile>(DefaultResourcePath);
            if (profile == null)
                throw new InvalidOperationException(
                    "Pixel clarity profile is missing from Resources/" + DefaultResourcePath + ".asset.");
            profile.ValidateOrThrow();
            return profile;
        }

        public void ValidateOrThrow()
        {
            if (referenceWidth <= 0 || referenceHeight <= 0)
                throw new InvalidOperationException("Pixel clarity reference resolution must be positive.");
            if (Math.Abs(ReferenceAspect - 16f / 9f) > 0.0001f)
                throw new InvalidOperationException("Pixel clarity reference resolution must be 16:9.");
            if (Math.Abs(nativeRenderScale - 1f) > 0.0001f)
                throw new InvalidOperationException("The clear default profile must render at native scale 1.0.");
            if (!disableLegacyHalfHeightPixelation)
                throw new InvalidOperationException("The clear default profile must disable legacy half-height pixelation.");
            if (legacyComparisonHeight < 180 || legacyComparisonHeight > 720)
                throw new InvalidOperationException("Legacy comparison height is outside the supported range.");
            if (!snapCameraToPhysicalPixelGrid || !snapMovingCharacterPresentation)
                throw new InvalidOperationException("The clear default profile must enable both presentation snap rules.");
            if (Math.Abs(pixelArtPixelsPerUnit - 180f) > 0.0001f)
                throw new InvalidOperationException("Runtime pixel-art PPU must remain the canonical 180.");
            if (officeSafeFraction <= 0f || officeSafeFraction > 1f)
                throw new InvalidOperationException("Office safe fraction must be in (0,1].");
            if (antiAliasingSamples != 0 && antiAliasingSamples != 2 &&
                antiAliasingSamples != 4 && antiAliasingSamples != 8)
                throw new InvalidOperationException("Anti-aliasing samples must be 0, 2, 4, or 8.");
            if (globalTextureMipmapLimit < 0)
                throw new InvalidOperationException("Global texture mipmap limit cannot be negative.");
        }
    }
}

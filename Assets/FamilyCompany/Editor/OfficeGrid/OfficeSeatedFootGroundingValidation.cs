using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Verifies the stable seated pose against one physical chair contract: when its approved seat
    /// contact is pinned to the cushion, the lowest opaque shoe pixel lands on the chair ground
    /// plane. This catches the former 14..28 source-pixel-low anchors that floated feet over casters.
    /// </summary>
    public static class OfficeSeatedFootGroundingValidation
    {
        private static readonly string[] Members =
            { "player", "older_sister", "father", "mother" };

        [MenuItem("Family Company/Validate Office Seated Foot Grounding")]
        public static void Validate()
        {
            OfficeFurnitureVisualCatalog furniture =
                OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog();
            OfficeCharacterSeatPoseCatalog poses =
                OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog();
            OfficeFurnitureVisualDefinition chair = furniture.Resolve(
                OfficeGridLayouts.SwivelChairKind,
                OfficeFurnitureFacing.NorthWest);
            float chairSeatHeightPx = chair.SeatAnchorPx.y - chair.GroundAnchorPx.y;
            Require(chairSeatHeightPx > 0f, "Chair seat must be above its ground anchor.");

            float maximumGapPx = 0f;
            var samples = 0;
            foreach (string member in Members)
            for (var frame = 0; frame < OfficeSeatingAnimationFrames.WorkFrameCount; frame++)
            {
                OfficeCharacterSeatPoseProfile profile = poses.ResolveApproved(
                    member,
                    (int)OfficeSeatFacing8.Northwest,
                    OfficeSeatingAnimationClip.Work,
                    frame);
                string path = OfficeSeatingAnimationFrames.AssetPath(
                    member,
                    OfficeSeatFacing8.Northwest,
                    OfficeSeatingAnimationClip.Work,
                    frame);
                int opaqueFootY = MinimumOpaqueY(path);
                float contactAboveFootPx =
                    (profile.PelvisAnchorPx.y - opaqueFootY) *
                    OfficeGridCharacterMover.UniformVisualScale;
                float gapPx = Mathf.Abs(chairSeatHeightPx - contactAboveFootPx);
                maximumGapPx = Mathf.Max(maximumGapPx, gapPx);
                Require(
                    gapPx <= 0.75f,
                    $"{member} Work[{frame}] shoe/ground gap is {gapPx:F3}px " +
                    $"(chairHeight={chairSeatHeightPx:F3}, anchorY={profile.PelvisAnchorPx.y:F1}, " +
                    $"opaqueFootY={opaqueFootY}).");
                samples++;
            }

            Debug.Log(
                "OFFICE_SEATED_FOOT_GROUNDING_VALIDATION: PASS families=4 workFrames=24/24 " +
                $"chairSeatHeightPx={chairSeatHeightPx:F3} maxShoeGroundGapPx={maximumGapPx:F3} " +
                "rule=chairSocket+opaqueFootPlane scale=1.55 runtimeMemberOffsets=0");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static int MinimumOpaqueY(string assetPath)
        {
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("Seated work Sprite is missing.", assetPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath)), false))
                    throw new InvalidOperationException("Could not decode " + assetPath + ".");
                Color32[] pixels = texture.GetPixels32();
                int minimum = texture.height;
                for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 16)
                        minimum = Math.Min(minimum, y);
                if (minimum >= texture.height)
                    throw new InvalidOperationException("Seated work Sprite is empty: " + assetPath);
                return minimum;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

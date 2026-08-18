using System;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Unity-side consumer validation for Character Locomotion Generation V1.  Pixel trajectory
    /// measurements live in Tools/verify_character_locomotion_v1.py; this gate proves that Unity
    /// imports and the shipping catalog consume the approved family 4 x 8 x 6 stable paths in the same
    /// direction/phase order used by the distance-owned runtime gait.
    /// </summary>
    public static class CharacterLocomotionGenerationV1Validation
    {
        private static readonly string[] CharacterIds =
        {
            "player", "older_sister", "father", "mother"
        };

        private static readonly string[] Directions =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        [MenuItem("Family Company/Validate Character Locomotion Generation V1")]
        public static void Run()
        {
            HighMotionCharacterArtBuilder.Validate();
            OfficeRuntimeCharacterArtCatalogBuilder.Build();

            OfficeRuntimeCharacterArtCatalog catalog =
                OfficeRuntimeCharacterArtCatalog.LoadDefault();
            Require(catalog != null, "Shipping runtime character catalog is missing.");
            HighMotionDirectionManifest manifest = HighMotionDirectionManifest.LoadDefault();
            Require(manifest != null, "High-motion direction manifest is missing.");

            int checkedFrames = 0;
            foreach (string characterId in CharacterIds)
            {
                Require(catalog.TryCopyWalkFrames(characterId, out Sprite[] frames),
                    "Catalog entry is missing: " + characterId);
                Require(frames.Length == DirectionalSpriteAnimator.RequiredFrameCount,
                    $"{characterId}: expected 48 frames, found {frames.Length}.");
                for (var phase = 0; phase < DirectionalSpriteAnimator.WalkFrameCount; phase++)
                for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
                {
                    Require(manifest.ResolveSourceDirection(characterId, direction) == direction,
                        $"{characterId}: direction {direction} is remapped by the manifest.");
                    Sprite sprite = frames[phase * DirectionalSpriteAnimator.DirectionCount + direction];
                    Require(sprite != null, $"{characterId}/{Directions[direction]}/{phase}: null sprite.");
                    string expected = $"{characterId}_{Directions[direction]}_walk_{phase}";
                    Require(string.Equals(sprite.name, expected, StringComparison.Ordinal),
                        $"Catalog order mismatch: expected {expected}, found {sprite.name}.");
                    checkedFrames++;
                }
            }

            float halfWidth = OfficeGridTilemapPresenter.TileWorldWidth * 0.5f;
            float halfHeight = OfficeGridTilemapPresenter.TileWorldHeight * 0.5f;
            float tileStride = Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            float stride = OfficeLocomotionGaitRules.DefaultStrideLength;
            Require(Mathf.Abs(tileStride - stride) <= 0.000001f,
                $"Tile/world stride mismatch: tile={tileStride:R} gait={stride:R}.");
            float stepsPerSecond = OfficeRuntimeAgent.DefaultMoveSpeed / stride * 2f;
            Require(stepsPerSecond >= 1.9f && stepsPerSecond <= 2.1f,
                $"Cadence is outside the tycoon-walk band: {stepsPerSecond:F4} steps/s.");
            for (var phase = 0; phase < DirectionalSpriteAnimator.WalkFrameCount; phase++)
            {
                float distance = stride * (phase + 0.01f) /
                                 DirectionalSpriteAnimator.WalkFrameCount;
                Require(OfficeLocomotionGaitRules.DistanceFrame(
                            distance,
                            stride,
                            DirectionalSpriteAnimator.WalkFrameCount) == phase,
                    "Distance-owned phase map is inconsistent at phase " + phase + ".");
            }

            Debug.Log(
                "CHARACTER_LOCOMOTION_GENERATION_V1_UNITY: PASS | " +
                $"characters={CharacterIds.Length} directions={Directions.Length} " +
                $"frames={checkedFrames} stride={stride:F8} stepsPerSecond={stepsPerSecond:F4}");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

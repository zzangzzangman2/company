using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatingFacingLockValidation
    {
        private const int Northwest = 3;
        private static readonly string[] DirectionNames =
        {
            "south",
            "southwest",
            "west",
            "northwest",
            "north",
            "northeast",
            "east",
            "southeast"
        };

        [MenuItem("Family Company/Validate Office Seating Facing Lock")]
        public static void Run()
        {
            try
            {
                ValidateCanonicalWorkActionDirections();
                ValidateImmutableFacingLockAndFallback();
                Debug.Log("FAMILY_COMPANY_OFFICE_SEATING_FACING_LOCK_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_SEATING_FACING_LOCK_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateCanonicalWorkActionDirections()
        {
            string[] guids = AssetDatabase.FindAssets("t:OfficeWorkActionFrameSet");
            var validatedMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var frameSet = AssetDatabase.LoadAssetAtPath<OfficeWorkActionFrameSet>(path);
                if (frameSet == null || string.IsNullOrWhiteSpace(frameSet.MemberId)) continue;

                ValidateClip(frameSet, OfficeWorkMicroAction.Typing, path);
                ValidateClip(frameSet, OfficeWorkMicroAction.Mouse, path);
                ValidateClip(frameSet, OfficeWorkMicroAction.Drink, path);
                validatedMembers.Add(frameSet.MemberId);
            }

            string[] requiredMembers = { "player", "older_sister", "father", "mother" };
            for (var index = 0; index < requiredMembers.Length; index++)
            {
                Require(
                    validatedMembers.Contains(requiredMembers[index]),
                    $"Missing canonical work-action frame set for {requiredMembers[index]}.");
            }
        }

        private static void ValidateClip(
            OfficeWorkActionFrameSet frameSet,
            OfficeWorkMicroAction action,
            string assetPath)
        {
            if (!frameSet.TryGetUsableClip(action, out OfficeWorkActionClip clip)) return;
            for (var frame = 0; frame < clip.FramesPerDirection; frame++)
            {
                long elapsedMilliseconds = checked((long)frame * clip.MillisecondsPerFrame);
                for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
                {
                    Sprite sprite = clip.ResolveFrame(direction, elapsedMilliseconds);
                    Require(sprite != null, $"Missing {action} frame {frame}/{direction}: {assetPath}.");
                    Require(
                        OfficeWorkActionFrameSet.TryResolveNamedDirection(sprite, out int namedDirection),
                        $"Direction token is missing from {sprite.name}: {assetPath}.");
                    Require(
                        namedDirection == direction,
                        $"Direction slot mismatch in {assetPath}: {sprite.name} is " +
                        $"{namedDirection}, expected {direction}.");
                }
            }
        }

        private static void ValidateImmutableFacingLockAndFallback()
        {
            using (var fixture = new AnimatorFixture())
            {
                var renderer = fixture.Root.AddComponent<SpriteRenderer>();
                var animator = fixture.Root.AddComponent<DirectionalSpriteAnimator>();
                Sprite[] walk = fixture.CreateDirectionalFrames(
                    DirectionalSpriteAnimator.WalkFrameCount,
                    "walk");
                Sprite[] sit = fixture.CreateDirectionalFrames(
                    OfficeSeatingAnimationFrames.SitDownFrameCount,
                    "sit");
                Sprite[] work = fixture.CreateDirectionalFrames(
                    OfficeSeatingAnimationFrames.WorkFrameCount,
                    "work");
                Sprite[] stand = fixture.CreateDirectionalFrames(
                    OfficeSeatingAnimationFrames.StandUpFrameCount,
                    "stand");
                animator.Configure(renderer, walk);
                animator.ConfigureOfficeSeating(sit, work, stand);

                animator.RestoreStandingFacing(Northwest);
                animator.SetTileDisplacement(Vector2.right);
                Require(
                    !animator.TryLockOfficeSeatingFacingAfterPlantedRotation(Northwest),
                    "A moving actor acquired the strict planted seating lock.");
                animator.StopTileMovementButKeepFacing();
                Require(
                    animator.TryLockOfficeSeatingFacingAfterPlantedRotation(Northwest),
                    "A planted northwest actor could not acquire the seating lock.");
                Require(animator.IsOfficeSeatingFacingLocked, "Seating facing lock was not active.");
                Require(animator.BeginSitDown(Northwest), "SitDown did not start under its lock.");

                animator.RestoreStandingFacing(5);
                animator.AccumulateStandingFacingRequest(5, 0.1f);
                CompleteTransition(animator, animator.SitDownDurationSeconds);
                Require(animator.CurrentDirection == Northwest, "Direction writer escaped during SitDown.");
                Require(animator.OfficeSeatingFacingViolationCount == 2, "Blocked writers were not counted.");
                Require(animator.MaximumOfficeSeatingFacingDelta == 2, "Blocked 90-degree turn was not measured.");

                var maliciousHook = new FixedHook(fixture.CreateSprite("qa_typing_northeast_0"));
                animator.ConfigureOfficeWorkAnimationHook(maliciousHook);
                Require(animator.BeginSeatedWork(), "Work did not start after SitDown.");
                animator.Tick(0.01f);
                Require(animator.CurrentDirection == Northwest, "Direction changed during Work.");
                Require(
                    animator.OfficeWorkSpriteDirectionViolationCount > 0,
                    "Misdirected work sprite was not rejected.");
                Require(
                    animator.MaximumOfficeWorkSpriteDirectionDelta == 2,
                    "Rejected work sprite did not record the 90-degree delta.");
                Require(
                    animator.CurrentSprite == work[Northwest],
                    "Rejected work sprite did not fall back to locked-direction Work frame zero.");
                Require(
                    animator.IsCurrentAppliedSpriteDirectionLocked,
                    "Fallback Work sprite does not match the lock.");

                Require(animator.BeginStandUp(), "StandUp did not start from Work.");
                CompleteTransition(animator, animator.StandUpDurationSeconds);
                Require(
                    animator.FinishOfficeSeatingPoseForLeavingSeat(),
                    "StandUp pose did not hand off to locked LeavingSeat.");
                Require(
                    animator.IsOfficeSeatingFacingLocked && !animator.IsOfficeSeatingPoseActive,
                    "LeavingSeat did not retain the lock after the seating clip ended.");

                animator.SetTileDisplacement(Vector2.right);
                animator.Tick(0.2f);
                Require(animator.CurrentDirection == Northwest, "Motion direction escaped during LeavingSeat.");
                Require(
                    animator.IsCurrentAppliedSpriteDirectionLocked,
                    "LeavingSeat sprite does not match the seat-facing lock.");
                Require(animator.ReleaseOfficeSeatingFacingLock(), "Explicit lock release failed.");
                Require(!animator.IsOfficeSeatingFacingLocked, "Explicit release left the lock active.");
            }
        }

        private static void CompleteTransition(DirectionalSpriteAnimator animator, float durationSeconds)
        {
            for (var tick = 0; tick < 8 && !animator.IsOfficeSeatingTransitionComplete; tick++)
                animator.Tick(durationSeconds + 0.001f);
            Require(animator.IsOfficeSeatingTransitionComplete, "Seating transition did not complete.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FixedHook : IOfficeSeatedWorkAnimationHook
        {
            private readonly Sprite _sprite;

            public FixedHook(Sprite sprite)
            {
                _sprite = sprite;
            }

            public bool TryBegin(int lockedDirection, out IOfficeSeatedWorkAnimationSession session)
            {
                session = new FixedSession(_sprite);
                return true;
            }
        }

        private sealed class FixedSession : IOfficeSeatedWorkAnimationSession
        {
            public FixedSession(Sprite sprite)
            {
                CurrentSprite = sprite;
            }

            public Sprite CurrentSprite { get; }
            public OfficeWorkMicroAction CurrentAction => OfficeWorkMicroAction.Typing;
            public bool IsSafeToStand => true;
            public void Tick(float deltaTime) { }
            public void RequestSafeStop() { }
            public void Dispose() { }
        }

        private sealed class AnimatorFixture : IDisposable
        {
            private readonly Texture2D _texture;
            private readonly List<Sprite> _sprites = new List<Sprite>();

            public AnimatorFixture()
            {
                Root = new GameObject("Office Seating Facing Lock Validation");
                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            }

            public GameObject Root { get; }

            public Sprite[] CreateDirectionalFrames(int frameCount, string clipName)
            {
                var frames = new Sprite[frameCount * DirectionalSpriteAnimator.DirectionCount];
                for (var frame = 0; frame < frameCount; frame++)
                {
                    for (var direction = 0;
                         direction < DirectionalSpriteAnimator.DirectionCount;
                         direction++)
                    {
                        frames[frame * DirectionalSpriteAnimator.DirectionCount + direction] =
                            CreateSprite($"qa_{clipName}_{DirectionNames[direction]}_{frame}");
                    }
                }
                return frames;
            }

            public Sprite CreateSprite(string spriteName)
            {
                var sprite = Sprite.Create(
                    _texture,
                    new Rect(0f, 0f, 2f, 2f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                sprite.name = spriteName;
                _sprites.Add(sprite);
                return sprite;
            }

            public void Dispose()
            {
                for (var index = 0; index < _sprites.Count; index++)
                    UnityEngine.Object.DestroyImmediate(_sprites[index]);
                UnityEngine.Object.DestroyImmediate(_texture);
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}

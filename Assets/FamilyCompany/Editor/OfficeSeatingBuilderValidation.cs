using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatingBuilderValidation
    {
        private static readonly string[] FamilyIds =
            { "player", "older_sister", "father", "mother" };

        private static readonly IReadOnlyDictionary<string, string> ExpectedAssignments =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "desk_a", "older_sister" },
                { "desk_b", "mother" },
                { "desk_c", "father" },
                { "desk_d", "player" }
            };

        [MenuItem("Family Company/Validate Office Seating Builder Wiring")]
        public static void ValidateCurrentScene()
        {
            var framePaths = ValidateFrameAssetsAndIndexes();
            var registry = UnityEngine.Object.FindFirstObjectByType<OfficeSeatRegistry>();
            if (registry == null) throw new InvalidOperationException("OfficeSeatRegistry is missing.");
            registry.Rebuild();
            if (registry.SeatCount != 4 || registry.Definitions.Count != 4)
                throw new InvalidOperationException("Office seating requires exactly four atomic runtime definitions.");

            var authorings = registry.GetComponentsInChildren<OfficeSeatAuthoring>(true)
                .OrderBy(item => item.SeatId, StringComparer.Ordinal)
                .ToArray();
            if (authorings.Length != 4 || authorings.Select(item => item.SeatId).Distinct(StringComparer.Ordinal).Count() != 4)
                throw new InvalidOperationException("Office seating authoring IDs are missing or duplicated.");
            var report = OfficeSeatRegistry.ValidateAuthoringCollection(authorings);
            if (report.HasErrors)
                throw new InvalidOperationException("Office seat authoring validation failed:\n" + report.FormatErrors());
            foreach (var authoring in authorings) ValidateSeatAuthoring(authoring);

            var coordinator = UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>();
            if (coordinator == null) throw new InvalidOperationException("OfficeAutonomyCoordinator is missing.");
            coordinator.InitializeNow();
            if (!coordinator.IsSeatingRuntimeReady || coordinator.SeatingState == null)
                throw new InvalidOperationException("Office seating runtime is not initially ready.");
            if (coordinator.SeatingState.SeatCount != registry.SeatCount)
                throw new InvalidOperationException("Coordinator state and registry definition counts differ.");
            foreach (var assignment in ExpectedAssignments)
            {
                if (!coordinator.SeatingState.TryGetSeat(assignment.Key, out var seat) ||
                    !string.Equals(seat.AssignedMemberId, assignment.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Long-term seat assignment mismatch: {assignment.Key} -> {assignment.Value}.");
                }
            }

            var player = UnityEngine.Object.FindFirstObjectByType<PrototypePlayerController>();
            if (player == null || player.GetComponent<OfficePlayerSeatingPresenter>() == null)
                throw new InvalidOperationException("OfficePlayerSeatingPresenter is missing from the player.");
            var animators = ResolveFamilyAnimators(player);
            foreach (var memberId in FamilyIds)
                ValidateAnimator(memberId, animators[memberId], framePaths);
            if (UnityEngine.Object.FindFirstObjectByType<OfficeVisualV2Presenter>() == null)
                throw new InvalidOperationException("OfficeVisualV2 foreground presenter is missing.");

            Debug.Log("OFFICE_SEATING_BUILDER_VALIDATION: PASS components=4 seats=4 frames=448 hook=fallback");
        }

        private static HashSet<string> ValidateFrameAssetsAndIndexes()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var memberId in FamilyIds)
            {
                foreach (OfficeSeatingAnimationClip clip in Enum.GetValues(typeof(OfficeSeatingAnimationClip)))
                {
                    for (var frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
                    {
                        for (var direction = 0; direction < OfficeSeatingAnimationFrames.DirectionCount; direction++)
                        {
                            var index = OfficeSeatingAnimationFrames.FlattenedIndex(clip, direction, frame);
                            if (index != frame * OfficeSeatingAnimationFrames.DirectionCount + direction)
                                throw new InvalidOperationException("Office seating frame-major index contract changed.");
                            var path = OfficeSeatingAnimationFrames.AssetPath(
                                memberId,
                                (OfficeSeatFacing8)direction,
                                clip,
                                frame);
                            if (!paths.Add(path)) throw new InvalidOperationException("Duplicate seating frame path: " + path);
                            if (!File.Exists(path) || AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                                throw new InvalidDataException("Missing seating Sprite asset: " + path);
                        }
                    }
                }
            }
            if (paths.Count != 448) throw new InvalidOperationException("Office seating requires exactly 448 unique Sprites.");
            return paths;
        }

        private static void ValidateSeatAuthoring(OfficeSeatAuthoring authoring)
        {
            if (!authoring.IsRuntimeValid || authoring.SemanticDestination == null ||
                authoring.SemanticDestination.Activity != OfficeActivity.Work)
            {
                throw new InvalidOperationException("Seat runtime anchors or semantic Work destination are invalid: " + authoring.SeatId);
            }
            if (!ExpectedAssignments.TryGetValue(authoring.SeatId, out var assignedMemberId) ||
                !string.Equals(authoring.LongTermAssignedMemberId, assignedMemberId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Serialized long-term assignment mismatch: " + authoring.SeatId);
            }
            if (authoring.ForegroundOcclusionMode != OfficeSeatForegroundOcclusionMode.BehindForeground)
                throw new InvalidOperationException("Desk seating must render behind the OfficeVisualV2 foreground: " + authoring.SeatId);
            if (authoring.ClickHotspot == null || !authoring.ClickHotspot.isTrigger)
                throw new InvalidOperationException("Seat click hotspot must be a non-blocking trigger: " + authoring.SeatId);
            if (!authoring.TryResolveFacing(out var facing) ||
                !authoring.ValidateExpectedFacing || authoring.ExpectedFacing != facing)
            {
                throw new InvalidOperationException("ComputerLookTarget facing is not stable: " + authoring.SeatId);
            }

            var expected = ExpectedArtAnchors(authoring.SeatId);
            RequirePixelError(authoring.SeatId + " approach", authoring.ApproachAnchor.position, expected.Approach);
            RequirePixelError(authoring.SeatId + " sit", authoring.SitAnchor.position, expected.Sit);
            RequirePixelError(authoring.SeatId + " computer-look", authoring.ComputerLookTarget.position, expected.Look);
        }

        private static Dictionary<string, DirectionalSpriteAnimator> ResolveFamilyAnimators(
            PrototypePlayerController player)
        {
            var result = new Dictionary<string, DirectionalSpriteAnimator>(StringComparer.Ordinal)
            {
                { "player", player.GetComponent<DirectionalSpriteAnimator>() }
            };
            foreach (var agent in UnityEngine.Object.FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None))
            {
                if (!result.TryAdd(agent.AgentId, agent.SpriteAnimator))
                    throw new InvalidOperationException("Duplicate family animator: " + agent.AgentId);
            }
            if (result.Count != 4 || FamilyIds.Any(id => !result.ContainsKey(id) || result[id] == null))
                throw new InvalidOperationException("Four canonical family seating animators are required.");
            return result;
        }

        private static void ValidateAnimator(
            string memberId,
            DirectionalSpriteAnimator animator,
            IReadOnlyCollection<string> framePaths)
        {
            if (!animator.HasOfficeSeatingFrames ||
                animator.ConfiguredOfficeSeatingFrameCount != OfficeSeatingAnimationFrames.RequiredSpriteCount)
            {
                throw new InvalidOperationException(memberId + " does not have 112 configured seating frames.");
            }
            if (!animator.SupportsOfficeWorkAnimationHook || !animator.HasOfficeWorkFallback)
                throw new InvalidOperationException(memberId + " micro-action hook/fallback contract is unavailable.");
            foreach (OfficeSeatingAnimationClip clip in Enum.GetValues(typeof(OfficeSeatingAnimationClip)))
            {
                for (var frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
                {
                    for (var direction = 0; direction < OfficeSeatingAnimationFrames.DirectionCount; direction++)
                    {
                        var path = OfficeSeatingAnimationFrames.AssetPath(
                            memberId,
                            (OfficeSeatFacing8)direction,
                            clip,
                            frame);
                        if (!framePaths.Contains(path) ||
                            animator.GetOfficeSeatingFrame(clip, direction, frame) != AssetDatabase.LoadAssetAtPath<Sprite>(path))
                        {
                            throw new InvalidOperationException("Animator seating frame reference mismatch: " + path);
                        }
                    }
                }
            }
        }

        private static void RequirePixelError(string label, Vector3 world, Vector2 expected)
        {
            var error = Vector2.Distance(OfficeVisualV2Calibration.WorldToArtPixel(world), expected);
            if (error > 1f)
                throw new InvalidOperationException($"{label} footpoint error {error:F3}px exceeds 1px.");
        }

        private static SeatArtAnchors ExpectedArtAnchors(string seatId)
        {
            switch (seatId)
            {
                case "desk_a": return new SeatArtAnchors(
                    OfficeVisualV2Calibration.DeskAApproachArt,
                    OfficeVisualV2Calibration.DeskASitArt,
                    OfficeVisualV2Calibration.DeskAMonitorArt);
                case "desk_b": return new SeatArtAnchors(
                    OfficeVisualV2Calibration.DeskBApproachArt,
                    OfficeVisualV2Calibration.DeskBSitArt,
                    OfficeVisualV2Calibration.DeskBMonitorArt);
                case "desk_c": return new SeatArtAnchors(
                    OfficeVisualV2Calibration.DeskCApproachArt,
                    OfficeVisualV2Calibration.DeskCSitArt,
                    OfficeVisualV2Calibration.DeskCMonitorArt);
                case "desk_d": return new SeatArtAnchors(
                    OfficeVisualV2Calibration.DeskDApproachArt,
                    OfficeVisualV2Calibration.DeskDSitArt,
                    OfficeVisualV2Calibration.DeskDMonitorArt);
                default: throw new InvalidOperationException("Unexpected office seat ID: " + seatId);
            }
        }

        private readonly struct SeatArtAnchors
        {
            public SeatArtAnchors(Vector2 approach, Vector2 sit, Vector2 look)
            {
                Approach = approach;
                Sit = sit;
                Look = look;
            }

            public Vector2 Approach { get; }
            public Vector2 Sit { get; }
            public Vector2 Look { get; }
        }
    }
}

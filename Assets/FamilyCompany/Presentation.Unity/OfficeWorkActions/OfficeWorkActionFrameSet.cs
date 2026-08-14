using System;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeWorkActions
{
    [Serializable]
    public sealed class OfficeWorkActionClip
    {
        [SerializeField] private OfficeWorkMicroAction action;
        [SerializeField, Tooltip("Frame-major layout: each frame stores South, Southwest, West, Northwest, North, Northeast, East, Southeast.")]
        private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(1)] private int millisecondsPerFrame = 110;
        [SerializeField] private bool loop = true;

        public OfficeWorkMicroAction Action => action;
        public int TotalFrameCount => frames?.Length ?? 0;
        public int FramesPerDirection => IsUsable
            ? frames.Length / OfficeWorkMicroActionAvailabilityRules.DirectionCount
            : 0;
        public int MillisecondsPerFrame => millisecondsPerFrame;
        public bool Loop => loop;
        public bool IsUsable =>
            action != OfficeWorkMicroAction.None &&
            OfficeWorkMicroActionAvailabilityRules.IsFrameChannelUsable(
                frames?.Length ?? 0,
                ContainsMissingFrame(frames),
                millisecondsPerFrame);

        public void Configure(
            OfficeWorkMicroAction configuredAction,
            Sprite[] configuredFrames,
            int configuredMillisecondsPerFrame = 110,
            bool configuredLoop = true)
        {
            action = configuredAction;
            frames = configuredFrames == null
                ? Array.Empty<Sprite>()
                : (Sprite[])configuredFrames.Clone();
            millisecondsPerFrame = configuredMillisecondsPerFrame;
            loop = configuredLoop;
        }

        public Sprite ResolveFrame(int direction, long actionElapsedMilliseconds)
        {
            if (!IsUsable)
                throw new InvalidOperationException($"Office work clip is not usable: {action}.");
            if (direction < 0 || direction >= OfficeWorkMicroActionAvailabilityRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (actionElapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(actionElapsedMilliseconds));

            var frameCount = FramesPerDirection;
            var rawFrame = actionElapsedMilliseconds / millisecondsPerFrame;
            var frame = loop
                ? (int)(rawFrame % frameCount)
                : (int)Math.Min(frameCount - 1L, rawFrame);
            return frames[frame * OfficeWorkMicroActionAvailabilityRules.DirectionCount + direction];
        }

        private static bool ContainsMissingFrame(Sprite[] source)
        {
            if (source == null) return true;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] == null) return true;
            }
            return false;
        }
    }

    [CreateAssetMenu(
        fileName = "OfficeWorkActionFrameSet",
        menuName = "Family Company/Office Work Action Frame Set")]
    public sealed class OfficeWorkActionFrameSet : ScriptableObject
    {
        private static readonly string[] DirectionNameTokens =
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

        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private OfficeWorkActionClip[] clips = Array.Empty<OfficeWorkActionClip>();

        public string MemberId => (memberId ?? string.Empty).Trim();
        public OfficeWorkMicroActionAvailability Availability =>
            OfficeWorkMicroActionAvailabilityRules.Resolve(
                HasUsableClip(OfficeWorkMicroAction.Typing),
                HasUsableClip(OfficeWorkMicroAction.Mouse),
                HasUsableClip(OfficeWorkMicroAction.Drink),
                HasUsableClip(OfficeWorkMicroAction.BriefIdle));
        public bool UsesExistingWorkLoopFallback =>
            OfficeWorkMicroActionAvailabilityRules.ShouldUseExistingWorkLoop(Availability);

        public void Configure(string configuredMemberId, OfficeWorkActionClip[] configuredClips)
        {
            memberId = configuredMemberId ?? string.Empty;
            clips = configuredClips == null
                ? Array.Empty<OfficeWorkActionClip>()
                : (OfficeWorkActionClip[])configuredClips.Clone();
        }

        public bool TryGetUsableClip(OfficeWorkMicroAction action, out OfficeWorkActionClip clip)
        {
            var source = clips ?? Array.Empty<OfficeWorkActionClip>();
            for (var index = 0; index < source.Length; index++)
            {
                var candidate = source[index];
                if (candidate == null || candidate.Action != action || !candidate.IsUsable) continue;
                clip = candidate;
                return true;
            }

            clip = null;
            return false;
        }

        /// <summary>
        /// Reads the independently authored direction token from a canonical sprite name.
        /// Slot position alone cannot detect a west/north sprite accidentally assigned to a
        /// northwest channel, so runtime seating QA uses this metadata as a second source.
        /// Unknown legacy names deliberately return false instead of guessing.
        /// </summary>
        public static bool TryResolveNamedDirection(Sprite sprite, out int direction)
        {
            direction = -1;
            if (sprite == null || string.IsNullOrWhiteSpace(sprite.name)) return false;

            var tokens = sprite.name.Split(
                new[] { '_', '-', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                for (var candidate = 0; candidate < DirectionNameTokens.Length; candidate++)
                {
                    if (!string.Equals(
                            tokens[tokenIndex],
                            DirectionNameTokens[candidate],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    direction = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool HasUsableClip(OfficeWorkMicroAction action)
        {
            return TryGetUsableClip(action, out _);
        }
    }
}

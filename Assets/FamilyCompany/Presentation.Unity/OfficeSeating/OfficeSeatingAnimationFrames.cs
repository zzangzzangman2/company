using System;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    public enum OfficeSeatingAnimationClip
    {
        SitDown = 0,
        Work = 1,
        StandUp = 2
    }

    public enum OfficeWorkerSeatingPhase
    {
        None = 0,
        MovingToApproach = 1,
        MovingToSit = 2,
        SittingDown = 3,
        Working = 4,
        FinishingWork = 5,
        StandingUp = 6
    }

    public static class OfficeSeatingAnimationFrames
    {
        public const int DirectionCount = 8;
        public const int SitDownFrameCount = 4;
        public const int WorkFrameCount = 6;
        public const int StandUpFrameCount = 4;
        public const int SitDownSpriteCount = DirectionCount * SitDownFrameCount;
        public const int WorkSpriteCount = DirectionCount * WorkFrameCount;
        public const int StandUpSpriteCount = DirectionCount * StandUpFrameCount;
        public const int RequiredSpriteCount = SitDownSpriteCount + WorkSpriteCount + StandUpSpriteCount;

        public static int FrameCount(OfficeSeatingAnimationClip clip)
        {
            return clip switch
            {
                OfficeSeatingAnimationClip.SitDown => SitDownFrameCount,
                OfficeSeatingAnimationClip.Work => WorkFrameCount,
                OfficeSeatingAnimationClip.StandUp => StandUpFrameCount,
                _ => throw new ArgumentOutOfRangeException(nameof(clip))
            };
        }

        // Layout matches DirectionalSpriteAnimator: frame-major, then the eight directions.
        public static int FlattenedIndex(
            OfficeSeatingAnimationClip clip,
            int direction,
            int frame)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            var frameCount = FrameCount(clip);
            if (frame < 0 || frame >= frameCount)
                throw new ArgumentOutOfRangeException(nameof(frame));
            return checked(frame * DirectionCount + direction);
        }

        public static string AssetPath(
            string memberId,
            OfficeSeatFacing8 direction,
            OfficeSeatingAnimationClip clip,
            int frame)
        {
            var root = RootForMember(memberId, out var prefix);
            var directionName = DirectionName(direction);
            var clipName = clip switch
            {
                OfficeSeatingAnimationClip.SitDown => "sit_down",
                OfficeSeatingAnimationClip.Work => "sit_work",
                OfficeSeatingAnimationClip.StandUp => "stand_up",
                _ => throw new ArgumentOutOfRangeException(nameof(clip))
            };
            if (frame < 0 || frame >= FrameCount(clip))
                throw new ArgumentOutOfRangeException(nameof(frame));
            return $"{root}/{prefix}_{directionName}_{clipName}_{frame}.png";
        }

        private static string RootForMember(string memberId, out string prefix)
        {
            switch ((memberId ?? string.Empty).Trim())
            {
                case "player":
                    prefix = "player";
                    return "Assets/Art/Characters/Player/Pixel/OfficeSeatingV1/Frames";
                case "older_sister":
                    prefix = "older_sister";
                    return "Assets/Art/Characters/Family/OlderSister/Pixel/OfficeSeatingV1/Frames";
                case "father":
                    prefix = "father";
                    return "Assets/Art/Characters/Family/Father/Pixel/OfficeSeatingV1/Frames";
                case "mother":
                    prefix = "mother";
                    return "Assets/Art/Characters/Family/Mother/Pixel/OfficeSeatingV1/Frames";
                default:
                    throw new ArgumentException("OfficeSeatingV1 frames exist only for the four canonical family IDs.", nameof(memberId));
            }
        }

        private static string DirectionName(OfficeSeatFacing8 direction)
        {
            return direction switch
            {
                OfficeSeatFacing8.South => "south",
                OfficeSeatFacing8.Southwest => "southwest",
                OfficeSeatFacing8.West => "west",
                OfficeSeatFacing8.Northwest => "northwest",
                OfficeSeatFacing8.North => "north",
                OfficeSeatFacing8.Northeast => "northeast",
                OfficeSeatFacing8.East => "east",
                OfficeSeatFacing8.Southeast => "southeast",
                _ => throw new ArgumentOutOfRangeException(nameof(direction))
            };
        }
    }
}

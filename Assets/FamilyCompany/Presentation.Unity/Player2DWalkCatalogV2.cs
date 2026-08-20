using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public static class Player2DWalkCatalogV2
    {
        public const int DirectionCount = 8;
        public const int PoseCount = 6;
        public const int FrameCount = DirectionCount * PoseCount;
        public const string ResourceRoot = "FamilyCompany/Player2DWalkV2/Frames/";

        public static readonly string[] DirectionNames =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        public static Sprite[] LoadFrames()
        {
            var frames = new Sprite[FrameCount];
            var index = 0;
            for (var pose = 0; pose < PoseCount; pose++)
            for (var direction = 0; direction < DirectionCount; direction++)
            {
                string resource = ResourceRoot + "player_" + DirectionNames[direction] +
                                  "_walk_" + pose + "_v2";
                Sprite sprite = Resources.Load<Sprite>(resource);
                if (sprite == null)
                    throw new InvalidOperationException("Missing Player 2D walk V2 frame: " + resource);
                frames[index++] = sprite;
            }
            return frames;
        }
    }
}

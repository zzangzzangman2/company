using System;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [Serializable]
    internal sealed class PlayerWalkRigV2BakeContract
    {
        public string contract = string.Empty;
        public string direction = string.Empty;
        public string rigPrefabPath = string.Empty;
        public string animationClipPath = string.Empty;
        public string sourcePsbPath = string.Empty;
        public string outputDirectory = string.Empty;
        public string rootMotionTransform = "Root";
        public string leftFootContactTransform = "FootContact_L";
        public string rightFootContactTransform = "FootContact_R";
        public string pelvisTransform = "pelvis";
        public int canvasWidth = 384;
        public int canvasHeight = 512;
        public float pixelsPerUnit = 324f;
        public float strideWorld = 1.2f;
        public float visualScale = 1.55f;
        public string sourcePsbSha256 = string.Empty;
        public string[] requiredLayers = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class PlayerBakedWalkV2BakeReceipt
    {
        public string contract = "FC-PLAYER-BAKED-WALK-V2-BAKE";
        public string direction = string.Empty;
        public int canvasWidth;
        public int canvasHeight;
        public float pixelsPerUnit;
        public float strideWorld;
        public float visualScale;
        public string validationProfile = "paper-doll-v2";
        public string sourcePsbSha256 = string.Empty;
        public string sourcePsbPath = string.Empty;
        public PlayerBakedWalkV2BakePoseReceipt[] poses =
            Array.Empty<PlayerBakedWalkV2BakePoseReceipt>();
    }

    [Serializable]
    internal sealed class PlayerBakedWalkV2BakePoseReceipt
    {
        public int pose;
        public string supportLeg = string.Empty;
        public string spritePath = string.Empty;
        public Vector2 rootWorld;
        public Vector2 leftFootAnchorPx;
        public Vector2 rightFootAnchorPx;
        public Vector2 pelvisAnchorPx;
        public float leftFootHeightWorld;
        public float rightFootHeightWorld;
    }
}

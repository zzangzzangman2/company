using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public enum PlayerWalkSupportLegV2
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    [Serializable]
    public sealed class PlayerBakedWalkDirectionV2
    {
        [SerializeField] private int directionIndex = -1;
        [SerializeField] private string directionName = string.Empty;
        [SerializeField] private Vector2Int canvasSizePx = new Vector2Int(384, 512);
        [SerializeField] private float pixelsPerUnit = 324f;
        [SerializeField] private Sprite[] sprites = Array.Empty<Sprite>();
        [SerializeField] private PlayerWalkSupportLegV2[] supportLegs =
            Array.Empty<PlayerWalkSupportLegV2>();
        [SerializeField] private Vector2[] leftFootAnchorsPx = Array.Empty<Vector2>();
        [SerializeField] private Vector2[] rightFootAnchorsPx = Array.Empty<Vector2>();
        [SerializeField] private Vector2[] pelvisAnchorsPx = Array.Empty<Vector2>();

        public int DirectionIndex => directionIndex;
        public string DirectionName => directionName ?? string.Empty;
        public Vector2Int CanvasSizePx => canvasSizePx;
        public float PixelsPerUnit => pixelsPerUnit;
        public IReadOnlyList<Sprite> Sprites => sprites;
        public IReadOnlyList<PlayerWalkSupportLegV2> SupportLegs => supportLegs;
        public IReadOnlyList<Vector2> LeftFootAnchorsPx => leftFootAnchorsPx;
        public IReadOnlyList<Vector2> RightFootAnchorsPx => rightFootAnchorsPx;
        public IReadOnlyList<Vector2> PelvisAnchorsPx => pelvisAnchorsPx;

        public Sprite SpriteAt(int pose) => sprites[pose];
        public PlayerWalkSupportLegV2 SupportLegAt(int pose) => supportLegs[pose];
        public Vector2 LeftFootAnchorAt(int pose) => leftFootAnchorsPx[pose];
        public Vector2 RightFootAnchorAt(int pose) => rightFootAnchorsPx[pose];
        public Vector2 PelvisAnchorAt(int pose) => pelvisAnchorsPx[pose];
        public Vector2 SupportFootAnchorAt(int pose) => supportLegs[pose] switch
        {
            PlayerWalkSupportLegV2.Left => leftFootAnchorsPx[pose],
            PlayerWalkSupportLegV2.Right => rightFootAnchorsPx[pose],
            _ => throw new InvalidOperationException(
                $"Player baked walk direction {directionIndex} pose {pose} has no support leg.")
        };

        public void Configure(
            int index,
            string name,
            Vector2Int canvas,
            float ppu,
            Sprite[] poseSprites,
            PlayerWalkSupportLegV2[] poseSupportLegs,
            Vector2[] leftAnchors,
            Vector2[] rightAnchors,
            Vector2[] pelvisAnchors)
        {
            directionIndex = index;
            directionName = name ?? string.Empty;
            canvasSizePx = canvas;
            pixelsPerUnit = ppu;
            sprites = poseSprites == null ? Array.Empty<Sprite>() : (Sprite[])poseSprites.Clone();
            supportLegs = poseSupportLegs == null
                ? Array.Empty<PlayerWalkSupportLegV2>()
                : (PlayerWalkSupportLegV2[])poseSupportLegs.Clone();
            leftFootAnchorsPx = leftAnchors == null ? Array.Empty<Vector2>() : (Vector2[])leftAnchors.Clone();
            rightFootAnchorsPx = rightAnchors == null ? Array.Empty<Vector2>() : (Vector2[])rightAnchors.Clone();
            pelvisAnchorsPx = pelvisAnchors == null ? Array.Empty<Vector2>() : (Vector2[])pelvisAnchors.Clone();
            Validate();
        }

        public void Validate()
        {
            if (directionIndex < 0 || directionIndex >= PlayerBakedWalkCatalogV2.DirectionCount)
                throw new InvalidOperationException($"Invalid baked walk direction index {directionIndex}.");
            if (!string.Equals(directionName, PlayerBakedWalkCatalogV2.DirectionNames[directionIndex],
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Baked walk direction {directionIndex} must be named " +
                    PlayerBakedWalkCatalogV2.DirectionNames[directionIndex] + ".");
            if (canvasSizePx.x <= 0 || canvasSizePx.y <= 0)
                throw new InvalidOperationException($"Baked walk {directionName} has an invalid canvas.");
            if (!IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0f)
                throw new InvalidOperationException($"Baked walk {directionName} has invalid PPU {pixelsPerUnit}.");
            RequirePoseCount(sprites, nameof(sprites));
            RequirePoseCount(supportLegs, nameof(supportLegs));
            RequirePoseCount(leftFootAnchorsPx, nameof(leftFootAnchorsPx));
            RequirePoseCount(rightFootAnchorsPx, nameof(rightFootAnchorsPx));
            RequirePoseCount(pelvisAnchorsPx, nameof(pelvisAnchorsPx));
            for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
            {
                if (sprites[pose] == null)
                    throw new InvalidOperationException($"Baked walk {directionName} pose {pose} Sprite is null.");
                if (sprites[pose].rect.width != canvasSizePx.x || sprites[pose].rect.height != canvasSizePx.y)
                    throw new InvalidOperationException(
                        $"Baked walk {directionName} pose {pose} canvas differs from {canvasSizePx}.");
                if (Mathf.Abs(sprites[pose].pixelsPerUnit - pixelsPerUnit) > 0.001f)
                    throw new InvalidOperationException(
                        $"Baked walk {directionName} pose {pose} PPU differs from {pixelsPerUnit}.");
                PlayerWalkSupportLegV2 expected = pose < 4
                    ? PlayerWalkSupportLegV2.Left
                    : PlayerWalkSupportLegV2.Right;
                if (supportLegs[pose] != expected)
                    throw new InvalidOperationException(
                        $"Baked walk {directionName} pose {pose} support must be {expected}.");
                ValidateAnchor(leftFootAnchorsPx[pose], "leftFoot", pose);
                ValidateAnchor(rightFootAnchorsPx[pose], "rightFoot", pose);
                ValidateAnchor(pelvisAnchorsPx[pose], "pelvis", pose);
            }
        }

        private void ValidateAnchor(Vector2 anchor, string label, int pose)
        {
            if (!IsFinite(anchor.x) || !IsFinite(anchor.y) ||
                anchor.x < 0f || anchor.x > canvasSizePx.x ||
                anchor.y < 0f || anchor.y > canvasSizePx.y)
                throw new InvalidOperationException(
                    $"Baked walk {directionName} pose {pose} {label} anchor {anchor} is outside {canvasSizePx}.");
        }

        private static void RequirePoseCount<T>(T[] values, string label)
        {
            if (values == null || values.Length != PlayerBakedWalkCatalogV2.PoseCount)
                throw new InvalidOperationException(
                    $"Baked walk {label} requires exactly {PlayerBakedWalkCatalogV2.PoseCount} values.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [CreateAssetMenu(menuName = "Family Company/Player/Baked Walk Catalog V2")]
    public sealed class PlayerBakedWalkCatalogV2 : ScriptableObject
    {
        public const int CurrentVersion = 2;
        public const int DirectionCount = 8;
        public const int PoseCount = 8;
        public const string ResourcePath =
            "FamilyCompany/PlayerBakedWalkV2/PlayerBakedWalkCatalogV2";
        public static readonly string[] DirectionNames =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private float strideWorld = OfficeLocomotionGaitRules.DefaultStrideLength;
        [SerializeField] private string sourceReceiptSha256 = string.Empty;
        [SerializeField] private PlayerBakedWalkDirectionV2[] directions =
            Array.Empty<PlayerBakedWalkDirectionV2>();

        public int Version => version;
        public float StrideWorld => strideWorld;
        public string SourceReceiptSha256 => sourceReceiptSha256 ?? string.Empty;
        public IReadOnlyList<PlayerBakedWalkDirectionV2> Directions => directions;

        public static PlayerBakedWalkCatalogV2 LoadDefault() =>
            Resources.Load<PlayerBakedWalkCatalogV2>(ResourcePath);

        public PlayerBakedWalkDirectionV2 DirectionAt(int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            foreach (PlayerBakedWalkDirectionV2 row in directions)
                if (row != null && row.DirectionIndex == direction)
                    return row;
            throw new KeyNotFoundException($"Baked player walk direction {direction} is missing.");
        }

        public void Configure(
            PlayerBakedWalkDirectionV2[] rows,
            string receiptSha256,
            float authoredStrideWorld)
        {
            directions = rows == null
                ? Array.Empty<PlayerBakedWalkDirectionV2>()
                : (PlayerBakedWalkDirectionV2[])rows.Clone();
            sourceReceiptSha256 = receiptSha256 ?? string.Empty;
            strideWorld = authoredStrideWorld;
            version = CurrentVersion;
            Validate();
        }

        public void Validate()
        {
            if (version != CurrentVersion)
                throw new InvalidOperationException($"Player baked walk catalog version {version} is unsupported.");
            if (Mathf.Abs(strideWorld - OfficeLocomotionGaitRules.DefaultStrideLength) > 0.000001f)
                throw new InvalidOperationException(
                    $"Player baked walk stride {strideWorld:F8} differs from runtime stride " +
                    OfficeLocomotionGaitRules.DefaultStrideLength.ToString("F8") + ".");
            if (!IsSha256(sourceReceiptSha256))
                throw new InvalidOperationException("Player baked walk catalog has no valid source receipt SHA-256.");
            if (directions == null || directions.Length != DirectionCount)
                throw new InvalidOperationException($"Player baked walk requires exactly {DirectionCount} rows.");
            var seen = new HashSet<int>();
            foreach (PlayerBakedWalkDirectionV2 row in directions)
            {
                if (row == null) throw new InvalidOperationException("Player baked walk contains a null row.");
                row.Validate();
                if (!seen.Add(row.DirectionIndex))
                    throw new InvalidOperationException(
                        $"Player baked walk direction {row.DirectionIndex} is duplicated.");
            }
            if (seen.Count != DirectionCount)
                throw new InvalidOperationException("Player baked walk does not contain all eight directions.");
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (char current in value)
            {
                bool hexadecimal = current >= '0' && current <= '9' ||
                                   current >= 'a' && current <= 'f' ||
                                   current >= 'A' && current <= 'F';
                if (!hexadecimal) return false;
            }
            return true;
        }
    }
}

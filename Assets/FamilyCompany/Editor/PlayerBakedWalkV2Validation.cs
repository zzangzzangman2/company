using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PlayerBakedWalkV2Validation
    {
        public const string PaperDollValidationProfile = "paper-doll-v2";
        public const string HumanoidValidationProfile = "humanoid-v1";
        public const float MaximumProjectedSupportDriftPx = 1.0f;
        public const float MaximumSupportDrift2dPx = 1.5f;
        public const float MaximumContactStepErrorPx = 1.0f;
        public const float MinimumPassingFootLiftPx = 3.0f;
        public const float MaximumVisibleHeightDeltaPercent = 1.0f;
        public const float MaximumHumanoidVisibleHeightDeltaPercent = 18.0f;
        public const float MaximumHumanoidDirectionMedianHeightDeltaPercent = 5.0f;
        public const int MaximumHumanoidMaterialComponents = 12;
        private const string SouthReceiptPath =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkV2/source-receipt-south.json";

        private static readonly Vector2[] DirectionVectors =
        {
            Vector2.down,
            new Vector2(-1f, -1f).normalized,
            Vector2.left,
            new Vector2(-1f, 1f).normalized,
            Vector2.up,
            new Vector2(1f, 1f).normalized,
            Vector2.right,
            new Vector2(1f, -1f).normalized
        };

        [MenuItem("Family Company/QA/Validate Player Baked Walk V2")]
        public static void Run()
        {
            PlayerBakedWalkCatalogV2 catalog = PlayerBakedWalkCatalogV2.LoadDefault();
            if (catalog == null)
                throw new InvalidOperationException(
                    "PlayerBakedWalkCatalogV2 is missing. A partial south candidate cannot be promoted as production.");
            ValidateCatalog(catalog);
            Debug.Log("PLAYER_BAKED_WALK_V2_STATIC_QA: PASS | directions=8 poses=64");
        }

        [MenuItem("Family Company/QA/Validate Player South Baked Candidate V2")]
        public static void RunSouthCandidate()
        {
            ValidateRuntimeModeFailSafe();
            PlayerBakedWalkV2BakeReceipt receipt = LoadReceipt(SouthReceiptPath);
            ValidateReceiptAndPngs(receipt, 0);
            Debug.Log("PLAYER_BAKED_WALK_V2_SOUTH_STATIC_QA: PASS | direction=south poses=8");
        }

        private static void ValidateRuntimeModeFailSafe()
        {
            if (PlayerWalkPresentationModeResolver.Resolve(Array.Empty<string>()) !=
                PlayerWalkPresentationMode.Legacy48)
                throw new InvalidOperationException(
                    "Player walk runtime must default to the approved Legacy48 catalog.");
            if (PlayerWalkPresentationModeResolver.Resolve(
                    new[] { PlayerWalkPresentationModeResolver.NaturalV1Flag }) !=
                PlayerWalkPresentationMode.NaturalV1)
                throw new InvalidOperationException("NaturalV1 must require its explicit opt-in flag.");
            if (PlayerWalkPresentationModeResolver.Resolve(
                    new[] { PlayerWalkPresentationModeResolver.BakedV2Flag }) !=
                PlayerWalkPresentationMode.BakedV2)
                throw new InvalidOperationException("BakedV2 must require its explicit opt-in flag.");
            try
            {
                PlayerWalkPresentationModeResolver.Resolve(new[]
                {
                    PlayerWalkPresentationModeResolver.NaturalV1Flag,
                    PlayerWalkPresentationModeResolver.BakedV2Flag
                });
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException(
                "Player walk runtime must reject mutually exclusive override flags.");
        }

        public static void ValidateCatalog(PlayerBakedWalkCatalogV2 catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            for (var direction = 0; direction < PlayerBakedWalkCatalogV2.DirectionCount; direction++)
                ValidateRow(catalog.DirectionAt(direction), catalog.StrideWorld, direction);
        }

        internal static void ValidateReceiptAndPngs(PlayerBakedWalkV2BakeReceipt receipt, int direction)
        {
            ValidateReceiptAndPngs(
                receipt,
                direction,
                MaximumVisibleHeightDeltaPercent,
                1,
                true);
        }

        internal static void ValidateHumanoidReceiptAndPngs(
            PlayerBakedWalkV2BakeReceipt receipt,
            int direction)
        {
            ValidateReceiptAndPngs(
                receipt,
                direction,
                MaximumHumanoidVisibleHeightDeltaPercent,
                MaximumHumanoidMaterialComponents,
                false);
        }

        internal static void ValidateReceiptUsingDeclaredProfile(
            PlayerBakedWalkV2BakeReceipt receipt,
            int direction)
        {
            string profile = string.IsNullOrWhiteSpace(receipt?.validationProfile)
                ? PaperDollValidationProfile
                : receipt.validationProfile;
            if (string.Equals(profile, HumanoidValidationProfile, StringComparison.Ordinal))
            {
                ValidateHumanoidReceiptAndPngs(receipt, direction);
                return;
            }
            if (string.Equals(profile, PaperDollValidationProfile, StringComparison.Ordinal))
            {
                ValidateReceiptAndPngs(receipt, direction);
                return;
            }
            throw new InvalidOperationException(
                "Player baked walk receipt has an unknown validation profile: " + profile);
        }

        private static void ValidateReceiptAndPngs(
            PlayerBakedWalkV2BakeReceipt receipt,
            int direction,
            float maximumVisibleHeightDeltaPercent,
            int maximumMaterialComponents,
            bool allowBottomEdgeContact)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            if (!string.Equals(receipt.contract, "FC-PLAYER-BAKED-WALK-V2-BAKE", StringComparison.Ordinal))
                throw new InvalidOperationException("Player baked walk receipt contract is invalid.");
            if (direction < 0 || direction >= PlayerBakedWalkCatalogV2.DirectionCount ||
                !string.Equals(receipt.direction, PlayerBakedWalkCatalogV2.DirectionNames[direction],
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Player baked walk receipt direction is invalid.");
            if (receipt.poses == null || receipt.poses.Length != PlayerBakedWalkCatalogV2.PoseCount)
                throw new InvalidOperationException("Player baked walk receipt requires exactly eight poses.");
            if (receipt.canvasWidth <= 0 || receipt.canvasHeight <= 0 || receipt.pixelsPerUnit <= 0f)
                throw new InvalidOperationException("Player baked walk receipt canvas/PPU is invalid.");
            if (Mathf.Abs(receipt.strideWorld - OfficeLocomotionGaitRules.DefaultStrideLength) > 0.000001f)
                throw new InvalidOperationException("Player baked walk receipt stride differs from runtime.");
            if (Mathf.Abs(receipt.visualScale - OfficeGridCharacterMover.UniformVisualScale) > 0.000001f)
                throw new InvalidOperationException("Player baked walk receipt visual scale differs from runtime.");

            var heights = new List<int>();
            foreach (PlayerBakedWalkV2BakePoseReceipt pose in receipt.poses.OrderBy(value => value.pose))
            {
                if (pose.pose < 0 || pose.pose >= PlayerBakedWalkCatalogV2.PoseCount)
                    throw new InvalidOperationException("Player baked walk receipt contains an invalid pose index.");
                string expectedLeg = pose.pose < 4 ? "left" : "right";
                if (!string.Equals(pose.supportLeg, expectedLeg, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Player baked walk pose {pose.pose} support must be {expectedLeg}.");
                string fullPath = Path.GetFullPath(pose.spritePath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Player baked walk PNG is missing.", fullPath);
                byte[] png = File.ReadAllBytes(fullPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, png, false) ||
                        texture.width != receipt.canvasWidth || texture.height != receipt.canvasHeight)
                        throw new InvalidOperationException(
                            $"Player baked walk pose {pose.pose} does not use the fixed canvas.");
                    Color32[] pixels = texture.GetPixels32();
                    ValidateHardAlphaAndBounds(
                        pixels,
                        texture.width,
                        texture.height,
                        pose.pose,
                        pose.pelvisAnchorPx,
                        maximumMaterialComponents,
                        allowBottomEdgeContact,
                        out int height);
                    heights.Add(height);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            ValidateVisibleHeight(heights, maximumVisibleHeightDeltaPercent);
            ValidateReceiptFootLock(
                receipt,
                direction,
                useAuthoredWorldFootHeights: !allowBottomEdgeContact,
                validatePassingLift: true);
        }

        internal static float ValidateHumanoidDirectionSet(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new ArgumentException("Humanoid output root is missing.", nameof(outputRoot));

            string root = outputRoot.TrimEnd('/', '\\');
            var rowHashes = new HashSet<string>(StringComparer.Ordinal);
            var poseDirectionHashes = Enumerable.Range(0, PlayerBakedWalkCatalogV2.PoseCount)
                .Select(_ => new HashSet<string>(StringComparer.Ordinal))
                .ToArray();
            var directionMedianHeights = new List<int>(PlayerBakedWalkCatalogV2.DirectionCount);

            for (var direction = 0; direction < PlayerBakedWalkCatalogV2.DirectionCount; direction++)
            {
                string directionName = PlayerBakedWalkCatalogV2.DirectionNames[direction];
                var frameHashes = new string[PlayerBakedWalkCatalogV2.PoseCount];
                var rowHeights = new List<int>(PlayerBakedWalkCatalogV2.PoseCount);
                for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
                {
                    string path = Path.Combine(
                        root,
                        "Frames",
                        directionName,
                        $"player_{directionName}_walk_{pose}_v2.png");
                    if (!File.Exists(path))
                        throw new FileNotFoundException(
                            "Humanoid direction-set frame is missing.",
                            path);
                    byte[] png = File.ReadAllBytes(path);
                    frameHashes[pose] = Sha256(png);
                    rowHeights.Add(MeasureVisibleAlphaHeight(png, directionName, pose));
                    poseDirectionHashes[pose].Add(frameHashes[pose]);
                }

                int distinctFrames = frameHashes.Distinct(StringComparer.Ordinal).Count();
                if (distinctFrames < 6)
                    throw new InvalidOperationException(
                        $"Humanoid walk {directionName} has only {distinctFrames}/8 distinct poses.");
                rowHashes.Add(Sha256(Encoding.UTF8.GetBytes(string.Join("|", frameHashes))));
                rowHeights.Sort();
                directionMedianHeights.Add((rowHeights[3] + rowHeights[4]) / 2);
            }

            if (rowHashes.Count != PlayerBakedWalkCatalogV2.DirectionCount)
                throw new InvalidOperationException(
                    $"Humanoid walk has only {rowHashes.Count}/8 distinct direction rows.");
            for (var pose = 0; pose < poseDirectionHashes.Length; pose++)
            {
                if (poseDirectionHashes[pose].Count < 4)
                    throw new InvalidOperationException(
                        $"Humanoid walk pose {pose} has only " +
                        $"{poseDirectionHashes[pose].Count}/8 distinct direction renders.");
            }

            int minimumMedianHeight = directionMedianHeights.Min();
            int maximumMedianHeight = directionMedianHeights.Max();
            float medianHeightDeltaPercent = minimumMedianHeight <= 0
                ? float.PositiveInfinity
                : (maximumMedianHeight - minimumMedianHeight) * 100f / minimumMedianHeight;
            if (medianHeightDeltaPercent > MaximumHumanoidDirectionMedianHeightDeltaPercent)
                throw new InvalidOperationException(
                    $"Humanoid walk direction median-height delta {medianHeightDeltaPercent:F3}% exceeds " +
                    $"{MaximumHumanoidDirectionMedianHeightDeltaPercent:F3}%.");

            Debug.Log(
                "PLAYER_WALK_HUMANOID_DIRECTION_SET: PASS | rows=8 poses=64 " +
                $"directionMedianHeightDelta={medianHeightDeltaPercent:F3}%");
            return medianHeightDeltaPercent;
        }

        private static void ValidateRow(
            PlayerBakedWalkDirectionV2 row,
            float strideWorld,
            int direction)
        {
            row.Validate();
            var receipt = new PlayerBakedWalkV2BakeReceipt
            {
                direction = row.DirectionName,
                canvasWidth = row.CanvasSizePx.x,
                canvasHeight = row.CanvasSizePx.y,
                pixelsPerUnit = row.PixelsPerUnit,
                strideWorld = strideWorld,
                visualScale = OfficeGridCharacterMover.UniformVisualScale,
                poses = Enumerable.Range(0, PlayerBakedWalkCatalogV2.PoseCount)
                    .Select(pose => new PlayerBakedWalkV2BakePoseReceipt
                    {
                        pose = pose,
                        supportLeg = pose < 4 ? "left" : "right",
                        leftFootAnchorPx = row.LeftFootAnchorAt(pose),
                        rightFootAnchorPx = row.RightFootAnchorAt(pose),
                        pelvisAnchorPx = row.PelvisAnchorAt(pose)
                    }).ToArray()
            };
            // Catalog rows intentionally contain only runtime data. Their source receipt already
            // passed the profile-specific passing-foot check before catalog promotion.
            ValidateReceiptFootLock(
                receipt,
                direction,
                useAuthoredWorldFootHeights: false,
                validatePassingLift: false);
        }

        private static void ValidateReceiptFootLock(
            PlayerBakedWalkV2BakeReceipt receipt,
            int direction,
            bool useAuthoredWorldFootHeights,
            bool validatePassingLift)
        {
            PlayerBakedWalkV2BakePoseReceipt[] poses = receipt.poses
                .OrderBy(value => value.pose).ToArray();
            Vector2 heading = DirectionVectors[direction];
            float rootStepPx = receipt.strideWorld / PlayerBakedWalkCatalogV2.PoseCount *
                               receipt.pixelsPerUnit / receipt.visualScale;
            var supportWorldPx = new Vector2[PlayerBakedWalkCatalogV2.PoseCount];
            for (var pose = 0; pose < supportWorldPx.Length; pose++)
            {
                Vector2 local = pose < 4 ? poses[pose].leftFootAnchorPx : poses[pose].rightFootAnchorPx;
                supportWorldPx[pose] = local + heading * (rootStepPx * pose);
            }
            ValidateSupportGroup(supportWorldPx, heading, 0, 3, receipt.direction);
            ValidateSupportGroup(supportWorldPx, heading, 4, 7, receipt.direction);

            float contactStepPx = Vector2.Dot(supportWorldPx[4] - supportWorldPx[0], heading);
            float expectedContactStepPx = receipt.strideWorld * 0.5f *
                                          receipt.pixelsPerUnit / receipt.visualScale;
            if (Mathf.Abs(contactStepPx - expectedContactStepPx) > MaximumContactStepErrorPx)
                throw new InvalidOperationException(
                    $"Player baked walk {receipt.direction} contact step error=" +
                    $"{Mathf.Abs(contactStepPx - expectedContactStepPx):F3}px.");

            if (!validatePassingLift) return;
            float rightPassingLift = useAuthoredWorldFootHeights
                ? (poses[2].rightFootHeightWorld - poses[2].leftFootHeightWorld) * receipt.pixelsPerUnit
                : poses[2].rightFootAnchorPx.y - poses[2].leftFootAnchorPx.y;
            float leftPassingLift = useAuthoredWorldFootHeights
                ? (poses[6].leftFootHeightWorld - poses[6].rightFootHeightWorld) * receipt.pixelsPerUnit
                : poses[6].leftFootAnchorPx.y - poses[6].rightFootAnchorPx.y;
            if (rightPassingLift < MinimumPassingFootLiftPx || leftPassingLift < MinimumPassingFootLiftPx)
                throw new InvalidOperationException(
                    $"Player baked walk {receipt.direction} passing lift is too low: " +
                    $"right={rightPassingLift:F3}px left={leftPassingLift:F3}px.");
        }

        private static void ValidateSupportGroup(
            Vector2[] world,
            Vector2 heading,
            int start,
            int end,
            string direction)
        {
            Vector2 origin = world[start];
            for (int pose = start + 1; pose <= end; pose++)
            {
                Vector2 delta = world[pose] - origin;
                float projected = Mathf.Abs(Vector2.Dot(delta, heading));
                float distance = delta.magnitude;
                if (projected > MaximumProjectedSupportDriftPx || distance > MaximumSupportDrift2dPx)
                    throw new InvalidOperationException(
                        $"Player baked walk {direction} support drift {start}->{pose}=" +
                        $"{projected:F3}px projected/{distance:F3}px 2D.");
            }
        }

        private static void ValidateHardAlphaAndBounds(
            Color32[] pixels,
            int width,
            int height,
            int pose,
            Vector2 pelvisAnchorPx,
            int maximumMaterialComponents,
            bool allowBottomEdgeContact,
            out int visibleHeight)
        {
            int minY = height;
            int maxY = -1;
            int componentSeeds = 0;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                byte alpha = pixels[y * width + x].a;
                if (alpha != 0 && alpha != 255)
                    throw new InvalidOperationException(
                        $"Player baked walk pose {pose} contains non-hard alpha {alpha}.");
                if (alpha == 0) continue;
                if (x == 0 || x == width - 1 || y == height - 1 ||
                    (y == 0 && !allowBottomEdgeContact))
                    throw new InvalidOperationException(
                        $"Player baked walk pose {pose} is clipped at the fixed canvas edge.");
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                componentSeeds++;
            }
            if (componentSeeds == 0)
                throw new InvalidOperationException($"Player baked walk pose {pose} is empty.");
            if (!HasOpaqueNear(pixels, width, height, pelvisAnchorPx, 4))
                throw new InvalidOperationException(
                    $"Player baked walk pose {pose} has a transparent waist at pelvis {pelvisAnchorPx}.");
            // Point-rotated paper-doll sprites can leave a few isolated outline pixels at a
            // joint. Treat only a component large enough to be a material body part as detached.
            int detachedComponents = CountMaterialAlphaComponents(pixels, width, height, 16);
            if (detachedComponents < 1 || detachedComponents > maximumMaterialComponents)
                throw new InvalidOperationException(
                    $"Player baked walk pose {pose} has {detachedComponents} material alpha components; " +
                    $"maximum is {maximumMaterialComponents}.");
            // The lower silhouette must shorten at the two legal passing poses; measuring the
            // full alpha bbox would incorrectly report that intentional knee lift as scale loss.
            // Pelvis-to-crown is the invariant authored height and catches actual scale/neck pops.
            visibleHeight = maxY - Mathf.RoundToInt(pelvisAnchorPx.y) + 1;
        }

        private static bool HasOpaqueNear(
            Color32[] pixels,
            int width,
            int height,
            Vector2 point,
            int radius)
        {
            int centerX = Mathf.RoundToInt(point.x);
            int centerY = Mathf.RoundToInt(point.y);
            for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(height - 1, centerY + radius); y++)
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(width - 1, centerX + radius); x++)
                if (pixels[y * width + x].a == 255)
                    return true;
            return false;
        }

        private static int CountMaterialAlphaComponents(
            Color32[] pixels,
            int width,
            int height,
            int minimumPixels)
        {
            var visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            int components = 0;
            for (var start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a == 0) continue;
                visited[start] = true;
                queue.Enqueue(start);
                int size = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    size++;
                    int x = index % width;
                    int y = index / width;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }
                if (size >= minimumPixels) components++;

                void Visit(int x, int y)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height) return;
                    int candidate = y * width + x;
                    if (visited[candidate] || pixels[candidate].a == 0) return;
                    visited[candidate] = true;
                    queue.Enqueue(candidate);
                }
            }
            return components;
        }

        private static void ValidateVisibleHeight(
            IReadOnlyList<int> heights,
            float maximumDeltaPercent)
        {
            int minimum = heights.Min();
            int maximum = heights.Max();
            float deltaPercent = minimum <= 0 ? float.PositiveInfinity :
                (maximum - minimum) * 100f / minimum;
            if (deltaPercent > maximumDeltaPercent)
                throw new InvalidOperationException(
                    $"Player baked walk visible-height delta {deltaPercent:F3}% exceeds " +
                    $"{maximumDeltaPercent:F3}%.");
        }

        private static string Sha256(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static int MeasureVisibleAlphaHeight(byte[] png, string direction, int pose)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, png, false))
                    throw new InvalidOperationException(
                        $"Humanoid walk {direction} pose {pose} PNG could not be decoded.");
                int minimumY = texture.height;
                int maximumY = -1;
                Color32[] pixels = texture.GetPixels32();
                for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a == 0) continue;
                    minimumY = Mathf.Min(minimumY, y);
                    maximumY = Mathf.Max(maximumY, y);
                }
                if (maximumY < minimumY)
                    throw new InvalidOperationException(
                        $"Humanoid walk {direction} pose {pose} PNG is empty.");
                return maximumY - minimumY + 1;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static PlayerBakedWalkV2BakeReceipt LoadReceipt(string assetPath)
        {
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("Player baked walk receipt is missing.", assetPath);
            PlayerBakedWalkV2BakeReceipt receipt = JsonUtility.FromJson<PlayerBakedWalkV2BakeReceipt>(
                File.ReadAllText(assetPath));
            if (receipt == null)
                throw new InvalidOperationException("Player baked walk receipt JSON is invalid.");
            return receipt;
        }
    }
}
